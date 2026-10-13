using UnityEngine;

/// <summary>
/// ゲームの速さ（1倍・2倍）と一時停止、キャラを扱っているあいだのスロー。
/// Time.timeScale で全体を止めたり速めたりする。
/// </summary>
public static class TimeController
{
    public static int Speed { get; private set; }
    public static bool Paused { get; private set; }

    /// <summary>ドラッグ中・選択中はゆっくりにする（アークナイツと同じ）</summary>
    public static bool Focus { get; private set; }

    private static float focusScale = 0.2f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ResetForStage(0.2f);
    }

    public static void ResetForStage(float focusTimeScale)
    {
        Speed = 1;
        Paused = false;
        Focus = false;
        focusScale = Mathf.Clamp01(focusTimeScale);
        Apply();
    }

    public static void ToggleSpeed()
    {
        Speed = Speed == 1 ? 2 : 1;
        Apply();
    }

    public static void TogglePause()
    {
        SetPaused(!Paused);
    }

    public static void SetPaused(bool paused)
    {
        Paused = paused;
        Apply();
    }

    public static void SetFocus(bool focus)
    {
        if (Focus == focus) return;
        Focus = focus;
        Apply();
    }

    private static void Apply()
    {
        Time.timeScale = Paused ? 0f : Speed * (Focus ? focusScale : 1f);
    }
}
