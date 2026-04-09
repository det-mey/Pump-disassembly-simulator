using Godot;
using System;

public partial class InventorySlot : Panel
{
    public int SlotIndex { get; set; }
    private TextureRect _iconRect;
    private Panel _newIndicator; // Желтый кружок
	private Panel _selectionBorder;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(64, 64);
        MouseFilter = MouseFilterEnum.Stop; 

        var slotStyle = new StyleBoxFlat {
            BgColor = new Color(0.15f, 0.15f, 0.15f, 1.0f), // Alpha изменена на 1.0f
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10
        };
        AddThemeStyleboxOverride("panel", slotStyle);

        _iconRect = new TextureRect();
        _iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        
        _iconRect.SetAnchorsPreset(LayoutPreset.FullRect);
        _iconRect.OffsetLeft = 8; _iconRect.OffsetTop = 8;
        _iconRect.OffsetRight = -8; _iconRect.OffsetBottom = -8;
        _iconRect.MouseFilter = MouseFilterEnum.Ignore; 
        
        // --- ЖЕЛТЫЙ ИНДИКАТОР НОВОГО ПРЕДМЕТА ---
        _newIndicator = new Panel();
        var style = new StyleBoxFlat {
            BgColor = Colors.Yellow,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10
        };
        _newIndicator.AddThemeStyleboxOverride("panel", style);
        _newIndicator.CustomMinimumSize = new Vector2(14, 14);
        _newIndicator.SetAnchorsPreset(LayoutPreset.TopRight);
        _newIndicator.Position = new Vector2(-18, 4);
        _newIndicator.MouseFilter = MouseFilterEnum.Ignore;
        _newIndicator.Visible = false;

        // --- ОБВОДКА ВЫБРАННОГО СЛОТА ---
        _selectionBorder = new Panel();
        var borderStyle = new StyleBoxFlat {
            BgColor = Colors.Transparent, // Прозрачный фон
            BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
            BorderColor = new Color(0.2f, 0.8f, 0.2f, 1f), // Зеленая обводка
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10
        };
        _selectionBorder.AddThemeStyleboxOverride("panel", borderStyle);
        _selectionBorder.SetAnchorsPreset(LayoutPreset.FullRect);
        _selectionBorder.MouseFilter = MouseFilterEnum.Ignore;
        _selectionBorder.Visible = false;

        AddChild(_iconRect);
        AddChild(_newIndicator);
        AddChild(_selectionBorder);
    }

    public void SetActiveStatus(bool isActive)
    {
        _selectionBorder.Visible = isActive;
        
        PivotOffset = CustomMinimumSize / 2; // Центр для масштабирования
        var tween = CreateTween();
        
        if (isActive)
        {
            ZIndex = 1; // Поверх остальных слотов
            tween.TweenProperty(this, "scale", new Vector2(1.1f, 1.1f), 0.15f)
                 .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        else
        {
            ZIndex = 0;
            tween.TweenProperty(this, "scale", Vector2.One, 0.15f);
        }
    }

    public void UpdateSlot(ItemData item, bool isNew)
    {
        bool wasEmpty = _iconRect.Texture == null;
        _iconRect.Texture = item?.Icon;
        _newIndicator.Visible = isNew && item != null; // Показываем кружок
        TooltipText = ""; 

        // Анимация плавного появления (Bounce effect) при добавлении предмета
        if (wasEmpty && item != null)
        {
            _iconRect.Scale = new Vector2(0.3f, 0.3f);
            _iconRect.PivotOffset = CustomMinimumSize / 2; // Центр слота
            
            var tween = CreateTween();
            tween.TweenProperty(_iconRect, "scale", Vector2.One, 0.4f)
                 .SetTrans(Tween.TransitionType.Back)
                 .SetEase(Tween.EaseType.Out);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
        {
             var item = InventoryManager.Instance.Items[SlotIndex];
             // Снимаем метку "Новый" при клике
             InventoryManager.Instance.MarkItemSeen(SlotIndex);
             UIManager.Instance?.ShowItemDetails(item);
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var item = InventoryManager.Instance.Items[SlotIndex];
        if (item == null) return default;

        // --- ЗВУК ВЗЯТИЯ ИКОНКИ ---
        if (UIManager.Instance?.SndItemDrag != null) 
            AudioManager.Instance?.PlayStream(UIManager.Instance.SndItemDrag);

        var preview = new TextureRect { Texture = item.Icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Size = new Vector2(50, 50), Position = new Vector2(-25, -25) };
        var c = new Control(); c.AddChild(preview); SetDragPreview(c);
        return SlotIndex; 
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => data.VariantType == Variant.Type.Int;
    
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        // --- ЗВУК БРОСАНИЯ ИКОНКИ ---
        if (UIManager.Instance?.SndItemDrop != null) 
            AudioManager.Instance?.PlayStream(UIManager.Instance.SndItemDrop);
            
        InventoryManager.Instance.SwapItems((int)data, SlotIndex);
    }
}
