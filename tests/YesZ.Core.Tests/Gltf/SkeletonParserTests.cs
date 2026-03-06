//  YesZ - SkeletonParser Tests
//
//  Tests for glTF skin → Skeleton3D parsing using RiggedSimple.glb.
//
//  Depends on: YesZ.Gltf (GlbReader, GltfDocument, AccessorReader, SkeletonParser),
//              YesZ (Skeleton3D), System.Numerics
//  Used by:    test runner

using System.Numerics;
using Xunit;
using YesZ.Gltf;

namespace YesZ.Tests.Gltf;

public class SkeletonParserTests
{
    private static Skeleton3D ParseRiggedSimple()
    {
        var glbData = TestHelper.LoadEmbeddedGlb("RiggedSimple.glb");
        var glb = GlbReader.Parse(glbData);
        var doc = GltfDocument.Deserialize(glb.Json);
        var reader = new AccessorReader(doc, glb.BinChunk);
        return SkeletonParser.Parse(doc.Skins![0], doc, reader);
    }

    [Fact]
    public void Parse_RiggedSimple_CorrectJointCount()
    {
        var skeleton = ParseRiggedSimple();
        Assert.Equal(2, skeleton.JointCount);
    }

    [Fact]
    public void Parse_RiggedSimple_ParentIndicesFormTree()
    {
        var skeleton = ParseRiggedSimple();

        // Joint 0 (Bone) is root → parent = -1
        Assert.Equal(-1, skeleton.ParentIndices[0]);

        // Joint 1 (Bone.001) is child of joint 0
        Assert.Equal(0, skeleton.ParentIndices[1]);
    }

    [Fact]
    public void Parse_RiggedSimple_JointNodeIndices()
    {
        var skeleton = ParseRiggedSimple();

        // From the glTF: joints = [3, 4] (node indices)
        Assert.Equal(3, skeleton.JointNodeIndices[0]);
        Assert.Equal(4, skeleton.JointNodeIndices[1]);
    }

    [Fact]
    public void Parse_RiggedSimple_IBMsAreInvertible()
    {
        var skeleton = ParseRiggedSimple();

        for (int j = 0; j < skeleton.JointCount; j++)
        {
            bool inverted = Matrix4x4.Invert(skeleton.InverseBindMatrices[j], out _);
            Assert.True(inverted, $"IBM for joint {j} is not invertible.");
        }
    }

    [Fact]
    public void Parse_NoIBM_DefaultsToIdentity()
    {
        // Create a minimal skin with no IBM accessor
        var skin = new GltfSkin { Joints = [0, 1] };
        var doc = new GltfDocument
        {
            Nodes =
            [
                new GltfNode { Name = "Root", Children = [1] },
                new GltfNode { Name = "Child" },
            ],
        };
        var reader = new AccessorReader(doc, []);

        var skeleton = SkeletonParser.Parse(skin, doc, reader);

        Assert.Equal(2, skeleton.JointCount);
        Assert.Equal(Matrix4x4.Identity, skeleton.InverseBindMatrices[0]);
        Assert.Equal(Matrix4x4.Identity, skeleton.InverseBindMatrices[1]);
    }
}
