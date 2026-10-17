using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRestorationExecutableTests
{
    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    [InlineData((ushort)57)]
    [InlineData((ushort)67)]
    public void Sparse_directories_and_unconditional_channels_fix_once_in_their_own_retained_bank(ushort effect)
    {
        uint address = NativeBattleRestorationProfile.ForEffect(effect).ExecutableAddress;
        byte[] bytes = new byte[NativeBattleRestorationExecutableState.Length(effect)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x44), 0x2000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x10C8), 24);
        NativeBattleRestorationExecutableSource source = new(effect, bytes.ToImmutableArray());
        NativeBattleRestorationExecutableState state = NativeBattleRestorationExecutableState.FromColdProcess(effect).Initialize(source);
        Assert.Equal(address + 0x1000, Read(4));
        Assert.Equal(address + 0x2000, Read(0x44));
        Assert.Equal(0u, Read(8));
        Assert.Equal(address + 24, Read(0x10C8));
        Assert.Equal(address, Read(0x10CC));
        Assert.Equal(1u, state.InitializationFlag);
        Assert.Same(state, state.Initialize(source));
        NativeBattleRestorationExecutableState restored = JsonSerializer.Deserialize<NativeBattleRestorationExecutableState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(state.Data!.Value.ToArray(), restored.Initialize(source).Data!.Value.ToArray());
        Assert.Throws<InvalidDataException>(() => state.Read(address - 1, 4));
        Assert.Throws<NotSupportedException>(() => new NativeBattleRestorationExecutableState(effect, 1, null).Initialize(source));
        Assert.Throws<InvalidDataException>(() => state.Initialize(source with { Effect = effect == 24 ? (ushort)27 : (ushort)24 }));

        uint Read(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(state.Read(address + (uint)offset, 4));
    }

    [Fact]
    public void Missing_old_memory_properties_remain_unknown_and_owners_cannot_be_exchanged()
    {
        NativeBattleEffectMemory cold = NativeBattleEffectMemory.FromColdProcess();
        Assert.Equal(NativeBattleRestorationExecutableState.FromColdProcess(24), cold.EsunaExecutable);
        Assert.Equal(NativeBattleRestorationExecutableState.FromColdProcess(27), cold.FullLifeExecutable);
        var json = JsonSerializer.SerializeToNode(cold)!.AsObject();
        Assert.True(json.Remove(nameof(NativeBattleEffectMemory.EsunaExecutable)));
        Assert.True(json.Remove(nameof(NativeBattleEffectMemory.FullLifeExecutable)));
        NativeBattleEffectMemory old = JsonSerializer.Deserialize<NativeBattleEffectMemory>(json)!;
        Assert.Null(old.BeginLoad(23).EsunaExecutable);
        Assert.Null(old.BeginLoad(26).FullLifeExecutable);
        Assert.Throws<InvalidDataException>(() => (cold with { EsunaExecutable = cold.FullLifeExecutable }).Validate());
    }
}
