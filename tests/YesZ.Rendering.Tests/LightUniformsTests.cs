//  YesZ - LightUniforms Tests
//
//  Verifies GPU uniform buffer layout matches WGSL expectations.
//  WebGPU requires 16-byte alignment and exact field offsets.
//
//  Depends on: YesZ.Rendering (LightUniforms)
//  Used by:    CI

using System.Runtime.InteropServices;
using Xunit;

namespace YesZ.Rendering.Tests;

public class LightUniformsTests
{
    [Fact]
    public void SizeOf_Returns64Bytes()
    {
        Assert.Equal(64, Marshal.SizeOf<LightUniforms>());
    }

    [Fact]
    public void AmbientColorOffset_Is0()
    {
        Assert.Equal(0, Marshal.OffsetOf<LightUniforms>(nameof(LightUniforms.AmbientColor)).ToInt32());
    }

    [Fact]
    public void DirectionalDirOffset_Is16()
    {
        Assert.Equal(16, Marshal.OffsetOf<LightUniforms>(nameof(LightUniforms.DirectionalDir)).ToInt32());
    }

    [Fact]
    public void DirectionalColorOffset_Is32()
    {
        Assert.Equal(32, Marshal.OffsetOf<LightUniforms>(nameof(LightUniforms.DirectionalColor)).ToInt32());
    }

    [Fact]
    public void CameraPositionOffset_Is48()
    {
        Assert.Equal(48, Marshal.OffsetOf<LightUniforms>(nameof(LightUniforms.CameraPosition)).ToInt32());
    }
}
