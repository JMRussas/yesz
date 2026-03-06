//  YesZ - ShadowConfig Tests
//
//  Tests for shadow map configuration defaults.
//
//  Depends on: YesZ.Rendering (ShadowConfig)
//  Used by:    test runner

using Xunit;

namespace YesZ.Rendering.Tests;

public class ShadowConfigTests
{
    [Fact]
    public void Defaults_Resolution2048()
    {
        var config = new ShadowConfig();
        Assert.Equal(2048, config.Resolution);
    }

    [Fact]
    public void Defaults_ShadowDistance50()
    {
        var config = new ShadowConfig();
        Assert.Equal(50.0f, config.ShadowDistance);
    }

    [Fact]
    public void Defaults_DepthBias()
    {
        var config = new ShadowConfig();
        Assert.Equal(0.005f, config.DepthBias);
    }

    [Fact]
    public void Defaults_NormalBias()
    {
        var config = new ShadowConfig();
        Assert.Equal(0.05f, config.NormalBias);
    }

    [Fact]
    public void Defaults_CascadeCount3()
    {
        var config = new ShadowConfig();
        Assert.Equal(3, config.CascadeCount);
    }

    [Fact]
    public void Defaults_Lambda075()
    {
        var config = new ShadowConfig();
        Assert.Equal(0.75f, config.Lambda);
    }

    [Fact]
    public void InitSyntax_OverridesDefaults()
    {
        var config = new ShadowConfig
        {
            Resolution = 1024,
            ShadowDistance = 100f,
            DepthBias = 0.01f,
            NormalBias = 0.1f,
            CascadeCount = 2,
            Lambda = 0.5f,
        };

        Assert.Equal(1024, config.Resolution);
        Assert.Equal(100f, config.ShadowDistance);
        Assert.Equal(0.01f, config.DepthBias);
        Assert.Equal(0.1f, config.NormalBias);
        Assert.Equal(2, config.CascadeCount);
        Assert.Equal(0.5f, config.Lambda);
    }
}
