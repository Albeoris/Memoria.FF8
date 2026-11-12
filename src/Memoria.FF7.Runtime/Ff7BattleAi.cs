using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleAiActor(int Slot, uint Statuses, uint Conditions, ushort MagicPoints, ushort MaxMagicPoints, uint HitPoints, uint MaxHitPoints, ushort Defense = 0, ushort MagicDefense = 0, byte Variant = 0, byte IdleAction = 0, byte HurtAction = 1, ushort LastAttackerMask = 0, byte? Level = null, ushort? EnemyId = null, byte? Strength = null, byte? Magic = null, byte? Speed = null, byte? FormationRow = null, uint? ElementHalve = null, uint? ElementNullify = null, uint? ElementAbsorb = null, uint? ElementWeakness = null, uint? PreviousStatuses = null, uint? CaptureMask = null, byte? PresentationByte = null, ushort? LastPhysicalAttackerMask = null, ushort? LastMagicAttackerMask = null, ushort? ExtraElementAbsorb = null, byte? PhysicalEvasion = null, byte? HitEffect = null);
public sealed record Ff7BattleAiContext(int ActorSlot, IReadOnlyList<Ff7BattleAiActor> Actors, ushort EligibleMask, ushort KnockedOutMask, ushort ExcludedOpponentMask = 0, byte Callback = 1, byte? QueuePriority = null, byte[]? GlobalMemory = null, byte[]? SaveBank1 = null);
public sealed record Ff7BattleAiCommand(int ActorSlot, byte CommandId, ushort AttackId, int SceneAttackSlot, ushort TargetMask, byte[]? Message = null, byte Callback = 1, byte Priority = 2);
public sealed record Ff7BattleAiTransfer(int SourceSlot, int DestinationSlot);
public sealed record Ff7BattleAiResult(byte[] LocalMemory, Ff7BattleAiCommand[] Commands, Ff7BattleAiActor[] ActorUpdates)
{
    public byte[] GlobalMemory { get; init; } = [];
    public Ff7BattleAiTransfer[] TransferUpdates { get; init; } = [];
    public IReadOnlyDictionary<byte, byte> SaveBank1Writes { get; init; } = new Dictionary<byte, byte>();
}

/// <summary>Runs a single native enemy AI callback. The encounter owns the input projections and RNG.</summary>
public static class Ff7BattleAi
{
    public const int LocalMemorySize = 128;
    private const ushort ActorMask = 0x03FF;
    private const int StackCapacity = 512;
    private const int InstructionLimit = 16384;

    public static Ff7BattleAiResult RunActorCallback(Ff7BattleAiContext context, ReadOnlySpan<byte> localMemory, SceneBattleData formation, Ff7BattleRng random, ReadOnlyMemory<byte> kernelAttacks = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(formation);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(context.Callback, (byte)16);
        SceneBattleEnemy enemy = formation.Enemies.SingleOrDefault(enemy => enemy.FormationSlot + 4 == context.ActorSlot) ?? throw new InvalidDataException("FFVII enemy AI callback has no formation actor owner.");
        ReadOnlySpan<byte> script = enemy.Scripts is null ? [] : enemy.Scripts[context.Callback].Span;
        return Run(script, context, localMemory, formation, random, kernelAttacks);
    }

    public static Ff7BattleAiResult Run(ReadOnlySpan<byte> script, Ff7BattleAiContext context, ReadOnlySpan<byte> localMemory, SceneBattleData formation, Ff7BattleRng random, ReadOnlyMemory<byte> kernelAttacks = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(formation);
        ArgumentNullException.ThrowIfNull(random);
        if (!localMemory.IsEmpty && localMemory.Length != LocalMemorySize)
        {
            throw new InvalidDataException("FFVII battle AI actor memory has an invalid size.");
        }
        byte[] memory = localMemory.IsEmpty ? new byte[LocalMemorySize] : localMemory.ToArray();
        if (script.IsEmpty)
        {
            if (context.GlobalMemory is { Length: not 64 })
            {
                throw new InvalidDataException("FFVII battle AI global memory has an invalid size.");
            }
            return new(memory, [], []) { GlobalMemory = context.GlobalMemory?.ToArray() ?? new byte[64] };
        }
        Machine machine = new(script, context, memory, formation, random, kernelAttacks);
        return machine.Run();
    }

