namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static NativeBattleEffectArena CaptureNativeEffect301Model(NativeBattleGraphicsModel model)
    {
        NativeBattleEffectArena bytes = CaptureNativeEffect210Model(model);
        if (model.Color40 is uint color40)
        {
            bytes = bytes.Write(0x28, BitConverter.GetBytes(color40));
        }
        if (model.Color44 is uint color44)
        {
            bytes = bytes.Write(0x2C, BitConverter.GetBytes(color44));
        }
        if (model.SequenceState is { } sequence)
        {
            bytes = bytes.Write(0x7C, BitConverter.GetBytes(sequence.MeshMask124));
        }
        return bytes;
    }

    private static NativeBattleGraphicsModel ApplyNativeEffect301ModelBytes(NativeBattleGraphicsModel model, int offset, NativeBattleEffectArena data, int length)
    {
        if (offset == 0x2C && length == 3)
        {
            uint color = model.Color44 ?? throw new NotSupportedException("Effect301 requires its current packed model color before its RGB store.");
            byte[] bytes = data.Read(0, 3);
            uint rgb = bytes[0] | ((uint)bytes[1] << 8) | ((uint)bytes[2] << 16);
            return model with { Color44 = (color & 0xFF000000) | rgb };
        }
        if (offset == 0x2F && length == 1)
        {
            uint color = model.Color44 ?? throw new NotSupportedException("Effect301 requires its current packed model color before its code-byte store.");
            return model with { Color44 = (color & 0xFFFFFF) | ((uint)data.Read(0, 1)[0] << 24) };
        }
        if (offset == 0 && length == 1)
        {
            return model with { Flags0 = unchecked((ushort)((model.Flags0 & 0xFF00) | data.Read(0, 1)[0])) };
        }
        return ApplyNativeEffect174ModelBytes(model, new(model.Index, offset, data, length));
    }

    private static int NativeEffect301Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect301 requires an actual current model address.");
        }
        return checked((int)(offset / 156));
    }
}
