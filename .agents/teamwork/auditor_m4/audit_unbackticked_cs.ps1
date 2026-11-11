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

$allRepoCs = Get-ChildItem -Path 'd:\Projects\jx-server' -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(\.git|bin|obj|\.agents)\\' }
$repoCsNames = $allRepoCs | Select-Object -ExpandProperty Name

Write-Host "Scanning for any un-backticked .cs references in tasks.md..."

$rawFindings = @()
foreach ($s in $specs) {
    $tPath = Join-Path 'specs' (Join-Path $s 'tasks.md')
    $lines = Get-Content $tPath
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -notmatch '^\s*-\s*\[') { continue }
        
        # Match any token ending in .cs
        $csTokens = [regex]::Matches($line, '([a-zA-Z0-9_\.]+\.cs)\b')
        foreach ($token in $csTokens) {
            $name = Split-Path ($token.Groups[1].Value) -Leaf
            if ($name -notin $repoCsNames) {
                $rawFindings += [PSCustomObject]@{
                    Spec = $s
                    LineNo = $i + 1
                    Token = $token.Groups[1].Value
                    Line = $line.Trim()
                }
            }
        }
    }
}

Write-Host "Non-existent .cs references found: $($rawFindings.Count)"
$rawFindings | Format-Table -AutoSize
