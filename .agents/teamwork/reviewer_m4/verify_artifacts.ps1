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

$results = foreach ($s in $specs) {
    $dir = Join-Path 'd:\Projects\jx-server\specs' $s
    $specExists = Test-Path (Join-Path $dir 'spec.md')
    $reqExists = Test-Path (Join-Path $dir 'checklists\requirements.md')
    $planExists = Test-Path (Join-Path $dir 'plan.md')
    $resExists = Test-Path (Join-Path $dir 'research.md')
    $dataExists = Join-Path $dir 'data-model.md' | Test-Path
    $quickExists = Join-Path $dir 'quickstart.md' | Test-Path
    $tasksExists = Join-Path $dir 'tasks.md' | Test-Path
    $contracts = Get-ChildItem -Path (Join-Path $dir 'contracts') -Filter '*.md' -ErrorAction SilentlyContinue
    $contractCount = if ($contracts) { $contracts.Count } else { 0 }
    
    $reqChecked = 0
    $reqTotal = 0
    if ($reqExists) {
        $lines = Get-Content (Join-Path $dir 'checklists\requirements.md')
        $reqChecked = ($lines | Select-String -Pattern '\[[xX]\]').Count
        $reqTotal = ($lines | Select-String -Pattern '\[[ xX]\]').Count
    }
    
    $tasksPending = 0
    $tasksDone = 0
    if ($tasksExists) {
        $lines = Get-Content (Join-Path $dir 'tasks.md')
        $tasksPending = ($lines | Select-String -Pattern '\[ \]').Count
        $tasksDone = ($lines | Select-String -Pattern '\[[xX]\]').Count
    }

    [PSCustomObject]@{
        Spec = $s
        SpecMd = $specExists
        Reqs = "$reqChecked/$reqTotal"
        PlanMd = $planExists
        ResearchMd = $resExists
        DataModelMd = $dataExists
        QuickstartMd = $quickExists
        Contracts = $contractCount
        TasksDone = $tasksDone
        TasksPending = $tasksPending
    }
}

$results | Format-Table -AutoSize
