using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Step AdvanceModelOverlay64C060(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        if ((frame.State.ReadDword(0x24FD50C) & 1) != 0)
        {
            return new(frame, 2);
        }
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0x400, 0, 0x800));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, 0x1A00, 0x1A00, 0x1A00);
        NativeBattleActionMatrix bone = NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, 0x24FD918).ReadMatrix(), ReadPacked(frame, 0x24FE8C0).ReadMatrix());
        local = NativeBattleGfMatrices.Compose56C2F0(bone, local);
        frame = StoreOverlayPoint(frame, 0x24FD980, local, new(0, 1000, -1000));
        frame = StoreOverlayPoint(frame, 0x24FD988, local, new(0, 6200, 600));
        NativeBattleActionMatrix modelView = NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, ViewMatrix).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(modelView, null));
        frame = ProjectOverlayPoint(frame, 0x24FD980, inputs);
        int firstDepth = unchecked((int)(Registers(frame).Geometry.Otz ?? throw new NotSupportedException("Effect187 overlay requires its retained OTZ before the first projection.")));
        frame = ProjectOverlayPoint(frame, 0x24FD988, inputs);
        int secondDepth = unchecked((int)(Registers(frame).Geometry.Otz ?? throw new NotSupportedException("Effect187 overlay requires its retained OTZ before the second projection.")));
        int tableBase = Math.Max(Math.Min(firstDepth, secondDepth) / 4 - 8, 0);
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 12) > 0)
        {
            (frame, uint temporary) = Allocate(frame, 88);
            NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xE41B28u)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(0x33u));
            NativeBattleDefaultActMeshStep drawn = NativeBattleEffect187MeshContext88.DrawLoaded(frame.Memory, resources.Mesh(0xE41B28), inputs.ReadProjection(frame), context, inputs.Colors, 14, tableBaseBucket: tableBase);
            frame = frame with { Memory = drawn.Memory };
            frame = PublishContext(frame, temporary, drawn.Context88, 88);
            frame = inputs.Execute(frame, new NativeBattleEffect187DeliverBatch(drawn.Batch, 0x1D8E054));
            frame = Release(frame, 88);
        }
        if ((inputs.ReadFlags(frame) & 0x201) == 0)
        {
            uint control = frame.State.ReadDword(0x24FD93C);
            uint skeleton = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, control, 4).Read(0, 4));
            frame = WriteBytes(frame, 0x24FE8C0, ReadBytes(frame, checked(skeleton + 0xB30), 32), 32);
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        }
        return new(frame, 0);
    }

    private static NativeBattleEffect187Frame StoreOverlayPoint(NativeBattleEffect187Frame frame, uint address, NativeBattleActionMatrix matrix, NativeBattleVector offset)
    {
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(matrix, offset);
        NativeBattleEffectArena point = ReadBytes(frame, address, 8).Write(0, BitConverter.GetBytes(unchecked((short)(rotated.X + matrix.X)))).Write(2, BitConverter.GetBytes(unchecked((short)(rotated.Y + matrix.Y)))).Write(4, BitConverter.GetBytes(unchecked((short)(rotated.Z + matrix.Z))));
        return WriteBytes(frame, address, point, 8);
    }

    private static NativeBattleEffect187Frame ProjectOverlayPoint(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleEffectArena point = ReadBytes(frame, address, 8);
        NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, vector, Padding(point, 6));
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect187 overlay requires its actual projection controls.");
        return Publish(frame, NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb));
    }
}
