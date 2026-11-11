Set-Location 'd:\Projects\jx-server'

Write-Host "=== 4. GROUND TRUTH FIDELITY AUDIT VS _archive_m1_m4 ==="

# Key ground truth invariants:
# 1. Handshake constants: 0x2E6D2399, 0x2E6D23CF
# 2. XOR constants: 0x3042, 0x38BC
# 3. Protocol opcodes:
#    - 0x41 (c2s_login)
#    - 0x37 (c2s_roleselect)
#    - 0x45 (c2s_rolenew)
#    - 0x3A (c2s_roledel)
#    - 0x64 (c2s_roleselect / s2c_objadd)
#    - 0x42 (c2s_logiclogin)
#    - 0x49 (s2c_syncworld / c2s_requestnpc)
#    - 0x43 (s2c_syncend)
#    - 0x4B (c2s_npcwalk) -> 0x50 (s2c_npcwalk)
#    - 0x4C (c2s_npcrun / s2c_syncnpc_full) -> 0x51 (s2c_npcrun)
#    - 0x4D (s2c_syncnpc_min / c2s_npcskill)
#    - 0x4E (c2s_npcjump / s2c_syncnpc_minplayer) -> 0x54 (s2c_npcjump)
#    - 0x71 (c2s_npcsit) -> 0x53 (s2c_npcsit)
#    - 0x35 (s2c_notifyplayerexchange)
#    - 0x5B (c2s_moveitem / s2c_playertalk) -> 0x61 (s2c_moveitem)
#    - 0x8E (s2c_itemautomove)
#    - 0x85 (c2s_divideitem)
#    - 0x59 (c2s_eatitem)
#    - 0x5E (c2s_throwawayitem)
#    - 0x83 (c2s_throwawayallmedicine)
#    - 0x6F (c2s_changeauraskill)
#    - 0x57 (c2s_addbaseattribute / s2c_npcdeath)
#    - 0x5D (s2c_playerattribute)
#    - 0x58 (c2s_applyaddskillpoint)
#    - 0x5E (s2c_playerskilllevel)
#    - 0x74 (c2s_playerrevive) -> 0x89 (s2c_npcrevive)
#    - 0x67 (s2c_objremove)
#    - 0x5A (c2s_pickupitem)
#    - 0x7E (c2s/s2c structured chat)
#    - 0xFB (s2c system notice)
#    - 0x82 / 0x70 / 0x99 (heartbeats / pings)

