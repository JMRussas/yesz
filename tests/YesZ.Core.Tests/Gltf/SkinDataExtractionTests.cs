//  YesZ - Skin Data Extraction Tests
//
//  Tests for skinned mesh extraction from glTF: JOINTS_0/WEIGHTS_0 parsing,
//  weight normalization, and joint index range validation.
//
//  Depends on: YesZ.Gltf (GlbReader, GltfDocument, AccessorReader, MeshExtractor),
//              YesZ (SkinnedMeshVertex3D, JointIndices4), System.Numerics
//  Used by:    test runner

using System.Numerics;
using Xunit;
using YesZ.Gltf;

namespace YesZ.Tests.Gltf;

public class SkinDataExtractionTests
{
    private const float Epsilon = 1e-4f;

    private static (ExtractedSkinnedMesh mesh, GltfDocument doc) ExtractRiggedSimpleSkinned()
    {
        var glbData = TestHelper.LoadEmbeddedGlb("RiggedSimple.glb");
        var glb = GlbReader.Parse(glbData);
        var doc = GltfDocument.Deserialize(glb.Json);
        var reader = new AccessorReader(doc, glb.BinChunk);

        // RiggedSimple has one mesh with one primitive
        var primitive = doc.Meshes![0].Primitives[0];
        var mesh = MeshExtractor.ExtractSkinnedPrimitive(primitive, reader, doc);
        return (mesh, doc);
    }

    [Fact]
    public void Extract_RiggedSimple_HasVertices()
    {
        var (mesh, _) = ExtractRiggedSimpleSkinned();
        Assert.True(mesh.Vertices.Length > 0, "Skinned mesh should have vertices.");
    }

    [Fact]
    public void Extract_RiggedSimple_JointIndicesInRange()
    {
        var (mesh, doc) = ExtractRiggedSimpleSkinned();
        // RiggedSimple has 2 joints
        int jointCount = doc.Skins![0].Joints!.Length;

        foreach (var v in mesh.Vertices)
        {
            Assert.True(v.Joints.Joint0 < jointCount,
                $"Joint0 index {v.Joints.Joint0} >= jointCount {jointCount}");
            Assert.True(v.Joints.Joint1 < jointCount,
                $"Joint1 index {v.Joints.Joint1} >= jointCount {jointCount}");
            Assert.True(v.Joints.Joint2 < jointCount,
                $"Joint2 index {v.Joints.Joint2} >= jointCount {jointCount}");
            Assert.True(v.Joints.Joint3 < jointCount,
                $"Joint3 index {v.Joints.Joint3} >= jointCount {jointCount}");
        }
    }

    [Fact]
    public void Extract_RiggedSimple_WeightsSumToOne()
    {
        var (mesh, _) = ExtractRiggedSimpleSkinned();

        for (int i = 0; i < mesh.Vertices.Length; i++)
        {
            var w = mesh.Vertices[i].JointWeights;
            float sum = w.X + w.Y + w.Z + w.W;
            Assert.InRange(sum, 1.0f - Epsilon, 1.0f + Epsilon);
        }
    }

    [Fact]
    public void NormalizeWeights_UnnormalizedWeights_AreNormalized()
    {
        var weights = new Vector4[]
        {
            new(2, 2, 0, 0),     // Sum = 4
            new(0.5f, 0, 0, 0),  // Sum = 0.5
            new(1, 0, 0, 0),     // Sum = 1 — should not change
        };

        MeshExtractor.NormalizeWeights(weights);

        Assert.InRange(weights[0].X + weights[0].Y + weights[0].Z + weights[0].W,
            1.0f - Epsilon, 1.0f + Epsilon);
        Assert.InRange(weights[0].X, 0.5f - Epsilon, 0.5f + Epsilon);

        Assert.InRange(weights[1].X + weights[1].Y + weights[1].Z + weights[1].W,
            1.0f - Epsilon, 1.0f + Epsilon);
        Assert.InRange(weights[1].X, 1.0f - Epsilon, 1.0f + Epsilon);

        Assert.InRange(weights[2].X, 1.0f - Epsilon, 1.0f + Epsilon);
    }

    [Fact]
    public void NormalizeWeights_ZeroWeights_DefaultToJoint0()
    {
        var weights = new Vector4[] { Vector4.Zero };

        MeshExtractor.NormalizeWeights(weights);

        Assert.Equal(1.0f, weights[0].X);
        Assert.Equal(0.0f, weights[0].Y);
        Assert.Equal(0.0f, weights[0].Z);
        Assert.Equal(0.0f, weights[0].W);
    }
}
