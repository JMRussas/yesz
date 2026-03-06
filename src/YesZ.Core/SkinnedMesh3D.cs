//  YesZ - Skinned 3D Mesh
//
//  Immutable skinned mesh container: skinned vertex/index data uploaded to GPU
//  on creation. Holds a RenderMesh handle and a Skeleton3D reference.
//
//  Depends on: YesZ.Core (SkinnedMeshVertex3D, Skeleton3D),
//              NoZ (Graphics, RenderMesh, BufferUsage)
//  Used by:    Graphics3D, GltfLoader (skinned path), game code

using System.Runtime.InteropServices;
using NoZ;
using NoZ.Platform;

namespace YesZ;

public class SkinnedMesh3D : IDisposable
{
    public RenderMesh RenderMesh { get; }
    public int IndexCount { get; }
    public Skeleton3D Skeleton { get; }

    private bool _disposed;

    private SkinnedMesh3D(RenderMesh renderMesh, int indexCount, Skeleton3D skeleton)
    {
        RenderMesh = renderMesh;
        IndexCount = indexCount;
        Skeleton = skeleton;
    }

    /// <summary>
    /// Creates an immutable GPU mesh from skinned vertex and index data.
    /// Must be called after Graphics is initialized.
    /// </summary>
    public static SkinnedMesh3D Create(SkinnedMeshVertex3D[] vertices, ushort[] indices, Skeleton3D skeleton)
    {
        var renderMesh = Graphics.CreateMesh<SkinnedMeshVertex3D>(vertices.Length, indices.Length, BufferUsage.Static, "SkinnedMesh3D");
        Graphics.Driver.UpdateMesh(
            renderMesh.Handle,
            MemoryMarshal.AsBytes<SkinnedMeshVertex3D>(vertices),
            indices
        );
        return new SkinnedMesh3D(renderMesh, indices.Length, skeleton);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (RenderMesh.Handle != nuint.Zero)
            Graphics.Driver.DestroyMesh(RenderMesh.Handle);

        GC.SuppressFinalize(this);
    }
}
