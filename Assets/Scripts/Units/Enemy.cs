using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 敵。経路の点を順にたどって歩き、最後の点（拠点）に着いたら Battle に知らせて消える。
/// Art/Enemies/{id}.png があればその絵を、なければ図形と文字（EnemyData の shape / color / label）で描く。
/// </summary>
public class Enemy : MonoBehaviour
{
    private const int OrderEnemy = 20;     // 敵どうしは SortingGroup で1体ずつまとめる
    private const float DefaultArtScale = 1.6f;
    private const float OutlineScale = 1.14f;
    private const float JitterRadius = 0.08f; // 重なって1体に見えないように、見た目だけ少しずらす
    private const float SpawnPopTime = 0.35f;

    public EnemyData Data { get; private set; }

    private Battle battle;
    private List<Vector3> path;
    private int nextPoint;
    private Transform visual;   // 弾む・傾く
    private Transform body;     // 進む向きで左右を反転する（絵のとき）
    private Transform shadow;
    private Vector3 shadowScale;
    private Vector3 jitter;
    private bool hasArt;
    private float bodyScale;
    private float facing = 1f;
    private float age;
    private float stepPhase;

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
        enemy.BuildVisual();
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
        stepPhase = Random.value * Mathf.PI * 2f;

        Sprite art = Art.Enemy(Data.id);
        if (art != null)
        {
            hasArt = true;
            bodyScale = size * (Data.artScale > 0f ? Data.artScale : DefaultArtScale);
            body = AddSprite(visual, "Art", art, Color.white, Vector3.one * bodyScale, 3).transform;
            body.localPosition = new Vector3(0f, size * 0.2f, 0f);
        }
        else
        {
            Color color = Palette.Hex(Data.color, Palette.EnemyFallback);
            Color outline = Data.boss ? Palette.BossOutline : Palette.EnemyOutline;
            AddSprite(visual, "Outline", SpriteFactory.ForShape(Data.shape), outline, Vector3.one * size * OutlineScale, 2);
            body = AddSprite(visual, "Body", SpriteFactory.ShadedShape(Data.shape), color, Vector3.one * size, 3).transform;
            var gloss = AddSprite(visual, "Gloss", SpriteFactory.Highlight, new Color(1f, 1f, 1f, 0.45f), new Vector3(size * 0.42f, size * 0.26f, 1f), 4);
            gloss.transform.localPosition = new Vector3(-size * 0.16f, size * 0.24f, 0f);
            gloss.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);

            string label = Data.Label;
            float fontSize = size * (label.Length <= 1 ? 6.2f : 4.4f);
            WorldText.Create(visual, label, fontSize, Palette.EnemyLabel, 5, true);
        }
        Animate();
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
        if (battle == null || battle.State.Phase != GamePhase.Playing) return;

        float dt = Time.deltaTime;
        age += dt;
        Move(Data.speed * dt);
        UpdateDepth();
        Animate();

        if (nextPoint >= path.Count) battle.OnEnemyReachedBase(this);
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

    /// <summary>下にいる敵ほど手前に描く</summary>
    private void UpdateDepth()
    {
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, p.y, p.y * 0.01f);
    }

    /// <summary>出てくるときに弾むように膨らみ、歩くあいだは跳ねながら左右に揺れる</summary>
    private void Animate()
    {
        float pop = Ease.OutBack(Mathf.Clamp01(age / SpawnPopTime));
        float step = age * (5f + Data.speed * 4f) + stepPhase;
        float hop = Mathf.Abs(Mathf.Sin(step));
        float squash = 1f + 0.06f * (hop - 0.5f);

        visual.localPosition = jitter + new Vector3(0f, hop * 0.06f * Data.size, 0f);
        visual.localScale = new Vector3(pop * (2f - squash), pop * squash, 1f);
        visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(step) * 5f);
        shadow.localScale = shadowScale * Mathf.Clamp01(pop) * (1f - hop * 0.12f);

        if (hasArt) body.localScale = new Vector3(bodyScale * facing, bodyScale, 1f);
    }
}
