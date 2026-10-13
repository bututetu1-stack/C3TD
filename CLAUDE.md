# CLAUDE.md — C3部員タワーディフェンス（仮）

## プロジェクト概要
- 大学の部「C3」の部員をモチーフにした、**アークナイツ型（マス配置型）タワーディフェンス**。
- **部内だけで遊ぶ作品**。外部への公開や配布はしない。
- 開発体制：プログラムとゲームデザインは Claude Code が担当する。オーナー（物理鉄人）はテストプレイ、部員ネタの提供、素材（イラスト・エフェクト・SE・BGM）の用意を担当する。
- オーナーとのやり取りは**日本語**で行う。

## 技術スタック
- Unity 2D / C#
- Unity バージョン：**6000.3.22f1**（URP 2D）
- Claude Code は Unity MCP 経由でエディタを操作する（パッケージ `jp.shiranui-isuzu.unity-mcp`。シェルから `isuzu-unity-cli` で操作する。手順は `docs/snippets/README.md`）
- 対象：Windows PC、1920x1080、マウス操作
- 新しいパッケージを追加するときは、事前にオーナーに確認する

## 最重要の開発方針：データ駆動
MCP経由のシーン・Prefab操作は失敗しやすいので、**コードとテキストデータで完結する構成**にする。

1. **ステータス・スキル・ステージ・敵はすべて JSON で定義する**（`Assets/Resources/Data/`）。コードに数値を直接書かない。
2. **ステージのマップはテキストから生成する**（形式は後述）。
3. **シーンは最小限にする**。`Boot` シーンに `GameBootstrap` を1つ置き、UIやマップ、ユニットは実行時にコードで生成する。
4. Prefab は必要最小限にとどめ、なるべくコードで生成する。
5. 変更後は MCP でコンソールを確認し、**コンパイルエラーが0であることを確かめてから報告する**。

## 素材の方針
- **最初は仮素材で作る**。色付きの四角や丸、文字、Particle System をコードで生成する。
- 本素材は後から差し替える。ファイル名の規則に合わせて置けば、自動で読み込まれる仕組みにする。
  - 部員アイコン：`Assets/Resources/Art/Icons/{characterId}.png`（正方形、256px以上）
    - ファイルがない場合は、名前の頭文字を入れた色付きの丸で代用する
  - SE：`Assets/Resources/Audio/SE/{key}.wav`、BGM：`Assets/Resources/Audio/BGM/{key}.ogg`
    - ファイルがない場合は無音で動作する
- 素材が必要になったら、**ファイル名・サイズ・用途の一覧**にしてオーナーに依頼する。

## ゲーム仕様

### マップ（テキスト形式）
| 記号 | 意味 |
|---|---|
| `.` | 地上：敵が通る。近接職を配置できる |
| `H` | 高台：敵は通らない。遠距離職を配置できる |
| `X` | 壁：配置できない |
| `S` | 敵の出撃口 |
| `B` | 防衛拠点 |

- 敵の経路は**ステージデータにウェイポイント（マス座標の配列）で指定する**。経路探索アルゴリズムは使わない。

### 基本ルール
- **配置コスト（DP）**：1秒ごとに1回復する。キャラごとに配置コストがある。
- **撤退**：配置したキャラを撤退させると、コストの50%が戻る。再配置までクールタイムがある。
- **ブロック**：地上に置いた近接キャラは、ブロック数の分だけ敵を足止めする。足止めされた敵はそのキャラを攻撃する。
- **ライフ**：敵が拠点に到達するとライフが1減る。0になったら敗北。全ウェーブの敵を倒したら勝利。
- **ステージ経過時間**を常に記録する（時間連動のスキルで使う）。
- 速度切り替え（1倍・2倍）と一時停止がある。
- 操作：画面下の編成バーからアイコンをマスにドラッグして配置する。配置済みのキャラをクリックすると、必殺技の発動ボタンと撤退ボタンが出る。
- キャラの向き（射程の方向）は Phase 2 で導入する。それまでは全方向の射程で実装する。

