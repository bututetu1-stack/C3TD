using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// マップの操作。編成バーのカードからのドラッグ配置と、配置した部員のクリック選択。
/// ドラッグ中・選択中は時間がゆっくりになり、置けるマスと射程がマップに出る。
/// </summary>
public class BattleInput : MonoBehaviour
{
    private const int GhostOrder = 95;

    public static BattleInput Instance { get; private set; }

    /// <summary>選択中の部員（いなければ null）</summary>
    public Character Selected { get; private set; }

    /// <summary>ドラッグ中の編成メンバー（いなければ null）</summary>
    public SquadMember Dragging { get; private set; }

    private HudUI hud;
    private Transform ghost;
    private readonly List<SpriteRenderer> ghostRenderers = new List<SpriteRenderer>();
    private Battle shownBattle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    public void Init(HudUI hud)
    {
        Instance = this;
        this.hud = hud;
    }

    /// <summary>編成バーのカードを押した</summary>
    public void BeginDrag(SquadMember member)
    {
        Battle battle = Battle.Current;
        if (battle == null || battle.State.Phase != GamePhase.Playing) return;
        if (!member.Ready || !battle.CanAfford(member)) return;
        Select(null);
        Dragging = member;
        CreateGhost(member);
        TimeController.SetFocus(true);
    }

    public void Select(Character c)
    {
        if (Selected != null) Selected.SetSelected(false);
        Selected = c;
        if (Selected != null) Selected.SetSelected(true);
        TimeController.SetFocus(Selected != null || Dragging != null);
    }

    /// <summary>選択中の部員の必殺技を使う（必殺技ボタン）</summary>
    public void ActivateSelected()
    {
        Character c = Selected;
        if (c == null || !c.Alive || c.Skills == null || !c.Skills.Ready) return;
        Select(null);
        c.Skills.Activate();
    }

    /// <summary>選択中の部員を撤退させる（撤退ボタン）</summary>
    public void RetreatSelected()
    {
        Character c = Selected;
        Select(null);
        if (c != null && Battle.Current != null) Battle.Current.Retreat(c);
    }

    void Update()
    {
        Battle battle = Battle.Current;
        if (battle != shownBattle)
        {
            shownBattle = battle;
            CancelDrag();
            Selected = null;
            TimeController.SetFocus(false);
        }
        if (battle == null) return;

        if (battle.State.Phase != GamePhase.Playing)
        {
            CancelDrag();
            if (Selected != null) Select(null);
            battle.Highlight.Clear();
            return;
        }
        // 倒された部員は破棄されて Unity では null と等しくなるので、参照が残っていれば外す
        if (!ReferenceEquals(Selected, null) && (Selected == null || !Selected.Alive)) Select(null);

        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;
        bool cancel = (kb != null && kb.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame);
        if (mouse == null)
        {
            battle.Highlight.Clear();
            return;
        }

        Vector2 screen = mouse.position.ReadValue();
        Vector3 world = ScreenToWorld(screen);
        Vector2Int cell = battle.Map.WorldToCell(world);

        if (Dragging != null)
        {
            bool overUi = hud != null && hud.IsOverUI(screen);
            bool valid = !overUi && battle.CanPlace(Dragging, cell) && battle.CanAfford(Dragging);
            MoveGhost(valid ? battle.UnitPosition(cell) : world, valid);
            battle.Highlight.Show(
                battle.PlaceableCells(Dragging),
                valid ? Combat.CellsInRange(battle.Map, cell, Dragging.Data.stats.range) : null,
                overUi || !battle.Map.InBounds(cell) ? (Vector2Int?)null : cell,
                valid);

            if (cancel)
            {
                CancelDrag();
            }
            else if (mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed)
            {
                // 離したフレームを取りこぼしても（ウィンドウの切り替えなど）、押されていなければ離したとみなす
                SquadMember member = Dragging;
                CancelDrag();
                if (valid) battle.Deploy(member, cell);
            }
            return;
        }

        if (cancel) Select(null);
        if (mouse.leftButton.wasPressedThisFrame && (hud == null || !hud.IsOverUI(screen)))
            Select(battle.CharacterAt(cell));

        if (Selected != null) battle.Highlight.Show(null, Combat.CellsInRange(battle.Map, Selected.Cell, Selected.Range), null, true);
        else battle.Highlight.Clear();
    }

    private void CancelDrag()
    {
        Dragging = null;
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = null;
        ghostRenderers.Clear();
        TimeController.SetFocus(Selected != null);
    }

    private static Vector3 ScreenToWorld(Vector2 screen)
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
        w.z = 0f;
        return w;
    }

    // ================= ドラッグ中の影 =================

    private void CreateGhost(SquadMember member)
    {
        ghost = new GameObject("DragGhost").transform;
        if (Battle.Current != null) ghost.SetParent(Battle.Current.transform, false);
        Color classColor = Palette.Hex(member.Class.color, Palette.Amber);
        AddGhostSprite(SpriteFactory.Ring, classColor, 0.86f, 0);
        AddGhostSprite(SpriteFactory.Circle, new Color(0.06f, 0.08f, 0.13f), 0.8f, 1);
        Sprite icon = Art.IconCircle(member.Data.id);
        AddGhostSprite(icon != null ? icon : SpriteFactory.Circle, icon != null ? Color.white : Palette.Hex(member.Data.color, classColor), 0.74f, 2);
        if (icon == null)
        {
            var text = WorldText.Create(ghost, member.Data.Initial, 4.6f, Color.white, GhostOrder + 3, true);
            text.transform.localPosition = Vector3.zero;
        }
    }

    private void AddGhostSprite(Sprite sprite, Color color, float scale, int order)
    {
        var obj = new GameObject("Ghost");
        obj.transform.SetParent(ghost, false);
        obj.transform.localScale = Vector3.one * scale;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = GhostOrder + order;
        ghostRenderers.Add(sr);
    }

    private void MoveGhost(Vector3 position, bool valid)
    {
        if (ghost == null) return;
        ghost.position = position;
        float alpha = valid ? 0.95f : 0.55f;
        ghost.localScale = Vector3.one * (valid ? 1f : 0.9f);
        foreach (SpriteRenderer sr in ghostRenderers)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}
