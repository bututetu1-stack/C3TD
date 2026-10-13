using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 配置された部員。射程内の敵を攻撃し（医療は味方を回復し）、地上ならブロック数まで敵を止める。
/// 見た目はアイコンを丸く切り抜いた駒（アイコンがなければ名前の頭文字を入れた色付きの丸）。
/// </summary>
public class Character : Unit
{
    private const int OrderCharacter = 24;  // 敵（20）より手前：大きな敵に止められていても駒が見える
    private const float DeployDropTime = 0.3f;
    private const float ArrowSpeed = 14f;   // マス/秒
    private const float OrbSpeed = 9f;

    public CharacterData Data { get; private set; }
    public ClassData Class { get; private set; }
    public SquadMember Member { get; private set; }
    public Vector2Int Cell { get; private set; }
    public Color ClassColor { get; private set; }

    /// <summary>いま止めている敵</summary>
    public readonly List<Enemy> Blocked = new List<Enemy>();

    public int BlockCapacity { get; private set; }

    /// <summary>まだ敵を止められるか（地上に置いたときだけ止める）</summary>
    public bool HasBlockRoom
    {
        get { return Class.placement == Placement.Ground && Blocked.Count < BlockCapacity; }
    }

    public float Range { get { return Data.stats.range; } }
    public override float Atk { get { return Data.stats.atk; } }
    public override float Def { get { return Data.stats.def; } }

    private Transform visual;
    private SpriteRenderer selectRing;
    private float age;
    private float attackTimer;
    private float flash;
    private float lunge;
    private Vector3 lungeDir;
    private float recoil;
    private bool selected;
    private readonly List<SpriteRenderer> tinted = new List<SpriteRenderer>();
    private readonly List<Color> tintBase = new List<Color>();

    public static Character Create(SquadMember member, Vector2Int cell, Vector3 position, Battle battle, Transform parent)
    {
        var obj = new GameObject("Character_" + member.Data.id);
        obj.transform.SetParent(parent, false);
        obj.transform.position = new Vector3(position.x, position.y, position.y * 0.01f);
        var c = obj.AddComponent<Character>();
        c.Data = member.Data;
        c.Class = member.Class;
        c.Member = member;
        c.Cell = cell;
        c.battle = battle;
        c.ClassColor = Palette.Hex(member.Class.color, Palette.Amber);
        c.BlockCapacity = member.Data.stats.block >= 0 ? member.Data.stats.block : member.Class.block;
        c.InitHealth(member.Data.stats.hp);
        c.BuildVisual();
        return c;
    }

    private void BuildVisual()
    {
        var group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = OrderCharacter;

        var shadow = AddSprite(transform, "Shadow", SpriteFactory.SoftCircle, Palette.Shadow, new Vector3(0.82f, 0.3f, 1f), 0);
        shadow.transform.localPosition = new Vector3(0f, -0.36f, 0f);

        selectRing = AddSprite(transform, "Select", SpriteFactory.ThinRing, Palette.Amber, Vector3.one * 1.02f, 1);
        selectRing.enabled = false;

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);

        AddSprite(visual, "Ring", SpriteFactory.Ring, ClassColor, Vector3.one * 0.86f, 2, true);
        AddSprite(visual, "Disc", SpriteFactory.Circle, new Color(0.06f, 0.08f, 0.13f), Vector3.one * 0.8f, 3, true);
        Sprite icon = Art.IconCircle(Data.id);
        if (icon != null)
        {
            AddSprite(visual, "Portrait", icon, Color.white, Vector3.one * 0.74f, 4, true);
        }
        else
        {
            Color fill = Palette.Hex(Data.color, ClassColor);
            AddSprite(visual, "Portrait", SpriteFactory.ShadedShape(EnemyShape.Circle), fill, Vector3.one * 0.74f, 4, true);
            WorldText.Create(visual, Data.Initial, 4.6f, Color.white, 5, true);
        }

        // 右下に職業の札
        var badge = AddSprite(visual, "ClassBadge", SpriteFactory.Circle, ClassColor, Vector3.one * 0.3f, 6, true);
        badge.transform.localPosition = new Vector3(0.3f, -0.27f, 0f);
        var badgeText = WorldText.Create(visual, Class.name.Substring(0, 1), 1.9f, Palette.Ink, 7, false);
        badgeText.transform.localPosition = badge.transform.localPosition;

