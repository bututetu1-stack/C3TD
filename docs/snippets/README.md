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

## バランスの確かめ（自動で戦う）

`Assets/Scripts/Dev/AutoPilot.cs` は出撃中の戦闘を自動で戦う開発用の部品。近接職を経路の合流点に、遠距離職を経路を多く射程に入れられる高台に置き、必殺技は溜まったらすぐ使う。

```csharp
var boot = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
boot.StartBattle("stage_05");
var ap = Battle.Current.gameObject.AddComponent<AutoPilot>();
ap.UseSkills = false;   // 必殺技を使わない
ap.TimeScale = 6f;      // 6倍速で回す
```

- 置き場所をよく考えて選ぶので、人が初めて遊ぶよりはずっと上手。これで楽に勝てても、オーナーには難しいことがある（2026-10-13、1-3 で分かった）。初めて遊ぶ人の手応えは、下の「初めて遊ぶ人のまね」で見る。
- 戦うとクリアの記録が残るので、確かめ終わったら `Progress.ResetAll()` で消す。

### 初めて遊ぶ人のまね（何回も戦わせて勝った割合を見る）

`AutoPilot.Novice = true` にすると、DP が足りる部員からばらばらに1人選び、まあまあ良い場所（いちばん良い場所の点数の `NoviceCut` 倍以上）のどこかに置く。置いたあと2秒は次を置かない。`Seed` で選び方が決まる。

`Assets/Scripts/Dev/BalanceRun.cs` は、それを Seed を変えて何回も戦わせ、勝った回数・★3 の回数・残りライフと、負けた回に拠点に着いた敵（何秒に・何が）をまとめる。

```csharp
var boot = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
var br = boot.gameObject.AddComponent<BalanceRun>();
br.Stages = new System.Collections.Generic.List<string> { "stage_03", "stage_04" };
br.Runs = 30;          // Seed 0〜29
br.Novice = true;      // false なら上の上手な置き方（毎回同じ結果なので Runs = 1 でよい）
br.UseSkills = true;
```

- エディタが背面のときは `play_mode_step --seconds 1000` を何回か流して進め、`br.Done` になったら `br.Report` を読む（コンソールにも出る）。1000 秒ぶんで数秒かかる。
- 編成は今の編成（`Progress.Squad`）。変えるときは先に `boot.SetSquad(...)` する。
- 負けた回の「拠点に着いた敵」で、何が原因か分かる。1-3・1-4 は、同じ敵が短い間隔で固まって来て、数秒でライフが全部なくなっていた。

## 結果（2026-10-13、デスクトップで 1-3・1-4 を調整したあと。編成：物理鉄人＋ダミー4人）

初めて遊ぶ人のまね（必殺技あり、30回）:

| ステージ | 勝ち | ★3 | 調整の前（20回） |
|---|---|---|---|
| 1-1 | 30/30 | 20 | 20/20 |
| 1-2 | 30/30 | 27 | 20/20 |
| 1-3 | 28/30 | 16 | 14/20 |
| 1-4 | 21/30 | 8 | 9/20 |
| 1-5 | 16/30 | 7 | 11/20 |

1-5 は、ボス（締切）が拠点に着いたときに減るライフを 2 → 1 にすると 28/30（★3 は 6）。ボスの HP・防御を下げても、ほとんど変わらなかった（負ける回は「序盤に1体抜け＋ボスで2」）。

上手な置き方（1回）:

| ステージ | 必殺技あり | 必殺技なし |
|---|---|---|
| 1-1 | 勝ち ★3 | 勝ち（ライフ残り2） |
| 1-2 | 勝ち ★3 | 勝ち（ライフ残り2） |
| 1-3 | 勝ち ★3 | 勝ち ★3 |
| 1-4 | 勝ち ★3 | 勝ち（ライフ残り2） |
| 1-5 | 勝ち ★3 | 勝ち（ライフ残り2） |

## 音の変換（m4a → mp3）

Unity は m4a（AAC）を読めない（インポートしても音にならない）。Gemini などから m4a で届いたら、`to_mp3.ps1` で mp3 にしてから `Assets/Resources/Audio/` に置く。Windows の Media Foundation を使うので、ffmpeg などは要らない。

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File docs/snippets/to_mp3.ps1 -src 'C:\path\Battle.m4a' -dstDir 'C:\path\out' -dstName battle.mp3
```

- 元の長さとビットレートを表示してから変換する。
- 元の m4a は `Assets` の外（`C:\Users\Kokik\unity\C3TD-audio-originals`）に移して残している。
- BGM のくり返しと、SE の前後の無音を切るのは `AudioManager` がやる。音量や使う部分を変えるときは `Data/audio.json`。

## 第2章の結果（2026-10-14 未明、初めて遊ぶ人のまね・必殺技あり・20回）

「時間切れ」は、置き場所が悪くて敵がなかなか減らない回（負けではない）。

| ステージ | 8人の編成 | 5人の編成 |
|---|---|---|
| 2-1 後期ガイダンス | 20/20（★3 14） | 16/20（★3 9、時間切れ 2） |
| 2-2 ゼミ発表 | 20/20（★3 16） | 20/20（★3 16） |
| 2-3 睡眠不足 | 18/20（★3 18、時間切れ 1） | 18/20（★3 16） |
| 2-4 再履修の悪夢 | 20/20（★3 5） | 20/20（★3 4） |
| 2-5 期末試験 | 14/20（★3 6） | 15/20（★3 8、時間切れ 1） |

- 8人：物理鉄人・前衛・重装・術師・医療・剣豪・足止め・徹甲のダミー（オーナーの編成）。5人：物理鉄人＋ダミー4人（はじめの編成）。
- 2-5 は、はじめの DP を 14→18 にし、最初の課題と通知を減らした（25 秒ごろに通知と課題が重なって抜けていた）。通知の群れは 3 体・3 秒おきまでにした。
- 上手な置き方（8人・必殺技あり、1回）：2-1・2-2・2-3・2-5 勝ち ★3、2-4 勝ち（ライフ残り1）。
