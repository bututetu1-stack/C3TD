using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 配置された部員（と、必殺技で召喚されたユニット）。射程内の敵を攻撃し（医療は味方を回復し）、
/// 地上ならブロック数まで敵を止める。特性と必殺技は Skills（SkillRunner）が動かす。
/// 見た目はアイコンを丸く切り抜いた駒（アイコンがなければ名前の頭文字を入れた色付きの丸）。
/// </summary>
public class Character : Unit
{
    private const int OrderCharacter = 24;  // 敵（20）より手前：大きな敵に止められていても駒が見える
    private const float DeployDropTime = 0.3f;
    private const float ArrowSpeed = 14f;   // マス/秒
    private const float OrbSpeed = 9f;
    private const float SummonScale = 0.82f;

    public CharacterData Data { get; private set; }
    public ClassData Class { get; private set; }

    /// <summary>編成のメンバー（召喚ユニットは null）</summary>
    public SquadMember Member { get; private set; }

    /// <summary>召喚したキャラ（召喚ユニットのとき）</summary>
    public Character Summoner { get; private set; }
    public bool IsSummon { get { return Summoner != null || Member == null; } }

    /// <summary>召喚ユニットが消えるまでの残り秒と、もとの長さ</summary>
    public float LifeRemaining { get; private set; }
    public float Lifetime { get; private set; }

    public Vector2Int Cell { get; private set; }
    public Color ClassColor { get; private set; }
    public SkillRunner Skills { get; private set; }

    /// <summary>いま止めている敵</summary>
    public readonly List<Enemy> Blocked = new List<Enemy>();

    public int BlockCapacity { get; private set; }

    /// <summary>まだ敵を止められるか（地上のマスにいるときだけ止める）</summary>
    public bool HasBlockRoom
    {
        get { return onGround && Blocked.Count < BlockCapacity; }
    }

    public float Range { get { return ApplyStat("range", baseRange); } }
    public float AttackInterval { get { return Mathf.Max(0.1f, ApplyStat("attackInterval", baseInterval)); } }
    protected override float BaseAtk { get { return baseAtk; } }
    protected override float BaseDef { get { return baseDef; } }

    private float baseAtk;
    private float baseDef;
    private float baseInterval;
    private float baseRange;
    private bool onGround;

    private Transform visual;
    private SpriteRenderer selectRing;
    private HealthBar spBar;
    private SpriteRenderer readyRing;
    private SpriteRenderer activeAura;
    private SpriteRenderer buffAura;
    private Transform sleepMark;
    private float age;
    // 降りてくる演出の経過。一時停止中に置いたときも着地まで見せるよう、止まっているあいだは実時間で進める
    private float dropAge;
    private float attackTimer;
    private float flash;
    private float lunge;
    private Vector3 lungeDir;
    private float recoil;
    private bool selected;
    private readonly List<SpriteRenderer> tinted = new List<SpriteRenderer>();
    private readonly List<Color> tintBase = new List<Color>();

    /// <summary>編成のメンバーを置く</summary>
    public static Character Create(SquadMember member, Vector2Int cell, Vector3 position, Battle battle, Transform parent)
    {
        Character c = Spawn(member.Data, member.Class, cell, position, battle, parent);
        c.Member = member;
        c.SetBaseStats(member.Data.stats.hp, member.Data.stats.atk, member.Data.stats.def);
        c.Finish();
        return c;
    }

    /// <summary>召喚ユニットを置く。inheritPercent が 0 より大きければ、HP・攻撃・防御を召喚者のその割合にする</summary>
    public static Character CreateSummon(CharacterData data, ClassData classData, Character summoner, float inheritPercent, float lifetime,
        Vector2Int cell, Vector3 position, Battle battle, Transform parent)
    {
        Character c = Spawn(data, classData, cell, position, battle, parent);
        c.Summoner = summoner;
        c.Lifetime = lifetime;
        c.LifeRemaining = lifetime;
        if (inheritPercent > 0f && summoner != null)
        {
            float k = inheritPercent / 100f;
            c.SetBaseStats(summoner.MaxHp * k, summoner.Atk * k, summoner.Def * k);
        }
        else
        {
            c.SetBaseStats(data.stats.hp, data.stats.atk, data.stats.def);
        }
        c.Finish();
        return c;
    }

