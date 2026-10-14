using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1つのステージの出撃。マップを作り、ウェーブの予定どおりに敵を出し、
/// 部員の配置・撤退と DP を管理し、勝敗を決める。
/// ステージ内のものはすべてこの GameObject の子に作るので、壊せばきれいに片付く。
/// </summary>
public class Battle : MonoBehaviour
{
    public static Battle Current { get; private set; }

    public StageData Stage { get; private set; }

    /// <summary>味方全員の攻撃力に足す%（僕の「鯖缶のやる気担当」。ステージが終わるまで残り、あとから置いた部員にも効く）</summary>
    public float TeamAtkPercent { get; private set; }

    /// <summary>味方全員の攻撃力を percent% 上げる（max% まで）。上がったら true</summary>
    public bool AddTeamAtk(float percent, float max)
    {
        float before = TeamAtkPercent;
        TeamAtkPercent = Mathf.Min(max, TeamAtkPercent + percent);
        return TeamAtkPercent > before;
    }
    public StageMap Map { get; private set; }
    public GameState State { get; private set; }

    /// <summary>部員ごとの活躍（結果の画面の「部員の活躍」と MVP）</summary>
    public BattleStats Stats { get; private set; }
    public MapHighlight Highlight { get; private set; }
    public RulesData Rules { get; private set; }

    /// <summary>場の敵が減らしている DP の回復（1秒あたり）</summary>
    public float DpDrain { get; private set; }

    /// <summary>場に出ている敵</summary>
    public readonly List<Enemy> Enemies = new List<Enemy>();

    /// <summary>配置中の部員</summary>
    public readonly List<Character> Characters = new List<Character>();

    /// <summary>編成（編成バーに並ぶ順）</summary>
    public readonly List<SquadMember> Squad = new List<SquadMember>();

    /// <summary>敵が拠点に着いた（HUD の演出用）</summary>
    public event Action<Enemy> EnemyLeaked;

    /// <summary>必殺技が発動した（カットイン用）</summary>
    public event Action<Character> SkillActivated;

    /// <summary>敵が出た（予定どおり・呼ばれて・分かれて。HUD の「NEW ENEMY」用）</summary>
    public event Action<Enemy> EnemySpawned;

    private class SpawnEntry
    {
        public float time;
        public int order;
        public EnemyData enemy;
        public List<Vector3> path;

        /// <summary>エンドレスの波の番号（ふつうは 0）。敵の強さの倍率に使う</summary>
        public int wave;
    }

    // エンドレス（ステージに endless があるとき）
    private EndlessDirector endless;

    public bool IsEndless { get { return endless != null; } }

    /// <summary>エンドレスでいま来ている波（1 から。エンドレスでなければ 0）</summary>
    public int EndlessWave { get { return endless != null ? endless.WaveAt(SpawnClock) : 0; } }

    /// <summary>エンドレスで負けたとき、最高記録を更新したか</summary>
    public bool EndlessNewBest { get; private set; }

    private readonly List<SpawnEntry> schedule = new List<SpawnEntry>();
    private int nextSpawn;
    private int nextWarn;
    // 出撃口ごとの、最後に予告した（予告なしで続いた）敵の出る時刻
    private readonly Dictionary<Vector3, float> lastWarn = new Dictionary<Vector3, float>();

    /// <summary>敵が出てくる何秒前に出撃口で予告するか</summary>
    private const float WarnLead = 2f;

    // 敵の予定を進める時計。「早く呼ぶ」で先へ進めたぶんだけ、ステージの経過時間より先にいる
    private float spawnOffset;
    private float SpawnClock { get { return State.ElapsedTime + spawnOffset; } }

    /// <summary>次の敵が出るまでの秒（もう予定がなければ -1）</summary>
    public float NextSpawnIn
    {
        get { return nextSpawn < schedule.Count ? Mathf.Max(0f, schedule[nextSpawn].time - SpawnClock) : -1f; }
    }

