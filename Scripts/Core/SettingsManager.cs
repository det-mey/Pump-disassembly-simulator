using Godot;

public partial class SettingsManager : Node
{
    public static SettingsManager Instance { get; private set; }

    public int WindowModeIndex { get; set; } = 0; // 0: Оконный, 1: Безрамочный, 2: Полноэкранный
    public float UiScale { get; set; } = 1.0f;
    public Vector2I Resolution { get; set; }
    public bool VSync { get; set; } = true;
    public int Msaa3D { get; set; } = 2;
    public float MasterVolume { get; set; } = 0.8f;
    public float MouseSensitivity { get; set; } = 0.002f;
    public bool ShowFPS { get; set; } = false;
    public int CurrentScreen { get; set; } = 0;

    private const string SETTINGS_PATH = "user://Config/game_settings.json";

    public override void _EnterTree()
    {
        Instance = this;
        // По умолчанию берем текущий экран
        CurrentScreen = DisplayServer.WindowGetCurrentScreen();
        Resolution = DisplayServer.ScreenGetSize(CurrentScreen);
        LoadSettings();
    }

    public void SaveSettings()
    {
        var dict = new Godot.Collections.Dictionary
        {
            { "window_mode", WindowModeIndex },
            { "ui_scale", UiScale },
            { "res_x", Resolution.X },
            { "res_y", Resolution.Y },
            { "vsync", VSync },
            { "msaa", Msaa3D },
            { "volume", MasterVolume },
            { "mouse_sens", MouseSensitivity },
            { "show_fps", ShowFPS },
            { "screen", CurrentScreen }
        };
        
        DirAccess.MakeDirRecursiveAbsolute("user://Config");
        using var file = FileAccess.Open(SETTINGS_PATH, FileAccess.ModeFlags.Write);
        file.StoreString(Json.Stringify(dict));
        ApplySettings();
    }

    public void LoadSettings()
    {
        if (FileAccess.FileExists(SETTINGS_PATH))
        {
            using var file = FileAccess.Open(SETTINGS_PATH, FileAccess.ModeFlags.Read);
            var json = new Json();
            if (json.Parse(file.GetAsText()) == Error.Ok)
            {
                var data = json.Data.AsGodotDictionary();
                WindowModeIndex = data.ContainsKey("window_mode") ? data["window_mode"].AsInt32() : 0;
                UiScale = data.ContainsKey("ui_scale") ? (float)data["ui_scale"].AsDouble() : 1.0f;
                if (data.ContainsKey("res_x")) Resolution = new Vector2I(data["res_x"].AsInt32(), data["res_y"].AsInt32());
                VSync = data.ContainsKey("vsync") ? data["vsync"].AsBool() : true;
                Msaa3D = data.ContainsKey("msaa") ? data["msaa"].AsInt32() : 2;
                MasterVolume = data.ContainsKey("volume") ? (float)data["volume"].AsDouble() : 1.0f;
                MouseSensitivity = data.ContainsKey("mouse_sens") ? (float)data["mouse_sens"].AsDouble() : 0.002f;
                ShowFPS = data.ContainsKey("show_fps") ? data["show_fps"].AsBool() : false;
                CurrentScreen = data.ContainsKey("screen") ? data["screen"].AsInt32() : DisplayServer.WindowGetCurrentScreen();
            }
        }
        ApplySettings();
    }

    public void ApplySettings()
    {
        // 1. ПРИВЯЗКА К МОНИТОРУ
        if (CurrentScreen < 0 || CurrentScreen >= DisplayServer.GetScreenCount()) CurrentScreen = 0;
        DisplayServer.WindowSetCurrentScreen(CurrentScreen);

        // 2. РЕЖИМ ОКНА
        switch (WindowModeIndex)
        {
            case 0: // Оконный
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                DisplayServer.WindowSetSize(Resolution);
                // Центрируем на выбранном экране
                Vector2I screenSize = DisplayServer.ScreenGetSize(CurrentScreen);
                Vector2I screenPos = DisplayServer.ScreenGetPosition(CurrentScreen);
                DisplayServer.WindowSetPosition(screenPos + (screenSize / 2 - Resolution / 2));
                break;
            case 1: // Безрамочный
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
                DisplayServer.WindowSetSize(DisplayServer.ScreenGetSize(CurrentScreen));
                DisplayServer.WindowSetPosition(DisplayServer.ScreenGetPosition(CurrentScreen));
                break;
            case 2: // Полноэкранный
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;
        }

        DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        GetTree().Root.Msaa3D = (Viewport.Msaa)Msaa3D;
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), Mathf.LinearToDb(MasterVolume));
        GetTree().Root.ContentScaleFactor = UiScale;
    }
}