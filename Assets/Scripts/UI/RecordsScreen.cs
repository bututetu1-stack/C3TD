using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 記録（タイトルから）。章ごとのクリア・★・強襲、エンドレスの最高記録、部員ごとの出撃と MVP の回数（MVP の多い順）。
/// </summary>
public class RecordsScreen
{
    public readonly VisualElement Root;
    private readonly Label totalLabel;
    private readonly VisualElement chapters;
    private readonly VisualElement members;
    private readonly Label achievementTitle;
    private readonly VisualElement achievements;

    public RecordsScreen(VisualElement parent, GameBootstrap bootstrap)
    {
        Root = UIFactory.Box("full", "menu-screen");
        MenuUI.Header(Root, "RECORDS", "記録", () => bootstrap.ShowTitle(), out totalLabel);

        var body = UIFactory.Box("records-body");
        var left = new ChamferBox();
        left.AddToClassList("records-panel");
        left.SetFill(Palette.UiPanel).SetStroke(Palette.UiPanelLine, 1f).SetCut(20f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight);
        left.Add(UIFactory.Text("OPERATIONS", UIFont.Display, "help-caption"));
        left.Add(UIFactory.Text("作戦", UIFont.Title, "help-title"));
        chapters = UIFactory.Box("records-list");
        left.Add(chapters);
        // 実績（取ったものは明るく、まだのものは薄く）
        achievementTitle = UIFactory.Text("", UIFont.Title, "records-subtitle");
        left.Add(achievementTitle);
        var achievementScroll = new ScrollView(ScrollViewMode.Vertical);
        achievementScroll.AddToClassList("records-scroll");
        achievementScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        achievementScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        achievements = UIFactory.Box("records-list");
        achievementScroll.Add(achievements);
        left.Add(achievementScroll);
        body.Add(left);

        var right = new ChamferBox();
        right.AddToClassList("records-panel");
        right.AddToClassList("records-panel--wide");
        right.SetFill(Palette.UiPanel).SetStroke(Palette.UiPanelLine, 1f).SetCut(20f, ChamferBox.Corners.TopLeft | ChamferBox.Corners.BottomRight);
        right.Add(UIFactory.Text("MEMBERS", UIFont.Display, "help-caption"));
        right.Add(UIFactory.Text("部員（MVP の多い順）", UIFont.Title, "help-title"));
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("records-scroll");
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        members = UIFactory.Box("records-list");
        scroll.Add(members);
        right.Add(scroll);
        body.Add(right);
        Root.Add(body);

        parent.Add(Root);
    }

