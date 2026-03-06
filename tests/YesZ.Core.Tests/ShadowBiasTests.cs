//  YesZ - Shadow Bias Tests
//
//  Tests the slope-scaled bias formula used in the shadow sampling shader.
//  Formula: max(baseBias * (1 - NdotL), baseBias * 0.1)
//
//  Depends on: System (MathF)
//  Used by:    Test runner

using Xunit;

namespace YesZ.Core.Tests;

public class ShadowBiasTests
{
    private const float BaseBias = 0.005f;

    private static float ComputeSlopeScaledBias(float baseBias, float NdotL)
    {
        return MathF.Max(baseBias * (1.0f - NdotL), baseBias * 0.1f);
    }

    [Fact]
    public void HeadOn_NdotL1_ReturnsMinimumBias()
    {
        float bias = ComputeSlopeScaledBias(BaseBias, 1.0f);
        // At NdotL=1: max(0.005 * 0, 0.005 * 0.1) = max(0, 0.0005) = 0.0005
        Assert.Equal(BaseBias * 0.1f, bias, 6);
    }

    [Fact]
    public void GrazingAngle_NdotL0_ReturnsMaximumBias()
    {
        float bias = ComputeSlopeScaledBias(BaseBias, 0.0f);
        // At NdotL=0: max(0.005 * 1, 0.005 * 0.1) = max(0.005, 0.0005) = 0.005
        Assert.Equal(BaseBias, bias, 6);
    }

    [Fact]
    public void FortyFiveDegrees_NdotL0707_IntermediateBias()
    {
        float NdotL = MathF.Cos(MathF.PI / 4.0f); // ~0.707
        float bias = ComputeSlopeScaledBias(BaseBias, NdotL);
        // Between min and max bias
        Assert.True(bias > BaseBias * 0.1f);
        Assert.True(bias < BaseBias);
    }

    [Fact]
    public void NearGrazing_NdotL01_NearMaxBias()
    {
        float bias = ComputeSlopeScaledBias(BaseBias, 0.1f);
        // At NdotL=0.1: max(0.005 * 0.9, 0.0005) = max(0.0045, 0.0005) = 0.0045
        Assert.Equal(BaseBias * 0.9f, bias, 6);
    }

    [Fact]
    public void NegativeNdotL_ClampedByFormula()
    {
        // NdotL should never be negative (clamped in shader), but test anyway
        float bias = ComputeSlopeScaledBias(BaseBias, -0.5f);
        // max(0.005 * 1.5, 0.0005) = 0.0075 — bias increases beyond baseBias
        Assert.True(bias > BaseBias);
    }
}
