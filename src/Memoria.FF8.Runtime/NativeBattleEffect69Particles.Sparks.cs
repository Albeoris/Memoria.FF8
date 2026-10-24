using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect69Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect69Particles
{
    public static NativeBattleEffect69Frame DrawSparks7001E0(NativeBattleEffect69Frame frame, NativeBattleEffect69Resources resources, NativeBattleEffect69Inputs inputs)
    {
        (frame, uint temporaryAddress) = Allocate(frame, 180);
        NativeBattleEffectArena temporary = ReadBytes(frame, temporaryAddress, 180);
        foreach (int offset in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            temporary = Word(temporary, offset, 0);
        }
        uint arena = frame.State.ReadDword(ArenaGlobal);
        for (int index = 0; index < 128; index++)
        {
            NativeBattleEffectArena record = ReadBytes(frame, arena + 0x8020 + (uint)index * 16, 16);
            sbyte life = unchecked((sbyte)record.Read(15, 1)[0]);
            if (life == 0)
            {
                continue;
            }
            uint source = BinaryPrimitives.ReadUInt32LittleEndian(record.Read(0, 4));
            if (source != resources.Sprite.Address)
            {
                throw new NotSupportedException("Effect69 spark selected an unsupported current sprite source.");
            }
            short scale = BinaryPrimitives.ReadInt16LittleEndian(record.Read(10, 2));
            frame = LoadMatrix(frame, inputs.ReadView(frame));
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(SlicePoint(record, 4)), scale));
            temporary = temporary.Write(0x84, new byte[20]);
            temporary = Word(Word(Word(temporary, 0x84, scale), 0x8C, scale), 0x94, scale);
            for (int axis = 0; axis < 3; axis++)
            {
                temporary = Dword(temporary, 0x98 + axis * 4, unchecked((uint)registers.Colors.Mac[axis]!.Value));
            }
            temporary = Dword(temporary, 0, source);
            int frameCount = BinaryPrimitives.ReadInt16LittleEndian(resources.Sprite.Bytes.AsSpan().Slice(8, 2));
            int selectedIndex = frameCount - (life - 1) % frameCount;
            int selected = BinaryPrimitives.ReadInt16LittleEndian(resources.Sprite.Bytes.AsSpan().Slice(8 + selectedIndex * 2, 2));
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(resources.Sprite.Bytes.AsSpan().Slice(selected, 4));
            temporary = Dword(Dword(temporary, 0x2C, unchecked(source + (uint)selected)), 0x30, count);
            if (unchecked((int)count) < 0)
            {
                temporary = temporary.Write(0x25, [(byte)(temporary.Read(0x25, 1)[0] | 1)]);
                temporary = Dword(temporary, 0x30, count & 0x7FFFFFFF);
            }
            temporary = Dword(temporary, 0x2C, unchecked(source + (uint)selected + 4));
            frame = Publish(frame, registers);
            NativeBattleShockwavePulsar260PuffStep draw = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(frame.Memory, temporary, resources.Sprite, inputs.ReadProjection(frame));
            frame = WriteBytes(frame with { Memory = draw.Memory }, temporaryAddress, draw.Temporary180, 180);
            frame = inputs.Execute(frame, new NativeBattleEffect69DeliverCommonBatch(draw.Batch));
            temporary = ReadBytes(frame, temporaryAddress, 180);
        }
        frame = WriteBytes(frame, temporaryAddress, temporary, 180);
        return Release(frame, 180);
    }

    private static NativeBattleEffectArena SlicePoint(NativeBattleEffectArena source, int offset) => NativeBattleEffectNodeContextMemory.Copy(source, offset, NativeBattleEffectArena.Clear(), 0, 8);
    private static NativeBattleEffectArena Word(NativeBattleEffectArena source, int offset, short value) => source.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena source, int offset, uint value) => source.Write(offset, BitConverter.GetBytes(value));
}
