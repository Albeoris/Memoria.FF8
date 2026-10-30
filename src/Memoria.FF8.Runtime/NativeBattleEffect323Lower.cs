using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect323Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect323Lower
{
    public static NativeBattleEffect323Step Advance(NativeBattleEffect323Frame frame, uint address, NativeBattleEffect323Resources resources, NativeBattleEffect323Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (task.Callback)
        {
            case 0x65A660:
                return AdvanceSprite(frame, address, resources, inputs);
            case 0x65AA80:
                return AdvanceMesh(frame, address, resources, inputs);
            case 0x65A720:
                NativeBattleEffectArena caller = NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 0x10, 8), 0, NativeBattleEffectArena.CreateUnknown(16), 0, 8);
                caller = Put323(Put323(caller, 8, unchecked((uint)-128)), 12, checked(frame.State.ReadDword(0x25052A8) + 0x1754));
                NativeBattleEffect323Step programme = NativeBattleEffect323Programme.Advance(frame, address + 0x18, 0x270, caller, resources, inputs, inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(323, task) : null);
                return new(programme.Frame, programme.Result == 0 ? 2 : 0);
            default:
                throw new NotSupportedException("The effect requires its selected lower callback.");
        }
    }

    public static NativeBattleEffect323Frame AllMatchingContacts65A5F0(NativeBattleEffect323Frame frame, uint model, NativeBattleEffect323Inputs inputs)
    {
        int actor = unchecked((int)(model - 0x1D972C0)) / 156;
        int relative = 0;
        while (true)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("The contact walk requires its current action header.");
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
            if (relative >= header.ReadByte(16))
            {
                return frame;
            }
            int first = header.FirstContactIndex ?? throw new NotSupportedException("The contact walk requires its current first row.");
            int index = checked(first + relative);
            NativeBattleContactPacket contact = packets.Contacts[index] ?? throw new NotSupportedException("The contact walk requires its current row.");
            if (contact.Primary.ActorIndex == actor)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect323Contact506690(index));
            }
            relative++;
        }
    }

    public static NativeBattleEffect323Frame DrawMesh572200(NativeBattleEffect323Frame frame, uint temporary88, NativeBattleEffect323Resources resources, NativeBattleEffect323Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleEffectArena context = Context(frame, temporary88, 88);
        NativeBattleStaticMeshContext88Step draw = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(Dword323(context, 0)), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, pilotIdentity: pilotIdentity);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary88, draw.Context, 88);
        return inputs.Execute(frame, new NativeBattleEffect323DrawBatch(inputs.CapturePresentation ? NativeBattleEffect323Presentation.PreserveNativeDepths(draw.Batch) : draw.Batch, 0x25052A4));
    }

    public static NativeBattleEffect323Step AdvanceSprite(NativeBattleEffect323Frame frame, uint address, NativeBattleEffect323Resources resources, NativeBattleEffect323Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Setup(frame, TaskView(task, 12, 8), 2048, -512, inputs);
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord323(PutWord323(Put323(Context(frame, temporary, 180), 0, 0xFE12B4), 4, task.ReadWord(0x12)), 0x24, 0);
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(323, task) with { ResourceGeneration = Dword323(context, 0) } : null);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect323DrawBatch(inputs.CapturePresentation ? NativeBattleEffect323Presentation.PreserveNativeDepths(draw.Batch) : draw.Batch, 0x25052A4));
        frame = Release(frame, 180);
        task = Read(frame, address);
        task = task.WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) + 1)));
        if (Signed(task, 0x12) >= 10)
        {
            return new(Write(frame, address, task), 2);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(12 + axis * 2, unchecked((ushort)(task.ReadWord(12 + axis * 2) + task.ReadWord(0x14 + axis * 2))));
            task = task.WriteWord(0x14 + axis * 2, unchecked((ushort)(task.ReadWord(0x14 + axis * 2) + task.ReadWord(0x1C + axis * 2))));
        }
        return new(Write(frame, address, task), 0);
    }

    public static NativeBattleEffect323Step AdvanceMesh(NativeBattleEffect323Frame frame, uint address, NativeBattleEffect323Resources resources, NativeBattleEffect323Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), Packed323(TaskView(task, 0x10, 32)).ReadMatrix());
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        int cue = NativeBattleCameraMath.Sine(unchecked(Signed(task, 12) << 10) / 8);
        int strength = Signed(task, 14);
        if (strength < 4096)
        {
            cue = unchecked(4096 - (unchecked((4096 - cue) * strength) >> 12));
        }
        NativeBattleEffectArena context = Put323(Put323(Put323(Context(frame, temporary, 88), 0, 0xFE2974), 12, unchecked((uint)cue)), 0x1C, 0xF0);
        context = context.Write(8, new byte[] { 0, 0, unchecked((byte)Math.Min(255, (4096 - cue) >> 4)) });
        frame = PublishContext(frame, temporary, context, 88);
        frame = DrawMesh572200(frame, temporary, resources, inputs, inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(323, task) with { ResourceGeneration = Dword323(context, 0) } : null);
        frame = Release(frame, 88);
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return new(Write(frame, address, task), Signed(task, 12) < 8 ? 0 : 2);
    }

    private static NativeBattleEffect323Frame Setup(NativeBattleEffect323Frame frame, NativeBattleEffectArena point, int scale, int depth, NativeBattleEffect323Inputs inputs)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector323(point), w, unchecked((short)scale), unchecked((short)depth)) };
    }

    public static NativeBattlePackedMatrix32 Packed323(NativeBattleEffectArena source)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    public static uint Dword323(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    public static short Word323(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    public static NativeBattleVector Vector323(NativeBattleEffectArena value) => new(Word323(value, 0), Word323(value, 2), Word323(value, 4));
    public static NativeBattleEffectArena Put323(NativeBattleEffectArena value, int offset, uint number) => value.Write(offset, BitConverter.GetBytes(number));
    public static NativeBattleEffectArena PutWord323(NativeBattleEffectArena value, int offset, int number) => value.Write(offset, BitConverter.GetBytes(unchecked((short)number)));
}
