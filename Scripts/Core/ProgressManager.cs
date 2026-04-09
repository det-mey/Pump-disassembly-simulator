using Godot;
using System.Collections.Generic;

public partial class ProgressManager : Node
{
    public static ProgressManager Instance { get; private set; }
    private const string SAVE_PATH = "user://Config/progress.json";

    // Словарь: [ScenarioName] = BestGrade (например: "Насос ЦНС" = "A")
    private Dictionary<string, string> _bestGrades = new Dictionary<string, string>();
    private Dictionary<string, int> _bestScores = new Dictionary<string, int>();

    public override void _EnterTree()
    {
        Instance = this;
        LoadProgress();
    }

    public void SaveResult(string scenarioName, int score, string grade)
    {
        // Сохраняем, если результат лучше предыдущего
        if (!_bestScores.ContainsKey(scenarioName) || score > _bestScores[scenarioName])
        {
            _bestScores[scenarioName] = score;
            _bestGrades[scenarioName] = grade;
            
            var data = new Godot.Collections.Dictionary();
            foreach (var pair in _bestScores)
            {
                data[pair.Key] = new Godot.Collections.Dictionary { 
                    { "score", pair.Value }, 
                    { "grade", _bestGrades[pair.Key] } 
                };
            }

            using var file = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Write);
            file.StoreString(Json.Stringify(data));
        }
    }

    private void LoadProgress()
    {
        if (!FileAccess.FileExists(SAVE_PATH)) return;
        using var file = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Read);
        var json = new Json();
        if (json.Parse(file.GetAsText()) == Error.Ok)
        {
            var data = json.Data.AsGodotDictionary();
            foreach (var key in data.Keys)
            {
                var entry = data[key].AsGodotDictionary();
                _bestScores[key.AsString()] = entry["score"].AsInt32();
                _bestGrades[key.AsString()] = entry["grade"].AsString();
            }
        }
    }

    public string GetBestGrade(string scenarioName) => _bestGrades.ContainsKey(scenarioName) ? _bestGrades[scenarioName] : "-";
}