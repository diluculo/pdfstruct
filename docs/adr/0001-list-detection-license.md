# ADR 0001 — List detection: clean-room reimplementation under Apache-2.0

Status: accepted (2026-05-01)
Owner: Jong Hyun Kim
Supersedes: —
Superseded by: —

## Context

PdfStruct ports its layout-analysis pipeline stage by stage from OpenDataLoader-pdf (ODL). When porting list detection — a stage ODL delegates to a separate veraPDF library — three distinct licenses interact and must be reconciled.

**PdfStruct.** Apache License 2.0. Single-license. NuGet distribution targets users who expect a permissive license without copyleft surface.

**OpenDataLoader-pdf (ODL).** Apache License 2.0. Copyright Hancom Inc., 2025–2026. The ODL `ListProcessor` and surrounding pipeline glue are Apache-licensed and compatible with PdfStruct's license.

**veraPDF wcag-algorithms (the algorithm library ODL depends on for list detection).** Verified locally on 2026-05-01 against tag `v1.31.18`, commit `fac9d656b218f8d8cfb30e9cf3e376c444712877`. The repository ships two LICENSE files at the root: `LICENSE.GPL` (GNU General Public License v3, 29 June 2007) and `LICENSE.MPL` (Mozilla Public License version 2.0). Each source file carries a header offering a dual choice: GPLv3+ **or** MPLv2+. The reuser picks one.

ODL's own `THIRD_PARTY_LICENSES.md` records that Hancom Inc. distributes the veraPDF dependencies under MPL-2.0, the dual-license choice they made.

## Constraints

* Apache-2.0 (PdfStruct) is **incompatible** with GPLv3+ for combined distribution: choosing the GPL option from the dual would force PdfStruct into copyleft, which is incompatible with the project's NuGet distribution goals.
* MPL-2.0 carries **file-level copyleft**: a port of an MPL-licensed source file is itself a derivative work and must remain MPL-2.0. Choosing the MPL option allows combined distribution alongside Apache-2.0 code (an MPL-2.0 § 3.3 "Larger Work"), but the *ported files themselves* would have to ship under MPL-2.0. The PdfStruct codebase would become two-licensed in practice.
* WCAG (Web Content Accessibility Guidelines) is a W3C Recommendation, royalty-free under the W3C Document License. WCAG itself describes *what* must be true of accessible PDFs (e.g., "lists must be programmatically marked up"); it does **not** specify *how* to reconstruct list semantics from an untagged PDF. The list-extraction algorithms in `wcag-algorithms` are veraPDF's R&D, not a W3C-mandated procedure.

## Decision

Phase 1 (and subsequent list-detection phases) ship as a **clean-room reimplementation under Apache-2.0**. PdfStruct remains a single-license project. No file is dual-licensed; no MPL-licensed sub-package is created.

Implementation work proceeds against an internally-authored behavior specification (`docs/list-detection-spec.md`) and an internally-authored data-model mapping (`docs/pdfpig-odl-mapping.md`). The Java source code of `wcag-algorithms` and ODL's `ListProcessor` is closed during coding.

## Rationale

1. **Algorithms are not protected by copyright; expression is.** US 17 U.S.C. § 102(b) and the parallel Korean copyright doctrine separate idea from expression: ideas, procedures, processes, systems, methods of operation are excluded from copyright protection. The list-detection techniques in `wcag-algorithms` (incrementing-numeric run grouping, common-prefix/suffix label decomposition, alignment-constrained label matching) are general document-processing techniques. They predate veraPDF and appear, in some form, in Apache PDFBox, Apache Tika, GROBID, and other open document tools. Independent reimplementation of the technique without literal code copying is therefore not a derivative work.

2. **WCAG-as-public-standard is a *strengthening* argument, not the primary one.** WCAG itself does not specify the algorithms in question, but its existence as a public standard signals that the broader subject area is intended for unimpeded implementation. This shores up the defense that the techniques are commodity, not veraPDF trade secrets.

3. **Direct port creates avoidable license complexity.** Taking the MPL-2.0 dual option and shipping ported files as MPL would require either dual-licensing the entire PdfStruct package or splitting list detection into a separate MPL package. Either path imposes ongoing compliance obligations on downstream PdfStruct users that are not justified by the modest implementation savings. Hancom's own approach (a separate MPL-2.0 sub-dependency) works for a JAR-based ecosystem but is structurally awkward for NuGet's flatter dependency model and adds a license-evaluation step that proprietary consumers would have to repeat.

