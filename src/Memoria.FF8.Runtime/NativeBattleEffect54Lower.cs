using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect54Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect54Lower
{
    public static NativeBattleEffect54Frame Initialize80D090(NativeBattleEffect54Frame frame, uint taskAddress, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        uint context = taskAddress + 0x34;
        frame = frame with { State = frame.State.WriteDword(CurrentContextGlobal, context) };
        Store16(ref frame, context + 0x22, task.ReadWord(0x29A));
        Store16(ref frame, context + 0x24, task.ReadWord(0x29C));
        uint source = task.ReadDword(0x30);
        for (int index = 0; index < 4; index++)
        {
            Store32(ref frame, context + 0x224 + (uint)(index * 4), source + 4 + (uint)(index * 64));
        }
        if (Read32(frame, source, resources) == 0)
        {
            frame = Relocate80D880(frame, source, resources);
        }
        NativeBattleModulePoolTask parent = Read(frame, task.ReadDword(0x10));
        short count = unchecked((short)parent.ReadWord(0x5A));
        Store16(ref frame, context + 0x1C, unchecked((ushort)count));
        Store16(ref frame, context + 0x1E, task.ReadByte(0x2C));
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        int headerIndex = Parameters(frame).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A));
        int first = packets.Headers[headerIndex].FirstContactIndex ?? throw new NotSupportedException("Effect54 requires its actual contact pointer.");
        for (int index = 0; index < count; index++)
        {
            Store32(ref frame, context + 4 + (uint)(index * 4), packets.ReadContact(first + index).Primary.ActorIndex);
        }
        Store16(ref frame, context + 0x1A, task.ReadWord(0x29E));
        if (Read16(frame, context + 0x24) != 0)
        {
            throw new NotSupportedException("Effect54 requires its selected current-anchor context mode.");
        }
        int actor = Read16(frame, context + 0x1E);
        frame = InitializeActor80D320(frame, actor, context, inputs);
        frame = InitializeTargets80D670(frame, context, inputs);
        NativeBattleVector casterPosition = Vector(inputs.ReadModelArena9C(frame, PhysicalModelAddress(actor)), 0x1C);
        NativeBattleVector targetPosition = Vector(inputs.ReadModelArena9C(frame, PhysicalModelAddress(unchecked((int)Read32(frame, context + 4)))), 0x1C);
        int dx = unchecked((short)(casterPosition.X - targetPosition.X));
        int dy = unchecked((short)(casterPosition.Y - targetPosition.Y));
        int dz = unchecked((short)(casterPosition.Z - targetPosition.Z));
        int square = unchecked(dx * dx + dy * dy + dz * dz);
        Store32(ref frame, context, unchecked((uint)(long)Math.Sqrt(square)));
        return frame;
    }

    public static NativeBattleEffect54Frame Relocate80D880(NativeBattleEffect54Frame frame, uint source, NativeBattleEffect54Resources resources)
    {
        NativeBattleFileStaging staging = RequireStaging(frame);
        if (source != SourceAddress)
        {
            throw new NotSupportedException("Effect54 source bank is unsupported.");
        }
        ImmutableArray<byte> current = resources.ReadBank(staging);
        byte[] relocated = NativeBattleEffectNodeBankFixups.Relocate(current, source);
        BinaryPrimitives.WriteUInt32LittleEndian(relocated, 1);
        return frame with { Staging = resources.PublishBank(staging, relocated.ToImmutableArray()) };
    }

    public static NativeBattleEffect54Frame InitializeActor80D320(NativeBattleEffect54Frame frame, int actor, uint context, NativeBattleEffect54Inputs inputs)
    {
        uint model = PhysicalModelAddress(actor);
        NativeBattleEffectArena packed = inputs.ReadModelArena9C(frame, model);
        NativeBattleEffect54PointStep first = inputs.Point502170(frame, model, 241, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        frame = first.Frame;
        NativeBattleVector point = Vector(first.Output8);
        PutQ16(ref frame, context + 0x1C4, point with { Y = Word54(packed, 0x24) });
        PutQ16(ref frame, context + 0x1D4, point with { Y = 0 });
        NativeBattleEffect54PointStep second = inputs.Point502170(frame, model, 241, 0, first.Output8);
        frame = second.Frame;
        PutQ16(ref frame, context + 0x1E4, Vector(second.Output8));
        NativeBattleEffect54PointStep third = inputs.Point502170(frame, model, 240, 0, second.Output8);
        frame = third.Frame;
        PutQ16(ref frame, context + 0x1F4, Vector(third.Output8));
        PutQ16(ref frame, context + 0x204, point with { Y = Word54(packed, 0x3C) });
        return frame;
    }

    public static NativeBattleEffect54Frame InitializeTargets80D670(NativeBattleEffect54Frame frame, uint context, NativeBattleEffect54Inputs inputs)
    {
        int count = Read16(frame, context + 0x1C);
        int minX = 0;
        int maxX = 0;
        int minZ = 0;
        int maxZ = 0;
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        for (int index = 0; index < count; index++)
        {
            uint model = PhysicalModelAddress(unchecked((int)Read32(frame, context + 4 + (uint)(index * 4))));
            NativeBattleEffectArena packed = inputs.ReadModelArena9C(frame, model);
            NativeBattleEffect54PointStep first = inputs.Point502170(frame, model, 241, 0, retained);
            frame = first.Frame;
            NativeBattleVector point = Vector(first.Output8);
            uint offset = (uint)(index * 16);
            PutQ16(ref frame, context + 0x54 + offset, point with { Y = Word54(packed, 0x24) });
            PutQ16(ref frame, context + 0x94 + offset, point with { Y = 0 });
            NativeBattleEffect54PointStep second = inputs.Point502170(frame, model, 241, 0, first.Output8);
            frame = second.Frame;
            PutQ16(ref frame, context + 0xD4 + offset, Vector(second.Output8));
            NativeBattleEffect54PointStep third = inputs.Point502170(frame, model, 240, 0, second.Output8);
            frame = third.Frame;
            retained = third.Output8;
            PutQ16(ref frame, context + 0x114 + offset, Vector(third.Output8));
            PutQ16(ref frame, context + 0x154 + offset, point with { Y = Word54(packed, 0x3C) });
            int x = unchecked((int)Read32(frame, context + 0x54 + offset));
            int z = unchecked((int)Read32(frame, context + 0x5C + offset));
            if (index == 0)
            {
                minX = maxX = x;
                minZ = maxZ = z;
            }
            else
            {
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minZ = Math.Min(minZ, z);
                maxZ = Math.Max(maxZ, z);
            }
        }
        Store32(ref frame, context + 0x194, unchecked((uint)(unchecked(minX + maxX) / 2)));
        Store32(ref frame, context + 0x198, 0);
        Store32(ref frame, context + 0x19C, unchecked((uint)(unchecked(minZ + maxZ) / 2)));
        return frame;
    }

    public static NativeBattleEffect54Step Advance80DAE0(NativeBattleEffect54Frame frame, uint taskAddress, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        uint context = taskAddress + 0x34;
        frame = frame with { State = frame.State.WriteDword(CurrentContextGlobal, context) };
        int result = 0;
        switch (Read16(frame, context + 0x20))
        {
            case 0:
                Store16(ref frame, context + 0x20, 1);
                break;
            case 1:
                frame = Birth811C80(frame, resources, inputs);
                frame = AdvanceList80F330(frame, resources, inputs);
                frame = DrawSprites80F1F0(frame, taskAddress, resources, inputs);
                frame = ScanMeshes80DB70(frame, resources);
                short age = unchecked((short)(Read16(frame, context + 0x18) + 1));
                Store16(ref frame, context + 0x18, unchecked((ushort)age));
                if (age >= Read16(frame, context + 0x1A) && Read16(frame, context + 0x14) == 0 && Read16(frame, context + 0x16) == 0)
                {
                    Store16(ref frame, context + 0x20, 2);
                }
                break;
            case 2:
                result = 1;
                break;
        }
        frame = frame with { State = frame.State.WriteWord(EmitterVisitCounter, unchecked((ushort)(frame.State.ReadWord(EmitterVisitCounter) + Read16(frame, context + 0x14)))).WriteWord(DrawVisitCounter, unchecked((ushort)(frame.State.ReadWord(DrawVisitCounter) + Read16(frame, context + 0x16)))) };
        return new(frame, result);
    }

    public static NativeBattleEffect54Frame AdvanceList80F330(NativeBattleEffect54Frame frame, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        uint node = Read32(frame, frame.State.ReadDword(CurrentContextGlobal) + 0x2C);
        while (node != 0)
        {
            short kind = Read16(frame, node + 8);
            if (kind == 0)
            {
                frame = Emit810600(frame, node, resources, inputs);
            }
            else if (kind == 1)
            {
                frame = AdvanceDrawable80F370(frame, node, resources, inputs);
            }
            node = Read32(frame, node + 4);
        }
        return frame;
    }

    public static NativeBattleEffect54Frame AdvanceDrawable80F370(NativeBattleEffect54Frame frame, uint node, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        byte[] descriptor = Descriptor(frame, node, resources);
        if (Read16(frame, node + 0x1CC) == 0)
        {
            frame = Cycle80FB40(frame, node, descriptor);
            if (ReadByte(frame, node + 0x1D2) == 1)
            {
                Store16(ref frame, node + 0x1CC, 1);
                Store32(ref frame, node + 0x170, 0);
            }
            else
            {
                frame = Motion80F400(frame, node, descriptor, resources);
                frame = Prepare80FC10(frame, node, descriptor);
                Store16(ref frame, node + 0x1CA, unchecked((ushort)(Read16(frame, node + 0x1CA) + 1)));
            }
        }
        else if (Read16(frame, node + 0x1CC) == 1)
        {
            frame = Unlink8117C0(frame, node);
            Store8(ref frame, node + 0x1D7, 0);
            uint counter = frame.State.ReadDword(CurrentContextGlobal) + 0x16;
            Store16(ref frame, counter, unchecked((ushort)(Read16(frame, counter) - 1)));
        }
        return frame;
    }

    public static NativeBattleEffect54Frame Cycle80FB40(NativeBattleEffect54Frame frame, uint node, byte[] descriptor)
    {
        RequireSelected(descriptor);
        sbyte phase = unchecked((sbyte)(ReadByte(frame, node + 0x1D0) + 1));
        Store8(ref frame, node + 0x1D0, unchecked((byte)phase));
        if (phase > unchecked((sbyte)ReadByte(frame, node + 0x1D1)))
        {
            Store8(ref frame, node + 0x1D0, 0);
            Store8(ref frame, node + 0x1D2, 1);
        }
        return frame;
    }

    public static (NativeBattleEffect54Frame Frame, uint Address) AllocateEmitter810560(NativeBattleEffect54Frame frame, uint parent, int source, int target, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        int index = unchecked((short)frame.State.ReadWord(EmitterHintGlobal));
        uint start = frame.State.ReadDword(EmitterRecordsGlobal);
        uint result = 0;
        for (int attempt = 0; attempt < EmitterCount; attempt++)
        {
            uint node = unchecked(start + (uint)(index * EmitterStride));
            if (ReadByte(frame, node + 0x69) == 0)
            {
                frame = WriteBytes(frame, node, NativeBattleEffectArena.Clear().Write(0, new byte[EmitterStride]), EmitterStride);
                Store8(ref frame, node + 0x6A, unchecked((byte)source));
                Store8(ref frame, node + 0x6B, unchecked((byte)target));
                Store32(ref frame, node + 0x5C, parent);
                Store8(ref frame, node + 0x69, 1);
                uint counter = frame.State.ReadDword(CurrentContextGlobal) + 0x14;
                Store16(ref frame, counter, unchecked((ushort)(Read16(frame, counter) + 1)));
                frame = Link811790(frame, node, 0);
                result = node;
                break;
            }
            index++;
            if (index >= ReachableEmitterCount)
            {
                index = 0;
            }
        }
        index++;
        frame = frame with { State = frame.State.WriteWord(EmitterHintGlobal, index >= ReachableEmitterCount ? (ushort)0 : unchecked((ushort)index)) };
        return (frame, result);
    }

    private static NativeBattleEffect54Frame ScanMeshes80DB70(NativeBattleEffect54Frame frame, NativeBattleEffect54Resources resources)
    {
        uint node = Read32(frame, frame.State.ReadDword(CurrentContextGlobal) + 0x2C);
        while (node != 0)
        {
            if (Read16(frame, node + 8) == 1)
            {
                RequireSelected(Descriptor(frame, node, resources));
            }
            node = Read32(frame, node + 4);
        }
        return frame;
    }

    private static void RequireSelected(byte[] descriptor)
    {
        if (descriptor[0x16] != 0 || descriptor[0x1B] != 0 || descriptor[0x1D] != 0 || descriptor[0x1E] != 0 || descriptor[0x26] != 0 || descriptor[0x27] != 0 || descriptor[0x30] != 0 || descriptor[0x22] > 1)
        {
            throw new NotSupportedException("Effect54 node descriptor requires an unsupported drawing or motion contract.");
        }
    }

    private static byte[] Descriptor(NativeBattleEffect54Frame frame, uint node, NativeBattleEffect54Resources resources)
    {
        uint table = Read32(frame, frame.State.ReadDword(CurrentContextGlobal) + 0x224);
        int index = unchecked((sbyte)ReadByte(frame, node + 0x1D6));
        uint pointer = Read32(frame, unchecked(table + (uint)(index * 4)), resources);
        byte[] descriptor = resources.ReadResource(RequireStaging(frame), pointer, NativeBattleEffect54Resources.DescriptorLength);
        RequireSelected(descriptor);
        return descriptor;
    }

    private static byte ReadByte(NativeBattleEffect54Frame frame, uint address) => ReadBytes(frame, address, 1).Read(0, 1)[0];
    private static short Read16(NativeBattleEffect54Frame frame, uint address) => Word54(ReadBytes(frame, address, 2), 0);
    private static uint Read32(NativeBattleEffect54Frame frame, uint address) => Dword54(ReadBytes(frame, address, 4), 0);
    private static uint Read32(NativeBattleEffect54Frame frame, uint address, NativeBattleEffect54Resources resources) => BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadResource(RequireStaging(frame), address, 4));
    private static void Store8(ref NativeBattleEffect54Frame frame, uint address, byte value) => frame = WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
    private static void Store16(ref NativeBattleEffect54Frame frame, uint address, ushort value) => frame = WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
    private static void Store32(ref NativeBattleEffect54Frame frame, uint address, uint value) => frame = WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);
    private static void Add32(ref NativeBattleEffect54Frame frame, uint address, uint value) => Store32(ref frame, address, unchecked(Read32(frame, address) + value));
    private static void PutQ16(ref NativeBattleEffect54Frame frame, uint address, NativeBattleVector point)
    {
        Store32(ref frame, address, unchecked((uint)(point.X << 16)));
        Store32(ref frame, address + 4, unchecked((uint)(point.Y << 16)));
        Store32(ref frame, address + 8, unchecked((uint)(point.Z << 16)));
    }
}
