using TMPro;
using UnityEngine;

/// <summary>ダメージや回復の数字。少し跳ねて浮かび上がり、消える</summary>
public class FloatingText : MonoBehaviour
{
    private const int Order = 90;
    private const float Life = 0.8f;

    private TextMeshPro text;
    private Color color;
    private Vector3 start;
    private float age;

    public static void Damage(Vector3 position, float amount, Color color)
    {
        Spawn(position, Mathf.RoundToInt(amount).ToString(), color, 2.4f);
    }

    public static void Heal(Vector3 position, float amount)
    {
        Spawn(position, "+" + Mathf.RoundToInt(amount), Palette.HealText, 2.4f);
    }

    private static void Spawn(Vector3 position, string value, Color color, float size)
    {
        Battle battle = Battle.Current;
        if (battle == null) return;
        var tmp = WorldText.Create(battle.transform, value, size, color, Order, true, true);
        tmp.transform.position = position + new Vector3(Random.Range(-0.12f, 0.12f), 0.3f, 0f);
        var ft = tmp.gameObject.AddComponent<FloatingText>();
        ft.text = tmp;
        ft.color = color;
        ft.start = tmp.transform.position;
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Life);
        transform.position = start + new Vector3(0f, 0.38f * Ease.OutCubic(t), 0f);
        float pop = t < 0.15f ? Mathf.Lerp(1.5f, 1f, t / 0.15f) : 1f;
        transform.localScale = Vector3.one * pop;
        Color c = color;
        c.a *= 1f - Ease.InQuad(Mathf.Clamp01((t - 0.4f) / 0.6f));
        text.color = c;
        if (t >= 1f) Destroy(gameObject);
    }
}
