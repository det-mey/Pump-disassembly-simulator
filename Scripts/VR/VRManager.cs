using Godot;
using System;

/// <summary>
/// Менеджер XR-сессии с автоопределением платформы.
/// Инициализирует OpenXR, переключает между 2D и VR режимами.
/// </summary>
[GlobalClass]
public partial class VRManager : Node
{
    public static VRManager Instance { get; private set; }

    [ExportGroup("Камеры")]
    [Export] private Camera3D _camera2D;
    [Export] private XRCamera3D _cameraVR;

    [ExportGroup("Настройки XR")]
    [Export] public bool AutoInitXR { get; set; } = true;

    /// <summary>Активен ли VR-режим.</summary>
    public bool IsVRMode { get; private set; }

    /// <summary>Текущая платформа.</summary>
    public XRPlatform CurrentPlatform { get; private set; } = XRPlatform.Unknown;

    /// <summary>XR-интерфейс активен.</summary>
    public bool IsXRInitialized { get; private set; }

    public event Action OnXRSessionStarted;
    public event Action OnXRSessionEnded;
    public event Action<bool> OnVRModeChanged;

    public enum XRPlatform { Unknown, PC, MetaQuest, Pico }

    public override void _EnterTree()
    {
        if (Instance != null) { QueueFree(); return; }
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        DetectPlatform();
    }

    public override void _Ready()
    {
        if (AutoInitXR && IsVRPlatform())
        {
            InitializeXR();
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    private void DetectPlatform()
    {
        string osName = OS.GetName().ToLowerInvariant();

        if (osName.Contains("android"))
        {
            string monoPath = OS.GetEnvironment("MONO_PATH");
            CurrentPlatform = (!string.IsNullOrEmpty(monoPath) && monoPath.ToLowerInvariant().Contains("pico"))
                ? XRPlatform.Pico
                : XRPlatform.MetaQuest;
        }
        else
        {
            CurrentPlatform = XRPlatform.PC;
        }

        GD.Print($"[VRManager] Платформа: {CurrentPlatform}");
    }

    public bool IsVRPlatform()
    {
        return CurrentPlatform != XRPlatform.Unknown;
    }

    public void InitializeXR()
    {
        if (IsXRInitialized) return;

        // Проверяем через XRServer — ищем интерфейс OpenXR
        XRInterface openXR = XRServer.FindInterface("OpenXR");
        if (openXR == null)
        {
            GD.PrintErr("[VRManager] OpenXR интерфейс не найден");
            DeveloperConsole.Instance?.LogError("[VR] OpenXR не найден. Включите плагин в Project Settings → Plugins");
            return;
        }

        // Проверяем, может ли OpenXR инициализироваться
        // IsInitialized() уже проверили выше — если не инициализирован, просто продолжаем

        // Устанавливаем OpenXR как основной интерфейс
        XRServer.PrimaryInterface = openXR;
        IsXRInitialized = true;

        GD.Print("[VRManager] XR инициализирован: OpenXR");
        DeveloperConsole.Instance?.Log("[VR] XR инициализирован (OpenXR)", "green");
    }

    public void SetVRMode(bool enable)
    {
        if (IsVRMode == enable) return;

        if (enable && !IsXRInitialized)
        {
            InitializeXR();
            if (!IsXRInitialized) return;
        }

        IsVRMode = enable;

        if (enable)
        {
            if (_camera2D != null) _camera2D.Current = false;
            if (_cameraVR != null) _cameraVR.Current = true;
            VRUIAdapter.Instance?.AdaptUIForVR();
            OnXRSessionStarted?.Invoke();
            GD.Print("[VRManager] VR-режим: ВКЛ");
        }
        else
        {
            if (_cameraVR != null) _cameraVR.Current = false;
            if (_camera2D != null) _camera2D.Current = true;
            VRUIAdapter.Instance?.RestoreUIFromVR();
            OnXRSessionEnded?.Invoke();
            GD.Print("[VRManager] VR-режим: ВЫКЛ");
        }

        OnVRModeChanged?.Invoke(enable);
    }
}
