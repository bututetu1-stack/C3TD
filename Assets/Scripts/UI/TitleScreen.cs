using UnityEngine.UIElements;

/// <summary>タイトル画面。「作戦開始」で作戦選択へ。下に「育成」「ガチャ」「強化」、その下に「敵図鑑」「遊び方」「記録」「設定」</summary>
public class TitleScreen
{
    public readonly VisualElement Root;
    private readonly Label freeBadge;

    /// <summary>タイトルを出すたびに：今日の無料ガチャがまだなら「無料」の札</summary>
    public void Refresh()
    {
        UIFactory.SetVisible(freeBadge, Growth.FreePullAvailable());
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
        if (!UnityEngine.Application.isMobilePlatform) block.Add(UIFactory.Text("Space・Enter でも始められます", UIFont.Medium, "title-hint"));
        Root.Add(block);

        parent.Add(Root);
    }
}
