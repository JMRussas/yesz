//  YesZ - 3D Camera
//
//  Perspective camera with view and projection matrices.
//  Matrices are computed fresh on each property access (no internal caching).
//
//  Depends on: System.Numerics
//  Used by:    YesZ.Rendering, game code

using System.Numerics;

namespace YesZ;

public class Camera3D
{
    public Vector3 Position { get; set; } = new(0, 0, 5);
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    public float FieldOfView { get; set; } = 60.0f;
    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 1000.0f;
    public float AspectRatio { get; set; } = 16.0f / 9.0f;

    public Matrix4x4 ViewMatrix
    {
        get
        {
            var forward = Vector3.Transform(-Vector3.UnitZ, Rotation);
            var up = Vector3.Transform(Vector3.UnitY, Rotation);
            return Matrix4x4.CreateLookAt(Position, Position + forward, up);
        }
    }

    public Matrix4x4 ProjectionMatrix =>
        Matrix4x4.CreatePerspectiveFieldOfView(
            FieldOfView * MathF.PI / 180.0f,
            AspectRatio,
            NearPlane,
            FarPlane
        );

    public Matrix4x4 ViewProjectionMatrix => ViewMatrix * ProjectionMatrix;

    public Vector3[] GetFrustumCorners(float near, float far)
    {
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(
            FieldOfView * MathF.PI / 180.0f, AspectRatio, near, far);
        var vp = ViewMatrix * proj;

        if (!Matrix4x4.Invert(vp, out var invVP))
            throw new InvalidOperationException("Cannot invert view-projection matrix");

        // NDC cube corners → world space
        // WebGPU NDC: x[-1,1], y[-1,1], z[0,1]
        Span<Vector3> ndcCorners =
        [
            new(-1, 1, 0), new(1, 1, 0), new(-1, -1, 0), new(1, -1, 0), // near
            new(-1, 1, 1), new(1, 1, 1), new(-1, -1, 1), new(1, -1, 1), // far
        ];

        var corners = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            var clip = Vector4.Transform(new Vector4(ndcCorners[i], 1), invVP);
            corners[i] = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
        }
        return corners;
    }
}
