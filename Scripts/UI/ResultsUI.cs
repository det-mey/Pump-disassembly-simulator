using Godot;
using System;

public partial class ResultsUI : Control
{
    public static ResultsUI Instance { get; private set; }

    [Export] private PanelContainer _mainPanel; // Перетащите сюда ваш ActualWindow

    private RichTextLabel _reportLabel;
    private FileDialog _saveDialog;
    private ColorRect _overlay; // Затемняющий щит
    private VBoxContainer _contentVBox;

    public override void _EnterTree()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        // 1. Создаем фоновый щит
        _overlay = new ColorRect {
            Color = new Color(0, 0, 0, 0.85f),
            MouseFilter = MouseFilterEnum.Stop, // Блокирует клики в мир
            Visible = false
        };
        _overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_overlay);
        // Перемещаем щит за окно, чтобы окно было сверху
        MoveChild(_overlay, 0);

        // 2. Инициализируем диалог сохранения ОДИН РАЗ
        _saveDialog = new FileDialog { 
            Access = FileDialog.AccessEnum.Filesystem, 
            FileMode = FileDialog.FileModeEnum.SaveFile,
            Title = "Сохранить протокол",
            Filters = new string[] { "*.html ; HTML Report" },
            Size = new Vector2I(800, 500),
            Exclusive = true,
            Visible = false
        };
        _saveDialog.FileSelected += OnFileSaveSelected;
        AddChild(_saveDialog);

        if (_mainPanel != null)
        {
            SetupPanelStyle();
            BuildInternalUI();
            _mainPanel.Visible = false;
        }
        
        Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (Visible && @event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            if (!_saveDialog.Visible) 
            {
                OnBackToMenuPressed();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void SetupPanelStyle()
    {
        var style = new StyleBoxFlat { 
            BgColor = new Color(0.08f, 0.08f, 0.08f, 1f), 
            CornerRadiusTopLeft = 25, CornerRadiusTopRight = 25, 
            CornerRadiusBottomLeft = 25, CornerRadiusBottomRight = 25,
            ContentMarginLeft = 40, ContentMarginTop = 40, 
            ContentMarginRight = 40, ContentMarginBottom = 40,
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1, 
            BorderColor = new Color(1, 1, 1, 0.1f)
        };
        _mainPanel.AddThemeStyleboxOverride("panel", style);
    }

    private void BuildInternalUI()
    {
        // 1. Очищаем старое содержимое
        foreach (Node child in _mainPanel.GetChildren()) child.QueueFree();

        // Основной вертикальный стек
        _contentVBox = new VBoxContainer();
        _contentVBox.AddThemeConstantOverride("separation", 25);
        // Заставляем стек заполнять всю панель
        _contentVBox.SetAnchorsPreset(LayoutPreset.FullRect);
        _mainPanel.AddChild(_contentVBox);

        // --- ИСПРАВЛЕНИЕ: СКРОЛЛ ЗОНА ---
        var scroll = new ScrollContainer { 
            // КРИТИЧЕСКИЙ ФИКС: Заставляем скролл забирать всё свободное место по вертикали
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            // Задаем минимальную высоту, чтобы он не схлопывался
            CustomMinimumSize = new Vector2(0, 350) 
        };
        _contentVBox.AddChild(scroll);

        // --- ИСПРАВЛЕНИЕ: ЛАЙБЕЛ ОТЧЕТА ---
        _reportLabel = new RichTextLabel { 
            // Заставляем текст заполнять скролл
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            BbcodeEnabled = true, 
            SelectionEnabled = true, 
            FocusMode = Control.FocusModeEnum.Click,
            // Фикс для корректного отображения больших текстов
            FitContent = false 
        };
        scroll.AddChild(_reportLabel);

        // Сетка кнопок (нижняя часть)
        var btnHBox = new HBoxContainer();
        btnHBox.AddThemeConstantOverride("separation", 15);
        btnHBox.Alignment = BoxContainer.AlignmentMode.Center;
        _contentVBox.AddChild(btnHBox);

        // Кнопки
        btnHBox.AddChild(CreateStyledButton("СОХРАНИТЬ", new Color(0.2f, 0.6f, 1f), () => _saveDialog.PopupCentered()));
        btnHBox.AddChild(CreateStyledButton("ПЕЧАТЬ", new Color(0.2f, 1f, 0.6f), OnPrintPressed));
        btnHBox.AddChild(CreateStyledButton("ПОВТОРИТЬ", new Color(1f, 0.8f, 0.2f), OnRetryPressed));
        btnHBox.AddChild(CreateStyledButton("В МЕНЮ", Colors.White, OnBackToMenuPressed));
    }

    public async void ShowResults()
    {
        if (UIManager.Instance != null) UIManager.Instance.TogglePauseMenu(false);
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetTree().Paused = true;
        
        string finalGrade = ActionLogger.Instance.CalculateGrade();
        ProgressManager.Instance?.SaveResult(GameManager.Instance.CurrentScenario.ScenarioName, ActionLogger.Instance.Score, finalGrade);

        _reportLabel.Text = ActionLogger.Instance.GenerateFullReport();
        
        // Показываем корень, оверлей и окно
        Visible = true;
        _overlay.Visible = true;
        _mainPanel.Visible = true;

        _contentVBox.QueueSort(); // Пересчитывает позиции детей
        
        // Анимация окна
        _mainPanel.Modulate = new Color(1, 1, 1, 0);
        _mainPanel.Scale = new Vector2(0.9f, 0.9f);

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _mainPanel.PivotOffset = _mainPanel.Size / 2;

        var tween = CreateTween().SetParallel(true).SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(_mainPanel, "modulate:a", 1.0f, 0.4f);
        tween.TweenProperty(_mainPanel, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void OnFileSaveSelected(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(ActionLogger.Instance.GenerateHtmlReport());
        DeveloperConsole.Instance?.Log($"Протокол сохранен: {path}", "green");
    }

    private void OnPrintPressed()
    {
        string tempPath = OS.GetUserDataDir().PathJoin("print_temp.html");
        using var file = FileAccess.Open(tempPath, FileAccess.ModeFlags.Write);
        file?.StoreString(ActionLogger.Instance.GenerateHtmlReport());
        file?.Close();
        OS.ShellOpen(ProjectSettings.GlobalizePath(tempPath));
    }

    private void OnRetryPressed() 
    { 
        Visible = false; _overlay.Visible = false;
        GetTree().Paused = false; 
        GameManager.Instance.RestartScenario(); 
    }

    private void OnBackToMenuPressed() 
    { 
        Visible = false; _overlay.Visible = false;
        GetTree().Paused = false; 
        GameManager.Instance.LoadMainMenu(); 
    }

    private Button CreateStyledButton(string text, Color color, Action action)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(170, 50), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var style = new StyleBoxFlat { BgColor = new Color(0.15f, 0.15f, 0.15f, 1f), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 };
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeStyleboxOverride("hover", style);
        btn.AddThemeStyleboxOverride("pressed", style);
        btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        btn.AddThemeColorOverride("font_color", color);
        
        btn.MouseEntered += () => AnimateHover(btn, true, 1.05f);
        btn.MouseExited += () => AnimateHover(btn, false, 1.0f);
        btn.Pressed += () => { AnimateClick(btn); action?.Invoke(); };
        return btn;
    }

    private void AnimateHover(Control c, bool h, float s) {
        c.PivotOffset = c.Size / 2;
        if (h && UIManager.Instance?.SndHover != null) AudioManager.Instance?.PlayStream(UIManager.Instance.SndHover, -5f);
        CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).TweenProperty(c, "scale", h ? new Vector2(s,s) : Vector2.One, 0.15f).SetTrans(Tween.TransitionType.Sine);
    }
    
    private void AnimateClick(Control c) {
        c.PivotOffset = c.Size / 2;
        if (UIManager.Instance?.SndClick != null) AudioManager.Instance?.PlayStream(UIManager.Instance.SndClick);
        var t = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        t.TweenProperty(c, "scale", new Vector2(0.95f, 0.95f), 0.05f);
        t.TweenProperty(c, "scale", new Vector2(1.05f, 1.05f), 0.15f);
    }
}