using Godot;

/// <summary>
/// VR-обёртка для MainMenu: черное пространство, меню перед камерой.
/// MainMenu обрабатывает ввод через VR-контроллеры с лазером (VRController).
/// </summary>
[GlobalClass]
public partial class VRMenu : Node3D
{
    [Export] private XROrigin3D _xrOrigin;
    [Export] private XRCamera3D _xrCamera;
    [Export] private Node3D _menuHolder;
    [Export] private MeshInstance3D _blackBackground;

    [Export] public float MenuDistance = 3.0f;
    [Export] public float MenuHeightOffset = -0.1f;

    public override void _Ready()
    {
        if (_xrCamera == null)
            _xrCamera = GetNodeOrNull<XRCamera3D>("XROrigin3D/XRCamera3D");
        if (_menuHolder == null)
            _menuHolder = GetNodeOrNull<Node3D>("MenuHolder");
        if (_blackBackground == null)
            _blackBackground = GetNodeOrNull<MeshInstance3D>("BlackBackground");

        // Фиксируем позицию игрока
        if (_xrOrigin != null)
            _xrOrigin.Position = Vector3.Zero;

        // Настраиваем черный фон
        if (_blackBackground != null)
        {
            _blackBackground.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0, 0, 0, 1),
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
        }

        // В VR курсор не нужен
        Input.MouseMode = Input.MouseModeEnum.Visible;

        GD.Print("[VRMenu] VR-меню инициализировано");
    }

    public override void _Process(double delta)
    {
        if (_xrCamera == null || _menuHolder == null) return;

        // Меню всегда перед камерой
        var forward = -_xrCamera.GlobalBasis.Z;
        forward.Y = 0;
        forward = forward.Normalized();

        _menuHolder.GlobalPosition = _xrCamera.GlobalPosition + forward * MenuDistance + Vector3.Up * MenuHeightOffset;
        _menuHolder.LookAt(_xrCamera.GlobalPosition, Vector3.Up);
    }
}
