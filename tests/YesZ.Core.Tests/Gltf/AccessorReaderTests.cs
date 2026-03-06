//  YesZ - AccessorReader Tests
//
//  Verifies accessor resolution: bufferView → BIN chunk → typed data.
//  Tests both tightly packed and strided buffer views.
//
//  Depends on: YesZ.Gltf (GlbReader, GltfDocument, AccessorReader),
//              System.Numerics, Xunit
//  Used by:    CI

using System.Numerics;
using YesZ.Gltf;
using Xunit;

namespace YesZ.Tests.Gltf;

public class AccessorReaderTests
{
    private static (GltfDocument Doc, AccessorReader Reader) LoadBox()
    {
        var data = TestHelper.LoadEmbeddedGlb("Box.glb");
        var glb = GlbReader.Parse(data);
        var doc = GltfDocument.Deserialize(glb.Json);
        return (doc, new AccessorReader(doc, glb.BinChunk));
    }

    private static (GltfDocument Doc, AccessorReader Reader) LoadBoxTextured()
    {
        var data = TestHelper.LoadEmbeddedGlb("BoxTextured.glb");
        var glb = GlbReader.Parse(data);
        var doc = GltfDocument.Deserialize(glb.Json);
        return (doc, new AccessorReader(doc, glb.BinChunk));
    }

    [Fact]
    public void Read_Indices_Returns36()
    {
        // Box.glb accessor 0: 36 ushort indices
        var (_, reader) = LoadBox();
        var indices = reader.ReadIndices(0);

        Assert.Equal(36, indices.Length);
    }

    [Fact]
    public void Read_Normals_Returns24()
    {
        // Box.glb accessor 1: 24 VEC3 normals
        var (_, reader) = LoadBox();
        var normals = reader.Read<Vector3>(1);

        Assert.Equal(24, normals.Length);
    }

    [Fact]
    public void Read_Positions_Returns24()
    {
        // Box.glb accessor 2: 24 VEC3 positions
        var (_, reader) = LoadBox();
        var positions = reader.Read<Vector3>(2);

        Assert.Equal(24, positions.Length);
    }

    [Fact]
    public void Read_Positions_ValuesInRange()
    {
        // Box.glb positions are in [-0.5, 0.5]
        var (_, reader) = LoadBox();
        var positions = reader.Read<Vector3>(2);

        foreach (var p in positions)
        {
            Assert.InRange(p.X, -0.5f, 0.5f);
            Assert.InRange(p.Y, -0.5f, 0.5f);
            Assert.InRange(p.Z, -0.5f, 0.5f);
        }
    }

    [Fact]
    public void Read_Normals_AreUnitLength()
    {
        var (_, reader) = LoadBox();
        var normals = reader.Read<Vector3>(1);

        foreach (var n in normals)
        {
            Assert.InRange(n.Length(), 0.99f, 1.01f);
        }
    }

    [Fact]
    public void Read_Indices_MaxIs23()
    {
        // Box.glb has 24 vertices (0..23)
        var (_, reader) = LoadBox();
        var indices = reader.ReadIndices(0);

        Assert.Equal(23, indices.Max());
    }

    [Fact]
    public void ReadBoxTextured_UVs_Returns24()
    {
        // BoxTextured.glb accessor 3: 24 VEC2 UVs
        var (_, reader) = LoadBoxTextured();
        var uvs = reader.Read<Vector2>(3);

        Assert.Equal(24, uvs.Length);
    }

    [Fact]
    public void Read_OutOfRange_Throws()
    {
        var (_, reader) = LoadBox();

        Assert.Throws<InvalidOperationException>(() => reader.Read<Vector3>(99));
    }
}
