using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// クリックした敵の情報（左の札）。部員の札と同じ場所・同じ見た目で、どちらか一方だけが出る。
/// </summary>
public class EnemyPanel
{
    private readonly ChamferBox info;
    private readonly VisualElement portrait;
    private readonly Label portraitInitial;
    private readonly Label kindLabel;
    private readonly Label nameLabel;
    private readonly Label hpValue;
    private readonly Label atkValue;
    private readonly Label defValue;
    private readonly Label resValue;
    private readonly Label speedValue;
    private readonly Label lifeValue;
    private readonly Label stateLabel;
    private readonly Label traitsLabel;
    private Enemy shown;
    private string shownText;
    private Enemy placedFor;

    public EnemyPanel(VisualElement root)
    {
        info = new ChamferBox();
        info.AddToClassList("unit-info");
        info.SetFill(Palette.UiBand).SetStroke(Palette.UiPanelLine, 1f)
            .SetCut(24f, ChamferBox.Corners.TopRight | ChamferBox.Corners.BottomLeft)
            .SetStripes(Palette.UiStripe, 18f);
        info.pickingMode = PickingMode.Position;

        var head = UIFactory.Box("unit-head");
        portrait = UIFactory.Box("unit-portrait", "enemy-portrait");
        portraitInitial = UIFactory.Text("", UIFont.Title, "unit-portrait-initial");
        portrait.Add(portraitInitial);
        head.Add(portrait);
        var names = UIFactory.Box("unit-names");
        kindLabel = UIFactory.Text("", UIFont.Body, "unit-class");
        names.Add(kindLabel);
        nameLabel = UIFactory.Text("", UIFont.Title, "unit-name");
        names.Add(nameLabel);
        head.Add(names);
        info.Add(head);

        var stats = UIFactory.Box("unit-stats");
        hpValue = AddStat(stats, "HP");
        atkValue = AddStat(stats, "ATK");
        defValue = AddStat(stats, "DEF");
        resValue = AddStat(stats, "RES");
        speedValue = AddStat(stats, "SPEED");
        lifeValue = AddStat(stats, "LIFE");
        info.Add(stats);

        traitsLabel = UIFactory.Text("", UIFont.Medium, "enemy-traits");
        info.Add(traitsLabel);

        stateLabel = UIFactory.Text("", UIFont.Medium, "enemy-state");
        info.Add(stateLabel);

        root.Add(info);
        UIFactory.SetVisible(info, false);
    }

    private static Label AddStat(VisualElement parent, string caption)
    {
        var item = UIFactory.Box("unit-stat");
        item.Add(UIFactory.Text(caption, UIFont.Display, "unit-stat-caption"));
        var value = UIFactory.Text("", UIFont.Display, "unit-stat-value");
        item.Add(value);
        parent.Add(item);
        return value;
    }

    public void Refresh(Enemy e)
    {
        // 倒された敵は破棄されて Unity では null と等しくなるので、参照そのもので比べる
        if (e == null || !e.Alive) e = null;
        if (!ReferenceEquals(e, shown))
        {
            shown = e;
            shownText = null;
            if (e != null) Fill(e);
            UIFactory.SetVisible(info, e != null);
        }
        if (e == null) return;
        UnitPanel.PlaceOpposite(info, e.Center, ReferenceEquals(e, placedFor));
        placedFor = e;

        // 体力と、必殺技などで変わる数値・状態は毎フレーム確かめる（変わったときだけ書き換える）
        string text = Mathf.CeilToInt(e.Hp) + "|" + Mathf.RoundToInt(e.Atk) + "|" + Mathf.RoundToInt(e.Def) + "|" + e.Stunned + "|" + (e.BlockedBy != null ? e.BlockedBy.Data.name : "");
        if (text == shownText) return;
        shownText = text;
        hpValue.text = Mathf.CeilToInt(e.Hp).ToString();
        atkValue.text = Mathf.RoundToInt(e.Atk).ToString();
        defValue.text = Mathf.RoundToInt(e.Def).ToString();
        string state = "";
        if (e.Stunned) state = "気絶中";
        else if (e.BlockedBy != null) state = e.BlockedBy.Data.name + "が足止め中";
        stateLabel.text = state;
        UIFactory.SetVisible(stateLabel, state.Length > 0);
    }

    private void Fill(Enemy e)
    {
        EnemyData d = e.Data;
        Color color = Palette.Hex(d.color, Palette.Crimson);
        Sprite art = Art.Enemy(d.id);
        portrait.style.backgroundImage = art != null ? new StyleBackground(art) : new StyleBackground(StyleKeyword.None);
        portrait.style.backgroundColor = art != null ? new Color(1f, 1f, 1f, 0.06f) : color;
        portraitInitial.text = art != null ? "" : d.Label;
        // 2文字以上（期末・ES など）は枠に収まるように小さく
        portraitInitial.style.fontSize = d.Label.Length >= 2 ? 34f : 52f;
        Color frame = d.boss ? Palette.Amber : Palette.Crimson;
        portrait.style.borderLeftColor = portrait.style.borderRightColor = portrait.style.borderTopColor = portrait.style.borderBottomColor = frame;

        nameLabel.text = d.name;
        kindLabel.text = d.boss ? "ボス" : "敵";
        kindLabel.style.backgroundColor = frame;
        kindLabel.style.color = d.boss ? new Color(0.08f, 0.07f, 0.04f) : Color.white;
        resValue.text = Mathf.RoundToInt(e.Res).ToString();
        speedValue.text = d.speed.ToString("0.0#");
        lifeValue.text = "-" + d.lifeDamage;
        string traits = Traits(d);
        traitsLabel.text = SkillText.Format(traits);
        UIFactory.SetVisible(traitsLabel, traits.Length > 0);
    }

