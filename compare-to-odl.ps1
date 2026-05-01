# Element-level diff between PdfStruct's JSON output and OpenDataLoader-pdf's
# golden JSON output for every fixture in playground/.
#
# For each <name>.pdf the script expects:
#   playground/<name>/<name>.json            (PdfStruct, produced by run-pdfstruct.ps1)
#   playground/<name>/odl/<name>.json        (ODL,        produced by run-odl-golden.ps1)
#
# The diff is element-counts only at this stage — per-page, per-type. That is
# enough to spot order-of-magnitude divergences (we are missing 15 lists, ODL
# has 5 tables we do not, etc.) which are the cases the divergence log calls
# investigate today. Element-level pairing by content / bbox proximity is a
# follow-up once human-marked ground truth exists for the same fixtures.
#
# Usage:
#   .\compare-to-odl.ps1
#   .\compare-to-odl.ps1 -Path D:\some\folder
#   .\compare-to-odl.ps1 -OutputFile playground\odl-diff.md

[CmdletBinding()]
param(
    [string] $Path = (Join-Path $PSScriptRoot 'playground'),
    [string] $OutputFile = (Join-Path $PSScriptRoot 'playground\odl-diff.md')
)

# --- helpers ---------------------------------------------------------------

# Walks a JSON document tree (root + nested kids / list items / table rows /
# table cells) and returns a hashtable keyed by "<page>~~<type>" with the
# count of elements seen at each (page, type) combination.
function Get-CountsByPageAndType {
    param($Doc)

    $counts = @{}
    foreach ($element in $Doc.kids) {
        Add-CountForElement $counts $element
    }
    return $counts
}

# Adds one count for the element and recurses into every container field
# the JSON shape exposes — element kids (header/footer/list-item kids), the
# list element's "list items" array, and the table element's row/cell tree.
function Add-CountForElement {
    param($Counts, $Element)

    $page = $Element.'page number'
    $type = if ($Element.type) { $Element.type } else { '<untyped>' }
    if (-not $page) { $page = 0 }

    $key = "$page~~$type"
    if ($Counts.ContainsKey($key)) {
        $Counts[$key]++
    } else {
        $Counts[$key] = 1
    }

    if ($Element.kids) {
        foreach ($child in $Element.kids) {
            Add-CountForElement $Counts $child
        }
    }
    if ($Element.'list items') {
        foreach ($item in $Element.'list items') {
            Add-CountForElement $Counts $item
            if ($item.kids) {
                foreach ($child in $item.kids) {
                    Add-CountForElement $Counts $child
                }
            }
        }
    }
    if ($Element.rows) {
        foreach ($row in $Element.rows) {
            Add-CountForElement $Counts $row
            if ($row.cells) {
                foreach ($cell in $row.cells) {
                    Add-CountForElement $Counts $cell
                    if ($cell.kids) {
                        foreach ($child in $cell.kids) {
                            Add-CountForElement $Counts $child
                        }
                    }
                }
            }
        }
    }
}

# Diffs two JSON documents at the (page, type) level and returns a small
# object with two views: aggregate counts by type, and the per-page rows
# whose count differs between the two sides.
function Get-FixtureDiff {
    param($OursDoc, $OdlDoc)

    $oursCounts = Get-CountsByPageAndType $OursDoc
    $odlCounts  = Get-CountsByPageAndType $OdlDoc

    $allKeys = @()
    foreach ($k in $oursCounts.Keys) { $allKeys += $k }
    foreach ($k in $odlCounts.Keys)  { if ($allKeys -notcontains $k) { $allKeys += $k } }
    $allKeys = $allKeys | Sort-Object

    $byType = @{}
    $pagesWithDelta = @()

    foreach ($key in $allKeys) {
        $ours = if ($oursCounts.ContainsKey($key)) { $oursCounts[$key] } else { 0 }
        $odl  = if ($odlCounts.ContainsKey($key))  { $odlCounts[$key]  } else { 0 }

        $page, $type = $key -split '~~', 2
        if (-not $byType.ContainsKey($type)) { $byType[$type] = @{ Ours = 0; Odl = 0 } }
        $byType[$type].Ours += $ours
        $byType[$type].Odl  += $odl

        if ($ours -ne $odl) {
            $pagesWithDelta += [PSCustomObject]@{
                Page = [int]$page
                Type = $type
                Ours = $ours
                Odl  = $odl
            }
        }
    }

    $byTypeRows = $byType.Keys | Sort-Object | ForEach-Object {
        [PSCustomObject]@{
            Type = $_
            Ours = $byType[$_].Ours
            Odl  = $byType[$_].Odl
        }
    }

    [PSCustomObject]@{
        ByType         = $byTypeRows
        PagesWithDelta = $pagesWithDelta | Sort-Object Page, Type
    }
}

