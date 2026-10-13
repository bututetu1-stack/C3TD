using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 必殺技のカットイン。斜めの帯が開き、絵が右から、技の名前とセリフが左から滑り込んで、閉じる。
/// 絵は立ち絵（Art/CutIn/{id}.png）があれば大きく、なければアイコンを四角く出す。
/// 実時間で動き、クリックは受けない（ゲームは止めない）。
/// </summary>
public class CutIn
{
    private const float Duration = 1.7f;

    private readonly VisualElement layer;
    private readonly ChamferBox band;
    private readonly VisualElement portrait;
    private readonly Label portraitInitial;
    private readonly VisualElement figure;
    private readonly VisualElement textBox;
    private readonly Label caption;
    private readonly Label skillName;
    private readonly Label line;
    private IVisualElementScheduledItem tween;

    public CutIn(VisualElement root)
    {
        layer = UIFactory.Box("full", "cutin-layer");
        band = new ChamferBox();
        band.AddToClassList("cutin-band");
        band.SetFill(Palette.UiBand).SetCut(0f, ChamferBox.Corners.None).SetStripes(Palette.UiStripe, 22f);
        band.pickingMode = PickingMode.Ignore;

        textBox = UIFactory.Box("cutin-text");
        caption = UIFactory.Text("", UIFont.Display, "cutin-caption");
        textBox.Add(caption);
        skillName = UIFactory.Text("", UIFont.Title, "cutin-name");
        textBox.Add(skillName);
        line = UIFactory.Text("", UIFont.Body, "cutin-line");
        textBox.Add(line);
        band.Add(textBox);

        portrait = UIFactory.Box("cutin-portrait");
        portraitInitial = UIFactory.Text("", UIFont.Title, "cutin-initial");
        portrait.Add(portraitInitial);
        band.Add(portrait);

        figure = UIFactory.Box("cutin-figure");
        band.Add(figure);

        layer.Add(band);
        root.Add(layer);
        UIFactory.SetVisible(layer, false);
    }

    public void Play(Character c)
    {
        if (c == null || c.Skills == null || c.Skills.Ultimate == null) return;
        SkillData ult = c.Skills.Ultimate;
        Color classColor = c.ClassColor;

        caption.text = "ULTIMATE  /  " + c.Data.name;
        skillName.text = ult.name;
        line.text = string.IsNullOrEmpty(ult.line) ? "" : "「" + ult.line + "」";
        UIFactory.SetVisible(line, !string.IsNullOrEmpty(ult.line));
        band.SetAccent(classColor, 14f);

        Sprite art = Art.CutIn(c.Data.id);
        UIFactory.SetVisible(figure, art != null);
        UIFactory.SetVisible(portrait, art == null);
        if (art != null)
        {
            figure.style.backgroundImage = new StyleBackground(art);
        }
        else
        {
            Sprite icon = Art.Icon(c.Data.id);
            portrait.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
            portrait.style.backgroundColor = icon != null ? Color.clear : Palette.Hex(c.Data.color, classColor);
            portraitInitial.text = icon != null ? "" : c.Data.Initial;
            portrait.style.borderLeftColor = portrait.style.borderRightColor = portrait.style.borderTopColor = portrait.style.borderBottomColor = classColor;
        }

        UIFactory.SetVisible(layer, true);
        UITween.Stop(ref tween);
        tween = UITween.Run(layer, Duration, t =>
        {
            float time = t * Duration;
            float open = Ease.OutCubic(time / 0.18f);
            float close = Ease.InCubic((time - (Duration - 0.35f)) / 0.35f);
            band.style.scale = new Scale(new Vector3(1f, Mathf.Max(0.001f, open * (1f - close)), 1f));
            layer.style.opacity = 1f - close;

            float slide = Ease.OutCubic((time - 0.05f) / 0.35f);
            var move = new Translate((1f - slide) * 420f - time * 18f, 0f);
            portrait.style.translate = move;
            figure.style.translate = move;
            float text = Ease.OutCubic((time - 0.12f) / 0.4f);
            textBox.style.translate = new Translate((1f - text) * -160f + time * 14f, 0f);
            textBox.style.opacity = text;
        }, () => UIFactory.SetVisible(layer, false));
    }
}
