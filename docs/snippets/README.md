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
- ステージを変えるには `UnityEngine.Object.FindAnyObjectByType<GameBootstrap>().StartStage("test_route");`。
- コンパイルの確認は `isuzu-unity-cli verify`。
