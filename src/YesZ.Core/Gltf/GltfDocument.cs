//  YesZ - glTF JSON Document Model
//
//  POCOs for System.Text.Json deserialization of the glTF 2.0 JSON chunk.
//  Only models the fields needed for Phase 4a (mesh extraction) and 4b
//  (model rendering). Additional fields (skins, animations) will be added
//  in Phase 5a.
//
//  Uses camelCase naming policy for glTF spec compliance.
//
//  Depends on: System.Text.Json
//  Used by:    GlbReader (deserialization), AccessorReader, MeshExtractor

using System.Text.Json;
using System.Text.Json.Serialization;

namespace YesZ.Gltf;

public class GltfDocument
{
    [JsonPropertyName("asset")]
    public GltfAsset? Asset { get; set; }

    [JsonPropertyName("scene")]
    public int? Scene { get; set; }

    [JsonPropertyName("scenes")]
    public GltfScene[]? Scenes { get; set; }

    [JsonPropertyName("nodes")]
    public GltfNode[]? Nodes { get; set; }

    [JsonPropertyName("meshes")]
    public GltfMesh[]? Meshes { get; set; }

    [JsonPropertyName("accessors")]
    public GltfAccessor[]? Accessors { get; set; }

    [JsonPropertyName("bufferViews")]
    public GltfBufferView[]? BufferViews { get; set; }

    [JsonPropertyName("buffers")]
    public GltfBuffer[]? Buffers { get; set; }

    [JsonPropertyName("materials")]
    public GltfMaterial[]? Materials { get; set; }

    [JsonPropertyName("textures")]
    public GltfTexture[]? Textures { get; set; }

    [JsonPropertyName("images")]
    public GltfImage[]? Images { get; set; }

    [JsonPropertyName("samplers")]
    public GltfSampler[]? Samplers { get; set; }

    /// <summary>
    /// Deserialize a glTF JSON string into a document.
    /// </summary>
    public static GltfDocument Deserialize(string json)
    {
        return JsonSerializer.Deserialize<GltfDocument>(json)
               ?? throw new InvalidOperationException("Failed to deserialize glTF JSON.");
    }
}

public class GltfAsset
{
    [JsonPropertyName("generator")]
    public string? Generator { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

public class GltfScene
{
    [JsonPropertyName("nodes")]
    public int[]? Nodes { get; set; }
}

public class GltfNode
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("mesh")]
    public int? Mesh { get; set; }

    [JsonPropertyName("children")]
    public int[]? Children { get; set; }

    [JsonPropertyName("matrix")]
    public float[]? Matrix { get; set; }

    [JsonPropertyName("translation")]
    public float[]? Translation { get; set; }

    [JsonPropertyName("rotation")]
    public float[]? Rotation { get; set; }

    [JsonPropertyName("scale")]
    public float[]? Scale { get; set; }
}

public class GltfMesh
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("primitives")]
    public GltfMeshPrimitive[]? Primitives { get; set; }
}

public class GltfMeshPrimitive
{
    [JsonPropertyName("attributes")]
    public Dictionary<string, int>? Attributes { get; set; }

    [JsonPropertyName("indices")]
    public int? Indices { get; set; }

    [JsonPropertyName("material")]
    public int? Material { get; set; }

    [JsonPropertyName("mode")]
    public int? Mode { get; set; }
}

public class GltfAccessor
{
    [JsonPropertyName("bufferView")]
    public int? BufferView { get; set; }

    [JsonPropertyName("byteOffset")]
    public int? ByteOffset { get; set; }

    [JsonPropertyName("componentType")]
    public int ComponentType { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("max")]
    public float[]? Max { get; set; }

    [JsonPropertyName("min")]
    public float[]? Min { get; set; }
}

public class GltfBufferView
{
    [JsonPropertyName("buffer")]
    public int Buffer { get; set; }

    [JsonPropertyName("byteOffset")]
    public int? ByteOffset { get; set; }

    [JsonPropertyName("byteLength")]
    public int ByteLength { get; set; }

    [JsonPropertyName("byteStride")]
    public int? ByteStride { get; set; }

    [JsonPropertyName("target")]
    public int? Target { get; set; }
}

public class GltfBuffer
{
    [JsonPropertyName("byteLength")]
    public int ByteLength { get; set; }
}

public class GltfMaterial
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("pbrMetallicRoughness")]
    public GltfPbrMetallicRoughness? PbrMetallicRoughness { get; set; }
}

public class GltfPbrMetallicRoughness
{
    [JsonPropertyName("baseColorFactor")]
    public float[]? BaseColorFactor { get; set; }

    [JsonPropertyName("baseColorTexture")]
    public GltfTextureInfo? BaseColorTexture { get; set; }

    [JsonPropertyName("metallicFactor")]
    public float? MetallicFactor { get; set; }

    [JsonPropertyName("roughnessFactor")]
    public float? RoughnessFactor { get; set; }
}

public class GltfTextureInfo
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("texCoord")]
    public int? TexCoord { get; set; }
}

public class GltfTexture
{
    [JsonPropertyName("sampler")]
    public int? Sampler { get; set; }

    [JsonPropertyName("source")]
    public int? Source { get; set; }
}

public class GltfImage
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("bufferView")]
    public int? BufferView { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }
}

public class GltfSampler
{
    [JsonPropertyName("magFilter")]
    public int? MagFilter { get; set; }

    [JsonPropertyName("minFilter")]
    public int? MinFilter { get; set; }

    [JsonPropertyName("wrapS")]
    public int? WrapS { get; set; }

    [JsonPropertyName("wrapT")]
    public int? WrapT { get; set; }
}
