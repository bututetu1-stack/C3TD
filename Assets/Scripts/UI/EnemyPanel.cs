using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// クリックした敵の情報（左の札）。部員の札と同じ場所・同じ見た目で、どちらか一方だけが出る。
/// </summary>
public class EnemyPanel
{
    private readonly ChamferBox info;
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
    private readonly Label stateLabel;
    private Enemy shown;
    private string shownText;
    private Enemy placedFor;

    public EnemyPanel(VisualElement root)
    {
        info = new ChamferBox();
        info.AddToClassList("unit-info");
        info.SetFill(Palette.UiBand).SetStroke(Palette.UiPanelLine, 1f)
            .SetCut(24f, ChamferBox.Corners.TopRight | ChamferBox.Corners.BottomLeft)
            .SetStripes(Palette.UiStripe, 18f);
        info.pickingMode = PickingMode.Position;

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
        info.Add(head);

        var stats = UIFactory.Box("unit-stats");
        hpValue = AddStat(stats, "HP");
        atkValue = AddStat(stats, "ATK");
        defValue = AddStat(stats, "DEF");
        resValue = AddStat(stats, "RES");
        speedValue = AddStat(stats, "SPEED");
        lifeValue = AddStat(stats, "LIFE");
        info.Add(stats);

        stateLabel = UIFactory.Text("", UIFont.Medium, "enemy-state");
        info.Add(stateLabel);

        root.Add(info);
        UIFactory.SetVisible(info, false);
    }

    private static Label AddStat(VisualElement parent, string caption)
    {
        var item = UIFactory.Box("unit-stat");
        item.Add(UIFactory.Text(caption, UIFont.Display, "unit-stat-caption"));
        var value = UIFactory.Text("", UIFont.Display, "unit-stat-value");
        item.Add(value);
        parent.Add(item);
        return value;
    }

    public void Refresh(Enemy e)
    {
        // 倒された敵は破棄されて Unity では null と等しくなるので、参照そのもので比べる
        if (e == null || !e.Alive) e = null;
        if (!ReferenceEquals(e, shown))
        {
            shown = e;
            shownText = null;
            if (e != null) Fill(e);
            UIFactory.SetVisible(info, e != null);
        }
        if (e == null) return;
        UnitPanel.PlaceOpposite(info, e.Center, ReferenceEquals(e, placedFor));
        placedFor = e;

        // 体力と、必殺技などで変わる数値・状態は毎フレーム確かめる（変わったときだけ書き換える）
        string text = Mathf.CeilToInt(e.Hp) + "|" + Mathf.RoundToInt(e.Atk) + "|" + Mathf.RoundToInt(e.Def) + "|" + e.Stunned + "|" + (e.BlockedBy != null ? e.BlockedBy.Data.name : "");
        if (text == shownText) return;
        shownText = text;
        hpValue.text = Mathf.CeilToInt(e.Hp).ToString();
        atkValue.text = Mathf.RoundToInt(e.Atk).ToString();
        defValue.text = Mathf.RoundToInt(e.Def).ToString();
        string state = "";
        if (e.Stunned) state = "気絶中";
        else if (e.BlockedBy != null) state = e.BlockedBy.Data.name + "が足止め中";
        stateLabel.text = state;
        UIFactory.SetVisible(stateLabel, state.Length > 0);
    }

    private void Fill(Enemy e)
    {
        EnemyData d = e.Data;
        Color color = Palette.Hex(d.color, Palette.Crimson);
        Sprite art = Art.Enemy(d.id);
        portrait.style.backgroundImage = art != null ? new StyleBackground(art) : new StyleBackground(StyleKeyword.None);
        portrait.style.backgroundColor = art != null ? new Color(1f, 1f, 1f, 0.06f) : color;
        portraitInitial.text = art != null ? "" : d.Label;
        Color frame = d.boss ? Palette.Amber : Palette.Crimson;
        portrait.style.borderLeftColor = portrait.style.borderRightColor = portrait.style.borderTopColor = portrait.style.borderBottomColor = frame;

        nameLabel.text = d.name;
        kindLabel.text = d.boss ? "ボス" : "敵";
        kindLabel.style.backgroundColor = frame;
        kindLabel.style.color = d.boss ? new Color(0.08f, 0.07f, 0.04f) : Color.white;
        resValue.text = Mathf.RoundToInt(e.Res).ToString();
        speedValue.text = d.speed.ToString("0.0#");
        lifeValue.text = "-" + d.lifeDamage;
    }
}
