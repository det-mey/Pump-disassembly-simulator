using Godot;
using System;
using System.Collections.Generic;

public enum LogType { Action, Warning, Error, System, StepComplete, GroupComplete }

public class ActionEntry
{
    public float Timestamp;
    public LogType Type;
    public string Message;
    public string ToolUsed;
    public string TargetPart;
    public int PointsChange;
    public string TechnicalDetails;

    public override string ToString()
    {
        string timeStr = TimeSpan.FromMilliseconds(Timestamp).ToString(@"mm\:ss\.fff");
        string pointsStr = PointsChange != 0 ? $" (Очки: {(PointsChange > 0 ? "+" : "")}{PointsChange})" : "";
        return $"[{timeStr}] [{Type}] {Message}{pointsStr}";
    }
}

public partial class ActionLogger : Node
{
    public static ActionLogger Instance { get; private set; }

    public List<ActionEntry> FullSessionHistory { get; private set; } = new List<ActionEntry>();
    public Dictionary<string, string> PartStates { get; private set; } = new Dictionary<string, string>();
    
    public int Score { get; private set; } = 0;
    public int PenaltyPoints { get; private set; } = 0;
    public float SessionStartTime { get; private set; }
    public int TotalAttempts { get; private set; } = 0; // Каждое взаимодействие (даже ошибочное)
    public int SuccessfulActions { get; private set; } = 0;
    
    private FileAccess _logFile;
    private string _currentLogPath;

    public override void _EnterTree()
    {
        Instance = this;
        StartNewSession();
    }

    public void StartNewSession()
    {
        FullSessionHistory.Clear();
        PartStates.Clear();
        Score = 0;
        PenaltyPoints = 0;
        TotalAttempts = 0;
        SuccessfulActions = 0;
        SessionStartTime = Time.GetTicksMsec(); // Засекаем время старта

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _currentLogPath = $"user://logs/session_{timestamp}.txt";
        
        try
        {
            DirAccess.MakeDirRecursiveAbsolute("user://logs");
            _logFile = FileAccess.Open(_currentLogPath, FileAccess.ModeFlags.Write);
            LogEvent(LogType.System, $"Запущено прохождение в режиме: {GameManager.Instance.CurrentMode}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[ActionLogger] Ошибка создания файла лога: {ex.Message}");
            _logFile = null;
        }
    }

    // Основной метод для записи ЛЮБОГО действия
    public void LogEvent(LogType type, string message, string tool = "Руки", string target = "Нет", int points = 0, string tech = "")
    {
        if (type == LogType.Action || type == LogType.Warning || type == LogType.Error)
            TotalAttempts++;

        if (type == LogType.StepComplete || points > 0)
            SuccessfulActions++;

        var entry = new ActionEntry {
            Timestamp = (float)Time.GetTicksMsec() - SessionStartTime,
            Type = type, Message = message, ToolUsed = tool, TargetPart = target, PointsChange = points, TechnicalDetails = tech
        };

        FullSessionHistory.Add(entry);
        if (points > 0) Score += points;
        else if (points < 0) PenaltyPoints += Math.Abs(points);

        _logFile?.StoreLine(entry.ToString());
        _logFile?.Flush();
    }

    // Специальный метод для изменения состояния деталей (вызывается из Fastener)
    public void UpdatePartState(string partId, string state, string partName)
    {
        if (string.IsNullOrEmpty(partId)) return;
        PartStates[partId] = state;
        
        // Автоматически уведомляем менеджер последовательностей
        SequenceManager.Instance?.EvaluateCurrentStep();
    }

    public string GetState(string partId)
    {
        return PartStates.ContainsKey(partId) ? PartStates[partId] : "Initial";
    }

    public void AddPenalty(string reason, int points, bool isAccident, bool notifyPlayer)
    {
        LogEvent(LogType.Error, $"ШТРАФ: {reason}", points: -points, tech: isAccident ? "АВАРИЯ" : "НАРУШЕНИЕ");
        
        if (notifyPlayer)
        {
            UIManager.Instance?.UpdatePrompt($"[{(isAccident ? "АВАРИЯ" : "ШТРАФ")}] {reason}", true);
        }
    }

