//  YesZ - Normal Matrix Computation Tests
//
//  Verifies ComputeNormalMatrix produces correct inverse-transpose
//  for identity, rotation, non-uniform scale, and singular matrices.
//
//  Depends on: YesZ.Rendering (Graphics3D.ComputeNormalMatrix)
//  Used by:    CI

using System.Numerics;
using Xunit;

namespace YesZ.Rendering.Tests;

public class NormalMatrixTests
{
    private const float Epsilon = 1e-5f;

    [Fact]
    public void Identity_ProducesIdentity()
    {
        var identity = Matrix4x4.Identity;
        var result = Graphics3D.ComputeNormalMatrix(in identity);
        AssertMatrixEqual(Matrix4x4.Identity, result, Epsilon);
    }

    [Fact]
    public void RotationOnly_EqualsModelMatrix()
    {
        // For pure rotation, inverse-transpose = original matrix
        var rotation = Matrix4x4.CreateRotationY(MathF.PI / 4);
        var result = Graphics3D.ComputeNormalMatrix(in rotation);
        AssertMatrixEqual(rotation, result, Epsilon);
    }

    [Fact]
    public void NonUniformScale_DiffersFromModelMatrix()
    {
        // Non-uniform scale (2,1,1) — the normal matrix must differ from the model matrix.
        // This is the entire reason normal matrices exist.
        var model = Matrix4x4.CreateScale(2, 1, 1);
        var result = Graphics3D.ComputeNormalMatrix(in model);

        // Normal matrix should NOT equal the model matrix for non-uniform scale
        Assert.False(MatricesEqual(model, result, Epsilon),
            "Normal matrix should differ from model matrix for non-uniform scale");

        // For Scale(2,1,1), the inverse-transpose should have Scale(0.5, 1, 1)
        var expected = Matrix4x4.CreateScale(0.5f, 1, 1);
        AssertMatrixEqual(expected, result, Epsilon);
    }

    [Fact]
    public void SingularMatrix_FallsBackToModel()
    {
        // A singular matrix (all zeros) can't be inverted — should fall back to model
        var singular = new Matrix4x4();
        var result = Graphics3D.ComputeNormalMatrix(in singular);
        AssertMatrixEqual(singular, result, Epsilon);
    }

    private static void AssertMatrixEqual(Matrix4x4 expected, Matrix4x4 actual, float epsilon)
    {
        Assert.True(MatricesEqual(expected, actual, epsilon),
            $"Matrices differ.\nExpected:\n{FormatMatrix(expected)}\nActual:\n{FormatMatrix(actual)}");
    }

    private static bool MatricesEqual(Matrix4x4 a, Matrix4x4 b, float epsilon)
    {
        return MathF.Abs(a.M11 - b.M11) < epsilon && MathF.Abs(a.M12 - b.M12) < epsilon
            && MathF.Abs(a.M13 - b.M13) < epsilon && MathF.Abs(a.M14 - b.M14) < epsilon
            && MathF.Abs(a.M21 - b.M21) < epsilon && MathF.Abs(a.M22 - b.M22) < epsilon
            && MathF.Abs(a.M23 - b.M23) < epsilon && MathF.Abs(a.M24 - b.M24) < epsilon
            && MathF.Abs(a.M31 - b.M31) < epsilon && MathF.Abs(a.M32 - b.M32) < epsilon
            && MathF.Abs(a.M33 - b.M33) < epsilon && MathF.Abs(a.M34 - b.M34) < epsilon
            && MathF.Abs(a.M41 - b.M41) < epsilon && MathF.Abs(a.M42 - b.M42) < epsilon
            && MathF.Abs(a.M43 - b.M43) < epsilon && MathF.Abs(a.M44 - b.M44) < epsilon;
    }

    private static string FormatMatrix(Matrix4x4 m)
    {
        return $"[{m.M11:F4}, {m.M12:F4}, {m.M13:F4}, {m.M14:F4}]\n"
             + $"[{m.M21:F4}, {m.M22:F4}, {m.M23:F4}, {m.M24:F4}]\n"
             + $"[{m.M31:F4}, {m.M32:F4}, {m.M33:F4}, {m.M34:F4}]\n"
             + $"[{m.M41:F4}, {m.M42:F4}, {m.M43:F4}, {m.M44:F4}]";
    }
}
