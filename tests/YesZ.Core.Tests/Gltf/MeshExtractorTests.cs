//  YesZ - MeshExtractor Tests
//
//  Verifies mesh extraction from glTF primitives: vertex/index counts,
//  normal unit length, UV ranges, and fallback behavior for missing attributes.
//
//  Depends on: YesZ.Gltf (GlbReader, GltfDocument, AccessorReader, MeshExtractor),
//              System.Numerics, Xunit
//  Used by:    CI

using System.Numerics;
using YesZ.Gltf;
using Xunit;

namespace YesZ.Tests.Gltf;

public class MeshExtractorTests
{
    private static ExtractedMesh ExtractBoxPrimitive()
    {
        var data = TestHelper.LoadEmbeddedGlb("Box.glb");
        var glb = GlbReader.Parse(data);
        var doc = GltfDocument.Deserialize(glb.Json);
        var reader = new AccessorReader(doc, glb.BinChunk);
        var primitive = doc.Meshes![0].Primitives![0];
        return MeshExtractor.ExtractPrimitive(primitive, reader);
    }

    private static ExtractedMesh ExtractBoxTexturedPrimitive()
    {
        var data = TestHelper.LoadEmbeddedGlb("BoxTextured.glb");
        var glb = GlbReader.Parse(data);
        var doc = GltfDocument.Deserialize(glb.Json);
        var reader = new AccessorReader(doc, glb.BinChunk);
        var primitive = doc.Meshes![0].Primitives![0];
        return MeshExtractor.ExtractPrimitive(primitive, reader);
    }

    [Fact]
    public void ExtractPrimitive_Box_Returns24Vertices()
    {
        var mesh = ExtractBoxPrimitive();
        Assert.Equal(24, mesh.Vertices.Length); // 4 per face × 6 faces
    }

    [Fact]
    public void ExtractPrimitive_Box_Returns36Indices()
    {
        var mesh = ExtractBoxPrimitive();
        Assert.Equal(36, mesh.Indices.Length); // 6 per face × 6 faces
    }

    [Fact]
    public void ExtractPrimitive_Box_NormalsAreUnitLength()
    {
        var mesh = ExtractBoxPrimitive();

        foreach (var v in mesh.Vertices)
        {
            var len = v.Normal.Length();
            Assert.InRange(len, 0.99f, 1.01f);
        }
    }

    [Fact]
    public void ExtractPrimitive_Box_PositionsInRange()
    {
        var mesh = ExtractBoxPrimitive();

        foreach (var v in mesh.Vertices)
        {
            Assert.InRange(v.Position.X, -0.5f, 0.5f);
            Assert.InRange(v.Position.Y, -0.5f, 0.5f);
            Assert.InRange(v.Position.Z, -0.5f, 0.5f);
        }
    }

    [Fact]
    public void ExtractPrimitive_Box_MissingUVs_DefaultsToZero()
    {
        // Box.glb has no TEXCOORD_0
        var mesh = ExtractBoxPrimitive();

        foreach (var v in mesh.Vertices)
        {
            Assert.Equal(Vector2.Zero, v.UV);
        }
    }

    [Fact]
    public void ExtractPrimitive_Box_VertexColorsAreWhite()
    {
        var mesh = ExtractBoxPrimitive();

        foreach (var v in mesh.Vertices)
        {
            Assert.Equal(1f, v.Color.R);
            Assert.Equal(1f, v.Color.G);
            Assert.Equal(1f, v.Color.B);
            Assert.Equal(1f, v.Color.A);
        }
    }

    [Fact]
    public void ExtractPrimitive_BoxTextured_HasUVs()
    {
        var mesh = ExtractBoxTexturedPrimitive();

        // BoxTextured.glb has TEXCOORD_0 — at least some UVs should be non-zero
        bool hasNonZeroUV = false;
        foreach (var v in mesh.Vertices)
        {
            if (v.UV != Vector2.Zero)
            {
                hasNonZeroUV = true;
                break;
            }
        }
        Assert.True(hasNonZeroUV, "BoxTextured should have non-zero UVs.");
    }

    [Fact]
    public void ExtractPrimitive_BoxTextured_Returns24Vertices()
    {
        var mesh = ExtractBoxTexturedPrimitive();
        Assert.Equal(24, mesh.Vertices.Length);
    }

    [Fact]
    public void GenerateFlatNormals_ProducesUnitNormals()
    {
        // Simple triangle
        var positions = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0),
        };
        ushort[] indices = [0, 1, 2];

        var normals = MeshExtractor.GenerateFlatNormals(positions, indices);

        Assert.Equal(3, normals.Length);
        foreach (var n in normals)
        {
            Assert.InRange(n.Length(), 0.99f, 1.01f);
        }
        // Normal should point in +Z for this winding
        Assert.InRange(normals[0].Z, 0.99f, 1.01f);
    }
}
