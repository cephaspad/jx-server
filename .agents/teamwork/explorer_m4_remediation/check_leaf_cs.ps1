$allRepoFiles = Get-ChildItem -Path 'd:\Projects\jx-server' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(\.git|\.agents|bin|obj)\\' }
$repoFileNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($f in $allRepoFiles) {
    [void]$repoFileNames.Add($f.Name)
}

$taskFiles = Get-ChildItem -Path 'd:\Projects\jx-server\specs' -Recurse -File -Filter "tasks.md" | Where-Object { $_.FullName -notmatch '_archive' }

$csNameMismatches = @()

foreach ($tf in $taskFiles) {
    $specName = $tf.Directory.Name
    $lines = Get-Content $tf.FullName -Encoding UTF8
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $matches = [regex]::Matches($line, '([a-zA-Z0-9_\.]+\.cs)\b')
        foreach ($m in $matches) {
            $name = Split-Path ($m.Groups[1].Value) -Leaf
            if ($name -match '^\.') {
                $csNameMismatches += [PSCustomObject]@{
                    Spec = $specName
                    LineNo = $i + 1
                    Type = "TruncatedCs"
                    FileName = $name
                    Line = $line.Trim()
                }
            }
            elseif (-not $repoFileNames.Contains($name)) {
                $csNameMismatches += [PSCustomObject]@{
                    Spec = $specName
                    LineNo = $i + 1
                    Type = "NonExistentCs"
                    FileName = $name
                    Line = $line.Trim()
                }
            }
        }
    }
}

Write-Host "Total filename-only .cs mismatches: $($csNameMismatches.Count)"
$csNameMismatches | Format-Table -AutoSize