    private readonly record struct Value(int Width, uint Scalar, uint[]? Actors = null, ushort Mask = ActorMask, bool IsAddress = false)
    {
        public int EncodedSize
        {
            get
            {
                int valueSize = Math.Max(1, Width);
                if (IsAddress)
                {
                    return 3;
                }
                return Actors is null ? 1 + valueSize : 3 + BitOperations.PopCount(Mask) * valueSize;
            }
        }

        public uint At(int slot) => Actors is null ? Scalar : Actors[slot];

        public uint First() => Actors is null ? Scalar : Mask == 0 ? 0 : Actors[BitOperations.TrailingZeroCount(Mask)];

        public bool Any()
        {
            for (int slot = 0; slot < 10; slot++)
            {
                if ((Mask & (1 << slot)) != 0 && At(slot) != 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    private ref struct Machine
    {
        private readonly ReadOnlySpan<byte> script;
        private readonly byte[] local;
        private readonly SceneBattleData formation;
        private readonly Ff7BattleRng random;
        private readonly ReadOnlyMemory<byte> kernelAttacks;
        private readonly Ff7BattleAiActor?[] actors;
        private readonly Ff7BattleAiContext context;
        private readonly ushort allies;
        private readonly ushort opponents;
        private readonly ushort knockedOutAllies;
        private readonly ushort knockedOutOpponents;
        private readonly ushort allocated;
        private readonly ushort self;
        private readonly byte priority;
        private readonly Stack<Value> stack = new();
        private readonly List<Ff7BattleAiCommand> commands = new();
        private readonly byte[] global;
        private readonly List<Ff7BattleAiTransfer> transferUpdates = new();
        private readonly Dictionary<byte, byte> saveBank1Writes = new();
        private ushort updatedActors;
        private ushort target;
        private int stackBytes;
        private int pc;

        public Machine(ReadOnlySpan<byte> script, Ff7BattleAiContext context, byte[] local, SceneBattleData formation, Ff7BattleRng random, ReadOnlyMemory<byte> kernelAttacks)
        {
            this.script = script;
            this.local = local;
            this.formation = formation;
            this.random = random;
            this.kernelAttacks = kernelAttacks;
            this.context = context;
            if (context.GlobalMemory is not null && context.GlobalMemory.Length != 64)
            {
                throw new InvalidDataException("FFVII battle AI global memory has an invalid size.");
            }
            global = context.GlobalMemory?.ToArray() ?? new byte[64];
            actors = new Ff7BattleAiActor?[10];
            ushort party = 0;
            ushort enemies = 0;
            foreach (Ff7BattleAiActor actor in context.Actors)
            {
                if (actor.Slot is < 0 or 3 or > 9 || actors[actor.Slot] is not null)
                {
                    throw new InvalidDataException("FFVII battle AI actor slots are invalid.");
                }
                actors[actor.Slot] = actor;
                ushort bit = (ushort)(1 << actor.Slot);
                if (actor.Slot < 4)
                {
                    party |= bit;
                }
                else
                {
                    enemies |= bit;
                }
            }

            bool formationScript = context.ActorSlot == 3;
            if (context.ActorSlot is < 3 or > 9 || (!formationScript && actors[context.ActorSlot] is null) || (formationScript && context.GlobalMemory is null) || context.Callback >= 16 || context.QueuePriority is > 6 || ((context.EligibleMask | context.KnockedOutMask | context.ExcludedOpponentMask) & ~(party | enemies)) != 0)
            {
                throw new InvalidDataException("FFVII enemy AI context has an invalid owner or mask.");
            }
            if (formationScript)
            {
                allocated = (ushort)ReadMemory(global, 2, 0x50);
                self = (ushort)ReadMemory(global, 2, 0x60);
                target = (ushort)ReadMemory(global, 2, 0x70);
                allies = (ushort)ReadMemory(global, 2, 0x80);
                knockedOutAllies = (ushort)ReadMemory(global, 2, 0x90);
                opponents = (ushort)ReadMemory(global, 2, 0xA0);
                knockedOutOpponents = (ushort)ReadMemory(global, 2, 0xB0);
                priority = context.QueuePriority ?? 0;
                return;
            }
            Ff7BattleAiActor current = actors[context.ActorSlot]!;
            bool swapped = BitOperations.PopCount(current.Statuses & 0x00400040) == 1;
            ushort friendly = (ushort)((swapped ? party : enemies) & context.EligibleMask);
            ushort opposing = (ushort)((swapped ? enemies : party) & context.EligibleMask);
            allies = (ushort)(friendly & ~context.KnockedOutMask);
            opponents = (ushort)(opposing & ~context.KnockedOutMask & ~context.ExcludedOpponentMask);
            knockedOutAllies = (ushort)(friendly & context.KnockedOutMask);
            knockedOutOpponents = (ushort)(opposing & context.KnockedOutMask);
            allocated = (ushort)(party | enemies);
            self = (ushort)(1 << context.ActorSlot);
            priority = context.QueuePriority ?? (context.Callback switch { 1 => (byte)2, 6 => (byte)6, _ => (byte)0 });
            target = opponents;
            WriteMemory(global, 2, 0x50, allocated);
            WriteMemory(global, 2, 0x60, self);
            WriteMemory(global, 2, 0x70, target);
            WriteMemory(global, 2, 0x80, allies);
            WriteMemory(global, 2, 0x90, knockedOutAllies);
            WriteMemory(global, 2, 0xA0, opponents);
            WriteMemory(global, 2, 0xB0, knockedOutOpponents);
        }

        public Ff7BattleAiResult Run()
        {
            if (script.IsEmpty)
            {
                return new(local, [], []) { GlobalMemory = global.ToArray() };
            }
            for (int instructions = 0; instructions < InstructionLimit; instructions++)
            {
                int instruction = pc;
                byte opcode = (byte)ReadOperand(1);
                switch (opcode)
                {
                    case >= 0x00 and <= 0x03:
                        Load(opcode, (ushort)ReadOperand(2));
                        break;
                    case >= 0x10 and <= 0x13:
                        Push(new(opcode & 3, ReadOperand(2), IsAddress: true));
                        break;
                    case >= 0x30 and <= 0x37:
                        Arithmetic(opcode);
                        break;
                    case >= 0x40 and <= 0x45:
                        Compare(opcode);
                        break;
                    case 0x50:
                    case 0x51:
                        bool right = Pop().Any();
                        bool left = Pop().Any();
                        Push(Boolean(opcode == 0x50 ? left && right : left || right));
                        break;
                    case 0x52:
                        Push(Boolean(!Pop().Any()));
                        break;
                    case >= 0x60 and <= 0x62:
                        int width = opcode - 0x5F;
                        Push(new(width, ReadOperand(width)));
                        break;
                    case 0x70:
                    case 0x71:
                    case 0x72:
                        Branch(opcode);
                        break;
                    case 0x73:
                        return Complete();
                    case 0x80:
                        if (!Peek().IsAddress)
                        {
                            Value value = Pop();
                            Push(value with { Mask = (ushort)(value.Mask & Pop().At(0)) });
                        }
                        break;
                    case 0x81:
                        Push(new(2, random.NextWord()));
                        break;
                    case 0x82:
                        SelectRandom();
                        break;
                    case 0x83:
                        CountOrCollapse();
                        break;
                    case 0x87:
                        Push(new(2, 1U << ((int)Pop().At(0) & 31)));
                        break;
                    case 0x84:
                    case 0x85:
                        SelectExtreme(opcode == 0x84);
                        break;
                    case 0x86:
                        Push(new(2, ReadCastingCost(Pop().At(0))));
                        break;
                    case 0x90:
                        Store();
                        break;
                    case 0x91:
                        Pop();
                        break;
                    case 0x92:
                        QueueAttack();
                        break;
                    case 0x93:
                        QueueMessage();
                        break;
                    case 0x94:
                        TransferActors();
                        break;
                    case 0x95:
                        ExchangeSaveBank();
                        break;
                    default:
                        throw new NotSupportedException($"FFVII battle AI opcode 0x{opcode:X2} at script offset 0x{instruction:X} is unsupported.");
                }
            }
            throw new InvalidDataException("FFVII battle AI exceeded its instruction bound.");
        }

        private Ff7BattleAiResult Complete()
        {
            List<Ff7BattleAiActor> changed = new();
            for (int slot = 0; slot < actors.Length; slot++)
            {
                if ((updatedActors & (1 << slot)) != 0)
                {
                    changed.Add(actors[slot]!);
                }
            }
            return new(local, commands.ToArray(), changed.ToArray()) { GlobalMemory = global.ToArray(), TransferUpdates = transferUpdates.ToArray(), SaveBank1Writes = new Dictionary<byte, byte>(saveBank1Writes) };
        }

        private void Branch(byte opcode)
        {
            int destination = (int)ReadOperand(2);
            bool taken = opcode switch
            {
                0x70 => Pop().First() == 0,
                0x71 => Pop().First() != Peek().First(),
                _ => true
            };
            if (taken)
            {
                if (destination >= script.Length)
                {
                    throw new InvalidDataException("FFVII battle AI branch leaves its script owner.");
                }
                pc = destination;
            }
        }

        private void Load(int width, ushort address)
        {
            if (address < 0x2000)
            {
                Push(new(width, ReadLocal(width, address)));
                return;
            }
            if (address < 0x4000)
            {
                Push(new(width, ReadGlobal(width, address)));
                return;
            }
            uint[] values = new uint[10];
            for (int slot = 0; slot < values.Length; slot++)
            {
                if (actors[slot] is Ff7BattleAiActor actor)
                {
                    values[slot] = ReadActor(actor, width, address);
                }
            }
            Push(new(width, 0, values));
        }

        private uint ReadGlobal(int width, ushort address) => ReadMemory(global, width, address - 0x2000);

        private void CountOrCollapse()
        {
            Value value = Pop();
            Push(value.Actors is null ? new(1, (uint)BitOperations.PopCount(value.Scalar & ActorMask)) : new(value.Width, value.First()));
        }

        private void ExchangeSaveBank()
        {
            uint address = Pop().At(0);
            uint mode = Pop().At(0);
            if (mode > 1)
            {
                return;
            }
            if (address > byte.MaxValue || context.SaveBank1 is null || context.SaveBank1.Length != 256)
            {
                throw new NotSupportedException("FFVII battle AI save-bank exchange requires native bank 1.");
            }
            byte index = (byte)address;
            if (mode == 0)
            {
                global[2] = saveBank1Writes.TryGetValue(index, out byte changed) ? changed : context.SaveBank1[index];
            }
            else
            {
                saveBank1Writes[index] = global[2];
            }
        }

        private void TransferActors()
        {
            ushort sourceMask = (ushort)Pop().At(0);
            ushort destinationMask = (ushort)Pop().At(0);
            int sourceSlot = BitOperations.TrailingZeroCount((uint)(sourceMask & ActorMask));
            if (sourceSlot >= actors.Length || actors[sourceSlot] is not Ff7BattleAiActor source)
            {
                return;
            }
            for (int slot = 0; slot < actors.Length; slot++)
            {
                if ((destinationMask & (1 << slot)) != 0 && actors[slot] is Ff7BattleAiActor destination)
                {
                    actors[slot] = destination with { HitPoints = source.HitPoints, MagicPoints = source.MagicPoints, Statuses = source.Statuses, PreviousStatuses = source.PreviousStatuses };
                    updatedActors |= (ushort)(1 << slot);
                    transferUpdates.Add(new(sourceSlot, slot));
                }
            }
        }

        private static uint ReadActor(Ff7BattleAiActor actor, int width, ushort address)
        {
            return (width, address) switch
            {
                (0, >= 0x4000 and < 0x4020) => actor.Statuses >> (address - 0x4000) & 1,
                (0, >= 0x4020 and < 0x4040) => actor.Conditions >> (address - 0x4020) & 1,
                (3, 0x4000) => actor.Statuses,
                (3, 0x4020) => actor.Conditions,
                (1, 0x4048) => actor.Level ?? 0,
                (1, 0x4060) => actor.Variant,
                (1, 0x4068) => actor.Strength ?? 0,
                (1, 0x4070) => actor.Magic ?? 0,
                (1, 0x4078) => actor.PhysicalEvasion ?? 0,
                (1, 0x42B0) => actor.HitEffect ?? (byte)(actor.Slot < 3 ? 8 : 2),
                (1, 0x40A0) => actor.Speed ?? 0,
                (1, 0x4270) => actor.FormationRow ?? 0,
                (1, 0x4278) => actor.PresentationByte ?? 0xFF,
                (1, 0x4080) => actor.IdleAction,
                (1, 0x4088) => actor.HurtAction,
                (2, 0x40D0) => actor.LastAttackerMask,
                (2, 0x40E0) => actor.LastPhysicalAttackerMask ?? 0,
                (2, 0x40F0) => actor.LastMagicAttackerMask ?? 0,
                (2, 0x4100) => actor.Defense,
                (2, 0x4110) => actor.MagicDefense,
                (2, 0x4120) => actor.EnemyId ?? 0xFFFF,
                (2, 0x4130) => actor.ExtraElementAbsorb ?? 0,
                (2, 0x4140) => actor.MagicPoints,
                (2, 0x4150) => actor.MaxMagicPoints,
                (3, 0x4160) => actor.HitPoints,
                (3, 0x4180) => actor.MaxHitPoints,
                (3, 0x41A0) => actor.ElementHalve ?? 0,
                (3, 0x41C0) => actor.ElementNullify ?? 0,
                (3, 0x41E0) => actor.ElementAbsorb ?? 0,
                (3, 0x4200) => actor.ElementWeakness ?? 0,
                (3, 0x4220) => actor.PreviousStatuses ?? 0,
                (2, 0x4300) => (actor.CaptureMask ?? 0) & 0xFFFF,
                (3, 0x4300) => actor.CaptureMask ?? 0,
                _ => throw new NotSupportedException($"FFVII battle AI actor address 0x{address:X4} width {width} has no native projection.")
            };
        }

        private void Store()
        {
            uint value = Pop().First();
            Value reference = Pop();
            if (!reference.IsAddress)
            {
                throw new InvalidDataException("FFVII battle AI store requires an address on the stack.");
            }
            int address = (int)reference.Scalar;
            if (address >= 0x4000)
            {
                StoreActor(reference, value);
                return;
            }
            if (address >= 0x2000)
            {
                WriteMemory(global, reference.Width, address - 0x2000, value);
                target = (ushort)ReadMemory(global, 2, 0x70);
                return;
            }
            WriteMemory(local, reference.Width, address, value);
        }

        private static void WriteMemory(byte[] memory, int width, int address, uint value)
        {
            int offset = MemoryOffset(memory, width, address);
            switch (width)
            {
                case 0:
                    int bit = 1 << (address & 7);
                    memory[offset] = (byte)((memory[offset] & ~bit) | (value == 0 ? 0 : bit));
                    break;
                case 1:
                    memory[offset] = (byte)value;
                    break;
                case 2:
                    BinaryPrimitives.WriteUInt16LittleEndian(memory.AsSpan(offset), (ushort)value);
                    break;
                case 3:
                    BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(offset), value);
                    break;
            }
        }

        private static uint ReadMemory(byte[] memory, int width, int address)
        {
            int offset = MemoryOffset(memory, width, address);
            return width switch
            {
                0 => (uint)(memory[offset] >> (address & 7)) & 1,
                1 => memory[offset],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(memory.AsSpan(offset)),
                _ => BinaryPrimitives.ReadUInt32LittleEndian(memory.AsSpan(offset))
            };
        }

        private static int MemoryOffset(byte[] memory, int width, int address)
        {
            int size = width switch { 0 or 1 => 1, 2 => 2, _ => 4 };
            int offset = width switch { 0 or 1 => address >> 3, 2 => (address >> 4) * 2, _ => (address >> 5) * 4 };
            if (address < 0 || offset < 0 || offset + size > memory.Length)
            {
                throw new InvalidDataException("FFVII battle AI memory address exceeds its native owner.");
            }
            return offset;
        }

        private void StoreActor(Value reference, uint value)
        {
            Value selector = Pop();
            if (!selector.IsAddress || selector.Scalar >= 0x4000)
            {
                throw new NotSupportedException("FFVII battle AI actor store requires a native scalar selector address.");
            }
            ushort selectorAddress = (ushort)selector.Scalar;
            uint mask = selectorAddress < 0x2000 ? ReadLocal(selector.Width, selectorAddress) : ReadGlobal(selector.Width, selectorAddress);
            for (int slot = 0; slot < actors.Length; slot++)
            {
                if ((mask & (1 << slot)) == 0 || actors[slot] is not Ff7BattleAiActor actor)
                {
                    continue;
                }
                Ff7BattleAiActor changed = (reference.Width, reference.Scalar) switch
                {
                    (0, >= 0x4000 and < 0x4020) => actor with { Statuses = SetBit(actor.Statuses, (int)reference.Scalar - 0x4000, value) },
                    (0, >= 0x4020 and < 0x4040) => actor with { Conditions = SetBit(actor.Conditions, (int)reference.Scalar - 0x4020, value) },
                    (3, 0x4000) => actor with { Statuses = value },
                    (3, 0x4020) => actor with { Conditions = value },
                    (1, 0x4048) => actor with { Level = (byte)value },
                    (1, 0x4068) => actor with { Strength = (byte)value },
                    (1, 0x4070) => actor with { Magic = (byte)value },
                    (1, 0x4078) => actor with { PhysicalEvasion = (byte)value },
                    (1, 0x42B0) => actor with { HitEffect = (byte)value },
                    (2, 0x4130) => actor with { ExtraElementAbsorb = (ushort)value },
                    (1, 0x40A0) => actor with { Speed = (byte)value },
                    (1, 0x4270) => actor with { FormationRow = (byte)value },
                    (1, 0x4278) => actor with { PresentationByte = (byte)value },
                    (2, 0x4140) => actor with { MagicPoints = (ushort)value },
                    (2, 0x4150) => actor with { MaxMagicPoints = (ushort)value },
                    (3, 0x4160) => actor with { HitPoints = value },
                    (3, 0x4180) => actor with { MaxHitPoints = value },
                    (3, 0x41A0) => actor with { ElementHalve = value },
                    (3, 0x41C0) => actor with { ElementNullify = value },
                    (3, 0x41E0) => actor with { ElementAbsorb = value },
                    (3, 0x4200) => actor with { ElementWeakness = value },
                    (3, 0x4220) => actor with { PreviousStatuses = value },
                    (2, 0x4300) => actor with { CaptureMask = ((actor.CaptureMask ?? 0) & 0xFFFF0000) | (ushort)value },
                    (3, 0x4300) => actor with { CaptureMask = value },
                    (1, 0x4080) => actor with { IdleAction = (byte)value },
                    (1, 0x4088) => actor with { HurtAction = (byte)value },
                    (2, 0x4100) => actor with { Defense = (ushort)value },
                    (2, 0x4110) => actor with { MagicDefense = (ushort)value },
                    _ => throw new NotSupportedException($"FFVII battle AI actor write to 0x{reference.Scalar:X4} width {reference.Width} has no native owner.")
                };
                if (changed != actor)
                {
                    actors[slot] = changed;
                    updatedActors |= (ushort)(1 << slot);
                }
            }
        }

        private static uint SetBit(uint current, int bit, uint value)
        {
            uint mask = 1U << bit;
            return (current & ~mask) | (value == 0 ? 0 : mask);
        }

        private uint ReadLocal(int width, int address) => ReadMemory(local, width, address);

        private void Arithmetic(byte opcode)
        {
            Value right = opcode == 0x37 ? new(0, 0) : Pop();
            Value left = Pop();
            int width = Math.Max(left.Width, right.Width);
            if (left.Actors is null && right.Actors is null)
            {
                Push(new(width, Calculate(opcode, left.Scalar, right.Scalar)));
                return;
            }
            ushort mask = (ushort)(left.Mask & right.Mask);
            uint[] values = new uint[10];
            for (int slot = 0; slot < values.Length; slot++)
            {
                if ((mask & (1 << slot)) != 0)
                {
                    values[slot] = Calculate(opcode, left.At(slot), right.At(slot));
                }
            }
            Push(new(width, 0, values, mask));
        }

        private static uint Calculate(byte opcode, uint left, uint right)
        {
            return opcode switch
            {
                0x30 => unchecked(left + right),
                0x31 => unchecked(left - right),
                0x32 => unchecked(left * right),
                0x33 when right != 0 => left / right,
                0x34 when right != 0 => left % right,
                0x35 => left & right,
                0x36 => left | right,
                0x37 => ~left,
                _ => throw new InvalidDataException("FFVII battle AI attempted division by zero.")
            };
        }

        private void Compare(byte opcode)
        {
            Value right = Pop();
            Value left = Pop();
            int matches = 0;
            for (int slot = 0; slot < 10; slot++)
            {
                if (((left.Mask & right.Mask) & (1 << slot)) == 0)
                {
                    continue;
                }
                uint lhs = left.At(slot);
                uint rhs = right.At(slot);
                bool equal = opcode switch
                {
                    0x40 => lhs == rhs,
                    0x41 => lhs != rhs,
                    0x42 => lhs >= rhs,
                    0x43 => lhs <= rhs,
                    0x44 => lhs > rhs,
                    _ => lhs < rhs
                };
                if (equal)
                {
                    matches |= 1 << slot;
                }
            }
            Push(left.Actors is null && right.Actors is null ? Boolean(matches != 0) : new(2, (uint)matches));
        }

        private void SelectRandom()
        {
            uint mask = (ushort)Pop().At(0);
            if (mask == 0)
            {
                Push(new(2, 0));
                return;
            }
            int ordinal = random.NextByte() % BitOperations.PopCount(mask);
            while (ordinal-- > 0)
            {
                mask &= mask - 1;
            }
            Push(new(2, mask & unchecked(0U - mask)));
        }

        private void SelectExtreme(bool greatest)
        {
            Value source = Pop();
            uint best = 0;
            uint result = 0;
            for (int slot = 0; slot < 10; slot++)
            {
                uint bit = 1U << slot;
                if ((source.Mask & bit) == 0)
                {
                    continue;
                }
                uint value = source.At(slot);
                if (result == 0 || (greatest ? value > best : value < best))
                {
                    best = value;
                    result = bit;
                }
                else if (value == best)
                {
                    result |= bit;
                }
            }
            Push(new(2, result));
        }

        private ushort ReadCastingCost(uint attackId)
        {
            if (attackId >= ushort.MaxValue)
            {
                return 0;
            }
            if (attackId >= 256)
            {
                return formation.Attacks.FirstOrDefault(attack => attack.Id == attackId)?.Data.CastingCost ?? 0;
            }
            int offset = (int)attackId * Ff7BattleAttack.RecordSize;
            if (offset + Ff7BattleAttack.RecordSize > kernelAttacks.Length)
            {
                throw new NotSupportedException("FFVII battle AI spell cost requires the kernel attack owner.");
            }
            return Ff7BattleAttack.Read(kernelAttacks.Span.Slice(offset, Ff7BattleAttack.RecordSize)).CastingCost;
        }

        private void QueueAttack()
        {
            ushort id = (ushort)Pop().At(0);
            ushort command = (ushort)Pop().At(0);
            ushort actionOwner = (ushort)ReadGlobal(2, 0x2060);
            int slot = actionOwner == 0 ? 0 : BitOperations.Log2(actionOwner);
            if (command == 0x22 && id >= 16)
            {
                throw new InvalidDataException("FFVII battle AI callback command has an invalid callback index.");
            }
            if (command is 0x01 or 0x22 or 0x24)
            {
                commands.Add(new(slot, (byte)command, id, -1, target, Callback: context.Callback, Priority: priority));
                return;
            }
            if (command != 0x20)
            {
                throw new NotSupportedException($"FFVII battle AI queue command 0x{command:X2} is unsupported.");
            }
            SceneBattleAttack attack = formation.Attacks.FirstOrDefault(attack => attack.Id == id) ?? throw new InvalidDataException($"FFVII battle AI attack {id} is absent from its scene.");
            commands.Add(new(slot, (byte)command, id, attack.Slot, target, Callback: context.Callback, Priority: priority));
        }

        private void QueueMessage()
        {
            int start = pc;
            uint value;
            do
            {
                value = ReadOperand(1);
                if (value is >= 0xEA and < 0xF2)
                {
                    ReadOperand(2);
                }
            }
            while (value != 0xFF);
            // The encounter's message owner allocates the native text slot when consuming this request.
            commands.Add(new(context.ActorSlot, 0x21, 0, -1, target, script[start..pc].ToArray(), context.Callback, priority));
        }

        private uint ReadOperand(int size)
        {
            if (size > script.Length - pc)
            {
                throw new InvalidDataException("FFVII battle AI script ended in an instruction.");
            }
            uint value = 0;
            for (int index = 0; index < size; index++)
            {
                value |= (uint)script[pc++] << (index * 8);
            }
            return value;
        }

        private static Value Boolean(bool value) => new(0, value ? 1U : 0U);

        private void Push(Value value)
        {
            uint widthMask = value.Width switch { 0 or 1 => 0xFF, 2 => 0xFFFF, _ => 0xFFFFFF };
            if (!value.IsAddress)
            {
                value = value with { Scalar = value.Scalar & widthMask };
                if (value.Actors is uint[] values)
                {
                    for (int index = 0; index < values.Length; index++)
                    {
                        values[index] &= widthMask;
                    }
                }
                else
                {
                    value = value with { Mask = ActorMask };
                }
            }
            int nextSize = stackBytes + value.EncodedSize;
            if (nextSize > StackCapacity)
            {
                throw new InvalidDataException("FFVII battle AI exceeded its stack bound.");
            }
            stackBytes = nextSize;
            stack.Push(value);
        }

        private Value Pop()
        {
            Value value = Peek();
            stack.Pop();
            stackBytes -= value.EncodedSize;
            return value;
        }

        private Value Peek()
        {
            if (!stack.TryPeek(out Value value))
            {
                throw new InvalidDataException("FFVII battle AI stack is empty.");
            }
            return value;
        }
    }
}
