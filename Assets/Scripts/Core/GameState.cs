using UnityEngine;

public enum GamePhase
{
    Playing,
    Won,
    Lost,
}

/// <summary>1回の出撃の進み具合（ライフ・経過時間・敵の数・勝敗）</summary>
public class GameState
{
    public int Life { get; private set; }
    public readonly int MaxLife;

    /// <summary>ステージ開始からの経過時間（秒）。一時停止中は進まず、2倍速なら2倍で進む</summary>
    public float ElapsedTime { get; private set; }

    public GamePhase Phase { get; private set; }

    public readonly int EnemiesTotal;
    public int EnemiesDefeated { get; private set; }
    public int EnemiesLeaked { get; private set; }

    /// <summary>倒したか拠点に着いた敵の数</summary>
    public int EnemiesFinished { get { return EnemiesDefeated + EnemiesLeaked; } }

    public GameState(int life, int enemiesTotal)
    {
        MaxLife = Mathf.Max(1, life);
        Life = MaxLife;
        EnemiesTotal = enemiesTotal;
        Phase = GamePhase.Playing;
    }

    public void Tick(float deltaTime)
    {
        if (Phase == GamePhase.Playing) ElapsedTime += deltaTime;
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
