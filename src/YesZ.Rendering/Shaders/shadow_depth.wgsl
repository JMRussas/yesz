// YesZ — Shadow Depth Shader
//
// Minimal vertex-only shader for shadow map generation.
// Transforms vertices by model matrix and light VP, both stored in the material UBO.
// No globals buffer — avoids QueueWriteBuffer race conditions with the scene pass.
// No fragment output — depth is written automatically by the depth-only pass.
//
// Flags: ShaderFlags.Depth | ShaderFlags.DepthLess

struct ShadowMaterial {
    light_vp: mat4x4f,
    model: mat4x4f,
}
@group(0) @binding(0) var<uniform> material: ShadowMaterial;

struct VertexInput {
    @location(0) position: vec3f,
    @location(1) normal: vec3f,
    @location(2) uv: vec2f,
    @location(3) color: vec4f,
}

@vertex fn vs_main(in: VertexInput) -> @builtin(position) vec4f {
    let world_pos = material.model * vec4f(in.position, 1.0);
    return material.light_vp * world_pos;
}

// Fragment shader outputs nothing — depth-only pass has no color attachments.
// WebGPU still requires a fragment entry point for the standard CreateShader path,
// but the depth-only pipeline creation will set Fragment = null, so this is never called.
@fragment fn fs_main() {}
