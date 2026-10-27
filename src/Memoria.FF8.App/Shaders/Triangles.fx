#if VULKAN || DIRECTX12
#define VS_PROFILE vs_6_0
#define PS_PROFILE ps_6_0
#define SAMPLE_TEXTURE(textureName, samplerName, uv) textureName.Sample(samplerName, uv)
#else
#define VS_PROFILE vs_4_0
#define PS_PROFILE ps_4_0
#define SAMPLE_TEXTURE(textureName, samplerName, uv) tex2D(samplerName, uv)
#endif

float2 ViewportSize;
float4 MaterialParameters; // texture present, selects alpha, alpha multiplier, positive depth mapping
float4 AlphaParameters; // minimum, maximum, minimum reciprocal W, source RGB multiplier
float4 FogParameters; // start, end, unused, unused
float3 FogColor;
float4 ClipRectangle;
float2 TextureSize;
float DepthScale;
bool CentroidTextureSampling;
float4x4 WorldView;
float3 WorldOffset;
float WorldFocal;
float WorldNativeCurvature;
#if VULKAN || DIRECTX12
Texture2D SourceTexture : register(t0);
#else
texture SourceTexture;
#endif
#if VULKAN || DIRECTX12
sampler TextureSampler : register(s0) = sampler_state
#else
sampler TextureSampler = sampler_state
#endif
{ Texture = <SourceTexture>; };
struct VSInput { float3 Position : POSITION0; float4 Color : COLOR0; float3 Uv : TEXCOORD0; float4 TextureScreenRectangle : TEXCOORD2; float4 TextureTexelRectangle : TEXCOORD3; };
struct VSOutput { float4 Position : SV_POSITION; float4 Color : COLOR0; float3 Uv : TEXCOORD0; float Depth : TEXCOORD1; nointerpolation float4 TextureScreenRectangle : TEXCOORD2; nointerpolation float4 TextureTexelRectangle : TEXCOORD3; centroid float3 TextureUv : TEXCOORD4; };
VSOutput VertexMain(VSInput input)
{
    VSOutput output;
    output.Position = float4(input.Position.x * 2 / ViewportSize.x - 1, 1 - input.Position.y * 2 / ViewportSize.y, 0, 1);
    output.Color = input.Color;
    output.Uv = input.Uv;
    output.TextureUv = input.Uv;
    output.Depth = input.Position.z;
    output.TextureScreenRectangle = input.TextureScreenRectangle;
    output.TextureTexelRectangle = input.TextureTexelRectangle;
    return output;
}
struct WorldVSInput
{
    float3 Position : POSITION0;
    float2 Uv : TEXCOORD0;
};
VSOutput WorldVertexMain(WorldVSInput input)
{
    float3 terrainPosition = input.Position + WorldOffset;
    if (WorldNativeCurvature > 0)
    {
        int cameraDepth = (int)round(-mul(float4(terrainPosition, 1), WorldView).z * 16);
        int distance = (clamp(cameraDepth, 0, 65535) >> 2) - 5216;
        if (distance >= 0)
        {
            int nativeHeight = -(int)(input.Position.y * 16);
            int curvedHeight = (distance >> 6) * (distance >> 6) + (((4096 - distance) * nativeHeight) >> 12);
            int signedHeight = (curvedHeight << 16) >> 16;
            terrainPosition.y = -signedHeight / 16.0;
        }
    }
    float3 camera = mul(float4(terrainPosition, 1), WorldView).xyz;
    float cameraZ = -camera.z;
    VSOutput output;
    // Homogeneous clipping preserves the camera-space near plane at Z = 1.
    output.Position = float4(camera.x * WorldFocal * 2 / ViewportSize.x, camera.y * WorldFocal * 2 / ViewportSize.y, cameraZ - 1, cameraZ);
    output.Color = float4(1, 1, 1, 1);
    output.Uv = float3(input.Uv, 1);
    output.TextureUv = output.Uv;
    output.Depth = cameraZ;
    output.TextureScreenRectangle = 0;
    output.TextureTexelRectangle = 0;
    return output;
}
struct PSOutput { float4 Color : SV_TARGET0; float Depth : SV_DEPTH; };
PSOutput PixelMain(VSOutput input)
{
    clip(input.Position.x - ClipRectangle.x);
    clip(input.Position.y - ClipRectangle.y);
    clip(ClipRectangle.z - input.Position.x);
    clip(ClipRectangle.w - input.Position.y);
    clip(input.Uv.z - AlphaParameters.z);
    float4 color = input.Color;
    if (MaterialParameters.x > 0)
    {
        clip(input.Uv.z - 0.0000000001);
        // A partially covered MSAA pixel can have its center outside the face.
        // Keep model texture/alpha samples inside it; Uv remains centered for depth and fog.
        float3 textureUv = CentroidTextureSampling ? input.TextureUv : input.Uv;
        float2 interpolatedUv = textureUv.xy / textureUv.z;
        float2 uv = interpolatedUv;
        if (input.TextureScreenRectangle.z > 0)
        {
            // Reconstruct local source pixels before adding the atlas origin.
            // Interpolated atlas UVs lose precision at source pixel boundaries.
            float2 local = (input.Position.xy - input.TextureScreenRectangle.xy) / input.TextureScreenRectangle.zw;
            float2 pixel = clamp(floor(local * input.TextureTexelRectangle.zw), 0, input.TextureTexelRectangle.zw - 1);
            uv = (input.TextureTexelRectangle.xy + pixel + 0.5) / TextureSize;
        }
        float4 texel = SAMPLE_TEXTURE(SourceTexture, TextureSampler, uv);
        if (FogParameters.w > 0)
        {
            // Integer source UVs are smooth RGB texel centers; texel above retains point-sampled cutout coverage.
            float2 texelPosition = clamp(interpolatedUv * TextureSize, 0, TextureSize - 1);
            float2 origin = floor(texelPosition);
            float2 weight = frac(texelPosition);
            float4 c00 = SAMPLE_TEXTURE(SourceTexture, TextureSampler, (origin + float2(0.5, 0.5)) / TextureSize);
            float4 c10 = SAMPLE_TEXTURE(SourceTexture, TextureSampler, (origin + float2(1.5, 0.5)) / TextureSize);
            float4 c01 = SAMPLE_TEXTURE(SourceTexture, TextureSampler, (origin + float2(0.5, 1.5)) / TextureSize);
            float4 c11 = SAMPLE_TEXTURE(SourceTexture, TextureSampler, (origin + float2(1.5, 1.5)) / TextureSize);
            float4 weights = float4((1-weight.x)*(1-weight.y), weight.x*(1-weight.y), (1-weight.x)*weight.y, weight.x*weight.y);
            weights *= float4(c00.a, c10.a, c01.a, c11.a);
            float total = dot(weights, float4(1,1,1,1));
            if (total > 0) texel.rgb = (c00.rgb*weights.x + c10.rgb*weights.y + c01.rgb*weights.z + c11.rgb*weights.w) / total;
        }
        clip(texel.a - AlphaParameters.x);
        clip(AlphaParameters.y - texel.a);
        color.rgb *= texel.rgb;
        color.a = MaterialParameters.y > 0 ? texel.a : texel.a * color.a;
    }
    else
    {
        clip(color.a - AlphaParameters.x);
        clip(AlphaParameters.y - color.a);
    }
    color.a *= MaterialParameters.z;
    if (FogParameters.y > FogParameters.x)
    {
        color.rgb = lerp(color.rgb, FogColor, saturate((1 / input.Uv.z - FogParameters.x) / (FogParameters.y - FogParameters.x)));
    }
    color.rgb = saturate(color.rgb);
    if (AlphaParameters.w < 0) color.a = 0.5;
    else color.rgb *= AlphaParameters.w;
    if (FogParameters.z > 0) input.Depth = 1 / input.Uv.z;
    PSOutput output;
    output.Color = color;
    if (MaterialParameters.w > 0)
    {
        clip(input.Depth);
        float scaledDepth = input.Depth * DepthScale;
        output.Depth = scaledDepth / (1 + scaledDepth);
    }
    else
    {
        clip(input.Depth);
        clip(1 - input.Depth);
        output.Depth = input.Depth;
    }
    return output;
}
technique TriangleTechnique
{
    pass P0
    {
        VertexShader = compile VS_PROFILE VertexMain();
        PixelShader = compile PS_PROFILE PixelMain();
    }
}

PSOutput WorldPixelMain(VSOutput input)
{
    // Perspective interpolation of camera Z produces the harmonic screen-space depth.
    float reciprocalZ = 1 / input.Depth;
    input.Uv = float3(input.Uv.xy * reciprocalZ, reciprocalZ);
    input.Depth = reciprocalZ;
    return PixelMain(input);
}
technique WorldTerrainTechnique
{
    pass P0
    {
        VertexShader = compile VS_PROFILE WorldVertexMain();
        PixelShader = compile PS_PROFILE WorldPixelMain();
    }
}
