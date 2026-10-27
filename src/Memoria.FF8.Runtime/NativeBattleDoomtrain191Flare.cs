using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191MeshDraw(uint ResourceAddress, NativeBattleActionMatrix Matrix, uint Flags, byte DepthShift, int? Fade = null)
{
    public int? SelectedVerticesOffset { get; init; }
}
public sealed record NativeBattleDoomtrain191MeshServices(Func<uint> ReadFlags, Func<NativeBattleDoomtrain191Frame, NativeBattleDoomtrain191MeshDraw, NativeBattleDoomtrain191Frame> Draw);

public static class NativeBattleDoomtrain191Flare
{
    public static NativeBattleDoomtrain191Frame Spawn6437F0(NativeBattleDoomtrain191Frame frame)
    {
        NativeBattleDoomtrain191ModelMemory memory = NativeBattleDoomtrain191Model.Require(frame);
        NativeBattleActionMatrix anchor = memory.Anchor.ReadMatrix();
        NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(anchor, new(0, -1150, -9750));
        NativeBattleEffectTaskPool pool = Pool(frame);
        (pool, int index) = pool.Append(0x643880);
        NativeBattleModulePoolTask task = pool.Slots[index].WriteWord(0xC, 0).WriteWord(0x10, unchecked((ushort)(point.X + anchor.X))).WriteWord(0x12, unchecked((ushort)(point.Y + anchor.Y))).WriteWord(0x14, unchecked((ushort)(point.Z + anchor.Z))).WriteWord(0x1C, 0xB00);
        NativeCrtRandomState random = frame.Random.Draw(out ushort angle);
        frame = NativeBattleDoomtrain191Model.Put(frame, memory with { OrbitAngle = angle % 4096, OrbitStep = 0x110 });
        return PutPool(frame with { Random = random }, pool.WriteSlot(index, task));
    }

    public static NativeBattleDoomtrain191LeafStep Advance643880(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleDoomtrain191MeshServices services)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x643880)
        {
            throw new InvalidDataException("Native Doomtrain flare requires its actual task.");
        }
        int age = unchecked((short)task.ReadWord(0xC));
        int angle = unchecked(1760 * Math.Min(age, 19)) / 19;
        int size = unchecked((short)task.ReadWord(0x1C));
        int scale = unchecked(NativeBattleCameraMath.Sine(angle) * size) >> 12;
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), scale, scale, size) with { X = unchecked((short)task.ReadWord(0x10)), Y = unchecked((short)task.ReadWord(0x12)), Z = unchecked((short)task.ReadWord(0x14)) };
        NativeBattleDoomtrain191State state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain flare requires its current view.");
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(state.BaseView.ReadMatrix(), world);
        frame = services.Draw(frame, new(0xE3A744, matrix, 0x33, 2));
        if ((services.ReadFlags() & 1) != 0)
        {
            return new(frame, 0);
        }
        ushort next = unchecked((ushort)(age + 1));
        pool = Pool(frame);
        frame = PutPool(frame, pool.WriteSlot(slot, task.WriteWord(0xC, next)));
        return new(frame, unchecked((short)next) >= 22 ? 2 : 0);
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}
