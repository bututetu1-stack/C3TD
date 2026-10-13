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

    public static Enemy Spawn(EnemyData data, List<Vector3> path, Battle battle, Transform parent)
    {
        var obj = new GameObject("Enemy_" + data.id);
        obj.transform.SetParent(parent, false);
        obj.transform.position = path[0];
        var enemy = obj.AddComponent<Enemy>();
        enemy.Data = data;
        enemy.battle = battle;
        enemy.path = path;
        enemy.nextPoint = 1;
        enemy.InitHealth(data.hp);
        enemy.remainingFrom = new float[path.Count];
        for (int i = path.Count - 2; i >= 0; i--)
            enemy.remainingFrom[i] = enemy.remainingFrom[i + 1] + Vector3.Distance(path[i], path[i + 1]);
        enemy.BuildVisual();
        enemy.UpdateRemaining();
        enemy.UpdateDepth();
        Fx.EnemySpawn(path[0], Palette.Hex(data.color, Palette.EnemyFallback));
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

        if (BlockedBy != null && !BlockedBy.Alive) BlockedBy = null;
        if (Stunned)
        {
            // スタン中は歩かず、攻撃もしない
        }
        else if (BlockedBy == null)
        {
            Move(ApplyStat("speed", Data.speed) * dt);
            walkTime += dt;
            TryBeBlocked();
            if (nextPoint >= path.Count)
            {
                battle.OnEnemyReachedBase(this);
                return;
            }
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

    /// <summary>止めている部員が撤退・ダウンしたので、また歩き出す</summary>
    public void Unblock()
    {
        BlockedBy = null;
    }

    private void TryBeBlocked()
    {
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
        target.TakeDamage(Atk, DamageType.Physical);
        Fx.Hit(target.Center, Palette.Crimson);
        AudioManager.PlaySE("hit");
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

        visual.localPosition = jitter + new Vector3(0f, hop * 0.06f * Data.size, 0f) + lungeOffset;
        visual.localScale = new Vector3(pop * (2f - squash), pop * squash, 1f);
        visual.localRotation = Quaternion.Euler(0f, 0f, BlockedBy == null ? Mathf.Sin(step) * 5f : 0f);
        shadow.localScale = shadowScale * Mathf.Clamp01(pop) * (1f - hop * 0.12f);

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
        battle.OnEnemyDefeated(this);
    }
}
