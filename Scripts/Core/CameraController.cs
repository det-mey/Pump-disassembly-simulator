using Godot;

public partial class CameraController : Node3D
{
    [ExportGroup("Настройки камеры")]
    [Export] public float MouseSensitivity = 0.002f;
    [Export] public float BobFrequency = 2.5f;
    [Export] public float BobAmplitude = 0.06f;

    private Camera3D _camera;
    private Node3D _head;
    private float _originalHeadY;
    private float _headbobTime = 0.0f;
    private float _cameraTilt = 0.0f;

    // Ссылки для получения данных от игрока
    private Player _player;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        _head = GetParent<Node3D>();
        _player = GetTree().GetFirstNodeInGroup("Player") as Player;

        _originalHeadY = _head.Position.Y;

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    /// <summary>
    /// Обработка вращения камеры (вызывается из Player._UnhandledInput)
    /// </summary>
    public void ProcessCameraRotation(InputEventMouseMotion mouseMotion)
    {
        // Поворот тела по горизонтали (Y)
        _head.RotateY(-mouseMotion.Relative.X * MouseSensitivity);
        
        // Поворот камеры по вертикали (X)
        float xRotation = _camera.Rotation.X - mouseMotion.Relative.Y * MouseSensitivity;
        
        // Жесткий замок, чтобы не перевернуться через голову (Gimbal Lock)
        xRotation = Mathf.Clamp(xRotation, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));

        _camera.Rotation = new Vector3(xRotation, _camera.Rotation.Y, 0);
    }

    /// <summary>
    /// Обработка динамики камеры: sway, приседание, head-bobbing
    /// </summary>
    public void ProcessCameraDynamics(Vector2 inputDir, double delta, float flatVelocity, bool isOnFloor, bool isSprinting, bool isCrouching, float itemWeight)
    {
        // Sway (Наклон)
        float targetTilt = -inputDir.X * 0.03f;
        _cameraTilt = Mathf.Lerp(_cameraTilt, targetTilt, 5f * (float)delta);
        _camera.Rotation = new Vector3(_camera.Rotation.X, _camera.Rotation.Y, _cameraTilt);

        // Crouch (Приседание)
        float targetHeadY = isCrouching ? _originalHeadY - 0.6f : _originalHeadY;
        _head.Position = new Vector3(_head.Position.X, Mathf.Lerp(_head.Position.Y, targetHeadY, 12f * (float)delta), _head.Position.Z);

        // Head-Bobbing (Раскачивание)
        if (isOnFloor && flatVelocity > 0.5f)
        {
            float bobSpeedCoef = isSprinting ? 1.4f : (isCrouching ? 0.6f : 1.0f);
            float weightFreqMod = itemWeight > 5 ? 0.8f : 1.0f;
            float weightAmpMod = itemWeight > 5 ? 1.3f : 1.0f;

            _headbobTime += (float)delta * flatVelocity * bobSpeedCoef * weightFreqMod;

            float bobY = Mathf.Sin(_headbobTime * BobFrequency) * BobAmplitude * weightAmpMod;
            float bobX = Mathf.Cos(_headbobTime * BobFrequency / 2) * (BobAmplitude / 2) * weightAmpMod;

            _camera.Position = new Vector3(bobX, bobY, 0);
        }
        else
        {
            _headbobTime = 0;
            _camera.Position = _camera.Position.Lerp(Vector3.Zero, 10f * (float)delta);
        }
    }

    /// <summary>
    /// Сброс камеры в нейтральное положение
    /// </summary>
    public void ResetCamera()
    {
        _cameraTilt = 0;
        _headbobTime = 0;
        _camera.Position = Vector3.Zero;
        _head.Position = new Vector3(_head.Position.X, _originalHeadY, _head.Position.Z);
    }
}
