// マウスを動かす（左ボタンを押したまま）。マスへのドラッグの確かめに使う。TX・TY を書き換えたコピーを作って流す。
// 離すときは WithButton の行を消したコピーを流す。InputSystem.Update() は呼ばない（呼ぶと「このフレームで離した」が取れない）
var cam = Camera.main;
var sp = cam.WorldToScreenPoint(Battle.Current.UnitPosition(new Vector2Int(TX, TY)));
var state = new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(sp.x, sp.y) };
state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true);
UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, state);
return "mouse at " + sp;
