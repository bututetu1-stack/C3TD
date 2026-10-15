// プレイヤーが読む説明文をまとめて Temp/text_dump.txt に書き出す（文の確かめ用）：
// 敵（性質・倒し方）、作戦（説明・ヒント）、部員（ひとこと・特性・必殺技）、職業、備品・素材、章の言葉
var sb = new System.Text.StringBuilder();
sb.AppendLine("==== 敵 ====");
foreach (var id in GameData.Campaign.AllStages().SelectMany(s => { var st = GameData.Stage(s); return st == null ? new string[0] : st.waves.Select(w => w.enemy); }).Distinct())
{
    var e = GameData.Enemy(id);
    if (e == null) continue;
    sb.AppendLine("[" + e.name + "] " + id);
    sb.AppendLine("  性質: " + EnemyPanel.Traits(e).Replace("\n", " / "));
    sb.AppendLine("  倒し方: " + StageTips.Advice(e));
}
sb.AppendLine("==== 作戦 ====");
foreach (var sid in GameData.Campaign.AllStages())
{
    var st = GameData.Stage(sid);
    if (st == null) continue;
    sb.AppendLine("[" + st.name + "] " + st.description);
    foreach (var h in st.hints) sb.AppendLine("  ヒント " + h.time + "s: " + h.text);
}
sb.AppendLine("==== 部員 ====");
foreach (var c in GameData.CharacterIds().Select(GameData.Character).Where(x => x != null))
{
    sb.AppendLine("[" + c.name + "] " + c.profile);
    if (c.trait != null) sb.AppendLine("  特性 " + c.trait.name + ": " + c.trait.description + (string.IsNullOrEmpty(c.trait.flavor) ? "" : "  (flavor: " + c.trait.flavor + ")"));
    if (c.ultimate != null) sb.AppendLine("  必殺技 " + c.ultimate.name + ": " + c.ultimate.description + (string.IsNullOrEmpty(c.ultimate.flavor) ? "" : "  (flavor: " + c.ultimate.flavor + ")") + (string.IsNullOrEmpty(c.ultimate.line) ? "" : "  「" + c.ultimate.line + "」"));
}
sb.AppendLine("==== 備品 ====");
foreach (var e in GameData.Items.equipment) sb.AppendLine("[" + e.name + "] ★" + e.rarity + " " + Growth.Describe(e, 0) + "  「" + e.description + "」");
foreach (var m in GameData.Items.materials) sb.AppendLine("[" + m.name + "] " + m.description);
System.IO.File.WriteAllText("Temp/text_dump.txt", sb.ToString());
return sb.Length + " chars";
