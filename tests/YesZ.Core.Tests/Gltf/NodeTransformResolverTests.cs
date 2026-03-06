//  YesZ - NodeTransformResolver Tests
//
//  Tests for glTF node transform resolution: TRS composition,
//  column-major matrix loading, and hierarchy composition.
//
//  Depends on: YesZ.Gltf (NodeTransformResolver, GltfNode), System.Numerics
//  Used by:    test runner

using System.Numerics;
using Xunit;
using YesZ.Gltf;

namespace YesZ.Core.Tests.Gltf;

public class NodeTransformResolverTests
{
    private const float Epsilon = 1e-5f;

    [Fact]
    public void ResolveLocal_IdentityTRS_ProducesIdentity()
    {
        var node = new GltfNode
        {
            Translation = [0, 0, 0],
            Rotation = [0, 0, 0, 1],
            Scale = [1, 1, 1],
        };

        var result = NodeTransformResolver.ResolveLocalTransform(node);

        AssertMatrixEqual(Matrix4x4.Identity, result);
    }

    [Fact]
    public void ResolveLocal_TranslationOnly_CorrectMatrix()
    {
        var node = new GltfNode
        {
            Translation = [1, 2, 3],
        };

        var result = NodeTransformResolver.ResolveLocalTransform(node);

        var expected = Matrix4x4.CreateTranslation(1, 2, 3);
        AssertMatrixEqual(expected, result);
    }

    [Fact]
    public void ResolveLocal_RotationOnly_CorrectMatrix()
    {
        // 90° around Y axis
        var q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        var node = new GltfNode
        {
            Rotation = [q.X, q.Y, q.Z, q.W],
        };

        var result = NodeTransformResolver.ResolveLocalTransform(node);

        var expected = Matrix4x4.CreateFromQuaternion(q);
        AssertMatrixEqual(expected, result);
    }

    [Fact]
    public void ResolveLocal_ColumnMajorMatrix_LoadedCorrectly()
    {
        // Use a scale(2,1,1) + translation(5,6,7) matrix — asymmetric, so
        // sequential vs transposed loading produces different results.
        // Column-major layout: col0=(2,0,0,0), col1=(0,1,0,0), col2=(0,0,1,0), col3=(5,6,7,1)
        var node = new GltfNode
        {
            Matrix =
            [
                2, 0, 0, 0,  // col 0
                0, 1, 0, 0,  // col 1
                0, 0, 1, 0,  // col 2
                5, 6, 7, 1,  // col 3
            ],
        };

        var result = NodeTransformResolver.ResolveLocalTransform(node);

        // In row-vector System.Numerics: S * T = CreateScale(2,1,1) * CreateTranslation(5,6,7)
        var expected = Matrix4x4.CreateScale(2, 1, 1) * Matrix4x4.CreateTranslation(5, 6, 7);
        AssertMatrixEqual(expected, result);

        // Verify asymmetric element: M11=2 (scale X), M41=5 (translation X)
        Assert.InRange(result.M11, 2 - Epsilon, 2 + Epsilon);
        Assert.InRange(result.M41, 5 - Epsilon, 5 + Epsilon);
        // If incorrectly transposed, M11 would be 2 but M14 would be 5 (wrong)
        Assert.InRange(result.M14, -Epsilon, Epsilon);
    }

    [Fact]
    public void ResolveLocal_TRS_OrderIsScaleRotateTranslate()
    {
        // Scale by 2, rotate 90° around Y, translate (1,0,0)
        // Expected: S * R * T (System.Numerics row-vector convention)
        var q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        var node = new GltfNode
        {
            Translation = [1, 0, 0],
            Rotation = [q.X, q.Y, q.Z, q.W],
            Scale = [2, 2, 2],
        };

        var result = NodeTransformResolver.ResolveLocalTransform(node);

        var expected = Matrix4x4.CreateScale(2)
                     * Matrix4x4.CreateFromQuaternion(q)
                     * Matrix4x4.CreateTranslation(1, 0, 0);
        AssertMatrixEqual(expected, result);
    }

    [Fact]
    public void ResolveLocal_NoTRSOrMatrix_ReturnsIdentity()
    {
        var node = new GltfNode();

        var result = NodeTransformResolver.ResolveLocalTransform(node);

        AssertMatrixEqual(Matrix4x4.Identity, result);
    }

    [Fact]
    public void HierarchyComposition_ChildWorld_IsLocalTimesParent()
    {
        // Parent at (10, 0, 0), child at (0, 5, 0) local
        var parentNode = new GltfNode { Translation = [10, 0, 0] };
        var childNode = new GltfNode { Translation = [0, 5, 0] };

        var parentLocal = NodeTransformResolver.ResolveLocalTransform(parentNode);
        var childLocal = NodeTransformResolver.ResolveLocalTransform(childNode);

        // Child world = childLocal * parentWorld (row-vector convention)
        var childWorld = childLocal * parentLocal;

        // Expected position: (10, 5, 0) — translation is additive
        Assert.InRange(childWorld.M41, 10 - Epsilon, 10 + Epsilon);
        Assert.InRange(childWorld.M42, 5 - Epsilon, 5 + Epsilon);
        Assert.InRange(childWorld.M43, -Epsilon, Epsilon);
    }

    [Fact]
    public void HierarchyComposition_ThreeLevels_ComposesCorrectly()
    {
        var root = new GltfNode { Translation = [1, 0, 0] };
        var child = new GltfNode { Translation = [0, 2, 0] };
        var grandchild = new GltfNode { Translation = [0, 0, 3] };

        var rootLocal = NodeTransformResolver.ResolveLocalTransform(root);
        var childLocal = NodeTransformResolver.ResolveLocalTransform(child);
        var grandchildLocal = NodeTransformResolver.ResolveLocalTransform(grandchild);

        var childWorld = childLocal * rootLocal;
        var grandchildWorld = grandchildLocal * childWorld;

        // Expected position: (1, 2, 3)
        Assert.InRange(grandchildWorld.M41, 1 - Epsilon, 1 + Epsilon);
        Assert.InRange(grandchildWorld.M42, 2 - Epsilon, 2 + Epsilon);
        Assert.InRange(grandchildWorld.M43, 3 - Epsilon, 3 + Epsilon);
    }

    private static void AssertMatrixEqual(Matrix4x4 expected, Matrix4x4 actual)
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
