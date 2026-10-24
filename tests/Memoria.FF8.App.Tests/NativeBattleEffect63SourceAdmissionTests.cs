using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect63SourceAdmissionTests
{
    [Fact]
    public void Fresh_current_bone_points_have_a_nonzero_native_XZ_basis_length_without_changing_source()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT63_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT63_ADMISSION_EVIDENCE");
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
        Assert.Equal(510, source.Definition.EncounterId);
        Assert.Equal(37, source.Definition.StageId);
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        BattleEncounter formation = assets.LoadEncounter(510);
        Assert.Equal(3, formation.Enemies.Count);
        List<object> actors = [];
        for (int index = 0; index < 3; index++)
        {
            int physical = index + 3;
            int original = index == 0 ? 124 : 125;
            BattleCombatantDefinition definition = session.GetCombatantDefinition(source.NativeGraphics.ActorIds[physical]!);
            Assert.Equal(original, definition.ModelId);
            Assert.True(definition.InitialActive);
            BattleEnemyPlacement placement = Assert.Single(formation.Enemies, actor => actor.Slot == definition.SceneSlot && actor.ModelId == original);
            Assert.True(placement.Enabled);
            Assert.Equal(placement.Hidden, definition.InitialHidden);
            Assert.Equal(placement.InitiallySuspended, definition.InitialSuspended);
            Assert.Equal(placement.Untargetable, definition.InitialUntargetable);
            NativeBattleGraphicsModel current = source.NativeGraphics.Models[physical];
            Assert.Equal(checked((byte)(original + 16)), current.ModelId4);
            Assert.NotEqual(0, current.Flags0 & 2);
            Assert.NotNull(current.Transform);
            Assert.NotNull(current.GeometryWorldMatrix);
            actors.Add(new { Physical = physical, definition.ModelId, definition.SceneSlot, placement.Enabled, placement.Hidden, placement.InitiallySuspended, placement.Untargetable, current.Flags0, current.Transform });
        }
        BattleCombatantDefinition caster = session.GetCombatantDefinition(source.NativeGraphics.ActorIds[4]!);
        Assert.Equal(1, caster.SceneSlot);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 361 && row.MagicId == 63 && row.KernelSection == 3 && row.KernelIndex == 361);
        Assert.Equal((byte)11, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleGraphicsModel model = source.NativeGraphics.Models[4];
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource ?? throw new NotSupportedException("The current caster requires its actual skeleton.");
        Assert.Equal(2, pose.BoneCount);
        Assert.Equal(pose.BoneCount, NativeBattleBodyBoneMatrices.Require(model, pose).Length);
        NativeBattleEffect63Resources resources = game.ReadNativeEffect63Resources();
        NativeBattleGeometryRegisterStep registers = new(source.NativeEffectMemory.GeometryRegisters!, source.NativeEffectMemory.ScriptedColorRegisters!);
        List<object> points = [];
        NativeBattleEffectArena first = Query(FirstPointAddress);
        NativeBattleEffectArena second = Query(SecondPointAddress);
        short midX = unchecked((short)((Word(first, 0) + Word(second, 0)) / 2));
        short midY = unchecked((short)((Word(first, 2) + Word(second, 2)) / 2));
        short midZ = unchecked((short)((Word(first, 4) + Word(second, 4)) / 2));
        NativeBattleModelTransform target = source.NativeGraphics.Models[0].Transform ?? throw new NotSupportedException("The source requires its actual current target position.");
        int x = midX - target.Position28.X;
        int z = target.Position28.Z - midZ;
        (NativeBattleActionMatrix basis, int length) = NativeBattleFunguarLaser277.Basis50CBA0(x, 0, z);
        Assert.NotEqual(0, length);
        int offsetX = unchecked(500 * x) / length;
        int offsetZ = unchecked(500 * z) / length;
        Assert.Equal(unchanged, JsonSerializer.Serialize(source));
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "current-input-admission.json"), JsonSerializer.Serialize(new { Status = "passed", Scope = "Current source pose only; exact point preparation and native basis length, no action execution or later-pose claim", source.Frame, source.NativeGraphics.Callback, source.Definition.EncounterId, source.Definition.StageId, Actors = actors, PhysicalCaster = 4, Bones = pose.BoneCount, Points = points, Midpoint = new { X = midX, Y = midY, Z = midZ }, Target = target.Position28, Difference = new { X = x, Z = z }, NativeBasis = basis, NativeLength = length, Division = new { X = offsetX, Z = offsetZ }, SnapshotUnchanged = true }));

        NativeBattleEffectArena Query(uint address)
        {
            byte[] local = resources.ReadStatic(address, 8);
            short selector = BinaryPrimitives.ReadInt16LittleEndian(local.AsSpan(6));
            Assert.Equal((short)1, selector);
            Assert.InRange(selector, (short)0, checked((short)(pose.BoneCount - 1)));
            NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(model, pose, NativeBattleEffectArena.Clear().MarkUnknown(0, 88));
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(prepared.Model, pose);
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(prepared.Model.GeometryWorldMatrix!, bones[selector]);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, composed, true, true, null) };
            NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(local), BinaryPrimitives.ReadInt16LittleEndian(local.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(local.AsSpan(4)));
            registers = NativeBattleGeometry.LoadVector(registers, 0, vector, selector);
            registers = NativeBattleGeometry.Transform(registers);
            Assert.Equal(local, registers.Colors.NormalInput!.Value);
            NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                result = result.Write(axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
            }
            Assert.Equal(new[] { true, true, true, true, true, true, false, false }, result.ReadSnapshot(0, 8).Known);
            (byte[] preparationBytes, bool[] preparationKnown) = prepared.Temporary58.ReadSnapshot(0, 88);
            (byte[] outputBytes, bool[] outputKnown) = result.ReadSnapshot(0, 8);
            points.Add(new { Address = address, Local = local, Selector = selector, CurrentBone = bones[selector], Composed = composed, PreparationBytes = preparationBytes, PreparationKnown = preparationKnown, OutputBytes = outputBytes, OutputKnown = outputKnown, registers.Colors.Mac, registers.Colors.Ir, registers.Colors.Flags, InputW = selector, OutputWKnown = false });
            return result;
        }
    }

    private static short Word(NativeBattleEffectArena point, int offset) => BinaryPrimitives.ReadInt16LittleEndian(point.Read(offset, 2));
}
