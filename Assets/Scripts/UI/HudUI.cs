using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// 出撃中の HUD。上のバー（ステージ名・経過時間・敵の数・ライフ・速度・一時停止）と、
/// 一時停止の表示、勝敗の画面。値は毎フレーム Battle.Current から読む。
/// </summary>
public class HudUI : MonoBehaviour
{
    private GameBootstrap bootstrap;

    private Label stageName;
    private Label timeValue;
    private Label enemyValue;
    private Label lifeValue;
    private Label lifeMax;
    private Button speedButton;
    private Button pauseButton;
    private VisualElement pauseBanner;

    private VisualElement resultOverlay;
    private Label resultTitle;
    private Label resultDetail;
    private Button nextButton;

    // 前のフレームの値（変わったときだけ書き換える）
    private Battle shownBattle;
    private int shownSeconds = -1;
    private int shownFinished = -1;
    private int shownLife = -1;
    private int shownSpeed = -1;
    private int shownPaused = -1;
    private GamePhase shownPhase;

    public void Init(GameBootstrap owner)
    {
        bootstrap = owner;
        VisualElement root = UIFactory.CreateScreen("HUD", transform, 10);
        BuildTopBar(root);

        pauseBanner = UIFactory.BoldLabel("一時停止中", "pause-banner");
        root.Add(pauseBanner);
        UIFactory.SetVisible(pauseBanner, false);

        BuildResult(root);
    }

    private void BuildTopBar(VisualElement root)
    {
        var bar = UIFactory.Box("hud-top");
        bar.pickingMode = PickingMode.Position; // バーの後ろのマップをクリックしないように
        root.Add(bar);

        stageName = UIFactory.BoldLabel("", "hud-stage-name");
        bar.Add(stageName);

        timeValue = AddChip(bar, "経過", "hud-chip-value");
        enemyValue = AddChip(bar, "敵", "hud-chip-value");

        var lifeChip = UIFactory.Box("hud-chip");
        lifeChip.Add(UIFactory.Label("ライフ", "hud-chip-label"));
        lifeValue = UIFactory.BoldLabel("", "hud-chip-value", "hud-life-value");
        lifeChip.Add(lifeValue);
        lifeMax = UIFactory.Label("", "hud-chip-sub");
        lifeChip.Add(lifeMax);
        bar.Add(lifeChip);

        speedButton = UIFactory.Button("×1", TimeController.ToggleSpeed, "hud-button");
        bar.Add(speedButton);
        pauseButton = UIFactory.Button("一時停止", TimeController.TogglePause, "hud-button", "hud-button--wide");
        bar.Add(pauseButton);
    }

    private Label AddChip(VisualElement bar, string caption, string valueClass)
    {
        var chip = UIFactory.Box("hud-chip");
        chip.Add(UIFactory.Label(caption, "hud-chip-label"));
        var value = UIFactory.BoldLabel("", valueClass);
        chip.Add(value);
        bar.Add(chip);
        return value;
    }

    private void BuildResult(VisualElement root)
    {
        resultOverlay = UIFactory.Box("overlay");
        resultOverlay.pickingMode = PickingMode.Position;
        root.Add(resultOverlay);

        resultTitle = UIFactory.BoldLabel("", "result-title");
        resultOverlay.Add(resultTitle);
        resultDetail = UIFactory.Label("", "result-detail");
        resultOverlay.Add(resultDetail);

        var buttons = UIFactory.Box("result-buttons");
        buttons.Add(UIFactory.Button("もう一度", () => bootstrap.Retry(), "result-button"));
        nextButton = UIFactory.Button("次のステージ", () => bootstrap.StartStage(bootstrap.NextStageId()), "result-button");
        buttons.Add(nextButton);
        resultOverlay.Add(buttons);

        UIFactory.SetVisible(resultOverlay, false);
    }

    void Update()
    {
        HandleKeys();

        Battle battle = Battle.Current;
        if (battle == null || battle.State == null) return;
        GameState state = battle.State;

        if (battle != shownBattle)
        {
            shownBattle = battle;
            stageName.text = battle.Stage.name;
            lifeMax.text = "/" + state.MaxLife;
            shownSeconds = shownFinished = shownLife = shownSpeed = shownPaused = -1;
            shownPhase = GamePhase.Playing;
            UIFactory.SetVisible(resultOverlay, false);
        }

        int seconds = Mathf.FloorToInt(state.ElapsedTime);
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            timeValue.text = UIFactory.FormatTime(state.ElapsedTime);
        }
        if (state.EnemiesFinished != shownFinished)
        {
            shownFinished = state.EnemiesFinished;
            enemyValue.text = state.EnemiesFinished + "/" + state.EnemiesTotal;
        }
        if (state.Life != shownLife)
        {
            shownLife = state.Life;
            lifeValue.text = state.Life.ToString();
        }
        if (TimeController.Speed != shownSpeed)
        {
            shownSpeed = TimeController.Speed;
            speedButton.text = "×" + TimeController.Speed;
            speedButton.EnableInClassList("hud-button--active", TimeController.Speed > 1);
        }
        int paused = TimeController.Paused ? 1 : 0;
        if (paused != shownPaused)
        {
            shownPaused = paused;
            pauseButton.text = TimeController.Paused ? "再開" : "一時停止";
            pauseButton.EnableInClassList("hud-button--active", TimeController.Paused);
            UIFactory.SetVisible(pauseBanner, TimeController.Paused);
        }
        if (state.Phase != shownPhase)
        {
            shownPhase = state.Phase;
            ShowResult(state);
        }
    }

    private void ShowResult(GameState state)
    {
        bool won = state.Phase == GamePhase.Won;
        resultTitle.text = won ? "防衛成功" : "防衛失敗";
        resultTitle.EnableInClassList("result-title--lost", !won);
        resultDetail.text = "ライフ " + state.Life + "/" + state.MaxLife + "　経過 " + UIFactory.FormatTime(state.ElapsedTime);
        UIFactory.SetVisible(nextButton, bootstrap.NextStageId() != null);
        UIFactory.SetVisible(resultOverlay, true);
    }

    /// <summary>Space：一時停止、R：やり直し、N：次のステージ（テスト用）</summary>
    private void HandleKeys()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.spaceKey.wasPressedThisFrame) TimeController.TogglePause();
        if (kb.rKey.wasPressedThisFrame) bootstrap.Retry();
        if (kb.nKey.wasPressedThisFrame) bootstrap.StartStage(bootstrap.NextStageId(true));
    }
}
