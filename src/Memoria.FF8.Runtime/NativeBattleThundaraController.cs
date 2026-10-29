using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleThundaraFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleThundaraOperation;
public sealed record NativeBattleThundaraSprite(uint ResourceAddress, int Frame, NativeBattleModuleAnchor Position, int Scale, int DepthOffset, ushort Flags, uint Color) : NativeBattleThundaraOperation
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleThundaraGeometry(uint Helper, NativeBattleModuleAnchor Position, short AngleOrBaseY, int Phase, int Duration) : NativeBattleThundaraOperation
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleThundaraFlash : NativeBattleThundaraOperation;
public sealed record NativeBattleThundaraContact(int Contact) : NativeBattleThundaraOperation;
public sealed record NativeBattleThundaraSound(NativeBattleModuleAnchor Position) : NativeBattleThundaraOperation;
public sealed record NativeBattleThundaraInputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleVector> ReadBone241, Func<int, NativeBattleModelTransform> ReadTransform, Func<NativeBattleThundaraFrame, NativeBattleThundaraOperation, NativeBattleThundaraFrame> Execute);

/// <summary>6DC2E0/6DC390's four retained banks and ordered primary, secondary and tertiary task contracts.</summary>
public static class NativeBattleThundaraController
{
    public const uint MainDescriptor = 0x2544DD0;
    public const uint MainBank = 0x2544368;
    public const uint PrimaryDescriptor = 0x2544DE0;
    public const uint PrimaryBank = 0x2543F18;
    public const uint SecondaryDescriptor = 0x2544DF0;
    public const uint SecondaryBank = 0x2544380;
    public const uint TertiaryDescriptor = 0x2544E00;
    public const uint TertiaryBank = 0x2544060;
    private const uint ActorBank = 0x1D972C0;

