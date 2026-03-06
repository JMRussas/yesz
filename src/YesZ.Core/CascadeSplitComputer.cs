//  YesZ - Cascade Split Computer
//
//  Computes view-frustum split distances for cascaded shadow maps using
//  a practical split scheme that blends logarithmic and uniform distribution.
//  Logarithmic gives more resolution near the camera; uniform prevents the
//  far cascade from covering a tiny depth range.
//
//  Depends on: System (MathF)
//  Used by:    YesZ.Rendering (Graphics3D cascade shadow pass), tests

using System;

namespace YesZ;

public static class CascadeSplitComputer
{
    /// <summary>
    /// Compute cascade split distances for the given frustum range.
    /// Returns cascadeCount + 1 values: splits[0] = near, splits[cascadeCount] = far.
    /// Lambda controls the blend: 0 = uniform, 1 = logarithmic, 0.75 = industry standard.
    /// </summary>
    public static float[] ComputeSplits(float near, float far, int cascadeCount, float lambda = 0.75f)
    {
        if (cascadeCount < 1)
            throw new ArgumentOutOfRangeException(nameof(cascadeCount), "Must be at least 1");
        if (near <= 0)
            throw new ArgumentOutOfRangeException(nameof(near), "Must be positive");
        if (far <= near)
            throw new ArgumentOutOfRangeException(nameof(far), "Must be greater than near");

        var splits = new float[cascadeCount + 1];
        splits[0] = near;
        splits[cascadeCount] = far;

        for (int i = 1; i < cascadeCount; i++)
        {
            float t = (float)i / cascadeCount;
            float log = near * MathF.Pow(far / near, t);
            float uniform = near + (far - near) * t;
            splits[i] = uniform * (1.0f - lambda) + log * lambda;
        }

        return splits;
    }
}
