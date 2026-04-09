using Godot;
using System.Linq;

public partial class SettingsUI : CanvasLayer
{
    [ExportGroup("Звуки Настроек")]
    [Export] private AudioStream _sndHover = GD.Load<AudioStream>("res://Assets/Sounds/select.mp3");
    [Export] private AudioStream _sndClick = GD.Load<AudioStream>("res://Assets/Sounds/click.mp3");
    [Export] private AudioStream _sndDropdown = GD.Load<AudioStream>("res://Assets/Sounds/dropdown.mp3");

    public static SettingsUI Instance { get; private set; }

    private PanelContainer _bgPanel;
    private CheckBox _fullscreenCheck, _vsyncCheck, _fpsCheck;
    private OptionButton _resOption, _msaaOption;
    private HSlider _scaleSlider, _volSlider, _sensSlider;
    private Label _scaleVal, _volVal, _sensVal;
    private OptionButton _winModeOption, _screenOption;

    private bool _tempFullscreen, _tempVSync, _tempShowFPS;
    private int _tempMsaa;
    private float _tempScale, _tempVol, _tempSens;
    private Vector2I _tempRes;
    private int _tempWinMode, _tempScreen;

    private System.Collections.Generic.List<Vector2I> _resolutions = new System.Collections.Generic.List<Vector2I> { 
        new Vector2I(1280, 720), 
        new Vector2I(1366, 768), 
        new Vector2I(1600, 900), 
        new Vector2I(1920, 1080), 
        new Vector2I(2560, 1440),
        new Vector2I(3840, 2160)
    };

    public override void _EnterTree()
    {
        Instance = this;
        Layer = 110;
        ProcessMode = ProcessModeEnum.Always;

        // Создаем UI немедленно при входе в дерево, чтобы ссылки не были null
        CreateUI();
        Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (Visible && @event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            ToggleSettings(false);
            GetViewport().SetInputAsHandled();
        }
    }

    private void CreateUI()
    {
        var overlay = new ColorRect { Color = new Color(0, 0, 0, 0.85f), MouseFilter = Control.MouseFilterEnum.Stop };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(overlay);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(center);

        _bgPanel = new PanelContainer { CustomMinimumSize = new Vector2(550, 0) };
        var style = new StyleBoxFlat { 
            BgColor = new Color(0.05f, 0.05f, 0.05f, 1f), 
            CornerRadiusTopLeft = 25, CornerRadiusBottomRight = 25, 
            CornerRadiusTopRight = 25, CornerRadiusBottomLeft = 25, 
            ContentMarginLeft = 35, ContentMarginTop = 35, 
            ContentMarginRight = 35, ContentMarginBottom = 35, 
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1, 
            BorderColor = new Color(1, 1, 1, 0.1f) 
        };
        _bgPanel.AddThemeStyleboxOverride("panel", style);
        center.AddChild(_bgPanel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 18);
        _bgPanel.AddChild(vbox);

        var title = new Label { Text = "НАСТРОЙКИ СИСТЕМЫ", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 26);
        vbox.AddChild(title);
        vbox.AddChild(new HSeparator());

        // --- ЗАМЕНА ПОЛНОЭКРАННОГО ЧЕКБОКСА НА СПИСОК ---
        _winModeOption = CreateDropdown("Режим окна", new[] { "Оконный", "Безрамочный", "Полноэкранный" }, vbox);
        _winModeOption.ItemSelected += (i) => _tempWinMode = (int)i;

        // --- ВЫБОР МОНИТОРА ---
        string[] screens = new string[DisplayServer.GetScreenCount()];
        for(int i=0; i<screens.Length; i++) screens[i] = $"Монитор {i+1}";
        _screenOption = CreateDropdown("Активный экран", screens, vbox);
        _screenOption.ItemSelected += (i) => _tempScreen = (int)i;

        Vector2I nativeRes = DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen());
        if (!_resolutions.Contains(nativeRes))
        {
            _resolutions.Add(nativeRes);
            // Сортируем список по количеству пикселей (по возрастанию)
            _resolutions.Sort((a, b) => (a.X * a.Y).CompareTo(b.X * b.Y)); 
        }

