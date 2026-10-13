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
    public StageMap Map { get; private set; }
    public GameState State { get; private set; }
    public MapHighlight Highlight { get; private set; }
    public RulesData Rules { get; private set; }

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

    private class SpawnEntry
    {
        public float time;
        public int order;
        public EnemyData enemy;
        public List<Vector3> path;
    }

    private readonly List<SpawnEntry> schedule = new List<SpawnEntry>();
    private int nextSpawn;
    private int nextWarn;
    // 出撃口ごとの、最後に予告した（予告なしで続いた）敵の出る時刻
    private readonly Dictionary<Vector3, float> lastWarn = new Dictionary<Vector3, float>();

    /// <summary>敵が出てくる何秒前に出撃口で予告するか</summary>
    private const float WarnLead = 2f;
    private Transform enemyRoot;
    private Transform characterRoot;
    private GamePhase lastPhase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
    }

    /// <summary>ステージを始める。squadIds は編成バーに並べる部員（null なら squad.json）</summary>
    public static Battle Create(string stageId, List<string> squadIds)
    {
        StageData data = GameData.Stage(stageId);
        if (data == null) return null;
        var obj = new GameObject("Battle_" + stageId);
        var battle = obj.AddComponent<Battle>();
        battle.Init(data, squadIds ?? GameData.Squad.members);
        return battle;
    }

    private void Init(StageData data, List<string> squadIds)
    {
        Current = this;
        Stage = data;
        Rules = GameData.Rules;
        Map = StageMap.FromText(data.map, data.id);
        MapBuilder.Build(Map, transform);
        Highlight = new MapHighlight(Map, transform);

        var worldPaths = new Dictionary<string, List<Vector3>>();
        AddPaths(data, data.paths, false, worldPaths);
        AddPaths(data, data.airPaths, true, worldPaths);
        BuildSchedule(data, worldPaths);
        BuildSquad(squadIds);

        characterRoot = new GameObject("Characters").transform;
        characterRoot.SetParent(transform, false);
        enemyRoot = new GameObject("Enemies").transform;
        enemyRoot.SetParent(transform, false);

        State = new GameState(data.life, schedule.Count, data.initialDp);
        if (State.MaxLife >= schedule.Count && schedule.Count > 0)
            Debug.LogWarning(data.id + ": ライフ（" + State.MaxLife + "）が敵の数（" + schedule.Count + "）以上なので、まったく防衛しなくても勝てます");
        lastPhase = State.Phase;
        TimeController.ResetForStage(Rules.focusTimeScale);
        AudioManager.PlayBGM(data.bgm);
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
            State.AddDp(Rules.dpPerSecond * dt, Rules.dpMax);
            foreach (SquadMember m in Squad)
                if (m.Cooldown > 0f) m.Cooldown = Mathf.Max(0f, m.Cooldown - dt);

            while (nextSpawn < schedule.Count && schedule[nextSpawn].time <= State.ElapsedTime)
            {
                SpawnEntry entry = schedule[nextSpawn++];
                Enemies.Add(Enemy.Spawn(entry.enemy, entry.path, this, enemyRoot));
                // ボスが出たら BGM を boss に（ファイルがなければそのまま）
                if (entry.enemy.boss && AudioManager.HasBGM("boss")) AudioManager.PlayBGM("boss");
            }
            WarnUpcoming();
            State.CheckVictory(nextSpawn >= schedule.Count, Enemies.Count);
        }

        if (State.Phase != lastPhase)
        {
            lastPhase = State.Phase;
            if (State.Phase == GamePhase.Won)
            {
                AudioManager.PlaySE("victory");
                Progress.RecordClear(Stage.id, State.Life, State.MaxLife);
            }
            if (State.Phase == GamePhase.Lost) AudioManager.PlaySE("defeat");
        }
    }

    /// <summary>
    /// もうすぐ敵が出る出撃口で予告する。同じ出撃口から続けて出るあいだは最初の1回だけ（ボスは必ず）
    /// </summary>
    private void WarnUpcoming()
    {
        while (nextWarn < schedule.Count && schedule[nextWarn].time - State.ElapsedTime <= WarnLead)
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
    public void SpawnSummoned(EnemyData data, Enemy summoner, int count)
    {
        for (int i = 0; i < count; i++)
            Enemies.Add(Enemy.SpawnOnPath(data, summoner.Path, summoner.NextPoint, summoner.transform.position, this, enemyRoot));
        State.AddEnemies(count);
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
