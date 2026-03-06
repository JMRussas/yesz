//  YesZ - HelloCube Application
//
//  Renders a spinning glTF model (BoxTextured) with lighting, and an animated
//  skinned model (RiggedSimple) demonstrating skeletal animation.
//
//  Depends on: YesZ.Core (Camera3D, Transform3D, DirectionalLight, AmbientLight, PointLight,
//              AnimationPlayer3D, JointMatrixComputer),
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
    private Model3D? _skinnedModel;
    private Transform3D _modelTransform;
    private float _rotationAngle;
    private AnimationPlayer3D? _animPlayer;
    private Matrix4x4[]? _localPoses;
    private Matrix4x4[]? _jointMatrices;

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
        // Set up camera — pulled back to see both models
        _camera = new Camera3D
        {
            Position = new Vector3(0, 1.5f, 6f),
            FieldOfView = 50f,
        };

        _modelTransform = Transform3D.Identity;

        // Initialize 3D rendering system
        Graphics3D.Initialize();

        var assembly = Assembly.GetExecutingAssembly();

        // Load static glTF model
        _model = GltfLoader.LoadFromEmbeddedResource(assembly,
            "YesZ.Samples.HelloCube.Assets.BoxTextured.glb");

        // Load skinned glTF model
        _skinnedModel = GltfLoader.LoadFromEmbeddedResource(assembly,
            "YesZ.Samples.HelloCube.Assets.RiggedSimple.glb");

        // Set up animation player if model has animations
        if (_skinnedModel.Skeleton != null && _skinnedModel.Animations is { Length: > 0 })
        {
            _animPlayer = new AnimationPlayer3D();
            _animPlayer.Play(_skinnedModel.Animations[0]);
            _localPoses = new Matrix4x4[_skinnedModel.Skeleton.JointCount];
            _jointMatrices = new Matrix4x4[_skinnedModel.Skeleton.JointCount];
        }
    }

    public void Update()
    {
        if (_camera == null || _model == null) return;

        Graphics.ClearColor = Color.FromRgb(0x0F172A);

        // Update aspect ratio from window size
        var size = Application.WindowSize;
        if (size.X > 0 && size.Y > 0)
            _camera.AspectRatio = (float)size.X / size.Y;

        // Spin the static model
        _rotationAngle += Time.DeltaTime * 1.2f;
        _modelTransform.Rotation = Quaternion.CreateFromYawPitchRoll(_rotationAngle, _rotationAngle * 0.7f, 0);

        // Update animation
        _animPlayer?.Update(Time.DeltaTime);

        // 3D rendering pass
        Graphics3D.Begin(_camera);

        // Lighting
        var directionalLight = new DirectionalLight
        {
            Direction = Vector3.Normalize(new Vector3(-0.5f, -1.0f, -0.5f)),
            Color = Vector3.One,
            Intensity = 1.5f,
        };
        Graphics3D.SetDirectionalLight(directionalLight);
        Graphics3D.SetAmbientLight(new AmbientLight
        {
            Color = Vector3.One,
            Intensity = 0.15f,
        });

        // Point light orbiting both models
        float orbitAngle = _rotationAngle * 0.8f;
        Graphics3D.AddPointLight(new PointLight
        {
            Position = new Vector3(MathF.Cos(orbitAngle) * 4f, 2f, MathF.Sin(orbitAngle) * 4f),
            Color = new Vector3(0.3f, 0.6f, 1.0f),
            Intensity = 2.5f,
            Range = 10f,
        });

        // Compute reusable transforms
        var staticWorld = _modelTransform.LocalMatrix
            * Matrix4x4.CreateTranslation(-2f, 0, 0);

        // Enable shadow rendering — casters are collected during DrawModel/DrawAnimatedModel,
        // and the depth pass executes in Graphics3D.End() via direct driver calls.
        Graphics3D.RenderShadowPass();

        // Draw static model on the left
        Graphics3D.DrawModel(_model, staticWorld);

        // Draw animated skinned model on the right
        if (_skinnedModel != null && _skinnedModel.Skeleton != null
            && _animPlayer != null && _skinnedModel.BindPose != null
            && _localPoses != null && _jointMatrices != null)
        {
            var skeleton = _skinnedModel.Skeleton;
            _animPlayer.Sample(skeleton, _skinnedModel.BindPose, _localPoses);
            JointMatrixComputer.Compute(skeleton, _localPoses, _jointMatrices);

            var skinnedWorld = Matrix4x4.CreateTranslation(2f, 0, 0);
            Graphics3D.DrawAnimatedModel(_skinnedModel, skinnedWorld, _jointMatrices);
        }

        Graphics3D.End();
    }

    public void UpdateUI()
    {
        using (UI.BeginContainer(RootStyle))
        {
            using (UI.BeginColumn(BoxStyle))
            {
                UI.Label("YesZ", TitleStyle);
                UI.Label("Phase 6c - Cascaded Shadows", SubtitleStyle);
            }
        }
    }
}
