using Godot;

[GlobalClass]
public partial class PickableItem : RigidBody3D, IInteractable
{
    [Export] public ItemData ItemResource { get; set; }

    public override void _Ready()
    {
        CollisionLayer = 2; 
        CollisionMask = 1; 
        
        ContactMonitor = true;
        MaxContactsReported = 1;
    }

    public void Interact(ItemData currentItem) { }
    public void InteractContinuous(ItemData currentItem, double delta) { }
    public bool CanInteractWith(ItemData item)
    {
        return false;
    }

    public void InteractAlt(ItemData currentItem)
    {
        if (ItemResource == null) return;

        if (ItemResource.PickupSound != null) 
            AudioManager.Instance?.PlayStream(ItemResource.PickupSound, -2f, (float)GD.RandRange(0.95, 1.05));

        if (InventoryManager.Instance.AddItem(ItemResource))
        {
            UIManager.Instance?.AnimateItemPickup(ItemResource.Icon);
            
            foreach (var child in GetChildren())
            {
                if (child is Node3D node3D) node3D.Visible = false;
            }
            
            CollisionLayer = 0; 
            GetTree().CreateTimer(0.6f).Timeout += QueueFree; 
        }
        else
        {
            UIManager.Instance?.UpdatePrompt("Инвентарь полон!", true);
        }
    }

    public string GetInteractPrompt()
    {
        return ItemResource != null ? $"[ПКМ] Подобрать: {ItemResource.ItemName}" : "";
    }
}