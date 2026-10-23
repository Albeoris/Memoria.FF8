using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMiniPulseCannon286Controller
{
    public static NativeBattleMiniPulseCannon286MuzzleStep UpdateMuzzle67A770(NativeBattleMiniPulseCannon286Frame frame, NativeBattleMiniPulseCannon286Resources resources, NativeBattleMiniPulseCannon286Inputs inputs)
    {
        ReadOnlySpan<byte> point = resources.SourcePoint8.AsSpan();
        if (point.Length != 8)
        {
            throw new InvalidDataException("Mini Pulse Cannon requires its actual packed muzzle point.");
        }
        int bone = BinaryPrimitives.ReadInt16LittleEndian(point[6..]);
        uint model = frame.State.ReadDword(0x2507CB0);
        frame = LoadMatrix(frame, inputs.ReadCurrentBoneMatrix(frame, model, bone));
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Mini Pulse Cannon muzzle requires its current arithmetic owner."));
        NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point[2..]), BinaryPrimitives.ReadInt16LittleEndian(point[4..]));
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, vector, unchecked((short)bone)));
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        return new(frame, registers.Colors.Mac[0]!.Value, registers.Colors.Mac[1]!.Value, registers.Colors.Mac[2]!.Value);
    }
}
