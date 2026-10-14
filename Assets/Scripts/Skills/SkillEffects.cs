using System.Collections.Generic;
using UnityEngine;

/// <summary>効果を使うときの状況</summary>
public class SkillContext
{
    public Character Caster;
    public Battle Battle;

    /// <summary>攻撃した相手など（なければ null）</summary>
    public Unit Target;

    /// <summary>効果に duration がないときの長さ（必殺技なら効果時間、特性なら 0 = ずっと）</summary>
    public float DefaultDuration;

    /// <summary>必殺技の終わりに外す増減をここに集める（null なら集めない）</summary>
    public List<KeyValuePair<Unit, StatModifier>> Collected;
}

/// <summary>
/// 効果の部品を使う。新しい技は JSON の組み合わせで作る。
///   Damage       攻撃力 × multiplier（省略時 1）のダメージ
///   Heal         value（量）・multiplier（攻撃力の倍率）・percent（相手の最大 HP の%）を書いたぶん足す。どれもなければ攻撃力ぶん
///   StatModifier stat（atk / def / attackInterval / range / speed / hits）を percent（%）と value で増減
///   Stun         duration 秒動けなくする
///   Summon       周りの空きマスに unitId を count 体置く（inheritStatsPercent で召喚者の能力を引き継ぐ）
///   MultiHit     攻撃が hits 回当たる
///   Dp           DP を value 増やす（上限は rules.json の dpMax）
///   Random       options の中から1つをランダムに使い、その label を出す（キャロのカードなど）
///   TeamBuff     味方全員の攻撃力（stat は atk だけ）を percent% 上げる。percent × maxStacks まで。ステージが終わるまで残る（僕）
///   Confuse      敵を duration 秒混乱させる（止められていなければ来た道を戻り、攻撃しない）
///   DamageControl 次に倒れるとき一度だけ、体力 percent%（省略時 50）で踏みとどまる（物理鉄人のダメコン。重ねない）
///   Remodel      必殺技を count 回使ったときに一度だけ、effects をずっと続く効果として使い、名前の後ろに label を付ける（物理鉄人の改二）
/// 対象：Self / Target / AreaAroundSelf（半径 radius、省略時 1.5 マス）/ AlliesInRange / EnemiesInRange
/// </summary>
public static class SkillEffects
{
    private const float DefaultRadius = 1.5f;

