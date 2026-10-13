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
- M1 完了（2026-10-13）。ステージは `stage_01`（CLAUDE.md の例のまま）と `test_route`（M1 確認用：曲がり道・出撃口2つ・敵4種、ライフ20）。

### 決めたこと・実装の約束
- マス座標は `[列, 行]`、行は**上から0**（マップの文字列と同じ向き）。ワールド座標は1マス=1ユニットで、左下のマスの中心が (0, 0)（`StageMap.CellToWorld`）。
- JSON は自前の `MiniJson` + `JsonMapper` で読む（JsonUtility では配列の配列や辞書が読めないため）。`//` コメントと末尾のカンマを書いてよい。知らないキーは警告を出して無視するので、綴りの間違いはコンソールで分かる。経路は読み込み時に検査する（範囲外・歩けないマスを通る・S で始まり B で終わらない）。
- 勝敗：ライフが0で敗北。敵がすべて出終わって場に残っていなければ、ライフが残っている限り勝利（拠点に着いた敵がいてもよい）。
- 実行時に作るもの：カメラの調整（`CameraFitter`、上の HUD 88px・下の編成バー 180px を空ける）、PanelSettings（`UIFactory`、1920x1080 を高さ基準で拡縮）、図形スプライト（`SpriteFactory`）、TextMeshPro のフォント（`GameFonts`）。
- フォントは Zen角ゴシック New（OFL、`Resources/Fonts`）。UI は UI Toolkit をコードで組み、見た目は `Resources/UI/Theme.uss`。
- エディタは「ドメインのリロードなし」で再生する設定なので、static な値は `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` で毎回リセットする。
- SE のキー（ファイルがなければ無音）：`click`（ボタン）、`life_lost`（拠点に着かれた）、`victory`、`defeat`。BGM はステージの `bgm` に書いたキー。
- テスト用のキー：Space 一時停止、R やり直し、N 次のステージ（最後の次は最初）。

### 気をつけること
- C3 は「サークル」ではなく「部」。表記は「部」「部員」（2026-10-13 オーナー確認）。
- 新しいスクリプトを書いている途中でエディタがコンパイルすると、そのファイルがコンパイル対象から漏れることがある（`The name 'X' does not exist` が消えない）。そのときは `AssetDatabase.DeleteAsset` で消して作り直す。
- スクリプトの改行は LF（`core.autocrlf=false`、`core.eol=lf`）。
- GitHub CLI（`gh`）がこの PC にはない。リポジトリ（https://github.com/bututetu1-stack/C3TD ）は公開リポジトリ。
