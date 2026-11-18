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
float4 MeteorLightWeights;
float2 MeteorHeadOffset;
float MeteorFlameIntensity;
float3 MeteorObjectAxisX;
float3 MeteorObjectAxisY;
float3 MeteorObjectAxisZ;
float2 MeteorTailOffsets[9];
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

// Object basis vectors are evaluated from absolute presentation time on the CPU.
// The orthographic camera, flight axis and light rig remain in flight space.
float3 MeteorToObject(float3 v)
{
    return float3(dot(v, MeteorObjectAxisX), dot(v, MeteorObjectAxisY), dot(v, MeteorObjectAxisZ));
}

float3 MeteorNormal(float3 p)
{
    const float e = 0.003;
    float d = MeteorDistance(p);
    return normalize(float3(MeteorDistance(p + float3(e, 0, 0)) - d, MeteorDistance(p + float3(0, e, 0)) - d, MeteorDistance(p + float3(0, 0, e)) - d));
}

// Shared by the luminous flame and its reflected light; the key stays stable.
float FlameLightIntensity()
{
    return MeteorFlameIntensity;
}

float MeteorLightVisibility(float3 p, float3 normal, float3 lightDirection)
{
    float visibility = 1;
    float travel = 0.025;
    float3 origin = p + normal * 0.012;
    // A source radius of roughly .5R at the key's distance gives a soft,
    // bounded penumbra. Only the solid participates, never the emissive plume.
    [loop]
    for (int step = 0; step < 24; step++)
    {
        float distance = MeteorDistance(origin + lightDirection * travel);
        if (distance < 0.0008)
        {
            return 0;
        }
        visibility = min(visibility, 10 * distance / travel);
        travel += clamp(distance * 0.85, 0.012, 0.18);
        if (travel > 2.5)
        {
            break;
        }
    }
    return saturate(visibility);
}

// Surface coordinates are fixed to the rock. Combustion travels around it.
float3 MeteorMaterial(float3 p, float3 n)
{
    float3 geometryNormal = n;
    float3 massNormal = normalize(p * float3(0.88 * 0.88, 1.05 * 1.05, 1));
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
    float3 tangent = normalize(cross(geometryNormal, abs(geometryNormal.z) < 0.9 ? float3(0, 0, 1) : float3(0, 1, 0)));
    float3 bitangent = cross(geometryNormal, tangent);
    float3 facetSlope = tangent * (hash(nearest + 11) - 0.5) + bitangent * (hash(nearest + 29) - 0.5);
    float3 bevelSlope = nearestDelta - geometryNormal * dot(nearestDelta, geometryNormal);
    n = normalize(geometryNormal + facetSlope * 0.55 + bevelSlope * bevel * 0.65);

    // Artist coordinates: X right, Y up, Z toward the camera. Transform the
    // supplied source offsets (-3,3,2), (3,1,-2), (0,-1,4) into object space:
    // X follows the tail, Y is screen-down, camera is on -Z. These vectors point
    // FROM the surface TOWARD each source, as required by N dot L.
    float3 keyDirection = MeteorToObject(normalize(float3(-1.797, -3.843, -2)));
    float3 rimDirection = MeteorToObject(normalize(float3(3.161, 0.083, 2)));
    float3 fillDirection = MeteorToObject(normalize(float3(-0.341, 0.94, -4)));
    float3 viewDirection = MeteorToObject(float3(0, 0, -1));
    float3 albedo = lerp(float3(0.018, 0.034, 0.041), float3(0.12, 0.19, 0.22), plates);
    albedo *= (0.55 + flakes * 0.9) * (1 - seams * 0.7);
    float3 stone = 0;
    float keyFacing = saturate(dot(n, keyDirection)) * smoothstep(-0.08, 0.12, dot(massNormal, keyDirection));
    if (MeteorLightWeights.x > 0 && keyFacing > 0)
    {
        float shadow = MeteorLightVisibility(p, geometryNormal, keyDirection);
        float specular = pow(saturate(dot(n, normalize(keyDirection + viewDirection))), 32) * 0.08;
        stone += (albedo * keyFacing + specular * keyFacing) * float3(0.95, 1, 1.08) * 3.6 * shadow * MeteorLightWeights.x;
    }
    float rimFacing = saturate(dot(n, rimDirection)) * smoothstep(-0.04, 0.16, dot(massNormal, rimDirection));
    if (MeteorLightWeights.y > 0 && rimFacing > 0)
    {
        float shadow = MeteorLightVisibility(p, geometryNormal, rimDirection);
        float edge = pow(1 - saturate(dot(massNormal, viewDirection)), 1.5);
        float specular = pow(saturate(dot(n, normalize(rimDirection + viewDirection))), 24) * 0.18;
        stone += (albedo * rimFacing + specular * rimFacing) * float3(0.04, 1, 0.74) * 16 * edge * shadow * FlameLightIntensity() * MeteorLightWeights.y;
    }
    // No ambient term. A final camera-side source contributes 6% of key power.
    float fillFacing = saturate(dot(normalize(massNormal * 0.6 + n * 0.4), fillDirection));
    stone += albedo * fillFacing * float3(0.95, 1, 1.08) * (3.6 * 0.06) * MeteorLightWeights.z;
    float hotCracks = seams * smoothstep(0.57, 0.72, noise(p * 6.3 + 17));
    // Only the golden fissures emit; all other stone brightness is reflected.
    stone += float3(0.95, 0.34, 0.035) * hotCracks * (0.7 + 0.3 * sin(Time * 1.3 + p.x * 6)) * MeteorLightWeights.w;
    return stone;
}

