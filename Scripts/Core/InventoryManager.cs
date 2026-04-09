using Godot;
using System;

public partial class InventoryManager : Node
{
    public static InventoryManager Instance { get; private set; }

    public const int TotalSlots = 20;
    public const int HotbarSize = 5;
    
    public ItemData[] Items { get; private set; } = new ItemData[TotalSlots];
    public bool[] IsNewItem { get; private set; } = new bool[TotalSlots];
    
    public int ActiveSlotIndex { get; private set; } = 0;

    public event Action<int, ItemData> OnActiveSlotChanged;
    public event Action OnInventoryUpdated;

    public override void _EnterTree() => Instance = this;

    public void SetActiveSlot(int index)
    {
        if (index < 0) index = HotbarSize - 1;
        if (index >= HotbarSize) index = 0;
        ActiveSlotIndex = index;
		MarkItemSeen(ActiveSlotIndex); 
        OnActiveSlotChanged?.Invoke(ActiveSlotIndex, Items[ActiveSlotIndex]);
    }

    public void NextSlot() => SetActiveSlot(ActiveSlotIndex + 1);
    public void PrevSlot() => SetActiveSlot(ActiveSlotIndex - 1);
    public ItemData GetActiveItem() => Items[ActiveSlotIndex];

	public bool AddItem(ItemData item)
    {
        // Если попадает сразу в руки (активный слот) - НЕ ПОМЕЧАЕМ КАК НОВЫЙ
        if (Items[ActiveSlotIndex] == null)
        {
            Items[ActiveSlotIndex] = item;
            IsNewItem[ActiveSlotIndex] = false; // <--- ИСПРАВЛЕНИЕ
            OnActiveSlotChanged?.Invoke(ActiveSlotIndex, item);
            OnInventoryUpdated?.Invoke();
            return true;
        }

        // Если падает в рюкзак - помечаем
        for (int i = 0; i < TotalSlots; i++)
        {
            if (Items[i] == null)
            {
                Items[i] = item;
                IsNewItem[i] = true;
                OnInventoryUpdated?.Invoke();
                return true;
            }
        }
        return false;
    }

    public void RemoveActiveItem()
    {
        Items[ActiveSlotIndex] = null;
        IsNewItem[ActiveSlotIndex] = false;
        SetActiveSlot(ActiveSlotIndex);
        OnInventoryUpdated?.Invoke();
    }

    public void SwapItems(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;

        var tempItem = Items[fromIndex];
        Items[fromIndex] = Items[toIndex];
        Items[toIndex] = tempItem;

        var tempNew = IsNewItem[fromIndex];
        IsNewItem[fromIndex] = IsNewItem[toIndex];
        IsNewItem[toIndex] = tempNew;

        OnInventoryUpdated?.Invoke();
        
        if (fromIndex == ActiveSlotIndex || toIndex == ActiveSlotIndex)
            OnActiveSlotChanged?.Invoke(ActiveSlotIndex, Items[ActiveSlotIndex]);
    }

    // Метод для снятия метки "Новый" при клике
    public void MarkItemSeen(int index)
    {
        if (IsNewItem[index])
        {
            IsNewItem[index] = false;
            OnInventoryUpdated?.Invoke();
        }
    }

    public void ClearInventory()
    {
        for (int i = 0; i < TotalSlots; i++)
        {
            Items[i] = null;
            IsNewItem[i] = false;
        }
        ActiveSlotIndex = 0;

        // Принудительное обновление UI
        OnInventoryUpdated?.Invoke();
        OnActiveSlotChanged?.Invoke(ActiveSlotIndex, null);
    }

    public void RemoveItemAtIndex(int index)
    {
        if (index < 0 || index >= TotalSlots) return;
        
        Items[index] = null;
        IsNewItem[index] = false;
        
        // Если мы выбросили то, что было в руках - обновляем визуал рук
        if (index == ActiveSlotIndex)
        {
            SetActiveSlot(ActiveSlotIndex);
        }
        
        // Оповещаем UI, чтобы иконка исчезла
        OnInventoryUpdated?.Invoke();
    }
}
