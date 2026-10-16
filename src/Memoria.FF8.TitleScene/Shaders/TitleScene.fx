float Time;
float LightIntensity;
float VortexRotation;
float VortexTravel;
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

// A cylindrical noise lattice closes around the vortex. Wrapping the rotation
// never exposes atan2's branch cut or resets the incoming material.
float cylinderNoise(float angle, float radial, float cells)
{
    float2 p = float2(angle * (cells / 6.28318530718), radial);
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float left = i.x - floor(i.x / cells) * cells;
    float right = left + 1;
    right -= floor(right / cells) * cells;
    return lerp(lerp(hash(float2(left, i.y)), hash(float2(right, i.y)), f.x), lerp(hash(float2(left, i.y + 1)), hash(float2(right, i.y + 1)), f.x), f.y);
}

float flowStrands(float angle, float radial, float cells)
{
    return cylinderNoise(angle, radial, cells) * 0.50 + cylinderNoise(angle + 0.17, radial * 1.73 + 17.1, cells * 2) * 0.27 + cylinderNoise(angle + 0.31, radial * 3.17 + 31.7, cells * 4) * 0.15 + cylinderNoise(angle + 0.53, radial * 5.91 + 45.2, cells * 8) * 0.08;
}

float2 flowCoordinates(float2 p, float t)
{
    float2 q = (p - float2(-0.035, 0.245)) * float2(1.0, 1.36);
    float r = length(q);
    float logRadius = log(r + 0.018) + VortexTravel;
    float angle = atan2(q.y, q.x) + VortexRotation;
    // Material is sampled progressively farther out: visible features shrink
    // exponentially into the fixed vanishing point while rotating around it.
    float warp = cylinderNoise(angle + logRadius * 0.21, logRadius * 1.7, 8) * 0.6 + cylinderNoise(angle + 0.7, logRadius * 3.4, 16) * 0.3 + cylinderNoise(angle + 1.3, logRadius * 6.8, 32) * 0.1 - 0.5;
    float flow = angle + logRadius * 0.62 + warp * 0.80 + sin(logRadius * 2.4 - t * 0.04) * 0.10;
    return float2(flow, logRadius * 1.6);
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
        float3 warm = float3(2.10, 0.92, 0.83) * upperGlow * LightIntensity * (1 - smoothstep(0.25, 0.55, p.y) * 0.4) + float3(2.5, 1.1, 0.55) * sunsetGlow;
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
    float2 material = flowCoordinates(p, t);
    float flow = material.x;
    float radial = material.y;
    float broad = cylinderNoise(flow, radial * 0.33, 24) * 0.7 + cylinderNoise(flow + 0.3, radial * 0.57 + 19.0, 48) * 0.3;
    float fabric = flowStrands(flow, radial * 4.25, 208);
    float fibers = flowStrands(flow + 0.1, radial * 22.0, 976);
    float shredded = flowStrands(flow + 0.6, radial * 11.0, 128);
    float sheet = smoothstep(0.31, 0.66, broad + (fabric - 0.5) * 0.20);
    float tears = smoothstep(0.26, 0.66, shredded + broad * 0.12);
    float face = sheet * tears;
    float fold = pow(saturate(fabric * 1.45), 2.4);
    float filament = pow(saturate(fibers * 1.85 - 0.44), 5.0) * pow(saturate(shredded * 1.45), 3.0);
    float patches = smoothstep(0.23, 0.70, flowStrands(flow + 0.4, radial * 3.0, 32));
    float3 deep = float3(0.23, 0.0017, 0.011);
    float3 red = float3(1.50, 0.017, 0.043);
    float3 hot = float3(1.9, 0.25, 0.20);
    float brightness = 0.3 + 0.7 * patches;
    float3 result = (deep + red * fold + hot * filament * 0.55 * LightIntensity) * face * brightness;
    float broadFace = sheet * (0.4 + fabric * 0.6) * (0.45 + tears * 0.55);
    result += float3(0.42, 0.003, 0.016) * broadFace * brightness;
    // Broken surface highlights and sparks, rather than continuous neon piping.
    float sparks = pow(saturate(shredded * 1.65 - 0.33), 8.0) * filament * face;
    result += float3(3.0, 0.38, 0.27) * sparks * LightIntensity;
    float distanceFade = smoothstep(0.015, 0.12, r) * (0.85 + 0.15 * sin(flow * 3.0 + 0.7));
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
    result += float3(1.2, 0.35, 0.11) * reflection * ripple * exp(-below * 10.0) * step(horizon, p.y) * (0.75 + LightIntensity * 0.25);
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
    float2 material = flowCoordinates(p, t);
    float flow = material.x;
    float radial = material.y;
    float breakup = cylinderNoise(flow, radial * 8, 256);
    // Remove only atan2's full-turn derivative jump before multiplying the
    // angular frequency. Keep genuine high-frequency gradients for filtering.
    float2 flowGradient = float2(ddx(flow), ddy(flow));
    flowGradient -= 6.28318530718 * round(flowGradient / 6.28318530718);
    float detailPhase = sin(radial * 7.0 + flow * 7.0) * 2.3 + breakup * 2.2;
    float phase = flow * 431.0 + detailPhase;
    float aa = max(dot(abs(flowGradient * 431.0 + float2(ddx(detailPhase), ddy(detailPhase))), float2(1, 1)), 0.004);
    float vein = (1 - smoothstep(0.0, aa * 0.8, abs(sin(phase)))) * (1 - smoothstep(0.8, 2.0, aa));
    float detailPhase2 = radial * 3.0 + breakup * 3.8;
    float phase2 = flow * 719.0 + detailPhase2;
    float aa2 = max(dot(abs(flowGradient * 719.0 + float2(ddx(detailPhase2), ddy(detailPhase2))), float2(1, 1)), 0.004);
    float hair = (1 - smoothstep(0.0, aa2 * 0.65, abs(sin(phase2)))) * (1 - smoothstep(0.8, 2.0, aa2));
    float broken = smoothstep(0.38, 0.76, breakup) * smoothstep(0.035, 0.12, r);
    result += float3(0.10, 0.012, 0.018) * (vein + hair * 0.5) * redCoverage * broken * LightIntensity;
    result *= 1 - redCoverage * (0.013 + 0.013 * sin(phase2));
    float2 light = p - float2(0.0, 0.065);
    float radius = length(light);
    float halo = exp(-radius * 12.0 / sqrt(LightIntensity));
    float core = exp(-radius * radius * 2700.0 / LightIntensity);
    float wide = exp(-radius * radius * 50.0 / LightIntensity);
    float angle = atan2(light.y, light.x);
    float rays = pow(saturate(0.5 + 0.25 * sin(angle * 31.0 + Time * 0.013) + 0.25 * sin(angle * 43.0)), 4.0);
    result += (float3(0.53, 0.27, 0.23) * wide + float3(1.35, 0.83, 0.61) * halo) * LightIntensity;
    result += float3(2.7, 2.5, 2.1) * core * LightIntensity;
    result += float3(0.23, 0.11, 0.085) * rays * exp(-radius * 7.0) * LightIntensity;
    float ring = exp(-abs(radius - 0.085) / max(InverseHeight, 0.0008)) * 0.14 + exp(-abs(radius - 0.104) / max(InverseHeight, 0.0007)) * 0.065;
    result += float3(0.85, 0.46, 0.36) * ring * LightIntensity;
    result *= 1 - smoothstep(0.49, 0.59, p.y);
    float underline = exp(-abs(p.y - 0.571) / max(InverseHeight * 0.7, 0.0004)) * exp(-pow(abs(p.x) / 0.59, 6.0));
    float underlineGlow = exp(-abs(p.y - 0.571) * 650.0) * exp(-abs(p.x) * 3.0);
    float2 glint = p - float2(0, 0.571);
    result += float3(0.60, 0.018, 0.030) * underline + float3(0.11, 0.003, 0.006) * underlineGlow;
    result += float3(1.2, 0.55, 0.5) * exp(-dot(glint * float2(1100.0, 1100.0), glint * float2(1100.0, 1100.0))) * LightIntensity;
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
