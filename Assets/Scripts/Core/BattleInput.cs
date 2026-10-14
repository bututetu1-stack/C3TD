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

    /// <summary>クリックして情報を見ている敵（いなければ null）</summary>
    public Enemy Inspected { get; private set; }
    private PulseRing inspectRing;

    private HudUI hud;
    private Transform ghost;
    private readonly List<SpriteRenderer> ghostRenderers = new List<SpriteRenderer>();
    private Battle shownBattle;

    // 押した・離したは InputAction で読む。ボタンの状態をフレームの終わりに見るだけだと、
    // 1 フレームのうちに押して離した短いタップ（重いスマホや自動操作で起きる）を取りこぼすため
    private InputAction pressAction;

    void OnEnable()
    {
        pressAction = new InputAction("Press", InputActionType.Button, "<Pointer>/press");
        pressAction.Enable();
    }

    void OnDisable()
    {
        if (pressAction == null) return;
        pressAction.Disable();
        pressAction.Dispose();
        pressAction = null;
    }

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

    /// <summary>
    /// カードを押して、マップに出さずに離したときの編成メンバー（いなければ null）。
    /// このあと置けるマスを押すと、そこに置く（スマホで小さなマスまでドラッグしなくてよいように）
    /// </summary>
    public SquadMember Armed { get; private set; }

    /// <summary>編成バーのカードを押した</summary>
    public void BeginDrag(SquadMember member)
    {
        Battle battle = Battle.Current;
        if (battle == null || battle.State.Phase != GamePhase.Playing) return;
        // 選んでおいたカードをもう一度押したら、選ぶのをやめる
        if (ReferenceEquals(Armed, member))
        {
            Disarm();
            return;
        }
        Disarm();
        if (!member.Ready || !battle.CanAfford(member)) return;
        Select(null);
        Inspect(null);
        Dragging = member;
        CreateGhost(member);
        TimeController.SetFocus(true);
    }

    private void Disarm()
    {
        if (Armed == null) return;
        Armed = null;
        TimeController.SetFocus(Selected != null || Dragging != null);
    }

    public void Select(Character c)
    {
        if (Selected != null) Selected.SetSelected(false);
        Selected = c;
        if (Selected != null) Selected.SetSelected(true);
        TimeController.SetFocus(Selected != null || Dragging != null);
    }


    /// <summary>敵の情報を出す（null で閉じる）。見ている敵には白い輪を出す</summary>
    public void Inspect(Enemy e)
    {
        if (e != null && !e.Alive) e = null;
        if (ReferenceEquals(e, Inspected)) return;
        Inspected = e;
        if (inspectRing != null) Destroy(inspectRing.gameObject);
        inspectRing = null;
        if (e != null) inspectRing = PulseRing.Create(e.transform, SpriteFactory.ThinRing, Color.white, 1.1f, 0.9f, 1.25f, 0f, 19);
    }

    /// <summary>クリックした場所にいる敵（いちばん近いもの）</summary>
    private static Enemy EnemyAt(Battle battle, Vector3 world)
    {
        Enemy best = null;
        float bestDist = float.MaxValue;
        foreach (Enemy e in battle.Enemies)
        {
            if (e == null || !e.Alive) continue;
            Vector3 d = e.Center - world;
            d.z = 0f;
            float dist = d.magnitude;
            if (dist <= Mathf.Max(0.45f, e.Data.size * 0.6f) && dist < bestDist)
            {
                best = e;
                bestDist = dist;
            }
        }
        return best;
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
            Armed = null;
            Selected = null;
            Inspected = null;
            inspectRing = null;
            TimeController.SetFocus(false);
        }
        if (battle == null) return;

        if (battle.State.Phase != GamePhase.Playing)
        {
            CancelDrag();
            Disarm();
            if (Selected != null) Select(null);
            Inspect(null);
            battle.Highlight.Clear();
            return;
        }
        // 倒された部員は破棄されて Unity では null と等しくなるので、参照が残っていれば外す
        if (!ReferenceEquals(Selected, null) && (Selected == null || !Selected.Alive)) Select(null);
        if (!ReferenceEquals(Inspected, null) && (Inspected == null || !Inspected.Alive)) Inspect(null);

        // マウスでもタッチでも動くよう、いちばん最近使ったポインター（マウス・タッチ・ペン）で読む
        Pointer pointer = Pointer.current;
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;
        bool cancel = (kb != null && kb.escapeKey.wasPressedThisFrame) || (mouse != null && ReferenceEquals(pointer, mouse) && mouse.rightButton.wasPressedThisFrame);
        if (pointer == null)
        {
            battle.Highlight.Clear();
            return;
        }

        Vector2 screen = pointer.position.ReadValue();
        bool pressed = pressAction != null ? pressAction.WasPressedThisFrame() : pointer.press.wasPressedThisFrame;
        bool released = pressAction != null ? pressAction.WasReleasedThisFrame() || !pressAction.IsPressed() : !pointer.press.isPressed;
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
            else if (released)
            {
                // 離したフレームを取りこぼしても（ウィンドウの切り替えなど）、押されていなければ離したとみなす
                SquadMember member = Dragging;
                CancelDrag();
                if (valid) battle.Deploy(member, cell);
                // マップに出さずにカードの上（UI の上）で離したら、カードを選んだままにして、次に押したマスに置く
                else if (overUi) Arm(member);
            }
            return;
        }

        if (Armed != null)
        {
            if (!Armed.Ready || !battle.CanAfford(Armed))
            {
                Disarm();
            }
            else
            {
                // マウスなら指しているマスに射程を出す（タッチは指していないので、置けるマスだけ）
                bool hover = ReferenceEquals(pointer, mouse) && !(hud != null && hud.IsOverUI(screen)) && battle.Map.InBounds(cell);
                bool hoverValid = hover && battle.CanPlace(Armed, cell);
                battle.Highlight.Show(
                    battle.PlaceableCells(Armed),
                    hoverValid ? Combat.CellsInRange(battle.Map, cell, Armed.Data.stats.range) : null,
                    hover ? cell : (Vector2Int?)null,
                    hoverValid);
                if (cancel)
                {
                    Disarm();
                }
                else if (pressed && (hud == null || !hud.IsOverUI(screen)))
                {
                    SquadMember member = Armed;
                    Disarm();
                    if (battle.CanPlace(member, cell)) battle.Deploy(member, cell);
                }
                return;
            }
        }

        if (cancel)
        {
            Select(null);
            Inspect(null);
        }
        if (pressed && (hud == null || !hud.IsOverUI(screen)))
        {
            // 部員がいればその部員を、いなければ敵を選ぶ（どちらか一方の札だけが出る）
            Character c = battle.CharacterAt(cell);
            Inspect(c == null ? EnemyAt(battle, world) : null);
            Select(c);
        }

        if (Selected != null) battle.Highlight.Show(null, Combat.CellsInRange(battle.Map, Selected.Cell, Selected.Range), null, true);
        else battle.Highlight.Clear();
    }

    /// <summary>カードを選んだままにする（次に押したマスに置く）</summary>
    private void Arm(SquadMember member)
    {
        Armed = member;
        Select(null);
        Inspect(null);
        TimeController.SetFocus(true);
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
