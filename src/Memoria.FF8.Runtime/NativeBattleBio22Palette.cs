using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The complete retained physical Bio TIM, independently owned from its immutable palette sources.</summary>
public sealed record NativeBattleBio22Palette([property: JsonRequired] ImmutableArray<byte> Bytes)
{
    public void Validate() => ValidateTim(Bytes);

    public static void ValidateTim(ImmutableArray<byte> bytes)
    {
        if (bytes.IsDefault || bytes.Length != 0x220 || U32(0) != 0x10 || U32(4) != 9 || U32(8) != 0x20C || U16(0xC) != 0x140 || U16(0xE) != 0xF1 || U16(0x10) != 256 || U16(0x12) != 1 || U32(0x214) != 12 || U16(0x21C) != 0 || U16(0x21E) != 0)
        {
            throw new InvalidDataException("Native Bio palette requires its complete original TIM layout.");
        }
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset));
    }

    public static NativeBattleBio22Palette FromColdProcess(NativeBattleEffectHeapAllocation authenticatedBaseline)
    {
        if (authenticatedBaseline is null || authenticatedBaseline.Identity != "FF8_EN:16146E8")
        {
            throw new InvalidDataException("Native Bio palette cold ingress requires its distinct authenticated baseline.");
        }
        NativeBattleBio22Palette result = new(authenticatedBaseline.Data);
        result.Validate();
        return result;
    }

    public NativeBattleBio22Palette Morph89CB90(ImmutableArray<byte> first, ImmutableArray<byte> second, short ratio)
    {
        Validate();
        ValidateTim(first);
        ValidateTim(second);
        ImmutableArray<NativeBattleVector> colors = NativeBattleGfMorphGeometry.MorphVertices45E9D0(Colors(first), 0, Colors(second), 0, 128, ratio);
        byte[] output = Bytes.ToArray();
        for (int index = 0; index < colors.Length; index++)
        {
            NativeBattleVector color = colors[index];
            ushort packed = (ushort)(0x8000 | (color.X & 31) | ((color.Y & 31) << 5) | ((color.Z & 31) << 10));
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(0x14 + 2 * index), packed);
        }
        return new(output.ToImmutableArray());

        static ImmutableArray<byte> Colors(ImmutableArray<byte> source)
        {
            byte[] vectors = new byte[128 * 8];
            for (int index = 0; index < 128; index++)
            {
                ushort packed = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(0x14 + index * 2));
                for (int component = 0; component < 3; component++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(vectors.AsSpan(index * 8 + component * 2), (short)((packed >> (component * 5)) & 31));
                }
            }
            return vectors.ToImmutableArray();
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBio22Palette? Bio22Palette
    {
        get => effectState.Bio22Palette;
        init => effectState = effectState with { Bio22Palette = value };
    }
}

public sealed record NativeBattleBio22PaletteStep(NativeBattleModulePoolTask Task, NativeBattleBio22Palette Palette, NativeCrtRandomState Random, bool QueueTim, uint Result);

public static class NativeBattleBio22PaletteTask
{
    public static NativeBattleBio22PaletteStep Advance89CA70(NativeBattleModulePoolTask task, NativeBattleModulePoolTask primary, NativeBattleBio22Palette palette, NativeBattleBio22Resources resources, NativeCrtRandomState random)
    {
        if (task.Callback != 0x89CA70 || task.PayloadLength != 0x38 - 12)
        {
            throw new InvalidDataException("Native Bio palette callback requires its actual38-byte hierarchy task.");
        }
        bool queue = false;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteWord(0x32, 0).WriteWord(0x34, 1).WriteByte(0x29, 1);
                break;
            case 1:
                task = task.WriteWord(0x36, unchecked((ushort)(task.ReadWord(0x36) + 0x800)));
                if (unchecked((short)task.ReadWord(0x36)) >= 0x1000)
                {
                    task = task.WriteWord(0x36, 0).WriteWord(0x32, task.ReadWord(0x34));
                    random = random.Draw(out ushort value);
                    short selected = unchecked((short)(task.ReadWord(0x34) + (value & 1) + 1));
                    while (selected > 2)
                    {
                        selected = unchecked((short)(selected - 3));
                    }
                    task = task.WriteWord(0x34, unchecked((ushort)selected));
                }
                int first = unchecked((short)task.ReadWord(0x32));
                int second = unchecked((short)task.ReadWord(0x34));
                if ((uint)first >= 3 || (uint)second >= 3 || resources.PaletteSources.IsDefault || resources.PaletteSources.Length != 3)
                {
                    throw new InvalidDataException("Native Bio palette selection leaves its supplied three-source directory.");
                }
                palette = palette.Morph89CB90(resources.PaletteSources[first].Data, resources.PaletteSources[second].Data, unchecked((short)task.ReadWord(0x36)));
                queue = true;
                if (primary.ReadWord(0x80) != 0)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Bio palette phase is unsupported.");
        }
        byte flags = task.ReadByte(0x26);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, palette, random, queue, (flags & 1) != 0 && task.ReadByte(0x28) == 0 ? 2u : 0u);
    }
}
