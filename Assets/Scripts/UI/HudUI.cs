using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// 出撃中の HUD。上のバー（作戦名・経過時間・敵の数・ライフ・速度・一時停止）、
/// 作戦開始の帯、一時停止の帯、勝敗の帯、拠点に着かれたときの赤い光。
/// 値は毎フレーム Battle.Current から読む。
/// </summary>
public class HudUI : MonoBehaviour
{
    private const float IntroDuration = 2.6f;

    private GameBootstrap bootstrap;
    private VisualElement root;

    // 上のバー
    private Label opCode;
    private VisualElement opSep;
    private Label opName;
    private Label timeValue;
    private Label enemyValue;
    private Label enemyTotal;
    private Label lifeValue;
    private Label lifeMax;
    private ChamferBox lifePlate;
    private C3Button speedButton;
    private C3Button pauseButton;

    // 全面の演出
    private VisualElement damageFlash;
    private VisualElement pauseLayer;
    private VisualElement introLayer;
    private ChamferBox introBand;
    private VisualElement introContent;
    private Label introCaption;
    private Label introName;
    private VisualElement resultLayer;
    private ChamferBox resultBand;
    private VisualElement resultContent;
    private VisualElement resultFooter;
    private Label resultEnglish;
    private Label resultTitle;
    private Label resultLife;
    private Label resultTime;
    private Label resultEnemy;
    private C3Button nextButton;

    private IVisualElementScheduledItem flashTween;
    private IVisualElementScheduledItem shakeTween;
    private IVisualElementScheduledItem introTween;
    private IVisualElementScheduledItem resultTween;

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
        root = UIFactory.CreateScreen("HUD", transform, 10);

        root.Add(UIFactory.Image(UIFactory.Vignette, Palette.UiVignette, "full"));
        root.Add(UIFactory.Image(UIFactory.FadeDown, Palette.UiShade, "top-shade"));
        root.Add(UIFactory.Image(UIFactory.FadeUp, Palette.UiShade, "bottom-shade"));
        damageFlash = UIFactory.Image(UIFactory.Vignette, Palette.Crimson, "full");
        damageFlash.style.opacity = 0f;
        root.Add(damageFlash);

