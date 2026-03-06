//  YesZ - 3D Model
//
//  Renderable model loaded from glTF: holds GPU-uploaded meshes and
//  materials with per-mesh material assignment. Created by GltfLoader.
//
//  Depends on: YesZ (Mesh3D), YesZ.Rendering (Material3D), NoZ (Graphics)
//  Used by:    Graphics3D.DrawModel, game code

using NoZ;

namespace YesZ.Rendering;

/// <summary>
/// A single mesh entry within a model: the GPU mesh and its material index.
/// </summary>
public readonly record struct ModelMesh(Mesh3D Mesh, int MaterialIndex);

/// <summary>
/// Renderable 3D model container. Owns GPU meshes, materials, and texture handles loaded from glTF.
/// </summary>
public class Model3D : IDisposable
{
    public ModelMesh[] Meshes { get; }
    public Material3D[] Materials { get; }

    private readonly nuint[] _ownedTextureHandles;
    private bool _disposed;

    public Model3D(ModelMesh[] meshes, Material3D[] materials, nuint[] ownedTextureHandles)
    {
        Meshes = meshes;
        Materials = materials;
        _ownedTextureHandles = ownedTextureHandles;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var entry in Meshes)
            entry.Mesh.Dispose();

        foreach (var handle in _ownedTextureHandles)
            Graphics.Driver.DestroyTexture(handle);

        GC.SuppressFinalize(this);
    }
}
