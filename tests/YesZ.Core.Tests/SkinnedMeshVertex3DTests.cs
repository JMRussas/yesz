//  YesZ - SkinnedMeshVertex3D Tests
//
//  Verifies skinned vertex struct layout, attribute descriptors, and hash uniqueness.
//
//  Depends on: YesZ.Core (SkinnedMeshVertex3D, MeshVertex3D),
//              NoZ (VertexAttribType)
//  Used by:    test runner

using NoZ;
using Xunit;

namespace YesZ.Tests;

public class SkinnedMeshVertex3DTests
{
    [Fact]
    public void GetFormatDescriptor_HasCorrectStride()
    {
        var desc = SkinnedMeshVertex3D.GetFormatDescriptor();
        Assert.Equal(SkinnedMeshVertex3D.SizeInBytes, desc.Stride);
        // 48 (base) + 4 (joints) + 16 (weights) = 68 bytes
        Assert.Equal(68, desc.Stride);
    }

    [Fact]
    public void GetFormatDescriptor_Has6Attributes()
    {
        var desc = SkinnedMeshVertex3D.GetFormatDescriptor();
        Assert.Equal(6, desc.Attributes.Length);
    }

    [Fact]
    public void GetFormatDescriptor_JointsAreUByte()
    {
        var desc = SkinnedMeshVertex3D.GetFormatDescriptor();
        // Attribute 4 = Joints
        Assert.Equal(VertexAttribType.UByte, desc.Attributes[4].Type);
        Assert.Equal(4, desc.Attributes[4].Components);
    }

    [Fact]
    public void GetFormatDescriptor_WeightsAreFloat()
    {
        var desc = SkinnedMeshVertex3D.GetFormatDescriptor();
        // Attribute 5 = Weights
        Assert.Equal(VertexAttribType.Float, desc.Attributes[5].Type);
        Assert.Equal(4, desc.Attributes[5].Components);
    }

    [Fact]
    public void GetFormatDescriptor_OffsetsMatchMarshal()
    {
        var desc = SkinnedMeshVertex3D.GetFormatDescriptor();
        Assert.Equal(0, desc.Attributes[0].Offset);   // Position
        Assert.Equal(12, desc.Attributes[1].Offset);  // Normal
        Assert.Equal(24, desc.Attributes[2].Offset);  // UV
        Assert.Equal(32, desc.Attributes[3].Offset);  // Color
        Assert.Equal(48, desc.Attributes[4].Offset);  // Joints
        Assert.Equal(52, desc.Attributes[5].Offset);  // Weights
    }

    [Fact]
    public void GetFormatDescriptor_LocationsAre0Through5()
    {
        var desc = SkinnedMeshVertex3D.GetFormatDescriptor();
        for (int i = 0; i < desc.Attributes.Length; i++)
        {
            Assert.Equal(i, desc.Attributes[i].Location);
        }
    }

    [Fact]
    public void VertexHash_DiffersFromMeshVertex3D()
    {
        Assert.NotEqual(MeshVertex3D.VertexHash, SkinnedMeshVertex3D.VertexHash);
    }
}
