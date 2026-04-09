using Godot;
using Godot.Collections; 

public enum ContinuousAnimType { None, RotateX, RotateY, RotateZ }

[Tool]
[GlobalClass]
public partial class Fastener : BasePart
{
    // --- ДАННЫЕ И НАСТРОЙКИ ---
    [ExportGroup("Логика Инструментов")]
    [Export] public Array<InteractionRule> ValidTools { get; set; } = new Array<InteractionRule>();
    [Export] public InteractionType Type { get; set; } = InteractionType.Continuous;
    [Export] public PartItemData PartItemResource { get; set; }[ExportGroup("Жизненный Цикл (Lifecycle)")]
    [Export] public bool GiveItemOnRemoval { get; set; } = true;
    [Export] public bool LeaveGhostOnRemoval { get; set; } = true;
    
    private bool _startsAsGhost = false;
    [Export] public bool StartsAsGhost 
    { 
        get => _startsAsGhost; 
        set { _startsAsGhost = value; if (value) IsInstalled = false; UpdateVisuals(); } 
    }[ExportGroup("Параметры")]
    [Export] public float Size { get; set; } = 16.0f; 
    [Export] public float MaxTorque { get; set; } = 120.0f;
    [Export] public float CurrentTorque { get; set; } = 120.0f;
    
    [ExportGroup("Состояние")]
    private bool _isInstalled = true;
    [Export] public bool IsInstalled 
    { 
        get => _isInstalled; 
        set { _isInstalled = value; UpdateVisuals(); } 
    }[ExportGroup("Зависимости сборки/разборки")]
    [Export] public Array<Fastener> BlockingParts { get; set; } = new Array<Fastener>();
    [Export] public Array<Fastener> RequiredParts { get; set; } = new Array<Fastener>();[ExportGroup("Опасности (Hazards)")]
    [Export] public bool IsUnderPressure { get; set; } = false;
    [Export] public string ReliefValveId { get; set; } = "";[ExportGroup("Дефектовка и Износ")]
    [Export] public bool IsDefective { get; set; } = false;
    [Export] public bool IsMeasurable { get; set; } = false;
    [Export] public float CurrentWear { get; set; } = 44.8f;
    [Export] public float NominalWear { get; set; } = 45.0f;[ExportGroup("Визуальные Эффекты")]
    [Export] public Array<PartVisualEffect> VisualEffects { get; set; } = new Array<PartVisualEffect>();
    
    [ExportGroup("Анимация процесса (Continuous)")]
    [Export] public bool EnableProgressAnim { get; set; } = true;[ExportSubgroup("Вращение")]
    [Export] public Vector3 RotateAxis { get; set; } = new Vector3(0, 1, 0);
    [Export] public float MaxRotationDegrees { get; set; } = 1080.0f;
    [ExportSubgroup("Вкручивание (Смещение)")]
    [Export] public Vector3 TranslateAxis { get; set; } = new Vector3(0, -1, 0);
    [Export] public float MaxTranslateDistance { get; set; } = 0.0f;

    // --- ВНУТРЕННИЕ ПЕРЕМЕННЫЕ ---
    private bool _hasBeenRemoved = false;
    private bool _isTighteningTarget = true;
    private ulong _lastInteractFrame = 0;
    private bool _isActionLocked = false;
    private Basis _originalVisualsBasis;
    private Vector3 _originalVisualsPos;
    private bool _isTweening = false;
    private System.Collections.Generic.Dictionary<MeshInstance3D, Material> _originalMaterials = new System.Collections.Generic.Dictionary<MeshInstance3D, Material>();

    private void CacheOriginalMaterials(Node node)
    {
        if (node is MeshInstance3D mesh)
        {
            _originalMaterials[mesh] = mesh.MaterialOverride;
        }
        foreach (Node child in node.GetChildren())
        {
            CacheOriginalMaterials(child);
        }
    }

    // --- ИНИЦИАЛИЗАЦИЯ ---
    public override void _Ready()
    {
        base._Ready(); 
        AddToGroup("Fasteners");

        var visuals = GetNodeOrNull<Node3D>("VisualsRoot") ?? GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (visuals != null)
        {
            _originalVisualsBasis = visuals.Basis;
            _originalVisualsPos = visuals.Position;
        }

        // --- ИСПРАВЛЕНИЕ: Запоминаем материалы до любых изменений ---
        CacheOriginalMaterials(this);

        if (StartsAsGhost) { IsInstalled = false; CurrentTorque = 0; }

        InitializeVisualsCache();
        SynchronizeWithResource();
        EnsureDefaultRules();

        _isTighteningTarget = CurrentTorque < MaxTorque;

        UpdateVisuals();
        UpdateContinuousAnimation();
    }

