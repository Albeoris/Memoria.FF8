namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSequenceParticle(uint PackedParameters, byte Placement, byte Bone, short Fraction, NativeBattleVector? Position, int NextOffset);

/// <summary>505900's shared model-sequence particle packet and operand cursor.</summary>
public static class NativeBattleSequenceParticles
{
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
        if ((flags & 8) != 0)
        {
            byte bone = suppressed ? (byte)0 : ReadByte();
            return new(packed, 3, bone, ground, null, offset);
        }
        if ((flags & 0x10) != 0)
        {
            byte bone = (selectRandomBone ?? throw new NotSupportedException("Native505900 requires its retained previous-bone and534AA0 owners."))();
            return new(packed, 3, bone, (short)(ground | 2048), null, offset);
        }
        if ((flags & 0x40) != 0)
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
}
