float2 ViewportSize;
float4 MaterialParameters; // texture present, selects alpha, alpha multiplier, positive depth mapping
float4 AlphaParameters; // minimum, maximum, minimum reciprocal W, source RGB multiplier
float4 FogParameters; // start, end, unused, unused
float3 FogColor;
float4 ClipRectangle;
float2 TextureSize;
float DepthScale;
texture SourceTexture;
sampler TextureSampler = sampler_state { Texture = <SourceTexture>; };
struct VSInput { float3 Position : POSITION0; float4 Color : COLOR0; float3 Uv : TEXCOORD0; float4 TextureScreenRectangle : TEXCOORD2; float4 TextureTexelRectangle : TEXCOORD3; };
struct VSOutput { float4 Position : SV_POSITION; float4 Color : COLOR0; float3 Uv : TEXCOORD0; float Depth : TEXCOORD1; nointerpolation float4 TextureScreenRectangle : TEXCOORD2; nointerpolation float4 TextureTexelRectangle : TEXCOORD3; };
VSOutput VertexMain(VSInput input)
{
    VSOutput output;
    output.Position = float4(input.Position.x * 2 / ViewportSize.x - 1, 1 - input.Position.y * 2 / ViewportSize.y, 0, 1);
    output.Color = input.Color;
    output.Uv = input.Uv;
    output.Depth = input.Position.z;
    output.TextureScreenRectangle = input.TextureScreenRectangle;
    output.TextureTexelRectangle = input.TextureTexelRectangle;
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
        float2 uv = input.Uv.xy / input.Uv.z;
        if (input.TextureScreenRectangle.z > 0)
        {
            // Reconstruct local source pixels before adding the atlas origin.
            // Interpolated atlas UVs lose precision at source pixel boundaries.
            float2 local = (input.Position.xy - input.TextureScreenRectangle.xy) / input.TextureScreenRectangle.zw;
            float2 pixel = clamp(floor(local * input.TextureTexelRectangle.zw), 0, input.TextureTexelRectangle.zw - 1);
            uv = (input.TextureTexelRectangle.xy + pixel + 0.5) / TextureSize;
        }
        float4 texel = tex2D(TextureSampler, uv);
        if (FogParameters.w > 0)
        {
            float2 texelPosition = clamp(input.Uv.xy / input.Uv.z * TextureSize - 0.5, 0, TextureSize - 1);
            float2 origin = floor(texelPosition);
            float2 weight = frac(texelPosition);
            float4 c00 = tex2D(TextureSampler, (origin + float2(0.5, 0.5)) / TextureSize);
            float4 c10 = tex2D(TextureSampler, (origin + float2(1.5, 0.5)) / TextureSize);
            float4 c01 = tex2D(TextureSampler, (origin + float2(0.5, 1.5)) / TextureSize);
            float4 c11 = tex2D(TextureSampler, (origin + float2(1.5, 1.5)) / TextureSize);
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
        VertexShader = compile vs_4_0 VertexMain();
        PixelShader = compile ps_4_0 PixelMain();
    }
}
