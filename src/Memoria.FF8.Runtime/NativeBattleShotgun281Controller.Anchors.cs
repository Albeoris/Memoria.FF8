using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleShotgun281Controller
{
    public static NativeBattleShotgun281Frame UpdateMuzzle67E500(NativeBattleShotgun281Frame frame, NativeBattleShotgun281Resources resources, NativeBattleShotgun281Inputs inputs)
    {
        ReadOnlySpan<byte> point = resources.MuzzlePoint8.AsSpan();
        if (point.Length != 8)
        {
            throw new InvalidDataException("Shotgun muzzle requires its actual packed point and bone selector.");
        }
        int bone = BinaryPrimitives.ReadInt16LittleEndian(point[6..]);
        uint model = frame.State.ReadDword(0x2507F30);
        NativeBattlePackedMatrix32 matrix = inputs.ReadCurrentBoneMatrix(frame, model, bone);
        frame = LoadMatrix(frame, matrix);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Shotgun muzzle requires its current arithmetic owner."));
        NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point[2..]), BinaryPrimitives.ReadInt16LittleEndian(point[4..]));
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, vector, unchecked((short)bone)));
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        return frame with { State = frame.State.WriteDword(0x2507FA4, unchecked((uint)registers.Colors.Mac[0]!.Value)).WriteDword(0x2507FA8, unchecked((uint)registers.Colors.Mac[1]!.Value)).WriteDword(0x2507FAC, unchecked((uint)registers.Colors.Mac[2]!.Value)) };
    }

    public static NativeBattleShotgun281PointStep ReadAnchor67E550(NativeBattleShotgun281Frame frame, uint model, int selector, NativeBattleEffectArena retainedPoint8, NativeBattleShotgun281Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 20);
        int bone = selector >= 0xF0 ? inputs.ReadBoneAlias(frame, model, selector) : selector;
        if (inputs.ReadBoneParent(frame, model, bone) < 0)
        {
            return new(Release(frame, 20), retainedPoint8, 1);
        }
        NativeBattleActionMatrix current = inputs.ReadCurrentBoneMatrix(frame, model, bone).ReadMatrix();
        NativeBattleActionMatrix world = inputs.ReadModelWorld(frame, model).ReadMatrix();
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(world, current.X, current.Y, current.Z);
        NativeBattleEffectArena context = Context(frame, temporary, 20).Write(0, BitConverter.GetBytes(x)).Write(4, BitConverter.GetBytes(y)).Write(8, BitConverter.GetBytes(z));
        frame = PublishContext(frame, temporary, context, 20);
        NativeBattleEffectArena point = retainedPoint8.Write(0, BitConverter.GetBytes(unchecked((short)(x + world.X)))).Write(2, BitConverter.GetBytes(unchecked((short)(y + world.Y)))).Write(4, BitConverter.GetBytes(unchecked((short)(z + world.Z))));
        return new(Release(frame, 20), point, 0);
    }
}
