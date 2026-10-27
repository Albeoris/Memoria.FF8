using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325BodyProjection
{
    public static NativeBattleEffectArena Project657B50(NativeBattleEffectArena arena, int outputOffset, int count, Func<int, NativeBattleVector> transformWorld, NativeBattleCameraProjection projection, int heightBase, short shift, short limit)
    {
        NativeBattleActionMatrix camera = projection.View ?? throw new NotSupportedException("Native Diablos body projection requires its current camera basis.");
        using NativeBattleEffectArenaTransaction? owned = arena.Transaction is null ? new(arena) : null;
        using NativeBattleEffectArenaWriter writer = new(owned?.View ?? arena);
        for (int index = 0; index < count; index++)
        {
            NativeBattleVector point = transformWorld(index);
            byte[] xyz = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(xyz, point.X);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), point.Y);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), point.Z);
            writer.Write(checked(outputOffset + index * 8), xyz);
        }
        for (int index = 0; index < count; index++)
        {
            int offset = checked(outputOffset + index * 8);
            byte[] xyz = writer.ReadCurrent(offset, 6);
            NativeBattleVector world = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(camera, world, projection);
            if (projected.Depth == 0)
            {
                writer.Write(offset + 4, new byte[2]);
                continue;
            }
            short shade = unchecked((short)(((world.Y - heightBase) >> (shift & 31)) + (limit >> 1)));
            if (shade < 0)
            {
                shade = 0;
            }
            else if (shade > limit)
            {
                shade = limit;
            }
            short sx = unchecked((short)projected.Screen);
            short sy = unchecked((short)(projected.Screen >> 16));
            byte clip = 0;
            if (sx < 0)
            {
                clip |= 1;
            }
            else if (sx >= 320 * 8)
            {
                clip |= 2;
            }
            if (sy < 0)
            {
                clip |= 4;
            }
            else if (sy >= 216 * 8)
            {
                clip |= 8;
            }
            byte[] record = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(record, projected.Screen);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4), projected.Depth);
            record[6] = clip;
            record[7] = unchecked((byte)shade);
            writer.Write(offset, record);
        }
        return owned?.Freeze() ?? writer.Freeze();
    }
}
