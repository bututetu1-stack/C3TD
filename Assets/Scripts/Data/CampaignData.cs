using System.Collections.Generic;

/// <summary>章とステージの並び（Resources/Data/campaign.json）。ここに書いた順に作戦選択に並び、前をクリアすると次が開く</summary>
public class CampaignData
{
    public List<ChapterData> chapters = new List<ChapterData>();

    /// <summary>すべての章のステージを順に並べたもの</summary>
    public List<string> AllStages()
    {
        var ids = new List<string>();
        foreach (ChapterData c in chapters)
            if (c.stages != null) ids.AddRange(c.stages);
        return ids;
    }

    /// <summary>そのステージがある章（なければ null）</summary>
    public ChapterData ChapterOf(string stageId)
    {
        foreach (ChapterData c in chapters)
            if (c.stages != null && c.stages.Contains(stageId)) return c;
        return null;
    }
}

public class ChapterData
{
    public string id;
    public string name;     // 第1章　前期
    public string english;  // CHAPTER 1

    /// <summary>章の最後のステージをクリアしたとき、結果に出すひとこと（省略時は「第1章　前期　クリア！」）</summary>
    public string clearText;
    public List<string> stages = new List<string>();
}
