using Godot;

[Tool] // Атрибут Tool позволяет скрипту работать прямо в редакторе!
[GlobalClass]
public partial class BasePart : StaticBody3D, IInteractable
{
    [ExportGroup("Настройки UI и Взаимодействия")]
    [Export] public bool IsInteractable { get; set; } = true;       // Можно ли вообще кликать?
    [Export] public bool ShowTooltipName { get; set; } = true;      // Показывать ли имя при наведении?
    [Export] public bool ShowTooltipDesc { get; set; } = true;      // Показывать ли описание?
    [Export] public bool ShowInteractPrompt { get; set; } = true;   // Показывать ли подсказку [ЛКМ/ПКМ]?
    [Export] public bool AllowMinimalPromptInExam { get; set; } = false; // Разрешить ли короткую подсказку в режиме экзамена

    [ExportGroup("Базовые параметры")]
    [Export] public string PartId { get; set; } = "part_unique_id";
    [Export] public string PartName { get; set; } = ""; // Оставляем пустым, заполнится из ресурса
    [Export(PropertyHint.MultilineText)] public string PartDescription { get; set; } = "";
    [ExportGroup("Состояния (Атрибуты)")]
    [Export] public Godot.Collections.Array<PartState> CurrentStates { get; set; } = new Godot.Collections.Array<PartState>();

    public virtual void Interact(ItemData currentItem) { HandleChemicals(currentItem); }
    public virtual void InteractContinuous(ItemData currentItem, double delta) { }
    public virtual void InteractAlt(ItemData currentItem) { }
    public virtual bool CanInteractWith(ItemData item)
    {
        // По умолчанию базовая деталь реагирует только на химию
        if (item is ChemicalData) return true;
        return false;
    }
    
    public virtual string GetInteractPrompt() => ShowInteractPrompt ? PartName : "";

    public bool HasState(PartState state) => CurrentStates.Contains(state);

    public override void _Ready()
    {
        AddToGroup("AllParts"); 
    }

    public void AddState(PartState state)
    {
        if (!CurrentStates.Contains(state)) { CurrentStates.Add(state); OnStateChanged(); }
    }

    public void RemoveState(PartState state)
    {
        if (CurrentStates.Contains(state)) { CurrentStates.Remove(state); OnStateChanged(); }
    }

    protected bool HandleChemicals(ItemData item)
    {
        if (item is ChemicalData chem)
        {
            // Логируем попытку применения химии
            ActionLogger.Instance.LogEvent(LogType.Action, 
                $"Попытка применить {chem.ItemName} к {PartName}", 
                tool: chem.ItemName, target: PartName);

            bool effectApplied = false;
            foreach (var stateToRemove in chem.RemovesStates)
            {
                if (HasState(stateToRemove)) { RemoveState(stateToRemove); effectApplied = true; }
            }

            if (effectApplied)
            {
                foreach (var stateToAdd in chem.AddsStates) AddState(stateToAdd);
                SpawnParticles(chem.ParticleColor); 
                
                // --- Тихий режим для тренировки/экзамена ---
                if (GameManager.Instance.CurrentMode == SimulatorMode.Learning)
                {
                    UIManager.Instance?.UpdatePrompt($"Применено: {chem.ItemName}", false);
                }

                if (this is Fastener f) f.UpdateVisuals();
                return true;
            }
        }
        return false;
    }

    protected void SpawnParticles(Color color)
    {
        if (Engine.IsEditorHint()) return; 
        
        var particles = new CpuParticles3D { Emitting = true, OneShot = true, Amount = 40, Lifetime = 0.4f, Explosiveness = 0.9f, Spread = 20f, InitialVelocityMin = 2f, InitialVelocityMax = 5f };
        var mat = new StandardMaterial3D { AlbedoColor = color, EmissionEnabled = true, Emission = color * 2.0f };
        particles.MaterialOverride = mat;
        particles.Mesh = new SphereMesh { Radius = 0.02f, Height = 0.04f };

        GetTree().Root.AddChild(particles);
        var cam = GetViewport().GetCamera3D();
        if (cam != null)
        {
            particles.GlobalPosition = cam.GlobalPosition - cam.GlobalBasis.Z * 0.5f;
            particles.LookAt(GlobalPosition);
        }
        GetTree().CreateTimer(1.0f).Timeout += () => particles.QueueFree();
    }

    protected virtual void OnStateChanged() { }
}