//  YesZ - 3D Graphics
//
//  Static entry point for 3D rendering.
//  Begin() saves the 2D projection, stores the camera, and clears per-frame light state.
//  DrawMesh() computes per-object transforms and issues draw commands through NoZ's
//  batch system. End() restores 2D state for NoZ UI overlay.
//
//  Two rendering paths:
//    Unlit/textured — MVP (model × view × projection) stored in the globals "projection"
//      field. Each DrawMesh creates a unique globals snapshot → unique batch state →
//      separate draw call (max ~64 unique transforms/frame).
//    Lit — VP in globals, model + normal matrix in LitMaterialUniforms (@binding 1),
//      light data in LightUniforms (@binding 4). Per-batch uniform snapshots ensure
//      each draw gets the correct material/light data during deferred execution.
//
//  Matrix convention: SetPassProjection pre-transposes to cancel NoZ's UploadGlobals
//  transpose. SetUniform uploads raw bytes — C# row-major naturally maps to WGSL
//  column-major (row-vector/column-vector flip cancels storage order flip). No
//  transpose needed for matrices passed through SetUniform.
//
//  Depends on: YesZ.Core (Camera3D, Mesh3D, MeshVertex3D, LightEnvironment, DirectionalLight, PointLight, AmbientLight),
//              YesZ.Rendering (Material3D, MaterialUniforms, LitMaterialUniforms, LightUniforms),
//              NoZ (Graphics, Shader, ShaderFlags, ShaderBinding, ShaderBindingType, TextureFormat, TextureFilter)
//  Used by:    Game code, samples

using System.Numerics;
using System.Reflection;
using NoZ;

namespace YesZ.Rendering;

public static class Graphics3D
{
    private static Shader? _unlitShader;
    private static Shader? _texturedShader;
    private static Shader? _litShader;
    private static Shader? _skinnedLitShader;
    private static Camera3D? _camera;
    private static Matrix4x4 _savedProjection;
    private static Matrix4x4 _viewProjection;
    private static bool _initialized;
    private static nuint _defaultWhiteTexture;
    private static Material3D? _currentMaterial;
    private static readonly LightEnvironment _lights = new();
    private static bool _lightsUploadedThisFrame;

    /// <summary>
    /// Initialize the 3D rendering system. Must be called after Graphics is initialized
    /// (e.g., during LoadAssets).
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        var flags = ShaderFlags.Depth | ShaderFlags.DepthLess;

        // Unlit shader (vertex color only, no textures)
        _unlitShader = CreateShaderFromEmbedded(
            "YesZ.Rendering.Shaders.unlit3d.wgsl",
            "unlit3d",
            flags,
            new List<ShaderBinding>
            {
                new() { Binding = 0, Type = ShaderBindingType.UniformBuffer, Name = "globals" },
            });

        // Textured shader (texture × vertex color × material color factor)
        _texturedShader = CreateShaderFromEmbedded(
            "YesZ.Rendering.Shaders.textured3d.wgsl",
            "textured3d",
            flags,
            new List<ShaderBinding>
            {
                new() { Binding = 0, Type = ShaderBindingType.UniformBuffer, Name = "globals" },
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
                new() { Binding = 0, Type = ShaderBindingType.UniformBuffer, Name = "globals" },
                new() { Binding = 1, Type = ShaderBindingType.UniformBuffer, Name = "material" },
                new() { Binding = 2, Type = ShaderBindingType.Texture2D, Name = "base_color_texture" },
                new() { Binding = 3, Type = ShaderBindingType.Sampler, Name = "base_color_sampler" },
                new() { Binding = 4, Type = ShaderBindingType.UniformBuffer, Name = "lights" },
            });

        // Skinned lit shader (skeletal animation + Blinn-Phong)
        _skinnedLitShader = CreateSkinnedShaderFromEmbedded(
            "YesZ.Rendering.Shaders.skinned3d.wgsl",
            "skinned3d",
            flags,
            new List<ShaderBinding>
            {
                new() { Binding = 0, Type = ShaderBindingType.UniformBuffer, Name = "globals" },
                new() { Binding = 1, Type = ShaderBindingType.UniformBuffer, Name = "material" },
                new() { Binding = 2, Type = ShaderBindingType.Texture2D, Name = "base_color_texture" },
                new() { Binding = 3, Type = ShaderBindingType.Sampler, Name = "base_color_sampler" },
                new() { Binding = 4, Type = ShaderBindingType.UniformBuffer, Name = "lights" },
                new() { Binding = 5, Type = ShaderBindingType.UniformBuffer, Name = "joints" },
            });

        // Default 1×1 white texture — untextured materials sample this (identity multiply)
        var white = new byte[] { 255, 255, 255, 255 };
        _defaultWhiteTexture = Graphics.Driver.CreateTexture(1, 1, white, TextureFormat.RGBA8, TextureFilter.Point, "DefaultWhite");

