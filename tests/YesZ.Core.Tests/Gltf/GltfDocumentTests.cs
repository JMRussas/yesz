//  YesZ - GltfDocument Tests
//
//  Verifies JSON deserialization of glTF document structure from
//  embedded .glb test files.
//
//  Depends on: YesZ.Gltf (GlbReader, GltfDocument), Xunit
//  Used by:    CI

using YesZ.Gltf;
using Xunit;

namespace YesZ.Tests.Gltf;

public class GltfDocumentTests
{
    private static GltfDocument LoadBoxDocument()
    {
        var data = TestHelper.LoadEmbeddedGlb("Box.glb");
        var glb = GlbReader.Parse(data);
        return GltfDocument.Deserialize(glb.Json);
    }

    private static GltfDocument LoadBoxTexturedDocument()
    {
        var data = TestHelper.LoadEmbeddedGlb("BoxTextured.glb");
        var glb = GlbReader.Parse(data);
        return GltfDocument.Deserialize(glb.Json);
    }

    [Fact]
    public void Deserialize_Box_HasAssetVersion2()
    {
        var doc = LoadBoxDocument();

        Assert.NotNull(doc.Asset);
        Assert.Equal("2.0", doc.Asset.Version);
    }

    [Fact]
    public void Deserialize_Box_HasOneScene()
    {
        var doc = LoadBoxDocument();

        Assert.NotNull(doc.Scenes);
        Assert.Single(doc.Scenes);
        Assert.Equal(0, doc.Scene);
    }

    [Fact]
    public void Deserialize_Box_HasOneMesh()
    {
        var doc = LoadBoxDocument();

        Assert.NotNull(doc.Meshes);
        Assert.Single(doc.Meshes);
        Assert.Equal("Mesh", doc.Meshes[0].Name);
    }

    [Fact]
    public void Deserialize_Box_HasAccessors()
    {
        var doc = LoadBoxDocument();

        Assert.NotNull(doc.Accessors);
        Assert.Equal(3, doc.Accessors.Length); // indices, normals, positions
    }

    [Fact]
    public void Deserialize_Box_HasMaterial()
    {
        var doc = LoadBoxDocument();

        Assert.NotNull(doc.Materials);
        Assert.Single(doc.Materials);
        Assert.Equal("Red", doc.Materials[0].Name);
        Assert.NotNull(doc.Materials[0].PbrMetallicRoughness);
    }

    [Fact]
    public void Deserialize_Box_MaterialHasBaseColorFactor()
    {
        var doc = LoadBoxDocument();
        var pbr = doc.Materials![0].PbrMetallicRoughness!;

        Assert.NotNull(pbr.BaseColorFactor);
        Assert.Equal(4, pbr.BaseColorFactor.Length);
        Assert.InRange(pbr.BaseColorFactor[0], 0.79f, 0.81f); // ~0.8 red
    }

    [Fact]
    public void Deserialize_Box_PrimitiveHasAttributes()
    {
        var doc = LoadBoxDocument();
        var prim = doc.Meshes![0].Primitives![0];

        Assert.NotNull(prim.Attributes);
        Assert.True(prim.Attributes.ContainsKey("POSITION"));
        Assert.True(prim.Attributes.ContainsKey("NORMAL"));
        Assert.Equal(0, prim.Indices);
    }

    [Fact]
    public void Deserialize_BoxTextured_HasTexture()
    {
        var doc = LoadBoxTexturedDocument();

        Assert.NotNull(doc.Textures);
        Assert.Single(doc.Textures);
    }

    [Fact]
    public void Deserialize_BoxTextured_HasImage()
    {
        var doc = LoadBoxTexturedDocument();

        Assert.NotNull(doc.Images);
        Assert.Single(doc.Images);
        Assert.Equal("image/png", doc.Images[0].MimeType);
    }

    [Fact]
    public void Deserialize_BoxTextured_HasTexCoord0()
    {
        var doc = LoadBoxTexturedDocument();
        var prim = doc.Meshes![0].Primitives![0];

        Assert.True(prim.Attributes!.ContainsKey("TEXCOORD_0"));
    }

    [Fact]
    public void Deserialize_BoxTextured_MaterialHasBaseColorTexture()
    {
        var doc = LoadBoxTexturedDocument();
        var pbr = doc.Materials![0].PbrMetallicRoughness!;

        Assert.NotNull(pbr.BaseColorTexture);
        Assert.Equal(0, pbr.BaseColorTexture.Index);
    }
}
