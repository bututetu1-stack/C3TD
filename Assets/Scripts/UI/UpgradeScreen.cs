using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 強化（タイトルから）。★で部室の設備をととのえて部員を少し強くする（Kingdom Rush の強化のように）。
/// 1 行に 1 つ：名前・いまの効果・段（■□□）・次の★・「そろえる」。下の「元に戻す」で★はすべて戻る。
/// </summary>
public class UpgradeScreen
{
    public readonly VisualElement Root;
    private readonly Label starsLabel;
    private readonly VisualElement list;

    public UpgradeScreen(VisualElement parent, GameBootstrap bootstrap)
    {
        Root = UIFactory.Box("full", "menu-screen");
        MenuUI.Header(Root, "UPGRADES", "強化", () => bootstrap.ShowTitle(), out starsLabel);

        Root.Add(UIFactory.Text("作戦でもらった★（★3 なら 3 つ。強襲でクリアするともう 1 つ）で、部室の設備をととのえて部員を少し強くする。「元に戻す」で★はすべて戻るので、何度でも選び直せる",
            UIFont.Medium, "upgrade-help"));

        var panel = new ChamferBox();
        panel.AddToClassList("upgrade-panel");
        panel.SetFill(Palette.UiPanel).SetStroke(Palette.UiPanelLine, 1f).SetCut(20f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight);
        list = UIFactory.Box("upgrade-list");
        panel.Add(list);
        Root.Add(panel);

        var reset = new C3Button(IconKind.Retry, 24f, "元に戻す（★をすべて戻す）", () =>
        {
            AudioManager.PlaySE("click");
            Upgrades.ResetAll();
            Open();
        });
        reset.AddToClassList("upgrade-reset");
        Root.Add(reset);

        parent.Add(Root);
    }

    public void Open()
    {
        starsLabel.text = "★ 残り " + Upgrades.StarsLeft() + "　／　手に入れた★ " + Upgrades.StarsEarned();
        list.Clear();
        foreach (UpgradeData u in Upgrades.All) list.Add(Row(u));
    }

    private VisualElement Row(UpgradeData u)
    {
        int level = Progress.UpgradeLevel(u.id);
        var row = UIFactory.Box("upgrade-row");

        var names = UIFactory.Box("upgrade-names");
        names.Add(UIFactory.Text(u.name, UIFont.Title, "upgrade-name"));
        // いまの効果（まだなら、1 段目の効果）
        names.Add(UIFactory.Text(SkillText.Format(level > 0 ? "いま：" + u.Describe(level) : "1 段目：" + u.Describe(1)), UIFont.Medium, "upgrade-effect"));
        row.Add(names);

        var pips = UIFactory.Box("upgrade-pips");
        for (int i = 0; i < u.MaxLevel; i++)
        {
            var pip = UIFactory.Box("upgrade-pip");
            pip.EnableInClassList("upgrade-pip--on", i < level);
            pips.Add(pip);
        }
        row.Add(pips);

        int cost = Upgrades.NextCost(u);
        bool canBuy = cost >= 0 && cost <= Upgrades.StarsLeft();
        var buy = new C3Button(IconKind.Star, 22f, cost < 0 ? "そろえた" : cost + " でそろえる", () =>
        {
            if (!Upgrades.Buy(u)) return;
            AudioManager.PlaySE("buff");
            Open();
        });
        buy.AddToClassList("upgrade-buy");
        buy.Interactable = canBuy;
        buy.Active = canBuy;
        row.Add(buy);
        return row;
    }
}
