using Godot;
using System;
using System.Collections.Generic;

public partial class ResultsHistoryUI : CanvasLayer
{
    public static ResultsHistoryUI Instance { get; private set; }

    // UI-узлы
    private ColorRect _overlay;
    private PanelContainer _mainPanel;
    private VBoxContainer _contentVBox;
    private ScrollContainer _scrollContainer;
    private VBoxContainer _cardsContainer;
    private Label _emptyLabel;

    // Кнопки панели
    private Button _btnBack;
    private Button _btnClearAll;
    private Button _btnRemoveOld;
    private Button _btnExportCsv;

    // Фильтры
    private OptionButton _filterMode;
    private OptionButton _filterScenario;

    // Детальный просмотр
    private PanelContainer _detailPanel;
    private RichTextLabel _detailReportLabel;
    private Button _btnDetailBack;
    private Button _btnDetailPrint;
    private Button _btnDetailSave;

    // Иконки
    private Texture2D _iconExportCsv;
    private Texture2D _iconClearOld;
    private Texture2D _iconClearAll;
    private Texture2D _iconPrint;
    private Texture2D _iconSaveHtml;
    private Texture2D _iconMenu;
    private Texture2D _iconBack;

    // Звуки
    private AudioStream _sndHover;
    private AudioStream _sndClick;

    // Стили
    private StyleBoxFlat _cardStyleNormal;
    private StyleBoxFlat _cardStyleHover;

    // Состояние
    private bool _isVisible = false;
    private bool _isDetailOpen = false;
    private string _currentFilterMode = "Все";
    private string _currentFilterScenario = "Все";

    public override void _EnterTree()
    {
        if (Instance != null) { QueueFree(); return; }
        Instance = this;
        Layer = 120;
        ProcessMode = ProcessModeEnum.Always;

        // Загрузка ресурсов
        _iconExportCsv = GD.Load<Texture2D>("res://Assets/UI/icon_export_csv.svg");
        _iconClearOld = GD.Load<Texture2D>("res://Assets/UI/icon_clear_old.svg");
        _iconClearAll = GD.Load<Texture2D>("res://Assets/UI/icon_clear_all.svg");
        _iconPrint = GD.Load<Texture2D>("res://Assets/UI/icon_print.svg");
        _iconSaveHtml = GD.Load<Texture2D>("res://Assets/UI/icon_save_html.svg");
        _iconMenu = GD.Load<Texture2D>("res://Assets/UI/icon_back.svg");
        _iconBack = GD.Load<Texture2D>("res://Assets/UI/icon_back.svg");

        BuildUI();
        InitializeStyles();
        Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isVisible) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            if (_isDetailOpen)
                CloseDetailView();
            else
                HideScreen();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUI()
    {
        // === ОВЕРЛЕЙ ===
        _overlay = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.85f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_overlay);

        // === ЦЕНТРАЛЬНЫЙ КОНТЕЙНЕР ===
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.AddChild(center);

        _mainPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2I(900, 600),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        center.AddChild(_mainPanel);
        _mainPanel.Visible = false;

        // === КОНТЕНТ ===
        _contentVBox = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _contentVBox.AddThemeConstantOverride("separation", 12);
        _mainPanel.AddChild(_contentVBox);

        // --- Заголовок ---
        var titleHBox = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titleHBox.AddThemeConstantOverride("separation", 10);

        var titleLabel = new Label { Text = "История результатов" };
        titleLabel.AddThemeFontSizeOverride("font_size", 26);
        titleLabel.AddThemeColorOverride("font_color", Colors.White);
        titleHBox.AddChild(titleLabel);

        titleHBox.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        _btnBack = CreateIconButton("В меню", _iconMenu, HideScreen, new Color(0.85f, 0.85f, 0.85f));
        _btnBack.CustomMinimumSize = new Vector2I(130, 40);
        titleHBox.AddChild(_btnBack);

