namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160ChildStep AdvanceChild334337(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, short cameraRoll, Func<byte, NativeBattleModuleAnchor> readAnchor)
    {
        return pool.Slots[slot].Callback switch
        {
            0x5878B0 or 0x5857D0 or 0x583160 or 0x580AB0 => Advance334337(frame, pool, slot, inputs, resources, readAnchor),
            0x588260 or 0x586230 or 0x583B90 or 0x581510 => AdvanceSpecialTrail(frame, pool, slot, inputs, view, projection, frame.Memory.ScriptedGraphics, colors),
            0x5887E0 or 0x5867B0 or 0x584110 or 0x581A90 => NativeBattleRenzokuken160Controller.Advance5FFFE0(frame, pool, slot, inputs),
            0x588910 or 0x5868E0 or 0x584240 or 0x581BC0 => AdvanceGroupContact(frame, pool, slot, inputs, resources),
            0x588CE0 or 0x586CB0 or 0x584610 or 0x581F90 => NativeBattleRenzokuken160Controller.Advance6004A0(frame, pool, slot, inputs, resources, view, projection, colors),
            0x588FC0 => Advance334Lines(frame, pool, slot, inputs, resources, view, projection, colors),
            0x5892B0 or 0x589710 or 0x5872C0 or 0x5848F0 or 0x584DE0 or 0x585220 or 0x582270 or 0x5826C0 or 0x582B50 => GroupLateMesh(frame, pool, slot, inputs, resources, view, projection, colors),
            0x589400 => AdvanceTargetSprites(frame, pool, slot, inputs, resources.SpecialSprites[0], view, projection, cameraRoll, 240, 32, 3, 90, 30),
            0x586F90 => AdvanceTargetSprites(frame, pool, slot, inputs, resources.SpecialSprites[0], view, projection, cameraRoll, 240, 16, 3, 300, 90, random335: true),
            0x584A30 => AdvanceTargetSprites(frame, pool, slot, inputs, resources.SpecialSprites[0], view, projection, cameraRoll, 17, 24, 3, 110, 40, 37),
            0x5823B0 => AdvanceTargetSprites(frame, pool, slot, inputs, resources.SpecialSprites[0], view, projection, cameraRoll, 241, 22, 3, 100, 25, scaleRange: 2176, scaleBase: 1792),
            0x584F20 or 0x582850 => AdvanceGroupRadialSprites(frame, pool, slot, inputs, resources.SpecialSprites[1], view, projection, cameraRoll),
            0x585360 or 0x582C90 => AdvanceGroupCamera(frame, pool, slot, inputs),
            _ => throw new NotSupportedException($"Native Renzokuken callback {pool.Slots[slot].Callback:X} has no original334/335/336/337 contract.")
        };
    }

    private static NativeBattleRenzokuken160ChildStep AdvanceGroupContact(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources)
    {
        uint callback = pool.Slots[slot].Callback switch
        {
            0x588910 => 0x588CE0,
            0x5868E0 => 0x586CB0,
            0x584240 => 0x584610,
            0x581BC0 => 0x581F90,
            _ => throw new InvalidDataException("Native Renzokuken contact requires its original producer.")
        };
        return NativeBattleRenzokuken160Controller.Advance600110(frame, pool, slot, inputs, resources, callback, Origin);

        NativeBattleRenzokukenContactOrigin Origin(int contact)
        {
            NativeBattleRenzokukenSpecialState state = ReadState(frame);
            var offsets = resources.ContactOffsets ?? state.ContactOffsets;
            if (offsets.IsDefault || (uint)contact >= offsets.Length)
            {
                throw new NotSupportedException("Native Renzokuken contact requires its actual retained offset vector.");
            }
            NativeBattleActionMatrix matrix = inputs.ReadModel(state.Core.Actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native Renzokuken contact requires its current actor matrix64.");
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(matrix, offsets[contact]);
            return new(new(unchecked((short)(state.Core.Origin.X + rotated.X)), unchecked((short)(state.Core.Origin.Y + rotated.Y)), unchecked((short)(state.Core.Origin.Z + rotated.Z))), null);
        }
    }
}
