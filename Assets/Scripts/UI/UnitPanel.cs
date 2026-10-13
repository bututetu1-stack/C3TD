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
    private readonly C3Button skillButton;
    private Character shown;
    private int shownHp = -1;
    private int shownAtk = -1;
    private string shownTrait;
    private string shownSkill;

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
        skillButton = new C3Button(IconKind.Bolt, 24f, "必殺技", () =>
        {
            if (BattleInput.Instance != null) BattleInput.Instance.ActivateSelected();
        });
        skillButton.AddToClassList("skill-button");
        actions.Add(skillButton);
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


    /// <summary>
    /// 札を、選んだものと反対の側に寄せる（左にいれば右に出す）。左端の出撃口を隠さないため。
    /// 動くもの（敵）は真ん中付近で行ったり来たりしないよう、左右 0.4 / 0.6 を越えたときだけ切り替える。
    /// </summary>
    public static void PlaceOpposite(VisualElement info, Vector3 world, bool sticky)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float x = cam.WorldToViewportPoint(world).x;
        bool right = info.ClassListContains("unit-info--right");
        if (!sticky) right = x < 0.5f;
        else if (x < 0.4f) right = true;
        else if (x > 0.6f) right = false;
        info.EnableInClassList("unit-info--right", right);
    }
    public void Refresh(Character c)
    {
        // 破棄された部員は Unity では null と等しくなるので、参照そのもので比べる
        if (c == null || !c.Alive) c = null;
        if (!ReferenceEquals(c, shown))
        {
            shown = c;
            shownHp = shownAtk = -1;
            shownTrait = shownSkill = null;
            if (c != null)
            {
                Fill(c);
                PlaceOpposite(info, c.Center, false);
            }
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
        int atk = Mathf.RoundToInt(c.Atk);
        if (atk != shownAtk)
        {
            shownAtk = atk;
            atkValue.text = atk.ToString();
            atkValue.EnableInClassList("unit-stat-value--up", c.PercentOf("atk") > 0.01f);
        }

        // 特性の重ね数
        string trait = c.Skills.Trait != null ? c.Skills.Trait.name + (c.Skills.TraitStatus.Length > 0 ? "　" + c.Skills.TraitStatus : "") : "";
        if (trait != shownTrait)
        {
            shownTrait = trait;
            traitName.text = trait;
        }

        // 必殺技のボタン：SP が溜まるまでは押せない。発動中は残り時間
        string skill;
        if (c.IsSummon) skill = "召喚　残り " + Mathf.CeilToInt(c.LifeRemaining) + "秒";
        else if (!c.Skills.HasUltimate) skill = "";
        else if (c.Skills.Active) skill = "発動中 " + Mathf.CeilToInt(c.Skills.ActiveRemaining) + "秒";
        else if (c.Skills.Ready) skill = "必殺技";
        else if (c.Stunned) skill = "スタン中";
        else skill = "SP " + Mathf.FloorToInt(c.Skills.Sp) + "/" + Mathf.RoundToInt(c.Skills.SpCost);
        if (skill != shownSkill)
        {
            shownSkill = skill;
            UIFactory.SetVisible(skillButton, skill.Length > 0);
            skillButton.Text.text = skill;
            skillButton.Interactable = c.Skills.Ready || c.IsSummon; // 召喚ユニットは残り時間の表示なので暗くしない（押しても何もしない）
            skillButton.Active = c.Skills.Ready || c.Skills.Active;
            if (c.Skills.HasUltimate)
                ultMeta.text = (c.Skills.Active ? "発動中" : c.Skills.Ready ? "使えます" : "SP " + Mathf.FloorToInt(c.Skills.Sp) + "/" + Mathf.RoundToInt(c.Skills.SpCost))
                    + "　効果 " + c.Skills.Ultimate.duration.ToString("0.#") + "秒";
        }

        // ボタンを駒の右上に置く
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
        classLabel.text = c.IsSummon ? c.Class.name + "・召喚" : c.Class.name;
        classLabel.style.backgroundColor = classColor;
        defValue.text = Mathf.RoundToInt(c.Def).ToString();
        blockValue.text = c.HasBlockRoom || c.Blocked.Count > 0 ? c.BlockCapacity.ToString() : "-";
        rangeValue.text = c.Range.ToString("0.#");
        costValue.text = c.IsSummon ? "-" : d.cost.ToString();
        UIFactory.SetVisible(retreatButton, !c.IsSummon);

        bool hasTrait = c.Skills.Trait != null;
        UIFactory.SetVisible(traitBox, hasTrait);
        if (hasTrait) traitDesc.text = c.Skills.Trait.description;

        bool hasUlt = c.Skills.HasUltimate;
        UIFactory.SetVisible(ultBox, hasUlt);
        if (hasUlt)
        {
            ultName.text = c.Skills.Ultimate.name;
            ultDesc.text = c.Skills.Ultimate.description;
        }

        if (c.Member != null) retreatButton.Text.text = "撤退 +" + Battle.Current.RefundOf(c.Member);
    }
}
