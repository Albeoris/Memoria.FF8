using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEntryWaveLoaderTests
{
    [Fact]
    public void PhaseZeroRequestsFileA5WithoutReadingPriorCompletionIoOrStaging()
    {
        NativeBattleEntryWaveStep step = NativeBattleEntryWaveLoader.Advance500AC0(0, 0xA7, null, null);
        Assert.Equal(new NativeBattleEntryWaveStep(1, 0xA7, false, true, null, null), step);
        Assert.Equal((ushort)0xA5, NativeBattleEntryWaveLoader.FileId);
        Assert.Equal(0x199B918u, NativeBattleEntryWaveLoader.Destination);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void PendingSignedIoDoesNotReadCurrentStagingOrComplete(int result)
    {
        Assert.Equal(new NativeBattleEntryWaveStep(1, 0xFF, false, false, null, null), NativeBattleEntryWaveLoader.Advance500AC0(1, 0xFF, result, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(171164)]
    [InlineData(int.MaxValue)]
    public void ReadyReadsCurrentDirectoryOperandsAndPcStubCompletesSameCallback(int result)
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(NativeBattleEntryWaveLoader.Destination + 4, Dword(0x120)).Write(NativeBattleEntryWaveLoader.Destination + 12, Dword(0xFFFFF700));
        NativeBattleEntryWaveStep step = NativeBattleEntryWaveLoader.Advance500AC0(1, 0, result, staging);
        Assert.Equal((byte)2, step.Phase);
        Assert.Equal((byte)1, step.Completion);
        Assert.True(step.Completed);
        Assert.False(step.RequestFile);
        Assert.Equal(NativeBattleEntryWaveLoader.Destination + 0x120, step.TimAddress);
        Assert.Equal(NativeBattleFileStaging.Address, step.SoundSourceAddress);
        //5011A0 reads neither the sound source nor a DAT count/length header.
        Assert.Throws<NotSupportedException>(() => staging.Read(step.SoundSourceAddress!.Value, 1));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(128, true)]
    [InlineData(255, true)]
    public void PhaseTwoOnlyReadsItsCompletionByte(byte completion, bool completed)
    {
        Assert.Equal(new NativeBattleEntryWaveStep(2, completion, completed, false, null, null), NativeBattleEntryWaveLoader.Advance500AC0(2, completion, null, null));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(255)]
    public void OtherBytePhasesDoNotWrapOrRelease(byte phase)
    {
        Assert.Equal(new NativeBattleEntryWaveStep(phase, 1, false, false, null, null), NativeBattleEntryWaveLoader.Advance500AC0(phase, 1, null, null));
    }

    [Fact]
    public void ReachedUnknownIoOrDirectoryWordsRemainStrict()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleEntryWaveLoader.Advance500AC0(1, 0, null, null));
        Assert.Throws<NotSupportedException>(() => NativeBattleEntryWaveLoader.Advance500AC0(1, 0, 0, null));
        Assert.Throws<NotSupportedException>(() => NativeBattleEntryWaveLoader.Advance500AC0(1, 0, 0, NativeBattleFileStaging.Unknown().Write(NativeBattleEntryWaveLoader.Destination + 4, Dword(20))));
    }

    [Fact]
    public void QueueRetainsCurrentStagingPointerAcrossJsonAndReadsMutationAtDrain()
    {
        uint address = NativeBattleEntryWaveLoader.Destination + 20;
        byte[] tim = Tim(0x1234);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, tim);
        var queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueStagingTim505E30(staging, address);
        Assert.Equal(address + (uint)tim.Length, queued.NextAddress);
        NativeBattleEffectTextureState restored = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(queued.State))!;
        Assert.Null(restored.Slots[0]!.Source);
        Assert.Equal(address, restored.Slots[0]!.StagingAddress);
        Assert.Throws<NotSupportedException>(() => restored.Drain505D20());
        Assert.Throws<NotSupportedException>(() => restored.Drain505D20(stagingSource: NativeBattleFileStaging.Unknown()));
        staging = staging.Write(address + 34, [0xEF, 0xBE]);
        NativeBattleEffectTextureDrain drain = restored.Drain505D20(stagingSource: staging);
        Assert.Equal(2, drain.Uploads.Length);
        Assert.Equal(new byte[] { 0xEF, 0xBE }, drain.Uploads[1].Bytes);
        Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(drain.State.ReadVram().AsSpan(2 * (256 * 1024 + 64))));
        Assert.Equal(0, drain.State.Count);
        Assert.Equal(address, drain.State.Slots[0]!.StagingAddress);
    }

    [Fact]
    public void EveryReusedSourceUnionReplacesTheOldStagingPointer()
    {
        NativeBattleEffectHeapAllocation tim = new("fixture", Tim(0x4321).ToImmutableArray());
        NativeBattleEffectArena arena = new NativeBattleEffectArena([]).Write(128, tim.Data.AsSpan());
        uint address = NativeBattleEntryWaveLoader.Destination + 20;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, tim.Data.AsSpan());
        NativeBattleEffectTextureState drained = NativeBattleEffectTextureState.FromColdProcess().EnqueueStagingTim505E30(staging, address).State.Drain505D20(stagingSource: staging).State;
        NativeBattleEffectTextureState[] replacements =
        [
            drained.EnqueueTim505E30(tim).State,
            drained.EnqueueArenaTim505E30(arena, 128).State,
            drained.EnqueueRectangle505DF0(new(64, 256, 1, 1), new("pixel", [0x21, 0x43])),
            drained.EnqueueCopy505EB0(64, 256, 1, 1, 64, 256)
        ];
        foreach (NativeBattleEffectTextureState replacement in replacements)
        {
            Assert.Null(replacement.Slots[0]!.StagingAddress);
            Assert.DoesNotContain("StagingAddress", JsonSerializer.Serialize(replacement));
            Assert.Equal(0x4321, BinaryPrimitives.ReadUInt16LittleEndian(replacement.Drain505D20(arena).State.ReadVram().AsSpan(2 * (256 * 1024 + 64))));
        }
    }

    [Fact]
    public void StagingUploadKeepsOriginal31SuccessfulSlotBoundary()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(NativeBattleEntryWaveLoader.Destination, Tim(0));
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        for (int index = 0; index < 31; index++)
        {
            state = state.EnqueueStagingTim505E30(staging, NativeBattleEntryWaveLoader.Destination).State;
        }
        Assert.Throws<NotSupportedException>(() => state.EnqueueStagingTim505E30(staging, NativeBattleEntryWaveLoader.Destination));
    }

    [Fact]
    public void QueueRequiresActualMutableTimBlocksAndRejectsAmbiguousPointerUnion()
    {
        uint address = NativeBattleEntryWaveLoader.Destination;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, Tim(0));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectTextureState.FromColdProcess().EnqueueStagingTim505E30(NativeBattleFileStaging.Unknown().Write(address + 8, Dword(14)), address));
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueStagingTim505E30(staging, address).State;
        NativeBattleEffectTextureState ambiguous = queued with { Slots = queued.Slots.SetItem(0, queued.Slots[0]! with { ArenaOffset = 0 }) };
        Assert.Throws<InvalidDataException>(() => ambiguous.Validate());
    }

    private static byte[] Dword(uint value)
    {
        byte[] result = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(result, value);
        return result;
    }

    private static byte[] Tim(ushort image)
    {
        byte[] bytes = new byte[36];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(22), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(30), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(34), image);
        return bytes;
    }
}
