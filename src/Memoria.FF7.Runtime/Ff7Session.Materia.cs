using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private FieldHostReply AcquireMateria(FieldMateriaCommand command)
    {
        if (Progress.Materia.Length == 200)
        {
            return new(true, unchecked((int)command.Encoded));
        }
        int id = (byte)command.Encoded;
        if (id >= materiaTypes.Length)
        {
            throw new NotSupportedException("FFVII acquired materia references an absent original kernel record.");
        }
        Progress = Progress with { Materia = [.. Progress.Materia, new(id, command.Encoded >> 8)] };
        int flags = executable.NativeMateriaClasses[materiaTypes[id] & 15] == 10 ? 1 : 0;
        if (id == 44)
        {
            flags |= 2;
        }
        if (flags != 0)
        {
            byte current = Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 75);
            Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 75, (byte)(current | flags));
        }
        return new(true, -1);
    }
}