### 職業
| 職業 | 配置場所 | 役割 |
|---|---|---|
| 前衛 | 地上 | 近接攻撃。ブロック数2 |
| 重装 | 地上 | 高耐久。ブロック数3 |
| 狙撃 | 高台 | 遠距離・単体攻撃 |
| 術師 | 高台 | 遠距離・範囲攻撃 |
| 医療 | 高台 | 味方を回復する |

### スキル（キャラ1人につき2つ）
- **特性（パッシブ）**：常に発動している。部員の性格や内輪ネタを表現する枠。演出はなくてもよい。
- **必殺技（アクティブ）**：SPが溜まったら手動で発動する。持続時間がある。エフェクト・SE・セリフなどの演出はここに集中させる。
- **部品を組み合わせる方式**で実装する。新しい技はJSONを書くだけで追加できるようにする。
  - 効果の部品：`Damage` / `Heal` / `StatModifier`（バフ・デバフ、持続時間つき）/ `Stun` / `Summon` / `MultiHit`
  - 対象：`Self` / `Target` / `AreaAroundSelf` / `AlliesInRange` / `EnemiesInRange`
  - 発動条件（特性用）：`Always` / `ElapsedTime` / `OnDeploy` / `OnAttack` / `OnSkillEnd`
  - SPの溜まり方：`PerSecond` / `OnAttack` / `OnHit`
- **召喚（Summon）**：周りの空いているマスに、時間制限つきでユニットを置く。召喚したユニットのステータスは、召喚者のステータスに連動させられるようにする。

### 敵（テーマ：大学生活の敵）
| ID | 名前 | 特徴 |
|---|---|---|
| kadai | 課題 | 標準的な敵 |
| shotest | 小テスト | 速いが脆い |
| report | レポート | 遅いが硬い |
| shimekiri | 締切 | ボス |

見た目は図形と文字だけで作る。

## データ例

### キャラクター（`Data/Characters/butsuri_tetsujin.json`）
※数値は仮の値。
```json
{
  "id": "butsuri_tetsujin",
  "name": "物理鉄人",
  "class": "Sniper",
  "cost": 12,
  "redeployTime": 60,
  "stats": { "hp": 1200, "atk": 400, "def": 80, "attackInterval": 1.0, "block": 1, "range": 3 },
  "trait": {
    "name": "夜戦主義",
    "description": "時間が経つほど攻撃力が上がる",
    "trigger": "ElapsedTime",
    "params": { "interval": 30 },
    "effects": [ { "type": "StatModifier", "stat": "atk", "percentPerStack": 10, "maxStacks": 5, "target": "Self" } ]
  },
  "ultimate": {
    "name": "深夜出撃",
    "description": "周りの空きマスに艦娘を召喚する。終了後に寝落ちしてスタンする",
    "spCost": 30,
    "spCharge": "PerSecond",
    "duration": 15,
    "effects": [
      { "type": "Summon", "unitId": "kanmusu_token", "count": 3, "target": "AreaAroundSelf", "inheritStatsPercent": 50 }
    ],
    "onEnd": [
      { "type": "Stun", "duration": 3, "target": "Self" }
    ]
  }
}
```

### ステージ（`Data/Stages/stage_01.json`）
```json
{
  "id": "stage_01",
  "name": "1-1 前期の始まり",
  "life": 3,
  "initialDp": 10,
  "map": [
    "XXXXXXXXXX",
    "XHHHHHHHHX",
    "S........B",
    "XHHHHHHHHX",
    "XXXXXXXXXX"
  ],
  "paths": { "p1": [[0,2],[9,2]] },
  "waves": [
    { "time": 3,  "enemy": "kadai",    "path": "p1", "count": 5, "interval": 2 },
    { "time": 20, "enemy": "shotest",  "path": "p1", "count": 3, "interval": 1 }
  ]
}
```

## フォルダ構成（予定）
```
Assets/
  Scenes/Boot.unity
  Scripts/
    Core/        GameBootstrap, GameState, TimeController
    Data/        JSONの読み込み、データクラス
    Map/         MapBuilder, Tile
    Units/       Unit, Character, Enemy, Summon
    Combat/      攻撃、ブロック、ターゲット選択
    Skills/      SkillSystem, Effects/, Triggers/
    UI/          編成バー、ユニットのメニュー、HUD
    Audio/       AudioManager
  Resources/
    Data/  Characters/ Enemies/ Stages/ Summons/
    Art/   Icons/ ...
    Audio/ SE/ BGM/
```

