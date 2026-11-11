using System.Buffers.Binary;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7BattleActorVisual(string ModelPrefix, Vector3 Position, float FacingRadians, int ClipIndex, int ClipFrame, int? WeaponIndex = null, BattleLimitAnimation? Limit = null)
{
    public Ff7BattleActorModelModifiers? ModelModifiers { get; init; }
}
public sealed record Ff7BattleRenderFrame(SceneCameraPlacement Camera, IReadOnlyList<Ff7BattleActorVisual> Actors, Ff7BattleFireState? Fire = null, Ff7BattleIceState? Ice = null, Ff7BattleBoltState? Bolt = null, Ff7BattleCureState? Cure = null, Ff7BattleBraverState? Braver = null)
{
    public CrossoverFf8BattleActorVisual? Guest { get; init; }
    public CrossoverNativeEffectVisual? GuestEffects { get; init; }
    public IReadOnlyList<CrossoverFf8BattleActorVisual> Guests { get; init; } = [];
    public IReadOnlyList<CrossoverNativeEffectVisual> GuestEffectLayers { get; init; } = [];
    public Ff7BattleBeatRushState? BeatRush { get; init; }
    public Ff7BattleBigShotState? BigShot { get; init; }
    public IReadOnlyList<Ff7BattleHitmarkState>? Hitmarks { get; init; }
    public Ff7BattleSmokeShotState? SmokeShot { get; init; }
    public Ff7BattleDestroyedState? Destroyed { get; init; }
    public Ff7BattleSmogState? Smog { get; init; }
    public Ff7BattleSecondaryState? SecondaryEffects { get; init; }
    public Ff7BattleSearchScopeState? SearchScope { get; init; }
    public Ff7BattleStageFadeState? StageFade { get; init; }
    public Ff7BattleRocketState? Rockets { get; init; }
    public Ff7BattlePrimaryState? PrimaryEffects { get; init; }
}

/// <summary>Submits original FFVII stage and rigid actor geometry without advancing the battle.</summary>
public sealed partial class Ff7BattleRenderer
{
    private readonly LgpArchive archive;
    private readonly Ff7BattleArenaResources arena;
    private readonly LgpArchive? magicArchive;
    private readonly PcTexture? cureTexture;
    private readonly PcTexture? grenadeTexture;
    private readonly Dictionary<(Ff7BattleCureKind Kind, byte AtlasY), Ff7BattleBillboardRenderer> cure = [];
    private Ff7BattleBillboardRenderer? braverShine;
    private Ff7BattleBillboardRenderer? braverImpact;
    private PixelImage? braverTrail;
    private Ff7BattleBillboardRenderer? fire;
    private Ff7BattleBillboardRenderer? lightning;
    private Ff7BattleBillboardRenderer? sparks;
    private PcPolygonModel? shockwave;
    private PcTexture[]? shockwaveTextures;
    private PcPolygonModel? ice;
    private readonly Dictionary<string, ActorModel> actors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<PcTexture, PixelImage> images = new(ReferenceEqualityComparer.Instance);
    private readonly PcTexture[] arenaTextures;

    public Ff7BattleRenderer(LgpArchive archive, int locationId, LgpArchive? magicArchive = null, PcTexture? cureTexture = null, PcTexture? grenadeTexture = null)
    {
        this.archive = archive ?? throw new ArgumentNullException(nameof(archive));
        this.magicArchive = magicArchive;
        this.cureTexture = cureTexture;
        this.grenadeTexture = grenadeTexture;
        arena = Ff7BattleArenaResources.Load(archive, locationId);
        arenaTextures = arena.Textures.Select(entry => entry.Texture).ToArray();
    }

    public static string PartyModelPrefix(int characterId)
    {
        return characterId switch
        {
            0 => "rt",
            1 => "sb",
            2 => "ru",
            _ => throw new NotSupportedException($"FFVII party battle model {characterId} is unsupported.")
        };
    }

