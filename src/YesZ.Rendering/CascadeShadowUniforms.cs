//  YesZ - Cascade Shadow Uniform Data
//
//  Per-frame uniform for the cascaded shadow mapping shader.
//  Contains up to 4 light-space VP matrices (one per cascade),
//  split distances for cascade selection, and bias parameters.
//  Uploaded via SetUniform("shadow") at @binding(5) in lit_cascade_shadow3d.wgsl.
//
//  Uses fixed Matrix4x4 fields (LightViewProj0..3) because C# doesn't
//  support fixed arrays of structs. SetLightViewProj() uses Unsafe.Add
//  for indexed access.
//
//  Depends on: System.Numerics, System.Runtime.InteropServices, System.Runtime.CompilerServices
//  Used by:    Graphics3D (cascade shadow upload), CascadeShadowUniformsTests

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YesZ.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct CascadeShadowUniforms
{
    public Matrix4x4 LightViewProj0;   // 64 bytes — cascade 0
    public Matrix4x4 LightViewProj1;   // 64 bytes — cascade 1
    public Matrix4x4 LightViewProj2;   // 64 bytes — cascade 2
    public Matrix4x4 LightViewProj3;   // 64 bytes — cascade 3
    public Vector4 SplitDepths;        // 16 bytes — camera-distance split boundaries
    public uint CascadeCount;          //  4 bytes
    public float ShadowBias;           //  4 bytes
    public float NormalBias;           //  4 bytes
    public float TexelSize;            //  4 bytes
    // Total: 288 bytes

    public void SetLightViewProj(int index, in Matrix4x4 matrix)
    {
        if ((uint)index >= 4)
            throw new ArgumentOutOfRangeException(nameof(index), "Cascade index must be 0-3");

        ref var first = ref LightViewProj0;
        Unsafe.Add(ref first, index) = matrix;
    }
}
