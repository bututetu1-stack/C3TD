/// <summary>出撃中の編成の1人。再配置までの待ち時間と、配置中のキャラを持つ</summary>
public class SquadMember
{
    public readonly CharacterData Data;
    public readonly ClassData Class;

    /// <summary>再配置できるようになるまでの残り秒</summary>
    public float Cooldown;

    /// <summary>配置中ならそのキャラ（いなければ null）</summary>
    public Character Deployed;

    /// <summary>狙う敵の選び方（部員の札で切り替える。撤退して置き直しても覚えている）</summary>
    public TargetMode Targeting = TargetMode.First;

    /// <summary>必殺技が溜まったら自動で使う（部員の札で切り替える。ふつうは手で使う）</summary>
    public bool AutoSkill;

    public SquadMember(CharacterData data, ClassData classData)
    {
        Data = data;
        Class = classData;
    }

    /// <summary>配置コスト（備品の「配置コスト -N」を引く。1 より下げない）</summary>
    public int Cost { get { return UnityEngine.Mathf.Max(1, Data.cost - Growth.CostCut(Data.id)); } }

    /// <summary>配置中でも待ち時間中でもない</summary>
    public bool Ready { get { return Deployed == null && Cooldown <= 0f; } }
}
