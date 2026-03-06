//  YesZ - HelloCube Application
//
//  Renders a spinning glTF model (BoxTextured) with directional, ambient,
//  and point lighting. Demonstrates glTF loading and multi-light rendering.
//
//  Depends on: YesZ.Core (Camera3D, Transform3D, DirectionalLight, AmbientLight, PointLight),
//              YesZ.Rendering (Graphics3D, GltfLoader, Model3D), NoZ (IApplication, Graphics, UI, Color, Time)
//  Used by:    Program.cs

using System;
using System.Numerics;
using System.Reflection;
using NoZ;
using YesZ;
using YesZ.Rendering;

namespace YesZ.Samples.HelloCube;

public class HelloCubeApp : IApplication
{
    private Camera3D? _camera;
    private Model3D? _model;
    private Transform3D _modelTransform;
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
        // Set up camera
        _camera = new Camera3D
        {
            Position = new Vector3(0, 0.8f, 4.5f),
            FieldOfView = 50f,
        };

        _modelTransform = Transform3D.Identity;

        // Initialize 3D rendering system
        Graphics3D.Initialize();

        // Load glTF model from embedded resource
        _model = GltfLoader.LoadFromEmbeddedResource(
            Assembly.GetExecutingAssembly(),
            "YesZ.Samples.HelloCube.Assets.BoxTextured.glb");
    }

    public void Update()
    {
        if (_camera == null || _model == null) return;

        Graphics.ClearColor = Color.FromRgb(0x0F172A);

        // Update aspect ratio from window size
        var size = Application.WindowSize;
        if (size.X > 0 && size.Y > 0)
            _camera.AspectRatio = (float)size.X / size.Y;

        // Spin the model
        _rotationAngle += Time.DeltaTime * 1.2f;
        _modelTransform.Rotation = Quaternion.CreateFromYawPitchRoll(_rotationAngle, _rotationAngle * 0.7f, 0);

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

        // Point light — orbits the model to show position-based attenuation
        float orbitAngle = _rotationAngle * 0.8f;
        Graphics3D.AddPointLight(new PointLight
        {
            Position = new Vector3(MathF.Cos(orbitAngle) * 3f, 1.5f, MathF.Sin(orbitAngle) * 3f),
            Color = new Vector3(0.3f, 0.6f, 1.0f),
            Intensity = 2.5f,
            Range = 8f,
        });

        Graphics3D.DrawModel(_model, _modelTransform.LocalMatrix);
        Graphics3D.End();
    }

    public void UpdateUI()
    {
        using (UI.BeginContainer(RootStyle))
        {
            using (UI.BeginColumn(BoxStyle))
            {
                UI.Label("YesZ", TitleStyle);
                UI.Label("Phase 4c - Node Hierarchy", SubtitleStyle);
            }
        }
    }
}
