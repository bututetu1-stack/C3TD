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
    private readonly VisualElement tipsBox;
    private readonly VisualElement slots;
    private readonly Label countLabel;
    private readonly C3Button goButton;

    // 右の説明
    private readonly CharacterInfo detail;

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
        // この作戦の注意（出てくる敵から。いまの編成でまかなえていなければ赤）
        tipsBox = UIFactory.Box("squad-tips");
        left.Add(tipsBox);
        roster = UIFactory.Box("squad-roster");
        // 部員が増えたり、スマホで UI が大きくなったりしても下の編成バーに重ならないよう、縦に流せるようにする
        var rosterScroll = new ScrollView(ScrollViewMode.Vertical);
        rosterScroll.AddToClassList("squad-roster-scroll");
        rosterScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        rosterScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        rosterScroll.Add(roster);
        left.Add(rosterScroll);
        body.Add(left);

        detail = new CharacterInfo(true, "squad-detail");
        detail.Root.SetCut(28f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetStripes(Palette.UiStripe, 20f);
        body.Add(detail.Root);
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
        stageLabel.text = (stage != null ? (stage.Code + "　" + stage.Title) : stageId) + (bootstrap.Hard && Progress.IsCleared(stageId) ? "　強襲" : "");
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
        RefreshTips();
        goButton.Interactable = squad.Count > 0;
    }

    private void RefreshTips()
    {
        tipsBox.Clear();
        List<StageTips.Tip> tips = StageTips.For(GameData.Stage(StageId), squad);
        UIFactory.SetVisible(tipsBox, tips.Count > 0);
        foreach (StageTips.Tip t in tips)
        {
            string mark = t.Covered == false ? "！ " : t.Covered == true ? "✓ " : "・ ";
            var chip = UIFactory.Text(mark + t.Text, UIFont.Body, "squad-tip");
            chip.AddToClassList(t.Covered == false ? "squad-tip--warn" : t.Covered == true ? "squad-tip--ok" : "squad-tip--info");
            tipsBox.Add(chip);
        }
    }

    private void ShowDetail(string id)
    {
        CharacterData d = id != null ? GameData.Character(id) : null;
        if (d == null) return;
        detail.Fill(d, GameData.Class(d.unitClass));
        // これまでの記録（出撃と MVP の回数）
        MemberRecord rec = Progress.Member(d.id);
        detail.SetStatus(rec != null && rec.sorties > 0 ? "出撃 " + rec.sorties + " 回・MVP " + rec.mvp + " 回" : "", Palette.AmberBright);
    }
}
