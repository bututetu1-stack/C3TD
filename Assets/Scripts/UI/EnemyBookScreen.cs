using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 敵図鑑。作戦に初めて出てくる順に、章ごとに敵を並べ、選んだ敵の能力・特別な性質・初めて出る作戦を右に出す。
/// 分かれて出る敵・呼ばれて出る敵（下書き・赤ペンなど）は、出す敵のすぐ後ろに並べる。タイトルの「敵図鑑」から開く。
/// </summary>
public class EnemyBookScreen
{
    public readonly VisualElement Root;

    private readonly VisualElement groups;
    private readonly Label countLabel;
    private readonly VisualElement portrait;
    private readonly Label portraitInitial;
    private readonly Label kindLabel;
    private readonly Label nameLabel;
    private readonly Label hpValue;
    private readonly Label atkValue;
    private readonly Label defValue;
    private readonly Label resValue;
    private readonly Label speedValue;
    private readonly Label lifeValue;
    private readonly Label traitsLabel;
    private readonly Label firstLabel;
    private readonly Dictionary<string, VisualElement> chips = new Dictionary<string, VisualElement>();
    private readonly Dictionary<string, string> firstStage = new Dictionary<string, string>();

    public EnemyBookScreen(VisualElement parent, GameBootstrap bootstrap)
    {
        Root = UIFactory.Box("full", "menu-screen");
        MenuUI.Header(Root, "ENEMY FILE", "敵図鑑", () => bootstrap.ShowTitle(), out countLabel);

        var body = UIFactory.Box("squad-body");
        var left = UIFactory.Box("squad-left");
        left.Add(UIFactory.Text("敵を押すと、能力と特別な性質が右に出る", UIFont.Medium, "squad-help"));
        groups = UIFactory.Box("book-groups");
        left.Add(groups);
        body.Add(left);

        var detail = new ChamferBox();
        detail.AddToClassList("squad-detail");
        detail.SetFill(Palette.UiBand).SetStroke(Palette.UiPanelLine, 1f)
            .SetCut(28f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetStripes(Palette.UiStripe, 20f);
        detail.pickingMode = PickingMode.Position;
        var head = UIFactory.Box("unit-head");
        portrait = UIFactory.Box("unit-portrait", "enemy-portrait");
        portraitInitial = UIFactory.Text("", UIFont.Title, "unit-portrait-initial");
        portrait.Add(portraitInitial);
        head.Add(portrait);
        var names = UIFactory.Box("unit-names");
        kindLabel = UIFactory.Text("", UIFont.Body, "unit-class");
        names.Add(kindLabel);
        nameLabel = UIFactory.Text("", UIFont.Title, "unit-name");
        names.Add(nameLabel);
        head.Add(names);
        detail.Add(head);

        var stats = UIFactory.Box("unit-stats");
        hpValue = Stat(stats, "HP");
        atkValue = Stat(stats, "ATK");
        defValue = Stat(stats, "DEF");
        resValue = Stat(stats, "RES");
        speedValue = Stat(stats, "SPEED");
        lifeValue = Stat(stats, "LIFE");
        detail.Add(stats);

        traitsLabel = UIFactory.Text("", UIFont.Medium, "enemy-traits");
        detail.Add(traitsLabel);
        firstLabel = UIFactory.Text("", UIFont.Medium, "book-first");
        detail.Add(firstLabel);
        body.Add(detail);
        Root.Add(body);

        parent.Add(Root);
    }

    /// <summary>図鑑を開く（データを変えても反映されるよう、開くたびに並べ直す）</summary>
    public void Open()
    {
        groups.Clear();
        chips.Clear();
        firstStage.Clear();
        var seen = new HashSet<string>();
        string firstId = null;
        foreach (ChapterData chapter in GameData.Campaign.chapters)
        {
            var ids = new List<string>();
            foreach (string stageId in chapter.stages)
            {
                StageData stage = GameData.Stage(stageId);
                if (stage == null) continue;
                foreach (WaveData w in stage.waves) Collect(w.enemy, stage, ids, seen);
            }
            if (ids.Count == 0) continue;
            AddGroup(chapter.name, ids);
            if (firstId == null) firstId = ids[0];
        }

        // 作戦にまだ出てこない敵
        var rest = new List<string>();
        foreach (TextAsset asset in Resources.LoadAll<TextAsset>("Data/Enemies"))
            if (!seen.Contains(asset.name) && GameData.Enemy(asset.name) != null) rest.Add(asset.name);
        rest.Sort(System.StringComparer.Ordinal);
        if (rest.Count > 0) AddGroup("まだ出てこない敵", rest);

        countLabel.text = "全 " + chips.Count + " 種";
        Select(firstId);
    }

    /// <summary>敵を並びに入れ、分かれて出る敵・呼ばれて出る敵をすぐ後ろに入れる</summary>
    private void Collect(string id, StageData stage, List<string> ids, HashSet<string> seen)
    {
        if (string.IsNullOrEmpty(id) || seen.Contains(id)) return;
        EnemyData d = GameData.Enemy(id);
        if (d == null) return;
        seen.Add(id);
        ids.Add(id);
        firstStage[id] = stage.name;
        if (d.split != null) Collect(d.split.enemy, stage, ids, seen);
        if (d.summon != null) Collect(d.summon.enemy, stage, ids, seen);
    }

    private void AddGroup(string title, List<string> ids)
    {
        var group = UIFactory.Box("book-group");
        group.Add(UIFactory.Text(title, UIFont.Body, "book-group-name"));
        var row = UIFactory.Box("book-row");
        foreach (string id in ids)
        {
            EnemyData d = GameData.Enemy(id);
            var chip = UIFactory.Box("book-chip");
            chip.pickingMode = PickingMode.Position;
            var icon = UIFactory.Box("enemy-icon");
            Sprite art = Art.Enemy(id);
            if (art != null) icon.style.backgroundImage = new StyleBackground(art);
            else
            {
                icon.style.backgroundColor = Palette.Hex(d.color, Palette.EnemyFallback);
                icon.Add(UIFactory.Text(d.Label, UIFont.Body, "enemy-icon-label"));
            }
            if (d.boss) icon.AddToClassList("enemy-icon--boss");
            chip.Add(icon);
            Label nameText = UIFactory.Text(d.name, UIFont.Body, "book-name");
            // 8文字以上（エントリーシートなど）は、最後の1文字だけ次の行に送られないよう小さく
            if (d.name.Length >= 8) nameText.AddToClassList("book-name--long");
            chip.Add(nameText);
            string captured = id;
            chip.AddManipulator(new Clickable(() =>
            {
                AudioManager.PlaySE("click");
                Select(captured);
            }));
            row.Add(chip);
            chips[id] = chip;
        }
        group.Add(row);
        groups.Add(group);
    }

    private void Select(string id)
    {
        foreach (var kv in chips) kv.Value.EnableInClassList("book-chip--on", kv.Key == id);
        EnemyData d = id != null ? GameData.Enemy(id) : null;
        if (d == null) return;

        Color color = Palette.Hex(d.color, Palette.Crimson);
        Sprite art = Art.Enemy(d.id);
        portrait.style.backgroundImage = art != null ? new StyleBackground(art) : new StyleBackground(StyleKeyword.None);
        portrait.style.backgroundColor = art != null ? new Color(1f, 1f, 1f, 0.06f) : color;
        portraitInitial.text = art != null ? "" : d.Label;
        // 2文字以上（期末・ES など）は枠に収まるように小さく
        portraitInitial.style.fontSize = d.Label.Length >= 2 ? 34f : 52f;
        Color frame = d.boss ? Palette.Amber : Palette.Crimson;
        portrait.style.borderLeftColor = portrait.style.borderRightColor = portrait.style.borderTopColor = portrait.style.borderBottomColor = frame;
        kindLabel.text = d.boss ? "ボス" : "敵";
        kindLabel.style.backgroundColor = frame;
        kindLabel.style.color = d.boss ? new Color(0.08f, 0.07f, 0.04f) : Color.white;
        nameLabel.text = d.name;

        hpValue.text = d.hp + (d.shield > 0f ? "+" + d.shield.ToString("0") : "");
        atkValue.text = d.atk.ToString();
        defValue.text = d.def.ToString();
        resValue.text = d.res.ToString("0");
        speedValue.text = d.speed.ToString("0.0#");
        lifeValue.text = "-" + d.lifeDamage;

        string traits = EnemyPanel.Traits(d);
        traitsLabel.text = traits.Length > 0 ? traits : "特別な性質はない";
        string stage;
        firstLabel.text = firstStage.TryGetValue(d.id, out stage) ? "初めて出る作戦：" + stage : "";
    }

    private static Label Stat(VisualElement parent, string caption)
    {
        var item = UIFactory.Box("unit-stat");
        item.Add(UIFactory.Text(caption, UIFont.Display, "unit-stat-caption"));
        var value = UIFactory.Text("", UIFont.Display, "unit-stat-value");
        item.Add(value);
        parent.Add(item);
        return value;
    }
}
