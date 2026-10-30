using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66Clouds
{
    private static NativeBattleEffect66Step AdvanceScrolling(NativeBattleEffect66Frame frame, uint task, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        (frame, uint outerAddress) = Allocate(frame, 104);
        NativeBattleEffectArena outer = Context(frame, outerAddress, 104);
        int age = unchecked((short)Read(frame, task).ReadWord(12));
        if (!Paused(frame, inputs) && age <= 120 && age % 16 == 0)
        {
            uint address = Free(frame, FirstPointAddress, 100);
            if (address != 0)
            {
                NativeBattleEffectArena row = Word(Put(ReadBytes(frame, address, 24), 0, 2), 4, 0);
                frame = Random(frame, out int random);
                row = Word(row, 6, random % 0x1400 + 0x600);
                frame = Random(frame, out random);
                row = Word(row, 8, unchecked((int)frame.State.ReadDword(CenterAddress)) + random % 6000 - 3000);
                row = Word(row, 10, 0);
                frame = Random(frame, out random);
                row = Word(row, 12, unchecked((int)frame.State.ReadDword(CenterAddress + 4)) + random % 6000 - 3000);
                frame = Random(frame, out random);
                row = Word(Word(row, 14, random & 4095), 16, 0);
                frame = Random(frame, out random);
                int speed = (random & 15) + 4;
                row = Word(Word(row, 18, speed), 22, speed);
                frame = WriteBytes(frame, address, row, 24);
            }
        }
        (frame, uint contextAddress) = Allocate(frame, 144);
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        outer = NativeBattleEffectNodeContextMemory.Copy(PackedArena(view), 0, outer, 8, 32);
        outer = Word(Word(Put(outer, 0x4C, 0x3000), 4, 0), 0, 0);
        NativeBattleEffectArena context = Context(frame, contextAddress, 144);
        context = Put(Put(Put(context, 0, 0xDE97A4), 8, 0), 0x10, 0);
        context = Word(Word(Word(Word(context, 0x18, 0), 0x1A, 128), 0x1C, 64), 0x1E, 128);
        for (int index = 0; index < 100; index++)
        {
            uint address = checked(FirstPointAddress + (uint)(index * 24));
            NativeBattleEffectArena row = ReadBytes(frame, address, 24);
            if ((U32(row, 0) & 2) == 0)
            {
                continue;
            }
            outer = Word(outer, 2, S16(row, 14));
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, S16(row, 14), 0)) with { X = S16(row, 8), Y = S16(row, 10), Z = S16(row, 12) };
            outer = Translation(Rotation(outer, 0x28, local), 0x28, local.X, local.Y, local.Z);
            int scale = S16(row, 6);
            outer = Put(Put(outer, 0x48, scale), 0x50, scale);
            local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, 0x3000, scale);
            NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattlePoseMatrices.Compose56C2F0(view.ReadMatrix(), local), null);
            outer = NativeBattleEffectNodeContextMemory.Copy(PackedArena(composed), 0, outer, 0x28, 32);
            frame = LoadMatrix(frame, composed);
            int phase = S16(row, 4);
            context = Put(context, 0x20, 0x33);
            if (phase < 16)
            {
                context = Put(Put(context, 12, (16 - phase) << 8), 0x20, 0xF3);
            }
            context = Put(context, 0x14, S16(row, 18));
            context = Put(context, 0x24, unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(0xDEA424 + (uint)(S16(row, 16) * 4), 4))));
            frame = PublishContext(PublishContext(frame, outerAddress, outer, 104), contextAddress, context, 144);
            frame = NativeBattleEffect66Drawing.DrawWindowed144(frame, contextAddress, 0xDE97A4, 2, 0x1D8E054, resources, inputs, NativeBattleEffect66VisualIdentity.Point(frame, address, 0xDE97A4));
            context = Context(frame, contextAddress, 144);
            if (!Paused(frame, inputs))
            {
                row = ReadBytes(frame, address, 24);
                int colorIndex = S16(row, 16) + 1;
                row = Word(row, 16, colorIndex >= 12 ? 0 : colorIndex);
                row = Word(row, 18, S16(row, 18) + S16(row, 22));
                row = Word(row, 4, S16(row, 4) + 1);
                frame = WriteBytes(frame, address, row, 24);
            }
        }
        frame = PublishContext(frame, outerAddress, outer, 104);
        frame = Release(Release(frame, 144), 104);
        return FinishTimed(frame, task, 140, FirstPointAddress, 100, 2, inputs);
    }
}
