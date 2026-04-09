using Godot;
using System;

[Tool]
[GlobalClass]
public partial class ScenarioData : Resource
{
    [Export] public string ScenarioName { get; set; } = "Насос ЦНС-100";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "Разборка и дефектовка центробежного насоса.";
    [Export] public Texture2D PreviewImage { get; set; }
    
    // Путь к файлу сцены (.tscn), который нужно загрузить при выборе этого сценария
    [Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; }

    [Export(PropertyHint.File, "*.json")] public string SequenceJsonPath { get; set; }
    [Export] public float TimeLimitSeconds { get; set; } = 300.0f;  // 5 минут по умолчанию

}