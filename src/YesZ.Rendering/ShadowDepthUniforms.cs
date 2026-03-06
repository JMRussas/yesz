//  YesZ - Shadow Depth Uniform Data
//
//  Per-draw uniform for the shadow depth pass.
//  Contains light view-projection and model matrices — no globals buffer needed.
//  Uploaded via SetUniform("material") for each shadow caster draw.
//
//  Depends on: System.Numerics, System.Runtime.InteropServices
//  Used by:    Graphics3D (shadow pass)

using System.Numerics;
using System.Runtime.InteropServices;

namespace YesZ.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct ShadowDepthUniforms
{
    public Matrix4x4 LightViewProj;  // 64 bytes
    public Matrix4x4 Model;          // 64 bytes
    // Total: 128 bytes
}
