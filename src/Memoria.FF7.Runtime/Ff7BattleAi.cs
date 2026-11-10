using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleAiActor(int Slot, uint Statuses, uint Conditions, ushort MagicPoints, ushort MaxMagicPoints, uint HitPoints, uint MaxHitPoints, ushort Defense = 0, ushort MagicDefense = 0, byte Variant = 0, byte IdleAction = 0, byte HurtAction = 1, ushort LastAttackerMask = 0);
public sealed record Ff7BattleAiContext(int ActorSlot, IReadOnlyList<Ff7BattleAiActor> Actors, ushort EligibleMask, ushort KnockedOutMask, ushort ExcludedOpponentMask = 0, byte Callback = 1);
public sealed record Ff7BattleAiCommand(int ActorSlot, byte CommandId, ushort AttackId, int SceneAttackSlot, ushort TargetMask, byte[]? Message = null, byte Callback = 1);
public sealed record Ff7BattleAiResult(byte[] LocalMemory, Ff7BattleAiCommand[] Commands, Ff7BattleAiActor[] ActorUpdates);

/// <summary>Runs a single native enemy AI callback. The encounter owns the input projections and RNG.</summary>
public static class Ff7BattleAi
{
    public const int LocalMemorySize = 128;
    private const ushort ActorMask = 0x03FF;
    private const int StackCapacity = 512;
    private const int InstructionLimit = 16384;

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
        private readonly Stack<Value> stack = new();
        private readonly List<Ff7BattleAiCommand> commands = new();
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

            if (context.ActorSlot is < 4 or > 9 || actors[context.ActorSlot] is not Ff7BattleAiActor current || context.Callback >= 16 || ((context.EligibleMask | context.KnockedOutMask | context.ExcludedOpponentMask) & ~(party | enemies)) != 0)
            {
                throw new InvalidDataException("FFVII enemy AI context has an invalid owner or mask.");
            }
            bool swapped = BitOperations.PopCount(current.Statuses & 0x00400040) == 1;
            ushort friendly = (ushort)((swapped ? party : enemies) & context.EligibleMask);
            ushort opposing = (ushort)((swapped ? enemies : party) & context.EligibleMask);
            allies = (ushort)(friendly & ~context.KnockedOutMask);
            opponents = (ushort)(opposing & ~context.KnockedOutMask & ~context.ExcludedOpponentMask);
            knockedOutAllies = (ushort)(friendly & context.KnockedOutMask);
            knockedOutOpponents = (ushort)(opposing & context.KnockedOutMask);
            allocated = (ushort)(party | enemies);
            self = (ushort)(1 << context.ActorSlot);
            target = opponents;
        }

        public Ff7BattleAiResult Run()
        {
            if (script.IsEmpty)
            {
                return new(local, [], []);
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
            return new(local, commands.ToArray(), changed.ToArray());
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

        private uint ReadGlobal(int width, ushort address)
        {
            return (width, address) switch
            {
                (2, 0x2050) => allocated,
                (2, 0x2060) => self,
                (2, 0x2070) => target,
                (2, 0x2080) => allies,
                (2, 0x2090) => knockedOutAllies,
                (2, 0x20A0) => opponents,
                (2, 0x20B0) => knockedOutOpponents,
                _ => throw new NotSupportedException($"FFVII battle AI global 0x{address:X4} width {width} has no native projection.")
            };
        }

        private static uint ReadActor(Ff7BattleAiActor actor, int width, ushort address)
        {
            return (width, address) switch
            {
                (0, >= 0x4000 and < 0x4020) => actor.Statuses >> (address - 0x4000) & 1,
                (0, >= 0x4020 and < 0x4040) => actor.Conditions >> (address - 0x4020) & 1,
                (3, 0x4000) => actor.Statuses,
                (3, 0x4020) => actor.Conditions,
                (1, 0x4060) => actor.Variant,
                (1, 0x4080) => actor.IdleAction,
                (1, 0x4088) => actor.HurtAction,
                (2, 0x40D0) => actor.LastAttackerMask,
                (2, 0x4100) => actor.Defense,
                (2, 0x4110) => actor.MagicDefense,
                (2, 0x4140) => actor.MagicPoints,
                (2, 0x4150) => actor.MaxMagicPoints,
                (3, 0x4160) => actor.HitPoints,
                (3, 0x4180) => actor.MaxHitPoints,
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
                if (address == 0x2070 && reference.Width == 2)
                {
                    target = (ushort)value;
                    return;
                }
                throw new NotSupportedException($"FFVII battle AI write to 0x{address:X4} width {reference.Width} has no native owner.");
            }
            int offset = LocalOffset(reference.Width, address);
            switch (reference.Width)
            {
                case 0:
                    int bit = 1 << (address & 7);
                    local[offset] = (byte)((local[offset] & ~bit) | (value == 0 ? 0 : bit));
                    break;
                case 1:
                    local[offset] = (byte)value;
                    break;
                case 2:
                    BinaryPrimitives.WriteUInt16LittleEndian(local.AsSpan(offset), (ushort)value);
                    break;
                case 3:
                    BinaryPrimitives.WriteUInt32LittleEndian(local.AsSpan(offset), value);
                    break;
            }
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
                    (0, 0x402C) => actor with { Conditions = (actor.Conditions & ~0x1000U) | (value == 0 ? 0 : 0x1000U) },
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

        private uint ReadLocal(int width, int address)
        {
            int offset = LocalOffset(width, address);
            return width switch
            {
                0 => (uint)(local[offset] >> (address & 7)) & 1,
                1 => local[offset],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(local.AsSpan(offset)),
                _ => BinaryPrimitives.ReadUInt32LittleEndian(local.AsSpan(offset))
            };
        }

        private static int LocalOffset(int width, int address)
        {
            int size = width == 0 ? 1 : 1 << (width - 1);
            int offset = address / (size * 8) * size;
            if (offset >= LocalMemorySize)
            {
                throw new InvalidDataException("FFVII battle AI address crosses its local actor memory.");
            }
            return offset;
        }

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
            int slot = self == 0 ? 0 : BitOperations.Log2(self);
            if (command == 0x24)
            {
                commands.Add(new(slot, (byte)command, id, -1, target, Callback: context.Callback));
                return;
            }
            if (command != 0x20)
            {
                throw new NotSupportedException($"FFVII battle AI queue command 0x{command:X2} is unsupported.");
            }
            SceneBattleAttack attack = formation.Attacks.FirstOrDefault(attack => attack.Id == id) ?? throw new InvalidDataException($"FFVII battle AI attack {id} is absent from its scene.");
            commands.Add(new(slot, (byte)command, id, attack.Slot, target, Callback: context.Callback));
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
            commands.Add(new(context.ActorSlot, 0x21, 0, -1, target, script[start..pc].ToArray(), context.Callback));
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
