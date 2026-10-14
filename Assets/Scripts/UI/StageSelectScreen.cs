using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 作戦選択。章のステージを左に並べ、選んだステージの説明（マップ・敵・ライフ・DP・★）を右に出す。
/// 前のステージをクリアすると次が開く。
/// </summary>
public class StageSelectScreen
{
    public readonly VisualElement Root;

    private readonly GameBootstrap bootstrap;
    private readonly VisualElement chapterTabs;
    private readonly Label chapterEnglish;
    private readonly Label chapterName;
    private readonly VisualElement nodeRow;
    private readonly Label progressLabel;

    // 右の説明
    private readonly Label detailCode;
    private readonly Label detailName;
    private readonly VisualElement detailStars;
    private readonly Label detailDesc;
    private readonly VisualElement detailMap;
    private readonly Label detailLife;
    private readonly Label detailDp;
    private readonly Label detailEnemyTotal;
    private readonly VisualElement detailEnemies;
    private readonly Label detailLock;
    private readonly C3Button goButton;
    private readonly Label unlockToggle;

    private readonly List<KeyValuePair<string, ChamferBox>> nodes = new List<KeyValuePair<string, ChamferBox>>();
    private string selected;

    public StageSelectScreen(VisualElement parent, GameBootstrap bootstrap)
    {
        this.bootstrap = bootstrap;
        Root = UIFactory.Box("full", "menu-screen");
        Label sub;
        MenuUI.Header(Root, "OPERATION SELECT", "作戦選択", () => bootstrap.ShowTitle(), out sub);
        progressLabel = sub;

        var body = UIFactory.Box("select-body");
        var left = UIFactory.Box("select-left");
        chapterTabs = UIFactory.Box("chapter-tabs");
        left.Add(chapterTabs);
        var chapter = UIFactory.Box("chapter-label");
        chapterEnglish = UIFactory.Text("", UIFont.Display, "chapter-english");
        chapter.Add(chapterEnglish);
        chapterName = UIFactory.Text("", UIFont.Title, "chapter-name");
        chapter.Add(chapterName);
        left.Add(chapter);
        nodeRow = UIFactory.Box("node-row");
        left.Add(nodeRow);
        unlockToggle = UIFactory.Text("", UIFont.Medium, "unlock-toggle");
        unlockToggle.pickingMode = PickingMode.Position;
        unlockToggle.AddManipulator(new Clickable(() =>
        {
            Progress.UnlockAll = !Progress.UnlockAll;
            Open(selected);
        }));
        left.Add(unlockToggle);
        body.Add(left);

        var detail = new ChamferBox();
        detail.AddToClassList("select-detail");
        detail.SetFill(Palette.UiBand).SetStroke(Palette.UiPanelLine, 1f)
            .SetCut(28f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetStripes(Palette.UiStripe, 20f);
        detail.pickingMode = PickingMode.Position;
        var head = UIFactory.Box("detail-head");
        detailCode = UIFactory.Text("", UIFont.Display, "detail-code");
        head.Add(detailCode);
        detailStars = StarRow(26f, "detail-stars");
        head.Add(detailStars);
        detail.Add(head);
        detailName = UIFactory.Text("", UIFont.Title, "detail-name");
        detail.Add(detailName);
        detailDesc = UIFactory.Text("", UIFont.Medium, "detail-desc");
        detail.Add(detailDesc);
        detailMap = UIFactory.Box("detail-map");
        detail.Add(detailMap);

        var facts = UIFactory.Box("detail-facts");
        detailLife = Fact(facts, "LIFE");
        detailDp = Fact(facts, "DP");
        detailEnemyTotal = Fact(facts, "ENEMY");
        detail.Add(facts);

        detail.Add(UIFactory.Text("出てくる敵", UIFont.Body, "detail-section"));
        detailEnemies = UIFactory.Box("detail-enemies");
        // 敵の種類が多いとき（とスマホで UI が大きいとき）は、下の「編成へ」に隠れないよう縦に流せるようにする
        var enemiesScroll = new ScrollView(ScrollViewMode.Vertical);
        enemiesScroll.AddToClassList("detail-enemies-scroll");
        enemiesScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        enemiesScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        enemiesScroll.Add(detailEnemies);
        detail.Add(enemiesScroll);

        detailLock = UIFactory.Text("前の作戦をクリアすると開きます", UIFont.Body, "detail-lock");
        detail.Add(detailLock);
        goButton = new C3Button(IconKind.Next, 30f, "編成へ", () =>
        {
            if (selected != null && Progress.IsUnlocked(selected)) bootstrap.ShowSquad(selected);
        });
        goButton.AddToClassList("detail-go");
        goButton.Active = true;
        detail.Add(goButton);
        body.Add(detail);
        Root.Add(body);

        parent.Add(Root);
    }

    /// <summary>作戦選択を開く。focus を選ぶ（開いていなければ、開いている中でいちばん先）</summary>
    public void Open(string focus)
    {
        CampaignData campaign = GameData.Campaign;
        // 指定がなければ、開いている中でいちばん先のステージ（の章）
        if (focus == null)
            foreach (string id in campaign.AllStages())
                if (Progress.IsUnlocked(id)) focus = id;
        ChapterData chapter = focus != null ? campaign.ChapterOf(focus) : null;
        if (chapter == null && campaign.chapters.Count > 0) chapter = campaign.chapters[0];

        nodeRow.Clear();
        nodes.Clear();
        if (chapter == null) return;
        chapterEnglish.text = chapter.english;
        chapterName.text = chapter.name;
        BuildChapterTabs(campaign, chapter);

        string latest = null;
        int cleared = 0;
        for (int i = 0; i < chapter.stages.Count; i++)
        {
            string id = chapter.stages[i];
            StageData stage = GameData.Stage(id);
            if (stage == null) continue;
            if (i > 0)
            {
                var link = UIFactory.Box("node-link");
                link.EnableInClassList("node-link--open", Progress.IsUnlocked(id));
                nodeRow.Add(link);
            }
            ChamferBox node = CreateNode(id, stage, i);
            nodeRow.Add(node);
            nodes.Add(new KeyValuePair<string, ChamferBox>(id, node));
            if (Progress.IsUnlocked(id)) latest = id;
            if (Progress.IsCleared(id)) cleared++;
        }
        progressLabel.text = "クリア " + cleared + "/" + chapter.stages.Count;
        unlockToggle.text = "全作戦を開放（テスト用）：" + (Progress.UnlockAll ? "ON" : "OFF");

        // 開いていないステージを指定されたら、その章で開いている中のいちばん先（章ごと閉じていれば、そのステージを見せる）
        bool focusInChapter = focus != null && chapter.stages.Contains(focus);
        string pick = focusInChapter && (Progress.IsUnlocked(focus) || latest == null) ? focus : latest;
        if (pick == null && chapter.stages.Count > 0) pick = chapter.stages[0];
        Select(pick);
    }

    /// <summary>章を切り替えるタブ（章が2つ以上のときだけ）。まだ開いていない章も中を見られる</summary>
    private void BuildChapterTabs(CampaignData campaign, ChapterData current)
    {
        chapterTabs.Clear();
        UIFactory.SetVisible(chapterTabs, campaign.chapters.Count > 1);
        foreach (ChapterData c in campaign.chapters)
        {
            ChapterData captured = c;
            var tab = UIFactory.Text(c.name, UIFont.Body, "chapter-tab");
            tab.pickingMode = PickingMode.Position;
            tab.EnableInClassList("chapter-tab--on", c == current);
            tab.EnableInClassList("chapter-tab--locked", c.stages.Count == 0 || !Progress.IsUnlocked(c.stages[0]));
            tab.AddManipulator(new Clickable(() =>
            {
                AudioManager.PlaySE("click");
                OpenChapter(captured);
            }));
            chapterTabs.Add(tab);
        }
    }

    private void OpenChapter(ChapterData chapter)
    {
        if (chapter == null || chapter.stages.Count == 0) return;
        string pick = null;
        foreach (string id in chapter.stages)
            if (Progress.IsUnlocked(id)) pick = id;
        Open(pick ?? chapter.stages[0]);
    }

    private ChamferBox CreateNode(string id, StageData stage, int index)
    {
        bool unlocked = Progress.IsUnlocked(id);
        var node = new ChamferBox();
        node.AddToClassList("stage-node");
        if (index % 2 == 1) node.AddToClassList("stage-node--low");
        node.SetCut(16f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight);
        node.pickingMode = PickingMode.Position;

        node.Add(UIFactory.Text(stage.Code, UIFont.Display, "node-code"));
        node.Add(UIFactory.Text(stage.Title, UIFont.Body, "node-name"));
        if (unlocked)
        {
            VisualElement stars = StarRow(20f, "node-stars");
            SetStars(stars, Progress.Stars(id));
            node.Add(stars);
        }
        else
        {
            var lockIcon = new IconView(IconKind.Lock, 26f, Palette.TextSub);
            lockIcon.AddToClassList("node-lock");
            node.Add(lockIcon);
            node.AddToClassList("stage-node--locked");
        }
        node.AddManipulator(new Clickable(() =>
        {
            AudioManager.PlaySE("click");
            Select(id);
        }));
        return node;
    }

    private void Select(string id)
    {
        selected = id;
        foreach (var kv in nodes)
        {
            bool on = kv.Key == id;
            bool unlocked = Progress.IsUnlocked(kv.Key);
            kv.Value.SetFill(on ? new Color(0.24f, 0.18f, 0.06f, 0.95f) : unlocked ? Palette.UiPanel : new Color(0.04f, 0.05f, 0.08f, 0.75f));
            kv.Value.SetStroke(on ? Palette.Amber : Palette.UiPanelLine, on ? 2.5f : 1f);
            kv.Value.SetAccent(on ? Palette.Amber : Color.clear, 6f);
        }
        if (id == null) return;

        StageData stage = GameData.Stage(id);
        if (stage == null) return;
        bool open = Progress.IsUnlocked(id);
        detailCode.text = string.IsNullOrEmpty(stage.Code) ? "--" : stage.Code;
        detailName.text = stage.Title;
        detailDesc.text = stage.description ?? "";
        SetStars(detailStars, Progress.Stars(id));
        detailMap.Clear();
        detailMap.Add(MapPreview.Build(stage, 560f, 210f));

        var counts = new List<KeyValuePair<string, int>>();
        int total = 0;
        foreach (WaveData w in stage.waves)
        {
            total += w.count;
            int k = counts.FindIndex(p => p.Key == w.enemy);
            if (k < 0) counts.Add(new KeyValuePair<string, int>(w.enemy, w.count));
            else counts[k] = new KeyValuePair<string, int>(w.enemy, counts[k].Value + w.count);
        }
        detailLife.text = stage.life.ToString();
        detailDp.text = stage.initialDp.ToString();
        detailEnemyTotal.text = total.ToString();

        detailEnemies.Clear();
        HashSet<string> seenBefore = EnemiesBefore(id);
        // 敵の種類が多いときは3列にして、小さく並べる（下の「編成へ」に隠れないように）
        detailEnemies.EnableInClassList("detail-enemies--many", counts.Count > 6);
        foreach (var kv in counts)
        {
            EnemyData enemy = GameData.Enemy(kv.Key);
            if (enemy != null) detailEnemies.Add(EnemyChip(enemy, kv.Value, !seenBefore.Contains(enemy.id)));
        }

        UIFactory.SetVisible(detailLock, !open);
        goButton.Interactable = open;
    }

    // ================= 部品 =================

    /// <summary>このステージより前の作戦（campaign.json の順）のウェーブに出てくる敵</summary>
    private static HashSet<string> EnemiesBefore(string stageId)
    {
        var seen = new HashSet<string>();
        foreach (string id in GameData.Campaign.AllStages())
        {
            if (id == stageId) break;
            StageData s = GameData.Stage(id);
            if (s == null) continue;
            foreach (WaveData w in s.waves) seen.Add(w.enemy);
        }
        return seen;
    }

    /// <summary>敵の札。この作戦で初めて出る敵には、アイコンの左上に「NEW」</summary>
    private static VisualElement EnemyChip(EnemyData enemy, int count, bool isNew)
    {
        var chip = UIFactory.Box("enemy-chip");
        var icon = UIFactory.Box("enemy-icon");
        Sprite art = Art.Enemy(enemy.id);
        if (art != null)
        {
            icon.style.backgroundImage = new StyleBackground(art);
        }
        else
        {
            icon.style.backgroundColor = Palette.Hex(enemy.color, Palette.EnemyFallback);
            icon.Add(UIFactory.Text(enemy.Label, UIFont.Body, "enemy-icon-label"));
        }
        if (enemy.boss) icon.AddToClassList("enemy-icon--boss");
        chip.Add(icon);
        var text = UIFactory.Box("enemy-text");
        text.Add(UIFactory.Text(enemy.name + (enemy.boss ? "（ボス）" : ""), UIFont.Body, "enemy-name"));
        text.Add(UIFactory.Text("×" + count, UIFont.Display, "enemy-count"));
        chip.Add(text);
        if (isNew) chip.Add(UIFactory.Text("NEW", UIFont.Display, "enemy-new"));
        return chip;
    }

    private static Label Fact(VisualElement parent, string caption)
    {
        var item = UIFactory.Box("detail-fact");
        item.Add(UIFactory.Text(caption, UIFont.Display, "detail-fact-caption"));
        var value = UIFactory.Text("", UIFont.Display, "detail-fact-value");
        item.Add(value);
        parent.Add(item);
        return value;
    }

    private static VisualElement StarRow(float size, string cls)
    {
        var row = UIFactory.Box(cls);
        row.style.flexDirection = FlexDirection.Row;
        for (int i = 0; i < 3; i++) row.Add(new IconView(IconKind.Star, size, Palette.Amber));
        return row;
    }

    private static void SetStars(VisualElement row, int stars)
    {
        for (int i = 0; i < row.childCount; i++)
            ((IconView)row[i]).SetColor(i < stars ? Palette.AmberBright : new Color(1f, 1f, 1f, 0.15f));
    }
}