        healthBar = HealthBar.Create(transform, new Vector3(0f, -0.5f, 0f), 0.74f, 0.07f, Palette.AllyHp, false, 8);
        Animate(0f);
    }

    private SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, Color color, Vector3 scale, int order, bool flashes = false)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localScale = scale;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (flashes)
        {
            tinted.Add(sr);
            tintBase.Add(color);
        }
        return sr;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (battle != null && Alive && battle.State.Phase == GamePhase.Playing)
        {
            Blocked.RemoveAll(e => e == null || !e.Alive || e.BlockedBy != this);
            attackTimer -= dt;
            if (attackTimer <= 0f) attackTimer = TryAct() ? Data.stats.attackInterval : 0f;
        }
        Animate(dt);
    }

    public void SetSelected(bool value)
    {
        selected = value;
        if (selectRing != null) selectRing.enabled = value;
    }

    public void AddBlocked(Enemy enemy)
    {
        if (!Blocked.Contains(enemy)) Blocked.Add(enemy);
    }

    /// <summary>止めていた敵をすべて放す（撤退・ダウン）</summary>
    public void ReleaseAll()
    {
        foreach (Enemy e in Blocked)
            if (e != null && e.BlockedBy == this) e.Unblock();
        Blocked.Clear();
    }

    // ================= 攻撃・回復 =================

    private bool TryAct()
    {
        if (Class.attack == AttackKind.Heal)
        {
            Character ally = Combat.FindHealTarget(battle, this);
            if (ally == null) return false;
            recoil = 1f;
            float amount = Atk;
            Projectile.Launch(ProjectileSprite(), ProjectileColor(), Center, ally, OrbSpeed, 0.34f, false, at =>
            {
                if (ally == null || !ally.Alive) return;
                ally.Heal(amount);
                Fx.Heal(ally.Center);
            });
            return true;
        }

        Enemy target = Combat.FindEnemyTarget(battle, this);
        if (target == null) return false;
        if (Class.attack == AttackKind.Melee)
        {
            lunge = 1f;
            lungeDir = target.transform.position - transform.position;
            lungeDir.z = 0f;
            lungeDir.Normalize();
            Fx.Slash(target.Center);
            Strike(target, target.Center);
        }
        else
        {
            recoil = 1f;
            bool orb = Class.damageType == DamageType.Arts;
            Projectile.Launch(ProjectileSprite(), ProjectileColor(), Center, target, orb ? OrbSpeed : ArrowSpeed, orb ? 0.34f : 0.5f, !orb || Art.Effect(Class.projectile) != null,
                at => Strike(target, at));
        }
        return true;
    }

    /// <summary>当たったところで攻撃力ぶんの攻撃を与える。範囲攻撃ならまわりの敵にも</summary>
    private void Strike(Enemy target, Vector3 at)
    {
        if (battle == null) return;
        float atk = Atk;
        if (Class.splash > 0f)
        {
            Fx.ArtsBurst(at, Class.splash, ClassColor);
            float r2 = Class.splash * Class.splash;
            foreach (Enemy e in new List<Enemy>(battle.Enemies))
            {
                if (!e.Alive) continue;
                Vector3 p = e.transform.position;
                float dx = p.x - at.x, dy = p.y - at.y;
                if (dx * dx + dy * dy <= r2) e.TakeDamage(atk, Class.damageType);
            }
        }
        else
        {
            if (target == null || !target.Alive) return;
            Fx.Hit(at, ClassColor);
            target.TakeDamage(atk, Class.damageType);
        }
        AudioManager.PlaySE("hit");
    }

    private Sprite ProjectileSprite()
    {
        Sprite art = string.IsNullOrEmpty(Class.projectile) ? null : Art.Effect(Class.projectile);
        if (art != null) return art;
        return Class.attack == AttackKind.Ranged && Class.damageType == DamageType.Physical ? SpriteFactory.Shard : SpriteFactory.Glow;
    }

    private Color ProjectileColor()
    {
        if (!string.IsNullOrEmpty(Class.projectile) && Art.Effect(Class.projectile) != null) return Color.white;
        if (Class.attack == AttackKind.Heal) return Palette.HealGlow;
        return Class.damageType == DamageType.Arts ? Palette.ArtsGlow : Palette.AmberBright;
    }

    // ================= 見た目 =================

    /// <summary>上から降りてきて着地し、攻撃では前へ突っ込む（近接）か少し縮む（遠距離）</summary>
    private void Animate(float dt)
    {
        float drop = Mathf.Clamp01(age / DeployDropTime);
        float land = Ease.OutBack(drop);
        float breathe = 1f + 0.015f * Mathf.Sin(age * 2.4f);

        lunge = Mathf.Max(0f, lunge - dt * 6f);
        recoil = Mathf.Max(0f, recoil - dt * 5f);
        Vector3 lungeOffset = lungeDir * Mathf.Sin(lunge * Mathf.PI) * 0.22f;
        float squash = 1f - 0.08f * Mathf.Sin(recoil * Mathf.PI);

        visual.localPosition = new Vector3(0f, (1f - drop) * 0.6f, 0f) + lungeOffset;
        float s = Mathf.LerpUnclamped(1.25f, 1f, land) * breathe;
        visual.localScale = new Vector3(s * (2f - squash), s * squash, 1f);

        flash = Mathf.Max(0f, flash - dt * 6f);
        for (int i = 0; i < tinted.Count; i++)
        {
            Color c = Color.Lerp(tintBase[i], new Color(1f, 0.35f, 0.35f, tintBase[i].a), flash * 0.6f);
            c.a = tintBase[i].a * drop;
            tinted[i].color = c;
        }

        if (selected && selectRing != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            selectRing.color = Palette.WithAlpha(Palette.Amber, 0.55f + 0.45f * pulse);
        }
    }

    protected override Color DamageColor(DamageType type)
    {
        return Palette.AllyDamage;
    }

    protected override void OnDamaged()
    {
        flash = 1f;
    }

    protected override void OnDeath()
    {
        battle.OnCharacterDown(this);
    }
}
