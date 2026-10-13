// 部員を置く。ID・X・Y を書き換えたコピーを作って流す（例: sed 's/ID/dummy_guard/; s/X, Y/4, 2/' deploy.cs > d.cs）
var b = Battle.Current;
var m = b.Squad.First(x => x.Data.id == "ID");
var c = b.Deploy(m, new Vector2Int(X, Y));
return (c != null ? "ok" : "FAIL") + " t=" + b.State.ElapsedTime.ToString("F1") + " dp=" + b.State.DpInt;
