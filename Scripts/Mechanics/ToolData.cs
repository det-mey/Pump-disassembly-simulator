using Godot;
using System;

[Tool]
[GlobalClass]
public partial class ToolData : ItemData
{
    [Export] public ToolCategory Category { get; set; } = ToolCategory.Wrench;
    [Export] public float Size { get; set; } = 16.0f; // Размер (мм, номер биты и т.д.)
    
    // Множитель скорости (1.0 = обычная, 5.0 = шуруповерт)
    [Export] public float Efficiency { get; set; } = 1.0f; 
}