        _resOption = CreateDropdown("Разрешение экрана", _resolutions.Select(r => $"{r.X}x{r.Y}").ToArray(), vbox);
        _resOption.ItemSelected += (i) => _tempRes = _resolutions[(int)i];

        _vsyncCheck = new CheckBox { Text = "Вертикальная синхронизация (VSync)" };
        _vsyncCheck.Toggled += (b) => _tempVSync = b;
        vbox.AddChild(_vsyncCheck);

        _fpsCheck = new CheckBox { Text = "Показывать счетчик FPS" };
        _fpsCheck.Toggled += (b) => _tempShowFPS = b;
        vbox.AddChild(_fpsCheck);

        _msaaOption = CreateDropdown("Сглаживание (MSAA 3D)", new[] { "Выкл", "2x", "4x", "8x" }, vbox);
        _msaaOption.ItemSelected += (i) => _tempMsaa = (int)i;

        vbox.AddChild(new HSeparator());

        _scaleSlider = CreateSlider("Масштаб интерфейса", 0.5, 2.0, 0.1, vbox, out _scaleVal);
        _scaleSlider.ValueChanged += (v) => { _tempScale = (float)v; _scaleVal.Text = v.ToString("0.0"); };

        _volSlider = CreateSlider("Громкость", 0.0, 1.0, 0.05, vbox, out _volVal);
        _volSlider.ValueChanged += (v) => { _tempVol = (float)v; _volVal.Text = (v * 100).ToString("0") + "%"; };

        _sensSlider = CreateSlider("Чувств. мыши", 0.001, 0.01, 0.001, vbox, out _sensVal);
        _sensSlider.ValueChanged += (v) => { _tempSens = (float)v; _sensVal.Text = v.ToString("0.000"); };

        var btnBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnBox.AddThemeConstantOverride("separation", 20);
        vbox.AddChild(btnBox);

        var btnCancel = CreateStyledButton("ОТМЕНА", Colors.White);
        btnCancel.Pressed += () => ToggleSettings(false);
        btnBox.AddChild(btnCancel);

