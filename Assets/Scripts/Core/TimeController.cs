using UnityEngine;

/// <summary>ゲームの速さ（1倍・2倍）と一時停止。Time.timeScale で全体を止めたり速めたりする</summary>
public static class TimeController
{
    public static int Speed { get; private set; }
    public static bool Paused { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetForStage()
    {
        Speed = 1;
        Paused = false;
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

    private static void Apply()
    {
        Time.timeScale = Paused ? 0f : Speed;
    }
}
