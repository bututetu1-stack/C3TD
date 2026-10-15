using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 育成の「昇進」を祝う画面（全体に重ねる。押すと閉じる）。部員のアイコンをそのまま大きく出し（AI で描き直さない）、
/// 後ろで光線が回り、光の粒が舞い、「昇進！」と効き目を出す。ガチャの演出と同じ光と音（GachaShow.RaysTexture・GachaSound）。
/// </summary>
public class Celebration
{
    private readonly VisualElement layer;
    private readonly VisualElement icon;
    private readonly Label title;
    private readonly Label name;
    private readonly Label detail;
    private readonly VisualElement rays;
    private readonly VisualElement sparks;
    private IVisualElementScheduledItem loop;
    private float openedAt;

    public Celebration(VisualElement root)
    {
        layer = UIFactory.Box("full", "celebrate");
        layer.pickingMode = PickingMode.Position;
        layer.RegisterCallback<PointerDownEvent>(e =>
        {
            // 出た直後の押しまちがいで閉じないよう、少し待つ
            if (Time.unscaledTime - openedAt > 0.6f) Hide();
        });

        rays = UIFactory.Box("celebrate-rays", "gacha-img");
        rays.style.backgroundImage = new StyleBackground(GachaShow.RaysTexture());
        rays.style.unityBackgroundImageTintColor = new Color(1f, 0.8f, 0.35f, 0.8f);
        layer.Add(rays);
        var glow = UIFactory.Box("celebrate-glow", "gacha-img");
        glow.style.backgroundImage = new StyleBackground(SpriteFactory.Glow);
        glow.style.unityBackgroundImageTintColor = new Color(1f, 0.75f, 0.3f, 0.6f);
        layer.Add(glow);
        sparks = UIFactory.Box("full");
        layer.Add(sparks);

        var column = UIFactory.Box("celebrate-column");
        icon = UIFactory.Box("celebrate-icon");
        column.Add(icon);
        title = UIFactory.Text("昇進！", UIFont.Title, "celebrate-title");
        column.Add(title);
        name = UIFactory.Text("", UIFont.Title, "celebrate-name");
        column.Add(name);
        detail = UIFactory.Text("", UIFont.Body, "celebrate-detail");
        column.Add(detail);
        column.Add(UIFactory.Text("押して閉じる", UIFont.Medium, "celebrate-hint"));
        layer.Add(column);

        root.Add(layer);
        UIFactory.SetVisible(layer, false);
    }

    public void Show(CharacterData d, int promotion, string description)
    {
        Sprite art = Art.Icon(d.id);
        icon.style.backgroundImage = art != null ? new StyleBackground(art) : new StyleBackground(StyleKeyword.None);
        icon.style.backgroundColor = art != null ? Color.clear : Palette.Hex(d.color, Palette.Amber);
        icon.Clear();
        if (art == null) icon.Add(UIFactory.Text(d.Initial, UIFont.Title, "celebrate-initial"));
        title.text = "昇進 " + new string('★', Mathf.Max(1, promotion)) + "！";
        name.text = d.name;
        detail.text = SkillText.Format(description ?? "");
        openedAt = Time.unscaledTime;
        UIFactory.SetVisible(layer, true);

        AudioManager.Duck(0.35f);
        GachaSound.Play("burst", 0.9f);
        GachaSound.Play("rare3", 0.7f);
        UITween.Run(layer, 0.5f, t =>
        {
            float e = Ease.OutBack(t);
            icon.style.scale = new Scale(new Vector3(Mathf.LerpUnclamped(2f, 1f, e), Mathf.LerpUnclamped(2f, 1f, e), 1f));
            layer.style.opacity = Mathf.Clamp01(t * 3f);
            title.style.scale = new Scale(new Vector3(Mathf.LerpUnclamped(0.3f, 1f, e), Mathf.LerpUnclamped(0.3f, 1f, e), 1f));
        });
        if (loop != null) loop.Pause();
        loop = layer.schedule.Execute(() =>
        {
            float t = Time.unscaledTime;
            rays.style.rotate = new Rotate(new Angle(t * 18f, AngleUnit.Degree));
            if (Random.value < 0.35f) Spark();
        }).Every(0);
    }

    private void Spark()
    {
        float w = layer.layout.width, h = layer.layout.height;
        if (float.IsNaN(w) || w < 10f) return;
        float x = Random.Range(0f, w), y = Random.Range(0f, h);
        float size = Random.Range(10f, 34f);
        var p = UIFactory.Box("gacha-img");
        p.style.position = Position.Absolute;
        p.style.width = size;
        p.style.height = size;
        p.style.left = x - size * 0.5f;
        p.style.top = y - size * 0.5f;
        Sprite star = Art.Effect("GachaSparkle");
        p.style.backgroundImage = new StyleBackground(star != null ? star : SpriteFactory.Glow);
        if (star == null) p.style.unityBackgroundImageTintColor = new Color(1f, 0.85f, 0.4f);
        sparks.Add(p);
        UITween.Run(p, 1f, t =>
        {
            float s = Mathf.Sin(t * Mathf.PI);
            p.style.opacity = s;
            p.style.scale = new Scale(new Vector3(s, s, 1f));
            p.style.top = y - size * 0.5f - 40f * t;
        }, () => p.RemoveFromHierarchy());
    }

    public void Hide()
    {
        if (loop != null) loop.Pause();
        loop = null;
        sparks.Clear();
        AudioManager.Duck(1f);
        UIFactory.SetVisible(layer, false);
    }
}
