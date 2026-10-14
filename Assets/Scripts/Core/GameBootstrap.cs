using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ゲームの入口と画面の流れ。Boot シーンに1つだけ置く（どのシーンで再生しても、なければ自動で作る）。
/// タイトル → 作戦選択 → 編成 → 出撃 → 結果（もう一度／次の作戦／作戦選択へ）。
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Tooltip("空でなければ、タイトルと編成を飛ばしてこのステージから始める（テスト用）")]
    [SerializeField] private string startStage = "";

    private Battle battle;
    private HudUI hud;
    private MenuUI menu;
    private string stageId;
    private List<string> squad;

    public string StageId { get { return stageId; } }

    /// <summary>強襲で出撃するか（作戦選択で切り替える。クリアしたステージだけ）</summary>
    public bool Hard { get; private set; }

    public void SetHard(bool value)
    {
        Hard = value;
    }

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

        var ui = new GameObject("UI");
        hud = ui.AddComponent<HudUI>();
        hud.Init(this);
        menu = ui.AddComponent<MenuUI>();
        menu.Init(this);
        gameObject.AddComponent<BattleInput>().Init(hud);
    }

    void Start()
    {
        squad = Progress.Squad;
        if (!string.IsNullOrEmpty(startStage)) StartBattle(startStage);
        else ShowTitle();
    }

    // ================= 画面 =================

    public void ShowTitle()
    {
        EndBattle();
        menu.ShowTitle();
    }

    /// <summary>作戦選択。focus のステージを選んだ状態で開く（null なら続きのステージ）</summary>
    public void ShowStageSelect(string focus = null)
    {
        EndBattle();
        menu.ShowStageSelect(focus ?? stageId);
    }

    public void ShowEnemyBook()
    {
        EndBattle();
        menu.ShowEnemyBook();
    }

    public void ShowHelp()
    {
        EndBattle();
        menu.ShowHelp();
    }

    public void ShowRecords()
    {
        EndBattle();
        menu.ShowRecords();
    }

    /// <summary>設定の札（タイトルの上に重ねる）</summary>
    public void ShowSettings()
    {
        menu.ShowSettings();
    }

    public void ShowSquad(string id)
    {
        EndBattle();
        stageId = id;
        menu.ShowSquad(id, squad);
    }

    /// <summary>編成画面で決めた編成を覚える</summary>
    public void SetSquad(List<string> ids)
    {
        squad = new List<string>(ids);
        Progress.Squad = squad;
    }

    public void StartBattle(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (battle != null) Destroy(battle.gameObject);
        battle = Battle.Create(id, squad, Hard && Progress.IsCleared(id));
        if (battle == null) return;
        stageId = id;
        menu.HideAll();
        CameraFitter.Ensure().Fit(battle.Map.WorldRect);
    }

    public void Retry()
    {
        StartBattle(stageId);
    }

    /// <summary>次の作戦の編成へ（最後なら作戦選択へ）</summary>
    public void GoToNext()
    {
        string next = NextStageId();
        Hard = false; // 次の作戦はふつうから
        if (next != null && Progress.IsUnlocked(next)) ShowSquad(next);
        else ShowStageSelect();
    }

    private void EndBattle()
    {
        if (battle != null) Destroy(battle.gameObject);
        battle = null;
        TimeController.ResetForStage(GameData.Rules.focusTimeScale);
    }

    /// <summary>campaign.json の順で次のステージ。wrap なら最後の次は最初に戻る</summary>
    public string NextStageId(bool wrap = false)
    {
        List<string> ids = GameData.Campaign.AllStages();
        if (ids.Count == 0) ids = GameData.StageIds();
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
