$repoFiles = Get-ChildItem -Path 'd:\Projects\jx-server' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(\.git|\.agents|bin|obj)\\' }
$repoSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$repoNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

foreach ($f in $repoFiles) {
    $rel = $f.FullName.Substring('d:\Projects\jx-server\'.Length).Replace('\', '/')
    [void]$repoSet.Add($rel)
    [void]$repoNames.Add($f.Name)
}

$planFiles = Get-ChildItem -Path 'd:\Projects\jx-server\specs' -Recurse -File -Filter "plan.md" | Where-Object { $_.FullName -notmatch '_archive' }

$treeDiscrepancies = @()

foreach ($pf in $planFiles) {
    $specName = $pf.Directory.Name
    $lines = Get-Content $pf.FullName
    $inTree = $false
    $treeLines = @()
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        
        # Look for filenames in lines with tree branches (+-- or +--)
        if ($line -match '[++]--\s*([a-zA-Z0-9_\-\.]+)(?:\s+#.*)?$') {
            $fileName = $matches[1].Trim()
            if ($fileName -match '\.(cs|json|pack|md|slnx|csproj)$') {
                if (-not $repoNames.Contains($fileName)) {
                    $treeDiscrepancies += [PSCustomObject]@{
                        Spec = $specName
                        PlanLine = $i + 1
                        File = $fileName
                        FullLine = $line.Trim()
                    }
                }
            }
        }
    }
}

Write-Host "Total missing files from plan.md trees: $($treeDiscrepancies.Count)"
$treeDiscrepancies | Format-Table -AutoSize