    public static NativeBattleOriginalEffectState Initialize6DC2E0(NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        if (effect.Binding.Entry != 0x6DC2D0 || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Thundara requires its original6DC2D0 entry and actual50AFC0 action parameters.");
        }
        NativeBattleActionHeaderSlot header = packets.Headers[effect.Parameters.HeaderIndex7C];
        if (effect.Parameters.HeaderIndex7C + header.ReadByte(17) >= NativeBattleActionPacketsState.HeaderCapacity)
        {
            throw new NotSupportedException("Native Thundara's explicit header list exceeds its physical action-header arena.");
        }
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 20, 1);
        (main, int root) = main.Append(0x6DC390);
        main = Put(main, root, Clear(main.Slots[root], 8));
        return effect with { Pools = [main, NativeBattleEffectTaskPool.Initialize(PrimaryDescriptor, PrimaryBank, 108, 3), NativeBattleEffectTaskPool.Initialize(SecondaryDescriptor, SecondaryBank, 44, 60), NativeBattleEffectTaskPool.Initialize(TertiaryDescriptor, TertiaryBank, 24, 32)], ReturnedDescriptor = MainDescriptor };
    }

    public static NativeBattleThundaraFrame Advance6DC390(NativeBattleThundaraFrame frame, NativeBattleThundaraInputs inputs)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Thundara lost its actual action parameters.");
        NativeBattleEffectTaskPool main = Pool(MainDescriptor);
        NativeBattleEffectTaskPool primary = Pool(PrimaryDescriptor);
        NativeBattleEffectTaskPool secondary = Pool(SecondaryDescriptor);
        NativeBattleEffectTaskPool tertiary = Pool(TertiaryDescriptor);
        NativeBattleEffectTaskStep rootStep = main.Advance((pool, slot) =>
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != 0x6DC390)
            {
                throw new NotSupportedException("Native Thundara's main bank requires6DC390.");
            }
            if (ReadByte(task, 15) == 0)
            {
                return new(Put(pool, slot, WriteByte(task, 15, 1)), 0);
            }
            bool alternate = ReadDword(task, 16) == 0;
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = alternate ? 0x8000 : 0 } };
            task = WriteDword(task, 16, alternate ? 1u : 0u);
            int sequence = ReadByte(task, 14);
            if (Word(task, 12) == 0 && sequence <= Header(0).ReadByte(17))
            {
                NativeBattleActionHeaderSlot header = Header(sequence);
                int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Thundara requires its actual header contact pointer.");
                int actor = inputs.Packets.ReadContact(contact).Primary.ActorIndex;
                uint actorAddress = ActorAddress(actor);
                bool duplicate = primary.Slots.Any(current => (current.Flags & 1) != 0 && ReadDword(current, 16) == actorAddress && Word(current, 12) <= 26);
                if (duplicate)
                {
                    task = task.WriteWord(12, 0);
                }
                else
                {
                    (primary, int child) = primary.Append(0x6DC510);
                    NativeBattleModulePoolTask next = Clear(primary.Slots[child], 96).WriteWord(14, (ushort)sequence);
                    next = WriteDword(next, 16, actorAddress);
                    next = Position(next, 20, inputs.ReadBone241(actor));
                    primary = Put(primary, child, next);
                    task = WriteByte(task, 14, unchecked((byte)(sequence + 1)));
                }
            }
            NativeBattleEffectTaskStep first = primary.Advance(Primary);
            primary = first.Pool;
            NativeBattleEffectTaskStep second = secondary.Advance(Secondary);
            secondary = second.Pool;
            NativeBattleEffectTaskStep third = tertiary.Advance(Tertiary);
            tertiary = third.Pool;
            if (first.Survivors == 0 && second.Survivors == 0 && third.Survivors == 0)
            {
                return new(Put(pool, slot, task), 2);
            }
            task = Add(task, 12, 1);
            if (Word(task, 12) >= 15)
            {
                task = task.WriteWord(12, 0);
            }
            return new(Put(pool, slot, task), 0);
        });
        return frame with { Effect = frame.Effect with { Pools = [rootStep.Pool, primary, secondary, tertiary], ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : MainDescriptor } };

        NativeBattleEffectTaskCallback Primary(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != 0x6DC510)
            {
                throw new NotSupportedException($"Native Thundara primary callback{task.Callback:X8} is unsupported.");
            }
            int phase = Word(task, 12);
            int actor = ActorIndex(ReadDword(task, 16));
            NativeBattleModuleAnchor position = Anchor(task, 20);
            if (phase == 4)
            {
                Emit(new NativeBattleThundaraSound(position));
            }
            if (phase == 0)
            {
                int radius = (inputs.ReadTransform(actor).Scale38 >> 1) + 200;
                for (int index = 0; index < 20; index++)
                {
                    (secondary, int child) = secondary.Append(0x6DCC00);
                    task = WriteDword(task, 104 - 4 * index, SecondaryBank + (uint)child * 44);
                    int angle = Rand();
                    NativeBattleModulePoolTask next = Clear(secondary.Slots[child], 32);
                    next = WriteByte(next, 14, (byte)(Rand() & 7));
                    next = next.WriteWord(16, unchecked((ushort)(position.X + (NativeBattleCameraMath.Sine(angle) * radius >> 12)))).WriteWord(18, unchecked((ushort)(position.Y + 500))).WriteWord(20, unchecked((ushort)(position.Z + (NativeBattleCameraMath.Cosine(angle) * radius >> 12))));
                    short velocity = unchecked((short)(-512 - (Rand() & 1023)));
                    next = next.WriteWord(26, unchecked((ushort)velocity)).WriteWord(34, unchecked((ushort)(-velocity >> 4))).WriteWord(42, (ushort)((Rand() & 15) + 24));
                    next = WriteByte(next, 15, unchecked((byte)Rand()));
                    secondary = Put(secondary, child, next);
                }
            }
            if (phase == 12)
            {
                for (int index = 0; index < 20; index++)
                {
                    int child = SecondarySlot(ReadDword(task, 104 - 4 * index));
                    NativeBattleModulePoolTask current = secondary.Slots[child];
                    if ((current.Flags & 1) == 0 || current.Callback != 0x6DCC00)
                    {
                        throw new NotSupportedException("Native Thundara's retained child pointer has no live6DCC00 owner.");
                    }
                    current = current.WriteWord(24, unchecked((ushort)((Word(current, 16) - position.X) << 4))).WriteWord(26, 0).WriteWord(28, unchecked((ushort)((Word(current, 20) - position.Z) << 4)));
                    current = current.WriteWord(32, unchecked((ushort)(-Word(current, 24) / 10))).WriteWord(36, unchecked((ushort)(-Word(current, 28) / 10))).WriteWord(42, unchecked((ushort)(-10 - (Rand() & 7))));
                    secondary = Put(secondary, child, current);
                }
                Emit(new NativeBattleThundaraFlash());
            }
            if (phase > 10)
            {
                int t = phase - 11;
                NativeBattleModuleAnchor raised = position with { Y = unchecked((short)(position.Y + 200)) };
                short ground = inputs.ReadTransform(actor).Angle36;
                if (t is >= 0 and <= 7)
                {
                    Sprite(task, 0x131E878, t, position);
                }
                if (t is >= 2 and <= 9)
                {
                    Sprite(task, 0x131EABC, t - 2, raised);
                    Sprite(task, 0x131ECB0, t - 2, position);
                }
                if (t is >= 2 and <= 10)
                {
                    Sprite(task, 0x131F0E8, t - 2, position);
                }
                if (t is >= 1 and <= 12)
                {
                    Emit(new NativeBattleThundaraGeometry(0x6DCD90, position, 512, t - 1, 12) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(102, task, 16) });
                }
                if (t is >= 0 and <= 11)
                {
                    Emit(new NativeBattleThundaraGeometry(0x6DCEE0, position, ground, t, 12) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(102, task, 17) });
                }
                if (t is >= 5 and <= 13)
                {
                    Sprite(task, 0x131FF74, t - 5, position);
                }
                if (t is >= 5 and <= 12)
                {
                    Sprite(task, 0x131EECC, t - 5, raised);
                }
                if (t is >= 3 and <= 14)
                {
                    Emit(new NativeBattleThundaraGeometry(0x6DCD90, position, -512, t - 3, 12) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(102, task, 18) });
                }
                if (t is >= 4 and <= 19)
                {
                    Emit(new NativeBattleThundaraGeometry(0x6DCFE0, position, ground, t - 4, 16) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(102, task, 19) });
                }
                if (t is >= 8 and <= 16)
                {
                    Sprite(task, 0x131F458, t - 8, position);
                }
                if (t is >= 11 and <= 19)
                {
                    Sprite(task, 0x131F5B4, t - 11, position);
                }
                if (t is >= 9 and <= 18)
                {
                    byte gray = unchecked((byte)(128 - (NativeBattleCameraMath.Sine(((t - 9) << 10) / 10) >> 7)));
                    Sprite(task, 0x131FA68, t - 9, position, 4, Rgb(gray));
                }
                if (t == 12)
                {
                    Emit(new NativeBattleThundaraFlash());
                }
                if (t is >= 4 and <= 18 && (t & 1) == 0)
                {
                    int radius = (inputs.ReadTransform(actor).Scale38 >> 1) + 300;
                    for (int index = 0; index < 2; index++)
                    {
                        (tertiary, int child) = tertiary.Append(0x6DCD10);
                        int angle = Rand();
                        int a = Rand();
                        int b = Rand();
                        NativeBattleModulePoolTask next = Clear(tertiary.Slots[child], 12).WriteWord(16, unchecked((ushort)(position.X + (NativeBattleCameraMath.Sine(angle) * radius >> 12)))).WriteWord(18, unchecked((ushort)(position.Y + (unchecked((a + b - 32768) * radius) >> 16)))).WriteWord(20, unchecked((ushort)(position.Z + (NativeBattleCameraMath.Cosine(angle) * radius >> 12))));
                        tertiary = Put(tertiary, child, next);
                    }
                }
            }
            if (phase == 26)
            {
                int contact = Header(Word(task, 14)).FirstContactIndex ?? throw new NotSupportedException("Native Thundara contact requires its actual physical header pointer.");
                Emit(new NativeBattleThundaraContact(contact));
            }
            if (phase >= 31)
            {
                return new(Put(pool, slot, task), 2);
            }
            return new(Put(pool, slot, Add(task, 12, 1)), 0);
        }

        NativeBattleEffectTaskCallback Secondary(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != 0x6DCC00)
            {
                throw new NotSupportedException($"Native Thundara secondary callback{task.Callback:X8} is unsupported.");
            }
            if (Word(task, 12) >= ReadByte(task, 14))
            {
                Sprite(task, 0x131F244, ReadByte(task, 15) % 20, Anchor(task, 16), 4, Rgb(unchecked((byte)task.ReadWord(40))));
                task = Add(task, 24, Word(task, 32));
                task = Add(task, 26, Word(task, 34));
                task = Add(task, 28, Word(task, 36));
                task = WriteByte(task, 15, unchecked((byte)(ReadByte(task, 15) + 1)));
                task = Add(task, 16, Word(task, 24) >> 4);
                task = Add(task, 18, Word(task, 26) >> 4);
                task = Add(task, 20, Word(task, 28) >> 4);
                task = Add(task, 40, Word(task, 42));
                task = task.WriteWord(40, (ushort)Math.Clamp((int)Word(task, 40), 0, 128));
            }
            task = Add(task, 12, 1);
            return new(Put(pool, slot, task), Word(task, 12) >= 32 ? 2 : 0);
        }

        NativeBattleEffectTaskCallback Tertiary(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != 0x6DCD10)
            {
                throw new NotSupportedException($"Native Thundara tertiary callback{task.Callback:X8} is unsupported.");
            }
            Emit(new NativeBattleThundaraSprite(0x131E760, ReadByte(task, 12) & 7, Anchor(task, 16), 3072, -768, 0, 0) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(102, task) });
            task = Add(task, 12, 1);
            return new(Put(pool, slot, task), Word(task, 12) >= 8 ? 2 : 0);
        }

        int Rand()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return value;
        }

        void Emit(NativeBattleThundaraOperation operation) => frame = inputs.Execute(frame, operation);
        void Sprite(NativeBattleModulePoolTask task, uint resource, int phase, NativeBattleModuleAnchor position, ushort flags = 0, uint color = 0) => Emit(new NativeBattleThundaraSprite(resource, phase, position, 4096, -1024, flags, color) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(102, task, SpritePrimitive(resource)) });
        NativeBattleEffectTaskPool Pool(uint descriptor) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == descriptor);
        NativeBattleActionHeaderSlot Header(int sequence) => inputs.Packets.Headers[checked(parameters.HeaderIndex7C + sequence)];
    }

    private static int SpritePrimitive(uint resource) => resource switch
    {
        0x131E878 => 0,
        0x131EABC => 1,
        0x131ECB0 => 2,
        0x131F0E8 => 3,
        0x131FF74 => 4,
        0x131EECC => 5,
        0x131F458 => 6,
        0x131F5B4 => 7,
        0x131FA68 => 8,
        0x131F244 => 9,
        _ => throw new NotSupportedException("Native Thundara sprite requires an authored resource selector.")
    };

    public static int ActorIndex(uint address)
    {
        long offset = (long)address - ActorBank;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new NotSupportedException("Native Thundara requires an exact retained156-byte graphics row pointer.");
        }
        return (int)(offset / 156);
    }

    public static int SecondarySlot(uint pointer)
    {
        long offset = (long)pointer - SecondaryBank;
        if (offset < 0 || offset % 44 != 0 || offset / 44 >= 60)
        {
            throw new NotSupportedException("Native Thundara's retained pointer is outside its actual secondary task bank.");
        }
        return (int)(offset / 44);
    }

    private static uint ActorAddress(int actor)
    {
        if ((uint)actor >= 7)
        {
            throw new NotSupportedException("Native Thundara's contact selects an unowned graphics row.");
        }
        return ActorBank + (uint)actor * 156;
    }

    private static NativeBattleModulePoolTask Clear(NativeBattleModulePoolTask task, int bytes)
    {
        if (task.PayloadLength != bytes)
        {
            throw new InvalidDataException("Native Thundara's clear requires the exact current physical payload extent.");
        }
        return task.ClearPayload(bytes);
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + value)));
    private static byte ReadByte(NativeBattleModulePoolTask task, int offset) => task.ReadByte(12 + (offset - 12));
    private static uint ReadDword(NativeBattleModulePoolTask task, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(task.ReadRawPayloadSpan()[(offset - 12)..]);
    private static NativeBattleModulePoolTask WriteByte(NativeBattleModulePoolTask task, int offset, byte value)
    {
        return task.WriteByte(offset, value);
    }

    private static NativeBattleModulePoolTask WriteDword(NativeBattleModulePoolTask task, int offset, uint value)
    {
        return task.WriteDword(offset, value);
    }

    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool.WriteSlot(slot, task);
    private static NativeBattleModuleAnchor Anchor(NativeBattleModulePoolTask task, int offset) => new(Word(task, offset), Word(task, offset + 2), Word(task, offset + 4), Word(task, offset + 6));
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, int offset, NativeBattleVector position) => task.WriteWord(offset, unchecked((ushort)position.X)).WriteWord(offset + 2, unchecked((ushort)position.Y)).WriteWord(offset + 4, unchecked((ushort)position.Z));
    private static uint Rgb(byte value) => (uint)(value | value << 8 | value << 16);
}