    public static void Apply(EffectData e, SkillContext ctx)
    {
        if (e == null || ctx.Caster == null || ctx.Battle == null) return;
        Character caster = ctx.Caster;
        float duration = e.duration > 0f ? e.duration : ctx.DefaultDuration;

        switch (e.type)
        {
            case EffectType.Damage:
                foreach (Unit u in Targets(e, ctx, true))
                {
                    Fx.Hit(u.Center, caster.ClassColor);
                    u.TakeDamage(caster.Atk * Multiplier(e), caster.Class.damageType);
                }
                break;

            case EffectType.Heal:
                foreach (Unit u in Targets(e, ctx, false))
                {
                    // value・multiplier・percent を書いたぶんだけ足す。どれもなければ攻撃力ぶん
                    float amount = 0f;
                    if (e.value > 0f) amount += e.value;
                    if (e.multiplier > 0f) amount += caster.Atk * e.multiplier;
                    if (e.percent > 0f) amount += u.MaxHp * e.percent / 100f;
                    if (amount <= 0f) amount = caster.Atk;
                    u.Heal(amount);
                    Fx.Heal(u.Center);
                }
                break;

            case EffectType.StatModifier:
                if (string.IsNullOrEmpty(e.stat)) break;
                bool debuff = e.percent + e.value < 0f;
                foreach (Unit u in Targets(e, ctx, debuff))
                {
                    StatModifier m = u.AddModifier(e.stat, e.percent, e.value, duration);
                    if (ctx.Collected != null && e.duration <= 0f) ctx.Collected.Add(new KeyValuePair<Unit, StatModifier>(u, m));
                }
                break;

            case EffectType.Stun:
                foreach (Unit u in Targets(e, ctx, true)) u.Stun(e.duration > 0f ? e.duration : 1f);
                break;

            case EffectType.Summon:
                ctx.Battle.Summon(e.unitId, caster, Mathf.Max(1, e.count), e.inheritStatsPercent, duration);
                break;

            case EffectType.MultiHit:
            {
                StatModifier m = caster.AddModifier("hits", 0f, Mathf.Max(0, e.hits - 1), duration);
                if (ctx.Collected != null && e.duration <= 0f) ctx.Collected.Add(new KeyValuePair<Unit, StatModifier>(caster, m));
                break;
            }

            case EffectType.Random:
            {
                if (e.options == null || e.options.Count == 0) break;
                EffectData pick = e.options[Random.Range(0, e.options.Count)];
                if (!string.IsNullOrEmpty(pick.label)) FloatingText.Status(caster.Center + new Vector3(0f, 0.45f, 0f), pick.label);
                Apply(pick, ctx);
                break;
            }

            case EffectType.TeamBuff:
            {
                if (e.stat != "atk" || e.percent <= 0f) break;
                float max = e.maxStacks > 0 ? e.percent * e.maxStacks : 100f;
                if (ctx.Battle.AddTeamAtk(e.percent, max))
                {
                    FloatingText.Status(caster.Center + new Vector3(0f, 0.45f, 0f), "全員の攻撃力 +" + ctx.Battle.TeamAtkPercent.ToString("0") + "%");
                    Fx.Buff(caster.Center);
                }
                break;
            }

            case EffectType.Confuse:
            {
                float r = e.target == EffectTarget.AreaAroundSelf ? (e.radius > 0f ? e.radius : DefaultRadius) : caster.Range;
                Fx.BrainWave(caster.transform.position, r);
                foreach (Unit u in Targets(e, ctx, true))
                {
                    var en = u as Enemy;
                    if (en != null) en.Confuse(e.duration > 0f ? e.duration : 3f);
                }
                break;
            }

            case EffectType.DamageControl:
            {
                foreach (Unit u in Targets(e, ctx, false))
                {
                    var ch = u as Character;
                    if (ch == null) continue;
                    ch.GiveDamageControl(e.percent > 0f ? e.percent : 50f);
                    Fx.Buff(ch.Center);
                }
                break;
            }

            case EffectType.Remodel:
            {
                SkillRunner skills = caster.Skills;
                if (skills == null || skills.Remodeled || skills.UseCount != Mathf.Max(1, e.count)) break;
                skills.MarkRemodeled(e.label);
                // 改装の強化は必殺技が終わっても外さない（撤退・ダウンまで続く）
                var permanent = new SkillContext { Caster = caster, Battle = ctx.Battle, Target = ctx.Target, DefaultDuration = 0f, Collected = null };
                if (e.effects != null)
                    foreach (EffectData sub in e.effects) Apply(sub, permanent);
                Fx.Revive(caster.transform.position);
                Fx.Buff(caster.Center);
                FloatingText.Status(caster.Center + new Vector3(0f, 0.45f, 0f), caster.DisplayName + "！");
                AudioManager.PlaySE("buff");
                break;
            }

            case EffectType.Dp:
            {
                int amount = Mathf.RoundToInt(e.value);
                if (amount <= 0) break;
                ctx.Battle.State.AddDp(amount, ctx.Battle.Rules.dpMax);
                Fx.DpGain(caster.Center);
                FloatingText.Status(caster.Center, "DP +" + amount);
                break;
            }
        }
    }

    private static float Multiplier(EffectData e)
    {
        return e.multiplier > 0f ? e.multiplier : 1f;
    }

    /// <summary>効果の対象。enemies は Self 以外のとき敵と味方のどちらを選ぶか（Target・AreaAroundSelf のとき）</summary>
    private static List<Unit> Targets(EffectData e, SkillContext ctx, bool enemies)
    {
        var list = new List<Unit>();
        Character caster = ctx.Caster;
        Battle battle = ctx.Battle;
        switch (e.target)
        {
            case EffectTarget.Self:
                list.Add(caster);
                break;

            case EffectTarget.Target:
                if (ctx.Target != null && ctx.Target.Alive) list.Add(ctx.Target);
                else if (enemies)
                {
                    Enemy t = Combat.FindEnemyTarget(battle, caster);
                    if (t != null) list.Add(t);
                }
                else
                {
                    Character t = Combat.FindHealTarget(battle, caster);
                    list.Add(t != null ? t : caster);
                }
                break;

            case EffectTarget.AreaAroundSelf:
            {
                float r = e.radius > 0f ? e.radius : DefaultRadius;
                Vector3 c = caster.transform.position;
                if (enemies)
                {
                    foreach (Enemy en in battle.Enemies)
                        if (en.Alive && Near(en.transform.position, c, r)) list.Add(en);
                }
                else
                {
                    foreach (Character ch in battle.Characters)
                        if (ch.Alive && Near(ch.transform.position, c, r)) list.Add(ch);
                }
                break;
            }

            case EffectTarget.AlliesInRange:
                foreach (Character ch in battle.Characters)
                    if (ch.Alive && Combat.InRange(caster.Cell, ch.Cell, caster.Range)) list.Add(ch);
                break;

            case EffectTarget.EnemiesInRange:
                foreach (Enemy en in battle.Enemies)
                    if (en.Alive && Combat.InRange(caster.Cell, battle.Map.WorldToCell(en.transform.position), caster.Range)) list.Add(en);
                break;
        }
        return list;
    }

    private static bool Near(Vector3 a, Vector3 b, float r)
    {
        float dx = a.x - b.x, dy = a.y - b.y;
        return dx * dx + dy * dy <= r * r;
    }
}
