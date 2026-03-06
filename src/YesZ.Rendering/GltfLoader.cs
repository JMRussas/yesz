//  YesZ - glTF Model Loader
//
//  High-level API: .glb bytes → Model3D with GPU-ready meshes, materials,
//  textures, and node hierarchy. Requires Graphics3D to be initialized
//  before calling Load().
//
//  Texture resolution: material → texture → image → bufferView → BIN chunk
//  → StbImageSharp decode → Graphics.Driver.CreateTexture().
//
//  Node hierarchy: resolves default scene → root nodes → recursive children,
//  composing TRS/matrix transforms per node via NodeTransformResolver.
//
//  Depends on: YesZ.Gltf (GlbReader, GltfDocument, AccessorReader, MeshExtractor, NodeTransformResolver),
//              YesZ.Rendering (Model3D, MeshGroup, ModelNode, Material3D, Graphics3D),
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

        // Build mesh groups (all primitives per mesh)
        var meshGroups = BuildMeshGroups(doc, reader, materials);

        // Build node hierarchy from default scene
        var root = BuildNodeHierarchy(doc, meshGroups.Length);

        // Collect owned texture handles for disposal
        var ownedTextures = new nuint[textures.Count];
        textures.Values.CopyTo(ownedTextures, 0);

        return new Model3D(root, meshGroups, materials, ownedTextures);
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

    private static MeshGroup[] BuildMeshGroups(GltfDocument doc, AccessorReader reader, Material3D[] materials)
    {
        if (doc.Meshes == null || doc.Meshes.Length == 0)
            return [];

        int defaultMaterialIdx = materials.Length - 1;
        var groups = new MeshGroup[doc.Meshes.Length];

        for (int m = 0; m < doc.Meshes.Length; m++)
        {
            var gltfMesh = doc.Meshes[m];
            if (gltfMesh.Primitives == null || gltfMesh.Primitives.Length == 0)
            {
                groups[m] = new MeshGroup([]);
                continue;
            }

            var primitives = new ModelMesh[gltfMesh.Primitives.Length];
            for (int p = 0; p < gltfMesh.Primitives.Length; p++)
            {
                var prim = gltfMesh.Primitives[p];
                var extracted = MeshExtractor.ExtractPrimitive(prim, reader);
                var mesh = Mesh3D.Create(extracted.Vertices, extracted.Indices);

                int materialIdx = prim.Material ?? defaultMaterialIdx;
                if (materialIdx < 0 || materialIdx >= materials.Length)
                    materialIdx = defaultMaterialIdx;

                primitives[p] = new ModelMesh(mesh, materialIdx);
            }

            groups[m] = new MeshGroup(primitives);
        }

        return groups;
    }

    private static ModelNode BuildNodeHierarchy(GltfDocument doc, int meshGroupCount)
    {
        // Determine root node indices from default scene
        int[] rootIndices;
        if (doc.Scenes != null && doc.Scenes.Length > 0)
        {
            int sceneIdx = doc.Scene ?? 0;
            if (sceneIdx < 0 || sceneIdx >= doc.Scenes.Length)
                sceneIdx = 0;
            rootIndices = doc.Scenes[sceneIdx].Nodes ?? [];
        }
        else
        {
            rootIndices = [];
        }

        // Track visited nodes to detect cycles (malformed glTF)
        var visited = new HashSet<int>();

        // Build child nodes recursively
        var children = new ModelNode[rootIndices.Length];
        for (int i = 0; i < rootIndices.Length; i++)
        {
            children[i] = BuildNode(doc, rootIndices[i], meshGroupCount, visited);
        }

        // Synthetic root node (identity transform, no mesh, scene root nodes as children)
        return new ModelNode(Matrix4x4.Identity, -1, children);
    }

    private static ModelNode BuildNode(GltfDocument doc, int nodeIndex, int meshGroupCount, HashSet<int> visited)
    {
        if (doc.Nodes == null || nodeIndex < 0 || nodeIndex >= doc.Nodes.Length)
            return new ModelNode(Matrix4x4.Identity, -1, []);

        // Cycle detection: skip nodes already in the current traversal path
        if (!visited.Add(nodeIndex))
            return new ModelNode(Matrix4x4.Identity, -1, []);

        var gltfNode = doc.Nodes[nodeIndex];
        var localTransform = NodeTransformResolver.ResolveLocalTransform(gltfNode);

        int meshGroupIdx = gltfNode.Mesh ?? -1;
        if (meshGroupIdx >= meshGroupCount)
            meshGroupIdx = -1;

        // Recurse children
        ModelNode[] children;
        if (gltfNode.Children != null && gltfNode.Children.Length > 0)
        {
            children = new ModelNode[gltfNode.Children.Length];
            for (int i = 0; i < gltfNode.Children.Length; i++)
            {
                children[i] = BuildNode(doc, gltfNode.Children[i], meshGroupCount, visited);
            }
        }
        else
        {
            children = [];
        }

        return new ModelNode(localTransform, meshGroupIdx, children);
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
