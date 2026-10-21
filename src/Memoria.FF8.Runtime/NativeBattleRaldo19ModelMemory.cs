using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRaldo19ModelMemory
{
    public static NativeBattleEffectArena Capture(NativeBattleGraphicsModel model)
    {
        NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear().MarkUnknown(0, 156);
        PutWord(0, model.Flags0);
        PutWord(0x0C, model.Rotation12);
        PutWord(0x0E, model.Rotation14);
        PutWord(0x10, model.Rotation16);
        PutWord(0x12, model.RotationPadding18 is { } padding ? unchecked((short)padding) : null);
        if (model.Transform is { } transform)
        {
            PutWord(0x1C, transform.Position28.X);
            PutWord(0x1E, transform.Position28.Y);
            PutWord(0x20, transform.Position28.Z);
            PutWord(0x22, transform.Vertical34);
            PutWord(0x24, transform.Angle36);
        }
        return bytes;

        void PutWord(int offset, int? value)
        {
            if (value is { } known)
            {
                byte[] word = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(word, unchecked((ushort)known));
                bytes = bytes.Write(offset, word);
            }
        }
    }

    public static NativeBattleGraphicsModel Apply(NativeBattleGraphicsModel model, int offset, NativeBattleEffectArena source, int sourceOffset, int length)
    {
        bool flags = offset == 0 && length is 1 or 2;
        bool rotation = offset == 0x0E && length == 2 || offset is 0x0C or 0x10 && length == 4;
        bool position = offset is 0x1C or 0x20 && length == 4 || offset is 0x1C or 0x1E or 0x20 && length == 2;
        bool translation = offset is 0x54 or 0x58 or 0x5C && length == 4;
        if (!flags && !rotation && !position && !translation)
        {
            throw new NotSupportedException("Raldo actor stores require their original field widths and destinations.");
        }
        NativeBattleEffectArena updated = NativeBattleRaldo19BallController.Copy(source, sourceOffset, Capture(model), offset, length);
        if (flags)
        {
            return model with { Flags0 = unchecked((ushort)Word(0)) };
        }
        if (rotation)
        {
            (byte[] bytes, bool[] known) = updated.ReadSnapshot(0x12, 2);
            return model with { Rotation12 = NullableWord(0x0C), Rotation14 = NullableWord(0x0E), Rotation16 = NullableWord(0x10), RotationPadding18 = known.All(value => value) ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : null };
        }
        if (position)
        {
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Raldo movement requires the actor's actual transform owner.");
            return model with { Transform = transform with { Position28 = new(Word(0x1C), Word(0x1E), Word(0x20)), Vertical34 = Word(0x22) } };
        }
        NativeBattleActionMatrix matrix = model.GeometryWorldMatrix ?? throw new NotSupportedException("Raldo movement requires the actor's actual world matrix.");
        int value = BinaryPrimitives.ReadInt32LittleEndian(updated.Read(offset, 4));
        return model with { GeometryWorldMatrix = offset switch { 0x54 => matrix with { X = value }, 0x58 => matrix with { Y = value }, 0x5C => matrix with { Z = value }, _ => throw new InvalidDataException("Raldo translation leaves its original matrix.") } };

        short Word(int address) => BinaryPrimitives.ReadInt16LittleEndian(updated.Read(address, 2));

        short? NullableWord(int address)
        {
            (byte[] bytes, bool[] known) = updated.ReadSnapshot(address, 2);
            return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        }
    }
}
