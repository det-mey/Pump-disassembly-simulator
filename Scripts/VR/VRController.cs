using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Скрипт для XRController3D — контроллеры рук в VR.
/// Поддерживает ray-based взаимодействие, триггер/grip,
/// вибрацию, визуальный лазер и звуковую обратную связь.
/// </summary>
[GlobalClass]
public partial class VRController : Node3D
{
    // --- РОЛЬ КОНТРОЛЛЕРА ---
    public enum HandRole { Left, Right }

    [ExportGroup("Роль")]
    [Export] public HandRole Hand { get; set; } = HandRole.Right;

    // --- ССЫЛКИ ---
    [ExportGroup("Ссылки")]
    [Export] private XRController3D _xrController;
    [Export] private RayCast3D _interactionRay;
    [Export] private MeshInstance3D _laserMesh;
    [Export] private CsgCylinder3D _laserCsg;

    // --- НАСТРОЙКИ ЛУЧА ---
    [ExportGroup("Настройки луча")]
    [Export] public float RayLength { get; set; } = 5.0f;
    [Export] public float RayThickness { get; set; } = 0.005f;
    [Export] public Color LaserColorNormal { get; set; } = new Color(0.2f, 0.8f, 1f, 0.8f);
    [Export] public Color LaserColorHover { get; set; } = new Color(1f, 0.9f, 0.2f, 0.9f);
    [Export] public Color LaserColorUI { get; set; } = new Color(0.2f, 1f, 0.2f, 0.9f);

    // --- НАСТРОЙКИ ВИБРАЦИИ ---
    [ExportGroup("Вибрация")]
    [Export] public float DefaultHapticStrength { get; set; } = 0.3f;
    [Export] public float DefaultHapticDuration { get; set; } = 0.05f;

    // --- ЗВУКИ ---
    [ExportGroup("Звуки")]
    [Export] public AudioStream SndHover { get; set; }
    [Export] public AudioStream SndInteract { get; set; }
    [Export] public AudioStream SndInteractAlt { get; set; }

    // --- СОСТОЯНИЕ ---
    private IInteractable _currentInteractable;
    private bool _isHoveringUI;
    private float _continuousHoldTime;
    private ItemData _activeItem;
    private bool _laserVisible;
    private Color _laserCurrentColor;

    // --- Кэш оптимизации ---
    private string _lastPromptText = "";
    private GodotObject _lastCollider;
    private Vector2 _currentThumbstick;

    public override void _Ready()
    {
        if (_xrController == null)
        {
            _xrController = GetParent<XRController3D>();
        }

        if (_xrController == null)
        {
            GD.PrintErr("[VRController] Не найден XRController3D!");
            return;
        }

        // Инициализируем луч
        if (_interactionRay == null)
        {
            _interactionRay = new RayCast3D
            {
                TargetPosition = new Vector3(0, 0, -RayLength),
                Enabled = true
            };
            AddChild(_interactionRay);
        }
        else
        {
            _interactionRay.TargetPosition = new Vector3(0, 0, -RayLength);
            _interactionRay.Enabled = true;
        }

        // Настраиваем лазер
        InitializeLaser();

        // Подписываемся на события контроллера
        _xrController.ButtonPressed += OnControllerButtonPressed;
        _xrController.ButtonReleased += OnControllerButtonReleased;
        _xrController.InputVector2Changed += OnThumbstickMoved;
    }

    public override void _ExitTree()
    {
        if (_xrController != null)
        {
            _xrController.ButtonPressed -= OnControllerButtonPressed;
            _xrController.ButtonReleased -= OnControllerButtonReleased;
            _xrController.InputVector2Changed -= OnThumbstickMoved;
        }
    }

    public override void _Process(double delta)
    {
        if (_xrController == null || VRManager.Instance?.IsVRMode != true) return;

        UpdateLaser(delta);
        HandleRayInteraction(delta);
    }

    // --- ИНИЦИАЛИЗАЦИЯ ЛАЗЕРА ---

