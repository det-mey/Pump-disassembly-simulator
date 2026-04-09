using Godot;
using System;

[Tool]
[GlobalClass]
public partial class ItemData : Resource
{
    [Export] public string ItemName { get; set; } = "Предмет";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "Описание отсутствует.";
    [Export] public Texture2D Icon { get; set; }
    
    [Export] public float Weight { get; set; } = 1.0f;
    
    [Export] public PackedScene WorldPrefab { get; set; }
    [Export] public PackedScene HandPrefab { get; set; }
    
    [ExportGroup("Анимация")]
    // Вектор смещения (Например, Z: -0.2 — толчок вперед)
    [Export] public Vector3 AnimPosOffset { get; set; } = new Vector3(0, 0, -0.1f); 
    // Вектор вращения в градусах (Например, Z: 30 — поворот кисти ключом)
    [Export] public Vector3 AnimRotDegrees { get; set; } = new Vector3(0, 0, 0);
    [Export] public float AnimDuration { get; set; } = 0.15f; // Длительность одного движения

    [ExportGroup("Аудио")]
    [Export] public AudioStream UseSound { get; set; } // Звук работы (вжик шуруповерта, пшик WD-40)
    [Export] public AudioStream EquipSound { get; set; }  // Звук взятия в руки (например, шорох одежды)
    [Export] public AudioStream DropSound { get; set; }   // Звук выбрасывания
    [Export] public AudioStream PickupSound { get; set; } // Звук подбора с пола
}
