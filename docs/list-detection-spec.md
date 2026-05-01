# List detection — Phase 1 behavior specification

Status: draft for review (2026-05-01)
Scope: Phase 1 only. Subsequent phases (Roman / alphabetic / Korean / circled-number / bullet-glyph labels, cross-page list joining, post-paragraph list rescue) are explicitly out of scope here and will be specified separately.

This document specifies the intent and observable behavior of Phase 1 list detection in PdfStruct. It is the single source of truth for the implementation. Implementers must work from this document; references to upstream Java source code during coding are forbidden by the project's clean-room procedure (see ADR 0001).

---

## 1. Goal

When a PDF page contains a run of text lines that each begin with an Arabic-numeric label following an incrementing sequence, recognise that run as a single list and emit each member as a list item. Detection must run **before** the line-to-paragraph merge stage, so that the labels are not silently absorbed into a generic body paragraph.

A "run" must contain **at least two** items to qualify as a list. Single-line label hits remain body text.

## 2. Inputs

The detector receives, for one page at a time, a sequence of text lines in document extraction order. Each text line carries:

- the text content as a string;
- a 2D bounding box in PDF user space (the four edges: left, right, top, bottom);
- the predominant font size in points;
- the baseline y-coordinate;
- a flag indicating that the line was filtered out as hidden (these are skipped without further consideration).

The detector does not see word-level or glyph-level information in Phase 1. If a future phase needs glyph-level x-coordinates (for example, to compute the precise body-text indent of a wrapped item), that input is added then; Phase 1 derives indent from the line's left edge.

## 3. Output

For each detected list the detector produces an aggregate that records:

- the numbering style (in Phase 1 always *arabic numeric*);
- the common prefix and common suffix that wrap each label (each may be empty);
- the ordered list of items belonging to the list.

For each item the detector records:

- the integer label number;
- the text content with the label and its trailing whitespace stripped from the front;
- a bounding box that initially equals the start line's bounding box and may grow during continuation absorption (§ 7);
- the line index within the page where the item began.

Lines that the detector claims — both label-bearing start lines and continuation lines absorbed into an item — must be flagged so that the downstream paragraph-merge stage skips them. Phase 1 introduces a per-line "claimed by list" flag for this purpose.

## 4. Label parsing

A line is a *label-bearing line* if its leading text matches one of three shapes. Whitespace between the label and the following body text is consumed as part of the label suffix; the label proper ends where ordinary body text begins.

| Shape | Examples | Prefix | Numbered part | Suffix |
| --- | --- | --- | --- | --- |
| trailing-punctuation | `1. `, `12) `, `3: ` | empty | the digit run | the trailing non-digit punctuation glyph plus whitespace |
| paren-wrapped | `(1) `, `(12) ` | `(` | the digit run | `)` plus whitespace |
| bracket-wrapped | `[1] `, `[12] ` | `[` | the digit run | `]` plus whitespace |

If a digit run is immediately followed by a literal decimal point and another digit (for example `1.5`), the line is **not** a label-bearing line. This rejects section numbers ("1.5 Discussion") and floating-point values that masquerade as labels.

The numbered part is parsed as a non-negative integer. Leading zeros are tolerated but not normalised away in the label suffix comparison.

## 5. Run grouping

The detector walks the page's text lines once, in extraction order, maintaining a working set of *open candidates*. An open candidate is the in-progress record of a labelled run that may or may not turn out to be a list. Each open candidate remembers the label of its most recently appended item, the left x-coordinate of the line that contributed that item, and the font size.

For each new line the detector first parses (or fails to parse) a label per § 4.

* If parsing fails, the new line registers a fresh open candidate that holds only this line and no label. Such candidates can never become confirmed lists, but their presence preserves index-order context for continuation absorption.
* If parsing succeeds, the detector walks the existing open candidates from most recently created to oldest, up to a bounded lookback (a few hundred candidates is sufficient for the longest realistic single-page list). For each open candidate, the detector decides whether the new line is the *immediate next* item of that candidate's run. If exactly one candidate matches, the new line is appended to it. Otherwise the new line registers as a fresh open candidate.

Two label-bearing lines belong to the same run iff **all** of the following hold:

