using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66Clouds
{
    private static NativeBattleEffect66Step AdvanceStreaks(NativeBattleEffect66Frame frame, uint task, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        (frame, uint outerAddress) = Allocate(frame, 128);
        NativeBattleEffectArena outer = Context(frame, outerAddress, 128);
        int age = unchecked((short)Read(frame, task).ReadWord(12));
        if (!Paused(frame, inputs) && age <= 70 && age % 3 == 0)
        {
            uint address = Free(frame, FirstPointAddress, 100);
            if (address != 0)
            {
                NativeBattleEffectArena row = Word(Put(ReadBytes(frame, address, 24), 0, 1), 4, 0);
                frame = WriteBytes(frame, address, row, 24);
                frame = Random(frame, out int random);
                int bone = BinaryPrimitives.ReadInt32LittleEndian(resources.ReadStatic(0xDEA360 + (uint)((random % 29) * 4), 4));
                frame = Random(frame, out random);
                NativeBattleEffectArena retained = NativeBattleEffectNodeContextMemory.Copy(outer, 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                NativeBattleEffect66PointStep queried = inputs.ReadBone502170(frame, (int)frame.State.ReadDword(CasterGlobal), bone, random & 4095, retained);
                frame = queried.Frame;
                outer = NativeBattleEffectNodeContextMemory.Copy(queried.Point8, 0, outer, 8, 8);
                int[] direction = new int[3];
                for (int axis = 0; axis < 3; axis++)
                {
                    frame = Random(frame, out random);
                    direction[axis] = S16(outer, 8 + axis * 2) - unchecked((short)frame.State.ReadWord(CenterAddress + (uint)(axis * 2))) + random % 600 - 300;
                }
                var unit = NativeBattleActionGeometryRules.NormalizeVector56BC50(direction[0], direction[1], direction[2]);
                outer = Put(Put(Put(outer, 0x50, unit.X), 0x54, unit.Y), 0x58, unit.Z);
                row = NativeBattleEffectNodeContextMemory.Copy(outer, 8, row, 8, 8);
                row = Word(Word(Word(row, 16, unit.X), 18, unit.Y), 20, unit.Z);
                frame = Random(frame, out random);
                row = Word(row, 14, random % 0x1200 + 0x2800);
                frame = Random(frame, out random);
                row = Word(row, 22, (random & 8191) + 0x1800);
                frame = WriteBytes(frame, address, row, 24);
            }
        }
        (frame, uint contextAddress) = Allocate(frame, 108);
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        outer = NativeBattleEffectNodeContextMemory.Copy(PackedArena(view), 0, outer, 0x10, 32);
        outer = Put(Put(Put(outer, 0x70, 0), 0x74, -4096), 0x78, 0);
        NativeBattleEffectArena context = Put(Put(Context(frame, contextAddress, 108), 0, 0xDE90CC), 8, 0);
        int remaining = 0;
        for (int index = 0; index < 100; index++)
        {
            uint address = checked(FirstPointAddress + (uint)(index * 24));
            NativeBattleEffectArena row = ReadBytes(frame, address, 24);
            if (U32(row, 0) != 1)
            {
                continue;
            }
            NativeBattleVector direction = Point(row, 16);
            outer = Put(Put(Put(outer, 0x50, direction.X), 0x54, direction.Y), 0x58, direction.Z);
            NativeBattleModuleAxisAngle align = NativeBattleModuleAlignment.Between571480(0, -4096, 0, direction.X, direction.Y, direction.Z);
            outer = Put(Put(Put(outer, 0x60, align.Axis.X), 0x64, align.Axis.Y), 0x68, align.Axis.Z);
            NativeBattleActionMatrix local = new(NativeBattleModuleAlignment.Rotation5714F0(align.Angle, align.Axis), S16(row, 8), S16(row, 10), S16(row, 12), 4096);
            outer = Translation(Rotation(outer, 0x30, local), 0x30, local.X, local.Y, local.Z);
            int scaleX = S16(row, 14);
            int scaleY = S16(row, 22);
            outer = Put(Put(Put(outer, 0x50, scaleX), 0x54, scaleY), 0x58, scaleX);
            local = NativeBattlePoseMatrices.Scale56BEF0(local, scaleX, scaleY, scaleX);
            NativeBattleActionMatrix composed = NativeBattlePoseMatrices.Compose56C2F0(view.ReadMatrix(), local);
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(composed, null);
            outer = NativeBattleEffectNodeContextMemory.Copy(PackedArena(packed), 0, outer, 0x30, 32);
            frame = LoadMatrix(frame, packed);
            int phase = S16(row, 4);
            context = Cue(context, phase, 18);
            context = Put(context, 0x20, unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(0xDEA3DC + (uint)((phase % 18) * 4), 4))));
            frame = PublishContext(PublishContext(frame, outerAddress, outer, 128), contextAddress, context, 108);
            frame = DrawCustom(frame, contextAddress, resources, inputs, NativeBattleEffect66VisualIdentity.Point(frame, address, 0xDE90CC));
            context = Context(frame, contextAddress, 108);
            if (!Paused(frame, inputs))
            {
                row = ReadBytes(frame, address, 24);
                if (S16(row, 4) >= 24)
                {
                    row = Put(row, 0, 0);
                }
                else
                {
                    row = Word(row, 4, S16(row, 4) + 1);
                    remaining++;
                }
                frame = WriteBytes(frame, address, row, 24);
            }
        }
        frame = PublishContext(frame, outerAddress, outer, 128);
        frame = Release(Release(frame, 108), 128);
        return Finish(frame, task, remaining, inputs);
    }

    private static NativeBattleEffectArena Cue(NativeBattleEffectArena context, int age, int fall)
    {
        context = Put(context, 0x1C, 0x33);
        if (age < 6)
        {
            context = Put(Put(context, 12, 4096 - age * 682), 0x1C, 0xF3);
        }
        else if (age >= fall)
        {
            context = Put(Put(context, 12, (age - fall) * 682), 0x1C, 0xF3);
        }
        return context;
    }

    private static NativeBattleEffect66Frame DrawCustom(NativeBattleEffect66Frame frame, uint contextAddress, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs, NativeBattlePilotVisualIdentity pilotIdentity)
    {
        NativeBattleEffect66Mesh108Step drawn = NativeBattleEffect66Mesh108.DrawLoaded(frame.Memory, resources.ReadMesh(0xDE90CC), inputs.ReadProjection(frame), Context(frame, contextAddress, 108), inputs.Colors, pilotIdentity: pilotIdentity);
        frame = PublishContext(frame with { Memory = drawn.Memory }, contextAddress, drawn.Context108, 108);
        return inputs.Execute(frame, new NativeBattleEffect66DeliverBatch(drawn.Batch, CursorGlobal));
    }
}
