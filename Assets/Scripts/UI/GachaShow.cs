using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// 部室ガチャの演出（画面いっぱいに重ねる。2026-10-15 オーナー：「射幸心が刺激されない。エフェクトとかもっと作るなり」）。
///   1. 備品箱が落ちてくる（着地で揺れる）
///   2. 箱が 3 回ふくらんで光る。光の色がいちばん良い★（白 ★1・青 ★2・金 ★3）。ときどき青から金に変わる「昇格演出」
///   3. 押すと箱がはじけ、光と粒が飛ぶ（★3 は画面が大きく揺れて、後ろで光線が回る）
///   4. 裏向きのカードが並び、1 枚ずつめくる。★2・★3 のカードはめくる前から光る。★3 は「スポットライト」で大きく見せる
///   5. まとめ（NEW・重ね・部費）と「もう一度」「閉じる」
/// 右上の SKIP でまとめへ。押す・Space・Enter で次へ進む。絵は Art/Gacha（Box・CardBack）と Art/Effects（GachaBurstGold など）、
/// 備品の絵は Art/Items/{id}。なければ図形と光で代わりにする。
/// </summary>
public class GachaShow
{
    public readonly VisualElement Layer;
    public bool IsOpen { get { return UIFactory.IsVisible(Layer); } }

    private readonly MonoBehaviour host;
    private readonly System.Action<int> onAgain;
    private readonly System.Action onClosed;

    private readonly VisualElement dim;
    private readonly VisualElement world;
    private readonly VisualElement back;
    private readonly VisualElement stage;
    private readonly VisualElement front;
    private readonly VisualElement spot;
    private readonly VisualElement flash;
    private readonly Label hint;
    private readonly C3Button skipButton;
    private readonly VisualElement summary;
    private readonly Label summaryText;
    private readonly C3Button againButton;
    private readonly C3Button closeButton;

    private Coroutine running;
    private bool advance;
    private bool skip;
    private int lastCount;
    private float W = 1920f, H = 1080f, S = 1f;
    private readonly List<IVisualElementScheduledItem> loops = new List<IVisualElementScheduledItem>();
    private readonly List<CardView> cards = new List<CardView>();
    private IVisualElementScheduledItem shakeItem;

