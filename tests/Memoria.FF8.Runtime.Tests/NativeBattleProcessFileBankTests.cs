using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleProcessFileBankTests
{
    [Fact]
    public void Cold_union_span_and_previous_narrow_import_share_one_addressed_owner()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleChoco97IoBanks banks = NativeBattleProcessFileBanks.Read(memory);
        Assert.Equal(0x19D1018u, banks.BaseAddress);
        Assert.Equal(0x23600, banks.Length);
        Assert.Throws<NotSupportedException>(() => banks.Read(0x19F1018, 1));
        memory = NativeBattleProcessFileBanks.Write(memory, banks.Write(0x19F1018, ImmutableArray.Create<byte>(3, 4)));
        Assert.Equal(new byte[] { 3, 4 }, NativeBattleProcessFileBanks.Read(memory).Read(0x19F1018, 2));
        Assert.Null(memory.Siren95!.Io);
        Assert.Same(memory.Choco97!.PendingIoBanks, NativeBattleProcessFileBanks.Read(memory));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        Assert.Equal(new byte[] { 3, 4 }, NativeBattleProcessFileBanks.Read(restored).Read(0x19F1018, 2));
        NativeBattleChoco97State previous = memory.Choco97 with { PendingIoBanks = new(0x19DB018, 0x13A68, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x13A68)) };
        previous.Validate();
        Assert.Throws<NotSupportedException>(() => previous.PendingIoBanks!.Write(0x19F1018, ImmutableArray.Create<byte>(1)));
    }

    [Fact]
    public void Initialized_IO_bank_is_the_same_authority_and_missing_imports_do_not_hydrate()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleChoco97IoBanks original = NativeBattleProcessFileBanks.Read(memory);
        NativeBattleChoco97IoState io = new(new byte[0x16C].ToImmutableArray(), new byte[0x100].ToImmutableArray(), 0x152B930, original);
        memory = memory with { Choco97 = memory.Choco97! with { Io = io, PendingIoBanks = null } };
        memory = NativeBattleProcessFileBanks.Write(memory, original.Write(0x19DB018, ImmutableArray.Create<byte>(8)));
        Assert.Same(memory.Choco97!.Io!.Banks, NativeBattleProcessFileBanks.Read(memory));
        Assert.Null(memory.Choco97.PendingIoBanks);
        Assert.Equal((byte)8, memory.Choco97.Io.Banks.Read(0x19DB018, 1)[0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleProcessFileBanks.Read(memory with { Choco97 = null }));
    }
}