    private void InitializeVisualsCache()
    {
        var visuals = GetNodeOrNull<Node3D>("VisualsRoot") ?? GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (visuals != null)
        {
            _originalVisualsBasis = visuals.Basis;
            _originalVisualsPos = visuals.Position;
        }
    }

    private void SynchronizeWithResource()
    {
        if (StartsAsGhost) { IsInstalled = false; CurrentTorque = 0; }
        if (PartItemResource != null)
        {
            if (string.IsNullOrEmpty(PartName) || PartName == "Деталь") PartName = PartItemResource.ItemName;
            if (string.IsNullOrEmpty(PartDescription) || PartDescription == "Описание детали.") PartDescription = PartItemResource.Description;
            if (Size <= 0.1f) Size = PartItemResource.Size;
        }
    }

    private void EnsureDefaultRules()
    {
        if (ValidTools.Count == 0 && Type == InteractionType.Continuous)
        {
            ValidTools.Add(new InteractionRule { AllowedTool = ToolCategory.Wrench, RequiredSize = Size });
        }
    }

    // --- ВИЗУАЛИЗАЦИЯ И ЭФФЕКТЫ ---
    public void UpdateVisuals()
    {
        var collider = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        
        // Включаем коллизию по умолчанию, чтобы не "терять" объект
        if (collider != null) collider.SetDeferred("disabled", false);

        if (IsInstalled)
        {
            // --- СОСТОЯНИЕ: УСТАНОВЛЕНО ---
            SetMeshesState(this, true, null); 
            CollisionLayer = 3; 
            CollisionMask = 1;  
        }
        else
        {
            // --- СОСТОЯНИЕ: СНЯТО / ПРИЗРАК ---
            
            // 1. Проверка на полное удаление (работает ТОЛЬКО в игре)
            // Если в игре деталь была снята И мы НЕ оставляем призрак
            if (!Engine.IsEditorHint() && _hasBeenRemoved && !LeaveGhostOnRemoval)
            {
                SetMeshesState(this, false, null); 
                CollisionLayer = 0; 
                CollisionMask = 0;
                if (collider != null) collider.SetDeferred("disabled", true);
                return; 
            }

            // 2. Логика отображения призрака (в редакторе или если LeaveGhostOnRemoval == true)
            bool showGhostsInGame = GameManager.Instance != null && GameManager.Instance.ShowGhosts;
            
            // В режиме тренировки/экзамена принудительно скрываем призрака в игре
            if (!Engine.IsEditorHint() && GameManager.Instance?.CurrentMode != SimulatorMode.Learning)
            {
                showGhostsInGame = false;
            }

            // В РЕДАКТОРЕ призраки видны ВСЕГДА, если IsInstalled = false
            bool finalShow = Engine.IsEditorHint() ? true : showGhostsInGame;

            Material ghostMat;
            if (Engine.IsEditorHint())
            {
                ghostMat = CreateEditorGhostMaterial();
            }
            else
            {
                ghostMat = GameManager.Instance?.GhostMaterial;
            }

            SetMeshesState(this, finalShow, finalShow ? ghostMat : null); 
            
            // Коллизия остается на слое 2, чтобы можно было нащупать место установки
            CollisionLayer = 2; 
            CollisionMask = 0;
        }
    }

    // Вспомогательный метод для создания материала в редакторе
    private Material CreateEditorGhostMaterial()
    {
        var mat = new StandardMaterial3D();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.AlbedoColor = new Color(0, 1, 0, 0.3f); // Зеленый полупрозрачный
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        return mat;
    }