    public void Open()
    {
        chapters.Clear();
        int stars = 0, maxStars = 0;
        foreach (ChapterData c in GameData.Campaign.chapters)
        {
            StageData first = c.stages.Count > 0 ? GameData.Stage(c.stages[0]) : null;
            if (first != null && first.endless != null)
            {
                int best = Progress.BestWave(first.id);
                chapters.Add(Row(c.name, best > 0 ? "最高 WAVE " + best : "まだ記録なし", null));
                continue;
            }
            int cleared = 0, chapterStars = 0, hard = 0;
            foreach (string id in c.stages)
            {
                if (Progress.IsCleared(id)) cleared++;
                chapterStars += Progress.Stars(id);
                if (Progress.IsHardCleared(id)) hard++;
            }
            stars += chapterStars;
            maxStars += c.stages.Count * 3;
            chapters.Add(Row(c.name, "クリア " + cleared + "/" + c.stages.Count + "　★ " + chapterStars + "/" + (c.stages.Count * 3),
                hard > 0 ? "強襲 " + hard : null));
        }
        totalLabel.text = "★ " + stars + " / " + maxStars;

        // 前の出撃のあとに条件を満たしたもの（強化をそろえた、など）もここで記録する
        Achievements.CheckNew();
        achievements.Clear();
        achievementTitle.text = "実績 " + Achievements.UnlockedCount() + " / " + Achievements.All.Count;
        foreach (Achievements.Entry e in Achievements.All)
        {
            bool done = Progress.HasAchievement(e.Id);
            var row = UIFactory.Box("records-row", "achievement-row");
            row.EnableInClassList("achievement-row--locked", !done);
            row.Add(new IconView(IconKind.Star, 22f, done ? Palette.AmberBright : new Color(1f, 1f, 1f, 0.2f)));
            var text = UIFactory.Box("achievement-text");
            text.Add(UIFactory.Text(e.Name, UIFont.Body, "achievement-name"));
            text.Add(UIFactory.Text(e.Description + "　（部費 " + e.Reward + "）", UIFont.Medium, "achievement-desc"));
            row.Add(text);
            achievements.Add(row);
        }

        members.Clear();
        var list = new List<CharacterData>();
        List<string> order = GameData.CharacterIds();
        foreach (string id in order)
        {
            CharacterData d = GameData.Character(id);
            if (d != null) list.Add(d);
        }
        // MVP の多い順、同じなら出撃の多い順（記録のない部員は後ろ）
        list.Sort((a, b) =>
        {
            MemberRecord ra = Progress.Member(a.id), rb = Progress.Member(b.id);
            int ma = ra != null ? ra.mvp : -1, mb = rb != null ? rb.mvp : -1;
            if (ma != mb) return mb.CompareTo(ma);
            int sa = ra != null ? ra.sorties : 0, sb = rb != null ? rb.sorties : 0;
            if (sa != sb) return sb.CompareTo(sa);
            return order.IndexOf(a.id).CompareTo(order.IndexOf(b.id)); // 同じなら編成画面の並び
        });
        int maxMvp = 1;
        foreach (CharacterData d in list)
        {
            MemberRecord r = Progress.Member(d.id);
            if (r != null) maxMvp = Mathf.Max(maxMvp, r.mvp);
        }
        int rank = 0;
        foreach (CharacterData d in list)
        {
            MemberRecord r = Progress.Member(d.id);
            rank++;
            members.Add(MemberRow(d, r, rank, maxMvp));
        }
    }

    private static VisualElement Row(string name, string value, string badge)
    {
        var row = UIFactory.Box("records-row");
        row.Add(UIFactory.Text(name, UIFont.Body, "records-name"));
        row.Add(UIFactory.Box("top-spacer"));
        if (badge != null) row.Add(UIFactory.Text(badge, UIFont.Body, "records-badge"));
        row.Add(UIFactory.Text(value, UIFont.Body, "records-value"));
        return row;
    }

    private static VisualElement MemberRow(CharacterData d, MemberRecord r, int rank, int maxMvp)
    {
        ClassData cls = GameData.Class(d.unitClass);
        Color classColor = Palette.Hex(cls.color, Palette.Amber);
        var row = UIFactory.Box("records-row", "records-member");
        row.Add(UIFactory.Text(rank.ToString(), UIFont.Display, "records-rank"));
        var portrait = UIFactory.Box("team-portrait");
        Sprite icon = Art.Icon(d.id);
        if (icon != null) portrait.style.backgroundImage = new StyleBackground(icon);
        else
        {
            portrait.style.backgroundColor = Palette.Hex(d.color, classColor);
            portrait.Add(UIFactory.Text(d.Initial, UIFont.Title, "team-initial"));
        }
        portrait.style.borderLeftColor = portrait.style.borderRightColor = portrait.style.borderTopColor = portrait.style.borderBottomColor = classColor;
        row.Add(portrait);
        row.Add(UIFactory.Text(d.name, UIFont.Body, "records-name"));
        var track = UIFactory.Box("records-track");
        var fill = UIFactory.Box("team-fill");
        fill.style.width = Length.Percent(r != null ? Mathf.Clamp01(r.mvp / (float)maxMvp) * 100f : 0f);
        fill.style.backgroundColor = classColor;
        track.Add(fill);
        row.Add(track);
        row.Add(UIFactory.Text(r != null && r.sorties > 0 ? "MVP " + r.mvp + "　出撃 " + r.sorties : "まだ出撃していない", UIFont.Body, "records-value"));
        return row;
    }
}
