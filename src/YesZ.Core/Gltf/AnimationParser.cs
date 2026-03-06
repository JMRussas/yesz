//  YesZ - glTF Animation Parser
//
//  Converts glTF animation data into AnimationClip3D.
//  Reads sampler input (timestamps) and output (TRS values) from accessors,
//  resolves channel targets to joint indices via skeleton mapping.
//
//  Depends on: YesZ.Gltf (GltfDocument, GltfAnimation, AccessorReader),
//              YesZ (AnimationClip3D, AnimationChannel3D, Skeleton3D),
//              System.Numerics
//  Used by:    GltfLoader (Phase 5d), AnimationParserTests

using System;
using System.Collections.Generic;
using System.Numerics;

namespace YesZ.Gltf;

public static class AnimationParser
{
    /// <summary>
    /// Parse all glTF animations into AnimationClip3D[], using the skeleton
    /// to map node targets to joint indices.
    /// </summary>
    public static AnimationClip3D[] ParseAll(GltfDocument doc, AccessorReader reader, Skeleton3D skeleton)
    {
        if (doc.Animations == null || doc.Animations.Length == 0)
            return [];

        // Build node → joint index lookup
        var nodeToJoint = new Dictionary<int, int>(skeleton.JointCount);
        for (int j = 0; j < skeleton.JointCount; j++)
            nodeToJoint[skeleton.JointNodeIndices[j]] = j;

        var clips = new AnimationClip3D[doc.Animations.Length];
        for (int i = 0; i < doc.Animations.Length; i++)
        {
            clips[i] = ParseAnimation(doc.Animations[i], reader, nodeToJoint, i);
        }

        return clips;
    }

    private static AnimationClip3D ParseAnimation(
        GltfAnimation anim, AccessorReader reader,
        Dictionary<int, int> nodeToJoint, int animIndex)
    {
        var channels = new List<AnimationChannel3D>();

        foreach (var channel in anim.Channels)
        {
            // Skip channels targeting non-joint nodes
            if (channel.Target.Node == null)
                continue;
            if (!nodeToJoint.TryGetValue(channel.Target.Node.Value, out int jointIndex))
                continue;

            if (channel.Sampler < 0 || channel.Sampler >= anim.Samplers.Length)
                continue;

            var sampler = anim.Samplers[channel.Sampler];

            // Read timestamps
            var times = reader.Read<float>(sampler.Input);

            // Parse interpolation mode
            var interpolation = sampler.Interpolation?.ToUpperInvariant() switch
            {
                "STEP" => InterpolationMode.Step,
                "CUBICSPLINE" => InterpolationMode.CubicSpline,
                _ => InterpolationMode.Linear,
            };

            // Read output values based on target path
            AnimationChannel3D? parsed = channel.Target.Path.ToLowerInvariant() switch
            {
                "translation" => ParseTranslationChannel(jointIndex, interpolation, times, reader, sampler.Output),
                "rotation" => ParseRotationChannel(jointIndex, interpolation, times, reader, sampler.Output),
                "scale" => ParseScaleChannel(jointIndex, interpolation, times, reader, sampler.Output),
                _ => null, // Unsupported path (e.g., "weights" for morph targets)
            };

            if (parsed != null)
                channels.Add(parsed);
        }

        string name = anim.Name ?? $"Animation_{animIndex}";
        return new AnimationClip3D(name, channels.ToArray());
    }

    private static AnimationChannel3D ParseTranslationChannel(
        int jointIndex, InterpolationMode interp, float[] times, AccessorReader reader, int outputAccessor)
    {
        var raw = reader.Read<Vector3>(outputAccessor);
        var values = interp == InterpolationMode.CubicSpline ? StripCubicSpline(raw, times.Length) : raw;
        return new AnimationChannel3D(jointIndex, AnimationPath.Translation, interp, times, values, null, null);
    }

    private static AnimationChannel3D ParseRotationChannel(
        int jointIndex, InterpolationMode interp, float[] times, AccessorReader reader, int outputAccessor)
    {
        var raw = reader.Read<Quaternion>(outputAccessor);
        var values = interp == InterpolationMode.CubicSpline ? StripCubicSpline(raw, times.Length) : raw;
        return new AnimationChannel3D(jointIndex, AnimationPath.Rotation, interp, times, null, values, null);
    }

    private static AnimationChannel3D ParseScaleChannel(
        int jointIndex, InterpolationMode interp, float[] times, AccessorReader reader, int outputAccessor)
    {
        var raw = reader.Read<Vector3>(outputAccessor);
        var values = interp == InterpolationMode.CubicSpline ? StripCubicSpline(raw, times.Length) : raw;
        return new AnimationChannel3D(jointIndex, AnimationPath.Scale, interp, times, null, null, values);
    }

    /// <summary>
    /// CubicSpline outputs have 3 values per keyframe: [in-tangent, value, out-tangent].
    /// Strip to just the value elements (every 3rd starting at index 1).
    /// </summary>
    private static T[] StripCubicSpline<T>(T[] raw, int keyframeCount)
    {
        var values = new T[keyframeCount];
        for (int i = 0; i < keyframeCount; i++)
            values[i] = raw[i * 3 + 1];
        return values;
    }
}
