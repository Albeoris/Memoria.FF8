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
float SceneAlphaCoverage; // legacy, opaque, mask, opaque core, fractional, blend
float4 SceneSampler; // wrap S, wrap T, min filter, mag filter (glTF enums)
float4 FogParameters; // start, end, unused, unused
float3 FogColor;
float4 ClipRectangle;
float2 TextureSize;
float2 TextureSize1;
float2 TextureSize2;
float2 TextureSize3;
float2 TextureSize4;
float2 TextureSize5;
float2 TextureSize6;
float2 TextureSize7;
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
sampler TextureSampler : register(s0) = sampler_state
{ Texture = <SourceTexture>; };
#if VULKAN || DIRECTX12
Texture2D SourceTexture1 : register(t1);
#else
texture SourceTexture1;
#endif
sampler TextureSampler1 : register(s1) = sampler_state
{ Texture = <SourceTexture1>; };
#if VULKAN || DIRECTX12
Texture2D SourceTexture2 : register(t2);
#else
texture SourceTexture2;
#endif
sampler TextureSampler2 : register(s2) = sampler_state
{ Texture = <SourceTexture2>; };
#if VULKAN || DIRECTX12
Texture2D SourceTexture3 : register(t3);
#else
texture SourceTexture3;
#endif
sampler TextureSampler3 : register(s3) = sampler_state
{ Texture = <SourceTexture3>; };
#if VULKAN || DIRECTX12
Texture2D SourceTexture4 : register(t4);
#else
texture SourceTexture4;
#endif
sampler TextureSampler4 : register(s4) = sampler_state
{ Texture = <SourceTexture4>; };
#if VULKAN || DIRECTX12
Texture2D SourceTexture5 : register(t5);
#else
texture SourceTexture5;
#endif
sampler TextureSampler5 : register(s5) = sampler_state
{ Texture = <SourceTexture5>; };
#if VULKAN || DIRECTX12
Texture2D SourceTexture6 : register(t6);
#else
texture SourceTexture6;
#endif
sampler TextureSampler6 : register(s6) = sampler_state
{ Texture = <SourceTexture6>; };
#if VULKAN || DIRECTX12
Texture2D SourceTexture7 : register(t7);
#else
texture SourceTexture7;
#endif
sampler TextureSampler7 : register(s7) = sampler_state
{ Texture = <SourceTexture7>; };
float4 SceneSun; // direction toward the sun in world space, directional shading strength
float4 SceneLinearTint; // RGB multiplier, active flag
float4 ColorGradeParameters; // first size, second size, interpolation, strength
#if VULKAN || DIRECTX12
Texture2D ColorGradeFirst : register(t9);
Texture2D ColorGradeSecond : register(t10);
#else
texture ColorGradeFirst;
texture ColorGradeSecond;
#endif
sampler ColorGradeFirstSampler : register(s9) = sampler_state
{ Texture = <ColorGradeFirst>; MinFilter = Linear; MagFilter = Linear; AddressU = Clamp; AddressV = Clamp; };
sampler ColorGradeSecondSampler : register(s10) = sampler_state
{ Texture = <ColorGradeSecond>; MinFilter = Linear; MagFilter = Linear; AddressU = Clamp; AddressV = Clamp; };

float4 ColorGradeCoordinates(float3 color, float size)
{
    float blue = color.b * (size - 1);
    float slice = floor(blue);
    float next = min(slice + 1, size - 1);
    float2 cell = color.rg * (size - 1) + 0.5;
    return float4((float2(slice, next) * size + cell.x) / (size * size), cell.y / size, blue - slice);
}

