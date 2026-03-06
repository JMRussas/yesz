//  YesZ - 3D Model
//
//  Renderable model loaded from glTF: holds GPU-uploaded meshes and
//  materials with per-mesh material assignment. Created by GltfLoader.
//
//  Depends on: YesZ (Mesh3D), YesZ.Rendering (Material3D)
//  Used by:    Graphics3D.DrawModel, game code

namespace YesZ.Rendering;

/// <summary>
/// A single mesh entry within a model: the GPU mesh and its material index.
/// </summary>
public readonly record struct ModelMesh(Mesh3D Mesh, int MaterialIndex);

/// <summary>
/// Renderable 3D model container. Owns GPU meshes and materials loaded from glTF.
/// </summary>
public class Model3D : IDisposable
{
    public ModelMesh[] Meshes { get; }
    public Material3D[] Materials { get; }

    private bool _disposed;

    public Model3D(ModelMesh[] meshes, Material3D[] materials)
    {
        Meshes = meshes;
        Materials = materials;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var entry in Meshes)
            entry.Mesh.Dispose();

        GC.SuppressFinalize(this);
    }
}
