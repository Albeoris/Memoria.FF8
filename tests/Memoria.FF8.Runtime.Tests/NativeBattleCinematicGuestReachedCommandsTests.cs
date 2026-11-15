using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicGuestReachedCommandsTests
{
    [Fact]
    public void NextContactWithZeroContactsDoesNotReadAnUnusedContact()
    {
        NativeBattleWaterExecutionFrame frame = ContactFixture(0, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x5008, 4) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextContact12E.Execute(frame, Services(0));
        Assert.Equal(0x1002u, Pc(result));
        Assert.Equal(0, result.Core.PresentationActors!.ContactsProcessed);
    }

    [Theory]
    [InlineData(128, 127)]
    [InlineData(255, 254)]
    [InlineData(256, 255)]
    public void NextContactUsesTheFullCountAndProjectsTheCounterOnlyWhileItFits(int count, byte processed)
    {
        NativeBattleWaterExecutionFrame frame = ContactFixture(unchecked((byte)count), processed);
        List<BattleContactOccurrence> applied = [];
        NativeBattleWaterContextServices services = Services(count) with { ApplyContact506690 = (current, occurrence) =>
        {
            applied.Add(occurrence);
            return current;
        } };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextContact12E.Execute(frame, services);
        Assert.Equal(new BattleContactOccurrence(processed), Assert.Single(applied));
        Assert.Equal(count, result.Core.PresentationActors!.ContactsProcessed);
        Assert.Equal(0x1002u, Pc(result));
        if (count <= byte.MaxValue)
        {
            Assert.Equal((byte)count, result.Core.Read(NativeBattleScriptedEffectMemory.Address + 0x3A, 1)[0]);
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => result.Core.Read(NativeBattleScriptedEffectMemory.Address + 0x3A, 1));
        }
    }

    [Fact]
    public void NextContactUsesCapturedOccurrenceThenRereadsTheCallbackCounterBeforePcAdvance()
    {
        NativeBattleWaterExecutionFrame frame = ContactFixture(3, 2);
        uint originalOwner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        NativeBattleWaterContextServices services = Services(3) with
        {
            ApplyContact506690 = (current, pointer) =>
            {
                Assert.Equal(new BattleContactOccurrence(2), pointer);
                Assert.Equal(0x1000u, Pc(current));
                current = WaterSchedulerFixture.CoreDword(current, 0x2797450, 0xFFFFFFFE);
                return current with { Core = current.Core with { PresentationActors = current.Core.PresentationActors!.WithContactsProcessed(8) } };
            }
        };
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextContact12E.Execute(frame, services);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicNextContact12E.Execute(restored, services)));
        Assert.Equal(0u, Pc(result));
        Assert.Equal((byte)9, result.Core.Read(originalOwner + 0x3A, 1)[0]);
        Assert.Equal(9, result.Core.PresentationActors!.ContactsProcessed);
    }

    [Fact]
    public void LinkedPositionCopiesOnlySignedCoordinatesToTheSelectedExistingObject()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        uint owner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, owner + 0x60 + 4 * 5, 0x1D975CC);
        frame = frame with { Arena = frame.Arena.Write(0x201B, [5]).Write(0x2094, [0, 0x80, 0xFF, 0x7F, 0xFE, 0xFF]) };
        frame = NativeBattleCinematicTypedTestFixture.BindActor(frame, OriginalOwner(5));
        NativeBattleLinkedModelTransform original = new(new(11, 22, 33), new(44, 55, 66));
        NativeBattleLinkedModelTransform? written = null;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicLinkedPosition70.Draw(frame, pointer =>
        {
            Assert.Equal(OriginalOwner(5), pointer);
            return original;
        }, (pointer, linked) =>
        {
            Assert.Equal(OriginalOwner(5), pointer);
            written = linked;
        });
        Assert.Same(frame, result);
        Assert.Equal(original.Rotation, written!.Rotation);
        Assert.Equal(new NativeBattleVector(-32768, 32767, -2), written.Position);
    }

    private static NativeBattleWaterExecutionFrame ContactFixture(byte count, byte index)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        uint manager = WaterSchedulerFixture.ReadCoreDword(frame, 0x27973EC);
        uint owner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, manager + 0xCC, Address(0x5000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = NativeBattleCinematicTypedTestFixture.WithContext(frame);
        return frame with { Core = frame.Core.Write(owner + 0x3A, [index]) with { PresentationActors = frame.Core.PresentationActors!.WithContactsProcessed(index) }, Arena = frame.Arena.Write(0x5008, BitConverter.GetBytes(Address(0x6000))).Write(0x5010, [count]) };
    }

    private static NativeBattleWaterContextServices Services(int count)
    {
        NativeBattleActionPacketsState packets = OriginalPackets();
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(OriginalOwner(0), 0, 0, 0, 0, 0, null));
        NativeBattleContactPacket contact = new(new(OriginalOwner(3), 0, 0, 0, 0, 0, 0), null);
        packets = packets.AppendContacts(Enumerable.Repeat(contact, count));
        (packets, _) = packets.Publish(count);
        return NativeBattleCinematicTypedTestFixture.Services() with { ReadCurrentHeader = _ => header, ReadPackets = () => packets };
    }
    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
}
