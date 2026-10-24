using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160ChildStep AdvanceSpecialTrail(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, NativeBattleSummonColors colors)
    {
        NativeBattleRenzokukenSpecialState retained = ReadState(frame);
        NativeBattleRenzokuken160State core = retained.Core;
        short counter = unchecked((short)pool.Slots[slot].ReadWord(12));
        int admitted = counter < core.Duration - 7 ? 7 : core.Duration - counter;
        if (counter >= 0 && admitted > 1)
        {
            int ring = counter % 7;
            int previous = ring == 0 ? 6 : ring - 1;
            if (BinaryPrimitives.ReadInt32LittleEndian(core.TrailHistory.AsSpan().Slice(previous * 20)) != 0)
            {
                frame = Store(frame, retained with { PreviousOrigin = core.Origin, PreviousDirection = core.Direction, PreviousOriginPadding = core.OriginPadding, PreviousDirectionPadding = core.DirectionPadding });
            }
        }
        return NativeBattleRenzokuken160Controller.Advance5FFAA0(frame, pool, slot, inputs, view, projection, registers, colors);
    }
}
