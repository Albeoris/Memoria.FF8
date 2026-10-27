using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect80ProgrammeTests
{
    [Theory]
    [InlineData(0x1754B2C, 196)]
    [InlineData(0x1757B2C, 216)]
    public void Programme_clears_only_selected_workspace_and_roundtrips_next_frame(uint sourceAddress, int clearBytes)
    {
        byte[] source = new byte[12288];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(20), 1);
        NativeBattleEffect80Resources resources = new(new("texture", []), new("camera", []), new("vibration", []), ImmutableDictionary<uint, NativeBattleEffect80NodeResourceBank>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, [new(sourceAddress, source.ToImmutableArray())], ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, ImmutableDictionary<uint, uint>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, NativeBattleEffect80Profile.TargetAddress);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 384, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].MarkPayloadUnknown(0x94, 384 - 0x94).WriteDword(0x74, sourceAddress).WriteDword(0x78, (uint)clearBytes);
        pool.WriteSlot(0, task);
        NativeBattleOriginalEffectState effect = new(80, 79, new(80, NativeBattleEffect80Profile.Entry, NativeBattleEffect80Profile.Initializer), null, null) { Pools = [pool] };
        NativeBattleEffect80Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleEffect80State.FromColdProcess());
        frame = NativeBattleEffect80Programme.Initialize7016B0(frame, 0x200, sourceAddress, clearBytes, resources);
        task = NativeBattleEffect80Controller.Read(frame, 0x200);
        Assert.Equal(sourceAddress, task.ReadDword(0x94));
        Assert.Equal(0u, task.ReadDword(0x94 + clearBytes - 4));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(0x94 + clearBytes));
        NativeBattleEffect80ProgrammeStep advanced = NativeBattleEffect80Programme.Advance701970(frame, 0x200, NativeBattleEffectArena.Clear().MarkUnknown(0, 92), resources, null!);
        Assert.Equal(0, advanced.Remaining);
        Assert.Equal(1u, NativeBattleEffect80Controller.Read(advanced.Frame, 0x200).ReadDword(0x98));
        NativeBattleEffect80Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect80Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(advanced.Frame)))!);
        NativeBattleEffect80ProgrammeStep next = NativeBattleEffect80Programme.Advance701970(restored, 0x200, NativeBattleEffectArena.Clear().MarkUnknown(0, 92), resources, null!);
        Assert.Equal(0, next.Remaining);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect80Controller.Read(next.Frame, 0x200).ReadWord(0x94 + clearBytes));
    }
}