    private void SetMeshesState(Node node, bool isVisible, Material mat)    
    {
        Material customOverlay = null;

        foreach (var effect in VisualEffects)
        {
            if (HasState(effect.State))
            {
                customOverlay = effect.GetMaterial(); 
                break; 
            }
        }

        if (node is MeshInstance3D mesh)
        {
            mesh.Visible = isVisible;
            
            // --- КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: ВОЗВРАТ РОДНОГО МАТЕРИАЛА ---
            if (mat != null) 
            {
                mesh.MaterialOverride = mat; // Ставим призрака
            }
            else 
            {
                // Возвращаем родной материал, если он был, иначе null
                mesh.MaterialOverride = _originalMaterials.ContainsKey(mesh) ? _originalMaterials[mesh] : null;
            }
            
            mesh.MaterialOverlay = customOverlay; 
        }
        
        foreach (Node child in node.GetChildren()) 
        {
            SetMeshesState(child, isVisible, mat);
        }
    }

    private void SetMeshesTransparency(Node node, float alpha)
    {
        if (node is GeometryInstance3D geom) geom.Transparency = alpha;
        foreach (Node child in node.GetChildren()) SetMeshesTransparency(child, alpha);
    }

    private void TweenMeshesTransparency(Tween tween, Node node, float targetAlpha, float duration)
    {
        if (node is GeometryInstance3D geom) tween.TweenProperty(geom, "transparency", targetAlpha, duration);
        foreach (Node child in node.GetChildren()) TweenMeshesTransparency(tween, child, targetAlpha, duration);
    }

