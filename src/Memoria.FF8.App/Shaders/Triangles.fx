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
float2 ActorOpacityParameters; // opacity, replace native alpha after coverage test
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
float4 WorldFf7Projection; // enabled, screen centre Y, curvature threshold, screen X offset
float4 WorldFf7Bridge; // native rotation's Y axis, enabled
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
struct VSInput { float3 WorldPosition : TEXCOORD5; float3 WorldNormal : TEXCOORD6; float3 Position : POSITION0; float4 Color : COLOR0; float3 Uv : TEXCOORD0; float4 TextureScreenRectangle : TEXCOORD2; float4 TextureTexelRectangle : TEXCOORD3; };
struct VSOutput { float3 WorldPosition : TEXCOORD5; float3 WorldNormal : TEXCOORD6; float4 Position : SV_POSITION; float4 Color : COLOR0; float3 Uv : TEXCOORD0; float Depth : TEXCOORD1; nointerpolation float4 TextureScreenRectangle : TEXCOORD2; nointerpolation float4 TextureTexelRectangle : TEXCOORD3; centroid float3 TextureUv : TEXCOORD4; };
VSOutput VertexMain(VSInput input)
{
    VSOutput output = (VSOutput)0;
    output.Position = float4(input.Position.x * 2 / ViewportSize.x - 1, 1 - input.Position.y * 2 / ViewportSize.y, 0, 1);
    output.Color = input.Color;
    output.WorldPosition = input.WorldPosition;
    output.WorldNormal = input.WorldNormal;
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
    float3 Normal : NORMAL0;
    float Deform : TEXCOORD1;
};
VSOutput Ff7WorldProjection(float3 camera, float2 uv, float4 color)
{
    VSOutput ff7 = (VSOutput)0;
    ff7.Position = float4((camera.x * WorldFocal + WorldFf7Projection.w * camera.z) * 2 / ViewportSize.x, -camera.y * WorldFocal * 2 / ViewportSize.y + (1 - WorldFf7Projection.y * 2 / ViewportSize.y) * camera.z, camera.z - 1, camera.z);
    ff7.Color = color;
    ff7.Uv = float3(uv / TextureSize, 1);
    ff7.TextureUv = ff7.Uv;
    ff7.Depth = camera.z;
    ff7.TextureScreenRectangle = 0;
    ff7.TextureTexelRectangle = 0;
    return ff7;
}
VSOutput WorldVertexMain(WorldVSInput input)
{
    float3 terrainPosition = input.Position + WorldOffset;
    if (WorldFf7Projection.x != 0)
    {
        if (WorldFf7Bridge.w != 0 && input.Deform != 0)
        {
            float height = input.Position.y - 20000;
            height -= floor((height + 32768) / 65536) * 65536;
            float3 rotated = trunc(height * WorldFf7Bridge.xyz);
            terrainPosition += float3(rotated.x, 20000 + rotated.y - input.Position.y, rotated.z);
        }
        float3 camera = mul(float4(terrainPosition, 1), WorldView).xyz;
        float distance = ((int)round(camera.z) >> 2) - WorldFf7Projection.z;
        if (distance > 0 && camera.z != 0)
        {
            float screenX = round(camera.x * (WorldFocal * 240 / ViewportSize.y) / camera.z);
            float bend = distance / 64 + abs(screenX) / 32;
            terrainPosition.y -= bend * bend;
            camera = mul(float4(terrainPosition, 1), WorldView).xyz;
        }
        float3 normal = mul(float4(input.Normal, 0), WorldView).xyz;
        float factor = (normal.x + normal.y + normal.z) * (110.0 / 255) + 64.0 / 255;
        float color = round(clamp(factor * 255, 0, 255)) / 255;
        return Ff7WorldProjection(camera, input.Uv, float4(color, color, color, 1));
    }
    if (WorldNativeCurvature > 0)
    {
        float cameraDepth = -mul(float4(terrainPosition, 1), WorldView).z * 16;
        float distance = clamp(cameraDepth, 0, 65535) * 0.25 - 5216;
        if (distance >= 0)
        {
            // Keep presentation continuous between native depth/height steps.
            float nativeHeight = -input.Position.y * 16;
            float squareRoot = distance / 64;
            float curvedHeight = squareRoot * squareRoot + (4096 - distance) * nativeHeight / 4096;
            float signedHeight = curvedHeight - 65536 * floor((curvedHeight + 32768) / 65536);
            terrainPosition.y = -signedHeight / 16;
        }
    }
    float3 camera = mul(float4(terrainPosition, 1), WorldView).xyz;
    float cameraZ = -camera.z;
    VSOutput output = (VSOutput)0;
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
struct Ff7UnderwaterVSInput
{
    float3 Position : POSITION0;
    float2 Uv : TEXCOORD0;
    float3 Normal : NORMAL0;
    float Deform : TEXCOORD1;
    float3 Next : TEXCOORD2;
    float3 Last : TEXCOORD3;
    float Corner : TEXCOORD4;
};
float3 Ff7UnderwaterPalette(int index)
{
    return float3(max(0, 187 - 2 * min(index, 128)), max(0, 187 - index), 255 - 2 * max(0, index - 128));
}
VSOutput Ff7UnderwaterVertexMain(Ff7UnderwaterVSInput input)
{
    float3 camera = mul(float4(input.Position + WorldOffset, 1), WorldView).xyz;
    VSOutput output = Ff7WorldProjection(camera, input.Uv, 0);
    float current = mul(float4(input.Position + WorldOffset, 1), WorldView).z;
    float next = mul(float4(input.Next + WorldOffset, 1), WorldView).z;
    float last = mul(float4(input.Last + WorldOffset, 1), WorldView).z;
    float3 depths = input.Corner < 0.5 ? float3(current, next, last) : input.Corner < 1.5 ? float3(last, current, next) : float3(next, last, current);
    int largest = depths.x < depths.y ? 1 : 0;
    largest = depths[largest] < depths.z ? 2 : largest;
    int other = largest == 1 ? 2 : 1;
    int nearest = largest == 0 ? (depths.y < depths.z ? 1 : 2) : (depths.x < depths[other] ? 0 : other);
    int middle = 3 - nearest - largest;
    float maximum = depths.z;
    if (round(depths.x - depths.y) < 0)
    {
        if (round(depths.y - depths.z) >= 0) maximum = depths.y;
    }
    else if (round(depths.x - depths.z) >= 0) maximum = depths.x;
    int delta = (int)round(maximum) - 16000;
    float3 palette;
    if (delta <= 0)
    {
        int index = (int)input.Corner == nearest ? 0 : (int)input.Corner == middle ? 4 : 8;
        palette = Ff7UnderwaterPalette(index);
    }
    else
    {
        int step = (delta >> 3) & 65472;
        int index = step > 960 ? 255 : (step + 64) >> 2;
        palette = float3(Ff7UnderwaterPalette(index).x, Ff7UnderwaterPalette(index - 4).y, Ff7UnderwaterPalette(index - 8).z);
    }
    float3 normal = mul(float4(input.Normal, 0), WorldView).xyz;
    float factor = normal.y * (110.0 / 255) + 32.0 / 255;
    output.Color = float4(round(clamp(palette * factor, 0, 255)) / 255, 1);
    return output;
}
// Only field model albedo crosses this local sRGB/linear boundary. Backgrounds and legacy passes do not.
float FieldLightingPass;
int FieldLightCount;
int FieldPrimaryIndex;
float4 FieldLightPositionRange[8];
float4 FieldLightDirectionIntensity[8];
float4 FieldLightColor[8];
float4 FieldLightCone[8];
// TODO(TD-032): Keep this uniform name short until the pinned DX12 compiler accepts adjacent reflection separators.
float4x4 FieldShadowMatrix;
float4 FieldShadowParameters; // available, ground opacity, PCF radius in UV units, texel size
#if VULKAN || DIRECTX12
Texture2D FieldShadowTexture : register(t1);
sampler FieldShadowSampler : register(s1) = sampler_state
#else
texture FieldShadowTexture;
sampler FieldShadowSampler = sampler_state
#endif
{ Texture = <FieldShadowTexture>; };
float3 FieldLinear(float3 color)
{
    return float3(color.r <= 0.04045 ? color.r / 12.92 : pow((color.r + 0.055) / 1.055, 2.4), color.g <= 0.04045 ? color.g / 12.92 : pow((color.g + 0.055) / 1.055, 2.4), color.b <= 0.04045 ? color.b / 12.92 : pow((color.b + 0.055) / 1.055, 2.4));
}
float3 FieldSrgb(float3 color)
{
    color = max(color, 0);
    return float3(color.r <= 0.0031308 ? color.r * 12.92 : 1.055 * pow(color.r, 1.0 / 2.4) - 0.055, color.g <= 0.0031308 ? color.g * 12.92 : 1.055 * pow(color.g, 1.0 / 2.4) - 0.055, color.b <= 0.0031308 ? color.b * 12.92 : 1.055 * pow(color.b, 1.0 / 2.4) - 0.055);
}
float FieldInfluence(int index, float3 world, out float3 incoming)
{
    float3 delta = FieldLightPositionRange[index].xyz - world;
    float distance = length(delta);
    incoming = distance > 0.00001 ? delta / distance : -FieldLightDirectionIntensity[index].xyz;
    float cosine = distance > 0.00001 ? dot(-incoming, FieldLightDirectionIntensity[index].xyz) : 1;
    float cone = saturate((cosine - FieldLightCone[index].y) / (FieldLightCone[index].x - FieldLightCone[index].y));
    cone = cone * cone * (3 - 2 * cone);
    float attenuation = saturate(1 - distance / FieldLightPositionRange[index].w);
    return FieldLightDirectionIntensity[index].w * attenuation * attenuation * cone;
}
float2 FieldShadowCoverage(float3 world)
{
    if (FieldShadowParameters.x == 0 || FieldPrimaryIndex < 0)
    {
        return 0;
    }
    float4 projection = mul(float4(world, 1), FieldShadowMatrix);
    if (projection.w <= 0)
    {
        return 0;
    }
    float3 projectedPoint = projection.xyz / projection.w;
    float2 uv = float2(projectedPoint.x * 0.5 + 0.5, 0.5 - projectedPoint.y * 0.5);
    // Reciprocal light depth is affine over a projected plane. Compare each tap at its actual texel centre,
    // rather than comparing neighboring plane depths to one centre depth and shadowing the plane itself.
    float receiverDepth = FieldLightPositionRange[FieldPrimaryIndex].w / dot(world - FieldLightPositionRange[FieldPrimaryIndex].xyz, FieldLightDirectionIntensity[FieldPrimaryIndex].xyz);
    float3 gradientX = ddx(float3(uv, receiverDepth));
    float3 gradientY = ddy(float3(uv, receiverDepth));
    float determinant = gradientX.x * gradientY.y - gradientX.y * gradientY.x;
    float2 depthGradient = determinant != 0 ? float2(gradientY.y * gradientX.z - gradientX.y * gradientY.z, gradientX.x * gradientY.z - gradientY.x * gradientX.z) / determinant : 0;
    if (any(uv < 0) || any(uv > 1) || projectedPoint.z < 0 || projectedPoint.z > 1)
    {
        return 0;
    }
    float2 result = 0;
    [unroll] for (int y = -1; y <= 1; y++)
    {
        [unroll] for (int x = -1; x <= 1; x++)
        {
            float2 sampleUv = uv + float2(x, y) * FieldShadowParameters.z;
            float2 stored = SAMPLE_TEXTURE(FieldShadowTexture, FieldShadowSampler, sampleUv).rg;
            float2 texelCentre = (floor(sampleUv / FieldShadowParameters.w) + 0.5) * FieldShadowParameters.w;
            float comparisonDepth = receiverDepth + dot(depthGradient, texelCentre - uv);
            // Sixteen float ULPs at unit scale cover arithmetic roundoff without a world-space shadow offset.
            float roundoff = abs(comparisonDepth) * 0.0000019073486328125;
            float covered = comparisonDepth + roundoff < stored.x && all(sampleUv >= 0) && all(sampleUv <= 1) ? 1 : 0;
            result += float2(covered, covered * stored.y);
        }
    }
    return result / 9;
}
float4 FieldShade(float4 albedo, VSOutput input)
{
    float3 world = input.WorldPosition / input.Uv.z;
    float2 shadow = FieldShadowCoverage(world);
    float4 result = 0;
    if (FieldLightingPass > 1.5)
    {
        if (FieldPrimaryIndex >= 0)
        {
            float3 incoming = 0;
            float influence = FieldInfluence(FieldPrimaryIndex, world, incoming);
            float3 rgb = FieldLightColor[FieldPrimaryIndex].rgb;
            float strength = saturate(influence * max(rgb.r, max(rgb.g, rgb.b)));
            result = float4(0, 0, 0, shadow.y * strength * FieldShadowParameters.y);
        }
    }
    else
    {
        float3 normal = input.WorldNormal / input.Uv.z;
        normal = dot(normal, normal) > 0.000001 ? normalize(normal) : 0;
        float3 illumination = 0;
        [loop] for (int index = 0; index < FieldLightCount; index++)
        {
            float3 incoming = 0;
            float influence = FieldInfluence(index, world, incoming);
            float visibility = index == FieldPrimaryIndex ? 1 - shadow.x : 1;
            illumination += FieldLightColor[index].rgb * (influence * max(0, dot(normal, incoming)) * visibility);
        }
        result = float4(FieldSrgb(FieldLinear(max(albedo.rgb, 0)) * illumination), albedo.a);
    }
    return result;
}
// TODO(TD-032): The pinned DX12 input parser omits unused fields still present in the DXIL signature.
struct FieldShadowInput
{
    float3 WorldPosition : TEXCOORD5;
    float4 Color : COLOR0;
    float3 Uv : TEXCOORD0;
};
struct FieldShadowOutput
{
    float4 Position : SV_POSITION;
    float2 Uv : TEXCOORD0;
    float Level : TEXCOORD1;
    float Depth : TEXCOORD2;
};
FieldShadowOutput FieldShadowVertex(FieldShadowInput input)
{
    FieldShadowOutput output;
    output.Position = mul(float4(input.WorldPosition, 1), FieldShadowMatrix);
    output.Uv = input.Uv.xy;
    output.Level = input.Color.a;
    output.Depth = dot(input.WorldPosition - FieldLightPositionRange[FieldPrimaryIndex].xyz, FieldLightDirectionIntensity[FieldPrimaryIndex].xyz) / FieldLightPositionRange[FieldPrimaryIndex].w;
    return output;
}
float4 FieldShadowPixel(FieldShadowOutput input) : SV_TARGET0
{
    float4 texel = SAMPLE_TEXTURE(SourceTexture, TextureSampler, input.Uv);
    clip(texel.a - AlphaParameters.x);
    return float4(1 / input.Depth, input.Level, 0, 1);
}

struct PSOutput { float4 Color : SV_TARGET0; float Depth : SV_DEPTH; };
PSOutput PixelMainCore(VSOutput input, bool fieldLighting)
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
    if (fieldLighting)
    {
        color = FieldShade(color, input);
    }
    color.a *= MaterialParameters.z;
    color.a = ActorOpacityParameters.y > 0 ? ActorOpacityParameters.x : color.a * ActorOpacityParameters.x;
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
PSOutput PixelMain(VSOutput input)
{
    return PixelMainCore(input, false);
}
PSOutput FieldPixelMain(VSOutput input)
{
    return PixelMainCore(input, true);
}
technique TriangleTechnique
{
    pass P0
    {
        VertexShader = compile VS_PROFILE VertexMain();
        PixelShader = compile PS_PROFILE PixelMain();
    }
}

technique FieldLightingTechnique
{
    pass P0
    {
        VertexShader = compile VS_PROFILE VertexMain();
        PixelShader = compile PS_PROFILE FieldPixelMain();
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
technique Ff7UnderwaterTerrainTechnique
{
    pass P0
    {
        VertexShader = compile VS_PROFILE Ff7UnderwaterVertexMain();
        PixelShader = compile PS_PROFILE WorldPixelMain();
    }
}

technique FieldShadowTechnique
{
    pass P0
    {
        VertexShader = compile VS_PROFILE FieldShadowVertex();
        PixelShader = compile PS_PROFILE FieldShadowPixel();
    }
}
