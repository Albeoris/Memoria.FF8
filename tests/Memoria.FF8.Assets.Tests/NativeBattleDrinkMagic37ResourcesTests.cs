using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDrinkMagic37ResourcesTests
{
    [Fact]
    public void Original_orb_wave_reaches_both_polygon_leaves_with_unknown_W_preserved_in_the_canonical_GTE_input()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the Drink Magic orb.");
        using GameAssets game = new(directory!);
        NativeBattleDrinkMagic37Resources resources = game.ReadNativeDrinkMagic37Resources();
        NativeBattleDrinkMagic37State cold = NativeBattleDrinkMagic37State.FromColdProcess(resources);
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleDrinkMagic37OrbWaveStep wave = NativeBattleDrinkMagic37OrbWave.Transform(resources.OrbShape, cold.OrbMesh, 0xA0, -0xA0, registers);
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleDrinkMagic37Profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(4, 0x85C1F0, 96);
        NativeBattleModulePoolTask task = appended.Hierarchy.Read(appended.Address).WriteDword(0x4C, 0x15D62EC).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteWord(0x5E, 8);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        NativeBattleDrinkMagic37OrbPreparation prepared = NativeBattleDrinkMagic37OrbRendering.Prepare(task, camera, NativeBattleEffectArena.Clear().MarkUnknown(0, 104));
        NativeBattleActorWaveMeshStep drawn = NativeBattleDrinkMagic37OrbRendering.EmitBatch(wave.Mesh, prepared, projection, wave.Registers, new(null, null, null, null, null), 0, 0, 1024, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Contains(drawn.Batch.Attempts, attempt => attempt.Primitive is not null && attempt.PacketLength == 40);
        Assert.Contains(drawn.Batch.Attempts, attempt => attempt.Primitive is not null && attempt.PacketLength == 52);
        Assert.False(drawn.Registers.Colors.NormalInputKnown!.Value[6]);
        Assert.False(drawn.Registers.Colors.NormalInputKnown.Value[7]);
        Assert.Equal(resources.OrbInitial.AsSpan().Slice(2216).ToArray(), wave.Mesh.Read(2216, resources.OrbInitial.Length - 2216));
    }

    [Fact]
    public void Original_resources_keep_the_mutable_orb_separate_from_its_vertex_only_shape_and_node_bank()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the Drink Magic module.");
        using GameAssets game = new(directory!);
        NativeBattleDrinkMagic37Resources resources = game.ReadNativeDrinkMagic37Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeDrinkMagic37Resources());
        Assert.Equal(0x440, resources.Animation.Data.Length);
        Assert.Equal(0x17C8420u, resources.Executable.ResourceAddress);
        Assert.Equal(0x430C, resources.Executable.Data.Length);
        Assert.Equal((ushort)37, resources.GenericNodes().Effect);
        Assert.Equal(8992, resources.OrbInitial.Length);
        Assert.Equal(2216, resources.OrbShape.Length);
        Assert.Equal(276u, BinaryPrimitives.ReadUInt32LittleEndian(resources.OrbShape.AsSpan().Slice(4)));
        Assert.Contains(resources.OrbInitial, value => value != 0);
        NativeBattleDrinkMagic37State cold = NativeBattleDrinkMagic37State.FromColdProcess(resources);
        Assert.Null(cold.Nodes);
        Assert.Equal(resources.OrbInitial.ToArray(), cold.OrbMesh.Read(0, NativeBattleDrinkMagic37Profile.OrbLength));
        Assert.All(cold.CapturedAnchor8, value => Assert.Equal((byte)0, value));
        Assert.Equal((ushort)6, BinaryPrimitives.ReadUInt16LittleEndian(resources.Trajectory.AsSpan().Slice(8)));
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(resources.OrbSprite.AsSpan().Slice(8)));
    }
}
