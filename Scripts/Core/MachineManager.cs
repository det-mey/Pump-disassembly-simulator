using Godot;
using System;
using System.Collections.Generic;

public partial class MachineManager : Node
{
	public static MachineManager Instance { get; private set; }
	
	[Export] public SimulatorState CurrentState { get; set; } = SimulatorState.Learning;
	private Queue<string> _assemblySteps = new Queue<string>();

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		LoadConfiguration();
	}

	private void LoadConfiguration()
	{
		_assemblySteps.Enqueue("Снять крышку корпуса");
		_assemblySteps.Enqueue("Открутить крепежную гайку М16");
		_assemblySteps.Enqueue("Извлечь изношенную прокладку");
	}

	public void CompleteStep(string stepName)
	{
		if (_assemblySteps.Count > 0 && _assemblySteps.Peek() == stepName)
		{
			_assemblySteps.Dequeue();
			AssessmentManager.Instance.RegisterSuccess($"Правильный шаг: {stepName}");
		}
		else if (CurrentState == SimulatorState.Exam)
		{
			AssessmentManager.Instance.RegisterError($"Нарушение последовательности. Ожидалось: {_assemblySteps.Peek()}");
		}
	}
}
