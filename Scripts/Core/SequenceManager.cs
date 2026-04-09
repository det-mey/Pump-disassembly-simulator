using Godot;
using System;
using System.Collections.Generic;

public partial class SequenceManager : Node
{
    public static SequenceManager Instance { get; private set; }

    private Godot.Collections.Array _groups;
    private int _currentGroupIndex = 0;
    private int _currentStepInGroupIndex = 0;
    private Godot.Collections.Array _steps;
    private int _currentStepIndex = 0;
    private string _currentStepName = "";
    private Godot.Collections.Array _penalties;
    private double _timeUpdateAccumulator = 0;
    public int MaxPossibleScore { get; private set; } = 0;

    public override void _EnterTree() => Instance = this;

    public override void _Process(double delta)
    {
        // Не обновляем время, если игра на паузе или сценарий завершен
        if (GetTree().Paused || _groups == null || _currentGroupIndex >= _groups.Count) return;

        _timeUpdateAccumulator += delta;
        if (_timeUpdateAccumulator >= 1.0) // Обновляем раз в секунду для экономии ресурсов
        {
            _timeUpdateAccumulator = 0;
            RefreshCurrentTaskDisplay();
        }
    }

    private void RefreshCurrentTaskDisplay()
    {
        if (_groups == null || _currentGroupIndex >= _groups.Count) return;

        var currentGroup = _groups[_currentGroupIndex].AsGodotDictionary();
        var steps = currentGroup["steps"].AsGodotArray();
        if (_currentStepInGroupIndex >= steps.Count) return;

        var currentStep = steps[_currentStepInGroupIndex].AsGodotDictionary();
        UpdateUI(currentGroup, currentStep, steps.Count);
    }

    public void LoadSequenceFromJson(string jsonPath)
    {
        _groups = new Godot.Collections.Array();
        _currentGroupIndex = 0;
        _currentStepInGroupIndex = 0;
        MaxPossibleScore = 0; // Сброс

        if (string.IsNullOrEmpty(jsonPath) || !FileAccess.FileExists(jsonPath)) return;

        using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
        var json = new Json();
        if (json.Parse(file.GetAsText()) == Error.Ok)
        {
            var data = json.Data.AsGodotDictionary();
            _groups = data.ContainsKey("groups") ? data["groups"].AsGodotArray() : new Godot.Collections.Array();
            
            // ПОДСЧЕТ МАКСИМАЛЬНОГО БАЛЛА
            foreach (var gVar in _groups)
            {
                var g = gVar.AsGodotDictionary();
                if (g.ContainsKey("steps"))
                {
                    foreach (var sVar in g["steps"].AsGodotArray())
                    {
                        var s = sVar.AsGodotDictionary();
                        if (s.ContainsKey("points")) MaxPossibleScore += s["points"].AsInt32();
                    }
                }
            }

            ApplyScenarioSetup(data);
            EvaluateCurrentStep(); 
        }
    }

    // МЕТОД ВАЛИДАЦИИ
    public static System.Collections.Generic.List<string> ValidateSequenceJsonStrict(string jsonPath, System.Collections.Generic.HashSet<string> validIds)
    {
        var errors = new System.Collections.Generic.List<string>();
        if (string.IsNullOrEmpty(jsonPath) || !FileAccess.FileExists(jsonPath)) return errors;

        using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok)
        {
            errors.Add($"Синтаксическая ошибка в строке {json.GetErrorLine()}: {json.GetErrorMessage()}");
            return errors; 
        }

        var data = json.Data.AsGodotDictionary();
        
        // ПРОВЕРКА ГРУПП
        if (!data.ContainsKey("groups"))
        {
            errors.Add("Критическая ошибка: В корневом объекте JSON отсутствует массив 'groups'!");
            return errors;
        }

        // Локальная функция проверки ID
        void CheckTargets(Godot.Collections.Dictionary dict, string context)
        {
            if (dict.ContainsKey("target"))
            {
                string target = dict["target"].AsString();
                if (!validIds.Contains(target)) errors.Add($"Неизвестный ID детали '{target}' в {context}!");
            }
            if (dict.ContainsKey("sub"))
            {
                var sub = dict["sub"];
                if (sub.VariantType == Variant.Type.Dictionary) CheckTargets(sub.AsGodotDictionary(), context);
                else if (sub.VariantType == Variant.Type.Array)
                {
                    foreach (var s in sub.AsGodotArray()) CheckTargets(s.AsGodotDictionary(), context);
                }
            }
        }