    /// <summary>特別な性質の説明（なければ空）</summary>
    public static string Traits(EnemyData d)
    {
        var lines = new System.Collections.Generic.List<string>();
        if (d.flying) lines.Add("飛行：止められず、前衛・重装は攻撃できない");
        if (d.range > 0f)
        {
            string line = "遠距離：射程 " + d.range.ToString("0.#") + " マスの部員を" + (d.damageType == DamageType.Arts ? "術で" : "") + "攻撃";
            if (d.stunOnHit > 0f) line += "し、" + d.stunOnHit.ToString("0.#") + " 秒眠らせる";
            lines.Add(line);
        }
        else if (d.stunOnHit > 0f)
        {
            lines.Add("攻撃した部員を " + d.stunOnHit.ToString("0.#") + " 秒眠らせる");
        }
        if (d.revive > 0) lines.Add("倒されても " + d.revive + " 回起き上がる（体力 " + d.reviveHpPercent.ToString("0") + "%）");
        if (d.aura != null && !string.IsNullOrEmpty(d.aura.stat))
            lines.Add("まわり " + d.aura.radius.ToString("0.#") + " マスの敵の" + StatName(d.aura.stat) + "を " + d.aura.percent.ToString("+0;-0") + "%");
        if (d.summon != null && !string.IsNullOrEmpty(d.summon.enemy))
        {
            EnemyData minion = GameData.Enemy(d.summon.enemy);
            lines.Add(d.summon.interval.ToString("0") + " 秒ごとに" + (minion != null ? minion.name : d.summon.enemy) + "を " + d.summon.count + " 体呼ぶ");
        }
        if (d.split != null && !string.IsNullOrEmpty(d.split.enemy))
        {
            EnemyData piece = GameData.Enemy(d.split.enemy);
            lines.Add("倒すと" + (piece != null ? piece.name : d.split.enemy) + " " + d.split.count + " 体に分かれる");
        }
        if (d.hitModifier != null && !string.IsNullOrEmpty(d.hitModifier.stat))
            lines.Add("攻撃した部員の" + StatName(d.hitModifier.stat) + "を " + d.hitModifier.duration.ToString("0.#") + " 秒 " + d.hitModifier.percent.ToString("+0;-0") + "%");
        if (d.dpDrain > 0f) lines.Add("場にいるあいだ、DP の回復を 1 秒あたり " + d.dpDrain.ToString("0.0#") + " 減らす");
        if (d.shield > 0f) lines.Add("体力より先に減る盾 " + d.shield.ToString("0"));
        if (d.dodge > 0f) lines.Add("物理攻撃を " + d.dodge.ToString("0") + "% の確率でかわす（術は当たる）");
        if (d.blink != null) lines.Add("止められていないとき、" + d.blink.interval.ToString("0.#") + " 秒ごとに " + d.blink.distance.ToString("0.#") + " マス先へ跳ぶ");
        if (d.accelerate != null) lines.Add(d.accelerate.interval.ToString("0.#") + " 秒ごとに速さ +" + d.accelerate.percent.ToString("0") + "%（+" + d.accelerate.max.ToString("0") + "% まで）");
        if (d.heal != null && d.heal.percent > 0f)
            lines.Add(d.heal.interval.ToString("0.#") + " 秒ごとに、まわり " + d.heal.radius.ToString("0.#") + " マスの敵の体力を " + d.heal.percent.ToString("0") + "%（" + d.heal.maxAmount.ToString("0") + " まで）回復");
        if (d.enrage != null)
        {
            var ups = new System.Collections.Generic.List<string>();
            if (d.enrage.atk != 0f) ups.Add("攻撃力 " + d.enrage.atk.ToString("+0;-0") + "%");
            if (d.enrage.speed != 0f) ups.Add("速さ " + d.enrage.speed.ToString("+0;-0") + "%");
            if (d.enrage.def != 0f) ups.Add("防御力 " + d.enrage.def.ToString("+0;-0") + "%");
            lines.Add("体力が " + d.enrage.hpPercent.ToString("0") + "% を切ると「" + (string.IsNullOrEmpty(d.enrage.name) ? "本気" : d.enrage.name) + "」：" + string.Join("・", ups));
        }
        // 弱点（硬い・術が効きにくい）
        if (!d.boss && d.def >= 300) lines.Add("防御が高い：術師の術なら防御が効かない");
        if (!d.boss && d.res >= 40f) lines.Add("術が効きにくい：狙撃・前衛の物理で");
        if (d.pulse != null)
            lines.Add(d.pulse.interval.ToString("0") + " 秒ごとに「" + (string.IsNullOrEmpty(d.pulse.name) ? "大技" : d.pulse.name) + "」：まわり " + d.pulse.radius.ToString("0.#") + " マスの部員を " + d.pulse.stun.ToString("0.#") + " 秒動けなくする");
        return string.Join("\n", lines);
    }

    private static string StatName(string stat)
    {
        switch (stat)
        {
            case "atk": return "攻撃力";
            case "def": return "防御力";
            case "speed": return "速さ";
            case "attackInterval": return "攻撃間隔";
            default: return stat;
        }
    }
}
