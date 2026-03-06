//  YesZ - Skinned 3D Mesh Vertex
//
//  68-byte vertex format for skinned 3D meshes: extends MeshVertex3D layout
//  with 4-byte joint indices (UByte4) and 16-byte joint weights (Vector4).
//  Implements NoZ's IVertex for pipeline cache compatibility.
//
//  Depends on: NoZ (IVertex, VertexFormatDescriptor, VertexAttribute, VertexFormatHash,
//              VertexAttribType, Color)
//  Used by:    SkinnedMesh3D, MeshExtractor (skinned path), skinned 3D shaders

using System.Numerics;
using System.Runtime.InteropServices;
using NoZ;
using NoZ.Platform;

namespace YesZ;

/// <summary>
/// 4-byte packed joint indices for skinned vertices.
/// Maps to Uint8x4 (vec4u) in WGSL. Max 256 joints per skeleton.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct JointIndices4
{
    public byte Joint0;
    public byte Joint1;
    public byte Joint2;
    public byte Joint3;
}

[StructLayout(LayoutKind.Sequential)]
public struct SkinnedMeshVertex3D : IVertex
{
    public Vector3 Position;        // location 0 — 3× float32, offset 0
    public Vector3 Normal;          // location 1 — 3× float32, offset 12
    public Vector2 UV;              // location 2 — 2× float32, offset 24
    public Color   Color;           // location 3 — 4× float32, offset 32
    public JointIndices4 Joints;    // location 4 — 4× uint8,   offset 48
    public Vector4 JointWeights;    // location 5 — 4× float32, offset 52

    public static readonly int SizeInBytes = Marshal.SizeOf<SkinnedMeshVertex3D>();
    public static readonly uint VertexHash = VertexFormatHash.Compute(GetFormatDescriptor().Attributes);

    public static VertexFormatDescriptor GetFormatDescriptor() => new()
    {
        Stride = SizeInBytes,
        Attributes =
        [
            new VertexAttribute(0, 3, VertexAttribType.Float, (int)Marshal.OffsetOf<SkinnedMeshVertex3D>(nameof(Position))),
            new VertexAttribute(1, 3, VertexAttribType.Float, (int)Marshal.OffsetOf<SkinnedMeshVertex3D>(nameof(Normal))),
            new VertexAttribute(2, 2, VertexAttribType.Float, (int)Marshal.OffsetOf<SkinnedMeshVertex3D>(nameof(UV))),
            new VertexAttribute(3, 4, VertexAttribType.Float, (int)Marshal.OffsetOf<SkinnedMeshVertex3D>(nameof(Color))),
            new VertexAttribute(4, 4, VertexAttribType.UByte, (int)Marshal.OffsetOf<SkinnedMeshVertex3D>(nameof(Joints))),
            new VertexAttribute(5, 4, VertexAttribType.Float, (int)Marshal.OffsetOf<SkinnedMeshVertex3D>(nameof(JointWeights))),
        ]
    };
}
