using Godot;
using Godot.Collections;

public partial class MainMenu : CanvasLayer
{
	[ExportGroup("Конфигурация (Данные)")]
	[Export] public Array<ScenarioData> Scenarios { get; set; } = new Array<ScenarioData>();

	[ExportGroup("Контейнеры (Из сцены)")]
	[Export] private PanelContainer _mainPanel;
	[Export] private VBoxContainer _cardsContainer;[Export] private GridContainer _buttonsGrid;

	[ExportGroup("Иконки кнопок (SVG|PNG)")]
	[Export] private Texture2D _iconMode;
	[Export] private Texture2D _iconResults;[Export] private Texture2D _iconNetwork;
	[Export] private Texture2D _iconStart;
	[Export] private Texture2D _iconSettings;
	[Export] private Texture2D _iconExit;

	[ExportGroup("Звуки Меню")]
    [Export] private AudioStream _sndHover;
    [Export] private AudioStream _sndClick;
    [Export] private AudioStream _sndDropdown;

	private int _selectedScenarioIndex = 0;
	private SimulatorMode _currentMode = SimulatorMode.Learning;
	private Array<PanelContainer> _cardNodes = new Array<PanelContainer>();
    private OptionButton _modeSelector;

	public override async void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;

		ApplyMainStyles();
		PopulateCards();
		PopulateButtons();
		
		if (Scenarios.Count > 0) SelectCard(0);
		
		// --- ИСПРАВЛЕНИЕ АНИМАЦИИ ЦЕНТРИРОВАНИЯ ---
		if (_mainPanel != null)
		{
			// Ждем один кадр, чтобы CenterContainer успел рассчитать идеальный центр по разрешению экрана
			await ToSignal(GetTree(), "process_frame"); 
			
			// Запоминаем правильную центральную позицию
			Vector2 finalPos = _mainPanel.Position; 
			
			_mainPanel.Modulate = new Color(1, 1, 1, 0);
			_mainPanel.Position = finalPos + new Vector2(0, 40); // Смещаем вниз относительно центра
			
			var tween = CreateTween().SetParallel(true);
			tween.TweenProperty(_mainPanel, "modulate", Colors.White, 0.6f).SetTrans(Tween.TransitionType.Cubic);
			// Анимируем обратно к рассчитанному центру!
			tween.TweenProperty(_mainPanel, "position", finalPos, 0.6f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}
	}

	private void ApplyMainStyles()
	{
		if (_mainPanel != null)
		{
			var mainStyle = new StyleBoxFlat {
				// Идеально черный матовый цвет (почти как у Apple)
				BgColor = new Color(0.05f, 0.05f, 0.05f, 0.95f), 
				CornerRadiusTopLeft = 25, CornerRadiusTopRight = 25,
				CornerRadiusBottomLeft = 25, CornerRadiusBottomRight = 25,
				ContentMarginLeft = 20, ContentMarginRight = 20,
				ContentMarginTop = 20, ContentMarginBottom = 20
			};
			_mainPanel.AddThemeStyleboxOverride("panel", mainStyle);
		}
	}

