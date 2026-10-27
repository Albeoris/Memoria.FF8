using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAeroFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleAeroOperation;
public sealed record NativeBattleAeroNodeConfig(NativeBattleModuleAnchor Anchor, int ScaleX, int ScaleY, int ScaleZ, bool GlobalScale, int DepthBias, int MorphArenaOffset);
public sealed record NativeBattleAeroNode(NativeBattleGfAnimationSource Source, NativeBattleGfAnimationNode Node, NativeBattleAeroNodeConfig Config) : NativeBattleAeroOperation;
public sealed record NativeBattleAeroParticle(NativeBattleModulePoolTask Task) : NativeBattleAeroOperation;
public sealed record NativeBattleAeroSound(NativeBattleModuleAnchor Position) : NativeBattleAeroOperation;
public sealed record NativeBattleAeroContact(int Contact) : NativeBattleAeroOperation;
public sealed record NativeBattleAeroInputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleModelTransform> ReadTransform, Func<int, NativeBattleModuleAnchor> ReadPosition, Func<NativeBattleAeroFrame, NativeBattleAeroOperation, NativeBattleAeroFrame> Execute);

/// <summary>Aero's two retained animation workspaces and physical particles with parent-owned timing.</summary>
public static class NativeBattleAeroController
{
    public const uint MainDescriptor = 0x2521640;
    public const uint MainBank = 0x25215E8;
    public const uint PrimaryDescriptor = 0x2521630;
    public const uint ParticleDescriptor = 0x2521620;
    public const uint PrimaryBank = NativeBattleEffectNodeMemory.ArenaAddress;
    public const uint ParticleBank = PrimaryBank + 0x13A4;
    public const int MorphArenaOffset = 0x22A4;
    public const int MorphLength = 0x2E0;
    public const int FirstWorkspaceOffset = 0x1C;
    public const int FirstWorkspaceLength = 0x4EC;
    public const int SecondWorkspaceOffset = 0x508;
    public const int SecondWorkspaceLength = 0x184;
    private const uint ActorBank = 0x1D972C0;

