//  YesZ - Lit Material Uniform Buffer Layout
//
//  GPU-compatible struct for lit material parameters. Uploaded via
//  Graphics.SetUniform<LitMaterialUniforms>("material", ...) and read
//  by the lit3d.wgsl shader at @binding(1).
//
//  Includes the model matrix (world transform) and normal matrix
//  (inverse-transpose of model upper 3x3) needed for world-space
//  lighting calculations in the vertex/fragment shaders.
//
//  Separate from MaterialUniforms (32 bytes) which serves the unlit
//  and textured shaders that use MVP-in-globals.
//
//  Depends on: System.Numerics, System.Runtime.InteropServices
//  Used by:    Graphics3D (uniform upload), LitMaterialUniformsTests

using System.Numerics;
using System.Runtime.InteropServices;

namespace YesZ.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct LitMaterialUniforms
{
    public Matrix4x4 Model;             // 64 bytes — world transform
    public Matrix4x4 NormalMatrix;      // 64 bytes — inverse-transpose of model (for normals)
    public Vector4 BaseColorFactor;     // 16 bytes — RGBA color multiplier
    public float Metallic;              //  4 bytes — 0 = dielectric, 1 = metal
    public float Roughness;             //  4 bytes — 0 = mirror, 1 = fully diffuse
    public float _pad0;                 //  4 bytes — align to 16-byte boundary
    public float _pad1;                 //  4 bytes
}
// Total: 160 bytes (WebGPU requires uniform buffer size to be multiple of 16)
