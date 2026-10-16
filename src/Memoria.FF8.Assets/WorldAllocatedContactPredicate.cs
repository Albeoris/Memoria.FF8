using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>546100/FF1A..FF1D consume the actual548CD0 primary/broad contact indices and native facing comparison.</summary>
public static class WorldAllocatedContactPredicate
{
    public static bool? Evaluate(WorldFieldSelectionInput input, ushort opcode, ushort shape, int bodyYaw, int? primaryContact, int? broadContact, int objectCount, Func<int, byte> shapeAt, Func<int, NativeWorldPathPosition> positionAt, IReadOnlyCollection<int> excludedIndices)
    {
        if (opcode is not (0xFF1A or 0xFF1B or 0xFF1C or 0xFF1D))
        {
            throw new ArgumentOutOfRangeException(nameof(opcode));
        }
        int? selected = opcode is 0xFF1A or 0xFF1B ? primaryContact : broadContact;
        if (selected is not int index)
        {
            return null;
        }
        if (index < 0)
        {
            return false;
        }
        if (index >= objectCount)
        {
            throw new InvalidDataException("World contact predicate index leaves its actual allocated object bank.");
        }
        //Native movzx operandWORD compares against movsx shapeBYTE.
        if (shape != unchecked((sbyte)shapeAt(index)) || opcode is 0xFF1B or 0xFF1D && excludedIndices.Contains(index))
        {
            return false;
        }
        if (opcode is 0xFF1A or 0xFF1B)
        {
            return true;
        }
        NativeWorldPathPosition target = positionAt(index);
        int dx = unchecked(target.X - input.X);
        int dz = unchecked(-(target.Z + input.RawZ));
        short targetYaw = unchecked((short)NativeWorldScriptedPathMath.NativeAngle(dz, dx));
        int normalizedTarget = (targetYaw % 4096 + 4096) % 4096;
        short nativeBodyYaw = unchecked((short)bodyYaw);
        int normalizedBody = (nativeBodyYaw % 4096 + 4096) % 4096;
        //558A00 returns a signed shortest delta in(-2048,2048].54682C
        //compares AX directly with512; it does not compare its magnitude.
        short difference = unchecked((short)NativeGardenMotor.Delta(normalizedTarget, normalizedBody));
        return difference <= 512;
    }
}
