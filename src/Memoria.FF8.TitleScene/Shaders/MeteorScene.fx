#if VULKAN || DIRECTX12
#define PS_PROFILE ps_6_0
#define SAMPLE_TEXTURE(textureName, samplerName, uv) textureName.Sample(samplerName, uv)
#else
#define PS_PROFILE ps_4_0
#define SAMPLE_TEXTURE(textureName, samplerName, uv) tex2D(samplerName, uv)
#endif

float Time;
float Aspect;
float InverseHeight;
#if VULKAN || DIRECTX12
Texture2D Texture : register(t0);
sampler TextureSampler : register(s0) = sampler_state
#else
texture Texture;
sampler TextureSampler = sampler_state
#endif
{
    Texture = <Texture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

float hash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float noise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3 - 2 * f);
    return lerp(lerp(lerp(hash(i), hash(i + float3(1, 0, 0)), f.x), lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
        lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x), lerp(hash(i + float3(0, 1, 1)), hash(i + 1), f.x), f.y), f.z);
}

float fbm(float3 p)
{
    float value = 0;
    float weight = 0.55;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        value += noise(p) * weight;
        p = p * 2.07 + float3(13.7, 7.2, 19.1);
        weight *= 0.48;
    }
    return value;
}

// Reusable implicit solid model in object space. Independent of the presentation
// clock and combustion: its closed surface can be sampled or meshed separately.
// The long axis is X; the oncoming face is -X. No baked image or flame silhouette.
float MeteorDistance(float3 p)
{
    float3 q = p * float3(0.88, 1.05, 1.0);
    float relief = noise(q * 3.7 + 4.1) * 0.18 + noise(q * 8.3) * 0.065 + abs(noise(q * 26) - 0.5) * 0.022;
    return (length(q) - 0.84 - relief) * 0.72;
}

float3 MeteorNormal(float3 p)
{
    const float e = 0.003;
    float d = MeteorDistance(p);
    return normalize(float3(MeteorDistance(p + float3(e, 0, 0)) - d, MeteorDistance(p + float3(0, e, 0)) - d, MeteorDistance(p + float3(0, 0, e)) - d));
}

// Surface coordinates are fixed to the rock. Combustion travels around it.
float3 MeteorMaterial(float3 p, float3 n)
{
    float3 grain = p * float3(8, 15, 12);
    float3 cell = floor(grain);
    float first = 20;
    float second = 20;
    float3 nearest = 0;
    float3 nearestDelta = 0;
    [unroll]
    for (int z = -1; z <= 1; z++)
    {
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            [unroll]
            for (int x = -1; x <= 1; x++)
            {
                float3 id = cell + float3(x, y, z);
                float3 offset = float3(hash(id), hash(id + 37), hash(id + 71));
                float3 delta = id + offset * 0.85 - grain;
                float distance = dot(delta, delta);
                if (distance < first)
                {
                    second = first;
                    first = distance;
                    nearest = id;
                    nearestDelta = delta;
                }
                else
                {
                    second = min(second, distance);
                }
            }
        }
    }
    float plates = hash(nearest + 13);
    float ridges = second - first;
    float seams = 1 - smoothstep(0.008, 0.045, ridges);
    float flakes = fbm(p * 71);
    float bevel = 1 - smoothstep(0.04, 0.24, ridges);
    float3 facetNormal = float3(hash(nearest + 11) - 0.5, hash(nearest + 29) - 0.5, -0.3);
    n = normalize(n + facetNormal * 0.8 + nearestDelta * bevel * 1.3);
    float3 light = normalize(float3(-0.7, -0.8, -0.9));
    float diffuse = saturate(dot(n, light));
    float greenBounce = saturate(dot(n, normalize(float3(0.9, 0.5, -0.6))));
    float3 stone = lerp(float3(0.018, 0.034, 0.041), float3(0.12, 0.19, 0.22), plates);
    stone *= (0.17 + diffuse * 1.7) * (0.55 + flakes * 0.9);
    stone *= 1 - seams * 0.7;
    stone += float3(0.005, 0.10, 0.078) * greenBounce * (0.2 + plates);
    float hotCracks = seams * smoothstep(0.57, 0.72, noise(p * 6.3 + 17));
    stone += float3(0.95, 0.34, 0.035) * hotCracks * (0.7 + 0.3 * sin(Time * 1.3 + p.x * 6));
    float edge = pow(1 - saturate(-n.z), 3);
    stone += float3(0.015, 0.46, 0.32) * edge;
    return stone;
}

