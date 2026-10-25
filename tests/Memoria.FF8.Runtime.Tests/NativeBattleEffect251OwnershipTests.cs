using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect251Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect251OwnershipTests
{
    [Fact]
    public void Reassigned_bank_has_one_owner_and_preserves_unwritten_payload_after_JSON()
    {
        NativeBattleEffect251Frame frame = Create();
        frame = InitializeCommonPool(frame, BurstDescriptor);
        (frame, uint old) = TrySpawn(frame, BurstDescriptor, BurstCallback);
        frame = Write(frame, old, Read(frame, old).WriteWord(0x20, 0xBEEF));
        frame = WriteBytes(frame, BurstDescriptor + 4, NativeBattleEffectArena.Clear(), 4);
        frame = WriteBytes(frame, BurstDescriptor, NativeBattleEffectArena.Clear(), 4);
        Assert.Equal((ushort)1, Read(frame, old).Flags);
        Assert.Throws<InvalidDataException>(() => InitializeCommonPool(frame, ActDescriptor));
        frame = RetireCommonPool(frame, BurstDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal((ushort)0xBEEF, WordUnsigned(ReadBytes(frame, old + 0x20, 2)));
        frame = JsonSerializer.Deserialize<NativeBattleEffect251Frame>(JsonSerializer.Serialize(frame))!;
        frame = InitializeCommonPool(frame, ActDescriptor);
        Assert.Single(frame.Effect.Pools);
        Assert.Equal(ActDescriptor, frame.Effect.Pools[0].DescriptorAddress);
        Assert.Equal((ushort)0xBEEF, WordUnsigned(ReadBytes(frame, old + 0x20, 2)));
        Assert.Equal(0u, Dword251(frame.State.ReadCommonQueue(BurstDescriptor), 0));
        (frame, int count) = AdvanceDescriptor(frame, BurstDescriptor, null!, null!);
        Assert.Equal(0, count);
        Assert.Single(frame.Effect.Pools);
        Assert.Throws<NotSupportedException>(() => ReadBytes(frame, CommonAddress + 0x1540, 1).Read(0, 1));
    }

    [Fact]
    public void Mandatory_trail_initializes_only_flags_and_leaves_other_physical_bytes()
    {
        NativeBattleEffect251Frame frame = Create();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x1700 + 0x18, [0xAB, 0xCD]) } };
        frame = InitializeCommonPool(frame, TrailDescriptor);
        frame = SpawnTrail5AF0(frame);
        uint trail = CommonAddress + 0x1700;
        Assert.Equal((ushort)0, Read(frame, trail).ReadWord(12));
        for (int index = 0; index < 7; index++)
        {
            Assert.Equal(0u, Read(frame, trail).ReadDword(0x10 + index * 20));
        }
        Assert.Equal(new byte[] { 0xAB, 0xCD }, Read(frame, trail).ReadBytes(0x18, 2));
        Assert.Throws<NotSupportedException>(() => Read(frame, trail).ReadWord(0x16));
    }

    private static ushort WordUnsigned(NativeBattleEffectArena arena)
    {
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(0, 2));
    }

    private static NativeBattleEffect251Frame Create()
    {
        NativeBattleEffect251State state = NativeBattleEffect251State.FromColdProcess().WriteDword(ArenaGlobal, CommonAddress);
        NativeBattleOriginalEffectState effect = new(251, 250, new(251, Entry, Initializer), new(3, 0, 1, 0, 0, 0), 0) { Pools = [] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffectArena.Length) };
        return new(memory, effect, new(42), state);
    }
}
