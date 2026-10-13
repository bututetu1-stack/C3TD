using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 選択中の部員の情報（左の札）と、駒の横に出る撤退ボタン。
/// 必殺技のボタンは M3 で足す。
/// </summary>
public class UnitPanel
{
    private readonly VisualElement root;
    private readonly ChamferBox info;
    private readonly VisualElement portrait;
    private readonly Label portraitInitial;
    private readonly Label nameLabel;
    private readonly Label classLabel;
    private readonly Label hpValue;
    private readonly Label atkValue;
    private readonly Label defValue;
    private readonly Label blockValue;
    private readonly Label rangeValue;
    private readonly Label costValue;
    private readonly VisualElement traitBox;
    private readonly Label traitName;
    private readonly Label traitDesc;
    private readonly VisualElement ultBox;
    private readonly Label ultName;
    private readonly Label ultMeta;
    private readonly Label ultDesc;
    private readonly VisualElement actions;
    private readonly C3Button retreatButton;
    private Character shown;
    private int shownHp = -1;

    public UnitPanel(VisualElement root)
    {
        this.root = root;

        info = new ChamferBox();
        info.AddToClassList("unit-info");
        info.SetFill(Palette.UiBand).SetStroke(Palette.UiPanelLine, 1f)
            .SetCut(24f, ChamferBox.Corners.TopRight | ChamferBox.Corners.BottomLeft)
            .SetStripes(Palette.UiStripe, 18f);
        info.pickingMode = PickingMode.Position;

        var head = UIFactory.Box("unit-head");
        portrait = UIFactory.Box("unit-portrait");
        portraitInitial = UIFactory.Text("", UIFont.Title, "unit-portrait-initial");
        portrait.Add(portraitInitial);
        head.Add(portrait);
        var names = UIFactory.Box("unit-names");
        classLabel = UIFactory.Text("", UIFont.Body, "unit-class");
        names.Add(classLabel);
        nameLabel = UIFactory.Text("", UIFont.Title, "unit-name");
        names.Add(nameLabel);
        head.Add(names);
        info.Add(head);

        var stats = UIFactory.Box("unit-stats");
        hpValue = AddStat(stats, "HP");
        atkValue = AddStat(stats, "ATK");
        defValue = AddStat(stats, "DEF");
        blockValue = AddStat(stats, "BLOCK");
        rangeValue = AddStat(stats, "RANGE");
        costValue = AddStat(stats, "COST");
        info.Add(stats);

        Label traitMeta;
        traitBox = AddSkill(info, "特性", out traitName, out traitMeta, out traitDesc);
        UIFactory.SetVisible(traitMeta, false);
        ultBox = AddSkill(info, "必殺技", out ultName, out ultMeta, out ultDesc);

        root.Add(info);

        actions = UIFactory.Box("unit-actions");
        retreatButton = new C3Button(IconKind.Cross, 22f, "撤退", () =>
        {
            if (BattleInput.Instance != null) BattleInput.Instance.RetreatSelected();
        });
        retreatButton.AddToClassList("retreat-button");
        actions.Add(retreatButton);
        root.Add(actions);

        UIFactory.SetVisible(info, false);
        UIFactory.SetVisible(actions, false);
    }

    private Label AddStat(VisualElement parent, string caption)
    {
        var item = UIFactory.Box("unit-stat");
        item.Add(UIFactory.Text(caption, UIFont.Display, "unit-stat-caption"));
        var value = UIFactory.Text("", UIFont.Display, "unit-stat-value");
        item.Add(value);
        parent.Add(item);
        return value;
    }

    private VisualElement AddSkill(VisualElement parent, string kind, out Label name, out Label meta, out Label desc)
    {
        var box = UIFactory.Box("unit-skill");
        var head = UIFactory.Box("unit-skill-head");
        head.Add(UIFactory.Text(kind, UIFont.Body, "unit-skill-kind"));
        name = UIFactory.Text("", UIFont.Body, "unit-skill-name");
        head.Add(name);
        box.Add(head);
        meta = UIFactory.Text("", UIFont.Body, "unit-skill-meta");
        box.Add(meta);
        desc = UIFactory.Text("", UIFont.Medium, "unit-skill-desc");
        box.Add(desc);
        parent.Add(box);
        return box;
    }

    public void Refresh(Character c)
    {
        // 破棄された部員は Unity では null と等しくなるので、参照そのもので比べる
        if (c == null || !c.Alive) c = null;
        if (!ReferenceEquals(c, shown))
        {
            shown = c;
            shownHp = -1;
            if (c != null) Fill(c);
            UIFactory.SetVisible(info, c != null);
            UIFactory.SetVisible(actions, c != null);
        }
        if (c == null) return;

        int hp = Mathf.CeilToInt(c.Hp);
        if (hp != shownHp)
        {
            shownHp = hp;
            hpValue.text = hp + "/" + Mathf.CeilToInt(c.MaxHp);
        }

        // 撤退ボタンを駒の右上に置く
        Camera cam = Camera.main;
        if (cam != null && root.panel != null)
        {
            Vector2 p = UIFactory.WorldToPanel(root, cam, c.transform.position + new Vector3(0.5f, 0.42f, 0f));
            actions.style.left = p.x;
            actions.style.top = p.y - 30f;
        }
    }

    private void Fill(Character c)
    {
        CharacterData d = c.Data;
        Color classColor = c.ClassColor;

        Sprite icon = Art.Icon(d.id);
        portrait.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
        portrait.style.backgroundColor = icon != null ? Color.clear : Palette.Hex(d.color, classColor);
        portraitInitial.text = icon != null ? "" : d.Initial;
        portrait.style.borderLeftColor = portrait.style.borderRightColor = portrait.style.borderTopColor = portrait.style.borderBottomColor = classColor;

        nameLabel.text = d.name;
        classLabel.text = c.Class.name;
        classLabel.style.backgroundColor = classColor;
        atkValue.text = Mathf.RoundToInt(c.Atk).ToString();
        defValue.text = Mathf.RoundToInt(c.Def).ToString();
        blockValue.text = c.Class.placement == Placement.Ground ? c.BlockCapacity.ToString() : "-";
        rangeValue.text = c.Range.ToString("0.#");
        costValue.text = d.cost.ToString();

        bool hasTrait = d.trait != null && !string.IsNullOrEmpty(d.trait.name);
        UIFactory.SetVisible(traitBox, hasTrait);
        if (hasTrait)
        {
            traitName.text = d.trait.name;
            traitDesc.text = d.trait.description;
        }

        bool hasUlt = d.ultimate != null && !string.IsNullOrEmpty(d.ultimate.name);
        UIFactory.SetVisible(ultBox, hasUlt);
        if (hasUlt)
        {
            ultName.text = d.ultimate.name;
            ultMeta.text = "SP " + d.ultimate.spCost.ToString("0") + "　効果 " + d.ultimate.duration.ToString("0.#") + "秒　（M3 で使えるようになります）";
            ultDesc.text = d.ultimate.description;
        }

        retreatButton.Text.text = "撤退 +" + Battle.Current.RefundOf(c.Member);
    }
}
