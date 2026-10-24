using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect210Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect210Lower
{
    public static NativeBattleEffect210Step Advance(NativeBattleEffect210Frame frame, uint address, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs) => Read(frame, address).Callback switch
    {
        0x6F6C00 or 0x6F6DA0 or 0x6F7130 or 0x6F7230 => AdvanceParticles(frame, address, resources, inputs),
        0x6F82D0 => AdvanceRibbon(frame, address, resources, inputs),
        0x6F9AA0 => NativeBattleEffect210Programme.Advance9AA0(frame, address, resources, inputs),
        0x6F9B60 or 0x6F9C60 => AdvanceStatic(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect210 requires its selected lower callback.")
    };

    public static NativeBattleEffect210Frame InitializeEcho6A1230(NativeBattleEffect210Frame frame, uint address)
    {
        NativeBattleEffectArena header = ReadBytes(frame, address, 136);
        header = Put(header, 0, 0).Write(4, [1]).Write(5, Enumerable.Repeat((byte)255, 128).ToArray()).Write(6, [0]);
        return WriteBytes(frame, address, header, 136);
    }

    public static NativeBattleEffect210Frame InitializeRibbon82A0(NativeBattleEffect210Frame frame, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        (frame, uint address) = Append(frame, EchoDescriptor, 0x6F82D0);
        if (address == 0)
        {
            throw new InvalidDataException("Effect210 ribbon factory requires its available selected slot.");
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, 0);
        for (int index = 0; index < 7; index++)
        {
            task = task.WriteDword(0x10 + index * 20, 0);
        }
        return Write(frame, address, task);
    }

    public static bool Paused(NativeBattleEffect210Frame frame) => frame.State.ReadDword(FrozenGlobal) != 0;
    public static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    public static short SignedWord(NativeBattleEffectArena arena, int offset) => unchecked((short)Word(arena, offset));
    public static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    public static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    public static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    public static NativeBattleEffect210Frame LoadCpuMatrix(NativeBattleEffect210Frame frame, NativeBattleActionMatrix matrix) => LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));

    public static NativeBattleEffect210Frame DrawSprite(NativeBattleEffect210Frame frame, uint temporary, Func<uint, NativeBattleModuleResource> readResource, NativeBattleEffect210Inputs inputs)
    {
        NativeBattleModuleSpriteDraw result = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, Context(frame, temporary, 180), readResource, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2);
        frame = frame with { Memory = result.Memory };
        frame = PublishContext(frame, temporary, result.Temporary180, 180);
        return inputs.Execute(frame, new NativeBattleEffect210DrawBatch(result.Batch, CursorGlobal));
    }

    public static NativeBattleEffect210Frame DrawStatic88(NativeBattleEffect210Frame frame, uint temporary, uint source, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), Context(frame, temporary, 88), inputs.Colors, 17, 2);
        frame = frame with { Memory = result.Memory };
        frame = PublishContext(frame, temporary, result.Context, 88);
        return inputs.Execute(frame, new NativeBattleEffect210DrawBatch(result.Batch, CursorGlobal));
    }

    private static NativeBattleEffect210Step AdvanceStatic(NativeBattleEffect210Frame frame, uint address, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback == 0x6F9C60)
        {
            frame = NativeBattleEffect210Particles.PrepareSprite571BC0(frame, TaskView(task, 12, 8), 4096, -1024, inputs);
            (frame, uint temporary) = Allocate(frame, 180);
            NativeBattleEffectArena context = Put(Context(frame, temporary, 180), 0, 0x145D4EC);
            context = PutWord(PutWord(context, 4, Signed(task, 0x12)), 0x24, 0);
            frame = PublishContext(frame, temporary, context, 180);
            frame = DrawSprite(frame, temporary, resources.Sprite, inputs);
            frame = Release(frame, 180);
            if (Paused(frame))
            {
                return new(frame, 0);
            }
            task = Read(frame, address);
            short next = unchecked((short)(Signed(task, 0x12) + 1));
            task = task.WriteWord(14, unchecked((ushort)(Signed(task, 14) + Signed(task, 0x14)))).WriteWord(0x12, unchecked((ushort)next));
            return new(Write(frame, address, task), next >= 16 ? 2 : 0);
        }
        (frame, uint meshTemporary) = Allocate(frame, 152);
        NativeBattleEffectArena meshContext = Context(frame, meshTemporary, 152);
        NativeBattleActionMatrix basis = NativeBattleDuel215Controller.ParticleBasis6ED1E0(unchecked((short)((sbyte)task.ReadByte(0x17) << 4)), Signed(task, 0x1A));
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(unchecked((short)((sbyte)task.ReadByte(0x16) << 4)), 0, 0));
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Compose56C2F0(rotation, basis) with { X = Signed(task, 12), Y = Signed(task, 14), Z = Signed(task, 16) };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        meshContext = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(basis, 0)), 0, meshContext, 0x78, 20);
        meshContext = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(composed, null)), 0, meshContext, 0x58, 32);
        meshContext = Put(Put(meshContext, 0, 0x1461D98), 0x1C, 0);
        frame = PublishContext(frame, meshTemporary, meshContext, 152);
        frame = LoadCpuMatrix(frame, composed);
        frame = DrawStatic88(frame, meshTemporary, 0x1461D98, resources, inputs);
        frame = Release(frame, 152);
        if (Paused(frame))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        short lifetime = unchecked((short)(Signed(task, 0x14) - 1));
        task = task.WriteWord(0x14, unchecked((ushort)lifetime));
        if (lifetime == 0)
        {
            return new(Write(frame, address, task), 2);
        }
        task = task.WriteByte(0x16, unchecked((byte)(task.ReadByte(0x16) + task.ReadByte(0x18)))).WriteByte(0x17, unchecked((byte)(task.ReadByte(0x17) + task.ReadByte(0x19))));
        task = task.WriteWord(14, unchecked((ushort)(Signed(task, 14) + Signed(task, 0x12)))).WriteWord(0x12, unchecked((ushort)(Signed(task, 0x12) + 20)));
        return new(Write(frame, address, task), 0);
    }
}
