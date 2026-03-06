//  YesZ - glTF Test Helper
//
//  Loads embedded .glb test resources for use in glTF parser tests.
//
//  Depends on: System.Reflection
//  Used by:    GlbReaderTests, GltfDocumentTests, AccessorReaderTests, MeshExtractorTests

using System.Reflection;

namespace YesZ.Tests.Gltf;

internal static class TestHelper
{
    public static byte[] LoadEmbeddedGlb(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"YesZ.Tests.TestData.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
