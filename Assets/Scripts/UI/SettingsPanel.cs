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
    private readonly C3Button hintButton;
    private readonly C3Button gachaButton;

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

        // 出撃中の説明（作戦のヒント・初めて会う敵）を出すか。出すあいだは時間が止まる
        var hintRow = UIFactory.Box("settings-row");
        hintRow.Add(UIFactory.Text("説明", UIFont.Body, "settings-caption"));
        hintButton = new C3Button(null, 0f, "", () =>
        {
            GameSettings.HintsOn = !GameSettings.HintsOn;
            Refresh();
            AudioManager.PlaySE("click");
        });
        hintButton.AddToClassList("settings-toggle");
        hintRow.Add(hintButton);
        panel.Add(hintRow);

        // ガチャの演出の長さ
        var gachaRow = UIFactory.Box("settings-row");
        gachaRow.Add(UIFactory.Text("ガチャ", UIFont.Body, "settings-caption"));
        gachaButton = new C3Button(null, 0f, "", () =>
        {
            GameSettings.GachaShort = !GameSettings.GachaShort;
            Refresh();
            AudioManager.PlaySE("click");
        });
        gachaButton.AddToClassList("settings-toggle");
        gachaRow.Add(gachaButton);
        panel.Add(gachaRow);

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
        hintButton.Text.text = GameSettings.HintsOn ? "出す（作戦をクリアするまで・初めての敵）　時間が止まる" : "出さない";
        hintButton.Active = GameSettings.HintsOn;
        gachaButton.Text.text = GameSettings.GachaShort ? "演出を短く（すぐ開く。★3 は大きく見せる）" : "演出をぜんぶ見る（箱をためて、押して開ける）";
        gachaButton.Active = !GameSettings.GachaShort;
    }

    private static void Fill(List<VisualElement> cells, Label value, float level)
    {
        int on = Mathf.RoundToInt(level * Steps);
        for (int i = 0; i < cells.Count; i++) cells[i].EnableInClassList("vol-cell--on", i < on);
        value.text = on == 0 ? "OFF" : Mathf.RoundToInt(level * 100f) + "%";
    }
}
