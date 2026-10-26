using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect218PointStep(NativeBattleEffect218Frame Frame, NativeBattleVector Point);

public static partial class NativeBattleEffect218Controller
{
    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceShaft6340A0(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleEffect218Resources resources, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<NativeBattleEffect218Frame, uint, byte, short, NativeBattleEffect218PointStep> readBone502170, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        NativeBattleEffect218PointStep first = readBone502170(frame, StreamedModelAddress, 46, 3072);
        NativeBattleEffect218PointStep second = readBone502170(first.Frame, StreamedModelAddress, 52, 3072);
        NativeBattleEffect218PointStep end = readBone502170(second.Frame, StreamedModelAddress, 240, 0);
        frame = end.Frame;
        NativeBattleVector middle = new(unchecked((short)((first.Point.X + second.Point.X) / 2)), unchecked((short)((first.Point.Y + second.Point.Y) / 2)), unchecked((short)((first.Point.Z + second.Point.Z) / 2)));
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(end.Point.X - middle.X, end.Point.Y - middle.Y, end.Point.Z - middle.Z);
        ReadOnlySpan<byte> up = resources.AlignmentVector.AsSpan();
        NativeBattleModuleAxisAngle angle = NativeBattleModuleAlignment.Between571480(BinaryPrimitives.ReadInt32LittleEndian(up), BinaryPrimitives.ReadInt32LittleEndian(up[4..]), BinaryPrimitives.ReadInt32LittleEndian(up[8..]), direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(angle.Angle, angle.Axis), 0, 0, 0, 4096);
        NativeBattleModulePoolTask task = Read(frame, address);
        short worldScale = Word(frame, 0x24F3598);
        int horizontal = unchecked(Signed(task, 28) * worldScale) >> 12;
        int vertical = unchecked(Signed(task, 32) * worldScale) >> 12;
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(rotation, horizontal, vertical, horizontal) with { X = unchecked((direction.X * 600) >> 12) + middle.X, Y = unchecked((direction.Y * 600) >> 12) + middle.Y, Z = unchecked((direction.Z * 600) >> 12) + middle.Z };
        NativeBattleActionMatrix view = NativeBattleArenaPose.ReadMatrix(frame.State.ReadView(CurrentView, 32), 0);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(view, local), null));
        int v = unchecked(-Signed(task, 12) * 32);
        frame = DrawWindowed218(frame, resources.Mesh(0xE1A0F0), projection, colors, context => context.Write(0, BitConverter.GetBytes(0xE1A0F0u)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(1024)).Write(0x10, new byte[4]).Write(0x14, BitConverter.GetBytes(v)).Write(0x18, new byte[4]).Write(0x1C, BitConverter.GetBytes((short)64)).Write(0x1E, BitConverter.GetBytes((short)128)).Write(0x20, BitConverter.GetBytes(0xF3u)).Write(0x24, BitConverter.GetBytes(0x00808080u)), deliver);
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 40 ? 2 : 0);
    }
}
