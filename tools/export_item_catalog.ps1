param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$rows = [System.Collections.Generic.List[string]]::new()
$rows.Add("id`tname`tkind`tbranch`tslot`ttier`tpower`tprice`tweight`tdurability`trange`tinterval`tset`teffect`tcapacity`tcarryBonus`tstack`tspeed")
$tiers = @('青铜','白银','黄金','钻石','传说')
$branches = @{ '刀'='blade'; '骑士剑'='sword'; '长枪'='spear'; '匕首'='dagger'; '法杖'='staff'; '重甲'='heavy'; '板甲'='plate'; '轻甲'='light'; '皮甲'='leather'; '法师袍'='robe' }
$kind = ''; $branch = ''; $serial = 0
function Numeric([string]$text) { if ($text -match '-?\d+(\.\d+)?') { return $Matches[0] }; return '0' }
foreach ($line in Get-Content -LiteralPath (Join-Path $ProjectRoot 'docs/装备清单.md')) {
    if ($line -match '^### ([23])\.\d+ ([^（\s]+)') { $kind = if ($Matches[1] -eq '2') { 'Weapon' } else { 'Armor' }; $branch = $branches[$Matches[2]] }
    if (!$line.StartsWith('|') -or !$branch) { continue }
    $v = @($line.Trim('|').Split('|') | ForEach-Object Trim)
    if ($v.Count -lt 12 -or $tiers -notcontains $v[1]) { continue }
    $tier = [array]::IndexOf($tiers, $v[1]); $serial++
    if ($kind -eq 'Weapon') {
        $range = Numeric $v[4]; $interval = Numeric $v[5]; if ($branch -eq 'staff') { $interval = '0.8' }
        $power = $v[2]; $effect = ''
        if ($branch -eq 'staff' -and $power -match '^(\d+)/(\d+)/(\d+)') { $power = $Matches[2]; $effect = "$($Matches[1]),$($Matches[3])" }
        $data = @(('eq_' + $serial.ToString('0000')),$v[0],$kind,$branch,'Weapon',$tier,$power,$v[11],$v[8],$v[9],$range,$interval,'',$effect,0,0,1,0)
    } else {
        $slot = @{ '头'='Head'; '胸'='Chest'; '腿'='Legs' }[$v[2]]
        $speed = ([double](Numeric $v[7]) * 0.05).ToString([cultureinfo]::InvariantCulture)
        $data = @(('eq_' + $serial.ToString('0000')),$v[0],$kind,$branch,$slot,$tier,$v[5],$v[10],$v[6],$v[9],0,0,$v[3],'',0,0,1,$speed)
    }
    $rows.Add(($data -join "`t"))
}
if ($serial -ne 625) { throw "Expected 625 equipment rows, got $serial" }
$extra = @(
@('rig_canvas','帆布胸挂','Rig','','Rig',0,0,80,1,0,0,0,'','',6,0,1,0),
@('rig_tactical','战术胸挂','Rig','','Rig',0,0,260,1,0,0,0,'','',8,0,1,0),
@('pack_canvas','帆布背包','Pack','','Pack',0,0,120,1,0,0,0,'','',16,0,1,0),
@('pack_explorer','探险背包','Pack','','Pack',0,0,420,1,0,0,0,'','',20,15,1,0),
@('potion_hp','小生命药剂','Potion','','Weapon',0,40,35,0.5,0,0,0,'','hp',0,0,99,0),
@('potion_hp_large','大生命药剂','Potion','','Weapon',1,100,80,0.5,0,0,0,'','hp',0,0,99,0),
@('potion_mp','蓝量药剂','Potion','','Weapon',0,30,40,0.5,0,0,0,'','mp',0,0,99,0),
@('potion_attack','狂怒药剂','Potion','','Weapon',1,20,90,0.5,0,0,0,'','attack',0,0,99,0),
@('potion_speed','轻步药剂','Potion','','Weapon',1,20,90,0.5,0,0,0,'','speed',0,0,99,0),
@('potion_defense','铁骨药剂','Potion','','Weapon',1,20,90,0.5,0,0,0,'','defense',0,0,99,0),
@('potion_hp_super','浓缩生命药剂','Potion','','Weapon',2,100,100,0.5,0,0,0,'','hp',0,0,99,0),
@('potion_mp_super','浓缩蓝量药剂','Potion','','Weapon',2,50,100,0.5,0,0,0,'','mp',0,0,99,0),
@('key','地下城钥匙','Key','','Weapon',0,0,150,0.2,0,0,0,'','',0,0,99,0)
)
foreach ($data in $extra) { $rows.Add(($data -join "`t")) }
foreach ($effect in @('return','fireball','detect','shield','strength','cleanse')) {
    $names = @{return='回城卷轴';fireball='火球卷轴';detect='探测卷轴';shield='护盾卷轴';strength='力量卷轴';cleanse='净化卷轴'}
    $rows.Add((@(('scroll_' + $effect),$names[$effect],'Scroll','','Weapon',1,0,160,0.5,0,0,0,'',$effect,0,0,99,0) -join "`t"))
}
for ($tier=0; $tier -lt 5; $tier++) {
    $rows.Add((@(('ore_' + $tier),($tiers[$tier] + '矿石'),'Material','','Weapon',$tier,0,(20 + $tier * 30),2,0,0,0,'','',0,0,99,0) -join "`t"))
    $rows.Add((@(('relic_' + $tier),($tiers[$tier] + '遗物'),'Treasure','','Weapon',$tier,0,(120 + $tier * 180),3,0,0,0,'','',0,0,99,0) -join "`t"))
}
$dest = Join-Path $ProjectRoot 'unity/KnightChronicles/Assets/Resources/Data/items.txt'
[System.IO.Directory]::CreateDirectory((Split-Path $dest)) | Out-Null
[System.IO.File]::WriteAllLines($dest, $rows, [System.Text.UTF8Encoding]::new($false))
Write-Output "Exported $serial equipment and $($rows.Count - 626) support items to $dest"
