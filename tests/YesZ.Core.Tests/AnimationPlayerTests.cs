//  YesZ - AnimationPlayer3D Tests
//
//  Tests for animation playback: sampling at various times,
//  looping, unanimated joints, and full pipeline integration.
//
//  Depends on: YesZ (AnimationPlayer3D, AnimationClip3D, AnimationChannel3D,
//              AnimationSampler, Skeleton3D, JointMatrixComputer), System.Numerics
//  Used by:    test runner

using System;
using System.Numerics;
using Xunit;

namespace YesZ.Tests;

public class AnimationPlayerTests
{
    private const float Epsilon = 1e-4f;

    /// <summary>
    /// Create a simple 2-joint skeleton with a translation animation on joint 1.
    /// Joint 0 (root) at origin, joint 1 translates from (0,0,0) to (0,5,0) over 1 second.
    /// </summary>
    private static (Skeleton3D skeleton, AnimationClip3D clip, Matrix4x4[] bindPose) CreateTestSetup()
    {
        var skeleton = new Skeleton3D(
            parentIndices: [-1, 0],
            inverseBindMatrices: [Matrix4x4.Identity, Matrix4x4.Identity],
            jointNodeIndices: [0, 1]);

        var channel = new AnimationChannel3D(
            jointIndex: 1,
            path: AnimationPath.Translation,
            interpolation: InterpolationMode.Linear,
            times: [0f, 1f],
            translations: [Vector3.Zero, new Vector3(0, 5, 0)],
            rotations: null,
            scales: null);

        var clip = new AnimationClip3D("test", [channel]);
        var bindPose = new Matrix4x4[] { Matrix4x4.Identity, Matrix4x4.Identity };

        return (skeleton, clip, bindPose);
    }

    [Fact]
    public void Sample_AtT0_ReturnsFirstKeyframeValues()
    {
        var (skeleton, clip, bindPose) = CreateTestSetup();
        var player = new AnimationPlayer3D();
        player.Play(clip);

        var localPoses = new Matrix4x4[2];
        player.Sample(skeleton, bindPose, localPoses);

        // Joint 1 at t=0: translation = (0,0,0), so localPose ≈ identity
        Assert.InRange(localPoses[1].M42, -Epsilon, Epsilon); // Y translation
    }

    [Fact]
    public void Sample_AtDuration_ReturnsLastKeyframeValues()
    {
        var (skeleton, clip, bindPose) = CreateTestSetup();
        var player = new AnimationPlayer3D();
        player.Looping = false;
        player.Play(clip);
        player.Update(1.0f); // Advance to end

        var localPoses = new Matrix4x4[2];
        player.Sample(skeleton, bindPose, localPoses);

        // Joint 1 at t=1: translation = (0,5,0)
        Assert.InRange(localPoses[1].M42, 5 - Epsilon, 5 + Epsilon);
    }

    [Fact]
    public void Sample_AtMidpoint_InterpolatesBetweenKeyframes()
    {
        var (skeleton, clip, bindPose) = CreateTestSetup();
        var player = new AnimationPlayer3D();
        player.Play(clip);
        player.Update(0.5f); // Advance to midpoint

        var localPoses = new Matrix4x4[2];
        player.Sample(skeleton, bindPose, localPoses);

        // Joint 1 at t=0.5: translation = (0,2.5,0)
        Assert.InRange(localPoses[1].M42, 2.5f - Epsilon, 2.5f + Epsilon);
    }

    [Fact]
    public void Sample_Looping_WrapsCorrectly()
    {
        var (skeleton, clip, bindPose) = CreateTestSetup();
        var player = new AnimationPlayer3D();
        player.Looping = true;
        player.Play(clip);
        player.Update(1.5f); // 1.5 % 1.0 = 0.5

        var localPoses = new Matrix4x4[2];
        player.Sample(skeleton, bindPose, localPoses);

        // Wrapped to t=0.5: translation = (0,2.5,0)
        Assert.InRange(localPoses[1].M42, 2.5f - Epsilon, 2.5f + Epsilon);
    }

