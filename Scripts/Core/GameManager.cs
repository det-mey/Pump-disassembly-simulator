using Godot;
using System;

// Единый enum режимов симулятора (объединяет SimulatorMode и SimulatorState)
public enum SimulatorMode { Learning, Training, Exam }

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    [Export] public bool ShowGhosts { get; set; } = true;
    public StandardMaterial3D GhostMaterial { get; private set; }

    public SimulatorMode CurrentMode { get; private set; } = SimulatorMode.Learning;
    public ScenarioData CurrentScenario { get; private set; }

    public override void _EnterTree()
    {
        if (Instance != null) { QueueFree(); return; }
        Instance = this;
        InitializeGhostMaterial();
        
        // ВАЖНО: GameManager работает даже во время паузы (для кнопок Рестарт/Выход)
        ProcessMode = ProcessModeEnum.Always; 
    }

    public void ToggleGhosts()
    {
        ShowGhosts = !ShowGhosts;
        GetTree().CallGroup("Fasteners", "UpdateVisuals");
    }

    private void InitializeGhostMaterial()
    {
        GhostMaterial = new StandardMaterial3D();
        GhostMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        GhostMaterial.AlbedoColor = new Color(0, 1, 0, 0.3f);
        GhostMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        GhostMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
    }

    // Универсальный метод загрузки любой карты с любым режимом
    public async void LoadScenario(ScenarioData scenario, SimulatorMode mode)
    {
        if (scenario == null) return;

        // 1. Сначала СБРАСЫВАЕМ ИНТЕРФЕЙС
        UIManager.Instance?.ResetUIForNewLevel();

        CurrentScenario = scenario;
        CurrentMode = mode;
        ShowGhosts = (mode == SimulatorMode.Learning);

        if (InventoryManager.Instance != null) InventoryManager.Instance.ClearInventory();
        if (ActionLogger.Instance != null) ActionLogger.Instance.StartNewSession();

        if (!string.IsNullOrEmpty(scenario.ScenePath))
        {
            GetTree().Paused = false; 
            
            Godot.Error err = GetTree().ChangeSceneToFile(scenario.ScenePath);
            if (err != Godot.Error.Ok)
            {
                DeveloperConsole.Instance?.ToggleConsole(true);
                DeveloperConsole.Instance?.LogError($"Ошибка загрузки сцены: {err}");
                return;
            }
            
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            
            SequenceManager.Instance?.LoadSequenceFromJson(scenario.SequenceJsonPath);
            
            // ВАЖНО: После загрузки сцены еще раз проверяем фокус мыши
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public void RestartScenario()
    {
        if (CurrentScenario != null) 
        {
            if (!ValidateScenarioConfig(CurrentScenario)) return; 

            // Сначала размораживаем игру, чтобы новая сцена могла начать процесс _Ready
            GetTree().Paused = false; 
            DeveloperConsole.Instance?.ToggleConsole(false);
            
            LoadScenario(CurrentScenario, CurrentMode);
        }
    }

    public void LoadMainMenu()
    {
        GetTree().Paused = false; 
        DeveloperConsole.Instance?.ToggleConsole(false); 

        string menuPath = "res://Scenes/UI/MainMenu.tscn"; 

        Godot.Error err = GetTree().ChangeSceneToFile(menuPath);
        // Если движок не смог загрузить файл
        if (err != Godot.Error.Ok)
        {
            DeveloperConsole.Instance?.ToggleConsole(true); // Принудительно открываем консоль
            DeveloperConsole.Instance?.LogError($"[ДВИЖОК] Не удалось загрузить Главное Меню!");
            DeveloperConsole.Instance?.LogError($"Код ошибки: {err}. Проверьте путь: {menuPath}");
        }
    }

    // Рекурсивно собирает все PartId из загруженной в память сцены
    private void FindAllPartIds(Node node, System.Collections.Generic.HashSet<string> ids)
    {
        if (node is BasePart bp && !string.IsNullOrEmpty(bp.PartId)) 
        {
            ids.Add(bp.PartId);
        }
        foreach (Node child in node.GetChildren()) 
        {
            FindAllPartIds(child, ids);
        }
    }

    public bool ValidateScenarioConfig(ScenarioData scenario)
    {
        if (scenario == null) return false;

        if (!string.IsNullOrEmpty(scenario.SequenceJsonPath) && FileAccess.FileExists(scenario.SequenceJsonPath))
        {
            var validIds = new System.Collections.Generic.HashSet<string>();
            
            // "Теневая" загрузка сцены для чтения деталей
            if (!string.IsNullOrEmpty(scenario.ScenePath))
            {
                var packedScene = GD.Load<PackedScene>(scenario.ScenePath);
                if (packedScene != null)
                {
                    Node tempRoot = packedScene.Instantiate();
                    FindAllPartIds(tempRoot, validIds);
                    tempRoot.QueueFree(); // Мгновенно удаляем из памяти
                }
            }

            // Прогоняем валидатор
            var errors = SequenceManager.ValidateSequenceJsonStrict(scenario.SequenceJsonPath, validIds);
            
            if (errors.Count > 0)
            {
                DeveloperConsole.Instance?.ToggleConsole(true); // Принудительно открываем консоль
                DeveloperConsole.Instance?.LogError($"ОШИБКА ЗАПУСКА КАРТЫ: '{scenario.ScenarioName}'");
                DeveloperConsole.Instance?.LogError("В JSON-сценарии найдены критические ошибки:");
                foreach (var err in errors) DeveloperConsole.Instance?.LogError(err);
                
                return false; // Валидация провалена
            }
        }
        
        return true; // Сценарий чист
    }
}
