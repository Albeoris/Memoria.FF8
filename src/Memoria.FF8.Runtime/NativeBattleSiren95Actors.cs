using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95SpawnServices(NativeBattleActionPacketsState Packets, Func<NativeBattleSiren95Frame, uint, uint, int, NativeBattleSiren95Frame> InitializeModel);
public sealed record NativeBattleSiren95StageStep(NativeBattleSiren95Frame Frame, bool Complete, NativeBattleStageState Stage);

public static class NativeBattleSiren95Actors
{
    public static NativeBattleSiren95Frame Spawn73BCF0(NativeBattleSiren95Frame frame, uint parent, NativeBattleSiren95SpawnServices services)
    {
        uint director = State(frame).DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren stage requires its current director pointer.");
        switch (unchecked((short)State(frame).ReadWord(director + 0x40)))
        {
            case 1:
                Prepared(0x73C1A0, 0x16A5380, 0x94, 0);
                Append(0x257FA90, 0x743C70, 0xE0);
                Append(0x257FA90, 0x744060, 0xE0);
                Append(0x257FA90, 0x746920, 0xE0);
                break;
            case 2:
                Prepared(0x73EE50, Control(0x128), 0x48, 1);
                Append(0x257FA90, 0x744450, 0xE0);
                Contacts(() => Append(0x257FA90, 0x7447C0, 0xE0));
                break;
            case 3:
                uint model = Append(0x258EC40, 0x746B60, 0x140);
                frame = services.InitializeModel(frame, model, Control(0x114), 0);
                Prepared(0x73EF60, Control(0x12C), 0x228, 2);
                Prepared(0x73F110, Control(0x130), 0x4A0, 3);
                Prepared(0x73F6A0, Control(0x138), 0xEC, 5);
                Append(0x257FA90, 0x744F90, 0xE0);
                break;
            case 4:
                Prepared(0x73F250, Control(0x134), 0x4A0, 4);
                Prepared(0x73F370, Control(0x134), 0x4A0, 4);
                Prepared(0x73F500, Control(0x134), 0x4A0, 4);
                Append(0x2585EF0, 0x745250, 0x64);
                for (ushort index = 0; index < 4; index++)
                {
                    uint task = Append(0x257FA90, 0x745AD0, 0xE0);
                    Store(task, Read(frame, task).WriteWord(0xDE, index));
                }
                break;
            case 5:
                break;
            case 6:
                Contacts(() => Append(0x2585EF0, 0x7456A0, 0x64));
                Contacts(() => Append(0x257FA90, 0x743250, 0xE0));
                break;
            case 7:
                Contacts(() => Prepared(0x73F800, Control(0x13C), 0x140, 6));
                uint secondary = Append(0x258BA90, 0x73F900, 0x2A4);
                Store(secondary, Read(frame, secondary).WriteDword(0x30, Control(0x10C)).WriteWord(0x298, 0).WriteWord(0x29C, 0).WriteWord(0x29E, 45));
                break;
            case 8:
                Append(0x257FA90, 0x746A40, 0xE0);
                break;
        }
        return frame;

        uint Control(uint offset) => State(frame).ReadDword(unchecked(State(frame).ReadDword(0x258EAA0) + offset));
        void Store(uint address, NativeBattleModulePoolTask task) => frame = Write(frame, address, task);
        uint Append(uint descriptor, uint callback, int size)
        {
            var appended = AppendTask(frame, descriptor, callback, size, parent, services.Packets);
            frame = appended.Frame;
            return appended.Address;
        }
        uint Prepared(uint callback, uint source, short size, ushort kind)
        {
            uint address = Append(0x258A010, callback, 0x534);
            Store(address, Read(frame, address).WriteDword(0x74, source).WriteDword(0x78, unchecked((uint)(int)size)).WriteWord(0x80, kind).WriteWord(0x82, 0));
            return address;
        }
        void Contacts(Func<uint> append)
        {
            int ordinal = 0;
            while (true)
            {
                NativeBattleModulePoolTask task = Read(frame, parent);
                NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Siren contact spawn requires its current action parameters.");
                int index = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                if ((uint)index >= services.Packets.Headers.Length)
                {
                    throw new NotSupportedException("Native Siren contact spawn leaves its current header owner.");
                }
                NativeBattleActionHeaderSlot header = services.Packets.Headers[index];
                if (ordinal >= header.ReadByte(16))
                {
                    break;
                }
                uint child = append();
                int contact = checked((header.FirstContactIndex ?? throw new NotSupportedException("Native Siren contact pointer is unknown.")) + ordinal);
                Store(child, Read(frame, child).WriteByte(0x2D, services.Packets.ReadContact(contact).Primary.ActorIndex));
                ordinal++;
            }
        }
    }

    public static NativeBattleSiren95TaskStep Advance73C1A0(NativeBattleSiren95Frame frame, uint address, NativeBattleGfAnimationSource source, Func<NativeBattleSiren95Frame, uint, NativeBattleSiren95Frame> draw)
    {
        return AdvanceSurface(frame, address, source, draw, 0x73C1A0, 0xE000, 2);
    }