    private static Character Spawn(CharacterData data, ClassData classData, Vector2Int cell, Vector3 position, Battle battle, Transform parent)
    {
        var obj = new GameObject("Character_" + data.id);
        obj.transform.SetParent(parent, false);
        obj.transform.position = new Vector3(position.x, position.y, position.y * 0.01f);
        var c = obj.AddComponent<Character>();
        c.Data = data;
        c.Class = classData;
        c.Cell = cell;
        c.battle = battle;
        c.ClassColor = Palette.Hex(classData.color, Palette.Amber);
        c.BlockCapacity = data.stats.block >= 0 ? data.stats.block : classData.block;
        c.baseInterval = data.stats.attackInterval;
        c.baseRange = data.stats.range;
        Tile tile = battle.Map.Get(cell);
        c.onGround = tile != null && tile.Type == TileType.Ground;
        return c;
    }

    private void SetBaseStats(float hp, float atk, float def)
    {
        baseAtk = atk;
        baseDef = def;
        InitHealth(hp);
    }

    private void Finish()
    {
        Skills = new SkillRunner(this, battle, Data.trait, IsSummon ? null : Data.ultimate);
        BuildVisual();
        Skills.OnDeploy();
    }

    // ================= 見た目 =================

    private void BuildVisual()
    {
        var group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = OrderCharacter;

        var shadow = AddSprite(transform, "Shadow", SpriteFactory.SoftCircle, Palette.Shadow, new Vector3(0.82f, 0.3f, 1f), 0);
        shadow.transform.localPosition = new Vector3(0f, -0.36f, 0f);

        // 足元の輪：必殺技が使える／発動中／攻撃力が上がっている
        Sprite readyArt = Art.Effect("SkillReady");
        readyRing = AddSprite(transform, "Ready", readyArt != null ? readyArt : SpriteFactory.Ring, readyArt != null ? Color.white : Palette.Amber, Vector3.one * 1.15f, 0);
        readyRing.gameObject.AddComponent<Spinner>().degreesPerSecond = 70f;
        readyRing.enabled = false;
        activeAura = AddSprite(transform, "Active", SpriteFactory.Glow, Palette.WithAlpha(Palette.Amber, 0.6f), Vector3.one * 1.5f, 0);
        activeAura.enabled = false;
        Sprite buffArt = Art.Effect("BuffAtk");
        buffAura = AddSprite(transform, "Buff", buffArt != null ? buffArt : SpriteFactory.Glow, buffArt != null ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 0.45f, 0.25f, 0.45f), Vector3.one * 0.95f, 0);
        buffAura.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        buffAura.enabled = false;

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

        if (IsSummon) visual.localScale = Vector3.one * SummonScale;

        healthBar = HealthBar.Create(transform, new Vector3(0f, -0.5f, 0f), 0.74f, 0.07f, Palette.AllyHp, false, 8);
        if (Skills.HasUltimate || IsSummon)
            spBar = HealthBar.Create(transform, new Vector3(0f, -0.59f, 0f), 0.74f, 0.05f, IsSummon ? Palette.SummonTime : Palette.SpBar, false, 8);
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

