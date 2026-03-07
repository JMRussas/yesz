//  YesZ - 3D Graphics
//
//  Static entry point for 3D rendering. Fully decoupled from NoZ's batch system.
//  All rendering (shadow depth + scene) uses direct IGraphicsDriver / IGraphicsDriver3D calls.
//
//  Begin() stores the camera and clears per-frame state.
//  DrawMesh() collects draw commands into a list.
//  End() executes: shadow depth passes → 3D scene pass → sets prepass flag so NoZ's
//  subsequent BeginScenePass uses LoadOp.Load (preserving 3D content for 2D overlay).
//
//  Two rendering paths:
//    Unlit/textured — full MVP in globals ("viewproj" binding), material params in UBO.
//    Lit — VP in globals, model + normal matrix in LitMaterialUniforms (@binding 1),
//      light data in LightUniforms (@binding 4).
//    Lit+Shadow (CSM) — Same as Lit, plus CascadeShadowUniforms (@binding 5) and
//      depth texture array (@binding 6) for cascaded shadow sampling.
//
//  Matrix convention: direct driver uploads use raw bytes (no transpose). C# row-major
//  memory maps naturally to WGSL column-major (row-vector/column-vector convention flip
//  cancels storage order flip).
//
//  Depends on: YesZ.Core (Camera3D, Mesh3D, MeshVertex3D, LightEnvironment, DirectionalLight, PointLight, AmbientLight,
//              LightSpaceComputer),
//              YesZ.Rendering (Material3D, MaterialUniforms, LitMaterialUniforms, LightUniforms,
//              ShadowDepthUniforms, CascadeShadowUniforms, ShadowConfig),
//              NoZ (Graphics, ShaderFlags, ShaderBinding, ShaderBindingType, TextureFormat, TextureFilter,
//              IGraphicsDriver, IGraphicsDriver3D)
//  Used by:    Game code, samples

using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using NoZ;
using NoZ.Platform;

namespace YesZ.Rendering;

public static class Graphics3D
{
    private static nuint _unlitShader;
    private static nuint _texturedShader;
    private static nuint _litShader;
    private static nuint _litCascadeShadowShader;
    private static nuint _skinnedLitShader;
    private static nuint _shadowDepthShader;
    private static Camera3D? _camera;
    private static Matrix4x4 _viewProjection;
    private static bool _initialized;
    private static nuint _defaultWhiteTexture;
    private static Material3D? _currentMaterial;
    private static readonly LightEnvironment _lights = new();
    private static IGraphicsDriver3D? _driver3d;

    // Cascaded shadow mapping state
    private static nuint _shadowDepthTextureArray;
    private static ShadowConfig _shadowConfig = new();
    private static readonly Matrix4x4[] _cascadeLightViewProjections = new Matrix4x4[ShadowConfig.MaxCascades];
    private static float[]? _cascadeSplits;

    // Draw command collection — populated during DrawMesh, executed in End()
    private struct SceneDrawCommand
    {
        public nuint ShaderHandle;
        public nuint MeshHandle;
        public nuint TextureHandle;
        public int IndexCount;
        public Matrix4x4 WorldMatrix;
        public LitMaterialUniforms LitMaterial;
        public MaterialUniforms UnlitMaterial;
        public bool IsLit;
        public bool UseShadow;
    }

    private static readonly List<SceneDrawCommand> _sceneDrawCommands = new();

    // Shadow caster collection — subset of scene draws needed for depth pass
    private struct ShadowCaster
    {
        public nuint MeshHandle;
        public int IndexCount;
        public Matrix4x4 WorldMatrix;
    }

    private static readonly List<ShadowCaster> _shadowCasters = new();
    private static bool _shadowPassEnabled;

    // Globals struct matching WGSL Globals (80 bytes with padding)
    [StructLayout(LayoutKind.Sequential)]
    private struct Globals3D
    {
        public Matrix4x4 Projection;  // 64 bytes
        public float Time;            // 4 bytes
        private float _pad0, _pad1, _pad2;  // 12 bytes padding = 80 total
    }

