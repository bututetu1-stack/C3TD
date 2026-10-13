using UnityEngine;

/// <summary>
/// マップ上の体力バー。減った分は白い帯が少し遅れて追いかける。
/// hideWhenFull なら満タンのあいだは隠す（敵）。
/// </summary>
public class HealthBar : MonoBehaviour
{
    private const float LagSpeed = 0.9f; // 1秒あたりに追いつく割合

    private SpriteRenderer back;
    private SpriteRenderer lag;
    private SpriteRenderer fill;
    private float width;
    private float height;
    private float value = 1f;
    private float lagValue = 1f;
    private bool hideWhenFull;

    public static HealthBar Create(Transform parent, Vector3 localPos, float width, float height, Color color, bool hideWhenFull, int order)
    {
        var obj = new GameObject("HealthBar");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        var bar = obj.AddComponent<HealthBar>();
        bar.width = width;
        bar.height = height;
        bar.hideWhenFull = hideWhenFull;
        bar.back = bar.Add("Back", new Color(0.02f, 0.03f, 0.06f, 0.85f), order);
        bar.back.transform.localScale = new Vector3(width + 0.04f, height + 0.04f, 1f);
        bar.lag = bar.Add("Lag", new Color(1f, 1f, 1f, 0.85f), order + 1);
        bar.fill = bar.Add("Fill", color, order + 2);
        bar.Apply();
        return bar;
    }

    public void SetColor(Color color)
    {
        fill.color = color;
    }

    public void Set(float ratio)
    {
        value = Mathf.Clamp01(ratio);
        if (lagValue < value) lagValue = value;
        Apply();
    }

    void Update()
    {
        if (lagValue > value)
        {
            lagValue = Mathf.Max(value, lagValue - LagSpeed * Time.deltaTime);
            Apply();
        }
    }

    private void Apply()
    {
        Place(fill, value);
        Place(lag, lagValue);
        bool visible = !hideWhenFull || value < 0.999f;
        back.enabled = visible;
        fill.enabled = visible && value > 0f;
        lag.enabled = visible && lagValue > value;
    }

    private void Place(SpriteRenderer sr, float ratio)
    {
        float w = width * ratio;
        sr.transform.localScale = new Vector3(w, height, 1f);
        sr.transform.localPosition = new Vector3(-width * 0.5f + w * 0.5f, 0f, 0f);
    }

    private SpriteRenderer Add(string name, Color color, int order)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }
}
