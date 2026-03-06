// YesZ — Shadow Depth Shader
//
// Minimal vertex-only shader for shadow map generation.
// Transforms vertices by model matrix (material UBO) and light VP (globals).
// No fragment output — depth is written automatically by the depth-only pass.
//
// Flags: ShaderFlags.Depth | ShaderFlags.DepthLess

struct Globals {
    projection: mat4x4f,
    time: f32,
}
@group(0) @binding(0) var<uniform> globals: Globals;

struct Material {
    model: mat4x4f,
    normal_matrix: mat4x4f,
    base_color_factor: vec4f,
    metallic: f32,
    roughness: f32,
}
@group(0) @binding(1) var<uniform> material: Material;

struct VertexInput {
    @location(0) position: vec3f,
    @location(1) normal: vec3f,
    @location(2) uv: vec2f,
    @location(3) color: vec4f,
}

@vertex fn vs_main(in: VertexInput) -> @builtin(position) vec4f {
    let world_pos = material.model * vec4f(in.position, 1.0);
    return globals.projection * world_pos;
}

// Fragment shader outputs nothing — depth-only pass has no color attachments.
// WebGPU still requires a fragment entry point for the pipeline when using
// the standard CreateShader path, but the depth-only pipeline creation
// will set Fragment = null, so this function is never called.
@fragment fn fs_main() {}