    public static void Initialize()
    {
        if (_initialized) return;

        var driver = Graphics.Driver;
        _driver3d = driver as IGraphicsDriver3D
            ?? throw new InvalidOperationException("Graphics driver does not implement IGraphicsDriver3D");

        var flags = ShaderFlags.Depth | ShaderFlags.DepthLess;

        // Unlit shader (vertex color only, no textures)
        _unlitShader = CreateShaderFromEmbedded(
            "YesZ.Rendering.Shaders.unlit3d.wgsl",
            "unlit3d",
            flags,
            new List<ShaderBinding>
            {
                new() { Binding = 0, Type = ShaderBindingType.UniformBuffer, Name = "viewproj" },
            });

        // Textured shader (texture × vertex color × material color factor)
        _texturedShader = CreateShaderFromEmbedded(
            "YesZ.Rendering.Shaders.textured3d.wgsl",
            "textured3d",
            flags,
            new List<ShaderBinding>
            {
                new() { Binding = 0, Type = ShaderBindingType.UniformBuffer, Name = "viewproj" },
                new() { Binding = 1, Type = ShaderBindingType.UniformBuffer, Name = "material" },
                new() { Binding = 2, Type = ShaderBindingType.Texture2D, Name = "base_color_texture" },
                new() { Binding = 3, Type = ShaderBindingType.Sampler, Name = "base_color_sampler" },
            });

        // Lit shader (Blinn-Phong diffuse + specular + ambient)
        _litShader = CreateShaderFromEmbedded(
            "YesZ.Rendering.Shaders.lit3d.wgsl",
            "lit3d",
            flags,
            new List<ShaderBinding>
            {
                new() { Binding = 0, Type = ShaderBindingType.UniformBuffer, Name = "viewproj" },
                new() { Binding = 1, Type = ShaderBindingType.UniformBuffer, Name = "material" },
                new() { Binding = 2, Type = ShaderBindingType.Texture2D, Name = "base_color_texture" },
                new() { Binding = 3, Type = ShaderBindingType.Sampler, Name = "base_color_sampler" },
                new() { Binding = 4, Type = ShaderBindingType.UniformBuffer, Name = "lights" },
            });

        // Lit + cascaded shadow shader (Blinn-Phong + CSM + PCF)
        _litCascadeShadowShader = CreateShaderFromEmbedded(
            "YesZ.Rendering.Shaders.lit_cascade_shadow3d.wgsl",
            "lit_cascade_shadow3d",
            flags,
            new List<ShaderBinding>
            {
                new() { Binding = 0, Type = ShaderBindingType.UniformBuffer, Name = "viewproj" },
                new() { Binding = 1, Type = ShaderBindingType.UniformBuffer, Name = "material" },
                new() { Binding = 2, Type = ShaderBindingType.Texture2D, Name = "base_color_texture" },
                new() { Binding = 3, Type = ShaderBindingType.Sampler, Name = "base_color_sampler" },
                new() { Binding = 4, Type = ShaderBindingType.UniformBuffer, Name = "lights" },
                new() { Binding = 5, Type = ShaderBindingType.UniformBuffer, Name = "shadow" },
                new() { Binding = 6, Type = ShaderBindingType.DepthTexture2DArray, Name = "shadow_maps" },
            });

        // Skinned lit shader (skeletal animation + Blinn-Phong)
        _skinnedLitShader = CreateShaderFromEmbedded(
            "YesZ.Rendering.Shaders.skinned3d.wgsl",
            "skinned3d",
            flags,
            new List<ShaderBinding>
            {
                new() { Binding = 0, Type = ShaderBindingType.UniformBuffer, Name = "viewproj" },
                new() { Binding = 1, Type = ShaderBindingType.UniformBuffer, Name = "material" },
                new() { Binding = 2, Type = ShaderBindingType.Texture2D, Name = "base_color_texture" },
                new() { Binding = 3, Type = ShaderBindingType.Sampler, Name = "base_color_sampler" },
                new() { Binding = 4, Type = ShaderBindingType.UniformBuffer, Name = "lights" },
                new() { Binding = 5, Type = ShaderBindingType.UniformBuffer, Name = "joints" },
            });

        // Shadow depth shader — no globals, light VP embedded in material UBO
        _shadowDepthShader = CreateShaderFromEmbedded(
            "YesZ.Rendering.Shaders.shadow_depth.wgsl",
            "shadow_depth",
            ShaderFlags.Depth | ShaderFlags.DepthLess,
            new List<ShaderBinding>
            {
                new() { Binding = 0, Type = ShaderBindingType.UniformBuffer, Name = "material" },
            });

        // Default 1×1 white texture — untextured materials sample this (identity multiply)
        var white = new byte[] { 255, 255, 255, 255 };
        _defaultWhiteTexture = driver.CreateTexture(1, 1, white, TextureFormat.RGBA8, TextureFilter.Point, "DefaultWhite");

        // Create shadow map depth texture array (all cascades in one texture)
        _shadowDepthTextureArray = _driver3d.CreateDepthTextureArray(
            _shadowConfig.Resolution, _shadowConfig.Resolution, ShadowConfig.MaxCascades, "ShadowMap_CascadeArray");

        _initialized = true;
    }