## マイルストーン
1. **M1**：テキストからマップを生成し、敵がウェイポイントに沿って歩く。拠点に到達するとライフが減る。
2. **M2**：DPを使ってキャラを配置できる。攻撃・ブロック・撤退が動き、勝敗が決まる。
3. **M3**：スキルシステム（特性、必殺技、SP、発動UI、召喚）。物理鉄人が仕様どおりに動く。
4. **M4**：複数ステージ、ステージ選択、出撃前の編成画面。
5. **M5**：本素材（アイコン・SE・BGM・エフェクト）を組み込み、演出を強化する。

各マイルストーンが終わったら、オーナーがテストプレイできる状態にし、**確認してほしい点**をまとめて報告する。

## 部員データについて
- 現時点で確定しているのは「物理鉄人」だけ。
- 他の部員はオーナーから情報（ニックネーム、性格、ネタ）をもらってから作る。**勝手に実在の部員の設定を作らない**。それまではダミーキャラ（`dummy_guard` など）でテストする。

## 作業メモ（Claude Code が更新する）

### 進み具合
- M1 完了（2026-10-13）。
- ステージは第1章の5つ（`Data/campaign.json` の順）：1-1 前期の始まり（一本道）、1-2 履修登録（出撃口2つが合流）、1-3 レポート地獄（長い蛇行路・レポート多め）、1-4 小テスト週間（出撃口3つ・小テスト多め）、1-5 締切前夜（ボス）。どれもライフ3。1-5 は M1 確認用の `test_route`（ライフ20で「何もしなくても勝てる」とオーナーに言われた）を直したもの（2026-10-13）。ライフが敵の数以上のステージは読み込み時に警告が出る。数値はどれも仮。
- 見た目を作り直した（2026-10-13、オーナーの「もっと凝ってよい」「文字が荒い」から）。
- M2 完了（2026-10-13）。編成バーからのドラッグ配置・DP・攻撃（近接・弾・範囲・回復）・ブロック・撤退・ダウン・再配置待ち。部員は物理鉄人と、テスト用の `dummy_guard` `dummy_defender` `dummy_caster` `dummy_medic`。特性と必殺技は読み込んで表示するだけ（M3 で動かす）。
- M3 完了（2026-10-13）。スキルの部品・特性・必殺技・SP・発動ボタン・カットイン・召喚。物理鉄人は仕様どおり（夜戦主義：30秒ごとに ATK+10%、5段まで／深夜出撃：SP 30・15秒、周りの空きマスに艦娘3体（召喚者の50%を引き継ぐ）、終わると3秒寝落ち）。セリフ（`line`・`endLine`）は Claude が仮に書いたもので、オーナーが JSON で直してよい。ダミー4人には部品を試すためのテスト技（名前が「テスト：」）を付けてある。
- M4 完了（2026-10-13）。タイトル → 作戦選択 → 編成 → 出撃 → 結果（もう一度／次の作戦／作戦選択へ）。作戦選択は前のステージをクリアすると次が開き、★（ライフを減らさずクリアで3、減らしてクリアで2）を記録する。小さなマップ・出てくる敵・ライフ・DP を出す。編成は全部員から最大8人（`rules.squadMax`）、入れた順に編成バーへ並ぶ。前回の編成とクリア状況は PlayerPrefs（`Progress`、キー `c3td.progress.v1`）に保存。作戦選択の左下に「全作戦を開放（テスト用）」。一時停止はメニュー（再開／やり直す／作戦選択へ）で、マップは触れない。
- 改良（2026-10-13 夕、オーナー不在のあいだ）：自動で戦う `Dev/AutoPilot` で5ステージを確かめ、簡単すぎた後半を強くした（敵の数値と 1-3〜1-5 のウェーブ。結果の表は `docs/snippets/README.md`）。敵が出る2秒前に出撃口で予告（白い輪、ボスは金色）、ボスが場にいるあいだ上にボスの体力バー、敵をクリックすると敵の札（`EnemyPanel`）、1-1 に操作のヒント、部員・敵の札は選んだものと反対の側に出す（左端の出撃口を隠さない）。
- 素材依頼帳の画像は 2026-10-13 に全部受け取った。依頼中は艦娘の砲弾 `Shell.png`、タイトルの絵 `Art/Background/Title.png`（任意。なければ戦闘の背景を使う）、効果音・BGM。

