// YesZ — Lit 3D shader with shadow mapping (Blinn-Phong, multi-light, PCF shadows)
//
// Extends lit3d.wgsl with directional light shadow sampling.
// Transforms vertices by Model (material UBO) and VP (globals).
// Evaluates directional light + up to 8 point lights with smooth attenuation.
// Applies shadow factor to directional light using textureLoad + PCF 3×3.
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

struct PointLightData {
    position: vec4f,   // xyz = world position, w = range
    color: vec4f,      // xyz = color * intensity, w = unused
}

struct Lights {
    ambient_color: vec4f,
    directional_dir: vec4f,
    directional_color: vec4f,
    camera_position: vec4f,
    point_light_count: u32,
    _pad0: u32,
    _pad1: u32,
    _pad2: u32,
    point_lights: array<PointLightData, 8>,
}
@group(0) @binding(4) var<uniform> lights: Lights;

struct ShadowData {
    light_view_proj: mat4x4f,
    shadow_bias: f32,
    normal_bias: f32,
    texel_size: vec2f,     // 1.0 / shadow_map_resolution
}
@group(0) @binding(5) var<uniform> shadow: ShadowData;
@group(0) @binding(6) var shadow_map: texture_depth_2d;

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

// Smooth quadratic falloff: reaches exactly zero at distance = range.
fn attenuate(distance: f32, range: f32) -> f32 {
    let ratio = clamp(distance / range, 0.0, 1.0);
    let falloff = 1.0 - ratio * ratio;
    return falloff * falloff;
}

fn compute_shadow(world_pos: vec3f, world_normal: vec3f, NdotL: f32) -> f32 {
    // Normal offset: push sample point along normal to reduce acne
    let offset_pos = world_pos + world_normal * shadow.normal_bias;

    // Project into light space
    let light_clip = shadow.light_view_proj * vec4f(offset_pos, 1.0);
    let light_ndc = light_clip.xyz / light_clip.w;

    // NDC [-1,1] → UV [0,1] (WebGPU: Y is flipped vs OpenGL)
    let shadow_uv = vec2f(light_ndc.x * 0.5 + 0.5, -light_ndc.y * 0.5 + 0.5);

    // Out-of-bounds check
    if (shadow_uv.x < 0.0 || shadow_uv.x > 1.0 || shadow_uv.y < 0.0 || shadow_uv.y > 1.0) {
        return 1.0;  // No shadow outside shadow map
    }

    // Slope-scaled bias: more bias at grazing angles where acne is worst
    let bias = max(shadow.shadow_bias * (1.0 - NdotL), shadow.shadow_bias * 0.1);
    let compare_depth = light_ndc.z - bias;

    // PCF 3×3 using textureLoad (manual depth comparison, no comparison sampler needed)
    let dim = textureDimensions(shadow_map);
    let texel = vec2i(shadow_uv * vec2f(dim));
    var shadow_sum = 0.0;
    for (var y = -1; y <= 1; y++) {
        for (var x = -1; x <= 1; x++) {
            let sample_texel = texel + vec2i(x, y);
            if (sample_texel.x >= 0 && sample_texel.x < i32(dim.x) &&
                sample_texel.y >= 0 && sample_texel.y < i32(dim.y)) {
                let shadow_depth = textureLoad(shadow_map, sample_texel, 0);
                shadow_sum += select(0.0, 1.0, compare_depth <= shadow_depth);
            } else {
                shadow_sum += 1.0;  // Outside shadow map = lit
            }
        }
    }
    return shadow_sum / 9.0;
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
    let shininess = mix(8.0, 256.0, 1.0 - material.roughness);
    let specular_strength = mix(0.04, 1.0, material.metallic);

    var total_diffuse = lights.ambient_color.xyz;
    var total_specular = vec3f(0.0);

    // Directional light with shadow
    {
        let L = normalize(-lights.directional_dir.xyz);
        let NdotL = max(dot(N, L), 0.0);
        let shadow_factor = compute_shadow(in.world_position, N, NdotL);
        total_diffuse += lights.directional_color.xyz * NdotL * shadow_factor;
        if (NdotL > 0.0) {
            let H = normalize(V + L);
            total_specular += lights.directional_color.xyz * pow(max(dot(N, H), 0.0), shininess) * shadow_factor;
        }
    }

    // Point lights (no shadows)
    for (var i = 0u; i < lights.point_light_count; i++) {
        let light = lights.point_lights[i];
        let light_vec = light.position.xyz - in.world_position;
        let distance = length(light_vec);
        let L = light_vec / distance;
        let atten = attenuate(distance, light.position.w);
        let NdotL = max(dot(N, L), 0.0);
        total_diffuse += light.color.xyz * NdotL * atten;
        if (NdotL > 0.0) {
            let H = normalize(V + L);
            total_specular += light.color.xyz * pow(max(dot(N, H), 0.0), shininess) * atten;
        }
    }

    let final_color = base_color.rgb * total_diffuse + total_specular * specular_strength;
    return vec4f(final_color, base_color.a);
}
