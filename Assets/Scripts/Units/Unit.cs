using UnityEngine;

/// <summary>
/// 戦うもの（部員と敵）の共通部分。体力・被ダメージ・回復・HP バー。
/// </summary>
public abstract class Unit : MonoBehaviour
{
    public float Hp { get; protected set; }
    public float MaxHp { get; protected set; }
    public bool Alive { get; protected set; }

    public abstract float Atk { get; }
    public abstract float Def { get; }
    public virtual float Res { get { return 0f; } }

    /// <summary>弾の当たる位置・演出を出す位置</summary>
    public virtual Vector3 Center
    {
        get
        {
            Vector3 p = transform.position;
            return new Vector3(p.x, p.y + 0.08f, 0f);
        }
    }

    protected Battle battle;
    protected HealthBar healthBar;

    protected void InitHealth(float maxHp)
    {
        MaxHp = Mathf.Max(1f, maxHp);
        Hp = MaxHp;
        Alive = true;
    }

    /// <summary>攻撃を受ける。防御・術耐性で減らしたあとの、実際に減った量を返す</summary>
    public float TakeDamage(float atk, DamageType type)
    {
        if (!Alive) return 0f;
        float damage = Combat.Mitigate(atk, type, Def, Res);
        Hp = Mathf.Max(0f, Hp - damage);
        if (healthBar != null) healthBar.Set(Hp / MaxHp);
        FloatingText.Damage(Center, damage, DamageColor(type));
        OnDamaged();
        if (Hp <= 0f)
        {
            Alive = false;
            OnDeath();
        }
        return damage;
    }

    /// <summary>回復する。実際に増えた量を返す</summary>
    public float Heal(float amount)
    {
        if (!Alive) return 0f;
        float before = Hp;
        Hp = Mathf.Min(MaxHp, Hp + amount);
        if (healthBar != null) healthBar.Set(Hp / MaxHp);
        float healed = Hp - before;
        if (healed >= 1f) FloatingText.Heal(Center, healed);
        return healed;
    }

    /// <summary>撤退・拠点到達などで、倒されずに場から消える</summary>
    public void Despawn()
    {
        Alive = false;
        Destroy(gameObject);
    }

    protected abstract Color DamageColor(DamageType type);
    protected virtual void OnDamaged() { }
    protected abstract void OnDeath();
}
