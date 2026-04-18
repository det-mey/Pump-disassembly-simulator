using Godot;
using System;

public partial class LoadingScreen : CanvasLayer
{
    public static LoadingScreen Instance { get; private set; }

    // Графические узлы
    private ColorRect _bgRect;
    private PanelContainer _mainPanel;
    private ProgressBar _progressBar;
    private Label _tipLabel;
    
    private string _scenePath;
    private Action _onLoadComplete;
    private bool _isLoading = false;

    // --- ИСПРАВЛЕНИЕ IDE0300: Современный синтаксис коллекций C# 12 ---
    private readonly string[] _tips =[
        "ПОДСКАЗКА: Используйте ПКМ для подбора и снятия деталей без инструмента.",
        "ПОДСКАЗКА: Внимательно читайте требования сценария — нарушение порядка приведет к штрафу.",
        "ПОДСКАЗКА: Детали со следами ржавчины необходимо сначала обработать химией.",
        "ПОДСКАЗКА: В режиме Экзамена визуальные подсказки отключены. Полагайтесь на свой опыт.",
        "ПОДСКАЗКА: Если деталь заблокирована, убедитесь, что вы открутили все смежные крепежи.",
        "ПОДСКАЗКА: Тяжелые предметы в руках замедляют скорость передвижения."
    ];

    public override void _EnterTree()
    {
        Instance = this;
        Layer = 150; // Самый высокий слой, чтобы перекрыть абсолютно ВСЁ
        ProcessMode = ProcessModeEnum.Always; 
        
        CreateUI();
        Visible = false;
    }

    private void CreateUI()
    {
        // --- ИСПРАВЛЕНИЕ CS0103: Анимируем фон, а не сам CanvasLayer ---
        _bgRect = new ColorRect { Color = new Color(0.02f, 0.02f, 0.02f, 1f) };
        _bgRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_bgRect);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _bgRect.AddChild(center);

        // --- ДИЗАЙН В СТИЛЕ ГЛАВНОГО МЕНЮ ---
        _mainPanel = new PanelContainer { CustomMinimumSize = new Vector2(700, 0) };
        var panelStyle = new StyleBoxFlat { 
            BgColor = new Color(0.05f, 0.05f, 0.05f, 1f), 
            CornerRadiusTopLeft = 25, CornerRadiusBottomRight = 25, 
            CornerRadiusTopRight = 25, CornerRadiusBottomLeft = 25, 
            ContentMarginLeft = 40, ContentMarginTop = 40, 
            ContentMarginRight = 40, ContentMarginBottom = 40, 
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1, 
            BorderColor = new Color(1, 1, 1, 0.1f) 
        };
        _mainPanel.AddThemeStyleboxOverride("panel", panelStyle);
        // Pivot в центр для красивой анимации увеличения
        _mainPanel.PivotOffset = new Vector2(350, 100); 
        center.AddChild(_mainPanel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 25);
        _mainPanel.AddChild(vbox);

        var title = new Label { Text = "ЗАГРУЗКА СИМУЛЯЦИИ", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", Colors.White);
        vbox.AddChild(title);

        _progressBar = new ProgressBar { CustomMinimumSize = new Vector2(0, 15) };
        _progressBar.ShowPercentage = false; // Убираем стандартные цифры процентов для эстетики
        
        var styleBg = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.12f, 1f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };
        var styleFill = new StyleBoxFlat { BgColor = new Color(0.2f, 0.8f, 0.2f, 1f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };
        _progressBar.AddThemeStyleboxOverride("background", styleBg);
        _progressBar.AddThemeStyleboxOverride("fill", styleFill);
        vbox.AddChild(_progressBar);

        _tipLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _tipLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1f));
        _tipLabel.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(_tipLabel);
    }

    public void LoadScene(string path, Action onComplete = null)
    {
        _scenePath = path;
        _onLoadComplete = onComplete;
        _progressBar.Value = 0;
        
        // Выбираем случайную подсказку
        _tipLabel.Text = _tips[GD.RandRange(0, _tips.Length - 1)];

        Visible = true;
        _bgRect.Modulate = new Color(1, 1, 1, 0); // Прозрачный старт
        _mainPanel.Scale = new Vector2(0.85f, 0.85f); // Сжатый старт
        
        // --- АНИМАЦИЯ ПОЯВЛЕНИЯ ЭКРАНА ---
        var tween = CreateTween().SetParallel(true).SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(_bgRect, "modulate:a", 1.0f, 0.3f).SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(_mainPanel, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        
        // Запуск тяжелой загрузки только после того, как экран потемнел
        tween.Chain().TweenCallback(Callable.From(() => {
            Error err = ResourceLoader.LoadThreadedRequest(_scenePath, "PackedScene");
            if (err == Error.Ok)
            {
                _isLoading = true;
            }
            else
            {
                DeveloperConsole.Instance?.LogError($"[LoadingScreen] Ошибка фонового запроса: {err}");
                Visible = false;
            }
        }));
    }

    public override void _Process(double delta)
    {
        if (!_isLoading) return;

        var progressArray = new Godot.Collections.Array();
        ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(_scenePath, progressArray);

        if (status == ResourceLoader.ThreadLoadStatus.InProgress)
        {
            // Плавно интерполируем значение прогресс-бара
            double targetValue = (double)progressArray[0] * 100.0;
            _progressBar.Value = Mathf.Lerp(_progressBar.Value, targetValue, 10f * delta);
        }
        else if (status == ResourceLoader.ThreadLoadStatus.Loaded)
        {
            _isLoading = false;
            _progressBar.Value = 100.0;

            // Извлекаем сцену из оперативной памяти и переходим на неё
            var packedScene = ResourceLoader.LoadThreadedGet(_scenePath) as PackedScene;
            GetTree().ChangeSceneToPacked(packedScene);

            // Инициализация скриптов новой сцены
            _onLoadComplete?.Invoke();

            // --- АНИМАЦИЯ РАСТВОРЕНИЯ ПОВЕРХ ЗАГРУЖЕННОГО МИРА ---
            var tween = CreateTween().SetParallel(true).SetPauseMode(Tween.TweenPauseMode.Process);
            tween.TweenProperty(_bgRect, "modulate:a", 0.0f, 0.4f).SetDelay(0.2f).SetTrans(Tween.TransitionType.Sine);
            tween.TweenProperty(_mainPanel, "scale", new Vector2(1.05f, 1.05f), 0.4f).SetDelay(0.2f).SetTrans(Tween.TransitionType.Cubic);
            
            tween.Chain().TweenCallback(Callable.From(() => Visible = false));
        }
        else if (status == ResourceLoader.ThreadLoadStatus.Failed || status == ResourceLoader.ThreadLoadStatus.InvalidResource)
        {
            _isLoading = false;
            DeveloperConsole.Instance?.LogError("[LoadingScreen] Фоновая загрузка завершилась сбоем!");
            Visible = false;
        }
    }
}