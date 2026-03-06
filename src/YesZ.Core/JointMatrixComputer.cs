//  YesZ - Joint Matrix Computer
//
//  Computes final joint matrices from skeleton hierarchy and per-joint
//  local transforms. Output matrices are ready for GPU upload (skinning).
//
//  jointMatrix[j] = globalTransform[j] * inverseBindMatrix[j]
//  globalTransform[j] = globalTransform[parent[j]] * localTransform[j]
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

        // First pass: compute global transforms
        // Joints must be ordered so that parents come before children
        // (glTF spec guarantees this for the joints array order)
        Span<Matrix4x4> globals = count <= 64
            ? stackalloc Matrix4x4[count]
            : new Matrix4x4[count];

        for (int j = 0; j < count; j++)
        {
            int parent = skeleton.ParentIndices[j];
            if (parent < 0)
            {
                // Root joint: global = local
                globals[j] = localPoses[j];
            }
            else
            {
                // Child joint: global = local * parent's global (row-vector convention)
                globals[j] = localPoses[j] * globals[parent];
            }
        }

        // Second pass: apply inverse bind matrices
        for (int j = 0; j < count; j++)
        {
            jointMatrices[j] = skeleton.InverseBindMatrices[j] * globals[j];
        }
    }
}
