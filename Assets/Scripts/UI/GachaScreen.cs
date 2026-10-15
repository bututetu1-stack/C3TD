using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 部室ガチャ（タイトルから）。周回で手に入れた部費で備品を引く（課金はない）。部員は実在の人なのでガチャでは出さない。
/// 引いた備品は、育成の画面で部員 1 人に 1 つ付けられる。同じ物がまた出ると重ねて強くなる（4 まで。それより上は部費に換わる）。
/// </summary>
public class GachaScreen
{
    public readonly VisualElement Root;
    private readonly Label walletLabel;
    private readonly C3Button oneButton;
    private readonly C3Button tenButton;
    private readonly VisualElement results;
    private readonly Label ownedLabel;

    public GachaScreen(VisualElement parent, GameBootstrap bootstrap)
    {
        Root = UIFactory.Box("full", "menu-screen");
        MenuUI.Header(Root, "GACHA", "部室ガチャ", () => bootstrap.ShowTitle(), out walletLabel);

        var panel = new ChamferBox();
        panel.AddToClassList("gacha-panel");
        panel.SetFill(Palette.UiBand).SetStroke(Palette.WithAlpha(Palette.Amber, 0.5f), 1.5f)
            .SetCut(26f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetAccent(Palette.Amber, 6f).SetStripes(Palette.UiStripe, 20f);
        panel.Add(UIFactory.Text("部室の備品", UIFont.Title, "gacha-title"));
        GachaData g = GameData.Items.gacha;
        string rates = g.rates.Count >= 3 ? "★3 " + g.rates[2].ToString("0.#") + "%　★2 " + g.rates[1].ToString("0.#") + "%　★1 " + g.rates[0].ToString("0.#") + "%" : "";
        panel.Add(UIFactory.Text("作戦でもらった部費で、部室の備品を引く。備品は育成の画面で部員 1 人に 1 つ付けられる。同じ物がまた出ると重ねて強くなる（4 回まで）。\n"
            + rates + "　10 回引くと、最後の 1 回は★2 以上", UIFont.Medium, "gacha-desc"));
        var buttons = UIFactory.Box("gacha-buttons");
        oneButton = new C3Button(IconKind.Star, 26f, "1 回引く　部費 " + g.cost, () => Pull(1));
        oneButton.AddToClassList("gacha-button");
        buttons.Add(oneButton);
        tenButton = new C3Button(IconKind.Star, 26f, "10 回引く　部費 " + g.cost10, () => Pull(10));
        tenButton.AddToClassList("gacha-button");
        buttons.Add(tenButton);
        var growth = new C3Button(IconKind.Next, 24f, "育成で備品を付ける", () => bootstrap.ShowGrowth());
        growth.AddToClassList("gacha-button");
        growth.AddToClassList("gacha-button--sub");
        buttons.Add(growth);
        panel.Add(buttons);
        Root.Add(panel);

        results = UIFactory.Box("gacha-results");
        Root.Add(results);
        ownedLabel = UIFactory.Text("", UIFont.Medium, "gacha-owned");
        Root.Add(ownedLabel);
        parent.Add(Root);
    }

    public void Open()
    {
        results.Clear();
        Refresh();
    }

    private void Refresh()
    {
        GachaData g = GameData.Items.gacha;
        walletLabel.text = "部費 " + Progress.Coins.ToString("N0");
        oneButton.Interactable = Progress.Coins >= g.cost;
        oneButton.Active = oneButton.Interactable;
        tenButton.Interactable = Progress.Coins >= g.cost10;
        tenButton.Active = tenButton.Interactable;
        ownedLabel.text = "持っている備品 " + Progress.OwnedEquipment.Count + " / " + GameData.Items.equipment.Count + " 種類";
    }

    private void Pull(int count)
    {
        List<GachaResult> pulled = Growth.Pull(count);
        if (pulled.Count == 0) return;
        results.Clear();
        bool rare = false;
        for (int i = 0; i < pulled.Count; i++)
        {
            GachaResult r = pulled[i];
            if (r.Equipment.rarity >= 3) rare = true;
            VisualElement card = ResultCard(r);
            card.style.opacity = 0f;
            results.Add(card);
            // 1 枚ずつめくる
            card.schedule.Execute(() => UITween.Run(card, 0.3f, t =>
            {
                float k = Ease.OutCubic(t);
                card.style.opacity = k;
                card.style.translate = new Translate(0f, (1f - k) * 20f);
            })).StartingIn(90 * i);
        }
        AudioManager.PlaySE(rare ? "victory" : "buff");
        Refresh();
    }

    private static VisualElement ResultCard(GachaResult r)
    {
        EquipmentData e = r.Equipment;
        Color color = GrowthScreen.RarityColor(e.rarity);
        var card = new ChamferBox();
        card.AddToClassList("gacha-card");
        card.SetFill(Palette.UiPanel).SetStroke(color, e.rarity >= 3 ? 3f : 1.5f).SetCut(14f, ChamferBox.Corners.TopRight | ChamferBox.Corners.BottomLeft);
        if (e.rarity >= 3) card.SetAccent(color, 6f);
        var stars = UIFactory.Text(GrowthScreen.Stars(e.rarity), UIFont.Body, "gacha-card-stars");
        stars.style.color = color;
        card.Add(stars);
        card.Add(UIFactory.Text(e.name, UIFont.Title, "gacha-card-name"));
        card.Add(UIFactory.Text(Growth.Describe(e, r.Stacks), UIFont.Medium, "gacha-card-effect"));
        card.Add(UIFactory.Text(e.description ?? "", UIFont.Medium, "gacha-card-flavor"));
        string badge = r.IsNew ? "NEW" : r.Coins > 0 ? "部費 +" + r.Coins : "重ね +" + r.Stacks;
        var tag = UIFactory.Text(badge, UIFont.Display, "gacha-card-badge");
        tag.EnableInClassList("gacha-card-badge--new", r.IsNew);
        card.Add(tag);
        return card;
    }
}