    public static NativeBattleSiren95TaskStep Advance73EE50(NativeBattleSiren95Frame frame, uint address, NativeBattleGfAnimationSource source, Func<NativeBattleSiren95Frame, uint, NativeBattleSiren95Frame> draw)
    {
        return AdvanceSurface(frame, address, source, draw, 0x73EE50, 0xF000, 3);
    }

    private static NativeBattleSiren95TaskStep AdvanceSurface(NativeBattleSiren95Frame frame, uint address, NativeBattleGfAnimationSource source, Func<NativeBattleSiren95Frame, uint, NativeBattleSiren95Frame> draw, uint callback, ushort zOffset, short endingStage)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != callback || source.Address != task.ReadDword(0x74))
        {
            throw new InvalidDataException("Native Siren surface requires its actual programme task and source.");
        }
        uint director = State(frame).DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren surface requires its director pointer.");
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(0x94 - 12).ToArray().ToImmutableArray());
                workspace = workspace.Initialize7016B0(source, unchecked((int)task.ReadDword(0x78)));
                byte[] payload = task.Payload.ToArray();
                workspace.Data.AsSpan().CopyTo(payload.AsSpan(0x94 - 12));
                task = task with { Payload = payload.ToImmutableArray() };
                task = task.WriteDword(0x1C, State(frame).ReadDword(director + 8)).WriteDword(0x20, State(frame).ReadDword(director + 0xC));
                task = task.WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + zOffset))).WriteWord(0x1E, 0).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);
                frame = draw(Write(frame, address, task), address);
                frame = Next(frame, address);
                break;
            case 1:
                if (unchecked((short)State(frame).ReadWord(director + 0x40)) >= endingStage)
                {
                    frame = Next(Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))), address);
                }
                else
                {
                    frame = draw(frame, address);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException($"Native Siren surface signed phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        task = Read(frame, address);
        if (callback == 0x73C1A0)
        {
            task = task.WriteWord(0x8C, (ushort)(unchecked((byte)(task.ReadByte(0x8C) + 4)) & 127)).WriteWord(0x90, (ushort)(unchecked((byte)(task.ReadByte(0x90) - 6)) & 127));
        }
        else
        {
            task = task.WriteWord(0x8C, (ushort)(unchecked((byte)(task.ReadByte(0x8C) - 4)) & 127));
        }
        return Finish(Write(frame, address, task), address);
    }

    public static NativeBattleSiren95StageStep Advance746920(NativeBattleSiren95Frame frame, uint address, NativeBattleStageState stage)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback is not (0x746920 or 0x746A40))
        {
            throw new InvalidDataException("Native Siren stage companion requires its actual callback task.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                short depth = task.Callback == 0x746A40 ? (short)2048 : (short)1536;
                frame = Next(Write(frame, address, task.WriteWord(0x1C, unchecked((ushort)depth))), address);
                stage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = depth, ColorFlags = group.ColorFlags & 0xFF000000, Flags = task.Callback == 0x746A40 ? (byte)((group.Flags & 0xFD) | ((group.Flags >> 1) & 2)) : group.Flags }).ToImmutableArray() };
                break;
            case 1:
                if (task.Callback == 0x746A40)
                {
                    short remaining = unchecked((short)(task.ReadWord(0x1C) - 512));
                    task = task.WriteWord(0x1C, unchecked((ushort)Math.Max(remaining, (short)0)));
                    if (remaining <= 0)
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                    }
                    frame = Write(frame, address, task);
                    stage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)task.ReadWord(0x1C)) }).ToImmutableArray() };
                    break;
                }
                uint director = State(frame).DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren stage companion requires its director pointer.");
                if (unchecked((short)State(frame).ReadWord(director + 0x40)) >= 3)
                {
                    frame = Next(Write(frame, address, task.WriteWord(0x1C, 4096).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))), address);
                    stage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = 4096, Flags = (byte)(group.Flags & ~2) }).ToImmutableArray() };
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException($"Native Siren stage companion signed phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        NativeBattleSiren95TaskStep finished = Finish(frame, address);
        return new(finished.Frame, finished.Complete, stage);
    }

    internal static (NativeBattleSiren95Frame Frame, uint Address) AppendTask(NativeBattleSiren95Frame frame, uint descriptor, uint callback, int size, uint parent, NativeBattleActionPacketsState packets)
    {
        int pool = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == descriptor);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Siren append requires its current action parameters.");
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(pool, callback, size, parent, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, packets, pointer, header, target));
        if (appended.Address == 0)
        {
            throw new NotSupportedException("Native Siren exhausted a required task bank.");
        }
        return (frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address);
    }

    private static NativeBattleSiren95TaskStep Finish(NativeBattleSiren95Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(Write(frame, address, task), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);
    }

    private static NativeBattleSiren95Frame Next(NativeBattleSiren95Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Write(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
    private static NativeBattleSiren95State State(NativeBattleSiren95Frame frame) => frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren actor requires its retained module owner.");
}
