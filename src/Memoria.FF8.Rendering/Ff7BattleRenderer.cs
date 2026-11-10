using System.Buffers.Binary;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7BattleActorVisual(string ModelPrefix, Vector3 Position, float FacingRadians, int ClipIndex, int ClipFrame, int? WeaponIndex = null, BattleLimitAnimation? Limit = null);
public sealed record Ff7BattleRenderFrame(SceneCameraPlacement Camera, IReadOnlyList<Ff7BattleActorVisual> Actors, Ff7BattleFireState? Fire = null, Ff7BattleIceState? Ice = null, Ff7BattleBoltState? Bolt = null, Ff7BattleCureState? Cure = null, Ff7BattleBraverState? Braver = null)
{
    public CrossoverFf8BattleActorVisual? Guest { get; init; }
}

/// <summary>Submits original FFVII stage and rigid actor geometry without advancing the battle.</summary>
public sealed class Ff7BattleRenderer
{
    private readonly LgpArchive archive;
    private readonly Ff7BattleArenaResources arena;
    private readonly LgpArchive? magicArchive;
    private readonly PcTexture? cureTexture;
    private Ff7BattleBillboardRenderer? cure;
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

    public Ff7BattleRenderer(LgpArchive archive, int locationId, LgpArchive? magicArchive = null, PcTexture? cureTexture = null)
    {
        this.archive = archive ?? throw new ArgumentNullException(nameof(archive));
        this.magicArchive = magicArchive;
        this.cureTexture = cureTexture;
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
        CameraView view = CameraView.Create(frame.Camera, width, height);
        renderer.Begin(width, height, new Vector4(0, 0, 0, 1), 0);
        try
        {
            Ff7ModelMaterialState material = new() { Cull = true };
            foreach (Ff7BattleArenaPart part in arena.Geometry)
            {
                DrawModel(renderer, part.Polygon, arenaTextures, Matrix4x4.Identity, view, material);
            }
            foreach (Ff7BattleActorVisual actor in frame.Actors)
            {
                DrawActor(renderer, actor, view, material);
            }
            if (frame.Guest is { } guest)
            {
                CrossoverFf8BattleActorRenderer.Draw(renderer, guest, view.Transform, width, height, view.Focal);
            }
            if (frame.Fire is { Sprites.Count: > 0 } effect)
            {
                fire ??= LoadBillboard("fi_a01.s", ["fire00.tex", "fire01.tex"], Ff7BattleFireEffect.SpriteFrameCount, page => page switch
                {
                    0x100 => 0,
                    0x104 or 0x144 => 1,
                    _ => throw new NotSupportedException($"FFVII Fire billboard uses unsupported texture page 0x{page:X4}.")
                });
                foreach (Ff7BattleFireSprite sprite in effect.Sprites)
                {
                    fire.Draw(renderer, sprite.Frame, SpriteAnchor(sprite.X, sprite.Y, sprite.Z, sprite.CollisionRadius, view), width, height, view.Focal);
                }
            }
            if (frame.Ice is { Completed: false, Frame: >= 0, Crystals: { } crystals } iceEffect)
            {
                ice ??= LoadIce();
                foreach (Ff7BattleIceCrystal crystal in crystals)
                {
                    Matrix4x4 transform = Matrix4x4.CreateScale(Ff7BattleIceEffect.Scale(iceEffect.Frame, crystal.CollisionRadius)) * Matrix4x4.CreateTranslation(crystal.X, crystal.Y, crystal.Z);
                    DrawModel(renderer, ice, [], transform, view, new() { Cull = true }, Ff7BattleIceEffect.Opacity(iceEffect.Frame));
                }
            }
            if (frame.Bolt is { Completed: false, Frame: >= 0 } bolt)
            {
                DrawBolt(renderer, bolt, view);
            }
            if (frame.Cure is { Particles.Count: > 0 } cureEffect)
            {
                cure ??= LoadCure();
                foreach (Ff7BattleCureParticle particle in cureEffect.Particles)
                {
                    cure.Draw(renderer, particle.Frame, SpriteAnchor(particle.X, particle.Y, particle.Z, 500, view), width, height, view.Focal, particle.Scale / 4096f);
                }
            }
            if (frame.Braver is { Completed: false } braver)
            {
                DrawBraver(renderer, braver, view);
            }
        }
        finally
        {
            renderer.End();
        }
    }

    private void DrawActor(ITriangleRenderer renderer, Ff7BattleActorVisual actor, CameraView view, Ff7ModelMaterialState material)
    {
        ActorModel resource = LoadActor(actor.ModelPrefix);
        Matrix4x4 world = Matrix4x4.CreateRotationY(actor.FacingRadians) * Matrix4x4.CreateTranslation(actor.Position);
        BattleClipFrame frame = Frame(resource.GetClip(actor.ClipIndex, false, actor.Limit), actor.ClipFrame);
        Span<Matrix4x4> transforms = stackalloc Matrix4x4[resource.Data.Bones.Count];
        BattleRigidPose.WriteTransforms(resource.Data, frame, world, transforms);
        for (int index = 0; index < transforms.Length; index++)
        {
            if (resource.Parts[index] is PcPolygonModel model)
            {
                DrawModel(renderer, model, resource.Textures, transforms[index], view, material);
            }
        }
        if (actor.WeaponIndex is int weaponIndex && resource.HasWeaponClip(actor.ClipIndex, actor.Limit))
        {
            BattleClipFrame weaponFrame = Frame(resource.GetClip(actor.ClipIndex, true, actor.Limit), actor.ClipFrame);
            if (weaponFrame.Rotations.Count != 1)
            {
                throw new NotSupportedException($"FFVII battle model '{actor.ModelPrefix}' weapon requires a separate multi-joint skeleton.");
            }
            DrawModel(renderer, resource.GetWeapon(archive, actor.ModelPrefix, weaponIndex), resource.Textures, BattleRigidPose.FrameTransform(weaponFrame) * world, view, material);
        }
    }