    public static Material3D CreateMaterial()
    {
        if (!_initialized)
            Initialize();

        return new Material3D(_texturedShader, _defaultWhiteTexture);
    }

    public static Material3D CreateLitMaterial()
    {
        if (!_initialized)
            Initialize();

        return new Material3D(_litShader, _defaultWhiteTexture);
    }

    public static void SetMaterial(Material3D? material)
    {
        _currentMaterial = material;
    }

    public static void SetDirectionalLight(in DirectionalLight light)
    {
        _lights.Directional = light;
    }

    public static void SetAmbientLight(in AmbientLight light)
    {
        _lights.Ambient = light;
    }

    public static void AddPointLight(in PointLight light)
    {
        _lights.AddPointLight(in light);
    }

    public static LightEnvironment Lights => _lights;

    public static void Begin(Camera3D camera)
    {
        if (!_initialized)
            Initialize();

        _camera = camera;
        _viewProjection = camera.ViewProjectionMatrix;
        _lights.ClearPointLights();
        _sceneDrawCommands.Clear();
        _shadowCasters.Clear();
        _shadowPassEnabled = false;
    }

    public static void DrawMesh(Mesh3D mesh, Matrix4x4 worldMatrix)
    {
        if (_camera == null) return;

        bool isLit = _currentMaterial != null && _currentMaterial.ShaderHandle == _litShader;

        if (isLit)
        {
            CollectLitDraw(mesh, worldMatrix);
        }
        else
        {
            CollectUnlitDraw(mesh, worldMatrix);
        }
    }

    public static void DrawModel(Model3D model, Matrix4x4 worldMatrix)
    {
        var savedMaterial = _currentMaterial;
        DrawNode(model.Root, worldMatrix, model);
        _currentMaterial = savedMaterial;
    }

    private static void DrawNode(ModelNode node, Matrix4x4 parentWorld, Model3D model)
    {
        var world = node.LocalTransform * parentWorld;

        if (node.MeshGroupIndex >= 0 && node.MeshGroupIndex < model.MeshGroups.Length)
        {
            var group = model.MeshGroups[node.MeshGroupIndex];
            foreach (var prim in group.Primitives)
            {
                int matIdx = prim.MaterialIndex;
                if (matIdx >= 0 && matIdx < model.Materials.Length)
                    SetMaterial(model.Materials[matIdx]);
                else if (model.Materials.Length > 0)
                    SetMaterial(model.Materials[0]);

                if (prim.Mesh != null)
                    DrawMesh(prim.Mesh, world);
            }
        }

        foreach (var child in node.Children)
            DrawNode(child, world, model);
    }

