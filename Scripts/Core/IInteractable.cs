using Godot;
using System;

public interface IInteractable
{
	void Interact(ItemData currentItem); // ЛКМ клик (Установка/Использование)
	void InteractContinuous(ItemData currentItem, double delta); // ЛКМ удержание (Откручивание/Закручивание)
	void InteractAlt(ItemData currentItem); // ПКМ клик (Подбор/Снятие)
	string GetInteractPrompt();
	bool CanInteractWith(ItemData item);
}
