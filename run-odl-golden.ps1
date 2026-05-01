# Generate OpenDataLoader reference outputs for every PDF in playground.
#
# For each <name>.pdf, produces:
#   <name>/ODL/<name>.json
#   <name>/ODL/<name>.md
#   <name>/ODL/<name>_annotated.pdf
#   <name>/ODL/page-NNN.png
#   <name>/ODL/images/*        - extracted images when ODL emits them
#
# Usage:
#   .\run-odl-golden.ps1
#   .\run-odl-golden.ps1 -Path D:\some\folder
#   .\run-odl-golden.ps1 -Clean

[CmdletBinding()]
param(
    [string] $Path = (Join-Path $PSScriptRoot 'playground'),
    [string] $OdlCli = (Join-Path $PSScriptRoot 'out\odl-venv\Scripts\opendataloader-pdf.exe'),
    [string] $PdfToPng = 'pdftoppm',
    [switch] $Clean
)

if (-not (Test-Path -LiteralPath $OdlCli -PathType Leaf)) {
    Write-Error "ODL CLI not found: $OdlCli`nInstall it first, for example: python -m venv out\odl-venv; out\odl-venv\Scripts\python.exe -m pip install opendataloader-pdf"
    exit 1
}

if (-not (Get-Command $PdfToPng -ErrorAction SilentlyContinue)) {
    Write-Error "PDF-to-PNG renderer not found: $PdfToPng"
    exit 1
}

if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
    Write-Error "Playground folder not found: $Path"
    exit 1
}

$pdfs = Get-ChildItem -LiteralPath $Path -Filter '*.pdf' -File
if ($pdfs.Count -eq 0) {
    Write-Warning "No PDFs found in $Path"
    exit 0
}

Write-Host "Found $($pdfs.Count) PDF(s) in $Path" -ForegroundColor Cyan
Write-Host "Using ODL CLI: $OdlCli" -ForegroundColor DarkGray
Write-Host "Using renderer: $PdfToPng`n" -ForegroundColor DarkGray

$results = @()
$totalStart = Get-Date

foreach ($pdf in $pdfs) {
    $name = [IO.Path]::GetFileNameWithoutExtension($pdf.Name)
    $pdfOutputDir = Join-Path $Path $name
    $odlOutputDir = Join-Path $pdfOutputDir 'ODL'
    $imageDir = Join-Path $odlOutputDir 'images'

    Write-Host "[$name] " -NoNewline -ForegroundColor Yellow
    Write-Host $pdf.Name -ForegroundColor White

    if ($Clean -and (Test-Path -LiteralPath $odlOutputDir)) {
        Remove-Item -LiteralPath $odlOutputDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $odlOutputDir -Force | Out-Null
    New-Item -ItemType Directory -Path $imageDir -Force | Out-Null

    Get-ChildItem -LiteralPath $odlOutputDir -Filter 'page-*.png' -File -ErrorAction SilentlyContinue |
        Remove-Item -Force

    $start = Get-Date
    $odlArgs = @(
        $pdf.FullName,
        '--output-dir', $odlOutputDir,
        '--format', 'json,markdown,pdf',
        '--image-output', 'external',
        '--image-format', 'png',
        '--image-dir', $imageDir,
        '--quiet'
    )

    $odlOutput = & $OdlCli @odlArgs 2>&1
    $odlExitCode = $LASTEXITCODE
    foreach ($line in $odlOutput) { Write-Host "  $line" -ForegroundColor DarkGray }

    $pngExitCode = 0
    $annotatedPdf = Join-Path $odlOutputDir "$($name)_annotated.pdf"
    if ($odlExitCode -eq 0 -and (Test-Path -LiteralPath $annotatedPdf -PathType Leaf)) {
        $pagePrefix = Join-Path $odlOutputDir 'page'
        $pngOutput = & $PdfToPng -png -r 144 $annotatedPdf $pagePrefix 2>&1
        $pngExitCode = $LASTEXITCODE
        foreach ($line in $pngOutput) { Write-Host "  $line" -ForegroundColor DarkGray }

        $renderedPages = Get-ChildItem -LiteralPath $odlOutputDir -Filter 'page-*.png' -File |
            Where-Object { $_.Name -match '^page-\d+\.png$' } |
            Sort-Object {
                if ($_.BaseName -match '^page-(\d+)$') { [int]$Matches[1] } else { 0 }
            }

        foreach ($page in $renderedPages) {
            if ($page.BaseName -match '^page-(\d+)$') {
                $target = Join-Path $odlOutputDir ('page-{0:D3}.png' -f [int]$Matches[1])
                if ($page.FullName -ne $target) {
                    if (Test-Path -LiteralPath $target) {
                        Remove-Item -LiteralPath $target -Force
                    }
                    Move-Item -LiteralPath $page.FullName -Destination $target
                }
            }
        }
    }

    $elapsed = (Get-Date) - $start
    $status = if ($odlExitCode -eq 0 -and $pngExitCode -eq 0) { 'OK' } else { "FAIL (odl=$odlExitCode png=$pngExitCode)" }
    $results += [PSCustomObject]@{
        Name = $name
        Status = $status
        Seconds = [math]::Round($elapsed.TotalSeconds, 2)
        Json = if (Test-Path -LiteralPath (Join-Path $odlOutputDir "$name.json")) { (Get-Item -LiteralPath (Join-Path $odlOutputDir "$name.json")).Length } else { 0 }
        Markdown = if (Test-Path -LiteralPath (Join-Path $odlOutputDir "$name.md")) { (Get-Item -LiteralPath (Join-Path $odlOutputDir "$name.md")).Length } else { 0 }
        Pages = (Get-ChildItem -LiteralPath $odlOutputDir -Filter 'page-*.png' -File -ErrorAction SilentlyContinue).Count
        Images = (Get-ChildItem -LiteralPath $imageDir -File -ErrorAction SilentlyContinue).Count
    }
}

$totalElapsed = (Get-Date) - $totalStart

Write-Host "`nSummary:" -ForegroundColor Cyan
$results | Format-Table -AutoSize

$failed = @($results | Where-Object Status -ne 'OK').Count
Write-Host ("Total: {0} files in {1:F1}s, {2} failed" -f $results.Count, $totalElapsed.TotalSeconds, $failed) -ForegroundColor $(if ($failed -eq 0) { 'Green' } else { 'Red' })

if ($failed -gt 0) { exit 1 }
