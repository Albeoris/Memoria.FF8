using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaSharedLifecycleTests
{
    [Fact]
    public void Current_bank_morph_survives_JSON_without_touching_other_spell_owners_or_vertex_padding()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaSpawnStep spawn = NativeBattleCuragaNodeKinematics.Spawn8818A0(fixture.Frame, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, 1, new(1));
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(spawn.Frame, source => source[0x16] = 5);
        frame = NativeBattleCuragaSpawnTests.Task(frame, fixture.Task, task => task.WriteDword(0x34 + 0x230, NativeBattleCuragaExecutableState.Address + 0xC4));
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0x130, 0, true);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0x134, 1, true);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0x138, 2048);
        frame = NativeBattleCuragaSpawnTests.Bank(frame, data =>
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0xC4), NativeBattleCuragaExecutableState.Address + 0x500);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0xC8), NativeBattleCuragaExecutableState.Address + 0x600);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x404), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x40E), 0xBEEF);
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x508 + axis * 2), (short)(100 + axis * 100));
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x608 + axis * 2), (short)(300 + axis * 100));
            }
        });
        NativeBattleCuragaFrame clone = NativeBattleCuragaSpawnTests.Clone(frame);
        string esuna = JsonSerializer.Serialize(frame.Memory.EsunaExecutable);
        string fullLife = JsonSerializer.Serialize(frame.Memory.FullLifeExecutable);
        NativeBattleCuragaFrame actual = NativeBattleCuragaNodeKinematics.AdvanceDrawable880DB0(frame, fixture.Task, NativeBattleCuragaSpawnTests.Node);
        NativeBattleCuragaFrame restored = NativeBattleCuragaNodeKinematics.AdvanceDrawable880DB0(clone, fixture.Task, NativeBattleCuragaSpawnTests.Node);
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(restored));
        byte[] vertex = actual.Memory.CuragaExecutable!.Read(NativeBattleCuragaExecutableState.Address + 0x408, 8);
        Assert.Equal(new short[] { 200, 300, 400 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(vertex.AsSpan(axis * 2))).ToArray());
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(vertex.AsSpan(6)));
        Assert.Equal(esuna, JsonSerializer.Serialize(actual.Memory.EsunaExecutable));
        Assert.Equal(fullLife, JsonSerializer.Serialize(actual.Memory.FullLifeExecutable));
    }

    [Fact]
    public void Memory_only_sprite_animation_keeps_an_unknown_bank_unknown()
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture();
        int node = fixture.Frame.State.Auxiliary16F80!.Value;
        NativeBattleCuragaFrame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { CuragaExecutable = null, Arena = fixture.Frame.Memory.Arena!.Write(node + 0x1D0, [0, 5, 0, 0, 0, 0]) } };
        NativeBattleCuragaFrame actual = NativeBattleCuragaSpriteAnimation.Advance880CE0(frame, fixture.Task, node, 0);
        Assert.Null(actual.Memory.CuragaExecutable);
        Assert.Equal(new byte[] { 1, 5, 0 }, actual.Memory.Arena!.Read(node + 0x1D0, 3));
    }

    [Fact]
    public void Memory_only_emitter_expiry_keeps_an_unknown_bank_unknown()
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture();
        NativeBattleCuragaNodeAllocation allocated = NativeBattleCuragaNodes.AllocateEmitter881700(fixture.Frame, fixture.Task, 0, 0, 0);
        int node = allocated.ArenaOffset!.Value;
        NativeBattleCuragaFrame frame = allocated.Frame with { Memory = allocated.Frame.Memory with { CuragaExecutable = null, Arena = allocated.Frame.Memory.Arena!.Write(node + 0x64, [1, 0]) } };
        NativeBattleCuragaFrame actual = NativeBattleCuragaNodes.AdvanceEmitterLifetime8817F0(frame, fixture.Task, node);
        Assert.Null(actual.Memory.CuragaExecutable);
        Assert.Equal((ushort)0, new NativeBattleEffectHierarchy(actual.Effect.Pools).Read(fixture.Task).ReadWord(0x34 + 0x14));
        Assert.Equal(new byte[] { 0 }, actual.Memory.Arena!.Read(node + 0x69, 1));
    }
}
