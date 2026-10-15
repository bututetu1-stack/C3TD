using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 選択中の部員の情報（左の札。中身は CharacterInfo）と、駒の横に出る必殺技・撤退のボタン。
/// </summary>
public class UnitPanel
{
    private readonly VisualElement root;
    private readonly CharacterInfo card;
    private readonly ChamferBox info;
    private readonly VisualElement actions;
    private readonly C3Button retreatButton;
    private readonly C3Button skillButton;
    private readonly C3Button targetButton;
    private readonly C3Button autoButton;
    private Character shown;
    private int shownHp = -1;
    private int shownAtk = -1;
    private int shownDef = -1;
    private string shownTrait;
    private string shownSkill;

    public UnitPanel(VisualElement root)
    {
        this.root = root;

        card = new CharacterInfo(false, "unit-info");
        info = card.Root;
        info.SetCut(24f, ChamferBox.Corners.TopRight | ChamferBox.Corners.BottomLeft);
        // 必殺技を自動で使うか（札のいちばん下）
        autoButton = new C3Button(IconKind.Bolt, 18f, "必殺技を自動で使う：OFF", ToggleAuto);
        autoButton.AddToClassList("auto-button");
        info.Add(autoButton);
        root.Add(info);

        actions = UIFactory.Box("unit-actions");
        skillButton = new C3Button(IconKind.Bolt, 24f, "必殺技", () =>
        {
            if (BattleInput.Instance != null) BattleInput.Instance.ActivateSelected();
        });
        skillButton.AddToClassList("skill-button");
        actions.Add(skillButton);
        retreatButton = new C3Button(IconKind.Cross, 22f, "撤退", () =>
        {
            if (BattleInput.Instance != null) BattleInput.Instance.RetreatSelected();
        });
        retreatButton.AddToClassList("retreat-button");
        actions.Add(retreatButton);
        // 狙い方（先頭 → 最後尾 → 強い敵 → 近い敵）。攻撃する部員だけ
        targetButton = new C3Button(null, 0f, "狙い：先頭", CycleTarget);
        targetButton.AddToClassList("target-button");
        actions.Add(targetButton);
        root.Add(actions);

        UIFactory.SetVisible(info, false);
        UIFactory.SetVisible(actions, false);
    }