	// --- ГЕНЕРАЦИЯ СПИСКА КАРТОЧЕК ---
    private void PopulateCards()
    {
        if (_cardsContainer == null) return;

        // --- ИСПРАВЛЕНИЕ СКРОЛЛА: Жестко отключаем горизонтальную прокрутку ---
        if (_cardsContainer.GetParent() is ScrollContainer scroll)
        {
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        }

        foreach (Node child in _cardsContainer.GetChildren()) child.QueueFree();
        _cardNodes.Clear();

        for (int i = 0; i < Scenarios.Count; i++)
        {
            int localIndex = i; 
            var scenario = Scenarios[localIndex];

            var cardWrapper = new MarginContainer();
            cardWrapper.AddThemeConstantOverride("margin_left", 15);
            cardWrapper.AddThemeConstantOverride("margin_right", 25); 
            cardWrapper.AddThemeConstantOverride("margin_top", 10);
            cardWrapper.AddThemeConstantOverride("margin_bottom", 10);

            var card = new PanelContainer { 
                CustomMinimumSize = new Vector2(0, 120), 
                MouseFilter = Control.MouseFilterEnum.Stop,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill 
            };

            cardWrapper.AddChild(card); 

            card.MouseEntered += () => AnimateHover(card, true, 1.03f);
            card.MouseExited += () => AnimateHover(card, false, 1.0f);
            
            card.GuiInput += (InputEvent @event) => {
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    SelectCard(localIndex);
                    AnimateClick(card); 
                }
            };

            var innerMargin = new MarginContainer();
            innerMargin.AddThemeConstantOverride("margin_left", 15); 
            innerMargin.AddThemeConstantOverride("margin_top", 15);
            innerMargin.AddThemeConstantOverride("margin_right", 15); 
            innerMargin.AddThemeConstantOverride("margin_bottom", 15);
            card.AddChild(innerMargin);

            var hbox = new HBoxContainer(); 
            hbox.AddThemeConstantOverride("separation", 20);
            innerMargin.AddChild(hbox);

            var icon = new TextureRect {
                Texture = scenario.PreviewImage,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(90, 90)
            };
            hbox.AddChild(icon);

            var vbox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var title = new Label { Text = scenario.ScenarioName };
            title.AddThemeFontSizeOverride("font_size", 20);
            
            // --- ИСПРАВЛЕНИЕ ТЕКСТА: Добавлен CustomMinimumSize ---
            var desc = new Label { 
                Text = scenario.Description, 
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                // Магия Godot: эта строка ломает бесконечное расширение контейнера 
                // и заставляет текст послушно переноситься на новую строку
                CustomMinimumSize = new Vector2(10, 0) 
            };
            desc.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f)); 
            desc.AddThemeFontSizeOverride("font_size", 14);

            vbox.AddChild(title);
            vbox.AddChild(desc);
            hbox.AddChild(vbox);

            _cardsContainer.AddChild(cardWrapper);
            _cardNodes.Add(card); 
        }
    }

	// --- ЛОГИКА ВЫДЕЛЕНИЯ КАРТОЧКИ ---
	private void SelectCard(int index)
	{
		if (index < 0 || index >= Scenarios.Count) return;
		_selectedScenarioIndex = index;

		for (int i = 0; i < _cardNodes.Count; i++)
		{
			bool isActive = (i == index);
			var style = new StyleBoxFlat {
				BgColor = isActive ? new Color(0.12f, 0.12f, 0.12f, 1f) : new Color(0.15f, 0.15f, 0.15f, 1f),
				CornerRadiusTopLeft = 15, CornerRadiusTopRight = 15,
				CornerRadiusBottomLeft = 15, CornerRadiusBottomRight = 15,
				BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
				BorderColor = isActive ? new Color(0.4f, 0.8f, 0.4f, 1f) : Colors.Transparent // Зеленая обводка активного
			};
			_cardNodes[i].AddThemeStyleboxOverride("panel", style);
		}
	}

	// --- ГЕНЕРАЦИЯ СЕТКИ КНОПОК ---
	private void PopulateButtons()
	{
		if (_buttonsGrid == null) return;
		foreach (Node child in _buttonsGrid.GetChildren()) child.QueueFree();

        // --- ВЫПАДАЮЩИЙ СПИСОК РЕЖИМА ---
        _modeSelector = new OptionButton();
        _modeSelector.CustomMinimumSize = new Vector2(300, 50);
        _modeSelector.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_modeSelector.AddThemeConstantOverride("arrow_margin", 20);
		_modeSelector.ButtonDown += () => { 
            if (_sndDropdown != null) AudioManager.Instance?.PlayStream(_sndDropdown); 
        };
        _modeSelector.AddItem("Режим: ОБУЧЕНИЕ", (int)SimulatorMode.Learning);
        _modeSelector.AddItem("Режим: ТРЕНИРОВКА", (int)SimulatorMode.Training);
        _modeSelector.AddItem("Режим: ЭКЗАМЕН", (int)SimulatorMode.Exam);
        
        var style = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.12f, 1f), CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 15 };
        _modeSelector.AddThemeStyleboxOverride("normal", style);
        _modeSelector.AddThemeStyleboxOverride("hover", style);
        _modeSelector.AddThemeStyleboxOverride("pressed", style);

        _buttonsGrid.AddChild(_modeSelector);

		_buttonsGrid.AddChild(CreateMenuButton("Запустить", _iconStart, StartGame, true));

		_buttonsGrid.AddChild(CreateMenuButton("Результаты работ", _iconResults, () => ResultsHistoryUI.Instance?.ShowScreen()));
		_buttonsGrid.AddChild(CreateMenuButton("Настройки", _iconSettings, () => SettingsUI.Instance?.ToggleSettings(true)));

		// TODO: Сетевая работа — временно скрыто до реализации
		// _buttonsGrid.AddChild(CreateMenuButton("Сетевая работа", _iconNetwork, () => { }));

		_buttonsGrid.AddChild(CreateMenuButton("Выход", _iconExit, () => GetTree().Quit(), false, true));
	}

	private Button CreateMenuButton(string text, Texture2D icon, System.Action action, bool isPrimary = false, bool isDanger = false)
	{
		var btn = new Button();
		btn.Text = text; // Больше никаких пробелов!
		btn.Icon = icon;
		btn.ExpandIcon = true;
		btn.Alignment = HorizontalAlignment.Left;
		btn.CustomMinimumSize = new Vector2(300, 50); 
		btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		
		// --- ИДЕАЛЬНОЕ ВЫРАВНИВАНИЕ ИКОНОК ---
		btn.AddThemeConstantOverride("h_separation", 15); // Отступ текста от иконки
		btn.AddThemeConstantOverride("icon_max_width", 24); // Жесткий размер иконки, чтобы они не прыгали
		
		btn.AddThemeColorOverride("icon_normal_color", new Color(0.8f, 0.8f, 0.8f));

		var normalStyle = new StyleBoxFlat {
			BgColor = new Color(0.12f, 0.12f, 0.12f, 1f), // Чуть светлее черного фона
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
			ContentMarginLeft = 20 // Отступ иконки от левого края кнопки
		};
		
		if (isPrimary) normalStyle.BorderColor = new Color(0.2f, 0.6f, 0.2f, 1f); 
		if (isDanger) normalStyle.BorderColor = new Color(0.6f, 0.2f, 0.2f, 1f);  
		if (isPrimary || isDanger) {
			normalStyle.BorderWidthTop = 2; normalStyle.BorderWidthBottom = 2;
			normalStyle.BorderWidthLeft = 2; normalStyle.BorderWidthRight = 2;
		}

		btn.AddThemeStyleboxOverride("normal", normalStyle);
		btn.AddThemeStyleboxOverride("hover", normalStyle); // Фон не меняем, меняем масштаб Твином!
		btn.AddThemeStyleboxOverride("pressed", normalStyle);
		btn.AddThemeStyleboxOverride("focus", normalStyle);

		// --- АНИМАЦИИ ---
		btn.MouseEntered += () => AnimateHover(btn, true, 1.04f);
		btn.MouseExited += () => AnimateHover(btn, false, 1.0f);
		
		btn.Pressed += () => {
			AnimateClick(btn);
			action?.Invoke();
		};
		
		return btn;
	}

	// --- ЗАПУСК ИГРЫ ---
    private void StartGame()
    {
        if (VRManager.Instance != null && !VRManager.Instance.IsVRMode)
        {
            VRManager.Instance.SetVRMode(true);
        }

        if (Scenarios.Count == 0 || _selectedScenarioIndex < 0) return;
        var scenario = Scenarios[_selectedScenarioIndex];
        
        // Жесткая проверка JSON перед стартом
        if (!GameManager.Instance.ValidateScenarioConfig(scenario))
        {
            return; 
        }

        _currentMode = _modeSelector != null ? (SimulatorMode)_modeSelector.GetSelectedId() : SimulatorMode.Learning;
        
        // --- ИСПРАВЛЕНИЕ: Вызываем загрузку напрямую. Экран загрузки сам сделает красивый переход ---
        GameManager.Instance.LoadScenario(scenario, _currentMode);
    }

	// --- ДВИЖОК АНИМАЦИЙ UI ---
	private void AnimateHover(Control control, bool isHovering, float targetScale)
	{
		// Звук наведения
        if (isHovering && _sndHover != null) 
            AudioManager.Instance?.PlayStream(_sndHover, -5f, (float)GD.RandRange(0.95, 1.05));

		control.PivotOffset = control.Size / 2;
		
		var tween = CreateTween();
		if (isHovering)
		{
			// Плавное увеличение
			tween.TweenProperty(control, "scale", new Vector2(targetScale, targetScale), 0.15f)
				 .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		}
		else
		{
			// Возврат в норму
			tween.TweenProperty(control, "scale", Vector2.One, 0.2f)
				 .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		}
	}

	private void AnimateClick(Control control)
	{
		// Звук клика
        if (_sndClick != null) 
            AudioManager.Instance?.PlayStream(_sndClick, 0f, (float)GD.RandRange(0.95, 1.05));

		control.PivotOffset = control.Size / 2;
		var tween = CreateTween();
		// Резкое сжатие (имитация физического нажатия)
		tween.TweenProperty(control, "scale", new Vector2(0.95f, 0.95f), 0.05f)
			 .SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
		// Возврат к размеру hover
		tween.TweenProperty(control, "scale", new Vector2(1.02f, 1.02f), 0.15f)
			 .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
	}
}
