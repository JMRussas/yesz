//  YesZ - glTF Mesh Extractor
//
//  Extracts vertex and index data from a glTF mesh primitive into
//  MeshVertex3D[] and ushort[] arrays suitable for Mesh3D.Create().
//
//  Returns raw arrays (no GPU upload) — Phase 4a is pure data.
//  Phase 4b's GltfLoader will call Mesh3D.Create() with the results.
//
//  Handles missing NORMAL (generates flat normals) and missing TEXCOORD_0
//  (defaults to zero). Vertex colors default to white.
//
//  Depends on: YesZ.Gltf (GltfDocument, GltfMeshPrimitive, AccessorReader),
//              YesZ (MeshVertex3D), NoZ (Color), System.Numerics
//  Used by:    GltfLoader (Phase 4b), MeshExtractorTests

using System;
using System.Numerics;
using NoZ;

namespace YesZ.Gltf;

/// <summary>
/// Result of extracting a mesh primitive: vertices + indices ready for GPU upload.
/// </summary>
public readonly record struct ExtractedMesh(MeshVertex3D[] Vertices, ushort[] Indices);

public static class MeshExtractor
{
    /// <summary>
    /// Extract vertex and index data from a glTF mesh primitive.
    /// </summary>
    public static ExtractedMesh ExtractPrimitive(GltfMeshPrimitive primitive, AccessorReader reader)
    {
        if (primitive.Attributes == null)
            throw new InvalidOperationException("Mesh primitive has no attributes.");

        // Only TRIANGLES (mode 4, or default when omitted) is supported
        int mode = primitive.Mode ?? 4;
        if (mode != 4)
            throw new NotSupportedException(
                $"Primitive mode {mode} is not supported. Only TRIANGLES (4) is supported.");

        if (!primitive.Attributes.TryGetValue("POSITION", out int posIdx))
            throw new InvalidOperationException("Mesh primitive has no POSITION attribute.");

        var positions = reader.Read<Vector3>(posIdx);

        // Indices — required for indexed geometry, generate sequential if missing
        ushort[] indices;
        if (primitive.Indices.HasValue)
        {
            indices = reader.ReadIndices(primitive.Indices.Value);
        }
        else
        {
            if (positions.Length > ushort.MaxValue)
                throw new InvalidOperationException(
                    $"Non-indexed primitive has {positions.Length} vertices, exceeding ushort.MaxValue (65535).");

            indices = new ushort[positions.Length];
            for (int i = 0; i < positions.Length; i++)
                indices[i] = (ushort)i;
        }

        // Normals — optional, generate flat normals from triangles if missing
        Vector3[] normals;
        if (primitive.Attributes.TryGetValue("NORMAL", out int normIdx))
        {
            normals = reader.Read<Vector3>(normIdx);
        }
        else
        {
            normals = GenerateFlatNormals(positions, indices);
        }

        // UVs — optional, default to (0, 0) if missing
        Vector2[] uvs;
        if (primitive.Attributes.TryGetValue("TEXCOORD_0", out int uvIdx))
        {
            uvs = reader.Read<Vector2>(uvIdx);
        }
        else
        {
            uvs = new Vector2[positions.Length];
        }

        // Build MeshVertex3D array
        var vertices = new MeshVertex3D[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            vertices[i] = new MeshVertex3D
            {
                Position = positions[i],
                Normal = normals[i],
                UV = uvs[i],
                Color = Color.White,
            };
        }

        return new ExtractedMesh(vertices, indices);
    }

    /// <summary>
    /// Generate normals from triangle vertex positions and indices.
    /// Accumulates face normals at shared vertices and normalizes, producing
    /// smooth normals. For non-shared vertices this is equivalent to flat normals.
    /// </summary>
    public static Vector3[] GenerateFlatNormals(Vector3[] positions, ushort[] indices)
    {
        var normals = new Vector3[positions.Length];

        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            var p0 = positions[indices[i]];
            var p1 = positions[indices[i + 1]];
            var p2 = positions[indices[i + 2]];

            var edge1 = p1 - p0;
            var edge2 = p2 - p0;
            var faceNormal = Vector3.Cross(edge1, edge2);

            // Skip degenerate triangles (zero-area → zero cross product)
            if (faceNormal.LengthSquared() < 1e-12f)
                continue;

            // Accumulate (unnormalized) face normal at each vertex
            normals[indices[i]] += faceNormal;
            normals[indices[i + 1]] += faceNormal;
            normals[indices[i + 2]] += faceNormal;
        }

        // Normalize accumulated normals; fallback to up vector for degenerate vertices
        for (int i = 0; i < normals.Length; i++)
        {
            if (normals[i].LengthSquared() < 1e-12f)
                normals[i] = Vector3.UnitY;
            else
                normals[i] = Vector3.Normalize(normals[i]);
        }

        return normals;
    }
}
