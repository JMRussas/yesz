//  YesZ - 3D Animation Player
//
//  Stateful animation player: manages current clip, elapsed time, looping,
//  and samples all channels to produce per-joint local transforms.
//
//  Depends on: YesZ (AnimationClip3D, AnimationChannel3D, AnimationSampler,
//              Skeleton3D), System.Numerics
//  Used by:    Game code, AnimationPlayerTests

using System;
using System.Numerics;

namespace YesZ;

/// <summary>
/// Plays animation clips and produces per-joint transforms ready for skinning.
/// </summary>
public class AnimationPlayer3D
{
    private AnimationClip3D? _clip;
    private float _time;
    private bool _looping = true;
    private float _speed = 1.0f;

    public AnimationClip3D? Clip => _clip;
    public float Time => _time;
    public bool Looping { get => _looping; set => _looping = value; }
    public float Speed { get => _speed; set => _speed = value; }

    /// <summary>
    /// Set the active animation clip and reset time to zero.
    /// </summary>
    public void Play(AnimationClip3D clip)
    {
        _clip = clip;
        _time = 0;
    }

    /// <summary>
    /// Advance the animation by deltaTime seconds.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (_clip == null || _clip.Duration <= 0) return;

        _time += deltaTime * _speed;

        if (_looping)
        {
            _time %= _clip.Duration;
            if (_time < 0) _time += _clip.Duration;
        }
        else
        {
            _time = Math.Clamp(_time, 0, _clip.Duration);
        }
    }

    /// <summary>
    /// Sample all channels at the current time and write per-joint local transforms.
    /// Joints not animated by the current clip retain their values from bindPose.
    /// </summary>
    /// <param name="skeleton">The skeleton this animation targets.</param>
    /// <param name="bindPose">Default local transforms for each joint (from glTF node TRS).</param>
    /// <param name="localPoses">Output: per-joint local transforms (length = JointCount).</param>
    public void Sample(Skeleton3D skeleton, ReadOnlySpan<Matrix4x4> bindPose, Span<Matrix4x4> localPoses)
    {
        // Start with bind pose for all joints
        bindPose.CopyTo(localPoses);

        if (_clip == null) return;

        SampleAtTime(_clip, _time, skeleton, localPoses);
    }

    /// <summary>
    /// Sample a clip at a specific time, overwriting animated joints in localPoses.
    /// </summary>
    public static void SampleAtTime(AnimationClip3D clip, float time, Skeleton3D skeleton, Span<Matrix4x4> localPoses)
    {
        // Per-joint TRS accumulator — track which components are animated
        Span<Vector3> translations = skeleton.JointCount <= 64
            ? stackalloc Vector3[skeleton.JointCount]
            : new Vector3[skeleton.JointCount];
        Span<Quaternion> rotations = skeleton.JointCount <= 64
            ? stackalloc Quaternion[skeleton.JointCount]
            : new Quaternion[skeleton.JointCount];
        Span<Vector3> scales = skeleton.JointCount <= 64
            ? stackalloc Vector3[skeleton.JointCount]
            : new Vector3[skeleton.JointCount];
        Span<byte> animated = skeleton.JointCount <= 64
            ? stackalloc byte[skeleton.JointCount]
            : new byte[skeleton.JointCount];

        // Initialize defaults
        for (int j = 0; j < skeleton.JointCount; j++)
        {
            translations[j] = Vector3.Zero;
            rotations[j] = Quaternion.Identity;
            scales[j] = Vector3.One;
        }

        // Sample each channel
        foreach (var channel in clip.Channels)
        {
            int j = channel.JointIndex;
            if (j < 0 || j >= skeleton.JointCount) continue;

            switch (channel.Path)
            {
                case AnimationPath.Translation:
                    translations[j] = AnimationSampler.SampleTranslation(channel, time);
                    animated[j] |= 1;
                    break;
                case AnimationPath.Rotation:
                    rotations[j] = AnimationSampler.SampleRotation(channel, time);
                    animated[j] |= 2;
                    break;
                case AnimationPath.Scale:
                    scales[j] = AnimationSampler.SampleScale(channel, time);
                    animated[j] |= 4;
                    break;
            }
        }

        // Compose TRS → Matrix4x4 for animated joints only
        for (int j = 0; j < skeleton.JointCount; j++)
        {
            if (animated[j] == 0) continue; // Keep bind pose

            // If only some components are animated, decompose bind pose for the rest
            if (animated[j] != 7) // Not all three animated
            {
                if (!Matrix4x4.Decompose(localPoses[j], out var bScale, out var bRot, out var bTrans))
                    continue; // Singular bind pose — skip, keep defaults
                if ((animated[j] & 1) == 0) translations[j] = bTrans;
                if ((animated[j] & 2) == 0) rotations[j] = bRot;
                if ((animated[j] & 4) == 0) scales[j] = bScale;
            }

            localPoses[j] = Matrix4x4.CreateScale(scales[j])
                          * Matrix4x4.CreateFromQuaternion(rotations[j])
                          * Matrix4x4.CreateTranslation(translations[j]);
        }
    }
}
