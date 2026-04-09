using Godot;
using System.Collections.Generic;

public partial class UIManager : CanvasLayer
{
	public static UIManager Instance { get; private set; }

	// --- ССЫЛКИ НА UI ---
	[ExportGroup("Меню Паузы")][Export] private Control _pauseMenuPanel;
	[ExportGroup("Иконки Меню Паузы")]
	[Export] private Texture2D _iconResume;
	[Export] private Texture2D _iconSettings;[Export] private Texture2D _iconRestart;
	[Export] private Texture2D _iconHome;

	[ExportGroup("Инвентарь")]
	[Export] private GridContainer _inventoryGrid; 
	[Export] private Control _inventoryWindow; 
	[Export] private PanelContainer _gridBackgroundPanel;
	[Export] private PanelContainer _detailsPanelContainer;
	[Export] private Control _detailsContentBox;[ExportGroup("Детали Предмета (Инвентарь)")]
	[Export] private Label _detailNameLabel;
	[Export] private Label _detailDescLabel;
	[Export] private TextureRect _detailIcon;

	[ExportGroup("Hotbar")]
	[Export] private HBoxContainer _hotbarContainer;

	[ExportGroup("HUD и Тултипы")]
	[Export] private ColorRect _crosshair;
	[Export] private Label _promptLabel;
	[Export] private PanelContainer _worldTooltipPanel;[Export] private Label _worldNameLabel;
	[Export] private Label _worldDescLabel;[Export] private Control _worldSeparator;
	[Export] private Label _worldStatesLabel;
	[Export] private Control _worldSeparator2;
	[Export] private ProgressBar _torqueProgressBar;
	[Export] private Label _torqueNumbersLabel;
	[Export] private Control _taskTrackerPanel;
	[Export] private ProgressBar _taskProgressBar;
	[Export] private Label _taskNameLabel;
	[Export] private Label _taskDescLabel;
	[Export] private Control _taskSeparator;
	[Export] private Label _fpsLabel;

	[ExportGroup("Звуки Системы и Инвентаря")]
	[Export] public AudioStream SndHover { get; set; }
	[Export] public AudioStream SndClick { get; set; }
	[Export] public AudioStream SndDropdown { get; set; }
	[Export] public AudioStream SndInvOpen { get; set; }
	[Export] public AudioStream SndInvClose { get; set; }
	[Export] public AudioStream SndHotbarSwitch { get; set; }
	[Export] public AudioStream SndItemDrag { get; set; }
	[Export] public AudioStream SndItemDrop { get; set; }
	[Export] public AudioStream SndTaskOk { get; set; }
	[Export] public AudioStream SndTaskComplete { get; set; }
	[Export] public AudioStream SndError { get; set; }

	// --- СОСТОЯНИЕ И АНИМАЦИИ ---
	private float _messageLockTimer = 0.0f;
	private string _normalPromptCache = "";
	private bool _isPaused = false;
	private bool _isDetailsOpen = false;

	private Tween _inventoryTween;
	private Tween _detailsTween;
	private Tween _pauseTween;

	private List<InventorySlot> _hotbarSlots = new List<InventorySlot>();
	private List<InventorySlot> _inventorySlots = new List<InventorySlot>();

	// --- ИНИЦИАЛИЗАЦИЯ ---
    public override void _EnterTree()
    {
        // Теперь Instance задается один раз и навсегда
        if (Instance == null) Instance = this;
    }

    public override void _Ready()
    {
        // Инициализируем всё один раз при старте всей ИГРЫ
        InitializeHUD();
        ApplyGlobalStyles();
        GenerateInventorySlots();

        // Подписываемся один раз
        InventoryManager.Instance.OnInventoryUpdated += RefreshAllSlots;
        InventoryManager.Instance.OnActiveSlotChanged += HighlightActiveSlot;
        
        // Скрываем всё лишнее
        _pauseMenuPanel.Visible = false;
        _pauseMenuPanel.Modulate = new Color(1, 1, 1, 0);

		if (_worldDescLabel != null)
		{
			_worldDescLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_worldDescLabel.CustomMinimumSize = new Vector2(400, 0); // Лимит ширины описания
		}
		if (_worldStatesLabel != null)
		{
			_worldStatesLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_worldStatesLabel.CustomMinimumSize = new Vector2(400, 0); // Лимит ширины статусов
		}
		if (_promptLabel != null)
		{
			_promptLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_promptLabel.CustomMinimumSize = new Vector2(400, 0); // Лимит для верхней подсказки
		}
    }