        _contentVBox.AddChild(titleHBox);

        // --- Фильтры и кнопки действий ---
        var filterHBox = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        filterHBox.AddThemeConstantOverride("separation", 10);

        var filterModeLabel = new Label { Text = "Режим:" };
        filterModeLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        filterModeLabel.AddThemeFontSizeOverride("font_size", 13);
        filterHBox.AddChild(filterModeLabel);

        _filterMode = CreateFilterDropdown();
        _filterMode.AddItem("Все", 0);
        _filterMode.AddItem("Обучение", 1);
        _filterMode.AddItem("Тренировка", 2);
        _filterMode.AddItem("Экзамен", 3);
        _filterMode.Selected = 0;
        _filterMode.ItemSelected += (id) => { _currentFilterMode = _filterMode.GetItemText((int)id); RefreshList(); };
        filterHBox.AddChild(_filterMode);

        var filterScenarioLabel = new Label { Text = "Сценарий:" };
        filterScenarioLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        filterScenarioLabel.AddThemeFontSizeOverride("font_size", 13);
        filterHBox.AddChild(filterScenarioLabel);

        _filterScenario = CreateFilterDropdown();
        _filterScenario.AddItem("Все", 0);
        _filterScenario.ItemSelected += (id) => { _currentFilterScenario = _filterScenario.GetItemText((int)id); RefreshList(); };
        filterHBox.AddChild(_filterScenario);

        filterHBox.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        _btnExportCsv = CreateIconButton("Экспорт CSV", _iconExportCsv, ExportToCsv, new Color(0.4f, 0.75f, 1f));
        _btnExportCsv.CustomMinimumSize = new Vector2I(155, 40);
        filterHBox.AddChild(_btnExportCsv);

        _btnRemoveOld = CreateIconButton("Старые >30д", _iconClearOld, () => ConfirmAndRemoveOld(), new Color(1f, 0.7f, 0.3f));
        _btnRemoveOld.CustomMinimumSize = new Vector2I(155, 40);
        filterHBox.AddChild(_btnRemoveOld);

        _btnClearAll = CreateIconButton("Очистить всё", _iconClearAll, () => ConfirmAndClearAll(), new Color(1f, 0.4f, 0.4f));
        _btnClearAll.CustomMinimumSize = new Vector2I(155, 40);
        filterHBox.AddChild(_btnClearAll);

        _contentVBox.AddChild(filterHBox);

        // --- Разделитель ---
        _contentVBox.AddChild(new HSeparator());