1. The candidate's last item also has a parsed label (i.e. the run is currently "live"); a candidate whose last entry was a non-label line cannot be extended.
2. The prefix strings are identical.
3. The first character of the suffix is identical (so `1.` and `2.` belong together; `1.` and `2)` do not).
4. The numbered part is exactly one greater than the candidate's last numbered part.
5. The two lines are *aligned*: their left x-coordinates are within a small alignment tolerance (about a third of a font size in points), or at least within four times that tolerance. Once a candidate has accumulated more than one item that all shared the same left x within tolerance, a new line that fails the strict same-left check is rejected.

Condition 4 enforces strict sequentiality. A run that skips a number (`1.`, `2.`, `4.`) does not merge across the gap; a fresh candidate starts at `4.`.

After all lines on the page are visited, every open candidate that holds two or more items is a confirmed list, with one exception described next.

## 6. Sanity filter

After confirmation, every list goes through one rejection pass: if **every** label in the list matches the regular expression `^\d+\.\d+$` (decimal numbers like `1.5`, `2.10`, `3.0`), the list is rejected. Such runs come from section numbering inside body paragraphs ("1.5 Discussion", "1.6 Conclusion"), not from real lists. Phase 1 has no other post-confirmation filter.

## 7. Continuation absorption

A list item often spans more than the start line; wrapped lines of body text continue the item until the next item begins. After confirming a list, the detector revisits the page lines lying between consecutive item start lines and decides which intermediate lines belong to the previous item's body.

For each intermediate line, in document order, attempting to extend item *k*:

1. Reject if the line has been claimed by *any* list (including this one) — the same line cannot belong to two lists.
2. Reject if its left x-coordinate is more than one alignment tolerance to the **left** of item *k*'s start line. Continuation lines may be at the same left edge (typical for justified body text) or further to the right (typical for hanging-indent layouts), but never to the left.
3. Reject if the vertical distance between this line's baseline and the previous absorbed line's baseline is greater than about 1.2 times the typical inter-line spacing of the run. A large vertical gap severs the item.
4. Reject if the line is itself a label-bearing line under § 4. We must not silently swallow a sibling whose parsing happened to fail earlier or whose label happens to be a different family.

Lines that pass all four checks are appended to item *k*'s body, the item's bounding box is extended to cover them, and they are flagged as claimed.

For the last item of a confirmed list the absorption walk runs from the start line until either the end of the page or the first rejected line.

## 8. Structural invariants of detector output

Two invariants must hold of the elements the detector produces. They are stated forward-compatibly so that later phases (which can recover more aggressively) inherit the same shape; Phase 1 enforces them by the conservative reconciliation in § 9.

**Invariant 1 — Sibling list bounding boxes are pairwise disjoint.**
Within any one parent container (in Phase 1 the parent is always the page), no two confirmed list elements may have bounding boxes that geometrically overlap. Containment is *not* a violation: a list nested inside the body of another list's item is a parent–child relationship, not a sibling, and therefore exempt. Phase 1 emits no nested lists, so the practical rule is: at the page level, any pair of confirmed lists must have disjoint bounding boxes.

**Invariant 2 — No paragraph shares a list's bounding-box interior.**
A paragraph element may not be wholly or substantially contained inside a list element's bounding box. If a paragraph block falls geometrically within the area a list claims, it indicates one of two things: either the list should have absorbed that paragraph as a child (a recovery the rescue stage in a later phase will perform), or the list itself was detected in error. Either way the situation is not a valid output.

**Why these invariants matter.** A list element's bounding box is the visual claim it makes on a region of the page. Allowing a paragraph or another list to sit inside that region produces overlapping boxes in any visual rendering and ambiguous parent–child relationships in any consuming tool. The invariants prevent both.

## 9. Conservative reconciliation for Phase 1

Phase 1 does not yet have the rescue mechanisms that a later phase will provide (re-detecting missed labels at the paragraph stage, absorbing intervening content into list-item child containers, joining adjacent runs across natural break points). Without those mechanisms, the only safe response to an invariant violation is to **un-confirm** the list and let its lines fall back into the paragraph stream. The principle is "false negative is preferable to false positive": a list that cannot cleanly own its territory is not emitted at all.