### デザインの方針（オーナーと決めたこと）
- 雰囲気はアークナイツ寄りの「作戦盤」：暗い紺の盤面、角を斜めに落とした板（`ChamferBox`）、琥珀色の差し色、英字の小見出し（OPERATION / TIME / ENEMY / LIFE / DP）。演出は凝ってよい。
- 書体の役割（`GameFonts` の `UIFont`）：Body＝Zen角ゴシック New Bold（日本語の本文・ボタン）、Display＝Oswald Bold（数字と英字）、Title＝しっぽり明朝 B1 ExtraBold（作戦名・勝敗・部員名などの日本語の大見出し）。どれも OFL で、`Resources/Fonts/Licenses` にライセンスがある。
- 文字は UI もマップ上も SDF のフォントアセットを実行時に作って描く（縮小しても荒れない）。
- 画像はオーナーが ChatGPT で作る。頼むときは素材依頼帳（https://claude.ai/artifact/GQmWJRGEXxumur5f64Mfoo 、元は `docs/art-requests.html`）にカードを足して文言を用意する。光るものは黒背景（ゲーム側で黒を透明にする）、物体とキャラは透過PNG、敵は右向き。素材は `Assets/Resources/Art` に直接置かれるので、作業前に `git status` で新しいファイルを確かめる。

### 決めたこと・実装の約束
- マス座標は `[列, 行]`、行は**上から0**（マップの文字列と同じ向き）。ワールド座標は1マス=1ユニットで、左下のマスの中心が (0, 0)（`StageMap.CellToWorld`）。
- JSON は自前の `MiniJson` + `JsonMapper` で読む（JsonUtility では配列の配列や辞書が読めないため）。`//` コメントと末尾のカンマを書いてよい。知らないキーは警告を出して無視するので、綴りの間違いはコンソールで分かる。経路は読み込み時に検査する（範囲外・歩けないマスを通る・S で始まり B で終わらない）。
- データ：`Data/rules.json`（DP の回復・上限、撤退で戻る割合、最低ダメージ、スローの速さ）、`Data/classes.json`（職業ごとの置ける地形・攻撃のしかた・ダメージの種類・標準ブロック数・範囲・弾の画像・色）、`Data/squad.json`（はじめの編成と、編成画面での部員の並び）、`Data/campaign.json`（章とステージの並び）、`Data/Characters/{id}.json`。ステージの `description` は作戦選択の説明。
- 敵の JSON：`res`（術耐性%）、`artScale`（絵の大きさ）、`artFacing`（絵の向き。課題とレポートは `Left`）、`lifeDamage`（拠点に着いたら減るライフ。ボスの締切は 2）。
- ステージの JSON の `hints`：`{ "time": 秒, "duration": 秒, "text": "…" }` の配列。その時刻に画面の左下にヒントが出る（いまは 1-1 だけ）。
- 部員の JSON の追加項目：`projectile`（弾の画像。職業の決まりを上書き）、スキルの `line`（発動時のセリフ。カットインに出る）・`endLine`（終わったときのセリフ。吹き出し）、効果の `radius`（AreaAroundSelf の半径。省略時 1.5）。召喚ユニットは `Data/Summons/{id}.json`（部員と同じ書き方）。
- スキル（`Skills/SkillRunner`・`Skills/SkillEffects` の先頭に一覧あり）：
  - 効果の部品：Damage（攻撃力×multiplier）、Heal（value・multiplier・percent を足す）、StatModifier（stat は atk / def / attackInterval / range / speed / hits。percent と value）、Stun、Summon、MultiHit（hits 回当たる）。
  - 対象：Self / Target / AreaAroundSelf / AlliesInRange / EnemiesInRange。Target と AreaAroundSelf は、Damage・Stun・下げる StatModifier なら敵、Heal・上げる StatModifier なら味方。
  - 効果の長さ：effect.duration があればそれ、なければ必殺技の効果時間（特性なら外すまでずっと）。必殺技で付けた増減は終わりに外す。撤退・ダウンでも外す。
  - 特性の ElapsedTime は「ステージの経過時間」で重なる（遅れて置いても、その時点の重ね数から始まる）。
  - SP：PerSecond（`params.spPerSecond`、省略時 1）/ OnAttack / OnHit。発動中・スタン中は溜まらない。必殺技は出撃中だけ使える。
  - 召喚ユニット：周りの8マス（上・右・下・左・斜め）の空いた地上・高台に置く。地上ならブロックする。撤退はできず、時間切れか倒されると消える。
