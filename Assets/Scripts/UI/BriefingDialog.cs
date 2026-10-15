using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 時間を止めて読む説明の札（作戦のヒント・初めて会う敵）。画面のまん中に出し、「OK」か Space・Enter・Esc で次へ。
/// 出しているあいだは TimeController.SetHeld(true)（敵も時間も止まる。配置に忙しいときに読まなくてよいように）。
/// </summary>
public class BriefingDialog
{
    public class Entry
    {
        public string Caption;
        public string Title;
        public string Body;

        /// <summary>敵の絵（なければ IconLabel を色付きの丸に入れる。どちらもなければアイコンなし）</summary>
        public Sprite Icon;
        public string IconLabel;
        public Color IconColor = Palette.Crimson;
        public bool Warning;
    }

    public readonly VisualElement Layer;
    private readonly ChamferBox card;
    private readonly VisualElement icon;
    private readonly Label iconLabel;
    private readonly Label caption;
    private readonly Label title;
    private readonly Label body;
    private readonly Queue<Entry> queue = new Queue<Entry>();
    private IVisualElementScheduledItem tween;

    public bool IsOpen { get { return UIFactory.IsVisible(Layer); } }

    public BriefingDialog(VisualElement root)
    {
        Layer = UIFactory.Box("full", "briefing-layer");
        Layer.pickingMode = PickingMode.Position; // 後ろ（マップ・編成バー）を押させない

        card = new ChamferBox();
        card.AddToClassList("briefing-card");
        card.SetFill(new Color(0.035f, 0.05f, 0.09f, 1f)).SetStroke(Palette.UiPanelLine, 1f)
            .SetCut(22f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetStripes(Palette.UiStripe, 20f);
        card.pickingMode = PickingMode.Position;

        var head = UIFactory.Box("briefing-head");
        icon = UIFactory.Box("briefing-icon");
        iconLabel = UIFactory.Text("", UIFont.Title, "briefing-icon-label");
        icon.Add(iconLabel);
        head.Add(icon);
        var titles = UIFactory.Box("briefing-titles");
        caption = UIFactory.Text("", UIFont.Display, "briefing-caption");
        titles.Add(caption);
        title = UIFactory.Text("", UIFont.Title, "briefing-title");
        titles.Add(title);
        head.Add(titles);
        card.Add(head);

        body = UIFactory.Text("", UIFont.Medium, "briefing-body");
        card.Add(body);

        var footer = UIFactory.Box("briefing-footer");
        footer.Add(UIFactory.Text(Application.isMobilePlatform ? "時間は止まっている" : "時間は止まっている　Space・Enter でも閉じる", UIFont.Medium, "briefing-note"));
        footer.Add(UIFactory.Box("top-spacer"));
        var ok = new C3Button(IconKind.Play, 22f, "OK（続ける）", Next);
        ok.AddToClassList("briefing-ok");
        ok.Active = true;
        footer.Add(ok);
        card.Add(footer);

        Layer.Add(card);
        root.Add(Layer);
        UIFactory.SetVisible(Layer, false);
    }

    public void Enqueue(Entry e)
    {
        if (e != null) queue.Enqueue(e);
    }

    /// <summary>毎フレーム：閉じていて、待っている札があれば出す（一時停止のメニューを出しているときは待つ）</summary>
    public void Update()
    {
        if (!IsOpen && queue.Count > 0 && !TimeController.Paused) ShowNext();
    }

    /// <summary>OK：次の札へ（なければ閉じて時間を戻す）</summary>
    public void Next()
    {
        AudioManager.PlaySE("click");
        if (queue.Count > 0) ShowNext();
        else Close();
    }

    /// <summary>出撃が終わった・やり直したとき：全部捨てて時間を戻す</summary>
    public void Clear()
    {
        queue.Clear();
        Close();
    }

    private void ShowNext()
    {
        Entry e = queue.Dequeue();
        caption.text = e.Caption ?? "";
        caption.style.color = e.Warning ? Palette.Crimson : Palette.Amber;
        title.text = e.Title ?? "";
        body.text = SkillText.Format(e.Body ?? "");
        bool hasIcon = e.Icon != null || !string.IsNullOrEmpty(e.IconLabel);
        UIFactory.SetVisible(icon, hasIcon);
        icon.style.backgroundImage = e.Icon != null ? new StyleBackground(e.Icon) : new StyleBackground(StyleKeyword.None);
        icon.style.backgroundColor = e.Icon != null ? new Color(1f, 1f, 1f, 0.06f) : e.IconColor;
        iconLabel.text = e.Icon != null ? "" : (e.IconLabel ?? "");
        Color frame = e.Warning ? Palette.Crimson : Palette.Amber;
        icon.style.borderLeftColor = icon.style.borderRightColor = icon.style.borderTopColor = icon.style.borderBottomColor = frame;
        card.SetAccent(frame, 6f);

        UIFactory.SetVisible(Layer, true);
        TimeController.SetHeld(true);
        UITween.Stop(ref tween);
        tween = UITween.Run(card, 0.25f, t =>
        {
            float k = Ease.OutCubic(t);
            card.style.opacity = k;
            card.style.translate = new Translate(0f, (1f - k) * 24f);
        });
        AudioManager.PlaySE("skill_ready");
    }

    private void Close()
    {
        UITween.Stop(ref tween);
        UIFactory.SetVisible(Layer, false);
        TimeController.SetHeld(false);
    }
}
