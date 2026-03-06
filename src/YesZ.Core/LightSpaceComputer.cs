//  YesZ - Light-Space Matrix Computer
//
//  Computes orthographic view + projection matrices from a directional
//  light's perspective, tightly fitting the camera's frustum for shadow
//  mapping. The resulting matrices transform world-space positions into
//  light clip space for depth comparison.
//
//  Depends on: System.Numerics, YesZ (Camera3D, DirectionalLight)
//  Used by:    YesZ.Rendering (Graphics3D shadow pass), tests

using System;
using System.Numerics;

namespace YesZ;

public static class LightSpaceComputer
{
    public static (Matrix4x4 View, Matrix4x4 Projection) Compute(
        in DirectionalLight light, Camera3D camera, float shadowDistance)
    {
        var corners = camera.GetFrustumCorners(camera.NearPlane, Math.Min(shadowDistance, camera.FarPlane));

        // Frustum center
        var center = Vector3.Zero;
        for (int i = 0; i < corners.Length; i++)
            center += corners[i];
        center /= corners.Length;

        // Light view: look from behind center along light direction
        var lightDir = Vector3.Normalize(light.Direction);

        // Pick a stable up vector — avoid degenerate case when light is nearly vertical
        var up = MathF.Abs(Vector3.Dot(lightDir, Vector3.UnitY)) > 0.99f
            ? Vector3.UnitZ
            : Vector3.UnitY;

        var lightView = Matrix4x4.CreateLookAt(
            center - lightDir * shadowDistance,
            center,
            up);

        // Transform frustum corners to light space → compute tight AABB
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < corners.Length; i++)
        {
            var p = Vector3.Transform(corners[i], lightView);
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
            if (p.Z < minZ) minZ = p.Z;
            if (p.Z > maxZ) maxZ = p.Z;
        }

        // CreateOrthographicOffCenter expects near/far as positive distances.
        // In right-handed view space (from CreateLookAt), objects in front of the
        // camera have negative Z. -maxZ = nearest, -minZ = farthest.
        var lightProj = Matrix4x4.CreateOrthographicOffCenter(minX, maxX, minY, maxY, -maxZ, -minZ);
        return (lightView, lightProj);
    }
}
