//  YesZ - AnimationParser Tests
//
//  Tests for glTF animation → AnimationClip3D parsing using RiggedSimple.glb.
//
//  Depends on: YesZ.Gltf (GlbReader, GltfDocument, AccessorReader, SkeletonParser, AnimationParser),
//              YesZ (AnimationClip3D, AnimationChannel3D, Skeleton3D)
//  Used by:    test runner

using Xunit;
using YesZ.Gltf;

namespace YesZ.Tests.Gltf;

public class AnimationParserTests
{
    private static (Skeleton3D skeleton, AnimationClip3D[] clips) ParseRiggedSimple()
    {
        var glbData = TestHelper.LoadEmbeddedGlb("RiggedSimple.glb");
        var glb = GlbReader.Parse(glbData);
        var doc = GltfDocument.Deserialize(glb.Json);
        var reader = new AccessorReader(doc, glb.BinChunk);
        var skeleton = SkeletonParser.Parse(doc.Skins![0], doc, reader);
        var clips = AnimationParser.ParseAll(doc, reader, skeleton);
        return (skeleton, clips);
    }

    [Fact]
    public void Parse_RiggedSimple_HasAnimation()
    {
        var (_, clips) = ParseRiggedSimple();
        Assert.Single(clips);
    }

    [Fact]
    public void Parse_RiggedSimple_HasThreeChannels()
    {
        var (_, clips) = ParseRiggedSimple();
        // RiggedSimple has translation, rotation, scale channels for joint 1
        Assert.Equal(3, clips[0].Channels.Length);
    }

    [Fact]
    public void Parse_RiggedSimple_KeyframeTimestampsAscending()
    {
        var (_, clips) = ParseRiggedSimple();

        foreach (var channel in clips[0].Channels)
        {
            for (int i = 1; i < channel.Times.Length; i++)
            {
                Assert.True(channel.Times[i] >= channel.Times[i - 1],
                    $"Timestamps not ascending at index {i}: {channel.Times[i - 1]} > {channel.Times[i]}");
            }
        }
    }

    [Fact]
    public void Parse_RiggedSimple_ChannelTargetsValidJoints()
    {
        var (skeleton, clips) = ParseRiggedSimple();

        foreach (var channel in clips[0].Channels)
        {
            Assert.InRange(channel.JointIndex, 0, skeleton.JointCount - 1);
        }
    }

    [Fact]
    public void Parse_RotationChannel_HasQuaternionKeyframes()
    {
        var (_, clips) = ParseRiggedSimple();

        var rotChannel = Assert.Single(clips[0].Channels, c => c.Path == AnimationPath.Rotation);
        Assert.NotNull(rotChannel.Rotations);
        Assert.Equal(rotChannel.Times.Length, rotChannel.Rotations.Length);
    }

    [Fact]
    public void Parse_TranslationChannel_HasVec3Keyframes()
    {
        var (_, clips) = ParseRiggedSimple();

        var transChannel = Assert.Single(clips[0].Channels, c => c.Path == AnimationPath.Translation);
        Assert.NotNull(transChannel.Translations);
        Assert.Equal(transChannel.Times.Length, transChannel.Translations.Length);
    }

    [Fact]
    public void Parse_RiggedSimple_DurationPositive()
    {
        var (_, clips) = ParseRiggedSimple();
        Assert.True(clips[0].Duration > 0, "Animation duration should be positive.");
    }
}