    // ================= 毎フレーム =================

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (dropAge < DeployDropTime) dropAge += TimeController.Paused ? Time.unscaledDeltaTime : dt;
        if (battle != null && Alive && battle.State.Phase == GamePhase.Playing)
        {
            TickStatus(dt);
            Skills.Tick(dt);
            Blocked.RemoveAll(e => e == null || !e.Alive || e.BlockedBy != this);

            if (!Stunned)
            {
                attackTimer -= dt;
                if (attackTimer <= 0f) attackTimer = TryAct() ? AttackInterval : 0f;
            }

            if (IsSummon && Lifetime > 0f)
            {
                LifeRemaining -= dt;
                if (LifeRemaining <= 0f)
                {
                    battle.Dismiss(this);
                    return;
                }
            }
        }
        UpdateStatusVisuals();
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
            Skills.OnAttack(ally);
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
            Sprite art = ProjectileArt();
            Projectile.Launch(ProjectileSprite(), ProjectileColor(), Center, target, orb ? OrbSpeed : ArrowSpeed, orb ? 0.34f : 0.5f, !orb || art != null,
                at => Strike(target, at));
        }
        Skills.OnAttack(target);
        return true;
    }

    /// <summary>当たったところで攻撃力ぶんの攻撃を与える。範囲攻撃ならまわりの敵にも。MultiHit なら何回か</summary>
    private void Strike(Enemy target, Vector3 at)
    {
        if (battle == null) return;
        float atk = Atk;
        int hits = 1 + Mathf.RoundToInt(ApplyStat("hits", 0f));
        for (int h = 0; h < hits; h++)
        {
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
        }
        AudioManager.PlaySE("hit");
    }

    private Sprite ProjectileArt()
    {
        string name = !string.IsNullOrEmpty(Data.projectile) ? Data.projectile : Class.projectile;
        return string.IsNullOrEmpty(name) ? null : Art.Effect(name);
    }

    private Sprite ProjectileSprite()
    {
        Sprite art = ProjectileArt();
        if (art != null) return art;
        return Class.attack == AttackKind.Ranged && Class.damageType == DamageType.Physical ? SpriteFactory.Shard : SpriteFactory.Glow;
    }

    private Color ProjectileColor()
    {
        if (ProjectileArt() != null) return Color.white;
        if (Class.attack == AttackKind.Heal) return Palette.HealGlow;
        return Class.damageType == DamageType.Arts ? Palette.ArtsGlow : Palette.AmberBright;
    }

    // ================= 見た目の更新 =================

    /// <summary>SP バー・足元の輪・攻撃力アップの気・寝落ちの印</summary>
    private void UpdateStatusVisuals()
    {
        if (spBar != null)
        {
            if (IsSummon) spBar.Set(Lifetime > 0f ? LifeRemaining / Lifetime : 1f);
            else if (Skills.Active)
            {
                spBar.SetColor(Palette.Amber);
                spBar.Set(Skills.ActiveRemaining / Mathf.Max(0.01f, Skills.Ultimate.duration));
            }
            else
            {
                spBar.SetColor(Skills.Ready ? Palette.SpBarFull : Palette.SpBar);
                spBar.Set(Skills.Sp / Mathf.Max(0.01f, Skills.SpCost));
            }
        }

        readyRing.enabled = Skills.Ready;
        if (readyRing.enabled)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
            readyRing.transform.localScale = Vector3.one * 1.15f * pulse;
        }

        activeAura.enabled = Skills.Active;
        if (activeAura.enabled)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * 6f);
            activeAura.color = Palette.WithAlpha(Palette.Amber, 0.35f + 0.3f * pulse);
        }

        buffAura.enabled = PercentOf("atk") > 0.01f;
        if (buffAura.enabled)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * 3f);
            buffAura.transform.localScale = Vector3.one * (0.9f + 0.08f * pulse);
        }

        if (Stunned && sleepMark == null)
        {
            Sprite sleep = Art.Effect("Sleep");
            var obj = new GameObject("Sleep");
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = new Vector3(0.28f, 0.55f, 0f);
            obj.transform.localScale = Vector3.one * 0.6f;
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sleep != null ? sleep : SpriteFactory.Glow;
            sr.color = sleep != null ? Color.white : Palette.RetreatGlow;
            sr.sortingOrder = 10;
            sleepMark = obj.transform;
        }
        else if (!Stunned && sleepMark != null)
        {
            Destroy(sleepMark.gameObject);
            sleepMark = null;
        }
        if (sleepMark != null)
            sleepMark.localPosition = new Vector3(0.28f, 0.55f + 0.05f * Mathf.Sin(age * 2.5f), 0f);
    }

    /// <summary>上から降りてきて着地し、攻撃では前へ突っ込む（近接）か少し縮む（遠距離）。スタン中は傾いて止まる</summary>
    private void Animate(float dt)
    {
        float drop = Mathf.Clamp01(dropAge / DeployDropTime);
        float land = Ease.OutBack(drop);
        float breathe = Stunned ? 1f : 1f + 0.015f * Mathf.Sin(age * 2.4f);

        lunge = Mathf.Max(0f, lunge - dt * 6f);
        recoil = Mathf.Max(0f, recoil - dt * 5f);
        Vector3 lungeOffset = lungeDir * Mathf.Sin(lunge * Mathf.PI) * 0.22f;
        float squash = 1f - 0.08f * Mathf.Sin(recoil * Mathf.PI);

        float baseScale = IsSummon ? SummonScale : 1f;
        visual.localPosition = new Vector3(0f, (1f - drop) * 0.6f, 0f) + lungeOffset;
        float s = Mathf.LerpUnclamped(1.25f, 1f, land) * breathe * baseScale;
        visual.localScale = new Vector3(s * (2f - squash), s * squash, 1f);
        visual.localRotation = Quaternion.Euler(0f, 0f, Stunned ? 14f : 0f);

        flash = Mathf.Max(0f, flash - dt * 6f);
        for (int i = 0; i < tinted.Count; i++)
        {
            Color c = Color.Lerp(tintBase[i], new Color(1f, 0.35f, 0.35f, tintBase[i].a), flash * 0.6f);
            if (Stunned) c = Color.Lerp(c, new Color(0.55f, 0.6f, 0.75f, c.a), 0.35f);
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
        if (Skills != null) Skills.OnHit();
    }

    protected override void OnDeath()
    {
        battle.OnCharacterDown(this);
    }
}
