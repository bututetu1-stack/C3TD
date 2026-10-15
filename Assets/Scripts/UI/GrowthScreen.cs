using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 育成（タイトルから）。周回で手に入れた部費と素材で、部員のレベルを上げ、昇進させ、ガチャの備品を付ける（ソシャゲの育成のように）。
/// 左に部員のカード、右に選んだ部員のレベル・数値（上げたあとの数値）・昇進・備品。上に持っている部費と素材。
/// </summary>
public class GrowthScreen
{
    public readonly VisualElement Root;
    private readonly GameBootstrap bootstrap;
    private readonly Label walletLabel;
    private readonly VisualElement inventory;
    private readonly VisualElement roster;
    private readonly VisualElement detail;
    private readonly Label notice;
    private string selected;
    private bool choosingEquip;

    public GrowthScreen(VisualElement parent, GameBootstrap bootstrap)
    {
        this.bootstrap = bootstrap;
        Root = UIFactory.Box("full", "menu-screen");
        MenuUI.Header(Root, "MEMBERS", "育成", () => bootstrap.ShowTitle(), out walletLabel);

        notice = UIFactory.Text("", UIFont.Body, "growth-notice");
        Root.Add(notice);
        inventory = UIFactory.Box("growth-inventory");
        Root.Add(inventory);

        var body = UIFactory.Box("growth-body");
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("growth-roster-scroll");
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        roster = UIFactory.Box("squad-roster");
        scroll.Add(roster);
        body.Add(scroll);

        var panel = new ChamferBox();
        panel.AddToClassList("growth-detail");
        panel.SetFill(Palette.UiBand).SetStroke(Palette.UiPanelLine, 1f)
            .SetCut(24f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetStripes(Palette.UiStripe, 20f);
        panel.pickingMode = PickingMode.Position;
        var detailScroll = new ScrollView(ScrollViewMode.Vertical);
        detailScroll.AddToClassList("info-scroll");
        detailScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        detailScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        detail = UIFactory.Box("growth-detail-body");
        detailScroll.Add(detail);
        panel.Add(detailScroll);
        body.Add(panel);
        Root.Add(body);

        parent.Add(Root);
    }

    public void Open(int pastCoins)
    {
        notice.text = pastCoins > 0 ? "これまでにクリアした作戦のぶんとして、部費 " + pastCoins.ToString("N0") + " と素材を受け取りました" : "";
        UIFactory.SetVisible(notice, pastCoins > 0);
        choosingEquip = false;
        if (selected == null || GameData.Character(selected) == null)
        {
            List<string> ids = GameData.CharacterIds();
            selected = ids.Count > 0 ? ids[0] : null;
        }
        Refresh();
    }

    private void Refresh()
    {
        walletLabel.text = "部費 " + Progress.Coins.ToString("N0");
        BuildInventory();
        BuildRoster();
        BuildDetail();
    }

    private void BuildInventory()
    {
        inventory.Clear();
        foreach (MaterialData m in GameData.Items.materials)
        {
            var chip = UIFactory.Box("growth-item");
            chip.Add(ItemIcon(m, 34f));
            chip.Add(UIFactory.Text(m.name, UIFont.Body, "growth-item-name"));
            chip.Add(UIFactory.Text("×" + Progress.ItemCount(m.id), UIFont.Display, "growth-item-count"));
            chip.tooltip = m.description;
            inventory.Add(chip);
        }
    }

    /// <summary>素材のアイコン（色の丸に字。枠はめずらしさの色）</summary>
    public static VisualElement ItemIcon(MaterialData m, float size)
    {
        var icon = UIFactory.Box("item-icon");
        icon.style.width = size;
        icon.style.height = size;
        icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius = icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = size / 2f;
        icon.style.backgroundColor = Palette.Hex(m != null ? m.color : null, Palette.TextSub);
        Color frame = RarityColor(m != null ? m.rarity : 1);
        icon.style.borderLeftColor = icon.style.borderRightColor = icon.style.borderTopColor = icon.style.borderBottomColor = frame;
        var label = UIFactory.Text(m != null ? m.label : "?", UIFont.Title, "item-icon-label");
        label.style.fontSize = size * 0.5f;
        icon.Add(label);
        return icon;
    }

    public static Color RarityColor(int rarity)
    {
        switch (rarity)
        {
            case 3: return Palette.AmberBright;
            case 2: return Palette.Cyan;
            default: return new Color(0.75f, 0.78f, 0.85f);
        }
    }

    private void BuildRoster()
    {
        roster.Clear();
        foreach (string id in GameData.CharacterIds())
        {
            CharacterData d = GameData.Character(id);
            if (d == null) continue;
            var card = new CharacterCard(d, GameData.Class(d.unitClass));
            card.AddToClassList("squad-card");
            bool on = id == selected;
            card.SetStroke(on ? Palette.Amber : Palette.WithAlpha(card.ClassColor, 0.6f), on ? 3f : 1.5f);
            string captured = id;
            card.AddManipulator(new Clickable(() =>
            {
                AudioManager.PlaySE("click");
                selected = captured;
                choosingEquip = false;
                Refresh();
            }));
            roster.Add(card);
        }
    }

    private void BuildDetail()
    {
        detail.Clear();
        CharacterData d = selected != null ? GameData.Character(selected) : null;
        if (d == null) return;
        ClassData cls = GameData.Class(d.unitClass);
        Color classColor = Palette.Hex(cls.color, Palette.Amber);

        // 顔・名前・レベル
        var head = UIFactory.Box("unit-head");
        var portrait = UIFactory.Box("unit-portrait");
        Sprite icon = Art.Icon(d.id);
        if (icon != null) portrait.style.backgroundImage = new StyleBackground(icon);
        else
        {
            portrait.style.backgroundColor = Palette.Hex(d.color, classColor);
            portrait.Add(UIFactory.Text(d.Initial, UIFont.Title, "unit-portrait-initial"));
        }
        portrait.style.borderLeftColor = portrait.style.borderRightColor = portrait.style.borderTopColor = portrait.style.borderBottomColor = classColor;
        head.Add(portrait);
        var names = UIFactory.Box("unit-names");
        var classLabel = UIFactory.Text(cls.name, UIFont.Body, "unit-class");
        classLabel.style.backgroundColor = classColor;
        names.Add(classLabel);
        names.Add(UIFactory.Text(d.name, UIFont.Title, "unit-name"));
        int level = Growth.Level(d.id);
        int promotion = Progress.MemberPromotion(d.id);
        names.Add(UIFactory.Text("Lv " + level + " / " + Growth.LevelCap(d.id) + "　昇進 " + promotion + " / " + GameData.Items.growth.promotions.Count, UIFont.Display, "growth-level"));
        head.Add(names);
        detail.Add(head);

        // 数値（いま → 1 つ上げたあと）
        float hp, atk, def;
        Growth.Stats(d, out hp, out atk, out def);
        int coin;
        LevelCost cost = Growth.NextLevelCost(d.id, out coin);
        var stats = UIFactory.Box("growth-stats");
        AddStat(stats, "HP", hp, cost != null ? (float?)(d.stats.hp * Upgrades.HpScale(d.unitClass) * (1f + (Growth.Percent(d.id, "hp") + GameData.Items.growth.perLevel) / 100f)) : null);
        AddStat(stats, "ATK", atk, cost != null ? (float?)(d.stats.atk * Upgrades.AtkScale(d.unitClass) * (1f + (Growth.Percent(d.id, "atk") + GameData.Items.growth.perLevel) / 100f)) : null);
        AddStat(stats, "DEF", def, cost != null ? (float?)(d.stats.def * (1f + (Growth.Percent(d.id, "def") + GameData.Items.growth.perLevel) / 100f)) : null);
        detail.Add(stats);

        // レベル上げ
        var levelBox = Section("レベル上げ", "1 レベルごとに体力・攻撃力・防御 +" + GameData.Items.growth.perLevel.ToString("0.#") + "%");
        if (cost != null)
        {
            levelBox.Add(CostLine(coin, cost.item, cost.count));
            var row = UIFactory.Box("growth-buttons");
            var up = new C3Button(IconKind.Next, 22f, "レベルを上げる", () =>
            {
                if (!Growth.LevelUp(d.id)) return;
                AudioManager.PlaySE("buff");
                Refresh();
            });
            up.AddToClassList("growth-button");
            up.Interactable = Growth.CanLevelUp(d.id);
            up.Active = up.Interactable;
            row.Add(up);
            var max = new C3Button(IconKind.FastForward, 22f, "上げられるだけ", () =>
            {
                bool any = false;
                while (Growth.LevelUp(d.id)) any = true;
                if (!any) return;
                AudioManager.PlaySE("buff");
                Refresh();
            });
            max.AddToClassList("growth-button");
            max.Interactable = Growth.CanLevelUp(d.id);
            row.Add(max);
            levelBox.Add(row);
        }
        else
        {
            levelBox.Add(UIFactory.Text(level >= GameData.Items.growth.maxLevel ? "最高のレベル" : "これより上げるには昇進が必要", UIFont.Medium, "growth-note"));
        }
        detail.Add(levelBox);

        // 昇進
        PromotionData next = Growth.NextPromotion(d.id);
        var promoBox = Section("昇進", next != null ? "Lv" + next.at + " で昇進できる：" + next.description : "昇進しきった");
        if (next != null)
        {
            var line = UIFactory.Box("growth-cost");
            line.Add(UIFactory.Text("部費 " + next.coin.ToString("N0"), UIFont.Body, Progress.Coins >= next.coin ? "growth-cost-ok" : "growth-cost-short"));
            foreach (ItemCount i in next.items) line.Add(ItemCost(i.id, i.count));
            promoBox.Add(line);
            var promote = new C3Button(IconKind.Star, 22f, "昇進する", () =>
            {
                if (!Growth.Promote(d.id)) return;
                AudioManager.PlaySE("skill");
                Refresh();
            });
            promote.AddToClassList("growth-button");
            promote.Interactable = Growth.CanPromote(d.id);
            promote.Active = promote.Interactable;
            promoBox.Add(promote);
        }
        detail.Add(promoBox);

        // 備品
        EquipmentData equipped = Growth.Equipment(Progress.MemberEquip(d.id));
        var equipBox = Section("備品", equipped != null
            ? Stars(equipped.rarity) + " " + equipped.name + "：" + Growth.Describe(equipped, Progress.EquipmentStacks(equipped.id))
            : "付けていない（部室ガチャで手に入る）");
        var equipRow = UIFactory.Box("growth-buttons");
        var change = new C3Button(IconKind.Next, 22f, choosingEquip ? "えらぶのをやめる" : "付け替える", () =>
        {
            choosingEquip = !choosingEquip;
            Refresh();
        });
        change.AddToClassList("growth-button");
        change.Interactable = Progress.OwnedEquipment.Count > 0;
        equipRow.Add(change);
        if (equipped != null)
        {
            var remove = new C3Button(IconKind.Cross, 20f, "外す", () =>
            {
                Progress.SetMemberEquip(d.id, null);
                Refresh();
            });
            remove.AddToClassList("growth-button");
            equipRow.Add(remove);
        }
        var gacha = new C3Button(IconKind.Next, 22f, "部室ガチャへ", () => bootstrap.ShowGacha());
        gacha.AddToClassList("growth-button");
        equipRow.Add(gacha);
        equipBox.Add(equipRow);
        if (choosingEquip) equipBox.Add(EquipList(d.id));
        detail.Add(equipBox);
    }

    private VisualElement EquipList(string memberId)
    {
        var list = UIFactory.Box("growth-equip-list");
        var owned = new List<EquipRecord>(Progress.OwnedEquipment);
        owned.Sort((a, b) =>
        {
            EquipmentData ea = Growth.Equipment(a.id), eb = Growth.Equipment(b.id);
            return (eb != null ? eb.rarity : 0).CompareTo(ea != null ? ea.rarity : 0);
        });
        foreach (EquipRecord r in owned)
        {
            EquipmentData e = Growth.Equipment(r.id);
            if (e == null) continue;
            var row = UIFactory.Box("growth-equip-row");
            row.pickingMode = PickingMode.Position;
            var name = UIFactory.Text(Stars(e.rarity) + " " + e.name + (r.stacks > 0 ? "  +" + r.stacks : ""), UIFont.Body, "growth-equip-name");
            name.style.color = GrowthScreen.RarityColor(e.rarity);
            row.Add(name);
            row.Add(UIFactory.Text(Growth.Describe(e, r.stacks), UIFont.Medium, "growth-equip-effect"));
            string user = Progress.EquippedBy(e.id);
            CharacterData userData = user != null ? GameData.Character(user) : null;
            if (userData != null) row.Add(UIFactory.Text(userData.name + "が使用中", UIFont.Medium, "growth-equip-user"));
            string id = e.id;
            row.AddManipulator(new Clickable(() =>
            {
                AudioManager.PlaySE("click");
                Progress.SetMemberEquip(memberId, id);
                choosingEquip = false;
                Refresh();
            }));
            list.Add(row);
        }
        return list;
    }

    public static string Stars(int rarity)
    {
        return new string('★', Mathf.Clamp(rarity, 1, 3));
    }

    private static void AddStat(VisualElement parent, string caption, float now, float? next)
    {
        var item = UIFactory.Box("growth-stat");
        item.Add(UIFactory.Text(caption, UIFont.Display, "unit-stat-caption"));
        var row = UIFactory.Box("stat-value-row");
        row.Add(UIFactory.Text(Mathf.RoundToInt(now).ToString(), UIFont.Display, "unit-stat-value"));
        item.Add(row);
        // 1 つ上げたあとの数（下の行に）
        if (next.HasValue && Mathf.RoundToInt(next.Value) != Mathf.RoundToInt(now))
            item.Add(UIFactory.Text("→ " + Mathf.RoundToInt(next.Value), UIFont.Display, "growth-stat-next"));
        parent.Add(item);
    }

    private static VisualElement Section(string title, string description)
    {
        var box = UIFactory.Box("growth-section");
        box.Add(UIFactory.Text(title, UIFont.Body, "unit-skill-kind"));
        box.Add(UIFactory.Text(SkillText.Format(description), UIFont.Medium, "growth-desc"));
        return box;
    }

    private static VisualElement CostLine(int coin, string item, int count)
    {
        var line = UIFactory.Box("growth-cost");
        line.Add(UIFactory.Text("部費 " + coin.ToString("N0"), UIFont.Body, Progress.Coins >= coin ? "growth-cost-ok" : "growth-cost-short"));
        if (!string.IsNullOrEmpty(item)) line.Add(ItemCost(item, count));
        return line;
    }

    private static VisualElement ItemCost(string item, int count)
    {
        var box = UIFactory.Box("growth-cost-item");
        box.Add(ItemIcon(Growth.Material(item), 26f));
        int have = Progress.ItemCount(item);
        box.Add(UIFactory.Text(Growth.ItemName(item) + " ×" + count + "（持っている " + have + "）", UIFont.Body, have >= count ? "growth-cost-ok" : "growth-cost-short"));
        return box;
    }
}
