using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleFatedCircle162Operation;
public sealed record NativeBattleFatedCircle162BindActor : NativeBattleFatedCircle162Operation;
public sealed record NativeBattleFatedCircle162Sound(uint Address) : NativeBattleFatedCircle162Operation;
public sealed record NativeBattleFatedCircle162Stage(int Intensity) : NativeBattleFatedCircle162Operation;
public sealed record NativeBattleFatedCircle162ReadTexture : NativeBattleFatedCircle162Operation;
public sealed record NativeBattleFatedCircle162EnqueueTexture : NativeBattleFatedCircle162Operation;
public sealed record NativeBattleFatedCircle162Flash : NativeBattleFatedCircle162Operation;
public sealed record NativeBattleFatedCircle162Contacts : NativeBattleFatedCircle162Operation;
public sealed record NativeBattleFatedCircle162Geometry(NativeBattleGfGeometryBatch Batch) : NativeBattleFatedCircle162Operation;
public sealed record NativeBattleFatedCircle162Sprite(NativeBattleModulePacketBatch Batch) : NativeBattleFatedCircle162Operation;
public sealed record NativeBattleFatedCircle162Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int> Pending508500, Func<byte, NativeBattleModuleAnchor> ReadAnchor571400, Func<ushort, NativeBattleModuleAnchor> ReadTarget5020A0, Func<byte, NativeBattleModuleAnchor> ReadBone240, Func<byte, NativeBattleGraphicsModel> ReadModel, Func<byte, short> ReadScale26, Func<NativeBattleFatedCircle162Frame, NativeBattleFatedCircle162Operation, NativeBattleFatedCircle162Frame> Execute, Func<NativeBattleFatedCircle162Frame, NativeBattleModuleAnchor, NativeBattleFatedCircle162Frame> Capture5FC5E0);

