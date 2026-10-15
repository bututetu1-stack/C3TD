using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 部室ガチャ（タイトルから）。周回で手に入れた部費で備品を引く（課金はない）。部員は実在の人なのでガチャでは出さない。
/// 引いた備品は、育成の画面で部員 1 人に 1 つ付けられる。同じ物がまた出ると重ねて強くなる（4 まで。それより上は部費に換わる）。
/// 左に今日のピックアップ（★3 が出たら pickupRate% でこれ。日ごとに変わる）、右に引くボタン（1 日 1 回は無料）と天井。
/// 引くと GachaShow の演出を画面いっぱいに出し、閉じると下に結果が並ぶ。
/// </summary>
public class GachaScreen
{
    public readonly VisualElement Root;
    private readonly Label walletLabel;
    private readonly C3Button freeButton;
    private readonly C3Button oneButton;
    private readonly C3Button tenButton;
    private readonly VisualElement results;
    private readonly VisualElement collection;
    private readonly Label collectionLabel;
    private readonly VisualElement collectionInfo;
    private readonly ScrollView scroll;
    private string infoId;
    private readonly Label ownedLabel;
    private readonly Label pityLabel;
    private readonly Label needLabel;
    private readonly VisualElement pityFill;
    private readonly VisualElement pickupArt;
    private readonly Label pickupName;
    private readonly Label pickupEffect;
    private readonly VisualElement pickupRays;
    private readonly VisualElement pickupCircle;
    private readonly Label pickupTimer;
    private int timerSecond = -1;
    private bool pityNear;
    private string pickupId;
    private HashSet<string> freshIds;
    private readonly GachaShow show;
    private List<GachaResult> last;

    /// <summary>演出を出しているか（Esc は演出が受け取る）</summary>
    public bool ShowOpen { get { return show.IsOpen; } }

    public void Escape()
    {
        show.Escape();
    }

