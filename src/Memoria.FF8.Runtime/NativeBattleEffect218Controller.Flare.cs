using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceFlare6338B0(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleEffect218Resources resources, NativeBattlePackedMatrix32 currentView, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<NativeBattleEffect218Frame, uint, byte, short, NativeBattleEffect218PointStep> readBone502170, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        uint target = ModelAddress(checked((int)frame.State.ReadDword(TargetGlobal)));
        NativeBattleEffect218PointStep sampled = readBone502170(frame, target, 240, 4096);
        frame = sampled.Frame;
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(16, unchecked((ushort)sampled.Point.X)).WriteWord(18, unchecked((ushort)sampled.Point.Y)).WriteWord(20, unchecked((ushort)sampled.Point.Z));
        frame = Write(frame, address, task);
        NativeBattleVector anchor = new(Word(frame, NativeBattleEffect218State.FlareAnchorAddress), Word(frame, NativeBattleEffect218State.FlareAnchorAddress + 2), Word(frame, NativeBattleEffect218State.FlareAnchorAddress + 4));
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(anchor.X - sampled.Point.X, anchor.Y - sampled.Point.Y, anchor.Z - sampled.Point.Z);
        ReadOnlySpan<byte> up = resources.UpVector.AsSpan();
        NativeBattleModuleAxisAngle angle = NativeBattleModuleAlignment.Between571480(BinaryPrimitives.ReadInt32LittleEndian(up), BinaryPrimitives.ReadInt32LittleEndian(up[4..]), BinaryPrimitives.ReadInt32LittleEndian(up[8..]), direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(angle.Angle, angle.Axis), 0, 0, 0, 4096);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(rotation, 6144, 6144, 13312) with { X = (unchecked(direction.X * -2000) >> 12) + anchor.X, Y = (unchecked(direction.Y * -2000) >> 12) + anchor.Y, Z = (unchecked(direction.Z * -2000) >> 12) + anchor.Z };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(currentView.ReadMatrix(), local), null));
        int v = unchecked(-Signed(task, 12) * 24);
        frame = DrawWindowed218(frame, resources.Mesh(0xE23660), projection, colors, context => context.Write(0, BitConverter.GetBytes(0xE23660u)).Write(8, new byte[4]).Write(0x10, new byte[4]).Write(0x14, BitConverter.GetBytes(v)).Write(0x18, BitConverter.GetBytes((short)64)).Write(0x1A, BitConverter.GetBytes((short)128)).Write(0x1C, BitConverter.GetBytes((short)64)).Write(0x1E, BitConverter.GetBytes((short)128)).Write(0x20, BitConverter.GetBytes(0x33u)).Write(0x24, BitConverter.GetBytes(0x00808080u)), deliver);
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 22 ? 2 : 0);
    }
}
