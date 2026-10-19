using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicGuestReachedCommandsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0x80)]
    [InlineData(0xFF)]
    public void NextContactUsesSignedCountWithoutReadingTheUnusedPointer(byte count)
    {
        NativeBattleWaterExecutionFrame frame = ContactFixture(count, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x5008, 4) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextContact12E.Execute(frame, Services());
        Assert.Equal(0x1002u, Pc(result));
    }

    [Fact]
    public void NextContactUsesCapturedStrideThenRereadsCallbackOwnerAndIndexBeforePcAdvance()
    {
        NativeBattleWaterExecutionFrame frame = ContactFixture(3, 2);
        uint originalOwner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        uint changedOwner = originalOwner + 0x200;
        NativeBattleWaterContextServices services = Services() with
        {
            ApplyContact506690 = (current, pointer) =>
            {
                Assert.Equal(Address(0x6030), pointer);
                Assert.Equal(0x1000u, Pc(current));
                current = WaterSchedulerFixture.CoreDword(current, 0x279744C, changedOwner);
                current = WaterSchedulerFixture.CoreDword(current, 0x2797450, 0xFFFFFFFE);
                return current with { Core = current.Core.Write(changedOwner + 0x3A, [0xFF]) };
            }
        };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextContact12E.Execute(frame, services);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleCinematicNextContact12E.Execute(restored, services)));
        Assert.Equal(0u, Pc(result));
        Assert.Equal((byte)0, result.Core.Read(changedOwner + 0x3A, 1)[0]);
        Assert.Equal((byte)2, result.Core.Read(originalOwner + 0x3A, 1)[0]);
    }

    [Fact]
    public void LinkedPositionCopiesOnlySignedCoordinatesToTheSelectedExistingObject()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        uint owner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, owner + 0x60 + 4 * 5, 0x1D975CC);
        frame = frame with { Arena = frame.Arena.Write(0x201B, [5]).Write(0x2094, [0, 0x80, 0xFF, 0x7F, 0xFE, 0xFF]) };
        NativeBattleLinkedModelTransform original = new(new(11, 22, 33), new(44, 55, 66));
        NativeBattleLinkedModelTransform? written = null;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicLinkedPosition70.Draw(frame, pointer =>
        {
            Assert.Equal(0x1D975CCu, pointer);
            return original;
        }, (pointer, linked) =>
        {
            Assert.Equal(0x1D975CCu, pointer);
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
        return frame with { Core = frame.Core.Write(owner + 0x3A, [index]), Arena = frame.Arena.Write(0x5008, BitConverter.GetBytes(Address(0x6000))).Write(0x5010, [count]) };
    }

    private static NativeBattleWaterContextServices Services() => new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
}
