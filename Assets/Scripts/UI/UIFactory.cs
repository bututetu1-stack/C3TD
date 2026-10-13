using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit の画面をコードで組み立てるためのヘルパー。
/// PanelSettings は実行時に作り、配置と文字の大きさ・色は Resources/UI/Theme.uss、
/// フォントはコード（GameFonts）で付ける。
/// </summary>
public static class UIFactory
{
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    private static PanelSettings panelSettings;
    private static StyleSheet theme;
    private static Texture2D vignette;
    private static Texture2D fadeDown;
    private static Texture2D fadeUp;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        panelSettings = null;
        theme = null;
        vignette = null;
        fadeDown = null;
        fadeUp = null;
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
        root.style.unityFontDefinition = GameFonts.UI(UIFont.Body);
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

    /// <summary>文字。大きさと色は USS のクラスで、フォントは役割で決める</summary>
    public static Label Text(string text, UIFont font, params string[] classes)
    {
        var label = new Label(text);
        label.AddToClassList("c3-text");
        foreach (var c in classes) label.AddToClassList(c);
        label.style.unityFontDefinition = GameFonts.UI(font);
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    /// <summary>画像を引き伸ばして貼った要素（tint で色を付ける）</summary>
    public static VisualElement Image(Texture2D texture, Color tint, params string[] classes)
    {
        var image = Box(classes);
        image.AddToClassList("stretch-image");
        image.style.backgroundImage = new StyleBackground(texture);
        image.style.unityBackgroundImageTintColor = tint;
        return image;
    }

    public static void SetVisible(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// 画面のピクセル座標（左下が原点）を、root の座標（UI の参照解像度、左上が原点）にする。
    /// RuntimePanelUtils.ScreenToPanel は拡大縮小を反映しないことがあるので、比率で計算する。
    /// </summary>
    public static Vector2 ScreenToPanel(VisualElement root, Vector2 screen, float screenWidth, float screenHeight)
    {
        if (screenWidth <= 0f || screenHeight <= 0f) return Vector2.zero;
        return new Vector2(screen.x / screenWidth * root.layout.width, (1f - screen.y / screenHeight) * root.layout.height);
    }

    /// <summary>ワールド座標を、root の座標にする（カメラの描画先の大きさで割るので、撮影用の RenderTexture でも合う）</summary>
    public static Vector2 WorldToPanel(VisualElement root, Camera cam, Vector3 world)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        return ScreenToPanel(root, sp, cam.pixelWidth, cam.pixelHeight);
    }

    /// <summary>秒を「分:秒」にする</summary>
    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    // ---------------- 生成するテクスチャ ----------------

    /// <summary>四隅ほど濃くなる周辺減光</summary>
    public static Texture2D Vignette
    {
        get
        {
            if (vignette == null)
            {
                vignette = Generate(128, 128, (u, v) =>
                {
                    float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                    float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                    float t = Mathf.Clamp01((r - 0.55f) / 0.75f);
                    return t * t * (3f - 2f * t);
                });
            }
            return vignette;
        }
    }

    /// <summary>上が濃く、下へ向かって透明になる</summary>
    public static Texture2D FadeDown
    {
        get
        {
            if (fadeDown == null) fadeDown = Generate(4, 128, (u, v) => v * v);
            return fadeDown;
        }
    }

    /// <summary>下が濃く、上へ向かって透明になる</summary>
    public static Texture2D FadeUp
    {
        get
        {
            if (fadeUp == null) fadeUp = Generate(4, 128, (u, v) => (1f - v) * (1f - v));
            return fadeUp;
        }
    }

    /// <summary>白地に不透明度だけを入れたテクスチャ。v は下が0、上が1</summary>
    private static Texture2D Generate(int w, int h, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color(1f, 1f, 1f, alpha((x + 0.5f) / w, (y + 0.5f) / h));
        tex.SetPixels32(px);
        tex.Apply(false, true);
        tex.hideFlags = HideFlags.DontSave;
        return tex;
    }
}
