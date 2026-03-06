//  YesZ - 3D Animation Clip
//
//  Baked animation data extracted from glTF: named clip with multiple
//  channels, each targeting a joint property (translation/rotation/scale)
//  with timestamped keyframes.
//
//  Depends on: System.Numerics
//  Used by:    AnimationParser, AnimationPlayer3D (Phase 5c)

using System.Numerics;

namespace YesZ;

/// <summary>
/// Which TRS property an animation channel targets.
/// </summary>
public enum AnimationPath
{
    Translation,
    Rotation,
    Scale,
}

/// <summary>
/// Interpolation mode for animation keyframes.
/// </summary>
public enum InterpolationMode
{
    Linear,
    Step,
    CubicSpline,
}

/// <summary>
/// A single animation channel: targets one joint's TRS property with keyframe data.
/// </summary>
public class AnimationChannel3D
{
    /// <summary>Index into the skeleton's joint array.</summary>
    public int JointIndex { get; }

    /// <summary>Which property is animated (translation, rotation, scale).</summary>
    public AnimationPath Path { get; }

    /// <summary>Interpolation mode for this channel.</summary>
    public InterpolationMode Interpolation { get; }

    /// <summary>Keyframe timestamps in seconds. Monotonically increasing.</summary>
    public float[] Times { get; }

    /// <summary>
    /// Translation keyframe values. Non-null only when Path == Translation.
    /// Length matches Times.Length.
    /// </summary>
    public Vector3[]? Translations { get; }

    /// <summary>
    /// Rotation keyframe values (quaternion). Non-null only when Path == Rotation.
    /// Length matches Times.Length.
    /// </summary>
    public Quaternion[]? Rotations { get; }

    /// <summary>
    /// Scale keyframe values. Non-null only when Path == Scale.
    /// Length matches Times.Length.
    /// </summary>
    public Vector3[]? Scales { get; }

    public AnimationChannel3D(int jointIndex, AnimationPath path, InterpolationMode interpolation,
        float[] times, Vector3[]? translations, Quaternion[]? rotations, Vector3[]? scales)
    {
        JointIndex = jointIndex;
        Path = path;
        Interpolation = interpolation;
        Times = times;
        Translations = translations;
        Rotations = rotations;
        Scales = scales;
    }
}

/// <summary>
/// A named animation clip containing multiple channels that animate different joints.
/// </summary>
public class AnimationClip3D
{
    public string Name { get; }
    public AnimationChannel3D[] Channels { get; }
    public float Duration { get; }

    public AnimationClip3D(string name, AnimationChannel3D[] channels)
    {
        Name = name;
        Channels = channels;

        // Duration is the max timestamp across all channels
        float maxTime = 0;
        foreach (var ch in channels)
        {
            if (ch.Times.Length > 0)
            {
                float lastTime = ch.Times[^1];
                if (lastTime > maxTime) maxTime = lastTime;
            }
        }
        Duration = maxTime;
    }
}
