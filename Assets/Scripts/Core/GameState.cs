using UnityEngine;

public enum GamePhase
{
    Playing,
    Won,
    Lost,
}

/// <summary>1回の出撃の進み具合（ライフ・DP・経過時間・敵の数・勝敗）</summary>
public class GameState
{
    public int Life { get; private set; }
    public readonly int MaxLife;

    /// <summary>配置コスト。少数ぶんは次の1点までの溜まり具合</summary>
    public float Dp { get; private set; }

    /// <summary>ステージ開始からの経過時間（秒）。一時停止中は進まず、2倍速なら2倍で進む</summary>
    public float ElapsedTime { get; private set; }

    public GamePhase Phase { get; private set; }

    public readonly int EnemiesTotal;
    public int EnemiesDefeated { get; private set; }
    public int EnemiesLeaked { get; private set; }

    /// <summary>倒したか拠点に着いた敵の数</summary>
    public int EnemiesFinished { get { return EnemiesDefeated + EnemiesLeaked; } }

    /// <summary>使える DP（整数）</summary>
    public int DpInt { get { return Mathf.FloorToInt(Dp + 0.0001f); } }

    public GameState(int life, int enemiesTotal, int initialDp)
    {
        MaxLife = Mathf.Max(1, life);
        Life = MaxLife;
        EnemiesTotal = enemiesTotal;
        Dp = initialDp;
        Phase = GamePhase.Playing;
    }

    public void Tick(float deltaTime)
    {
        if (Phase == GamePhase.Playing) ElapsedTime += deltaTime;
    }

    public void AddDp(float amount, int max)
    {
        Dp = Mathf.Min(max, Dp + amount);
    }

    public bool SpendDp(int amount)
    {
        if (DpInt < amount) return false;
        Dp -= amount;
        return true;
    }

    public void OnEnemyLeaked(int lifeDamage)
    {
        if (Phase != GamePhase.Playing) return;
        EnemiesLeaked++;
        Life = Mathf.Max(0, Life - lifeDamage);
        if (Life <= 0) Phase = GamePhase.Lost;
    }

    public void OnEnemyDefeated()
    {
        if (Phase != GamePhase.Playing) return;
        EnemiesDefeated++;
    }

    /// <summary>敵がすべて出終わって、場に残っていなければ勝ち</summary>
    public void CheckVictory(bool allSpawned, int enemiesOnField)
    {
        if (Phase == GamePhase.Playing && allSpawned && enemiesOnField == 0 && Life > 0) Phase = GamePhase.Won;
    }
}
