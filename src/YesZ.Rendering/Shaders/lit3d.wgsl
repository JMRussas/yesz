// YesZ — Lit 3D shader (Blinn-Phong)
//
// Transforms vertices by Model (material UBO) and VP (globals).
// Computes Lambertian diffuse + Blinn-Phong specular + ambient.
// Samples base color texture, multiplies by vertex color and material color factor.
//
// Flags: ShaderFlags.Depth | ShaderFlags.DepthLess

// Layout must match NoZ's Globals buffer (see Graphics.UploadGlobals).
// In lit mode, globals.projection holds VP (view × projection), not MVP.
struct Globals {
    projection: mat4x4f,
    time: f32,             // Unused here — required for layout compatibility with NoZ
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
@group(0) @binding(2) var base_color_texture: texture_2d<f32>;
@group(0) @binding(3) var base_color_sampler: sampler;

struct Lights {
    ambient_color: vec4f,
    directional_dir: vec4f,
    directional_color: vec4f,
    camera_position: vec4f,
}
@group(0) @binding(4) var<uniform> lights: Lights;

struct VertexInput {
    @location(0) position: vec3f,
    @location(1) normal: vec3f,
    @location(2) uv: vec2f,
    @location(3) color: vec4f,
}

struct VertexOutput {
    @builtin(position) clip_position: vec4f,
    @location(0) world_position: vec3f,
    @location(1) world_normal: vec3f,
    @location(2) uv: vec2f,
    @location(3) color: vec4f,
}

@vertex fn vs_main(in: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    let world_pos = material.model * vec4f(in.position, 1.0);
    out.clip_position = globals.projection * world_pos;
    out.world_position = world_pos.xyz;
    // Normal transform uses w=0 so translation doesn't affect normals
    out.world_normal = normalize((material.normal_matrix * vec4f(in.normal, 0.0)).xyz);
    out.uv = in.uv;
    out.color = in.color;
    return out;
}

@fragment fn fs_main(in: VertexOutput) -> @location(0) vec4f {
    let base_color = textureSample(base_color_texture, base_color_sampler, in.uv)
                     * in.color * material.base_color_factor;

    let N = normalize(in.world_normal);
    let V = normalize(lights.camera_position.xyz - in.world_position);
    let L = normalize(-lights.directional_dir.xyz);  // negate: stored direction is toward scene

    // Diffuse (Lambertian)
    let NdotL = max(dot(N, L), 0.0);
    let diffuse = lights.directional_color.xyz * NdotL;

    // Specular (Blinn-Phong) — only on front-lit faces
    var specular = vec3f(0.0);
    if (NdotL > 0.0) {
        let H = normalize(V + L);
        let NdotH = max(dot(N, H), 0.0);
        let shininess = mix(8.0, 256.0, 1.0 - material.roughness);
        specular = lights.directional_color.xyz * pow(NdotH, shininess)
                   * mix(0.04, 1.0, material.metallic);
    }

    let ambient = lights.ambient_color.xyz;
    let final_color = base_color.rgb * (ambient + diffuse) + specular;
    return vec4f(final_color, base_color.a);
}
