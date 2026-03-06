//  YesZ - Interpolation Tests
//
//  Tests for AnimationSampler: LINEAR lerp/slerp, STEP, keyframe search.
//
//  Depends on: YesZ (AnimationSampler, AnimationChannel3D), System.Numerics
//  Used by:    test runner

using System;
using System.Numerics;
using Xunit;

namespace YesZ.Tests;

public class InterpolationTests
{
    private const float Epsilon = 1e-5f;

    private static AnimationChannel3D MakeTranslationChannel(
        float[] times, Vector3[] values, InterpolationMode mode = InterpolationMode.Linear)
    {
        return new AnimationChannel3D(0, AnimationPath.Translation, mode, times, values, null, null);
    }

    private static AnimationChannel3D MakeRotationChannel(
        float[] times, Quaternion[] values, InterpolationMode mode = InterpolationMode.Linear)
    {
        return new AnimationChannel3D(0, AnimationPath.Rotation, mode, times, null, values, null);
    }

    [Fact]
    public void LinearLerp_Midpoint_ReturnsAverage()
    {
        var ch = MakeTranslationChannel(
            [0f, 1f],
            [Vector3.Zero, new Vector3(2, 2, 2)]);

        var result = AnimationSampler.SampleTranslation(ch, 0.5f);
        Assert.InRange(result.X, 1 - Epsilon, 1 + Epsilon);
        Assert.InRange(result.Y, 1 - Epsilon, 1 + Epsilon);
        Assert.InRange(result.Z, 1 - Epsilon, 1 + Epsilon);
    }

    [Fact]
    public void LinearLerp_AtT0_ReturnsStart()
    {
        var ch = MakeTranslationChannel(
            [0f, 1f],
            [new Vector3(3, 4, 5), new Vector3(6, 7, 8)]);

        var result = AnimationSampler.SampleTranslation(ch, 0f);
        Assert.InRange(result.X, 3 - Epsilon, 3 + Epsilon);
    }

    [Fact]
    public void LinearLerp_AtT1_ReturnsEnd()
    {
        var ch = MakeTranslationChannel(
            [0f, 1f],
            [new Vector3(3, 4, 5), new Vector3(6, 7, 8)]);

        var result = AnimationSampler.SampleTranslation(ch, 1f);
        Assert.InRange(result.X, 6 - Epsilon, 6 + Epsilon);
    }

    [Fact]
    public void Slerp_IdentityToRotation_Midpoint()
    {
        var q0 = Quaternion.Identity;
        var q1 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2); // 90°

        var ch = MakeRotationChannel([0f, 1f], [q0, q1]);
        var result = AnimationSampler.SampleRotation(ch, 0.5f);

        // Midpoint should be ~45° around Y
        var expected = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4);
        Assert.InRange(Quaternion.Dot(result, expected), 1 - Epsilon, 1 + Epsilon);
    }

    [Fact]
    public void Slerp_ShortPath_NegatesDotLessThanZero()
    {
        // q and -q represent the same rotation. If we interpolate between
        // q0 and -q0_rotated_slightly, slerp should take the short path.
        var q0 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.1f);
        // Negate q0 — represents same rotation but opposite hemisphere
        var q1 = new Quaternion(-q0.X, -q0.Y, -q0.Z, -q0.W);
        // Rotate slightly more
        q1 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.2f);
        q1 = new Quaternion(-q1.X, -q1.Y, -q1.Z, -q1.W);

        var result = AnimationSampler.SlerpShortPath(q0, q1, 0.5f);
        // Should not produce a ~180° rotation — should be near 0.15 rad
        var angle = 2 * MathF.Acos(MathF.Abs(result.W));
        Assert.True(angle < MathF.PI / 2, $"Slerp took long path: angle = {angle}");
    }

    [Fact]
    public void Slerp_NearlyIdentical_DoesNotNaN()
    {
        var q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.0001f);
        var result = AnimationSampler.SlerpShortPath(Quaternion.Identity, q, 0.5f);
        Assert.False(float.IsNaN(result.X) || float.IsNaN(result.Y) ||
                     float.IsNaN(result.Z) || float.IsNaN(result.W),
            "Slerp produced NaN for nearly identical quaternions");
    }

    [Fact]
    public void Step_BeforeKeyframe_ReturnsPrevious()
    {
        var ch = MakeTranslationChannel(
            [0f, 1f, 2f],
            [Vector3.Zero, Vector3.UnitX, Vector3.UnitY],
            InterpolationMode.Step);

        var result = AnimationSampler.SampleTranslation(ch, 0.5f);
        Assert.InRange(result.X, -Epsilon, Epsilon); // Should be value at t=0
    }

    [Fact]
    public void Step_ExactlyAtKeyframe_ReturnsCurrent()
    {
        var ch = MakeTranslationChannel(
            [0f, 1f, 2f],
            [Vector3.Zero, Vector3.UnitX, Vector3.UnitY],
            InterpolationMode.Step);

        var result = AnimationSampler.SampleTranslation(ch, 1f);
        Assert.InRange(result.X, 1 - Epsilon, 1 + Epsilon); // Should be value at t=1
    }

    [Fact]
    public void FindKeyframe_ReturnsCorrectBracket()
    {
        var times = new float[] { 0, 0.5f, 1.0f, 1.5f, 2.0f };

        Assert.Equal(0, AnimationSampler.FindKeyframe(times, 0.25f));
        Assert.Equal(1, AnimationSampler.FindKeyframe(times, 0.75f));
        Assert.Equal(2, AnimationSampler.FindKeyframe(times, 1.25f));
        Assert.Equal(3, AnimationSampler.FindKeyframe(times, 1.75f));
    }

    [Fact]
    public void FindKeyframe_ExactMatch_ReturnsCorrectBracket()
    {
        var times = new float[] { 0, 1f, 2f };

        Assert.Equal(0, AnimationSampler.FindKeyframe(times, 0f));
        Assert.Equal(1, AnimationSampler.FindKeyframe(times, 1f));
    }
}