    /// <summary>
    /// 次に出る敵と、そのあと続けて出る同じ敵の数（同じ敵どうしの間が 5 秒をこえたら別の群れ。ほかの敵がはさまってもよい）。
    /// 予定がなければ null
    /// </summary>
    public EnemyData NextEnemy(out int count)
    {
        count = 0;
        if (nextSpawn >= schedule.Count) return null;
        EnemyData enemy = schedule[nextSpawn].enemy;
        float last = schedule[nextSpawn].time;
        for (int i = nextSpawn; i < schedule.Count; i++)
        {
            if (schedule[i].enemy != enemy) continue;
            if (schedule[i].time - last > 5f) break;
            last = schedule[i].time;
            count++;
        }
        return enemy;
    }

    /// <summary>次の敵を早く呼べるか（次まで callEarlyMinGap 秒以上あるとき）</summary>
    public bool CanCallEarly
    {
        get { return State.Phase == GamePhase.Playing && NextSpawnIn >= Rules.callEarlyMinGap; }
    }

    /// <summary>早く呼んだときにもらえる DP</summary>
    public int CallEarlyBonus
    {
        get { return Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(0f, NextSpawnIn) * Rules.callEarlyDpPerSecond)); }
    }

    /// <summary>次の敵をすぐ出す（そのぶん DP をもらう）。予定の時計を進めるので、そのあとの敵もみな早まる</summary>
    public bool CallEarly()
    {
        if (!CanCallEarly) return false;
        int bonus = CallEarlyBonus;
        Vector3 at = schedule[nextSpawn].path[0];
        spawnOffset += NextSpawnIn;
        State.AddDp(bonus, Rules.dpMax);
        Fx.DpGain(at);
        FloatingText.Status(at + new Vector3(0f, 0.5f, 0f), "早く呼んだ　DP +" + bonus);
        AudioManager.PlaySE("buff");
        return true;
    }
    private Transform enemyRoot;
    private Transform characterRoot;
    private GamePhase lastPhase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
    }

    /// <summary>強襲（ライフ 1・敵が強い。エンドレスにはない）</summary>
    public bool Hard { get; private set; }

    /// <summary>ステージを始める。squadIds は編成バーに並べる部員（null なら squad.json）。hard なら強襲</summary>
    public static Battle Create(string stageId, List<string> squadIds, bool hard = false)
    {
        StageData data = GameData.Stage(stageId);
        if (data == null) return null;
        var obj = new GameObject("Battle_" + stageId);
        var battle = obj.AddComponent<Battle>();
        battle.Hard = hard && data.endless == null;
        battle.Init(data, squadIds ?? GameData.Squad.members);
        return battle;
    }

    private void Init(StageData data, List<string> squadIds)
    {
        Current = this;
        Stage = data;
        Rules = GameData.Rules;
        Map = StageMap.FromText(data.map, data.id);
        MapBuilder.Build(Map, transform, data.background);
        Highlight = new MapHighlight(Map, transform);

        var worldPaths = new Dictionary<string, List<Vector3>>();
        AddPaths(data, data.paths, false, worldPaths);
        AddPaths(data, data.airPaths, true, worldPaths);
        BuildSchedule(data, worldPaths);
        if (data.endless != null)
        {
            var ground = new List<List<Vector3>>();
            var air = new List<List<Vector3>>();
            foreach (string key in data.paths.Keys) if (worldPaths.ContainsKey(key)) ground.Add(worldPaths[key]);
            if (data.airPaths != null) foreach (string key in data.airPaths.Keys) if (worldPaths.ContainsKey(key)) air.Add(worldPaths[key]);
            endless = new EndlessDirector(data.endless, ground, air);
        }
        BuildSquad(squadIds);
        Stats = new BattleStats(Squad);

        characterRoot = new GameObject("Characters").transform;
        characterRoot.SetParent(transform, false);
        enemyRoot = new GameObject("Enemies").transform;
        enemyRoot.SetParent(transform, false);

        State = new GameState(Hard ? 1 : data.life, schedule.Count, data.initialDp);
        FillEndless();
        if (endless == null && !Hard && State.MaxLife >= schedule.Count && schedule.Count > 0)
            Debug.LogWarning(data.id + ": ライフ（" + State.MaxLife + "）が敵の数（" + schedule.Count + "）以上なので、まったく防衛しなくても勝てます");
        lastPhase = State.Phase;
        TimeController.ResetForStage(Rules.focusTimeScale);
        // 章ごとの曲（battle2 など）がまだ置かれていなければ、ふつうの戦闘曲を流す
        AudioManager.PlayBGM(!string.IsNullOrEmpty(data.bgm) && AudioManager.HasBGM(data.bgm) ? data.bgm : "battle");
    }

    /// <summary>経路を検査してワールド座標にし、矢印を描く（飛ぶ敵の経路は水色）</summary>
    private void AddPaths(StageData data, Dictionary<string, List<Vector2Int>> paths, bool air, Dictionary<string, List<Vector3>> worldPaths)
    {
        if (paths == null) return;
        foreach (var kv in paths)
        {
            if (worldPaths.ContainsKey(kv.Key)) Debug.LogWarning(data.id + ": 経路の名前 \"" + kv.Key + "\" が paths と airPaths で重なっています");
            if (!Map.ValidatePath(data.id, kv.Key, kv.Value, air)) continue;
            var points = new List<Vector3>();
            foreach (var cell in kv.Value) points.Add(Map.CellToWorld(cell));
            worldPaths[kv.Key] = points;
            MapBuilder.DrawPath(points, transform, air);
        }
    }

    private void BuildSchedule(StageData data, Dictionary<string, List<Vector3>> worldPaths)
    {
        for (int w = 0; w < data.waves.Count; w++)
        {
            WaveData wave = data.waves[w];
            string where = data.id + ": waves[" + w + "]";
            EnemyData enemy = GameData.Enemy(wave.enemy);
            if (enemy == null)
            {
                Debug.LogError(where + " の敵 \"" + wave.enemy + "\" が読めないので飛ばします");
                continue;
            }
            List<Vector3> path;
            if (wave.path == null || !worldPaths.TryGetValue(wave.path, out path))
            {
                Debug.LogError(where + " の経路 \"" + wave.path + "\" がないので飛ばします");
                continue;
            }
            for (int i = 0; i < wave.count; i++)
                schedule.Add(new SpawnEntry { time = wave.time + i * wave.interval, order = schedule.Count, enemy = enemy, path = path });
        }
        // 同じ時刻なら書いた順
        schedule.Sort((a, b) =>
        {
            int c = a.time.CompareTo(b.time);
            return c != 0 ? c : a.order.CompareTo(b.order);
        });
    }

    private void BuildSquad(List<string> squadIds)
    {
        foreach (string id in squadIds)
        {
            CharacterData data = GameData.Character(id);
            if (data == null)
            {
                Debug.LogError("編成の部員 \"" + id + "\" が読めないので外します");
                continue;
            }
            Squad.Add(new SquadMember(data, GameData.Class(data.unitClass)));
        }
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Update()
    {
        if (State.Phase == GamePhase.Playing)
        {
            float dt = Time.deltaTime;
            State.Tick(dt);
            // お祈りメールなどがいるあいだは、DP の回復が遅くなる（0 より下がらない）
            DpDrain = 0f;
            foreach (Enemy e in Enemies)
                if (e.Alive) DpDrain += e.Data.dpDrain;
            State.AddDp((Rules.dpPerSecond - DpDrain) * dt, Rules.dpMax);
            foreach (SquadMember m in Squad)
                if (m.Cooldown > 0f) m.Cooldown = Mathf.Max(0f, m.Cooldown - dt);

            FillEndless();
            while (nextSpawn < schedule.Count && schedule[nextSpawn].time <= SpawnClock)
            {
                SpawnEntry entry = schedule[nextSpawn++];
                Enemy spawned = Enemy.Spawn(entry.enemy, entry.path, this, enemyRoot);
                float hpScale = entry.wave > 0 ? endless.HpScale(entry.wave) : 1f;
                float atkScale = entry.wave > 0 ? endless.AtkScale(entry.wave) : 1f;
                if (Hard)
                {
                    hpScale *= Rules.hardHpScale;
                    atkScale *= Rules.hardAtkScale;
                }
                if (hpScale != 1f || atkScale != 1f) spawned.ApplyScale(hpScale, atkScale);
                Enemies.Add(spawned);
                if (EnemySpawned != null) EnemySpawned(spawned);
                // ボスが出たら BGM をステージの bossBgm（省略時は boss）に（ファイルがなければそのまま）
                string bossKey = string.IsNullOrEmpty(Stage.bossBgm) ? "boss" : Stage.bossBgm;
                if (entry.enemy.boss && AudioManager.HasBGM(bossKey)) AudioManager.PlayBGM(bossKey);
            }
            WarnUpcoming();
            // エンドレスは終わらない（ライフがなくなるまで）
            State.CheckVictory(endless == null && nextSpawn >= schedule.Count, Enemies.Count);
        }

        if (State.Phase != lastPhase)
        {
            lastPhase = State.Phase;
            if (State.Phase != GamePhase.Playing) RecordMembers();
            if (State.Phase == GamePhase.Won)
            {
                AudioManager.PlaySE("victory");
                Progress.RecordClear(Stage.id, State.Life, State.MaxLife, Hard);
            }
            if (State.Phase == GamePhase.Lost)
            {
                AudioManager.PlaySE("defeat");
                // エンドレスは、守りきった波（いまの波のひとつ前）を記録する
                if (endless != null) EndlessNewBest = Progress.RecordEndless(Stage.id, EndlessSurvived);
            }
        }
    }

    /// <summary>出撃が終わったら、置いた部員の出撃回数と MVP を記録する（部員の札に出す）</summary>
    private void RecordMembers()
    {
        var ids = new List<string>();
        foreach (MemberStats s in Stats.Deployed()) ids.Add(s.Data.id);
        if (ids.Count == 0) return;
        MemberStats mvp = Stats.Mvp();
        Progress.RecordMembers(ids, mvp != null ? mvp.Data.id : null);
    }

    /// <summary>エンドレスで守りきった波の数（いまの波のひとつ前まで）</summary>
    public int EndlessSurvived { get { return Mathf.Max(0, EndlessWave - 1); } }

    /// <summary>エンドレスの波を、40 秒先まで作り足す（あとから足した敵も、倒す敵の数に入れる）</summary>
    private void FillEndless()
    {
        if (endless == null || State == null) return;
        int before = schedule.Count;
        endless.Fill(SpawnClock, 40f, (t, e, path, wave) =>
            schedule.Add(new SpawnEntry { time = t, order = schedule.Count, enemy = e, path = path, wave = wave }));
        int added = schedule.Count - before;
        if (added <= 0) return;
        State.AddEnemies(added);
        // まだ出ていない敵だけを時刻の順に並べ直す（作り足した波は今より先なので、出た敵・予告した敵の順は変わらない）
        int start = Mathf.Max(nextSpawn, nextWarn);
        schedule.Sort(start, schedule.Count - start, Comparer<SpawnEntry>.Create((a, b) =>
        {
            int c = a.time.CompareTo(b.time);
            return c != 0 ? c : a.order.CompareTo(b.order);
        }));
    }

    /// <summary>
    /// もうすぐ敵が出る出撃口で予告する。同じ出撃口から続けて出るあいだは最初の1回だけ（ボスは必ず）
    /// </summary>
    private void WarnUpcoming()
    {
        while (nextWarn < schedule.Count && schedule[nextWarn].time - SpawnClock <= WarnLead)
        {
            SpawnEntry entry = schedule[nextWarn++];
            Vector3 at = entry.path[0];
            float last;
            bool quiet = !lastWarn.TryGetValue(at, out last) || entry.time - last > 4f;
            lastWarn[at] = entry.time;
            if (quiet || entry.enemy.boss) Fx.SpawnWarning(at, entry.enemy.boss);
            if (entry.enemy.boss) AudioManager.PlaySE("boss_warning");
        }
    }

    // ================= 配置・撤退 =================

    public bool CanAfford(SquadMember member)
    {
        return State.DpInt >= member.Cost;
    }

    /// <summary>そのマスに置けるか（職業に合った地形で、空いている）</summary>
    public bool CanPlace(SquadMember member, Vector2Int cell)
    {
        Tile tile = Map.Get(cell);
        if (tile == null || tile.Occupant != null) return false;
        return member.Class.placement == Placement.Ground ? tile.Type == TileType.Ground : tile.Type == TileType.HighGround;
    }

    public IEnumerable<Vector2Int> PlaceableCells(SquadMember member)
    {
        foreach (Tile tile in Map.AllTiles())
            if (CanPlace(member, tile.Cell)) yield return tile.Cell;
    }

    public Character CharacterAt(Vector2Int cell)
    {
        Tile tile = Map.Get(cell);
        return tile != null ? tile.Occupant : null;
    }

    public Vector3 UnitPosition(Vector2Int cell)
    {
        Tile tile = Map.Get(cell);
        return Map.CellToWorld(cell) + new Vector3(0f, tile != null ? tile.Lift : 0f, 0f);
    }

    public Character Deploy(SquadMember member, Vector2Int cell)
    {
        if (State.Phase != GamePhase.Playing || !member.Ready || !CanAfford(member) || !CanPlace(member, cell)) return null;
        State.SpendDp(member.Cost);
        Vector3 pos = UnitPosition(cell);
        Character c = Character.Create(member, cell, pos, this, characterRoot);
        member.Deployed = c;
        Stats.Deploy(member);
        Map.Get(cell).Occupant = c;
        Characters.Add(c);
        Fx.Deploy(pos, c.ClassColor);
        AudioManager.PlaySE("deploy");
        return c;
    }

    /// <summary>撤退時に戻る DP</summary>
    public int RefundOf(SquadMember member)
    {
        return Mathf.FloorToInt(member.Cost * Rules.retreatRefundRate);
    }

    public void Retreat(Character c)
    {
        if (c == null || !c.Alive || c.Member == null) return; // 召喚ユニットは撤退できない
        State.AddDp(RefundOf(c.Member), Rules.dpMax);
        Fx.Retreat(c.Center);
        AudioManager.PlaySE("retreat");
        Remove(c);
    }

    /// <summary>部員が倒された（DP は戻らない）</summary>
    public void OnCharacterDown(Character c)
    {
        Fx.UnitDown(c.Center);
        AudioManager.PlaySE("unit_down");
        Remove(c);
    }

    // ================= 必殺技・召喚 =================

    public void OnSkillActivated(Character c)
    {
        Fx.SkillActivate(c.Center, c.ClassColor);
        AudioManager.PlaySE("skill");
        if (SkillActivated != null) SkillActivated(c);
    }

    // 周りの8マス（上・右・下・左を先に）
    private static readonly Vector2Int[] Neighbors =
    {
        new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0),
        new Vector2Int(1, -1), new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
    };

    /// <summary>summoner の周りの空いている地上・高台のマスに、unitId を count 体まで置く。置いた数を返す</summary>
    public int Summon(string unitId, Character summoner, int count, float inheritPercent, float lifetime)
    {
        CharacterData data = GameData.Summon(unitId);
        if (data == null || summoner == null) return 0;
        ClassData classData = GameData.Class(data.unitClass);
        int made = 0;
        foreach (Vector2Int d in Neighbors)
        {
            if (made >= count) break;
            Vector2Int cell = summoner.Cell + d;
            Tile tile = Map.Get(cell);
            if (tile == null || tile.Occupant != null) continue;
            if (tile.Type != TileType.Ground && tile.Type != TileType.HighGround) continue;
            Vector3 pos = UnitPosition(cell);
            Character c = Character.CreateSummon(data, classData, summoner, inheritPercent, lifetime, cell, pos, this, characterRoot);
            tile.Occupant = c;
            Characters.Add(c);
            Fx.Summon(pos);
            made++;
        }
        if (made > 0) AudioManager.PlaySE("summon");
        return made;
    }

    /// <summary>召喚ユニットの時間切れ</summary>
    public void Dismiss(Character c)
    {
        if (c == null || !c.Alive) return;
        Fx.Retreat(c.Center);
        Remove(c);
    }

    private void Remove(Character c)
    {
        c.ReleaseAll();
        Characters.Remove(c);
        Tile tile = Map.Get(c.Cell);
        if (tile != null && tile.Occupant == c) tile.Occupant = null;
        if (c.Skills != null) c.Skills.Cancel();
        if (c.Member != null)
        {
            c.Member.Deployed = null;
            c.Member.Cooldown = c.Data.redeployTime;
        }
        c.Despawn();
    }

    // ================= 敵 =================

    public void OnEnemyDefeated(Enemy enemy)
    {
        Enemies.Remove(enemy);
        if (enemy.BlockedBy != null) enemy.BlockedBy.Blocked.Remove(enemy);
        State.OnEnemyDefeated();
        Fx.EnemyDefeat(enemy.Center, Palette.Hex(enemy.Data.color, Palette.EnemyFallback));
        AudioManager.PlaySE("enemy_defeat");
        Destroy(enemy.gameObject);
    }

    /// <summary>敵が手下を呼ぶ。呼んだ敵の位置から、同じ経路の続きを進む（倒す敵の数も増える）</summary>
    public void SpawnSummoned(EnemyData data, Enemy summoner, int count, bool announce = true)
    {
        for (int i = 0; i < count; i++)
        {
            Enemy spawned = Enemy.SpawnOnPath(data, summoner.Path, summoner.NextPoint, summoner.transform.position, this, enemyRoot);
            // 呼んだ敵と同じ強さ（エンドレスで波が進んだぶん）
            if (summoner.HpScale != 1f || summoner.AtkScale != 1f) spawned.ApplyScale(summoner.HpScale, summoner.AtkScale);
            Enemies.Add(spawned);
            if (EnemySpawned != null) EnemySpawned(spawned);
        }
        State.AddEnemies(count);
        if (!announce) return;
        Fx.Summon(summoner.transform.position);
        AudioManager.PlaySE("summon");
    }

    public void OnEnemyReachedBase(Enemy enemy)
    {
        Enemies.Remove(enemy);
        State.OnEnemyLeaked(enemy.Data.lifeDamage);
        AudioManager.PlaySE("life_lost");

        Vector2Int cell = Map.WorldToCell(enemy.transform.position);
        Vector3 at = Map.CellToWorld(cell);
        FlashTile(cell);
        Fx.EnemyLeak(at);
        CameraFitter.Shake(0.14f, 0.35f);
        if (EnemyLeaked != null) EnemyLeaked(enemy);
        enemy.Despawn();
    }

    /// <summary>拠点が赤く光る</summary>
    private void FlashTile(Vector2Int cell)
    {
        Tile tile = Map.Get(cell);
        if (tile != null && tile.Type == TileType.Base && tile.Renderer != null) StartCoroutine(Flash(tile.Renderer));
    }

    private IEnumerator Flash(SpriteRenderer sr)
    {
        Color baseColor = Palette.Base;
        Color flash = new Color(1f, 0.25f, 0.2f);
        const float duration = 0.5f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (sr == null) yield break;
            sr.color = Color.Lerp(flash, baseColor, t / duration);
            yield return null;
        }
        if (sr != null) sr.color = baseColor;
    }
}
