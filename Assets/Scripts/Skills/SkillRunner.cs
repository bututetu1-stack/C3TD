using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 部員1人の特性と必殺技の状態（SP・発動中の残り時間・特性の重ね数）。Character が持つ。
///
/// 特性の発動条件：
///   Always      置いたときから効果がずっと続く
///   ElapsedTime ステージの経過時間 params.interval 秒ごとに重なる（StatModifier の percentPerStack × 重ね数、maxStacks まで）。
///               StatModifier 以外の効果は、置いてから interval 秒ごとに1回使う
///   OnDeploy    置いたときに1回
///   OnAttack    攻撃するたび（Target は攻撃した敵）
///   OnSkillEnd  必殺技が終わったとき
/// 必殺技の SP：PerSecond（1秒に params.spPerSecond、省略時 1）/ OnAttack（攻撃で1）/ OnHit（攻撃を受けて1）
/// </summary>
public class SkillRunner
{
    private readonly Character owner;
    private readonly Battle battle;
    public readonly SkillData Trait;
    public readonly SkillData Ultimate;

    public float Sp { get; private set; }
    public bool Active { get; private set; }
    public float ActiveRemaining { get; private set; }

    /// <summary>ElapsedTime の特性の重ね数</summary>
    public int TraitStacks { get; private set; }

    private readonly List<KeyValuePair<Unit, StatModifier>> ultimateModifiers = new List<KeyValuePair<Unit, StatModifier>>();
    private readonly List<EffectData> stackEffects = new List<EffectData>();
    private readonly List<StatModifier> stackModifiers = new List<StatModifier>();
    private int firedIntervals;
    private bool wasReady;

    public SkillRunner(Character owner, Battle battle, SkillData trait, SkillData ultimate)
    {
        this.owner = owner;
        this.battle = battle;
        Trait = trait != null && !string.IsNullOrEmpty(trait.name) ? trait : null;
        Ultimate = ultimate != null && !string.IsNullOrEmpty(ultimate.name) && ultimate.spCost > 0f ? ultimate : null;
        if (Ultimate != null) Sp = Mathf.Clamp(Ultimate.initialSp, 0f, Ultimate.spCost);
    }

    public bool HasUltimate { get { return Ultimate != null; } }
    public float SpCost { get { return Ultimate != null ? Ultimate.spCost : 0f; } }

    /// <summary>必殺技を使える（SP が溜まっていて、発動中でもスタン中でもない）</summary>
    public bool Ready
    {
        get { return Ultimate != null && !Active && Sp >= Ultimate.spCost && !owner.Stunned && battle.State.Phase == GamePhase.Playing; }
    }

    // ================= 特性 =================

    public void OnDeploy()
    {
        if (Trait == null) return;
        switch (Trait.trigger)
        {
            case SkillTrigger.Always:
            case SkillTrigger.OnDeploy:
                foreach (EffectData e in Trait.effects) SkillEffects.Apply(e, Context(0f, null, null));
                break;

            case SkillTrigger.ElapsedTime:
                foreach (EffectData e in Trait.effects)
                {
                    if (e.type != EffectType.StatModifier || string.IsNullOrEmpty(e.stat)) continue;
                    stackEffects.Add(e);
                    stackModifiers.Add(owner.AddModifier(e.stat, 0f, 0f, 0f));
                }
                firedIntervals = IntervalCount();
                UpdateStacks(false);
                break;
        }
    }

    public void Tick(float dt)
    {
        if (Trait != null && Trait.trigger == SkillTrigger.ElapsedTime)
        {
            UpdateStacks(true);
            int count = IntervalCount();
            while (firedIntervals < count)
            {
                firedIntervals++;
                foreach (EffectData e in Trait.effects)
                    if (!stackEffects.Contains(e)) SkillEffects.Apply(e, Context(0f, null, null));
            }
        }

        if (Ultimate == null) return;
        if (Active)
        {
            ActiveRemaining -= dt;
            if (ActiveRemaining <= 0f) End();
        }
        else if (Ultimate.spCharge == SpCharge.PerSecond && !owner.Stunned)
        {
            AddSp(dt * Ultimate.Param("spPerSecond", 1f));
        }

        bool ready = Ready;
        if (ready && !wasReady)
        {
            AudioManager.PlaySE("skill_ready");
            Fx.SkillReady(owner.Center);
        }
        wasReady = ready;
    }

