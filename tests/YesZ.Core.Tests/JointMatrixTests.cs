//  YesZ - JointMatrixComputer Tests
//
//  Tests for joint matrix computation: bind pose produces near-identity,
//  rotated joints propagate through hierarchy.
//
//  Depends on: YesZ (JointMatrixComputer, Skeleton3D), System.Numerics
//  Used by:    test runner

using System;
using System.Numerics;
using Xunit;

namespace YesZ.Tests;

public class JointMatrixTests
{
    private const float Epsilon = 1e-4f;

    /// <summary>
    /// Create a simple 2-joint skeleton: joint 0 = root, joint 1 = child.
    /// Both have identity IBMs (bind pose = identity).
    /// </summary>
    private static Skeleton3D CreateSimpleSkeleton()
    {
        return new Skeleton3D(
            parentIndices: [-1, 0],
            inverseBindMatrices: [Matrix4x4.Identity, Matrix4x4.Identity],
            jointNodeIndices: [0, 1]);
    }

    [Fact]
    public void Compute_BindPose_AllJointMatricesNearIdentity()
    {
        var skeleton = CreateSimpleSkeleton();
        var localPoses = new Matrix4x4[]
        {
            Matrix4x4.Identity,
            Matrix4x4.Identity,
        };
        var jointMatrices = new Matrix4x4[2];

        JointMatrixComputer.Compute(skeleton, localPoses, jointMatrices);

        AssertMatrixNearIdentity(jointMatrices[0]);
        AssertMatrixNearIdentity(jointMatrices[1]);
    }

    [Fact]
    public void Compute_RotatedJoint_ProducesExpectedTransform()
    {
        var skeleton = CreateSimpleSkeleton();
        var rotation90Y = Matrix4x4.CreateFromQuaternion(
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));

        var localPoses = new Matrix4x4[]
        {
            Matrix4x4.Identity,   // Root: no rotation
            rotation90Y,           // Child: 90° Y rotation
        };
        var jointMatrices = new Matrix4x4[2];

        JointMatrixComputer.Compute(skeleton, localPoses, jointMatrices);

        // Root should still be identity (IBM = identity, global = identity)
        AssertMatrixNearIdentity(jointMatrices[0]);

        // Child should reflect the 90° rotation
        // Joint matrix = IBM * global = Identity * (local * parent) = rotation90Y * Identity = rotation90Y
        AssertMatrixNear(rotation90Y, jointMatrices[1]);
    }

    [Fact]
    public void Compute_HierarchyOrder_ChildInheritsParent()
    {
        var skeleton = CreateSimpleSkeleton();
        var rotX = Matrix4x4.CreateFromQuaternion(
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 4));
        var rotY = Matrix4x4.CreateFromQuaternion(
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4));

        var localPoses = new Matrix4x4[]
        {
            rotX,   // Root: 45° X
            rotY,   // Child: 45° Y (local)
        };
        var jointMatrices = new Matrix4x4[2];

        JointMatrixComputer.Compute(skeleton, localPoses, jointMatrices);

        // Root: global = rotX, joint = IBM * global = Identity * rotX = rotX
        AssertMatrixNear(rotX, jointMatrices[0]);

        // Child: global = rotY * rotX (child local * parent global, row-vector)
        // Joint = IBM * global = Identity * (rotY * rotX) = rotY * rotX
        var expectedChild = rotY * rotX;
        AssertMatrixNear(expectedChild, jointMatrices[1]);
    }

    [Fact]
    public void Compute_WithIBM_BindPoseProducesIdentity()
    {
        // Root at translation (0, 2, 0), child at (0, 4, 0) global
        var rootGlobal = Matrix4x4.CreateTranslation(0, 2, 0);
        var childGlobal = Matrix4x4.CreateTranslation(0, 4, 0);

        // IBM = inverse of bind-pose global transform
        Matrix4x4.Invert(rootGlobal, out var rootIBM);
        Matrix4x4.Invert(childGlobal, out var childIBM);

        var skeleton = new Skeleton3D(
            parentIndices: [-1, 0],
            inverseBindMatrices: [rootIBM, childIBM],
            jointNodeIndices: [0, 1]);

        // Bind pose: root local = rootGlobal, child local = offset from parent
        var childLocal = Matrix4x4.CreateTranslation(0, 2, 0); // 2 units above root
        var localPoses = new Matrix4x4[] { rootGlobal, childLocal };
        var jointMatrices = new Matrix4x4[2];

        JointMatrixComputer.Compute(skeleton, localPoses, jointMatrices);

        // In bind pose: jointMatrix = IBM * globalTransform = inverse(bindGlobal) * bindGlobal = Identity
        AssertMatrixNearIdentity(jointMatrices[0]);
        AssertMatrixNearIdentity(jointMatrices[1]);
    }

    private static void AssertMatrixNearIdentity(Matrix4x4 m)
    {
        AssertMatrixNear(Matrix4x4.Identity, m);
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        Assert.InRange(actual.M11, expected.M11 - Epsilon, expected.M11 + Epsilon);
        Assert.InRange(actual.M12, expected.M12 - Epsilon, expected.M12 + Epsilon);
        Assert.InRange(actual.M13, expected.M13 - Epsilon, expected.M13 + Epsilon);
        Assert.InRange(actual.M14, expected.M14 - Epsilon, expected.M14 + Epsilon);
        Assert.InRange(actual.M21, expected.M21 - Epsilon, expected.M21 + Epsilon);
        Assert.InRange(actual.M22, expected.M22 - Epsilon, expected.M22 + Epsilon);
        Assert.InRange(actual.M23, expected.M23 - Epsilon, expected.M23 + Epsilon);
        Assert.InRange(actual.M24, expected.M24 - Epsilon, expected.M24 + Epsilon);
        Assert.InRange(actual.M31, expected.M31 - Epsilon, expected.M31 + Epsilon);
        Assert.InRange(actual.M32, expected.M32 - Epsilon, expected.M32 + Epsilon);
        Assert.InRange(actual.M33, expected.M33 - Epsilon, expected.M33 + Epsilon);
        Assert.InRange(actual.M34, expected.M34 - Epsilon, expected.M34 + Epsilon);
        Assert.InRange(actual.M41, expected.M41 - Epsilon, expected.M41 + Epsilon);
        Assert.InRange(actual.M42, expected.M42 - Epsilon, expected.M42 + Epsilon);
        Assert.InRange(actual.M43, expected.M43 - Epsilon, expected.M43 + Epsilon);
        Assert.InRange(actual.M44, expected.M44 - Epsilon, expected.M44 + Epsilon);
    }
}
