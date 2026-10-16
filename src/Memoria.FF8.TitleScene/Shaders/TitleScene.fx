float Time;
float Aspect;
float InverseHeight;
texture Texture;
sampler TextureSampler = sampler_state
{
    Texture = <Texture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

float hash(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

float noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + 1), f.x), f.y);
}

float fbm(float2 p)
{
    float sum = 0;
    float weight = 0.52;
    [unroll]
    for (int i = 0; i < 6; i++)
    {
        sum += noise(p) * weight;
        p = float2(p.x * 1.62 - p.y * 1.26, p.x * 1.26 + p.y * 1.62) + 13.7;
        weight *= 0.48;
    }
    return sum;
}

float strands(float2 p)
{
    return noise(p) * 0.50 + noise(p * float2(2.07, 1.73) + 17.1) * 0.27 + noise(p * float2(4.13, 3.17) + 31.7) * 0.15 + noise(p * float2(8.11, 5.91) + 45.2) * 0.08;
}

float2 scene(float2 uv)
{
    return float2((uv.x - 0.5) * Aspect, uv.y);
}

float hash3(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float noise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = lerp(lerp(hash3(i), hash3(i + float3(1, 0, 0)), f.x), lerp(hash3(i + float3(0, 1, 0)), hash3(i + float3(1, 1, 0)), f.x), f.y);
    float b = lerp(lerp(hash3(i + float3(0, 0, 1)), hash3(i + float3(1, 0, 1)), f.x), lerp(hash3(i + float3(0, 1, 1)), hash3(i + 1), f.x), f.y);
    return lerp(a, b, f.z);
}

float cloudDensity(float3 p)
{
    float n = noise3(p) * 0.49 + noise3(p * 2.03 + 17.1) * 0.27 + noise3(p * 4.11 + 39.7) * 0.15 + noise3(p * 8.23 + 61.3) * 0.07 + noise3(p * 16.41 + 87.1) * 0.02;
    return max(0, n - 0.47) * 6.2;
}

float3 clouds(float2 p, float t)
{
    float3 sum = 0;
    float transmittance = 1;
    float2 xy = p * float2(8.5, 9.5) + float2(t * 0.15, -t * 0.06);
    float2 warp = float2(fbm(p * 4.0 + t * 0.05), fbm(p * 4.0 + 13.1 - t * 0.04));
    xy += warp * 1.2;
    float2 lightDirection = normalize(float2(0.0, 0.065) - p + float2(0.0001, 0));
    float upperGlow = exp(-length(p - float2(0, 0.065)) * 2.3);
    float sunsetGlow = exp(-length((p - float2(0.63, 0.335)) * float2(1.0, 2.0)) * 2.0) * smoothstep(0.18, 0.62, p.x);
    [loop]
    for (int i = 0; i < 14; i++)
    {
        float z = i * 0.065;
        float3 samplePosition = float3(xy, z);
        float density = cloudDensity(samplePosition);
        float lightDensity = cloudDensity(samplePosition + float3(lightDirection * 0.21, 0.15));
        float illumination = pow(saturate(0.30 + (density - lightDensity) * 2.2), 2.0) * exp(-lightDensity * 3.0);
        float3 ambient = float3(0.024, 0.019, 0.030);
        float3 warm = float3(2.10, 0.92, 0.83) * upperGlow * (1 - smoothstep(0.25, 0.55, p.y) * 0.4) + float3(2.5, 1.1, 0.55) * sunsetGlow;
        float3 cloudColor = ambient + warm * illumination;
        float opacity = 1 - exp(-density * 0.22);
        sum += cloudColor * opacity * transmittance;
        transmittance *= 1 - opacity;
    }
    float detail = fbm(p * 24.0 + warp * 3.7 + float2(t * 0.12, -t * 0.08));
    float detailShadow = fbm((p + lightDirection * 0.005) * 24.0 + warp * 3.7 + float2(t * 0.12, -t * 0.08));
    float rimDetail = saturate(0.18 + (detail - detailShadow) * 5.5);
    return sum * (0.42 + detail * 0.9) + sum * float3(1.0, 0.8, 0.68) * rimDetail;
}

