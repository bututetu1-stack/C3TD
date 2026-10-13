using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>UI の小さなアニメーション。実時間で進むので、一時停止中や2倍速でも同じ速さで動く</summary>
public static class UITween
{
    /// <summary>duration 秒かけて t を 0→1 に進め、毎フレーム apply(t) を呼ぶ。終わったら done</summary>
    public static IVisualElementScheduledItem Run(VisualElement owner, float duration, Action<float> apply, Action done = null)
    {
        float start = Time.unscaledTime;
        IVisualElementScheduledItem item = null;
        apply(0f);
        item = owner.schedule.Execute(() =>
        {
            float t = duration > 0f ? Mathf.Clamp01((Time.unscaledTime - start) / duration) : 1f;
            apply(t);
            if (t >= 1f)
            {
                item.Pause();
                if (done != null) done();
            }
        }).Every(0);
        return item;
    }

    public static void Stop(ref IVisualElementScheduledItem item)
    {
        if (item != null) item.Pause();
        item = null;
    }
}
