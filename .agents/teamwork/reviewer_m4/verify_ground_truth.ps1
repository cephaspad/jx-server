$checks = @(
    @{ Spec = '002'; Key = '0x2E6D2399'; Desc = 'Handshake key obfuscation constant 1' },
    @{ Spec = '002'; Key = '0x2E6D23CF'; Desc = 'Handshake key obfuscation constant 2' },
    @{ Spec = '002'; Key = '0x3042'; Desc = 'Linear congruential XOR constant 1' },
    @{ Spec = '002'; Key = '0x38BC'; Desc = 'Linear congruential XOR constant 2' },
    @{ Spec = '003'; Key = '0x00050001'; Desc = 'Bishop return code success' },
    @{ Spec = '003'; Key = '0x0005000B'; Desc = 'Bishop return code role in use' },
    @{ Spec = '004'; Key = 'IZoneModule'; Desc = 'Modular zone interface' },
    @{ Spec = '004'; Key = 'ZoneTickPhase'; Desc = 'Tick phase enum' },
    @{ Spec = '004'; Key = '0x42'; Desc = 'World entry request opcode' },
    @{ Spec = '004'; Key = '0x43'; Desc = 'World entry response opcode' },
    @{ Spec = '005'; Key = '0x4C'; Desc = 'SyncNpcFullS2CPacket opcode (61B)' },
    @{ Spec = '005'; Key = '0x4D'; Desc = 'SyncNpcMinS2CPacket opcode (18B)' },
    @{ Spec = '005'; Key = '0x4E'; Desc = 'SyncNpcMinPlayerS2CPacket opcode (13B)' },
    @{ Spec = '006'; Key = '0x24A1'; Desc = 'Walk anti-cheat key' },
    @{ Spec = '006'; Key = '0x1D78'; Desc = 'Run anti-cheat key' },
    @{ Spec = '006'; Key = '0x50'; Desc = 'Walk S2C opcode' },
    @{ Spec = '006'; Key = '0x51'; Desc = 'Run S2C opcode' },
    @{ Spec = '007'; Key = '0x35'; Desc = 'Notify player exchange S2C opcode' },
    @{ Spec = '007'; Key = '0x1A0A'; Desc = 'Plain LE port 6666' },
    @{ Spec = '007'; Key = 'g_FileName2Id'; Desc = 'Trap script file path hash' },
    @{ Spec = '008'; Key = '0x5F'; Desc = 'SyncItem S2C opcode (80B)' },
    @{ Spec = '008'; Key = '0x8E'; Desc = 'ItemAutoMove S2C opcode' },
    @{ Spec = '008'; Key = 'SViewItemInfo'; Desc = '64-byte item view struct' },
    @{ Spec = '009'; Key = '64'; Desc = '64-direction discrete angles' },
    @{ Spec = '009'; Key = '0x56'; Desc = 'do_hurt opcode' },
    @{ Spec = '010'; Key = '18'; Desc = '18-Hz monster tick rate' },
    @{ Spec = '010'; Key = '0x57'; Desc = 'Player death / attribute sync' },
    @{ Spec = '011'; Key = 'm_wVersion'; Desc = 'KItemGenerator version' },
    @{ Spec = '011'; Key = '0x5A'; Desc = 'Pickup opcode' },
    @{ Spec = '016'; Key = '0x7E'; Desc = 'Multi-channel chat opcode' },
    @{ Spec = '016'; Key = '0x5B'; Desc = 'Local chat speech opcode' },
    @{ Spec = '016'; Key = '0xFB'; Desc = 'System notice opcode' }
)

Write-Host "=== GROUND TRUTH PRESERVATION AUDIT ==="
$passed = 0
$failed = 0

foreach ($c in $checks) {
    $specDir = Get-ChildItem -Path 'd:\Projects\jx-server\specs' -Directory | Where-Object { $_.Name -like "$($c.Spec)-*" }
    if (-not $specDir) {
        Write-Host "FAIL: Spec $($c.Spec) not found!"
        $failed++
        continue
    }
    
    $foundInFiles = @()
    $files = Get-ChildItem -Path $specDir.FullName -Recurse -Filter '*.md'
    foreach ($f in $files) {
        $content = Get-Content $f.FullName -Raw
        if ($content.Contains($c.Key)) {
            $foundInFiles += $f.Name
        }
    }
    
    if ($foundInFiles.Count -gt 0) {
        Write-Host "PASS: [$($c.Spec)] $($c.Key) ($($c.Desc)) found in $($foundInFiles -join ', ')"
        $passed++
    } else {
        Write-Host "FAIL: [$($c.Spec)] $($c.Key) ($($c.Desc)) NOT FOUND in spec $($c.Spec)!"
        $failed++
    }
}

Write-Host "`nResult: $passed Passed, $failed Failed out of $($checks.Count) checks."