	public override void _ExitTree() { }

	private void InitializeHUD()
	{
		HideTorqueUI();
		if (_inventoryWindow != null) 
		{
			_inventoryWindow.Visible = false;
			_inventoryWindow.Modulate = new Color(1, 1, 1, 0);
			_inventoryWindow.MouseFilter = Control.MouseFilterEnum.Stop;
		}
		
		if (_worldTooltipPanel != null) _worldTooltipPanel.Visible = false;
		
		if (_pauseMenuPanel != null) 
		{
			_pauseMenuPanel.Visible = false;
			_pauseMenuPanel.Modulate = new Color(1, 1, 1, 0);
		}

		if (_promptLabel != null)
		{
			_promptLabel.TopLevel = true;
			_promptLabel.HorizontalAlignment = HorizontalAlignment.Left;
		}

		if (_detailIcon != null)
		{
			_detailIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			_detailIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			_detailIcon.CustomMinimumSize = new Vector2(100, 100);
			_detailIcon.Size = new Vector2(100, 100);
		}
		
		if (_detailsContentBox != null) _detailsContentBox.Visible = false;
	}

	private void ApplyGlobalStyles()
	{
		StyleBoxFlat CreateDarkPanel(int radius, int margin) => new StyleBoxFlat { 
			BgColor = new Color(0.12f, 0.12f, 0.12f, 1.0f), 
			CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, 
			CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
			ContentMarginLeft = margin, ContentMarginRight = margin, 
			ContentMarginTop = margin, ContentMarginBottom = margin
		};

		if (_gridBackgroundPanel != null) _gridBackgroundPanel.AddThemeStyleboxOverride("panel", CreateDarkPanel(15, 15));
		
		if (_detailsPanelContainer != null)
		{
			_detailsPanelContainer.AddThemeStyleboxOverride("panel", CreateDarkPanel(15, 20));
			_detailsPanelContainer.ClipContents = true; 
			_detailsPanelContainer.CustomMinimumSize = new Vector2(0, 0); 
		}

		StyleBoxFlat CreateTooltipPanel() => new StyleBoxFlat {
			BgColor = new Color(0.12f, 0.12f, 0.12f, 0.95f),
			CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
			CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
			ContentMarginLeft = 15, ContentMarginRight = 15,
			ContentMarginTop = 10, ContentMarginBottom = 10,
			BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
			BorderColor = new Color(1, 1, 1, 0.08f) 
		};

		if (_worldTooltipPanel != null) _worldTooltipPanel.AddThemeStyleboxOverride("panel", CreateTooltipPanel());
		if (_taskTrackerPanel != null && _taskTrackerPanel is PanelContainer tp) tp.AddThemeStyleboxOverride("panel", CreateTooltipPanel());

		var sepStyle = new StyleBoxLine { Color = new Color(1, 1, 1, 0.15f), Thickness = 1, GrowBegin = 0, GrowEnd = 0 };
		ThemeDB.GetDefaultTheme().SetStylebox("separator", "HSeparator", sepStyle);
	}

	// --- ПРОЦЕССЫ КАДРА (HUD И АНИМАЦИИ) ---
	public override void _Process(double delta)
	{
		ProcessPromptTimer(delta);
		ProcessFPSDisplay();
		ProcessFloatingUI(delta);
	}

	private void ProcessPromptTimer(double delta)
	{
		if (_messageLockTimer > 0)
		{
			_messageLockTimer -= (float)delta;
			if (_messageLockTimer <= 0 && _promptLabel != null)
			{
				_promptLabel.Modulate = Colors.White;
				_promptLabel.Text = _normalPromptCache;
			}
		}
	}

	private void ProcessFPSDisplay()
	{
		if (_fpsLabel != null)
		{
			if (SettingsManager.Instance != null && SettingsManager.Instance.ShowFPS)
			{
				_fpsLabel.Visible = true;
				double fps = Engine.GetFramesPerSecond();
				_fpsLabel.Text = $"FPS: {fps}";
				_fpsLabel.Modulate = fps >= 60 ? Colors.Green : (fps >= 30 ? Colors.Yellow : Colors.Red);
			}
			else _fpsLabel.Visible = false;
		}
	}