// Flow coordinates stretch turbulent detail along curved sheets instead of drawing smooth analytic strips.
float3 crimsonFlow(float2 p, float t)
{
    float2 q = p - float2(-0.035, 0.245);
    q.y *= 1.36;
    float r = length(q);
    float angle = atan2(q.y, q.x);
    // The angular seam sits below the frame, inside its black fade.
    angle = angle < -1.5707963 ? angle + 6.2831853 : angle;
    float2 warpP = p * float2(4.0, 6.0) + float2(t * 0.11, -t * 0.07);
    float warp = noise(warpP * 0.6) * 0.6 + noise(warpP * 1.2 + 13.1) * 0.3 + noise(warpP * 2.4 + 41.9) * 0.1 - 0.5;
    float fineWarp = fbm(warpP * 2.9 + 14.3) - 0.5;
    float flow = angle + log(r + 0.18) * 0.92 + warp * 0.80 + fineWarp * 0.015 + sin(r * 7.0 - t * 0.21) * 0.16;
    float radial = r * 4.2 - t * 0.18;
    float broad = noise(float2(flow * 3.9, radial * 0.33)) * 0.7 + noise(float2(flow * 7.8 + 19.0, radial * 0.57)) * 0.3;
    float fabric = strands(float2(flow * 33.0 + fineWarp * 0.8, radial * 4.25));
    float fibers = strands(float2(flow * 155.0 + warp * 4.0, radial * 22.0));
    float shredded = fbm(p * 42.0 + float2(t * 0.3, -t * 0.12));
    float sheet = smoothstep(0.31, 0.66, broad + (fabric - 0.5) * 0.20);
    float tears = smoothstep(0.26, 0.66, shredded + broad * 0.12);
    float face = sheet * tears;
    float fold = pow(saturate(fabric * 1.45), 2.4);
    float filament = pow(saturate(fibers * 1.85 - 0.44), 5.0) * pow(saturate(shredded * 1.45), 3.0);
    float patches = smoothstep(0.23, 0.70, fbm(p * 13.0 + warp * 2.8));
    float3 deep = float3(0.23, 0.0017, 0.011);
    float3 red = float3(1.50, 0.017, 0.043);
    float3 hot = float3(1.9, 0.25, 0.20);
    float brightness = 0.3 + 0.7 * patches;
    float3 result = (deep + red * fold + hot * filament * 0.55) * face * brightness;
    float broadFace = sheet * (0.4 + fabric * 0.6) * (0.45 + tears * 0.55);
    result += float3(0.42, 0.003, 0.016) * broadFace * brightness;
    // Broken surface highlights and sparks, rather than continuous neon piping.
    float sparks = pow(saturate(shredded * 1.65 - 0.33), 8.0) * filament * face;
    result += float3(3.0, 0.38, 0.27) * sparks;
    float distanceFade = smoothstep(0.015, 0.12, r) * (0.85 + 0.15 * sin(angle * 3.0 + 0.7));
    return result * distanceFade;
}

float4 SurfacePixel(float4 position : SV_POSITION, float4 color : COLOR0, float2 uv : TEXCOORD0) : SV_TARGET
{
    [branch]
    if (uv.y > 0.61)
    {
        return float4(0, 0, 0, 1);
    }
    float2 p = scene(uv);
    float t = Time * 0.075;
    float3 result = clouds(p, t);
    float3 flow = crimsonFlow(p, t);
    result = result * (1 - saturate(max(flow.r - 0.06, 0.0) * 1.8)) + flow;
    // A warm opening low on the right and its broken reflection in the water.
    float horizon = 0.345;
    float2 sun = p - float2(0.64, horizon);
    float sunset = exp(-dot(sun * float2(3.7, 18.0), sun * float2(3.7, 18.0)));
    float rightOpening = smoothstep(0.18, 0.60, p.x) * (1 - smoothstep(0.39, 0.49, p.y));
    result = lerp(result, result * 0.28 + float3(0.61, 0.17, 0.055) * sunset, rightOpening * 0.65);
    result += float3(1.3, 0.57, 0.23) * exp(-dot(sun * float2(53.0, 112.0), sun * float2(53.0, 112.0)));
    float below = max(0, p.y - horizon);
    float reflection = exp(-pow((p.x - 0.64 + sin(p.y * 220.0 + t) * 0.007) / (0.013 + below * 0.48), 2.0));
    float ripple = pow(saturate(0.5 + 0.32 * sin(p.y * 910.0 + noise(float2(p.y * 180.0, t)) * 8.0) + 0.18 * sin(p.y * 483.0 - t)), 3.0);
    result += float3(1.2, 0.35, 0.11) * reflection * ripple * exp(-below * 10.0) * step(horizon, p.y);
    result *= 1.0 - smoothstep(0.49, 0.59, p.y);
    return float4(result, 1);
}

