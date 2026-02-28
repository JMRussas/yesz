//  YesZ - LitMaterialUniforms Tests
//
//  Verifies GPU uniform buffer layout matches WGSL expectations.
//  WebGPU requires 16-byte alignment and exact field offsets.
//
//  Depends on: YesZ.Rendering (LitMaterialUniforms)
//  Used by:    CI

using System.Runtime.InteropServices;
using Xunit;

namespace YesZ.Rendering.Tests;

public class LitMaterialUniformsTests
{
    [Fact]
    public void SizeOf_Returns160Bytes()
    {
        Assert.Equal(160, Marshal.SizeOf<LitMaterialUniforms>());
    }

    [Fact]
    public void ModelOffset_Is0()
    {
        Assert.Equal(0, Marshal.OffsetOf<LitMaterialUniforms>(nameof(LitMaterialUniforms.Model)).ToInt32());
    }

    [Fact]
    public void NormalMatrixOffset_Is64()
    {
        Assert.Equal(64, Marshal.OffsetOf<LitMaterialUniforms>(nameof(LitMaterialUniforms.NormalMatrix)).ToInt32());
    }

    [Fact]
    public void BaseColorFactorOffset_Is128()
    {
        Assert.Equal(128, Marshal.OffsetOf<LitMaterialUniforms>(nameof(LitMaterialUniforms.BaseColorFactor)).ToInt32());
    }

    [Fact]
    public void MetallicOffset_Is144()
    {
        Assert.Equal(144, Marshal.OffsetOf<LitMaterialUniforms>(nameof(LitMaterialUniforms.Metallic)).ToInt32());
    }

    [Fact]
    public void RoughnessOffset_Is148()
    {
        Assert.Equal(148, Marshal.OffsetOf<LitMaterialUniforms>(nameof(LitMaterialUniforms.Roughness)).ToInt32());
    }
}
