using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 敵。経路の点を順にたどって歩き、最後の点（拠点）に着いたら Battle に知らせて消える。
/// 地上の前衛・重装に触れると止められ（ブロック）、止めた部員を攻撃する。
/// Art/Enemies/{id}.png があればその絵を、なければ図形と文字（EnemyData の shape / color / label）で描く。
/// </summary>
public class Enemy : Unit
{
    private const int OrderEnemy = 20;     // 敵どうしは SortingGroup で1体ずつまとめる
    private const float DefaultArtScale = 1.6f;
    private const float OutlineScale = 1.14f;
    private const float JitterRadius = 0.08f; // 重なって1体に見えないように、見た目だけ少しずらす
    private const float SpawnPopTime = 0.35f;
    private const float BlockReach = 0.6f;    // 部員の中心からこの距離まで来たら止められる

    public EnemyData Data { get; private set; }

    /// <summary>止めている部員（いなければ null）</summary>
    public Character BlockedBy { get; private set; }

    /// <summary>経路に沿った、拠点までの残りの距離（狙う順番に使う）</summary>
    public float RemainingDistance { get; private set; }

    protected override float BaseAtk { get { return Data.atk; } }
    protected override float BaseDef { get { return Data.def; } }
    public override float Res { get { return Data.res; } }

    private List<Vector3> path;
    private float[] remainingFrom; // 各点から拠点までの距離
    private int nextPoint;
    private Transform visual;      // 弾む・傾く
    private Transform body;        // 進む向きで左右を反転する（絵のとき）
    private SpriteRenderer bodyRenderer;
    private Color bodyColor;
    private Color flashColor;
    private Transform shadow;
    private Vector3 shadowScale;
    private Vector3 jitter;
    private bool hasArt;
    private float bodyScale;
    private float facing = 1f;
    private float age;
    private float walkTime;
    private float attackTimer;
    private float flash;
    private float lunge;
    private Vector3 lungeDir;
    private Transform stunMark;
    private Transform slowMark;
    private Transform defDownMark;

    // 特別な性質（EnemyData の flying / range / revive / aura / summon）
    private int revivesLeft;
    private float rangedTimer;
    private float summonTimer;
    private float auraTimer;
    private float pulseTimer;
    private bool pulseWarned;
    private float shieldHp;
    private HealthBar shieldBar;
    private float healTimer;
    private bool enraged;
    private readonly Dictionary<Enemy, StatModifier> auraTargets = new Dictionary<Enemy, StatModifier>();
    private Transform buffMark;

    /// <summary>たどっている経路と、次に向かう点（手下を呼ぶときに使う）</summary>
    public List<Vector3> Path { get { return path; } }
    public int NextPoint { get { return nextPoint; } }

    public static Enemy Spawn(EnemyData data, List<Vector3> path, Battle battle, Transform parent)
    {
        return SpawnOnPath(data, path, 1, path[0], battle, parent);
    }

    /// <summary>経路の途中（position から nextPoint の点へ向かうところ）に出す。ボスが呼ぶ手下など</summary>
    public static Enemy SpawnOnPath(EnemyData data, List<Vector3> path, int nextPoint, Vector3 position, Battle battle, Transform parent)
    {
        var obj = new GameObject("Enemy_" + data.id);
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        var enemy = obj.AddComponent<Enemy>();
        enemy.Data = data;
        enemy.battle = battle;
        enemy.path = path;
        enemy.nextPoint = Mathf.Clamp(nextPoint, 1, path.Count);
        enemy.InitHealth(data.hp);
        enemy.revivesLeft = data.revive;
        enemy.rangedTimer = data.attackInterval * 0.5f;
        enemy.summonTimer = data.summon != null ? data.summon.firstDelay : 0f;
        enemy.pulseTimer = data.pulse != null ? data.pulse.firstDelay : 0f;
        enemy.shieldHp = data.shield;
        enemy.healTimer = data.heal != null ? data.heal.interval : 0f;
        enemy.remainingFrom = new float[path.Count];
        for (int i = path.Count - 2; i >= 0; i--)
            enemy.remainingFrom[i] = enemy.remainingFrom[i + 1] + Vector3.Distance(path[i], path[i + 1]);
        enemy.BuildVisual();
        enemy.UpdateRemaining();
        enemy.UpdateDepth();
        Fx.EnemySpawn(position, Palette.Hex(data.color, Palette.EnemyFallback));
        return enemy;
    }

