using Godot;
using System;

// УСТАРЕЛО: Используйте SimulatorMode из GameManager.cs
// Этот файл оставлен для обратной совместимости
[System.Obsolete("Используйте SimulatorMode из GameManager.cs")]
public enum SimulatorState
{
	Learning = (int)SimulatorMode.Learning,
	Exam = (int)SimulatorMode.Exam,
	Theory = 2 // Theory не имеет аналога в SimulatorMode
}
