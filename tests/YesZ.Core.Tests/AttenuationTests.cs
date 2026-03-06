//  YesZ - Attenuation Tests
//
//  Validates the smooth quadratic attenuation formula used by point lights:
//  attenuate(d, r) = (1 - clamp(d/r, 0, 1)²)²
//
//  The formula lives in WGSL (lit3d.wgsl) but we test the equivalent C#
//  computation to verify the math at known distances.
//
//  Depends on: Xunit
//  Used by:    CI

using System;
using Xunit;

namespace YesZ.Tests;

public class AttenuationTests
{
    /// <summary>
    /// C# equivalent of the WGSL attenuate() function in lit3d.wgsl.
    /// </summary>
    private static float Attenuate(float distance, float range)
    {
        float ratio = Math.Clamp(distance / range, 0f, 1f);
        float falloff = 1f - ratio * ratio;
        return falloff * falloff;
    }

    [Fact]
    public void AtDistance0_Returns1()
    {
        Assert.Equal(1f, Attenuate(0f, 10f));
    }

    [Fact]
    public void AtDistanceEqualToRange_Returns0()
    {
        Assert.Equal(0f, Attenuate(10f, 10f));
    }

    [Fact]
    public void AtHalfRange_ReturnsExpected()
    {
        // (1 - 0.25)^2 = 0.75^2 = 0.5625
        Assert.Equal(0.5625f, Attenuate(5f, 10f), 5);
    }

    [Fact]
    public void BeyondRange_Returns0()
    {
        Assert.Equal(0f, Attenuate(15f, 10f));
    }

    [Fact]
    public void AtQuarterRange_HigherThanHalf()
    {
        float atQuarter = Attenuate(2.5f, 10f);
        float atHalf = Attenuate(5f, 10f);
        Assert.True(atQuarter > atHalf, $"Quarter={atQuarter} should be > Half={atHalf}");
    }
}
