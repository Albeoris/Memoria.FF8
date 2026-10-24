using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect287Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect287Lower
{
    public static NativeBattleEffect287Step Advance(NativeBattleEffect287Frame frame, uint address, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x6F3B80 => AdvanceSprite(frame, address, resources, inputs),
            0x6F3C20 => AdvanceDebris(frame, address, resources, inputs),
            0x6F3D00 => AdvanceTarget(frame, address, resources, inputs),
            0x6F3DF0 => NativeBattleEffect287Attachments.Advance(frame, address, resources, inputs),
            _ => throw new NotSupportedException("The effect requires its selected lower callback.")
        };
    }

    public static NativeBattleEffect287Step AdvanceSprite(NativeBattleEffect287Frame frame, uint address, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short scale = Signed(task, 16);
        short depth = unchecked((short)-(unchecked((int)task.ReadDword(16)) >> 2));
        short w = Signed(task, 26);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 20), Signed(task, 22), Signed(task, 24)), w, scale, depth) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord287(PutWord287(Put287(Context(frame, temporary, 180), 0, task.ReadDword(12)), 4, task.ReadWord(26)), 36, 0);
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleModuleSpriteDraw result = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2);
        frame = PublishContext(frame with { Memory = result.Memory }, temporary, result.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect287DrawBatch(result.Batch, 0x2556374));
        frame = Release(frame, 180);
        task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(20 + axis * 2, unchecked((ushort)(Signed(task, 20 + axis * 2) + Signed(task, 28 + axis * 2))));
        }
        short next = unchecked((short)(Signed(task, 26) + 1));
        task = task.WriteWord(26, unchecked((ushort)next));
        return new(Write(frame, address, task), next >= Signed(task, 34) ? 2 : 0);
    }

    public static NativeBattleEffect287Step AdvanceDebris(NativeBattleEffect287Frame frame, uint address, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(new(Signed(task, 24), Signed(task, 26), Signed(task, 28))) with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(composed, null));
        NativeBattleEffectArena context = Put287(Put287(Context(frame, temporary, 88), 0, 0x13EF72C), 28, 0);
        frame = PublishContext(frame, temporary, context, 88);
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(0x13EF72C), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2);
        frame = PublishContext(frame with { Memory = result.Memory }, temporary, result.Context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect287DrawBatch(result.Batch, 0x2556374));
        frame = Release(frame, 88);
        task = Read(frame, address);
        short velocity = unchecked((short)(Signed(task, 22) + 10));
        task = task.WriteWord(22, unchecked((ushort)velocity)).WriteWord(24, unchecked((ushort)(Signed(task, 24) + Signed(task, 30)))).WriteWord(20, unchecked((ushort)(Signed(task, 20) + 400))).WriteWord(18, unchecked((ushort)(Signed(task, 18) + velocity)));
        if (Signed(task, 18) > 0)
        {
            task = task.WriteWord(22, unchecked((ushort)-velocity));
        }
        short age = unchecked((short)(Signed(task, 12) + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)age))), age >= 10 ? 2 : 0);
    }

    public static NativeBattleEffect287Step AdvanceTarget(NativeBattleEffect287Frame frame, uint address, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(0) with { X = Signed(task, 12), Y = Signed(task, 14), Z = Signed(task, 16) };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(composed, null)).MarkUnknown(34, 1).Write(32, BitConverter.GetBytes((short)-128)).Write(35, [0]).Write(36, BitConverter.GetBytes(checked(frame.State.ReadDword(0x2556380) + 0x1A18)));
        NativeBattleEffect287Step result = NativeBattleEffect287Programme.Advance(frame, address + 20, resources.WorkspaceLength(0x13F8F5C), caller, resources, inputs);
        return new(result.Frame, result.Result == 0 ? 2 : 0);
    }

    public static short Word287(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.Read(offset, 2));
    public static uint Dword287(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Read(offset, 4));
    public static NativeBattleEffectArena Put287(NativeBattleEffectArena bytes, int offset, uint number) => bytes.Write(offset, BitConverter.GetBytes(number));
    public static NativeBattleEffectArena PutWord287(NativeBattleEffectArena bytes, int offset, int number) => bytes.Write(offset, BitConverter.GetBytes(unchecked((short)number)));
}
