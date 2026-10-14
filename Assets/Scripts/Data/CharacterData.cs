using System.Collections.Generic;

/// <summary>職業</summary>
public enum UnitClass
{
    Guard,    // 前衛
    Defender, // 重装
    Sniper,   // 狙撃
    Caster,   // 術師
    Medic,    // 医療
}

/// <summary>特性の発動条件</summary>
public enum SkillTrigger
{
    Always,
    ElapsedTime,
    OnDeploy,
    OnAttack,
    OnSkillEnd,
    /// <summary>敵を止めている（攻撃を耐えている）時間が params.interval 秒たまるごと（僕）</summary>
    Blocking,
}

/// <summary>SP の溜まり方</summary>
public enum SpCharge
{
    PerSecond,
    OnAttack,
    OnHit,
}

/// <summary>スキルの効果の部品</summary>
public enum EffectType
{
    Damage,
    Heal,
    StatModifier,
    Stun,
    Summon,
    MultiHit,
    Dp,
    Random,
    TeamBuff,
    Confuse,
}

/// <summary>スキルの効果の対象</summary>
public enum EffectTarget
{
    Self,
    Target,
    AreaAroundSelf,
    AlliesInRange,
    EnemiesInRange,
}

/// <summary>部員の定義（Resources/Data/Characters/{id}.json）</summary>
public class CharacterData
{
    public string id;
    public string name;
    [JsonKey("class")] public UnitClass unitClass;
    public int cost;
    public float redeployTime;

    /// <summary>アイコンがないときの丸の色（省略時は職業の色）</summary>
    public string color;

    /// <summary>飛ばす弾の画像名（省略時は職業の決まり）</summary>
    public string projectile;

    /// <summary>範囲攻撃の半径（マス）。0 なら単体。省略時（-1）は職業の決まり</summary>
    public float splash = -1f;

    /// <summary>近接（前衛・重装）でも飛ぶ敵を狙える（物理鉄人）</summary>
    public bool antiAir;

    public CharacterStats stats = new CharacterStats();
    public SkillData trait;
    public SkillData ultimate;

    /// <summary>アイコンがないときに丸に入れる文字</summary>
    public string Initial
    {
        get { return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1); }
    }
}

public class CharacterStats
{
    public int hp;
    public int atk;
    public int def;
    public float attackInterval;

    /// <summary>ブロック数。-1 なら職業の標準（地上に置いたときだけ効く）</summary>
    public int block = -1;

    /// <summary>射程（マス）。マスの中心どうしの距離がこれ以下のマスに届く</summary>
    public float range;
}

/// <summary>特性・必殺技（M3 で動かす。M2 では読み込みと表示だけ）</summary>
public class SkillData
{
    public string name;
    public string description;
    public SkillTrigger trigger;
    [JsonKey("params")] public Dictionary<string, object> parameters = new Dictionary<string, object>();
    public float spCost;
    public float initialSp;
    public SpCharge spCharge;
    public float duration;
    public List<EffectData> effects = new List<EffectData>();
    public List<EffectData> onEnd = new List<EffectData>();

    /// <summary>必殺技を発動したときのセリフ（カットインに出す。セリフはこれだけ：2026-10-14 オーナー）</summary>
    public string line;

    /// <summary>params の数値を読む（なければ fallback）</summary>
    public float Param(string key, float fallback)
    {
        object v;
        if (parameters == null || !parameters.TryGetValue(key, out v)) return fallback;
        return v is double ? (float)(double)v : fallback;
    }
}

/// <summary>効果の部品1つ。type によって使う項目が違う</summary>
public class EffectData
{
    public EffectType type;
    public EffectTarget target;
    public string stat;
    public float value;
    public float percent;
    public float percentPerStack;
    public int maxStacks;
    public float duration;
    public string unitId;
    public int count;
    public float inheritStatsPercent;
    public float multiplier;
    public int hits;

    /// <summary>AreaAroundSelf の半径（マス）。0 なら 1.5</summary>
    public float radius;

    /// <summary>Random のとき、この中から1つをランダムに使う</summary>
    public List<EffectData> options;

    /// <summary>Random で選ばれたときに出す言葉（「ストレート：一斉攻撃」など）</summary>
    public string label;
}
