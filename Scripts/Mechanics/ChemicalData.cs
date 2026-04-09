using Godot;
using System;

[Tool]
[GlobalClass]
public partial class ChemicalData : ItemData
{
	[ExportGroup("Эффекты химии")]
	[Export] public Godot.Collections.Array<PartState> RemovesStates { get; set; } = new Godot.Collections.Array<PartState>();
	[Export] public Godot.Collections.Array<PartState> AddsStates { get; set; } = new Godot.Collections.Array<PartState>();
	
	[Export] public Color ParticleColor { get; set; } = new Color(1, 1, 0.5f); // Цвет пшика (для визуала в будущем)

	// --- ОПТИМИЗАЦИЯ ---
    private StandardMaterial3D _cachedParticleMat;

    public StandardMaterial3D GetParticleMaterial()
    {
        if (_cachedParticleMat != null) return _cachedParticleMat;
        
        _cachedParticleMat = new StandardMaterial3D { 
            AlbedoColor = ParticleColor, 
            EmissionEnabled = true, 
            Emission = ParticleColor * 2.0f 
        };
        return _cachedParticleMat;
    }
}
