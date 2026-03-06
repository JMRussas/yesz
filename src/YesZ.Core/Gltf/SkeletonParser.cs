//  YesZ - glTF Skeleton Parser
//
//  Converts a glTF skin + node hierarchy into a Skeleton3D.
//  Resolves joint parent indices from node children relationships,
//  reads inverse bind matrices from accessor data.
//
//  Depends on: YesZ.Gltf (GltfDocument, GltfSkin, AccessorReader),
//              YesZ (Skeleton3D), System.Numerics
//  Used by:    GltfLoader (Phase 5d), SkeletonParserTests

using System;
using System.Collections.Generic;
using System.Numerics;

namespace YesZ.Gltf;

public static class SkeletonParser
{
    /// <summary>
    /// Parse a glTF skin into a Skeleton3D.
    /// </summary>
    public static Skeleton3D Parse(GltfSkin skin, GltfDocument doc, AccessorReader reader)
    {
        int jointCount = skin.Joints.Length;
        if (jointCount == 0)
            throw new InvalidOperationException("Skin has no joints.");

        // Build joint-node-index → joint-index lookup
        var nodeToJoint = new Dictionary<int, int>(jointCount);
        for (int j = 0; j < jointCount; j++)
            nodeToJoint[skin.Joints[j]] = j;

        // Resolve parent indices from node hierarchy
        var parentIndices = ResolveParentIndices(skin, doc, nodeToJoint);

        // Read inverse bind matrices (column-major MAT4 → row-major Matrix4x4)
        var ibms = ReadInverseBindMatrices(skin, reader, jointCount);

        return new Skeleton3D(parentIndices, ibms, (int[])skin.Joints.Clone());
    }

    private static int[] ResolveParentIndices(GltfSkin skin, GltfDocument doc, Dictionary<int, int> nodeToJoint)
    {
        int jointCount = skin.Joints.Length;
        var parentIndices = new int[jointCount];
        Array.Fill(parentIndices, -1); // Default: root (no parent)

        if (doc.Nodes == null) return parentIndices;

        // For each joint node, check if any other joint node lists it as a child
        foreach (int jointNodeIdx in skin.Joints)
        {
            if (jointNodeIdx < 0 || jointNodeIdx >= doc.Nodes.Length)
                continue;

            var node = doc.Nodes[jointNodeIdx];
            if (node.Children == null) continue;

            foreach (int childNodeIdx in node.Children)
            {
                if (nodeToJoint.TryGetValue(childNodeIdx, out int childJointIdx))
                {
                    int parentJointIdx = nodeToJoint[jointNodeIdx];
                    parentIndices[childJointIdx] = parentJointIdx;
                }
            }
        }

        return parentIndices;
    }

    private static Matrix4x4[] ReadInverseBindMatrices(GltfSkin skin, AccessorReader reader, int jointCount)
    {
        if (!skin.InverseBindMatrices.HasValue)
        {
            // No IBMs specified — default to identity
            var identities = new Matrix4x4[jointCount];
            Array.Fill(identities, Matrix4x4.Identity);
            return identities;
        }

        // Read raw MAT4 data — stored column-major in glTF
        var rawMatrices = reader.Read<Matrix4x4>(skin.InverseBindMatrices.Value);
        if (rawMatrices.Length < jointCount)
            throw new InvalidOperationException(
                $"IBM accessor has {rawMatrices.Length} matrices but skin has {jointCount} joints.");

        // Column-major → row-major conversion: sequential loading is correct
        // (same convention cancellation as NodeTransformResolver)
        // Matrix4x4 memory layout matches glTF column-major storage directly.
        // No transpose needed.

        return rawMatrices;
    }
}