float2 MeteorCoordinates(float2 p)
{
    float compositionScale = min(1.0, Aspect / 1.35);
    float2 delta = float2(p.x / compositionScale + 0.30, (p.y - 0.30) / compositionScale);
    return float2(dot(delta, float2(0.94, -0.341)), dot(delta, float2(0.341, 0.94)));
}

// Advected turbulent sheets stream downstream (+X), continuously replenished at
// the shock front. The envelope and the solid never travel toward a destination.
float3 Combustion(float2 q)
{
    float x = q.x;
    float y = q.y;
    float downstream = smoothstep(-0.20, 0.12, x);
    float extent = 1 - smoothstep(0.64, 1.12, x);
    float width = lerp(0.205, 0.014, saturate((x + 0.08) / 1.13));
    float bow = sqrt(max(0.0, 0.23 * 0.23 - x * x));
    float envelope = lerp(bow, width, downstream);
    float3 fire = 0;
    [unroll]
    for (int i = 0; i < 7; i++)
    {
        float lane = (i - 3) / 3.0;
        float3 flow = float3(x * 6 - Time * (1.1 + i * 0.047), y * 35, i * 7.13);
        float turbulence = fbm(flow);
        float folds = fbm(flow * float3(2.7, 0.9, 1) + 11);
        float curl = fbm(float3(x * 5 - Time * 0.9, y * 9, i * 7.13));
        float center = lane * envelope + (curl - 0.48) * (0.025 + downstream * 0.12);
        float distance = abs(y - center);
        float thickness = (0.006 + turbulence * 0.035) * (0.25 + extent * 0.75);
        float sheet = exp(-distance * distance / (thickness * thickness));
        float torn = smoothstep(0.32, 0.64, folds + turbulence * 0.16);
        float veins = pow(saturate(folds * 1.7), 8);
        float edge = exp(-abs(distance - thickness * 0.7) / 0.002);
        float brightness = sheet * (0.04 + torn * 0.52 + veins * 0.9) + edge * torn * 0.22;
        fire += lerp(float3(0.002, 0.48, 0.15), float3(0.42, 1.4, 1.1), saturate(veins * 0.9 + abs(lane) * 0.3)) * brightness;
    }
    float frontRadius = 0.236 + noise(float3(q * 70, Time * 0.9)) * 0.012;
    float front = exp(-abs(length(q * float2(0.91, 1.04)) - frontRadius) * 190);
    front *= 1 - smoothstep(-0.07, 0.16, x);
    float frontNoise = fbm(float3(x * 40 - Time * 2, y * 40, Time * 0.4));
    fire = fire * extent * smoothstep(-0.255, -0.19, x);
    fire += float3(0.48, 1.8, 1.5) * front * (0.18 + frontNoise * frontNoise * 2.5);
    float halo = exp(-dot(q * float2(2.7, 5), q * float2(2.7, 5)));
    fire += float3(0.002, 0.12, 0.075) * halo;
    return fire;
}

float3 AblationSparks(float2 q)
{
    float3 result = 0;
    [unroll]
    for (int i = 0; i < 18; i++)
    {
        float seed = hash(float3(i, 7, 3));
        float age = frac(Time * (0.17 + seed * 0.09) + seed * 13);
        float x = 0.08 + age * (0.72 + seed * 0.42);
        float spread = (hash(float3(i, 4, 8)) - 0.5) * 0.49;
        float y = spread * (0.6 + age * 0.7) + sin(age * 3 + seed * 19) * 0.025;
        float2 distance = (q - float2(x, y)) * float2(100, 570);
        float light = exp(-dot(distance, distance)) * sin(age * 3.14159265);
        result += float3(0.13, 0.8, 0.42) * light;
    }
    return result;
}