    public static string EnemyModelPrefix(int enemyId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(enemyId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(enemyId, 26 * 26);
        return EncodeLetters(enemyId);
    }

    public int GetClipFrameCount(string modelPrefix, int clipIndex, BattleLimitAnimation? limit = null)
    {
        return LoadActor(modelPrefix).GetClip(clipIndex, false, limit).Count;
    }

    public void Render(ITriangleRenderer renderer, int width, int height, Ff7BattleRenderFrame frame)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Ff7BattlePrimaryState? primary = frame.PrimaryEffects ?? frame.SearchScope?.Primary ?? frame.Cure?.Primary ?? frame.Bolt?.Primary ?? frame.Destroyed?.Primary ?? frame.Fire?.Primary ?? frame.Ice?.Primary ?? frame.Braver?.Primary ?? frame.BeatRush?.Primary ?? frame.BigShot?.Primary ?? frame.SmokeShot?.Primary ?? frame.Smog?.Primary ?? frame.Rockets?.Primary;
        Ff7BattleStageFadeState? stageFade = frame.StageFade ?? primary?.StageFade;
        ModelColorBlend? stageTint = stageFade is { Alpha: < 255 } fade ? new(fade.Red, fade.Green, fade.Blue, fade.Alpha) : null;
        IReadOnlyList<CrossoverFf8BattleActorVisual> guests = frame.Guests.Count > 0 ? frame.Guests : frame.Guest is { } guest ? [guest] : [];
        IReadOnlyList<CrossoverNativeEffectVisual> guestEffects = frame.GuestEffectLayers.Count > 0 ? frame.GuestEffectLayers : frame.GuestEffects is { } effectLayer ? [effectLayer] : [];
        CrossoverNativeStageColor? guestStageColor = guestEffects.Select(effect => effect.StageColor).FirstOrDefault(color => color is not null);
        CameraView view = CameraView.Create(frame.Camera, width, height);
        renderer.Begin(width, height, new Vector4(0, 0, 0, 1), 0);
        try
        {
            Ff7ModelMaterialState material = new() { Cull = true };
            foreach (Ff7BattleArenaPart part in arena.Geometry)
            {
                DrawModel(renderer, part.Polygon, arenaTextures, Matrix4x4.Identity, view, material, colorBlend: stageTint, guestStageColor: guestStageColor);
            }
            foreach (Ff7BattleActorVisual actor in frame.Actors)
            {
                DrawActor(renderer, actor, view, material);
            }
            foreach (CrossoverFf8BattleActorVisual actor in guests)
            {
                CrossoverFf8BattleActorRenderer.Draw(renderer, actor, view.Transform, width, height, view.Focal);
            }
            foreach (Ff7BattleHitmarkState hitmark in frame.Hitmarks ?? [])
            {
                DrawHitmark(renderer, hitmark, view);
            }
            if (frame.SecondaryEffects is { } secondary)
            {
                DrawSecondaryEffects(renderer, secondary, view);
            }
            if (primary is null && frame.Fire is { Sprites.Count: > 0 } effect)
            {
                foreach (Ff7BattleFireSprite sprite in effect.Sprites)
                {
                    DrawFireSprite(renderer, sprite, view);
                }
            }
            if (primary is null && frame.Ice is { Completed: false, Frame: >= 0, Crystals: { } crystals } iceEffect)
            {
                foreach (Ff7BattleIceCrystal crystal in crystals)
                {
                    int localFrame = iceEffect.Frame - crystal.StartedAt;
                    if (localFrame >= 0 && localFrame < Ff7BattleIceEffect.FrameCount)
                    {
                        DrawIceCrystal(renderer, crystal, localFrame, view);
                    }
                }
            }
            if (primary is null && frame.Bolt is { Completed: false, Frame: >= 0 } bolt)
            {
                DrawBolt(renderer, bolt, view);
            }
            if (primary is null && frame.Cure is { Particles.Count: > 0 } cureEffect)
            {
                foreach (Ff7BattleCureParticle particle in cureEffect.Particles)
                {
                    DrawCureParticle(renderer, cureEffect.Kind, particle, view);
                }
            }
            if (primary is null && frame.Braver is { Completed: false } braver)
            {
                DrawBraver(renderer, braver, view);
            }
            if (primary is null && frame.BeatRush is { Completed: false } beatRush)
            {
                DrawBeatRush(renderer, beatRush, view);
            }
            if (primary is null && frame.BigShot is { Completed: false } bigShot)
            {
                DrawBigShot(renderer, bigShot, view);
            }
            if (frame.SmokeShot is { Sprites.Count: > 0 } smokeShot)
            {
                DrawSmokeShot(renderer, smokeShot, view);
            }
            if (primary is null && frame.Destroyed is { Completed: false } destroyedEffect)
            {
                DrawDestroyed(renderer, destroyedEffect, view);
            }
            if (primary is null && frame.Smog is { Completed: false } smog)
            {
                DrawSmog(renderer, smog, view);
            }
            if (primary is null && frame.SearchScope is { Completed: false } searchScope)
            {
                DrawSearchScope(renderer, searchScope, view);
            }
            if (primary is null && frame.Rockets is { Completed: false } rockets)
            {
                DrawRockets(renderer, rockets, view);
            }
            if (primary is not null)
            {
                DrawPrimaryEffects(renderer, primary, view);
            }
            foreach (CrossoverNativeEffectVisual guestEffect in guestEffects)
            {
                guestEffect.Draw(renderer, frame.Camera, width, height);
            }
        }
        finally
        {
            renderer.End();
        }
    }

