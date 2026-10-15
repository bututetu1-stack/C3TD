using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// 特性・必殺技・敵の性質の説明を読みやすくする（UI Toolkit のリッチテキスト）。
///   ・JSON の説明に改行（\n）があれば、その行のまま。なければ文（。）ごとに改行する
///   ・数値（+30%・20秒・2マス・1.5 など）を明るい琥珀色にする
///   ・「・」で始まる行は箇条書き（点を琥珀色に）
/// </summary>
public static class SkillText
{
    private const string NumberColor = "#FFD27A";
    private const string BulletColor = "#F4B63F";

    // 前に + - が付いた数と、後ろの単位まで。直前が英数字のとき（C3 など）は数値とみなさない
    private static readonly Regex Number = new Regex(@"(?<![A-Za-z0-9.])[+\-−]?\d+(?:\.\d+)?(?:%|秒|マス|倍|回|体|段|人)?");

    public static string Format(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string body = text.Replace("\r", "");
        // 改行がなければ「。」ごとに行を分ける（「。」は残す）
        if (!body.Contains("\n")) body = body.Replace("。", "。\n");
        var sb = new StringBuilder();
        foreach (string raw in body.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (sb.Length > 0) sb.Append('\n');
            if (line.StartsWith("・"))
            {
                // 句読点の決まり：箇条書きには「。」を付けない（文の行は「。」で終える）
                if (line.EndsWith("。")) line = line.Substring(0, line.Length - 1);
                sb.Append("<color=").Append(BulletColor).Append(">・</color>");
                line = line.Substring(1);
            }
            // タグと読まれないように、説明の中の < > は全角にする
            line = line.Replace("<", "＜").Replace(">", "＞");
            sb.Append(Number.Replace(line, m => "<color=" + NumberColor + ">" + m.Value + "</color>"));
        }
        return sb.ToString();
    }

    /// <summary>必殺技の SP と効果時間の1行（「SP 28（時間で溜まる・最初 10）　効果 15秒」）</summary>
    public static string UltimateMeta(SkillData ult)
    {
        if (ult == null) return "";
        string sp = "SP " + ult.spCost.ToString("0") + "（" + MenuUI.SpChargeName(ult.spCharge)
            + (ult.initialSp > 0f ? "・最初 " + ult.initialSp.ToString("0") : "") + "）";
        return sp + (ult.duration > 0f ? "　効果 " + ult.duration.ToString("0.#") + "秒" : "　すぐに効く");
    }
}