    public static void DrawSkinnedMesh(SkinnedMesh3D mesh, ReadOnlySpan<Matrix4x4> jointMatrices)
    {
        if (_camera == null || _skinnedLitShader == 0) return;

        // Skinned meshes are collected as draw commands and executed in End()
        var uniforms = new LitMaterialUniforms
        {
            Model = Matrix4x4.Identity,
            NormalMatrix = Matrix4x4.Identity,
            BaseColorFactor = _currentMaterial?.BaseColorFactor ?? new Vector4(1, 1, 1, 1),
            Metallic = _currentMaterial?.Metallic ?? 0,
            Roughness = _currentMaterial?.Roughness ?? 0.5f,
        };

        // For skinned meshes we need to store joint data — use a special draw command
        // Since joints can be large, we execute skinned draws immediately during End()
        // by storing the joint uniforms in the command
        _sceneDrawCommands.Add(new SceneDrawCommand
        {
            ShaderHandle = _skinnedLitShader,
            MeshHandle = mesh.RenderMesh.Handle,
            TextureHandle = _currentMaterial?.BaseColorTexture ?? _defaultWhiteTexture,
            IndexCount = mesh.IndexCount,
            WorldMatrix = Matrix4x4.Identity, // unused for skinned — joint matrices handle placement
            LitMaterial = uniforms,
            IsLit = true,
            UseShadow = false,
        });

        // Store joint matrices for the skinned draw — we need to capture them now
        // since the span may not be valid later. Store in a side buffer.
        var jointUniforms = new JointMatrixUniforms();
        int count = Math.Min(jointMatrices.Length, JointMatrixUniforms.MaxJoints);
        for (int i = 0; i < count; i++)
            jointUniforms.Set(i, in jointMatrices[i]);
        _skinnedJointData.Add(jointUniforms);
    }

    // Side storage for skinned mesh joint data (indexed same as skinned draw commands)
    private static readonly List<JointMatrixUniforms> _skinnedJointData = new();

    public static void DrawAnimatedModel(
        Model3D model, Matrix4x4 worldMatrix, ReadOnlySpan<Matrix4x4> jointMatrices)
    {
        var savedMaterial = _currentMaterial;
        DrawNodeAnimated(model.Root, worldMatrix, model, jointMatrices);
        _currentMaterial = savedMaterial;
    }

    private static void DrawNodeAnimated(
        ModelNode node, Matrix4x4 parentWorld, Model3D model, ReadOnlySpan<Matrix4x4> jointMatrices)
    {
        var world = node.LocalTransform * parentWorld;

        if (node.MeshGroupIndex >= 0 && node.MeshGroupIndex < model.MeshGroups.Length)
        {
            var group = model.MeshGroups[node.MeshGroupIndex];
            foreach (var prim in group.Primitives)
            {
                int matIdx = prim.MaterialIndex;
                if (matIdx >= 0 && matIdx < model.Materials.Length)
                    SetMaterial(model.Materials[matIdx]);
                else if (model.Materials.Length > 0)
                    SetMaterial(model.Materials[0]);

                if (prim.SkinnedMesh != null && jointMatrices.Length > 0)
                {
                    DrawSkinnedMesh(prim.SkinnedMesh, jointMatrices);
                }
                else if (prim.Mesh != null)
                {
                    DrawMesh(prim.Mesh, world);
                }
            }
        }

        foreach (var child in node.Children)
            DrawNodeAnimated(child, world, model, jointMatrices);
    }

    public static nuint ShadowDepthTextureHandle => _shadowDepthTextureArray;
    public static Matrix4x4 LightViewProjection => _cascadeLightViewProjections[0];
    public static ShadowConfig ShadowSettings => _shadowConfig;

    public static void ConfigureShadows(ShadowConfig config)
    {
        if (!_initialized)
            Initialize();

        if (config.Resolution != _shadowConfig.Resolution)
        {
            if (_shadowDepthTextureArray != 0)
                _driver3d!.DestroyDepthTextureArray(_shadowDepthTextureArray);
            _shadowDepthTextureArray = _driver3d!.CreateDepthTextureArray(
                config.Resolution, config.Resolution, ShadowConfig.MaxCascades, "ShadowMap_CascadeArray");
        }
        _shadowConfig = config;
    }

