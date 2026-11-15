using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectParticipantResourceTests
{
    [Fact]
    public void Source_load_publishes_real_body_and_linked_rows_without_reinitializing_actor_progress()
    {
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, 8);
        NativeBattleGraphicsResource resource = new([0, 0]) { ModelLoadSources = [body, linked] };
        BattleDefinition definition = new() { Combatants = [new() { Id = "source", Name = "Source", IsPlayer = true }, new() { Id = "target", Name = "Target" }] };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        definition = (BattleDefinition)typeof(BattleSession).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
        typeof(BattleDefinition).GetProperty(nameof(BattleDefinition.NativeGraphics))!.SetValue(definition, new NativeBattleGraphicsDefinition(null!, [new("source", 0, resource)], []));
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeFileIo))!.SetValue(battle, NativeBattleFileIoState.FromColdProcess());
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeFileStaging))!.SetValue(battle, NativeBattleFileStaging.FromColdProcess());
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(battle, NativeBattleEffectMemory.FromColdProcess());
        NativeBattleSequenceState sequence = new() { Default2 = 14 };
        NativeBattleGraphicsModel source = new(3) { ModelId4 = 0, FileSelector128 = 0, FileSelector129 = 0, Flags2 = 4, SequenceState = sequence, LinkedTransform = new(default, default) };
        BattleModelOwner owner = battle.NativeActionPackets.ModelAliases.DecodeLiteral(0);
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, owner, source)]);
        typeof(BattleSession).GetField("presentationSourceOwner", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, owner);
        MethodInfo load = typeof(BattleSession).GetMethod("InitializeEffectPresentationSourceResources", BindingFlags.Instance | BindingFlags.NonPublic)!;
        graphics = (NativeBattleGraphicsState)load.Invoke(battle, [graphics, resource, false])!;
        NativeBattleGraphicsModel loaded = graphics.ActorModels.Get(owner);
        Assert.Equal(source.Flags0, loaded.Flags0);
        Assert.Same(sequence, loaded.SequenceState);
        Assert.Equal(loaded.ActiveResourceRow100, loaded.OriginalResourceRow104);
        Assert.NotNull(loaded.ActiveResourceRow100);
        Assert.NotNull(loaded.LinkedTextureResourceIndex);
        NativeBattleModelCpuResources cpu = battle.NativeModelCpuResources!;
        cpu.Validate();
        Assert.True(cpu.Cpu.Banks[0].HasResourceStorage);
        Assert.Equal(body.Bytes.AsSpan(body.Section(0), 64).ToArray(), NativeBattleModelResourceView.Open(cpu, battle.NativeFileStaging!, cpu.Rows[loaded.ActiveResourceRow100!.Value].Require(12), 64).Read(0, 64));
        Assert.Equal((short?)0, battle.NativeFileIo!.ActiveCount);

        uint directory = NativeBattleFileStaging.Address + 0x40000;
        uint row = directory + 0x100;
        ImmutableArray<byte> animation = body.Bytes.Slice(body.Section(2), body.Section(3) - body.Section(2));
        NativeBattleFileStaging staging = battle.NativeFileStaging!.Write(directory, ActorDat(animation));
        NativeBattleVictoryResourceBinding bound = NativeBattleVictoryResources.Bind5022C0(staging, cpu, loaded, row, directory);
        BoundSource restored = NativeBattleTestSnapshots.RestoreJson<BoundSource>(NativeBattleTestSnapshots.Json(new BoundSource(cpu, bound.Staging, bound.Model)));
        NativeBattleGraphicsResource resolved = NativeBattleVictoryResources.Resolve(restored.Staging, restored.Cpu, restored.Model, resource);
        Assert.Equal(1, resolved.PoseSource!.BoneCount);
        Assert.Equal(1, resolved.LinkedPoseSource!.BoneCount);
        Assert.Equal(animation, resolved.PoseSource.Animations);
        Assert.Equal((short?)14, restored.Model.SequenceState!.Locals[7]);
        Assert.Equal(1, restored.Model.SequenceState.Default2);
    }

    private static byte[] ActorDat(ImmutableArray<byte> animation)
    {
        byte[] bytes = new byte[20 + animation.Length * 2 + 4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 3);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 20);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 20 + animation.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 24 + animation.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), bytes.Length);
        animation.AsSpan().CopyTo(bytes.AsSpan(20));
        animation.AsSpan().CopyTo(bytes.AsSpan(24 + animation.Length));
        return bytes;
    }

    private sealed record BoundSource(NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model);
}