    public void OnAttack(Unit target)
    {
        if (Ultimate != null && !Active && Ultimate.spCharge == SpCharge.OnAttack) AddSp(1f);
        if (Trait != null && Trait.trigger == SkillTrigger.OnAttack)
            foreach (EffectData e in Trait.effects) SkillEffects.Apply(e, Context(0f, target, null));
    }

    public void OnHit()
    {
        if (Ultimate != null && !Active && Ultimate.spCharge == SpCharge.OnHit) AddSp(1f);
    }

    private int IntervalCount()
    {
        float interval = Mathf.Max(0.1f, Trait.Param("interval", 30f));
        return Mathf.FloorToInt(battle.State.ElapsedTime / interval);
    }

    /// <summary>重ね数はステージの経過時間で決まる（「時間が経つほど」強くなる）</summary>
    private void UpdateStacks(bool announce)
    {
        if (stackEffects.Count == 0) return;
        int count = IntervalCount();
        int shown = 0;
        for (int i = 0; i < stackEffects.Count; i++)
        {
            EffectData e = stackEffects[i];
            int stacks = e.maxStacks > 0 ? Mathf.Min(count, e.maxStacks) : count;
            stackModifiers[i].Percent = e.percent + e.percentPerStack * stacks;
            stackModifiers[i].Value = e.value;
            shown = Mathf.Max(shown, stacks);
        }
        if (shown == TraitStacks) return;
        bool up = shown > TraitStacks;
        TraitStacks = shown;
        if (announce && up)
        {
            Fx.Buff(owner.Center);
            FloatingText.Status(owner.Center, Trait.name + " ×" + shown);
            AudioManager.PlaySE("buff");
        }
    }

    /// <summary>特性の状態の短い説明（情報の札に出す）。なければ空</summary>
    public string TraitStatus
    {
        get
        {
            if (stackEffects.Count == 0) return "";
            var parts = new List<string>();
            for (int i = 0; i < stackEffects.Count; i++)
            {
                StatModifier m = stackModifiers[i];
                if (Mathf.Abs(m.Percent) > 0.01f) parts.Add(StatName(m.Stat) + (m.Percent >= 0f ? " +" : " ") + m.Percent.ToString("0") + "%");
            }
            return "×" + TraitStacks + (parts.Count > 0 ? "（" + string.Join("・", parts) + "）" : "");
        }
    }

    // ================= 必殺技 =================

    public bool Activate()
    {
        if (!Ready) return false;
        Sp = 0f;
        wasReady = false;
        ultimateModifiers.Clear();
        var ctx = Context(Ultimate.duration, null, ultimateModifiers);
        foreach (EffectData e in Ultimate.effects) SkillEffects.Apply(e, ctx);
        battle.OnSkillActivated(owner);
        if (Ultimate.duration > 0f)
        {
            Active = true;
            ActiveRemaining = Ultimate.duration;
        }
        else
        {
            End();
        }
        return true;
    }

    private void End()
    {
        Active = false;
        ActiveRemaining = 0f;
        foreach (var kv in ultimateModifiers)
            if (kv.Key != null) kv.Key.RemoveModifier(kv.Value);
        ultimateModifiers.Clear();

        foreach (EffectData e in Ultimate.onEnd) SkillEffects.Apply(e, Context(0f, null, null));
        if (Trait != null && Trait.trigger == SkillTrigger.OnSkillEnd)
            foreach (EffectData e in Trait.effects) SkillEffects.Apply(e, Context(0f, null, null));
    }

    /// <summary>撤退・ダウンで場を離れる：必殺技でほかの味方に掛けた増減を外す（終わりの効果は使わない）</summary>
    public void Cancel()
    {
        foreach (var kv in ultimateModifiers)
            if (kv.Key != null) kv.Key.RemoveModifier(kv.Value);
        ultimateModifiers.Clear();
        Active = false;
    }

    private void AddSp(float amount)
    {
        Sp = Mathf.Min(Ultimate.spCost, Sp + amount);
    }

    private SkillContext Context(float defaultDuration, Unit target, List<KeyValuePair<Unit, StatModifier>> collected)
    {
        return new SkillContext { Caster = owner, Battle = battle, Target = target, DefaultDuration = defaultDuration, Collected = collected };
    }

    public static string StatName(string stat)
    {
        switch (stat)
        {
            case "atk": return "ATK";
            case "def": return "DEF";
            case "attackInterval": return "攻撃間隔";
            case "range": return "射程";
            case "speed": return "移動速度";
            default: return stat;
        }
    }
}