    private void AnimateVisuals(bool isInstalling)
    {
        var visuals = GetNodeOrNull<Node3D>("VisualsRoot") ?? GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        var cam = GetViewport().GetCamera3D();
        
        if (visuals == null || cam == null) { UpdateVisuals(); return; }

        _isTweening = true; 
        Vector3 directionToPlayer = (cam.GlobalPosition - GlobalPosition).Normalized();
        Vector3 pullOffset = visuals.ToLocal(GlobalPosition + directionToPlayer * 0.2f);

        var tween = CreateTween();
        tween.SetParallel(true); 

        if (isInstalling)
        {
            visuals.Position = pullOffset;
            UpdateVisuals(); 
            SetMeshesTransparency(visuals, 1.0f); 
            
            tween.TweenProperty(visuals, "position", Vector3.Zero, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            TweenMeshesTransparency(tween, visuals, 0.0f, 0.3f); 
            
            // Завершение установки
            tween.Chain().TweenCallback(Callable.From(() => {
                _isTweening = false;
                UpdateContinuousAnimation(); 
            }));
        }
        else
        {
            visuals.Position = Vector3.Zero;
            
            tween.TweenProperty(visuals, "position", pullOffset, 0.2f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            TweenMeshesTransparency(tween, visuals, 1.0f, 0.2f); 
            
            // Завершение снятия
            tween.Chain().TweenCallback(Callable.From(() => {
                _isTweening = false;
                visuals.Position = Vector3.Zero; 
                SetMeshesTransparency(visuals, 0.0f); 
                UpdateVisuals();
            }));
        }
    }

    private void UpdateContinuousAnimation()
    {
        if (Type != InteractionType.Continuous || !EnableProgressAnim || _isTweening) return;

        Node3D visuals = GetNodeOrNull<Node3D>("VisualsRoot") ?? GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (visuals == null) return;

        float progress = MaxTorque > 0 ? CurrentTorque / MaxTorque : 0;
        Vector3 targetPos = _originalVisualsPos;

        if (MaxTranslateDistance != 0) targetPos += TranslateAxis.Normalized() * (MaxTranslateDistance * progress);

        if (MaxRotationDegrees != 0 && RotateAxis != Vector3.Zero)
        {
            float angleRad = Mathf.DegToRad(MaxRotationDegrees * progress);
            Basis rotatedBasis = _originalVisualsBasis * new Basis(RotateAxis.Normalized(), angleRad);
            visuals.Transform = new Transform3D(rotatedBasis, targetPos);
        }
        else
        {
            visuals.Position = targetPos;
        }
    }

    // --- ЛОГИКА ВЗАИМОДЕЙСТВИЯ И ПРОВЕРОК ---
    private bool IsToolValid(ToolData tool, out float speedMultiplier)
    {
        speedMultiplier = 1.0f;
        if (tool == null) return false;

        foreach (var rule in ValidTools)
        {
            if (rule.AllowedTool == tool.Category)
            {
                if (rule.IgnoreSize || Mathf.Abs(tool.Size - rule.RequiredSize) <= rule.SizeTolerance)
                {
                    speedMultiplier = tool.Efficiency;
                    return true;
                }
            }
        }
        return false;
    }

    private bool CheckDependencies(bool isInstalling)
    {
        if (isInstalling)
        {
            foreach (var part in RequiredParts)
            {
                if (part != null && !part.IsInstalled)
                {
                    UIManager.Instance?.UpdatePrompt($"[ОШИБКА] Сначала установите: {part.PartName}", true);
                    return false;
                }
            }
        }
        else
        {
            foreach (var part in BlockingParts)
            {
                if (part != null && part.IsInstalled)
                {
                    UIManager.Instance?.UpdatePrompt($"[БЛОКИРОВКА] Мешает: {part.PartName}", true);
                    return false;
                }
            }
        }
        return true;
    }

    // --- ИНТЕРФЕЙС IInteractable ---
    public override void Interact(ItemData currentItem)
    {
        string toolName = currentItem != null ? currentItem.ItemName : "Руки";

        // --- 1. ЛОГИРОВАНИЕ ПОПЫТКИ ---
        ActionLogger.Instance?.LogEvent(LogType.Action, 
            $"Взаимодействие: {toolName} -> {PartName}", 
            tool: toolName, target: PartName);

        // --- 2. ЗАМЕРЫ И ДЕФЕКТОВКА ---
        if (currentItem is ToolData measureTool && measureTool.Category == ToolCategory.Measurement && IsMeasurable)
        {
            ActionLogger.Instance?.LogEvent(LogType.Action, $"Замер детали: {PartName}", tool: toolName, target: PartName);
            
            string verdict = (Mathf.Abs(CurrentWear - NominalWear) > 0.5f) ? "ТРЕБУЕТ ЗАМЕНЫ" : "В НОРМЕ";
            // Результат замера на приборе показываем ВСЕГДА (это не подсказка, а данные с инструмента)
            UIManager.Instance?.UpdatePrompt($"Замер [{PartName}]: {CurrentWear} мм (Номинал: {NominalWear} мм) | {verdict}");
            return; 
        }

        // --- 3. ЛОГИКА УСТАНОВКИ (Если на месте призрака) ---
        if (!IsInstalled)
        {
            if (currentItem is PartItemData part)
            {
                // Проверка совместимости (Размер + Идентичность ресурса)
                bool sizeMatches = Mathf.Abs(part.Size - Size) < 0.1f;
                bool typeMatches = (PartItemResource == null) || (part == PartItemResource || part.ItemName == PartItemResource.ItemName);

                if (sizeMatches && typeMatches)
                {
                    if (!CheckDependencies(true)) return; // Проверка, поставлена ли прокладка и т.д.
                    
                    // Звук установки из конфига детали
                    if (PartItemResource?.InstallSound != null)
                        AudioManager.Instance?.PlayStream3D(PartItemResource.InstallSound, GlobalPosition, 0f, (float)GD.RandRange(0.9, 1.1));

                    _isInstalled = true; // Используем поле, чтобы не вызвать UpdateVisuals раньше времени
                    IsDefective = !part.IsNewConsumable; // Если поставили старую деталь, помечаем износ
                    _hasBeenRemoved = false; 
                    CurrentTorque = (Type == InteractionType.Instant) ? MaxTorque : 0; 
                    
                    InventoryManager.Instance.RemoveActiveItem();
                    
                    AnimateVisuals(true);
                    UpdateVisuals();
                    UpdateContinuousAnimation();

                    ActionLogger.Instance?.UpdatePartState(PartId, "Installed", PartName);
                    ActionLogger.Instance?.LogEvent(LogType.Action, $"Успешная установка: {PartName}", toolName, PartName);

                    // В Обучении пишем успех, в Экзамене - тишина
                    if (GameManager.Instance.CurrentMode == SimulatorMode.Learning)
                        UIManager.Instance?.UpdatePrompt($"{PartName} установлена.", false);
                }
                else 
                {
                    string reason = !typeMatches ? $"Неверный тип (нужна {PartName})" : $"Неверный размер (нужен {Size})";
                    ActionLogger.Instance?.LogEvent(LogType.Warning, $"Ошибка установки {PartName}", toolName, PartName, tech: reason);
                    
                    if (GameManager.Instance.CurrentMode == SimulatorMode.Learning)
                        UIManager.Instance?.UpdatePrompt(reason, true);
                }
            }
            return;
        }

        // --- 4. ХИМИЯ (WD-40 и др.) ---
        if (HandleChemicals(currentItem)) return;

        // --- 5. СНЯТИЕ БЫСТРОСЪЕМНЫХ ДЕТАЛЕЙ (Instant) ---
        if (Type == InteractionType.Instant)
        {
            if (HasState(PartState.Rusted)) 
            { 
                ActionLogger.Instance?.LogEvent(LogType.Warning, "Попытка снять заржавевшую деталь", toolName, PartName);
                if (GameManager.Instance.CurrentMode == SimulatorMode.Learning)
                    UIManager.Instance?.UpdatePrompt("ЗАРЖАВЕЛО! Требуется обработка.", true); 
                return; 
            }

            // Проверяем, подходит ли инструмент (например, лом для крышки)
            if (currentItem is ToolData removeTool && IsToolValid(removeTool, out _))
            {
                RemovePart();
            }
            else if (ValidTools.Count > 0)
            {
                ActionLogger.Instance?.LogEvent(LogType.Warning, "Попытка снять без спец. инструмента", toolName, PartName);
                if (GameManager.Instance.CurrentMode == SimulatorMode.Learning)
                    UIManager.Instance?.UpdatePrompt("Нужен специальный инструмент!", true);
            }
        }
    }

    private bool HandleMeasurement(ItemData currentItem)
    {
        if (currentItem is ToolData measureTool && measureTool.Category == ToolCategory.Measurement && IsMeasurable)
        {
            ActionLogger.Instance?.LogAction(PartId, "Measured");
            string verdict = (Mathf.Abs(CurrentWear - NominalWear) > 0.5f) ? "ТРЕБУЕТ ЗАМЕНЫ" : "В НОРМЕ";
            UIManager.Instance?.UpdatePrompt($"Замер [{PartName}]: {CurrentWear} мм (Номинал: {NominalWear} мм) | {verdict}");
            return true; 
        }
        return false;
    }

    private void TryInstallPart(ItemData currentItem)
    {
        if (currentItem == null) return; 
        string toolName = currentItem.ItemName;

        if (currentItem is PartItemData part)
        {
            bool sizeMatches = Mathf.Abs(part.Size - Size) < 0.1f;
            bool typeMatches = (PartItemResource == null) || (part == PartItemResource || part.ItemName == PartItemResource.ItemName);

            if (sizeMatches && typeMatches)
            {
                if (!CheckDependencies(true)) return;

				if (PartItemResource != null && PartItemResource.InstallSound != null)
                    AudioManager.Instance?.PlayStream3D(PartItemResource.InstallSound, GlobalPosition, 0f, (float)GD.RandRange(0.9, 1.1));

                IsInstalled = true;
                IsDefective = !part.IsNewConsumable; 
                _hasBeenRemoved = false; 
                CurrentTorque = (Type == InteractionType.Instant) ? MaxTorque : 0; 
                InventoryManager.Instance.RemoveActiveItem();
                
                AnimateVisuals(true);
                UpdateVisuals();
                UpdateContinuousAnimation();

                ActionLogger.Instance.LogEvent(LogType.Action, $"Установка детали: {PartName}", toolName, PartName);
            }
            else 
            {
                // ЛОГИРУЕМ НЕУДАЧНУЮ ПОПЫТКУ УСТАНОВКИ
                string reason = !typeMatches ? $"Неверный тип (нужна {PartName})" : $"Неверный размер (нужен {Size})";
                ActionLogger.Instance.LogEvent(LogType.Warning, 
                    $"Неудачная попытка установки {PartName}", 
                    toolName, PartName, tech: reason);
                
                if (GameManager.Instance.CurrentMode == SimulatorMode.Learning)
                    UIManager.Instance?.UpdatePrompt(reason, true);
            }
        }
    }

    private void TryRemoveInstantPart(ItemData currentItem)
    {
        if (HasState(PartState.Rusted)) { UIManager.Instance?.UpdatePrompt("ЗАРЖАВЕЛО!", true); return; }

        if (currentItem is ToolData removeTool && IsToolValid(removeTool, out _))
        {
            RemovePart();
        }
        else if (ValidTools.Count > 0)
        {
            UIManager.Instance?.UpdatePrompt("Нужен специальный инструмент!", true);
        }
    }

    public override void InteractContinuous(ItemData currentItem, double delta)
    {
        if (!IsInstalled || Type == InteractionType.Instant) return;
        
        string toolName = currentItem != null ? currentItem.ItemName : "Руки";

        // 1. Проверка на ржавчину
        if (HasState(PartState.Rusted)) 
        {
            if (Input.IsActionJustPressed("interact")) // Логируем только первый щелчок
                ActionLogger.Instance.LogEvent(LogType.Warning, $"Попытка крутить заржавевшую деталь {PartName}", toolName, PartName);
            return;
        }

        // 2. Проверка инструмента
        ToolData tool = currentItem as ToolData;
        if (!IsToolValid(tool, out float efficiency)) 
        {
            if (Input.IsActionJustPressed("interact")) // Записываем только момент нажатия
            {
                string reqTool = ValidTools.Count > 0 ? ValidTools[0].AllowedTool.ToString() : "Спец. инструмент";
                string reqSize = ValidTools.Count > 0 ? ValidTools[0].RequiredSize.ToString() : "Любой";
                
                ActionLogger.Instance.LogEvent(LogType.Warning, 
                    $"Попытка работы неверным инструментом по {PartName}", 
                    currentItem?.ItemName ?? "Руки", PartName, 
                    tech: $"Требуется: {reqTool} ({reqSize})");
            }
            return;
        }

        // 3. Проверка блокировок
        if (!_isTighteningTarget && !CheckDependencies(false)) 
        {
            if (Input.IsActionJustPressed("interact"))
                ActionLogger.Instance.LogEvent(LogType.Warning, $"Попытка открутить заблокированную деталь {PartName}", toolName, PartName);
            return;
        }

        // --- ЕСЛИ ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ - КРУТИМ ---
        ulong currentFrame = Engine.GetPhysicsFrames();
        if (currentFrame > _lastInteractFrame + 1) _isActionLocked = false;
        _lastInteractFrame = currentFrame;

        if (_isActionLocked) return;

        float finalSpeed = 80.0f * efficiency * (float)delta;

        if (_isTighteningTarget)
        {
            CurrentTorque += finalSpeed;
            if (CurrentTorque >= MaxTorque)
            {
                CurrentTorque = MaxTorque;
                _isTighteningTarget = false; 
                _isActionLocked = true; 
                ActionLogger.Instance.UpdatePartState(PartId, "Torqued", PartName);
                ActionLogger.Instance.LogEvent(LogType.Action, $"Деталь {PartName} полностью затянута", toolName, PartName);
                
                if (PartItemResource?.TorqueSound != null)
                    AudioManager.Instance.PlayStream3D(PartItemResource.TorqueSound, GlobalPosition);
            }
        }
        else
        {
            CurrentTorque -= finalSpeed;
            if (CurrentTorque <= 0)
            {
                CurrentTorque = 0;
                _isTighteningTarget = true; 
                _isActionLocked = true; 
                ActionLogger.Instance?.LogAction(PartId, "Loosened", PartName);
            }
        }

        UIManager.Instance?.UpdateTorqueUI(CurrentTorque, MaxTorque);
        UpdateContinuousAnimation();
    }

    public override void InteractAlt(ItemData currentItem)
    {
        if (!IsInstalled) return;

        bool canRemove = (Type == InteractionType.Continuous && CurrentTorque <= 0.1f) || 
                         (Type == InteractionType.Instant && ValidTools.Count == 0);

        if (canRemove)
        {
            RemovePart();
        }
        else
        {
            // Логируем, почему не получилось снять на ПКМ
            string reason = (Type == InteractionType.Continuous) ? "Деталь не откручена" : "Требуется инструмент (ЛКМ)";
            ActionLogger.Instance.LogEvent(LogType.Warning, $"Не удалось снять {PartName}: {reason}", "Руки", PartName);
        }
    }

    private void RemovePart()
    {
        // Проверка физических блокировок (например, не сняты болты сверху)
        if (!CheckDependencies(false)) return;

        // --- ТЕХНИКА БЕЗОПАСНОСТИ: ДАВЛЕНИЕ ---
        if (IsUnderPressure && !string.IsNullOrEmpty(ReliefValveId))
        {
            if (ActionLogger.Instance?.GetState(ReliefValveId) != "Removed") 
            {
                // Критическая ошибка: выброс жидкости/газа
                ActionLogger.Instance?.AddPenalty($"АВАРИЯ: Снятие {PartName} под давлением!", 25, true, true);
                SpawnParticles(new Color(0.8f, 0.1f, 0.1f)); // Аварийные красные брызги
            }
        }

        // Проверка места в инвентаре
        bool success = !GiveItemOnRemoval || InventoryManager.Instance.AddItem(PartItemResource);
        
        if (success)
        {
            // Звук снятия
            if (PartItemResource?.RemoveSound != null)
                AudioManager.Instance?.PlayStream3D(PartItemResource.RemoveSound, GlobalPosition, 0f, (float)GD.RandRange(0.9, 1.1));

            _isInstalled = false; 
            _hasBeenRemoved = true;
            
            // Регистрируем изменение в логах и сценарии
            ActionLogger.Instance?.UpdatePartState(PartId, "Removed", PartName);
            ActionLogger.Instance?.LogEvent(LogType.Action, $"Деталь демонтирована: {PartName}", "Руки", PartName);

            // Отключаем физику, чтобы деталь не мешала игроку во время анимации
            var collider = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
            if (collider != null) collider.SetDeferred("disabled", true);

            // Запускаем 3D анимацию отлета (AnimateVisuals сама вызовет UpdateVisuals в конце)
            AnimateVisuals(false);

            // Запускаем анимацию иконки в UI с задержкой (эффект "попадания в карман")
            if (GiveItemOnRemoval)
            {
                GetTree().CreateTimer(0.15f).Timeout += () => {
                    if (PartItemResource != null && PartItemResource.Icon != null)
                        UIManager.Instance?.AnimateItemPickup(PartItemResource.Icon);
                };
            }
        }
        else
        {
            ActionLogger.Instance?.LogEvent(LogType.Warning, "Инвентарь переполнен при попытке снять деталь", "Система", PartName);
            UIManager.Instance?.UpdatePrompt("Инвентарь полон!", true);
        }
    }

    public override string GetInteractPrompt()
    {
        if (!ShowInteractPrompt) return "";
        var mode = GameManager.Instance?.CurrentMode ?? SimulatorMode.Learning;

        // --- КРИТИЧЕСКОЕ ИЗМЕНЕНИЕ: Жесткая блокировка в Экзамене/Тренировке ---
        if (mode != SimulatorMode.Learning && !AllowMinimalPromptInExam)
        {
            return ""; // Никакого текста вообще
        }

        // Если мы здесь, значит либо режим Обучение, либо AllowMinimalPromptInExam == true
        if (!IsInstalled) 
        {
            if (_hasBeenRemoved && !LeaveGhostOnRemoval) return "";

            // В режиме Экзамена/Тренировки показываем только "сухой" технический текст
            if (mode != SimulatorMode.Learning) return "[ЛКМ] Установить деталь";

            foreach (var parts in RequiredParts)
                if (parts != null && !parts.IsInstalled) return $"[ЗАКРЫТО] Сначала: {parts.PartName}";
            
            return $"Место для: {PartName} ({Size})";
        }

        // Блокировка другими деталями (только в обучении или если разрешено)
        foreach (var part in BlockingParts)
            if (part != null && part.IsInstalled) return mode == SimulatorMode.Learning ? $"[ЗАКРЫТО] Мешает: {part.PartName}" : "";

        // Текст состояний
        if (HasState(PartState.Rusted) || HasState(PartState.Painted)) 
            return mode == SimulatorMode.Learning ? "[!] ТРЕБУЕТСЯ ОБРАБОТКА ДЕТАЛИ" : "Требуется обработка";
        
        if (Type == InteractionType.Instant) 
        {
            if (ValidTools.Count == 0) return mode == SimulatorMode.Learning ? $"[ПКМ] Снять {PartName}" : "Взаимодействие";
            else return mode == SimulatorMode.Learning ? $"[ПКМ] Снять | [ЛКМ] Использовать инструмент" : "Взаимодействие";
        }

        // Для резьбовых деталей
        if (mode != SimulatorMode.Learning) return "Взаимодействие";

        string actionStr = _isTighteningTarget ? "ЗАКРУТИТЬ" : "ОТКРУТИТЬ";
        if (CurrentTorque <= 0.1f) return $"[ПКМ] Забрать | [ЛКМ] {actionStr}";
        return $"[ЛКМ] {actionStr}";
    }

    public override bool CanInteractWith(ItemData item)
    {
        if (!IsInstalled) return item is PartItemData part && Mathf.Abs(part.Size - Size) < 0.1f;
        if (item is ChemicalData) return true;
        if (item is ToolData tool) return IsToolValid(tool, out _);
        return false;
    }
}