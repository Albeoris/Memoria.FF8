namespace Memoria.FF8.Runtime;

/// <summary>50A370's current model-matrix rotation and50A5F0's vertical WORD writer, called before50C110/502D40 by50A0A0.</summary>
public static class NativeBattleModelStatusMotion
{
    public static NativeBattleGraphicsModel Advance50A370(NativeBattleGraphicsModel model)
    {
        ushort flags = model.Flags2 ?? throw new NotSupportedException("Native50A370 requires its model flags WORD2.");
        if ((flags & 8) != 0)
        {
            return model;
        }
        byte id = model.ModelId4 ?? throw new NotSupportedException("Native50A370 requires its unsigned model ID BYTE4.");
        byte sequence = model.Sequence ?? throw new NotSupportedException("Native50A370 requires its actual resource-header sequence BYTE0.");
        if (id < 16 && sequence == 19)
        {
            return model;
        }
        byte standing = model.SequenceState?.Default2 ?? throw new NotSupportedException("Native50A370 requires its resource-header default sequence BYTE2.");
        if (id < 16 ? (sequence != standing) ^ (sequence == 16) : sequence != standing && sequence != 8)
        {
            return model;
        }
        uint statuses = model.Flags8 ?? throw new NotSupportedException("Native50A370 requires its actual status DWORD8.");
        int angle;
        if ((statuses & 0x800) != 0)
        {
            byte timer = ReadTimer(model, 11, "50A370");
            angle = timer << 8;
            model = model with { StatusTimers = model.StatusTimers.SetItem(11, unchecked((byte)(timer + 1))) };
        }
        else if ((statuses & 4) != 0)
        {
            angle = 2048;
        }
        else
        {
            return model;
        }
        NativeBattleActionMatrix matrix = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native50A370 requires its retained model matrix at+40h before body submission.");
        return model with { GeometryWorldMatrix = NativeBattleGfMatrices.RotateY56D020(matrix, angle) };
    }

    public static NativeBattleGraphicsModel Advance50A5F0(NativeBattleGraphicsModel model)
    {
        ushort flags = model.Flags2 ?? throw new NotSupportedException("Native50A5F0 requires its model flags WORD2.");
        if ((flags & 8) != 0)
        {
            return model;
        }
        byte sequence = model.Sequence ?? throw new NotSupportedException("Native50A5F0 requires its actual resource-header sequence BYTE0.");
        byte standing = model.SequenceState?.Default2 ?? throw new NotSupportedException("Native50A5F0 requires its resource-header default sequence BYTE2.");
        if (sequence != standing && sequence != 8)
        {
            return model;
        }
        uint statuses = model.Flags8 ?? throw new NotSupportedException("Native50A5F0 requires its actual status DWORD8.");
        if ((statuses & 0x200) == 0)
        {
            return model;
        }
        byte timer = ReadTimer(model, 9, "50A5F0");
        int sine = NativeBattleCameraMath.Sine(timer << 8);
        int fraction = (sine + 4096) / 2;
        short vertical = unchecked((short)((-100 * fraction >> 12) - 200));
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native50A5F0 requires its vertical model WORD22h owner.");
        return model with { StatusTimers = model.StatusTimers.SetItem(9, unchecked((byte)(timer + 1))), Transform = transform with { Vertical34 = vertical } };
    }

    private static byte ReadTimer(NativeBattleGraphicsModel model, int index, string function)
    {
        if (model.StatusTimers.IsDefault || model.StatusTimers.Length <= index || model.StatusTimers[index] is not byte timer)
        {
            throw new NotSupportedException($"Native{function} requires the selected status timer BYTE{index:X2}; missing retained bytes are unknown.");
        }
        return timer;
    }
}