        var btnSave = CreateStyledButton("СОХРАНИТЬ", new Color(0.4f, 0.8f, 0.4f));
		btnSave.Pressed += () => {
            var sm = SettingsManager.Instance;
            sm.WindowModeIndex = _tempWinMode;
            sm.CurrentScreen = _tempScreen;
            sm.Resolution = _tempRes; 
            sm.VSync = _tempVSync;
            sm.Msaa3D = _tempMsaa;
            sm.ShowFPS = _tempShowFPS;
            sm.UiScale = _tempScale; 
            sm.MasterVolume = _tempVol; 
            sm.MouseSensitivity = _tempSens;
            
            sm.SaveSettings(); 
            ToggleSettings(false);
        };
        btnBox.AddChild(btnSave);
    }

    private OptionButton CreateDropdown(string labelText, string[] options, VBoxContainer parent)
    {
        var hbox = new HBoxContainer();
        hbox.AddChild(new Label { Text = labelText, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var ob = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
        foreach (var opt in options) ob.AddItem(opt);
        
        var style = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.12f, 1f), ContentMarginLeft = 10, CornerRadiusTopLeft = 5, CornerRadiusBottomRight = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5 };
        ob.AddThemeStyleboxOverride("normal", style);
        ob.AddThemeStyleboxOverride("hover", style);
        ob.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        // АНИМАЦИИ ДЛЯ ДРОПДАУНА
        ob.MouseEntered += () => AnimateHover(ob, true, 1.02f);
        ob.MouseExited += () => AnimateHover(ob, false, 1.0f);
        ob.ButtonDown += () => { 
            AnimateClick(ob);
            if (_sndDropdown != null) AudioManager.Instance?.PlayStream(_sndDropdown); 
        };

        hbox.AddChild(ob);
        parent.AddChild(hbox);
        return ob;
    }

    private HSlider CreateSlider(string labelText, double min, double max, double step, VBoxContainer parent, out Label valLabel)
    {
        parent.AddChild(new Label { Text = labelText });
        var hbox = new HBoxContainer();
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        valLabel = new Label { CustomMinimumSize = new Vector2(60, 0), HorizontalAlignment = HorizontalAlignment.Right };
        hbox.AddChild(slider);
        hbox.AddChild(valLabel);
        parent.AddChild(hbox);
        return slider;
    }

    private Button CreateStyledButton(string text, Color textColor)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(180, 45) };
        var bStyle = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.12f, 1f), CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10 };
        btn.AddThemeStyleboxOverride("normal", bStyle);
        btn.AddThemeStyleboxOverride("hover", bStyle);
        btn.AddThemeColorOverride("font_color", textColor);
        btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        // ПОДКЛЮЧАЕМ АНИМАЦИИ
        btn.MouseEntered += () => AnimateHover(btn, true, 1.05f);
        btn.MouseExited += () => AnimateHover(btn, false, 1.0f);
        btn.Pressed += () => AnimateClick(btn);

        return btn;
    }

    public void ToggleSettings(bool show)
    {
        if (show)
        {
            var sm = SettingsManager.Instance;
            _tempWinMode = sm.WindowModeIndex;
            _tempScreen = sm.CurrentScreen;
            _tempRes = sm.Resolution;
            _tempVSync = sm.VSync;
            _tempMsaa = sm.Msaa3D;
            _tempScale = sm.UiScale;
            _tempVol = sm.MasterVolume;
            _tempSens = sm.MouseSensitivity;
            _tempShowFPS = sm.ShowFPS;

            // БЕЗОПАСНАЯ УСТАНОВКА ЗНАЧЕНИЙ (Null Check)
            if (_winModeOption != null) _winModeOption.Select(_tempWinMode);
            if (_screenOption != null) _screenOption.Select(_tempScreen);
            if (_vsyncCheck != null) _vsyncCheck.ButtonPressed = _tempVSync;
            if (_fpsCheck != null) _fpsCheck.ButtonPressed = _tempShowFPS;
            
            if (_resOption != null) {
                int resIdx = _resolutions.IndexOf(_tempRes);
                if (resIdx >= 0) _resOption.Select(resIdx);
            }
            
            if (_msaaOption != null) _msaaOption.Select(_tempMsaa);
            if (_scaleSlider != null) _scaleSlider.Value = _tempScale;
            if (_volSlider != null) _volSlider.Value = _tempVol;
            if (_sensSlider != null) _sensSlider.Value = _tempSens;
        }
        Visible = show;
    }

    private void BindAudioToButton(BaseButton btn)
    {
        btn.MouseEntered += () => { 
            if (_sndHover != null) AudioManager.Instance?.PlayStream(_sndHover, -5f, (float)GD.RandRange(0.95, 1.05)); 
        };
        btn.Pressed += () => { 
            if (_sndClick != null) AudioManager.Instance?.PlayStream(_sndClick, 0f, (float)GD.RandRange(0.95, 1.05)); 
        };
    }

    private void AnimateHover(Control control, bool isHovering, float targetScale)
    {
        // Звук наведения
        if (isHovering && _sndHover != null) AudioManager.Instance?.PlayStream(_sndHover, -5f);

        control.PivotOffset = control.Size / 2;
        var tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(control, "scale", isHovering ? new Vector2(targetScale, targetScale) : Vector2.One, 0.15f)
             .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    private void AnimateClick(Control control)
    {
        if (_sndClick != null) AudioManager.Instance?.PlayStream(_sndClick);

        control.PivotOffset = control.Size / 2;
        var tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(control, "scale", new Vector2(0.95f, 0.95f), 0.05f).SetTrans(Tween.TransitionType.Bounce);
        tween.TweenProperty(control, "scale", new Vector2(1.05f, 1.05f), 0.15f);
    }
}