using UnityEngine.UIElements;

/// <summary>タイトル画面。「作戦開始」で作戦選択へ</summary>
public class TitleScreen
{
    public readonly VisualElement Root;

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
        block.Add(UIFactory.Text("Space・Enter でも始められます", UIFont.Medium, "title-hint"));
        Root.Add(block);

        parent.Add(Root);
    }
}