float2 MeteorCoordinates(float2 p)
{
    float compositionScale = min(1.0, Aspect / 1.35);
    float2 delta = float2(p.x / compositionScale + 0.30, (p.y - 0.30) / compositionScale);
    return float2(dot(delta, float2(0.94, -0.341)), dot(delta, float2(0.341, 0.94)));
}

float2 MeteorPlumeCoordinates(float2 q)
{
    // Anchor the shell to the head and retain a fixed flight axis. Only the far
    // plume receives delayed translation; transverse response grows quadratically.
    float s = saturate((q.x - MeteorHeadOffset.x - 0.12) / 0.98);
    float response = s * s;
    float history = response * 8;
    int first = min((int)history, 7);
    float2 offset = lerp(MeteorTailOffsets[first], MeteorTailOffsets[first + 1], history - first);
    offset = lerp(MeteorHeadOffset, offset, response);
    offset.y = lerp(MeteorHeadOffset.y, offset.y * 0.25, response);
    return q - offset;
}

// Advected turbulent sheets stream downstream (+X), continuously replenished at
// the shock front. The envelope and the solid never travel toward a destination.
float3 Combustion(float2 q)
{
    float x = q.x;
    float y = q.y;
    float downstream = smoothstep(-0.20, 0.12, x);
    float s = saturate((x - 0.12) / 0.98);
    float extent = 1 - smoothstep(0.64, 1.12, x);
    float width = lerp(0.205, 0.014, saturate((x + 0.08) / 1.13));
    float bow = sqrt(max(0.0, 0.23 * 0.23 - x * x));
    float envelope = lerp(bow, width, downstream);
    float3 fire = 0;
    [unroll]
    for (int i = 0; i < 7; i++)
    {
        float lane = (i - 3) / 3.0;
        float3 flow = float3(x * 6 - Time * (3.0 + i * 0.10), y * 35, i * 7.13);
        float turbulence = fbm(flow);
        float folds = fbm(flow * float3(2.7, 0.9, 1) + 11);
        float curl = fbm(float3(x * 5 - Time * 2.5, y * 9, i * 7.13));
        float center = lane * envelope + (curl - 0.5) * 0.008 * s * s;
        float distance = abs(y - center);
        float thickness = (0.010 + turbulence * 0.012) * (0.25 + extent * 0.75);
        float sheet = exp(-distance * distance / (thickness * thickness));
        float torn = smoothstep(0.32, 0.64, folds + turbulence * 0.16);
        float veins = pow(saturate(folds * 1.7), 8);
        float edge = exp(-abs(distance - thickness * 0.7) / 0.002);
        float brightness = sheet * (0.04 + torn * 0.52 + veins * 0.9) + edge * torn * 0.22;
        fire += lerp(float3(0.002, 0.48, 0.15), float3(0.42, 1.4, 1.1), saturate(veins * 0.9 + abs(lane) * 0.3)) * brightness;
    }
    float frontRadius = 0.242 + (noise(float3(q * 70, Time * 0.9)) - 0.5) * 0.0012;
    float front = exp(-abs(length(q * float2(0.91, 1.04)) - frontRadius) * 190);
    front *= 1 - smoothstep(-0.07, 0.16, x);
    float frontNoise = fbm(float3(x * 40 - Time * 12, y * 40, Time * 0.4));
    fire = fire * extent * smoothstep(-0.255, -0.19, x);
    fire += float3(0.48, 1.8, 1.5) * front * (0.18 + frontNoise * frontNoise * 2.5);
    float halo = exp(-dot(q * float2(2.7, 5), q * float2(2.7, 5)));
    fire += float3(0.002, 0.12, 0.075) * halo;
    return fire * FlameLightIntensity();
}

float3 AblationSparks(float2 q)
{
    float3 result = 0;
    [unroll]
    for (int i = 0; i < 18; i++)
    {
        float seed = hash(float3(i, 7, 3));
        float age = frac(Time * (0.35 + seed * 0.15) + seed * 13);
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
    float2 head = q - MeteorHeadOffset;
    float2 plume = MeteorPlumeCoordinates(q);
    float mist = fbm(float3(p * 3, Time * 0.014));
    float3 result = float3(0.002, 0.009, 0.013) + float3(0.001, 0.027, 0.019) * mist;
    result += float3(0.0, 0.045, 0.028) * exp(-dot((p - float2(-0.25, 0.2)) * 2.8, (p - float2(-0.25, 0.2)) * 2.8));
    result = (result + Combustion(plume)) * MeteorLightWeights.w;
    // Orthographic camera through the independent 3D solid. A bounded footprint
    // and fixed maximum step count keep cost independent of elapsed time.
    [branch]
    // The full rotating solid fits inside the .305-radius footprint. Subtract
    // translation first so all poses retain that bound without clipping.
    if (abs(head.x) < 0.305 && abs(head.y) < 0.305)
    {
        float3 ray = MeteorToObject(float3(head / 0.24, -1.6));
        float3 rayDirection = MeteorToObject(float3(0, 0, 1));
        float depth = 0;
        float d = 1;
        [loop]
        for (int step = 0; step < 38; step++)
        {
            d = MeteorDistance(ray + rayDirection * depth);
            if (d < 0.0015 || depth > 3.2)
            {
                break;
            }
            depth += max(d, 0.001);
        }
        if (d < 0.0015)
        {
            float3 hitPosition = ray + rayDirection * depth;
            float3 normal = MeteorNormal(hitPosition);
            result = MeteorMaterial(hitPosition, normal);
        }
    }
    result += (Lifestream(p) + AblationSparks(plume)) * MeteorLightWeights.w;
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
    result += float3(0.12, 0.55, 0.39) * underline * MeteorLightWeights.w;
    float2 glint = (p - float2(0, underlineY)) * float2(240, 1900);
    result += float3(0.25, 0.8, 0.6) * exp(-dot(glint, glint)) * MeteorLightWeights.w;
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
