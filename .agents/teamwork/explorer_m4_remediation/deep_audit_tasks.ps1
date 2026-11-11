$allSrcFiles = Get-ChildItem -Path 'd:\Projects\jx-server\src' -Recurse -File -Filter "*.cs" | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$allTestsFiles = Get-ChildItem -Path 'd:\Projects\jx-server\tests' -Recurse -File -Filter "*.cs" | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$allRepoFiles = Get-ChildItem -Path 'd:\Projects\jx-server' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(\.git|\.agents|bin|obj)\\' }

$repoRelPaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$repoFileNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

foreach ($f in $allRepoFiles) {
    $rel = $f.FullName.Substring('d:\Projects\jx-server\'.Length).Replace('\', '/')
    [void]$repoRelPaths.Add($rel)
    [void]$repoFileNames.Add($f.Name)
}

# Also collect all type names declared in src and tests
$declaredTypes = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($f in ($allSrcFiles + $allTestsFiles)) {
    # File name without extension is usually the type name
    [void]$declaredTypes.Add([System.IO.Path]::GetFileNameWithoutExtension($f.Name))
    
    # Also parse type declarations
    $content = [System.IO.File]::ReadAllText($f.FullName)
    $typeMatches = [regex]::Matches($content, '\b(?:class|interface|struct|enum|record)\s+([a-zA-Z0-9_]+)\b')
    foreach ($tm in $typeMatches) {
        [void]$declaredTypes.Add($tm.Groups[1].Value)
    }
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
        
        # Check all backticked items
        $backticks = [regex]::Matches($line, '`([^`]+)`')
        foreach ($b in $backticks) {
            $val = $b.Groups[1].Value.Trim()
            
            # If it contains a slash, check as relative path
            if ($val -match '/') {
                # Could be a dir or file
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
                    # Directory check
                    $dirClean = $val.TrimEnd('/')
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
            # If it ends with .cs or .json or .pack
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
            # If it's a PascalCase word (possible class/interface/enum name), check if declared
            elseif ($val -match '^[I]?[A-Z][a-zA-Z0-9]{3,}$' -and $val -notmatch '^(Phase|Task|US\d+|T\d+|Map|Port|Packets|Bishop|GameServer|Tests|True|False|None|Null)$') {
                if (-not $declaredTypes.Contains($val)) {
                    # Check if it's a known non-type word or if it might be a missing type
                    $findings += [PSCustomObject]@{
                        Spec = $sd.Name
                        LineNo = $i + 1
                        Category = "BacktickTypeName"
                        Token = $val
                        Line = $line.Trim()
                    }
                }
            }
        }
    }
}

Write-Host "Total findings in tasks.md: $($findings.Count)"
$findings | Format-Table -AutoSize