- 部員を選ぶと、駒の右上に「必殺技」（SP が溜まると琥珀色。溜まるまでは SP の値、発動中は残り秒）と「撤退 +N」のボタンが出る。必殺技を使うと斜めの帯のカットイン（アイコン・技の名前・セリフ）。
- ステージの `name` は「1-1 前期の始まり」のように番号と題名を空白で区切る（HUD と作戦開始の帯で分けて出す）。
- 戦闘の決まり（アークナイツに合わせた）：
  - 射程は「マスの中心どうしの距離 ≤ range」のマス（Phase 2 で向きを入れるまでは全方向）。敵はいまいるマスで判定する。
  - 狙う順：自分が止めている敵 → 射程内でいちばん拠点に近い敵（経路に沿った残りの距離）。医療は射程内で HP の割合がいちばん低い味方。
  - ダメージ：物理は 攻撃−防御、術は 攻撃×(1−術耐性%)。どちらも攻撃の 5%（`minDamageRate`）より下がらない。
  - ブロック：地上に置いた部員だけ。敵は部員の中心から 0.6 マスで止まり、止めた部員を攻撃する。部員が撤退・ダウンしたら歩き出す。
  - 撤退は コスト×0.5（切り捨て）が戻る。倒されたら戻らない。どちらも `redeployTime` 秒の再配置待ち。
  - ドラッグ中・選択中は時間が 0.2 倍（`focusTimeScale`）。一時停止中はメニューが出て、配置はできない。
- 実行時に作るもの：カメラの調整（`CameraFitter`、上の HUD 96px・下の編成バー 180px を空ける。揺れもここ）、PanelSettings（`UIFactory`、1920x1080 を高さ基準で拡縮）、図形スプライト（`SpriteFactory`、256px・ミップマップつき）、フォント（`GameFonts`）。
- UI は UI Toolkit をコードで組む。配置・文字の大きさと色は `Resources/UI/Theme.uss`、フォントはコードで付ける。部品は `ChamferBox`、`C3Button`、`IconView`（Painter2D のアイコン）、`UITween`（実時間で動く小さなアニメ）、`DeployBar`（編成バーと DP）、`UnitPanel`（選択中の部員の札と撤退ボタン）、`EnemyPanel`（クリックした敵の札。部員の札と同じ場所で、どちらか一方だけ出る）。出撃前の画面は `MenuUI`（背景と画面の切り替え・Esc で戻る）の下に `TitleScreen`・`StageSelectScreen`・`SquadScreen`。部員のカードは `CharacterCard`（編成バーと編成画面で共通）、作戦選択の小さなマップは `MapPreview`。画面の流れは `GameBootstrap`、クリア状況と編成の保存は `Progress`。
- 描く順番：マス 0〜9、部員 24（敵より手前。大きな敵に止められていても駒が見える）、敵 20、弾 70、演出 80、数字 90、ドラッグ中の影 95。
- 本素材の読み込みは `Art`（`ArtTexture` で縮小して読むので、インポート設定は触らなくてよい）。透過のない画像の背景は、光るもの（`Art/Effects`・出撃口）は明るさに応じて透明に、物の絵（敵・拠点）はふちから続く黒だけを透明にする。置き場所は `Resources/` の下：
  - `Art/Icons/{characterId}.png`（マップ上では丸く切り抜く）、`Art/Enemies/{enemyId}.png`
  - `Art/Map/Spawn.png`・`Base.png`、`Art/Background/Battle.png`
  - `Art/Effects/{名前}.png`。使っている名前：`SpawnBurst` `LeakBurst` `Deploy` `Retreat` `Slash` `Hit` `Arrow` `MagicOrb` `MagicBurst` `Heal` `EnemyDefeat`。M3 用に届いている名前：`SkillReady` `SkillActivate` `BuffAtk` `Sleep` `SummonSplash`（召喚ユニットのアイコンは `Art/Icons/kanmusu_token.png`）
