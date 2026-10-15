// ガチャの演出を確かめる：部費を足し、天井の手前にして（必ず★3 が出る）10 回引く。
// このあと play_mode_step で進めて tools/snap.sh で撮る。押すのは tools/gacha_tap.cs
var boot = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
boot.ShowGacha();
Progress.AddCoins(5000);
Progress.GachaPity = GameData.Items.gacha.pity - 1;
var menu = UnityEngine.Object.FindAnyObjectByType<MenuUI>();
var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var gacha = menu.GetType().GetField("gacha", bf).GetValue(menu);
gacha.GetType().GetMethod("Pull", bf).Invoke(gacha, new object[]{ 10, false });
return "pulled";
