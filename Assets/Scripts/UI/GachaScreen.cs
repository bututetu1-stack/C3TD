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
    private readonly Label ownedLabel;
    private readonly Label pityLabel;
    private readonly VisualElement pityFill;
    private readonly VisualElement pickupArt;
    private readonly Label pickupName;
    private readonly Label pickupEffect;
    private readonly VisualElement pickupRays;
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
        pickupArt = UIFactory.Box("gacha-banner-art", "gacha-img");
        banner.Add(pickupArt);
        var bannerText = UIFactory.Box("gacha-banner-text");
        bannerText.Add(UIFactory.Text("PICK UP", UIFont.Display, "gacha-banner-caption"));
        bannerText.Add(UIFactory.Text("★★★", UIFont.Body, "gacha-banner-stars"));
        pickupName = UIFactory.Text("", UIFont.Title, "gacha-banner-name");
        bannerText.Add(pickupName);
        pickupEffect = UIFactory.Text("", UIFont.Body, "gacha-banner-effect");
        bannerText.Add(pickupEffect);
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
        buttons.Add(tenButton);
        side.Add(buttons);

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
            float s = 1f + 0.04f * Mathf.Sin(t * 2.2f);
            pickupRays.style.scale = new Scale(new Vector3(s, s, 1f));
            pickupArt.style.translate = new Translate(0f, Mathf.Sin(t * 1.6f) * 10f);
            float pulse = 0.85f + 0.15f * Mathf.Sin(t * 5f);
            if (freeButton.Interactable) freeButton.style.opacity = pulse;
            if (tenButton.Interactable) tenButton.style.scale = new Scale(new Vector3(1f + 0.015f * Mathf.Sin(t * 4f), 1f + 0.015f * Mathf.Sin(t * 4f), 1f));
        }).Every(0);

        // 引いたときの演出（画面いっぱいに重ねる）。閉じたら、結果を下に並べる
        show = new GachaShow(Root, bootstrap, n => Pull(n, false), () =>
        {
            Refresh();
            ShowLast();
        });
    }

    public void Open()
    {
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
        ownedLabel.text = "持っている備品 " + Progress.OwnedEquipment.Count + " / " + GameData.Items.equipment.Count + " 種類　引いた回数 " + Progress.GachaPulls;

        int left = Growth.PityLeft();
        pityLabel.text = left > 0 ? "天井：あと <color=#FFD27A>" + left + " 回</color>で★3 確定" : "";
        UIFactory.SetVisible(pityLabel, left > 0);
        float rate = g.pity > 0 ? Mathf.Clamp01(Progress.GachaPity / (float)g.pity) : 0f;
        pityFill.style.width = Length.Percent(rate * 100f);

        BuildCollection();

        EquipmentData pickup = Growth.Pickup();
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
        var all = new List<EquipmentData>(GameData.Items.equipment);
        all.Sort((a, b) => b.rarity != a.rarity ? b.rarity.CompareTo(a.rarity) : 0);
        int owned = 0;
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
            tile.tooltip = have ? e.name + "：" + Growth.Describe(e, stacks) : "？？？";
            collection.Add(tile);
        }
        collectionLabel.text = "備品図鑑 " + owned + " / " + all.Count + (owned >= all.Count ? "　コンプリート！" : "");
    }

    private void Pull(int count, bool free)
    {
        List<GachaResult> pulled = Growth.Pull(count, free);
        if (pulled.Count == 0) return;
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
