$allRepoFiles = Get-ChildItem -Path 'd:\Projects\jx-server' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(\.git|\.agents|bin|obj)\\' }
$repoRelPaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$repoFileNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

foreach ($f in $allRepoFiles) {
    $rel = $f.FullName.Substring('d:\Projects\jx-server\'.Length).Replace('\', '/')
    [void]$repoRelPaths.Add($rel)
    [void]$repoFileNames.Add($f.Name)
}

$specDirs = Get-ChildItem -Path 'd:\Projects\jx-server\specs' -Directory | Where-Object { $_.Name -notmatch '^_archive' }

$findings = @()

foreach ($sd in $specDirs) {
    $taskFile = Join-Path $sd.FullName 'tasks.md'
    if (-not (Test-Path $taskFile)) { continue }
    
    $lines = Get-Content $taskFile -Encoding UTF8
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -notmatch '^\s*-\s*\[') { continue }
        
        $backticks = [regex]::Matches($line, '`([^`]+)`')
        foreach ($b in $backticks) {
            $val = $b.Groups[1].Value.Trim()
            
            if ($val -match '/') {
                if ($val -match '\.[a-zA-Z0-9]+$') {
                    if (-not $repoRelPaths.Contains($val)) {
                        $findings += [PSCustomObject]@{
                            Spec = $sd.Name
                            LineNo = $i + 1
                            Category = "BacktickFilePath"
                            Token = $val
                            Line = $line.Trim()
                        }
                    }
                } else {
                    $dirClean = $val.TrimEnd('/')
                    if ($dirClean -ne 'try/catch') {
                        $fullDir = Join-Path 'd:\Projects\jx-server' $dirClean
                        if (-not (Test-Path $fullDir)) {
                            $findings += [PSCustomObject]@{
                                Spec = $sd.Name
                                LineNo = $i + 1
                                Category = "BacktickDirectory"
                                Token = $val
                                Line = $line.Trim()
                            }
                        }
                    }
                }
            }
            elseif ($val -match '\.(cs|json|pack)$') {
                if (-not $repoFileNames.Contains($val)) {
                    $findings += [PSCustomObject]@{
                        Spec = $sd.Name
                        LineNo = $i + 1
                        Category = "BacktickFileName"
                        Token = $val
                        Line = $line.Trim()
                    }
                }
            }
        }
    }
}

$findings | Format-Table -AutoSize