float3 ApplyColorGrade(float3 color)
{
    if (ColorGradeParameters.w <= 0)
    {
        return color;
    }
    float3 first = color;
    if (ColorGradeParameters.x > 0)
    {
        float4 coordinate = ColorGradeCoordinates(color, ColorGradeParameters.x);
        first = lerp(SAMPLE_TEXTURE(ColorGradeFirst, ColorGradeFirstSampler, coordinate.xz).rgb, SAMPLE_TEXTURE(ColorGradeFirst, ColorGradeFirstSampler, coordinate.yz).rgb, coordinate.w);
    }
    float3 second = color;
    if (ColorGradeParameters.y > 0 && ColorGradeParameters.z > 0)
    {
        float4 coordinate = ColorGradeCoordinates(color, ColorGradeParameters.y);
        second = lerp(SAMPLE_TEXTURE(ColorGradeSecond, ColorGradeSecondSampler, coordinate.xz).rgb, SAMPLE_TEXTURE(ColorGradeSecond, ColorGradeSecondSampler, coordinate.yz).rgb, coordinate.w);
    }
    return lerp(color, lerp(first, second, ColorGradeParameters.z), ColorGradeParameters.w);
}

float2 SelectedTextureSize(float slot)
{
    switch ((int)slot)
    {
        case 1: return TextureSize1;
        case 2: return TextureSize2;
        case 3: return TextureSize3;
        case 4: return TextureSize4;
        case 5: return TextureSize5;
        case 6: return TextureSize6;
        case 7: return TextureSize7;
        default: return TextureSize;
    }
}
float4 SampleSourceTexture(float slot, float2 uv)
{
    switch ((int)slot)
    {
        case 1: return SAMPLE_TEXTURE(SourceTexture1, TextureSampler1, uv);
        case 2: return SAMPLE_TEXTURE(SourceTexture2, TextureSampler2, uv);
        case 3: return SAMPLE_TEXTURE(SourceTexture3, TextureSampler3, uv);
        case 4: return SAMPLE_TEXTURE(SourceTexture4, TextureSampler4, uv);
        case 5: return SAMPLE_TEXTURE(SourceTexture5, TextureSampler5, uv);
        case 6: return SAMPLE_TEXTURE(SourceTexture6, TextureSampler6, uv);
        case 7: return SAMPLE_TEXTURE(SourceTexture7, TextureSampler7, uv);
        default: return SAMPLE_TEXTURE(SourceTexture, TextureSampler, uv);
    }
}
struct VSInput { float3 WorldPosition : TEXCOORD5; float3 WorldNormal : TEXCOORD6; float3 Position : POSITION0; float4 Color : COLOR0; float3 Uv : TEXCOORD0; float4 TextureScreenRectangle : TEXCOORD2; float4 TextureTexelRectangle : TEXCOORD3; float TextureSlot : TEXCOORD7; };
struct VSOutput { float3 WorldPosition : TEXCOORD5; float3 WorldNormal : TEXCOORD6; float4 Position : SV_POSITION; float4 Color : COLOR0; float3 Uv : TEXCOORD0; float Depth : TEXCOORD1; nointerpolation float4 TextureScreenRectangle : TEXCOORD2; nointerpolation float4 TextureTexelRectangle : TEXCOORD3; centroid float3 TextureUv : TEXCOORD4; nointerpolation float TextureSlot : TEXCOORD7; };
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
    output.TextureSlot = input.TextureSlot;
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
    ff7.TextureSlot = 0;
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
    output.WorldNormal = input.Normal;
    output.Uv = float3(input.Uv, 1);
    output.TextureUv = output.Uv;
    output.Depth = cameraZ;
    output.TextureScreenRectangle = 0;
    output.TextureTexelRectangle = 0;
    output.TextureSlot = 0;
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
float2 FieldLightingPreview;
int FieldLightCount;
int FieldPrimaryIndex;
float4 FieldLightPositionRange[8];
float4 FieldLightDirectionIntensity[8];
float4 FieldLightColor[8];
float4 FieldLightCone[8]; // inner cosine, outer cosine, sunlight enabled, unused
// TODO(TD-032): Keep this uniform name short until the pinned DX12 compiler accepts adjacent reflection separators.
float4x4 FieldShadowMatrix;
float4 FieldShadowParameters; // available, ground opacity, PCF radius in UV units, texel size
#if VULKAN || DIRECTX12
Texture2D FieldShadowTexture : register(t8);
sampler FieldShadowSampler : register(s8) = sampler_state
#else
texture FieldShadowTexture;
sampler FieldShadowSampler : register(s8) = sampler_state
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
float SceneTexelIndex(float index, float size, float wrapMode)
{
    if (wrapMode == 33071) return clamp(index, 0, size - 1);
    if (wrapMode == 33648)
    {
        float mirrored = frac(index / (2 * size)) * (2 * size);
        return min(mirrored, 2 * size - 1 - mirrored);
    }
    return frac(index / size) * size;
}
float4 ReadSceneTexel(float slot, float2 index, float2 size)
{
    float2 wrapped = float2(SceneTexelIndex(index.x, size.x, SceneSampler.x), SceneTexelIndex(index.y, size.y, SceneSampler.y));
    float4 sampled = SampleSourceTexture(slot, (wrapped + 0.5) / size);
    return float4(FieldLinear(sampled.rgb), SceneAlphaCoverage == 1 ? 1 : sampled.a);
}
float4 SceneTexture(float slot, float2 uv)
{
    float2 size = SelectedTextureSize(slot);
    float2 position = uv * size;
    float2 horizontal = ddx(position);
    float2 vertical = ddy(position);
    float filter = max(dot(horizontal, horizontal), dot(vertical, vertical)) > 1 ? SceneSampler.z : SceneSampler.w;
    if (filter == 9728 || filter == 9984 || filter == 9986)
    {
        return ReadSceneTexel(slot, floor(position), size);
    }
    // Filter linear premultiplied texels, with straight alpha at the blending boundary.
    float2 origin = floor(position - 0.5);
    float2 factor = frac(position - 0.5);
    float4 a = ReadSceneTexel(slot, origin, size);
    float4 b = ReadSceneTexel(slot, origin + float2(1, 0), size);
    float4 c = ReadSceneTexel(slot, origin + float2(0, 1), size);
    float4 d = ReadSceneTexel(slot, origin + 1, size);
    a.rgb *= a.a;
    b.rgb *= b.a;
    c.rgb *= c.a;
    d.rgb *= d.a;
    float4 sampled = lerp(lerp(a, b, factor.x), lerp(c, d, factor.x), factor.y);
    if (sampled.a > 0) sampled.rgb /= sampled.a;
    return sampled;
}

