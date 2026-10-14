using UnityEngine;

/// <summary>遊ぶ人の設定（PlayerPrefs に覚える。クリアの記録とは別のキー）。音量は AudioManager</summary>
public static class GameSettings
{
    private const string HintsKey = "c3td.hints";

    /// <summary>
    /// 出撃中の説明（作戦のヒントと、初めて会う敵）を出すか。出すときも、ヒントはその作戦をクリアするまで、
    /// 敵は初めて会ったときだけ。出すあいだは時間が止まる
    /// </summary>
    public static bool HintsOn
    {
        get { return PlayerPrefs.GetInt(HintsKey, 1) != 0; }
        set
        {
            PlayerPrefs.SetInt(HintsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