    private void BuildVisual()
    {
        var group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = OrderEnemy;
        float size = Data.size;

        shadowScale = new Vector3(size * 1.15f, size * 0.42f, 1f);
        shadow = AddSprite(transform, "Shadow", SpriteFactory.SoftCircle, Palette.Shadow, shadowScale, 1).transform;
        shadow.localPosition = new Vector3(0f, -size * 0.46f, 0f);

        if (Data.boss)
        {
            PulseRing.Create(transform, SpriteFactory.Glow, Palette.BossAura, 1.2f, size * 1.4f, size * 2.6f, 0f, 0);
            PulseRing.Create(transform, SpriteFactory.ThinRing, Palette.WithAlpha(Palette.BossOutline, 0.8f), 1.2f, size * 0.9f, size * 1.9f, 0.5f, 0);
        }

        // まわりの敵を強くする敵は、届く範囲に琥珀色の輪を出す
        if (Data.aura != null && !string.IsNullOrEmpty(Data.aura.stat))
            PulseRing.Create(transform, SpriteFactory.ThinRing, Palette.WithAlpha(Palette.Amber, 0.6f), 2f, Data.aura.radius * 0.8f, Data.aura.radius * 2f, 0f, 0);

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        jitter = Random.insideUnitCircle * JitterRadius;
        walkTime = Random.value * 10f;

        Sprite art = Art.Enemy(Data.id);
        if (art != null)
        {
            hasArt = true;
            bodyScale = size * (Data.artScale > 0f ? Data.artScale : DefaultArtScale);
            bodyRenderer = AddSprite(visual, "Art", art, Color.white, Vector3.one * bodyScale, 3);
            body = bodyRenderer.transform;
            body.localPosition = new Vector3(0f, size * 0.2f, 0f);
            bodyColor = Color.white;
            flashColor = new Color(1f, 0.55f, 0.55f);
        }
        else
        {
            Color color = Palette.Hex(Data.color, Palette.EnemyFallback);
            Color outline = Data.boss ? Palette.BossOutline : Palette.EnemyOutline;
            AddSprite(visual, "Outline", SpriteFactory.ForShape(Data.shape), outline, Vector3.one * size * OutlineScale, 2);
            bodyRenderer = AddSprite(visual, "Body", SpriteFactory.ShadedShape(Data.shape), color, Vector3.one * size, 3);
            body = bodyRenderer.transform;
            bodyColor = color;
            flashColor = Color.white;
            var gloss = AddSprite(visual, "Gloss", SpriteFactory.Highlight, new Color(1f, 1f, 1f, 0.45f), new Vector3(size * 0.42f, size * 0.26f, 1f), 4);
            gloss.transform.localPosition = new Vector3(-size * 0.16f, size * 0.24f, 0f);
            gloss.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);

            string label = Data.Label;
            float fontSize = size * (label.Length <= 1 ? 6.2f : 4.4f);
            WorldText.Create(visual, label, fontSize, Palette.EnemyLabel, 5, true);
        }

