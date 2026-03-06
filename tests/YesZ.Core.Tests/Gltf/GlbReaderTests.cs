//  YesZ - GlbReader Tests
//
//  Verifies .glb binary container parsing: header validation, chunk
//  extraction, and error handling for malformed files.
//
//  Depends on: YesZ.Gltf (GlbReader), Xunit
//  Used by:    CI

using YesZ.Gltf;
using Xunit;

namespace YesZ.Tests.Gltf;

public class GlbReaderTests
{
    [Fact]
    public void Parse_ValidGlb_ExtractsJsonAndBinChunks()
    {
        var data = TestHelper.LoadEmbeddedGlb("Box.glb");
        var result = GlbReader.Parse(data);

        Assert.False(string.IsNullOrEmpty(result.Json));
        Assert.NotEmpty(result.BinChunk);
    }

    [Fact]
    public void Parse_ValidGlb_JsonContainsAsset()
    {
        var data = TestHelper.LoadEmbeddedGlb("Box.glb");
        var result = GlbReader.Parse(data);

        Assert.Contains("\"asset\"", result.Json);
        Assert.Contains("\"version\"", result.Json);
    }

    [Fact]
    public void Parse_ValidGlb_BinChunkMatchesBufferSize()
    {
        // Box.glb has one buffer of 648 bytes
        var data = TestHelper.LoadEmbeddedGlb("Box.glb");
        var result = GlbReader.Parse(data);

        Assert.Equal(648, result.BinChunk.Length);
    }

    [Fact]
    public void Parse_BoxTextured_BinChunkMatchesBufferSize()
    {
        // BoxTextured.glb has one buffer of 4592 bytes
        var data = TestHelper.LoadEmbeddedGlb("BoxTextured.glb");
        var result = GlbReader.Parse(data);

        Assert.Equal(4592, result.BinChunk.Length);
    }

    [Fact]
    public void Parse_TruncatedFile_Throws()
    {
        var data = new byte[] { 0x67, 0x6C, 0x54, 0x46, 0x02, 0x00 };

        Assert.Throws<InvalidOperationException>(() => GlbReader.Parse(data));
    }

    [Fact]
    public void Parse_WrongMagic_Throws()
    {
        var data = new byte[20];
        data[0] = 0xFF; // Wrong magic

        Assert.Throws<InvalidOperationException>(() => GlbReader.Parse(data));
    }

    [Fact]
    public void Parse_EmptyFile_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => GlbReader.Parse(Array.Empty<byte>()));
    }
}
