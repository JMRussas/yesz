//  YesZ - 3D Model
//
//  Renderable model loaded from glTF: holds GPU-uploaded meshes organized
//  into mesh groups (multi-primitive), materials, textures, and a node
//  hierarchy for transform composition.
//
//  Depends on: YesZ (Mesh3D), YesZ.Rendering (Material3D), NoZ (Graphics)
//  Used by:    Graphics3D.DrawModel, game code

using System.Numerics;
using NoZ;

namespace YesZ.Rendering;

/// <summary>
/// A single primitive within a mesh group: the GPU mesh and its material index.
/// </summary>
public readonly record struct ModelMesh(Mesh3D Mesh, int MaterialIndex);

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
/// </summary>
public class Model3D : IDisposable
{
    public ModelNode Root { get; }
    public MeshGroup[] MeshGroups { get; }
    public Material3D[] Materials { get; }

    private readonly nuint[] _ownedTextureHandles;
    private bool _disposed;

    public Model3D(ModelNode root, MeshGroup[] meshGroups, Material3D[] materials, nuint[] ownedTextureHandles)
    {
        Root = root;
        MeshGroups = meshGroups;
        Materials = materials;
        _ownedTextureHandles = ownedTextureHandles;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var group in MeshGroups)
            foreach (var prim in group.Primitives)
                prim.Mesh.Dispose();

        foreach (var handle in _ownedTextureHandles)
            Graphics.Driver.DestroyTexture(handle);

        GC.SuppressFinalize(this);
    }
}
