using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Step AdvanceRibbons64C330(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        uint scale = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(0xE41E5Cu + (uint)(age * 4)), 4));
        (frame, uint temporary) = Allocate(frame, 64);
        NativeBattleEffectArena context = Context(frame, temporary, 64);
        NativeBattleEffectArena endpoints = ReadBytes(frame, 0x24FD980, 16);
        int x = Word(endpoints, 8) - Word(endpoints, 0);
        int y = Word(endpoints, 10) - Word(endpoints, 2);
        int z = Word(endpoints, 12) - Word(endpoints, 4);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        context = context.Write(0x30, BitConverter.GetBytes(direction.X)).Write(0x34, BitConverter.GetBytes(direction.Y)).Write(0x38, BitConverter.GetBytes(direction.Z));
        int distance = unchecked(NativeBattleRayBomb10Geometry.Sqrt56BEC0(NativeBattleSleepingGas168.NormalizeSquare(x, y, z)) * (int)scale) >> 12;
        context = context.Write(0x28, BitConverter.GetBytes(unchecked((short)(Word(endpoints, 0) + (unchecked(direction.X * distance) >> 12))))).Write(0x2A, BitConverter.GetBytes(unchecked((short)(Word(endpoints, 2) + (unchecked(direction.Y * distance) >> 12))))).Write(0x2C, BitConverter.GetBytes(unchecked((short)(Word(endpoints, 4) + (unchecked(direction.Z * distance) >> 12)))));
        int ring = age % 10;
        if (ring < 0)
        {
            throw new InvalidDataException("Effect187 ribbon requires its live nonnegative history cursor.");
        }
        NativeBattleEffectArena history = ReadBytes(frame, 0x24FE7B8, 200).Write(ring * 20, BitConverter.GetBytes(1u));
        history = CopyBytes(endpoints, 0, history, ring * 20 + 4, 8);
        history = CopyBytes(context, 0x28, history, ring * 20 + 12, 8);
        NativeBattleEffectArena firstControls = ReadBytes(frame, 0x24FD458, 80);
        NativeBattleEffectArena secondControls = ReadBytes(frame, 0x24FD4B0, 80);
        int count = 0;
        for (; count < 10; count++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(history.Read(ring * 20, 4)) == 0)
            {
                break;
            }
            firstControls = CopyBytes(history, ring * 20 + 4, firstControls, count * 8, 8);
            secondControls = count == 0 ? CopyBytes(endpoints, 8, secondControls, 0, 8) : CopyBytes(history, ring * 20 + 12, secondControls, count * 8, 8);
            ring = ring == 0 ? 9 : ring - 1;
        }
        frame = WriteBytes(frame, 0x24FE7B8, history, 200);
        frame = WriteBytes(frame, 0x24FD458, firstControls, 80);
        frame = WriteBytes(frame, 0x24FD4B0, secondControls, 80);
        if (count > 1)
        {
            (frame, uint workAddress) = Allocate(frame, 400);
            NativeBattleEffectArena work = Context(frame, workAddress, 400);
            for (int side = 0; side < 2; side++)
            {
                work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, side == 0 ? firstControls : secondControls, 0, work);
                uint destination = side == 0 ? 0x24FD6D8u : 0x24FD7D8u;
                NativeBattleEffectArena samples = ReadBytes(frame, destination, 256);
                for (int sample = 0; sample < 32; sample++)
                {
                    samples = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, sample * 4096 / 31, samples, sample * 8);
                }
                frame = WriteBytes(frame, destination, samples, 256);
                frame = PublishContext(frame, workAddress, work, 400);
            }
            frame = Release(frame, 400);
            frame = LoadMatrix(frame, ReadPacked(frame, ViewMatrix));
            NativeBattleRibbonPair72Step drawn = NativeBattleRibbonPair72.DrawLoaded(frame.Memory, ReadBytes(frame, 0x24FD6D8, 256), ReadBytes(frame, 0x24FD7D8, 256), context, inputs.ReadProjection(frame), inputs.Colors, 0x3A903030);
            frame = frame with { Memory = drawn.Memory };
            context = drawn.Temporary64;
            frame = PublishContext(frame, temporary, context, 64);
            frame = inputs.Execute(frame, new NativeBattleEffect187DeliverBatch(drawn.Batch, 0x1D8E054));
        }
        else
        {
            frame = PublishContext(frame, temporary, context, 64);
        }
        frame = Release(frame, 64);
        return IncrementMeshClock(frame, address, inputs, 6);

        static short Word(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
    }
}
