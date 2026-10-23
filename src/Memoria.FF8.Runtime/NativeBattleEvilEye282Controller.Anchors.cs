using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEvilEye282Controller
{
    public static NativeBattleEvilEye282MuzzleStep UpdateMuzzle67DAC0(NativeBattleEvilEye282Frame frame, NativeBattleEvilEye282Resources resources, NativeBattleEvilEye282Inputs inputs)
    {
        ReadOnlySpan<byte> point = resources.SourcePoint8.AsSpan();
        if (point.Length != 8)
        {
            throw new InvalidDataException("Evil-Eye muzzle requires its actual packed point and bone selector.");
        }
        int bone = BinaryPrimitives.ReadInt16LittleEndian(point[6..]);
        uint model = frame.State.ReadDword(0x2507E98);
        NativeBattleActionMatrix world = inputs.ReadModelWorld(frame, model).ReadMatrix();
        NativeBattleActionMatrix current = inputs.ReadCurrentBoneMatrix(frame, model, bone).ReadMatrix();
        NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(world, current), null);
        frame = LoadMatrix(frame, composed);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Evil-Eye muzzle requires its current arithmetic owner."));
        NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point[2..]), BinaryPrimitives.ReadInt16LittleEndian(point[4..]));
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, vector, unchecked((short)bone)));
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        return new(frame, registers.Colors.Mac[0]!.Value, registers.Colors.Mac[1]!.Value, registers.Colors.Mac[2]!.Value);
    }

    public static NativeBattleEvilEye282PointStep ReadAnchor67E550(NativeBattleEvilEye282Frame frame, uint model, int selector, NativeBattleEffectArena retainedPoint8, NativeBattleEvilEye282Inputs inputs)
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
