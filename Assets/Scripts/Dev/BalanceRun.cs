using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 開発用：ステージを AutoPilot で何回も戦わせて、勝った回数と残ったライフをまとめる（バランスの確かめ用）。
/// Novice なら AutoPilot の「初めて遊ぶ人のまね」で、Seed を 0 から Runs-1 まで変えて戦う。
/// 負けた回は、拠点に着いた敵（何秒に・何が）も書く。MaxTime 秒を過ぎても終わらない回は「時間切れ」にする。
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

    /// <summary>ステージの経過時間がこれを超えたら「時間切れ」で次へ（置き場所が悪くて敵がなかなか減らない回）</summary>
    public float MaxTime = 300f;

    public bool Done { get; private set; }
    public string Report { get; private set; }

    IEnumerator Start()
    {
        var boot = GetComponent<GameBootstrap>();
        var sb = new StringBuilder();
        foreach (string id in Stages)
        {
            int wins = 0, perfect = 0, timeouts = 0;
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
                while (battle != null && battle.State.Phase == GamePhase.Playing && battle.State.ElapsedTime < MaxTime) yield return null;
                if (battle == null) break;
                GameState s = battle.State;
                if (s.Phase == GamePhase.Won)
                {
                    wins++;
                    if (s.Life == s.MaxLife) perfect++;
                }
                else if (s.Phase == GamePhase.Playing)
                {
                    timeouts++;
                    // 残った敵（何が・どこで・体力・止めている部員）を書く。敵が動かなくなる不具合と見分けるため
                    var rest = new List<string>();
                    foreach (Enemy e in battle.Enemies)
                    {
                        if (e == null) continue;
                        Vector3 p = e.transform.position;
                        rest.Add(e.Data.id + " (" + p.x.ToString("0.0") + "," + p.y.ToString("0.0") + ") HP " + Mathf.RoundToInt(e.Hp) + "/" + Mathf.RoundToInt(e.MaxHp)
                            + " 残り " + e.RemainingDistance.ToString("0.0") + (e.BlockedBy != null ? " 止め:" + e.BlockedBy.Data.id : "") + (e.Stunned ? " 気絶" : ""));
                    }
                    losses.Append("  seed " + seed + "：時間切れ（残りの敵 " + battle.Enemies.Count + "：" + string.Join("／", rest) + "）\n");
                }
                else
                {
                    losses.Append("  seed " + seed + "：" + string.Join("、", leaks) + "\n");
                }
                lives.Add(s.Life);
            }
            sb.Append(id + ": 勝ち " + wins + "/" + Runs + "（★3 " + perfect + (timeouts > 0 ? "、時間切れ " + timeouts : "") + "） 残りライフ " + string.Join(",", lives) + "\n");
            sb.Append(losses);
        }
        Time.timeScale = 1f;
        Report = sb.ToString();
        Debug.Log("BalanceRun\n" + Report);
        Done = true;
    }
}
