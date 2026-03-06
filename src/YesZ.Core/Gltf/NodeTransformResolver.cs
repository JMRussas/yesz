//  YesZ - glTF Node Transform Resolver
//
//  Resolves a GltfNode's local transform to a Matrix4x4.
//  Handles both explicit matrix (column-major float[16]) and TRS
//  (translation/rotation/scale) decomposed form.
//
//  Depends on: YesZ.Gltf (GltfNode), System.Numerics
//  Used by:    GltfLoader (Phase 4c), NodeTransformResolverTests

using System.Numerics;

namespace YesZ.Gltf;

public static class NodeTransformResolver
{
    /// <summary>
    /// Resolve a glTF node's local transform to a Matrix4x4.
    /// If the node has an explicit matrix, it is loaded from column-major float[16].
    /// Otherwise, TRS components are composed as Scale * Rotation * Translation.
    /// Returns Identity if neither is present.
    /// </summary>
    public static Matrix4x4 ResolveLocalTransform(GltfNode node)
    {
        if (node.Matrix is { Length: 16 })
        {
            // glTF stores matrices in column-major order with column-vector convention.
            // System.Numerics.Matrix4x4 is row-major with row-vector convention.
            // The two convention changes cancel: loading sequentially is correct.
            // (Column-major→row-major transpose cancels column-vector→row-vector transpose.)
            var m = node.Matrix;
            return new Matrix4x4(
                m[0],  m[1],  m[2],  m[3],
                m[4],  m[5],  m[6],  m[7],
                m[8],  m[9],  m[10], m[11],
                m[12], m[13], m[14], m[15]);
        }

        var t = node.Translation;
        var r = node.Rotation;
        var s = node.Scale;

        // If no TRS or matrix, return identity
        if (t == null && r == null && s == null)
            return Matrix4x4.Identity;

        // glTF TRS application order: T × R × S (scale first in world space)
        // System.Numerics row-vector convention: S * R * T
        var scale = s is { Length: 3 }
            ? Matrix4x4.CreateScale(s[0], s[1], s[2])
            : Matrix4x4.Identity;

        var rotation = r is { Length: 4 }
            ? Matrix4x4.CreateFromQuaternion(new Quaternion(r[0], r[1], r[2], r[3]))
            : Matrix4x4.Identity;

        var translation = t is { Length: 3 }
            ? Matrix4x4.CreateTranslation(t[0], t[1], t[2])
            : Matrix4x4.Identity;

        return scale * rotation * translation;
    }
}
