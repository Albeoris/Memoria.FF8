using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlLaterWindowTests
{
    [Fact]
    public void AddressedBanksRejectUnknownPointersAndDoNotAliasFivePairsIntoTen()
    {
        byte[] pair = new byte[80];
        for (int offset = 2; offset < pair.Length; offset += 4)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pair.AsSpan(offset), 1);
        }
        NativeBattleQuezacotlRibbonResources resources = new(pair.ToImmutableArray(), new byte[24].ToImmutableArray(), new ushort[256].ToImmutableArray(), new byte[16384].ToImmutableArray());
        Assert.Throws<NotSupportedException>(() => resources.ResolveBank(0x12AF800, 0x12AF828));
        NativeBattleQuezacotlRibbonBank bank = new(0x12AF800, 0x12AF828, pair.AsSpan(0, 40).ToArray().ToImmutableArray(), new byte[24].ToImmutableArray());
        resources = resources with { SecondaryBank = bank };
        resources.Validate();
        for (uint index = 0; index < 5; index++)
        {
            Assert.Same(bank, resources.ResolveBank(0x12AF800 + index * 8, 0x12AF828));
        }
        Assert.Throws<InvalidDataException>(() => resources.ResolveBank(0x12AF828, 0x12AF828));
        Assert.Throws<InvalidDataException>(() => resources.ResolveBank(0x12AF801, 0x12AF828));
        Assert.Throws<InvalidDataException>(() => resources.ResolveBank(0x12AF800, 0x12AE840));
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleQuezacotlRibbonResources>(JsonSerializer.Serialize(resources))));
    }

    [Theory]
    [InlineData(-128, 0)]
    [InlineData(-128, 32768)]
    [InlineData(-128, -32767)]
    public void LaterContextUsesYTimesXThenPriorViewAndCurrentTarget(int pitch, int yaw)
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 0, 4096, 0, -4096, 0], 100, 200, 300, 4096);
        NativeBattleVector target = new(-90, 40, 70);
        NativeBattleActionMatrix actual = NativeBattleGfMatrices.LaterWindowContext6C64C0(view, pitch, yaw, target);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Euler701310(new(0, unchecked((short)yaw), 0)), NativeBattleGfMatrices.Euler701310(new(unchecked((short)pitch), 0, 0)));
        Assert.Equal(NativeBattleGfMatrices.Multiply56C090(view, local).Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal((10, 270, 260), (actual.X, actual.Y, actual.Z));
    }

    [Fact]
    public void FourthWorkspaceReadsCurrentSubregionAndWritesOnlyItsActualPrefix()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        byte[] current = new byte[NativeBattleQuezacotlExecutableMemory.Length];
        int offset = checked((int)(0x12A1164 - NativeBattleQuezacotlExecutableMemory.Address));
        current[offset] = 0xA7;
        memory = memory with { QuezacotlExecutableMemory = new(current.ToImmutableArray()), Arena = memory.Arena!.Write(0xD4 + 0x1A0, [0xCC, 0xDD]) };
        Assert.Equal((byte)0xA7, NativeBattleQuezacotlWorkspace.Source(memory, 0x12A1164).Data[0]);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (pool, _) = pool.Append(0x6C3940);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [pool] };
        NativeBattleQuezacotlWorkspaceWrite result = NativeBattleQuezacotlWorkspace.Write(memory, effect, new(Enumerable.Repeat((byte)0x5A, 0x1A0).ToImmutableArray()));
        Assert.Equal(new byte[] { 0xCC, 0xDD }, result.Memory.Arena!.Read(0xD4 + 0x1A0, 2));
        Assert.Equal(Enumerable.Repeat((byte)0x5A, 0x1A0), result.Memory.Arena.Read(0xD4, 0x1A0));
    }
}
