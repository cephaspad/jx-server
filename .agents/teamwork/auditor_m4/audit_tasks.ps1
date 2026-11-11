Set-Location 'd:\Projects\jx-server'

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

Write-Host "=== 3. AUDIT TASKS.MD (ALL [X], REAL FILE PATHS) ==="

$taskStats = @()
$allMissingPaths = @()
$totalTasks = 0
$totalPathsChecked = 0

foreach ($s in $specs) {
    $taskPath = Join-Path 'specs' (Join-Path $s 'tasks.md')
    if (-not (Test-Path $taskPath)) {
        Write-Error "Missing tasks.md in $s"
        continue
    }

    $lines = Get-Content $taskPath
    
    # Count tasks
    $completedTasks = ($lines | Select-String -Pattern '^\s*-\s*\[[Xx]\]' -CaseSensitive:$false).Count
    $uncompletedTasks = ($lines | Select-String -Pattern '^\s*-\s*\[ \]' -CaseSensitive:$false).Count
    $totalTasks += ($completedTasks + $uncompletedTasks)

    # Extract file paths from backticks or task lines
    # Looking for patterns like `src/...`, `tests/...`, `tools/...`, `schemas/...`, `data/...`
    $extractedPaths = @()
    foreach ($line in $lines) {
        $matches = [regex]::Matches($line, '`([^`]+)`')
        foreach ($m in $matches) {
            $val = $m.Groups[1].Value.Trim()
            # If it looks like a file path with an extension or directory in src/tests/tools/schemas/data/specs
            if ($val -match '^(src|tests|tools|schemas|data|specs)/' -or $val -match '\.(cs|json|md|csproj|slnx)$') {
                # clean up line numbers or trailing punctuation if any (e.g. path.cs:42)
                $cleanPath = $val -replace ':\d+$', ''
                # Only check if it points to a file or directory
                $extractedPaths += $cleanPath
            }
        }
    }

    $missingInSpec = @()
    foreach ($p in ($extractedPaths | Select-Object -Unique)) {
        # Normalize path
        $normPath = $p -replace '/', '\'
        $fullPath = Join-Path 'd:\Projects\jx-server' $normPath
        $exists = Test-Path $fullPath
        $totalPathsChecked++
        if (-not $exists) {
            $missingInSpec += [PSCustomObject]@{
                Spec = $s
                Path = $p
                Normalized = $normPath
            }
            $allMissingPaths += [PSCustomObject]@{
                Spec = $s
                Path = $p
                Normalized = $normPath
            }
        }
    }

    $taskStats += [PSCustomObject]@{
        Spec = $s
        CompletedTasks = $completedTasks
        UncompletedTasks = $uncompletedTasks
        UniquePathsReferenced = ($extractedPaths | Select-Object -Unique).Count
        MissingPathsCount = $missingInSpec.Count
        Status = if ($uncompletedTasks -eq 0 -and $missingInSpec.Count -eq 0) { "CLEAN" } else { "VIOLATION" }
    }
}

$taskStats | Format-Table -AutoSize
Write-Host "Total Tasks Across 12 Specs: $totalTasks"
Write-Host "Total Path References Checked: $totalPathsChecked"
Write-Host "Total Missing Paths: $($allMissingPaths.Count)"

if ($allMissingPaths.Count -gt 0) {
    Write-Host "`n--- MISSING PATH DETAILS ---"
    $allMissingPaths | Format-Table -AutoSize
}
