//  YesZ - Shadow Map Configuration
//
//  Parameters for shadow map generation: resolution, shadow distance,
//  and bias values. Passed to Graphics3D.RenderShadowPass() to control
//  the depth-only render pass.
//
//  Depends on: nothing
//  Used by:    Graphics3D (shadow pass), game code

namespace YesZ.Rendering;

public class ShadowConfig
{
    public int Resolution { get; init; } = 2048;
    public float ShadowDistance { get; init; } = 50.0f;
    public float DepthBias { get; init; } = 0.005f;
    public float NormalBias { get; init; } = 0.05f;
}