    private void DrawModel(ITriangleRenderer renderer, PcPolygonModel model, IReadOnlyList<PcTexture> textures, Matrix4x4 transform, CameraView view, Ff7ModelMaterialState material, float opacity = 1)
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
            TriangleMaterial triangleMaterial = new(image, opacity < 1 ? TriangleBlend.Alpha : material.Blend, material.DepthTest ? TriangleDepthComparison.GreaterEqual : TriangleDepthComparison.Always, material.DepthWrite && opacity == 1, material.AlphaReference, material.Sampler) { PreserveTargetAlpha = false };
            CameraSpaceVertex Vertex(int local)
            {
                int vertex = group.VertexStart + local;
                uint argb = vertex < model.VertexColors.Count ? model.VertexColors[vertex] : uint.MaxValue;
                Vector4 color = new((argb >> 16 & 255) / 255f, (argb >> 8 & 255) / 255f, (argb & 255) / 255f, (argb >> 24) / 255f * state.VertexAlpha / 255f * opacity);
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
        const string archivePath = "ff7/data/battle/magic/thunder/";
        if (bolt.Frame < 16 && bolt.Strikes is { } strikes)
        {
            shockwave ??= PcPolygonModel.Read(Read(MagicArchive(), archivePath + "thund_3.p"));
            shockwaveTextures ??= [PcTexture.Read(Read(MagicArchive(), archivePath + "sonic_1.tex"))];
            foreach (Ff7BattleBoltStrike strike in strikes)
            {
                if (bolt.Frame < 9)
                {
                    lightning ??= LoadBillboard("kami_1.s", [archivePath + "thunde00.tex"], 5, _ => 0);
                    lightning.Draw(renderer, bolt.Frame / 2, SpriteAnchor(strike.X, strike.Y, strike.Z, strike.CollisionRadius, view), view.Width, view.Height, view.Focal, scale: 2);
                }
                float scale = 1 + bolt.Frame / 8f;
                float opacity = bolt.Frame < 8 ? 1 : (16 - bolt.Frame) / 8f;
                Matrix4x4 transform = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(strike.X, strike.Y, strike.Z);
                DrawModel(renderer, shockwave, shockwaveTextures, transform, view, new() { Cull = true }, opacity);
            }
        }
        if (bolt.Sparks is { Count: > 0 } active)
        {
            sparks ??= LoadBillboard(archivePath + "spark_1.s", [archivePath + "thunde01.tex"], 8, _ => 0);
            foreach (Ff7BattleBoltSpark spark in active)
            {
                sparks.Draw(renderer, spark.Frame, SpriteAnchor(spark.X, spark.Y, spark.Z, spark.CollisionRadius, view), view.Width, view.Height, view.Focal, scale: 2, flip: spark.Flip);
            }
        }
    }

    private Ff7BattleBillboardRenderer LoadBillboard(string name, string[] textures, int frames, Func<ushort, int> selectPage, TriangleBlend blend = TriangleBlend.Replace)
    {
        LgpArchive magic = MagicArchive();
        PcTexture[] pages = textures.Select(texture => PcTexture.Read(Read(magic, texture))).ToArray();
        Ff7BattleBillboardRenderer renderer = new(BattleBillboard.Read(Read(magic, name)), pages, selectPage, blend);
        if (renderer.FrameCount != frames)
        {
            throw new InvalidDataException($"FFVII battle billboard '{name}' differs from its native callback duration.");
        }
        return renderer;
    }

    private static Vector3 SpriteAnchor(short x, short y, short z, int radius, CameraView view)
    {
        return view.Transform(new(x, y, z)) - Vector3.UnitZ * radius;
    }

    private LgpArchive MagicArchive()
    {
        return magicArchive ?? throw new InvalidOperationException("FFVII battle effect presentation requires its original magic archive.");
    }

    private Ff7BattleBillboardRenderer LoadCure()
    {
        PcTexture texture = cureTexture ?? throw new InvalidOperationException("FFVII Cure presentation requires its original battle effect texture.");
        IReadOnlyList<BattleBillboardQuad>[] frames = new IReadOnlyList<BattleBillboardQuad>[8];
        for (int frame = 0; frame < frames.Length; frame++)
        {
            frames[frame] = [new(0x3A, 0, -16, -16, (short)(frame * 32), 0, 0x2E, 0x7807, 31, 31)];
        }
        return new(frames, [texture], _ => 0);
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

    private readonly record struct CameraView(Vector3 Position, Vector3 Right, Vector3 Up, Vector3 Forward, int Width, int Height, float Focal)
    {
        public static CameraView Create(SceneCameraPlacement camera, int width, int height)
        {
            Vector3 direction = camera.Focus - camera.Position;
            Vector3 right = Vector3.Cross(direction, -Vector3.UnitY);
            if (right.LengthSquared() == 0)
            {
                throw new InvalidDataException("FFVII battle camera requires a nonvertical view direction.");
            }
            direction = Vector3.Normalize(direction);
            right = Vector3.Normalize(right);
            Vector3 up = Vector3.Cross(right, direction);
            return new(camera.Position, right, up, direction, width, height, 512 * height / 240f);
        }

        public Vector3 Transform(Vector3 world)
        {
            Vector3 relative = world - Position;
            return new(Vector3.Dot(relative, Right), Vector3.Dot(relative, Up), Vector3.Dot(relative, Forward));
        }
    }
}