    private void InitializeLaser()
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = LaserColorNormal,
            EmissionEnabled = true,
            EmissionEnergyMultiplier = 0.5f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        if (_laserMesh != null)
        {
            _laserMesh.MaterialOverride = material;
            _laserMesh.Visible = false;
            _laserVisible = false;
            _laserCurrentColor = LaserColorNormal;
        }
        else if (_laserCsg != null)
        {
            _laserCsg.Material = material;
            _laserCsg.Visible = false;
            _laserVisible = false;
            _laserCurrentColor = LaserColorNormal;
        }
        else
        {
            // Создаём лазер программно через CSG
            _laserCsg = new CsgCylinder3D
            {
                Radius = RayThickness,
                Height = RayLength,
                Material = material,
                Visible = false
            };
            _laserCsg.Position = new Vector3(0, 0, -RayLength / 2);
            _laserCsg.Rotation = new Vector3(Mathf.Pi / 2, 0, 0);
            AddChild(_laserCsg);
            _laserVisible = false;
            _laserCurrentColor = LaserColorNormal;
        }
    }

    // --- ОБНОВЛЕНИЕ ЛАЗЕРА ---

    private void UpdateLaser(double delta)
    {
        bool hasHit = _interactionRay?.IsColliding() == true;
        float hitDistance = RayLength;

        if (hasHit)
        {
            var collisionPoint = _interactionRay.GetCollisionPoint();
            hitDistance = GlobalPosition.DistanceTo(collisionPoint);
        }

        // Обновляем длину лазера
        if (_laserMesh != null)
        {
            _laserMesh.Visible = true;
            _laserMesh.Scale = new Vector3(1, 1, hitDistance / RayLength);
            _laserVisible = true;
        }
        else if (_laserCsg != null)
        {
            _laserCsg.Visible = true;
            _laserCsg.Height = hitDistance;
            _laserCsg.Position = new Vector3(0, 0, -hitDistance / 2);
            _laserVisible = true;
        }

        // Обновляем цвет лазера
        Color targetColor = LaserColorNormal;

        if (_isHoveringUI)
        {
            targetColor = LaserColorUI;
        }
        else if (_currentInteractable != null)
        {
            targetColor = LaserColorHover;
        }

        if (_laserCurrentColor != targetColor)
        {
            _laserCurrentColor = _laserCurrentColor.Lerp(targetColor, 15f * (float)delta);

            if (_laserMesh != null && _laserMesh.MaterialOverride is StandardMaterial3D laserMat)
            {
                laserMat.AlbedoColor = _laserCurrentColor;
                laserMat.EmissionEnergyMultiplier = 0.5f;
            }
            else if (_laserCsg != null && _laserCsg.Material is StandardMaterial3D csgMat)
            {
                csgMat.AlbedoColor = _laserCurrentColor;
                csgMat.EmissionEnergyMultiplier = 0.5f;
            }
        }
    }

    /// <summary>Скрывает лазер.</summary>
    public void HideLaser()
    {
        if (_laserMesh != null) _laserMesh.Visible = false;
        if (_laserCsg != null) _laserCsg.Visible = false;
        _laserVisible = false;
    }

    /// <summary>Показывает лазер.</summary>
    public void ShowLaser()
    {
        if (_laserMesh != null) _laserMesh.Visible = true;
        if (_laserCsg != null) _laserCsg.Visible = true;
        _laserVisible = true;
    }

    // --- RAY-BASED ВЗАИМОДЕЙСТВИЕ ---

    private void HandleRayInteraction(double delta)
    {
        if (!_interactionRay.IsColliding())
        {
            ClearInteractionState();
            return;
        }

        var collider = _interactionRay.GetCollider();

        // Проверяем, является ли объект UI
        _isHoveringUI = IsUIElement(collider);

        if (collider is IInteractable interactable)
        {
            if (collider != _lastCollider)
            {
                _currentInteractable = interactable;
                _lastCollider = collider;
                _continuousHoldTime = 0f;

                // Звук наведения
                if (SndHover != null) AudioManager.Instance?.PlayStream(SndHover, -8f);

                // Вибрация при наведении на интерактивный объект
                TriggerHapticPulse(DefaultHapticStrength * 0.3f, DefaultHapticDuration * 0.5f);
            }

            // Обновляем подсказки через UIManager
            UpdateInteractionPrompt(interactable);

            // Обновляем тултипы для Learning-режима
            if (GameManager.Instance?.CurrentMode == SimulatorMode.Learning)
            {
                UpdateInteractionTooltips(collider);
            }
            else
            {
                UIManager.Instance?.ShowWorldTooltip("", "", "");
            }

            // Обрабатываем удержание для continuous-действий
            if (_currentInteractable != null)
            {
                _continuousHoldTime += (float)delta;
                _currentInteractable.InteractContinuous(_activeItem, delta);
            }
        }
        else
        {
            // Не интерактивный объект — очищаем состояние
            if (_currentInteractable != null)
            {
                ClearInteractionState();
            }
            UIManager.Instance?.ShowWorldTooltip("", "", "");
        }
    }

    /// <summary>Проверяет, является ли объект элементом UI.</summary>
    private static bool IsUIElement(GodotObject obj)
    {
        return obj is Control || obj is Viewport;
    }

    private void UpdateInteractionPrompt(IInteractable interactable)
    {
        string prompt = interactable.GetInteractPrompt();
        if (prompt != _lastPromptText)
        {
            UIManager.Instance?.UpdatePrompt(prompt);
            _lastPromptText = prompt;
        }
    }

    private static void UpdateInteractionTooltips(GodotObject collider)
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
                var stateList = new List<string>();
                foreach (var s in bp.CurrentStates)
                {
                    string sName = s switch
                    {
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

        UIManager.Instance?.ShowWorldTooltip(tooltipName, tooltipDesc, tooltipStates);
    }

    private void ClearInteractionState()
    {
        _currentInteractable = null;
        _lastCollider = null;
        _continuousHoldTime = 0f;
        _isHoveringUI = false;

        if (_lastPromptText != "")
        {
            UIManager.Instance?.UpdatePrompt("");
            _lastPromptText = "";
        }
        UIManager.Instance?.ShowWorldTooltip("", "", "");
        UIManager.Instance?.HideTorqueUI();
    }

    // --- ОБРАБОТКА КНОПОК КОНТРОЛЛЕРА ---

    private void OnControllerButtonPressed(string button)
    {
        if (VRManager.Instance?.IsVRMode != true) return;

        GD.Print($"[VRController] Кнопка нажата: {button}");

        // Trigger — основное взаимодействие (Interact)
        if (button == "trigger_click" || button == "trigger")
        {
            if (_currentInteractable != null)
            {
                _currentInteractable.Interact(_activeItem);
                if (SndInteract != null) AudioManager.Instance?.PlayStream(SndInteract);
                TriggerHapticPulse(DefaultHapticStrength, DefaultHapticDuration);
            }
        }

        // Grip — альтернативное взаимодействие (InteractAlt — подбор/снятие)
        if (button == "grip_click" || button == "grip")
        {
            if (_currentInteractable != null)
            {
                _currentInteractable.InteractAlt(_activeItem);
                if (SndInteractAlt != null) AudioManager.Instance?.PlayStream(SndInteractAlt);
                TriggerHapticPulse(DefaultHapticStrength * 0.7f, DefaultHapticDuration * 1.5f);
            }
        }
    }

    private void OnControllerButtonReleased(string button)
    {
        if (VRManager.Instance?.IsVRMode != true) return;

        // Останавливаем continuous-действие при отпускании trigger
        if (button == "trigger_click" || button == "trigger")
        {
            _continuousHoldTime = 0f;
        }
    }

    private void OnThumbstickMoved(string name, Vector2 vector)
    {
        if (VRManager.Instance?.IsVRMode != true) return;

        _currentThumbstick = vector;

        // Навигация по слотам инвентаря
        if (vector.X > 0.5f)
        {
            InventoryManager.Instance?.NextSlot();
            UpdateActiveItem();
            TriggerHapticPulse(DefaultHapticStrength * 0.2f, DefaultHapticDuration * 0.5f);
        }
        else if (vector.X < -0.5f)
        {
            InventoryManager.Instance?.PrevSlot();
            UpdateActiveItem();
            TriggerHapticPulse(DefaultHapticStrength * 0.2f, DefaultHapticDuration * 0.5f);
        }
    }

    /// <summary>Обновляет активный предмет из инвентаря.</summary>
    private void UpdateActiveItem()
    {
        if (InventoryManager.Instance != null)
        {
            _activeItem = InventoryManager.Instance.GetActiveItem();
        }
    }

    // --- ВИБРАЦИЯ КОНТРОЛЛЕРА ---

    /// <summary>Вызывает вибрацию контроллера через Godot Call (динамический вызов).</summary>
    /// <param name="strength">Сила вибрации (0..1).</param>
    /// <param name="duration">Длительность в секундах.</param>
    public void TriggerHapticPulse(float strength = 0.3f, float duration = 0.05f)
    {
        if (_xrController == null) return;

        // Получаем трекер через XRServer
        // XRController3D.Tracker — это StringName, получаем XRPositionalTracker
        StringName trackerName = _xrController.Tracker;
        var tracker = XRServer.GetTracker(trackerName);

        if (tracker != null)
        {
            // XRPositionalTracker.trigger_haptic_pulse(action_name, haptic, duration, frequency, amplitude)
            // Вызываем через Call для совместимости
            tracker.Call("trigger_haptic_pulse", "haptic", 0, strength, duration, 0f);
        }
    }

    /// <summary>Сильная вибрация для ошибок/подтверждений.</summary>
    public void TriggerHapticStrong()
    {
        TriggerHapticPulse(0.8f, 0.15f);
    }

    /// <summary>Лёгкая вибрация для наведений.</summary>
    public void TriggerHapticLight()
    {
        TriggerHapticPulse(0.15f, 0.03f);
    }

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ---

    /// <summary>Возвращает IInteractable объект при наведении.</summary>
    public IInteractable GetTargetInteractable()
    {
        return _currentInteractable;
    }

    /// <summary>Возвращает объект, на который указывает луч.</summary>
    public GodotObject GetRayTarget()
    {
        if (_interactionRay?.IsColliding() == true)
        {
            return _interactionRay.GetCollider();
        }
        return null;
    }

    /// <summary>Принудительно вызывает Interact для текущего объекта.</summary>
    public void DoInteract()
    {
        if (_currentInteractable != null)
        {
            _currentInteractable.Interact(_activeItem);
            TriggerHapticPulse(DefaultHapticStrength, DefaultHapticDuration);
        }
    }

    /// <summary>Принудительно вызывает InteractAlt для текущего объекта.</summary>
    public void DoInteractAlt()
    {
        if (_currentInteractable != null)
        {
            _currentInteractable.InteractAlt(_activeItem);
            TriggerHapticPulse(DefaultHapticStrength * 0.7f, DefaultHapticDuration * 1.5f);
        }
    }

    /// <summary>Устанавливает активный предмет для взаимодействия.</summary>
    public void SetActiveItem(ItemData item)
    {
        _activeItem = item;
    }
}
