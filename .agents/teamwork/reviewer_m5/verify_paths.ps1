$specsDir = "d:\Projects\jx-server\specs"
$root = "d:\Projects\jx-server"
$specs = Get-ChildItem -Path $specsDir -Directory
$missing = @()
$checked = 0
$pattern = '`([^`\r\n]+\.(?:cs|json|md|slnx|csproj))`'

foreach ($s in $specs) {
    $tasksFile = Join-Path $s.FullName "tasks.md"
    $content = Get-Content $tasksFile -Raw
    $matches = [regex]::Matches($content, $pattern)
    foreach ($m in $matches) {
        $relPath = $m.Groups[1].Value.Trim()
        if ($relPath -match '^(src|tests|tools|schemas|data|docs|\.specify)[/\\]') {
            $relPath = $relPath -replace ':\d+.*$', ''
            if ($relPath -match '\*') { continue }
            $fullPath = Join-Path $root $relPath
            $checked++
            if (-not (Test-Path $fullPath)) {
                $missing += [PSCustomObject]@{
                    Spec = $s.Name
                    ReferencedPath = $relPath
                }
            }
        }
    }
}

Write-Output "Total paths checked: $checked"
Write-Output "Total missing paths: $($missing.Count)"
if ($missing.Count -gt 0) {
    $missing | Format-Table -AutoSize
}
