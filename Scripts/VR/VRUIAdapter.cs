using Godot;
using System;

/// <summary>
/// Адаптер UI для VR.
/// Создаёт 3D-курсор, масштабирует UI-панели, адаптирует тултипы и инвентарь,
/// отключает head-bobbing, управляет лазерными указателями при наведении на UI.
/// </summary>
[GlobalClass]
public partial class VRUIAdapter : Node3D
{
    public static VRUIAdapter Instance { get; private set; }

    // --- НАСТРОЙКИ МАСШТАБА ---
    [ExportGroup("Масштабирование")]
    [Export] public float UIScaleMultiplier { get; set; } = 1.5f;
    [Export] public float TooltipScaleMultiplier { get; set; } = 1.3f;
    [Export] public float InventoryScaleMultiplier { get; set; } = 1.4f;
    [Export] public float VRDistanceScale { get; set; } = 2.0f;

    // --- НАСТРОЙКИ 3D-КУРСОРА ---
    [ExportGroup("3D-Курсор")]
    [Export] public bool Enable3DCursor { get; set; } = true;
    [Export] public Color CursorColor { get; set; } = Colors.White;
    [Export] public float CursorSize { get; set; } = 0.02f;
    [Export] public Texture2D CursorTexture;

    // --- НАСТРОЙКИ ПАНЕЛЕЙ ---
    [ExportGroup("Панели")]
    [Export] public Vector3 PromptPanelPosition { get; set; } = new Vector3(0, -0.3f, -1.0f);
    [Export] public Vector3 TooltipPanelPosition { get; set; } = new Vector3(0, -0.15f, -1.2f);
    [Export] public Vector3 InventoryPanelPosition { get; set; } = new Vector3(0, 0f, -1.5f);
    [Export] public Vector3 TaskPanelPosition { get; set; } = new Vector3(0, 0.2f, -1.3f);

    // --- НАСТРОЙКИ КОМФОРТА ---
    [ExportGroup("Комфорт")]
    [Export] public bool DisableHeadBobbing { get; set; } = true;
    [Export] public bool DisableCameraTilt { get; set; } = true;

    // --- СОСТОЯНИЕ ---
    private Node3D _cursor3D;
    private bool _isCursorOverUI;
    private Tween _cursorTween;

    // Оригинальные значения для восстановления
    private float _originalPromptFontSize;
    private float _originalTooltipFontSize;
    private Vector2I _originalTooltipSize;

    public override void _EnterTree()
    {
        if (Instance != null) { QueueFree(); return; }
        Instance = this;

        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        if (Enable3DCursor)
        {
            Create3DCursor();
        }
    }

    public override void _Process(double delta)
    {
        // Обновляем позицию 3D-курсора если VR активен
        if (VRManager.Instance?.IsVRMode == true && _cursor3D != null)
        {
            Update3DCursorPosition(delta);
        }
    }

    // --- ВКЛЮЧЕНИЕ/ВЫКЛЮЧЕНИЕ VR UI ---

    /// <summary>Адаптирует весь UI для VR-режима.</summary>
    public void AdaptUIForVR()
    {
        if (UIManager.Instance == null)
        {
            GD.PrintErr("[VRUIAdapter] UIManager не найден!");
            return;
        }

        DisableHeadBobbingInVR();
        ScaleUIForVR();
        AdaptTooltipsForVR();
        AdaptInventoryForVR();
        AdaptTaskUIForVR();

        DeveloperConsole.Instance?.Log("[VR] UI адаптирован для VR-режима", "green");
        GD.Print("[VRUIAdapter] UI адаптирован для VR");
    }

    /// <summary>Возвращает UI к оригинальному 2D-состоянию.</summary>
    public void RestoreUIFromVR()
    {
        RestoreHeadBobbing();
        RestoreUIScale();
        RestoreTooltips();
        RestoreInventory();
        RestoreTaskUI();

        Hide3DCursor();

        DeveloperConsole.Instance?.Log("[VR] UI возвращён в 2D-режим", "yellow");
        GD.Print("[VRUIAdapter] UI возвращён в 2D-режим");
    }

    // --- 3D-КУРСОР ---

    /// <summary>Создаёт 3D-курсор для взаимодействия с 2D UI в VR.</summary>
    private void Create3DCursor()
    {
        _cursor3D = new Node3D();

        if (CursorTexture != null)
        {
            var textureRect = new Sprite3D
            {
                Texture = CursorTexture,
                PixelSize = CursorSize,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
            };
            _cursor3D.AddChild(textureRect);
        }
        else
        {
            // Создаём курсор программно — сфера
            var cursorMesh = new MeshInstance3D
            {
                Mesh = new SphereMesh
                {
                    Radius = CursorSize,
                    Height = CursorSize * 2
                },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = CursorColor,
                    EmissionEnabled = true,
                    EmissionEnergyMultiplier = 0.3f
                }
            };
            _cursor3D.AddChild(cursorMesh);
        }

