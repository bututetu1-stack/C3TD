// ガチャの演出で「押す」（箱を開ける・スポットライトを閉じる）
var menu = UnityEngine.Object.FindAnyObjectByType<MenuUI>();
var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var gacha = menu.GetType().GetField("gacha", bf).GetValue(menu);
var show = gacha.GetType().GetField("show", bf).GetValue(gacha);
show.GetType().GetField("advance", bf).SetValue(show, true);
return "tap";
