using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect227Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect227Lower
{
    public const uint ArenaGlobal = 0x2508768;
    public const uint CursorGlobal = 0x2508708;

    public static NativeBattleEffect227Step Advance(NativeBattleEffect227Frame frame, uint address, NativeBattleEffect227Resources resources, NativeBattleEffect227Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x694360)
        {
            throw new NotSupportedException("The effect requires its selected lower task callback.");
        }
        (frame, uint temporary) = Allocate(frame, 88);
        int age = Signed(task, 12);
        int phase = ((age << 12) / 16) >> 2;
        int sine = NativeBattleCameraMath.Sine(phase);
        short diagonalX = unchecked((short)(sine + 4096));
        diagonalX = unchecked((short)(diagonalX + (diagonalX >> 1)));
        NativeBattleActionMatrix diagonal = new([diagonalX, 0, 0, 0, unchecked((short)(sine * 2)), 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix yaw = NativeBattlePandemona291Restoration.Rotation701270(Signed(task, 16));
        NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Multiply56C090(yaw, diagonal);
        NativeBattleActionMatrix roll = NativeBattleGfMatrices.Euler701310(new(0, 0, Signed(task, 14)));
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(scaled, roll);
        NativeBattleEffectArena anchor = ReadBytes(frame, 0x2508758, 6);
        local = local with { X = SignedWord(anchor, 0), Y = SignedWord(anchor, 2) - 200, Z = SignedWord(anchor, 4) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        int fade = age > 10 ? ((age - 10) << 12) / 6 : age < 3 ? (3 - age) << 10 : 0;
        NativeBattleEffectArena context = Put(Put(Put(Put(Put(Context(frame, temporary, 88), 0, 0x112DAA8), 8, 0), 12, unchecked((uint)fade)), 0x18, unchecked((uint)-128)), 0x1C, 0xF3);
        frame = PublishContext(frame, temporary, context, 88);
        NativeBattleDefaultActMeshStep draw = NativeBattleDefaultActMesh.Draw(frame.Memory, resources.ReadMesh(0x112DAA8).Bytes, 0x112DAA8, 0, context, inputs.ReadProjection(frame), inputs.Colors, tableBaseBucket: 17, depthShift: 2);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context88, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect227DrawBatch(draw.Batch, CursorGlobal));
        frame = Release(frame, 88);
        task = Read(frame, address);
        if (Signed(task, 12) >= 16)
        {
            return new(frame, 2);
        }
        task = task.WriteWord(16, unchecked((ushort)(Signed(task, 16) + Signed(task, 18))));
        task = task.WriteWord(14, unchecked((ushort)(Signed(task, 14) + Signed(task, 20))));
        task = task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1)));
        return new(Write(frame, address, task), 0);
    }

    public static uint Dword(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    public static short SignedWord(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    public static NativeBattleVector Vector(NativeBattleEffectArena value) => new(SignedWord(value, 0), SignedWord(value, 2), SignedWord(value, 4));
    public static NativeBattleEffectArena Put(NativeBattleEffectArena value, int offset, uint number) => value.Write(offset, BitConverter.GetBytes(number));
    public static NativeBattleEffectArena PutWord(NativeBattleEffectArena value, int offset, int number) => value.Write(offset, BitConverter.GetBytes(unchecked((short)number)));
    public static NativeBattleEffectArena Store(NativeBattleEffectArena value, NativeBattleVector point) => PutWord(PutWord(PutWord(value, 0, point.X), 2, point.Y), 4, point.Z);
}
