using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Хранит полную историю всех результатов сессий.
/// Каждый результат: дата, сценарий, режим, очки, оценка, время, протокол.
/// Данные сохраняются в user://Config/results_history.json
/// </summary>
public partial class ResultsHistoryManager : Node
{
    public static ResultsHistoryManager Instance { get; private set; }

    private const string SAVE_PATH = "user://Config/results_history.json";
    private const int MAX_HISTORY_ENTRIES = 200;

    public List<ResultEntry> History { get; private set; } = new List<ResultEntry>();

    public override void _EnterTree()
    {
        if (Instance != null) { QueueFree(); return; }
        Instance = this;
        LoadHistory();
    }

    /// <summary>
    /// Добавить новый результат в историю (вызывается из ActionLogger/SequenceManager при завершении сессии)
    /// </summary>
    public void AddResult(string scenarioName, SimulatorMode mode, int score, string grade,
                          float elapsedSeconds, string protocolSummary, string fullProtocolHtml = "")
    {
        var entry = new ResultEntry
        {
            Id = Guid.NewGuid().ToString().Substring(0, 8),
            Date = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            ScenarioName = scenarioName,
            Mode = GetRussianMode(mode),
            Score = score,
            Grade = grade,
            ElapsedSeconds = elapsedSeconds,
            ProtocolSummary = protocolSummary,
            FullProtocolHtml = fullProtocolHtml
        };

        History.Insert(0, entry); // Новые сверху

        // Ограничиваем размер
        while (History.Count > MAX_HISTORY_ENTRIES)
            History.RemoveAt(History.Count - 1);

        SaveHistory();
    }

    /// <summary>
    /// Удалить конкретную запись по ID
    /// </summary>
    public void RemoveEntry(string id)
    {
        History.RemoveAll(e => e.Id == id);
        SaveHistory();
    }

    /// <summary>
    /// Удалить все записи
    /// </summary>
    public void ClearAll()
    {
        History.Clear();
        SaveHistory();
    }

    /// <summary>
    /// Удалить записи старше N дней
    /// </summary>
    public void RemoveOlderThanDays(int days)
    {
        var cutoff = DateTime.Now.AddDays(-days);
        History.RemoveAll(e =>
        {
            if (DateTime.TryParseExact(e.Date, "dd.MM.yyyy HH:mm", null,
                    System.Globalization.DateTimeStyles.None, out var parsed))
            {
                return parsed < cutoff;
            }
            return false;
        });
        SaveHistory();
    }

    private void SaveHistory()
    {
        var dictArray = new Godot.Collections.Array();
        foreach (var entry in History)
        {
            var d = new Godot.Collections.Dictionary
            {
                { "id", entry.Id },
                { "date", entry.Date },
                { "scenario", entry.ScenarioName },
                { "mode", entry.Mode },
                { "score", entry.Score },
                { "grade", entry.Grade },
                { "elapsed", entry.ElapsedSeconds },
                { "protocol", entry.ProtocolSummary },
                { "full_protocol", entry.FullProtocolHtml }
            };
            dictArray.Add(d);
        }

        var wrapper = new Godot.Collections.Dictionary { { "results", dictArray } };

        using var file = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Write);
        file.StoreString(Json.Stringify(wrapper, "  "));
    }

    private void LoadHistory()
    {
        if (!FileAccess.FileExists(SAVE_PATH)) return;

        using var file = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Read);
        var json = new Json();
        if (json.Parse(file.GetAsText()) == Error.Ok)
        {
            var data = json.Data.AsGodotDictionary();
            if (data.ContainsKey("results"))
            {
                History.Clear();
                foreach (var item in data["results"].AsGodotArray())
                {
                    var d = item.AsGodotDictionary();
                    History.Add(new ResultEntry
                    {
                        Id = d.ContainsKey("id") ? d["id"].AsString() : "unknown",
                        Date = d.ContainsKey("date") ? d["date"].AsString() : "-",
                        ScenarioName = d.ContainsKey("scenario") ? d["scenario"].AsString() : "-",
                        Mode = d.ContainsKey("mode") ? d["mode"].AsString() : "-",
                        Score = d.ContainsKey("score") ? d["score"].AsInt32() : 0,
                        Grade = d.ContainsKey("grade") ? d["grade"].AsString() : "-",
                        ElapsedSeconds = d.ContainsKey("elapsed") ? (float)d["elapsed"].AsDouble() : 0f,
                        ProtocolSummary = d.ContainsKey("protocol") ? d["protocol"].AsString() : "",
                        FullProtocolHtml = d.ContainsKey("full_protocol") ? d["full_protocol"].AsString() : ""
                    });
                }
            }
        }
    }

    private static string GetRussianMode(SimulatorMode mode) => mode switch
    {
        SimulatorMode.Learning => "Обучение",
        SimulatorMode.Training => "Тренировка",
        SimulatorMode.Exam => "Экзамен",
        _ => "Неизвестно"
    };
}

/// <summary>
/// Структура записи результата сессии
/// </summary>
public class ResultEntry
{
    public string Id { get; set; } = "";
    public string Date { get; set; } = "";
    public string ScenarioName { get; set; } = "";
    public string Mode { get; set; } = "";
    public int Score { get; set; } = 0;
    public string Grade { get; set; } = "";
    public float ElapsedSeconds { get; set; } = 0f;
    public string ProtocolSummary { get; set; } = "";
    public string FullProtocolHtml { get; set; } = "";

    public string FormattedTime
    {
        get
        {
            int mins = (int)(ElapsedSeconds / 60);
            int secs = (int)(ElapsedSeconds % 60);
            return $"{mins:00}:{secs:00}";
        }
    }

    public string GradeColored
    {
        get
        {
            return Grade switch
            {
                "5 (Отлично)" => "[color=#4caf50]5 (Отлично)[/color]",
                "4 (Хорошо)" => "[color=#8bc34a]4 (Хорошо)[/color]",
                "3 (Удовл.)" => "[color=#ff9800]3 (Удовл.)[/color]",
                "2 (Незачет)" => "[color=#f44336]2 (Незачет)[/color]",
                "1 (Авария)" => "[color=#b71c1c]1 (Авария)[/color]",
                _ => Grade
            };
        }
    }
}
