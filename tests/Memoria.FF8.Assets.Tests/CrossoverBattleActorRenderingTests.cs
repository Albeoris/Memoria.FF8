using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class CrossoverBattleActorRenderingTests(ITestOutputHelper output)
{
    [Fact]
    public void Installed_Squall_and_Revolver_submit_distinct_original_materials_in_the_FFVII_arena_pass()
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select the original FFVII and FFVIII installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattlePlacement placement = Ff7PcExecutableData.LoadBattlePlacement(installation).Party(formation.Layout, 1, 0, 0, false);
        using BattleAssets assets = new(ff8Root!);
        using GameAssets game = new(ff8Root!);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        byte weaponId = vitals.Records[vitals.FindPhysicalRow(0)].Weapon;
        BattleModel body = assets.LoadPlayer(0);
        BattleModel weapon = Assert.IsType<BattleModel>(assets.ResolveWeaponModel(0, weaponId, body));
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = weaponId });
        NativeBattlePoseSource source = Assert.IsType<NativeBattlePoseSource>(resource.PoseSource);
        NativeBattleVector origin = new(0, 0, 2200);
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = new(0, 0)
        {
            Transform = new(origin, origin, 0, 0, 4096),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() },
            LinkedTransform = new(default, default)
        };
        model = NativeBattleGraphicsModels.CompleteLoad(model, 0, 0, ref random);
        model = NativeBattleGraphicsModels.SelectSequence(model, 1);
        model = Publish(NativeBattleSequenceMachine.Advance(model, resource, new(0, 0)).Model);
        model = model with { Flags2 = (ushort)(model.Flags2!.Value & ~4) };
        (float top, float feet) = SquallBounds(model, body, source);
        (float cloudTop, float cloudFeet) = CloudBounds(archive);
        float scale = (cloudFeet - cloudTop) / (feet - top);
        Assert.InRange(scale, 0.05f, 5f);
        output.WriteLine($"Squall body Y {top}..{feet}; Cloud body Y {cloudTop}..{cloudFeet}; scale {scale:R}; party anchor {placement}; shared weapon skeleton {weapon.NativeWeaponUsesBodySkeleton}");
        Matrix4x4 mapping = Matrix4x4.CreateTranslation(0, -feet, -origin.Z) * Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateRotationY(placement.Yaw * MathF.Tau / 4096) * Matrix4x4.CreateTranslation(placement.Position + new Vector3(0, cloudFeet, 0));
        CrossoverFf8BattleActorVisual guest = new(body, weapon, model, mapping);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        SceneCameraPlacement camera = scene.Cameras[0];
        Capture arenaOnly = Render(null);
        Capture standing = Render(guest);
        Assert.Equal(arenaOnly.Triangles.Count, standing.Triangles.Count - GuestTriangles(standing).Count());
        Assert.Contains(standing.Triangles, triangle => body.Textures.Any(texture => ReferenceEquals(texture, triangle.Texture)));
        Assert.Contains(standing.Triangles, triangle => weapon.Textures.Any(texture => ReferenceEquals(texture, triangle.Texture)));
        Assert.All(GuestTriangles(standing), triangle => Assert.True(triangle.Depth > 0 && float.IsFinite(triangle.Depth)));
        Assert.Contains(GuestTriangles(standing), triangle => triangle.A.X is >= 0 and < 320 && triangle.A.Y is >= 0 and < 240);
        output.WriteLine($"Stage triangles {arenaOnly.Triangles.Count}; guest triangles {GuestTriangles(standing).Count()}");

        model = NativeBattleGraphicsModels.SelectSequence(model, game.ReadNativeBattleCommandSequences()[1]);
        NativeBattleAuxiliaryState auxiliary = NativeBattleAuxiliaryState.Clear();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleSequenceInputs inputs = new(0, 0)
        {
            ReadModelExternal = (_, variable) => variable switch
            {
                17 => 1999,
                25 or 46 => 0,
                18 or 19 => memory.ReadSequenceGlobal5044B0(variable),
                _ => throw new NotSupportedException($"Unexpected Squall render probe variable {variable}.")
            },
            WriteExternal = (variable, value) => memory = memory.WriteSequenceGlobal5048E0(variable, value),
            ScheduleSoundParticle = (program, two, bone) =>
            {
                (auxiliary, random, bool allocated) = auxiliary.AppendSoundParticle(0, two, bone, program, random);
                Assert.True(allocated);
            }
        };
        for (int callback = 0; callback < 16 && model.SequenceState!.Header40 == 0; callback++)
        {
            model = Publish(NativeBattleSequenceMachine.Advance(model, resource, inputs).Model);
        }
        Assert.NotEqual(0, model.SequenceState!.Header40);
        Capture moving = Render(guest with { Model = model });
        Assert.NotEmpty(GuestTriangles(moving));
        Assert.NotEqual(GuestTriangles(standing).First().A, GuestTriangles(moving).First().A);
        NativeBattleGraphicsModel displacedPoseAtOrigin = Publish(model with { SequenceState = model.SequenceState with { Header40 = 0 } });
        Capture samePoseAtOrigin = Render(guest with { Model = displacedPoseAtOrigin });
        Assert.NotEqual(GuestTriangles(samePoseAtOrigin).First().A, GuestTriangles(moving).First().A);

        NativeBattleGraphicsModel Publish(NativeBattleGraphicsModel current) => current with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(current, source) };

        Capture Render(CrossoverFf8BattleActorVisual? actor)
        {
            Capture capture = new();
            renderer.Render(capture, 320, 240, new(camera, []) { Guest = actor });
            Assert.Equal((1, 1), (capture.Begins, capture.Ends));
            return capture;
        }

        IEnumerable<Triangle> GuestTriangles(Capture capture) => capture.Triangles.Where(triangle => body.Textures.Concat(weapon.Textures).Any(texture => ReferenceEquals(texture, triangle.Texture)));
    }

    private static (float Top, float Feet) SquallBounds(NativeBattleGraphicsModel model, BattleModel body, NativeBattlePoseSource source)
    {
        NativeBattleActionMatrix[] bones = NativeBattleBodyBoneMatrices.Require(model, source).Select(bone => NativeBattleGfMatrices.Compose56C2F0(model.GeometryWorldMatrix!, bone)).ToArray();
        int[] vertices = body.Meshes.SelectMany(mesh => mesh.Vertices).Select(vertex => NativeBattleGfMatrices.TransformMac460840(bones[vertex.Bone], vertex.NativePosition!.Value).Y).ToArray();
        return (vertices.Min(), vertices.Max());
    }

    private static (float Top, float Feet) CloudBounds(LgpArchive archive)
    {
        byte[] Read(string name) => archive.ReadEntry(Assert.Single(archive.Find(name)));
        BattleModelData data = BattleModelData.Read(Read("rtaa"), Read("rtda"));
        Matrix4x4[] bones = new Matrix4x4[data.Bones.Count];
        BattleRigidPose.WriteTransforms(data, BattleClipDecoder.Decode(data.BodyClips[0])[0], Matrix4x4.Identity, bones);
        List<float> heights = [];
        for (int index = 0; index < data.Bones.Count; index++)
        {
            if (data.Bones[index].HasModel)
            {
                int file = 12 + index;
                string name = new(['r', 't', (char)('a' + file / 26), (char)('a' + file % 26)]);
                heights.AddRange(PcPolygonModel.Read(Read(name)).Vertices.Select(vertex => Vector3.Transform(vertex, bones[index]).Y));
            }
        }
        return (heights.Min(), heights.Max());
    }

    private sealed record Triangle(Vector2 A, float Depth, PixelImage? Texture);

    private sealed class Capture : ITriangleRenderer
    {
        public int Begins { get; private set; }
        public int Ends { get; private set; }
        public List<Triangle> Triangles { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Begins++;
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Assert.Equal(1, Begins);
            Assert.Equal(0, Ends);
            Triangles.Add(new(a.Position, a.Depth, material.Texture));
        }

        public void End()
        {
            Ends++;
        }
    }
}
