$specs = @(
    '001-core-game-data-pipeline',
    '002-core-legacy-wire-protocol',
    '003-core-auth-roster-and-leases',
    '004-core-zone-spine-and-world-entry',
    '005-core-spatial-aoi-and-spawns',
    '006-core-movement-and-obstacles',
    '007-core-portals-and-map-handoff',
    '008-core-inventory-and-equipment',
    '009-core-combat-and-missiles',
    '010-core-monster-ai-and-progression',
    '011-core-loot-and-item-generation',
    '016-module-chat-channels'
)

Write-Host "=== CONTRACT FILES ==="
foreach ($s in $specs) {
    $dir = Join-Path 'd:\Projects\jx-server\specs' $s
    $contracts = Get-ChildItem -Path (Join-Path $dir 'contracts') -Filter '*.md' -ErrorAction SilentlyContinue
    foreach ($c in $contracts) {
        Write-Host "[$s] $($c.Name)"
    }
}

Write-Host "`n=== TASKS.MD REFERENCED SOURCE/TEST FILES EXISTENCE CHECK ==="
$totalPaths = 0
$missingPaths = 0
$missingList = @()

foreach ($s in $specs) {
    $taskFile = Join-Path 'd:\Projects\jx-server\specs' "$s\tasks.md"
    if (Test-Path $taskFile) {
        $content = Get-Content $taskFile -Raw
        # Look for patterns like `src/...` or `tests/...` or `tools/...`
        $matches = [regex]::Matches($content, '`((?:src|tests|tools)/[^`]+)`')
        foreach ($m in $matches) {
            $relPath = $m.Groups[1].Value
            # Clean up line numbers if any (e.g., file.cs:123)
            $cleanPath = $relPath -replace ':\d+.*$', ''
            $fullPath = Join-Path 'd:\Projects\jx-server' $cleanPath
            $totalPaths++
            if (-not (Test-Path $fullPath)) {
                $missingPaths++
                $missingList += "[$s] $relPath (resolved: $cleanPath)"
            }
        }
    }
}

Write-Host "Total source/test paths extracted: $totalPaths"
Write-Host "Missing paths: $missingPaths"
if ($missingPaths -gt 0) {
    Write-Host "Missing paths list:"
    $missingList | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Host "All referenced source/test file paths in tasks.md EXIST in repository!"
}


Write-Host "`n=== LOCATING MISSING FILES ==="
$search = @('CharacterItemUnitTests.cs', 'RegionObstacle*.cs', '*Obstacle*.cs', 'NpcTemplate*.cs', 'PlayerTemplate*.cs', '*TemplateCatalog.cs')
foreach ($s in $search) {
    $matches = Get-ChildItem -Path 'd:\Projects\jx-server' -Filter $s -Recurse -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
    Write-Host "Pattern: $s"
    foreach ($m in $matches) {
        Write-Host "  Found: $m"
    }
}
