using Godot;

public partial class DeveloperConsole : CanvasLayer
{
    public static DeveloperConsole Instance { get; private set; }

    private Panel _bgPanel;
    private RichTextLabel _output;
    private LineEdit _input;
    private bool _isOpen = false;
	private Input.MouseModeEnum _previousMouseMode;

	public override void _EnterTree()
    {
        Instance = this;
        Layer = 100;

        System.AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            LogError($"[СИСТЕМНЫЙ СБОЙ] {args.ExceptionObject}");
        };

        _bgPanel = new Panel();
        _bgPanel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _bgPanel.CustomMinimumSize = new Vector2(0, 350);
        var style = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.05f, 0.95f), BorderWidthBottom = 2, BorderColor = Colors.Green };
        _bgPanel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("margin_left", 10);
        vbox.AddThemeConstantOverride("margin_right", 10);
        _bgPanel.AddChild(vbox);

        _output = new RichTextLabel();
        _output.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _output.BbcodeEnabled = true;
        _output.ScrollFollowing = true;
        _output.AddThemeFontOverride("normal_font", ThemeDB.FallbackFont);
        
        _output.SelectionEnabled = true; 

        vbox.AddChild(_output);

        _input = new LineEdit();
        _input.PlaceholderText = "Введите команду (например: clear, help)...";
        _input.TextSubmitted += OnCommandSubmitted;
        vbox.AddChild(_input);

        AddChild(_bgPanel);
        _bgPanel.Visible = false;
        
        Log("Тренажер инициализирован. Консоль разработчика активна.", "green");
    }
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            // Открытие/Закрытие (~)
            if (keyEvent.Keycode == Key.Quoteleft) 
            {
                ToggleConsole();
                GetViewport().SetInputAsHandled();
            }
            // Закрытие на ESC
            else if (_isOpen && keyEvent.Keycode == Key.Escape)
            {
                ToggleConsole(false);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public void ToggleConsole(bool? forceState = null)
    {
        bool nextState = forceState ?? !_isOpen;
        if (_isOpen == nextState) return;

        _isOpen = nextState;
        _bgPanel.Visible = _isOpen;
        
        if (_isOpen)
        {
            _previousMouseMode = Input.MouseMode;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _input.GrabFocus();
            if (UIManager.Instance != null) UIManager.Instance.ToggleFullInventory(false); 
        }
        else
        {
            _input.ReleaseFocus();

            // Проверяем, на какой сцене мы находимся
            if (GetTree().CurrentScene != null && GetTree().CurrentScene.Name == "MainMenu")
            {
                Input.MouseMode = Input.MouseModeEnum.Visible; // В меню всегда видна
            }
            else
            {
                Input.MouseMode = _previousMouseMode; // В игре возвращаем как было
            }
        }
    }

    public void Log(string message, string color = "white")
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");

        if (_output != null) 
        {
            _output.Text += $"[color=gray][{timestamp}][/color] [color={color}]{message}[/color]\n";
        }
        
        GD.Print($"[Console] [{timestamp}] {message}");
    }

    public void LogError(string error)
    {
        Log($"[ERROR] {error}", "red");
        if (!_isOpen) ToggleConsole(true);
    }

    private void OnCommandSubmitted(string text)
    {
        _input.Clear();
        Log($"> {text}", "gray");
        
        if (text.ToLower() == "clear") _output.Text = "";
        else if (text.ToLower() == "help") Log("Доступные команды: clear, help, restart, menu", "yellow");
        else if (text.ToLower() == "restart") GameManager.Instance?.RestartScenario();
        else if (text.ToLower() == "menu") GameManager.Instance?.LoadMainMenu();
        else Log("Неизвестная команда.", "red");
    }
}