# エディタで使う小さなスクリプト

`isuzu-unity-cli call execute_code --file <ファイル>` で流す。

| ファイル | 使いどころ |
|---|---|
| `cap_setup.cs` | 画面の確認の準備。PanelSettings とカメラの両方に RenderTexture（1920x1080）を設定する。このあと `play_mode_step --seconds 0.3` などで数フレーム進める |
| `cap_save_template.cs` | 画面を PNG で保存する。`OUTPATH` を保存先（`/` 区切りの絶対パス）に書き換えたコピーを作って流す |

画面の確認の流れ（bash）:

```bash
isuzu-unity-cli call play_mode_play
isuzu-unity-cli call play_mode_step --seconds 6
isuzu-unity-cli call execute_code --file docs/snippets/cap_setup.cs
isuzu-unity-cli call play_mode_step --seconds 0.3
sed 's#OUTPATH#C:/tmp/shot.png#' docs/snippets/cap_save_template.cs > /tmp/shot.cs
isuzu-unity-cli call execute_code --file /tmp/shot.cs
isuzu-unity-cli call play_mode_stop
```

- エディタが背面にあると時間が進まないので、プレイ中は `play_mode_step` で進める。`--seconds` は `Time.time` で数えるので、2倍速ならゲーム内でも同じ秒数進む。
- 再生直後の1回目の `play_mode_step --seconds` は、待っていたあいだの実時間で `Time.time` が先に進んでいて、ほとんど進まないことがある。状態は `Battle.Current.State` で確かめる。
- ステージを変えるには `UnityEngine.Object.FindAnyObjectByType<GameBootstrap>().StartStage("stage_02");`。
- コンパイルの確認は `isuzu-unity-cli verify`。

## 戦闘の確かめ（M2）

| ファイル | 使いどころ |
|---|---|
| `state.cs` | いまの出撃の状態（時間・DP・ライフ・部員の HP とブロック数・敵の HP・編成の待ち時間）を1行で返す |
| `deploy.cs` | 部員を指定のマスに置く（`ID`・`X`・`Y` を書き換えて使う） |
| `mouse.cs` | Input System にマウスの状態を流し込む（ドラッグ配置の確かめ） |

- エディタが前面にないと UI Toolkit はクリックを受け付けない（アプリにフォーカスがないため）。カードを押す部分は `BattleInput.Instance.BeginDrag(Battle.Current.Squad[1])` で代わりに呼び、マスへの移動と離すのは `mouse.cs` で流す。
- `input_pointer` の合成イベントは Input System に届かない（マウスの位置が変わらない）。
- 部員を倒すのを確かめるときは `c.TakeDamage(999999, DamageType.Physical)`。
