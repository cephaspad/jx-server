$allRepoFiles = Get-ChildItem -Path 'd:\Projects\jx-server' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(\.git|\.agents|bin|obj)\\' }
$repoRelPaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($f in $allRepoFiles) {
    $rel = $f.FullName.Substring('d:\Projects\jx-server\'.Length).Replace('\', '/')
    [void]$repoRelPaths.Add($rel)
}

$taskFiles = Get-ChildItem -Path 'd:\Projects\jx-server\specs' -Recurse -File -Filter "tasks.md" | Where-Object { $_.FullName -notmatch '_archive' }

$otherMismatches = @()

foreach ($tf in $taskFiles) {
    $specName = $tf.Directory.Name
    $lines = Get-Content $tf.FullName -Encoding UTF8
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $matches = [regex]::Matches($line, '`?((?:data|schemas|tools)/[a-zA-Z0-9_\-\./]+?\.(?:json|cs|pack|md|slnx|csproj))`?')
        foreach ($m in $matches) {
            $otherPath = $m.Groups[1].Value
            if (-not $repoRelPaths.Contains($otherPath)) {
                $otherMismatches += [PSCustomObject]@{
                    Spec = $specName
                    LineNo = $i + 1
                    Path = $otherPath
                    Line = $line.Trim()
                }
            }
        }
    }
}

Write-Host "Total data/schemas/tools path mismatches: $($otherMismatches.Count)"
$otherMismatches | Format-Table -AutoSize