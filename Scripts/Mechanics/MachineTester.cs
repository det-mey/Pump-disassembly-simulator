using Godot;

[Tool]
[GlobalClass]
public partial class MachineTester : BasePart
{
    [ExportGroup("Проверка герметичности")]
    // Сюда в инспекторе перетащите все прокладки (O-Rings, Gaskets) насоса
    [Export] public Godot.Collections.Array<Fastener> RequiredSeals { get; set; } = new Godot.Collections.Array<Fastener>();

    public override void Interact(ItemData currentItem)
    {
        bool hasLeak = false;

        foreach (var seal in RequiredSeals)
        {
            if (seal == null || !seal.IsInstalled || seal.IsDefective)
            {
                hasLeak = true;
                string reason = !seal.IsInstalled ? "отсутствует" : "изношено";
                
                // --- ИСПРАВЛЕНИЕ: Добавлены isAccident=true и notifyPlayer=true ---
                ActionLogger.Instance?.AddPenalty($"Утечка! Уплотнение [{seal?.PartName}] {reason}.", 5, true, true);
                
                SpawnLeak(seal.GlobalPosition); 
            }
        }

        if (hasLeak)
        {
            UIManager.Instance?.UpdatePrompt("[КРИТИЧЕСКАЯ ОШИБКА] Насос дал течь при тестовом запуске!", true);
            // Метод LogSystemEvent теперь доступен через наш враппер выше
            ActionLogger.Instance?.LogSystemEvent("TEST_RUN", "FAILED - LEAK DETECTED");
        }
        else
        {
            UIManager.Instance?.UpdatePrompt("[УСПЕХ] Тестовый запуск прошел штатно.", false);
            ActionLogger.Instance?.LogSystemEvent("TEST_RUN", "SUCCESS");
        }
    }

    private void SpawnLeak(Vector3 pos)
    {
        var particles = new CpuParticles3D { Emitting = true, OneShot = false, Amount = 100, Lifetime = 2.0f, Explosiveness = 0.1f };
        var mat = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.1f, 0.8f, 0.6f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        particles.MaterialOverride = mat;
        particles.Mesh = new SphereMesh { Radius = 0.05f };
        GetTree().Root.AddChild(particles);
        particles.GlobalPosition = pos;
    }
}