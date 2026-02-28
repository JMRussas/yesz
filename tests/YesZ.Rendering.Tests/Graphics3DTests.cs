//  YesZ - Graphics3D Tests
//
//  Unit tests for 3D rendering logic that doesn't require GPU initialization.
//  Full integration testing requires running HelloCube visually.
//
//  Depends on: YesZ.Rendering (Graphics3D), YesZ.Core (Camera3D, MeshVertex3D)
//  Used by:    CI

using Xunit;

namespace YesZ.Rendering.Tests;

public class Graphics3DTests
{
    [Fact]
    public void EmbeddedShader_Unlit_Exists()
    {
        var assembly = typeof(Graphics3D).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();
        Assert.Contains("YesZ.Rendering.Shaders.unlit3d.wgsl", resourceNames);
    }

    [Fact]
    public void EmbeddedShader_Unlit_HasContent()
    {
        var assembly = typeof(Graphics3D).Assembly;
        using var stream = assembly.GetManifestResourceStream("YesZ.Rendering.Shaders.unlit3d.wgsl");
        Assert.NotNull(stream);
        Assert.True(stream!.Length > 0);

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Contains("vs_main", content);
        Assert.Contains("fs_main", content);
        Assert.Contains("globals", content);
    }

    [Fact]
    public void EmbeddedShader_Textured_Exists()
    {
        var assembly = typeof(Graphics3D).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();
        Assert.Contains("YesZ.Rendering.Shaders.textured3d.wgsl", resourceNames);
    }

    [Fact]
    public void EmbeddedShader_Textured_HasContent()
    {
        var assembly = typeof(Graphics3D).Assembly;
        using var stream = assembly.GetManifestResourceStream("YesZ.Rendering.Shaders.textured3d.wgsl");
        Assert.NotNull(stream);
        Assert.True(stream!.Length > 0);

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Contains("vs_main", content);
        Assert.Contains("fs_main", content);
        Assert.Contains("globals", content);
        Assert.Contains("material", content);
        Assert.Contains("base_color_texture", content);
        Assert.Contains("textureSample", content);
    }

    [Fact]
    public void EmbeddedShader_Lit_Exists()
    {
        var assembly = typeof(Graphics3D).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();
        Assert.Contains("YesZ.Rendering.Shaders.lit3d.wgsl", resourceNames);
    }

    [Fact]
    public void EmbeddedShader_Lit_HasContent()
    {
        var assembly = typeof(Graphics3D).Assembly;
        using var stream = assembly.GetManifestResourceStream("YesZ.Rendering.Shaders.lit3d.wgsl");
        Assert.NotNull(stream);
        Assert.True(stream!.Length > 0);

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Contains("vs_main", content);
        Assert.Contains("fs_main", content);
        Assert.Contains("globals", content);
        Assert.Contains("material", content);
        Assert.Contains("lights", content);
        Assert.Contains("world_normal", content);
        Assert.Contains("textureSample", content);
    }
}