	private void ProcessFloatingUI(double delta)
	{
		// Оптимизация: обрабатываем только если элементы видимы
		bool hasPrompt = _promptLabel != null && _promptLabel.Visible;
		bool hasTooltip = _worldTooltipPanel != null && _worldTooltipPanel.Visible;
		
		if (!hasPrompt && !hasTooltip) return;
		
		Vector2 mousePos = GetViewport().GetMousePosition();

		if (_promptLabel != null && !string.IsNullOrEmpty(_promptLabel.Text))
		{
			_promptLabel.Visible = true;
			Vector2 promptTarget = mousePos + new Vector2(25, -30);
			_promptLabel.GlobalPosition = _promptLabel.GlobalPosition.Lerp(promptTarget, 25f * (float)delta);
		}
		else if (_promptLabel != null) _promptLabel.Visible = false;

        if (_worldTooltipPanel != null && _worldTooltipPanel.Visible)
        {
            Vector2 screenSize = GetViewport().GetVisibleRect().Size;
            Vector2 panelSize = _worldTooltipPanel.Size;
            
            // Желаемая позиция (чуть ниже и правее курсора)
            Vector2 targetPos = mousePos + new Vector2(25, 25);
            
            // МАТЕМАТИЧЕСКИЙ ФИКС: Ограничиваем координаты, чтобы панель всегда была в кадре
            targetPos.X = Mathf.Clamp(targetPos.X, 0, screenSize.X - panelSize.X - 10);
            targetPos.Y = Mathf.Clamp(targetPos.Y, 0, screenSize.Y - panelSize.Y - 10);

            _worldTooltipPanel.GlobalPosition = _worldTooltipPanel.GlobalPosition.Lerp(targetPos, 20f * (float)delta);
            _worldTooltipPanel.Size = Vector2.Zero; // Принудительный пересчет под новую ширину
        }
	}

	// --- ИНВЕНТАРЬ ---
	private void GenerateInventorySlots()
	{
		// Проверяем, назначены ли контейнеры в Инспекторе
		if (_hotbarContainer == null || _inventoryGrid == null)
		{
			GD.PrintErr("[UI ERROR] Контейнеры Hotbar или InventoryGrid не назначены в Инспекторе UIManager!");
			return;
		}

		foreach (Node child in _hotbarContainer.GetChildren()) child.QueueFree();
		foreach (Node child in _inventoryGrid.GetChildren()) child.QueueFree();
		
		_hotbarSlots.Clear();
		_inventorySlots.Clear();

		for (int i = 0; i < InventoryManager.HotbarSize; i++)
		{
			var slot = new InventorySlot { SlotIndex = i };
			_hotbarContainer.AddChild(slot);
			_hotbarSlots.Add(slot);
		}

		for (int i = 0; i < InventoryManager.TotalSlots; i++)
		{
			var slot = new InventorySlot { SlotIndex = i };
			_inventoryGrid.AddChild(slot);
			_inventorySlots.Add(slot);
		}
	}

	private void RefreshAllSlots()
	{
		var items = InventoryManager.Instance.Items;
		var isNew = InventoryManager.Instance.IsNewItem; 
		
		for (int i = 0; i < _hotbarSlots.Count; i++) _hotbarSlots[i].UpdateSlot(items[i], isNew[i]);
		for (int i = 0; i < _inventorySlots.Count; i++) _inventorySlots[i].UpdateSlot(items[i], isNew[i]);
	}

	private void HighlightActiveSlot(int index, ItemData item)
	{
		for (int i = 0; i < _hotbarSlots.Count; i++) _hotbarSlots[i].SetActiveStatus(i == index);
	}