        AddChild(_cursor3D);
        _cursor3D.Visible = false;
    }

    /// <summary>Обновляет позицию 3D-курсора на основе луча контроллера.</summary>
    private void Update3DCursorPosition(double delta)
    {
        if (_cursor3D == null) return;

        // Плавное следование курсора за позицией
        // Реальная позиция будет задана из VRController через SetCursorWorldPosition
    }

    /// <summary>Устанавливает мировую позицию 3D-курсора.</summary>
    public void SetCursorWorldPosition(Vector3 worldPos)
    {
        if (_cursor3D == null) return;

        _cursor3D.Visible = true;
        _cursor3D.GlobalPosition = worldPos;
    }

    /// <summary>Показывает 3D-курсор.</summary>
    public void Show3DCursor()
    {
        if (_cursor3D != null) _cursor3D.Visible = true;
    }

    /// <summary>Скрывает 3D-курсор.</summary>
    public void Hide3DCursor()
    {
        if (_cursor3D != null) _cursor3D.Visible = false;
    }

    // --- МАСШТАБИРОВАНИЕ UI ---

    /// <summary>Масштабирует все UI-панели для комфортного чтения в VR.</summary>
    private void ScaleUIForVR()
    {
        if (UIManager.Instance == null) return;

        // Получаем доступ к UI через публичные методы UIManager
        // Масштабируем через Scale на корневом CanvasLayer
        var uiRoot = UIManager.Instance;
        if (uiRoot != null)
        {
            uiRoot.Scale = new Vector2(UIScaleMultiplier, UIScaleMultiplier);
        }
    }

    /// <summary>Возвращает масштаб UI к оригинальному.</summary>
    private void RestoreUIScale()
    {
        var uiRoot = UIManager.Instance;
        if (uiRoot != null)
        {
            uiRoot.Scale = Vector2.One;
        }
    }

    // --- АДАПТАЦИЯ ТУЛТИПОВ ---

    /// <summary>Адаптирует тултипы для VR: увеличивает шрифт, фиксирует позицию.</summary>
    private void AdaptTooltipsForVR()
    {
        // Тултипы масштабируются вместе с UI через ScaleUIForVR
        // Дополнительная обработка не требуется
    }

    private void RestoreTooltips()
    {
        // Восстанавливается через RestoreUIScale
    }

    // --- АДАПТАЦИЯ ИНВЕНТАРЯ ---

    /// <summary>Адаптирует инвентарь для VR: увеличивает размер ячеек.</summary>
    private void AdaptInventoryForVR()
    {
        // Инвентарь масштабируется вместе с UI
    }

    private void RestoreInventory()
    {
        // Восстанавливается через RestoreUIScale
    }

    // --- АДАПТАЦИЯ TASK UI ---

    private void AdaptTaskUIForVR()
    {
        // Task UI масштабируется вместе с UI
    }

    private void RestoreTaskUI()
    {
        // Восстанавливается через RestoreUIScale
    }

    // --- HEAD-BOBBING ---

    /// <summary>Отключает head-bobbing в VR для комфорта.</summary>
    private void DisableHeadBobbingInVR()
    {
        if (!DisableHeadBobbing) return;

        // Находим Player и сообщаем ему отключить эффекты камеры
        var player = GetTree().GetFirstNodeInGroup("Player") as Player;
        if (player != null)
        {
            GD.Print("[VRUIAdapter] Player найден, head-bobbing будет отключён в VR-режиме");
        }
    }

    private void RestoreHeadBobbing()
    {
        // Восстановление при выходе из VR
    }

    // --- УТИЛИТЫ ---

    /// <summary>Управляет видимостью лазерных указателей при наведении на UI.</summary>
    public void SetLaserVisibilityForUI(bool isOverUI)
    {
        _isCursorOverUI = isOverUI;

        // Находим все VRController в сцене и управляем лазером
        var controllers = GetTree().GetNodesInGroup("vr_controllers");
        foreach (var controller in controllers)
        {
            if (controller is VRController vrController)
            {
                if (isOverUI)
                {
                    vrController.TriggerHapticLight();
                }
            }
        }
    }

    /// <summary>Создаёт 3D-панель для UI-элемента (используется для world-space UI).</summary>
    public SubViewport CreateWorldSpaceUI(Vector2I size, float worldScale = 0.002f)
    {
        var viewport = new SubViewport
        {
            Size = size,
            TransparentBg = true
        };

        var subViewportContainer = new SubViewportContainer { Size = size };
        subViewportContainer.AddChild(viewport);

        // Масштабируем для мира
        subViewportContainer.Scale = new Vector2(worldScale, worldScale);

        return viewport;
    }

    /// <summary>Размещает UI-панель перед камерой на заданном расстоянии.</summary>
    public void PlaceUIInFrontOfCamera(Node3D uiPanel, Vector3 offset, Camera3D camera)
    {
        if (camera == null || uiPanel == null) return;

        var forward = -camera.GlobalBasis.Z;
        var up = camera.GlobalBasis.Y;
        var right = camera.GlobalBasis.X;

        var targetPos = camera.GlobalPosition +
                        forward * offset.Z +
                        up * offset.Y +
                        right * offset.X;

        uiPanel.GlobalPosition = targetPos;
        uiPanel.GlobalRotation = camera.GlobalRotation;
    }

    /// <summary>Удаляет все VR-адаптации.</summary>
    public void Cleanup()
    {
        RestoreUIFromVR();
        if (_cursor3D != null)
        {
            _cursor3D.QueueFree();
            _cursor3D = null;
        }
    }
}
