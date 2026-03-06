//  YesZ - glTF Model Loader
//
//  High-level API: .glb bytes → Model3D with GPU-ready meshes, materials,
//  and textures. Requires Graphics3D to be initialized before calling Load().
//
//  Texture resolution: material → texture → image → bufferView → BIN chunk
//  → StbImageSharp decode → Graphics.Driver.CreateTexture().
//
//  Depends on: YesZ.Gltf (GlbReader, GltfDocument, AccessorReader, MeshExtractor),
//              YesZ.Rendering (Model3D, Material3D, TextureLoader, Graphics3D),
//              YesZ (Mesh3D), NoZ (Graphics, TextureFilter), StbImageSharp
//  Used by:    Game code, samples

using System;
using System.Collections.Generic;
using System.Numerics;
using NoZ;
using StbImageSharp;
using YesZ.Gltf;

namespace YesZ.Rendering;

public static class GltfLoader
{
    /// <summary>
    /// Load a .glb file from raw bytes into a GPU-ready Model3D.
    /// Graphics3D must be initialized before calling this.
    /// </summary>
    public static Model3D Load(byte[] glbData)
    {
        var glb = GlbReader.Parse(glbData);
        var doc = GltfDocument.Deserialize(glb.Json);
        var reader = new AccessorReader(doc, glb.BinChunk);

        // Load textures (deduplicated by image index)
        var textures = LoadTextures(doc, glb.BinChunk);

        // Load materials
        var materials = LoadMaterials(doc, textures);

        // Extract meshes (first primitive per mesh for Phase 4b)
        var meshEntries = new List<ModelMesh>();
        if (doc.Meshes != null)
        {
            foreach (var gltfMesh in doc.Meshes)
            {
                if (gltfMesh.Primitives == null || gltfMesh.Primitives.Length == 0)
                    continue;

                var prim = gltfMesh.Primitives[0];
                var extracted = MeshExtractor.ExtractPrimitive(prim, reader);
                var mesh = Mesh3D.Create(extracted.Vertices, extracted.Indices);
                int materialIdx = prim.Material ?? (materials.Length - 1);
                if (materialIdx < 0 || materialIdx >= materials.Length)
                    materialIdx = materials.Length - 1;
                meshEntries.Add(new ModelMesh(mesh, materialIdx));
            }
        }

        // Collect owned texture handles for disposal
        var ownedTextures = new nuint[textures.Count];
        textures.Values.CopyTo(ownedTextures, 0);

        return new Model3D(meshEntries.ToArray(), materials, ownedTextures);
    }

    /// <summary>
    /// Load a .glb file from an embedded resource into a GPU-ready Model3D.
    /// </summary>
    public static Model3D LoadFromEmbeddedResource(System.Reflection.Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Load(ms.ToArray());
    }

    private static Dictionary<int, nuint> LoadTextures(GltfDocument doc, byte[] binChunk)
    {
        var textureHandles = new Dictionary<int, nuint>();

        if (doc.Textures == null || doc.Images == null)
            return textureHandles;

        foreach (var texture in doc.Textures)
        {
            if (texture.Source == null)
                continue;

            int imageIndex = texture.Source.Value;
            if (textureHandles.ContainsKey(imageIndex))
                continue; // Already loaded (deduplication)

            if (imageIndex < 0 || imageIndex >= doc.Images.Length)
                continue;

            var image = doc.Images[imageIndex];
            if (image.BufferView == null)
                continue; // External URI images not supported

            if (doc.BufferViews == null)
                continue;

            int bvIndex = image.BufferView.Value;
            if (bvIndex < 0 || bvIndex >= doc.BufferViews.Length)
                continue;

            var view = doc.BufferViews[bvIndex];
            int offset = view.ByteOffset ?? 0;
            int length = view.ByteLength;

            // Decode image from BIN chunk bytes
            var imageData = ImageResult.FromMemory(
                binChunk.AsSpan(offset, length).ToArray(),
                ColorComponents.RedGreenBlueAlpha);

            var handle = Graphics.Driver.CreateTexture(
                imageData.Width,
                imageData.Height,
                imageData.Data,
                TextureFormat.RGBA8,
                TextureFilter.Linear,
                $"glTF_image_{imageIndex}");

            textureHandles[imageIndex] = handle;
        }

        return textureHandles;
    }

    private static Material3D[] LoadMaterials(GltfDocument doc, Dictionary<int, nuint> textures)
    {
        if (doc.Materials == null || doc.Materials.Length == 0)
        {
            // No materials — create a single default lit material
            return [Graphics3D.CreateLitMaterial()];
        }

        // Index 0..N-1 = authored materials, index N = default for unassigned primitives
        var materials = new Material3D[doc.Materials.Length + 1];
        for (int i = 0; i < doc.Materials.Length; i++)
        {
            var gltfMat = doc.Materials[i];
            var mat = Graphics3D.CreateLitMaterial();

            if (gltfMat.PbrMetallicRoughness != null)
            {
                var pbr = gltfMat.PbrMetallicRoughness;

                // Base color factor
                if (pbr.BaseColorFactor is { Length: 4 })
                {
                    mat.BaseColorFactor = new Vector4(
                        pbr.BaseColorFactor[0],
                        pbr.BaseColorFactor[1],
                        pbr.BaseColorFactor[2],
                        pbr.BaseColorFactor[3]);
                }

                // Metallic / roughness
                mat.Metallic = pbr.MetallicFactor ?? 1.0f;
                mat.Roughness = pbr.RoughnessFactor ?? 1.0f;

                // Base color texture
                if (pbr.BaseColorTexture != null && doc.Textures != null)
                {
                    int texIdx = pbr.BaseColorTexture.Index;
                    if (texIdx >= 0 && texIdx < doc.Textures.Length)
                    {
                        var tex = doc.Textures[texIdx];
                        if (tex.Source != null && textures.TryGetValue(tex.Source.Value, out nuint handle))
                        {
                            mat.BaseColorTexture = handle;
                        }
                    }
                }
            }

            materials[i] = mat;
        }

        // Default material at the end for primitives with no material assignment
        materials[doc.Materials.Length] = Graphics3D.CreateLitMaterial();

        return materials;
    }
}