	public void ToggleFullInventory(bool isOpen)
	{
		if (_inventoryWindow != null)
		{
			if (!isOpen) ShowItemDetails(null); 

			_inventoryTween?.Kill();
			_inventoryTween = CreateTween();

			if (isOpen)
			{
				if (SndInvOpen != null) AudioManager.Instance?.PlayStream(SndInvOpen);
				_inventoryWindow.Visible = true;
				RefreshAllSlots();
				_inventoryTween.TweenProperty(_inventoryWindow, "modulate", Colors.White, 0.2f).SetTrans(Tween.TransitionType.Sine);
			}
			else
			{
				 if (SndInvClose != null) AudioManager.Instance?.PlayStream(SndInvClose);
				_inventoryTween.TweenProperty(_inventoryWindow, "modulate", new Color(1, 1, 1, 0), 0.2f).SetTrans(Tween.TransitionType.Sine);
				_inventoryTween.TweenCallback(Callable.From(() => _inventoryWindow.Visible = false));
			}
		}
		if (_crosshair != null) _crosshair.Visible = !isOpen;
	}

	public void ShowItemDetails(ItemData item)
	{
		if (_detailsPanelContainer == null || _detailsContentBox == null) return;

		if (item == null)
		{
			if (_isDetailsOpen)
			{
				_isDetailsOpen = false;
				_detailsContentBox.Visible = false; 
				
				_detailsTween?.Kill(); 
				_detailsTween = CreateTween();
				
				_detailsTween.Parallel().TweenProperty(_detailsPanelContainer, "custom_minimum_size:x", 0.0f, 0.25f)
							 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
				_detailsTween.Parallel().TweenProperty(_detailsPanelContainer, "modulate", new Color(1, 1, 1, 0), 0.2f);
				_detailsTween.TweenCallback(Callable.From(() => _detailsPanelContainer.Visible = false));
			}
			return;
		}

		if (_detailNameLabel != null) _detailNameLabel.Text = item.ItemName;
		if (_detailDescLabel != null) _detailDescLabel.Text = item.Description;
		if (_detailIcon != null) _detailIcon.Texture = item.Icon;

		if (!_isDetailsOpen)
		{
			_isDetailsOpen = true;
			_detailsPanelContainer.Visible = true;
			
			_detailsTween?.Kill();
			_detailsTween = CreateTween();
			
			_detailsTween.Parallel().TweenProperty(_detailsPanelContainer, "custom_minimum_size:x", 300.0f, 0.3f)
						 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_detailsTween.Parallel().TweenProperty(_detailsPanelContainer, "modulate", Colors.White, 0.2f);
			
			_detailsTween.TweenCallback(Callable.From(() => _detailsContentBox.Visible = true));
		}
	}

