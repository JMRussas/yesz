//  YesZ - CascadeSplitComputer Tests
//
//  Validates cascade split distance computation for cascaded shadow maps.
//  Tests boundary values, monotonicity, and lambda blending behavior.
//
//  Depends on: YesZ (CascadeSplitComputer), System (MathF)
//  Used by:    test runner

using System;
using Xunit;

namespace YesZ.Tests;

public class CascadeSplitTests
{
    private const float Epsilon = 1e-5f;

    [Fact]
    public void Compute_3Cascades_Returns4Splits()
    {
        var splits = CascadeSplitComputer.ComputeSplits(0.1f, 100f, 3);
        Assert.Equal(4, splits.Length);
    }

    [Fact]
    public void Compute_3Cascades_FirstSplitIsNear()
    {
        var splits = CascadeSplitComputer.ComputeSplits(0.1f, 100f, 3);
        Assert.Equal(0.1f, splits[0], Epsilon);
    }

    [Fact]
    public void Compute_3Cascades_LastSplitIsFar()
    {
        var splits = CascadeSplitComputer.ComputeSplits(0.1f, 100f, 3);
        Assert.Equal(100f, splits[3], Epsilon);
    }

    [Fact]
    public void Compute_3Cascades_SplitsAreMonotonicallyIncreasing()
    {
        var splits = CascadeSplitComputer.ComputeSplits(0.1f, 100f, 3);
        for (int i = 1; i < splits.Length; i++)
            Assert.True(splits[i] > splits[i - 1],
                $"splits[{i}] ({splits[i]}) should be > splits[{i - 1}] ({splits[i - 1]})");
    }

    [Fact]
    public void Compute_Lambda0_UniformDistribution()
    {
        var splits = CascadeSplitComputer.ComputeSplits(0.1f, 100f, 3, lambda: 0f);

        // Uniform: near + (far - near) * (i / cascadeCount)
        float expected1 = 0.1f + (100f - 0.1f) * (1f / 3f);
        float expected2 = 0.1f + (100f - 0.1f) * (2f / 3f);

        Assert.Equal(expected1, splits[1], Epsilon);
        Assert.Equal(expected2, splits[2], Epsilon);
    }

    [Fact]
    public void Compute_Lambda1_LogarithmicDistribution()
    {
        var splits = CascadeSplitComputer.ComputeSplits(0.1f, 100f, 3, lambda: 1f);

        // Logarithmic: near * pow(far / near, i / cascadeCount)
        float expected1 = 0.1f * MathF.Pow(100f / 0.1f, 1f / 3f);
        float expected2 = 0.1f * MathF.Pow(100f / 0.1f, 2f / 3f);

        Assert.Equal(expected1, splits[1], Epsilon);
        Assert.Equal(expected2, splits[2], Epsilon);
    }

    [Fact]
    public void Compute_DefaultLambda_NearSplitCloserThanUniform()
    {
        // With default lambda (0.75), logarithmic dominance means
        // the first split is closer to the camera than uniform would be
        var splits = CascadeSplitComputer.ComputeSplits(0.1f, 100f, 3);
        float uniformFirst = 0.1f + (100f - 0.1f) * (1f / 3f);

        Assert.True(splits[1] < uniformFirst,
            $"Default lambda split ({splits[1]}) should be closer than uniform ({uniformFirst})");
    }
}
