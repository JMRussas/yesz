//  YesZ - ShadowDepthUniforms Tests
//
//  Validates struct layout and size for the shadow depth pass uniform buffer.
//  The struct must be exactly 128 bytes to match the WGSL ShadowMaterial struct.
//
//  Depends on: YesZ.Rendering (ShadowDepthUniforms), System.Runtime.InteropServices
//  Used by:    Test runner

using System.Numerics;
using System.Runtime.InteropServices;
using Xunit;

namespace YesZ.Rendering.Tests;

public class ShadowDepthUniformsTests
{
    [Fact]
    public void SizeOf_Is128Bytes()
    {
        Assert.Equal(128, Marshal.SizeOf<ShadowDepthUniforms>());
    }

    [Fact]
    public void LightViewProjOffset_Is0()
    {
        Assert.Equal(0, Marshal.OffsetOf<ShadowDepthUniforms>(nameof(ShadowDepthUniforms.LightViewProj)).ToInt32());
    }

    [Fact]
    public void ModelOffset_Is64()
    {
        Assert.Equal(64, Marshal.OffsetOf<ShadowDepthUniforms>(nameof(ShadowDepthUniforms.Model)).ToInt32());
    }
}
