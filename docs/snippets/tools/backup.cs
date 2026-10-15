// テストで戦う前に：オーナーのセーブ（クリアの記録・編成・育成・ガチャ）を c3td.backup に取っておく
string p = PlayerPrefs.GetString("c3td.progress.v1", "");
PlayerPrefs.SetString("c3td.backup", p);
PlayerPrefs.Save();
return p.Length + " chars backed up";
