$repoFiles = Get-ChildItem -Path 'd:\Projects\jx-server' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(\.git|\.agents|bin|obj)\\' }
$repoNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($f in $repoFiles) { [void]$repoNames.Add($f.Name) }

$planFiles = Get-ChildItem -Path 'd:\Projects\jx-server\specs' -Recurse -File -Filter "plan.md" | Where-Object { $_.FullName -notmatch '_archive' }

$treeDiscrepancies = @()

foreach ($pf in $planFiles) {
    $specName = $pf.Directory.Name
    $lines = Get-Content $pf.FullName -Encoding UTF8
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        
        # Match any line with a file extension
        if ($line -match '([a-zA-Z0-9_\-\.]+\.(?:cs|json|pack|md|slnx|csproj))\b') {
            $fileName = $matches[1]
            if (-not $repoNames.Contains($fileName)) {
                $treeDiscrepancies += [PSCustomObject]@{
                    Spec = $specName
                    LineNo = $i + 1
                    FileName = $fileName
                    Line = $line.Trim()
                }
            }
        }
    }
}

Write-Host "Total missing file tokens in all plan.md files: $($treeDiscrepancies.Count)"
$treeDiscrepancies | Format-Table -AutoSize