    // Генерация финального отчета для экрана итогов
    public string GenerateFullReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("[center][font_size=24][b]ПРОТОКОЛ ВЫПОЛНЕНИЯ РАБОТ[/b][/font_size][/center]\n");
        sb.Append($"[center][color=gray]Режим: {GetRussianMode(GameManager.Instance.CurrentMode)} | Дата: {DateTime.Now:dd.MM.yyyy HH:mm}[/color][/center]\n");
        sb.Append("[color=gray]________________________________________________________________________________________________[/color]\n\n");

        foreach (var entry in FullSessionHistory)
        {
            string timeStr = TimeSpan.FromMilliseconds(entry.Timestamp).ToString(@"mm\:ss");
            string color = GetColorForType(entry.Type);
            
            sb.Append($"[color=gray][{timeStr}][/color] ");
            sb.Append($"[color={color}][b]{GetIconForType(entry.Type)} {GetRussianType(entry.Type)}:[/b] {entry.Message}[/color]");
            
            if (entry.PointsChange != 0)
            {
                string pColor = entry.PointsChange > 0 ? "#55ff55" : "#ff5555";
                sb.Append($" [color={pColor}][{(entry.PointsChange > 0 ? "+" : "")}{entry.PointsChange}][/color]");
            }
            sb.Append("\n\n");
        }

        sb.Append("[color=gray]________________________________________________________________________________________________[/color]\n\n");
        sb.Append($"[b]ИТОГОВЫЙ СЧЕТ:[/b] [color=#55ff55]{Score}[/color] из {SequenceManager.Instance.MaxPossibleScore} баллов\n");
        sb.Append($"[b]ШТРАФНЫЕ ОЧКИ:[/b] [color=#ff5555]{PenaltyPoints}[/color]\n");

        int errors = 0;
        foreach(var e in FullSessionHistory) if(e.Type == LogType.Error || e.Type == LogType.Warning) errors++;
        sb.Append($"[b]ОШИБОК ДОПУЩЕНО:[/b] {errors}\n\n");
        
        string grade = CalculateGrade();
        string gradeColor = grade.StartsWith("5") ? "#55ff55" : (grade.StartsWith("4") ? "#ffff55" : "#ff5555");
        
        sb.Append($"[center][font_size=32][b]ИТОГОВАЯ ОЦЕНКА: [color={gradeColor}]{grade}[/color][/b][/font_size][/center]");

