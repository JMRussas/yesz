//  YesZ - Shadow Sampling Uniform Data
//
//  Per-frame uniform for the lit+shadow shader pass.
//  Contains light-space matrix and bias parameters for shadow map sampling.
//  Uploaded via SetUniform("shadow") at @binding(5) in lit_shadow3d.wgsl.
//
//  Depends on: System.Numerics, System.Runtime.InteropServices
//  Used by:    Graphics3D (lit+shadow draw path)

using System.Numerics;
using System.Runtime.InteropServices;

namespace YesZ.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct ShadowUniforms
{
    public Matrix4x4 LightViewProj;  // 64 bytes — light-space VP for projecting world pos
    public float ShadowBias;         //  4 bytes — constant depth bias
    public float NormalBias;          //  4 bytes — normal offset bias
    public float TexelSizeX;         //  4 bytes — 1.0 / shadow_map_resolution
    public float TexelSizeY;         //  4 bytes — 1.0 / shadow_map_resolution
    // Total: 80 bytes
}
