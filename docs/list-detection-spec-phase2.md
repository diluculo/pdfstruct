# List detection — Phase 2 behavior specification

Status: draft for review (2026-05-01)
Builds on: `docs/list-detection-spec.md` (Phase 1, frozen)
Scope: Phase 2 only.

This document specifies what Phase 2 of list detection adds on top of Phase 1. The Phase 1 specification is unchanged and remains the authoritative description of label parsing, run grouping, the decimal sanity filter, body continuation, structural invariants, and conservative reconciliation. Phase 2 modifies one stage (continuation absorption), adds one new responsibility (children placement under each list item), and updates the reconciliation rule to account for the new structure.

The clean-room procedural guardrails of `docs/adr/0001-list-detection-license.md` continue to govern the implementation: behaviour is described in this document in our own terms, and the implementation is written from this document without reference to upstream source code.

---

## 1. Goal

Recover from the conservative drops Phase 1 produces when non-continuation lines fall between consecutive list items. In Phase 1 such lines remain in the residual stream, form provisional sibling paragraphs, and trigger invariant 2, which forces the entire list to be rejected. Phase 2 instead places those lines inside the previous item's child container so they are no longer siblings of the list — they are its children. This restores detection of any list whose intervening content is text content that would have been absorbed had the structure been recursive in Phase 1.

## 2. Inputs

The detector input is unchanged from Phase 1: per-page text-line stream, in document order, with bounding boxes, font sizes, baselines, and average glyph heights.

The Phase 2 work consumes the *output* of Phase 1's body-absorption stage (§ 7 of Phase 1) plus access to the same paragraph-merger function the page-level pipeline uses.

## 3. Output

For every confirmed list element, every list item that is **not the last item of its list** carries an ordered collection of child content elements. For Phase 2 the child elements are paragraph elements only.

A list item's child elements:
- own their own bounding box, derived from the lines that contributed to them.
- own their own page number, identical to the parent item's page number.
- carry the same `Kids` field type as the document root and other container elements, so a future phase can extend with sub-lists or other element types without changing the schema.

The last item of a list does not gain children in Phase 2. (See § 8.)

The list element's bounding box already encloses every item's bounding box, including the items' children, by construction (§ 6).

## 4. Item territory

An item's *territory* is the half-open range of line indices in the page's line stream from the line immediately after the item's start line, up to (but not including) the next item's start line. Lines in this range are candidates for either body continuation (§ 5) or child absorption (§ 6) of the current item.

For the last item of a list, no territory is defined in Phase 2. The last item only undergoes Phase 1 body absorption with no child handling.

## 5. Body continuation (refinement of Phase 1 § 7)

Body continuation runs unchanged from Phase 1 except that it is now framed as the *first* of two zones in the territory walk. While the walk is still in the body zone, the existing four rejection rules apply:

1. The line is already claimed by some list.
2. The line's left x-coordinate is more than one alignment tolerance to the left of the item's start line.
3. The vertical distance between this line's baseline and the previous absorbed line's baseline exceeds about 1.2 times the typical inter-line spacing.
4. The line is itself a label-bearing line under Phase 1 § 4.

Phase 1 broke the entire walk on any of these. Phase 2 splits them into two groups:

- Rules 1, 2, and 4 mean the line is **outside the item's territory entirely**. The walk breaks; subsequent lines are not processed for this item; the offending line stays in the residual stream (or belongs to another list).
- Rule 3 means the line is **inside the territory but not body**. The walk does not break; instead it transitions to the child zone (§ 6). The line that triggered the transition is the first child line.

Lines absorbed in the body zone extend the item's body text and bounding box exactly as in Phase 1.

## 6. Child absorption

Once the walk has transitioned to the child zone, every subsequent line in the territory is collected into a per-item *children buffer*, until either:
- the territory ends (the next item's start line is reached), or
- one of rules 1, 2, or 4 from § 5 triggers (the line is outside the territory).

A line in the child zone is not subject to the body-zone gap test (rule 3). The child zone has no upper bound on baseline gap; it is bounded only by the item's territory and the three out-of-zone rules.

Lines collected into the children buffer are flagged as claimed (so they are removed from the page-level residual stream).

After the territory walk for item k completes, the children buffer is processed through the page-level paragraph-merger function, identical to the call the pipeline makes on the page-wide residual. The merger produces zero or more paragraph blocks, each of which is converted to a paragraph element with its own bounding box, page number, and text content. These paragraph elements become item k's children, in document order.

The same merger function is used. Phase 2 does not introduce a simplified or duplicate merger. Reusing the global function guarantees children paragraphs are formed by the same rules as page-level paragraphs.

## 7. Reconciliation update

The structural invariants in Phase 1 § 8 remain in force:
- Sibling list bounding boxes are pairwise disjoint.
- No paragraph element shares a list element's bounding-box interior.

The change in Phase 2 is that "paragraph element" in invariant 2 is implicitly **page-level sibling** paragraph element. A paragraph element that is a child of a list item is, by definition, contained inside that list's bounding box (transitively, through the item's bounding box). Containment of a child is not a violation; it is the definition of containment.

This refinement is implemented mechanically: the reconciliation pass forms its provisional sibling paragraph blocks from the residual line stream *after* Phase 2's child absorption has flagged Kids lines as claimed. Children paragraphs are therefore not in the provisional set, and the invariant 2 check is naturally restricted to siblings.

The list-list invariant (criterion 7) is unaffected by Phase 2: Phase 2 does not introduce nested lists, so all lists remain page-level siblings.

## 8. Out of scope for Phase 2

The following are recognised gaps that Phase 2 leaves to later phases:

