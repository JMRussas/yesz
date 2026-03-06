//  YesZ - Animation Sampler
//
//  Samples animation channels at arbitrary time values using binary search
//  for keyframe brackets and per-mode interpolation (LINEAR, STEP).
//
//  Depends on: YesZ (AnimationChannel3D, AnimationPath, InterpolationMode),
//              System.Numerics
//  Used by:    AnimationPlayer3D, InterpolationTests

using System;
using System.Numerics;

namespace YesZ;

public static class AnimationSampler
{
    /// <summary>
    /// Sample a translation channel at the given time.
    /// </summary>
    public static Vector3 SampleTranslation(AnimationChannel3D channel, float time)
    {
        var times = channel.Times;
        var values = channel.Translations!;
        if (times.Length == 0) return Vector3.Zero;
        if (times.Length == 1 || time <= times[0]) return values[0];
        if (time >= times[^1]) return values[^1];

        int i = FindKeyframe(times, time);
        float t = (time - times[i]) / (times[i + 1] - times[i]);

        return channel.Interpolation switch
        {
            InterpolationMode.Step => values[i],
            InterpolationMode.Linear => Vector3.Lerp(values[i], values[i + 1], t),
            _ => values[i], // Fallback
        };
    }

    /// <summary>
    /// Sample a rotation channel at the given time.
    /// </summary>
    public static Quaternion SampleRotation(AnimationChannel3D channel, float time)
    {
        var times = channel.Times;
        var values = channel.Rotations!;
        if (times.Length == 0) return Quaternion.Identity;
        if (times.Length == 1 || time <= times[0]) return values[0];
        if (time >= times[^1]) return values[^1];

        int i = FindKeyframe(times, time);
        float t = (time - times[i]) / (times[i + 1] - times[i]);

        return channel.Interpolation switch
        {
            InterpolationMode.Step => values[i],
            InterpolationMode.Linear => SlerpShortPath(values[i], values[i + 1], t),
            _ => values[i], // Fallback
        };
    }

    /// <summary>
    /// Sample a scale channel at the given time.
    /// </summary>
    public static Vector3 SampleScale(AnimationChannel3D channel, float time)
    {
        var times = channel.Times;
        var values = channel.Scales!;
        if (times.Length == 0) return Vector3.One;
        if (times.Length == 1 || time <= times[0]) return values[0];
        if (time >= times[^1]) return values[^1];

        int i = FindKeyframe(times, time);
        float t = (time - times[i]) / (times[i + 1] - times[i]);

        return channel.Interpolation switch
        {
            InterpolationMode.Step => values[i],
            InterpolationMode.Linear => Vector3.Lerp(values[i], values[i + 1], t),
            _ => values[i], // Fallback
        };
    }

    /// <summary>
    /// Slerp with short-path selection: negate one quaternion if dot product is negative.
    /// Falls back to lerp+normalize for very small angles to avoid NaN.
    /// </summary>
    public static Quaternion SlerpShortPath(Quaternion q0, Quaternion q1, float t)
    {
        float dot = Quaternion.Dot(q0, q1);
        if (dot < 0)
        {
            q1 = new Quaternion(-q1.X, -q1.Y, -q1.Z, -q1.W);
            dot = -dot;
        }

        // For very close quaternions, use lerp to avoid division by near-zero sin
        if (dot > 0.9995f)
        {
            var result = new Quaternion(
                q0.X + t * (q1.X - q0.X),
                q0.Y + t * (q1.Y - q0.Y),
                q0.Z + t * (q1.Z - q0.Z),
                q0.W + t * (q1.W - q0.W));
            return Quaternion.Normalize(result);
        }

        return Quaternion.Slerp(q0, q1, t);
    }

    /// <summary>
    /// Binary search for the keyframe index i such that times[i] &lt;= time &lt; times[i+1].
    /// </summary>
    public static int FindKeyframe(float[] times, float time)
    {
        int lo = 0;
        int hi = times.Length - 2; // Last valid bracket is [N-2, N-1]
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2; // Round up to avoid infinite loop
            if (times[mid] <= time)
                lo = mid;
            else
                hi = mid - 1;
        }
        return lo;
    }
}
