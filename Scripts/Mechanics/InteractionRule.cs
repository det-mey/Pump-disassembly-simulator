using Godot;
using System;

[GlobalClass]
public partial class InteractionRule : Resource
{
	[Export] public ToolCategory AllowedTool { get; set; } = ToolCategory.Wrench;
	[Export] public float RequiredSize { get; set; } = 16.0f;
	
	// Допуск по размеру (для ключей 0.1, для лома может быть 0 - любой размер)
	[Export] public float SizeTolerance { get; set; } = 0.1f; 
	
	// Если true, то размер инструмента игнорируется (например, любой лом подходит)
	[Export] public bool IgnoreSize { get; set; } = false;
}
