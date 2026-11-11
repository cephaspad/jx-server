$gitFiles = git -C 'd:\Projects\jx-server' ls-tree -r --name-only HEAD specs/
Write-Host "Total tracked spec files in git HEAD: $($gitFiles.Count)"
$diffs = 0
foreach ($gf in $gitFiles) {
    $archivedRel = $gf -replace '^specs/', 'specs/_archive_m1_m4/'
    $archivedPath = "d:\Projects\jx-server\$archivedRel"
    if (-not (Test-Path $archivedPath)) {
        Write-Host "Missing in archive: $archivedPath"
        $diffs++
        continue
    }
    $archivedHash = (git -C 'd:\Projects\jx-server' hash-object $archivedPath).Trim()
    $headHash = (git -C 'd:\Projects\jx-server' rev-parse "HEAD:$gf").Trim()
    if ($archivedHash -ne $headHash) {
        Write-Host "Mismatch in $gf : HEAD $headHash vs ARCHIVE $archivedHash"
        $diffs++
    }
}
Write-Host "Tracked files comparison done. Total git diffs: $diffs"

# Now verify worker's before_hashes.xml against after
$beforeXmlPath = "d:\Projects\jx-server\.agents\teamwork\worker_m1\before_hashes.xml"
if (Test-Path $beforeXmlPath) {
    $before = Import-Clixml $beforeXmlPath
    Write-Host "before_hashes.xml contains $($before.Count) files."
    $hashMismatches = 0
    foreach ($b in $before) {
        $rel = $b.FullName.Substring("D:\Projects\jx-server\specs\".Length)
        $target = "D:\Projects\jx-server\specs\_archive_m1_m4\$rel"
        if (-not (Test-Path $target)) {
            Write-Host "Missing target for before entry: $rel"
            $hashMismatches++
            continue
        }
        $targetHash = (Get-FileHash -Path $target -Algorithm SHA256).Hash
        $targetLength = (Get-Item -Path $target).Length
        if ($targetHash -ne $b.SHA256 -or $targetLength -ne $b.Length) {
            Write-Host "Mismatch in $rel : Before $($b.SHA256)/$($b.Length) vs After $targetHash/$targetLength"
            $hashMismatches++
        }
    }
    Write-Host "Total SHA-256 mismatches against before_hashes.xml: $hashMismatches"
} else {
    Write-Host "Warning: before_hashes.xml not found at $beforeXmlPath"
}
