//  YesZ - Joint Matrix Uniforms
//
//  Fixed-size UBO for uploading per-skeleton joint matrices to the GPU.
//  128 joints × 64 bytes = 8192 bytes, well within WebGPU's 64KB UBO limit.
//
//  Depends on: System.Numerics, System.Runtime.CompilerServices
//  Used by:    Graphics3D (skinned draw path)

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YesZ.Rendering;

[StructLayout(LayoutKind.Sequential, Size = MaxJoints * 64)]
internal struct JointMatrixUniforms
{
    public const int MaxJoints = 128;

    private Matrix4x4 _m0;

    /// <summary>
    /// Write a joint matrix at the given index (0..MaxJoints-1).
    /// </summary>
    public void Set(int index, in Matrix4x4 matrix)
    {
        Unsafe.Add(ref _m0, index) = matrix;
    }
}