# --- entry -----------------------------------------------------------------

if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
    Write-Error "Playground folder not found: $Path"
    exit 1
}

$pdfs = Get-ChildItem -LiteralPath $Path -Filter '*.pdf' -File
if ($pdfs.Count -eq 0) {
    Write-Warning "No PDFs found in $Path"
    exit 0
}

$fixtureSummaries = @()
$missing = @()

foreach ($pdf in $pdfs) {
    $name      = [IO.Path]::GetFileNameWithoutExtension($pdf.Name)
    $oursPath  = Join-Path $Path "$name\$name.json"
    $odlPath   = Join-Path $Path "$name\odl\$name.json"

    if (-not (Test-Path -LiteralPath $oursPath -PathType Leaf)) {
        $missing += "[$name] PdfStruct JSON missing: $oursPath"
        continue
    }
    if (-not (Test-Path -LiteralPath $odlPath -PathType Leaf)) {
        $missing += "[$name] ODL JSON missing:        $odlPath"
        continue
    }

    Write-Host "[$name]" -ForegroundColor Yellow

    $oursDoc = Get-Content -LiteralPath $oursPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $odlDoc  = Get-Content -LiteralPath $odlPath -Raw -Encoding UTF8 | ConvertFrom-Json

    $fixtureSummaries += [PSCustomObject]@{
        Name      = $name
        OursPages = $oursDoc.'number of pages'
        OdlPages  = $odlDoc.'number of pages'
        Diff      = (Get-FixtureDiff -OursDoc $oursDoc -OdlDoc $odlDoc)
    }
}

# Render the report markdown into $OutputFile. The format is one section per
# fixture, each containing a counts-by-type table and a counts-by-page table
# so the reader can drill down to the page where a divergence concentrates.
$report = New-Object System.Text.StringBuilder

[void]$report.AppendLine('# ODL vs PdfStruct element-count diff')
[void]$report.AppendLine()
[void]$report.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$report.AppendLine()
[void]$report.AppendLine('Per-fixture, per-page, per-type element counts. ODL is the *baseline*, not ground truth (see `docs/odl-divergence-log.md`). A non-zero delta is a starting point for investigation, not a verdict on which side is correct.')
[void]$report.AppendLine()

if ($missing.Count -gt 0) {
    [void]$report.AppendLine('## Skipped fixtures')
    foreach ($m in $missing) {
        [void]$report.AppendLine("- $m")
    }
    [void]$report.AppendLine()
}

foreach ($fixture in $fixtureSummaries) {
    [void]$report.AppendLine("## $($fixture.Name)")
    [void]$report.AppendLine()
    [void]$report.AppendLine("PdfStruct pages: $($fixture.OursPages); ODL pages: $($fixture.OdlPages)")
    [void]$report.AppendLine()

    $diff = $fixture.Diff

    [void]$report.AppendLine('### Counts by type')
    [void]$report.AppendLine()
    [void]$report.AppendLine('| type | PdfStruct | ODL | delta |')
    [void]$report.AppendLine('|---|---:|---:|---:|')
    foreach ($row in $diff.ByType) {
        $delta = $row.Ours - $row.Odl
        [void]$report.AppendLine("| $($row.Type) | $($row.Ours) | $($row.Odl) | $delta |")
    }
    [void]$report.AppendLine()

    if ($diff.PagesWithDelta.Count -gt 0) {
        [void]$report.AppendLine('### Pages with non-zero delta')
        [void]$report.AppendLine()
        [void]$report.AppendLine('| page | type | PdfStruct | ODL | delta |')
        [void]$report.AppendLine('|---:|---|---:|---:|---:|')
        foreach ($row in $diff.PagesWithDelta) {
            $delta = $row.Ours - $row.Odl
            [void]$report.AppendLine("| $($row.Page) | $($row.Type) | $($row.Ours) | $($row.Odl) | $delta |")
        }
        [void]$report.AppendLine()
    }
}

$reportText = $report.ToString()
$dir = Split-Path -Parent $OutputFile
if ($dir -and -not (Test-Path -LiteralPath $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}
Set-Content -LiteralPath $OutputFile -Value $reportText -Encoding UTF8

Write-Host ""
Write-Host "Wrote report: $OutputFile" -ForegroundColor Green