        _initialized = true;
    }

    /// <summary>
    /// Create a new Material3D using the textured (unlit) shader and default white texture.
    /// </summary>
    public static Material3D CreateMaterial()
    {
        if (!_initialized)
            Initialize();

        return new Material3D(_texturedShader!.Handle, _defaultWhiteTexture);
    }

    /// <summary>
    /// Create a new Material3D using the lit shader and default white texture.
    /// Lit materials receive directional + ambient lighting via the light UBO.
    /// </summary>
    public static Material3D CreateLitMaterial()
    {
        if (!_initialized)
            Initialize();

        return new Material3D(_litShader!.Handle, _defaultWhiteTexture);
    }

    /// <summary>
    /// Set the active material for subsequent DrawMesh calls.
    /// Pass null to revert to the unlit (vertex-color-only) shader.
    /// </summary>
    public static void SetMaterial(Material3D? material)
    {
        _currentMaterial = material;
    }

    /// <summary>
    /// Set the directional light for the current frame.
    /// </summary>
    public static void SetDirectionalLight(in DirectionalLight light)
    {
        _lights.Directional = light;
    }

    /// <summary>
    /// Set the ambient light for the current frame.
    /// </summary>
    public static void SetAmbientLight(in AmbientLight light)
    {
        _lights.Ambient = light;
    }

    /// <summary>
    /// Add a point light for the current frame.
    /// Up to <see cref="LightEnvironment.MaxPointLights"/> can be added per frame.
    /// </summary>
    public static void AddPointLight(in PointLight light)
    {
        _lights.AddPointLight(in light);
    }

    /// <summary>
    /// Read-only access to the current light environment (for testing and diagnostics).
    /// </summary>
    public static LightEnvironment Lights => _lights;

    /// <summary>
    /// Begin 3D rendering pass. Saves the current 2D projection, stores
    /// the camera for per-draw MVP computation, and clears per-frame light state.
    /// Light UBO upload is deferred to the first lit draw call.
    /// </summary>
    public static void Begin(Camera3D camera)
    {
        if (!_initialized)
            Initialize();

        _savedProjection = Graphics.GetPassProjection();
        _camera = camera;
        _viewProjection = camera.ViewProjectionMatrix;
        _lights.ClearPointLights();
        _lightsUploadedThisFrame = false;
    }

    /// <summary>
    /// Draw a 3D mesh with the given world transform.
    /// For lit materials: uploads VP to globals, model + normal matrix to material UBO.
    /// For unlit/textured: uploads full MVP to globals, material params to material UBO.
    /// </summary>
    public static void DrawMesh(Mesh3D mesh, Matrix4x4 worldMatrix)
    {
        if (_camera == null) return;

        bool isLit = _currentMaterial != null && _litShader != null
                     && _currentMaterial.ShaderHandle == _litShader.Handle;

        if (isLit)
        {
            DrawMeshLit(mesh, worldMatrix);
        }
        else
        {
            DrawMeshUnlit(mesh, worldMatrix);
        }
    }

    /// <summary>
    /// Draw all meshes in a model with the given root world transform.
    /// Recursively traverses the node hierarchy, composing transforms.
    /// </summary>
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

    /// <summary>
    /// Draw a skinned mesh with the given joint matrices (from JointMatrixComputer).
    /// Joint matrices replace the model matrix — the skin matrix handles world placement.
    /// The current material is used for lighting and texturing.
    /// </summary>
    public static void DrawSkinnedMesh(SkinnedMesh3D mesh, ReadOnlySpan<Matrix4x4> jointMatrices)
    {
        if (_camera == null || _skinnedLitShader == null) return;

        // Upload light UBO if not done yet
        if (!_lightsUploadedThisFrame)
        {
            UploadLightUniforms();
            _lightsUploadedThisFrame = true;
        }

        // VP in globals (same as lit path)
        Graphics.SetPassProjection(Matrix4x4.Transpose(_viewProjection));

        // Material UBO — model/normal matrix set to identity since joint matrices
        // absorb the full world transform (glTF spec: skinned mesh node transform is ignored)
        var uniforms = new LitMaterialUniforms
        {
            Model = Matrix4x4.Identity,
            NormalMatrix = Matrix4x4.Identity,
            BaseColorFactor = _currentMaterial?.BaseColorFactor ?? new Vector4(1, 1, 1, 1),
            Metallic = _currentMaterial?.Metallic ?? 0,
            Roughness = _currentMaterial?.Roughness ?? 0.5f,
        };
        Graphics.SetUniform("material", in uniforms);

        // Joint matrix UBO
        var jointUniforms = new JointMatrixUniforms();
        int count = Math.Min(jointMatrices.Length, JointMatrixUniforms.MaxJoints);
        for (int i = 0; i < count; i++)
            jointUniforms.Set(i, in jointMatrices[i]);
        Graphics.SetUniform("joints", in jointUniforms);

        // Shader, texture, mesh, draw
        Graphics.SetShader(_skinnedLitShader);
        var texture = _currentMaterial?.BaseColorTexture ?? _defaultWhiteTexture;
        Graphics.SetTexture(texture, 0, TextureFilter.Linear);
        Graphics.SetMesh(mesh.RenderMesh);
        Graphics.DrawElements(mesh.IndexCount, 0);
    }

    /// <summary>
    /// Draw all meshes in a model with skeletal animation.
    /// Computes joint matrices from the animation player state and renders skinned meshes.
    /// Non-skinned primitives are rendered with the given world transform.
    /// </summary>
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
                    // Skinned: joint matrices handle world placement
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

    /// <summary>
    /// End 3D rendering pass. Restores the 2D orthographic projection
    /// for NoZ UI overlay rendering.
    /// </summary>
    public static void End()
    {
        // Restore 2D projection so subsequent UI draws use the correct matrix
        Graphics.SetPassProjection(_savedProjection);
        _camera = null;
        _currentMaterial = null;
    }

    /// <summary>
    /// Compute the normal matrix for a given model matrix.
    /// Returns the transpose of the inverse of the upper 3×3, stored as a full mat4x4
    /// for GPU alignment. Falls back to the model matrix if inversion fails.
    /// </summary>
    internal static Matrix4x4 ComputeNormalMatrix(in Matrix4x4 model)
    {
        if (Matrix4x4.Invert(model, out var inverted))
        {
            // Transpose of the inverse — this is the standard normal matrix
            return Matrix4x4.Transpose(inverted);
        }

        // Singular matrix fallback: use model as-is (correct for rotation + uniform scale)
        return model;
    }

    private static void DrawMeshLit(Mesh3D mesh, Matrix4x4 worldMatrix)
    {
        // Upload light UBO once per frame, on first lit draw.
        // Deferred from Begin() so user can set lights between Begin() and DrawMesh().
        if (!_lightsUploadedThisFrame)
        {
            UploadLightUniforms();
            _lightsUploadedThisFrame = true;
        }

        // Lit path: globals get VP (view × projection), material gets model + normal matrix.
        // Pre-transpose VP to cancel NoZ's UploadGlobals transpose.
        Graphics.SetPassProjection(Matrix4x4.Transpose(_viewProjection));

        // SetUniform uploads raw bytes (no auto-transpose like UploadGlobals).
        // C# row-major bytes naturally map to WGSL column-major layout because the
        // row-vector/column-vector convention flip cancels the storage order flip.
        var normalMatrix = ComputeNormalMatrix(in worldMatrix);

        var uniforms = new LitMaterialUniforms
        {
            Model = worldMatrix,
            NormalMatrix = normalMatrix,
            BaseColorFactor = _currentMaterial!.BaseColorFactor,
            Metallic = _currentMaterial.Metallic,
            Roughness = _currentMaterial.Roughness,
        };
        Graphics.SetUniform("material", in uniforms);

        Graphics.SetShader(_litShader!);
        Graphics.SetTexture(_currentMaterial.BaseColorTexture, 0, TextureFilter.Linear);
        Graphics.SetMesh(mesh.RenderMesh);
        Graphics.DrawElements(mesh.IndexCount, 0);
    }

    private static void DrawMeshUnlit(Mesh3D mesh, Matrix4x4 worldMatrix)
    {
        // Unlit/textured path: globals get full MVP (model × view × projection).
        var mvp = worldMatrix * _viewProjection;
        Graphics.SetPassProjection(Matrix4x4.Transpose(mvp));

        var shader = _currentMaterial != null ? _texturedShader : _unlitShader;
        if (shader == null) return;

        Graphics.SetShader(shader);

        if (_currentMaterial != null)
        {
            var uniforms = new MaterialUniforms
            {
                BaseColorFactor = _currentMaterial.BaseColorFactor,
                Metallic = _currentMaterial.Metallic,
                Roughness = _currentMaterial.Roughness,
            };
            Graphics.SetUniform("material", in uniforms);
            Graphics.SetTexture(_currentMaterial.BaseColorTexture, 0, TextureFilter.Linear);
        }

        Graphics.SetMesh(mesh.RenderMesh);
        Graphics.DrawElements(mesh.IndexCount, 0);
    }

    private static void UploadLightUniforms()
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

        Graphics.SetUniform("lights", in uniforms);
    }

    private static Shader CreateShaderFromEmbedded(
        string resourceName,
        string shaderName,
        ShaderFlags flags,
        List<ShaderBinding> bindings)
    {
        var wgslSource = LoadEmbeddedResource(resourceName);
        var handle = Graphics.Driver.CreateShader(shaderName, wgslSource, wgslSource, bindings);

        try
        {
            return Shader.CreateRaw(shaderName, handle, flags, bindings, MeshVertex3D.VertexHash);
        }
        catch
        {
            Graphics.Driver.DestroyShader(handle);
            throw;
        }
    }

    private static Shader CreateSkinnedShaderFromEmbedded(
        string resourceName,
        string shaderName,
        ShaderFlags flags,
        List<ShaderBinding> bindings)
    {
        var wgslSource = LoadEmbeddedResource(resourceName);
        var handle = Graphics.Driver.CreateShader(shaderName, wgslSource, wgslSource, bindings);

        try
        {
            return Shader.CreateRaw(shaderName, handle, flags, bindings, SkinnedMeshVertex3D.VertexHash);
        }
        catch
        {
            Graphics.Driver.DestroyShader(handle);
            throw;
        }
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