    public static void RenderShadowPass()
    {
        _shadowPassEnabled = true;
    }

    public static void End()
    {
        if (_camera == null) return;

        var driver = Graphics.Driver;

        // 1. Execute shadow depth passes
        if (_shadowPassEnabled && _shadowCasters.Count > 0)
            ExecuteShadowPass(driver);

        // 2. Execute 3D scene pass
        if (_sceneDrawCommands.Count > 0)
            ExecuteScenePass(driver);

        _camera = null;
        _currentMaterial = null;
        _skinnedJointData.Clear();
    }

    private static void ExecuteShadowPass(IGraphicsDriver driver)
    {
        if (_camera == null || _shadowDepthShader == 0)
            return;

        var directional = _lights.Directional;
        int cascadeCount = Math.Clamp(_shadowConfig.CascadeCount, 1, ShadowConfig.MaxCascades);

        // Compute cascade split distances
        _cascadeSplits = CascadeSplitComputer.ComputeSplits(
            _camera.NearPlane,
            Math.Min(_shadowConfig.ShadowDistance, _camera.FarPlane),
            cascadeCount,
            _shadowConfig.Lambda);

        // Render each cascade's depth map into the texture array
        for (int cascade = 0; cascade < cascadeCount; cascade++)
        {
            var (lightView, lightProj) = LightSpaceComputer.Compute(
                in directional, _camera, _cascadeSplits[cascade], _cascadeSplits[cascade + 1]);
            _cascadeLightViewProjections[cascade] = lightView * lightProj;

            _driver3d!.BeginDepthOnlyPassLayer(
                _shadowDepthTextureArray, cascade, _shadowConfig.Resolution, _shadowConfig.Resolution);

            for (int i = 0; i < _shadowCasters.Count; i++)
            {
                var caster = _shadowCasters[i];

                var uniforms = new ShadowDepthUniforms
                {
                    LightViewProj = _cascadeLightViewProjections[cascade],
                    Model = caster.WorldMatrix,
                };

                driver.BindShader(_shadowDepthShader);
                driver.SetUniform("material", MemoryMarshal.AsBytes(
                    new ReadOnlySpan<ShadowDepthUniforms>(in uniforms)));
                driver.BindMesh(caster.MeshHandle);
                driver.DrawElements(0, caster.IndexCount);
            }

            _driver3d!.EndDepthOnlyPass();
        }
    }

