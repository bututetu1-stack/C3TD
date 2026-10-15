using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 部員の札の中身（顔・職業・名前・ひとこと・能力値・特性・必殺技）。
/// 編成画面の右の札（SquadScreen）、出撃中に選んだ部員の札（UnitPanel）、編成バーのカードに重ねたときの札（DeployBar）で使う。
/// 説明は SkillText で、文ごとの改行と数値の色付けをする。札の形（角の落とし方）は使う側で決める。
/// </summary>
public class CharacterInfo
{
    public readonly ChamferBox Root;

    public readonly Label NameLabel;
    public readonly Label ClassLabel;
    public readonly Label HpValue;
    public readonly Label AtkValue;
    public readonly Label DefValue;
    public readonly Label BlockValue;
    public readonly Label RangeValue;
    public readonly Label CostValue;
    public readonly Label TraitName;
    public readonly Label UltName;
    public readonly Label UltMeta;

    /// <summary>レベルと昇進（育成。召喚ユニットは出さない）</summary>
    public readonly Label LevelLabel;

    /// <summary>名前の下の状態（「配置中」「再配置まで 12秒」など）。空なら隠す</summary>
    public readonly Label StatusLabel;

    private readonly VisualElement portrait;
    private readonly Label portraitInitial;
    private readonly Label profileLabel;
    private readonly VisualElement stats;
    private readonly VisualElement traitBox;
    private readonly Label traitFlavor;
    private readonly Label traitDesc;
    private readonly VisualElement ultBox;
    private readonly Label ultFlavor;
    private readonly Label ultDesc;
    private readonly Label compactSkills;
    private bool compact;

    /// <summary>ひとこと紹介・技のひとことを出すか（出撃中の札は高さを抑えるため、出さないことがある）</summary>
    public bool ShowProfile = true;
    public bool ShowFlavor = true;

