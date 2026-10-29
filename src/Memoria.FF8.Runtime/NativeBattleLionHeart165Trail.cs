using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLionHeart165TrailDraw(NativeBattleGfGeometryBatch Batch, int CursorAdvance) : NativeBattleLionHeart165Operation;

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165ChildStep Advance5E9E00(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 64);
        uint temporary = allocation.Address;
        frame = frame with { Staging = allocation.Staging };
        NativeBattleRenzokuken160WeaponTip tip = inputs.ReadWeaponTip(frame.Parameters.Actor78);
        NativeBattleVector endpoint = new(unchecked((short)(tip.Position.X + ((666 * tip.DirectionX) >> 12))), unchecked((short)(tip.Position.Y + ((666 * tip.DirectionY) >> 12))), unchecked((short)(tip.Position.Z + ((666 * tip.DirectionZ) >> 12))));
        NativeBattleFileStaging staging = Point(Point(frame.Staging, temporary + 32, tip.Position), temporary + 40, endpoint);
        staging = WriteDword(WriteDword(WriteDword(staging, temporary + 48, unchecked((uint)tip.DirectionX)), temporary + 52, unchecked((uint)tip.DirectionY)), temporary + 56, unchecked((uint)tip.DirectionZ));
        int current = counter % 6;
        uint history = checked(0x23C6898u + (uint)current * 20);
        staging = WriteDword(staging, history, 1);
        staging = CopyPhysical(CopyPhysical(staging, temporary + 32, history + 4, 8), temporary + 40, history + 12, 8);
        int maximum = counter < 296 ? 6 : 302 - counter;
        int count = 0;
        for (; count < maximum; count++)
        {
            uint row = 0x23C6898u + (uint)current * 20;
            if (ReadDword(staging, row) == 0)
            {
                break;
            }
            staging = CopyPhysical(CopyPhysical(staging, row + 4, 0x23C3278u + (uint)count * 8, 8), row + 12, 0x23C32B0u + (uint)count * 8, 8);
            if (--current < 0)
            {
                current = 5;
            }
        }
        frame = frame with { Staging = staging };
        if (count > 1)
        {
            frame = Interpolate(frame, 0x23C3278, count, 0x23C56A8);
            frame = Interpolate(frame, 0x23C32B0, count, 0x23C57B0);
            staging = CopyPhysical(CopyPhysical(frame.Staging, 0x23C57B0, 0x23C58B8, 8), 0x23C56A8, 0x23C58D8, 8);
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(ReadWord(staging, 0x23C57B0) - ReadWord(staging, 0x23C57B8), ReadWord(staging, 0x23C57B2) - ReadWord(staging, 0x23C57BA), ReadWord(staging, 0x23C57B4) - ReadWord(staging, 0x23C57BC));
            staging = LongVector(staging, 0x23C58C8, direction.X, direction.Y, direction.Z);
            var width = NativeBattleActionGeometryRules.NormalizeVector56BC50(ReadWord(staging, 0x23C57B0) - ReadWord(staging, 0x23C56A8), ReadWord(staging, 0x23C57B2) - ReadWord(staging, 0x23C56AA), ReadWord(staging, 0x23C57B4) - ReadWord(staging, 0x23C56AC));
            staging = LongVector(staging, 0x23C5900, width.X, width.Y, width.Z);
            const uint color = 0x3A903030;
            staging = WriteDword(WriteDword(staging, temporary + 20, color), temporary + 28, color);
            NativeBattleEffectArena curves = NativeBattleEffectArena.Clear();
            for (int index = 0; index < 32; index++)
            {
                curves = curves.Write(NativeBattleGilgameshSwordTrailState.CurveA + index * 8, staging.Read(0x23C56A8u + (uint)index * 8, 6)).MarkUnknown(NativeBattleGilgameshSwordTrailState.CurveA + index * 8 + 6, 2);
                curves = curves.Write(NativeBattleGilgameshSwordTrailState.CurveB + index * 8, staging.Read(0x23C57B0u + (uint)index * 8, 6)).MarkUnknown(NativeBattleGilgameshSwordTrailState.CurveB + index * 8 + 6, 2);
            }
            frame = frame with { Staging = staging };
            NativeBattleGilgameshSwordTrailPacketResult packets = NativeBattleGilgameshSwordTrailPackets.Emit(new(curves), view, projection, frame.Memory.ScriptedGraphics, colors, color, observation =>
            {
                NativeBattleFileStaging observed = WriteDword(WriteDword(frame.Staging, temporary + 12, observation.FirstThreeFlags), temporary + 16, observation.PreviousColor);
                if (observation.AverageDepth is uint depth)
                {
                    observed = WriteDword(observed, temporary + 8, depth);
                }
                if (observation.EndColor is uint end)
                {
                    observed = WriteDword(observed, temporary + 20, end);
                }
                frame = frame with { Staging = observed };
            }, NativeBattlePilotVisualLifetimes.Identity(165, task));
            frame = inputs.Execute(frame, new NativeBattleLionHeart165TrailDraw(packets.Batch, packets.PoolCursorAdvance));
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 64) };
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), task.ReadWord(12) >= 301 ? 2 : 0);
    }

    private static NativeBattleLionHeart165Frame Interpolate(NativeBattleLionHeart165Frame frame, uint points, int count, uint destination)
    {
        NativeBattleTemporaryAllocation work400 = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 400);
        NativeBattleTemporaryAllocation work68 = NativeBattleTemporaryMemory.Allocate5082B0(work400.Staging, 68);
        NativeBattleFloatSplineResult result = NativeBattleFloatSpline.Interpolate5EA210(NativeBattleTemporaryMemory.ReadView(work68.Staging, points, count * 8), 0, count, 32, NativeBattleTemporaryMemory.ReadView(work68.Staging, destination, 256), 0, NativeBattleTemporaryMemory.ReadView(work68.Staging, work400.Address, 400), NativeBattleTemporaryMemory.ReadView(work68.Staging, work68.Address, 68));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(work68.Staging, destination, result.Output, 256);
        staging = NativeBattleTemporaryMemory.PublishView(staging, work400.Address, result.Work400, 400);
        staging = NativeBattleTemporaryMemory.PublishView(staging, work68.Address, result.Work68, 68);
        staging = NativeBattleTemporaryMemory.Release5082D0(NativeBattleTemporaryMemory.Release5082D0(staging, 68), 400);
        return frame with { Staging = staging };
    }

    private static NativeBattleFileStaging CopyPhysical(NativeBattleFileStaging staging, uint source, uint destination, int length)
    {
        return NativeBattleTemporaryMemory.PublishView(staging, destination, NativeBattleTemporaryMemory.ReadView(staging, source, length), length);
    }

    private static NativeBattleFileStaging LongVector(NativeBattleFileStaging staging, uint destination, int x, int y, int z)
    {
        return WriteDword(WriteDword(WriteDword(staging, destination, unchecked((uint)x)), destination + 4, unchecked((uint)y)), destination + 8, unchecked((uint)z));
    }
}