float3 Lifestream(float2 p)
{
    float3 result = 0;
    [unroll]
    for (int i = 0; i < 7; i++)
    {
        float phase = i * 1.71;
        float travel = p.x * 2.5 - Time * (0.05 + i * 0.003);
        float bend = sin(p.x * 2.1 + phase * 0.16 + Time * 0.035) * 0.07;
        float center = 0.94 - p.x * p.x * 0.20 + bend + sin(p.x * 5 + phase + Time * 0.09) * 0.023;
        float strand = p.y - center;
        float n = fbm(float3(travel * 4, strand * 38, phase));
        float warp = strand + (n - 0.5) * 0.022;
        float sheet = exp(-abs(warp) * (65 + i * 12));
        float thread = exp(-abs(warp) * 780) * smoothstep(0.36, 0.72, n);
        float wisps = pow(saturate(noise(float3(travel * 14, strand * 240, phase)) * 1.4), 5);
        result += float3(0.005, 0.15, 0.095) * sheet * (0.35 + wisps) + float3(0.07, 0.55, 0.39) * thread;
    }
    return result;
}

float4 SurfacePixel(float4 position : SV_POSITION, float4 color : COLOR0, float2 uv : TEXCOORD0) : SV_TARGET
{
    float2 p = float2((uv.x - 0.5) * Aspect, uv.y);
    float2 q = MeteorCoordinates(p);
    float mist = fbm(float3(p * 3, Time * 0.014));
    float3 result = float3(0.002, 0.009, 0.013) + float3(0.001, 0.027, 0.019) * mist;
    result += float3(0.0, 0.045, 0.028) * exp(-dot((p - float2(-0.25, 0.2)) * 2.8, (p - float2(-0.25, 0.2)) * 2.8));
    result += Combustion(q);
    // Orthographic camera through the independent 3D solid. A bounded footprint
    // and fixed maximum step count keep cost independent of elapsed time.
    [branch]
    // Includes the maximum relief (.84 + .18 + .065 + .011), object
    // anisotropy and .24 scene scale, so trailing protrusions are not clipped.
    if (abs(q.x) < 0.305 && abs(q.y) < 0.255)
    {
        float3 ray = float3(q / 0.24, -1.6);
        float depth = 0;
        float d = 1;
        [loop]
        for (int step = 0; step < 38; step++)
        {
            d = MeteorDistance(ray + float3(0, 0, depth));
            if (d < 0.0015 || depth > 2.7)
            {
                break;
            }
            depth += max(d, 0.001);
        }
        if (d < 0.0015)
        {
            float3 hitPosition = ray + float3(0, 0, depth);
            float3 normal = MeteorNormal(hitPosition);
            result = MeteorMaterial(hitPosition, normal);
            // Thin ablation over the surface connects the fire to the rock.
            float ablation = pow(1 - saturate(-normal.z), 4);
            result += Combustion(q) * (0.05 + ablation * 0.65);
        }
    }
    result += Lifestream(p);
    result += AblationSparks(q);
    // Preserve a quiet menu area while allowing the lower streams to frame it.
    return float4(result, 1);
}

float4 CompositePixel(float4 position : SV_POSITION, float4 color : COLOR0, float2 uv : TEXCOORD0) : SV_TARGET
{
    float3 result = SAMPLE_TEXTURE(Texture, TextureSampler, uv).rgb;
    float2 p = float2((uv.x - 0.5) * Aspect, uv.y);
    float wordmarkScale = min(1.0, Aspect / (1600.0 / 900.0));
    float underlineY = 469.0 / 900.0 + 103.0 / 900.0 * wordmarkScale;
    float underline = exp(-abs(p.y - underlineY) / max(InverseHeight * 0.65, 0.00045));
    underline *= 1 - smoothstep(0.58 * wordmarkScale, 0.75 * wordmarkScale, abs(p.x));
    result += float3(0.12, 0.55, 0.39) * underline;
    float2 glint = (p - float2(0, underlineY)) * float2(240, 1900);
    result += float3(0.25, 0.8, 0.6) * exp(-dot(glint, glint));
    float vignette = 1 - 0.38 * smoothstep(0.45, 1.1, length(float2(p.x, (p.y - 0.4) * 1.2)));
    result = 1 - exp(-result * 1.5);
    return float4(result * vignette, 1);
}

technique Surface
{
    pass Pass0
    {
        PixelShader = compile PS_PROFILE SurfacePixel();
    }
}

technique Composite
{
    pass Pass0
    {
        PixelShader = compile PS_PROFILE CompositePixel();
    }
}
