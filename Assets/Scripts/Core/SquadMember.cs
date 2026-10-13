/// <summary>出撃中の編成の1人。再配置までの待ち時間と、配置中のキャラを持つ</summary>
public class SquadMember
{
    public readonly CharacterData Data;
    public readonly ClassData Class;

    /// <summary>再配置できるようになるまでの残り秒</summary>
    public float Cooldown;

    /// <summary>配置中ならそのキャラ（いなければ null）</summary>
    public Character Deployed;

    public SquadMember(CharacterData data, ClassData classData)
    {
        Data = data;
        Class = classData;
    }

    public int Cost { get { return Data.cost; } }

    /// <summary>配置中でも待ち時間中でもない</summary>
    public bool Ready { get { return Deployed == null && Cooldown <= 0f; } }
}
