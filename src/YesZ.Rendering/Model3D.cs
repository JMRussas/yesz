//  YesZ - 3D Model
//
//  Renderable model loaded from glTF: holds GPU-uploaded meshes organized
//  into mesh groups (multi-primitive), materials, textures, and a node
//  hierarchy for transform composition.
//  Optionally holds skeleton data and animation clips for skinned models.
//
//  Depends on: YesZ (Mesh3D, SkinnedMesh3D, Skeleton3D, AnimationClip3D),
//              YesZ.Rendering (Material3D), NoZ (Graphics)
//  Used by:    Graphics3D.DrawModel, Graphics3D.DrawAnimatedModel, game code

using System.Numerics;
using NoZ;

namespace YesZ.Rendering;

/// <summary>
/// A single primitive within a mesh group: either a standard or skinned GPU mesh
/// and its material index.
/// </summary>
public class ModelMesh
{
    public Mesh3D? Mesh { get; }
    public SkinnedMesh3D? SkinnedMesh { get; }
    public int MaterialIndex { get; }

    public ModelMesh(Mesh3D mesh, int materialIndex)
    {
        Mesh = mesh;
        MaterialIndex = materialIndex;
    }

    public ModelMesh(SkinnedMesh3D skinnedMesh, int materialIndex)
    {
        SkinnedMesh = skinnedMesh;
        MaterialIndex = materialIndex;
    }
}

/// <summary>
/// A group of primitives that share a glTF mesh index.
/// Each primitive has its own vertex/index data and material.
/// </summary>
public class MeshGroup
{
    public ModelMesh[] Primitives { get; }

    public MeshGroup(ModelMesh[] primitives)
    {
        Primitives = primitives;
    }
}

/// <summary>
/// A node in the model's transform hierarchy.
/// Each node has a local transform, an optional mesh group reference, and children.
/// </summary>
public class ModelNode
{
    public Matrix4x4 LocalTransform { get; }
    public int MeshGroupIndex { get; }
    public ModelNode[] Children { get; }

    public ModelNode(Matrix4x4 localTransform, int meshGroupIndex, ModelNode[] children)
    {
        LocalTransform = localTransform;
        MeshGroupIndex = meshGroupIndex;
        Children = children;
    }
}

/// <summary>
/// Renderable 3D model container. Owns GPU meshes, materials, texture handles,
/// and a node hierarchy loaded from glTF.
/// Optionally contains skeleton and animation data for skinned models.
/// </summary>
public class Model3D : IDisposable
{
    public ModelNode Root { get; }
    public MeshGroup[] MeshGroups { get; }
    public Material3D[] Materials { get; }

    /// <summary>Skeleton for skinned models. Null for static models.</summary>
    public Skeleton3D? Skeleton { get; }

    /// <summary>Animation clips for skinned models. Null or empty for static models.</summary>
    public AnimationClip3D[]? Animations { get; }

    /// <summary>Per-joint local transforms in bind pose (from glTF node TRS). Null for static models.</summary>
    public Matrix4x4[]? BindPose { get; }

    private readonly nuint[] _ownedTextureHandles;
    private bool _disposed;

    public Model3D(ModelNode root, MeshGroup[] meshGroups, Material3D[] materials, nuint[] ownedTextureHandles,
        Skeleton3D? skeleton = null, AnimationClip3D[]? animations = null, Matrix4x4[]? bindPose = null)
    {
        Root = root;
        MeshGroups = meshGroups;
        Materials = materials;
        _ownedTextureHandles = ownedTextureHandles;
        Skeleton = skeleton;
        Animations = animations;
        BindPose = bindPose;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var group in MeshGroups)
            foreach (var prim in group.Primitives)
            {
                prim.Mesh?.Dispose();
                prim.SkinnedMesh?.Dispose();
            }

        foreach (var handle in _ownedTextureHandles)
            Graphics.Driver.DestroyTexture(handle);

        GC.SuppressFinalize(this);
    }
}
