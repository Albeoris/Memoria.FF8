using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlExecutableMemoryTests
{
    [Fact]
    public void StageToggleChangesOnlyTheFourActualVisibilityBitsAndCallbackGateKeepsSignedThreshold()
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess();
        stage = stage with { Groups = stage.Groups.Select(group => group with { Flags = 0xA7, ColorDepth = -12, ColorFlags = 0x123456 }).ToImmutableArray() };
        NativeBattleStageState hidden = NativeBattleQuezacotlWorkspace.SetStageVisibility6C6500(stage, 0);
        Assert.All(hidden.Groups, group =>
        {
            Assert.Equal((byte)0xA5, group.Flags);
            Assert.Equal((short)-12, group.ColorDepth);
            Assert.Equal(0x123456u, group.ColorFlags);
        });
        Assert.All(NativeBattleQuezacotlWorkspace.SetStageVisibility6C6500(hidden, 3).Groups, group => Assert.Equal((byte)0xA7, group.Flags));
        byte[] node = new byte[44];
        Assert.True(NativeBattleQuezacotlWorkspace.SkipsGeometry6C6060(new(0, 0, node.ToImmutableArray())));
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(24), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(36), 4096);
        Assert.True(NativeBattleQuezacotlWorkspace.SkipsGeometry6C6060(new(0, 0, node.ToImmutableArray())));
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(36), -1);
        Assert.False(NativeBattleQuezacotlWorkspace.SkipsGeometry6C6060(new(0, 0, node.ToImmutableArray())));
    }

    [Fact]
    public void CurrentExecutableWritesPersistAcrossArenaResetAndRestoreOnlyTheActualBackupRanges()
    {
        byte[] initial = Enumerable.Repeat((byte)0x5A, NativeBattleQuezacotlExecutableMemory.Length).ToArray();
        NativeBattleQuezacotlExecutableMemory executable = new(initial.ToImmutableArray());
        NativeBattleGuardianWorkBuffer backup = NativeBattleGuardianWorkBuffer.FromColdProcess().Write(0, executable.Data.AsSpan()).Write(NativeBattleGuardianWorkBuffer.Length - 1, [0xA5]);
        executable = executable.Write(NativeBattleQuezacotlExecutableMemory.Address + NativeBattleQuezacotlExecutableMemory.PrimaryLength, [1, 2, 3, 4]);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { QuezacotlExecutableMemory = executable, GuardianWorkBuffer = backup };
        NativeBattleEffectMemory reset = memory.BeginLoad(116);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, reset.QuezacotlExecutableMemory!.Data.AsSpan().Slice(NativeBattleQuezacotlExecutableMemory.PrimaryLength, 4).ToArray());
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(reset))!;
        Assert.Equal(initial, restored.QuezacotlExecutableMemory!.Restore6C5196(restored.GuardianWorkBuffer!).Data.ToArray());
        Assert.Equal((byte)0xA5, restored.GuardianWorkBuffer!.Read(NativeBattleGuardianWorkBuffer.Length - 1, 1)[0]);
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().QuezacotlExecutableMemory);
        Assert.Null((memory with { QuezacotlExecutableMemory = null }).BeginLoad(116).QuezacotlExecutableMemory);
        Assert.Throws<InvalidDataException>(() => executable.Write(NativeBattleQuezacotlExecutableMemory.Address - 1, [0]));
        Assert.Throws<InvalidDataException>(() => executable.Write(NativeBattleQuezacotlExecutableMemory.Address + NativeBattleQuezacotlExecutableMemory.Length, [0]));
    }
}
