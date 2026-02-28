//  YesZ - Light Uniform Buffer Layout
//
//  GPU-compatible struct for light parameters. Uploaded via
//  Graphics.SetUniform<LightUniforms>("lights", ...) and read
//  by the lit3d.wgsl shader at @binding(4).
//
//  Intensity is pre-multiplied into color vectors (color * intensity)
//  to save a shader multiply and UBO space.
//
//  Depends on: System.Numerics, System.Runtime.InteropServices
//  Used by:    Graphics3D (light upload), LightUniformsTests

using System.Numerics;
using System.Runtime.InteropServices;

namespace YesZ.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct LightUniforms
{
    public Vector4 AmbientColor;        // 16 bytes — RGB × intensity (xyz), padding (w)
    public Vector4 DirectionalDir;      // 16 bytes — normalized direction (xyz), padding (w)
    public Vector4 DirectionalColor;    // 16 bytes — RGB × intensity (xyz), padding (w)
    public Vector4 CameraPosition;      // 16 bytes — world-space camera pos (xyz), padding (w)
}
// Total: 64 bytes
