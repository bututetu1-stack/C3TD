using UnityEngine;
using UnityEngine.UIElements;

/// <summary>タイトル画面。「作戦開始」で作戦選択へ。下に「育成」「ガチャ」「強化」、その下に「敵図鑑」「遊び方」「記録」「設定」</summary>
public class TitleScreen
{
    public readonly VisualElement Root;
    private readonly Label freeBadge;
    private readonly Label coinLabel;
    private readonly VisualElement missionList;
    private readonly Label toast;
    private readonly VisualElement pickupArt;
    private readonly Label pickupName;
    private readonly Label pickupTimer;
    private IVisualElementScheduledItem toastTween;

    /// <summary>タイトルを出すたびに：ログインボーナス、今日の無料ガチャの「無料」、今日の任務、部費</summary>
    public void Refresh()
    {
        int login = Daily.Login();
        if (login > 0) Toast("ログインボーナス　部費 +" + login);
        RefreshMissions();
        RefreshPickup();
    }

    /// <summary>右下の今日のピックアップ：絵・名前・入れかわりまでの時間</summary>
    private void RefreshPickup()
    {
        EquipmentData pickup = Growth.Pickup();
        if (pickup == null) return;
        Sprite art = Art.Item(pickup.id);
        pickupArt.style.backgroundImage = art != null ? new StyleBackground(art) : new StyleBackground(StyleKeyword.None);
        pickupName.text = pickup.name;
        System.TimeSpan left = System.DateTime.Now.Date.AddDays(1) - System.DateTime.Now;
        pickupTimer.text = "入れかわりまで あと " + left.Hours + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00");
    }

    /// <summary>ガチャのボタンの札：今日の無料があれば「無料」、部費で 10 回引ければ「10回OK」、1 回なら「引ける」</summary>
    private void RefreshBadge()
    {
        GachaData g = GameData.Items.gacha;
        string text = Growth.FreePullAvailable() ? "無料" : Progress.Coins >= g.cost10 ? "10回OK" : Progress.Coins >= g.cost ? "引ける" : "";
        freeBadge.text = text;
        UIFactory.SetVisible(freeBadge, text.Length > 0);
    }

    private void RefreshMissions()
    {
        coinLabel.text = "部費 " + Progress.Coins.ToString("N0");
        RefreshBadge();
        missionList.Clear();
        foreach (Daily.Mission m in Daily.Missions)
        {
            var row = UIFactory.Box("mission-row");
            var text = UIFactory.Box("mission-text");
            text.Add(UIFactory.Text(m.Text, UIFont.Body, "mission-name"));
            int count = Mathf.Min(Daily.CountOf(m.Id), m.Goal);
            var track = UIFactory.Box("mission-track");
            var fill = UIFactory.Box("mission-fill");
            fill.style.width = Length.Percent(count * 100f / m.Goal);
            track.Add(fill);
            text.Add(track);
            row.Add(text);
            row.Add(UIFactory.Text(count + "/" + m.Goal, UIFont.Display, "mission-count"));
            Daily.Mission captured = m;
            if (Daily.Claimed(m)) row.Add(UIFactory.Text("受取済", UIFont.Body, "mission-done"));
            else
            {
                var claim = new C3Button(null, 0f, "部費 +" + m.Reward, () =>
                {
                    int got = Daily.Claim(captured);
                    if (got <= 0) return;
                    AudioManager.PlaySE("buff");
                    Toast("任務達成　部費 +" + got);
                    RefreshMissions();
                });
                claim.AddToClassList("mission-claim");
                claim.Interactable = Daily.Done(m);
                claim.Active = Daily.Done(m);
                row.Add(claim);
            }
            missionList.Add(row);
        }
        // 全部受け取ったおまけ
        var all = UIFactory.Box("mission-row", "mission-row--all");
        all.Add(UIFactory.Text("全部受け取ると", UIFont.Body, "mission-name"));
        all.Add(UIFactory.Box("top-spacer"));
        if (Daily.AllBonusClaimed()) all.Add(UIFactory.Text("受取済", UIFont.Body, "mission-done"));
        else
        {
            var bonus = new C3Button(IconKind.Star, 18f, "部費 +" + Daily.AllBonus, () =>
            {
                int got = Daily.ClaimAllBonus();
                if (got <= 0) return;
                AudioManager.PlaySE("victory");
                Toast("今日の任務コンプリート　部費 +" + got);
                RefreshMissions();
            });
            bonus.AddToClassList("mission-claim");
            bonus.Interactable = Daily.AllClaimed();
            bonus.Active = Daily.AllClaimed();
            all.Add(bonus);
        }
        missionList.Add(all);
    }

    private void Toast(string text)
    {
        toast.text = text;
        UIFactory.SetVisible(toast, true);
        UITween.Stop(ref toastTween);
        toastTween = UITween.Run(toast, 3.2f, t =>
        {
            float a = t < 0.1f ? t / 0.1f : t > 0.8f ? (1f - t) / 0.2f : 1f;
            toast.style.opacity = a;
            toast.style.translate = new Translate(0f, (1f - Mathf.Min(1f, t / 0.1f)) * -20f);
        }, () => UIFactory.SetVisible(toast, false));
    }

