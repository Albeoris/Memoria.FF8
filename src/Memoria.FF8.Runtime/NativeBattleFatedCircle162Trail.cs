namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFatedCircle162Trail(NativeBattleGfGeometryBatch Batch, int CursorAdvance) : NativeBattleFatedCircle162Operation;

public static partial class NativeBattleFatedCircle162Controller
{
    public static NativeBattleFatedCircle162ChildStep Advance5F99C0(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, Func<byte, NativeBattleRenzokuken160WeaponTip> readWeaponTip, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        if (counter < 0)
        {
            throw new NotSupportedException("Native5F99C0 requires a nonnegative original history index.");
        }
        byte actor = frame.Memory.FatedCircle162?.Parameters?.Actor78 ?? throw new InvalidDataException("Native5F99C0 requires its actual caster owner.");
        NativeBattleRenzokuken160WeaponTip tip = readWeaponTip(actor);
        NativeBattleVector endpoint = new(unchecked((short)(tip.Position.X + (unchecked(666 * tip.DirectionX) >> 12))), unchecked((short)(tip.Position.Y + (unchecked(666 * tip.DirectionY) >> 12))), unchecked((short)(tip.Position.Z + (unchecked(666 * tip.DirectionZ) >> 12))));
        int ring = counter % 7;
        uint record = checked(0x23D0CE8u + (uint)ring * 20);
        NativeBattleFileStaging staging = WriteDword(frame.Staging, record, 1);
        staging = Point(staging, record + 4, tip.Position, tip.PositionPadding);
        staging = Point(staging, record + 12, endpoint, tip.EndpointPadding);
        int duration = ReadDword(staging, 0x23D0D74);
        int maximum = counter < duration - 7 ? 7 : duration - counter;
        int count = 0;
        NativeBattleEffectArena curve = NativeBattleEffectArena.Clear();
        for (int index = ring; count < maximum; index = index == 0 ? 6 : index - 1)
        {
            uint source = checked(0x23D0CE8u + (uint)index * 20);
            if (ReadDword(staging, source) == 0)
            {
                break;
            }
            staging = Copy(staging, source + 4, checked(0x23CE588u + (uint)count * 8), 8);
            staging = Copy(staging, source + 12, checked(0x23CE5C8u + (uint)count * 8), 8);
            curve = curve.Write(NativeBattleGilgameshSwordTrailState.GatherA + count * 8, staging.Read(source + 4, 6));
            curve = curve.Write(NativeBattleGilgameshSwordTrailState.GatherB + count * 8, staging.Read(source + 12, 6));
            count++;
        }
        frame = frame with { Staging = staging };
        if (count > 1)
        {
            curve = curve.Write(NativeBattleGilgameshSwordTrailState.CurveA, staging.Read(0x23CF1C0, 256)).Write(NativeBattleGilgameshSwordTrailState.CurveB, staging.Read(0x23CF2C0, 256));
            NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, curve, NativeBattleGilgameshSwordTrailState.GatherA, NativeBattleEffectArena.Clear().MarkUnknown(0, 400));
            for (int index = 0; index < 32; index++)
            {
                curve = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, index * 4096 / 31, curve, NativeBattleGilgameshSwordTrailState.CurveA + index * 8);
            }
            work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, curve, NativeBattleGilgameshSwordTrailState.GatherB, work);
            for (int index = 0; index < 32; index++)
            {
                curve = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, index * 4096 / 31, curve, NativeBattleGilgameshSwordTrailState.CurveB + index * 8);
            }
            staging = staging.Write(0x23CF1C0, curve.Read(NativeBattleGilgameshSwordTrailState.CurveA, 256)).Write(0x23CF2C0, curve.Read(NativeBattleGilgameshSwordTrailState.CurveB, 256));
            staging = Copy(staging, 0x23CF2C0, 0x23CF3C0, 8);
            var tangent = NativeBattleActionGeometryRules.NormalizeVector56BC50(ReadWord(staging, 0x23CF2C0) - ReadWord(staging, 0x23CF2C8), ReadWord(staging, 0x23CF2C2) - ReadWord(staging, 0x23CF2CA), ReadWord(staging, 0x23CF2C4) - ReadWord(staging, 0x23CF2CC));
            staging = Direction(staging, 0x23CF408, tangent.X, tangent.Y, tangent.Z);
            staging = Copy(staging, 0x23CF1C0, 0x23CF418, 8);
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(ReadWord(staging, 0x23CF3C0) - ReadWord(staging, 0x23CF1C0), ReadWord(staging, 0x23CF3C2) - ReadWord(staging, 0x23CF1C2), ReadWord(staging, 0x23CF3C4) - ReadWord(staging, 0x23CF1C4));
            staging = Direction(staging, 0x23CF420, direction.X, direction.Y, direction.Z);
            frame = frame with { Staging = staging };
            NativeBattleGilgameshSwordTrailPacketResult drawn = NativeBattleGilgameshSwordTrailPackets.Emit(new(curve), view, projection, registers, colors, 0x3A903030);
            frame = inputs.Execute(frame, new NativeBattleFatedCircle162Trail(drawn.Batch, drawn.PoolCursorAdvance));
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, Word(task, 12) >= duration - 1 ? 2 : 0);
    }

    private static NativeBattleFileStaging Direction(NativeBattleFileStaging staging, uint address, int x, int y, int z)
    {
        return WriteDword(WriteDword(WriteDword(staging, address, unchecked((uint)x)), address + 4, unchecked((uint)y)), address + 8, unchecked((uint)z));
    }

    private static NativeBattleFileStaging Point(NativeBattleFileStaging staging, uint address, NativeBattleVector point, ushort? padding)
    {
        staging = WriteWord(WriteWord(WriteWord(staging, address, unchecked((ushort)point.X)), address + 2, unchecked((ushort)point.Y)), address + 4, unchecked((ushort)point.Z));
        return padding is ushort observed ? WriteWord(staging, address + 6, observed) : Forget(staging, address + 6, 2);
    }

    private static NativeBattleFileStaging Copy(NativeBattleFileStaging staging, uint source, uint target, int length) => staging.CopyMemory(source, target, length);

    private static NativeBattleFileStaging Forget(NativeBattleFileStaging staging, uint address, int length) => staging.MarkUnknown(address, length);
}
