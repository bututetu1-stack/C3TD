using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 出撃前の編成。部員のカードを押すと編成に入れたり外したりできる（入れた順に編成バーに並ぶ）。
/// 右に、最後に押した部員の能力・特性・必殺技を出す。
/// </summary>
public class SquadScreen
{
    public readonly VisualElement Root;
    public string StageId { get; private set; }

    private readonly GameBootstrap bootstrap;
    private readonly Label stageLabel;
    private readonly VisualElement roster;
    private readonly VisualElement slots;
    private readonly Label countLabel;
    private readonly C3Button goButton;

    // 右の説明
    private readonly VisualElement portrait;
    private readonly Label portraitInitial;
    private readonly Label detailClass;
    private readonly Label detailName;
    private readonly Label hpValue;
    private readonly Label atkValue;
    private readonly Label defValue;
    private readonly Label blockValue;
    private readonly Label rangeValue;
    private readonly Label costValue;
    private readonly Label traitName;
    private readonly Label traitDesc;
    private readonly VisualElement traitBox;
    private readonly Label ultName;
    private readonly Label ultMeta;
    private readonly Label ultDesc;
    private readonly VisualElement ultBox;

    private readonly Dictionary<string, CharacterCard> cards = new Dictionary<string, CharacterCard>();
    private readonly Dictionary<string, Label> badges = new Dictionary<string, Label>();
    private readonly List<string> squad = new List<string>();
    private int max;

