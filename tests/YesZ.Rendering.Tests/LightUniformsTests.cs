//  YesZ - LightUniforms Tests
//
//  Verifies GPU uniform buffer layout matches WGSL expectations.
//  WebGPU requires 16-byte alignment and exact field offsets.
//
//  Depends on: YesZ.Rendering (LightUniforms, PointLightData)
//  Used by:    CI

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Xunit;

namespace YesZ.Rendering.Tests;

public class LightUniformsTests
{
    [Fact]
    public void SizeOf_Returns336Bytes()
    {
        Assert.Equal(336, Marshal.SizeOf<LightUniforms>());
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

    [Fact]
    public void PointLightCountOffset_Is64()
    {
        Assert.Equal(64, Marshal.OffsetOf<LightUniforms>(nameof(LightUniforms.PointLightCount)).ToInt32());
    }

    [Fact]
    public void PointLight0Offset_Is80()
    {
        Assert.Equal(80, Marshal.OffsetOf<LightUniforms>(nameof(LightUniforms.PointLight0)).ToInt32());
    }

    [Fact]
    public void PointLightDataSizeOf_Returns32Bytes()
    {
        Assert.Equal(32, Marshal.SizeOf<PointLightData>());
    }

    [Fact]
    public void SetPointLight_ValidIndex_WritesData()
    {
        var uniforms = new LightUniforms();
        var data = new PointLightData
        {
            Position = new Vector4(1f, 2f, 3f, 10f),
            Color = new Vector4(0.5f, 0.8f, 0.2f, 0f),
        };

        uniforms.SetPointLight(0, in data);
        Assert.Equal(new Vector4(1f, 2f, 3f, 10f), uniforms.PointLight0.Position);
        Assert.Equal(new Vector4(0.5f, 0.8f, 0.2f, 0f), uniforms.PointLight0.Color);

        uniforms.SetPointLight(3, in data);
        Assert.Equal(new Vector4(1f, 2f, 3f, 10f), uniforms.PointLight3.Position);
    }

    [Fact]
    public void SetPointLight_LastIndex_WritesData()
    {
        var uniforms = new LightUniforms();
        var data = new PointLightData
        {
            Position = new Vector4(9f, 8f, 7f, 5f),
            Color = new Vector4(1f, 1f, 1f, 0f),
        };

        uniforms.SetPointLight(7, in data);
        Assert.Equal(new Vector4(9f, 8f, 7f, 5f), uniforms.PointLight7.Position);
    }

    [Fact]
    public void SetPointLight_IndexOutOfRange_Throws()
    {
        var uniforms = new LightUniforms();
        var data = new PointLightData();

        Assert.Throws<ArgumentOutOfRangeException>(() => uniforms.SetPointLight(8, in data));
        Assert.Throws<ArgumentOutOfRangeException>(() => uniforms.SetPointLight(-1, in data));
    }
}