4. **The *pipeline shape* is what we need to be faithful to, not the source code.** The behavior we want to reproduce — "given an untagged page of text lines, recover ordered list runs and emit them as list elements before paragraph merging" — is a description, not a transcription. The shape of ODL's pipeline (line-grouping → header/footer → list → paragraph → heading → reading-order) is itself a design choice we follow, but a design choice expressed as natural language in a specification rather than as Java method bodies.

## Procedural guardrails (binding)

The following procedure governs Phase 1 and every subsequent list-detection phase. Deviations require an updated ADR, not a one-off exception.

1. **Behavior specification is single-source.** `docs/list-detection-spec.md` is authored from observed and documented behavior of upstream tools. It contains no Java class names, no method signatures, no Java-language code excerpts, and no veraPDF/ODL identifier names. References to upstream constants describe their *intent* (e.g. "about a third of a font size") rather than reproducing literal numerals as direct quotations. Pseudocode is language-agnostic.

2. **Data-model mapping is internal.** `docs/pdfpig-odl-mapping.md` records what each pipeline stage sees and produces, in terms of PdfPig types and PdfStruct types. It identifies gaps (fields the spec needs that PdfStruct does not yet expose) so the implementation can fill them deliberately.

3. **Source code is closed during coding.** When implementing Phase 1, the implementer (human or AI) does not have `wcag-algorithms` or ODL Java source open in any window, IDE buffer, agent context, or printed material. Open questions during coding are resolved by enhancing the spec, by inspecting fixture behavior, or by deriving from the existing PdfStruct codebase — never by re-reading the upstream Java.

4. **C# design is independent.** The 13-class veraPDF Java structure for label detection is **not** mirrored. The implementation chooses C#-idiomatic constructs (interfaces, sealed records, pattern matching, switch expressions) to express the algorithm. Variable names, method names, and dispatch shape are designed for the C# codebase, not translated from Java.

5. **Provenance is declared at every commit.** Every commit and pull request that touches list detection includes the standard process declaration in its description:

   > Behavior spec extracted from veraPDF wcag-algorithms reference (MPL-2.0/GPL-3.0 dual). Implementation written from spec without reference to original source code during coding. Class hierarchy, naming, and dispatch redesigned for idiomatic C#. No verbatim translation. License: Apache-2.0 (clean-room reimplementation of unprotected algorithm/idea).

6. **Baseline-diff regression is mandatory.** Before Phase 1 lands, baseline Markdown and JSON outputs are committed for at least five fixtures spanning the project's coverage matrix (Korean legal text, Western legal text, single-column body, two-column academic, magazine layout). Phase 1 is judged by the diff of these snapshots, fixture by fixture. A fixture-specific tweak that improves one fixture but visibly regresses another requires explicit justification.

## Consequences

* PdfStruct remains pure Apache-2.0. NuGet packaging metadata, NOTICE files, and downstream license evaluation stay simple.
* Behavior may diverge from veraPDF in edge cases. Fixture regression covers the dominant cases; rare edge cases are accepted divergences.
* Tracking upstream improvements requires re-specification rather than mechanical diff translation. This is a meaningful maintenance cost, recorded here for awareness.
* The ported repository (`D:/Codes/verapdf-wcag-algs`, used for the initial license verification) is closed and not consulted during implementation.
* The local OpenDataLoader-pdf clone (`D:/Codes/opendataloader-pdf`) remains useful for *pipeline-stage ordering* and for non-list portions of the pipeline (pure Apache-2.0 → Apache-2.0 port is permitted with attribution). It is not consulted for list detection.

## References

* Verified on 2026-05-01:
  * `D:/Codes/verapdf-wcag-algs/LICENSE.GPL` (GPL v3, 29 June 2007)
  * `D:/Codes/verapdf-wcag-algs/LICENSE.MPL` (Mozilla Public License v2.0)
  * `D:/Codes/verapdf-wcag-algs/license/template/license.txt` (per-file dual-license header template)
  * Upstream tag `v1.31.18`, commit `fac9d656b218f8d8cfb30e9cf3e376c444712877`
* OpenDataLoader-pdf `THIRD_PARTY_LICENSES.md` recording MPL-2.0 distribution choice for veraPDF components.
* PdfStruct `LICENSE` (Apache 2.0).
* US 17 U.S.C. § 102(b); Korean Copyright Act Art. 2(1) (idea/expression dichotomy as applied to algorithms).
* W3C Document License governing WCAG 2.1 / 2.2 publications.
