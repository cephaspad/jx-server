$allRepoFiles = Get-ChildItem -Path 'd:\Projects\jx-server' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(\.git|\.agents|bin|obj)\\' }
$repoRelPaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($f in $allRepoFiles) {
    $rel = $f.FullName.Substring('d:\Projects\jx-server\'.Length).Replace('\', '/')
    [void]$repoRelPaths.Add($rel)
}

$taskFiles = Get-ChildItem -Path 'd:\Projects\jx-server\specs' -Recurse -File -Filter "tasks.md" | Where-Object { $_.FullName -notmatch '_archive' }

$srcMismatches = @()

foreach ($tf in $taskFiles) {
    $specName = $tf.Directory.Name
    $lines = Get-Content $tf.FullName -Encoding UTF8
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $matches = [regex]::Matches($line, '`?(src/[a-zA-Z0-9_\-\./]+?\.cs)`?')
        foreach ($m in $matches) {
            $srcPath = $m.Groups[1].Value
            if (-not $repoRelPaths.Contains($srcPath)) {
                $srcMismatches += [PSCustomObject]@{
                    Spec = $specName
                    LineNo = $i + 1
                    SrcPath = $srcPath
                    Line = $line.Trim()
                }
            }
        }
    }
}

Write-Host "Total src path mismatches: $($srcMismatches.Count)"
$srcMismatches | Format-Table -AutoSize