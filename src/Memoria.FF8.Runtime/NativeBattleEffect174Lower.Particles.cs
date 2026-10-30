using static Memoria.FF8.Runtime.NativeBattleEffect174Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect174Lower
{
    private static NativeBattleEffect174Step AdvanceParticles(NativeBattleEffect174Frame frame, uint taskAddress, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        bool moving = task.Callback == 0x5D6AA0;
        int marker = moving ? 2 : 1;
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = L174Word(L174Put(Context(frame, temporary, 180), 0, 0xD59648), 0x24, 0);
        frame = PublishContext(frame, temporary, context, 180);
        int live = 0;
        for (int index = 0; index < 100; index++)
        {
            uint row = L174Records + (uint)(index * 24);
            if ((frame.State.Read(row, 1)[0] & marker) == 0)
            {
                continue;
            }
            short scale = unchecked((short)frame.State.ReadWord(row + 6));
            NativeBattleEffectArena point = frame.State.ReadView(row + 8, 8);
            (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
            frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), L174Vector(point), known.All(value => value) ? L174S(NativeBattleEffectArena.Clear().Write(0, w), 0) : null, scale, unchecked((short)-(scale >> 4))) };
            context = L174Word(Context(frame, temporary, 180), 4, frame.State.ReadWord(row + 4));
            frame = L174Sprite(frame, temporary, context, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattleEffect174Presentation.Row(frame, L174Records, index, taskAddress, 0xD59648) : null);
            if (L174Paused(frame, inputs))
            {
                continue;
            }
            frame = frame with { State = frame.State.WriteWord(row + 4, unchecked((ushort)(frame.State.ReadWord(row + 4) + 1))) };
            if (L174S(Context(frame, temporary, 180), 0x28) < 0)
            {
                frame = frame with { State = frame.State.WriteDword(row, 0) };
                continue;
            }
            if (moving)
            {
                frame = frame with { State = frame.State.WriteWord(row + 6, unchecked((ushort)(scale - (scale >> 4)))) };
                for (int axis = 0; axis < 3; axis++)
                {
                    uint velocityAddress = row + 16 + (uint)(axis * 2);
                    short velocity = unchecked((short)frame.State.ReadWord(velocityAddress));
                    frame = frame with { State = frame.State.WriteWord(row + 8 + (uint)(axis * 2), unchecked((ushort)(frame.State.ReadWord(row + 8 + (uint)(axis * 2)) + velocity))).WriteWord(velocityAddress, unchecked((ushort)(velocity - (velocity >> 2)))) };
                }
            }
            else
            {
                short velocity = unchecked((short)frame.State.ReadWord(row + 18));
                frame = frame with { State = frame.State.WriteWord(row + 10, unchecked((ushort)(frame.State.ReadWord(row + 10) + velocity))).WriteWord(row + 18, unchecked((ushort)(velocity - (velocity >> 2)))) };
            }
            live++;
        }
        frame = Release(frame, 180);
        if (L174Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, taskAddress);
        short age = Signed(task, 12);
        bool births = age >= 0 && age <= (moving ? 2 : 56);
        if (births)
        {
            if (!moving)
            {
                int caster = checked((int)frame.State.ReadDword(0x2395E50));
                NativeBattleEffect174PointStep point = inputs.Point502170(frame, caster, 25, 8192, TaskView(task, 0x10, 8));
                frame = point.Frame;
                task = CopyTask(Read(frame, taskAddress), 0x10, point.Output8, 8);
                frame = Write(frame, taskAddress, task);
            }
            for (int birth = 0; birth < (moving ? 20 : 3); birth++)
            {
                int slot = 0;
                while (slot < 100 && frame.State.ReadDword(L174Records + (uint)(slot * 24)) != 0)
                {
                    slot++;
                }
                if (slot == 100)
                {
                    break;
                }
                uint row = L174Records + (uint)(slot * 24);
                int size = L174Random(ref frame) % (moving ? 768 : 640) + (moving ? 256 : 384);
                frame = frame with { State = frame.State.WriteDword(row, (uint)marker).WriteWord(row + 4, 0).WriteWord(row + 6, (ushort)size).WriteView(row + 8, TaskView(Read(frame, taskAddress), 0x10, 8), 8) };
                if (inputs.CapturePresentation)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, L174Records, slot);
                }
                if (moving)
                {
                    int x = L174Random(ref frame) % 4096 - 2048;
                    int y = L174Random(ref frame) % 4096 - 2048;
                    int z = L174Random(ref frame) % 4096 - 2048;
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                    int speed = L174Random(ref frame) % 200 + 170;
                    frame = frame with { State = frame.State.WriteWord(row + 16, unchecked((ushort)(unchecked(direction.X * speed) >> 12))).WriteWord(row + 18, unchecked((ushort)(unchecked(direction.Y * speed) >> 12))).WriteWord(row + 20, unchecked((ushort)(unchecked(direction.Z * speed) >> 12))) };
                }
                else
                {
                    int x = L174Random(ref frame) % 200 - 100;
                    int y = L174Random(ref frame) % 100 - 50;
                    int z = L174Random(ref frame) % 200 - 100;
                    int speed = -40 - L174Random(ref frame) % 80;
                    frame = frame with { State = frame.State.WriteWord(row + 8, unchecked((ushort)(frame.State.ReadWord(row + 8) + x))).WriteWord(row + 10, unchecked((ushort)(frame.State.ReadWord(row + 10) + y))).WriteWord(row + 12, unchecked((ushort)(frame.State.ReadWord(row + 12) + z))).WriteWord(row + 18, unchecked((ushort)speed)) };
                }
            }
        }
        task = Read(frame, taskAddress);
        short next = unchecked((short)(task.ReadWord(12) + 1));
        frame = Write(frame, taskAddress, task.WriteWord(12, unchecked((ushort)next)));
        return new(frame, next >= 4 && live == 0 ? 2 : 0);
    }
}