    private void DrawActor(ITriangleRenderer renderer, Ff7BattleActorVisual actor, CameraView view, Ff7ModelMaterialState material)
    {
        if (actor.ModelModifiers is { Visible: false })
        {
            return;
        }
        ModelColorBlend? tint = actor.ModelModifiers is { Alpha: < 255 } modifiers ? new(modifiers.Red, modifiers.Green, modifiers.Blue, modifiers.Alpha) : null;
        VisitActorGeometry(actor, (model, textures, transform) => DrawModel(renderer, model, textures, transform, view, material, colorBlend: tint));
    }

    private void DrawModel(ITriangleRenderer renderer, PcPolygonModel model, IReadOnlyList<PcTexture> textures, Matrix4x4 transform, CameraView view, Ff7ModelMaterialState material, float opacity = 1, ModelColorBlend? colorBlend = null, CrossoverNativeStageColor? guestStageColor = null)
    {
        if (model.Groups.Count != model.RenderStates.Count)
        {
            throw new InvalidDataException("FFVII battle P groups require their original material records.");
        }
        for (int index = 0; index < model.Groups.Count; index++)
        {
            PcModelGroup group = model.Groups[index];
            PcRenderState state = PcRenderState.Read(model.RenderStates[index].Span);
            material.Apply(state);
            PixelImage? image = null;
            if (group.TexturesUsed != 0)
            {
                if ((uint)group.TextureNumber >= textures.Count)
                {
                    throw new InvalidDataException($"FFVII battle P group {index} references absent texture {group.TextureNumber}.");
                }
                PcTexture texture = textures[group.TextureNumber];
                if (!images.TryGetValue(texture, out image))
                {
                    image = Ff7FieldRenderer.DecodeTexture(texture);
                    images.Add(texture, image);
                }
            }
            TriangleMaterial triangleMaterial = new(image, opacity < 1 ? TriangleBlend.Alpha : material.Blend, material.DepthTest ? TriangleDepthComparison.GreaterEqual : TriangleDepthComparison.Always, material.DepthWrite && opacity == 1, material.AlphaReference, material.Sampler) { PreserveTargetAlpha = false, TextureSelectsAlpha = false };
            CameraSpaceVertex Vertex(int local)
            {
                int vertex = group.VertexStart + local;
                uint argb = vertex < model.VertexColors.Count ? model.VertexColors[vertex] : uint.MaxValue;
                argb = guestStageColor?.Apply(argb) ?? argb;
                float red = argb >> 16 & 255;
                float green = argb >> 8 & 255;
                float blue = argb & 255;
                if (colorBlend is { } blend)
                {
                    float originalWeight = blend.OriginalWeight * (1f / 255f);
                    float tintWeight = 1f - originalWeight;
                    red = MathF.Round(red * originalWeight + blend.Red * tintWeight);
                    green = MathF.Round(green * originalWeight + blend.Green * tintWeight);
                    blue = MathF.Round(blue * originalWeight + blend.Blue * tintWeight);
                }
                Vector4 color = new(red / 255f, green / 255f, blue / 255f, (argb >> 24) / 255f * state.VertexAlpha / 255f * opacity);
                Vector2 uv = image is null ? Vector2.Zero : model.TextureCoordinates[group.TextureCoordinateStart + local];
                return new(view.Transform(Vector3.Transform(model.Vertices[vertex], transform)), uv, color);
            }
            for (int polygonIndex = group.PolygonStart; polygonIndex < group.PolygonStart + group.PolygonCount; polygonIndex++)
            {
                PcModelPolygon polygon = model.Polygons[polygonIndex];
                CameraSpaceVertex a = Vertex(polygon.A);
                CameraSpaceVertex b = Vertex(polygon.B);
                CameraSpaceVertex c = Vertex(polygon.C);
                if (!material.Gouraud)
                {
                    b = b with { Color = a.Color };
                    c = c with { Color = a.Color };
                }
                CameraSpaceTriangleSubmission.Draw(renderer, a, b, c, view.Width, view.Height, view.Focal, triangleMaterial, material.Cull, clockwiseFront: true);
            }
        }
    }

