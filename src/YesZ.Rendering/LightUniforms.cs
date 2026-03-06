//  YesZ - Light Uniform Buffer Layout
//
//  GPU-compatible structs for light parameters. Uploaded via
//  Graphics.SetUniform<LightUniforms>("lights", ...) and read
//  by the lit3d.wgsl shader at @binding(4).
//
//  Intensity is pre-multiplied into color vectors (color * intensity)
//  to save a shader multiply and UBO space.
//
//  PointLightData uses fixed fields (PointLight0..7) because C# doesn't
//  support fixed arrays of structs. SetPointLight() uses Unsafe.Add for
//  indexed access.
//
//  Depends on: System.Numerics, System.Runtime.InteropServices, System.Runtime.CompilerServices
//  Used by:    Graphics3D (light upload), LightUniformsTests

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YesZ.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct PointLightData
{
    public Vector4 Position;   // xyz = world position, w = range
    public Vector4 Color;      // xyz = color * intensity, w = unused
}
// 32 bytes

[StructLayout(LayoutKind.Sequential)]
internal struct LightUniforms
{
    public Vector4 AmbientColor;        // 16 bytes — RGB * intensity (xyz), padding (w)
    public Vector4 DirectionalDir;      // 16 bytes — normalized direction (xyz), padding (w)
    public Vector4 DirectionalColor;    // 16 bytes — RGB * intensity (xyz), padding (w)
    public Vector4 CameraPosition;      // 16 bytes — world-space camera pos (xyz), padding (w)
    public uint PointLightCount;        //  4 bytes
    public uint _pad0;                  //  4 bytes — align to 16
    public uint _pad1;                  //  4 bytes
    public uint _pad2;                  //  4 bytes
    public PointLightData PointLight0;  // 32 bytes
    public PointLightData PointLight1;  // 32 bytes
    public PointLightData PointLight2;  // 32 bytes
    public PointLightData PointLight3;  // 32 bytes
    public PointLightData PointLight4;  // 32 bytes
    public PointLightData PointLight5;  // 32 bytes
    public PointLightData PointLight6;  // 32 bytes
    public PointLightData PointLight7;  // 32 bytes

    public const int MaxPointLights = 8;

    /// <summary>
    /// Set point light data at the given index (0..7) using Unsafe.Add
    /// to address the unrolled PointLight0..7 fields.
    /// </summary>
    public void SetPointLight(int index, in PointLightData data)
    {
        if ((uint)index >= MaxPointLights)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Point light index must be 0..{MaxPointLights - 1}, got {index}.");

        ref var first = ref PointLight0;
        Unsafe.Add(ref first, index) = data;
    }
}
// Total: 336 bytes (4×16 base + 16 count/pad + 8×32 point lights)
