using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1つのステージの出撃。マップを作り、ウェーブの予定どおりに敵を出し、勝敗を決める。
/// ステージ内のものはすべてこの GameObject の子に作るので、壊せばきれいに片付く。
/// </summary>
public class Battle : MonoBehaviour
{
    public static Battle Current { get; private set; }

    public StageData Stage { get; private set; }
    public StageMap Map { get; private set; }
    public GameState State { get; private set; }

    /// <summary>場に出ている敵</summary>
    public readonly List<Enemy> Enemies = new List<Enemy>();

    private class SpawnEntry
    {
        public float time;
        public int order;
        public EnemyData enemy;
        public List<Vector3> path;
    }

    private readonly List<SpawnEntry> schedule = new List<SpawnEntry>();
    private int nextSpawn;
    private Transform enemyRoot;
    private GamePhase lastPhase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
    }

    public static Battle Create(string stageId)
    {
        StageData data = GameData.Stage(stageId);
        if (data == null) return null;
        var obj = new GameObject("Battle_" + stageId);
        var battle = obj.AddComponent<Battle>();
        battle.Init(data);
        return battle;
    }

    private void Init(StageData data)
    {
        Current = this;
        Stage = data;
        Map = StageMap.FromText(data.map, data.id);
        MapBuilder.Build(Map, transform);

        var worldPaths = new Dictionary<string, List<Vector3>>();
        foreach (var kv in data.paths)
        {
            if (!Map.ValidatePath(data.id, kv.Key, kv.Value)) continue;
            var points = new List<Vector3>();
            foreach (var cell in kv.Value) points.Add(Map.CellToWorld(cell));
            worldPaths[kv.Key] = points;
            MapBuilder.DrawPath(points, transform);
        }
        BuildSchedule(data, worldPaths);

        enemyRoot = new GameObject("Enemies").transform;
        enemyRoot.SetParent(transform, false);

        State = new GameState(data.life, schedule.Count);
        lastPhase = State.Phase;
        TimeController.ResetForStage();
        AudioManager.PlayBGM(data.bgm);
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

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Update()
    {
        if (State.Phase == GamePhase.Playing)
        {
            State.Tick(Time.deltaTime);
            while (nextSpawn < schedule.Count && schedule[nextSpawn].time <= State.ElapsedTime)
            {
                SpawnEntry entry = schedule[nextSpawn++];
                Enemies.Add(Enemy.Spawn(entry.enemy, entry.path, this, enemyRoot));
            }
            State.CheckVictory(nextSpawn >= schedule.Count, Enemies.Count);
        }

        if (State.Phase != lastPhase)
        {
            lastPhase = State.Phase;
            if (State.Phase == GamePhase.Won) AudioManager.PlaySE("victory");
            if (State.Phase == GamePhase.Lost) AudioManager.PlaySE("defeat");
        }
    }

    public void OnEnemyReachedBase(Enemy enemy)
    {
        Enemies.Remove(enemy);
        State.OnEnemyLeaked(enemy.Data.lifeDamage);
        AudioManager.PlaySE("life_lost");
        FlashTile(Map.WorldToCell(enemy.transform.position));
        Destroy(enemy.gameObject);
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