        // 足元に出す（上の段の部員の HP バーと重ならないように）
        healthBar = HealthBar.Create(transform, new Vector3(0f, -size * 0.66f - 0.03f, 0f), Mathf.Max(0.5f, size * 1.1f), 0.07f, Palette.EnemyHp, true, 6);
        // 盾のある敵は、体力バーのすぐ上に水色のバー
        if (Data.shield > 0f)
        {
            shieldBar = HealthBar.Create(transform, new Vector3(0f, -size * 0.66f + 0.07f, 0f), Mathf.Max(0.5f, size * 1.1f), 0.06f, Palette.Cyan, false, 6);
            shieldBar.Set(1f);
        }
        Animate(0f);
    }

    private SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, Color color, Vector3 scale, int order)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localScale = scale;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    void Update()
    {
        if (battle == null || !Alive || battle.State.Phase != GamePhase.Playing) return;

        float dt = Time.deltaTime;
        age += dt;
        TickStatus(dt);
        UpdateStunMark();
        UpdateDebuffMarks();
        CheckEnrage();

        if (BlockedBy != null && !BlockedBy.Alive) BlockedBy = null;
        UpdateAura(dt);
        if (Stunned)
        {
            // スタン中は歩かず、攻撃も手下を呼ぶこともしない
        }
        else
        {
            TickSummon(dt);
            TickPulse(dt);
            TickHeal(dt);
            if (BlockedBy == null)
            {
                Move(ApplyStat("speed", Data.speed) * dt);
                walkTime += dt;
                TryBeBlocked();
                if (nextPoint >= path.Count)
                {
                    battle.OnEnemyReachedBase(this);
                    return;
                }
                if (Data.range > 0f) TickRanged(dt);
            }
            else
            {
                attackTimer -= dt;
                if (attackTimer <= 0f)
                {
                    attackTimer = ApplyStat("attackInterval", Data.attackInterval);
                    AttackBlocker();
                }
            }
        }

        UpdateRemaining();
        UpdateDepth();
        Animate(dt);
    }

    /// <summary>スタン中は頭の上で星が回る</summary>
    private void UpdateStunMark()
    {
        if (Stunned && stunMark == null)
        {
            stunMark = new GameObject("Stun").transform;
            stunMark.SetParent(transform, false);
            stunMark.localPosition = new Vector3(0f, Data.size * 0.75f, 0f);
            stunMark.gameObject.AddComponent<Spinner>().degreesPerSecond = 240f;
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                var star = new GameObject("Star");
                star.transform.SetParent(stunMark, false);
                star.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.22f, Mathf.Sin(a) * 0.08f, 0f);
                star.transform.localScale = Vector3.one * 0.12f;
                var sr = star.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.ForShape(EnemyShape.Diamond);
                sr.color = Palette.AmberBright;
                sr.sortingOrder = 9;
            }
        }
        else if (!Stunned && stunMark != null)
        {
            Destroy(stunMark.gameObject);
            stunMark = null;
        }
    }

    /// <summary>
    /// 遅くなっている・防御が下がっているあいだは、体力バーの左に小さな印を出す。
    /// 絵（Art/Effects/StatusSlow.png・StatusDefDown.png）がなければ、色付きの丸に「遅」「防」
    /// </summary>
    private void UpdateDebuffMarks()
    {
        float y = -Data.size * 0.66f - 0.03f;
        float x = -Mathf.Max(0.5f, Data.size * 1.1f) * 0.5f - 0.18f;
        SetMark(ref slowMark, PercentOf("speed") < 0f, "StatusSlow", "遅", Palette.Cyan, new Vector3(x, y, 0f));
        if (slowMark != null) x -= 0.3f;
        SetMark(ref defDownMark, PercentOf("def") < 0f, "StatusDefDown", "防", Palette.Crimson, new Vector3(x, y, 0f));
        // まわりの敵に強くされているあいだは、体力バーの右に「強」
        bool buffed = PercentOf("def") > 0f || PercentOf("atk") > 0f || PercentOf("speed") > 0f;
        SetMark(ref buffMark, buffed, "StatusBuff", "強", Palette.Amber, new Vector3(Mathf.Max(0.5f, Data.size * 1.1f) * 0.5f + 0.18f, y, 0f));
    }

    private void SetMark(ref Transform mark, bool on, string artName, string letter, Color color, Vector3 position)
    {
        if (on && mark == null)
        {
            mark = new GameObject(artName).transform;
            mark.SetParent(transform, false);
            var body = new GameObject("Body");
            body.transform.SetParent(mark, false);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 9;
            Sprite art = Art.Effect(artName);
            if (art != null)
            {
                sr.sprite = art;
                body.transform.localScale = Vector3.one * 0.34f;
            }
            else
            {
                sr.sprite = SpriteFactory.Circle;
                sr.color = color;
                body.transform.localScale = Vector3.one * 0.27f;
                WorldText.Create(mark, letter, 2.0f, Color.white, 10, true);
            }
        }
        else if (!on && mark != null)
        {
            Destroy(mark.gameObject);
            mark = null;
        }
        if (mark != null) mark.localPosition = position;
    }

    /// <summary>止めている部員が撤退・ダウンしたので、また歩き出す</summary>
    public void Unblock()
    {
        BlockedBy = null;
    }

    // ================= 特別な性質 =================

    /// <summary>遠距離の敵：歩きながら、射程内のいちばん近い部員を撃つ</summary>
    private void TickRanged(float dt)
    {
        rangedTimer -= dt;
        if (rangedTimer > 0f) return;
        Character target = NearestCharacterInRange();
        if (target == null)
        {
            rangedTimer = 0.2f;
            return;
        }
        rangedTimer = ApplyStat("attackInterval", Data.attackInterval);
        lunge = 0.6f;
        lungeDir = target.transform.position - transform.position;
        lungeDir.z = 0f;
        lungeDir.Normalize();
        Color color = Palette.Hex(Data.color, Palette.EnemyFallback);
        Projectile.Launch(SpriteFactory.Glow, color, Center, target, 6f, 0.34f, false, at => HitCharacter(target));
    }

    private Character NearestCharacterInRange()
    {
        Vector2Int cell = battle.Map.WorldToCell(transform.position);
        Character best = null;
        float bestDistance = float.MaxValue;
        foreach (Character c in battle.Characters)
        {
            if (!c.Alive || !Combat.InRange(cell, c.Cell, Data.range)) continue;
            float d = Dist2(c.transform.position, transform.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = c;
            }
        }
        return best;
    }

    /// <summary>部員に攻撃を当てる（止められているときも、遠距離の弾も）。stunOnHit があれば眠らせる</summary>
    private void HitCharacter(Character target)
    {
        if (target == null || !target.Alive || battle == null) return;
        target.TakeDamage(Atk, Data.damageType);
        if (Data.stunOnHit > 0f) target.Stun(Data.stunOnHit);
        EnemyHitModifier hm = Data.hitModifier;
        if (hm != null && !string.IsNullOrEmpty(hm.stat) && target.Alive
            && target.AddOrRefreshModifier(Data, hm.stat, hm.percent, hm.duration))
            FloatingText.Status(target.Center, StatLabel(hm.stat) + (hm.percent < 0f ? "ダウン" : "アップ"));
        Fx.Hit(target.Center, Palette.Crimson);
        AudioManager.PlaySE("hit");
    }

    private static string StatLabel(string stat)
    {
        switch (stat)
        {
            case "atk": return "攻撃";
            case "def": return "防御";
            case "attackInterval": return "攻撃速度";
            default: return stat;
        }
    }

    /// <summary>ボスの大技：interval 秒ごとに、まわり radius マスの部員を stun 秒動けなくする</summary>
    private void TickPulse(float dt)
    {
        EnemyPulse pulse = Data.pulse;
        if (pulse == null) return;
        pulseTimer -= dt;
        // 1 秒前から、届く範囲に赤い輪を出して知らせる
        if (!pulseWarned && pulseTimer <= 1f)
        {
            pulseWarned = true;
            FxSprite.Create(SpriteFactory.ThinRing, transform.position, Palette.WithAlpha(Palette.Crimson, 0.9f), 1f, pulse.radius * 2f, pulse.radius * 2f, 79);
        }
        if (pulseTimer > 0f) return;
        pulseTimer = Mathf.Max(1f, pulse.interval);
        pulseWarned = false;
        Vector2Int cell = battle.Map.WorldToCell(transform.position);
        foreach (Character c in battle.Characters)
            if (c.Alive && Combat.InRange(cell, c.Cell, pulse.radius)) c.Stun(pulse.stun);
        FxSprite.Create(SpriteFactory.Ring, transform.position, Palette.WithAlpha(Palette.Crimson, 0.9f), 0.5f, 0.4f, pulse.radius * 2.2f, 80);
        FloatingText.Status(Center + new Vector3(0f, 0.4f, 0f), string.IsNullOrEmpty(pulse.name) ? "大技！" : pulse.name);
        CameraFitter.Shake(0.1f, 0.3f);
        AudioManager.PlaySE("boss_warning");
    }

    /// <summary>盾のある敵：体力より先に盾が受け止める。割れたら盾のバーを消す</summary>
    protected override float Absorb(float damage)
    {
        if (shieldHp <= 0f) return damage;
        float taken = Mathf.Min(shieldHp, damage);
        shieldHp -= taken;
        FloatingText.Damage(Center, taken, Palette.Cyan);
        if (shieldBar != null) shieldBar.Set(shieldHp / Mathf.Max(1f, Data.shield));
        if (shieldHp <= 0f)
        {
            if (shieldBar != null) Destroy(shieldBar.gameObject);
            shieldBar = null;
            FloatingText.Status(Center, "盾が割れた");
        }
        return damage - taken;
    }

    /// <summary>回復する敵：interval 秒ごとに、まわりの敵（自分も）の体力を回復し、届く範囲に緑の輪を出す</summary>
    private void TickHeal(float dt)
    {
        EnemyHeal h = Data.heal;
        if (h == null || h.percent <= 0f) return;
        healTimer -= dt;
        if (healTimer > 0f) return;
        healTimer = Mathf.Max(0.5f, h.interval);
        float r2 = h.radius * h.radius;
        foreach (Enemy e in battle.Enemies)
        {
            if (e == null || !e.Alive || e.Hp >= e.MaxHp || Dist2(e.transform.position, transform.position) > r2) continue;
            e.Heal(Mathf.Min(e.MaxHp * h.percent / 100f, h.maxAmount));
            Fx.Heal(e.Center);
        }
        FxSprite.Create(SpriteFactory.ThinRing, transform.position, Palette.WithAlpha(Palette.HealGlow, 0.8f), 0.6f, 0.4f, h.radius * 2f, 79);
    }

    /// <summary>本気になる敵（ボス）：体力が hpPercent% 以下になったら、一度だけ能力が上がり、体が赤みを帯びる</summary>
    private void CheckEnrage()
    {
        EnemyEnrage en = Data.enrage;
        if (en == null || enraged || Hp > MaxHp * en.hpPercent / 100f) return;
        enraged = true;
        if (en.atk != 0f) AddModifier("atk", en.atk, 0f, 0f);
        if (en.speed != 0f) AddModifier("speed", en.speed, 0f, 0f);
        if (en.def != 0f) AddModifier("def", en.def, 0f, 0f);
        FloatingText.Status(Center + new Vector3(0f, 0.4f, 0f), string.IsNullOrEmpty(en.name) ? "本気！" : en.name);
        Fx.Buff(Center);
        bodyColor = Color.Lerp(bodyColor, new Color(1f, 0.4f, 0.4f), 0.5f);
        CameraFitter.Shake(0.08f, 0.25f);
    }

    /// <summary>手下を呼ぶ敵：interval 秒ごとに、自分の位置から同じ経路の続きへ出す</summary>
    private void TickSummon(float dt)
    {
        EnemySummon s = Data.summon;
        if (s == null || string.IsNullOrEmpty(s.enemy)) return;
        summonTimer -= dt;
        if (summonTimer > 0f) return;
        summonTimer = Mathf.Max(1f, s.interval);
        EnemyData minion = GameData.Enemy(s.enemy);
        if (minion != null) battle.SpawnSummoned(minion, this, Mathf.Max(1, s.count));
    }

    /// <summary>まわりの敵の能力を上げる敵：0.25 秒ごとに、範囲に入った敵へ増減を付け、出た敵から外す</summary>
    private void UpdateAura(float dt)
    {
        EnemyAura a = Data.aura;
        if (a == null || string.IsNullOrEmpty(a.stat)) return;
        auraTimer -= dt;
        if (auraTimer > 0f) return;
        auraTimer = 0.25f;
        float r2 = a.radius * a.radius;
        Vector3 p = transform.position;
        var gone = new List<Enemy>();
        foreach (var kv in auraTargets)
            if (kv.Key == null || !kv.Key.Alive || Dist2(kv.Key.transform.position, p) > r2) gone.Add(kv.Key);
        foreach (Enemy e in gone)
        {
            if (e != null) e.RemoveModifier(auraTargets[e]);
            auraTargets.Remove(e);
        }
        foreach (Enemy e in battle.Enemies)
        {
            if (ReferenceEquals(e, this) || e == null || !e.Alive || auraTargets.ContainsKey(e)) continue;
            if (Dist2(e.transform.position, p) <= r2) auraTargets[e] = e.AddModifier(a.stat, a.percent, 0f, 0f);
        }
    }

    private void ClearAura()
    {
        foreach (var kv in auraTargets)
            if (kv.Key != null) kv.Key.RemoveModifier(kv.Value);
        auraTargets.Clear();
    }

    void OnDestroy()
    {
        ClearAura();
    }

    private static float Dist2(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    private void TryBeBlocked()
    {
        if (Data.flying) return; // 飛ぶ敵は止められない
        Vector3 p = transform.position;
        foreach (Character c in battle.Characters)
        {
            if (!c.Alive || !c.HasBlockRoom) continue;
            Vector3 cp = c.transform.position;
            float dx = p.x - cp.x, dy = p.y - cp.y;
            if (dx * dx + dy * dy > BlockReach * BlockReach) continue;
            BlockedBy = c;
            c.AddBlocked(this);
            attackTimer = Data.attackInterval * 0.5f;
            return;
        }
    }

    private void AttackBlocker()
    {
        Character target = BlockedBy;
        if (target == null || !target.Alive) return;
        lunge = 1f;
        lungeDir = target.transform.position - transform.position;
        lungeDir.z = 0f;
        lungeDir.Normalize();
        HitCharacter(target);
    }

    private void Move(float distance)
    {
        Vector3 pos = transform.position;
        Vector3 start = pos;
        while (distance > 0f && nextPoint < path.Count)
        {
            Vector3 target = path[nextPoint];
            target.z = pos.z;
            float d = Vector3.Distance(pos, target);
            if (d <= distance)
            {
                pos = target;
                distance -= d;
                nextPoint++;
            }
            else
            {
                pos = Vector3.MoveTowards(pos, target, distance);
                distance = 0f;
            }
        }
        transform.position = pos;
        float dx = pos.x - start.x;
        if (Mathf.Abs(dx) > 0.0001f) facing = Mathf.Sign(dx);
    }

    private void UpdateRemaining()
    {
        if (nextPoint >= path.Count)
        {
            RemainingDistance = 0f;
            return;
        }
        Vector3 p = transform.position;
        Vector3 next = path[nextPoint];
        RemainingDistance = new Vector2(next.x - p.x, next.y - p.y).magnitude + remainingFrom[nextPoint];
    }

    /// <summary>下にいる敵ほど手前に描く</summary>
    private void UpdateDepth()
    {
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, p.y, p.y * 0.01f);
    }

    /// <summary>出てくるときに弾むように膨らみ、歩くあいだは跳ねながら左右に揺れる。攻撃では前へ突っ込む</summary>
    private void Animate(float dt)
    {
        float pop = Ease.OutBack(Mathf.Clamp01(age / SpawnPopTime));
        float step = walkTime * (5f + Data.speed * 4f);
        float hop = BlockedBy == null ? Mathf.Abs(Mathf.Sin(step)) : 0.2f + 0.1f * Mathf.Sin(age * 3f);
        float squash = 1f + 0.06f * (hop - 0.5f);

        lunge = Mathf.Max(0f, lunge - dt * 5f);
        Vector3 lungeOffset = lungeDir * Mathf.Sin(lunge * Mathf.PI) * 0.16f;

        // 飛ぶ敵は跳ねずに、宙に浮いてゆっくり上下する（影は地面に小さく残す）
        float lift = Data.flying ? 0.42f + 0.05f * Mathf.Sin(age * 3f) : hop * 0.06f * Data.size;
        visual.localPosition = jitter + new Vector3(0f, lift, 0f) + lungeOffset;
        visual.localScale = new Vector3(pop * (2f - squash), pop * squash, 1f);
        visual.localRotation = Quaternion.Euler(0f, 0f, BlockedBy == null ? Mathf.Sin(step) * (Data.flying ? 2f : 5f) : 0f);
        shadow.localScale = shadowScale * Mathf.Clamp01(pop) * (Data.flying ? 0.65f : 1f - hop * 0.12f);

        flash = Mathf.Max(0f, flash - dt * 6f);
        bodyRenderer.color = Color.Lerp(bodyColor, flashColor, flash * 0.75f);

        if (hasArt)
        {
            float flip = Data.artFacing == ArtFacing.Left ? -facing : facing;
            body.localScale = new Vector3(bodyScale * flip, bodyScale, 1f);
        }
    }

    protected override Color DamageColor(DamageType type)
    {
        return type == DamageType.Arts ? Palette.ArtsDamage : Palette.PhysicalDamage;
    }

    protected override void OnDamaged()
    {
        flash = 1f;
    }

    protected override void OnDeath()
    {
        // 起き上がる敵（再履修など）：体力を戻して、少しのあいだ動けない
        if (revivesLeft > 0)
        {
            revivesLeft--;
            Revive(MaxHp * Data.reviveHpPercent / 100f);
            Stun(1f);
            FloatingText.Status(Center, "復活！");
            Fx.EnemySpawn(transform.position, Palette.Hex(Data.color, Palette.EnemyFallback));
            return;
        }
        ClearAura();
        // 分かれる敵（エントリーシートなど）：倒れた場所から、同じ経路の続きへ出す
        EnemySplit split = Data.split;
        if (split != null && !string.IsNullOrEmpty(split.enemy))
        {
            EnemyData piece = GameData.Enemy(split.enemy);
            if (piece != null) battle.SpawnSummoned(piece, this, Mathf.Max(1, split.count), false);
        }
        battle.OnEnemyDefeated(this);
    }
}
