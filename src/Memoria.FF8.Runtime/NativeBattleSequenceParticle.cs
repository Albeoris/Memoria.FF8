namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSequenceParticle(uint PackedParameters, byte Placement, byte Bone, short Fraction, NativeBattleVector? Position, int NextOffset);

/// <summary>505900's shared model-sequence particle packet and operand cursor.</summary>
public static class NativeBattleSequenceParticles
{
    private enum Placement
    {
        Model,
        ExplicitBone,
        RandomBone,
        LocalPosition
    }

    internal static int ReadOperandEnd(NativeBattleAuxiliaryProgram program)
    {
        program.Validate();
        int operands = program.Offset;
        if (operands + 1 >= program.Source.Length)
        {
            throw new InvalidDataException("Native505900 packet exceeds its original sequence resource.");
        }
        byte flags = program.Source[operands + 1];
        int argumentCount = ReadPlacement(flags) switch { Placement.ExplicitBone => 1, Placement.LocalPosition => 6, _ => 0 };
        return operands + 2 + System.Numerics.BitOperations.PopCount((uint)flags & 7) + argumentCount;
    }

    public static NativeBattleSequenceParticle Read505900(NativeBattleAuxiliaryProgram program, int cameraAngle, Func<byte>? selectRandomBone = null, bool suppressed = false)
    {
        program.Validate();
        int offset = program.Offset;
        byte resource = ReadByte();
        byte flags = ReadByte();
        uint packed = 0x100000u | resource;
        if ((flags & 1) != 0)
        {
            packed = (packed & ~0xFF00u) | (uint)ReadByte() << 8;
        }
        if ((flags & 2) != 0)
        {
            packed = (packed & ~0xFF0000u) | (uint)ReadByte() << 16;
        }
        if ((flags & 4) != 0)
        {
            packed = (packed & ~0xFF000000u) | (uint)ReadByte() << 24;
        }
        if (((cameraAngle + 1024) & 4095) >= 2048)
        {
            packed |= 0x800000;
        }
        short ground = (flags & 0x20) != 0 ? short.MinValue : (short)0;
        Placement placement = ReadPlacement(flags);
        if (placement == Placement.ExplicitBone)
        {
            byte bone = suppressed ? (byte)0 : ReadByte();
            return new(packed, 3, bone, ground, null, offset);
        }
        if (placement == Placement.RandomBone)
        {
            byte bone = (selectRandomBone ?? throw new NotSupportedException("Native505900 requires its retained previous-bone and534AA0 owners."))();
            return new(packed, 3, bone, (short)(ground | 2048), null, offset);
        }
        if (placement == Placement.LocalPosition)
        {
            NativeBattleVector position = new(ReadWord(), ReadWord(), ReadWord());
            return new(packed, 4, 0, 0, position, offset);
        }
        return new(packed, 1, 0, 0, null, offset);

        byte ReadByte()
        {
            if ((uint)offset >= program.Source.Length)
            {
                throw new InvalidDataException("Native505900 packet exceeds its original sequence resource.");
            }
            return program.Source[offset++];
        }

        short ReadWord()
        {
            int low = ReadByte();
            return unchecked((short)(low | ReadByte() << 8));
        }
    }

    private static Placement ReadPlacement(byte flags)
    {
        return (flags & 8) != 0 ? Placement.ExplicitBone : (flags & 0x10) != 0 ? Placement.RandomBone : (flags & 0x40) != 0 ? Placement.LocalPosition : Placement.Model;
    }
}