    public GachaScreen(VisualElement parent, GameBootstrap bootstrap)
    {
        Root = UIFactory.Box("full", "menu-screen");
        MenuUI.Header(Root, "GACHA", "部室ガチャ", () => bootstrap.ShowTitle(), out walletLabel);
        GachaData g = GameData.Items.gacha;

        // 画面が小さい（スマホ）ときも収まるよう、見出しより下は縦に流せるようにする
        var page = new ScrollView(ScrollViewMode.Vertical);
        scroll = page;
        page.AddToClassList("gacha-page");
        page.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        page.verticalScrollerVisibility = ScrollerVisibility.Auto;
        var body = UIFactory.Box("gacha-body");

        // ---- 左：今日のピックアップ ----
        var banner = new ChamferBox();
        banner.AddToClassList("gacha-banner");
        banner.SetFill(new Color(0.06f, 0.05f, 0.03f, 0.95f)).SetStroke(Palette.WithAlpha(Palette.AmberBright, 0.8f), 2f)
            .SetCut(30f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetAccent(Palette.AmberBright, 8f);
        banner.style.overflow = Overflow.Hidden;
        pickupRays = UIFactory.Box("gacha-banner-rays", "gacha-img");
        pickupRays.style.backgroundImage = new StyleBackground(GachaShow.RaysTexture());
        pickupRays.style.unityBackgroundImageTintColor = new Color(1f, 0.78f, 0.35f, 0.55f);
        banner.Add(pickupRays);
        var bannerGlow = UIFactory.Box("gacha-banner-rays", "gacha-img");
        bannerGlow.style.backgroundImage = new StyleBackground(SpriteFactory.Glow);
        bannerGlow.style.unityBackgroundImageTintColor = new Color(1f, 0.7f, 0.3f, 0.35f);
        banner.Add(bannerGlow);
        // 絵の後ろで、魔法陣がゆっくり回る（Art/Gacha/Circle。なければ出さない）
        pickupCircle = UIFactory.Box("gacha-banner-circle", "gacha-img");
        Sprite circleArt = Art.GachaGlow("Circle");
        if (circleArt != null) pickupCircle.style.backgroundImage = new StyleBackground(circleArt);
        pickupCircle.style.unityBackgroundImageTintColor = new Color(1f, 0.82f, 0.45f, 0.35f);
        banner.Add(pickupCircle);
        pickupArt = UIFactory.Box("gacha-banner-art", "gacha-img");
        banner.Add(pickupArt);
        var bannerText = UIFactory.Box("gacha-banner-text");
        var captionRow = UIFactory.Box("gacha-banner-caption-row");
        captionRow.Add(UIFactory.Text("PICK UP", UIFont.Display, "gacha-banner-caption"));
        captionRow.Add(UIFactory.Text("確率UP", UIFont.Body, "gacha-banner-rateup"));
        bannerText.Add(captionRow);
        bannerText.Add(UIFactory.Text("★★★", UIFont.Body, "gacha-banner-stars"));
        pickupName = UIFactory.Text("", UIFont.Title, "gacha-banner-name");
        bannerText.Add(pickupName);
        pickupEffect = UIFactory.Text("", UIFont.Body, "gacha-banner-effect");
        bannerText.Add(pickupEffect);
        pickupTimer = UIFactory.Text("", UIFont.Body, "gacha-banner-timer");
        bannerText.Add(pickupTimer);
        bannerText.Add(UIFactory.Text("★3 が出たら " + g.pickupRate.ToString("0") + "% でこの備品。ピックアップは毎日かわる", UIFont.Medium, "gacha-banner-note"));
        banner.Add(bannerText);
        body.Add(banner);

        // ---- 右：引く ----
        var side = new ChamferBox();
        side.AddToClassList("gacha-side");
        side.SetFill(Palette.UiBand).SetStroke(Palette.UiPanelLine, 1f)
            .SetCut(24f, ChamferBox.Corners.TopRight | ChamferBox.Corners.BottomLeft).SetStripes(Palette.UiStripe, 20f);
        side.Add(UIFactory.Text("部室の備品", UIFont.Title, "gacha-title"));
        string rates = g.rates.Count >= 3 ? "★3 " + g.rates[2].ToString("0.#") + "%　★2 " + g.rates[1].ToString("0.#") + "%　★1 " + g.rates[0].ToString("0.#") + "%" : "";
        side.Add(UIFactory.Text("作戦でもらった部費で備品を引き、育成の画面で部員に 1 つ付ける。同じ物は重ねて強くなる（4 回まで）\n"
            + rates + "　10 回引くと最後の 1 回は★2 以上", UIFont.Medium, "gacha-desc"));

        freeButton = new C3Button(IconKind.Star, 26f, "今日の無料 1 回", () => Pull(1, true));
        freeButton.AddToClassList("gacha-button");
        freeButton.AddToClassList("gacha-button--free");
        side.Add(freeButton);
        var buttons = UIFactory.Box("gacha-buttons");
        oneButton = new C3Button(IconKind.Star, 24f, "1 回　部費 " + g.cost, () => Pull(1, false));
        oneButton.AddToClassList("gacha-button");
        buttons.Add(oneButton);
        tenButton = new C3Button(IconKind.Star, 24f, "10 回　部費 " + g.cost10, () => Pull(10, false));
        tenButton.AddToClassList("gacha-button");
        tenButton.Add(UIFactory.Text("★2以上 1 枚確定", UIFont.Body, "gacha-ten-badge"));
        buttons.Add(tenButton);
        side.Add(buttons);
        needLabel = UIFactory.Text("", UIFont.Medium, "gacha-need");
        side.Add(needLabel);

        // 天井のゲージ
        pityLabel = UIFactory.Text("", UIFont.Body, "gacha-pity");
        side.Add(pityLabel);
        var track = UIFactory.Box("gacha-pity-track");
        pityFill = UIFactory.Box("gacha-pity-fill");
        track.Add(pityFill);
        side.Add(track);

        var growth = new C3Button(IconKind.Next, 22f, "育成で備品を付ける", () => bootstrap.ShowGrowth());
        growth.AddToClassList("gacha-button");
        growth.AddToClassList("gacha-button--sub");
        side.Add(growth);
        ownedLabel = UIFactory.Text("", UIFont.Medium, "gacha-owned");
        side.Add(ownedLabel);
        body.Add(side);
        page.Add(body);
        Root.Add(page);

        // 備品図鑑：持っていない物は影だけ（そろえたくなるように）
        collectionLabel = UIFactory.Text("", UIFont.Body, "gacha-results-caption");
        page.Add(collectionLabel);
        collection = UIFactory.Box("gacha-collection");
        page.Add(collection);
        // 図鑑のマスを押すと、その備品の札（スマホでは重ねて出る説明が見られないので）
        collectionInfo = new ChamferBox();
        collectionInfo.AddToClassList("gacha-tile-info");
        ((ChamferBox)collectionInfo).SetFill(Palette.UiPanel).SetStroke(Palette.UiPanelLine, 1f).SetCut(12f, ChamferBox.Corners.TopRight | ChamferBox.Corners.BottomLeft);
        UIFactory.SetVisible(collectionInfo, false);
        page.Add(collectionInfo);

        page.Add(UIFactory.Text("前回の結果", UIFont.Body, "gacha-results-caption"));
        results = UIFactory.Box("gacha-results");
        page.Add(results);
        parent.Add(Root);

        // 光をゆっくり回し、ピックアップの絵をふわふわ、引けるボタンを光らせる
        Root.schedule.Execute(() =>
        {
            if (!UIFactory.IsVisible(Root)) return;
            float t = Time.unscaledTime;
            pickupRays.style.rotate = new Rotate(new Angle(t * 8f, AngleUnit.Degree));
            pickupCircle.style.rotate = new Rotate(new Angle(-t * 6f, AngleUnit.Degree));
            float s = 1f + 0.04f * Mathf.Sin(t * 2.2f);
            pickupRays.style.scale = new Scale(new Vector3(s, s, 1f));
            pickupArt.style.translate = new Translate(0f, Mathf.Sin(t * 1.6f) * 10f);
            float pulse = 0.85f + 0.15f * Mathf.Sin(t * 5f);
            if (freeButton.Interactable) freeButton.style.opacity = pulse;
            if (tenButton.Interactable) tenButton.style.scale = new Scale(new Vector3(1f + 0.015f * Mathf.Sin(t * 4f), 1f + 0.015f * Mathf.Sin(t * 4f), 1f));
            if (Random.value < 0.06f) BannerSpark();
            pityFill.style.opacity = pityNear ? 0.6f + 0.4f * Mathf.Sin(t * 8f) : 1f;
            UpdateTimer();
        }).Every(0);

        // 引いたときの演出（画面いっぱいに重ねる）。閉じたら、結果を下に並べ、図鑑に新しく入った物を光らせる
        show = new GachaShow(Root, bootstrap, n => Pull(n, false), () =>
        {
            freshIds = new HashSet<string>();
            if (last != null) foreach (GachaResult r in last) if (r.IsNew) freshIds.Add(r.Equipment.id);
            Refresh();
            freshIds = null;
            ShowLast();
        });
    }

    public void Open()
    {
        // ガチャの BGM を先に作っておく（引いた瞬間に作ると一瞬止まるので）
        GachaMusic.Loop();
        last = null;
        results.Clear();
        Refresh();
    }

    private void Refresh()
    {
        GachaData g = GameData.Items.gacha;
        walletLabel.text = "部費 " + Progress.Coins.ToString("N0");
        bool free = Growth.FreePullAvailable();
        UIFactory.SetVisible(freeButton, free);
        freeButton.Interactable = free;
        freeButton.Active = free;
        oneButton.Interactable = Progress.Coins >= g.cost;
        oneButton.Active = oneButton.Interactable;
        tenButton.Interactable = Progress.Coins >= g.cost10;
        tenButton.Active = tenButton.Interactable;
        int need = Progress.Coins < g.cost ? g.cost - Progress.Coins : Progress.Coins < g.cost10 ? g.cost10 - Progress.Coins : 0;
        needLabel.text = need <= 0 ? "" : "あと部費 <color=#FFD27A>" + need + "</color> で" + (Progress.Coins < g.cost ? " 1 回" : " 10 回") + "引ける（作戦・今日の任務・実績でもらえる）";
        UIFactory.SetVisible(needLabel, need > 0);
        ownedLabel.text = "持っている備品 " + Progress.OwnedEquipment.Count + " / " + GameData.Items.equipment.Count + " 種類　引いた回数 " + Progress.GachaPulls;

        int left = Growth.PityLeft();
        pityNear = left > 0 && left <= 5;
        pityLabel.text = left <= 0 ? "" : pityNear ? "もうすぐ天井！ あと <color=#FF8A80>" + left + " 回</color>で★3 確定" : "天井：あと <color=#FFD27A>" + left + " 回</color>で★3 確定";
        pityFill.EnableInClassList("gacha-pity-fill--near", pityNear);
        UIFactory.SetVisible(pityLabel, left > 0);
        float rate = g.pity > 0 ? Mathf.Clamp01(Progress.GachaPity / (float)g.pity) : 0f;
        pityFill.style.width = Length.Percent(rate * 100f);

        BuildCollection();

        EquipmentData pickup = Growth.Pickup();
        pickupId = pickup != null ? pickup.id : null;
        if (pickup != null)
        {
            Sprite art = Art.Item(pickup.id);
            pickupArt.style.backgroundImage = art != null ? new StyleBackground(art) : new StyleBackground(StyleKeyword.None);
            pickupName.text = pickup.name;
            pickupEffect.text = Growth.Describe(pickup, 0) + "　" + (pickup.description ?? "");
        }
    }

    private void BuildCollection()
    {
        collection.Clear();
        UIFactory.SetVisible(collectionInfo, false);
        infoId = null;
        var all = new List<EquipmentData>(GameData.Items.equipment);
        all.Sort((a, b) => b.rarity != a.rarity ? b.rarity.CompareTo(a.rarity) : 0);
        int owned = 0, popped = 0;
        foreach (EquipmentData e in all)
        {
            int stacks;
            bool have = Progress.HasEquipment(e.id, out stacks);
            if (have) owned++;
            Color color = GachaShow.RarityColor(e.rarity);
            var tile = UIFactory.Box("gacha-tile");
            tile.style.borderLeftColor = tile.style.borderRightColor = tile.style.borderTopColor = tile.style.borderBottomColor = have ? color : new Color(1f, 1f, 1f, 0.12f);
            Sprite art = Art.Item(e.id);
            var img = UIFactory.Box("gacha-tile-art", "gacha-img");
            if (art != null)
            {
                img.style.backgroundImage = new StyleBackground(art);
                // 持っていない物は黒い影に
                if (!have) img.style.unityBackgroundImageTintColor = new Color(0f, 0f, 0f, 0.85f);
            }
            tile.Add(img);
            if (!have) tile.Add(UIFactory.Text("?", UIFont.Display, "gacha-tile-unknown"));
            var stars = UIFactory.Text(GrowthScreen.Stars(e.rarity), UIFont.Body, "gacha-tile-stars");
            stars.style.color = have ? color : new Color(1f, 1f, 1f, 0.3f);
            tile.Add(stars);
            if (have && stacks > 0) tile.Add(UIFactory.Text("+" + stacks, UIFont.Display, "gacha-tile-stack"));
            if (freshIds != null && freshIds.Contains(e.id)) PopNew(tile, popped++, color);
            tile.tooltip = have ? e.name + "：" + Growth.Describe(e, stacks) : "？？？";
            tile.pickingMode = PickingMode.Position;
            EquipmentData picked = e;
            bool owns = have;
            int stackCount = stacks;
            tile.RegisterCallback<ClickEvent>(ev => ShowTileInfo(picked, owns, stackCount));
            collection.Add(tile);
        }
        collectionLabel.text = "備品図鑑 " + owned + " / " + all.Count + (owned >= all.Count ? "　コンプリート！" : "");
    }

    /// <summary>図鑑のマスを押したとき：絵・★・名前・効き目・ひとこと（持っていない物は ★ だけ）。もう一度押すと閉じる</summary>
    private void ShowTileInfo(EquipmentData e, bool have, int stacks)
    {
        if (infoId == e.id && UIFactory.IsVisible(collectionInfo))
        {
            UIFactory.SetVisible(collectionInfo, false);
            infoId = null;
            return;
        }
        infoId = e.id;
        AudioManager.PlaySE("click");
        collectionInfo.Clear();
        Color color = GachaShow.RarityColor(e.rarity);
        var img = UIFactory.Box("gacha-tile-info-art", "gacha-img");
        Sprite art = Art.Item(e.id);
        if (art != null)
        {
            img.style.backgroundImage = new StyleBackground(art);
            if (!have) img.style.unityBackgroundImageTintColor = new Color(0f, 0f, 0f, 0.85f);
        }
        collectionInfo.Add(img);
        var text = UIFactory.Box("gacha-tile-info-text");
        var stars = UIFactory.Text(GrowthScreen.Stars(e.rarity), UIFont.Body, "gacha-tile-info-stars");
        stars.style.color = color;
        text.Add(stars);
        text.Add(UIFactory.Text(have ? e.name : "？？？", UIFont.Title, "gacha-tile-info-name"));
        if (have)
        {
            text.Add(UIFactory.Text(Growth.Describe(e, stacks) + (stacks > 0 ? "（重ね " + stacks + "）" : ""), UIFont.Body, "gacha-tile-info-effect"));
            if (!string.IsNullOrEmpty(e.description)) text.Add(UIFactory.Text("「" + e.description + "」", UIFont.Body, "gacha-tile-info-flavor"));
        }
        else text.Add(UIFactory.Text("まだ持っていない。ガチャで出ると、ここに絵と効き目が出る", UIFont.Body, "gacha-tile-info-flavor"));
        collectionInfo.Add(text);
        UIFactory.SetVisible(collectionInfo, true);
        // 画面が小さいときは、札が見えるところまで流す
        collectionInfo.schedule.Execute(() => scroll.ScrollTo(collectionInfo)).StartingIn(30);
        UITween.Run(collectionInfo, 0.2f, t =>
        {
            collectionInfo.style.opacity = t;
            collectionInfo.style.translate = new Translate(0f, (1f - Ease.OutCubic(t)) * 10f);
        });
    }

    /// <summary>図鑑に新しく入った物：光って、はねて、NEW が付く</summary>
    private static void PopNew(VisualElement tile, int index, Color color)
    {
        var tag = UIFactory.Text("NEW", UIFont.Display, "gacha-card-badge", "gacha-card-badge--new", "gacha-tile-new");
        tile.Add(tag);
        tile.style.scale = new Scale(Vector3.zero);
        tile.schedule.Execute(() =>
        {
            GachaSound.Play("star", 0.5f);
            UITween.Run(tile, 0.5f, t =>
            {
                float s = Ease.OutBack(t);
                tile.style.scale = new Scale(new Vector3(s, s, 1f));
                tile.style.backgroundColor = Color.Lerp(Palette.WithAlpha(color, 0.7f), new Color(1f, 1f, 1f, 0.05f), t);
            });
        }).StartingIn(300 + 120 * index);
    }

    /// <summary>ピックアップの入れかわり（夜の 0 時）までの残り。日が変わったら絵も変える</summary>
    private void UpdateTimer()
    {
        System.DateTime now = System.DateTime.Now;
        if (now.Second == timerSecond) return;
        timerSecond = now.Second;
        System.TimeSpan left = now.Date.AddDays(1) - now;
        pickupTimer.text = "入れかわりまで あと " + left.Hours + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00");
        EquipmentData pickup = Growth.Pickup();
        if (pickup != null && pickup.id != pickupId) Refresh();
    }

    /// <summary>ピックアップの絵のまわりに、きらっと光る粒</summary>
    private void BannerSpark()
    {
        Rect r = pickupArt.layout;
        if (float.IsNaN(r.width) || r.width < 10f || pickupArt.parent == null) return;
        Sprite star = Art.Effect("GachaSparkle");
        float size = Random.Range(30f, 80f);
        var p = UIFactory.Box("gacha-img");
        p.style.position = Position.Absolute;
        p.style.width = size;
        p.style.height = size;
        p.style.left = r.x + Random.Range(0.05f, 0.95f) * r.width - size * 0.5f;
        p.style.top = r.y + Random.Range(0.05f, 0.95f) * r.height - size * 0.5f;
        p.style.backgroundImage = new StyleBackground(star != null ? star : SpriteFactory.Glow);
        if (star == null) p.style.unityBackgroundImageTintColor = new Color(1f, 0.85f, 0.4f);
        VisualElement parent = pickupArt.parent;
        parent.Insert(parent.IndexOf(pickupArt) + 1, p);
        UITween.Run(p, 1.2f, t =>
        {
            float s = Mathf.Sin(t * Mathf.PI);
            p.style.opacity = s;
            p.style.scale = new Scale(new Vector3(s, s, 1f));
            p.style.rotate = new Rotate(new Angle(t * 60f, AngleUnit.Degree));
        }, () => p.RemoveFromHierarchy());
    }

    private void Pull(int count, bool free)
    {
        List<GachaResult> pulled = Growth.Pull(count, free);
        if (pulled.Count == 0) return;
        // 引いて取った実績（備品集め・★3・ガチャの回数など）。部費はここで入る
        var got = Achievements.CheckNew();
        var names = new List<string>();
        int reward = 0;
        foreach (Achievements.Entry a in got) { names.Add("「" + a.Name + "」"); reward += a.Reward; }
        show.SetNote(got.Count > 0 ? "<color=#FFD27A>実績を取った：</color>" + string.Join("", names) + (reward > 0 ? "　部費 +" + reward : "") : "");
        last = pulled;
        results.Clear();
        Refresh();
        // まとめの「もう一度」：同じ回数をもう一度引けるか（無料のあとは 1 回ぶんの部費で）
        GachaData g = GameData.Items.gacha;
        int cost = count >= 10 ? g.cost10 : g.cost * count;
        show.SetAgain("もう一度 " + count + " 回（部費 " + cost + "）", Progress.Coins >= cost);
        show.Play(pulled);
    }

    /// <summary>演出を閉じたら、前回の結果を下に小さく並べる</summary>
    private void ShowLast()
    {
        results.Clear();
        if (last == null) return;
        for (int i = 0; i < last.Count; i++)
        {
            VisualElement card = ResultCard(last[i]);
            card.style.opacity = 0f;
            results.Add(card);
            card.schedule.Execute(() => UITween.Run(card, 0.25f, t =>
            {
                float k = Ease.OutCubic(t);
                card.style.opacity = k;
                card.style.translate = new Translate(0f, (1f - k) * 16f);
            })).StartingIn(50 * i);
        }
    }

    private static VisualElement ResultCard(GachaResult r)
    {
        EquipmentData e = r.Equipment;
        Color color = GachaShow.RarityColor(e.rarity);
        var card = new ChamferBox();
        card.AddToClassList("gacha-card");
        card.SetFill(Palette.UiPanel).SetStroke(color, e.rarity >= 3 ? 3f : 1.5f).SetCut(12f, ChamferBox.Corners.TopRight | ChamferBox.Corners.BottomLeft);
        if (e.rarity >= 3) card.SetAccent(color, 5f);
        Sprite art = Art.Item(e.id);
        if (art != null)
        {
            var img = UIFactory.Box("gacha-card-icon", "gacha-img");
            img.style.backgroundImage = new StyleBackground(art);
            card.Add(img);
        }
        var stars = UIFactory.Text(GrowthScreen.Stars(e.rarity), UIFont.Body, "gacha-card-stars");
        stars.style.color = color;
        card.Add(stars);
        card.Add(UIFactory.Text(e.name, UIFont.Title, "gacha-card-name"));
        card.Add(UIFactory.Text(Growth.Describe(e, r.Stacks), UIFont.Medium, "gacha-card-effect"));
        string badge = r.IsNew ? "NEW" : r.Coins > 0 ? "部費 +" + r.Coins : "重ね +" + r.Stacks;
        var tag = UIFactory.Text(badge, UIFont.Display, "gacha-card-badge");
        tag.EnableInClassList("gacha-card-badge--new", r.IsNew);
        card.Add(tag);
        return card;
    }
}