    public SquadScreen(VisualElement parent, GameBootstrap bootstrap)
    {
        this.bootstrap = bootstrap;
        Root = UIFactory.Box("full", "menu-screen");
        Label sub;
        MenuUI.Header(Root, "SQUAD", "編成", () => bootstrap.ShowStageSelect(StageId), out sub);
        stageLabel = sub;

        var body = UIFactory.Box("squad-body");
        var left = UIFactory.Box("squad-left");
        left.Add(UIFactory.Text("カードを押すと編成に入れる・外す（入れた順に編成バーへ並ぶ）", UIFont.Medium, "squad-help"));
        roster = UIFactory.Box("squad-roster");
        // 部員が増えたり、スマホで UI が大きくなったりしても下の編成バーに重ならないよう、縦に流せるようにする
        var rosterScroll = new ScrollView(ScrollViewMode.Vertical);
        rosterScroll.AddToClassList("squad-roster-scroll");
        rosterScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        rosterScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        rosterScroll.Add(roster);
        left.Add(rosterScroll);
        body.Add(left);

        var detail = new ChamferBox();
        detail.AddToClassList("squad-detail");
        detail.SetFill(Palette.UiBand).SetStroke(Palette.UiPanelLine, 1f)
            .SetCut(28f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetStripes(Palette.UiStripe, 20f);
        detail.pickingMode = PickingMode.Position;
        var head = UIFactory.Box("unit-head");
        portrait = UIFactory.Box("unit-portrait");
        portraitInitial = UIFactory.Text("", UIFont.Title, "unit-portrait-initial");
        portrait.Add(portraitInitial);
        head.Add(portrait);
        var names = UIFactory.Box("unit-names");
        detailClass = UIFactory.Text("", UIFont.Body, "unit-class");
        names.Add(detailClass);
        detailName = UIFactory.Text("", UIFont.Title, "unit-name");
        names.Add(detailName);
        head.Add(names);
        detail.Add(head);

        var stats = UIFactory.Box("unit-stats");
        hpValue = Stat(stats, "HP");
        atkValue = Stat(stats, "ATK");
        defValue = Stat(stats, "DEF");
        blockValue = Stat(stats, "BLOCK");
        rangeValue = Stat(stats, "RANGE");
        costValue = Stat(stats, "COST");
        detail.Add(stats);

        Label unusedMeta;
        traitBox = Skill(detail, "特性", out traitName, out unusedMeta, out traitDesc);
        UIFactory.SetVisible(unusedMeta, false);
        ultBox = Skill(detail, "必殺技", out ultName, out ultMeta, out ultDesc);
        body.Add(detail);
        Root.Add(body);

        // 下の帯：編成の並び・人数・出撃
        var footer = UIFactory.Box("squad-footer");
        footer.Add(UIFactory.Text("SQUAD", UIFont.Display, "squad-footer-caption"));
        slots = UIFactory.Box("squad-slots");
        footer.Add(slots);
        countLabel = UIFactory.Text("", UIFont.Display, "squad-count");
        footer.Add(countLabel);
        footer.Add(UIFactory.Box("top-spacer"));
        goButton = new C3Button(IconKind.Play, 32f, "出撃", () =>
        {
            if (squad.Count == 0) return;
            bootstrap.SetSquad(squad);
            bootstrap.StartBattle(StageId);
        });
        goButton.AddToClassList("squad-go");
        goButton.Active = true;
        footer.Add(goButton);
        Root.Add(footer);

        parent.Add(Root);
    }

    public void Open(string stageId, List<string> current)
    {
        StageId = stageId;
        StageData stage = GameData.Stage(stageId);
        stageLabel.text = stage != null ? (stage.Code + "　" + stage.Title) : stageId;
        max = Mathf.Max(1, GameData.Rules.squadMax);

        roster.Clear();
        cards.Clear();
        badges.Clear();
        squad.Clear();
        List<string> ids = GameData.CharacterIds();
        foreach (string id in current)
            if (ids.Contains(id) && !squad.Contains(id) && squad.Count < max) squad.Add(id);

        string first = null;
        foreach (string id in ids)
        {
            CharacterData data = GameData.Character(id);
            if (data == null) continue;
            var card = new CharacterCard(data, GameData.Class(data.unitClass));
            card.AddToClassList("squad-card");
            var badge = UIFactory.Text("", UIFont.Display, "squad-badge");
            card.Add(badge);
            string captured = id;
            card.AddManipulator(new Clickable(() =>
            {
                AudioManager.PlaySE("click");
                Toggle(captured);
            }));
            card.RegisterCallback<PointerEnterEvent>(e => ShowDetail(captured));
            roster.Add(card);
            cards[id] = card;
            badges[id] = badge;
            if (first == null) first = id;
        }
        Refresh();
        ShowDetail(squad.Count > 0 ? squad[0] : first);
    }

    private void Toggle(string id)
    {
        if (squad.Contains(id)) squad.Remove(id);
        else if (squad.Count < max) squad.Add(id);
        ShowDetail(id);
        Refresh();
    }

    private void Refresh()
    {
        foreach (var kv in cards)
        {
            int index = squad.IndexOf(kv.Key);
            bool on = index >= 0;
            kv.Value.EnableInClassList("squad-card--on", on);
            kv.Value.SetStroke(on ? Palette.Amber : Palette.WithAlpha(kv.Value.ClassColor, 0.6f), on ? 3f : 1.5f);
            badges[kv.Key].text = on ? (index + 1).ToString() : "";
            UIFactory.SetVisible(badges[kv.Key], on);
        }

        slots.Clear();
        for (int i = 0; i < max; i++)
        {
            var slot = UIFactory.Box("squad-slot");
            if (i < squad.Count)
            {
                CharacterData data = GameData.Character(squad[i]);
                Sprite icon = data != null ? Art.Icon(data.id) : null;
                if (icon != null) slot.style.backgroundImage = new StyleBackground(icon);
                else if (data != null)
                {
                    slot.style.backgroundColor = Palette.Hex(data.color, Palette.Hex(GameData.Class(data.unitClass).color, Palette.Amber));
                    slot.Add(UIFactory.Text(data.Initial, UIFont.Title, "squad-slot-initial"));
                }
                slot.AddToClassList("squad-slot--on");
            }
            slots.Add(slot);
        }
        countLabel.text = squad.Count + "/" + max;
        goButton.Interactable = squad.Count > 0;
    }

    private void ShowDetail(string id)
    {
        CharacterData d = id != null ? GameData.Character(id) : null;
        if (d == null) return;
        ClassData cls = GameData.Class(d.unitClass);
        Color classColor = Palette.Hex(cls.color, Palette.Amber);

        Sprite icon = Art.Icon(d.id);
        portrait.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
        portrait.style.backgroundColor = icon != null ? Color.clear : Palette.Hex(d.color, classColor);
        portraitInitial.text = icon != null ? "" : d.Initial;
        portrait.style.borderLeftColor = portrait.style.borderRightColor = portrait.style.borderTopColor = portrait.style.borderBottomColor = classColor;

        detailName.text = d.name;
        detailClass.text = cls.name;
        detailClass.style.backgroundColor = classColor;
        hpValue.text = d.stats.hp.ToString();
        atkValue.text = d.stats.atk.ToString();
        defValue.text = d.stats.def.ToString();
        int block = d.stats.block >= 0 ? d.stats.block : cls.block;
        blockValue.text = cls.placement == Placement.Ground ? block.ToString() : "-";
        rangeValue.text = d.stats.range.ToString("0.#");
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
            ultMeta.text = "SP " + d.ultimate.spCost.ToString("0") + "（" + MenuUI.SpChargeName(d.ultimate.spCharge) + "）"
                + (d.ultimate.duration > 0f ? "　効果 " + d.ultimate.duration.ToString("0.#") + "秒" : "　すぐに効く");
            ultDesc.text = d.ultimate.description;
        }
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

    private static VisualElement Skill(VisualElement parent, string kind, out Label name, out Label meta, out Label desc)
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
}
