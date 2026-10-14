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
    private const float IntroDuration = 2.0f;

    /// <summary>ヒントの札と編成バーのあいだ</summary>
    private const float HintGap = 24f;

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

    // 次の敵（何が・何体・あと何秒）と「早く呼ぶ」
    private ChamferBox nextPlate;
    private VisualElement nextIcon;
    private Label nextIconLabel;
    private Label nextCount;
    private Label nextTime;
    private C3Button callButton;
    private EnemyData shownNextEnemy;
    private int shownNextCount = -1;
    private int shownNextSeconds = -2;
    private int shownCallBonus = -1;

    // 初めて出る敵の知らせ（Kingdom Rush の新しい敵の札）
    private VisualElement newEnemyLayer;
    private ChamferBox newEnemyPlate;
    private VisualElement newEnemyIcon;
    private Label newEnemyIconLabel;
    private Label newEnemyName;
    private Label newEnemyTraits;
    private Enemy newEnemyShown;
    private float newEnemyRemaining;
    private IVisualElementScheduledItem newEnemyTween;
    private readonly System.Collections.Generic.Queue<Enemy> newEnemyQueue = new System.Collections.Generic.Queue<Enemy>();
    private System.Collections.Generic.HashSet<string> knownEnemies = new System.Collections.Generic.HashSet<string>();
    private const float NewEnemyDuration = 7f;

    // ボスの体力（ボスが場にいるあいだだけ出す）
    private VisualElement bossLayer;
    private Label bossName;
    private Label bossHp;
    private VisualElement bossFill;
    private Enemy shownBoss;
    private IVisualElementScheduledItem bossTween;

    // 操作のヒント（ステージに hints があるときだけ）
    private ChamferBox hintPlate;
    private Label hintText;
    private int hintIndex;
    // 消えるまでの残り秒。2倍速でも読めるよう実時間で数え、一時停止中は減らさない
    private float hintRemaining;
    private bool hintShown;
    private IVisualElementScheduledItem hintTween;

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
    private Label resultMilestone;
    private Label resultLife;
    private Label resultTime;
    private Label resultEnemy;
    private VisualElement resultStars;
    private VisualElement resultTeam;
    private C3Button nextButton;

    // 編成バーと選択中の部員
    private DeployBar deployBar;
    private UnitPanel unitPanel;
    private EnemyPanel enemyPanel;
    private CutIn cutIn;

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
        BuildBossBar();
        BuildHint();
        BuildNewEnemy();
        deployBar = new DeployBar(root);
        // ヒントの札は編成バーの左の空いたところに収める（幅の上限は USS の max-width）
        deployBar.Root.RegisterCallback<GeometryChangedEvent>(e =>
            hintPlate.style.right = root.layout.width - deployBar.Root.layout.xMin + HintGap);
        unitPanel = new UnitPanel(root);
        enemyPanel = new EnemyPanel(root);
        cutIn = new CutIn(root);
        deployBar.AttachTooltip(root);
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
        BuildNext(bar);

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

    /// <summary>次の敵の札：敵のアイコン・数・あと何秒。間があいているときは「呼ぶ」で早く出して DP をもらえる</summary>
    private void BuildNext(VisualElement bar)
    {
        nextPlate = new ChamferBox();
        nextPlate.AddToClassList("stat");
        nextPlate.AddToClassList("next-plate");
        nextPlate.SetFill(Palette.UiPanel).SetStroke(Palette.UiPanelLine, 1f).SetCut(12f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight);
        var text = UIFactory.Box("stat-text");
        text.Add(UIFactory.Text("NEXT", UIFont.Display, "stat-caption"));
        var row = UIFactory.Box("stat-value-row");
        nextTime = UIFactory.Text("", UIFont.Display, "stat-value");
        row.Add(nextTime);
        text.Add(row);
        nextPlate.Add(text);
        nextIcon = UIFactory.Box("next-icon");
        nextIconLabel = UIFactory.Text("", UIFont.Title, "next-icon-label");
        nextIcon.Add(nextIconLabel);
        nextPlate.Add(nextIcon);
        nextCount = UIFactory.Text("", UIFont.Display, "next-count");
        nextPlate.Add(nextCount);
        callButton = new C3Button(IconKind.Next, 20f, "呼ぶ", () =>
        {
            Battle b = Battle.Current;
            if (b != null) b.CallEarly();
        });
        callButton.AddToClassList("call-button");
        nextPlate.Add(callButton);
        bar.Add(nextPlate);
    }

    private void RefreshNext(Battle battle)
    {
        float wait = battle.NextSpawnIn;
        bool show = wait >= 0f && battle.State.Phase == GamePhase.Playing;
        UIFactory.SetVisible(nextPlate, show);
        if (!show) return;

        int count;
        EnemyData enemy = battle.NextEnemy(out count);
        if (!ReferenceEquals(enemy, shownNextEnemy))
        {
            shownNextEnemy = enemy;
            Sprite art = enemy != null ? Art.Enemy(enemy.id) : null;
            nextIcon.style.backgroundImage = art != null ? new StyleBackground(art) : new StyleBackground(StyleKeyword.None);
            nextIcon.style.backgroundColor = art != null ? new Color(1f, 1f, 1f, 0.06f) : Palette.Hex(enemy != null ? enemy.color : null, Palette.Crimson);
            nextIconLabel.text = art != null || enemy == null ? "" : enemy.Label;
            Color frame = enemy != null && enemy.boss ? Palette.Amber : Palette.Crimson;
            nextIcon.style.borderLeftColor = nextIcon.style.borderRightColor = nextIcon.style.borderTopColor = nextIcon.style.borderBottomColor = frame;
        }
        if (count != shownNextCount)
        {
            shownNextCount = count;
            nextCount.text = "×" + count;
        }
        int seconds = Mathf.CeilToInt(wait);
        if (seconds != shownNextSeconds)
        {
            shownNextSeconds = seconds;
            nextTime.text = seconds <= 0 ? "NOW" : seconds + "s";
        }
        bool canCall = battle.CanCallEarly;
        UIFactory.SetVisible(callButton, canCall);
        if (canCall)
        {
            int bonus = battle.CallEarlyBonus;
            if (bonus != shownCallBonus)
            {
                shownCallBonus = bonus;
                callButton.Text.text = (Application.isMobilePlatform ? "+" : "呼ぶ +") + bonus;
            }
        }
    }

    /// <summary>初めて出る敵の札（画面の上のまん中。押すとその敵の札を開く）</summary>
    private void BuildNewEnemy()
    {
        newEnemyLayer = UIFactory.Box("new-enemy-layer");
        newEnemyPlate = new ChamferBox();
        newEnemyPlate.AddToClassList("new-enemy");
        newEnemyPlate.SetFill(Palette.UiBand).SetStroke(Palette.WithAlpha(Palette.Crimson, 0.7f), 1.5f)
            .SetCut(16f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetAccent(Palette.Crimson, 6f).SetStripes(Palette.UiStripe, 18f);
        newEnemyPlate.pickingMode = PickingMode.Position;
        newEnemyIcon = UIFactory.Box("new-enemy-icon");
        newEnemyIconLabel = UIFactory.Text("", UIFont.Title, "new-enemy-icon-label");
        newEnemyIcon.Add(newEnemyIconLabel);
        newEnemyPlate.Add(newEnemyIcon);
        var text = UIFactory.Box("new-enemy-text");
        var head = UIFactory.Box("new-enemy-head");
        head.Add(UIFactory.Text("NEW ENEMY", UIFont.Display, "new-enemy-caption"));
        newEnemyName = UIFactory.Text("", UIFont.Title, "new-enemy-name");
        head.Add(newEnemyName);
        text.Add(head);
        newEnemyTraits = UIFactory.Text("", UIFont.Medium, "new-enemy-traits");
        text.Add(newEnemyTraits);
        newEnemyPlate.Add(text);
        newEnemyPlate.AddManipulator(new Clickable(() =>
        {
            if (newEnemyShown != null && newEnemyShown.Alive && BattleInput.Instance != null) BattleInput.Instance.Inspect(newEnemyShown);
            HideNewEnemy();
        }));
        newEnemyLayer.Add(newEnemyPlate);
        UIFactory.SetVisible(newEnemyLayer, false);
        root.Add(newEnemyLayer);
    }

    private void OnEnemySpawned(Enemy e)
    {
        if (e == null || e.Data == null || knownEnemies.Contains(e.Data.id)) return;
        knownEnemies.Add(e.Data.id);
        newEnemyQueue.Enqueue(e);
    }

    private void RefreshNewEnemy(Battle battle)
    {
        if (UIFactory.IsVisible(newEnemyLayer))
        {
            if (!TimeController.Paused) newEnemyRemaining -= Time.unscaledDeltaTime;
            if (newEnemyRemaining <= 0f || battle.State.Phase != GamePhase.Playing) HideNewEnemy();
        }
        // ボスの体力バーが出ていれば、その下に
        newEnemyLayer.style.top = shownBoss != null ? 196f : 104f;
        if (UIFactory.IsVisible(newEnemyLayer) || newEnemyQueue.Count == 0 || battle.State.Phase != GamePhase.Playing) return;

        Enemy e = newEnemyQueue.Dequeue();
        EnemyData d = e.Data;
        newEnemyShown = e;
        newEnemyRemaining = NewEnemyDuration;
        Sprite art = Art.Enemy(d.id);
        newEnemyIcon.style.backgroundImage = art != null ? new StyleBackground(art) : new StyleBackground(StyleKeyword.None);
        newEnemyIcon.style.backgroundColor = art != null ? new Color(1f, 1f, 1f, 0.06f) : Palette.Hex(d.color, Palette.Crimson);
        newEnemyIconLabel.text = art != null ? "" : d.Label;
        newEnemyName.text = d.name + (d.boss ? "（ボス）" : "");
        string traits = EnemyPanel.Traits(d);
        newEnemyTraits.text = traits.Length > 0 ? SkillText.Format(traits) : "特別な性質はない";
        UIFactory.SetVisible(newEnemyLayer, true);
        UITween.Stop(ref newEnemyTween);
        newEnemyTween = UITween.Run(newEnemyPlate, 0.35f, t =>
        {
            float k = Ease.OutCubic(t);
            newEnemyPlate.style.opacity = k;
            newEnemyPlate.style.translate = new Translate(0f, (1f - k) * -20f);
        });
        AudioManager.PlaySE("skill_ready");
    }

    private void HideNewEnemy()
    {
        newEnemyShown = null;
        newEnemyRemaining = 0f;
        UITween.Stop(ref newEnemyTween);
        UIFactory.SetVisible(newEnemyLayer, false);
    }

    private void BuildBossBar()
    {
        bossLayer = UIFactory.Box("boss-layer");
        var plate = new ChamferBox();
        plate.AddToClassList("boss-plate");
        plate.SetFill(Palette.UiPanel).SetCut(14f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetAccent(Palette.Crimson, 5f);
        var head = UIFactory.Box("boss-head");
        head.Add(UIFactory.Text("BOSS", UIFont.Display, "boss-caption"));
        bossName = UIFactory.Text("", UIFont.Title, "boss-name");
        head.Add(bossName);
        head.Add(UIFactory.Box("top-spacer"));
        bossHp = UIFactory.Text("", UIFont.Display, "boss-hp");
        head.Add(bossHp);
        plate.Add(head);
        var track = UIFactory.Box("boss-track");
        bossFill = UIFactory.Box("boss-fill");
        track.Add(bossFill);
        plate.Add(track);
        bossLayer.Add(plate);
        UIFactory.SetVisible(bossLayer, false);
        root.Add(bossLayer);
    }

    /// <summary>場にいる最初のボスの体力を出す</summary>
    private void RefreshBoss(Battle battle)
    {
        Enemy boss = null;
        foreach (Enemy e in battle.Enemies)
            if (e != null && e.Alive && e.Data.boss) { boss = e; break; }
        if (!ReferenceEquals(boss, shownBoss))
        {
            shownBoss = boss;
            UIFactory.SetVisible(bossLayer, boss != null);
            UITween.Stop(ref bossTween);
            if (boss != null)
            {
                bossName.text = boss.Data.name;
                bossTween = UITween.Run(bossLayer, 0.4f, t =>
                {
                    float e = Ease.OutCubic(t);
                    bossLayer.style.opacity = e;
                    bossLayer.style.translate = new Translate(0f, (1f - e) * -24f);
                });
            }
        }
        if (boss == null) return;
        float rate = Mathf.Clamp01(boss.Hp / boss.MaxHp);
        bossFill.style.width = Length.Percent(rate * 100f);
        bossHp.text = Mathf.CeilToInt(boss.Hp).ToString("N0") + " / " + Mathf.CeilToInt(boss.MaxHp).ToString("N0");
    }

    private void BuildHint()
    {
        hintPlate = new ChamferBox();
        hintPlate.AddToClassList("hint-plate");
        hintPlate.SetFill(Palette.UiPanel).SetCut(14f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight).SetAccent(Palette.Amber, 5f);
        hintPlate.Add(UIFactory.Text("HINT", UIFont.Display, "hint-caption"));
        hintText = UIFactory.Text("", UIFont.Body, "hint-text");
        hintPlate.Add(hintText);
        UIFactory.SetVisible(hintPlate, false);
        root.Add(hintPlate);
    }

    /// <summary>ステージの hints を時刻どおりに1つずつ出す</summary>
    private void RefreshHint(Battle battle)
    {
        GameState state = battle.State;
        if (hintShown && !TimeController.Paused) hintRemaining -= Time.unscaledDeltaTime;
        if (hintShown && (hintRemaining <= 0f || state.Phase != GamePhase.Playing))
        {
            hintShown = false;
            UITween.Stop(ref hintTween);
            UIFactory.SetVisible(hintPlate, false);
        }
        var hints = battle.Stage.hints;
        if (state.Phase != GamePhase.Playing || hints == null || hintIndex >= hints.Count) return;
        if (hints[hintIndex].time > state.ElapsedTime) return;

        HintData hint = hints[hintIndex++];
        // スマホでは「クリック」を「タップ」と読み替える
        hintText.text = Application.isMobilePlatform && hint.text != null ? hint.text.Replace("クリック", "タップ") : hint.text;
        hintRemaining = Mathf.Max(1f, hint.duration);
        hintShown = true;
        UIFactory.SetVisible(hintPlate, true);
        UITween.Stop(ref hintTween);
        hintTween = UITween.Run(hintPlate, 0.35f, t =>
        {
            float e = Ease.OutCubic(t);
            hintPlate.style.opacity = e;
            hintPlate.style.translate = new Translate((1f - e) * -30f, 0f);
        });
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
        // スマホにはキーボードがないので、キーの案内は出さない
        band.Add(UIFactory.Text(Application.isMobilePlatform ? "一時停止中" : "一時停止中　Space で再開", UIFont.Body, "pause-sub"));
        pauseLayer.Add(band);

        // 一時停止中はメニュー（後ろのマップは触れない）
        pauseLayer.pickingMode = PickingMode.Position;
        var buttons = UIFactory.Box("result-buttons");
        var resume = new C3Button(IconKind.Play, 26f, "再開", () => TimeController.SetPaused(false));
        resume.AddToClassList("result-button");
        resume.Active = true;
        buttons.Add(resume);
        var retry = new C3Button(IconKind.Retry, 28f, "やり直す", () => bootstrap.Retry());
        retry.AddToClassList("result-button");
        buttons.Add(retry);
        var quit = new C3Button(IconKind.Back, 28f, "作戦選択へ", () => bootstrap.ShowStageSelect());
        quit.AddToClassList("result-button");
        buttons.Add(quit);
        pauseLayer.Add(buttons);
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

        resultStars = UIFactory.Box("result-stars");
        for (int i = 0; i < 3; i++)
        {
            var star = new IconView(IconKind.Star, 46f, Palette.Amber);
            star.AddToClassList("result-star");
            resultStars.Add(star);
        }
        resultContent.Add(resultStars);

        resultMilestone = UIFactory.Text("", UIFont.Body, "result-milestone");
        resultContent.Add(resultMilestone);

        var stats = UIFactory.Box("result-stats");
        resultLife = AddResultStat(stats, "LIFE");
        resultTime = AddResultStat(stats, "TIME");
        resultEnemy = AddResultStat(stats, "ENEMY");
        resultContent.Add(stats);
        resultBand.Add(resultContent);
        resultLayer.Add(resultBand);

        // 部員の活躍（置いた部員ごとの与えたダメージ・回復と MVP）
        resultTeam = UIFactory.Box("result-team");
        resultLayer.Add(resultTeam);

        resultFooter = UIFactory.Box("result-buttons");
        var retry = new C3Button(IconKind.Retry, 30f, "もう一度", () => bootstrap.Retry());
        retry.AddToClassList("result-button");
        resultFooter.Add(retry);
        nextButton = new C3Button(IconKind.Next, 30f, "次の作戦", () => bootstrap.GoToNext());
        nextButton.AddToClassList("result-button");
        nextButton.Active = true;
        resultFooter.Add(nextButton);
        var select = new C3Button(IconKind.Back, 30f, "作戦選択へ", () => bootstrap.ShowStageSelect());
        select.AddToClassList("result-button");
        resultFooter.Add(select);
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
        // 出撃中だけ HUD を出す（タイトル・作戦選択・編成では隠す）
        Battle battle = Battle.Current;
        bool inBattle = battle != null && battle.State != null;
        root.style.display = inBattle ? DisplayStyle.Flex : DisplayStyle.None;
        if (!inBattle)
        {
            shownBattle = null;
            return;
        }
        HandleKeys();
        GameState state = battle.State;

        if (battle != shownBattle) OnBattleChanged(battle);
        deployBar.Refresh(battle);
        unitPanel.Refresh(BattleInput.Instance != null ? BattleInput.Instance.Selected : null);
        enemyPanel.Refresh(BattleInput.Instance != null ? BattleInput.Instance.Inspected : null);
        RefreshBoss(battle);
        RefreshHint(battle);
        RefreshNext(battle);
        RefreshNewEnemy(battle);

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
        if (TimeController.Paused && introTween != null)
        {
            // 一時停止メニューの後ろに作戦開始の帯が透けないように消す
            UITween.Stop(ref introTween);
            UIFactory.SetVisible(introLayer, false);
        }

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
        battle.SkillActivated += c => cutIn.Play(c);
        battle.EnemySpawned += OnEnemySpawned;
        knownEnemies = GameData.EnemiesBefore(battle.Stage.id);
        newEnemyQueue.Clear();
        HideNewEnemy();
        shownNextEnemy = null;
        shownNextCount = shownNextSeconds = shownCallBonus = -1;

        StageData stage = battle.Stage;
        opCode.text = stage.Code;
        UIFactory.SetVisible(opCode, !string.IsNullOrEmpty(stage.Code));
        UIFactory.SetVisible(opSep, !string.IsNullOrEmpty(stage.Code));
        opName.text = stage.Title;
        lifeMax.text = "/" + battle.State.MaxLife;

        shownSeconds = shownFinished = shownLife = shownSpeed = shownPaused = -1;
        shownPhase = GamePhase.Playing;
        hintIndex = 0;
        hintShown = false;
        UITween.Stop(ref hintTween);
        UIFactory.SetVisible(hintPlate, false);
        UITween.Stop(ref resultTween);
        UIFactory.SetVisible(resultLayer, false);
        deployBar.Rebuild(battle);
        PlayIntro(stage);
    }

    // ================= 演出 =================

    /// <summary>作戦開始の帯：開いて、名前が滑り込み、閉じる</summary>
    private void PlayIntro(StageData stage)
    {
        AudioManager.PlaySE("start");
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

    /// <summary>章の最後のステージなら章のクリアと次の章、全作戦の最後なら卒業のひとこと（それ以外は null）</summary>
    private static string Milestone(string stageId)
    {
        ChapterData chapter = GameData.Campaign.ChapterOf(stageId);
        if (chapter == null || chapter.stages[chapter.stages.Count - 1] != stageId) return null;
        var chapters = GameData.Campaign.chapters;
        int index = chapters.IndexOf(chapter);
        ChapterData next = index + 1 < chapters.Count ? chapters[index + 1] : null;
        // 章の clearText があればそれ（4-5 の卒業など）。次の章があれば、開いたことを添える
        string text = !string.IsNullOrEmpty(chapter.clearText) ? chapter.clearText : chapter.name + "　クリア！";
        if (next != null) text += "　次の章「" + next.name + "」が開いた";
        return text;
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
        UIFactory.SetVisible(nextButton, won && bootstrap.NextStageId() != null);
        string milestone = won && shownBattle != null ? Milestone(shownBattle.Stage.id) : null;
        resultMilestone.text = milestone ?? "";
        UIFactory.SetVisible(resultMilestone, milestone != null);

        // ★：ライフを減らさずにクリアで3、減らしてクリアで2（Progress と同じ決まり）
        int stars = !won ? 0 : state.Life >= state.MaxLife ? 3 : 2;
        UIFactory.SetVisible(resultStars, won);
        for (int i = 0; i < resultStars.childCount; i++)
            ((IconView)resultStars[i]).SetColor(i < stars ? Palette.AmberBright : new Color(1f, 1f, 1f, 0.15f));

        FillTeam(shownBattle);

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
            float team = Ease.OutCubic((time - 0.4f) / 0.4f);
            resultTeam.style.opacity = team;
            resultTeam.style.translate = new Translate(0f, (1f - team) * 24f);
            float footer = Ease.OutCubic((time - 0.5f) / 0.4f);
            resultFooter.style.opacity = footer;
            resultFooter.style.translate = new Translate(0f, (1f - footer) * 24f);
        });
    }

    /// <summary>部員の活躍：置いた部員ごとに、いちばん大きな働き（与えたダメージ・回復・止めた時間）と、いちばん活躍した部員に MVP</summary>
    private void FillTeam(Battle battle)
    {
        resultTeam.Clear();
        if (battle == null || battle.Stats == null) return;
        var list = battle.Stats.Deployed();
        UIFactory.SetVisible(resultTeam, list.Count > 0);
        if (list.Count == 0) return;
        MemberStats mvp = battle.Stats.Mvp();
        float max = 1f;
        foreach (MemberStats s in list) max = Mathf.Max(max, s.Score);

        resultTeam.Add(UIFactory.Text("部員の活躍", UIFont.Body, "team-caption"));
        var row = UIFactory.Box("team-row");
        foreach (MemberStats s in list)
        {
            bool isMvp = ReferenceEquals(s, mvp);
            Color classColor = Palette.Hex(s.Class.color, Palette.Amber);
            var chip = new ChamferBox();
            chip.AddToClassList("team-chip");
            chip.SetFill(Palette.UiPanel).SetStroke(isMvp ? Palette.AmberBright : Palette.WithAlpha(classColor, 0.6f), isMvp ? 3f : 1.5f)
                .SetCut(12f, ChamferBox.Corners.TopRight | ChamferBox.Corners.BottomLeft);

            var head = UIFactory.Box("team-head");
            var portrait = UIFactory.Box("team-portrait");
            Sprite icon = Art.Icon(s.Data.id);
            if (icon != null) portrait.style.backgroundImage = new StyleBackground(icon);
            else
            {
                portrait.style.backgroundColor = Palette.Hex(s.Data.color, classColor);
                portrait.Add(UIFactory.Text(s.Data.Initial, UIFont.Title, "team-initial"));
            }
            portrait.style.borderLeftColor = portrait.style.borderRightColor = portrait.style.borderTopColor = portrait.style.borderBottomColor = classColor;
            head.Add(portrait);
            head.Add(UIFactory.Text(s.Data.name, UIFont.Body, "team-name"));
            chip.Add(head);

            // いちばん大きな働きを1つ、数字で
            string caption, value;
            if (s.Heal > s.Damage && s.Heal >= 1f)
            {
                caption = "回復";
                value = Mathf.RoundToInt(s.Heal).ToString("N0");
            }
            else if (s.BlockTime >= 10f && s.Taken > s.Damage)
            {
                caption = "止めた";
                value = Mathf.RoundToInt(s.BlockTime) + "秒";
            }
            else
            {
                caption = "ダメージ";
                value = Mathf.RoundToInt(s.Damage).ToString("N0");
            }
            var line = UIFactory.Box("team-line");
            line.Add(UIFactory.Text(caption, UIFont.Body, "team-stat-caption"));
            line.Add(UIFactory.Text(value, UIFont.Display, "team-stat-value"));
            chip.Add(line);
            chip.Add(UIFactory.Text(s.Kills > 0 ? "撃破 " + s.Kills : "撃破 0", UIFont.Body, "team-sub"));

            var track = UIFactory.Box("team-track");
            var fill = UIFactory.Box("team-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01(s.Score / max) * 100f);
            fill.style.backgroundColor = isMvp ? Palette.AmberBright : classColor;
            track.Add(fill);
            chip.Add(track);

            if (isMvp) chip.Add(UIFactory.Text("MVP", UIFont.Display, "team-mvp"));
            row.Add(chip);
        }
        resultTeam.Add(row);
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

    /// <summary>マウスを重ねている編成バーのカードの部員（いなければ null）</summary>
    public SquadMember HoveredCard
    {
        get { return deployBar != null ? deployBar.Hovered : null; }
    }

    /// <summary>画面のこの位置（Input System の座標：左下が原点）に、クリックを受ける UI があるか</summary>
    public bool IsOverUI(Vector2 screenPosition)
    {
        IPanel panel = root != null ? root.panel : null;
        if (panel == null) return false;
        Vector2 panelPos = UIFactory.ScreenToPanel(root, screenPosition, Screen.width, Screen.height);
        VisualElement picked = panel.Pick(panelPos);
        return picked != null && picked != root;
    }

    /// <summary>ビルドしたゲームでは、ほかのウィンドウに切り替えたら出撃中を一時停止する（エディタでは止めない）</summary>
    void OnApplicationFocus(bool focus)
    {
        if (Application.isEditor || focus) return;
        Battle battle = Battle.Current;
        if (battle != null && battle.State != null && battle.State.Phase == GamePhase.Playing)
            TimeController.SetPaused(true);
    }

    /// <summary>Space：一時停止、R：やり直し、N：次のステージ（テスト用）</summary>
    private void HandleKeys()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.spaceKey.wasPressedThisFrame) TimeController.TogglePause();
        if (kb.rKey.wasPressedThisFrame) bootstrap.Retry();
        if (kb.nKey.wasPressedThisFrame) bootstrap.StartBattle(bootstrap.NextStageId(true));
    }
}