float ApplySceneCoverage(float alpha)
{
    // At 0.999 the preceding 8-bit alpha value (254/255) remains fractional.
    const float opaque = 0.999;
    if (SceneAlphaCoverage == 1) return 1;
    if (SceneAlphaCoverage == 2)
    {
        clip(alpha - AlphaParameters.x);
        return 1;
    }
    if (SceneAlphaCoverage == 3)
    {
        clip(alpha - opaque);
        return 1;
    }
    if (alpha <= 0 || (SceneAlphaCoverage == 4 && alpha >= opaque)) discard;
    return alpha;
}
float FieldInfluence(int index, float3 world, out float3 incoming)
{
    float influence = FieldLightDirectionIntensity[index].w;
    incoming = -FieldLightDirectionIntensity[index].xyz;
    if (FieldLightCone[index].z == 0)
    {
        float3 delta = FieldLightPositionRange[index].xyz - world;
        float distance = length(delta);
        incoming = distance > 0.00001 ? delta / distance : incoming;
        float cosine = distance > 0.00001 ? dot(-incoming, FieldLightDirectionIntensity[index].xyz) : 1;
        float cone = saturate((cosine - FieldLightCone[index].y) / (FieldLightCone[index].x - FieldLightCone[index].y));
        cone = cone * cone * (3 - 2 * cone);
        float attenuation = saturate(1 - distance / FieldLightPositionRange[index].w);
        influence = influence * attenuation * attenuation * cone;
    }
    return influence;
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
    // Orthographic depth and reciprocal perspective depth are affine over a projected plane.
    // Evaluate the receiver depth at each tap's texel centre to account for receiver slope.
    bool sunlight = FieldLightCone[FieldPrimaryIndex].z > 0;
    float receiverDepth = sunlight ? 1 - projectedPoint.z : FieldLightPositionRange[FieldPrimaryIndex].w / dot(world - FieldLightPositionRange[FieldPrimaryIndex].xyz, FieldLightDirectionIntensity[FieldPrimaryIndex].xyz);
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
            // The orthographic encoding subtracts from one, so its roundoff allowance uses unit scale.
            float roundoff = (sunlight ? 1 : abs(comparisonDepth)) * 0.0000019073486328125;
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
    if (FieldLightingPass == 2 || FieldLightingPass == 3)
    {
        float amount = 0;
        if (FieldPrimaryIndex >= 0)
        {
            float3 incoming = 0;
            float influence = FieldInfluence(FieldPrimaryIndex, world, incoming);
            float3 rgb = FieldLightColor[FieldPrimaryIndex].rgb;
            float strength = saturate(influence * max(rgb.r, max(rgb.g, rgb.b)));
            amount = saturate(shadow.y * strength * FieldShadowParameters.y);
        }
        if (FieldLightingPass == 3)
        {
            result = float4(FieldSrgb(FieldLinear(albedo.rgb) * (1 - amount)), albedo.a);
        }
        else
        {
            result = float4(0, 0, 0, amount * albedo.a);
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
            float visibility = index == FieldPrimaryIndex && FieldLightingPass != 4 ? 1 - shadow.x : 1;
            float diffuse = max(0, (dot(normal, incoming) + FieldLightingPreview.y) / (1 + FieldLightingPreview.y));
            illumination += FieldLightColor[index].rgb * (influence * diffuse * visibility);
        }
        float3 surface = FieldLightingPreview.x > 0 ? float3(0.5, 0.5, 0.5) : albedo.rgb;
        result = float4(FieldSrgb(FieldLinear(max(surface, 0)) * illumination), albedo.a);
    }
    return result;
}
// TODO(TD-032): The pinned DX12 input parser omits unused fields still present in the DXIL signature.
struct FieldShadowInput
{
    float3 WorldPosition : TEXCOORD5;
    float TextureSlot : TEXCOORD7;
    float4 Color : COLOR0;
    float3 Uv : TEXCOORD0;
};
struct FieldShadowOutput
{
    nointerpolation float TextureSlot : TEXCOORD7;
    float4 Position : SV_POSITION;
    float2 Uv : TEXCOORD0;
    float Level : TEXCOORD1;
    float Depth : TEXCOORD2;
};
FieldShadowOutput FieldShadowVertex(FieldShadowInput input)
{
    FieldShadowOutput output;
    output.TextureSlot = input.TextureSlot;
    output.Position = mul(float4(input.WorldPosition, 1), FieldShadowMatrix);
    output.Uv = input.Uv.xy;
    output.Level = input.Color.a;
    output.Depth = FieldLightCone[FieldPrimaryIndex].z > 0 ? output.Position.z : dot(input.WorldPosition - FieldLightPositionRange[FieldPrimaryIndex].xyz, FieldLightDirectionIntensity[FieldPrimaryIndex].xyz) / FieldLightPositionRange[FieldPrimaryIndex].w;
    return output;
}
float4 FieldShadowPixel(FieldShadowOutput input) : SV_TARGET0
{
    float level = input.Level;
    if (SceneAlphaCoverage > 0)
    {
        float alpha = MaterialParameters.x > 0 ? SceneTexture(input.TextureSlot, input.Uv).a : 1;
        level = ApplySceneCoverage(alpha * level);
    }
    else
    {
        float4 texel = SampleSourceTexture(input.TextureSlot, input.Uv);
        clip(texel.a - AlphaParameters.x);
    }
    float depth = FieldLightCone[FieldPrimaryIndex].z > 0 ? 1 - input.Depth : 1 / input.Depth;
    return float4(depth, level, 0, 1);
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
    if (SceneAlphaCoverage > 0)
    {
        clip(input.Uv.z - 0.0000000001);
        color /= input.Uv.z;
        if (MaterialParameters.x > 0)
        {
            color *= SceneTexture(input.TextureSlot, input.Uv.xy / input.Uv.z);
        }
        color.a = ApplySceneCoverage(color.a);
        color.rgb = FieldSrgb(color.rgb);
    }
    else if (MaterialParameters.x > 0)
    {
        clip(input.Uv.z - 0.0000000001);
        // A partially covered MSAA pixel can have its center outside the face.
        // Keep model texture/alpha samples inside it; Uv remains centered for depth and fog.
        float3 textureUv = CentroidTextureSampling ? input.TextureUv : input.Uv;
        float2 interpolatedUv = textureUv.xy / textureUv.z;
        float2 textureSize = SelectedTextureSize(input.TextureSlot);
        float2 uv = interpolatedUv;
        if (input.TextureScreenRectangle.z > 0)
        {
            // Reconstruct local source pixels before adding the atlas origin.
            // Interpolated atlas UVs lose precision at source pixel boundaries.
            float2 local = (input.Position.xy - input.TextureScreenRectangle.xy) / input.TextureScreenRectangle.zw;
            float2 pixel = clamp(floor(local * input.TextureTexelRectangle.zw), 0, input.TextureTexelRectangle.zw - 1);
            uv = (input.TextureTexelRectangle.xy + pixel + 0.5) / textureSize;
        }
        float4 texel = SampleSourceTexture(input.TextureSlot, uv);
        if (FogParameters.w > 1.5)
        {
            float2 cell = interpolatedUv * textureSize - 0.5;
            float2 vertex = floor(cell);
            float2 weight = frac(cell);
            float2 low = (clamp(vertex, 0, textureSize - 1) + 0.5) / textureSize;
            float2 high = (clamp(vertex + 1, 0, textureSize - 1) + 0.5) / textureSize;
            float4 c00 = SampleSourceTexture(input.TextureSlot, low);
            float4 c10 = SampleSourceTexture(input.TextureSlot, float2(high.x, low.y));
            float4 c01 = SampleSourceTexture(input.TextureSlot, float2(low.x, high.y));
            float4 c11 = SampleSourceTexture(input.TextureSlot, high);
            float4 weights = float4((1-weight.x)*(1-weight.y), weight.x*(1-weight.y), (1-weight.x)*weight.y, weight.x*weight.y);
            weights *= float4(c00.a, c10.a, c01.a, c11.a);
            texel.a = dot(weights, float4(1, 1, 1, 1));
            texel.rgb = (c00.rgb*weights.x + c10.rgb*weights.y + c01.rgb*weights.z + c11.rgb*weights.w) / max(0.00001, texel.a);
        }
        else if (FogParameters.w > 0)
        {
            // Integer source UVs are smooth RGB texel centers; texel above retains point-sampled cutout coverage.
            float2 texelPosition = clamp(interpolatedUv * textureSize, 0, textureSize - 1);
            float2 origin = floor(texelPosition);
            float2 weight = frac(texelPosition);
            float4 c00 = SampleSourceTexture(input.TextureSlot, (origin + float2(0.5, 0.5)) / textureSize);
            float4 c10 = SampleSourceTexture(input.TextureSlot, (origin + float2(1.5, 0.5)) / textureSize);
            float4 c01 = SampleSourceTexture(input.TextureSlot, (origin + float2(0.5, 1.5)) / textureSize);
            float4 c11 = SampleSourceTexture(input.TextureSlot, (origin + float2(1.5, 1.5)) / textureSize);
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
    if (SceneLinearTint.w > 0 || SceneSun.w > 0)
    {
        float3 tint = SceneLinearTint.w > 0 ? SceneLinearTint.rgb : 1;
        float magnitude = dot(input.WorldNormal, input.WorldNormal);
        if (SceneSun.w > 0 && magnitude > 1e-12)
        {
            float facing = clamp(dot(input.WorldNormal * rsqrt(magnitude), SceneSun.xyz), -1, 1);
            tint *= 1 - SceneSun.w * (0.5 - 0.5 * facing);
        }
        color.rgb = FieldSrgb(saturate(FieldLinear(color.rgb) * tint));
    }
    color.rgb = ApplyColorGrade(color.rgb);
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
