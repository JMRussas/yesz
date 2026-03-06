//  YesZ - ShadowUniforms Tests
//
//  Validates struct layout and size for the lit+shadow pass shadow UBO.
//  The struct must be exactly 80 bytes to match the WGSL ShadowData struct.
//
//  Depends on: YesZ.Rendering (ShadowUniforms), System.Runtime.InteropServices
//  Used by:    Test runner

using System.Runtime.InteropServices;
using Xunit;

namespace YesZ.Rendering.Tests;

public class ShadowUniformsTests
{
    [Fact]
    public void SizeOf_Is80Bytes()
    {
        Assert.Equal(80, Marshal.SizeOf<ShadowUniforms>());
    }

    [Fact]
    public void LightViewProjOffset_Is0()
    {
        Assert.Equal(0, Marshal.OffsetOf<ShadowUniforms>(nameof(ShadowUniforms.LightViewProj)).ToInt32());
    }

    [Fact]
    public void ShadowBiasOffset_Is64()
    {
        Assert.Equal(64, Marshal.OffsetOf<ShadowUniforms>(nameof(ShadowUniforms.ShadowBias)).ToInt32());
    }

    [Fact]
    public void NormalBiasOffset_Is68()
    {
        Assert.Equal(68, Marshal.OffsetOf<ShadowUniforms>(nameof(ShadowUniforms.NormalBias)).ToInt32());
    }

    [Fact]
    public void TexelSizeXOffset_Is72()
    {
        Assert.Equal(72, Marshal.OffsetOf<ShadowUniforms>(nameof(ShadowUniforms.TexelSizeX)).ToInt32());
    }

    [Fact]
    public void TexelSizeYOffset_Is76()
    {
        Assert.Equal(76, Marshal.OffsetOf<ShadowUniforms>(nameof(ShadowUniforms.TexelSizeY)).ToInt32());
    }
}
