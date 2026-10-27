using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianWorkBufferTests
{
    [Fact]
    public void Separate571B70BankPreservesItsOwnWritesAcross571870AndHonorsActualSize()
    {
        NativeBattleGuardianWorkBuffer work = NativeBattleGuardianWorkBuffer.FromColdProcess().Write(0x3FFFC, [1, 2, 3, 4]);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GuardianWorkBuffer = work };
        NativeBattleEffectMemory loaded = memory.BeginLoad(115).LoadIntoArena([9, 8, 7, 6]);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, loaded.GuardianWorkBuffer!.Read(0x3FFFC, 4));
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, loaded.Arena!.Read(0, 4));
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(loaded))!;
        restored.Validate();
        Assert.Equal(work.Read(0x3FFFC, 4), restored.GuardianWorkBuffer!.Read(0x3FFFC, 4));
        Assert.Throws<InvalidDataException>(() => work.Write(0x3FFFD, [1, 2, 3, 4]));
        Assert.Throws<InvalidDataException>(() => work.Read(0x40000, 1));
        NativeBattleEffectMemory unknown = memory with { GuardianWorkBuffer = null, EffectIo = null };
        Assert.Null(unknown.BeginLoad(115).GuardianWorkBuffer);
        Assert.Null(unknown.BeginLoad(115).EffectIo);
    }
}