    private static void ExecuteScenePass(IGraphicsDriver driver)
    {
        if (_camera == null) return;

        // Start 3D scene pass (clears color + depth)
        _driver3d!.BeginScenePass3D(Graphics.ClearColor);

        // Upload globals (VP matrix) — named "viewproj" to avoid driver's special "globals" handling
        var globals = new Globals3D { Projection = _viewProjection };
        driver.SetUniform("viewproj", MemoryMarshal.AsBytes(
            new ReadOnlySpan<Globals3D>(in globals)));

        // Upload light UBO once for the entire scene pass
        bool hasLitDraws = false;
        bool hasShadowDraws = false;
        foreach (var cmd in _sceneDrawCommands)
        {
            if (cmd.IsLit) hasLitDraws = true;
            if (cmd.UseShadow) hasShadowDraws = true;
        }

        if (hasLitDraws)
            UploadLightUniforms(driver);

        if (hasShadowDraws)
            UploadCascadeShadowUniforms(driver);

        // Execute each draw command
        int skinnedIndex = 0;
        bool viewprojIsVP = true; // tracks whether viewproj holds VP or was overwritten by unlit MVP
        foreach (var cmd in _sceneDrawCommands)
        {
            driver.BindShader(cmd.ShaderHandle);

            if (cmd.IsLit)
            {
                // Lit path needs VP in viewproj — re-upload if unlit draw overwrote it
                if (!viewprojIsVP)
                {
                    driver.SetUniform("viewproj", MemoryMarshal.AsBytes(
                        new ReadOnlySpan<Globals3D>(in globals)));
                    viewprojIsVP = true;
                }

                driver.SetUniform("material", MemoryMarshal.AsBytes(
                    new ReadOnlySpan<LitMaterialUniforms>(in cmd.LitMaterial)));

                // Handle skinned shaders — upload joint matrices
                if (cmd.ShaderHandle == _skinnedLitShader && skinnedIndex < _skinnedJointData.Count)
                {
                    var joints = _skinnedJointData[skinnedIndex++];
                    driver.SetUniform("joints", MemoryMarshal.AsBytes(
                        new ReadOnlySpan<JointMatrixUniforms>(in joints)));
                }
            }
            else
            {
                // Unlit/textured — per-draw MVP in viewproj
                var mvp = cmd.WorldMatrix * _viewProjection;
                var unlitGlobals = new Globals3D { Projection = mvp };
                driver.SetUniform("viewproj", MemoryMarshal.AsBytes(
                    new ReadOnlySpan<Globals3D>(in unlitGlobals)));
                viewprojIsVP = false;

                if (cmd.ShaderHandle == _texturedShader)
                {
                    driver.SetUniform("material", MemoryMarshal.AsBytes(
                        new ReadOnlySpan<MaterialUniforms>(in cmd.UnlitMaterial)));
                }
            }

            if (cmd.UseShadow)
                _driver3d!.BindDepthTextureArrayForSampling(_shadowDepthTextureArray);

            driver.BindTexture(cmd.TextureHandle, 0, TextureFilter.Linear);
            driver.BindMesh(cmd.MeshHandle);
            driver.DrawElements(0, cmd.IndexCount);
        }

        // End 3D scene pass — sets prepass flag so NoZ's BeginScenePass uses LoadOp.Load
        _driver3d!.EndScenePass3D();
    }

    internal static Matrix4x4 ComputeNormalMatrix(in Matrix4x4 model)
    {
        if (Matrix4x4.Invert(model, out var inverted))
        {
            return Matrix4x4.Transpose(inverted);
        }

        // Singular matrix fallback: use model as-is (correct for rotation + uniform scale)
        return model;
    }

    private static void CollectLitDraw(Mesh3D mesh, Matrix4x4 worldMatrix)
    {
        // Collect shadow caster
        if (_shadowPassEnabled)
        {
            _shadowCasters.Add(new ShadowCaster
            {
                MeshHandle = mesh.RenderMesh.Handle,
                IndexCount = mesh.IndexCount,
                WorldMatrix = worldMatrix,
            });
        }

        bool useShadowShader = _shadowPassEnabled && _litCascadeShadowShader != 0 && _shadowDepthTextureArray != 0;

        var normalMatrix = ComputeNormalMatrix(in worldMatrix);

        var uniforms = new LitMaterialUniforms
        {
            Model = worldMatrix,
            NormalMatrix = normalMatrix,
            BaseColorFactor = _currentMaterial!.BaseColorFactor,
            Metallic = _currentMaterial.Metallic,
            Roughness = _currentMaterial.Roughness,
        };

        _sceneDrawCommands.Add(new SceneDrawCommand
        {
            ShaderHandle = useShadowShader ? _litCascadeShadowShader : _litShader,
            MeshHandle = mesh.RenderMesh.Handle,
            TextureHandle = _currentMaterial.BaseColorTexture,
            IndexCount = mesh.IndexCount,
            WorldMatrix = worldMatrix,
            LitMaterial = uniforms,
            IsLit = true,
            UseShadow = useShadowShader,
        });
    }

