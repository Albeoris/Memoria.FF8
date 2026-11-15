using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleCinematicActorAccess;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorWriterTests
{
    private const uint Header = NativeBattleScriptedEffectMemory.Address;
    private const uint Pc = 0x20E0AB8;

    [Fact]
    public void HostedOrderPreservesRepeatsAndMirrorsSixBytesWithoutOverwritingFlags()
    {
        NativeBattleWaterExecutionFrame frame = Frame(0x809A, 7);
        frame = frame with
        {
            Core = frame.Core.Write(Header + 0x4E, [0xB4, 0xFA]) with { PresentationActors = Context().CaptureCurrentContacts([7, 24], 7) },
            Arena = frame.Arena.Write(0x1000, [0x9A, 0x80, 24, 0xFF, 7, 0xFE, 24, 0xFD, 7, 0xFC, 7, 0xFB, 24, 0xFA, 7, 0xF9, 24, 0xF8])
        };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorOrder9A.Execute(frame, new(0x1000, 18, Pc));
        Assert.Equal(new byte[] { 24, 7, 24, 7, 7, 24, 7, 24 }, result.Core.PresentationActors!.CurrentActors);
        Assert.Equal((byte)24, ReadCurrentActor(result, 0));
        Assert.Equal((byte)1, ReadActorOrdinal(result, 24));
        Assert.Equal(new byte[] { 24, 7, 24, 7, 7, 24, 0xB4, 0xFA }, result.Core.Read(Header + 0x48, 8));
        Assert.Equal((byte)8, result.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal(Pc + 18, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));

        result = NativeBattleCinematicActorOrder9A.Execute(result.WriteTemporaryWord(0x14A, 0x9A), new(-1, 0, 0));
        Assert.Equal(0, ReadCurrentCount(result));
        Assert.Equal((byte)24, ReadCurrentActor(result, 0));
        Assert.Equal((byte)0, result.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal((byte)24, result.Core.Read(Header + 0x48, 1)[0]);
        Assert.Equal(Pc + 20, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void HostedRingRetainsTheFullSavedListAndBoundsItsRawMirror(int mode, bool linked)
    {
        ImmutableArray<byte> original = [.. Enumerable.Range(0, 25).Select(row => (byte)(24 - row))];
        NativeBattleCinematicActorContext context = Context().CaptureCurrentContacts(original, 24).WithContactCursor(49);
        NativeBattleWaterExecutionFrame frame = Frame((ushort)(0xCF | mode << 12), 24);
        frame = frame with
        {
            Core = frame.Core.Write(Header + 0x41, [99])
                .Write(Header + 0x48, [8, 9, 10, 11, 12, 13, 0xC0, 0xFA])
                .Write(Header + 0x174, [0xAB, 0xAB, 0xAB, 0xAB, 0xAB, 0xAB, 0xFE, 0xCD, 0xEF]) with { PresentationActors = context }
        };
        int resolves = 0;
        NativeBattleWaterContextServices services = Services(pointer =>
        {
            Assert.Equal(NativeBattleGraphicsModelAddress.Encode(24), pointer);
            return linked ? NativeBattleGraphicsModelAddress.Encode(7) : 0;
        }, pointer =>
        {
            Assert.Equal(NativeBattleGraphicsModelAddress.Encode(24), pointer);
            resolves++;
            return NativeBattleGraphicsModelAddress.Encode(7);
        });

        NativeBattleWaterExecutionFrame result = NativeBattleWaterContextCommands.ExecuteRing(frame, services);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(result));
        NativeBattleCinematicActorContext updated = restored.Core.PresentationActors!;
        byte[] expectedSaved = !linked ? [] : mode == 0 ? [24] : original.ToArray();
        Assert.Equal(expectedSaved, updated.ReadSavedActors());
        Assert.Equal(expectedSaved.Length, ReadSavedCount(restored, signedNative: true));
        Assert.Equal(linked ? new byte[] { 7 } : original.ToArray(), updated.CurrentActors);
        Assert.Equal(linked ? (byte)7 : (byte)24, ReadCurrentActor(restored, 0));
        Assert.Equal((byte)24, updated.ReadActorOrdinal(OriginalOwner(0)));
        Assert.Equal(49, updated.ContactCursor);
        Assert.Equal(linked ? 1 : 0, resolves);
        Assert.Equal(linked ? 7 : 24, WaterSchedulerFixture.ReadSlotWord(restored, 0, 0x1B));
        Assert.Equal(linked ? 1 : 99, restored.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal(new byte[] { 0xC0, 0xFA }, restored.Core.Read(Header + 0x4E, 2));
        Assert.Equal(new byte[] { (byte)expectedSaved.Length, 0xCD, 0xEF }, restored.Core.Read(Header + 0x17A, 3));
        byte[] prefix = [.. expectedSaved.Take(6), .. Enumerable.Repeat((byte)0xAB, Math.Max(0, 6 - expectedSaved.Length))];
        Assert.Equal(prefix, restored.Core.Read(Header + 0x174, 6));
        Assert.Equal(Pc + 2, WaterSchedulerFixture.ReadCoreDword(restored, 0x2797450));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0xFE)]
    public void NativeRingPreservesTheSignedCopyLoopAndPhysicalBytes(byte count)
    {
        NativeBattleWaterExecutionFrame frame = Frame(0x10CF, 2);
        frame = WaterSchedulerFixture.CoreDword(frame, Header + 0x60 + 8, NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode(2)));
        frame = frame with
        {
            Core = frame.Core.Write(Header + 0x41, [count])
                .Write(Header + 0x48, [2, 5, 4])
                .Write(Header + 0x174, [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x18, 0x19])
        };

        NativeBattleWaterExecutionFrame result = NativeBattleWaterContextCommands.ExecuteRing(frame, Services(_ => NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode(1)), _ => NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode(1))));
        Assert.Null(result.Core.PresentationActors);
        Assert.Equal(count == 3 ? new byte[] { 2, 5, 4, 0xDD, 0xEE, 0xFF, count, 0x19 } : new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, count, 0x19 }, result.Core.Read(Header + 0x174, 8));
        Assert.Equal(new byte[] { 1, 5, 4 }, result.Core.Read(Header + 0x48, 3));
        Assert.Equal((byte)1, result.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal((ushort)1, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x1B));
        Assert.Equal(Pc + 2, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Frame(ushort instruction, ushort actor)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x1B, actor);
        return WaterSchedulerFixture.CoreDword(frame, 0x2797450, Pc);
    }

    private static NativeBattleCinematicActorContext Context()
    {
        BattleModelRoster roster = new([.. Enumerable.Range(0, 25).Select(row => new BattleModelOwner($"actor-{row}", row < 3))]);
        ImmutableArray<NativeBattleGraphicsModel> models = [.. Enumerable.Range(0, 25).Select(row => new NativeBattleGraphicsModel(0))];
        return NativeBattleCinematicActorContext.FromGraphics(roster, models);
    }

    private static NativeBattleWaterContextServices Services(Func<uint, uint> linked, Func<uint, uint> resolve)
    {
        return new((_, pointer) => linked(pointer), (_, pointer) => resolve(pointer), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
    }
}
