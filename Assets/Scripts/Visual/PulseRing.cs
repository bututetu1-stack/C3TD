using UnityEngine;

/// <summary>広がりながら薄れるのをくり返す輪（出撃口・拠点・ボスの気配）</summary>
public class PulseRing : MonoBehaviour
{
    private SpriteRenderer sr;
    private Color color;
    private float period;
    private float from;
    private float to;
    private float phase;

    public static PulseRing Create(Transform parent, Sprite sprite, Color color, float period, float from, float to, float phase, int order)
    {
        var obj = new GameObject("Pulse");
        obj.transform.SetParent(parent, false);
        var ring = obj.AddComponent<PulseRing>();
        ring.sr = obj.AddComponent<SpriteRenderer>();
        ring.sr.sprite = sprite;
        ring.sr.sortingOrder = order;
        ring.color = color;
        ring.period = period;
        ring.from = from;
        ring.to = to;
        ring.phase = phase;
        ring.Update();
        return ring;
    }

    void Update()
    {
        float t = Mathf.Repeat(Time.time / period + phase, 1f);
        float s = Mathf.Lerp(from, to, Ease.OutCubic(t));
        transform.localScale = new Vector3(s, s, 1f);
        Color c = color;
        c.a *= (1f - t) * Mathf.Clamp01(t * 6f);
        sr.color = c;
    }
}
