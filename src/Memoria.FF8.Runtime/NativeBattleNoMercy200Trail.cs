using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNoMercy200TrailDraw(NativeBattleGfGeometryBatch Batch, int CursorAdvance) : NativeBattleNoMercy200Operation;

public static partial class NativeBattleNoMercy200Controller
{
    public static NativeBattleNoMercy200ChildStep Begin63A220(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool)
    {
        (pool, int slot) = pool.Append(0x63A250);
        pool = pool.WriteSlot(slot, pool.Slots[slot].WriteWord(12, 0));
        NativeBattleFileStaging staging = frame.Staging;
        for (uint address = 0x24FBCE8; address < 0x24FBD60; address += 20)
        {
            staging = WriteDword(staging, address, 0);
        }
        return new(frame with { Staging = staging }, pool, 0);
    }

    public static NativeBattleNoMercy200ChildStep Advance63A250(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleNoMercy200Inputs inputs, NativeBattleRenzokuken160WeaponTip tip, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short tick = Word(task, 12);
        if (tick < 0)
        {
            throw new NotSupportedException("Native No Mercy signed trail index leaves its reached six-row ring.");
        }
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 64);
        uint work = temporary.Address;
        NativeBattleFileStaging staging = WritePoint(temporary.Staging, work + 32, tip.Position);
        staging = WriteDword(WriteDword(WriteDword(staging, work + 48, unchecked((uint)tip.DirectionX)), work + 52, unchecked((uint)tip.DirectionY)), work + 56, unchecked((uint)tip.DirectionZ));
        NativeBattleVector end = new(unchecked((short)(tip.Position.X + (unchecked(737 * tip.DirectionX) >> 12))), unchecked((short)(tip.Position.Y + (unchecked(737 * tip.DirectionY) >> 12))), unchecked((short)(tip.Position.Z + (unchecked(737 * tip.DirectionZ) >> 12))));
        staging = WritePoint(staging, work + 40, end);
        int ring = tick % 6;
        uint row = checked(0x24FBCE8u + (uint)ring * 20);
        staging = WriteDword(staging, row, 1);
        staging = CopyEight(staging, work + 32, row + 4);
        staging = CopyEight(staging, work + 40, row + 12);
        int admitted = tick < 147 ? 6 : 153 - tick;
        int count = 0;
        for (int index = ring; count < admitted; index = index == 0 ? 5 : index - 1)
        {
            uint source = checked(0x24FBCE8u + (uint)index * 20);
            if (ReadDword(staging, source) == 0)
            {
                break;
            }
            staging = CopyEight(staging, source + 4, checked(0x24F7A68u + (uint)count * 8));
            staging = CopyEight(staging, source + 12, checked(0x24F8D20u + (uint)count * 8));
            count++;
        }
        if (count > 1)
        {
            staging = Spline(staging, 0x24F7A68, 0x24F9730);
            staging = Spline(staging, 0x24F8D20, 0x24F9830);
            staging = CopyEight(staging, 0x24F9830, 0x24F9958);
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(ReadWord(staging, 0x24F9830) - ReadWord(staging, 0x24F9838), ReadWord(staging, 0x24F9832) - ReadWord(staging, 0x24F983A), ReadWord(staging, 0x24F9834) - ReadWord(staging, 0x24F983C));
            staging = WriteDword(WriteDword(WriteDword(staging, 0x24F9960, unchecked((uint)direction.X)), 0x24F9964, unchecked((uint)direction.Y)), 0x24F9968, unchecked((uint)direction.Z));
            staging = CopyEight(staging, 0x24F9730, 0x24F9970);
            var side = NativeBattleActionGeometryRules.NormalizeVector56BC50(ReadWord(staging, 0x24F9958) - ReadWord(staging, 0x24F9730), ReadWord(staging, 0x24F995A) - ReadWord(staging, 0x24F9732), ReadWord(staging, 0x24F995C) - ReadWord(staging, 0x24F9734));
            staging = WriteDword(WriteDword(WriteDword(staging, 0x24F9980, unchecked((uint)side.X)), 0x24F9984, unchecked((uint)side.Y)), 0x24F9988, unchecked((uint)side.Z));
            NativeBattleEffectArena curves = NativeBattleEffectArena.Clear();
            for (int index = 0; index < 32; index++)
            {
                curves = curves.Write(NativeBattleGilgameshSwordTrailState.CurveA + index * 8, staging.Read(checked(0x24F9730u + (uint)index * 8), 6));
                curves = curves.Write(NativeBattleGilgameshSwordTrailState.CurveB + index * 8, staging.Read(checked(0x24F9830u + (uint)index * 8), 6));
            }
            staging = WriteDword(WriteDword(staging, work + 20, 0x3A185820), work + 28, 0x3A185820);
            NativeBattleGilgameshSwordTrailPacketResult drawn = NativeBattleGilgameshSwordTrailPackets.Emit(new(curves), inputs.View, projection, registers, colors, 0x3A185820, observation =>
            {
                staging = WriteDword(WriteDword(staging, work + 12, observation.FirstThreeFlags), work + 16, observation.PreviousColor);
                if (observation.AverageDepth is uint depth)
                {
                    staging = WriteDword(staging, work + 8, depth);
                }
                if (observation.EndColor is uint color)
                {
                    staging = WriteDword(staging, work + 20, color);
                }
            });
            frame = frame with { Staging = staging };
            frame = inputs.Execute(frame, new NativeBattleNoMercy200TrailDraw(drawn.Batch, drawn.PoolCursorAdvance));
            staging = frame.Staging;
        }
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 64);
        frame = frame with { Staging = staging };
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(frame, pool.WriteSlot(slot, task), Word(task, 12) >= 152 ? 2 : 0);

        NativeBattleFileStaging Spline(NativeBattleFileStaging current, uint points, uint output)
        {
            NativeBattleTemporaryAllocation work400 = NativeBattleTemporaryMemory.Allocate5082B0(current, 400);
            NativeBattleTemporaryAllocation work68 = NativeBattleTemporaryMemory.Allocate5082B0(work400.Staging, 68);
            current = work68.Staging;
            NativeBattleFloatSplineResult result = NativeBattleFloatSpline.Interpolate5EA210(NativeBattleTemporaryMemory.ReadView(current, points, count * 8), 0, count, 32, NativeBattleTemporaryMemory.ReadView(current, output, 256), 0, NativeBattleTemporaryMemory.ReadView(current, work400.Address, 400), NativeBattleTemporaryMemory.ReadView(current, work68.Address, 68));
            current = NativeBattleTemporaryMemory.PublishView(current, output, result.Output, 256);
            current = NativeBattleTemporaryMemory.PublishView(current, work400.Address, result.Work400, 400);
            current = NativeBattleTemporaryMemory.PublishView(current, work68.Address, result.Work68, 68);
            return NativeBattleTemporaryMemory.Release5082D0(NativeBattleTemporaryMemory.Release5082D0(current, 68), 400);
        }
    }

    private static NativeBattleFileStaging CopyEight(NativeBattleFileStaging staging, uint source, uint destination) => NativeBattleTemporaryMemory.PublishView(staging, destination, NativeBattleTemporaryMemory.ReadView(staging, source, 8), 8);
}
