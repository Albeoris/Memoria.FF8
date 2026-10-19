using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDiablos325Puffs
{
    public static NativeBattleDiablos325Frame SpawnRing6558A4(NativeBattleDiablos325Frame frame)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos ring birth requires its actual arena.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArenaWriter writer = new(owned?.View ?? source);
        NativeCrtRandomState? random = frame.Random;
        for (int countdown = 31; countdown >= 0; countdown--)
        {
            int? selected = Allocate658B20(writer, ref state);
            if (selected is not int offset)
            {
                continue;
            }
            random = (random ?? throw new NotSupportedException("Native Diablos ring birth requires its retained CRT random owner.")).Draw(out ushort angleRandom);
            random = random.Draw(out ushort radiusRandom);
            int angle = angleRandom & 0x7FF;
            int radius = (radiusRandom & 0x1FF) + 512;
            short x = unchecked((short)(NativeBattleCameraMath.Cosine(angle) * radius >> 12));
            short y = unchecked((short)(NativeBattleCameraMath.Sine(angle) * radius >> 12));
            int z = -(radius / 4);
            random = random.Draw(out ushort shadeRandom);
            byte[] bytes = new byte[16];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, (countdown & 1) == 0 ? 0xFD6814u : 0xFD69C0u);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), x);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), unchecked((short)(y - 8192)));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), unchecked((short)(state.FarZ51DC + z)));
            bytes[10] = unchecked((byte)(angle / 16));
            bytes[11] = (byte)((shadeRandom & 0x3F) + 16);
            bytes[12] = unchecked((byte)(x / 64));
            bytes[13] = unchecked((byte)(y / 64));
            bytes[14] = unchecked((byte)(z / 16));
            bytes[15] = 16;
            writer.Write(offset, bytes);
        }
        return frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? writer.Freeze(), Diablos325 = state }, Random = random };
    }

    public static NativeBattleDiablos325Frame Drift658CE0(NativeBattleDiablos325Frame frame)
    {
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos ring drift requires its actual arena.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArenaWriter writer = new(owned?.View ?? source);
        for (int index = 0; index < Capacity; index++)
        {
            int offset = BaseOffset + index * 16;
            byte life = writer.ReadCurrent(offset + 15, 1)[0];
            if (life == 0)
            {
                continue;
            }
            writer.Write(offset + 15, [unchecked((byte)(life - 1))]);
            byte[] position = writer.ReadCurrent(offset + 4, 6);
            byte[] velocity = writer.ReadCurrent(offset + 12, 3);
            for (int axis = 0; axis < 3; axis++)
            {
                short value = BinaryPrimitives.ReadInt16LittleEndian(position.AsSpan(axis * 2));
                BinaryPrimitives.WriteInt16LittleEndian(position.AsSpan(axis * 2), unchecked((short)(value + (unchecked((sbyte)velocity[axis]) << 2))));
            }
            writer.Write(offset + 4, position);
        }
        return frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? writer.Freeze() } };
    }
}