    public static NativeBattleOriginalEffectState Initialize6C0CB0(NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        if (effect.Binding.Entry != 0x6C0C80 || effect.Parameters is null || effect.Binding.AeroResources is null)
        {
            throw new InvalidDataException("Native Aero requires its original6C0C80 entry, action parameters and resources.");
        }
        int first = effect.Parameters.HeaderIndex7C;
        int last = packets.Headers[first].ReadByte(17);
        if (last >= 3 || first + last >= NativeBattleActionPacketsState.HeaderCapacity)
        {
            throw new NotSupportedException("Native Aero owns exactly three primary task slots.");
        }
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 20, 1);
        (main, int slot) = main.Append(0x6C0D20);
        main = Put(main, slot, Clear(main.Slots[slot], 8));
        return effect with { Pools = [main], ReturnedDescriptor = MainDescriptor };
    }

    public static NativeBattleAeroFrame Advance6C0D20(NativeBattleAeroFrame frame, NativeBattleAeroInputs inputs)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Aero lost its actual action parameters.");
        NativeBattleAeroResources resources = frame.Effect.Binding.AeroResources ?? throw new NotSupportedException("Native Aero requires its original animation and geometry resources.");
        NativeBattleEffectTaskPool main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == MainDescriptor);
        NativeBattleEffectTaskPool? primary = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == PrimaryDescriptor);
        NativeBattleEffectTaskPool? particles = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == ParticleDescriptor);
        NativeBattleEffectTaskStep rootStep = main.Advance((pool, slot) =>
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            Require(task, 0x6C0D20);
            bool alternate = Dword(task, 16) == 0;
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = alternate ? 0x1A584 : 0x2584 } };
            task = WriteDword(task, 16, alternate ? 1u : 0u);
            if (Word(task, 12) == 1)
            {
                if (task.ReadByte(15) == 0)
                {
                    primary = NativeBattleEffectTaskPool.Initialize(PrimaryDescriptor, PrimaryBank, 0x68C, 3);
                    particles = NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, ParticleBank, 0x3C, 64);
                    task = WriteByte(task, 15, 1);
                }
                int sequence = task.ReadByte(14);
                if (sequence <= Header(0).ReadByte(17))
                {
                    int contact = Header(sequence).FirstContactIndex ?? throw new NotSupportedException("Native Aero requires its actual header contact owner.");
                    int actor = inputs.Packets.ReadContact(contact).Primary.ActorIndex;
                    (primary, int child) = primary!.Append(0x6C0EC0);
                    NativeBattleModulePoolTask next = Clear(primary.Slots[child], 0x680).WriteWord(14, (ushort)sequence);
                    next = WriteDword(next, 16, ActorAddress(actor));
                    next = Position(next, 20, inputs.ReadPosition(actor));
                    NativeBattleGfAnimationWorkspace first = ReadWorkspace(next, true).Initialize7016B0(resources.SourceA, FirstWorkspaceLength);
                    NativeBattleGfAnimationWorkspace second = ReadWorkspace(next, false).Initialize7016B0(resources.SourceB, SecondWorkspaceLength);
                    next = StoreWorkspace(StoreWorkspace(next, true, first), false, second);
                    primary = Put(primary, child, next);
                    task = WriteByte(task, 14, (byte)(sequence + 1));
                }
            }
            if (task.ReadByte(15) != 0)
            {
                NativeBattleEffectTaskStep first = primary!.Advance(Primary);
                primary = first.Pool;
                NativeBattleEffectTaskStep second = particles!.Advance(Particle);
                particles = second.Pool;
                if (first.Survivors == 0 && second.Survivors == 0)
                {
                    return new(Put(pool, slot, task), 2);
                }
            }
            task = Add(task, 12, 1);
            if (Word(task, 12) >= 10)
            {
                task = task.WriteWord(12, 0);
            }
            return new(Put(pool, slot, task), 0);
        });
        ImmutableArray<NativeBattleEffectTaskPool> pools = primary is null ? [rootStep.Pool] : [rootStep.Pool, primary, particles!];
        return frame with { Effect = frame.Effect with { Pools = pools, ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : MainDescriptor } };

        NativeBattleEffectTaskCallback Primary(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            Require(task, 0x6C0EC0);
            int phase = Word(task, 12);
            int actor = ActorIndex(Dword(task, 16));
            if (phase == 8)
            {
                Emit(new NativeBattleAeroSound(Anchor(task, 20)));
            }
            NativeBattleModelTransform transform = inputs.ReadTransform(actor);
            int scale = (transform.Scale38 >> 1) + 2048;
            int remaining = 1;
            if (phase > 10)
            {
                NativeBattleModuleAnchor firstAnchor = Anchor(task, 20) with { Y = transform.Angle36 };
                NativeBattleAeroNodeConfig config = new(firstAnchor, scale, scale, scale, true, (-2048 - transform.Scale38) >> 5, MorphArenaOffset);
                NativeBattleGfAnimationAdvance first = ReadWorkspace(task, true).Advance701970(resources.SourceA, 0, (current, node) =>
                {
                    Emit(new NativeBattleAeroNode(resources.SourceA, node, config));
                    return current;
                });
                task = StoreWorkspace(task, true, first.Workspace);
                config = config with { Anchor = inputs.ReadPosition(actor) };
                NativeBattleGfAnimationAdvance second = ReadWorkspace(task, false).Advance701970(resources.SourceB, 0, (current, node) =>
                {
                    Emit(new NativeBattleAeroNode(resources.SourceB, node, config));
                    return current;
                });
                task = StoreWorkspace(task, false, second.Workspace);
                remaining = first.Remaining | second.Remaining;
            }
            if ((ushort)phase < 20 && particles!.Slots.Any(task => (task.Flags & 1) == 0))
            {
                (particles, int child) = particles.Append(0x6C14B0);
                int height = Rand();
                int angle = Rand();
                NativeBattleModulePoolTask next = Clear(particles.Slots[child], 48).WriteWord(12, task.ReadWord(14)).WriteWord(14, unchecked((ushort)scale)).WriteWord(16, 512).WriteWord(18, (ushort)angle).WriteWord(20, 64).WriteWord(22, task.ReadWord(20)).WriteWord(24, task.ReadWord(24)).WriteWord(28, (ushort)(height & 255)).WriteWord(36, unchecked((ushort)-4));
                for (int axis = 0; axis < 3; axis++)
                {
                    next = next.WriteWord(42 + axis * 2, (ushort)Rand());
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    next = WriteByte(next, 50 + axis, unchecked((byte)(Rand() + 128)));
                }
                next = WriteDword(next, 56, PrimaryBank + (uint)slot * 0x68C);
                particles = Put(particles, child, next);
            }
            task = Add(task, 12, 1);
            if (remaining == 0)
            {
                Emit(new NativeBattleAeroContact(Header(task.ReadWord(14)).FirstContactIndex ?? throw new NotSupportedException("Native Aero lost its sequence contact owner.")));
                task = task.WriteWord(14, 0xFFFF);
                return new(Put(pool, slot, task), 2);
            }
            return new(Put(pool, slot, task), 0);
        }

        NativeBattleEffectTaskCallback Particle(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            Require(task, 0x6C14B0);
            NativeBattleModulePoolTask parent = primary!.Slots[PrimarySlot(Dword(task, 56))];
            int parentPhase = Word(parent, 12);
            if (parentPhase < 40)
            {
                if (parentPhase >= 10)
                {
                    task = Add(Add(task, 36, -4), 20, 16);
                }
                task = Add(task, 16, 256);
                if (Word(task, 16) > 4096)
                {
                    task = task.WriteWord(16, 4096);
                }
                task = Add(task, 28, Word(task, 36));
                int q = Word(task, 28) >> 6;
                int radius = (Word(task, 14) << 5) / (q * q + 512);
                task = Add(task, 18, Word(task, 20));
                short cosine = unchecked((short)NativeBattleCameraMath.Cosine(Word(task, 18)));
                short sine = unchecked((short)NativeBattleCameraMath.Sine(Word(task, 18)));
                task = task.WriteWord(34, unchecked((ushort)cosine)).WriteWord(38, unchecked((ushort)sine));
                task = task.WriteWord(26, unchecked((ushort)((unchecked(cosine * radius) >> 9) + Word(task, 22)))).WriteWord(30, unchecked((ushort)((unchecked(sine * radius) >> 9) + Word(task, 24))));
            }
            else
            {
                short oldAge = Word(task, 48);
                task = Add(task, 48, 1);
                if (oldAge > 16)
                {
                    task = Add(task, 16, -512);
                    if (Word(task, 16) <= 0)
                    {
                        return new(Put(pool, slot, task), 2);
                    }
                }
                task = Add(task, 36, 16);
                task = Add(task, 28, Word(task, 36));
                if (Word(task, 28) >= 0)
                {
                    task = task.WriteWord(36, unchecked((ushort)-Word(task, 36)));
                }
                task = Add(Add(task, 26, Word(task, 34) >> 6), 30, Word(task, 38) >> 6);
            }
            for (int axis = 0; axis < 3; axis++)
            {
                task = Add(task, 42 + axis * 2, unchecked((sbyte)task.ReadByte(50 + axis)) << 3);
            }
            Emit(new NativeBattleAeroParticle(task));
            return new(Put(pool, slot, task), 0x58);
        }

        NativeBattleActionHeaderSlot Header(int sequence) => inputs.Packets.Headers[parameters.HeaderIndex7C + sequence];
        int Rand()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return value;
        }
        void Emit(NativeBattleAeroOperation operation) => frame = inputs.Execute(frame, operation);
    }

    public static NativeBattleGfAnimationWorkspace ReadWorkspace(NativeBattleModulePoolTask task, bool first) => new(task.ReadRawPayloadSpan().Slice((first ? FirstWorkspaceOffset : SecondWorkspaceOffset) - 12, first ? FirstWorkspaceLength : SecondWorkspaceLength).ToArray().ToImmutableArray());
    public static int PrimarySlot(uint pointer) => Index(pointer, PrimaryBank, 0x68C, 3);
    public static int ActorIndex(uint pointer) => Index(pointer, ActorBank, 156, 7);
    private static int Index(uint pointer, uint bank, uint stride, uint count)
    {
        uint delta = unchecked(pointer - bank);
        if (delta % stride != 0 || delta / stride >= count)
        {
            throw new NotSupportedException("Native Aero pointer has no exact physical task or actor owner.");
        }
        return (int)(delta / stride);
    }
    private static uint ActorAddress(int actor) => actor is >= 0 and < 7 ? ActorBank + (uint)actor * 156 : throw new NotSupportedException("Native Aero requires an actual actor graphics row.");
    private static NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, bool first, NativeBattleGfAnimationWorkspace workspace)
    {
        int expected = first ? FirstWorkspaceLength : SecondWorkspaceLength;
        if (workspace.Data.Length != expected)
        {
            throw new InvalidDataException("Native Aero animation workspace changed its physical extent.");
        }
        return task.WriteRetainedBytes((first ? FirstWorkspaceOffset : SecondWorkspaceOffset), workspace.Data.AsSpan());
    }
    private static void Require(NativeBattleModulePoolTask task, uint callback)
    {
        if (task.Callback != callback)
        {
            throw new NotSupportedException($"Native Aero callback{task.Callback:X8} is unsupported in this task bank.");
        }
    }
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static uint Dword(NativeBattleModulePoolTask task, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(task.ReadRawPayloadSpan().Slice(offset - 12, 4));
    private static NativeBattleModulePoolTask WriteDword(NativeBattleModulePoolTask task, int offset, uint value)
    {
        return task.WriteDword(12 + (offset - 12), unchecked((uint)(value)));
    }
    private static NativeBattleModulePoolTask WriteByte(NativeBattleModulePoolTask task, int offset, byte value) => task.WriteByte(12 + (offset - 12), value);
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + value)));
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, int offset, NativeBattleModuleAnchor position) => task.WriteWord(offset, unchecked((ushort)position.X)).WriteWord(offset + 2, unchecked((ushort)position.Y)).WriteWord(offset + 4, unchecked((ushort)position.Z)).WriteWord(offset + 6, unchecked((ushort)position.W));
    private static NativeBattleModuleAnchor Anchor(NativeBattleModulePoolTask task, int offset) => new(Word(task, offset), Word(task, offset + 2), Word(task, offset + 4), Word(task, offset + 6));
    private static NativeBattleModulePoolTask Clear(NativeBattleModulePoolTask task, int length)
    {
        return task.WriteRetainedBytes(12, new byte[length]);
    }
    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool.WriteSlot(slot, task);
}