    /// <summary>
    /// 札を、選んだものと反対の側に寄せる（左にいれば右に出す）。左端の出撃口を隠さないため。
    /// 動くもの（敵）は真ん中付近で行ったり来たりしないよう、左右 0.4 / 0.6 を越えたときだけ切り替える。
    /// </summary>
    public static void PlaceOpposite(VisualElement info, Vector3 world, bool sticky)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float x = cam.WorldToViewportPoint(world).x;
        bool right = info.ClassListContains("unit-info--right");
        if (!sticky) right = x < 0.5f;
        else if (x < 0.4f) right = true;
        else if (x > 0.6f) right = false;
        info.EnableInClassList("unit-info--right", right);
    }
    public void Refresh(Character c)
    {
        // 破棄された部員は Unity では null と等しくなるので、参照そのもので比べる
        if (c == null || !c.Alive) c = null;
        if (!ReferenceEquals(c, shown))
        {
            shown = c;
            shownHp = shownAtk = shownDef = -1;
            shownTrait = shownSkill = null;
            if (c != null)
            {
                Fill(c);
                PlaceOpposite(info, c.Center, false);
            }
            UIFactory.SetVisible(info, c != null);
            UIFactory.SetVisible(actions, c != null);
        }
        if (c == null) return;

        int hp = Mathf.CeilToInt(c.Hp);
        if (hp != shownHp)
        {
            shownHp = hp;
            card.HpValue.text = hp + "/" + Mathf.CeilToInt(c.MaxHp);
        }
        int atk = Mathf.RoundToInt(c.Atk);
        if (atk != shownAtk)
        {
            shownAtk = atk;
            card.AtkValue.text = atk.ToString();
            // 上がっていれば色を変える（味方全員に掛かる分も含む）
            float atkPercent = c.PercentOf("atk") + (Battle.Current != null ? Battle.Current.TeamAtkPercent : 0f);
            card.AtkValue.EnableInClassList("unit-stat-value--up", atkPercent > 0.01f);
            card.AtkValue.EnableInClassList("unit-stat-value--down", atkPercent < -0.01f);
        }
        int def = Mathf.RoundToInt(c.Def);
        if (def != shownDef)
        {
            shownDef = def;
            card.DefValue.text = def.ToString();
            card.DefValue.EnableInClassList("unit-stat-value--up", c.PercentOf("def") > 0.01f);
            card.DefValue.EnableInClassList("unit-stat-value--down", c.PercentOf("def") < -0.01f);
        }
        string range = c.Range.ToString("0.#");
        if (card.RangeValue.text != range) card.RangeValue.text = range;

        // 改装すると名前が変わる（物理鉄人改二）
        if (card.NameLabel.text != c.DisplayName) card.NameLabel.text = c.DisplayName;

        // 特性の重ね数
        string trait = c.Skills.Trait != null ? c.Skills.Trait.name + (c.Skills.TraitStatus.Length > 0 ? "　" + c.Skills.TraitStatus : "") : "";
        if (trait != shownTrait)
        {
            shownTrait = trait;
            card.TraitName.text = trait;
        }

        // 必殺技のボタン：SP が溜まるまでは押せない。発動中は残り時間
        string skill;
        if (c.IsSummon) skill = "召喚　残り " + Mathf.CeilToInt(c.LifeRemaining) + "秒";
        else if (!c.Skills.HasUltimate) skill = "";
        else if (c.Skills.Active) skill = "発動中 " + Mathf.CeilToInt(c.Skills.ActiveRemaining) + "秒";
        else if (c.Skills.Ready) skill = "必殺技";
        else if (c.Stunned) skill = "スタン中";
        else skill = "SP " + Mathf.FloorToInt(c.Skills.Sp) + "/" + Mathf.RoundToInt(c.Skills.SpCost);
        if (skill != shownSkill)
        {
            shownSkill = skill;
            UIFactory.SetVisible(skillButton, skill.Length > 0);
            skillButton.Text.text = skill;
            // 召喚ユニットの残り時間と、発動中の残り時間は、読めるよう暗くしない（押しても何もしない）。琥珀色は「使える」ときだけ
            skillButton.Interactable = c.Skills.Ready || c.IsSummon || c.Skills.Active;
            skillButton.Active = c.Skills.Ready;
            if (c.Skills.HasUltimate)
                card.UltMeta.text = (c.Skills.Active ? "発動中" : c.Skills.Ready ? "使える" : "SP " + Mathf.FloorToInt(c.Skills.Sp) + "/" + Mathf.RoundToInt(c.Skills.SpCost))
                    + "（" + MenuUI.SpChargeName(c.Skills.Ultimate.spCharge) + "）"
                    + (c.Skills.Ultimate.duration > 0f ? "　効果 " + c.Skills.Ultimate.duration.ToString("0.#") + "秒" : "　すぐに効く");
        }

        // ボタンを駒の右上に置く
        Camera cam = Camera.main;
        if (cam != null && root.panel != null)
        {
            Vector2 p = UIFactory.WorldToPanel(root, cam, c.transform.position + new Vector3(0.5f, 0.42f, 0f));
            // 右端の部員でもボタンが画面からはみ出さないように
            float width = actions.layout.width;
            if (!float.IsNaN(width) && width > 0f) p.x = Mathf.Min(p.x, root.layout.width - width - 12f);
            actions.style.left = p.x;
            actions.style.top = p.y - 30f;
        }
    }

    private void ToggleAuto()
    {
        Character c = shown;
        if (c == null || c.Member == null) return;
        c.Member.AutoSkill = !c.Member.AutoSkill;
        RefreshAuto(c);
        AudioManager.PlaySE("click");
    }

    private void RefreshAuto(Character c)
    {
        bool can = c.Member != null && c.Skills.HasUltimate;
        UIFactory.SetVisible(autoButton, can);
        if (!can) return;
        autoButton.Text.text = "必殺技を自動で使う：" + (c.Member.AutoSkill ? "ON" : "OFF");
        autoButton.Active = c.Member.AutoSkill;
    }

    /// <summary>選んでいる部員の狙い方を次へ（T キーでも）</summary>
    public void CycleTarget()
    {
        Character c = shown;
        if (c == null || c.Member == null || c.Class.attack == AttackKind.Heal) return;
        c.Member.Targeting = (TargetMode)(((int)c.Member.Targeting + 1) % 4);
        targetButton.Text.text = "狙い：" + Combat.TargetName(c.Targeting);
        AudioManager.PlaySE("click");
    }

    private void Fill(Character c)
    {
        // 出撃中の札は高さを抑える：ひとこと紹介は出さず、スマホでは技のひとことも出さない
        card.ShowProfile = false;
        card.ShowFlavor = !(Application.isMobilePlatform || UIFactory.Zoom > 1f);
        card.Fill(c.Data, c.Class, c.IsSummon);
        card.NameLabel.text = c.DisplayName;
        card.BlockValue.text = c.HasBlockRoom || c.Blocked.Count > 0 ? c.BlockCapacity.ToString() : "-";
        UIFactory.SetVisible(retreatButton, !c.IsSummon);
        if (c.Member != null) retreatButton.Text.text = "撤退 +" + Battle.Current.RefundOf(c.Member);
        RefreshAuto(c);
        UIFactory.SetVisible(targetButton, c.Member != null && c.Class.attack != AttackKind.Heal);
        targetButton.Text.text = "狙い：" + Combat.TargetName(c.Targeting);
    }
}