        BuildTopBar();
        BuildIntro();
        BuildPause();
        BuildResult();
    }

    // ================= 組み立て =================

    private void BuildTopBar()
    {
        var bar = UIFactory.Box("top-bar");
        root.Add(bar);

        var op = new ChamferBox();
        op.AddToClassList("op-plate");
        op.SetFill(Palette.UiPanel).SetCut(26f, ChamferBox.Corners.BottomRight).SetAccent(Palette.Amber, 6f).SetStripes(Palette.UiStripe, 16f);
        op.Add(UIFactory.Text("OPERATION", UIFont.Display, "op-caption"));
        var row = UIFactory.Box("op-row");
        opCode = UIFactory.Text("", UIFont.Display, "op-code");
        row.Add(opCode);
        opSep = UIFactory.Box("op-sep");
        row.Add(opSep);
        opName = UIFactory.Text("", UIFont.Title, "op-name");
        row.Add(opName);
        op.Add(row);
        bar.Add(op);

        bar.Add(UIFactory.Box("top-spacer"));

        Label unused;
        AddStat(bar, IconKind.Clock, Palette.TextSub, "TIME", out timeValue, out unused);
        AddStat(bar, IconKind.Paper, Palette.TextSub, "ENEMY", out enemyValue, out enemyTotal);
        lifePlate = AddStat(bar, IconKind.Heart, Palette.Crimson, "LIFE", out lifeValue, out lifeMax);
        lifeValue.AddToClassList("life-value");

        speedButton = new C3Button(IconKind.FastForward, 26f, "x1", TimeController.ToggleSpeed);
        speedButton.AddToClassList("hud-button");
        speedButton.AddToClassList("hud-button--speed");
        bar.Add(speedButton);
        pauseButton = new C3Button(IconKind.Pause, 26f, null, TimeController.TogglePause);
        pauseButton.AddToClassList("hud-button");
        bar.Add(pauseButton);
    }

    private ChamferBox AddStat(VisualElement bar, IconKind icon, Color iconColor, string caption, out Label value, out Label sub)
    {
        var plate = new ChamferBox();
        plate.AddToClassList("stat");
        plate.SetFill(Palette.UiPanel).SetStroke(Palette.UiPanelLine, 1f).SetCut(12f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight);
        var iconView = new IconView(icon, 30f, iconColor);
        iconView.AddToClassList("stat-icon");
        plate.Add(iconView);

        var text = UIFactory.Box("stat-text");
        text.Add(UIFactory.Text(caption, UIFont.Display, "stat-caption"));
        var valueRow = UIFactory.Box("stat-value-row");
        value = UIFactory.Text("", UIFont.Display, "stat-value");
        valueRow.Add(value);
        sub = UIFactory.Text("", UIFont.Display, "stat-sub");
        valueRow.Add(sub);
        text.Add(valueRow);
        plate.Add(text);
        bar.Add(plate);
        return plate;
    }

    private void BuildIntro()
    {
        introLayer = UIFactory.Box("full", "intro-layer");
        introBand = new ChamferBox();
        introBand.AddToClassList("intro-band");
        introBand.SetFill(Palette.UiBand).SetCut(0f, ChamferBox.Corners.None).SetAccent(Palette.Amber, 10f).SetStripes(Palette.UiStripe, 22f);
        introBand.pickingMode = PickingMode.Ignore;

        introContent = UIFactory.Box("intro-content");
        introCaption = UIFactory.Text("", UIFont.Display, "intro-caption");
        introContent.Add(introCaption);
        introName = UIFactory.Text("", UIFont.Title, "intro-name");
        introContent.Add(introName);
        var start = UIFactory.Box("intro-start-row");
        start.Add(UIFactory.Box("intro-line"));
        start.Add(UIFactory.Text("作戦開始", UIFont.Body, "intro-start"));
        start.Add(UIFactory.Box("intro-line"));
        introContent.Add(start);
        introBand.Add(introContent);
        introLayer.Add(introBand);
        root.Add(introLayer);
        UIFactory.SetVisible(introLayer, false);
    }

    private void BuildPause()
    {
        pauseLayer = UIFactory.Box("full", "dim", "center-column");
        var band = new ChamferBox();
        band.AddToClassList("pause-band");
        band.SetFill(Palette.UiBand).SetCut(0f, ChamferBox.Corners.None).SetStripes(Palette.UiStripe, 22f);
        band.pickingMode = PickingMode.Ignore;
        band.Add(UIFactory.Text("PAUSE", UIFont.Display, "pause-title"));
        band.Add(UIFactory.Text("一時停止中　Space で再開", UIFont.Body, "pause-sub"));
        pauseLayer.Add(band);
        root.Add(pauseLayer);
        UIFactory.SetVisible(pauseLayer, false);
    }

    private void BuildResult()
    {
        resultLayer = UIFactory.Box("full", "dim", "center-column");
        resultLayer.pickingMode = PickingMode.Position; // 後ろをクリックさせない

        resultBand = new ChamferBox();
        resultBand.AddToClassList("result-band");
        resultBand.SetCut(0f, ChamferBox.Corners.None).SetStripes(Palette.UiStripe, 22f);
        resultBand.pickingMode = PickingMode.Ignore;

        resultContent = UIFactory.Box("result-content");
        resultEnglish = UIFactory.Text("", UIFont.Display, "result-en");
        resultContent.Add(resultEnglish);
        resultTitle = UIFactory.Text("", UIFont.Title, "result-title");
        resultContent.Add(resultTitle);

        var stats = UIFactory.Box("result-stats");
        resultLife = AddResultStat(stats, "LIFE");
        resultTime = AddResultStat(stats, "TIME");
        resultEnemy = AddResultStat(stats, "ENEMY");
        resultContent.Add(stats);
        resultBand.Add(resultContent);
        resultLayer.Add(resultBand);

        resultFooter = UIFactory.Box("result-buttons");
        var retry = new C3Button(IconKind.Retry, 30f, "もう一度", () => bootstrap.Retry());
        retry.AddToClassList("result-button");
        resultFooter.Add(retry);
        nextButton = new C3Button(IconKind.Next, 30f, "次のステージ", () => bootstrap.StartStage(bootstrap.NextStageId()));
        nextButton.AddToClassList("result-button");
        resultFooter.Add(nextButton);
        resultLayer.Add(resultFooter);

        root.Add(resultLayer);
        UIFactory.SetVisible(resultLayer, false);
    }

    private Label AddResultStat(VisualElement parent, string caption)
    {
        var item = UIFactory.Box("result-stat");
        item.Add(UIFactory.Text(caption, UIFont.Display, "result-stat-caption"));
        var value = UIFactory.Text("", UIFont.Display, "result-stat-value");
        item.Add(value);
        parent.Add(item);
        return value;
    }

    // ================= 毎フレーム =================

    void Update()
    {
        HandleKeys();

        Battle battle = Battle.Current;
        if (battle == null || battle.State == null) return;
        GameState state = battle.State;

        if (battle != shownBattle) OnBattleChanged(battle);

        int seconds = Mathf.FloorToInt(state.ElapsedTime);
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            timeValue.text = UIFactory.FormatTime(state.ElapsedTime);
        }
        if (state.EnemiesFinished != shownFinished)
        {
            shownFinished = state.EnemiesFinished;
            enemyValue.text = state.EnemiesFinished.ToString();
            enemyTotal.text = "/" + state.EnemiesTotal;
        }
        if (state.Life != shownLife)
        {
            shownLife = state.Life;
            lifeValue.text = state.Life.ToString();
        }
        if (TimeController.Speed != shownSpeed)
        {
            shownSpeed = TimeController.Speed;
            speedButton.Text.text = "x" + TimeController.Speed;
            speedButton.Active = TimeController.Speed > 1;
        }
        int paused = TimeController.Paused ? 1 : 0;
        if (paused != shownPaused)
        {
            shownPaused = paused;
            pauseButton.Icon.SetKind(TimeController.Paused ? IconKind.Play : IconKind.Pause);
            pauseButton.Active = TimeController.Paused;
        }
        UIFactory.SetVisible(pauseLayer, TimeController.Paused && state.Phase == GamePhase.Playing);

        if (state.Phase != shownPhase)
        {
            shownPhase = state.Phase;
            if (state.Phase != GamePhase.Playing) ShowResult(state);
        }
    }

    private void OnBattleChanged(Battle battle)
    {
        shownBattle = battle;
        battle.EnemyLeaked += OnEnemyLeaked;

        StageData stage = battle.Stage;
        opCode.text = stage.Code;
        UIFactory.SetVisible(opCode, !string.IsNullOrEmpty(stage.Code));
        UIFactory.SetVisible(opSep, !string.IsNullOrEmpty(stage.Code));
        opName.text = stage.Title;
        lifeMax.text = "/" + battle.State.MaxLife;

        shownSeconds = shownFinished = shownLife = shownSpeed = shownPaused = -1;
        shownPhase = GamePhase.Playing;
        UITween.Stop(ref resultTween);
        UIFactory.SetVisible(resultLayer, false);
        PlayIntro(stage);
    }

    // ================= 演出 =================

    /// <summary>作戦開始の帯：開いて、名前が滑り込み、閉じる</summary>
    private void PlayIntro(StageData stage)
    {
        introCaption.text = string.IsNullOrEmpty(stage.Code) ? "OPERATION" : "OPERATION  " + stage.Code;
        introName.text = stage.Title;
        UIFactory.SetVisible(introLayer, true);
        UITween.Stop(ref introTween);
        introTween = UITween.Run(introLayer, IntroDuration, t =>
        {
            float time = t * IntroDuration;
            float open = Ease.OutCubic(time / 0.35f);
            float close = Ease.InCubic((time - (IntroDuration - 0.45f)) / 0.45f);
            introBand.style.scale = new Scale(new Vector3(1f, Mathf.Max(0.001f, open * (1f - close)), 1f));
            float show = Ease.OutCubic((time - 0.12f) / 0.5f);
            introContent.style.opacity = show * (1f - close);
            introContent.style.translate = new Translate((1f - show) * -80f + close * 80f, 0f);
        }, () => UIFactory.SetVisible(introLayer, false));
    }

    /// <summary>勝敗の帯</summary>
    private void ShowResult(GameState state)
    {
        bool won = state.Phase == GamePhase.Won;
        Color accent = won ? Palette.Amber : Palette.Crimson;
        resultEnglish.text = won ? "OPERATION COMPLETE" : "OPERATION FAILED";
        resultEnglish.style.color = accent;
        resultTitle.text = won ? "防衛成功" : "防衛失敗";
        resultBand.SetFill(Palette.UiBand).SetAccent(accent, 12f);
        resultLife.text = state.Life + "/" + state.MaxLife;
        resultTime.text = UIFactory.FormatTime(state.ElapsedTime);
        resultEnemy.text = state.EnemiesFinished + "/" + state.EnemiesTotal;
        UIFactory.SetVisible(nextButton, bootstrap.NextStageId() != null);

        UITween.Stop(ref introTween);
        UIFactory.SetVisible(introLayer, false);
        UIFactory.SetVisible(resultLayer, true);
        UITween.Stop(ref resultTween);
        resultTween = UITween.Run(resultLayer, 1.0f, t =>
        {
            float time = t * 1.0f;
            resultLayer.style.opacity = Ease.OutCubic(time / 0.25f);
            resultBand.style.scale = new Scale(new Vector3(1f, Mathf.Max(0.001f, Ease.OutCubic((time - 0.05f) / 0.35f)), 1f));
            float title = Ease.OutCubic((time - 0.2f) / 0.45f);
            resultContent.style.opacity = title;
            resultContent.style.translate = new Translate((1f - title) * -100f, 0f);
            float footer = Ease.OutCubic((time - 0.5f) / 0.4f);
            resultFooter.style.opacity = footer;
            resultFooter.style.translate = new Translate(0f, (1f - footer) * 24f);
        });
    }

    /// <summary>拠点に着かれた：画面のふちが赤く光り、ライフの札が揺れる</summary>
    private void OnEnemyLeaked(Enemy enemy)
    {
        UITween.Stop(ref flashTween);
        flashTween = UITween.Run(damageFlash, 0.7f, t => damageFlash.style.opacity = 0.9f * (1f - Ease.OutCubic(t)));
        UITween.Stop(ref shakeTween);
        shakeTween = UITween.Run(lifePlate, 0.45f, t =>
        {
            float k = 1f - t;
            lifePlate.style.translate = new Translate(Mathf.Sin(t * 40f) * 9f * k, 0f);
            float pop = 1f + 0.35f * k * k;
            lifeValue.style.scale = new Scale(new Vector3(pop, pop, 1f));
        });
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
