using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66Clouds
{
    private static NativeBattleEffect66Step AdvanceGroundStreaks(NativeBattleEffect66Frame frame, uint task, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        (frame, uint outerAddress) = Allocate(frame, 104);
        NativeBattleEffectArena outer = Context(frame, outerAddress, 104);
        int age = unchecked((short)Read(frame, task).ReadWord(12));
        if (!Paused(frame, inputs) && age <= 110)
        {
            uint address = Free(frame, FirstPointAddress, 100);
            if (address != 0)
            {
                NativeBattleEffectArena row = Word(Put(ReadBytes(frame, address, 24), 0, 1), 4, 0);
                frame = Random(frame, out int random);
                row = Word(row, 6, random % 0x2800 + 0x3000);
                frame = Random(frame, out random);
                row = Word(Word(row, 8, random % 12), 10, 0);
                frame = Random(frame, out random);
                int phase = random & 4095;
                row = Word(row, 12, phase);
                frame = Random(frame, out random);
                row = Word(row, 18, -8 - random % 24);
                frame = Random(frame, out random);
                int velocity = random % 20 + 5;
                row = Word(row, 20, (phase & 1) != 0 ? -velocity : velocity);
                frame = WriteBytes(frame, address, row, 24);
            }
        }
        (frame, uint contextAddress) = Allocate(frame, 108);
        NativeBattleEffectArena center = frame.State.ReadView(CenterAddress, 8);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Point(center, 0), OptionalWord(center, 6), 4096, -1000) };
        outer = Word(Word(outer, 0, 0), 2, 0);
        outer = Put(Put(outer, 0x4C, 0x2800), 0x50, 0x2800);
        NativeBattleEffectArena context = Put(Put(Context(frame, contextAddress, 108), 0, 0xDE90CC), 8, 0);
        for (int index = 0; index < 100; index++)
        {
            uint address = checked(FirstPointAddress + (uint)(index * 24));
            NativeBattleEffectArena row = ReadBytes(frame, address, 24);
            if ((U32(row, 0) & 1) == 0)
            {
                continue;
            }
            outer = Word(outer, 4, S16(row, 12));
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, S16(row, 12)));
            outer = Rotation(outer, 0x28, local);
            outer = Put(outer, 0x48, S16(row, 6));
            local = NativeBattlePoseMatrices.Scale56BEF0(local, S16(row, 6), 0x2800, 0x2800);
            outer = Rotation(outer, 0x28, local);
            frame = LoadMatrix(frame, Packed(outer, 0x28), true, false);
            context = Cue(context, S16(row, 4), 10);
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(0xDEA454 + (uint)(S16(row, 8) * 4), 4));
            context = Put(context, 0x20, unchecked((int)color));
            frame = PublishContext(PublishContext(frame, outerAddress, outer, 104), contextAddress, context, 108);
            frame = DrawCustom(frame, contextAddress, resources, inputs, NativeBattleEffect66VisualIdentity.Point(frame, address, 0xDE90CC));
            context = Context(frame, contextAddress, 108);
            if (!Paused(frame, inputs))
            {
                row = ReadBytes(frame, address, 24);
                if (S16(row, 4) >= 16)
                {
                    row = Put(row, 0, 0);
                }
                else
                {
                    row = Word(row, 4, S16(row, 4) + 1);
                    int colorIndex = S16(row, 8) + 1;
                    row = Word(row, 8, colorIndex >= 12 ? 0 : colorIndex);
                    row = Word(row, 10, S16(row, 10) + S16(row, 18));
                    row = Word(row, 12, S16(row, 12) + S16(row, 20));
                }
                frame = WriteBytes(frame, address, row, 24);
            }
        }
        frame = PublishContext(frame, outerAddress, outer, 104);
        frame = Release(Release(frame, 108), 104);
        return FinishTimed(frame, task, 120, FirstPointAddress, 100, 1, inputs);
    }

    private static NativeBattleEffect66Step FinishTimed(NativeBattleEffect66Frame frame, uint task, int endAge, uint pool, int slots, byte marker, NativeBattleEffect66Inputs inputs)
    {
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        int next = unchecked((short)(Read(frame, task).ReadWord(12) + 1));
        frame = Write(frame, task, Read(frame, task).WriteWord(12, unchecked((ushort)next)));
        if (next < endAge)
        {
            return new(frame, 0);
        }
        for (int index = 0; index < slots; index++)
        {
            uint address = checked(pool + (uint)(index * 24));
            if ((ReadBytes(frame, address, 1).Read(0, 1)[0] & marker) != 0)
            {
                frame = WriteBytes(frame, address, NativeBattleEffectArena.Clear(), 4);
            }
        }
        return new(frame, 2);
    }
}
