//  YesZ - CascadeShadowUniforms Tests
//
//  Validates struct layout, size, and field offsets for the cascade shadow
//  uniform buffer. Must match the WGSL CascadeShadowData struct exactly.
//
//  Depends on: YesZ.Rendering (CascadeShadowUniforms), System.Numerics,
//              System.Runtime.InteropServices
//  Used by:    test runner

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Xunit;

namespace YesZ.Rendering.Tests;

public class CascadeShadowUniformsTests
{
    [Fact]
    public void SizeOf_Is288Bytes()
    {
        Assert.Equal(288, Marshal.SizeOf<CascadeShadowUniforms>());
    }

    [Fact]
    public void LightViewProj0Offset_Is0()
    {
        Assert.Equal(0, Marshal.OffsetOf<CascadeShadowUniforms>(
            nameof(CascadeShadowUniforms.LightViewProj0)).ToInt32());
    }

    [Fact]
    public void SplitDepthsOffset_Is256()
    {
        Assert.Equal(256, Marshal.OffsetOf<CascadeShadowUniforms>(
            nameof(CascadeShadowUniforms.SplitDepths)).ToInt32());
    }

    [Fact]
    public void CascadeCountOffset_Is272()
    {
        Assert.Equal(272, Marshal.OffsetOf<CascadeShadowUniforms>(
            nameof(CascadeShadowUniforms.CascadeCount)).ToInt32());
    }

    [Fact]
    public void ShadowBiasOffset_Is276()
    {
        Assert.Equal(276, Marshal.OffsetOf<CascadeShadowUniforms>(
            nameof(CascadeShadowUniforms.ShadowBias)).ToInt32());
    }

    [Fact]
    public void SetLightViewProj_ValidIndex_WritesData()
    {
        var uniforms = new CascadeShadowUniforms();
        var matrix = Matrix4x4.CreateTranslation(1, 2, 3);

        uniforms.SetLightViewProj(2, in matrix);

        Assert.Equal(matrix, uniforms.LightViewProj2);
    }

    [Fact]
    public void SetLightViewProj_InvalidIndex_Throws()
    {
        var uniforms = new CascadeShadowUniforms();
        var matrix = Matrix4x4.Identity;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            uniforms.SetLightViewProj(4, in matrix));
    }
}
