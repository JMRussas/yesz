//  YesZ - LightSpaceComputer Tests
//
//  Tests for light-space matrix computation: verifying that the
//  orthographic projection tightly fits the camera frustum and that
//  the resulting matrices transform scene geometry correctly.
//
//  Depends on: YesZ (LightSpaceComputer, Camera3D, DirectionalLight), System.Numerics
//  Used by:    test runner

using System;
using System.Numerics;
using Xunit;

namespace YesZ.Tests;

public class LightSpaceMatrixTests
{
    private const float Epsilon = 1e-3f;

    private static Camera3D CreateCamera() => new()
    {
        Position = Vector3.Zero,
        Rotation = Quaternion.Identity,
        FieldOfView = 60.0f,
        AspectRatio = 16.0f / 9.0f,
        NearPlane = 0.1f,
        FarPlane = 100.0f,
    };

    [Fact]
    public void Compute_ReturnsBothMatrices()
    {
        var cam = CreateCamera();
        var light = new DirectionalLight { Direction = new Vector3(-1, -1, -1), Color = Vector3.One, Intensity = 1.0f };

        var (view, proj) = LightSpaceComputer.Compute(in light, cam, 50f);

        // Both matrices should be non-zero (not default)
        Assert.NotEqual(Matrix4x4.Identity, view);
        Assert.NotEqual(default(Matrix4x4), proj);
    }

    [Fact]
    public void Compute_FrustumCornersInsideLightClipSpace()
    {
        var cam = CreateCamera();
        var light = new DirectionalLight { Direction = new Vector3(0, -1, 0), Color = Vector3.One, Intensity = 1.0f };

        var (view, proj) = LightSpaceComputer.Compute(in light, cam, 50f);
        var lightVP = view * proj;

        // All frustum corners should land inside the ortho clip volume
        var corners = cam.GetFrustumCorners(cam.NearPlane, 50f);
        foreach (var corner in corners)
        {
            var clip = Vector4.Transform(new Vector4(corner, 1), lightVP);
            // NDC: x,y in [-1,1], z in [0,1] for WebGPU
            var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
            Assert.InRange(ndc.X, -1.0f - Epsilon, 1.0f + Epsilon);
            Assert.InRange(ndc.Y, -1.0f - Epsilon, 1.0f + Epsilon);
            Assert.InRange(ndc.Z, -Epsilon, 1.0f + Epsilon);
        }
    }

    [Fact]
    public void Compute_VerticalLight_DoesNotDegenerate()
    {
        var cam = CreateCamera();
        // Straight-down light — tests the up-vector fallback
        var light = new DirectionalLight { Direction = new Vector3(0, -1, 0), Color = Vector3.One, Intensity = 1.0f };

        var (view, proj) = LightSpaceComputer.Compute(in light, cam, 50f);

        // Should produce valid (invertible) matrices
        Assert.True(Matrix4x4.Invert(view, out _), "Light view matrix is singular");
        Assert.True(Matrix4x4.Invert(proj, out _), "Light projection matrix is singular");
    }

    [Fact]
    public void Compute_ShadowDistanceLimitsFrustum()
    {
        var cam = CreateCamera();
        var light = new DirectionalLight { Direction = new Vector3(-1, -1, -1), Color = Vector3.One, Intensity = 1.0f };

        // Small shadow distance should produce a tighter projection
        var (_, projNear) = LightSpaceComputer.Compute(in light, cam, 10f);
        var (_, projFar) = LightSpaceComputer.Compute(in light, cam, 100f);

        // The near projection should have a smaller volume — check M11 (1/width)
        // Tighter bounds → larger M11 (more magnification)
        Assert.True(MathF.Abs(projNear.M11) > MathF.Abs(projFar.M11),
            "Near shadow distance should produce tighter projection than far");
    }

    [Fact]
    public void Compute_CenterOfFrustumInLightView_NearOrigin()
    {
        var cam = CreateCamera();
        var light = new DirectionalLight { Direction = new Vector3(-1, -1, -1), Color = Vector3.One, Intensity = 1.0f };

        var (view, _) = LightSpaceComputer.Compute(in light, cam, 50f);

        // Frustum center
        var corners = cam.GetFrustumCorners(cam.NearPlane, 50f);
        var center = Vector3.Zero;
        for (int i = 0; i < corners.Length; i++) center += corners[i];
        center /= corners.Length;

        // Transform center to light space — should be near the light-space origin
        // (the view is constructed to look at the center)
        var lightCenter = Vector3.Transform(center, view);
        Assert.InRange(lightCenter.X, -Epsilon, Epsilon);
        Assert.InRange(lightCenter.Y, -Epsilon, Epsilon);
    }
}
