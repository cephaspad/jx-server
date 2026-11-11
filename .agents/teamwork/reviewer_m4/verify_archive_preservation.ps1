$archiveDir = 'd:\Projects\jx-server\specs\_archive_m1_m4'
$specsDir = 'd:\Projects\jx-server\specs'

Write-Host "=== ARCHIVE DIRECTORIES ==="
Get-ChildItem -Path $archiveDir -Directory | ForEach-Object {
    Write-Host "Archive Subdir: $($_.Name)"
    Get-ChildItem -Path $_.FullName -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($archiveDir.Length + 1)
        Write-Host "  $rel"
    }
}
