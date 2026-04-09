using Godot;
using System;

[Tool]
[GlobalClass]
public partial class PartItemData : ItemData
{
	[Export] public float Size { get; set; } = 16.0f;
	[Export] public float RequiredTorque { get; set; } = 120.0f;
    [Export] public bool IsNewConsumable { get; set; } = true;
	
	[ExportGroup("Звуки Детали")]
    [Export] public AudioStream InstallSound { get; set; } // Звук установки в слот
    [Export] public AudioStream RemoveSound { get; set; }  // Звук извлечения
    [Export] public AudioStream TorqueSound { get; set; }  // Звук трещотки/щелчка при закручивании
}
