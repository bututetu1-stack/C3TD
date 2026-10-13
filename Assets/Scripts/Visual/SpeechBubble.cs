using TMPro;
using UnityEngine;

/// <summary>駒の上に出るセリフの吹き出し。少し浮かんでから消える</summary>
public class SpeechBubble : MonoBehaviour
{
    private const int Order = 92;
    private const float Life = 2.6f;

    private SpriteRenderer back;
    private TextMeshPro text;
    private Transform follow;
    private float age;

    public static void Show(Transform target, string line)
    {
        Battle battle = Battle.Current;
        if (battle == null || target == null || string.IsNullOrEmpty(line)) return;
        var obj = new GameObject("Speech");
        obj.transform.SetParent(battle.transform, false);
        var bubble = obj.AddComponent<SpeechBubble>();
        bubble.follow = target;

        bubble.text = WorldText.Create(obj.transform, line, 2.4f, Palette.Ink, Order + 1, false);
        bubble.text.ForceMeshUpdate();
        Vector2 size = bubble.text.GetRenderedValues(false);
        float w = Mathf.Max(0.6f, size.x + 0.3f), h = Mathf.Max(0.3f, size.y + 0.18f);

        var backObj = new GameObject("Back");
        backObj.transform.SetParent(obj.transform, false);
        bubble.back = backObj.AddComponent<SpriteRenderer>();
        bubble.back.sprite = SpriteFactory.PanelFill;
        bubble.back.drawMode = SpriteDrawMode.Sliced;
        bubble.back.size = new Vector2(w, h);
        bubble.back.color = new Color(1f, 0.97f, 0.9f, 0.96f);
        bubble.back.sortingOrder = Order;
        bubble.Update();
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        if (follow != null) transform.position = follow.position + new Vector3(0f, 0.85f + 0.1f * Ease.OutCubic(age / 0.3f), 0f);
        float fade = 1f - Mathf.Clamp01((age - (Life - 0.4f)) / 0.4f);
        float pop = Ease.OutBack(Mathf.Clamp01(age / 0.25f));
        transform.localScale = Vector3.one * pop;
        Color c = back.color;
        c.a = 0.96f * fade;
        back.color = c;
        Color t = text.color;
        t.a = fade;
        text.color = t;
        if (age >= Life) Destroy(gameObject);
    }
}
