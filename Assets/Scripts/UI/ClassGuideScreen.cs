using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 職業の説明（遊び方と編成画面から）。こういうゲームを初めて遊ぶ人向けに、職業ごとに
/// 置ける場所（地上・高台）・何をするか・止める数・攻撃の種類・得意と苦手・置き方のコツ・射程の形・その職業の部員を札にして並べる。
/// 文は Data/classes.json の role・strong・weak・tip。
/// </summary>
public class ClassGuideScreen
{
    private static readonly UnitClass[] Order = { UnitClass.Guard, UnitClass.Defender, UnitClass.Sniper, UnitClass.Caster, UnitClass.Medic };

    public readonly VisualElement Root;
    private readonly VisualElement grid;

    public ClassGuideScreen(VisualElement parent, Action onBack)
    {
        Root = UIFactory.Box("full", "menu-screen");
        Label sub;
        MenuUI.Header(Root, "CLASSES", "職業の説明", onBack, out sub);
        sub.text = "5 つの職業";

        Root.Add(UIFactory.Text("部員には 5 つの職業がある。置ける場所（暗いマス＝地上、明るいマス＝高台）と、やることが違う。" +
            "地上の前衛・重装で敵を止めて、高台の狙撃・術師で倒し、医療で回復するのが基本。", UIFont.Medium, "class-intro"));

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("help-scroll");
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        grid = UIFactory.Box("class-grid");
        scroll.Add(grid);
        Root.Add(scroll);
        parent.Add(Root);
    }

    public void Open()
    {
        grid.Clear();
        foreach (UnitClass c in Order) grid.Add(Card(c));
    }

    private static VisualElement Card(UnitClass unitClass)
    {
        ClassData cls = GameData.Class(unitClass);
        Color color = Palette.Hex(cls.color, Palette.Amber);
        var card = new ChamferBox();
        card.AddToClassList("class-card");
        card.SetFill(Palette.UiPanel).SetStroke(Palette.WithAlpha(color, 0.6f), 1.5f)
            .SetCut(16f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetAccent(color, 5f);

        // 見出し：色の札に頭文字・名前・ひとめで分かる札（置き場所・止める数・攻撃）
        var head = UIFactory.Box("class-head");
        var badge = UIFactory.Box("class-badge");
        badge.style.backgroundColor = color;
        badge.Add(UIFactory.Text(cls.name.Substring(0, 1), UIFont.Title, "class-badge-label"));
        head.Add(badge);
        var names = UIFactory.Box("class-names");
        names.Add(UIFactory.Text(cls.name, UIFont.Title, "class-name"));
        var chips = UIFactory.Box("class-chips");
        bool ground = cls.placement == Placement.Ground;
        chips.Add(UIFactory.Text(ground ? "地上に置く" : "高台に置く", UIFont.Body, "class-chip"));
        chips.Add(UIFactory.Text(ground ? "敵を " + cls.block + " 体止める" : "敵は止めない", UIFont.Body, "class-chip"));
        chips.Add(UIFactory.Text(cls.attack == AttackKind.Heal ? "回復する" : cls.damageType == DamageType.Arts ? "術で攻撃" : "物理で攻撃", UIFont.Body, "class-chip"));
        names.Add(chips);
        head.Add(names);
        card.Add(head);

        card.Add(UIFactory.Text(cls.role ?? "", UIFont.Medium, "class-role"));

        // 本文（得意・苦手・コツ）と、右に射程の形と置ける場所
        var body = UIFactory.Box("class-body");
        var lines = UIFactory.Box("class-lines");
        if (!string.IsNullOrEmpty(cls.strong)) lines.Add(UIFactory.Text("<color=#7CE0A0>得意</color>　" + cls.strong, UIFont.Medium, "class-line"));
        if (!string.IsNullOrEmpty(cls.weak)) lines.Add(UIFactory.Text("<color=#FF8A92>苦手</color>　" + cls.weak, UIFont.Medium, "class-line"));
        if (!string.IsNullOrEmpty(cls.tip)) lines.Add(UIFactory.Text("<color=#FFD27A>コツ</color>　" + cls.tip, UIFont.Medium, "class-line"));
        body.Add(lines);
        body.Add(Diagram(unitClass, cls, color));
        card.Add(body);

        // その職業の部員
        var members = UIFactory.Box("class-members");
        foreach (string id in GameData.CharacterIds())
        {
            CharacterData d = GameData.Character(id);
            if (d == null || d.unitClass != unitClass) continue;
            var m = UIFactory.Box("class-member");
            var portrait = UIFactory.Box("team-portrait");
            Sprite icon = Art.Icon(d.id);
            if (icon != null) portrait.style.backgroundImage = new StyleBackground(icon);
            else
            {
                portrait.style.backgroundColor = Palette.Hex(d.color, color);
                portrait.Add(UIFactory.Text(d.Initial, UIFont.Title, "team-initial"));
            }
            portrait.style.borderLeftColor = portrait.style.borderRightColor = portrait.style.borderTopColor = portrait.style.borderBottomColor = color;
            m.Add(portrait);
            m.Add(UIFactory.Text(d.name, UIFont.Body, "class-member-name"));
            members.Add(m);
        }
        card.Add(members);
        return card;
    }

    /// <summary>射程の形（その職業の部員でいちばん多い射程。まん中が部員）と、置ける場所の札</summary>
    private static VisualElement Diagram(UnitClass unitClass, ClassData cls, Color color)
    {
        var box = UIFactory.Box("class-diagram");
        float range = TypicalRange(unitClass);
        const int size = 7;
        int c = size / 2;
        for (int y = 0; y < size; y++)
        {
            var row = UIFactory.Box("class-grid-row");
            for (int x = 0; x < size; x++)
            {
                var cell = UIFactory.Box("class-cell");
                if (x == c && y == c) cell.style.backgroundColor = color;
                else if (Combat.InRange(new Vector2Int(c, c), new Vector2Int(x, y), range)) cell.AddToClassList("class-cell--range");
                row.Add(cell);
            }
            box.Add(row);
        }
        box.Add(UIFactory.Text(cls.attack == AttackKind.Heal ? "回復の届く範囲" : "攻撃の届く範囲", UIFont.Medium, "class-diagram-caption"));
        var tiles = UIFactory.Box("class-tile-row");
        var groundTile = UIFactory.Text("地上", UIFont.Body, "class-tile", "class-tile--ground");
        var highTile = UIFactory.Text("高台", UIFont.Body, "class-tile", "class-tile--high");
        bool ground = cls.placement == Placement.Ground;
        groundTile.EnableInClassList("class-tile--off", !ground);
        highTile.EnableInClassList("class-tile--off", ground);
        tiles.Add(groundTile);
        tiles.Add(highTile);
        box.Add(tiles);
        return box;
    }

    /// <summary>その職業の部員でいちばん多い射程（いなければ 1）</summary>
    private static float TypicalRange(UnitClass unitClass)
    {
        var counts = new Dictionary<float, int>();
        foreach (string id in GameData.CharacterIds())
        {
            CharacterData d = GameData.Character(id);
            if (d == null || d.unitClass != unitClass) continue;
            int n;
            counts.TryGetValue(d.stats.range, out n);
            counts[d.stats.range] = n + 1;
        }
        float best = 1f;
        int most = 0;
        foreach (var kv in counts)
            if (kv.Value > most) { most = kv.Value; best = kv.Key; }
        return best;
    }
}
