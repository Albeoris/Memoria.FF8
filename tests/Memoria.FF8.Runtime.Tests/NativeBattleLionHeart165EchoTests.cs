using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLionHeart165EchoTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EchoUsesThePhysicalVertexTailAndPublishesTheNativeAreaBeforeClipping(bool reverse)
    {
        CheckEcho(reverse, 0);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    public void TypedSourceCursorKeepsGuestOrLogicalPinAcrossRestoreWithoutChangingPackets(bool reverse, int domain)
    {
        CheckEcho(reverse, domain);
    }

    private static void CheckEcho(bool reverse, int domain)
    {
        const uint temporary = NativeBattleTemporaryMemory.Address;
        const uint vertices = 0x196A018;
        const uint packets = 0x1901018;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(temporary, Enumerable.Repeat((byte)0xA5, 424).ToArray()).Write(vertices, Enumerable.Repeat((byte)0xA5, 24).ToArray());
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(165, 164, new(165, 0x5E9470, 0x5E9450), parameters, 0x23C34B0);
        NativeBattleLionHeart165Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, staging, NativeCrtRandomState.FromColdProcess());
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), reverse ? (ushort)2 : (ushort)1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), reverse ? (ushort)1 : (ushort)2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 0x3040);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(8), 0x38001020);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x01402030);
        NativeBattleRawMesh mesh = new([new(new(-10, -20, 1024), 0), new(new(10, -20, 1024), 0), new(new(0, 0, 1024), 0)], [new(false, face.ToImmutableArray())]);
        NativeBattleActionMatrix identity = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        NativeBattleResourcePointer? source = null;
        NativeBattleModelCpuBank? bank = null;
        NativeBattlePartyCpuLayout layout = domain == 3 ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        if (domain != 0)
        {
            cpu = cpu with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
            int origin = domain == 1 ? 4 : layout.BankLength + 4;
            bank = cpu.Cpu.Banks[0].BindPartyResource(origin, face.ToImmutableArray(), 0, face.Length);
            cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
            source = new NativeBattleResourcePointer(new(0, origin) { Layout = layout, ResourceOnly = domain >= 2 }, null, face.Length).Pin(cpu);
        }
        NativeBattleLionHeart165EchoDraw result = source is null ? NativeBattleLionHeart165Controller.DrawEchoMesh5EF0C0(frame, temporary, vertices, 0x1939100, mesh, [identity], identity, new(identity, 256, 160, 108), new(0, -4096, 0, 0), Enumerable.Repeat(0, 56).ToImmutableArray(), 0x808080, 0x1D8E09C, packets, 341, 256, 0, 0) : NativeBattleLionHeart165Controller.DrawEchoMesh5EF0C0(frame, temporary, vertices, source, mesh, [identity], identity, new(identity, 256, 160, 108), new(0, -4096, 0, 0), Enumerable.Repeat(0, 56).ToImmutableArray(), 0x808080, 0x1D8E09C, packets, 341, 256, 0, 0);
        Assert.Equal(vertices, Dword(result.Frame.Staging, temporary + 4));
        Assert.Equal(reverse ? -1600 : 1600, unchecked((int)Dword(result.Frame.Staging, temporary + 72)));
        Assert.Equal(0xA5A5A5A5u, Dword(result.Frame.Staging, temporary + 76));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.Frame.Staging.Read(vertices + 6, 2));
        Assert.Equal(-10, BinaryPrimitives.ReadInt16LittleEndian(result.Frame.Staging.Read(vertices, 2)));
        Assert.Equal(reverse ? 0 : 32, result.Batch.AllocationBytes);
        Assert.Equal(packets + (reverse ? 0u : 32u), result.PacketCursor);
        Assert.Equal(result.PacketCursor, Dword(result.Frame.Staging, temporary + 68));
        if (!reverse)
        {
            Assert.Equal(255u, Dword(result.Frame.Staging, temporary + 80));
            Assert.Equal(0u, Dword(result.Frame.Staging, temporary + 44));
            Assert.Equal(0x24808080u, Assert.Single(result.Batch.Attempts).Primitive!.Words[1]);
        }
        else
        {
            Assert.Equal(0xA5A5A5A5u, Dword(result.Frame.Staging, temporary + 44));
        }
        if (source is not null)
        {
            NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(result.Frame.Staging));
            NativeBattleResourcePointer cursor = restored.ReadResourcePointer(temporary)!;
            Assert.True(NativeBattleModelCpuBank.SamePublication(bank, cursor.CpuPublication));
            NativeBattleModelCpuResources replaced = cpu with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
            Assert.Equal(face, NativeBattleModelResourceView.Open(replaced, restored, cursor, face.Length).Read(0, face.Length));
            if (domain == 1)
            {
                Assert.Equal(source.Cpu!.PhysicalAddress, Dword(restored, temporary));
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => restored.ReadUInt32(temporary));
                Assert.Empty(bank!.KnownRanges);
            }
        }
    }

    private static uint Dword(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
}
