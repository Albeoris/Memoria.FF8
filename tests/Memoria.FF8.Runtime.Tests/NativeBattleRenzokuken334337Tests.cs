using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRenzokuken334337Tests
{
    [Fact]
    public void Native334StoredMeshCapturesTrailAndPreservesChildLinks()
    {
        NativeBattleRenzokuken160Frame frame = Frame(334);
        (NativeBattleEffectTaskPool pool, int slot) = Controller(334, 101);
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenSpecialController.Advance334337(frame, pool, slot, Inputs(), Resources(), _ => throw new InvalidOperationException());
        Assert.Equal(0x5892B0u, step.Pool.Slots[1].Callback);
        Assert.Equal(0x589400u, step.Pool.Slots[2].Callback);
        Assert.Equal(1, step.Pool.Slots[0].Next);
        Assert.Equal(2, step.Pool.Slots[1].Next);
        NativeCrtRandomState random = frame.Random.Draw(out ushort angle).Draw(out ushort scale);
        Assert.Equal(random, step.Frame.Random);
        Assert.Equal(angle % 4096, step.Pool.Slots[1].ReadWord(24));
        Assert.Equal((scale % 768 + 2560) / 3, step.Pool.Slots[1].ReadWord(28));
        Assert.Equal(100, step.Pool.Slots[1].ReadWord(16));
        Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(NativeBattleRenzokukenSpecialController.ReadState(step.Frame).ExtraStorage.AsSpan()));
    }

    [Fact]
    public void Native335RelocatesAtThirteenAndRestoresInitialCoordinatesAtDurationMinusFive()
    {
        NativeBattleRenzokuken160Frame frame = Frame(335);
        List<NativeBattleRenzokuken160Operation> operations = [];
        NativeBattleRenzokuken160Inputs inputs = Inputs() with
        {
            Execute = (actual, operation) =>
            {
                operations.Add(operation);
                return actual;
            },
            ReadModel = _ => new(0, 0) { Transform = new(new(400, 0, 600), new(100, 0, 300), 0, 0, 4096) }
        };
        (NativeBattleEffectTaskPool pool, int slot) = Controller(335, 13);
        NativeBattleRenzokukenSpecialController.Advance334337(frame, pool, slot, inputs, Resources(), target =>
        {
            Assert.Equal(3, target);
            return new(1200, -50, -800, 0);
        });
        Assert.Contains(new NativeBattleRenzokukenSpecialActorPosition(1200, 3050), operations);
        operations.Clear();
        (pool, slot) = Controller(335, 195);
        NativeBattleRenzokukenSpecialController.Advance334337(frame, pool, slot, inputs, Resources(), _ => throw new InvalidOperationException());
        Assert.Contains(new NativeBattleRenzokukenSpecialActorPosition(400, 600), operations);
    }

    [Fact]
    public void Native336FinalBurstReadsCurrentTargetBoneAndSchedulesCameraOwner()
    {
        NativeBattleRenzokuken160Frame frame = Frame(336);
        (NativeBattleEffectTaskPool pool, int slot) = Controller(336, 120);
        NativeBattleRenzokuken160Inputs inputs = Inputs() with
        {
            ReadBone = (actor, bone, fraction) =>
            {
                Assert.Equal(3, actor);
                Assert.Equal(241, bone);
                Assert.Equal(0, fraction);
                return new(700, -900, 1100);
            }
        };
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenSpecialController.Advance334337(frame, pool, slot, inputs, Resources(), _ => throw new InvalidOperationException());
        Assert.Equal(new uint[] { 0x583160, 0x584F20, 0x585220, 0x585360 }, step.Pool.Slots.Take(4).Select(task => task.Callback));
        Assert.Equal(700, step.Pool.Slots[1].ReadWord(16));
        Assert.Equal(unchecked((ushort)-900), step.Pool.Slots[2].ReadWord(18));
        Assert.Equal(1100, step.Pool.Slots[2].ReadWord(20));
        NativeCrtRandomState random = frame.Random.Draw(out ushort scale);
        Assert.Equal(random, step.Frame.Random);
        Assert.Equal((scale % 2560 + 7680) / 3, step.Pool.Slots[2].ReadWord(28));
    }

    [Fact]
    public void Native337RetainsStoredDirectionAndOriginSeparatelyFromLaterTrailUpdates()
    {
        NativeBattleRenzokuken160Frame frame = Frame(337);
        (NativeBattleEffectTaskPool pool, int slot) = Controller(337, 84);
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenSpecialController.Advance334337(frame, pool, slot, Inputs(), Resources(), _ => throw new InvalidOperationException());
        Assert.Equal(0x5826C0u, step.Pool.Slots[1].Callback);
        var bytes = NativeBattleRenzokukenSpecialController.ReadState(step.Frame).ExtraStorage.AsSpan();
        Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(bytes));
        Assert.Equal(100, BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(8)));
        Assert.Equal(-200, BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(10)));
        Assert.Equal(300, BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(12)));
        Assert.Equal(4096, BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(24)));
        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(26)));
        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(28)));
    }

    [Theory]
    [InlineData(334, 101)]
    [InlineData(335, 59)]
    [InlineData(336, 120)]
    [InlineData(337, 84)]
    public void PauseGatesUniqueControllersBeforeRandomAndBoneQueries(int effect, int counter)
    {
        NativeBattleRenzokuken160Frame frame = Frame((ushort)effect);
        (NativeBattleEffectTaskPool pool, int slot) = Controller((ushort)effect, counter);
        NativeBattleRenzokuken160Inputs inputs = new(0x201, [], _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenSpecialController.Advance334337(frame, pool, slot, inputs, Resources(), _ => throw new InvalidOperationException());
        Assert.Same(frame, step.Frame);
        Assert.Same(pool, step.Pool);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    public void EndpointCopiesMarkOnlySpawnedRowsUnknownAndRetainMaskThroughSnapshot(int firstRow)
    {
        NativeBattleRenzokuken160Frame frame = Frame(334);
        NativeBattleRenzokukenSpecialState state = NativeBattleRenzokukenSpecialController.ReadState(frame);
        byte[] storage = state.ExtraStorage.ToArray();
        for (int row = 0; row < firstRow; row++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(storage.AsSpan(row * 32), -1);
        }
        frame = NativeBattleRenzokukenSpecialController.Store(frame, state with { ExtraStorage = storage.ToImmutableArray() });
        frame = NativeBattleRenzokukenSpecialController.SetExtraPaddingUnknown(frame, 99, true);
        (NativeBattleEffectTaskPool pool, int slot) = Controller(334, 0);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot] with { Callback = 0x589400 }) };
        byte[] spriteBytes = new byte[18];
        BinaryPrimitives.WriteUInt16LittleEndian(spriteBytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(spriteBytes.AsSpan(10), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(spriteBytes.AsSpan(12), 14);
        NativeBattleSearchSpriteResource sprite = new(0xD70000, spriteBytes.ToImmutableArray());
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1024, 4096);
        NativeBattleRenzokuken160Inputs inputs = Inputs() with { ReadBone = (_, _, fraction) => new(0, 0, fraction) };
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenSpecialController.AdvanceTargetSprites(frame, pool, slot, inputs, sprite, view, new(view, 256, 160, 108), 0, 240, 32, 3, 90, 30);
        NativeBattleRenzokukenSpecialState actual = NativeBattleRenzokukenSpecialController.ReadState(step.Frame);
        Assert.Equal(firstRow == 0 ? 7UL : 0UL, actual.ExtraStorageUnknownPaddingLow);
        Assert.Equal((1UL << 35) | (firstRow == 64 ? 7UL : 0UL), actual.ExtraStorageUnknownPaddingHigh);
        for (int row = 0; row < 100; row++)
        {
            Assert.Equal(row == 99 || row >= firstRow && row < firstRow + 3, NativeBattleRenzokukenSpecialController.ReadExtraPaddingUnknown(step.Frame, row));
        }
        NativeBattleRenzokukenSpecialState restored = JsonSerializer.Deserialize<NativeBattleRenzokukenSpecialState>(JsonSerializer.Serialize(actual))!;
        Assert.Equal(actual.ExtraStorageUnknownPaddingLow, restored.ExtraStorageUnknownPaddingLow);
        Assert.Equal(actual.ExtraStorageUnknownPaddingHigh, restored.ExtraStorageUnknownPaddingHigh);
        Assert.Equal(actual.ExtraStorage.ToArray(), restored.ExtraStorage.ToArray());
    }

    private static NativeBattleRenzokuken160Resources Resources() => new(new("animation", []), null!, [], [], [], [], 0, 0);
    private static NativeBattleRenzokuken160Inputs Inputs() => new(0, [], _ => new(0, 0), (frame, _) => frame);

    private static NativeBattleRenzokuken160Frame Frame(ushort effect)
    {
        NativeBattleRenzokukenSpecialProfile profile = NativeBattleRenzokukenSpecialProfiles.ForEffect(effect);
        NativeBattleRenzokuken160State core = NativeBattleRenzokuken160State.FromColdProcess().Enter(0, 0, 3, false) with { Duration = 200, Origin = new(100, -200, 300), Direction = new(4096, 0, 0) };
        NativeBattleRenzokukenSpecialState state = new(core, Resources()) { ExtraStorage = new byte[3200].ToImmutableArray() };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { RenzokukenSpecials = ImmutableDictionary<ushort, NativeBattleRenzokukenSpecialState>.Empty.Add(effect, state) };
        return new(memory, new(effect, (ushort)(effect - 1), new(effect, profile.Entry, profile.Initializer), null, null), NativeCrtRandomState.FromColdProcess());
    }

    private static (NativeBattleEffectTaskPool Pool, int Slot) Controller(ushort effect, int counter)
    {
        NativeBattleRenzokukenSpecialProfile profile = NativeBattleRenzokukenSpecialProfiles.ForEffect(effect);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, profile.ChildBank, 36, 100);
        (pool, int slot) = pool.Append(profile.ControllerCallback);
        return (pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, (ushort)counter)) }, slot);
    }
}
