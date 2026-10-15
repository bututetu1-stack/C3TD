using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 遊び方（タイトルから）。初めて遊ぶ部員向けに、勝ち負け・置き方・職業・必殺技・便利な操作・やりこみを札で並べる。
/// 文は SkillText で行に分け、数値に色を付ける。
/// </summary>
public class HelpScreen
{
    public readonly VisualElement Root;

    public HelpScreen(VisualElement parent, GameBootstrap bootstrap)
    {
        Root = UIFactory.Box("full", "menu-screen");
        Label sub;
        MenuUI.Header(Root, "HOW TO PLAY", "遊び方", () => bootstrap.ShowTitle(), out sub);
        sub.text = "アークナイツ風のタワーディフェンス";

        // 職業がいちばん分かりにくいので、くわしい説明へのボタンを上に
        var classButton = new C3Button(IconKind.Next, 24f, "職業をくわしく見る（前衛・重装・狙撃・術師・医療）", () => bootstrap.ShowClassGuide(false));
        classButton.AddToClassList("help-class-button");
        classButton.Active = true;
        Root.Add(classButton);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("help-scroll");
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        var grid = UIFactory.Box("help-grid");
        scroll.Add(grid);
        Root.Add(scroll);

        bool mobile = Application.isMobilePlatform;
        string press = mobile ? "タップ" : "クリック";

        Card(grid, "GOAL", "勝ち負け",
            "敵は赤い出撃口から矢印に沿って歩き、青い拠点を目指す\n" +
            "拠点に着かれるとライフが減り、0 になったら負け\n" +
            "全部の敵を倒せば勝ち。ライフを減らさずに勝つと★3\n" +
            "初めての作戦のヒントと、初めて会う敵の説明が出るあいだは、時間が止まる（設定で出さないこともできる）");
        Card(grid, "DEPLOY", "部員を置く",
            "下のカードをマスまでドラッグして置く（カードを" + press + "してから置くマスを" + press + "してもよい" + (mobile ? "" : "。1〜9 のキーでもカードを選べる") + "）\n" +
            "置くには DP を使う。DP は 1 秒に 1 たまる\n" +
            "カードを動かしている間・部員を選んでいる間は、時間がゆっくりになる");
        Card(grid, "TILE", "置ける場所",
            "暗いマス（地上）：前衛・重装。敵の通り道\n" +
            "明るいマス（高台）：狙撃・術師・医療\n" +
            "カードにマウスを重ねると、置けるマスが光る");
        Card(grid, "BLOCK", "止める",
            "地上の前衛・重装は、敵を止めて戦う\n" +
            "止められる数：前衛 2 体、重装 3 体\n" +
            "空を飛ぶ敵は止められず、前衛・重装の攻撃も届かない（狙撃・術師で落とす）");
        Card(grid, "CLASS", "職業",
            "・前衛：近くの敵を攻撃して止める\n" +
            "・重装：とても硬く、たくさん止める\n" +
            "・狙撃：遠くの敵を 1 体ずつ撃つ（防御の高い敵は苦手）\n" +
            "・術師：術で攻撃する。防御が効かない\n" +
            "・医療：味方の体力を回復する");
        Card(grid, "SKILL", "特性と必殺技",
            "特性はずっと効いている、部員らしい力\n" +
            "必殺技は SP がたまったら、置いた部員を" + press + "して「必殺技」\n" +
            "部員を" + press + "すると、射程と、くわしい札が出る\n" +
            "攻撃する部員は「狙い」で、先頭・最後尾・強い敵・近い敵を選べる\n" +
            "部員の札の「必殺技を自動で使う」を ON にすると、溜まったらすぐ使う");
        Card(grid, "RETREAT", "撤退",
            "置いた部員を" + press + "して「撤退」。コストの半分の DP が戻る\n" +
            "倒されると DP は戻らない。どちらも、また置けるまで待ち時間がある");
        Card(grid, "NEXT", "次の敵",
            "右上の NEXT に、次に来る敵と、あと何秒で来るかが出る\n" +
            "間があいているときに「呼ぶ」を押すと次の敵がすぐ来て、早めた秒数に応じて DP がもらえる\n" +
            "敵を" + press + "すると性質が見られる。負けたときは、結果の画面に倒し方のヒントが出る");
        Card(grid, "MORE", "やりこみ",
            "強襲：クリアした作戦は、作戦選択で「強襲」にできる。ライフ 1・敵が強い\n" +
            "エンドレス：1-5 をクリアすると開く。何波まで守れるかを競う\n" +
            "強化：作戦の★で部室の設備をととのえて、部員を少し強くできる（タイトルの「強化」）\n" +
            "育成：作戦をクリアすると部費と素材が落ちる。部費と素材で部員のレベルを上げ、昇進させる（タイトルの「育成」）\n" +
            "ガチャ：部費で備品を引いて、部員に 1 つ付ける（タイトルの「ガチャ」。課金はない）\n" +
            "結果の画面には部員ごとの活躍と MVP が出る。編成画面で、部員ごとの出撃と MVP の回数が見られる");

        parent.Add(Root);
    }

    private static void Card(VisualElement parent, string english, string title, string body)
    {
        var card = new ChamferBox();
        card.AddToClassList("help-card");
        card.SetFill(Palette.UiPanel).SetStroke(Palette.UiPanelLine, 1f).SetCut(16f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight)
            .SetAccent(Palette.Amber, 4f);
        card.Add(UIFactory.Text(english, UIFont.Display, "help-caption"));
        card.Add(UIFactory.Text(title, UIFont.Title, "help-title"));
        card.Add(UIFactory.Text(SkillText.Format(body), UIFont.Medium, "help-body"));
        parent.Add(card);
    }
}