	public void AnimateItemPickup(Texture2D icon)
	{
		if (icon == null) return;

		var flyingIcon = new TextureRect {
			Texture = icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Size = new Vector2(64, 64)
		};
		flyingIcon.PivotOffset = flyingIcon.Size / 2;
		flyingIcon.GlobalPosition = GetViewport().GetVisibleRect().Size / 2;
		AddChild(flyingIcon);

		Vector2 endPos = new Vector2(GetViewport().GetVisibleRect().Size.X / 2, GetViewport().GetVisibleRect().Size.Y - 50);

		var tween = CreateTween().SetParallel(true); 
		tween.TweenProperty(flyingIcon, "global_position", endPos, 0.5f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
		tween.TweenProperty(flyingIcon, "scale", new Vector2(0.2f, 0.2f), 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		tween.TweenProperty(flyingIcon, "modulate:a", 0f, 0.5f);
		tween.Finished += () => flyingIcon.QueueFree();
	}

	// --- ПАУЗА И ДЕЙСТВИЯ ---
    public override void _UnhandledInput(InputEvent @event)
    {
        // Только "живой" экземпляр с привязанным интерфейсом может ставить игру на паузу
        if (_hotbarContainer == null) return;

        if (@event.IsActionPressed("ui_cancel"))
        {
            // Не позволяем открывать паузу, если мы в Главном Меню
            if (GetTree().CurrentScene != null && GetTree().CurrentScene.Name == "MainMenu") return;
            
            TogglePauseMenu(!_isPaused);
        }
    }

    public void TogglePauseMenu(bool pause)
    {
        // Защита от вызова на пустом синглтоне
        if (_pauseMenuPanel == null || !IsInsideTree()) return;

        _isPaused = pause;
        GetTree().Paused = _isPaused; 

        if (_isPaused)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            if (_crosshair != null) _crosshair.Visible = false;

            var container = FindContainer(_pauseMenuPanel);
            if (container != null && container.GetChildCount() == 0)
            {
                if (container is BoxContainer box) box.AddThemeConstantOverride("separation", 15);
                container.AddChild(CreatePauseButton("Продолжить", _iconResume, OnResumePressed));
                container.AddChild(CreatePauseButton("Начать заново", _iconRestart, OnRestartPressed));
                container.AddChild(CreatePauseButton("Настройки", _iconSettings, OnSettingsPausePressed));
                container.AddChild(CreatePauseButton("Выйти в меню", _iconHome, OnMainMenuPressed));
            }

            _pauseMenuPanel.Visible = true;
            _pauseTween?.Kill();
            _pauseTween = CreateTween();
            _pauseTween.SetPauseMode(Tween.TweenPauseMode.Process);
            _pauseTween.TweenProperty(_pauseMenuPanel, "modulate:a", 1.0f, 0.2f).SetTrans(Tween.TransitionType.Sine);
        }
        else
        {
            // Прячем меню
            _pauseTween?.Kill();
            _pauseTween = CreateTween();
            _pauseTween.SetPauseMode(Tween.TweenPauseMode.Process);
            _pauseTween.TweenProperty(_pauseMenuPanel, "modulate:a", 0.0f, 0.2f).SetTrans(Tween.TransitionType.Sine);
            _pauseTween.TweenCallback(Callable.From(() => _pauseMenuPanel.Visible = false));

            // Возвращаем мышь только если не открыт инвентарь
            bool isInvOpen = _inventoryWindow != null && _inventoryWindow.Visible;
            Input.MouseMode = isInvOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
            if (_crosshair != null && !isInvOpen) _crosshair.Visible = true;
        }
    }

	private Control FindContainer(Node root)
	{
		if (root is VBoxContainer || root is HBoxContainer) return (Control)root;
		foreach (Node child in root.GetChildren())
		{
			var result = FindContainer(child);
			if (result != null) return result;
		}
		return null;
	}

	private Button CreatePauseButton(string text, Texture2D icon, System.Action action)
	{
		var btn = new Button { Text = text, Icon = icon, ExpandIcon = true, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(280, 55) };
		btn.AddThemeConstantOverride("h_separation", 15);
		btn.AddThemeConstantOverride("icon_max_width", 24);

		var style = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.12f, 1f), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12, ContentMarginLeft = 20 };
		btn.AddThemeStyleboxOverride("normal", style);
		btn.AddThemeStyleboxOverride("hover", style); 
		btn.AddThemeStyleboxOverride("pressed", style);
		btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

		btn.MouseEntered += () => AnimateHover(btn, true, 1.05f);
		btn.MouseExited += () => AnimateHover(btn, false, 1.0f);
		btn.Pressed += () => { AnimateClick(btn); action?.Invoke(); };

