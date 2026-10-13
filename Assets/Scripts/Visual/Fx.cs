using UnityEngine;

/// <summary>
/// 場面ごとの演出。Art/Effects/ に画像があれば重ねて使い、なければ光の輪と粒だけで見せる。
/// </summary>
public static class Fx
{
    public const int Order = 80;

    /// <summary>敵が出撃口から現れる</summary>
    public static void EnemySpawn(Vector3 position, Color color)
    {
        Sprite art = Art.Effect("SpawnBurst");
        if (art != null) FxSprite.Create(art, position, Color.white, 0.55f, 0.7f, 1.5f, Order).Spin(80f);
        FxSprite.Create(SpriteFactory.Glow, position, Palette.WithAlpha(color, 0.9f), 0.35f, 1.4f, 0.5f, Order);
        FxSprite.Create(SpriteFactory.Ring, position, Palette.WithAlpha(color, 0.9f), 0.45f, 0.2f, 1.2f, Order);
    }

    /// <summary>敵が拠点にたどり着いた</summary>
    public static void EnemyLeak(Vector3 position)
    {
        Color c = Palette.Crimson;
        Sprite art = Art.Effect("LeakBurst");
        if (art != null) FxSprite.Create(art, position, Color.white, 0.65f, 0.9f, 2.2f, Order + 2);

        FxSprite.Create(SpriteFactory.RoundedSquare, position, new Color(1f, 0.9f, 0.9f, 0.85f), 0.25f, 0.95f, 1.05f, Order + 1);
        FxSprite.Create(SpriteFactory.Ring, position, c, 0.5f, 0.3f, 2.4f, Order);
        FxSprite.Create(SpriteFactory.ThinRing, position, Palette.WithAlpha(c, 0.7f), 0.7f, 0.3f, 3.2f, Order).Delay(0.08f);
        FxSprite.Create(SpriteFactory.Glow, position, Palette.WithAlpha(c, 0.8f), 0.5f, 2.6f, 1.2f, Order);
        for (int i = 0; i < 16; i++)
        {
            float angle = Random.value * 360f;
            Vector3 dir = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
            FxSprite.Create(SpriteFactory.Shard, position, i % 3 == 0 ? Color.white : c, Random.Range(0.35f, 0.6f), 0.32f, 0.12f, Order + 1)
                .Rotate(angle)
                .Move(dir * Random.Range(3f, 6f), 5f);
        }
    }
}
