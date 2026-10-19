using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88VisibilityStep(NativeBattleRinoa88SceneMemory Scene, ImmutableArray<NativeBattleGraphicsModel> Models);

public static class NativeBattleRinoa88ModelOperations
{
    public static NativeBattleRinoa88VisibilityStep ApplyVisibility(NativeBattleRinoa88SceneMemory scene, ImmutableArray<NativeBattleGraphicsModel> models, uint callback)
    {
        if (models.IsDefault || models.Length != 7 || callback is not (0x769CB0 or 0x769DD0 or 0x769E90 or 0x769F40))
        {
            throw new InvalidDataException("Native Rinoa88 visibility requires its actual actor banks and reached callback.");
        }
        int first = callback is 0x769CB0 or 0x769F40 ? 3 : 0;
        int count = first == 3 ? 4 : 3;
        int saved = first == 3 ? 44 : 32;
        bool hide = callback is 0x769CB0 or 0x769DD0;
        for (int index = 0; index < count; index++)
        {
            NativeBattleGraphicsModel model = models[first + index];
            if ((model.Flags0 & 2) == 0)
            {
                continue;
            }
            if (hide)
            {
                scene = scene with { Settings = scene.Settings.Write(saved + index * 4, BitConverter.GetBytes((uint)model.Flags0)) };
                model = model with { Flags0 = unchecked((ushort)(model.Flags0 | 4)) };
            }
            else
            {
                ushort original = scene.Word(saved + index * 4);
                model = model with { Flags0 = unchecked((ushort)(model.Flags0 ^ ((model.Flags0 ^ original) & 4))) };
            }
            models = models.SetItem(first + index, model);
        }
        return new(scene, models);
    }

    public static ImmutableArray<byte> PartyMidpoint769A90(ImmutableArray<NativeBattleGraphicsModel> models)
    {
        if (models.IsDefault || models.Length != 7)
        {
            throw new InvalidDataException("Native769A90 requires the original seven actor records.");
        }
        short minX = 0;
        short maxX = 0;
        short minZ = 0;
        short maxZ = 0;
        bool seen = false;
        for (int index = 0; index < 3; index++)
        {
            if ((models[index].Flags0 & 2) == 0)
            {
                continue;
            }
            NativeBattleModelTransform transform = models[index].Transform ?? throw new NotSupportedException("Native769A90 requires each active party member's actual position.");
            short x = unchecked((short)transform.Position28.X);
            short z = unchecked((short)transform.Position28.Z);
            if (!seen)
            {
                minX = maxX = x;
                minZ = maxZ = z;
                seen = true;
            }
            else
            {
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minZ = Math.Min(minZ, z);
                maxZ = Math.Max(maxZ, z);
            }
        }
        byte[] result = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(result, unchecked((short)((minX + maxX) / 2)));
        BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(4), unchecked((short)((minZ + maxZ) / 2)));
        // Native769A90 leaves WORD6 as unconsumed stack padding; its unknown status is retained by the camera caller.
        return result.ToImmutableArray();
    }

    public static NativeBattleModulePoolTask Deform773420(NativeBattleModulePoolTask task, uint color, short amount, short mode)
    {
        if (amount == 0)
        {
            return task.WriteDword(88, color).WriteByte(91, 0).WriteByte(55, 0);
        }
        if (mode is < 0 or > 3)
        {
            throw new NotSupportedException("Native773420 requires its reached color modes0 through3.");
        }
        if (mode == 1)
        {
            color = (color & 0xFF000000) | ((color & 0x00FEFEFE) >> 1);
            task = task.WriteByte(91, 2);
        }
        else if (mode is 2 or 3)
        {
            task = task.WriteByte(91, 2);
            if (mode == 3)
            {
                task = task.WriteByte(55, (byte)(unchecked((byte)(-1 - 255 * amount / 4096)) >> 2));
            }
        }
        for (int index = 0; index < 3; index++)
        {
            byte component = unchecked((byte)(color >> (index * 8)));
            task = task.WriteByte(88 + index, unchecked((byte)(component - amount * component / 4096)));
        }
        return task;
    }
}
