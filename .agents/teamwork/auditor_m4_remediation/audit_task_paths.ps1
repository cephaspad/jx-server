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

# Index all files in the entire repo for fast lookup
Write-Host "Indexing all files in d:\Projects\jx-server..."
$allRepoFiles = Get-ChildItem -Path 'd:\Projects\jx-server' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(\.git|\.agents|bin|obj)\\' }
$repoFileByName = @{}
$repoFileByRelPath = @{}

foreach ($f in $allRepoFiles) {
    $rel = $f.FullName.Substring('d:\Projects\jx-server\'.Length).Replace('\', '/')
    $name = $f.Name
    $repoFileByRelPath[$rel.ToLowerInvariant()] = $f.FullName
    if (-not $repoFileByName.ContainsKey($name.ToLowerInvariant())) {
        $repoFileByName[$name.ToLowerInvariant()] = @($f.FullName)
    } else {
        $repoFileByName[$name.ToLowerInvariant()] += $f.FullName
    }
}
Write-Host "Indexed $($allRepoFiles.Count) repository files."

$auditResults = @()
$missingSummary = @()
$totalTasksChecked = 0
$totalFilesChecked = 0

foreach ($s in $specs) {
    $taskPath = Join-Path 'specs' (Join-Path $s 'tasks.md')
    $lines = Get-Content $taskPath
    
    $specChecked = 0
    $specMissing = @()

    foreach ($line in $lines) {
        if ($line -notmatch '^\s*-\s*\[[ Xx]\]') { continue }
        $totalTasksChecked++
        
        # Check task completion status
        $isComplete = ($line -match '^\s*-\s*\[[Xx]\]')
        if (-not $isComplete) {
            $specMissing += [PSCustomObject]@{
                Spec = $s
                Issue = "Uncompleted task: $line"
            }
        }

        # Extract backticked elements safely using non-conflicting variable name
        $btMatches = [regex]::Matches($line, '`([^`]+)`')
        $dirInLine = $null
        foreach ($m in $btMatches) {
            $val = $m.Groups[1].Value.Trim()
            if ($val -match '/$' -or $val -match '^(src|tests|tools|schemas|data|artifacts)/') {
                if ($val -notmatch '\.[a-zA-Z0-9]+$') {
                    $dirInLine = $val.TrimEnd('/')
                }
            }
        }

        foreach ($m in $btMatches) {
            $val = $m.Groups[1].Value.Trim()
            
            # Check if this looks like a file name or path
            if ($val -match '\.(cs|json|md|csproj|slnx|pack)$') {
                $specChecked++
                $totalFilesChecked++
                $clean = $val -replace ':\d+$', ''
                
                # Option 1: Direct relative path
                $found = $false
                if ($repoFileByRelPath.ContainsKey($clean.ToLowerInvariant())) {
                    $found = $true
                }
                # Option 2: Combine with dirInLine if available
                elseif ($dirInLine -and $repoFileByRelPath.ContainsKey(($dirInLine + '/' + $clean).ToLowerInvariant())) {
                    $found = $true
                }
                # Option 3: By filename alone
                elseif ($repoFileByName.ContainsKey((Split-Path $clean -Leaf).ToLowerInvariant())) {
                    $found = $true
                }

                if (-not $found) {
                    $specMissing += [PSCustomObject]@{
                        Spec = $s
                        Line = $line
                        ReferencedFile = $clean
                        ContextDir = $dirInLine
                    }
                    $missingSummary += [PSCustomObject]@{
                        Spec = $s
                        ReferencedFile = $clean
                        Line = $line
                    }
                }
            }
        }
    }

    $auditResults += [PSCustomObject]@{
        Spec = $s
        FilesChecked = $specChecked
        MissingCount = $specMissing.Count
        Status = if ($specMissing.Count -eq 0) { "PASS" } else { "FAIL" }
    }
}

Write-Host "`n=== SPEC TASK FILE RESOLUTION AUDIT ==="
$auditResults | Format-Table -AutoSize

Write-Host "Total Tasks Checked: $totalTasksChecked"
Write-Host "Total File References Checked: $totalFilesChecked"

if ($missingSummary.Count -gt 0) {
    Write-Host "`n=== MISSING FILES DETAIL ($($missingSummary.Count)) ==="
    $missingSummary | Format-Table -AutoSize
} else {
    Write-Host "`nAll referenced files exist in repository!"
}
