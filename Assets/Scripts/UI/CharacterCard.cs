using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 部員のカード（アイコン・コスト・職業）。編成バーと編成画面で使う。大きさは USS のクラスで決める。
/// アイコンがなければ、名前の頭文字を入れた色付きの板になる。
/// </summary>
public class CharacterCard : ChamferBox
{
    public readonly CharacterData Data;
    public readonly ClassData Class;
    public readonly Color ClassColor;
    public readonly Label Cost;

    public CharacterCard(CharacterData data, ClassData classData)
    {
        Data = data;
        Class = classData;
        ClassColor = Palette.Hex(classData.color, Palette.Amber);
        AddToClassList("char-card");
        SetFill(Palette.UiPanel).SetStroke(Palette.WithAlpha(ClassColor, 0.75f), 1.5f)
            .SetCut(14f, Corners.TopRight | Corners.BottomLeft);
        pickingMode = PickingMode.Position;

        var portrait = UIFactory.Box("card-portrait");
        Sprite icon = Art.Icon(data.id);
        if (icon != null)
        {
            portrait.style.backgroundImage = new StyleBackground(icon);
        }
        else
        {
            portrait.AddToClassList("card-fallback");
            portrait.style.backgroundColor = Palette.Hex(data.color, ClassColor);
            portrait.Add(UIFactory.Text(data.Initial, UIFont.Title, "card-initial"));
        }
        Add(portrait);

        var cls = UIFactory.Text(classData.name, UIFont.Body, "card-class");
        cls.style.backgroundColor = Palette.WithAlpha(ClassColor, 0.92f);
        Add(cls);

        Cost = UIFactory.Text(data.cost.ToString(), UIFont.Display, "card-cost");
        Add(Cost);
    }
}
