using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 画面下の編成バー。DP の札と、部員のカード（アイコン・コスト・職業）。
/// カードを押してマップへドラッグすると配置できる。配置中・再配置待ち・DP 不足はカードの見た目で分かる。
/// マウスをカードに重ねると、カードの上に部員の札（CharacterInfo）が出る。スマホではカードを選んだとき小さな札が出る。
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

    // カードに重ねたときの札
    private VisualElement tipLayer;
    private CharacterInfo tip;
    private SquadMember tipFor;
    private string shownTipStatus;

    /// <summary>マウスを重ねているカードの部員（いなければ null）。BattleInput が置けるマスを薄く出すのに使う</summary>
    public SquadMember Hovered { get; private set; }

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

    /// <summary>カードに重ねたときの札を作る（ほかの札より手前に出すため、HUD を組み終えてから呼ぶ）</summary>
    public void AttachTooltip(VisualElement root)
    {
        tipLayer = root;
        tip = new CharacterInfo(false, "card-tip");
        tip.Root.SetCut(20f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetStroke(Palette.WithAlpha(Palette.Amber, 0.45f), 1.5f);
        tip.Root.pickingMode = PickingMode.Ignore;
        UIFactory.SetVisible(tip.Root, false);
        root.Add(tip.Root);
    }

    public void Rebuild(Battle battle)
    {
        Hovered = null;
        tipFor = null;
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
        // タッチでは「重ねる」がないので、マウスのときだけ札を出す
        box.RegisterCallback<PointerEnterEvent>(e =>
        {
            if (e.pointerType == UnityEngine.UIElements.PointerType.mouse) Hovered = member;
        });
        box.RegisterCallback<PointerLeaveEvent>(e =>
        {
            if (ReferenceEquals(Hovered, member)) Hovered = null;
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
        SquadMember armed = BattleInput.Instance != null ? BattleInput.Instance.Armed : null;
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
            int isDragging = dragging == m || armed == m ? 1 : 0;
            if (isDragging != c.ShownDragging)
            {
                c.ShownDragging = isDragging;
                c.Box.EnableInClassList("deploy-card--dragging", isDragging == 1);
            }
        }
        RefreshTooltip(battle, dragging, armed);
    }

    /// <summary>
    /// マウスなら重ねているカード（ドラッグ中・カードを選んでいるときは出さない）、
    /// スマホなら選んでいるカード（小さな札）の上に、部員の札を出す
    /// </summary>
    private void RefreshTooltip(Battle battle, SquadMember dragging, SquadMember armed)
    {
        if (tip == null) return;
        bool mobile = Application.isMobilePlatform || UIFactory.Zoom > 1f;
        SquadMember target = mobile ? armed : (dragging == null && armed == null ? Hovered : null);
        Card card = null;
        foreach (Card c in cards)
            if (ReferenceEquals(c.Member, target)) { card = c; break; }
        if (card == null) target = null;

        if (!ReferenceEquals(target, tipFor))
        {
            tipFor = target;
            shownTipStatus = null;
            UIFactory.SetVisible(tip.Root, target != null);
            if (target != null)
            {
                tip.SetCompact(mobile);
                tip.Fill(target.Data, target.Class);
            }
        }
        if (target == null) return;

        // 置けるかどうか（配置中・再配置待ち・DP 不足）を名前の下に
        string status;
        Color color = Palette.AmberBright;
        if (target.Deployed != null) status = "配置中";
        else if (target.Cooldown > 0f) status = "再配置まで " + Mathf.CeilToInt(target.Cooldown) + "秒";
        else if (battle.State.DpInt < target.Cost)
        {
            status = "DP があと " + (target.Cost - battle.State.DpInt) + " 足りない";
            color = Palette.Crimson;
        }
        else status = mobile ? "置くマスを押す" : "ドラッグしてマップに置く";
        if (status != shownTipStatus)
        {
            shownTipStatus = status;
            tip.SetStatus(status, color);
        }

        // カードの真上に。画面の端からははみ出さない
        Rect r = card.Box.worldBound;
        float width = float.IsNaN(tip.Root.resolvedStyle.width) || tip.Root.resolvedStyle.width <= 0f ? 600f : tip.Root.resolvedStyle.width;
        float panelWidth = tipLayer.layout.width;
        float panelHeight = tipLayer.layout.height;
        float left = Mathf.Clamp(r.center.x - width * 0.5f, 16f, Mathf.Max(16f, panelWidth - width - 16f));
        tip.Root.style.left = left;
        tip.Root.style.bottom = panelHeight - r.yMin + 14f;
    }
}
