using static Memoria.FF8.Runtime.NativeBattleEffect167Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect167Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect167Lower
{
    public static NativeBattleEffect167Step AdvanceStatic(NativeBattleEffect167Frame frame, uint address, NativeBattleEffect167Resources resources, NativeBattleEffect167Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool first = task.Callback == 0x5E38D0;
        if (!first && task.Callback != 0x5E3A00)
        {
            throw new InvalidDataException("Effect167 static callback requires its selected task.");
        }
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(0, 0, (short)(first ? 0x8AC : 0))) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = 288 };
        int scale = Signed(task, 0x1C);
        int other = first ? Signed(task, 0x20) : scale;
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, other, other);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        (frame, uint temporary) = Allocate(frame, 88);
        uint source = first ? 0xD6FA7Cu : 0xD7042Cu;
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(0x33u));
        frame = inputs.Execute(frame, new NativeBattleEffect167SetProjectionH(288));
        NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2);
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporary, drawn.Context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect167DrawBatch(drawn.Batch, CursorGlobal));
        frame = inputs.Execute(frame, new NativeBattleEffect167SetProjectionH(unchecked((ushort)inputs.ReadScalarFocal(frame))));
        frame = Release(frame, 88);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        int velocity = Signed(task, 0x1E);
        short next = unchecked((short)(Signed(task, 12) + 1));
        task = task.WriteWord(0x1C, unchecked((ushort)(Signed(task, 0x1C) + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 3))).WriteWord(12, unchecked((ushort)next));
        frame = Write(frame, address, task);
        return new(frame, next >= (first ? 13 : 9) ? 2 : 0);
    }
}
