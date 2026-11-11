$allFiles = Get-ChildItem -Path 'd:\Projects\jx-server' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(\.git|\.agents|bin|obj)\\' }
$fileSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$nameSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

foreach ($f in $allFiles) {
    $rel = $f.FullName.Substring('d:\Projects\jx-server\'.Length).Replace('\', '/')
    [void]$fileSet.Add($rel)
    [void]$nameSet.Add($f.Name)
}

$specDirs = Get-ChildItem -Path 'd:\Projects\jx-server\specs' -Directory | Where-Object { $_.Name -notmatch '^_archive' }

$results = @()

foreach ($sd in $specDirs) {
    $mdFiles = Get-ChildItem -Path $sd.FullName -Recurse -File -Filter "*.md"
    foreach ($mf in $mdFiles) {
        $lines = Get-Content $mf.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            
            # Pattern 1: Explicit relative paths starting with src/, tests/, tools/, schemas/, data/, artifacts/
            $pathMatches = [regex]::Matches($line, '(?:^|[ `''"\(\[])((?:src|tests|tools|schemas|data|artifacts)/[a-zA-Z0-9_\-\./]+?\.(?:cs|json|pack|md|csproj|slnx|txt|ini|flt))(?:[ `''"\)\]]|$)')
            foreach ($m in $pathMatches) {
                $p = $m.Groups[1].Value.Trim()
                if (-not $fileSet.Contains($p)) {
                    $results += [PSCustomObject]@{
                        Spec = $sd.Name
                        File = $mf.Name
                        LineNo = $i + 1
                        Type = "ExplicitPath"
                        Reference = $p
                        LineText = $line.Trim()
                    }
                }
            }

            # Pattern 2: Standalone .cs files in code blocks or backticks or text (excluding common syntax like .cs\)
            $csMatches = [regex]::Matches($line, '`([a-zA-Z0-9_\.]+\.cs)`')
            foreach ($m in $csMatches) {
                $csName = $m.Groups[1].Value.Trim()
                if ($csName -match '^\.') {
                    $results += [PSCustomObject]@{
                        Spec = $sd.Name
                        File = $mf.Name
                        LineNo = $i + 1
                        Type = "TruncatedCs"
                        Reference = $csName
                        LineText = $line.Trim()
                    }
                } elseif (-not $nameSet.Contains($csName)) {
                    $results += [PSCustomObject]@{
                        Spec = $sd.Name
                        File = $mf.Name
                        LineNo = $i + 1
                        Type = "MissingCsByName"
                        Reference = $csName
                        LineText = $line.Trim()
                    }
                }
            }
        }
    }
}

Write-Host "Total discrepancies found across all spec markdown files: $($results.Count)"
$results | Format-Table -AutoSize
