$csFiles = Get-ChildItem -Path "d:\Projects\jx-server\src\JXServer.ZoneEngine" -Filter "*.cs" -Recurse

foreach ($file in $csFiles) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    $pattern = 'public\s+(?:sealed\s+)?class\s+(\w+)'
    $m = [System.Text.RegularExpressions.Regex]::Match($text, $pattern)
    if (-not $m.Success) { continue }
    $className = $m.Groups[1].Value

    $propMatches = [System.Text.RegularExpressions.Regex]::Matches($text, 'public\s+[\w\<\>\[\],\?\s]+\s+(\w+)\s*\{\s*get;')
    
    $ctorSigRegex = [System.Text.RegularExpressions.Regex]::new('public\s+' + $className + '\s*\(([^)]*)\)')
    $ctorSigMatch = $ctorSigRegex.Match($text)
    if (-not $ctorSigMatch.Success) { continue }
    
    $afterCtor = $text.Substring($ctorSigMatch.Index + $ctorSigMatch.Length)
    $braceStart = $afterCtor.IndexOf('{')
    if ($braceStart -lt 0) { continue }
    
    $depth = 0
    $bodyEnd = -1
    for ($i = $braceStart; $i -lt $afterCtor.Length; $i++) {
        if ($afterCtor[$i] -eq '{') { $depth++ }
        elseif ($afterCtor[$i] -eq '}') {
            $depth--
            if ($depth -eq 0) {
                $bodyEnd = $i
                break
            }
        }
    }
    if ($bodyEnd -lt 0) { continue }
    $ctorBody = $afterCtor.Substring($braceStart, $bodyEnd - $braceStart + 1)

    foreach ($pm in $propMatches) {
        $propName = $pm.Groups[1].Value
        $assignedPattern = '\b' + $propName + '\s*='
        if (-not ($ctorBody -match $assignedPattern)) {
            Write-Host "Unassigned Prop: $($file.Name) :: ${className}.${propName}"
        }
    }
}
