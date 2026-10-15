using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 毎日のこと（ソシャゲのログインボーナスとデイリー任務のように。2026-10-15）。日付（端末の日付）が変わると数え直す。
///   ログインボーナス：その日はじめてタイトルを開くと部費 LoginCoins
///   今日の任務：作戦を 3 回クリア・★3 で 1 回クリア・必殺技を 5 回・ガチャを 1 回。達成したらタイトルの札で「受け取る」。全部受け取るとおまけ
/// 自動で戦わせているあいだ（Upgrades.Enabled が false）は数えない。
/// </summary>
public static class Daily
{
    public const int LoginCoins = 100;
    public const int AllBonus = 200;

    public class Mission
    {
        public string Id;
        public string Text;
        public int Goal;
        public int Reward;
    }

    public static readonly Mission[] Missions =
    {
        new Mission { Id = "clear", Text = "作戦を 3 回クリアする", Goal = 3, Reward = 150 },
        new Mission { Id = "star3", Text = "★3 で 1 回クリアする", Goal = 1, Reward = 100 },
        new Mission { Id = "skill", Text = "必殺技を 5 回使う", Goal = 5, Reward = 100 },
        new Mission { Id = "gacha", Text = "ガチャを 1 回引く（無料でもよい）", Goal = 1, Reward = 50 },
    };

    private static string Today { get { return System.DateTime.Now.ToString("yyyy-MM-dd"); } }

    /// <summary>日付が変わっていたら数え直す</summary>
    private static void Roll()
    {
        if (Progress.DailyDate == Today) return;
        Progress.ResetDaily(Today);
    }

    /// <summary>何かをした（clear・star3・skill・gacha）。自動で戦わせているあいだは数えない</summary>
    public static void Count(string id, int amount = 1)
    {
        if (!Upgrades.Enabled) return;
        Roll();
        Progress.AddDailyCount(id, amount);
    }

    public static int CountOf(string id)
    {
        Roll();
        return Progress.DailyCount(id);
    }

    public static bool Done(Mission m) { return CountOf(m.Id) >= m.Goal; }
    public static bool Claimed(Mission m) { Roll(); return Progress.DailyClaimed(m.Id); }

    /// <summary>受け取る（達成していて、まだなら部費を渡す）。渡した部費を返す</summary>
    public static int Claim(Mission m)
    {
        if (!Done(m) || Claimed(m)) return 0;
        Progress.SetDailyClaimed(m.Id);
        Progress.AddCoins(m.Reward);
        return m.Reward;
    }

    public static bool AllClaimed()
    {
        foreach (Mission m in Missions) if (!Claimed(m)) return false;
        return true;
    }

    /// <summary>全部受け取ったときのおまけ（まだならもらえる）</summary>
    public static int ClaimAllBonus()
    {
        if (!AllClaimed() || Progress.DailyClaimed("all")) return 0;
        Progress.SetDailyClaimed("all");
        Progress.AddCoins(AllBonus);
        return AllBonus;
    }

    public static bool AllBonusClaimed() { Roll(); return Progress.DailyClaimed("all"); }

    /// <summary>受け取れる任務があるか（タイトルの札に印）</summary>
    public static bool AnyClaimable()
    {
        foreach (Mission m in Missions) if (Done(m) && !Claimed(m)) return true;
        return AllClaimed() && !AllBonusClaimed();
    }

    /// <summary>その日はじめてならログインボーナスを渡す。渡した部費を返す（もう渡していれば 0）</summary>
    public static int Login()
    {
        Roll();
        if (Progress.LoginDate == Today) return 0;
        Progress.LoginDate = Today;
        Progress.AddCoins(LoginCoins);
        return LoginCoins;
    }
}