        // --- Скролл ---
        _scrollContainer = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2I(0, 380)
        };
        _contentVBox.AddChild(_scrollContainer);

        _cardsContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        _cardsContainer.AddThemeConstantOverride("separation", 6);
        _scrollContainer.AddChild(_cardsContainer);

        _emptyLabel = new Label
        {
            Text = "Нет сохранённых результатов.\nПройдите сценарий, чтобы увидеть здесь свои результаты.",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        _emptyLabel.AddThemeFontSizeOverride("font_size", 16);
        _emptyLabel.CustomMinimumSize = new Vector2I(0, 120);
        _cardsContainer.AddChild(_emptyLabel);
    }

    private OptionButton CreateFilterDropdown()
    {
        var opt = new OptionButton { CustomMinimumSize = new Vector2I(150, 38) };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.12f, 1f),
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12
        };
        opt.AddThemeStyleboxOverride("normal", style);
        opt.AddThemeStyleboxOverride("hover", style);
        opt.AddThemeStyleboxOverride("pressed", style);
        return opt;
    }

    private void InitializeStyles()
    {
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.06f, 0.97f),
            CornerRadiusTopLeft = 25, CornerRadiusTopRight = 25,
            CornerRadiusBottomLeft = 25, CornerRadiusBottomRight = 25,
            ContentMarginLeft = 25, ContentMarginTop = 18,
            ContentMarginRight = 25, ContentMarginBottom = 18,
            BorderWidthTop = 1, BorderWidthBottom = 1,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderColor = new Color(1, 1, 1, 0.08f)
        };
        _mainPanel.AddThemeStyleboxOverride("panel", panelStyle);

        _cardStyleNormal = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.10f, 0.10f, 1f),
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10
        };

        _cardStyleHover = new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.14f, 0.14f, 1f),
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10
        };
    }

    public void ShowScreen()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetTree().Paused = true;
        _isVisible = true;
        Visible = true;
        _overlay.Visible = true;
        _mainPanel.Visible = true;

        PopulateScenarioFilter();
        RefreshList();

        _mainPanel.Modulate = new Color(1, 1, 1, 0);
        _mainPanel.Scale = new Vector2(0.92f, 0.92f);

        var tween = CreateTween().SetParallel(true).SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(_mainPanel, "modulate:a", 1.0f, 0.3f).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(_mainPanel, "scale", Vector2.One, 0.3f)
             .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public void HideScreen()
    {
        _isVisible = false;
        GetTree().Paused = false;

        var tween = CreateTween().SetParallel(true).SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(_mainPanel, "modulate:a", 0.0f, 0.2f).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(_mainPanel, "scale", new Vector2(0.95f, 0.95f), 0.2f).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenCallback(Callable.From(() =>
        {
            Visible = false;
            _overlay.Visible = false;
            _mainPanel.Visible = false;
        }));
    }

    private void PopulateScenarioFilter()
    {
        var scenarios = new HashSet<string>();
        if (ResultsHistoryManager.Instance != null)
            foreach (var e in ResultsHistoryManager.Instance.History) scenarios.Add(e.ScenarioName);

        var cur = _filterScenario.GetItemText(_filterScenario.Selected);
        _filterScenario.Clear();
        _filterScenario.AddItem("Все", 0);
        int idx = 1;
        foreach (var s in scenarios)
        {
            _filterScenario.AddItem(s, idx);
            if (s == cur) _filterScenario.Selected = idx;
            idx++;
        }
    }

    private void RefreshList()
    {
        foreach (Node child in _cardsContainer.GetChildren()) child.QueueFree();

        if (ResultsHistoryManager.Instance == null || ResultsHistoryManager.Instance.History.Count == 0)
        {
            _cardsContainer.AddChild(_emptyLabel);
            _emptyLabel.Visible = true;
            return;
        }

        var filtered = FilterEntries(ResultsHistoryManager.Instance.History);
        if (filtered.Count == 0)
        {
            var lbl = new Label { Text = "Нет результатов по заданным фильтрам", HorizontalAlignment = HorizontalAlignment.Center };
            lbl.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            lbl.AddThemeFontSizeOverride("font_size", 15);
            lbl.CustomMinimumSize = new Vector2I(0, 80);
            _cardsContainer.AddChild(lbl);
            return;
        }

        foreach (var e in filtered)
            _cardsContainer.AddChild(CreateResultCard(e));
    }

    private List<ResultEntry> FilterEntries(List<ResultEntry> entries)
    {
        var result = new List<ResultEntry>();
        foreach (var e in entries)
        {
            if ((_currentFilterMode == "Все" || e.Mode == _currentFilterMode) &&
                (_currentFilterScenario == "Все" || e.ScenarioName == _currentFilterScenario))
                result.Add(e);
        }
        return result;
    }

    // ============================================================
    // КАРТОЧКА — фиксированная структура, ничего не растягивается
    // ============================================================
    private PanelContainer CreateResultCard(ResultEntry entry)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2I(0, 56),
            MouseFilter = Control.MouseFilterEnum.Stop,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        card.AddThemeStyleboxOverride("panel", _cardStyleNormal);

        card.MouseEntered += () =>
        {
            card.AddThemeStyleboxOverride("panel", _cardStyleHover);
            AnimateHover(card, true, 1.003f);
        };
        card.MouseExited += () =>
        {
            card.AddThemeStyleboxOverride("panel", _cardStyleNormal);
            AnimateHover(card, false, 1.0f);
        };

        card.GuiInput += (InputEvent @event) =>
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                ShowDetailView(entry);
                AnimateClick(card);
            }
        };

        // Внутренний контейнер
        var inner = new MarginContainer();
        inner.AddThemeConstantOverride("margin_left", 8);
        inner.AddThemeConstantOverride("margin_right", 4);
        inner.AddThemeConstantOverride("margin_top", 3);
        inner.AddThemeConstantOverride("margin_bottom", 3);
        card.AddChild(inner);

        // HBox: полоска | инфо | удалить
        var hbox = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill
        };
        hbox.AddThemeConstantOverride("separation", 8);
        inner.AddChild(hbox);

        // 1) Цветная полоска
        hbox.AddChild(GetGradeColorBar(entry.Grade));

        // 2) Информация
        var infoVBox = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill
        };
        infoVBox.AddThemeConstantOverride("separation", 0);
        hbox.AddChild(infoVBox);

        // Строка 1: дата + режим + время
        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", 6);

        row1.AddChild(MakeSmallLabel(entry.Date, new Color(0.5f, 0.5f, 0.5f)));
        row1.AddChild(MakeSmallLabel(entry.Mode, new Color(0.5f, 0.75f, 0.5f)));
        row1.AddChild(MakeSmallLabel(entry.FormattedTime, new Color(0.5f, 0.5f, 0.5f)));

        infoVBox.AddChild(row1);

        // Строка 2: сценарий | баллы | оценка
        var row2 = new HBoxContainer();
        row2.AddThemeConstantOverride("separation", 8);

        var lblScen = new Label
        {
            Text = entry.ScenarioName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        lblScen.AddThemeFontSizeOverride("font_size", 14);
        lblScen.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.92f));
        row2.AddChild(lblScen);

        row2.AddChild(MakeSmallLabel($"{entry.Score} баллов", new Color(0.6f, 0.6f, 0.6f)));

        var gradeLabel = new Label { Text = entry.Grade };
        gradeLabel.AddThemeFontSizeOverride("font_size", 13);
        gradeLabel.AddThemeColorOverride("font_color", GetGradeTextColor(entry.Grade));
        row2.AddChild(gradeLabel);

        infoVBox.AddChild(row2);

        // 3) Кнопка удаления — ФИКСИРОВАННЫЙ размер, НЕ растягивается
        var delBtn = CreateDeleteButton(() => DeleteEntry(entry.Id));
        hbox.AddChild(delBtn);

        return card;
    }

    private static ColorRect GetGradeColorBar(string grade)
    {
        var bar = new ColorRect
        {
            CustomMinimumSize = new Vector2I(4, 48),
            SizeFlagsVertical = Control.SizeFlags.Fill,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        bar.Color = GetGradeBarColor(grade);
        return bar;
    }

    private static Color GetGradeBarColor(string grade)
    {
        return grade switch
        {
            "5 (Отлично)" or "5 (ОТЛИЧНО)" => new Color(0.3f, 0.85f, 0.3f),
            "4 (Хорошо)" or "4 (ХОРОШО)" => new Color(0.55f, 0.85f, 0.3f),
            "3 (Удовл.)" or "3 (УДОВЛ.)" => new Color(1f, 0.75f, 0.2f),
            "2 (Незачет)" or "2 (НЕЗАЧЕТ)" => new Color(1f, 0.4f, 0.25f),
            "1 (Авария)" or "1 (НЕУДОВЛЕТВОРИТЕЛЬНО)" => new Color(0.85f, 0.15f, 0.15f),
            _ => new Color(0.45f, 0.45f, 0.45f)
        };
    }

    private static Color GetGradeTextColor(string grade)
    {
        return grade switch
        {
            "5 (Отлично)" or "5 (ОТЛИЧНО)" => new Color(0.4f, 1f, 0.4f),
            "4 (Хорошо)" or "4 (ХОРОШО)" => new Color(0.65f, 1f, 0.4f),
            "3 (Удовл.)" or "3 (УДОВЛ.)" => new Color(1f, 0.85f, 0.3f),
            "2 (Незачет)" or "2 (НЕЗАЧЕТ)" => new Color(1f, 0.5f, 0.35f),
            "1 (Авария)" or "1 (НЕУДОВЛЕТВОРИТЕЛЬНО)" => new Color(1f, 0.3f, 0.3f),
            _ => new Color(0.7f, 0.7f, 0.7f)
        };
    }

    private static Label MakeSmallLabel(string text, Color color)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeColorOverride("font_color", color);
        lbl.AddThemeFontSizeOverride("font_size", 11);
        return lbl;
    }

    private Button CreateDeleteButton(Action action)
    {
        var btn = new Button
        {
            Text = "✕",
            CustomMinimumSize = new Vector2I(28, 28),
            // Важно: НЕ задаём SizeFlags.ExpandFill — по умолчанию ShrinkBegin
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.22f, 0.06f, 0.06f, 1f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 0, ContentMarginRight = 0,
            ContentMarginTop = 0, ContentMarginBottom = 0
        };
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeStyleboxOverride("hover", style);
        btn.AddThemeStyleboxOverride("pressed", style);
        btn.AddThemeColorOverride("font_color", new Color(0.9f, 0.35f, 0.35f));
        btn.AddThemeFontSizeOverride("font_size", 14);

        btn.MouseEntered += () => AnimateHover(btn, true, 1.08f);
        btn.MouseExited += () => AnimateHover(btn, false, 1.0f);
        btn.Pressed += () => action?.Invoke();

        return btn;
    }

    private Button CreateTextButton(string text, Action action, Color fontColor)
    {
        var btn = new Button
        {
            Text = text,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.12f, 1f),
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 16, ContentMarginRight = 16
        };
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeStyleboxOverride("hover", style);
        btn.AddThemeStyleboxOverride("pressed", style);
        btn.AddThemeColorOverride("font_color", fontColor);

        btn.MouseEntered += () => AnimateHover(btn, true, 1.04f);
        btn.MouseExited += () => AnimateHover(btn, false, 1.0f);
        btn.Pressed += () => { AnimateClick(btn); action?.Invoke(); };

        return btn;
    }

    private Button CreateIconButton(string text, Texture2D icon, Action action, Color fontColor)
    {
        var btn = new Button
        {
            Text = text,
            Icon = icon,
            ExpandIcon = true,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };

        btn.AddThemeConstantOverride("h_separation", 8);
        btn.AddThemeConstantOverride("icon_max_width", 18);
        btn.AddThemeConstantOverride("icon_max_height", 18);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.12f, 1f),
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12, ContentMarginRight = 12
        };
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeStyleboxOverride("hover", style);
        btn.AddThemeStyleboxOverride("pressed", style);
        btn.AddThemeColorOverride("font_color", fontColor);
        // Иконка белая — задаём через стиль
        btn.AddThemeColorOverride("icon_normal_color", Colors.White);

        btn.MouseEntered += () => AnimateHover(btn, true, 1.04f);
        btn.MouseExited += () => AnimateHover(btn, false, 1.0f);
        btn.Pressed += () => { AnimateClick(btn); action?.Invoke(); };

        return btn;
    }

    private void DeleteEntry(string id)
    {
        ResultsHistoryManager.Instance?.RemoveEntry(id);
        RefreshList();
        PopulateScenarioFilter();
    }

    private void ConfirmAndClearAll()
    {
        if (ResultsHistoryManager.Instance == null || ResultsHistoryManager.Instance.History.Count == 0) return;
        var dlg = new ConfirmationDialog { Title = "Подтверждение", DialogText = "Удалить ВСЕ записи?", MinSize = new Vector2I(400, 130) };
        dlg.Confirmed += () => { ResultsHistoryManager.Instance?.ClearAll(); RefreshList(); PopulateScenarioFilter(); };
        AddChild(dlg); dlg.PopupCentered();
        dlg.TreeExiting += () => { if (IsInstanceValid(dlg)) dlg.QueueFree(); };
    }

    private void ConfirmAndRemoveOld()
    {
        var dlg = new ConfirmationDialog { Title = "Удалить старые", DialogText = "Удалить записи старше 30 дней?", MinSize = new Vector2I(400, 130) };
        dlg.Confirmed += () => { ResultsHistoryManager.Instance?.RemoveOlderThanDays(30); RefreshList(); PopulateScenarioFilter(); };
        AddChild(dlg); dlg.PopupCentered();
        dlg.TreeExiting += () => { if (IsInstanceValid(dlg)) dlg.QueueFree(); };
    }

    // ============================================================
    // ДЕТАЛЬНЫЙ ПРОСМОТР
    // ============================================================

    private void ShowDetailView(ResultEntry entry)
    {
        _isDetailOpen = true;
        _scrollContainer.Visible = false;
        _filterMode.Visible = false;
        _filterScenario.Visible = false;
        _btnExportCsv.Visible = false;
        _btnRemoveOld.Visible = false;
        _btnClearAll.Visible = false;

        BuildDetailView(entry);

        _detailPanel.Modulate = new Color(1, 1, 1, 0);
        _detailPanel.Scale = new Vector2(0.92f, 0.92f);

        var tween = CreateTween().SetParallel(true).SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(_detailPanel, "modulate:a", 1.0f, 0.25f).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(_detailPanel, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void CloseDetailView()
    {
        _isDetailOpen = false;

        var tween = CreateTween().SetParallel(true).SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(_detailPanel, "modulate:a", 0.0f, 0.2f).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(_detailPanel, "scale", new Vector2(0.95f, 0.95f), 0.2f).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenCallback(Callable.From(() =>
        {
            _detailPanel.QueueFree();
            _detailPanel = null;
            _scrollContainer.Visible = true;
            _filterMode.Visible = true;
            _filterScenario.Visible = true;
            _btnExportCsv.Visible = true;
            _btnRemoveOld.Visible = true;
            _btnClearAll.Visible = true;
        }));
    }

    private void BuildDetailView(ResultEntry entry)
    {
        _detailPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2I(900, 600),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            AnchorRight = 1f, AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Begin,
            GrowVertical = Control.GrowDirection.Begin,
            Position = new Vector2I(150, 60)
        };

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.06f, 0.97f),
            CornerRadiusTopLeft = 25, CornerRadiusTopRight = 25,
            CornerRadiusBottomLeft = 25, CornerRadiusBottomRight = 25,
            ContentMarginLeft = 25, ContentMarginTop = 18,
            ContentMarginRight = 25, ContentMarginBottom = 18,
            BorderWidthTop = 1, BorderWidthBottom = 1,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderColor = new Color(1, 1, 1, 0.08f)
        };
        _detailPanel.AddThemeStyleboxOverride("panel", panelStyle);
        _mainPanel.GetParent().AddChild(_detailPanel);

        var vbox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vbox.AddThemeConstantOverride("separation", 12);
        _detailPanel.AddChild(vbox);

        // Заголовок
        var titleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var titleLbl = new Label { Text = entry.ScenarioName };
        titleLbl.AddThemeFontSizeOverride("font_size", 22);
        titleLbl.AddThemeColorOverride("font_color", Colors.White);
        titleRow.AddChild(titleLbl);
        titleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        _btnDetailBack = CreateIconButton("Назад", _iconBack, CloseDetailView, new Color(0.85f, 0.85f, 0.85f));
        _btnDetailBack.CustomMinimumSize = new Vector2I(120, 40);
        titleRow.AddChild(_btnDetailBack);
        vbox.AddChild(titleRow);

        // Мета-информация
        var metaRow = new HBoxContainer();
        metaRow.AddThemeConstantOverride("separation", 15);
        metaRow.AddChild(MakeSmallLabel(entry.Date, new Color(0.5f, 0.5f, 0.5f)));
        metaRow.AddChild(MakeSmallLabel(entry.Mode, new Color(0.5f, 0.75f, 0.5f)));
        metaRow.AddChild(MakeSmallLabel(entry.FormattedTime, new Color(0.5f, 0.5f, 0.5f)));
        metaRow.AddChild(MakeSmallLabel($"{entry.Score} баллов", new Color(0.6f, 0.6f, 0.6f)));

        var gradeLbl = new Label { Text = entry.Grade };
        gradeLbl.AddThemeFontSizeOverride("font_size", 14);
        gradeLbl.AddThemeColorOverride("font_color", GetGradeTextColor(entry.Grade));
        metaRow.AddChild(gradeLbl);
        vbox.AddChild(metaRow);

        vbox.AddChild(new HSeparator());

        // Протокол — PlainTextLabel вместо RichTextLabel
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2I(0, 380)
        };
        vbox.AddChild(scroll);

        _detailReportLabel = new RichTextLabel
        {
            BbcodeEnabled = false, // Чистый текст — никаких BBCode тегов
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectionEnabled = true,
            FocusMode = Control.FocusModeEnum.Click,
            FitContent = false
        };
        _detailReportLabel.AddThemeColorOverride("default_color", new Color(0.82f, 0.82f, 0.82f));
        _detailReportLabel.AddThemeFontSizeOverride("normal_font_size", 13);
        scroll.AddChild(_detailReportLabel);

        if (!string.IsNullOrEmpty(entry.FullProtocolHtml))
            _detailReportLabel.Text = HtmlToPlainText(entry.FullProtocolHtml);
        else
            _detailReportLabel.Text = "Полный протокол недоступен.";

        // Кнопки
        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 15);
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;

        _btnDetailSave = CreateIconButton("Сохранить HTML", _iconSaveHtml, () => SaveDetailReport(entry), new Color(0.4f, 0.7f, 1f));
        _btnDetailSave.CustomMinimumSize = new Vector2I(180, 40);
        btnRow.AddChild(_btnDetailSave);

        _btnDetailPrint = CreateIconButton("Печать", _iconPrint, () => PrintDetailReport(entry), new Color(0.4f, 1f, 0.65f));
        _btnDetailPrint.CustomMinimumSize = new Vector2I(130, 40);
        btnRow.AddChild(_btnDetailPrint);

        vbox.AddChild(btnRow);

        _detailPanel.Visible = true;
    }

    // ============================================================
    // HTML → Plain Text (полная очистка <style>, <script>, HTML и BBCode тегов)
    // ============================================================

    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html)) return "Нет данных.";

        // 1. Полностью удаляем <style>...</style> блоки (включая содержимое)
        var result = System.Text.RegularExpressions.Regex.Replace(
            html, @"<style[^>]*>.*?</style>", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        // 2. Полностью удаляем <script>...</script> блоки
        result = System.Text.RegularExpressions.Regex.Replace(
            result, @"<script[^>]*>.*?</script>", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        // 3. Заменяем <br> на переводы строк ДО удаления остальных тегов
        result = result.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");
        result = result.Replace("</p>", "\n\n").Replace("</div>", "\n");
        result = result.Replace("<hr>", "\n---\n");

        // 4. Удаляем ВСЕ HTML-теги <...>
        result = System.Text.RegularExpressions.Regex.Replace(result, @"<[^>]+>", "");

        // 5. Удаляем ВСЕ BBCode-теги [...] (на случай если HTML содержал их как текст)
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\[[^\]]+\]", "");

        // 6. Декодируем HTML-сущности
        result = result.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
        result = result.Replace("&nbsp;", " ").Replace("&quot;", "\"");

        // 7. Убираем множественные пробелы и пустые строки
        result = System.Text.RegularExpressions.Regex.Replace(result, @"[ \t]+", " ");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\n\s*\n", "\n\n");

        return result.Trim();
    }

    // ============================================================
    // CSV ЭКСПОРТ
    // ============================================================

    private void ExportToCsv()
    {
        if (ResultsHistoryManager.Instance == null || ResultsHistoryManager.Instance.History.Count == 0) return;
        var filtered = FilterEntries(ResultsHistoryManager.Instance.History);
        if (filtered.Count == 0) return;

        var dlg = new FileDialog { Access = FileDialog.AccessEnum.Filesystem, FileMode = FileDialog.FileModeEnum.SaveFile, Title = "Экспорт CSV", Filters = new string[] { "*.csv" }, Size = new Vector2I(700, 400), Exclusive = true };
        dlg.FileSelected += (path) =>
        {
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            f?.StoreString(GenerateCsv(filtered));
        };
        AddChild(dlg); dlg.PopupCentered();
        dlg.TreeExiting += () => { if (IsInstanceValid(dlg)) dlg.QueueFree(); };
    }

    private static string GenerateCsv(List<ResultEntry> entries)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Дата,Сценарий,Режим,Оценка,Баллы,Время");
        foreach (var e in entries)
            sb.AppendLine($"{EscapeCsv(e.Date)},{EscapeCsv(e.ScenarioName)},{EscapeCsv(e.Mode)},{EscapeCsv(e.Grade)},{e.Score},{e.FormattedTime}");
        return sb.ToString();
    }

    private static string EscapeCsv(string v)
    {
        if (string.IsNullOrEmpty(v)) return "\"\"";
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return $"\"{v}\"";
    }

    private void SaveDetailReport(ResultEntry entry)
    {
        var dlg = new FileDialog { Access = FileDialog.AccessEnum.Filesystem, FileMode = FileDialog.FileModeEnum.SaveFile, Title = "Сохранить", Filters = new string[] { "*.html" }, Size = new Vector2I(700, 400), Exclusive = true };
        dlg.FileSelected += (path) =>
        {
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            f?.StoreString(entry.FullProtocolHtml);
        };
        AddChild(dlg); dlg.PopupCentered();
        dlg.TreeExiting += () => { if (IsInstanceValid(dlg)) dlg.QueueFree(); };
    }

    private void PrintDetailReport(ResultEntry entry)
    {
        string tmp = OS.GetUserDataDir().PathJoin("print_temp_history.html");
        using var f = FileAccess.Open(tmp, FileAccess.ModeFlags.Write);
        f?.StoreString(entry.FullProtocolHtml);
        OS.ShellOpen(ProjectSettings.GlobalizePath(tmp));
    }

    // ============================================================
    // АНИМАЦИИ
    // ============================================================

    private void AnimateHover(Control c, bool h, float s)
    {
        if (h && _sndHover != null) AudioManager.Instance?.PlayStream(_sndHover, -5f);
        c.PivotOffset = c.Size / 2;
        var t = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        t.TweenProperty(c, "scale", h ? new Vector2(s, s) : Vector2.One, 0.12f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    private void AnimateClick(Control c)
    {
        if (_sndClick != null) AudioManager.Instance?.PlayStream(_sndClick);
        c.PivotOffset = c.Size / 2;
        var t = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        t.TweenProperty(c, "scale", new Vector2(0.95f, 0.95f), 0.05f).SetTrans(Tween.TransitionType.Bounce);
        t.TweenProperty(c, "scale", Vector2.One, 0.12f);
    }
}
