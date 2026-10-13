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

    public void HideAll()
    {
        root.style.display = DisplayStyle.None;
        current = null;
    }

    /// <summary>画面を切り替える。新しい画面は少し右から滑り込む</summary>
    private void Show(VisualElement screen)
    {
        root.style.display = DisplayStyle.Flex;
        foreach (VisualElement s in new[] { title.Root, select.Root, squad.Root, book.Root }) UIFactory.SetVisible(s, s == screen);
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
            if (current == squad.Root) bootstrap.ShowStageSelect(squad.StageId);
            else if (current == select.Root || current == book.Root) bootstrap.ShowTitle();
        }
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
