using System;
using UnityEngine;

/// <summary>相手を追いかけて飛ぶ弾（矢・術の弾・回復の弾）。届いたら onHit を呼んで消える</summary>
public class Projectile : MonoBehaviour
{
    private const int Order = 70;

    private Unit target;
    private Vector3 targetPos;
    private float speed;
    private bool rotate;
    private Action<Vector3> onHit;
    private SpriteRenderer trail;

    public static Projectile Launch(Sprite sprite, Color color, Vector3 from, Unit target, float speed, float size, bool rotate, Action<Vector3> onHit)
    {
        var obj = new GameObject("Projectile");
        Battle battle = Battle.Current;
        if (battle != null) obj.transform.SetParent(battle.transform, false);
        obj.transform.position = from;
        var p = obj.AddComponent<Projectile>();
        p.target = target;
        p.targetPos = target.Center;
        p.speed = speed;
        p.rotate = rotate;
        p.onHit = onHit;

        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = Order;
        obj.transform.localScale = rotate ? new Vector3(size, size * 0.35f, 1f) : Vector3.one * size;

        // 後ろに淡い光の尾
        var tail = new GameObject("Glow");
        tail.transform.SetParent(obj.transform, false);
        tail.transform.localScale = rotate ? new Vector3(1.4f, 2.2f, 1f) : Vector3.one * 1.8f;
        p.trail = tail.AddComponent<SpriteRenderer>();
        p.trail.sprite = SpriteFactory.Glow;
        p.trail.color = Palette.WithAlpha(color, 0.35f);
        p.trail.sortingOrder = Order - 1;

        p.Face();
        return p;
    }

    void Update()
    {
        if (target != null && target.Alive) targetPos = target.Center;
        Vector3 pos = transform.position;
        Vector3 to = new Vector3(targetPos.x, targetPos.y, pos.z);
        float step = speed * Time.deltaTime;
        if (Vector3.Distance(pos, to) <= step)
        {
            transform.position = to;
            if (onHit != null) onHit(targetPos);
            Destroy(gameObject);
            return;
        }
        transform.position = Vector3.MoveTowards(pos, to, step);
        Face();
    }

    private void Face()
    {
        if (!rotate) return;
        Vector3 d = targetPos - transform.position;
        if (d.sqrMagnitude < 1e-6f) return;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }
}
