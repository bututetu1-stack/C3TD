using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// 出撃前の画面（タイトル・作戦選択・編成・敵図鑑）。背景は戦闘画面の背景の絵を暗くして敷く。
/// Esc で1つ前の画面に戻る。
/// </summary>
public class MenuUI : MonoBehaviour
{
    private GameBootstrap bootstrap;
    private VisualElement root;
    private TitleScreen title;
    private StageSelectScreen select;
    private SquadScreen squad;
    private EnemyBookScreen book;
    private HelpScreen help;
    private RecordsScreen records;
    private UpgradeScreen upgrade;
    private ClassGuideScreen classGuide;
    private GrowthScreen growth;
    private GachaScreen gacha;
    private Action classGuideBack;
    private SettingsPanel settings;
    private VisualElement current;
    private IVisualElementScheduledItem showTween;

    public void Init(GameBootstrap owner)
    {
        bootstrap = owner;
        root = UIFactory.CreateScreen("Menu", transform, 5);
        root.pickingMode = PickingMode.Position;
        BuildBackground();
        title = new TitleScreen(root, bootstrap);
        select = new StageSelectScreen(root, bootstrap);
        squad = new SquadScreen(root, bootstrap);
        book = new EnemyBookScreen(root, bootstrap);
        help = new HelpScreen(root, bootstrap);
        records = new RecordsScreen(root, bootstrap);
        upgrade = new UpgradeScreen(root, bootstrap);
        classGuide = new ClassGuideScreen(root, () => { if (classGuideBack != null) classGuideBack(); });
        growth = new GrowthScreen(root, bootstrap);
        gacha = new GachaScreen(root, bootstrap);
        settings = new SettingsPanel(root);
        HideAll();
    }

    private void BuildBackground()
    {
        var bg = UIFactory.Box("full", "menu-bg");
        // タイトル用の絵があればそれ、なければ戦闘画面の背景
        Sprite art = Art.Background("Title");
        if (art == null) art = Art.Background("Battle");
        if (art != null)
        {
            bg.style.backgroundImage = new StyleBackground(art);
            bg.style.unityBackgroundImageTintColor = new Color(0.42f, 0.45f, 0.55f);
        }
        root.Add(bg);
        root.Add(UIFactory.Image(UIFactory.Vignette, Palette.UiVignette, "full"));
        root.Add(UIFactory.Image(UIFactory.FadeDown, Palette.UiShade, "top-shade"));
        root.Add(UIFactory.Image(UIFactory.FadeUp, Palette.UiShade, "bottom-shade"));
    }

    public void ShowTitle()
    {
        title.Refresh();
        Show(title.Root);
    }

    public void ShowStageSelect(string focus)
    {
        select.Open(focus);
        Show(select.Root);
    }

    public void ShowSquad(string stageId, List<string> squadIds)
    {
        squad.Open(stageId, squadIds);
        Show(squad.Root);
    }

    public void ShowEnemyBook()
    {
        book.Open();
        Show(book.Root);
    }

    public void ShowHelp()
    {
        Show(help.Root);
    }

    public void ShowRecords()
    {
        records.Open();
        Show(records.Root);
    }

    public void ShowUpgrades()
    {
        upgrade.Open();
        Show(upgrade.Root);
    }

    /// <summary>育成。pastCoins は、この仕組みの前にクリアした作戦のぶんとして渡した部費（お知らせを出す）</summary>
    public void ShowGrowth(int pastCoins)
    {
        growth.Open(pastCoins);
        Show(growth.Root);
    }

    public void ShowGacha()
    {
        gacha.Open();
        Show(gacha.Root);
    }

    /// <summary>職業の説明（遊び方・編成画面から）。back は「戻る」と Esc で戻る先</summary>
    public void ShowClassGuide(Action back)
    {
        classGuideBack = back;
        classGuide.Open();
        Show(classGuide.Root);
    }

    public void ShowSettings()
    {
        settings.Show();
    }

    public void HideAll()
    {
        settings.Hide();
        root.style.display = DisplayStyle.None;
        current = null;
    }

    /// <summary>画面を切り替える。新しい画面は少し右から滑り込む</summary>
    private void Show(VisualElement screen)
    {
        root.style.display = DisplayStyle.Flex;
        foreach (VisualElement s in new[] { title.Root, select.Root, squad.Root, book.Root, help.Root, records.Root, upgrade.Root, classGuide.Root, growth.Root, gacha.Root }) UIFactory.SetVisible(s, s == screen);
        current = screen;
        AudioManager.PlayBGM("menu");
        UITween.Stop(ref showTween);
        showTween = UITween.Run(screen, 0.35f, t =>
        {
            float e = Ease.OutCubic(t);
            screen.style.opacity = e;
            screen.style.translate = new Translate((1f - e) * 40f, 0f);
        });
    }

    void Update()
    {
        if (current == null) return;
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame)
        {
            // 設定の札が開いていれば、先にそれを閉じる。ガチャの演出中なら、演出が受け取る
            if (settings.IsOpen) settings.Hide();
            else if (current == gacha.Root && gacha.ShowOpen) gacha.Escape();
            else if (current == squad.Root) bootstrap.ShowStageSelect(squad.StageId);
            else if (current == classGuide.Root) { if (classGuideBack != null) classGuideBack(); }
            else if (current == select.Root || current == book.Root || current == help.Root || current == records.Root || current == upgrade.Root || current == growth.Root || current == gacha.Root) bootstrap.ShowTitle();
            return;
        }
        if (settings.IsOpen || (current == gacha.Root && gacha.ShowOpen)) return;
        if (current == title.Root && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            bootstrap.ShowStageSelect();
    }

    // ================= 画面で共通の部品 =================

    /// <summary>上の帯：戻るボタン・英字の小見出し・題名</summary>
    public static VisualElement Header(VisualElement parent, string english, string title, Action onBack, out Label sub)
    {
        var header = UIFactory.Box("menu-header");
        if (onBack != null)
        {
            var back = new C3Button(IconKind.Back, 26f, "戻る", onBack);
            back.AddToClassList("menu-back");
            header.Add(back);
        }
        var titles = UIFactory.Box("menu-titles");
        titles.Add(UIFactory.Text(english, UIFont.Display, "menu-caption"));
        titles.Add(UIFactory.Text(title, UIFont.Title, "menu-title"));
        header.Add(titles);
        header.Add(UIFactory.Box("top-spacer"));
        sub = UIFactory.Text("", UIFont.Body, "menu-sub");
        header.Add(sub);
        parent.Add(header);
        return header;
    }

    /// <summary>SP の溜まり方を日本語で</summary>
    public static string SpChargeName(SpCharge charge)
    {
        switch (charge)
        {
            case SpCharge.OnAttack: return "攻撃で溜まる";
            case SpCharge.OnHit: return "攻撃を受けて溜まる";
            default: return "時間で溜まる";
        }
    }
}