        return sb.ToString();
    }

    private string GetColorForType(LogType type) => type switch {
        LogType.Error => "#ff5555",          // Красный
        LogType.Warning => "#ffcc00",        // Желтый
        LogType.StepComplete => "#55ff55",   // Зеленый
        LogType.GroupComplete => "#00ffff",  // Циан
        LogType.Action => "#ffffff",         // Белый
        LogType.System => "#aaaaaa",         // Серый
        _ => "white"
    };

    private string GetIconForType(LogType type) => type switch {
        LogType.Error => "✖",
        LogType.Warning => "⚠",
        LogType.StepComplete => "✔",
        LogType.GroupComplete => "★",
        LogType.Action => "•",
        LogType.System => "⚙",
        _ => ""
    };

    public string CalculateGrade()
    {
        int max = SequenceManager.Instance.MaxPossibleScore;
        if (max <= 0) return "БЕЗ ОЦЕНКИ (Свободный режим)";

        // 1. ПРОВЕРКА НА КРИТИЧЕСКИЕ АВАРИИ
        // Если были аварии (Penalty >= 50), это всегда провал
        if (PenaltyPoints >= 50) return "1 (НЕУДОВЛЕТВОРИТЕЛЬНО)";

        // 2. РАСЧЕТ ПРОЦЕНТА ВЫПОЛНЕНИЯ (Чистый прогресс)
        // Мы вычитаем штрафы из счета, но ограничиваем снизу нулем
        float netScore = Mathf.Max(0, Score - PenaltyPoints);
        float percentage = netScore / max;
        
        // 3. НОВЫЙ РАСЧЕТ ТОЧНОСТИ (Accuracy)
        // Считаем количество только ПЛОХИХ действий (ошибки и предупреждения)
        int badActionsCount = 0;
        foreach (var entry in FullSessionHistory)
        {
            if (entry.Type == LogType.Error || entry.Type == LogType.Warning)
            {
                badActionsCount++;
            }
        }

        // Точность = 1.0 (идеал) минус штраф за каждую ошибку
        // Одна ошибка снижает точность на 5%. Если ошибок нет - точность 100%
        float accuracy = Mathf.Max(0, 1.0f - (badActionsCount * 0.05f));

        // 4. ИТОГОВАЯ ЛОГИКА (Пятибалльная система)
        if (percentage >= 0.95f && accuracy >= 0.9f) return "5 (ОТЛИЧНО)";
        if (percentage >= 0.8f && accuracy >= 0.7f)  return "4 (ХОРОШО)";
        if (percentage >= 0.5f)                     return "3 (УДОВЛЕТВОРИТЕЛЬНО)";
        
        return "2 (НЕЗАЧЕТ)";
    }

    public override void _ExitTree()
    {
        _logFile?.Close();
    }

    public void LogSystemEvent(string eventType, string message)
    {
        LogEvent(LogType.System, $"{eventType}: {message}");
    }

    public void LogAction(string partId, string state, string details = "")
    {
        // Вызываем новый системный метод обновления состояния
        UpdatePartState(partId, state, partId);
        
        // Записываем это как действие в историю
        LogEvent(LogType.Action, $"Действие с деталью: {state}", target: partId, tech: details);
    }

    private string GetRussianType(LogType type) => type switch {
        LogType.Action => "ДЕЙСТВИЕ",
        LogType.Warning => "ПРЕДУПРЕЖДЕНИЕ",
        LogType.Error => "ОШИБКА",
        LogType.System => "СИСТЕМА",
        LogType.StepComplete => "ЭТАП ВЫПОЛНЕН",
        LogType.GroupComplete => "ГРУППА ЗАВЕРШЕНА",
        _ => "ИНФО"
    };

    private string GetRussianMode(SimulatorMode mode) => mode switch {
        SimulatorMode.Learning => "Обучение",
        SimulatorMode.Training => "Тренировка",
        SimulatorMode.Exam => "Экзамен",
        _ => "Неизвестно"
    };

    public string GenerateHtmlReport()
    {
        var sb = new System.Text.StringBuilder();
        try
        {
            sb.Append("<!DOCTYPE html><html><head><meta charset='UTF-8'><style>");
            sb.Append("body { font-family: sans-serif; padding: 20px; background: #fff; color: #000; }");
            sb.Append(".paper { max-width: 800px; margin: auto; border: 1px solid #ccc; padding: 40px; }");
            sb.Append("h1 { text-align: center; text-transform: uppercase; border-bottom: 2px solid #000; }");
            sb.Append(".entry { margin: 10px 0; border-bottom: 1px solid #eee; font-size: 14px; }");
            sb.Append(".time { color: #666; font-family: monospace; padding-right: 10px; }");
            sb.Append(".Error { color: #d00; font-weight: bold; } .StepComplete { color: #080; font-weight: bold; }");
            sb.Append("@media print { .no-print { display: none; } body { padding: 0; } .paper { border: none; } }");
            sb.Append("</style></head><body>");
            sb.Append("<div class='paper'>");
            sb.Append("<h1>Протокол выполнения работ</h1>");
            sb.Append($"<p><b>Сценарий:</b> {GameManager.Instance.CurrentScenario?.ScenarioName ?? "Неизвестно"}<br>");
            sb.Append($"<b>Режим:</b> {GetRussianMode(GameManager.Instance.CurrentMode)}<br>");
            sb.Append($"<b>Дата:</b> {DateTime.Now:dd.MM.yyyy HH:mm}</p><hr>");

            foreach (var entry in FullSessionHistory)
            {
                string timeStr = TimeSpan.FromMilliseconds(entry.Timestamp).ToString(@"mm\:ss");
                sb.Append($"<div class='entry'><span class='time'>[{timeStr}]</span> <span class='{entry.Type}'><b>{GetRussianType(entry.Type)}:</b></span> {entry.Message}</div>");
            }

            string grade = CalculateGrade();
            sb.Append($"<hr><h2>ИТОГОВАЯ ОЦЕНКА: {grade}</h2>");
            sb.Append($"<p>Баллы: {Score} / {SequenceManager.Instance?.MaxPossibleScore ?? 0} | Штрафы: {PenaltyPoints}</p>");
            sb.Append("</div>");
            sb.Append("<script>window.onload = function() { window.print(); }</script>");
            sb.Append("</body></html>");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[ActionLogger] Ошибка генерации HTML-отчёта: {ex.Message}");
            sb.Append("<html><body><h1>Ошибка генерации отчёта</h1><p>");
            sb.Append(ex.Message);
            sb.Append("</p></body></html>");
        }
        return sb.ToString();
    }
}