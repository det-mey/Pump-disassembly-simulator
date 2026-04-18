using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// УСТАРЕВШИЙ МЕНЕДЖЕР: Функционал полностью перенесён в SequenceManager.
/// Оставлен для обратной совместимости — не регистрируйте его как autoload.
/// </summary>
[Obsolete("Используйте SequenceManager и GameManager.CurrentMode")]
public partial class MachineManager : Node
{
	public static MachineManager Instance { get; private set; }

	[Export] public SimulatorMode CurrentMode { get; set; } = SimulatorMode.Learning;
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
			GD.Print($"[MachineManager] Правильный шаг: {stepName}");
		}
		else if (CurrentMode == SimulatorMode.Exam)
		{
			GD.PrintErr($"[MachineManager] Нарушение последовательности. Ожидалось: {_assemblySteps.Peek()}");
		}
	}
}
