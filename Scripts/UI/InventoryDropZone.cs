using Godot;

public partial class InventoryDropZone : Control
{
    public override void _Ready()
    {
        // Растягиваем на весь экран
        SetAnchorsPreset(LayoutPreset.FullRect);
        // Ставим подложку в самый низ иерархии внутри родителя
        ShowBehindParent = true;
        // Важно: ловим мышь, но пропускаем клики, если это не Drag-and-Drop
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.Int;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (UIManager.Instance?.SndItemDrop != null) 
            AudioManager.Instance?.PlayStream(UIManager.Instance.SndItemDrop);

        int slotIndex = (int)data;
        var player = GetTree().GetFirstNodeInGroup("Player") as Player;
        if (player != null)
        {
            player.DropItemFromIndex(slotIndex);
            UIManager.Instance?.ShowItemDetails(null);
        }
    }
}