    public TitleScreen(VisualElement parent, GameBootstrap bootstrap)
    {
        Root = UIFactory.Box("full", "title-screen");

        var block = UIFactory.Box("title-block");
        block.Add(UIFactory.Text("C3 CLUB  /  TOWER DEFENSE", UIFont.Display, "title-caption"));
        block.Add(UIFactory.Text("C3TD", UIFont.Display, "title-logo"));
        block.Add(UIFactory.Text("部員タワーディフェンス（仮）", UIFont.Title, "title-sub"));
        block.Add(UIFactory.Box("title-bar"));

        var start = new C3Button(IconKind.Play, 30f, "作戦開始", () => bootstrap.ShowStageSelect());
        start.AddToClassList("title-button");
        start.Active = true;
        block.Add(start);
        var growRow = UIFactory.Box("title-subrow");
        var growth = new C3Button(IconKind.Next, 26f, "育成", () => bootstrap.ShowGrowth());
        growth.AddToClassList("title-button");
        growth.AddToClassList("title-button--sub");
        growRow.Add(growth);
        var gacha = new C3Button(IconKind.Star, 24f, "ガチャ", () => bootstrap.ShowGacha());
        gacha.AddToClassList("title-button");
        gacha.AddToClassList("title-button--sub");
        freeBadge = UIFactory.Text("無料", UIFont.Body, "title-free-badge");
        gacha.Add(freeBadge);
        growRow.Add(gacha);
        var subRow = UIFactory.Box("title-subrow");
        var book = new C3Button(IconKind.Next, 26f, "敵図鑑", () => bootstrap.ShowEnemyBook());
        book.AddToClassList("title-button");
        book.AddToClassList("title-button--sub");
        subRow.Add(book);
        var help = new C3Button(IconKind.Next, 26f, "遊び方", () => bootstrap.ShowHelp());
        help.AddToClassList("title-button");
        help.AddToClassList("title-button--sub");
        subRow.Add(help);
        var records = new C3Button(IconKind.Next, 26f, "記録", () => bootstrap.ShowRecords());
        records.AddToClassList("title-button");
        records.AddToClassList("title-button--sub");
        subRow.Add(records);
        var upgrades = new C3Button(IconKind.Next, 26f, "強化", () => bootstrap.ShowUpgrades());
        upgrades.AddToClassList("title-button");
        upgrades.AddToClassList("title-button--sub");
        growRow.Add(upgrades);
        block.Add(growRow);
        var settings = new C3Button(IconKind.Next, 26f, "設定", () => bootstrap.ShowSettings());
        settings.AddToClassList("title-button");
        settings.AddToClassList("title-button--sub");
        subRow.Add(settings);
        block.Add(subRow);
        if (!UnityEngine.Application.isMobilePlatform) block.Add(UIFactory.Text("Space・Enter でも始められる", UIFont.Medium, "title-hint"));
        Root.Add(block);

        // 右下：今日のピックアップ（押すとガチャへ）と、今日の任務・部費
        var right = UIFactory.Box("title-right");
        var pick = new ChamferBox();
        pick.AddToClassList("title-pickup");
        pick.SetFill(new Color(0.07f, 0.06f, 0.03f, 0.92f)).SetStroke(Palette.WithAlpha(Palette.AmberBright, 0.8f), 1.5f)
            .SetCut(16f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetAccent(Palette.AmberBright, 5f);
        pick.style.overflow = Overflow.Hidden;
        pick.pickingMode = PickingMode.Position;
        pick.RegisterCallback<ClickEvent>(e => { AudioManager.PlaySE("click"); bootstrap.ShowGacha(); });
        var pickRays = UIFactory.Box("title-pickup-rays", "gacha-img");
        pickRays.style.backgroundImage = new StyleBackground(GachaShow.RaysTexture());
        pickRays.style.unityBackgroundImageTintColor = new Color(1f, 0.78f, 0.35f, 0.5f);
        pickRays.pickingMode = PickingMode.Ignore;
        pick.Add(pickRays);
        pickupArt = UIFactory.Box("title-pickup-art", "gacha-img");
        pickupArt.pickingMode = PickingMode.Ignore;
        pick.Add(pickupArt);
        var pickText = UIFactory.Box("title-pickup-text");
        pickText.pickingMode = PickingMode.Ignore;
        pickText.Add(UIFactory.Text("PICK UP　★★★", UIFont.Body, "title-pickup-caption"));
        pickupName = UIFactory.Text("", UIFont.Title, "title-pickup-name");
        pickText.Add(pickupName);
        pickupTimer = UIFactory.Text("", UIFont.Body, "title-pickup-timer");
        pickText.Add(pickupTimer);
        pick.Add(pickText);
        pick.Add(UIFactory.Text("ガチャへ", UIFont.Body, "title-pickup-go"));
        right.Add(pick);
        pick.schedule.Execute(() =>
        {
            if (!UIFactory.IsVisible(Root)) return;
            float t = Time.unscaledTime;
            pickRays.style.rotate = new Rotate(new Angle(t * 12f, AngleUnit.Degree));
            pickupArt.style.translate = new Translate(0f, Mathf.Sin(t * 2f) * 4f);
        }).Every(0);
        pick.schedule.Execute(() => { if (UIFactory.IsVisible(Root)) RefreshPickup(); }).Every(1000);

        var panel = new ChamferBox();
        panel.AddToClassList("mission-panel");
        panel.SetFill(Palette.UiBand).SetStroke(Palette.UiPanelLine, 1f)
            .SetCut(20f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetAccent(Palette.Amber, 5f).SetStripes(Palette.UiStripe, 18f);
        panel.pickingMode = PickingMode.Position;
        var head = UIFactory.Box("mission-head");
        head.Add(UIFactory.Text("今日の任務", UIFont.Title, "mission-title"));
        head.Add(UIFactory.Box("top-spacer"));
        coinLabel = UIFactory.Text("", UIFont.Display, "mission-coins");
        head.Add(coinLabel);
        panel.Add(head);
        missionList = UIFactory.Box("mission-list");
        panel.Add(missionList);
        right.Add(panel);
        Root.Add(right);

        toast = UIFactory.Text("", UIFont.Body, "title-toast");
        UIFactory.SetVisible(toast, false);
        Root.Add(toast);

        parent.Add(Root);
    }
}
