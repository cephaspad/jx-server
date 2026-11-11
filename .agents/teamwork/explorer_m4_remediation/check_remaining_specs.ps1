$specsToCheck = @(
    '002-core-legacy-wire-protocol',
    '003-core-auth-roster-and-leases',
    '009-core-combat-and-missiles',
    '011-core-loot-and-item-generation',
    '016-module-chat-channels'
)

$allRepoFiles = Get-ChildItem -Path 'd:\Projects\jx-server' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(\.git|\.agents|bin|obj)\\' }
$repoRelPaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$repoFileNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

foreach ($f in $allRepoFiles) {
    $rel = $f.FullName.Substring('d:\Projects\jx-server\'.Length).Replace('\', '/')
    [void]$repoRelPaths.Add($rel)
    [void]$repoFileNames.Add($f.Name)
}

$issues = @()

foreach ($s in $specsToCheck) {
    $dir = Join-Path 'd:\Projects\jx-server\specs' $s
    $mds = Get-ChildItem -Path $dir -Recurse -File -Filter "*.md"
    foreach ($m in $mds) {
        $lines = Get-Content $m.FullName -Encoding UTF8
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            
            # Check explicit paths
            $explicitMatches = [regex]::Matches($line, '`((?:src|tests|tools|schemas|data|artifacts)/[a-zA-Z0-9_\-\./]+?\.(?:cs|json|pack|md|csproj|slnx))`')
            foreach ($em in $explicitMatches) {
                $p = $em.Groups[1].Value.Trim()
                if (-not $repoRelPaths.Contains($p)) {
                    $issues += [PSCustomObject]@{
                        Spec = $s
                        File = $m.Name
                        LineNo = $i + 1
                        Type = "ExplicitPath"
                        Token = $p
                        Line = $line.Trim()
                    }
                }
            }
            
            # Check .cs tokens
            $csMatches = [regex]::Matches($line, '`([a-zA-Z0-9_\.]+\.cs)`')
            foreach ($cm in $csMatches) {
                $csName = $cm.Groups[1].Value.Trim()
                if (-not $repoFileNames.Contains($csName)) {
                    $issues += [PSCustomObject]@{
                        Spec = $s
                        File = $m.Name
                        LineNo = $i + 1
                        Type = "CsFileName"
                        Token = $csName
                        Line = $line.Trim()
                    }
                }
            }
        }
    }
}

Write-Host "Total issues in 002, 003, 009, 011, 016: $($issues.Count)"
$issues | Format-Table -AutoSize