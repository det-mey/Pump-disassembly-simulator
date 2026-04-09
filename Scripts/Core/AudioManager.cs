using Godot;

public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    // Воспроизведение 2D звука (UI, системные щелчки, работа в руках)
    public void PlaySound(string resourcePath, float volumeDb = 0f, float pitch = 1f)
    {
        if (!FileAccess.FileExists(resourcePath)) return;
        
        var stream = GD.Load<AudioStream>(resourcePath);
        if (stream == null) return;

        var player = new AudioStreamPlayer { Stream = stream, VolumeDb = volumeDb, PitchScale = pitch };
        
        // Привязываем к нашей главной шине звука (чтобы ползунок настроек работал)
        player.Bus = "Master"; 
        AddChild(player);
        
        player.Finished += player.QueueFree; // Самоуничтожение после окончания звука
        player.Play();
    }

    // Воспроизведение 3D звука (Падение предметов, аварии в мире)
    public void PlaySound3D(string resourcePath, Vector3 globalPosition, float volumeDb = 0f, float pitch = 1f)
    {
        if (!FileAccess.FileExists(resourcePath)) return;

        var stream = GD.Load<AudioStream>(resourcePath);
        if (stream == null) return;

        var player = new AudioStreamPlayer3D { 
            Stream = stream, 
            GlobalPosition = globalPosition, 
            VolumeDb = volumeDb, 
            PitchScale = pitch, 
            MaxDistance = 15f,
            Bus = "Master"
        };
        GetTree().Root.AddChild(player);
        
        player.Finished += player.QueueFree;
        player.Play();
    }

	// Воспроизведение напрямую из ресурса AudioStream
    public void PlayStream(AudioStream stream, float volumeDb = 0f, float pitch = 1f)
    {
        if (stream == null) return;

        var player = new AudioStreamPlayer { 
            Stream = stream, 
            VolumeDb = volumeDb, 
            PitchScale = pitch,
            Bus = "Master" 
        };
        
        AddChild(player);
        player.Finished += player.QueueFree; 
        player.Play();
    }

    public void PlayStream3D(AudioStream stream, Vector3 globalPosition, float volumeDb = 0f, float pitch = 1f)
    {
        if (stream == null) return;

        var player = new AudioStreamPlayer3D { 
            Stream = stream, 
            GlobalPosition = globalPosition, 
            VolumeDb = volumeDb, 
            PitchScale = pitch, 
            MaxDistance = 15f,
            Bus = "Master"
        };
        
        GetTree().Root.AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
    }
}