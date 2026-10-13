using System.Collections.Generic;
using UnityEngine;

/// <summary>敵の経路に沿って、拠点へ向かって流れる矢印を並べる</summary>
public class PathFlow : MonoBehaviour
{
    private const float Spacing = 0.42f;
    private const float Speed = 0.8f;   // マス/秒
    private const float MarkSize = 0.2f;
    private const float FadeLength = 0.45f;

    private readonly List<SpriteRenderer> marks = new List<SpriteRenderer>();
    private List<Vector3> points;
    private float[] distances;
    private float total;
    private Color color;

    public static PathFlow Create(List<Vector3> points, Transform parent, Color color, int order)
    {
        var obj = new GameObject("PathFlow");
        obj.transform.SetParent(parent, false);
        var flow = obj.AddComponent<PathFlow>();
        flow.points = points;
        flow.color = color;
        flow.distances = new float[points.Count];
        for (int i = 1; i < points.Count; i++) flow.distances[i] = flow.distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        flow.total = flow.distances[points.Count - 1];

        int count = Mathf.FloorToInt(flow.total / Spacing) + 1;
        for (int i = 0; i < count; i++)
        {
            var mark = new GameObject("Mark");
            mark.transform.SetParent(obj.transform, false);
            mark.transform.localScale = new Vector3(MarkSize, MarkSize, 1f);
            var sr = mark.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Chevron;
            sr.sortingOrder = order;
            flow.marks.Add(sr);
        }
        flow.Update();
        return flow;
    }

    void Update()
    {
        // 拠点からの距離で並べるので、終わりが同じ経路どうしは重なる区間の矢印がぴったり重なる
        float offset = Mathf.Repeat(Time.time * Speed, Spacing);
        for (int i = 0; i < marks.Count; i++)
        {
            float d = total - (i * Spacing + (Spacing - offset));
            SpriteRenderer sr = marks[i];
            if (d < 0f)
            {
                sr.enabled = false;
                continue;
            }
            sr.enabled = true;
            Vector3 dir;
            sr.transform.localPosition = Sample(d, out dir);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            Color c = color;
            c.a *= Mathf.Clamp01(d / FadeLength) * Mathf.Clamp01((total - d) / FadeLength);
            sr.color = c;
        }
    }

    private Vector3 Sample(float d, out Vector3 dir)
    {
        for (int i = 1; i < points.Count; i++)
        {
            if (d <= distances[i] || i == points.Count - 1)
            {
                float len = distances[i] - distances[i - 1];
                float t = len > 0f ? (d - distances[i - 1]) / len : 0f;
                dir = (points[i] - points[i - 1]).normalized;
                return Vector3.Lerp(points[i - 1], points[i], t);
            }
        }
        dir = Vector3.right;
        return points[0];
    }
}
