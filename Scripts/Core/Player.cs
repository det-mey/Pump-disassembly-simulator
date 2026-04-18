using Godot;

public partial class Player : CharacterBody3D
{
    [ExportGroup("Передвижение")][Export] public float WalkSpeed = 4.0f;
    [Export] public float SprintSpeed = 7.0f;
    [Export] public float CrouchSpeed = 2.0f;[Export] public float Acceleration = 8.0f;
    [Export] public float Friction = 10.0f;
    [Export] public float JumpVelocity = 4.5f;[ExportGroup("Взаимодействие")]
    [Export] public float InteractRange = 2.0f;

    private float _gravity;
    private Node3D _head;
    private CameraController _cameraController;
    private Camera3D _camera;
    private Node3D _handPosition;
    private RayCast3D _interactionRay;
    private Tween _toolActionTween;
    private AudioStreamPlayer _handAudioPlayer;

    private Node3D _currentHandMeshInstance;
    private bool _isInventoryOpen = false;
    private Vector3 _handDefaultPos;

    // VR-состояние
    private bool _isVRMode = false;

    // Кэш оптимизации UI
    private string _lastPromptText = "";
    private string _lastTooltipName = "";
    private string _lastTooltipDesc = "";
    private string _lastTooltipStates = "";

    private Vector2 _currentInputDir = Vector2.Zero;

    public override void _Ready()
    {
        _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

        _head = GetNode<Node3D>("Head");
        _cameraController = GetNode<CameraController>("Head/CameraController");
        _camera = GetNode<Camera3D>("Head/CameraController/XROrigin3D/XRCamera3D/Camera3D");
        _handPosition = GetNode<Node3D>("Head/CameraController/XROrigin3D/XRCamera3D/HandPosition");
        _interactionRay = GetNode<RayCast3D>("Head/CameraController/XROrigin3D/XRCamera3D/RayCast3D");

        _handAudioPlayer = new AudioStreamPlayer { Bus = "Master" };
        AddChild(_handAudioPlayer);

        _interactionRay.TargetPosition = new Vector3(0, 0, -InteractRange);

        _handDefaultPos = _handPosition.Position;

        // Проверяем VR-режим
        _isVRMode = VRManager.Instance?.IsVRMode == true;
        
        if (_isVRMode)
        {
            // В VR мышь захватывается системой, не нужно Captured
            Input.MouseMode = Input.MouseModeEnum.Visible;
            GD.Print("[Player] VR-режим обнаружен, управление через контроллеры");
        }
        else
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        InventoryManager.Instance.OnActiveSlotChanged += UpdateHandVisual;
        UpdateHandVisual(0, null);

        Callable.From(() => InventoryManager.Instance.SetActiveSlot(0)).CallDeferred();
    }