- **Sub-list detection inside Kids.** A child paragraph's first line might itself be a list label, indicating a nested list. Phase 2 does not detect this. The child remains a paragraph; the nested-list run is missed. This is the principal task of the post-paragraph rescue work in a later phase.
- **Cross-page list joining.** A list whose items span a page break is still detected as two separate lists (or one of them is rejected). Phase 2 changes nothing here.
- **Last-item children.** The last item of a list does not gain children in Phase 2. The motivation is to avoid over-absorbing post-list page content (the next section header, the next paragraph) as a child of the last list item, which would happen if the territory of the last item were defined to extend to the end of the page.
- **Image, table, formula, or other non-text children.** Only paragraph elements may be children of a list item in Phase 2. Non-text content lines are not absorbed by the territory walk in any case (the walk operates on `TextLineBlock` instances only).
- **Recursive list detection.** Even though Phase 2 reuses the paragraph-merger function on the children buffer, it does *not* re-run the list detector on those merged paragraphs. A child paragraph that begins with `1.` does not become a sub-list run in Phase 2.

## 9. Pipeline placement

Phase 2 changes the wiring between Phase 1 detection and reconciliation:

```
Before Phase 2:
  detect labels → group into runs → sanity filter → body continuation → reconciliation

Phase 2:
  detect labels → group into runs → sanity filter → body+child territory walk → reconciliation
```

The body+child territory walk replaces Phase 1's body-only walk. After the walk, every confirmed list's items (except the last) carry their Kids. Reconciliation then runs on the (smaller) residual line stream and the (now Kids-aware) confirmed lists. Pages without detected lists are unaffected.

## 10. Pseudocode (language-agnostic)

```
function detect_arabic_lists(lines):
    candidates  = build_candidates(lines)        # Phase 1 §§ 4–5, unchanged
    confirmed   = sanity_filter(candidates)      # Phase 1 § 6, unchanged

    for cand in confirmed:
        absorb_territory(cand, lines)            # Phase 2 §§ 5–6, replaces Phase 1 § 7

    return confirmed


function absorb_territory(cand, lines):
    items = cand.items
    last_index = len(items) - 1

    for k from 0 to last_index - 1:
        item = items[k]
        territory_start = item.start_line + 1
        territory_end   = items[k+1].start_line  # exclusive

        body_mode = True
        children_buffer = []
        previous_baseline = lines[item.start_line].baseline
        typical_spacing = max(lines[item.start_line].avg_height, 1.0) * 1.2

        for j from territory_start to territory_end - 1:
            line = lines[j]
            if line is claimed by any list: break
            if line.left < item.left - same_left_tolerance(line.font_size): break
            if try_parse_arabic_label(line.text) is not None: break

            if body_mode:
                gap = abs(line.baseline - previous_baseline)
                if gap <= typical_spacing:
                    item.absorb_body(line)
                    mark_claimed(j)
                    previous_baseline = line.baseline
                    continue
                else:
                    body_mode = False

            # child mode (sticky once entered)
            children_buffer.append(line)
            mark_claimed(j)
            previous_baseline = line.baseline

        if children_buffer is not empty:
            child_blocks = paragraph_merger(children_buffer)   # same merger as page-level
            item.kids = [paragraph_element(b) for b in child_blocks]

    # last item: body absorption only, walk to end of page, no children
    last_item = items[last_index]
    last_start = last_item.start_line + 1
    last_end   = len(lines)
    previous_baseline = lines[last_item.start_line].baseline
    typical_spacing = max(lines[last_item.start_line].avg_height, 1.0) * 1.2

    for j from last_start to last_end - 1:
        line = lines[j]
        if line is claimed by any list: break
        if line.left < last_item.left - same_left_tolerance(line.font_size): break
        if try_parse_arabic_label(line.text) is not None: break

        gap = abs(line.baseline - previous_baseline)
        if gap > typical_spacing: break

        last_item.absorb_body(line)
        mark_claimed(j)
        previous_baseline = line.baseline
```

## 11. Acceptance criteria

Phase 2 is correct when:

1. All Phase 1 acceptance criteria (§ 13 of `list-detection-spec.md`, items 1–8) continue to hold.
2. On a fixture where Phase 1 conservatively rejected a list because of one or more provisional sibling paragraphs falling inside its bounding box, and where those paragraphs would be absorbed as Kids by the territory walk, the list is now confirmed and the Markdown / JSON output reflects it.
3. The Markdown renderer indents Kids of a list item beneath the item's body line so that nested structure is visually evident.
4. The JSON renderer populates each list item's `kids` array with the absorbed paragraph elements.
5. The structural invariant test suite continues to pass: no two sibling list bounding boxes overlap; no page-level sibling paragraph's bounding box is substantially contained inside a list element's bounding box. Children paragraphs (which are contained by definition) are not asserted as sibling violations.
6. The fixture baseline diff for committed regression fixtures is reviewable: the diff is the union of (a) lists that move from rejected to confirmed, and (b) any Kids paragraphs newly attached to them. No unrelated text reordering or classification regression should appear.

## 12. Notes for the implementer

Phase 2 reuses the page-level paragraph-merger function. The implementation must call the existing function on the children buffer and not introduce a near-duplicate.

The reconciliation pass already operates on the residual after detection. With Phase 2 marking children lines as claimed, the residual naturally excludes them. No reconciliation-side change is required to honour invariant 2's refinement to "page-level siblings only" — the structural change in detection is sufficient.

ListItem already exposes a `Kids` field. Phase 2 populates it; no model change is required.

The Markdown renderer must learn to emit indented Kids. The JSON renderer must learn to emit a `kids` array per list item with the same element-wrapping logic used for top-level elements. Both renderers should treat Kids as ordinary `ContentElement` so future expansion (sub-lists, images) does not require renderer change.
