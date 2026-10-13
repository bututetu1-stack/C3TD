using UnityEngine;

/// <summary>
/// 一度だけ再生して消えるスプライトの演出。大きさが start → end に変わりながら薄れていく。
/// 動き（速度と減速）・回転・遅れて始まる、を足せる。一時停止中は止まる。
/// ただし一時停止中に作ったもの（止めたまま配置・撤退・必殺技を使ったとき）は、止まったまま残らないよう実時間で動く。
/// </summary>
public class FxSprite : MonoBehaviour
{
    private SpriteRenderer sr;
    private Color color;
    private float life;
    private float age;
    private float delay;
    private float startScale;
    private float endScale;
    private float aspect = 1f;
    private Vector3 velocity;
    private float drag;
    private float spin;
    private bool realtime;

    public static FxSprite Create(Sprite sprite, Vector3 position, Color color, float life, float startScale, float endScale, int order)
    {
        var obj = new GameObject("Fx");
        Battle battle = Battle.Current;
        if (battle != null) obj.transform.SetParent(battle.transform, false);
        obj.transform.position = position;
        var fx = obj.AddComponent<FxSprite>();
        fx.sr = obj.AddComponent<SpriteRenderer>();
        fx.sr.sprite = sprite;
        fx.sr.sortingOrder = order;
        fx.color = color;
        fx.life = Mathf.Max(0.01f, life);
        fx.startScale = startScale;
        fx.endScale = endScale;
        fx.realtime = TimeController.Paused;
        fx.Apply(0f);
        return fx;
    }

    public FxSprite Move(Vector3 velocity, float drag)
    {
        this.velocity = velocity;
        this.drag = drag;
        return this;
    }

    public FxSprite Spin(float degreesPerSecond)
    {
        spin = degreesPerSecond;
        return this;
    }

    public FxSprite Rotate(float degrees)
    {
        transform.rotation = Quaternion.Euler(0f, 0f, degrees);
        return this;
    }

    /// <summary>横に引き伸ばす（1 で正方形）</summary>
    public FxSprite Stretch(float widthPerHeight)
    {
        aspect = widthPerHeight;
        Apply(0f);
        return this;
    }

    public FxSprite Delay(float seconds)
    {
        delay = seconds;
        sr.enabled = false;
        return this;
    }

    void Update()
    {
        float dt = realtime ? Time.unscaledDeltaTime : Time.deltaTime;
        age += dt;
        if (age < delay) return;
        sr.enabled = true;

        float t = Mathf.Clamp01((age - delay) / life);
        Apply(t);
        if (velocity != Vector3.zero)
        {
            transform.position += velocity * dt;
            velocity *= Mathf.Exp(-drag * dt);
        }
        if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);
        if (t >= 1f) Destroy(gameObject);
    }

    private void Apply(float t)
    {
        float s = Mathf.LerpUnclamped(startScale, endScale, Ease.OutCubic(t));
        transform.localScale = new Vector3(s * aspect, s, 1f);
        Color c = color;
        c.a *= 1f - Ease.InQuad(t);
        sr.color = c;
    }
}
