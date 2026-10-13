using System.Collections.Generic;

/// <summary>配置できる場所</summary>
public enum Placement
{
    Ground,
    HighGround,
}

/// <summary>攻撃のしかた</summary>
public enum AttackKind
{
    Melee,  // 射程内の敵を直接たたく
    Ranged, // 弾を飛ばす
    Heal,   // 味方を回復する
}

public enum DamageType
{
    Physical, // 防御で減る
    Arts,     // 術耐性（%）で減る
}

/// <summary>職業ごとの決まり（Resources/Data/classes.json）</summary>
public class ClassData
{
    public string name;
    public Placement placement;
    public AttackKind attack;
    public DamageType damageType;
    public int block;

    /// <summary>範囲攻撃の半径（マス）。0 なら単体</summary>
    public float splash;

    /// <summary>飛ばす弾の画像名（Art/Effects/{名前}.png。なければ光の弾）</summary>
    public string projectile;

    public string color;
}

/// <summary>全体のルール（Resources/Data/rules.json）</summary>
public class RulesData
{
    public float dpPerSecond = 1f;
    public int dpMax = 99;
    public float retreatRefundRate = 0.5f;
    public float minDamageRate = 0.05f;

    /// <summary>キャラをドラッグ中・選択中の時間の速さ（アークナイツのスロー）</summary>
    public float focusTimeScale = 0.2f;
}

/// <summary>出撃する部員（Resources/Data/squad.json）。M4 で編成画面に置き換える</summary>
public class SquadData
{
    public List<string> members = new List<string>();
}
