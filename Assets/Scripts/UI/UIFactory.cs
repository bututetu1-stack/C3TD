using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit の画面をコードで組み立てるためのヘルパー。
/// PanelSettings は実行時に作り、見た目は Resources/UI/Theme.uss で付ける。
/// </summary>
public static class UIFactory
{
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    private static PanelSettings panelSettings;
    private static StyleSheet theme;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        panelSettings = null;
        theme = null;
    }

    private static PanelSettings SharedPanelSettings
    {
        get
        {
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = "RuntimePanelSettings";
                panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/DefaultTheme");
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int((int)ReferenceWidth, (int)ReferenceHeight);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = 1f; // 高さに合わせる（CameraFitter と同じ）
            }
            return panelSettings;
        }
    }

    private static StyleSheet Theme
    {
        get { return theme != null ? theme : (theme = Resources.Load<StyleSheet>("UI/Theme")); }
    }

    /// <summary>画面全体を覆う UIDocument を作り、そのルートを返す。sortingOrder が大きいほど手前</summary>
    public static VisualElement CreateScreen(string name, Transform parent, int sortingOrder)
    {
        var obj = new GameObject(name);
        obj.SetActive(false); // PanelSettings を設定してから有効にする
        obj.transform.SetParent(parent, false);
        var doc = obj.AddComponent<UIDocument>();
        doc.panelSettings = SharedPanelSettings;
        doc.sortingOrder = sortingOrder;
        obj.SetActive(true);

        VisualElement root = doc.rootVisualElement;
        if (Theme != null) root.styleSheets.Add(Theme);
        root.AddToClassList("c3-root");
        if (GameFonts.Medium != null) root.style.unityFontDefinition = GameFonts.UIRegular;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;
        return root;
    }

    public static VisualElement Box(params string[] classes)
    {
        var box = new VisualElement();
        foreach (var c in classes) box.AddToClassList(c);
        box.pickingMode = PickingMode.Ignore;
        return box;
    }

    public static Label Label(string text, params string[] classes)
    {
        var label = new Label(text);
        foreach (var c in classes) label.AddToClassList(c);
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    /// <summary>太字のラベル（太字用のフォントに切り替える）</summary>
    public static Label BoldLabel(string text, params string[] classes)
    {
        var label = Label(text, classes);
        SetBold(label);
        return label;
    }

    public static Button Button(string text, Action onClick, params string[] classes)
    {
        var button = new Button(() =>
        {
            AudioManager.PlaySE("click");
            if (onClick != null) onClick();
        });
        button.text = text;
        button.AddToClassList("c3-button");
        foreach (var c in classes) button.AddToClassList(c);
        SetBold(button);
        return button;
    }

    public static void SetBold(VisualElement element)
    {
        if (GameFonts.Bold != null) element.style.unityFontDefinition = GameFonts.UIBold;
    }

    public static void SetVisible(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>秒を「分:秒」にする</summary>
    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}
