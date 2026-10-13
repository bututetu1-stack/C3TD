using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 開発用：ステージを AutoPilot で何回も戦わせて、勝った回数と残ったライフをまとめる（バランスの確かめ用）。
/// Novice なら AutoPilot の「初めて遊ぶ人のまね」で、Seed を 0 から Runs-1 まで変えて戦う。
/// 負けた回は、拠点に着いた敵（何秒に・何が）も書く。
/// エディタから GameBootstrap の GameObject に AddComponent して使い、Done になったら Report を読む。
/// </summary>
public class BalanceRun : MonoBehaviour
{
    public List<string> Stages = new List<string>();
    public int Runs = 10;
    public bool Novice = true;
    public float NoviceCut = 0.6f;
    public bool UseSkills = true;
    public float TimeScale = 6f;

    public bool Done { get; private set; }
    public string Report { get; private set; }

    IEnumerator Start()
    {
        var boot = GetComponent<GameBootstrap>();
        var sb = new StringBuilder();
        foreach (string id in Stages)
        {
            int wins = 0, perfect = 0;
            var lives = new List<int>();
            var losses = new StringBuilder();
            for (int seed = 0; seed < Runs; seed++)
            {
                boot.StartBattle(id);
                Battle battle = Battle.Current;
                if (battle == null) break;
                var leaks = new List<string>();
                battle.EnemyLeaked += e => leaks.Add(Mathf.RoundToInt(battle.State.ElapsedTime) + "s " + e.Data.id);
                var pilot = battle.gameObject.AddComponent<AutoPilot>();
                pilot.Novice = Novice;
                pilot.Seed = seed;
                pilot.NoviceCut = NoviceCut;
                pilot.UseSkills = UseSkills;
                pilot.TimeScale = TimeScale;
                while (battle != null && battle.State.Phase == GamePhase.Playing) yield return null;
                if (battle == null) break;
                GameState s = battle.State;
                if (s.Phase == GamePhase.Won)
                {
                    wins++;
                    if (s.Life == s.MaxLife) perfect++;
                }
                else
                {
                    losses.Append("  seed " + seed + "：" + string.Join("、", leaks) + "\n");
                }
                lives.Add(s.Life);
            }
            sb.Append(id + ": 勝ち " + wins + "/" + Runs + "（★3 " + perfect + "） 残りライフ " + string.Join(",", lives) + "\n");
            sb.Append(losses);
        }
        Time.timeScale = 1f;
        Report = sb.ToString();
        Debug.Log("BalanceRun\n" + Report);
        Done = true;
    }
}
