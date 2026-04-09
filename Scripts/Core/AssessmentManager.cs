using Godot;
using System;
using System.Collections.Generic;

public partial class AssessmentManager : Node
{
	public static AssessmentManager Instance { get; private set; }
	public int ErrorCount { get; private set; } = 0;
	public List<string> ErrorLog { get; private set; } = [];

	public override void _EnterTree()
	{
		Instance = this;
	}

	public void RegisterError(string message)
	{
		ErrorCount++;
		ErrorLog.Add(message);
		GD.PrintErr($"[ОЦЕНКА] Ошибка: {message}. Всего ошибок: {ErrorCount}");
	}

	public void RegisterSuccess(string message)
	{
		GD.Print($"[ОЦЕНКА] Успех: {message}");
	}
}