public static partial class NativeBattleFatedCircle162Controller
{
    public static NativeBattleFatedCircle162ChildStep Advance5F91D0(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0 && ((inputs.GraphicsFlags & 1) != 0 || inputs.Pending508500() < 0))
        {
            return new(frame, pool, 0);
        }
        NativeBattleEffectParameters parameters = frame.Memory.FatedCircle162?.Parameters ?? throw new InvalidDataException("Native5F91D0 requires its entered parameter owner.");
        byte actor = parameters.Actor78;
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        switch (counter)
        {
            case 1:
                Execute(new NativeBattleFatedCircle162BindActor());
                break;
            case 2:
                Append(0x5F99C0);
                for (uint address = 0x23D0CE8; address < 0x23D0D74; address += 20)
                {
                    frame = frame with { Staging = WriteDword(frame.Staging, address, 0) };
                }
                break;
            case 6:
                NativeBattleModuleAnchor anchor = inputs.ReadAnchor571400(actor);
                anchor = anchor with { Y = anchor.W, W = anchor.Y };
                int spiral = Append(0x5F9F70, anchor);
                Set(spiral, 24, Rand() % 4096);
                Set(spiral, 26, -50 - Rand() % 40);
                Set(spiral, 28, 3328);
                Append(0x5FA0E0, anchor);
                int ring = Append(0x5FA340, anchor);
                Set(ring, 30, 1024);
                Set(ring, 28, 1024);
                break;
            case 18:
                NativeBattleModuleAnchor tip = new(unchecked((short)(ReadWord(frame.Staging, 0x23CF418) + ((444 * ReadDword(frame.Staging, 0x23CF420)) >> 12))), unchecked((short)(ReadWord(frame.Staging, 0x23CF41A) + ((444 * ReadDword(frame.Staging, 0x23CF424)) >> 12))), unchecked((short)(ReadWord(frame.Staging, 0x23CF41C) + ((444 * ReadDword(frame.Staging, 0x23CF428)) >> 12))), ReadWord(frame.Staging, 0x23CF41E));
                task = Position(task, tip);
                int glow = Append(0x5FA480, tip);
                Set(glow, 28, 192);
                Set(glow, 30, 96);
                Append(0x5FA540, tip);
                Append(0x5FA8F0, tip);
                break;
            case 31:
                Append(0x5FABD0, Position(task));
                break;
            case 40:
                NativeBattleModuleAnchor bone = inputs.ReadBone240(actor);
                NativeBattleActionMatrix matrix = inputs.ReadModel(actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native5F91D0 requires its current actor matrix64.");
                NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, 0, 2600));
                Append(0x5FAF50, bone with { X = unchecked((short)(bone.X + offset.X)), Y = unchecked((short)(bone.Y + offset.Y)), Z = unchecked((short)(bone.Z + offset.Z)) }, false);
                break;
            case 57:
                int first = Append(0x5FB3F0, inputs.ReadAnchor571400(actor));
                Set(first, 28, 1280);
                Set(first, 30, 213);
                Set(first, 24, inputs.ReadModel(actor).Rotation14 ?? throw new NotSupportedException("Native5F91D0 requires its actual actor rotationWORD14."));
                NativeBattleModuleAnchor center = inputs.ReadTarget5020A0(parameters.TargetMask7A);
                int second = Append(0x5FB630, center with { Y = -1400, Z = unchecked((short)(center.Z + 3000)) }, false);
                Set(second, 14, 22);
                Set(second, 24, Rand() % 4096);
                Set(second, 28, 1280);
                Set(second, 30, 256);
                break;
            case 71:
                NativeBattleModuleAnchor target = inputs.ReadTarget5020A0(parameters.TargetMask7A);
                target = target with { Z = unchecked((short)(target.Z - 800)) };
                task = PositionXyz(task, target);
                frame = inputs.Capture5FC5E0(frame, target);
                Execute(new NativeBattleFatedCircle162ReadTexture());
                break;
            case 93:
                Append(0x5FCD60, Position(task));
                frame = frame with { Staging = WriteDword(frame.Staging, 0x23D0D80, 1) };
                break;
            case 108:
                if (inputs.Pending508500() < 0)
                {
                    return new(frame, pool, 0);
                }
                Execute(new NativeBattleFatedCircle162EnqueueTexture());
                NativeBattleEffectTaskPool main = frame.Effect.Pools[0];
                (main, int loader) = main.Append(0x5FCFF0);
                main = main.WriteSlot(loader, main.Slots[loader].WriteWord(12, 0));
                frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, main) } };
                break;
            case 110:
                Execute(new NativeBattleFatedCircle162Flash());
                break;
            case 111:
                NativeBattleModuleAnchor origin = Position(task);
                int morph = Append(0x5FB7A0, origin with { Z = unchecked((short)(origin.Z + 2300)) });
                Set(morph, 30, 896);
                Set(morph, 28, 896);
                frame = frame with { Staging = PrepareMorph5FD030(frame.Staging, 0xDA2350, 0xDA3770, 93, 24) };
                frame = frame with { Staging = WriteDword(frame.Staging, 0x23D0D80, 0) };
                for (int index = 0; index < 8; index++)
                {
                    int fragment = Append(0x5FBD00, origin);
                    Set(fragment, 14, index);
                    Set(fragment, 22, -300 - Rand() % 300);
                    Set(fragment, 24, -460 - Rand() % 180);
                    int delayBase = index * (Rand() % 3);
                    int delay = Rand() % 9 + delayBase;
                    Set(fragment, 26, delay);
                    Set(fragment, 28, 0);
                    Set(fragment, 30, Rand() % 25 + 20);
                    Set(fragment, 32, 0);
                    int angularSpeed = Rand() % 30 + 35;
                    if ((pool.Slots[fragment].ReadWord(30) & 0x801) != 0)
                    {
                        angularSpeed = -angularSpeed;
                    }
                    Set(fragment, 34, angularSpeed);
                    int reflection = Append(0x5FC480, origin);
                    Set(reflection, 14, index);
                    Set(reflection, 26, delay);
                }
                Append(0x5FC850, origin with { Z = unchecked((short)(origin.Z + 2700)) });
                break;
            case 116:
                NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C];
                int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Native5F91D0 requires its physical contact array.");
                for (int index = 0; index < header.ReadByte(16); index++)
                {
                    byte recipient = inputs.Packets.ReadContact(firstContact + index).Primary.ActorIndex;
                    int impact = Append(0x5FCB00, inputs.ReadAnchor571400(recipient));
                    Set(impact, 28, (2200 * inputs.ReadScale26(recipient)) >> 12);
                }
                break;
            case 150:
                Execute(new NativeBattleFatedCircle162Contacts());
                break;
        }
        if (counter >= 162)
        {
            Execute(new NativeBattleFatedCircle162Stage((170 - counter) << 8));
        }
        if (counter == 1)
        {
            Execute(new NativeBattleFatedCircle162Sound(unchecked((uint)ReadDword(frame.Staging, 0xDA8C68))));
        }
        if (counter == 57)
        {
            Execute(new NativeBattleFatedCircle162Sound(unchecked((uint)ReadDword(frame.Staging, 0xDA8C6C))));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1))).SetNext(pool.Slots[slot].Next);
        pool = pool.WriteSlot(slot, task);
        if (Word(task, 12) <= 170)
        {
            return new(frame, pool, 0);
        }
        Execute(new NativeBattleFatedCircle162Stage(0));
        return new(frame, pool, 2);

        int Append(uint callback, NativeBattleModuleAnchor? position = null, bool writePadding = true)
        {
            (pool, int created) = pool.Append(callback);
            NativeBattleModulePoolTask child = pool.Slots[created].WriteWord(12, 0);
            if (position is not null)
            {
                child = writePadding ? Position(child, position) : PositionXyz(child, position);
            }
            pool = pool.WriteSlot(created, child);
            return created;
        }
        void Set(int targetSlot, int offset, int value)
        {
            pool = pool.WriteSlot(targetSlot, pool.Slots[targetSlot].WriteWord(offset, unchecked((ushort)value)));
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
        void Execute(NativeBattleFatedCircle162Operation operation)
        {
            frame = inputs.Execute(frame, operation);
        }
    }

    public static NativeBattleFileStaging PrepareMorph5FD030(NativeBattleFileStaging staging, uint source, uint target, int count, int duration)
    {
        if (count < 0 || duration == 0)
        {
            throw new InvalidDataException("Native5FD030 requires a bounded vertex count and nonzero morph duration.");
        }
        for (int index = 0; index < count; index++)
        {
            for (int component = 0; component < 3; component++)
            {
                uint offset = checked((uint)(index * 8 + component * 2));
                int delta = ReadWord(staging, target + offset) - ReadWord(staging, source + offset);
                staging = WriteWord(staging, 0x23D0258 + offset, unchecked((ushort)(delta / duration)));
            }
        }
        return staging;
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModuleAnchor Position(NativeBattleModulePoolTask task) => new(Word(task, 16), Word(task, 18), Word(task, 20), Word(task, 22));
    private static NativeBattleModulePoolTask PositionXyz(NativeBattleModulePoolTask task, NativeBattleModuleAnchor point) => task.WriteWord(16, unchecked((ushort)point.X)).WriteWord(18, unchecked((ushort)point.Y)).WriteWord(20, unchecked((ushort)point.Z));
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleModuleAnchor point) => task.WriteWord(16, unchecked((ushort)point.X)).WriteWord(18, unchecked((ushort)point.Y)).WriteWord(20, unchecked((ushort)point.Z)).WriteWord(22, unchecked((ushort)point.W));
}
