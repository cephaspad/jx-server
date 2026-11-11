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

Write-Host "Indexing repo files..."
$allRepoFiles = Get-ChildItem -Path 'd:\Projects\jx-server' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(\.git|\.agents|bin|obj)\\' }
$repoFileByName = @{}
$repoFileByRelPath = @{}

foreach ($f in $allRepoFiles) {
    $rel = $f.FullName.Substring('d:\Projects\jx-server\'.Length).Replace('\', '/')
    $name = $f.Name
    $repoFileByRelPath[$rel.ToLowerInvariant()] = $f.FullName
    $nameLower = $name.ToLowerInvariant()
    if (-not $repoFileByName.ContainsKey($nameLower)) {
        $repoFileByName[$nameLower] = [System.Collections.Generic.List[string]]::new()
    }
    $repoFileByName[$nameLower].Add($f.FullName)
}
Write-Host "Indexed $($allRepoFiles.Count) files."

$allFindings = @()
$specSummaries = @()

foreach ($s in $specs) {
    $taskPath = Join-Path 'specs' (Join-Path $s 'tasks.md')
    $lines = Get-Content $taskPath
    
    $taskCount = 0
    $uncompletedCount = 0
    $referencedFiles = @()
    $specMissingFiles = @()

    foreach ($line in $lines) {
        if ($line -notmatch '^\s*-\s*\[[ Xx]\]') { continue }
        $taskCount++
        
        if ($line -notmatch '^\s*-\s*\[[Xx]\]') {
            $uncompletedCount++
            $allFindings += [PSCustomObject]@{
                Spec = $s
                Type = "UNCOMPLETED_TASK"
                Detail = $line.Trim()
            }
        }

        # Find backtick items
        $regexObj = [regex]'`([^`]+)`'
        $matchCol = $regexObj.Matches($line)
        
        $contextDir = $null
        foreach ($m in $matchCol) {
            $v = $m.Groups[1].Value.Trim()
            if (($v -match '^(src|tests|tools|schemas|data)/' -or $v -match '/$') -and $v -notmatch '\.[a-zA-Z0-9]+$') {
                $contextDir = $v.TrimEnd('/')
            }
        }

        foreach ($m in $matchCol) {
            $v = $m.Groups[1].Value.Trim()
            # If it looks like a code/schema/data/test file
            if ($v -match '\.(cs|json|md|csproj|slnx)$') {
                # Ignore markdown links or spec.md/plan.md self-references
                if ($v -in @('spec.md', 'plan.md', 'tasks.md', 'research.md', 'data-model.md', 'quickstart.md')) {
                    continue
                }

                $clean = $v -replace ':\d+$', ''
                $referencedFiles += $clean

                $resolved = $false
                # Check 1: direct relative path from repo root
                if ($repoFileByRelPath.ContainsKey($clean.ToLowerInvariant())) {
                    $resolved = $true
                }
                # Check 2: relative to contextDir
                elseif ($contextDir -and $repoFileByRelPath.ContainsKey(($contextDir + '/' + $clean).ToLowerInvariant())) {
                    $resolved = $true
                }
                # Check 3: by leaf file name anywhere in repo
                elseif ($repoFileByName.ContainsKey((Split-Path $clean -Leaf).ToLowerInvariant())) {
                    $resolved = $true
                }

                if (-not $resolved) {
                    $finding = [PSCustomObject]@{
                        Spec = $s
                        Type = "MISSING_OR_HALLUCINATED_FILE"
                        Referenced = $clean
                        ContextDir = $contextDir
                        TaskLine = $line.Trim()
                    }
                    $specMissingFiles += $finding
                    $allFindings += $finding
                }
            }
        }
    }

    $specSummaries += [PSCustomObject]@{
        Spec = $s
        Tasks = $taskCount
        Uncompleted = $uncompletedCount
        FilesReferenced = ($referencedFiles | Select-Object -Unique).Count
        MissingFiles = $specMissingFiles.Count
        Verdict = if ($uncompletedCount -eq 0 -and $specMissingFiles.Count -eq 0) { "CLEAN" } else { "VIOLATION" }
    }
}

Write-Host "`n=== DETAILED TASK AUDIT BY SPEC ==="
$specSummaries | Format-Table -AutoSize

Write-Host "`n=== ALL VIOLATIONS FOUND ($($allFindings.Count)) ==="
$allFindings | Format-Table -AutoSize