$invariants = @(
    @{ Name = "Handshake Key Obfuscation 1"; Pattern = "0x2E6D2399"; RequiredSpec = "002" },
    @{ Name = "Handshake Key Obfuscation 2"; Pattern = "0x2E6D23CF"; RequiredSpec = "002" },
    @{ Name = "XOR Cipher Constant 1"; Pattern = "0x3042"; RequiredSpec = "002" },
    @{ Name = "XOR Cipher Constant 2"; Pattern = "0x38BC"; RequiredSpec = "002" },
    @{ Name = "Bishop Login Opcode 0x41"; Pattern = "0x41"; RequiredSpec = "003" },
    @{ Name = "Bishop Roster Opcode 0x37"; Pattern = "0x37"; RequiredSpec = "003" },
    @{ Name = "Bishop Role New Opcode 0x45"; Pattern = "0x45"; RequiredSpec = "003" },
    @{ Name = "Bishop Role Delete Opcode 0x3A"; Pattern = "0x3A"; RequiredSpec = "003" },
    @{ Name = "World Handoff Ticket Opcode 0x34"; Pattern = "0x34"; RequiredSpec = "003" },
    @{ Name = "Logic Login Opcode 0x42"; Pattern = "0x42"; RequiredSpec = "004" },
    @{ Name = "Sync End Opcode 0x43"; Pattern = "0x43"; RequiredSpec = "004" },
    @{ Name = "Ping Reply Opcode 0x70"; Pattern = "0x70"; RequiredSpec = "004" },
    @{ Name = "AOI Sync Full Opcode 0x4C"; Pattern = "0x4C"; RequiredSpec = "005" },
    @{ Name = "AOI Sync Min Opcode 0x4D"; Pattern = "0x4D"; RequiredSpec = "005" },
    @{ Name = "Walk Wire Check Key 0x24A1"; Pattern = "0x24A1"; RequiredSpec = "006" },
    @{ Name = "Run Wire Check Key 0x1D78"; Pattern = "0x1D78"; RequiredSpec = "006" },
    @{ Name = "NpcWalk C2S 0x4B / S2C 0x50"; Pattern = "0x4B"; RequiredSpec = "006" },
    @{ Name = "NpcRun C2S 0x4C / S2C 0x51"; Pattern = "0x51"; RequiredSpec = "006" },
    @{ Name = "NpcSit C2S 0x71 / S2C 0x53"; Pattern = "0x71"; RequiredSpec = "006" },
    @{ Name = "NotifyPlayerExchange 0x35"; Pattern = "0x35"; RequiredSpec = "007" },
    @{ Name = "SyncWorld 0x49"; Pattern = "0x49"; RequiredSpec = "007" },
    @{ Name = "Little-Endian Port 0x1A0A"; Pattern = "0x1A0A"; RequiredSpec = "007" },
    @{ Name = "SyncItem 0x5F (80B)"; Pattern = "0x5F"; RequiredSpec = "008" },
    @{ Name = "MoveItem C2S 0x5B / S2C 0x61"; Pattern = "0x61"; RequiredSpec = "008" },
    @{ Name = "ItemAutoMove S2C 0x8E"; Pattern = "0x8E"; RequiredSpec = "008" },
    @{ Name = "DivideItem C2S 0x85"; Pattern = "0x85"; RequiredSpec = "008" },
    @{ Name = "EatItem C2S 0x59"; Pattern = "0x59"; RequiredSpec = "008" },
    @{ Name = "Skill Cast 0x4D / 0x87"; Pattern = "0x87"; RequiredSpec = "009" },
    @{ Name = "Aura Skill 0x6F"; Pattern = "0x6F"; RequiredSpec = "009" },
    @{ Name = "Hurt Recoil 0x56"; Pattern = "0x56"; RequiredSpec = "009" },
    @{ Name = "64-Direction Trig Vectors"; Pattern = "64"; RequiredSpec = "009" },
    @{ Name = "Base Attribute Alloc 0x57 / 0x5D"; Pattern = "0x5D"; RequiredSpec = "010" },
    @{ Name = "Skill Point Alloc 0x58 / 0x5E"; Pattern = "0x58"; RequiredSpec = "010" },
    @{ Name = "Player Revive 0x74 / 0x89"; Pattern = "0x74"; RequiredSpec = "010" },
    @{ Name = "ObjAdd 0x64 / ObjRemove 0x67"; Pattern = "0x67"; RequiredSpec = "011" },
    @{ Name = "Player Pickup 0x5A"; Pattern = "0x5A"; RequiredSpec = "011" },
    @{ Name = "Chat Structured 0x7E"; Pattern = "0x7E"; RequiredSpec = "016" },
    @{ Name = "Chat Nearby 0x5B"; Pattern = "0x5B"; RequiredSpec = "016" },
    @{ Name = "System Notice 0xFB"; Pattern = "0xFB"; RequiredSpec = "016" }
)

$results = @()
foreach ($inv in $invariants) {
    $targetDir = Get-ChildItem -Path 'specs' -Directory | Where-Object { $_.Name -like "$($inv.RequiredSpec)*" }
    $found = $false
    if ($targetDir) {
        $hits = Get-ChildItem -Path $targetDir.FullName -Recurse -Filter '*.md' | Select-String -Pattern $inv.Pattern -SimpleMatch
        if ($hits.Count -gt 0) {
            $found = $true
        }
    }
    $results += [PSCustomObject]@{
        Invariant = $inv.Name
        RequiredSpec = $inv.RequiredSpec
        Pattern = $inv.Pattern
        Found = $found
    }
}

$results | Format-Table -AutoSize
$missingInvariants = $results | Where-Object { -not $_.Found }
Write-Host "Total Invariants Checked: $($results.Count)"
Write-Host "Missing Invariants: $($missingInvariants.Count)"
