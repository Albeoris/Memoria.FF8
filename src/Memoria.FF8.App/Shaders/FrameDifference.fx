float2 InverseSize;
float2 ViewportOrigin;
texture CurrentTexture;
texture PreviousTexture;
sampler CurrentSampler = sampler_state
{
    Texture = <CurrentTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};
sampler PreviousSampler = sampler_state
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
    float3 difference = abs(tex2D(CurrentSampler, uv).rgb - tex2D(PreviousSampler, uv).rgb);
    // The output is quantized to bytes. A single RGB byte change must survive; alpha is not displayed.
    clip(max(difference.r, max(difference.g, difference.b)) - 0.5 / 255.0);
    return 0;
}

technique FrameDifference
{
    pass Pass0
    {
        PixelShader = compile ps_4_0 DifferencePixel();
    }
}
