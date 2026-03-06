// YesZ — Skinned lit 3D shader (Blinn-Phong, multi-light, skeletal animation)
//
// Same as lit3d.wgsl but vertex shader blends position/normal by joint matrices.
// Joint matrices at @binding(5) replace the model matrix for world transform.
// Fragment shader is identical to lit3d — Blinn-Phong with directional + point lights.
//
// Flags: ShaderFlags.Depth | ShaderFlags.DepthLess

// Layout must match NoZ's Globals buffer (see Graphics.UploadGlobals).
// In lit mode, globals.projection holds VP (view × projection), not MVP.
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
@group(0) @binding(2) var base_color_texture: texture_2d<f32>;
@group(0) @binding(3) var base_color_sampler: sampler;

struct PointLightData {
    position: vec4f,
    color: vec4f,
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

struct JointMatrices {
    matrices: array<mat4x4f, 128>,
}
@group(0) @binding(5) var<uniform> joints: JointMatrices;

struct VertexInput {
    @location(0) position: vec3f,
    @location(1) normal: vec3f,
    @location(2) uv: vec2f,
    @location(3) color: vec4f,
    @location(4) joint_indices: vec4u,
    @location(5) weights: vec4f,
}

struct VertexOutput {
    @builtin(position) clip_position: vec4f,
    @location(0) world_position: vec3f,
    @location(1) world_normal: vec3f,
    @location(2) uv: vec2f,
    @location(3) color: vec4f,
}

fn attenuate(distance: f32, range: f32) -> f32 {
    let ratio = clamp(distance / range, 0.0, 1.0);
    let falloff = 1.0 - ratio * ratio;
    return falloff * falloff;
}

@vertex fn vs_main(in: VertexInput) -> VertexOutput {
    // Compute skinning matrix from weighted joint matrices
    let skin =
        in.weights.x * joints.matrices[in.joint_indices.x] +
        in.weights.y * joints.matrices[in.joint_indices.y] +
        in.weights.z * joints.matrices[in.joint_indices.z] +
        in.weights.w * joints.matrices[in.joint_indices.w];

    var out: VertexOutput;
    let world_pos = skin * vec4f(in.position, 1.0);
    out.clip_position = globals.projection * world_pos;
    out.world_position = world_pos.xyz;
    // Normal transform: use w=0 so translation doesn't affect normals
    out.world_normal = normalize((skin * vec4f(in.normal, 0.0)).xyz);
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

    // Directional light
    {
        let L = normalize(-lights.directional_dir.xyz);
        let NdotL = max(dot(N, L), 0.0);
        total_diffuse += lights.directional_color.xyz * NdotL;
        if (NdotL > 0.0) {
            let H = normalize(V + L);
            total_specular += lights.directional_color.xyz * pow(max(dot(N, H), 0.0), shininess);
        }
    }

    // Point lights
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
