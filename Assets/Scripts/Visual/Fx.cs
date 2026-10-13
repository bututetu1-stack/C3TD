using UnityEngine;

/// <summary>
/// 場面ごとの演出。Art/Effects/{名前}.png に画像があれば重ねて使い、なければ光の輪と粒だけで見せる。
/// 画像の名前は素材依頼帳と同じ（SpawnBurst・LeakBurst・Deploy・Retreat・Slash・Hit・MagicBurst・Heal・EnemyDefeat）。
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
        Shards(position, c, Color.white, 16, 3f, 6f, 0.32f);
    }

    /// <summary>部員を配置した：光の柱が立ち、足元に輪が広がる</summary>
    public static void Deploy(Vector3 position, Color classColor)
    {
        Sprite art = Art.Effect("Deploy");
        if (art != null) FxSprite.Create(art, position + new Vector3(0f, 0.3f, 0f), Color.white, 0.6f, 1.2f, 1.5f, Order);
        FxSprite.Create(SpriteFactory.Glow, position + new Vector3(0f, 0.45f, 0f), Palette.WithAlpha(Palette.DeployGlow, 0.85f), 0.45f, 1.0f, 1.3f, Order).Stretch(0.35f);
        FxSprite.Create(SpriteFactory.Ring, position, Palette.WithAlpha(classColor, 0.95f), 0.5f, 0.3f, 1.5f, Order);
        FxSprite.Create(SpriteFactory.ThinRing, position, Palette.WithAlpha(Color.white, 0.8f), 0.6f, 0.4f, 2.0f, Order).Delay(0.06f);
        Shards(position, Palette.DeployGlow, classColor, 10, 1.5f, 3.5f, 0.22f);
    }

    /// <summary>部員が撤退した：青白い光になって昇っていく</summary>
    public static void Retreat(Vector3 position)
    {
        Sprite art = Art.Effect("Retreat");
        if (art != null) FxSprite.Create(art, position + new Vector3(0f, 0.3f, 0f), Color.white, 0.7f, 1.1f, 1.3f, Order);
        FxSprite.Create(SpriteFactory.Glow, position, Palette.WithAlpha(Palette.RetreatGlow, 0.9f), 0.5f, 1.3f, 0.3f, Order);
        for (int i = 0; i < 10; i++)
        {
            Vector3 p = position + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.3f, 0.2f), 0f);
            FxSprite.Create(SpriteFactory.Glow, p, Palette.RetreatGlow, Random.Range(0.5f, 0.8f), 0.22f, 0.08f, Order)
                .Move(new Vector3(0f, Random.Range(1.2f, 2.2f), 0f), 1.5f);
        }
    }

    /// <summary>部員が倒された</summary>
    public static void UnitDown(Vector3 position)
    {
        FxSprite.Create(SpriteFactory.Glow, position, new Color(1f, 0.4f, 0.4f, 0.8f), 0.4f, 1.4f, 0.4f, Order);
        FxSprite.Create(SpriteFactory.Ring, position, new Color(0.7f, 0.75f, 0.85f, 0.9f), 0.5f, 0.9f, 0.2f, Order);
        Shards(position, new Color(0.75f, 0.8f, 0.9f), Palette.Crimson, 12, 2f, 4f, 0.24f);
    }

    /// <summary>近接攻撃の斬撃</summary>
    public static void Slash(Vector3 position)
    {
        Sprite art = Art.Effect("Slash");
        if (art != null)
        {
            FxSprite.Create(art, position, Color.white, 0.25f, 0.9f, 1.1f, Order + 1);
            return;
        }
        float angle = Random.Range(-50f, -20f);
        FxSprite.Create(SpriteFactory.Shard, position, new Color(1f, 1f, 1f, 0.95f), 0.22f, 0.5f, 0.9f, Order + 1).Stretch(3.2f).Rotate(angle);
        FxSprite.Create(SpriteFactory.Shard, position, Palette.WithAlpha(Palette.Cyan, 0.8f), 0.28f, 0.6f, 1.05f, Order).Stretch(3.6f).Rotate(angle);
    }

    /// <summary>攻撃が当たった</summary>
    public static void Hit(Vector3 position, Color color)
    {
        Sprite art = Art.Effect("Hit");
        if (art != null) FxSprite.Create(art, position, Color.white, 0.25f, 0.45f, 0.75f, Order + 1).Rotate(Random.value * 360f);
        FxSprite.Create(SpriteFactory.Glow, position, Palette.WithAlpha(color, 0.8f), 0.2f, 0.6f, 0.3f, Order);
        FxSprite.Create(SpriteFactory.ThinRing, position, Palette.WithAlpha(Color.white, 0.8f), 0.22f, 0.15f, 0.55f, Order);
        Shards(position, Color.white, color, 4, 2f, 3.5f, 0.14f);
    }

    /// <summary>術の範囲攻撃の着弾</summary>
    public static void ArtsBurst(Vector3 position, float radius, Color color)
    {
        Sprite art = Art.Effect("MagicBurst");
        if (art != null) FxSprite.Create(art, position, Color.white, 0.45f, radius * 1.2f, radius * 2.2f, Order + 1).Spin(60f);
        FxSprite.Create(SpriteFactory.Glow, position, Palette.WithAlpha(Palette.ArtsGlow, 0.85f), 0.35f, radius * 2.2f, radius * 1.2f, Order);
        FxSprite.Create(SpriteFactory.Ring, position, Palette.WithAlpha(color, 0.9f), 0.4f, radius * 0.6f, radius * 2.1f, Order);
        Shards(position, Palette.ArtsGlow, Color.white, 6, 1.5f, 3f, 0.16f);
    }

    /// <summary>回復した</summary>
    public static void Heal(Vector3 position)
    {
        Sprite art = Art.Effect("Heal");
        if (art != null) FxSprite.Create(art, position + new Vector3(0f, 0.15f, 0f), Color.white, 0.6f, 0.8f, 1.0f, Order);
        FxSprite.Create(SpriteFactory.Glow, position, Palette.WithAlpha(Palette.HealGlow, 0.7f), 0.45f, 0.6f, 1.2f, Order);
        for (int i = 0; i < 5; i++)
        {
            Vector3 p = position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.25f, 0.1f), 0f);
            FxSprite.Create(SpriteFactory.Glow, p, Palette.HealGlow, Random.Range(0.4f, 0.7f), 0.18f, 0.06f, Order)
                .Move(new Vector3(0f, Random.Range(0.8f, 1.4f), 0f), 1.2f);
        }
    }

    /// <summary>敵を倒した：紙が破れて散る</summary>
    public static void EnemyDefeat(Vector3 position, Color color)
    {
        Sprite art = Art.Effect("EnemyDefeat");
        if (art != null) FxSprite.Create(art, position, Color.white, 0.55f, 0.7f, 1.4f, Order + 1).Rotate(Random.value * 360f);
        FxSprite.Create(SpriteFactory.Glow, position, new Color(1f, 0.95f, 0.85f, 0.85f), 0.3f, 1.1f, 0.4f, Order);
        FxSprite.Create(SpriteFactory.Ring, position, Palette.WithAlpha(color, 0.85f), 0.4f, 0.3f, 1.3f, Order);
        for (int i = 0; i < 12; i++)
        {
            float angle = Random.value * 360f;
            Vector3 dir = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
            Color c = i % 4 == 0 ? color : new Color(0.97f, 0.96f, 0.92f);
            FxSprite.Create(SpriteFactory.Square, position, c, Random.Range(0.45f, 0.75f), Random.Range(0.08f, 0.14f), 0.05f, Order + 1)
                .Stretch(Random.Range(0.6f, 1.4f))
                .Rotate(angle)
                .Spin(Random.Range(-540f, 540f))
                .Move(dir * Random.Range(1.5f, 3.5f) + new Vector3(0f, 0.8f, 0f), 2.5f);
        }
    }

    /// <summary>必殺技を発動した：大きな閃光</summary>
    public static void SkillActivate(Vector3 position, Color classColor)
    {
        Sprite art = Art.Effect("SkillActivate");
        if (art != null) FxSprite.Create(art, position, Color.white, 0.7f, 0.8f, 2.6f, Order + 2).Spin(40f);
        FxSprite.Create(SpriteFactory.Glow, position, Palette.WithAlpha(Palette.AmberBright, 0.9f), 0.5f, 2.8f, 1.2f, Order);
        FxSprite.Create(SpriteFactory.Ring, position, Palette.WithAlpha(classColor, 0.95f), 0.55f, 0.3f, 2.6f, Order);
        FxSprite.Create(SpriteFactory.ThinRing, position, Palette.WithAlpha(Color.white, 0.85f), 0.7f, 0.4f, 3.4f, Order).Delay(0.08f);
        Shards(position, Palette.AmberBright, Color.white, 14, 3f, 6f, 0.3f);
    }

    /// <summary>SP が溜まって必殺技が使えるようになった</summary>
    public static void SkillReady(Vector3 position)
    {
        FxSprite.Create(SpriteFactory.Ring, position, Palette.WithAlpha(Palette.AmberBright, 0.9f), 0.5f, 1.6f, 0.9f, Order);
        FxSprite.Create(SpriteFactory.Glow, position, Palette.WithAlpha(Palette.AmberBright, 0.6f), 0.4f, 1.2f, 0.6f, Order);
    }

    /// <summary>能力が上がった（特性の重ね数が増えたなど）</summary>
    public static void Buff(Vector3 position)
    {
        Sprite art = Art.Effect("BuffAtk");
        if (art != null) FxSprite.Create(art, position + new Vector3(0f, 0.2f, 0f), Color.white, 0.8f, 0.9f, 1.2f, Order);
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0f), 0f);
            FxSprite.Create(SpriteFactory.Glow, p, new Color(1f, 0.55f, 0.3f), Random.Range(0.5f, 0.8f), 0.2f, 0.06f, Order)
                .Move(new Vector3(0f, Random.Range(1.2f, 2f), 0f), 1.5f);
        }
    }

    /// <summary>召喚ユニットが現れた（艦娘：海から水柱）</summary>
    public static void Summon(Vector3 position)
    {
        Sprite art = Art.Effect("SummonSplash");
        if (art != null) FxSprite.Create(art, position + new Vector3(0f, 0.45f, 0f), Color.white, 0.7f, 1.2f, 1.5f, Order);
        FxSprite.Create(SpriteFactory.Ring, position, Palette.WithAlpha(Palette.Cyan, 0.9f), 0.5f, 0.3f, 1.4f, Order);
        FxSprite.Create(SpriteFactory.ThinRing, position, Palette.WithAlpha(Color.white, 0.8f), 0.6f, 0.3f, 1.8f, Order).Delay(0.1f);
        Shards(position, Color.white, Palette.Cyan, 8, 1.5f, 3f, 0.2f);
    }

    private static void Shards(Vector3 position, Color a, Color b, int count, float minSpeed, float maxSpeed, float size)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Random.value * 360f;
            Vector3 dir = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
            FxSprite.Create(SpriteFactory.Shard, position, i % 3 == 0 ? b : a, Random.Range(0.3f, 0.55f), size, size * 0.35f, Order + 1)
                .Rotate(angle)
                .Move(dir * Random.Range(minSpeed, maxSpeed), 5f);
        }
    }
}