Concretely, after detection returns its confirmed lists and before the paragraph merger runs over the residual lines:

1. Form provisional paragraph blocks from the residual line stream using the same merging rules the pipeline uses downstream.
2. For every pair of confirmed lists, test their bounding boxes for geometric overlap. If they overlap, mark **both** lists for rejection. (No tiebreaker by item count or any other length signal — the user's earlier review correctly rejected length-based heuristics; presence of the conflict is itself the rejection signal.)
3. For every confirmed list that has not already been rejected, test whether any provisional paragraph block's bounding box is wholly or substantially contained within the list's bounding box. If so, mark the list for rejection.
4. For every list marked for rejection: remove all of its items' claimed line indices from the claim set. The lines return to the residual stream in their original document order.
5. After all rejections are processed, the residual stream is final. The paragraph merger runs over it normally; the surviving lists pass through to placement.

Rejection is deliberately final within Phase 1. The rejected list's lines do not get re-considered as part of a smaller candidate; they simply re-enter the paragraph stream as if detection had never matched them.

This policy is intentionally lossy. It accepts that some real lists will be missed (because a single mis-detected sibling drags the whole list down with it) in exchange for the guarantee that whatever the detector *does* emit is structurally clean. Replacing this conservative drop with a richer reconciliation — absorbing the intervening content into the appropriate list item's child container and then re-detecting at the paragraph stage — is the principal task of the next phase.

## 10. Pipeline placement

The detector is inserted into the per-page processing flow **after** word-to-line grouping and the running-furniture filter, and **before** the line-to-paragraph merge. Once the detector returns its confirmed lists:

* Each confirmed list becomes a list element in the page's content sequence, occupying the position of its first item's start line.
* All claimed lines are removed from the line stream that feeds the paragraph merger.
* Unclaimed lines flow into the paragraph merger unchanged.

The downstream paragraph merger must respect the "claimed by list" flag and refuse to merge claimed lines back into surrounding paragraphs. The XY-Cut layout analyzer treats the new list element identically to a paragraph for the purpose of geometric reading-order ordering.

## 11. Out of scope for Phase 1

The following behaviors are recognised gaps and will be addressed in later phases. They must not be smuggled into Phase 1 even if a fixture seems to demand them.

* Roman, alphabetic, Korean letter, circled-number, and bullet-glyph labels.
* Multi-style detection that tries several numbering styles per run.
* Lists that visually continue across a page break.
* Re-discovering missed lists from already-merged paragraphs.
* Reconciliation between heading classification and list detection when a label-bearing line also looks heading-like by font.
* Multi-column lists where items alternate left/right.
* Nested lists (a list whose item body contains a sub-list).

When a fixture exhibits one of these patterns, Phase 1 leaves it as ordinary paragraph text, exactly as the current pipeline does today. The Phase 1 baseline diff (see ADR 0001 §Procedural guardrails) measures how much new ground is gained, not how much remaining ground is missed.

## 12. Pseudocode (language-agnostic)

The pseudocode below summarises §§ 4 – 7 and § 9. It is normative for behavior but not for code structure: the implementation is free to redesign the dispatch and class layout for idiomatic C#.

```
function detect_arabic_lists(lines):
    open = empty list of candidates
    for index, line in enumerate(lines):
        if line is hidden or line.text is blank or line.font_size < 1:
            continue
        label = try_parse_arabic_label(line.text)
        if label is None:
            open.append(new candidate with one non-label entry)
            continue
        target = None
        for cand in reversed(open[: bounded_lookback]):
            if cand can be extended by (line, label):
                target = cand
                break
        if target is not None:
            target.append(line, label)
        else:
            open.append(new candidate seeded with (line, label))

    confirmed = [c for c in open if c.label_count >= 2 and not all_labels_are_decimals(c)]

    for cand in confirmed:
        absorb_continuations(cand, lines)

    return confirmed


function try_parse_arabic_label(text):
    if text matches "( <digits> ) <ws>":         return Label(prefix='(',  number=digits, suffix=') ' + ws)
    if text matches "[ <digits> ] <ws>":         return Label(prefix='[',  number=digits, suffix='] ' + ws)
    if text matches "<digits> <punct> <ws>":
        if next char after digits is '.' followed by digit:
            return None     # decimal, not a label
        return Label(prefix='', number=digits, suffix=punct + ws)
    return None


function can_extend(cand, line, label):
    last = cand.last_label_entry
    if last is None: return False
    if last.label.prefix != label.prefix: return False
    if last.label.suffix.first_char != label.suffix.first_char: return False
    if label.number != last.label.number + 1: return False
    tol = same_left_tolerance(font_size = last.font_size)
    same_left = abs(line.left - last.left) <= tol
    near_left = abs(line.left - last.left) <= 4 * tol
    if cand.requires_strict_same_left and not same_left: return False
    return same_left or near_left


function absorb_continuations(cand, lines):
    items = cand.items
    for k from 0 to len(items) - 1:
        start = items[k].line_index
        end   = items[k+1].line_index if k+1 < len(items) else len(lines)
        previous_baseline = lines[start].baseline
        for j from start + 1 to end - 1:
            line = lines[j]
            if line is claimed by any list: break
            if line.left < items[k].left - same_left_tolerance(font_size=line.font_size): break
            if abs(line.baseline - previous_baseline) > 1.2 * typical_line_spacing(items[k]): break
            if try_parse_arabic_label(line.text) is not None: break
            items[k].absorb(line)
            previous_baseline = line.baseline


function reconcile(confirmed_lists, residual_lines):
    # Build provisional paragraph blocks the same way the downstream merger would.
    provisional_paragraphs = paragraph_merge(residual_lines)

    rejected = empty set

    # Invariant 1: pairwise sibling list bounding boxes must be disjoint.
    for (a, b) in pairs(confirmed_lists):
        if bounding_box_overlaps(a.bbox, b.bbox):
            rejected.add(a)
            rejected.add(b)

    # Invariant 2: no provisional paragraph may sit substantially inside a list bbox.
    for L in confirmed_lists:
        if L in rejected: continue
        for P in provisional_paragraphs:
            if bounding_box_substantially_contains(L.bbox, P.bbox):
                rejected.add(L)
                break

    # Restore lines from rejected lists to the residual stream in original order.
    surviving = [L for L in confirmed_lists if L not in rejected]
    restored_indices = sorted(union of every rejected.claimed_line_indices)
    final_residual = merge(residual_lines, lines_at(restored_indices))

    return surviving, final_residual
```

## 13. Acceptance criteria

The Phase 1 implementation is considered correct when:

1. On a fixture that contains a clean `1.`/`2.`/`3.` enumerated list in body text, those lines are emitted as a list element rather than as a paragraph, and the Markdown output renders the list with the correct numbers.
2. On a fixture that contains section numbers like "1.5 Discussion" followed by "1.6 Conclusion", no list is emitted from those numbers; they remain paragraphs.
3. On a fixture with no Arabic-numeric lists (e.g. `lorem_ipsum.pdf`), the Markdown and JSON output diff against the baseline is empty.
4. On Roman-numbered fixtures (e.g. `us_constitution.pdf`'s Article numbers), the Markdown and JSON output diff against the baseline is empty (Phase 1 is Arabic-only by design). On a fixture that mixes Korean section markers with Arabic enumeration inside articles (e.g. `kr_constitution.pdf` Articles 89, 111), only the Arabic enumerations become lists; Korean section markers stay paragraphs.
5. A list that wraps body text across multiple lines preserves the body content, with the label stripped from the start line only.
6. The XY-Cut and renumbering stages downstream see the list element as a single unit, preserving overall reading order.
7. **Sibling list bounding boxes are pairwise disjoint.** No two list elements emitted at the page level have overlapping bounding boxes.
8. **No paragraph element shares a list element's bounding-box interior.** No paragraph block's bounding box is wholly or substantially contained within any list element's bounding box.

Criteria 7 and 8 are the structural invariants stated in § 8; their enforcement mechanism is the conservative reconciliation in § 9. Violation of 7 or 8 is a hard failure regardless of how good 1–6 look on the same fixture.

Acceptance is judged against the committed baseline snapshots (see § 4 of ADR 0001 procedural guardrails); fixture-by-fixture diff is the deciding artefact for criteria 1–6, and a structural assertion across all element pairs is the deciding artefact for criteria 7–8.
