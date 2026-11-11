Set-Location 'd:\Projects\jx-server'

Write-Host "=== 5. CHEAT PATTERNS & FACADE DETECTION ==="

$srcFiles = Get-ChildItem -Path 'src' -Recurse -Filter '*.cs'
$notImplHits = @()
$todoHits = @()

foreach ($f in $srcFiles) {
    $lines = Get-Content $f.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match 'NotImplementedException') {
            $notImplHits += [PSCustomObject]@{
                File = $f.FullName.Substring('d:\Projects\jx-server\'.Length)
                LineNumber = $i + 1
                Line = $line.Trim()
            }
        }
        if ($line -match '\b(TODO|FIXME|HACK|XXX)\b' -and $line -notmatch 'TODO items in documentation') {
            $todoHits += [PSCustomObject]@{
                File = $f.FullName.Substring('d:\Projects\jx-server\'.Length)
                LineNumber = $i + 1
                Line = $line.Trim()
            }
        }
    }
}

Write-Host "NotImplementedException in src/: $($notImplHits.Count)"
if ($notImplHits.Count -gt 0) {
    $notImplHits | Format-Table -AutoSize
}

Write-Host "TODO/FIXME in src/: $($todoHits.Count)"
if ($todoHits.Count -gt 0) {
    $todoHits | Format-Table -AutoSize
}

# Check for pre-populated result logs or artifacts in workspace
Write-Host "`nChecking for pre-populated result artifacts..."
$prePop = Get-ChildItem -Path '.' -Include '*.log', '*result*.txt', '*output*.txt' -Recurse | Where-Object { $_.FullName -notmatch '\\(\.git|bin|obj|\.agents)\\' }
Write-Host "Pre-populated log artifacts in working dir: $($prePop.Count)"
if ($prePop.Count -gt 0) {
    $prePop | Select-Object FullName
}
