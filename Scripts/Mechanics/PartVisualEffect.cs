using Godot;
using System;

public enum EffectRenderMode { None, ColorOverlay, TextureOverlay }

[Tool]
[GlobalClass]
public partial class PartVisualEffect : Resource
{
    [Export] public PartState State { get; set; } = PartState.Rusted;
    [Export] public EffectRenderMode Mode { get; set; } = EffectRenderMode.ColorOverlay;
    [Export] public Color OverlayColor { get; set; } = new Color(0.6f, 0.25f, 0.05f, 0.85f);
    [Export] public Texture2D OverlayTexture { get; set; }

    // --- ОПТИМИЗАЦИЯ: КЭШИРОВАНИЕ ---
    private Material _cachedMaterial;

    public Material GetMaterial()
    {
        // Если материал уже был создан ранее, просто возвращаем ссылку на него (0 нагрузки на ПК)
        if (_cachedMaterial != null) return _cachedMaterial;
        if (Mode == EffectRenderMode.None) return null;

        var mat = new StandardMaterial3D();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

        if (Mode == EffectRenderMode.ColorOverlay)
        {
            mat.AlbedoColor = OverlayColor;
            mat.Metallic = 0.1f;
            mat.Roughness = 1.0f;
        }
        else if (Mode == EffectRenderMode.TextureOverlay && OverlayTexture != null)
        {
            mat.AlbedoTexture = OverlayTexture;
        }

        _cachedMaterial = mat;
        return _cachedMaterial;
    }
}
