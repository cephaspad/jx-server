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

Write-Host "=== 1. SPEC DIRECTORIES AND ARTIFACT PRESENCE ==="
$results = @()
$allFiles = @()
foreach ($s in $specs) {
    $dir = Join-Path 'specs' $s
    $exists = Test-Path $dir
    $files = if ($exists) { Get-ChildItem -Path $dir -Recurse -File } else { @() }
    $allFiles += $files

    $hasSpec = Test-Path (Join-Path $dir 'spec.md')
    $hasPlan = Test-Path (Join-Path $dir 'plan.md')
    $hasTasks = Test-Path (Join-Path $dir 'tasks.md')
    $hasResearch = Test-Path (Join-Path $dir 'research.md')
    $hasDataModel = Test-Path (Join-Path $dir 'data-model.md')
    $hasQuickstart = Test-Path (Join-Path $dir 'quickstart.md')
    $hasChecklist = Test-Path (Join-Path $dir 'checklists\requirements.md')
    $contracts = if (Test-Path (Join-Path $dir 'contracts')) { Get-ChildItem (Join-Path $dir 'contracts') -Filter '*.md' } else { @() }
    
    $emptyFiles = $files | Where-Object { $_.Length -eq 0 }

    $results += [PSCustomObject]@{
        Spec = $s
        Exists = $exists
        FileCount = $files.Count
        SpecMd = $hasSpec
        PlanMd = $hasPlan
        TasksMd = $hasTasks
        ResearchMd = $hasResearch
        DataModelMd = $hasDataModel
        QuickstartMd = $hasQuickstart
        ChecklistMd = $hasChecklist
        Contracts = $contracts.Count
        EmptyFiles = $emptyFiles.Count
    }
}

$results | Format-Table -AutoSize

Write-Host "Total Files across 12 specs: $($allFiles.Count)"
$zeroLength = $allFiles | Where-Object { $_.Length -eq 0 }
Write-Host "Zero-length files count: $($zeroLength.Count)"

Write-Host "`n=== 2. CHECKLISTS REQUIREMENTS (16/16 [x]) ==="
$checklistResults = @()
foreach ($s in $specs) {
    $chkPath = Join-Path 'specs' (Join-Path $s 'checklists\requirements.md')
    if (Test-Path $chkPath) {
        $lines = Get-Content $chkPath
        $checked = ($lines | Select-String -Pattern '^\s*-\s*\[x\]' -CaseSensitive:$false).Count
        $unchecked = ($lines | Select-String -Pattern '^\s*-\s*\[ \]' -CaseSensitive:$false).Count
        $totalCheckboxes = $checked + $unchecked
        $checklistResults += [PSCustomObject]@{
            Spec = $s
            Checked = $checked
            Unchecked = $unchecked
            Total = $totalCheckboxes
            Valid = ($checked -eq 16 -and $unchecked -eq 0)
        }
    } else {
        $checklistResults += [PSCustomObject]@{
            Spec = $s
            Checked = 0
            Unchecked = 0
            Total = 0
            Valid = $false
        }
    }
}
$checklistResults | Format-Table -AutoSize