    [Fact]
    public void Sample_NoLooping_ClampsAtEnd()
    {
        var (skeleton, clip, bindPose) = CreateTestSetup();
        var player = new AnimationPlayer3D();
        player.Looping = false;
        player.Play(clip);
        player.Update(2.0f); // Beyond duration, should clamp to 1.0

        var localPoses = new Matrix4x4[2];
        player.Sample(skeleton, bindPose, localPoses);

        // Clamped to t=1: translation = (0,5,0)
        Assert.InRange(localPoses[1].M42, 5 - Epsilon, 5 + Epsilon);
    }

    [Fact]
    public void Sample_UnanimatedJoint_UsesBindPose()
    {
        var (skeleton, clip, _) = CreateTestSetup();
        // Set bind pose for joint 0 to a known translation
        var bindPose = new Matrix4x4[]
        {
            Matrix4x4.CreateTranslation(10, 20, 30),
            Matrix4x4.Identity,
        };

        var player = new AnimationPlayer3D();
        player.Play(clip);
        player.Update(0.5f);

        var localPoses = new Matrix4x4[2];
        player.Sample(skeleton, bindPose, localPoses);

        // Joint 0 is not animated — should keep bind pose
        Assert.InRange(localPoses[0].M41, 10 - Epsilon, 10 + Epsilon);
        Assert.InRange(localPoses[0].M42, 20 - Epsilon, 20 + Epsilon);
        Assert.InRange(localPoses[0].M43, 30 - Epsilon, 30 + Epsilon);
    }

    [Fact]
    public void JointMatrices_AtBindPose_NearIdentity()
    {
        var (skeleton, clip, bindPose) = CreateTestSetup();
        var player = new AnimationPlayer3D();
        // Don't play any clip — should use bind pose
        var localPoses = new Matrix4x4[2];
        player.Sample(skeleton, bindPose, localPoses);

        var jointMatrices = new Matrix4x4[2];
        JointMatrixComputer.Compute(skeleton, localPoses, jointMatrices);

        // With identity IBMs and identity bind pose, joint matrices should be identity
        AssertMatrixNear(Matrix4x4.Identity, jointMatrices[0]);
        AssertMatrixNear(Matrix4x4.Identity, jointMatrices[1]);
    }

    [Fact]
    public void Update_Speed_AffectsPlayback()
    {
        var (skeleton, clip, bindPose) = CreateTestSetup();
        var player = new AnimationPlayer3D();
        player.Speed = 2.0f;
        player.Play(clip);
        player.Update(0.25f); // 0.25 * 2.0 = 0.5 effective time

        var localPoses = new Matrix4x4[2];
        player.Sample(skeleton, bindPose, localPoses);

        Assert.InRange(localPoses[1].M42, 2.5f - Epsilon, 2.5f + Epsilon);
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        Assert.InRange(actual.M11, expected.M11 - Epsilon, expected.M11 + Epsilon);
        Assert.InRange(actual.M12, expected.M12 - Epsilon, expected.M12 + Epsilon);
        Assert.InRange(actual.M13, expected.M13 - Epsilon, expected.M13 + Epsilon);
        Assert.InRange(actual.M14, expected.M14 - Epsilon, expected.M14 + Epsilon);
        Assert.InRange(actual.M21, expected.M21 - Epsilon, expected.M21 + Epsilon);
        Assert.InRange(actual.M22, expected.M22 - Epsilon, expected.M22 + Epsilon);
        Assert.InRange(actual.M23, expected.M23 - Epsilon, expected.M23 + Epsilon);
        Assert.InRange(actual.M24, expected.M24 - Epsilon, expected.M24 + Epsilon);
        Assert.InRange(actual.M31, expected.M31 - Epsilon, expected.M31 + Epsilon);
        Assert.InRange(actual.M32, expected.M32 - Epsilon, expected.M32 + Epsilon);
        Assert.InRange(actual.M33, expected.M33 - Epsilon, expected.M33 + Epsilon);
        Assert.InRange(actual.M34, expected.M34 - Epsilon, expected.M34 + Epsilon);
        Assert.InRange(actual.M41, expected.M41 - Epsilon, expected.M41 + Epsilon);
        Assert.InRange(actual.M42, expected.M42 - Epsilon, expected.M42 + Epsilon);
        Assert.InRange(actual.M43, expected.M43 - Epsilon, expected.M43 + Epsilon);
        Assert.InRange(actual.M44, expected.M44 - Epsilon, expected.M44 + Epsilon);
    }
}
