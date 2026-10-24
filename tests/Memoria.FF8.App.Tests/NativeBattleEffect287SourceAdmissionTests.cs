using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect287SourceAdmissionTests
{
    [Fact]
    public void Fresh_original287_source_has_nonzero_current_local_and_world_composed_triangle_inputs()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT287_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT287_ADMISSION_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a fresh standalone and a new input-admission evidence directory.");
        Assert.False(Directory.Exists(output));
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string unchanged = JsonSerializer.Serialize(source);
        BattleSession session = BattleSession.Restore(source);
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Assert.True(session.NativeEntryReady);
        Assert.Equal(0, source.Frame);
        Assert.Equal(431, source.Definition.EncounterId);
        Assert.Equal(126, source.Definition.StageId);
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        NativeBattleGraphicsModel model = source.NativeGraphics.Models[3];
        BattleCombatantDefinition caster = session.GetCombatantDefinition(source.NativeGraphics.ActorIds[3]!);
        Assert.Equal(109, caster.ModelId);
        Assert.Equal(1, caster.SceneSlot);
        Assert.Equal((byte)125, model.ModelId4);
        Assert.NotNull(model.GeometryWorldMatrix);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource ?? throw new InvalidDataException("The selected caster requires its actual current skeleton.");
        Assert.Equal(28, pose.BoneCount);
        ImmutableArray<NativeBattleActionMatrix> local = NativeBattleBodyBoneMatrices.Require(model, pose);
        NativeBattleGraphicsModel composedModel = NativeBattleBodyBoneMatrices.Compose5095B0(model, model.GeometryWorldMatrix!);
        ImmutableArray<NativeBattleActionMatrix> composed = NativeBattleBodyBoneMatrices.Require(composedModel, pose);
        NativeBattleEffect287Resources resources = game.ReadNativeEffect287Resources();
        List<object> results = [];
        Observe("local", local);
        Observe("world-composed", composed);
        Assert.Equal(unchanged, JsonSerializer.Serialize(source));
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "current-input-admission.json"), JsonSerializer.Serialize(new { Status = "passed", Scope = "Fresh source input domain only; current canonical composition and GTE arithmetic, no effect execution or later-pose claim", source.Frame, source.NativeGraphics.Callback, PhysicalCaster = 3, Bones = local.Length, CurrentWorld = model.GeometryWorldMatrix, LocalBone1 = local[1], WorldComposedBone1 = composed[1], Triangles = results }));

        void Observe(string kind, ImmutableArray<NativeBattleActionMatrix> bones)
        {
            NativeBattleGeometryRegisterStep step = new(source.NativeEffectMemory.GeometryRegisters!, source.NativeEffectMemory.ScriptedColorRegisters!);
            int[] endpoint = Transform(resources.ReadStatic(0x13FAEA0, 8));
            for (int index = 0; index < 13; index++)
            {
                byte[] raw = resources.ReadStatic(0x13FAEB0 + (uint)(index * 24), 24);
                int[][] points = Enumerable.Range(0, 3).Select(corner => Transform(raw.AsSpan(corner * 8, 8).ToArray())).ToArray();
                int[] a = Enumerable.Range(0, 3).Select(axis => points[0][axis] - points[1][axis]).ToArray();
                int[] b = Enumerable.Range(0, 3).Select(axis => points[2][axis] - points[0][axis]).ToArray();
                byte?[] rotation = step.Geometry.Rotation20.ToArray();
                for (int axis = 0; axis < 3; axis++)
                {
                    byte[] bytes = BitConverter.GetBytes(a[axis]);
                    for (int lane = 0; lane < 4; lane++)
                    {
                        rotation[axis * 8 + lane] = bytes[lane];
                    }
                }
                step = step with { Geometry = step.Geometry with { Rotation20 = rotation.ToImmutableArray() }, Colors = step.Colors with { Ir = b.Select(value => (int?)value).ToImmutableArray() } };
                step = NativeBattleGeometry.OuterProduct45E670(step);
                int[] cross = step.Colors.Mac.Select(value => value!.Value).ToArray();
                int maximum = cross.Select(value => value < 0 ? unchecked(-value) : value).Max();
                step = step with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(step.Geometry, unchecked((uint)maximum)) };
                int leading = checked((int)step.Geometry.Lzcr!.Value);
                int[] reduced = cross.Select(value => leading < 18 ? value >> (18 - leading) : value).ToArray();
                int normalLength = Length(reduced);
                int[] center = Enumerable.Range(0, 3).Select(axis => (int)unchecked((short)(unchecked((points[0][axis] + points[1][axis] + points[2][axis]) * 21845) >> 16))).ToArray();
                int distance = Length(Enumerable.Range(0, 3).Select(axis => endpoint[axis] - center[axis]).ToArray());
                results.Add(new { Kind = kind, Index = index, Points = points, Cross = cross, ReducedCross = reduced, NormalLength = normalLength, Center = center, Endpoint = endpoint, EndpointDistance = distance });
            }

            int[] Transform(byte[] point)
            {
                int bone = BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(6));
                Assert.InRange(bone, 0, bones.Length - 1);
                step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, bones[bone], true, true, null) };
                step = NativeBattleGeometry.LoadVector(step, 0, new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(4))), checked((short)bone));
                step = NativeBattleGeometry.Transform(step);
                return step.Colors.Ir.Select(value => value!.Value).ToArray();
            }

            int Length(int[] lanes)
            {
                step = step with { Colors = step.Colors with { Ir = lanes.Select(value => (int?)value).ToImmutableArray() } };
                step = NativeBattleReflectBeam135Geometry.Square45F930(step);
                int sum = unchecked(step.Colors.Mac[0]!.Value + step.Colors.Mac[1]!.Value + step.Colors.Mac[2]!.Value);
                step = step with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(step.Geometry, unchecked((uint)sum)) };
                int even = checked((int)step.Geometry.Lzcr!.Value) & ~1;
                int tableIndex = even <= 24 ? sum >> (24 - even) : unchecked(sum << (even - 24));
                Assert.InRange(tableIndex, 64, 255);
                int root = unchecked(resources.ReadNormalizationWord(tableIndex) << ((31 - even) >> 1));
                int length = root >> 12;
                Assert.True(length > 0, "The current input reaches the original zero-length division boundary.");
                return length;
            }
        }
    }
}