    private static Texture2D raysTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        raysTexture = null;
    }

    private class CardView
    {
        public GachaResult Result;
        public VisualElement Root;
        public VisualElement Back;
        public VisualElement Face;
        public VisualElement Aura;
        public bool Open;
        public float X, Y, Width, Height;
    }

    public GachaShow(VisualElement root, MonoBehaviour host, System.Action<int> onAgain, System.Action onClosed)
    {
        this.host = host;
        this.onAgain = onAgain;
        this.onClosed = onClosed;

        Layer = UIFactory.Box("full", "gacha-show");
        Layer.pickingMode = PickingMode.Position;
        Layer.RegisterCallback<PointerDownEvent>(e => advance = true);

        dim = UIFactory.Box("full", "gacha-show-dim");
        var center = UIFactory.Box("full", "gacha-img");
        center.style.backgroundImage = new StyleBackground(SpriteFactory.Glow);
        center.style.unityBackgroundImageTintColor = new Color(0.16f, 0.2f, 0.36f, 0.55f);
        dim.Add(center);
        dim.Add(UIFactory.Image(UIFactory.Vignette, new Color(0f, 0f, 0f, 0.9f), "full"));
        Layer.Add(dim);
        world = UIFactory.Box("full");
        back = UIFactory.Box("full");
        stage = UIFactory.Box("full");
        front = UIFactory.Box("full");
        world.Add(back);
        world.Add(stage);
        world.Add(front);
        Layer.Add(world);
        spot = UIFactory.Box("full");
        Layer.Add(spot);
        flash = UIFactory.Box("full", "gacha-flash");
        flash.style.opacity = 0f;
        Layer.Add(flash);

        hint = UIFactory.Text("", UIFont.Body, "gacha-hint");
        Layer.Add(hint);

        skipButton = new C3Button(IconKind.FastForward, 22f, "SKIP", () =>
        {
            skip = true;
            advance = true;
        });
        skipButton.AddToClassList("gacha-skip");
        Layer.Add(skipButton);

        summary = UIFactory.Box("gacha-summary");
        summaryText = UIFactory.Text("", UIFont.Body, "gacha-summary-text");
        summary.Add(summaryText);
        var buttons = UIFactory.Box("gacha-summary-buttons");
        againButton = new C3Button(IconKind.Retry, 24f, "もう一度", () =>
        {
            if (onAgain != null) onAgain(lastCount);
        });
        againButton.AddToClassList("gacha-summary-button");
        againButton.Active = true;
        buttons.Add(againButton);
        closeButton = new C3Button(IconKind.Back, 24f, "閉じる", Close);
        closeButton.AddToClassList("gacha-summary-button");
        buttons.Add(closeButton);
        summary.Add(buttons);
        Layer.Add(summary);

        root.Add(Layer);
        UIFactory.SetVisible(Layer, false);
    }

    // ================= 外から =================

    public void Play(List<GachaResult> results)
    {
        if (results == null || results.Count == 0) return;
        if (running != null) host.StopCoroutine(running);
        lastCount = results.Count;
        Reset();
        UIFactory.SetVisible(Layer, true);
        running = host.StartCoroutine(Run(results));
    }

    /// <summary>Esc：演出中なら飛ばしてまとめへ、まとめなら閉じる</summary>
    public void Escape()
    {
        if (UIFactory.IsVisible(summary)) Close();
        else
        {
            skip = true;
            advance = true;
        }
    }

    public void Close()
    {
        if (running != null) host.StopCoroutine(running);
        running = null;
        Reset();
        UIFactory.SetVisible(Layer, false);
        if (onClosed != null) onClosed();
    }

    /// <summary>もう一度引けるか（まとめの「もう一度」の文と押せるか）</summary>
    public void SetAgain(string text, bool enabled)
    {
        againButton.Text.text = text;
        againButton.Interactable = enabled;
        againButton.Active = enabled;
    }

    private void Reset()
    {
        foreach (IVisualElementScheduledItem l in loops) if (l != null) l.Pause();
        loops.Clear();
        UITween.Stop(ref shakeItem);
        back.Clear();
        stage.Clear();
        front.Clear();
        spot.Clear();
        cards.Clear();
        world.style.translate = new Translate(0f, 0f);
        flash.style.opacity = 0f;
        dim.style.opacity = 1f;
        UIFactory.SetVisible(summary, false);
        UIFactory.SetVisible(skipButton, true);
        hint.text = "";
        skip = false;
        advance = false;
    }

    // ================= 流れ =================

    private IEnumerator Run(List<GachaResult> results)
    {
        // 大きさは、いつも出ている親（ガチャの画面）から読む（出したばかりの Layer はまだ大きさが決まっていない）
        yield return null;
        Rect area = Layer.parent != null ? Layer.parent.layout : Layer.layout;
        if ((float.IsNaN(area.width) || area.width < 10f) && Layer.panel != null) area = Layer.panel.visualTree.layout;
        W = area.width;
        H = area.height;
        if (float.IsNaN(W) || W < 10f) { W = 1920f; H = 1080f; }
        S = H / 1080f;

        int best = 1, golds = 0;
        foreach (GachaResult r in results)
        {
            best = Mathf.Max(best, r.Equipment.rarity);
            if (r.Equipment.rarity >= 3) golds++;
        }
        // ★3 が 2 枚以上：最後の溜めで光が虹色になる（いちばんの当たり）
        bool rainbow = golds >= 2;
        // 昇格演出：いちばん良い★より 1 つ下の色で光りはじめ、途中で変わる
        int shown = best;
        bool upgrade = false;
        if (best == 3 && Random.value < 0.45f) { shown = 2; upgrade = true; }
        else if (best == 2 && Random.value < 0.25f) { shown = 1; upgrade = true; }

        // 1. 背景と、備品箱が落ちてくる
        dim.style.opacity = 0f;
        UITween.Run(dim, 0.25f, t => dim.style.opacity = t);
        float cx = W * 0.5f, cy = H * 0.52f;
        float boxSize = 380f * S;

        Loop(back, () =>
        {
            if (Random.value < 0.15f) Drift();
        });
        VisualElement rays = Image(RaysTexture(), cx, cy, H * 1.6f, H * 1.6f, back);
        rays.style.opacity = 0f;
        Spin(rays, 14f);
        VisualElement glow = Image(SpriteFactory.Glow, cx, cy, boxSize * 1.2f, boxSize * 1.2f, back);
        glow.style.opacity = 0f;
        VisualElement box = Box(cx, cy, boxSize);

        yield return Tween(0.45f, t =>
        {
            float e = Ease.OutCubic(t);
            box.style.translate = new Translate(0f, (1f - e) * -H * 0.7f);
            box.style.rotate = new Rotate(new Angle((1f - e) * -12f, AngleUnit.Degree));
        });
        GachaSound.Play("land", 0.9f);
        Shake(14f * S, 0.3f);
        Dust(cx, cy + boxSize * 0.42f);
        yield return Squash(box, 0.18f);
        if (skip) { yield return Summary(results); yield break; }

        // 2. 箱がふくらんで光る（3 回。だんだん強く）
        Color color = RarityColor(shown);
        SetHint("");
        for (int i = 0; i < 3; i++)
        {
            if (skip) break;
            float k = (i + 1) / 3f;
            glow.style.unityBackgroundImageTintColor = color;
            if (shown >= 2) rays.style.unityBackgroundImageTintColor = Palette.WithAlpha(color, 1f);
            GachaSound.Play("charge" + i);
            Rise(cx, cy, color, 6 + i * 4);
            yield return Tween(0.42f, t =>
            {
                float pulse = Mathf.Sin(t * Mathf.PI);
                float s = 1f + (0.06f + 0.05f * i) * pulse;
                box.style.scale = new Scale(new Vector3(s, s, 1f));
                box.style.rotate = new Rotate(new Angle(Mathf.Sin(t * Mathf.PI * 4f) * (3f + 3f * i), AngleUnit.Degree));
                glow.style.opacity = (0.25f + 0.45f * k) * (0.6f + 0.4f * pulse);
                float g = boxSize * (1.1f + 0.8f * k + 0.2f * pulse);
                Resize(glow, cx, cy, g, g);
                rays.style.opacity = shown >= 2 ? (0.05f + 0.45f * k * k) * (0.7f + 0.3f * pulse) : 0f;
            });
            // 昇格演出：2 回目のあとで色が変わる
            if (upgrade && i == 1 && !skip)
            {
                yield return Wait(0.25f);
                shown = best;
                color = RarityColor(shown);
                FlashScreen(Color.white, 0.6f, 0.45f);
                GachaSound.Play("upgrade", 0.9f);
                Shake(22f * S, 0.45f);
                Pop("!?", cx + boxSize * 0.45f, cy - boxSize * 0.55f, color);
                Ring(cx, cy, color, boxSize * 0.6f, boxSize * 3f, 0.6f);
                Burst(cx, cy, color, 18, boxSize * 1.6f);
            }
        }
        if (skip) { yield return Summary(results); yield break; }

        if (rainbow && !skip)
        {
            yield return Wait(0.2f);
            FlashScreen(Color.white, 0.8f, 0.6f);
            GachaSound.Play("upgrade", 1f);
            GachaSound.Play("rare3", 0.6f);
            Shake(28f * S, 0.6f);
            Pop("!!!", cx + boxSize * 0.4f, cy - boxSize * 0.6f, Color.white);
            Loop(glow, () =>
            {
                Color hue = Color.HSVToRGB((Time.unscaledTime * 0.6f) % 1f, 0.6f, 1f);
                glow.style.unityBackgroundImageTintColor = hue;
                rays.style.unityBackgroundImageTintColor = hue;
            });
            rays.style.opacity = 0.7f;
        }

        // 押すまで待つ（箱はふるえつづける）
        SetHint(rainbow ? "押して開ける……！！" : best >= 3 ? "押して開ける……！" : "押して開ける");
        float nextBeat = Time.unscaledTime;
        var shiver = Loop(box, () =>
        {
            float t = Time.unscaledTime;
            // どくん・どくん（★3 のときは速く）
            if (t >= nextBeat)
            {
                GachaSound.Play("tap", shown >= 3 ? 0.9f : 0.6f);
                nextBeat = t + (shown >= 3 ? 0.75f : 1.1f);
            }
            box.style.rotate = new Rotate(new Angle(Mathf.Sin(t * 30f) * (shown >= 3 ? 3f : 1.5f), AngleUnit.Degree));
            glow.style.opacity = 0.65f + 0.2f * Mathf.Sin(t * 6f);
        });
        yield return WaitTap(8f);
        shiver.Pause();
        SetHint("");
        if (skip) { yield return Summary(results); yield break; }

        // 3. はじける
        GachaSound.Play("burst", 1f);
        if (best >= 3) GachaSound.Play("rare3", 0.7f);
        else if (best == 2) GachaSound.Play("rare2", 0.6f);
        FlashScreen(best >= 3 ? new Color(1f, 0.93f, 0.75f) : Color.white, 1f, best >= 3 ? 0.8f : 0.5f);
        Shake((best >= 3 ? 34f : best == 2 ? 20f : 12f) * S, best >= 3 ? 0.8f : 0.45f);
        Sprite burstArt = Art.Effect(best >= 3 ? "GachaBurstGold" : best == 2 ? "GachaBurstBlue" : "GachaBurstWhite");
        VisualElement burst = Image(burstArt != null ? burstArt : SpriteFactory.Glow, cx, cy, boxSize, boxSize, front);
        if (burstArt == null) burst.style.unityBackgroundImageTintColor = color;
        UITween.Run(burst, 0.9f, t =>
        {
            float e = Ease.OutCubic(t);
            Resize(burst, cx, cy, boxSize * (0.6f + 2.6f * e), boxSize * (0.6f + 2.6f * e));
            burst.style.opacity = 1f - t;
        }, () => burst.RemoveFromHierarchy());
        Ring(cx, cy, color, boxSize * 0.4f, W * 0.9f, 0.7f);
        Ring(cx, cy, Color.white, boxSize * 0.2f, W * 0.6f, 0.5f);
        Burst(cx, cy, color, best >= 3 ? 70 : 40, H * 0.75f);
        // 箱の絵があれば、上と下の半分に割れて飛んでいく（なければふくらんで消える）
        Sprite boxArt = Art.Gacha("Box");
        if (boxArt != null)
        {
            box.RemoveFromHierarchy();
            BoxHalf(boxArt, cx, cy, boxSize, true);
            BoxHalf(boxArt, cx, cy, boxSize, false);
        }
        else
        {
            UITween.Run(box, 0.25f, t =>
            {
                float s = 1f + 0.5f * t;
                box.style.scale = new Scale(new Vector3(s, s, 1f));
                box.style.opacity = 1f - t;
            }, () => box.RemoveFromHierarchy());
        }
        UITween.Run(glow, 0.8f, t => glow.style.opacity = (1f - t) * 0.8f);
        if (best >= 3) rays.style.opacity = 0.45f;
        else
        {
            float from = rays.resolvedStyle.opacity;
            UITween.Run(rays, 0.6f, t => rays.style.opacity = from * (1f - t));
        }
        yield return Wait(0.55f);
        if (skip) { yield return Summary(results); yield break; }

        // 4. カードが並び、1 枚ずつめくる
        LayoutCards(results);
        for (int i = 0; i < cards.Count; i++)
        {
            CardView c = cards[i];
            float fromX = cx, fromY = cy;
            UITween.Run(c.Root, 0.38f, t =>
            {
                float e = Ease.OutBack(t);
                float x = Mathf.LerpUnclamped(fromX, c.X, e), y = Mathf.LerpUnclamped(fromY, c.Y, e);
                c.Root.style.left = x - c.Width * 0.5f;
                c.Root.style.top = y - c.Height * 0.5f;
                float s = Mathf.Lerp(0.2f, 1f, Ease.OutCubic(t));
                c.Root.style.scale = new Scale(new Vector3(s, s, 1f));
                c.Root.style.opacity = Mathf.Clamp01(t * 3f);
            });
            GachaSound.Play("flip", 0.4f);
            yield return Wait(cards.Count > 1 ? 0.05f : 0f);
        }
        yield return Wait(0.45f);
        SetHint(cards.Count > 1 ? "押すと早くめくる" : "");

        bool fast = false;
        advance = false;
        foreach (CardView c in cards)
        {
            if (skip) break;
            if (advance) { fast = true; advance = false; }
            int rarity = c.Result.Equipment.rarity;
            if (rarity >= 3)
            {
                // めくる前に、ふるえて光が強くなる
                GachaSound.Play("charge2", 0.8f);
                Vector2 basePos = new Vector2(c.X, c.Y);
                yield return Tween(0.55f, t =>
                {
                    float a = 6f * S * t;
                    c.Root.style.translate = new Translate(Random.Range(-a, a), Random.Range(-a, a));
                    if (c.Aura != null)
                    {
                        float g = c.Width * (1.6f + 0.8f * t);
                        Resize(c.Aura, c.Width * 0.5f, c.Height * 0.5f, g, g);
                        c.Aura.style.opacity = 0.6f + 0.4f * t;
                    }
                });
                c.Root.style.translate = new Translate(0f, 0f);
            }
            yield return Flip(c, fast ? 0.12f : 0.22f);
            if (rarity >= 3)
            {
                Ring(c.X, c.Y, RarityColor(3), c.Width * 0.3f, c.Width * 3f, 0.6f);
                Burst(c.X, c.Y, RarityColor(3), 30, c.Width * 2.2f);
                Shake(16f * S, 0.35f);
                GachaSound.Play("rare3", 0.8f);
                if (!skip) yield return Spotlight(c.Result);
            }
            else if (rarity == 2)
            {
                Ring(c.X, c.Y, RarityColor(2), c.Width * 0.3f, c.Width * 1.8f, 0.45f);
                Burst(c.X, c.Y, RarityColor(2), 12, c.Width * 1.2f);
                GachaSound.Play("rare2", 0.7f);
            }
            else
            {
                GachaSound.Play("flip", 0.6f);
            }
            if (c.Result.IsNew) Stamp(c);
            yield return Wait(fast ? 0.03f : rarity >= 2 ? 0.28f : 0.14f);
        }

        yield return Summary(results);
    }

    /// <summary>★3：画面を暗くして、光線の前に大きなカードを出す</summary>
    private IEnumerator Spotlight(GachaResult r)
    {
        EquipmentData e = r.Equipment;
        Color gold = RarityColor(3);
        float cx = W * 0.5f, cy = H * 0.42f;

        var shade = UIFactory.Box("full", "gacha-spot-dim");
        shade.style.opacity = 0f;
        spot.Add(shade);
        UITween.Run(shade, 0.25f, t => shade.style.opacity = t);

        VisualElement rays = Image(RaysTexture(), cx, cy, H * 1.5f, H * 1.5f, spot);
        rays.style.unityBackgroundImageTintColor = gold;
        Spin(rays, 22f);
        Sprite raysArt = Art.Effect("GachaRays");
        if (raysArt != null)
        {
            VisualElement r2 = Image(raysArt, cx, cy, H * 1.2f, H * 1.2f, spot);
            Spin(r2, -10f);
        }
        VisualElement halo = Image(SpriteFactory.Glow, cx, cy, H * 0.9f, H * 0.9f, spot);
        halo.style.unityBackgroundImageTintColor = Palette.WithAlpha(gold, 0.8f);

        float cw = Mathf.Min(340f * S, H * 0.5f / 1.4f), ch = cw * 1.4f;
        VisualElement card = CardFace(r, cw, ch, true);
        card.style.position = Position.Absolute;
        card.style.left = cx - cw * 0.5f;
        card.style.top = cy - ch * 0.5f;
        spot.Add(card);

        // 光の粒がずっと降る
        var sparkle = Loop(spot, () =>
        {
            if (Random.value < 0.5f) Spark(cx + Random.Range(-W * 0.4f, W * 0.4f), cy + Random.Range(-H * 0.4f, H * 0.4f), gold, spot);
        });

        GachaSound.Play("burst", 0.8f);
        yield return Tween(0.45f, t =>
        {
            float s = Mathf.LerpUnclamped(2.2f, 1f, Ease.OutBack(t));
            card.style.scale = new Scale(new Vector3(s, s, 1f));
            card.style.opacity = Mathf.Clamp01(t * 2f);
            rays.style.opacity = t;
        });
        Shake(18f * S, 0.4f);
        Burst(cx, cy, gold, 40, H * 0.7f);

        // 「★3」と名前の帯
        var banner = UIFactory.Box("gacha-spot-banner");
        var stars = UIFactory.Text("", UIFont.Display, "gacha-spot-stars");
        stars.style.fontSize = 50f * S;
        stars.style.height = 58f * S;
        banner.Add(stars);
        var spotName = UIFactory.Text(e.name, UIFont.Title, "gacha-spot-name");
        spotName.style.fontSize = 48f * S;
        banner.Add(spotName);
        var spotEffect = UIFactory.Text(Growth.Describe(e, r.Stacks), UIFont.Body, "gacha-spot-effect");
        spotEffect.style.fontSize = 26f * S;
        banner.Add(spotEffect);
        banner.style.top = cy + ch * 0.5f + 10f * S;
        spot.Add(banner);
        for (int i = 0; i < 3; i++)
        {
            yield return Wait(0.16f);
            stars.text = new string('★', i + 1);
            GachaSound.Play("star", 0.7f);
        }
        if (r.IsNew)
        {
            var stamp = UIFactory.Text("NEW!", UIFont.Display, "gacha-spot-new");
            stamp.style.fontSize = 64f * S;
            stamp.style.left = cx + cw * 0.25f;
            stamp.style.top = cy - ch * 0.62f;
            spot.Add(stamp);
            UITween.Run(stamp, 0.3f, t =>
            {
                float s = Mathf.Lerp(3f, 1f, Ease.OutCubic(t));
                stamp.style.scale = new Scale(new Vector3(s, s, 1f));
                stamp.style.opacity = t;
            }, () => Shake(10f * S, 0.2f));
            GachaSound.Play("stamp", 0.9f);
        }
        hint.AddToClassList("gacha-hint--top");
        SetHint("押して続ける");
        yield return WaitTap(10f);
        SetHint("");
        hint.RemoveFromClassList("gacha-hint--top");
        sparkle.Pause();
        yield return Tween(0.25f, t => spot.style.opacity = 1f - t);
        spot.Clear();
        spot.style.opacity = 1f;
    }

    private IEnumerator Summary(List<GachaResult> results)
    {
        // 飛ばしたときは、箱や光を片付け、まだ並んでいないカードを並べて全部表にする
        back.Clear();
        front.Clear();
        for (int i = stage.childCount - 1; i >= 0; i--)
        {
            VisualElement child = stage[i];
            if (!cards.Exists(c => c.Root == child)) child.RemoveFromHierarchy();
        }
        if (cards.Count == 0) LayoutCards(results);
        foreach (CardView c in cards)
        {
            c.Root.style.left = c.X - c.Width * 0.5f;
            c.Root.style.top = c.Y - c.Height * 0.5f;
            c.Root.style.scale = new Scale(Vector3.one);
            c.Root.style.opacity = 1f;
            c.Root.style.translate = new Translate(0f, 0f);
            if (!c.Open) ShowFace(c);
        }
        spot.Clear();
        spot.style.opacity = 1f;
        SetHint("");
        UIFactory.SetVisible(skipButton, false);

        int r3 = 0, r2 = 0, fresh = 0, coins = 0;
        foreach (GachaResult r in results)
        {
            if (r.Equipment.rarity >= 3) r3++;
            else if (r.Equipment.rarity == 2) r2++;
            if (r.IsNew) fresh++;
            coins += r.Coins;
        }
        var parts = new List<string>();
        if (r3 > 0) parts.Add("<color=#FFD27A>★3 ×" + r3 + "</color>");
        if (r2 > 0) parts.Add("<color=#7FD8F0>★2 ×" + r2 + "</color>");
        if (fresh > 0) parts.Add("<color=#FFD27A>NEW ×" + fresh + "</color>");
        if (results.Count == 1 && fresh == 0 && coins == 0) parts.Add("重ねて強くなった");
        if (coins > 0) parts.Add("部費に換わった " + coins);
        summaryText.text = string.Join("　", parts);
        UIFactory.SetVisible(summary, true);
        GachaSound.Play(r3 > 0 ? "rare3" : "rare2", 0.5f);
        yield break;
    }

    // ================= カード =================

    private void LayoutCards(List<GachaResult> results)
    {
        int n = results.Count;
        int cols = n >= 10 ? 5 : n;
        int rows = Mathf.CeilToInt(n / (float)cols);
        float gap = 18f * S;
        float cw = n == 1 ? 330f * S : Mathf.Min(260f * S, W * 0.86f / cols - gap);
        cw = Mathf.Min(cw, (H * 0.6f - gap * (rows - 1)) / rows / 1.4f);
        float chh = cw * 1.4f;
        float totalW = cols * cw + (cols - 1) * gap;
        float totalH = rows * chh + (rows - 1) * gap;
        float x0 = W * 0.5f - totalW * 0.5f + cw * 0.5f;
        float y0 = H * 0.44f - totalH * 0.5f + chh * 0.5f;
        for (int i = 0; i < n; i++)
        {
            var c = new CardView { Result = results[i], Width = cw, Height = chh };
            c.X = x0 + (i % cols) * (cw + gap);
            c.Y = y0 + (i / cols) * (chh + gap);
            c.Root = UIFactory.Box("gacha-card-slot");
            c.Root.style.position = Position.Absolute;
            c.Root.style.width = cw;
            c.Root.style.height = chh;
            c.Root.style.left = W * 0.5f - cw * 0.5f;
            c.Root.style.top = H * 0.5f - chh * 0.5f;
            c.Root.style.opacity = 0f;

            int rarity = results[i].Equipment.rarity;
            // めくる前から、★2 は青く、★3 は金色に光る
            if (rarity >= 2)
            {
                c.Aura = Image(SpriteFactory.Glow, cw * 0.5f, chh * 0.5f, cw * 1.6f, cw * 1.6f, c.Root);
                c.Aura.style.unityBackgroundImageTintColor = RarityColor(rarity);
                VisualElement aura = c.Aura;
                float phase = Random.value * 6f;
                bool gold = rarity >= 3;
                Loop(c.Root, () =>
                {
                    if (!c.Open) aura.style.opacity = (gold ? 0.75f : 0.5f) + 0.25f * Mathf.Sin(Time.unscaledTime * (gold ? 7f : 4f) + phase);
                });
            }
            c.Back = CardBack(cw, chh);
            c.Root.Add(c.Back);
            stage.Add(c.Root);
            cards.Add(c);
        }
    }

    private IEnumerator Flip(CardView c, float duration)
    {
        yield return Tween(duration * 0.5f, t => c.Root.style.scale = new Scale(new Vector3(1f - t, 1f + 0.06f * t, 1f)));
        ShowFace(c);
        yield return Tween(duration * 0.5f, t => c.Root.style.scale = new Scale(new Vector3(t, 1.06f - 0.06f * t, 1f)));
        c.Root.style.scale = new Scale(Vector3.one);
    }

    private void ShowFace(CardView c)
    {
        c.Open = true;
        if (c.Back != null) c.Back.RemoveFromHierarchy();
        if (c.Aura != null) c.Aura.style.opacity = c.Result.Equipment.rarity >= 3 ? 0.55f : 0.3f;
        c.Face = CardFace(c.Result, c.Width, c.Height, false);
        c.Root.Add(c.Face);
        // 重ね・部費のしるし（NEW は Stamp で押す）
        if (!c.Result.IsNew)
        {
            string text = c.Result.Coins > 0 ? "部費 +" + c.Result.Coins : "重ね +" + c.Result.Stacks;
            var tag = UIFactory.Text(text, UIFont.Display, "gacha-card-badge");
            tag.style.fontSize = Mathf.Max(13f, c.Width * 0.065f);
            c.Face.Add(tag);
        }
    }

    private void Stamp(CardView c)
    {
        if (c.Face == null) return;
        GachaSound.Play("stamp", 0.5f);
        var tag = UIFactory.Text("NEW", UIFont.Display, "gacha-card-badge", "gacha-card-badge--new");
        tag.style.fontSize = Mathf.Max(14f, c.Width * 0.075f);
        c.Face.Add(tag);
        UITween.Run(tag, 0.25f, t =>
        {
            float s = Mathf.Lerp(2.4f, 1f, Ease.OutCubic(t));
            tag.style.scale = new Scale(new Vector3(s, s, 1f));
            tag.style.opacity = t;
        });
    }

    /// <summary>カードの裏（Art/Gacha/CardBack。なければ紺地に琥珀色の模様と「C3」）</summary>
    private static VisualElement CardBack(float w, float h)
    {
        var card = new ChamferBox();
        card.AddToClassList("gacha-card-back");
        card.style.position = Position.Absolute;
        card.style.left = 0f;
        card.style.top = 0f;
        card.style.width = w;
        card.style.height = h;
        card.SetFill(new Color(0.07f, 0.09f, 0.16f, 1f)).SetStroke(Palette.WithAlpha(Palette.Amber, 0.8f), 2f)
            .SetCut(w * 0.08f, ChamferBox.Corners.TopRight | ChamferBox.Corners.BottomLeft).SetStripes(new Color(1f, 0.82f, 0.45f, 0.06f), 14f);
        Sprite art = Art.Gacha("CardBack");
        if (art != null)
        {
            var img = UIFactory.Box("gacha-img", "full");
            img.style.backgroundImage = new StyleBackground(art);
            card.Add(img);
        }
        else
        {
            var mark = UIFactory.Text("C3", UIFont.Display, "gacha-card-back-mark");
            mark.style.fontSize = w * 0.3f;
            card.Add(mark);
        }
        return card;
    }

    /// <summary>カードの表：★・備品の絵・名前・効き目（★3 は光の筋が走る）</summary>
    private static VisualElement CardFace(GachaResult r, float w, float h, bool big)
    {
        EquipmentData e = r.Equipment;
        Color color = RarityColor(e.rarity);
        var card = new ChamferBox();
        card.AddToClassList("gacha-card-face");
        card.style.position = Position.Absolute;
        card.style.left = 0f;
        card.style.top = 0f;
        card.style.width = w;
        card.style.height = h;
        card.style.overflow = Overflow.Hidden;
        card.SetFill(new Color(0.05f, 0.07f, 0.12f, 1f)).SetStroke(color, e.rarity >= 3 ? 3f : 2f)
            .SetCut(w * 0.08f, ChamferBox.Corners.TopRight | ChamferBox.Corners.BottomLeft);
        if (e.rarity >= 2) card.SetAccent(color, Mathf.Max(4f, w * 0.025f));

        // 下地の光
        var glow = UIFactory.Box("gacha-img");
        glow.style.position = Position.Absolute;
        glow.style.left = w * 0.05f;
        glow.style.top = h * 0.08f;
        glow.style.width = w * 0.9f;
        glow.style.height = w * 0.9f;
        glow.style.backgroundImage = new StyleBackground(SpriteFactory.Glow);
        glow.style.unityBackgroundImageTintColor = Palette.WithAlpha(color, e.rarity >= 3 ? 0.55f : 0.3f);
        card.Add(glow);

        var stars = UIFactory.Text(GrowthScreen.Stars(e.rarity), UIFont.Body, "gacha-face-stars");
        stars.style.color = color;
        stars.style.fontSize = Mathf.Max(14f, w * 0.085f);
        card.Add(stars);

        var icon = UIFactory.Box("gacha-img");
        icon.style.position = Position.Absolute;
        float iconSize = w * 0.66f;
        icon.style.left = (w - iconSize) * 0.5f;
        icon.style.top = h * 0.15f;
        icon.style.width = iconSize;
        icon.style.height = iconSize;
        Sprite art = Art.Item(e.id);
        if (art != null) icon.style.backgroundImage = new StyleBackground(art);
        else
        {
            var letter = UIFactory.Text(e.name.Substring(0, 1), UIFont.Title, "gacha-face-letter");
            letter.style.fontSize = iconSize * 0.5f;
            letter.style.color = color;
            icon.Add(letter);
        }
        card.Add(icon);

        var name = UIFactory.Text(e.name, UIFont.Title, "gacha-face-name");
        name.style.fontSize = Mathf.Max(14f, w * (big ? 0.085f : 0.08f));
        name.style.top = h * 0.68f;
        card.Add(name);
        var effect = UIFactory.Text(Growth.Describe(e, r.Stacks), UIFont.Body, "gacha-face-effect");
        effect.style.fontSize = Mathf.Max(12f, w * 0.06f);
        effect.style.top = h * 0.84f;
        card.Add(effect);

        // ★3 は光の筋がくり返し走る
        if (e.rarity >= 3)
        {
            var shine = UIFactory.Box("gacha-shine");
            shine.style.width = w * 0.35f;
            shine.style.height = h * 1.6f;
            shine.style.top = -h * 0.3f;
            card.Add(shine);
            float start = Random.value * 2f;
            card.schedule.Execute(() =>
            {
                float t = ((Time.unscaledTime + start) % 2.2f) / 2.2f;
                shine.style.left = Mathf.Lerp(-w * 0.6f, w * 1.3f, Mathf.Clamp01(t * 1.6f));
            }).Every(0);
        }
        return card;
    }

    // ================= 部品 =================

    private VisualElement Box(float cx, float cy, float size)
    {
        Sprite art = Art.Gacha("Box");
        VisualElement box;
        if (art != null)
        {
            box = UIFactory.Box("gacha-img");
            box.style.backgroundImage = new StyleBackground(art);
        }
        else
        {
            var fallback = new ChamferBox();
            fallback.SetFill(new Color(0.62f, 0.45f, 0.28f, 1f)).SetStroke(new Color(0.25f, 0.16f, 0.08f), 4f).SetCut(size * 0.06f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight);
            var tape = UIFactory.Box("gacha-box-tape");
            fallback.Add(tape);
            box = fallback;
        }
        box.style.position = Position.Absolute;
        float h = art != null ? size : size * 0.75f;
        box.style.left = cx - size * 0.5f;
        box.style.top = cy - h * 0.5f;
        box.style.width = size;
        box.style.height = h;
        stage.Add(box);
        return box;
    }

