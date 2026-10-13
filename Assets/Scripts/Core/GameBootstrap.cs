using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ゲームの入口。Boot シーンに1つだけ置く（どのシーンで再生しても、なければ自動で作る）。
/// カメラ・ライト・HUD を用意して、ステージを始める。
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private string stageId = "stage_01";

    private Battle battle;
    private HudUI hud;

    public string StageId { get { return stageId; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (FindAnyObjectByType<GameBootstrap>() == null) new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
    }

    void Awake()
    {
        Application.targetFrameRate = 60;
        CameraFitter.Ensure();
        EnsureGlobalLight();

        hud = new GameObject("UI").AddComponent<HudUI>();
        hud.Init(this);
    }

    void Start()
    {
        StartStage(stageId);
    }

    public void StartStage(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (battle != null) Destroy(battle.gameObject);
        battle = Battle.Create(id);
        if (battle == null) return;
        stageId = id;
        CameraFitter.Ensure().Fit(battle.Map.WorldRect);
    }

    public void Retry()
    {
        StartStage(stageId);
    }

    /// <summary>Data/Stages の名前順で次のステージ。wrap なら最後の次は最初に戻る</summary>
    public string NextStageId(bool wrap = false)
    {
        List<string> ids = GameData.StageIds();
        if (ids.Count == 0) return null;
        int index = ids.IndexOf(stageId);
        if (index + 1 < ids.Count) return ids[index + 1];
        return wrap ? ids[0] : null;
    }

    /// <summary>スプライトは 2D ライトで照らされるので、全体を照らすライトがなければ作る</summary>
    private static void EnsureGlobalLight()
    {
        foreach (var light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (light.lightType == Light2D.LightType.Global) return;
        }
        var obj = new GameObject("Global Light 2D");
        var global = obj.AddComponent<Light2D>();
        global.lightType = Light2D.LightType.Global;
        global.intensity = 1f;
        global.color = Color.white;
    }
}