    /// <summary>scroll なら、顔と名前より下を縦に流せるようにする（編成画面の札。説明が長くても潰れない）</summary>
    public CharacterInfo(bool scroll, params string[] classes)
    {
        Root = new ChamferBox();
        foreach (string c in classes) Root.AddToClassList(c);
        Root.SetFill(Palette.UiBand).SetStroke(Palette.UiPanelLine, 1f).SetStripes(Palette.UiStripe, 18f);
        Root.pickingMode = PickingMode.Position;
        VisualElement body = Root;
        if (scroll)
        {
            var view = new ScrollView(ScrollViewMode.Vertical);
            view.AddToClassList("info-scroll");
            view.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            view.verticalScrollerVisibility = ScrollerVisibility.Auto;
            body = view;
        }

        var head = UIFactory.Box("unit-head");
        portrait = UIFactory.Box("unit-portrait");
        portraitInitial = UIFactory.Text("", UIFont.Title, "unit-portrait-initial");
        portrait.Add(portraitInitial);
        head.Add(portrait);
        var names = UIFactory.Box("unit-names");
        ClassLabel = UIFactory.Text("", UIFont.Body, "unit-class");
        var classRow = UIFactory.Box("unit-class-row");
        classRow.Add(ClassLabel);
        LevelLabel = UIFactory.Text("", UIFont.Display, "unit-level");
        classRow.Add(LevelLabel);
        names.Add(classRow);
        NameLabel = UIFactory.Text("", UIFont.Title, "unit-name");
        names.Add(NameLabel);
        StatusLabel = UIFactory.Text("", UIFont.Body, "unit-status");
        names.Add(StatusLabel);
        UIFactory.SetVisible(StatusLabel, false);
        head.Add(names);
        Root.Add(head);
        if (body != Root) Root.Add(body);

        // 小さな札では、特性と必殺技の名前だけを1行で
        compactSkills = UIFactory.Text("", UIFont.Body, "unit-compact-skills");
        Root.Add(compactSkills);
        UIFactory.SetVisible(compactSkills, false);

        profileLabel = UIFactory.Text("", UIFont.Medium, "unit-profile");
        body.Add(profileLabel);

        stats = UIFactory.Box("unit-stats");
        HpValue = Stat(stats, "HP");
        AtkValue = Stat(stats, "ATK");
        DefValue = Stat(stats, "DEF");
        BlockValue = Stat(stats, "BLOCK");
        RangeValue = Stat(stats, "RANGE");
        CostValue = Stat(stats, "COST");
        body.Add(stats);

        Label traitMeta;
        traitBox = Skill(body, "特性", out TraitName, out traitMeta, out traitFlavor, out traitDesc);
        UIFactory.SetVisible(traitMeta, false);
        ultBox = Skill(body, "必殺技", out UltName, out UltMeta, out ultFlavor, out ultDesc);
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

    private static VisualElement Skill(VisualElement parent, string kind, out Label name, out Label meta, out Label flavor, out Label desc)
    {
        var box = UIFactory.Box("unit-skill");
        var head = UIFactory.Box("unit-skill-head");
        head.Add(UIFactory.Text(kind, UIFont.Body, "unit-skill-kind"));
        name = UIFactory.Text("", UIFont.Body, "unit-skill-name");
        head.Add(name);
        box.Add(head);
        meta = UIFactory.Text("", UIFont.Body, "unit-skill-meta");
        box.Add(meta);
        flavor = UIFactory.Text("", UIFont.Medium, "unit-skill-flavor");
        box.Add(flavor);
        desc = UIFactory.Text("", UIFont.Medium, "unit-skill-desc");
        box.Add(desc);
        parent.Add(box);
        return box;
    }

    /// <summary>小さくする（能力値・ひとこと・説明を隠し、名前と技の名前だけ）。スマホでカードを選んだときの札</summary>
    public void SetCompact(bool value)
    {
        compact = value;
        Root.EnableInClassList("unit-info--compact", value);
        UIFactory.SetVisible(stats, !value);
        UIFactory.SetVisible(profileLabel, !value && ShowProfile && profileLabel.text.Length > 0);
        UIFactory.SetVisible(traitDesc, !value);
        UIFactory.SetVisible(ultDesc, !value);
        UIFactory.SetVisible(traitFlavor, !value && ShowFlavor && traitFlavor.text.Length > 0);
        UIFactory.SetVisible(ultFlavor, !value && ShowFlavor && ultFlavor.text.Length > 0);
        if (value)
        {
            UIFactory.SetVisible(traitBox, false);
            UIFactory.SetVisible(ultBox, false);
        }
        UIFactory.SetVisible(compactSkills, value && compactSkills.text.Length > 0);
    }

    /// <summary>JSON の値で埋める（出撃中の数値は使う側で上書きする）</summary>
    public void Fill(CharacterData d, ClassData cls, bool summon = false)
    {
        Color classColor = Palette.Hex(cls.color, Palette.Amber);
        Sprite icon = Art.Icon(d.id);
        portrait.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
        portrait.style.backgroundColor = icon != null ? Color.clear : Palette.Hex(d.color, classColor);
        portraitInitial.text = icon != null ? "" : d.Initial;
        portrait.style.borderLeftColor = portrait.style.borderRightColor = portrait.style.borderTopColor = portrait.style.borderBottomColor = classColor;

        NameLabel.text = d.name;
        // 職業と攻撃の種類（物理は防御で減り、術は術耐性で減る）
        string kind = cls.attack == AttackKind.Heal ? "" : cls.damageType == DamageType.Arts ? "・術" : "・物理";
        ClassLabel.text = cls.name + kind + (summon ? "・召喚" : "");
        ClassLabel.style.backgroundColor = classColor;
        profileLabel.text = d.profile ?? "";

        // 部員は、強化・育成ぶん込みの数値（出撃で使う数と同じ）
        float hp = d.stats.hp, atk = d.stats.atk, def = d.stats.def;
        if (!summon) Growth.Stats(d, out hp, out atk, out def);
        HpValue.text = Mathf.RoundToInt(hp).ToString();
        AtkValue.text = Mathf.RoundToInt(atk).ToString();
        DefValue.text = Mathf.RoundToInt(def).ToString();
        int promotion = Progress.MemberPromotion(d.id);
        LevelLabel.text = summon ? "" : "Lv" + Growth.Level(d.id) + (promotion > 0 ? "  昇進" + promotion : "");
        UIFactory.SetVisible(LevelLabel, !summon);
        int block = d.stats.block >= 0 ? d.stats.block : cls.block;
        BlockValue.text = cls.placement == Placement.Ground ? block.ToString() : "-";
        RangeValue.text = d.stats.range.ToString("0.#");
        CostValue.text = summon ? "-" : d.cost.ToString();

        bool hasTrait = d.trait != null && !string.IsNullOrEmpty(d.trait.name);
        UIFactory.SetVisible(traitBox, hasTrait);
        bool hasUltName = !summon && d.ultimate != null && !string.IsNullOrEmpty(d.ultimate.name) && d.ultimate.spCost > 0f;
        compactSkills.text = (hasTrait ? "特性 " + d.trait.name : "") + (hasTrait && hasUltName ? "　／　" : "") + (hasUltName ? "必殺技 " + d.ultimate.name : "");
        if (hasTrait)
        {
            TraitName.text = d.trait.name;
            traitFlavor.text = d.trait.flavor ?? "";
            traitDesc.text = SkillText.Format(d.trait.description);
        }
        bool hasUlt = !summon && d.ultimate != null && !string.IsNullOrEmpty(d.ultimate.name) && d.ultimate.spCost > 0f;
        UIFactory.SetVisible(ultBox, hasUlt);
        if (hasUlt)
        {
            UltName.text = d.ultimate.name;
            UltMeta.text = SkillText.UltimateMeta(d.ultimate);
            ultFlavor.text = d.ultimate.flavor ?? "";
            ultDesc.text = SkillText.Format(d.ultimate.description);
        }
        SetCompact(compact);
    }

    /// <summary>名前の下の状態を出す（空なら隠す）</summary>
    public void SetStatus(string text, Color color)
    {
        StatusLabel.text = text ?? "";
        StatusLabel.style.color = color;
        UIFactory.SetVisible(StatusLabel, !string.IsNullOrEmpty(text));
    }
}