    private void DrawBraver(ITriangleRenderer renderer, Ff7BattleBraverState effect, CameraView view)
    {
        if (effect.Shine is { } shine)
        {
            braverShine ??= LoadBillboard("shining.s", ["blaver01.tex"], 10, _ => 0, TriangleBlend.Add);
            braverShine.Draw(renderer, effect.Frame - 10, view.Transform(shine.Position), view.Width, view.Height, view.Focal, scale: 4);
        }
        if (effect.Trail is { } current && effect.PreviousTrail is { } previous)
        {
            if (braverTrail is null)
            {
                PcTexture texture = PcTexture.Read(Read(MagicArchive(), "blaver01.tex"));
                byte[] header = texture.Header.ToArray();
                BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0xD0), (0x7888 >> 6) - BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0xE0)));
                braverTrail = Ff7FieldRenderer.DecodeTexture(texture with { Header = header });
            }

            TriangleMaterial material = new(braverTrail, TriangleBlend.Add, TriangleDepthComparison.GreaterEqual, false, Sampler: TriangleSampler.PointClamp);
            CameraSpaceVertex a = Vertex(current.Base, 0x80, 0x50);
            CameraSpaceVertex b = Vertex(current.Tip, 0xC0, 0x50);
            CameraSpaceVertex c = Vertex(previous.Base, 0x80, 0);
            CameraSpaceVertex d = Vertex(previous.Tip, 0xC0, 0);
            CameraSpaceTriangleSubmission.Draw(renderer, a, b, c, view.Width, view.Height, view.Focal, material);
            CameraSpaceTriangleSubmission.Draw(renderer, b, d, c, view.Width, view.Height, view.Focal, material);

            CameraSpaceVertex Vertex(Ff7BattleBraverPoint point, int u, int v)
            {
                return new(view.Transform(point.Position), new((u + 0.5f) / braverTrail.Width, (v + 0.5f) / braverTrail.Height));
            }
        }
        if (effect.Impact is { } impact)
        {
            braverImpact ??= LoadBillboard("blaver.s", ["blaver00.tex"], 7, _ => 0, TriangleBlend.Add);
            Ff7BattleBraverPoint point = impact.Anchor;
            braverImpact.Draw(renderer, effect.Frame - 26, SpriteAnchor(point.X, point.Y, point.Z, impact.CollisionRadius, view), view.Width, view.Height, view.Focal);
        }
    }

    private void DrawBolt(ITriangleRenderer renderer, Ff7BattleBoltState bolt, CameraView view)
    {
        if (bolt.Strikes is { } strikes)
        {
            foreach (Ff7BattleBoltStrike strike in strikes)
            {
                int frame = bolt.Frame - strike.StartedAt;
                if (frame is < 0 or >= 16)
                {
                    continue;
                }
                DrawBoltStrike(renderer, strike, frame, view);
            }
        }
        if (bolt.Sparks is { Count: > 0 } active)
        {
            foreach (Ff7BattleBoltSpark spark in active)
            {
                DrawBoltSpark(renderer, spark, view);
            }
        }
    }

    private void DrawBoltStrike(ITriangleRenderer renderer, Ff7BattleBoltStrike strike, int frame, CameraView view)
    {
        const string archivePath = "ff7/data/battle/magic/thunder/";
        shockwave ??= PcPolygonModel.Read(Read(MagicArchive(), archivePath + "thund_3.p"));
        shockwaveTextures ??= [PcTexture.Read(Read(MagicArchive(), archivePath + "sonic_1.tex"))];
        if (frame < 9)
        {
            lightning ??= LoadBillboard("kami_1.s", [archivePath + "thunde00.tex"], 5, _ => 0);
            lightning.Draw(renderer, frame / 2, SpriteAnchor(strike.X, strike.Y, strike.Z, strike.CollisionRadius, view), view.Width, view.Height, view.Focal, scale: 2);
        }
        float scale = 1 + frame / 8f;
        float opacity = frame < 8 ? 1 : (16 - frame) / 8f;
        Matrix4x4 transform = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(strike.X, strike.Y, strike.Z);
        DrawModel(renderer, shockwave, shockwaveTextures, transform, view, new() { Cull = true }, opacity);
    }

    private void DrawBoltSpark(ITriangleRenderer renderer, Ff7BattleBoltSpark spark, CameraView view)
    {
        const string archivePath = "ff7/data/battle/magic/thunder/";
        sparks ??= LoadBillboard(archivePath + "spark_1.s", [archivePath + "thunde01.tex"], 8, _ => 0);
        sparks.Draw(renderer, spark.Frame, SpriteAnchor(spark.X, spark.Y, spark.Z, spark.CollisionRadius, view), view.Width, view.Height, view.Focal, scale: 2, flip: spark.Flip);
    }

    private void DrawCureParticle(ITriangleRenderer renderer, Ff7BattleCureKind kind, Ff7BattleCureParticle particle, CameraView view)
    {
        if (!cure.TryGetValue((kind, particle.AtlasY), out Ff7BattleBillboardRenderer? particles))
        {
            particles = LoadCure(kind, particle.AtlasY);
            cure.Add((kind, particle.AtlasY), particles);
        }
        particles.Draw(renderer, particle.Frame, SpriteAnchor(particle.X, particle.Y, particle.Z, 500, view), view.Width, view.Height, view.Focal, particle.Scale / 4096f);
    }

    private void DrawFireSprite(ITriangleRenderer renderer, Ff7BattleFireSprite sprite, CameraView view)
    {
        fire ??= LoadBillboard("fi_a01.s", ["fire00.tex", "fire01.tex"], Ff7BattleFireEffect.SpriteFrameCount, page => page switch
        {
            0x2C => 0,
            0x2D => 1,
            _ => throw new NotSupportedException($"FFVII Fire billboard uses unsupported texture page 0x{page:X4}.")
        });
        fire.Draw(renderer, sprite.Frame, SpriteAnchor(sprite.X, sprite.Y, sprite.Z, sprite.CollisionRadius, view), view.Width, view.Height, view.Focal);
    }

    private void DrawIceCrystal(ITriangleRenderer renderer, Ff7BattleIceCrystal crystal, int frame, CameraView view)
    {
        ice ??= LoadIce();
        Matrix4x4 transform = Matrix4x4.CreateScale(Ff7BattleIceEffect.Scale(frame, crystal.CollisionRadius)) * Matrix4x4.CreateTranslation(crystal.X, crystal.Y, crystal.Z);
        DrawModel(renderer, ice, [], transform, view, new() { Cull = true }, Ff7BattleIceEffect.Opacity(frame));
    }

    private Ff7BattleBillboardRenderer LoadBillboard(string name, string[] textures, int frames, Func<ushort, int> selectPage, TriangleBlend blend = TriangleBlend.Replace, bool nativePageOrigin = false)
    {
        LgpArchive magic = MagicArchive();
        PcTexture[] pages = textures.Select(texture => PcTexture.Read(Read(magic, texture))).ToArray();
        Ff7BattleBillboardRenderer renderer = new(BattleBillboard.Read(Read(magic, name)), pages, selectPage, blend, nativePageOrigin: nativePageOrigin);
        if (renderer.FrameCount != frames)
        {
            throw new InvalidDataException($"FFVII battle billboard '{name}' differs from its native callback duration.");
        }
        return renderer;
    }

    private static Vector3 SpriteAnchor(short x, short y, short z, int radius, CameraView view)
    {
        return OffsetTowardCamera(view.Transform(new(x, y, z)), radius);
    }

    private static Vector3 OffsetTowardCamera(Vector3 point, int radius)
    {
        int x = (int)point.X;
        int y = (int)-point.Y;
        int z = (int)point.Z;
        float length = MathF.Sqrt((float)((double)x * x + (double)y * y + (double)z * z));
        if (length == 0)
        {
            return Vector3.Zero;
        }
        float unit = 4096f / length;
        int Shift(int component) => (int)((double)component * unit) * -radius >> 12;
        return new(x + Shift(x), -(y + Shift(y)), z + Shift(z));
    }

    private LgpArchive MagicArchive()
    {
        return magicArchive ?? throw new InvalidOperationException("FFVII battle effect presentation requires its original magic archive.");
    }

    private Ff7BattleBillboardRenderer LoadCure(Ff7BattleCureKind kind, byte atlasY)
    {
        PcTexture texture = cureTexture ?? throw new InvalidOperationException("FFVII Cure presentation requires its original battle effect texture.");
        ushort clut = kind switch
        {
            Ff7BattleCureKind.Ether => 0x7A47,
            Ff7BattleCureKind.Antidote => 0x7887,
            Ff7BattleCureKind.PhoenixDown => 0x78C7,
            _ => 0x7807
        };
        IReadOnlyList<BattleBillboardQuad>[] frames = new IReadOnlyList<BattleBillboardQuad>[atlasY == 0x80 ? 8 : 9];
        for (int frame = 0; frame < frames.Length; frame++)
        {
            frames[frame] = [new(0, 0, -16, -16, (short)(frame % 8 * 32), atlasY, 0x3A, clut, 31, 31)];
        }
        return new(frames, [texture], _ => 0, TriangleBlend.Add);
    }

    private PcPolygonModel LoadIce()
    {
        return PcPolygonModel.Read(Read(MagicArchive(), "brizad.p"));
    }

    private ActorModel LoadActor(string prefix)
    {
        if (actors.TryGetValue(prefix, out ActorModel? existing))
        {
            return existing;
        }
        byte[] header = Read(archive, prefix + "aa");
        BattleModelData data = BattleModelData.Read(header, Read(archive, prefix + "da"));
        int textureCount = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(24));
        if (textureCount is < 0 or > 10)
        {
            throw new InvalidDataException($"FFVII battle model '{prefix}' has invalid texture count.");
        }
        PcTexture[] textures = new PcTexture[textureCount];
        for (int index = 0; index < textures.Length; index++)
        {
            textures[index] = PcTexture.Read(Read(archive, prefix + EncodeLetters(2 + index)));
        }
        PcPolygonModel?[] parts = new PcPolygonModel?[data.Bones.Count];
        for (int index = 0; index < parts.Length; index++)
        {
            if (data.Bones[index].HasModel)
            {
                parts[index] = PcPolygonModel.Read(Read(archive, prefix + EncodeLetters(12 + index)));
            }
        }
        ActorModel actor = new(data, parts, textures);
        actors.Add(prefix, actor);
        return actor;
    }

    private static BattleClipFrame Frame(IReadOnlyList<BattleClipFrame> clip, int frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        return clip[frame % clip.Count];
    }

    private static string EncodeLetters(int value)
    {
        return new([(char)('a' + value / 26), (char)('a' + value % 26)]);
    }

    private static byte[] Read(LgpArchive archive, string name)
    {
        IReadOnlyList<LgpEntry> entries = archive.Find(name);
        if (entries.Count != 1)
        {
            throw new InvalidDataException($"FFVII battle model resource '{name}' resolves to {entries.Count} members.");
        }
        return archive.ReadEntry(entries[0]);
    }

    private sealed class ActorModel(BattleModelData data, PcPolygonModel?[] parts, PcTexture[] textures)
    {
        private readonly Dictionary<(BattlePackedClip Pack, bool Weapon), IReadOnlyList<BattleClipFrame>> clips = [];
        private readonly Dictionary<int, PcPolygonModel> weapons = [];
        public BattleModelData Data => data;
        public PcPolygonModel?[] Parts => parts;
        public PcTexture[] Textures => textures;

        public bool HasWeaponClip(int index, BattleLimitAnimation? limit)
        {
            return PackedClip(index, true, limit) is { PackedData.Length: > 4 };
        }

        public IReadOnlyList<BattleClipFrame> GetClip(int index, bool weapon, BattleLimitAnimation? limit)
        {
            BattlePackedClip packed = PackedClip(index, weapon, limit) ?? throw new InvalidDataException($"FFVII battle clip {index} is absent from its model bank.");
            if (!clips.TryGetValue((packed, weapon), out IReadOnlyList<BattleClipFrame>? clip))
            {
                clip = BattleClipDecoder.Decode(packed);
                clips.Add((packed, weapon), clip);
            }
            return clip;
        }

        private BattlePackedClip? PackedClip(int index, bool weapon, BattleLimitAnimation? limit)
        {
            if (limit is not null && index >= BattleLimitAnimation.FirstClip)
            {
                IReadOnlyList<BattlePackedClip> overlay = weapon ? limit.Weapon : limit.Body;
                int relative = index - BattleLimitAnimation.FirstClip;
                return relative < overlay.Count ? overlay[relative] : null;
            }

            IReadOnlyList<BattlePackedClip> bank = weapon ? data.WeaponClips : data.BodyClips;
            return (uint)index < bank.Count ? bank[index] : null;
        }

        public PcPolygonModel GetWeapon(LgpArchive archive, string prefix, int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, 16);
            if (!weapons.TryGetValue(index, out PcPolygonModel? weapon))
            {
                weapon = PcPolygonModel.Read(Read(archive, prefix + EncodeLetters(62 + index)));
                weapons.Add(index, weapon);
            }
            return weapon;
        }
    }

    private readonly record struct ModelColorBlend(byte Red, byte Green, byte Blue, byte OriginalWeight);

    private readonly record struct CameraView(Vector3 Position, Vector3 Right, Vector3 Up, Vector3 Forward, int Width, int Height, float Focal)
    {
        public static CameraView Create(SceneCameraPlacement camera, int width, int height)
        {
            BattleCameraBasis basis = BattleCameraBasis.Create(camera);
            return new(basis.Position, basis.Right, basis.Up, basis.Forward, width, height, (camera.FocalLength ?? 512) * height / 240f);
        }

        public Vector3 Transform(Vector3 world)
        {
            Vector3 relative = world - Position;
            return new(Vector3.Dot(relative, Right), Vector3.Dot(relative, Up), Vector3.Dot(relative, Forward));
        }
    }
}