/// <summary>箱の上か下の半分（はじけたときに飛んでいく）</summary>
    private void BoxHalf(Sprite art, float cx, float cy, float size, bool top)
    {
        var clip = UIFactory.Box();
        clip.style.position = Position.Absolute;
        clip.style.left = cx - size * 0.5f;
        clip.style.top = cy - size * 0.5f + (top ? 0f : size * 0.5f);
        clip.style.width = size;
        clip.style.height = size * 0.5f;
        clip.style.overflow = Overflow.Hidden;
        var img = UIFactory.Box("gacha-img");
        img.style.position = Position.Absolute;
        img.style.left = 0f;
        img.style.top = top ? 0f : -size * 0.5f;
        img.style.width = size;
        img.style.height = size;
        img.style.backgroundImage = new StyleBackground(art);
        clip.Add(img);
        stage.Add(clip);
        float dx = Random.Range(-90f, 90f) * S;
        float dy = (top ? -H * 0.55f : H * 0.35f);
        float spin = top ? Random.Range(-40f, -15f) : Random.Range(10f, 30f);
        UITween.Run(clip, 0.7f, t =>
        {
            float e = Ease.OutCubic(t);
            clip.style.translate = new Translate(dx * e, dy * e + (top ? 0f : 200f * S * t * t));
            clip.style.rotate = new Rotate(new Angle(spin * e, AngleUnit.Degree));
            clip.style.opacity = 1f - t * t;
        }, () => clip.RemoveFromHierarchy());
    }

        private VisualElement Image(Sprite sprite, float cx, float cy, float w, float h, VisualElement parent)
    {
        var e = UIFactory.Box("gacha-img");
        e.style.position = Position.Absolute;
        if (sprite != null) e.style.backgroundImage = new StyleBackground(sprite);
        Resize(e, cx, cy, w, h);
        parent.Add(e);
        return e;
    }

    private VisualElement Image(Texture2D texture, float cx, float cy, float w, float h, VisualElement parent)
    {
        var e = UIFactory.Box("gacha-img");
        e.style.position = Position.Absolute;
        if (texture != null) e.style.backgroundImage = new StyleBackground(texture);
        Resize(e, cx, cy, w, h);
        parent.Add(e);
        return e;
    }

    private static void Resize(VisualElement e, float cx, float cy, float w, float h)
    {
        e.style.left = cx - w * 0.5f;
        e.style.top = cy - h * 0.5f;
        e.style.width = w;
        e.style.height = h;
    }

    private void Spin(VisualElement e, float degreesPerSecond)
    {
        float start = Time.unscaledTime;
        Loop(e, () => e.style.rotate = new Rotate(new Angle((Time.unscaledTime - start) * degreesPerSecond, AngleUnit.Degree)));
    }

    private IVisualElementScheduledItem Loop(VisualElement owner, System.Action tick)
    {
        IVisualElementScheduledItem item = owner.schedule.Execute(tick).Every(0);
        loops.Add(item);
        return item;
    }

    private void Shake(float amount, float duration)
    {
        UITween.Stop(ref shakeItem);
        shakeItem = UITween.Run(world, duration, t =>
        {
            float a = amount * (1f - t) * (1f - t);
            world.style.translate = new Translate(Random.Range(-a, a), Random.Range(-a, a));
        }, () => world.style.translate = new Translate(0f, 0f));
    }

    private void FlashScreen(Color color, float strength, float duration)
    {
        flash.style.backgroundColor = color;
        UITween.Run(flash, duration, t => flash.style.opacity = strength * (1f - Ease.OutCubic(t)));
    }

    /// <summary>光の粒がまわりへ飛び散る</summary>
    private void Burst(float cx, float cy, Color color, int count, float distance)
    {
        Sprite star = Art.Effect("GachaSparkle");
        for (int i = 0; i < count; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float dist = distance * Random.Range(0.35f, 1f);
            float size = Random.Range(10f, i % 4 == 0 ? 46f : 26f) * S;
            bool useStar = star != null && i % 3 == 0;
            var p = Image(useStar ? star : SpriteFactory.Glow, cx, cy, size, size, front);
            Color c = i % 5 == 0 ? Color.white : color;
            if (!useStar) p.style.unityBackgroundImageTintColor = c;
            float life = Random.Range(0.6f, 1.2f);
            float dx = Mathf.Cos(angle) * dist, dy = Mathf.Sin(angle) * dist;
            float spin = Random.Range(-360f, 360f);
            UITween.Run(p, life, t =>
            {
                float e = Ease.OutCubic(t);
                p.style.left = cx + dx * e - size * 0.5f;
                p.style.top = cy + dy * e + 120f * S * t * t - size * 0.5f;
                p.style.opacity = 1f - t * t;
                p.style.rotate = new Rotate(new Angle(spin * t, AngleUnit.Degree));
            }, () => p.RemoveFromHierarchy());
        }
    }

    /// <summary>背景をゆっくり舞う光の粒</summary>
    private void Drift()
    {
        float x = Random.Range(0f, W), y = H + 20f;
        float size = Random.Range(6f, 16f) * S;
        var p = Image(SpriteFactory.Glow, x, y, size, size, back);
        p.style.unityBackgroundImageTintColor = new Color(0.6f, 0.7f, 1f, 0.6f);
        float rise = Random.Range(H * 0.5f, H * 1.1f), sway = Random.Range(-60f, 60f) * S;
        float life = Random.Range(3f, 6f);
        UITween.Run(p, life, t =>
        {
            p.style.top = y - rise * t - size * 0.5f;
            p.style.left = x + sway * Mathf.Sin(t * 6f) - size * 0.5f;
            p.style.opacity = Mathf.Sin(t * Mathf.PI) * 0.8f;
        }, () => p.RemoveFromHierarchy());
    }

    /// <summary>箱から光の粒が立ちのぼる</summary>
    private void Rise(float cx, float cy, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float x = cx + Random.Range(-150f, 150f) * S;
            float size = Random.Range(8f, 22f) * S;
            var p = Image(SpriteFactory.Glow, x, cy, size, size, front);
            p.style.unityBackgroundImageTintColor = color;
            float rise = Random.Range(180f, 420f) * S;
            float life = Random.Range(0.6f, 1.1f);
            UITween.Run(p, life, t =>
            {
                p.style.top = cy - rise * Ease.OutCubic(t) - size * 0.5f;
                p.style.opacity = Mathf.Sin(t * Mathf.PI);
            }, () => p.RemoveFromHierarchy());
        }
    }

    private void Spark(float x, float y, Color color, VisualElement parent)
    {
        Sprite star = Art.Effect("GachaSparkle");
        float size = Random.Range(14f, 40f) * S;
        var p = Image(star != null ? star : SpriteFactory.Glow, x, y, size, size, parent);
        if (star == null) p.style.unityBackgroundImageTintColor = color;
        UITween.Run(p, 0.9f, t =>
        {
            float s = Mathf.Sin(t * Mathf.PI);
            p.style.scale = new Scale(new Vector3(s, s, 1f));
            p.style.opacity = s;
            p.style.rotate = new Rotate(new Angle(t * 90f, AngleUnit.Degree));
        }, () => p.RemoveFromHierarchy());
    }

    private void Ring(float cx, float cy, Color color, float from, float to, float duration)
    {
        var ring = Image(SpriteFactory.Ring, cx, cy, from, from, front);
        ring.style.unityBackgroundImageTintColor = color;
        UITween.Run(ring, duration, t =>
        {
            float s = Mathf.Lerp(from, to, Ease.OutCubic(t));
            Resize(ring, cx, cy, s, s);
            ring.style.opacity = 1f - t;
        }, () => ring.RemoveFromHierarchy());
    }

    /// <summary>着地の土ぼこり</summary>
    private void Dust(float cx, float cy)
    {
        for (int i = 0; i < 12; i++)
        {
            float dir = i < 6 ? -1f : 1f;
            float size = Random.Range(30f, 70f) * S;
            var p = Image(SpriteFactory.SoftCircle, cx + dir * 60f * S, cy, size, size, front);
            p.style.unityBackgroundImageTintColor = new Color(0.8f, 0.75f, 0.68f, 0.5f);
            float dx = dir * Random.Range(120f, 260f) * S, dy = -Random.Range(10f, 60f) * S;
            UITween.Run(p, 0.6f, t =>
            {
                float e = Ease.OutCubic(t);
                p.style.left = cx + dir * 60f * S + dx * e - size * 0.5f;
                p.style.top = cy + dy * e - size * 0.5f;
                p.style.opacity = 0.7f * (1f - t);
            }, () => p.RemoveFromHierarchy());
        }
    }

    private void Pop(string text, float x, float y, Color color)
    {
        var label = UIFactory.Text(text, UIFont.Display, "gacha-pop");
        label.style.color = color;
        label.style.left = x;
        label.style.top = y;
        front.Add(label);
        UITween.Run(label, 1.1f, t =>
        {
            float s = t < 0.2f ? Mathf.Lerp(2.5f, 1f, t / 0.2f) : 1f;
            label.style.scale = new Scale(new Vector3(s, s, 1f));
            label.style.opacity = t < 0.75f ? 1f : (1f - t) / 0.25f;
        }, () => label.RemoveFromHierarchy());
    }

    private IEnumerator Squash(VisualElement e, float duration)
    {
        yield return Tween(duration, t =>
        {
            float k = Mathf.Sin(t * Mathf.PI);
            e.style.scale = new Scale(new Vector3(1f + 0.12f * k, 1f - 0.12f * k, 1f));
        });
        e.style.scale = new Scale(Vector3.one);
    }

    private void SetHint(string text)
    {
        hint.text = text;
        UIFactory.SetVisible(hint, !string.IsNullOrEmpty(text));
    }

    // ================= 待つ =================

    private IEnumerator Tween(float duration, System.Action<float> apply)
    {
        float start = Time.unscaledTime;
        while (true)
        {
            float t = duration > 0f ? Mathf.Clamp01((Time.unscaledTime - start) / duration) : 1f;
            apply(t);
            if (t >= 1f) yield break;
            yield return null;
        }
    }

    private IEnumerator Wait(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end && !skip) yield return null;
    }

    /// <summary>押す（Space・Enter）まで待つ。max 秒たったら進む</summary>
    private IEnumerator WaitTap(float max)
    {
        advance = false;
        float end = Time.unscaledTime + max;
        while (!advance && !skip && Time.unscaledTime < end)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) advance = true;
            yield return null;
        }
        advance = false;
    }

    // ================= 色・光線 =================

    public static Color RarityColor(int rarity)
    {
        switch (rarity)
        {
            case 3: return new Color(1f, 0.8f, 0.32f);
            case 2: return new Color(0.4f, 0.82f, 1f);
            default: return new Color(0.88f, 0.9f, 0.96f);
        }
    }

    /// <summary>放射状の光線（絵がなくても出せるよう、コードで作る）</summary>
    public static Texture2D RaysTexture()
    {
        if (raysTexture != null) return raysTexture;
        const int n = 512;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float angle = Mathf.Atan2(dy, dx);
                float wide = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(angle * 12f), 14f);
                float thin = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(angle * 12f + Mathf.PI), 40f) * 0.6f;
                float fall = Mathf.Clamp01(1f - r);
                float core = Mathf.Clamp01(1f - r * 3f);
                float a = Mathf.Clamp01((wide + thin) * fall * (0.4f + 0.6f * fall) + core * core * 0.6f);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels32(px);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.DontSave;
        tex.Apply(false, true);
        raysTexture = tex;
        return tex;
    }
}
