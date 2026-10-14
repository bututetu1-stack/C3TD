using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 設定の札（BGM・効果音の音量）。タイトルと出撃中の一時停止メニューから開き、画面の上に重ねる。
/// 音量は AudioManager が PlayerPrefs に覚える（クリアの記録とは別）。
/// </summary>
public class SettingsPanel
{
    private const int Steps = 10;

    public readonly VisualElement Layer;
    private readonly List<VisualElement> bgmCells = new List<VisualElement>();
    private readonly List<VisualElement> seCells = new List<VisualElement>();
    private readonly Label bgmValue;
    private readonly Label seValue;

    public bool IsOpen { get { return UIFactory.IsVisible(Layer); } }

    public SettingsPanel(VisualElement root)
    {
        Layer = UIFactory.Box("full", "settings-layer");
        Layer.pickingMode = PickingMode.Position; // 後ろを押させない

        var panel = new ChamferBox();
        panel.AddToClassList("settings-panel");
        panel.SetFill(new Color(0.035f, 0.05f, 0.09f, 1f)).SetStroke(Palette.UiPanelLine, 1f)
            .SetCut(24f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetAccent(Palette.Amber, 6f).SetStripes(Palette.UiStripe, 20f);
        panel.pickingMode = PickingMode.Position;
        panel.Add(UIFactory.Text("SETTINGS", UIFont.Display, "menu-caption"));
        panel.Add(UIFactory.Text("設定", UIFont.Title, "settings-title"));

        bgmValue = AddRow(panel, "BGM", bgmCells, v => AudioManager.BgmLevel = v, () => AudioManager.BgmLevel, false);
        seValue = AddRow(panel, "効果音", seCells, v => AudioManager.SeLevel = v, () => AudioManager.SeLevel, true);

        if (!Application.isMobilePlatform)
            panel.Add(UIFactory.Text("出撃中のキー：1〜9 カードを選ぶ　S 必殺技　T 狙い方　Space 一時停止　Esc・右クリック 取り消し", UIFont.Medium, "settings-keys"));

        var close = new C3Button(IconKind.Back, 26f, "閉じる", Hide);
        close.AddToClassList("settings-close");
        close.Active = true;
        panel.Add(close);

        Layer.Add(panel);
        root.Add(Layer);
        UIFactory.SetVisible(Layer, false);
    }

    /// <summary>音量の1行：－・10 個の目盛り（押すとその大きさ）・＋・いまの%</summary>
    private Label AddRow(VisualElement parent, string caption, List<VisualElement> cells, System.Action<float> set, System.Func<float> get, bool preview)
    {
        var row = UIFactory.Box("settings-row");
        row.Add(UIFactory.Text(caption, UIFont.Body, "settings-caption"));
        var value = UIFactory.Text("", UIFont.Display, "settings-value");

        System.Action<float> apply = v =>
        {
            set(Mathf.Clamp01(v));
            Refresh();
            if (preview) AudioManager.PlaySE("click");
        };
        var minus = new C3Button(null, 0f, "-", () => apply(get() - 1f / Steps));
        minus.AddToClassList("settings-step");
        row.Add(minus);
        var bar = UIFactory.Box("settings-bar");
        for (int i = 0; i < Steps; i++)
        {
            int level = i + 1;
            var cell = UIFactory.Box("vol-cell");
            cell.pickingMode = PickingMode.Position;
            cell.AddManipulator(new Clickable(() => apply(level / (float)Steps)));
            bar.Add(cell);
            cells.Add(cell);
        }
        row.Add(bar);
        var plus = new C3Button(null, 0f, "＋", () => apply(get() + 1f / Steps));
        plus.AddToClassList("settings-step");
        row.Add(plus);
        row.Add(value);
        parent.Add(row);
        return value;
    }

    public void Show()
    {
        Refresh();
        UIFactory.SetVisible(Layer, true);
    }

    public void Hide()
    {
        UIFactory.SetVisible(Layer, false);
    }

    private void Refresh()
    {
        Fill(bgmCells, bgmValue, AudioManager.BgmLevel);
        Fill(seCells, seValue, AudioManager.SeLevel);
    }

    private static void Fill(List<VisualElement> cells, Label value, float level)
    {
        int on = Mathf.RoundToInt(level * Steps);
        for (int i = 0; i < cells.Count; i++) cells[i].EnableInClassList("vol-cell--on", i < on);
        value.text = on == 0 ? "OFF" : Mathf.RoundToInt(level * 100f) + "%";
    }
}