- 演出の部品：`Fx`（場面ごとの演出）、`FxSprite`（一度だけ再生して消える）、`PulseRing`、`Spinner`、`PathFlow`（経路の矢印。拠点からの距離で並べるので、終わりが同じ経路は矢印が重なる）、`FloatingText`（ダメージ・回復の数字）、`HealthBar`。ワールドの演出はゲーム時間（一時停止で止まる）、UI のアニメは実時間。
- エディタは「ドメインのリロードなし」で再生する設定なので、static な値は `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` で毎回リセットする。
- SE のキー（ファイルがなければ無音）：`click` `life_lost` `victory` `defeat` `deploy` `retreat` `hit` `enemy_defeat` `unit_down` `skill` `skill_ready` `summon` `buff`。BGM はステージの `bgm` に書いたキー（いまはどのステージも `battle`）と、メニューの `menu`。
- テスト用のキー：出撃中は Space 一時停止、R やり直し、N 次のステージ（最後の次は最初・編成はそのまま）、Esc・右クリックで選択とドラッグの取り消し。メニューでは Esc で1つ前の画面、タイトルで Space・Enter。Boot シーンの GameBootstrap の `startStage` にステージ id を書くと、タイトルと編成を飛ばしてそこから始まる。
- 確かめ方は `docs/snippets/README.md`（画面の撮影、部員を置く、マウスを流し込む、自動で戦わせてバランスを見る）。ステージや敵の数値を変えたら `AutoPilot` で「必殺技あり／なし」を回して、表を更新する。
- テストで戦うとクリアの記録が PlayerPrefs に残るので、終わったら `Progress.ResetAll()` で消してオーナーに渡す。
- テストの環境（エディタが背面）では、流し込んだマウスの「押した瞬間」「離した瞬間」を取りこぼすことがある。クリックの確かめは、当たり判定（`BattleInput` の `EnemyAt` など）と表示（`Inspect`・`Select`）を分けて呼んで確かめる。

### 気をつけること
- C3 は「サークル」ではなく「部」。表記は「部」「部員」（2026-10-13 オーナー確認）。
- 新しいスクリプトを書いている途中でエディタがコンパイルすると、そのファイルがコンパイル対象から漏れることがある（`The name 'X' does not exist` が消えない）。そのときは `AssetDatabase.DeleteAsset` で消して作り直す。新しいファイルが多いときは、作業用フォルダで全部書いてから一度にコピーする。
- エディタの外から JSON を書き換えたら、再生する前に `AssetDatabase.Refresh()`（または compile_request）で取り込む。取り込まないと古い内容のまま動く。
- コンパイルの前に再生を止める（`play_mode_stop`）。再生中に再コンパイルすると状態が壊れて NullReference が出る。オーナーが再生したままのこともある。
- 破棄された Unity のオブジェクトは `== null` が true になる。「前のフレームの値」と比べるとき（選択中の部員など）は `ReferenceEquals` で比べる。
- `RuntimePanelUtils.ScreenToPanel` / `CameraTransformWorldToPanel` は UI の拡大縮小を反映しないことがあるので、`UIFactory.ScreenToPanel` / `WorldToPanel`（比率で計算）を使う。
- 実行時に作った Oswald のフォントアセットは字詰めが強く掛かりすぎる（「PAUS E」）ので、`getFontFeatures = false` にしている。
- オーナーの PC は表示倍率 150% で、Game ビューは小さい（734x361 ポイント）。Game ビューは 16:9・Low Resolution オフ（実ピクセルで描く）・Play Maximized にした。
- スクリプトの改行は LF（`core.autocrlf=false`、`core.eol=lf`）。
- GitHub CLI（`gh`）がこの PC にはない。リポジトリ（https://github.com/bututetu1-stack/C3TD ）は公開リポジトリ。
