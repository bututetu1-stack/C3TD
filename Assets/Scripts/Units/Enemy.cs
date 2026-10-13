using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 敵。経路の点を順にたどって歩き、最後の点（拠点）に着いたら Battle に知らせて消える。
/// 見た目は図形と文字だけ（EnemyData の shape / color / size / label）。
/// </summary>
public class Enemy : MonoBehaviour
{
    // 描画の順番（敵どうしは SortingGroup で1体ずつまとめる）
    private const int OrderEnemy = 20;

    private const float OutlineScale = 1.14f;
    private const float JitterRadius = 0.1f;   // 重なって1体に見えないように、見た目だけ少しずらす
    private const float SpawnPopTime = 0.25f;

    public EnemyData Data { get; private set; }

    private Battle battle;
    private List<Vector3> path;
    private int nextPoint;
    private Transform visual;
    private float age;
    private float bobPhase;

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
        return enemy;
    }

    private void BuildVisual()
    {
        var group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = OrderEnemy;

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        visual.localPosition = Random.insideUnitCircle * JitterRadius;
        bobPhase = Random.value * Mathf.PI * 2f;

        Sprite sprite = SpriteFactory.ForShape(Data.shape);
        Color color = Palette.Hex(Data.color, Palette.EnemyFallback);
        Color outline = Data.boss ? new Color(1f, 0.82f, 0.3f) : Palette.EnemyOutline;

        AddSprite("Outline", sprite, outline, Data.size * OutlineScale, 0);
        AddSprite("Body", sprite, color, Data.size, 1);

        string label = Data.Label;
        float fontSize = Data.size * (label.Length <= 1 ? 6f : 4.2f);
        var text = WorldText.Create(visual, label, fontSize, Palette.EnemyLabel, 2, true);
        text.transform.localPosition = Vector3.zero;

        visual.localScale = Vector3.zero;
    }

    private void AddSprite(string name, Sprite sprite, Color color, float scale, int order)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(visual, false);
        obj.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
    }

    void Update()
    {
        if (battle == null || battle.State.Phase != GamePhase.Playing) return;

        float dt = Time.deltaTime;
        age += dt;
        Move(Data.speed * dt);
        Animate();

        if (nextPoint >= path.Count) battle.OnEnemyReachedBase(this);
    }

    private void Move(float distance)
    {
        Vector3 pos = transform.position;
        while (distance > 0f && nextPoint < path.Count)
        {
            Vector3 target = path[nextPoint];
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
    }

    /// <summary>出てくるときに膨らみ、歩くあいだは少し弾む</summary>
    private void Animate()
    {
        float pop = Mathf.Clamp01(age / SpawnPopTime);
        float scale = 1f + 0.15f * Mathf.Sin(pop * Mathf.PI); // 少し大きくなってから戻る
        float bob = 0.05f * Mathf.Sin(age * 10f + bobPhase);
        visual.localScale = new Vector3(scale * pop * (1f - bob), scale * pop * (1f + bob), 1f);
    }
}