        // 1. Проверка структуры групп и шагов
        var groups = data["groups"].AsGodotArray();
        if (groups.Count == 0) errors.Add("Массив 'groups' пуст. Сценарию нечего выполнять.");

        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i].AsGodotDictionary();
            string gName = group.ContainsKey("name") ? group["name"].AsString() : $"Группа {i}";
            
            if (!group.ContainsKey("steps"))
            {
                errors.Add($"В группе '{gName}' отсутствует массив 'steps'!");
                continue;
            }

            var steps = group["steps"].AsGodotArray();
            for (int j = 0; j < steps.Count; j++)
            {
                var step = steps[j].AsGodotDictionary();
                string sName = step.TryGetValue("name", out Variant n) ? n.AsString() : $"Шаг {j}";
                
                if (step.ContainsKey("condition"))
                    CheckTargets(step["condition"].AsGodotDictionary(), $"Группе '{gName}' -> Шаге '{sName}'");
                else
                    errors.Add($"В Шаге '{sName}' (Группа: {gName}) отсутствует условие 'condition'!");
            }
        }

        // 2. Проверка штрафов
        if (data.ContainsKey("penalties"))
        {
            var penalties = data["penalties"].AsGodotArray();
            foreach (var penVar in penalties)
            {
                var pen = penVar.AsGodotDictionary();
                if (pen.ContainsKey("on_action"))
                {
                    string actionStr = pen["on_action"].AsString();
                    int lastUnderscore = actionStr.LastIndexOf('_');
                    if (lastUnderscore != -1)
                    {
                        string partId = actionStr.Substring(0, lastUnderscore);
                        if (!validIds.Contains(partId)) errors.Add($"Неизвестный ID детали '{partId}' в штрафе on_action: '{actionStr}'!");
                    }
                    else errors.Add($"Неверный формат on_action: '{actionStr}'. Ожидается 'ID_Действие'.");
                }
                if (pen.ContainsKey("condition")) CheckTargets(pen["condition"].AsGodotDictionary(), "блоке Штрафов");
            }
        }

        // 3. Проверка стартового setup (random_states, dependencies)
        if (data.ContainsKey("setup"))
        {
            var setup = data["setup"].AsGodotDictionary();
            if (setup.ContainsKey("random_states"))
            {
                foreach(var rsVar in setup["random_states"].AsGodotArray())
                {
                    var rs = rsVar.AsGodotDictionary();
                    if (rs.ContainsKey("target") && !validIds.Contains(rs["target"].AsString()))
                        errors.Add($"Неизвестный ID '{rs["target"].AsString()}' в setup -> random_states!");
                }
            }
            if (setup.ContainsKey("dependencies"))
            {
                foreach(var depVar in setup["dependencies"].AsGodotArray())
                {
                    var d = depVar.AsGodotDictionary();
                    if (!d.ContainsKey("target")) continue;
                    string t = d["target"].AsString();
                    if (!validIds.Contains(t)) errors.Add($"Неизвестный ID '{t}' в dependencies!");
                    
                    if (d.ContainsKey("requires")) foreach(var req in d["requires"].AsGodotArray())
                        if (!validIds.Contains(req.AsString())) errors.Add($"Неизвестный ID '{req.AsString()}' в requires для '{t}'!");
                    
                    if (d.ContainsKey("blocks")) foreach(var blk in d["blocks"].AsGodotArray())
                        if (!validIds.Contains(blk.AsString())) errors.Add($"Неизвестный ID '{blk.AsString()}' в blocks для '{t}'!");
                }
            }
        }

        return errors;
    }

    public void EvaluateCurrentStep()
    {
        if (_groups == null || _currentGroupIndex >= _groups.Count) return;

        var currentGroup = _groups[_currentGroupIndex].AsGodotDictionary();
        var steps = currentGroup["steps"].AsGodotArray();
        bool isStrict = currentGroup.TryGetValue("strict", out Variant s) && s.AsBool();

        if (_currentStepInGroupIndex >= steps.Count)
        {
            CompleteGroup(currentGroup);
            return;
        }

        var currentStep = steps[_currentStepInGroupIndex].AsGodotDictionary();
        UpdateUI(currentGroup, currentStep, steps.Count);

        // Проверка условия
        if (EvaluateConditionRecursive(currentStep["condition"].AsGodotDictionary()))
        {
            ProcessStepSuccess(currentGroup, currentStep);
            return; 
        }
        
        if (isStrict)
        {
            CheckStrictOrderViolation(steps);
        }
    }

    private void CheckStrictOrderViolation(Godot.Collections.Array steps)
    {
        for (int i = _currentStepInGroupIndex + 1; i < steps.Count; i++)
        {
            var step = steps[i].AsGodotDictionary();
            if (EvaluateConditionRecursive(step["condition"].AsGodotDictionary()))
            {
                // Игрок выполнил шаг 'i', хотя мы ждали шаг '_currentStepInGroupIndex'
                var groupName = _groups[_currentGroupIndex].AsGodotDictionary()["name"].AsString();
                var penaltyPoints = _groups[_currentGroupIndex].AsGodotDictionary().TryGetValue("penalty_points", out Variant p) ? p.AsInt32() : 10;
                var isAccident = _groups[_currentGroupIndex].AsGodotDictionary().TryGetValue("is_accident", out Variant a) && a.AsBool();
                var notify = _groups[_currentGroupIndex].AsGodotDictionary().TryGetValue("notify", out Variant n) && n.AsBool();

                ActionLogger.Instance.AddPenalty($"Нарушена строгая последовательность в группе '{groupName}'", penaltyPoints, isAccident, notify);
                
                // Чтобы не стопорить игру, "подтягиваем" индекс (игрок как бы перепрыгнул шаг)
                _currentStepInGroupIndex = i;
            }
        }
    }

    private void ProcessStepSuccess(Godot.Collections.Dictionary group, Godot.Collections.Dictionary step)
    {
        string stepName = step["name"].AsString();
        int points = step.TryGetValue("points", out Variant p) ? p.AsInt32() : 0;
        
        ActionLogger.Instance.LogEvent(LogType.StepComplete, $"Выполнен шаг: {stepName}", points: points);
        
        _currentStepInGroupIndex++;
        EvaluateCurrentStep(); // Проверяем следующий
    }

    private void CompleteGroup(Godot.Collections.Dictionary group)
    {
        string groupName = group.TryGetValue("name", out Variant n) ? n.AsString() : "Группа";
        ActionLogger.Instance.LogEvent(LogType.GroupComplete, $"ЗАВЕРШЕНА ГРУППА: {groupName}");
        
        _currentGroupIndex++;
        _currentStepInGroupIndex = 0;
        
        if (_currentGroupIndex < _groups.Count)
        {
            EvaluateCurrentStep();
        }
        else
        {
            // --- ВСЁ ВЫПОЛНЕНО ---
            ActionLogger.Instance.LogEvent(LogType.System, "СЦЕНАРИЙ ПОЛНОСТЬЮ ЗАВЕРШЕН");
            
            // Задержка 2 секунды перед показом результатов
            GetTree().CreateTimer(2.0f).Timeout += () => {
                ResultsUI.Instance?.ShowResults();
            };
        }
    }

    private void UpdateUI(Godot.Collections.Dictionary group, Godot.Collections.Dictionary step, int totalStepsInGroup)
    {
        string gName = group["name"].AsString();
        string sName = step["name"].AsString();
        string sDesc = step.TryGetValue("description", out Variant d) ? d.AsString() : "";
        
        // Считаем оставшееся время
        float elapsed = (Time.GetTicksMsec() - ActionLogger.Instance.SessionStartTime) / 1000f;
        float remaining = GameManager.Instance.CurrentScenario.TimeLimitSeconds - elapsed;

        if (remaining <= 0 && GameManager.Instance.CurrentMode == SimulatorMode.Exam)
        {
            ActionLogger.Instance.AddPenalty("Время на выполнение задачи истекло!", 100, true, true);
            ResultsUI.Instance?.ShowResults(); // Принудительный конец
        }

        string timeStr = remaining > 0 ? $" | Время: {(int)remaining / 60}:{(int)remaining % 60:D2}" : " | ВРЕМЯ ИСТЕКЛО";
        UIManager.Instance?.UpdateTaskUI(_currentStepInGroupIndex, totalStepsInGroup, $"{gName}: {sName}{timeStr}", sDesc);
    }

    // РЕКУРСИВНЫЙ АНАЛИЗАТОР ЛОГИКИ (AND, OR, NOT, STATE)
    private bool EvaluateConditionRecursive(Godot.Collections.Dictionary cond)
    {
        string op = cond.ContainsKey("op") ? cond["op"].AsString().ToUpper() : "STATE";

        if (op == "STATE")
        {
            string targetId = cond["target"].AsString();
            string expectedState = cond["state"].AsString();
            return ActionLogger.Instance.GetState(targetId) == expectedState;
        }
        else if (op == "AND")
        {
            var subConditions = cond["sub"].AsGodotArray();
            foreach (var sub in subConditions)
                if (!EvaluateConditionRecursive(sub.AsGodotDictionary())) return false;
            return true;
        }
        else if (op == "OR")
        {
            var subConditions = cond["sub"].AsGodotArray();
            foreach (var sub in subConditions)
                if (EvaluateConditionRecursive(sub.AsGodotDictionary())) return true;
            return false;
        }
        else if (op == "NOT")
        {
            var sub = cond["sub"].AsGodotDictionary();
            return !EvaluateConditionRecursive(sub);
        }
        return false;
    }

    public void BroadcastCurrentStepUI()
    {
        if (_steps == null || _steps.Count == 0) 
        {
            // --- ИСПРАВЛЕНИЕ: Передаем 0, 0 для скрытия панели ---
            UIManager.Instance?.UpdateTaskUI(0, 0, "Свободный режим", "Сценарий не задан");
            return;
        }
        
        if (_currentStepIndex < _steps.Count)
        {
            var currentStep = _steps[_currentStepIndex].AsGodotDictionary();
            string stepName = currentStep.TryGetValue("name", out Variant nameVar) ? nameVar.AsString() : "Шаг " + (_currentStepIndex + 1);
            string stepDesc = currentStep.TryGetValue("description", out Variant descVar) ? descVar.AsString() : "";
            
            UIManager.Instance?.UpdateTaskUI(_currentStepIndex, _steps.Count, stepName, stepDesc);
        }
        else
        {
            UIManager.Instance?.UpdateTaskUI(_steps.Count, _steps.Count, "ЗАДАЧА ВЫПОЛНЕНА!", "Все шаги завершены.");
        }
    }

    // --- НОВОЕ: МЕТОД ИНИЦИАЛИЗАЦИИ СЦЕНАРИЯ ---
    private void ApplyScenarioSetup(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("setup")) return;
        var setup = data["setup"].AsGodotDictionary();
        
        var allParts = GetTree().GetNodesInGroup("AllParts");
        var partsDict = new System.Collections.Generic.Dictionary<string, BasePart>();
        foreach(BasePart part in allParts)
            if (!string.IsNullOrEmpty(part.PartId)) partsDict[part.PartId] = part;

        // 1. УЛУЧШЕННЫЕ СЛУЧАЙНЫЕ СОСТОЯНИЯ
        if (setup.ContainsKey("random_states"))
        {
            foreach(var rsVar in setup["random_states"].AsGodotArray())
            {
                var rs = rsVar.AsGodotDictionary();
                string target = rs["target"].AsString();
                
                if (partsDict.TryGetValue(target, out BasePart bp))
                {
                    int chance = rs.ContainsKey("chance") ? rs["chance"].AsInt32() : 100;
                    if (GD.RandRange(1, 100) <= chance)
                    {
                        string stateStr = rs["state"].AsString();
                        if (Enum.TryParse(stateStr, out PartState pState)) bp.AddState(pState);

                        // --- НОВОЕ: Случайный износ для дефектовки ---
                        if (rs.ContainsKey("wear_range") && bp is Fastener f)
                        {
                            var range = rs["wear_range"].AsVector2(); // Задать в JSON как [min, max]
                            f.CurrentWear = (float)GD.RandRange(range.X, range.Y);
                            f.IsDefective = Mathf.Abs(f.CurrentWear - f.NominalWear) > 0.5f;
                        }
                        
                        if (bp is Fastener fast) fast.UpdateVisuals();
                    }
                }
            }
        }

        // 2. Инъекция физических зависимостей из JSON прямо в детали
        if (setup.ContainsKey("dependencies"))
        {
            foreach(var depVar in setup["dependencies"].AsGodotArray())
            {
                var dep = depVar.AsGodotDictionary();
                string target = dep["target"].AsString();
                
                if (partsDict.TryGetValue(target, out BasePart bp) && bp is Fastener targetFastener)
                {
                    // То, без чего нельзя установить
                    if (dep.ContainsKey("requires"))
                    {
                        foreach(var reqVar in dep["requires"].AsGodotArray())
                        {
                            if (partsDict.TryGetValue(reqVar.AsString(), out BasePart reqPart) && reqPart is Fastener reqFastener)
                                targetFastener.RequiredParts.Add(reqFastener);
                        }
                    }
                    // То, что мешает снять
                    if (dep.ContainsKey("blocks"))
                    {
                        foreach(var blkVar in dep["blocks"].AsGodotArray())
                        {
                            if (partsDict.TryGetValue(blkVar.AsString(), out BasePart blkPart) && blkPart is Fastener blkFastener)
                                targetFastener.BlockingParts.Add(blkFastener);
                        }
                    }
                }
            }
        }

        // 3. Включение минимальных подсказок для экзамена/тренировки из JSON
        if (setup.ContainsKey("exam_prompts"))
        {
            foreach (var idVar in setup["exam_prompts"].AsGodotArray())
            {
                string targetId = idVar.AsString();
                if (partsDict.TryGetValue(targetId, out BasePart bp))
                {
                    bp.AllowMinimalPromptInExam = true;
                }
            }
        }
    }
}