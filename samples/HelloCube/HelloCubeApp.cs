//  YesZ - HelloCube Application
//
//  Renders a spinning lit cube with directional and ambient lighting.
//  Demonstrates Graphics3D lit material system with Blinn-Phong shading.
//
//  Depends on: YesZ.Core (Camera3D, Transform3D, Mesh3D, Mesh3DBuilder, DirectionalLight, AmbientLight),
//              YesZ.Rendering (Graphics3D, Material3D, TextureLoader), NoZ (IApplication, Graphics, UI, Color, Time)
//  Used by:    Program.cs

using System.Numerics;
using NoZ;
using YesZ;
using YesZ.Rendering;

namespace YesZ.Samples.HelloCube;

public class HelloCubeApp : IApplication
{
    private Camera3D? _camera;
    private Mesh3D? _cube;
    private Material3D? _material;
    private Transform3D _cubeTransform;
    private float _rotationAngle;

    private static readonly ContainerStyle RootStyle = new()
    {
        Size = new Size2(Size.Percent(1), Size.Percent(1)),
    };

    private static readonly ContainerStyle BoxStyle = new()
    {
        Size = Size2.Fit,
        AlignX = Align.Center,
        AlignY = Align.Max,
        Color = Color.FromRgba(0x16A34A, 0.85f),
        Padding = EdgeInsets.Symmetric(12, 24),
        Margin = new EdgeInsets(0, 0, 32, 0),
        Border = new BorderStyle { Radius = 8 },
    };

    private static readonly LabelStyle TitleStyle = new()
    {
        FontSize = 24,
        Color = Color.White,
        AlignX = Align.Center,
    };

    private static readonly LabelStyle SubtitleStyle = new()
    {
        FontSize = 14,
        Color = Color.FromRgba(0xFFFFFF, 0.7f),
        AlignX = Align.Center,
    };

    public void LoadAssets()
    {
        // Create cube mesh
        var (vertices, indices) = Mesh3DBuilder.CreateCube();
        _cube = Mesh3D.Create(vertices, indices);

        // Set up camera
        _camera = new Camera3D
        {
            Position = new Vector3(0, 0.8f, 4.5f),
            FieldOfView = 50f,
        };

        // Initialize cube transform
        _cubeTransform = Transform3D.Identity;

        // Initialize 3D rendering system
        Graphics3D.Initialize();

        // Create checkerboard texture and material
        var texture = TextureLoader.CreateCheckerboard(
            256,
            new Color(0.9f, 0.85f, 0.7f),
            new Color(0.4f, 0.35f, 0.25f),
            cellSize: 32);

        _material = Graphics3D.CreateLitMaterial();
        _material.BaseColorTexture = texture;
        _material.Roughness = 0.6f;
    }

    public void Update()
    {
        if (_camera == null || _cube == null) return;

        Graphics.ClearColor = Color.FromRgb(0x0F172A);

        // Update aspect ratio from window size
        var size = Application.WindowSize;
        if (size.X > 0 && size.Y > 0)
            _camera.AspectRatio = (float)size.X / size.Y;

        // Spin the cube
        _rotationAngle += Time.DeltaTime * 1.2f;
        _cubeTransform.Rotation = Quaternion.CreateFromYawPitchRoll(_rotationAngle, _rotationAngle * 0.7f, 0);

        // 3D rendering pass
        Graphics3D.Begin(_camera);

        // Lighting — directional from upper-left-front, soft ambient fill
        Graphics3D.SetDirectionalLight(new DirectionalLight
        {
            Direction = Vector3.Normalize(new Vector3(-0.5f, -1.0f, -0.5f)),
            Color = Vector3.One,
            Intensity = 1.5f,
        });
        Graphics3D.SetAmbientLight(new AmbientLight
        {
            Color = Vector3.One,
            Intensity = 0.15f,
        });

        Graphics3D.SetMaterial(_material);
        Graphics3D.DrawMesh(_cube, _cubeTransform.LocalMatrix);
        Graphics3D.End();
    }

    public void UpdateUI()
    {
        using (UI.BeginContainer(RootStyle))
        {
            using (UI.BeginColumn(BoxStyle))
            {
                UI.Label("YesZ", TitleStyle);
                UI.Label("Phase 3b - Lit Shading", SubtitleStyle);
            }
        }
    }
}
