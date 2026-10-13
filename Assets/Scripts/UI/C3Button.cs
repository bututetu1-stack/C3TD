using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 角を落としたボタン。アイコンと文字を並べられる。
/// Active にすると琥珀色に点灯する（2倍速・一時停止中など）。
/// </summary>
public class C3Button : ChamferBox
{
    public readonly IconView Icon;
    public readonly Label Text;

    private bool hover;
    private bool pressed;
    private bool active;

    public C3Button(IconKind? icon, float iconSize, string text, Action onClick)
    {
        AddToClassList("c3-button");
        if (icon.HasValue)
        {
            Icon = new IconView(icon.Value, iconSize, Palette.Text);
            Add(Icon);
        }
        if (text != null)
        {
            Text = UIFactory.Text(text, UIFont.Body, "c3-button-text");
            Add(Text);
        }

        RegisterCallback<PointerEnterEvent>(e => { hover = true; Restyle(); });
        RegisterCallback<PointerLeaveEvent>(e => { hover = false; pressed = false; Restyle(); });
        RegisterCallback<PointerDownEvent>(e => { pressed = true; Restyle(); }, TrickleDown.TrickleDown);
        RegisterCallback<PointerUpEvent>(e => { pressed = false; Restyle(); }, TrickleDown.TrickleDown);
        this.AddManipulator(new Clickable(() =>
        {
            AudioManager.PlaySE("click");
            if (onClick != null) onClick();
        }));

        SetCut(12f, Corners.TopLeft | Corners.BottomRight);
        Restyle();
    }

    public bool Active
    {
        get { return active; }
        set
        {
            if (active == value) return;
            active = value;
            Restyle();
        }
    }

    /// <summary>押せるか（押せないときは暗くなり、クリックも受けない）</summary>
    public bool Interactable
    {
        get { return enabledSelf; }
        set
        {
            if (enabledSelf == value) return;
            SetEnabled(value);
            if (!value) hover = pressed = false;
            Restyle();
        }
    }

    private void Restyle()
    {
        bool usable = enabledSelf;
        Color fill;
        if (!usable) fill = Palette.WithAlpha(Palette.UiButton, 0.75f);
        else if (active) fill = hover ? Palette.AmberBright : Palette.Amber;
        else fill = hover ? Palette.UiButtonHover : Palette.UiButton;
        if (pressed) fill = Color.Lerp(fill, Color.white, 0.18f);
        SetFill(fill);
        SetStroke(usable && active ? Palette.AmberBright : Palette.UiButtonLine, 1.5f);

        Color fg = !usable ? Palette.TextSub : active ? Palette.Ink : Palette.Text;
        if (Icon != null) Icon.SetColor(fg);
        if (Text != null) Text.style.color = fg;
        style.scale = new Scale(pressed ? new Vector3(0.95f, 0.95f, 1f) : Vector3.one);
    }
}
