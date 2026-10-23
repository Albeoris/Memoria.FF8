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
        NativeBattleBoneAnchor67E550Step result = NativeBattleBoneAnchor67E550.Query(selector, Context(frame, temporary, 20), retainedPoint8, value => inputs.ReadBoneAlias(frame, model, value), bone => inputs.ReadBoneParent(frame, model, bone), bone => inputs.ReadCurrentBoneMatrix(frame, model, bone), () => inputs.ReadModelWorld(frame, model));
        if (result.Result == 0)
        {
            frame = PublishContext(frame, temporary, result.Temporary20, 20);
        }
        return new(Release(frame, 20), result.Point8, result.Result);
    }
}
