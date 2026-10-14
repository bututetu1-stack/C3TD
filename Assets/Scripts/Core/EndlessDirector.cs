using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// エンドレス（ステージの endless）の敵の波を、出す少し前に作り足していく。
/// 波の番号が進むほど、出てくる敵の種類が増え、体力と攻撃力が上がり、数も増える。bossEvery 波ごとにボス。
/// </summary>
public class EndlessDirector
{
    private readonly EndlessData cfg;
    private readonly List<List<Vector3>> ground;
    private readonly List<List<Vector3>> air;
    private readonly List<float> waveStarts = new List<float>();
    private float nextStart;

    /// <summary>作った波の数</summary>
    public int Built { get { return waveStarts.Count; } }

    public EndlessDirector(EndlessData cfg, List<List<Vector3>> ground, List<List<Vector3>> air)
    {
        this.cfg = cfg;
        this.ground = ground;
        this.air = air;
        nextStart = cfg.firstDelay;
    }

    /// <summary>clock の時点で始まっている波の番号（1 から。まだなら 0）</summary>
    public int WaveAt(float clock)
    {
        int n = 0;
        while (n < waveStarts.Count && waveStarts[n] <= clock) n++;
        return n;
    }

    /// <summary>敵1体の体力の倍率（波 wave のとき）</summary>
    public float HpScale(int wave)
    {
        return 1f + cfg.hpGrowth * Mathf.Max(0, wave - 1);
    }

    public float AtkScale(int wave)
    {
        return 1f + cfg.atkGrowth * Mathf.Max(0, wave - 1);
    }

    /// <summary>次の波が clock から lookAhead 秒以内に始まるなら、作って add に渡す（何回でも呼んでよい）</summary>
    public void Fill(float clock, float lookAhead, System.Action<float, EnemyData, List<Vector3>, int> add)
    {
        int guard = 0;
        while (nextStart - clock <= lookAhead && guard++ < 4)
        {
            int wave = waveStarts.Count + 1;
            waveStarts.Add(nextStart);
            BuildWave(wave, nextStart, add);
            nextStart += Mathf.Max(cfg.minInterval, cfg.interval - cfg.intervalShrink * (wave - 1));
        }
    }

    private void BuildWave(int wave, float start, System.Action<float, EnemyData, List<Vector3>, int> add)
    {
        float t = start;
        // ボスの波：ボスと、少しの手下
        if (cfg.bossEvery > 0 && wave % cfg.bossEvery == 0 && cfg.bosses.Count > 0)
        {
            EnemyData boss = GameData.Enemy(cfg.bosses[(wave / cfg.bossEvery - 1) % cfg.bosses.Count]);
            List<Vector3> path = PathFor(boss);
            if (boss != null && path != null)
            {
                add(t + 4f, boss, path, wave);
                t += 1.5f;
            }
        }

        int groups = Mathf.Clamp(1 + (wave - 1) / 3, 1, cfg.maxGroups);
        float points = cfg.basePoints + cfg.pointsPerWave * (wave - 1);
        for (int g = 0; g < groups; g++)
        {
            EnemyData enemy = PickEnemy(wave);
            if (enemy == null) continue;
            List<Vector3> path = PathFor(enemy);
            if (path == null) continue;
            // 体力の低い敵ほど多く（課題 1650 なら points とほぼ同じ数）
            int count = Mathf.Clamp(Mathf.RoundToInt(points / groups * 1650f / Mathf.Max(300f, enemy.hp)), 1, 8);
            float gap = enemy.speed >= 1.5f ? 1.1f : enemy.speed <= 0.6f ? 3.5f : 2f;
            for (int i = 0; i < count; i++) add(t + i * gap, enemy, path, wave);
            t += 2.5f + Random.Range(0f, 2f);
        }
    }

    /// <summary>開いている敵の中から1つ（いちばん新しく開いた組を半分の確率で選ぶ）</summary>
    private EnemyData PickEnemy(int wave)
    {
        var open = new List<EndlessPool>();
        foreach (EndlessPool p in cfg.pools)
            if (p.from <= wave && p.enemies != null && p.enemies.Count > 0) open.Add(p);
        if (open.Count == 0) return null;
        EndlessPool pool = open.Count > 1 && Random.value < 0.5f ? open[open.Count - 1] : open[Random.Range(0, open.Count)];
        for (int tries = 0; tries < 6; tries++)
        {
            EnemyData e = GameData.Enemy(pool.enemies[Random.Range(0, pool.enemies.Count)]);
            if (e != null && !e.boss && PathFor(e) != null) return e;
        }
        return null;
    }

    private List<Vector3> PathFor(EnemyData e)
    {
        if (e == null) return null;
        List<List<Vector3>> list = e.flying ? air : ground;
        return list.Count > 0 ? list[Random.Range(0, list.Count)] : null;
    }
}
