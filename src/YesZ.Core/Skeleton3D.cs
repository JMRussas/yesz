//  YesZ - 3D Skeleton
//
//  Represents a skeleton hierarchy for skeletal animation.
//  Stores joint parent indices, inverse bind matrices, and
//  joint-to-node mapping from glTF skin data.
//
//  Depends on: System.Numerics
//  Used by:    SkeletonParser, JointMatrixComputer, AnimationPlayer3D (Phase 5c)

using System.Numerics;

namespace YesZ;

/// <summary>
/// Skeleton joint hierarchy for skeletal animation.
/// Joint indices are 0-based in the order defined by the glTF skin's joints array.
/// </summary>
public class Skeleton3D
{
    /// <summary>Number of joints in this skeleton.</summary>
    public int JointCount { get; }

    /// <summary>
    /// Parent index for each joint (-1 for root joints).
    /// Length = JointCount.
    /// </summary>
    public int[] ParentIndices { get; }

    /// <summary>
    /// Inverse bind matrix for each joint. Transforms from mesh space to joint-local space.
    /// Length = JointCount.
    /// </summary>
    public Matrix4x4[] InverseBindMatrices { get; }

    /// <summary>
    /// Maps joint index → glTF node index.
    /// Used to look up node transforms during animation.
    /// Length = JointCount.
    /// </summary>
    public int[] JointNodeIndices { get; }

    public Skeleton3D(int[] parentIndices, Matrix4x4[] inverseBindMatrices, int[] jointNodeIndices)
    {
        JointCount = parentIndices.Length;
        ParentIndices = parentIndices;
        InverseBindMatrices = inverseBindMatrices;
        JointNodeIndices = jointNodeIndices;
    }
}