float4 CompositePixel(float4 position : SV_POSITION, float4 color : COLOR0, float2 uv : TEXCOORD0) : SV_TARGET
{
    [branch]
    if (uv.y > 0.61)
    {
        return float4(0, 0, 0, 1);
    }
    float2 p = scene(uv);
    float3 result = tex2D(TextureSampler, uv).rgb;
    // Native-resolution fibres follow the same coarse flow as the bounded material field.
    // Red coverage suppresses them in clouds, black gaps and the warm water opening.
    float redCoverage = saturate((result.r - result.g * 1.8 - result.b * 0.8) * 2.5);
    float t = Time * 0.075;
    float2 q = (p - float2(-0.035, 0.245)) * float2(1.0, 1.36);
    float r = length(q);
    float flowAngle = atan2(q.y, q.x);
    flowAngle = flowAngle < -1.5707963 ? flowAngle + 6.2831853 : flowAngle;
    float2 warpP = p * float2(4.0, 6.0) + float2(t * 0.11, -t * 0.07);
    float warp = noise(warpP * 0.6) * 0.6 + noise(warpP * 1.2 + 13.1) * 0.3 + noise(warpP * 2.4 + 41.9) * 0.1 - 0.5;
    float flow = flowAngle + log(r + 0.18) * 0.92 + warp * 0.80 + sin(r * 7.0 - t * 0.21) * 0.16;
    float breakup = noise(float2(flow * 41.0, r * 32.0 - t * 0.6));
    float phase = flow * 431.0 + sin(r * 29.0 + flow * 7.0) * 2.3 + breakup * 2.2;
    float aa = max(fwidth(phase), 0.004);
    float vein = (1 - smoothstep(0.0, aa * 0.8, abs(sin(phase)))) * (1 - smoothstep(0.8, 2.0, aa));
    float phase2 = flow * 719.0 + r * 11.0 + breakup * 3.8;
    float aa2 = max(fwidth(phase2), 0.004);
    float hair = (1 - smoothstep(0.0, aa2 * 0.65, abs(sin(phase2)))) * (1 - smoothstep(0.8, 2.0, aa2));
    float broken = smoothstep(0.38, 0.76, breakup) * smoothstep(0.035, 0.12, r);
    result += float3(0.10, 0.012, 0.018) * (vein + hair * 0.5) * redCoverage * broken;
    result *= 1 - redCoverage * (0.013 + 0.013 * sin(phase2));
    float2 light = p - float2(0.0, 0.065);
    float radius = length(light);
    float halo = exp(-radius * 12.0);
    float core = exp(-radius * radius * 2700.0);
    float wide = exp(-radius * radius * 50.0);
    float angle = atan2(light.y, light.x);
    float rays = pow(saturate(0.5 + 0.25 * sin(angle * 31.0 + Time * 0.013) + 0.25 * sin(angle * 43.0)), 4.0);
    result += float3(0.53, 0.27, 0.23) * wide + float3(1.35, 0.83, 0.61) * halo;
    result += float3(2.7, 2.5, 2.1) * core;
    result += float3(0.23, 0.11, 0.085) * rays * exp(-radius * 7.0);
    float ring = exp(-abs(radius - 0.085) / max(InverseHeight, 0.0008)) * 0.14 + exp(-abs(radius - 0.104) / max(InverseHeight, 0.0007)) * 0.065;
    result += float3(0.85, 0.46, 0.36) * ring;
    result *= 1 - smoothstep(0.49, 0.59, p.y);
    float underline = exp(-abs(p.y - 0.571) / max(InverseHeight * 0.7, 0.0004)) * exp(-pow(abs(p.x) / 0.59, 6.0));
    float underlineGlow = exp(-abs(p.y - 0.571) * 650.0) * exp(-abs(p.x) * 3.0);
    float2 glint = p - float2(0, 0.571);
    result += float3(0.60, 0.018, 0.030) * underline + float3(0.11, 0.003, 0.006) * underlineGlow;
    result += float3(1.2, 0.55, 0.5) * exp(-dot(glint * float2(1100.0, 1100.0), glint * float2(1100.0, 1100.0)));
    // Filmic highlight shoulder keeps bright filament texture instead of clipping it to solid red.
    result = 1.0 - exp(-result * 1.4);
    result *= 1.0 - 0.30 * smoothstep(0.40, 1.05, length(float2(p.x, (p.y - 0.25) * 1.4)));
    return float4(result, 1);
}

technique Surface
{
    pass Pass0
    {
        PixelShader = compile ps_4_0 SurfacePixel();
    }
}

technique Composite
{
    pass Pass0
    {
        PixelShader = compile ps_4_0 CompositePixel();
    }
}
