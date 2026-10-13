using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 画面下の編成バー。DP の札と、部員のカード（アイコン・コスト・職業）。
/// カードを押してマップへドラッグすると配置できる。配置中・再配置待ち・DP 不足はカードの見た目で分かる。
/// </summary>
public class DeployBar
{
    /// <summary>DP の札とカードの並び全体（部員が多いほど左へ伸びる）</summary>
    public readonly VisualElement Root;

    private readonly VisualElement cardRow;
    private readonly Label dpValue;
    private readonly Label dpCaption;
    private int shownDrained = -1;
    private readonly VisualElement dpFill;
    private readonly List<Card> cards = new List<Card>();
    private int shownDp = -1;

    private class Card
    {
        public SquadMember Member;
        public ChamferBox Box;
        public Label Cost;
        public VisualElement Shade;
        public Label Timer;
        public Label State;
        public int ShownState = -1;
        public int ShownTimer = -1;
        public int ShownAffordable = -1;
        public int ShownDragging = -1;
    }

    public DeployBar(VisualElement root)
    {
        var bar = UIFactory.Box("deploy-bar");
        root.Add(bar);
        Root = bar;

        var dp = new ChamferBox();
        dp.AddToClassList("dp-plate");
        dp.SetFill(Palette.UiPanel).SetStroke(Palette.UiPanelLine, 1f)
            .SetCut(14f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight)
            .SetAccent(Palette.Amber, 5f).SetStripes(Palette.UiStripe, 16f);
        dpCaption = UIFactory.Text("DP", UIFont.Display, "dp-caption");
        dp.Add(dpCaption);
        dpValue = UIFactory.Text("0", UIFont.Display, "dp-value");
        dp.Add(dpValue);
        var track = UIFactory.Box("dp-track");
        dpFill = UIFactory.Box("dp-fill");
        track.Add(dpFill);
        dp.Add(track);
        bar.Add(dp);

        cardRow = UIFactory.Box("card-row");
        bar.Add(cardRow);
    }

    public void Rebuild(Battle battle)
    {
        cardRow.Clear();
        cards.Clear();
        shownDp = -1;
        shownDrained = -1;
        foreach (SquadMember member in battle.Squad) cards.Add(CreateCard(member));
    }

    private Card CreateCard(SquadMember member)
    {
        var card = new Card { Member = member };

        var box = new CharacterCard(member.Data, member.Class);
        box.AddToClassList("deploy-card");
        card.Cost = box.Cost;

        card.Shade = UIFactory.Box("card-shade");
        card.Timer = UIFactory.Text("", UIFont.Display, "card-timer");
        card.Shade.Add(card.Timer);
        card.State = UIFactory.Text("", UIFont.Body, "card-state");
        card.Shade.Add(card.State);
        box.Add(card.Shade);
        UIFactory.SetVisible(card.Shade, false);

        box.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button == 0 && BattleInput.Instance != null) BattleInput.Instance.BeginDrag(member);
        });

        card.Box = box;
        cardRow.Add(box);
        return card;
    }

    public void Refresh(Battle battle)
    {
        GameState state = battle.State;
        int dp = state.DpInt;
        if (dp != shownDp)
        {
            shownDp = dp;
            dpValue.text = dp.ToString();
        }
        // 敵に DP の回復を減らされているあいだは、数字を赤くして「▼」を付ける
        int drained = battle.DpDrain > 0f ? 1 : 0;
        if (drained != shownDrained)
        {
            shownDrained = drained;
            dpCaption.text = drained == 1 ? "DP ▼" : "DP";
            dpValue.EnableInClassList("dp-value--drained", drained == 1);
        }
        float progress = dp >= battle.Rules.dpMax ? 1f : state.Dp - Mathf.Floor(state.Dp);
        dpFill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);

        SquadMember dragging = BattleInput.Instance != null ? BattleInput.Instance.Dragging : null;
        foreach (Card c in cards)
        {
            SquadMember m = c.Member;
            int s = m.Deployed != null ? 2 : (m.Cooldown > 0f ? 1 : 0);
            if (s != c.ShownState)
            {
                c.ShownState = s;
                UIFactory.SetVisible(c.Shade, s != 0);
                UIFactory.SetVisible(c.Timer, s == 1);
                c.State.text = s == 2 ? "配置中" : s == 1 ? "再配置まで" : "";
                c.ShownTimer = -1;
            }
            if (s == 1)
            {
                int t = Mathf.CeilToInt(m.Cooldown);
                if (t != c.ShownTimer)
                {
                    c.ShownTimer = t;
                    c.Timer.text = t.ToString();
                }
            }
            int affordable = dp >= m.Cost ? 1 : 0;
            if (affordable != c.ShownAffordable)
            {
                c.ShownAffordable = affordable;
                c.Box.EnableInClassList("deploy-card--short", affordable == 0);
            }
            int isDragging = dragging == m ? 1 : 0;
            if (isDragging != c.ShownDragging)
            {
                c.ShownDragging = isDragging;
                c.Box.EnableInClassList("deploy-card--dragging", isDragging == 1);
            }
        }
    }
}
