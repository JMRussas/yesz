//  YesZ - Shadow Map Configuration
//
//  Parameters for cascaded shadow map generation: resolution, shadow distance,
//  cascade count, split lambda, and bias values.
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

    /// <summary>
    /// Number of shadow map cascades (1-4). Default 3.
    /// More cascades = better quality at the cost of more draw passes.
    /// </summary>
    public int CascadeCount { get; init; } = 3;

    /// <summary>
    /// Cascade split blend factor: 0 = uniform, 1 = logarithmic, 0.75 = industry standard.
    /// Higher values allocate more resolution near the camera.
    /// </summary>
    public float Lambda { get; init; } = 0.75f;

    internal const int MaxCascades = 4;
}
