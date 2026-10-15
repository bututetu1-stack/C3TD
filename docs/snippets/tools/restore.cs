// テストが終わったら（再生を止めてから）：取っておいたセーブを書き戻す
string b = PlayerPrefs.GetString("c3td.backup", null);
if (string.IsNullOrEmpty(b)) return "no backup";
PlayerPrefs.SetString("c3td.progress.v1", b);
PlayerPrefs.DeleteKey("c3td.backup");
PlayerPrefs.Save();
return "restored " + b.Length + " chars";