    private static void CollectUnlitDraw(Mesh3D mesh, Matrix4x4 worldMatrix)
    {
        // Collect shadow caster for unlit meshes too (they cast shadows even if not lit)
        if (_shadowPassEnabled)
        {
            _shadowCasters.Add(new ShadowCaster
            {
                MeshHandle = mesh.RenderMesh.Handle,
                IndexCount = mesh.IndexCount,
                WorldMatrix = worldMatrix,
            });
        }

        var shader = _currentMaterial != null ? _texturedShader : _unlitShader;
        if (shader == 0) return;

        var cmd = new SceneDrawCommand
        {
            ShaderHandle = shader,
            MeshHandle = mesh.RenderMesh.Handle,
            TextureHandle = _currentMaterial?.BaseColorTexture ?? _defaultWhiteTexture,
            IndexCount = mesh.IndexCount,
            WorldMatrix = worldMatrix,
            IsLit = false,
            UseShadow = false,
        };

        if (_currentMaterial != null)
        {
            cmd.UnlitMaterial = new MaterialUniforms
            {
                BaseColorFactor = _currentMaterial.BaseColorFactor,
                Metallic = _currentMaterial.Metallic,
                Roughness = _currentMaterial.Roughness,
            };
        }

        _sceneDrawCommands.Add(cmd);
    }

    private static void UploadLightUniforms(IGraphicsDriver driver)
    {
        if (_camera == null) return;

        var ambient = _lights.Ambient;
        var dir = _lights.Directional;

        var uniforms = new LightUniforms
        {
            AmbientColor = new Vector4(ambient.EffectiveColor, 0),
            DirectionalDir = new Vector4(dir.Direction, 0),
            DirectionalColor = new Vector4(dir.EffectiveColor, 0),
            CameraPosition = new Vector4(_camera.Position, 0),
            PointLightCount = (uint)_lights.PointLightCount,
        };

        var pointLights = _lights.PointLights;
        for (int i = 0; i < pointLights.Length; i++)
        {
            ref readonly var pl = ref pointLights[i];
            uniforms.SetPointLight(i, new PointLightData
            {
                Position = new Vector4(pl.Position, pl.Range),
                Color = new Vector4(pl.EffectiveColor, 0),
            });
        }

        driver.SetUniform("lights", MemoryMarshal.AsBytes(
            new ReadOnlySpan<LightUniforms>(in uniforms)));
    }

    private static void UploadCascadeShadowUniforms(IGraphicsDriver driver)
    {
        if (_camera == null) return;

        var directional = _lights.Directional;
        int cascadeCount = Math.Clamp(_shadowConfig.CascadeCount, 1, ShadowConfig.MaxCascades);
        float shadowFar = Math.Min(_shadowConfig.ShadowDistance, _camera.FarPlane);

        _cascadeSplits = CascadeSplitComputer.ComputeSplits(
            _camera.NearPlane, shadowFar, cascadeCount, _shadowConfig.Lambda);

        var uniforms = new CascadeShadowUniforms
        {
            CascadeCount = (uint)cascadeCount,
            ShadowBias = _shadowConfig.DepthBias,
            NormalBias = _shadowConfig.NormalBias,
            TexelSize = 1.0f / _shadowConfig.Resolution,
        };

        var splitDepths = new Vector4(
            cascadeCount > 1 ? _cascadeSplits[1] : shadowFar,
            cascadeCount > 2 ? _cascadeSplits[2] : shadowFar,
            cascadeCount > 3 ? _cascadeSplits[3] : shadowFar,
            shadowFar);
        uniforms.SplitDepths = splitDepths;

        for (int i = 0; i < cascadeCount; i++)
        {
            var (lightView, lightProj) = LightSpaceComputer.Compute(
                in directional, _camera, _cascadeSplits[i], _cascadeSplits[i + 1]);
            _cascadeLightViewProjections[i] = lightView * lightProj;
            uniforms.SetLightViewProj(i, _cascadeLightViewProjections[i]);
        }

        driver.SetUniform("shadow", MemoryMarshal.AsBytes(
            new ReadOnlySpan<CascadeShadowUniforms>(in uniforms)));
    }

    private static nuint CreateShaderFromEmbedded(
        string resourceName,
        string shaderName,
        ShaderFlags flags,
        List<ShaderBinding> bindings)
    {
        var wgslSource = LoadEmbeddedResource(resourceName);
        var handle = Graphics.Driver.CreateShader(shaderName, wgslSource, wgslSource, bindings);
        Graphics.Driver.SetShaderFlags(handle, flags);
        return handle;
    }

    private static string LoadEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded shader resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
