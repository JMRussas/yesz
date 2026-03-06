//  YesZ - Joint Matrix Computer
//
//  Computes final joint matrices from skeleton hierarchy and per-joint
//  local transforms. Output matrices are ready for GPU upload (skinning).
//
//  jointMatrix[j] = inverseBindMatrix[j] * globalTransform[j]
//  globalTransform[j] = localTransform[j] * globalTransform[parent[j]]
//
//  Processes joints in topological order (parents before children) regardless
//  of the order in the skeleton's joints array.
//
//  Depends on: YesZ (Skeleton3D), System.Numerics
//  Used by:    AnimationPlayer3D (Phase 5c), JointMatrixTests

using System;
using System.Numerics;

namespace YesZ;

public static class JointMatrixComputer
{
    /// <summary>
    /// Compute final joint matrices for GPU skinning.
    /// </summary>
    /// <param name="skeleton">The skeleton hierarchy.</param>
    /// <param name="localPoses">Per-joint local transforms (length = JointCount).</param>
    /// <param name="jointMatrices">Output buffer for final joint matrices (length >= JointCount).</param>
    public static void Compute(Skeleton3D skeleton, ReadOnlySpan<Matrix4x4> localPoses, Span<Matrix4x4> jointMatrices)
    {
        int count = skeleton.JointCount;

        // Compute global transforms in topological order (parents before children)
        Span<Matrix4x4> globals = count <= 64
            ? stackalloc Matrix4x4[count]
            : new Matrix4x4[count];

        // Build processing order: topological sort via parent indices
        Span<int> order = count <= 64
            ? stackalloc int[count]
            : new int[count];
        TopologicalSort(skeleton.ParentIndices, order, count);

        for (int i = 0; i < count; i++)
        {
            int j = order[i];
            int parent = skeleton.ParentIndices[j];
            if (parent < 0)
            {
                globals[j] = localPoses[j];
            }
            else
            {
                // Row-vector convention: global = local * parent's global
                globals[j] = localPoses[j] * globals[parent];
            }
        }

        // Apply inverse bind matrices
        for (int j = 0; j < count; j++)
        {
            jointMatrices[j] = skeleton.InverseBindMatrices[j] * globals[j];
        }
    }

    /// <summary>
    /// Topological sort: outputs joint indices so parents always come before children.
    /// Uses iterative approach — roots first, then their children, etc.
    /// </summary>
    private static void TopologicalSort(int[] parentIndices, Span<int> order, int count)
    {
        // Count children per joint to detect processing completeness
        Span<bool> processed = count <= 64
            ? stackalloc bool[count]
            : new bool[count];

        int written = 0;

        // First pass: add all roots (parent == -1)
        for (int j = 0; j < count; j++)
        {
            if (parentIndices[j] < 0)
            {
                order[written++] = j;
                processed[j] = true;
            }
        }

        // Subsequent passes: add joints whose parent is already processed
        // Max depth = count (degenerate chain), typically 2-3 passes
        while (written < count)
        {
            int prevWritten = written;
            for (int j = 0; j < count; j++)
            {
                if (processed[j]) continue;
                int parent = parentIndices[j];
                if (parent >= 0 && parent < count && processed[parent])
                {
                    order[written++] = j;
                    processed[j] = true;
                }
            }

            // Safety: if no progress, remaining joints have invalid parents — add them as roots
            if (written == prevWritten)
            {
                for (int j = 0; j < count; j++)
                {
                    if (!processed[j])
                    {
                        order[written++] = j;
                        processed[j] = true;
                    }
                }
            }
        }
    }
}
