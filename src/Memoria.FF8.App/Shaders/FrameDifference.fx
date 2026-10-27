#if VULKAN || DIRECTX12
#define VS_PROFILE vs_6_0
#define PS_PROFILE ps_6_0
#define SAMPLE_TEXTURE(textureName, samplerName, uv) textureName.Sample(samplerName, uv)
#else
#define VS_PROFILE vs_4_0
#define PS_PROFILE ps_4_0
#define SAMPLE_TEXTURE(textureName, samplerName, uv) tex2D(samplerName, uv)
#endif

float2 InverseSize;
float2 ViewportOrigin;
#if VULKAN || DIRECTX12
Texture2D CurrentTexture : register(t0);
#else
texture CurrentTexture;
#endif
#if VULKAN || DIRECTX12
Texture2D PreviousTexture : register(t1);
#else
texture PreviousTexture;
#endif
#if VULKAN || DIRECTX12
sampler CurrentSampler : register(s0) = sampler_state
#else
sampler CurrentSampler = sampler_state
#endif
{
    Texture = <CurrentTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};
#if VULKAN || DIRECTX12
sampler PreviousSampler : register(s1) = sampler_state
#else
sampler PreviousSampler = sampler_state
#endif
{
    Texture = <PreviousTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

float4 DifferencePixel(float4 position : SV_POSITION) : SV_TARGET0
{
    float2 uv = (position.xy - ViewportOrigin) * InverseSize;
    float3 difference = abs(SAMPLE_TEXTURE(CurrentTexture, CurrentSampler, uv).rgb - SAMPLE_TEXTURE(PreviousTexture, PreviousSampler, uv).rgb);
    // The output is quantized to bytes. A single RGB byte change must survive; alpha is not displayed.
    clip(max(difference.r, max(difference.g, difference.b)) - 0.5 / 255.0);
    return 0;
}

technique FrameDifference
{
    pass Pass0
    {
        PixelShader = compile PS_PROFILE DifferencePixel();
    }
}
