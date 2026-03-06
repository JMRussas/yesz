//  YesZ - glTF Mesh Extractor
//
//  Extracts vertex and index data from a glTF mesh primitive into
//  MeshVertex3D[] (or SkinnedMeshVertex3D[]) and ushort[] arrays.
//
//  Returns raw arrays (no GPU upload) — caller does Mesh3D.Create() or
//  SkinnedMesh3D.Create() with the results.
//
//  Handles missing NORMAL (generates flat normals) and missing TEXCOORD_0
//  (defaults to zero). Vertex colors default to white.
//  Skinned path reads JOINTS_0 (ubyte/ushort) and WEIGHTS_0 (float),
//  normalizing weights to sum to 1.0.
//
//  Depends on: YesZ.Gltf (GltfDocument, GltfMeshPrimitive, AccessorReader),
//              YesZ (MeshVertex3D, SkinnedMeshVertex3D, JointIndices4),
//              NoZ (Color), System.Numerics
//  Used by:    GltfLoader, MeshExtractorTests, SkinDataExtractionTests

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using NoZ;

namespace YesZ.Gltf;

/// <summary>
/// Result of extracting a mesh primitive: vertices + indices ready for GPU upload.
/// </summary>
public readonly record struct ExtractedMesh(MeshVertex3D[] Vertices, ushort[] Indices);

/// <summary>
/// Result of extracting a skinned mesh primitive: skinned vertices + indices.
/// </summary>
public readonly record struct ExtractedSkinnedMesh(SkinnedMeshVertex3D[] Vertices, ushort[] Indices);

/// <summary>
/// 4× ushort for reading JOINTS_0 with UNSIGNED_SHORT component type.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct UShort4
{
    public ushort X, Y, Z, W;
}

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
    /// Extract skinned vertex and index data from a glTF mesh primitive.
    /// Reads JOINTS_0 and WEIGHTS_0 in addition to the standard attributes.
    /// Weights are normalized to sum to 1.0.
    /// </summary>
    public static ExtractedSkinnedMesh ExtractSkinnedPrimitive(
        GltfMeshPrimitive primitive, AccessorReader reader, GltfDocument doc)
    {
        // Extract base mesh data using the same logic as unskinned path
        var baseMesh = ExtractPrimitive(primitive, reader);
        int vertexCount = baseMesh.Vertices.Length;

        // Read JOINTS_0 — ubyte4 or ushort4, convert to JointIndices4 (byte4)
        JointIndices4[] joints;
        if (primitive.Attributes!.TryGetValue("JOINTS_0", out int jointsIdx))
        {
            joints = ReadJointIndices(reader, doc, jointsIdx);
        }
        else
        {
            joints = new JointIndices4[vertexCount]; // All zeros
        }

        // Read WEIGHTS_0 — float4, normalize to sum = 1.0
        Vector4[] weights;
        if (primitive.Attributes.TryGetValue("WEIGHTS_0", out int weightsIdx))
        {
            weights = reader.Read<Vector4>(weightsIdx);
            NormalizeWeights(weights);
        }
        else
        {
            // Default: all weight on joint 0
            weights = new Vector4[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                weights[i] = new Vector4(1, 0, 0, 0);
        }

        // Assemble skinned vertices
        var vertices = new SkinnedMeshVertex3D[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            var bv = baseMesh.Vertices[i];
            vertices[i] = new SkinnedMeshVertex3D
            {
                Position = bv.Position,
                Normal = bv.Normal,
                UV = bv.UV,
                Color = bv.Color,
                Joints = joints[i],
                JointWeights = weights[i],
            };
        }

        return new ExtractedSkinnedMesh(vertices, baseMesh.Indices);
    }

    /// <summary>
    /// Read JOINTS_0 accessor data as JointIndices4[].
    /// Handles UNSIGNED_BYTE (5121) and UNSIGNED_SHORT (5123) component types.
    /// </summary>
    internal static JointIndices4[] ReadJointIndices(
        AccessorReader reader, GltfDocument doc, int accessorIndex)
    {
        var accessor = doc.Accessors![accessorIndex];
        return accessor.ComponentType switch
        {
            5121 => reader.Read<JointIndices4>(accessorIndex), // UNSIGNED_BYTE — direct 4-byte read
            5123 => ConvertUShortJoints(reader, accessorIndex),
            _ => throw new NotSupportedException(
                $"JOINTS_0 component type {accessor.ComponentType} not supported. " +
                "Expected UNSIGNED_BYTE (5121) or UNSIGNED_SHORT (5123).")
        };
    }

    private static JointIndices4[] ConvertUShortJoints(AccessorReader reader, int accessorIndex)
    {
        var ushortJoints = reader.Read<UShort4>(accessorIndex);
        var result = new JointIndices4[ushortJoints.Length];
        for (int i = 0; i < ushortJoints.Length; i++)
        {
            if (ushortJoints[i].X > 255 || ushortJoints[i].Y > 255 ||
                ushortJoints[i].Z > 255 || ushortJoints[i].W > 255)
                throw new InvalidOperationException(
                    $"Joint index at vertex {i} exceeds 255 (UByte max). " +
                    "Skeletons with >256 joints are not supported.");

            result[i] = new JointIndices4
            {
                Joint0 = (byte)ushortJoints[i].X,
                Joint1 = (byte)ushortJoints[i].Y,
                Joint2 = (byte)ushortJoints[i].Z,
                Joint3 = (byte)ushortJoints[i].W,
            };
        }
        return result;
    }

    /// <summary>
    /// Normalize joint weights so each vertex's weights sum to 1.0.
    /// </summary>
    public static void NormalizeWeights(Vector4[] weights)
    {
        for (int i = 0; i < weights.Length; i++)
        {
            float sum = weights[i].X + weights[i].Y + weights[i].Z + weights[i].W;
            if (sum > 1e-6f && MathF.Abs(sum - 1.0f) > 1e-6f)
            {
                weights[i] /= sum;
            }
            else if (sum <= 1e-6f)
            {
                // Zero weights — default to all weight on joint 0
                weights[i] = new Vector4(1, 0, 0, 0);
            }
        }
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
