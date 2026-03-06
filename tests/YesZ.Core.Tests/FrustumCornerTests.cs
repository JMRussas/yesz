//  YesZ - Frustum Corner Tests
//
//  Tests for Camera3D.GetFrustumCorners(): verifies that corners
//  are correctly computed in world space from the inverse VP matrix.
//
//  Depends on: YesZ (Camera3D), System.Numerics
//  Used by:    test runner

using System;
using System.Numerics;
using Xunit;

namespace YesZ.Tests;

public class FrustumCornerTests
{
    private const float Epsilon = 1e-3f;

    private static Camera3D CreateDefaultCamera() => new()
    {
        Position = Vector3.Zero,
        Rotation = Quaternion.Identity,
        FieldOfView = 90.0f,
        AspectRatio = 1.0f,
        NearPlane = 1.0f,
        FarPlane = 100.0f,
    };

    [Fact]
    public void Returns8Corners()
    {
        var cam = CreateDefaultCamera();
        var corners = cam.GetFrustumCorners(1f, 100f);
        Assert.Equal(8, corners.Length);
    }

    [Fact]
    public void NearCorners_AreAtNearDistance()
    {
        var cam = CreateDefaultCamera();
        var corners = cam.GetFrustumCorners(1f, 100f);

        // Camera at origin looking down -Z. Near plane at z = -1.
        for (int i = 0; i < 4; i++)
            Assert.InRange(corners[i].Z, -1 - Epsilon, -1 + Epsilon);
    }

    [Fact]
    public void FarCorners_AreAtFarDistance()
    {
        var cam = CreateDefaultCamera();
        var corners = cam.GetFrustumCorners(1f, 100f);

        // Far plane at z = -100
        for (int i = 4; i < 8; i++)
            Assert.InRange(corners[i].Z, -100 - Epsilon, -100 + Epsilon);
    }

    [Fact]
    public void NearCorners_90DegFov_FormSquareAtNear()
    {
        var cam = CreateDefaultCamera();
        var corners = cam.GetFrustumCorners(1f, 100f);

        // With 90° FOV and aspect=1, at z=-1, half-extent = tan(45°) * 1 = 1
        // Near corners should span from -1 to +1 in X and Y
        float maxX = float.MinValue, minX = float.MaxValue;
        float maxY = float.MinValue, minY = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            if (corners[i].X > maxX) maxX = corners[i].X;
            if (corners[i].X < minX) minX = corners[i].X;
            if (corners[i].Y > maxY) maxY = corners[i].Y;
            if (corners[i].Y < minY) minY = corners[i].Y;
        }

        Assert.InRange(maxX, 1 - Epsilon, 1 + Epsilon);
        Assert.InRange(minX, -1 - Epsilon, -1 + Epsilon);
        Assert.InRange(maxY, 1 - Epsilon, 1 + Epsilon);
        Assert.InRange(minY, -1 - Epsilon, -1 + Epsilon);
    }

    [Fact]
    public void FarCorners_90DegFov_ScaleWithDistance()
    {
        var cam = CreateDefaultCamera();
        var corners = cam.GetFrustumCorners(1f, 100f);

        // At z=-100, half-extent = tan(45°) * 100 = 100
        float maxX = float.MinValue;
        for (int i = 4; i < 8; i++)
            if (corners[i].X > maxX) maxX = corners[i].X;

        Assert.InRange(maxX, 100 - Epsilon, 100 + Epsilon);
    }

    [Fact]
    public void TranslatedCamera_OffsetsCornersInWorldSpace()
    {
        var cam = CreateDefaultCamera();
        cam.Position = new Vector3(10, 20, 30);
        var corners = cam.GetFrustumCorners(1f, 10f);

        // Center of frustum should be roughly at camera position shifted along -Z
        var center = Vector3.Zero;
        for (int i = 0; i < 8; i++) center += corners[i];
        center /= 8;

        Assert.InRange(center.X, 10 - Epsilon, 10 + Epsilon);
        Assert.InRange(center.Y, 20 - Epsilon, 20 + Epsilon);
    }

    [Fact]
    public void CustomNearFar_UseProvidedValues()
    {
        var cam = CreateDefaultCamera();
        // Use a sub-range of the frustum
        var corners = cam.GetFrustumCorners(5f, 50f);

        for (int i = 0; i < 4; i++)
            Assert.InRange(corners[i].Z, -5 - Epsilon, -5 + Epsilon);
        for (int i = 4; i < 8; i++)
            Assert.InRange(corners[i].Z, -50 - Epsilon, -50 + Epsilon);
    }
}