		return btn;
	}

	public void OnResumePressed() => TogglePauseMenu(false);
	public void OnRestartPressed() { GetTree().Paused = false; GameManager.Instance.RestartScenario(); }
	public void OnSettingsPausePressed() => SettingsUI.Instance?.ToggleSettings(true);
	public void OnMainMenuPressed() { GetTree().Paused = false; GameManager.Instance.LoadMainMenu(); }

	// --- АНИМАЦИИ ---
	private void AnimateHover(Control control, bool isHovering, float targetScale)
	{
		// Звук наведения
		if (isHovering && SndHover != null) AudioManager.Instance?.PlayStream(SndHover, -5f);

		control.PivotOffset = control.Size / 2;
		var tween = CreateTween();
		tween.TweenProperty(control, "scale", isHovering ? new Vector2(targetScale, targetScale) : Vector2.One, 0.15f)
			 .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
	}

	private void AnimateClick(Control control)
	{
		// Звук клика
		if (SndClick != null) AudioManager.Instance?.PlayStream(SndClick);

		control.PivotOffset = control.Size / 2;
		var tween = CreateTween();
		tween.TweenProperty(control, "scale", new Vector2(0.95f, 0.95f), 0.05f).SetTrans(Tween.TransitionType.Bounce);
		tween.TweenProperty(control, "scale", new Vector2(1.04f, 1.04f), 0.15f);
	}

	// --- ИНФОРМАЦИОННЫЕ ПАНЕЛИ ---
	public void UpdatePrompt(string text, bool isError = false)
	{
		if (_promptLabel == null) return;

		// --- ИСПРАВЛЕНИЕ: ПРИОРЕТЕТЫ СООБЩЕНИЙ ---
		if (isError)
		{
			_promptLabel.Text = text;
			_promptLabel.Modulate = Colors.Red;
			_messageLockTimer = 3.0f; // Увеличим до 3 секунд для аварий
			return;
		}

		// Если висит блокировка от ошибки - сохраняем текст в кэш, но не выводим
		_normalPromptCache = text;
		
		if (_messageLockTimer <= 0)
		{
			_promptLabel.Text = text;
			_promptLabel.Modulate = Colors.White;
		}
	}

	public void ShowWorldTooltip(string name, string desc, string states = "")
	{
		if (_worldTooltipPanel == null) return;

		if (string.IsNullOrEmpty(name))
		{
			_worldTooltipPanel.Visible = false;
		}
		else
		{
			_worldTooltipPanel.Visible = true;
			if (_worldNameLabel != null) _worldNameLabel.Text = name;
			
			bool hasDesc = !string.IsNullOrEmpty(desc);
			if (_worldDescLabel != null) { _worldDescLabel.Text = desc; _worldDescLabel.Visible = hasDesc; }
			if (_worldSeparator != null) _worldSeparator.Visible = hasDesc;

			bool hasStates = !string.IsNullOrEmpty(states);
			if (_worldStatesLabel != null) { _worldStatesLabel.Text = states; _worldStatesLabel.Visible = hasStates; }
			if (_worldSeparator2 != null) _worldSeparator2.Visible = hasStates;
		}
	}

	public void UpdateTorqueUI(float current, float max)
	{
		if (_torqueProgressBar == null) return;
		_torqueProgressBar.Visible = true;
		_torqueNumbersLabel.Visible = true;
		_torqueProgressBar.MaxValue = max;
		_torqueProgressBar.Value = max - current; 
		_torqueNumbersLabel.Text = $"{current:F1} / {max} Н·м";
	}

	public void HideTorqueUI()
	{
		if (_torqueProgressBar != null) _torqueProgressBar.Visible = false;
		if (_torqueNumbersLabel != null) _torqueNumbersLabel.Visible = false;
	}

	public void UpdateCrosshairColor(Color color)
	{
		if (_crosshair != null) _crosshair.Color = _crosshair.Color.Lerp(color, 15f * (float)GetProcessDeltaTime());
	}

	public void UpdateTaskUI(int currentStep, int totalSteps, string taskName, string taskDesc = "")
	{
		if (_taskTrackerPanel != null) _taskTrackerPanel.Visible = (totalSteps > 0);

		if (_taskNameLabel != null)
		{
			_taskNameLabel.Text = $"ЗАДАЧА: {taskName} ({currentStep}/{totalSteps})";
			_taskNameLabel.Modulate = new Color(0.2f, 0.9f, 0.2f); 
		}

		bool hasDesc = !string.IsNullOrEmpty(taskDesc);

		if (_taskDescLabel != null)
		{
			_taskDescLabel.Text = taskDesc;
			_taskDescLabel.Visible = hasDesc;
		}

		if (_taskSeparator != null) _taskSeparator.Visible = hasDesc;

		if (_taskProgressBar != null)
		{
			_taskProgressBar.MaxValue = totalSteps;
			_taskProgressBar.Value = currentStep;
			_taskProgressBar.CustomMinimumSize = new Vector2(0, 12);
		}
	}

    public void ResetUIForNewLevel()
    {
        _isPaused = false;
        GetTree().Paused = false;
        
        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.Visible = false;
            _pauseMenuPanel.Modulate = new Color(1, 1, 1, 0);
        }

        if (_inventoryWindow != null)
        {
            _inventoryWindow.Visible = false;
            _inventoryWindow.Modulate = new Color(1, 1, 1, 0);
        }

        // Полный сброс кэша текстов и таймеров ошибок
        _messageLockTimer = 0.0f;
        _normalPromptCache = "";
        
        if (_promptLabel != null)
        {
            _promptLabel.Text = "";
            _promptLabel.Modulate = Colors.White;
        }

        ShowWorldTooltip("", "", "");
        HideTorqueUI();
        
        if (_crosshair != null) _crosshair.Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }
}