    public override void _ExitTree()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnActiveSlotChanged -= UpdateHandVisual;
        }
    }

    private void SwitchSlotWithSound(int index) {
        InventoryManager.Instance.SetActiveSlot(index);
        if (UIManager.Instance?.SndHotbarSwitch != null) AudioManager.Instance?.PlayStream(UIManager.Instance.SndHotbarSwitch);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("inventory_toggle"))
        {
            _isInventoryOpen = !_isInventoryOpen;
            Input.MouseMode = _isInventoryOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
            UIManager.Instance?.ToggleFullInventory(_isInventoryOpen);
        }

        if (_isInventoryOpen) return;

        // В VR управление камерой через XR-трекеры, мышь не используется
        if (!_isVRMode)
        {
            // ПРЯМОЕ УПРАВЛЕНИЕ УГЛАМИ (делегировано в CameraController)
            if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                _cameraController?.ProcessCameraRotation(mouseMotion);
            }
        }

        if (@event.IsActionPressed("slot_next")) { InventoryManager.Instance.NextSlot(); SwitchSlotWithSound(InventoryManager.Instance.ActiveSlotIndex); }
        if (@event.IsActionPressed("slot_prev")) { InventoryManager.Instance.PrevSlot(); SwitchSlotWithSound(InventoryManager.Instance.ActiveSlotIndex); }
        for (int i = 0; i < 5; i++) if (@event.IsActionPressed($"slot_{i+1}")) SwitchSlotWithSound(i);
        if (@event.IsActionPressed("drop_item")) DropItemFromIndex(InventoryManager.Instance.ActiveSlotIndex);
        
        if (@event.IsActionPressed("debug_toggle") || Input.IsKeyPressed(Key.F2))
        {
            GameManager.Instance.ToggleGhosts();
            UIManager.Instance?.UpdatePrompt("Режим призраков переключен", false);
        }
    }

    public override void _Process(double delta)
    {
        // Плавная анимация рук (устраняет дрожание)
        ProcessHandAnimation(delta);

        if (!_isInventoryOpen && !_isVRMode)
        {
            // В VR динамика камеры обрабатывается XR-трекерами
            bool isCrouching = Input.IsActionPressed("crouch");
            bool isSprinting = Input.IsActionPressed("sprint") && !isCrouching && IsOnFloor();
            ItemData activeItem = InventoryManager.Instance.GetActiveItem();
            float itemWeight = activeItem != null ? activeItem.Weight : 0.0f;
            float flatVelocity = new Vector2(Velocity.X, Velocity.Z).Length();
            
            _cameraController?.ProcessCameraDynamics(_currentInputDir, delta, flatVelocity, IsOnFloor(), isSprinting, isCrouching, itemWeight);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 currentVelocity = Velocity;
        
        // Запоминаем ввод для использования в рендере (_Process)
        _currentInputDir = _isInventoryOpen ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_backward");

        currentVelocity = ApplyGravityAndJump(currentVelocity, delta);
        currentVelocity = ApplyMovement(currentVelocity, _currentInputDir, delta);

        Velocity = currentVelocity;
        MoveAndSlide();

        if (!_isInventoryOpen)
        {
            HandleInteraction(delta);
        }
        else
        {
            ClearInteractionUI();
        }
    }

    // --- МОДУЛИ ФИЗИКИ И КАМЕРЫ ---

    private void ProcessHandAnimation(double delta)
    {
        _handPosition.Position = _handPosition.Position.Lerp(_handDefaultPos, 12f * (float)delta);
    }

    private Vector3 ApplyGravityAndJump(Vector3 velocity, double delta)
    {
        if (!IsOnFloor()) velocity.Y -= _gravity * (float)delta;

        bool isCrouching = Input.IsActionPressed("crouch");
        ItemData activeItem = InventoryManager.Instance.GetActiveItem();
        float itemWeight = activeItem != null ? activeItem.Weight : 0.0f;
        float jumpPenalty = Mathf.Clamp(itemWeight * 0.1f, 0.0f, 1.5f);

        if (Input.IsActionJustPressed("jump") && IsOnFloor() && !_isInventoryOpen && !isCrouching)
        {
            velocity.Y = JumpVelocity - jumpPenalty;
        }

        return velocity;
    }

    private Vector3 ApplyMovement(Vector3 velocity, Vector2 inputDir, double delta)
    {
        if (_isInventoryOpen)
        {
            velocity.X = Mathf.Lerp(velocity.X, 0, Friction * (float)delta);
            velocity.Z = Mathf.Lerp(velocity.Z, 0, Friction * (float)delta);
            return velocity;
        }

        bool isCrouching = Input.IsActionPressed("crouch");
        bool isSprinting = Input.IsActionPressed("sprint") && !isCrouching && IsOnFloor();
        ItemData activeItem = InventoryManager.Instance.GetActiveItem();
        float speedPenalty = activeItem != null ? Mathf.Clamp(activeItem.Weight * 0.1f, 0.0f, 2.0f) : 0.0f; 

        float targetSpeed = isCrouching ? CrouchSpeed : (isSprinting ? SprintSpeed : WalkSpeed);
        targetSpeed = Mathf.Max(1.0f, targetSpeed - speedPenalty);

        // Используем Basis головы, чтобы движение всегда шло туда, куда смотрим
        Vector3 direction = (_head.GlobalTransform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        
        if (direction != Vector3.Zero)
        {
            velocity.X = Mathf.Lerp(velocity.X, direction.X * targetSpeed, Acceleration * (float)delta);
            velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * targetSpeed, Acceleration * (float)delta);
        }
        else
        {
            velocity.X = Mathf.Lerp(velocity.X, 0, Friction * (float)delta);
            velocity.Z = Mathf.Lerp(velocity.Z, 0, Friction * (float)delta);
        }

        return velocity;
    }

    // --- МОДУЛИ ВЗАИМОДЕЙСТВИЯ ---

    private void HandleInteraction(double delta)
    {
        if (!_interactionRay.IsColliding())
        {
            ClearInteractionUI();
            UIManager.Instance?.UpdateCrosshairColor(new Color(1f, 1f, 1f, 0.5f));
            return; // --- ИСПРАВЛЕНИЕ БАГА №1 (Надпись не останется) ---
        }

        var collider = _interactionRay.GetCollider();
        if (collider is BasePart bpDisableCheck && !bpDisableCheck.IsInteractable)
        {
            ClearInteractionUI();
            return;
        }

        if (collider is IInteractable interactable)
        {
            UpdateCrosshairAndPrompt(collider, interactable);
            
            // --- ИСПРАВЛЕНИЕ БАГА №4 (Скрытие тултипов) ---
            if (GameManager.Instance?.CurrentMode == SimulatorMode.Learning)
            {
                UpdateTooltips(collider);
            }
            else
            {
                // В Экзамене/Тренировке тултипы всегда пустые
                UIManager.Instance?.ShowWorldTooltip("", "", "");
            }

            ProcessInteractionInput(interactable, delta);
        }
        else
        {
            ClearInteractionUI();
        }
    }

    private void UpdateCrosshairAndPrompt(GodotObject collider, IInteractable interactable)
    {
        Color targetColor = new Color(1f, 1f, 1f, 0.5f);

        if (collider is Fastener fCheck)
        {
            if (fCheck.HasState(PartState.Rusted)) targetColor = new Color(1f, 0.2f, 0.2f, 0.8f);
            else if (!fCheck.IsInstalled) targetColor = new Color(0.8f, 0.2f, 1f, 0.8f);
            else if (fCheck.Type == InteractionType.Instant || fCheck.CurrentTorque <= 0.1f) targetColor = new Color(0.2f, 0.8f, 1f, 0.8f);
            else targetColor = new Color(0.2f, 1f, 0.2f, 0.8f);
        }
        else if (collider is PickableItem)
        {
            targetColor = new Color(0.2f, 0.8f, 1f, 0.8f);
        }
        
        UIManager.Instance?.UpdateCrosshairColor(targetColor);

        string newPrompt = interactable.GetInteractPrompt();
        if (newPrompt != _lastPromptText)
        {
            UIManager.Instance?.UpdatePrompt(newPrompt);
            _lastPromptText = newPrompt;
        }
    }

    private void UpdateTooltips(GodotObject collider)
    {
        string tooltipName = ""; 
        string tooltipDesc = ""; 
        string tooltipStates = "";

        if (collider is BasePart bp)
        {
            if (bp.ShowTooltipName) tooltipName = bp.PartName;
            if (bp.ShowTooltipDesc) tooltipDesc = bp.PartDescription;

            if (bp.CurrentStates.Count > 0)
            {
                var stateList = new System.Collections.Generic.List<string>();
                foreach (var s in bp.CurrentStates)
                {
                    string sName = s switch {
                        PartState.Rusted => "РЖАВЧИНА",
                        PartState.Oiled => "СМАЗАНО",
                        PartState.Painted => "КРАСКА",
                        PartState.Stripped => "СОРВАНА РЕЗЬБА",
                        _ => s.ToString().ToUpper()
                    };
                    stateList.Add(sName);
                }
                tooltipStates = "СТАТУС: " + string.Join(" | ", stateList);
            }

            if (collider is Fastener f && f.IsInstalled && f.ShowTooltipDesc && f.Type == InteractionType.Continuous)
            {
                string torqueInfo = $"ЗАТЯЖКА: {f.CurrentTorque:F0}/{f.MaxTorque} Нм";
                if (string.IsNullOrEmpty(tooltipStates)) tooltipStates = "СТАТУС: " + torqueInfo;
                else tooltipStates += " | " + torqueInfo;
            }
        }
        else if (collider is PickableItem p && p.ItemResource != null)
        {
            tooltipName = p.ItemResource.ItemName;
            tooltipDesc = p.ItemResource.Description;
        }

        if (tooltipName != _lastTooltipName || tooltipDesc != _lastTooltipDesc || tooltipStates != _lastTooltipStates)
        {
            UIManager.Instance?.ShowWorldTooltip(tooltipName, tooltipDesc, tooltipStates);
            _lastTooltipName = tooltipName; 
            _lastTooltipDesc = tooltipDesc; 
            _lastTooltipStates = tooltipStates;
        }
    }

    private void ProcessInteractionInput(IInteractable interactable, double delta)
    {
        ItemData currentItem = InventoryManager.Instance.GetActiveItem();

        // --- ИСПРАВЛЕНИЕ БАГА №3: ПРОВЕРКА CanInteractWith ПЕРЕД АНИМАЦИЕЙ ---
        bool canWork = interactable.CanInteractWith(currentItem);

        if (Input.IsActionJustPressed("interact")) 
        {
            interactable.Interact(currentItem);
            if (currentItem != null && canWork) PlayToolActionAnimation(currentItem, false);
        }
        else if (Input.IsActionPressed("interact")) 
        {
            interactable.InteractContinuous(currentItem, delta);
            // Анимация и звук сработают только если инструмент подходит!
            if (currentItem != null && canWork) PlayToolActionAnimation(currentItem, true);
        }
        else if (Input.IsActionJustPressed("interact_alt")) 
        {
            interactable.InteractAlt(currentItem);
        }
        else 
        {
            UIManager.Instance?.HideTorqueUI();
            if (_handAudioPlayer.Playing) _handAudioPlayer.Stop();
        }
    }

    private void ClearInteractionUI()
    {
        // Принудительно сбрасываем всё, когда луч уходит
        if (_lastPromptText != "")
        {
            UIManager.Instance?.UpdatePrompt(""); // Пустая строка сбросит lockTimer в UI
            _lastPromptText = "";
        }
        _lastTooltipName = ""; _lastTooltipDesc = ""; _lastTooltipStates = "";
        UIManager.Instance?.ShowWorldTooltip("", "", "");
        UIManager.Instance?.HideTorqueUI();
        if (_handAudioPlayer.Playing) _handAudioPlayer.Stop();
    }

    // --- УПРАВЛЕНИЕ ИНВЕНТАРЕМ В РУКАХ И ВЫБРОС ---

    private void UpdateHandVisual(int slotIndex, ItemData item)
    {
        if (_currentHandMeshInstance != null)
        {
            _currentHandMeshInstance.QueueFree();
            _currentHandMeshInstance = null;
        }

        if (item != null && item.HandPrefab != null)
        {
            if (item.EquipSound != null) 
                AudioManager.Instance?.PlayStream(item.EquipSound, -2f, (float)GD.RandRange(0.95, 1.05));

            Node3D handVisual = item.HandPrefab.Instantiate<Node3D>();
            _handPosition.AddChild(handVisual);
            _currentHandMeshInstance = handVisual;

            DisablePhysicsRecursive(handVisual);

            _handPosition.Position = _handDefaultPos + new Vector3(0, -0.5f, 0.5f);
        }
    }

    private void DisablePhysicsRecursive(Node node)
    {
        if (node is RigidBody3D rb)
        {
            rb.Freeze = true; 
            rb.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
            rb.CollisionLayer = 0; rb.CollisionMask = 0;
            rb.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (node is StaticBody3D sb)
        {
            sb.CollisionLayer = 0; sb.CollisionMask = 0; sb.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (node is CollisionShape3D shape) shape.Disabled = true;
        foreach (Node child in node.GetChildren()) DisablePhysicsRecursive(child);
    }

    public void DropItemFromIndex(int index)
    {
        ItemData item = InventoryManager.Instance.Items[index];
        if (item == null || item.WorldPrefab == null) return;

        if (item.DropSound != null) 
            AudioManager.Instance?.PlayStream(item.DropSound, -2f, (float)GD.RandRange(0.9, 1.1));

        Node3D droppedNode = item.WorldPrefab.Instantiate<Node3D>();
        if (droppedNode is PickableItem pickable) pickable.ItemResource = item;

        GetTree().Root.GetNode("MainSimulator").AddChild(droppedNode);
        
        Vector3 throwDirection = -_camera.GlobalBasis.Z;
        droppedNode.GlobalPosition = _camera.GlobalPosition + throwDirection * 0.7f;
        
        if (droppedNode is RigidBody3D rb)
        {
            rb.AddCollisionExceptionWith(this);
            rb.CollisionLayer = 2; 
            rb.CollisionMask = 1;  

            rb.LinearVelocity = throwDirection * 4.0f + Velocity * 0.5f + Vector3.Up * 1.5f;
            rb.AngularVelocity = new Vector3((float)GD.RandRange(-3, 3), (float)GD.RandRange(-3, 3), (float)GD.RandRange(-3, 3));

            var timer = GetTree().CreateTimer(1.0f);
            timer.Timeout += () => {
                if (IsInstanceValid(rb) && IsInstanceValid(this))
                {
                    rb.RemoveCollisionExceptionWith(this);
                    rb.CollisionMask |= (1 << 2); 
                }
            };
        }

        InventoryManager.Instance.RemoveItemAtIndex(index);
    }

    // АНИМАЦИЯ РАБОТЫ ИНСТРУМЕНТОМ
    private void PlayToolActionAnimation(ItemData item, bool isContinuous)
    {
        if (_currentHandMeshInstance == null || item == null) return;
        
        // Для продолжительных действий (зажатая ЛКМ) блокируем наслоение анимаций
        if (isContinuous && _toolActionTween != null && _toolActionTween.IsRunning()) return;

        // 1. ПРОИГРЫВАЕМ УНИКАЛЬНЫЙ ЗВУК ИНСТРУМЕНТА
        if (item.UseSound != null)
        {
            if (isContinuous)
            {
                // Играем звук только если он СЕЙЧАС не играет (чтобы не было эффекта "пулемета")
                if (!_handAudioPlayer.Playing || _handAudioPlayer.Stream != item.UseSound)
                {
                    _handAudioPlayer.Stream = item.UseSound;
                    _handAudioPlayer.PitchScale = (float)GD.RandRange(0.95, 1.05);
                    _handAudioPlayer.Play();
                }
            }
            else
            {
                // Одиночные действия (например, удар ломом) играются через глобальный менеджер
                AudioManager.Instance?.PlayStream(item.UseSound, -2f, (float)GD.RandRange(0.95, 1.05));
            }
        }

        // 2. ЗАПУСКАЕМ УНИКАЛЬНУЮ АНИМАЦИЮ

        // Блокируем наслоение самих 3D-анимаций
        if (isContinuous && _toolActionTween != null && _toolActionTween.IsRunning()) return;

        _toolActionTween?.Kill();
        _toolActionTween = CreateTween();
        
        Vector3 targetPos = item.AnimPosOffset;
        Vector3 targetRot = new Vector3(
            Mathf.DegToRad(item.AnimRotDegrees.X), 
            Mathf.DegToRad(item.AnimRotDegrees.Y), 
            Mathf.DegToRad(item.AnimRotDegrees.Z)
        );

        // Движение ТУДА (Применяем векторы из конфига)
        _toolActionTween.Parallel().TweenProperty(_currentHandMeshInstance, "position", targetPos, item.AnimDuration)
                       .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _toolActionTween.Parallel().TweenProperty(_currentHandMeshInstance, "rotation", targetRot, item.AnimDuration)
                       .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

        // Движение ОБРАТНО (В нули)
        _toolActionTween.Chain().TweenProperty(_currentHandMeshInstance, "position", Vector3.Zero, item.AnimDuration)
                       .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        _toolActionTween.Parallel().TweenProperty(_currentHandMeshInstance, "rotation", Vector3.Zero, item.AnimDuration)
                       .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
    }
}