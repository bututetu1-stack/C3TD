using System.Collections.Generic;

/// <summary>素材（周回で落ちる。部員の育成に使う）</summary>
public class MaterialData
{
    public string id;
    public string name;

    /// <summary>アイコンの丸に入れる字</summary>
    public string label;
    public int rarity = 1;
    public string color;
    public string description;
}

/// <summary>クリアしたときの部費</summary>
public class CoinRule
{
    [JsonKey("base")] public float baseCoins = 60f;
    public float perChapter = 20f;
    public float perStage = 10f;
    public float lossRate = 0.2f;
    public float endlessPerWave = 12f;
    // 勝ったとき greatChance% で「大成功」：部費と素材が greatScale 倍
    public float greatChance = 15f;
    public float greatScale = 2f;
}

public class MaterialDropRule
{
    [JsonKey("base")] public int baseCount = 2;
}

/// <summary>レベル from〜to から次のレベルへ上げるときの費用（部費は coinPerLevel × いまのレベル）</summary>
public class LevelCost
{
    public int from;
    public int to;
    public float coinPerLevel;
    public string item;
    public int count;
}

public class ItemCount
{
    public string id;
    public int count;
}

/// <summary>昇進（at のレベルで昇進できる）</summary>
public class PromotionData
{
    public int at;
    public int coin;
    public List<ItemCount> items = new List<ItemCount>();
    public float initialSp;
    public float spCostPercent;
    public float atkPercent;
    public string description;
}

public class GrowthData
{
    public int maxLevel = 20;

    /// <summary>1 レベルごとの体力・攻撃力・防御（%）</summary>
    public float perLevel = 1.5f;
    public List<LevelCost> levels = new List<LevelCost>();
    public List<PromotionData> promotions = new List<PromotionData>();
}

/// <summary>備品（ガチャで出る。部員 1 人に 1 つ）</summary>
public class EquipmentData
{
    public string id;
    public string name;
    public int rarity = 1;

    /// <summary>hp / atk / def（%）、all（体力・攻撃力・防御 %）、interval（攻撃間隔 -%）、sp（最初の SP +）、redeploy（再配置 -%）、skillTime（必殺技の効果時間 +%）、cost（配置コスト -）</summary>
    public string stat;
    public float value;
    public string description;

    /// <summary>絵がないときに出す一字（省略時は名前の頭文字）</summary>
    public string label;

    public string Letter { get { return !string.IsNullOrEmpty(label) ? label : name.Substring(0, 1); } }
}

public class GachaData
{
    public int cost = 100;
    public int cost10 = 900;

    /// <summary>★1〜★3 の出る割合（%）</summary>
    public List<float> rates = new List<float>();

    /// <summary>重ねきった物がまた出たときの部費（★1〜★3）</summary>
    public List<int> duplicateCoins = new List<int>();

    /// <summary>天井：★3 が出ないままこの回数引くと、次は必ず★3（0 なら天井なし）</summary>
    public int pity = 40;

    /// <summary>★3 が出たとき、今日のピックアップになる割合（%）</summary>
    public float pickupRate = 50f;

    /// <summary>1 日 1 回、無料で 1 回引ける</summary>
    public bool freeDaily = true;
}

/// <summary>育成とガチャ（Resources/Data/items.json）</summary>
public class ItemsData
{
    public List<MaterialData> materials = new List<MaterialData>();
    public Dictionary<string, string> chapterDrops = new Dictionary<string, string>();
    public CoinRule coins = new CoinRule();
    public MaterialDropRule materialDrop = new MaterialDropRule();
    public GrowthData growth = new GrowthData();
    public List<EquipmentData> equipment = new List<EquipmentData>();
    public GachaData gacha = new GachaData();
}
