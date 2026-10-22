using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed class NativeBattleDeathFamilyProfile
{
    private NativeBattleDeathFamilyProfile(ushort effect)
    {
        Effect = effect;
    }

    private static readonly NativeBattleDeathFamilyProfile Death = new(14);
    private static readonly NativeBattleDeathFamilyProfile LevelDeath = new(74);
    private static readonly ImmutableDictionary<uint, uint> LevelCallbacks = new Dictionary<uint, uint>
    {
        [0x8B6A40] = 0x7C45E0,
        [0x8B6C90] = 0x7C4830,
        [0x8B6E30] = 0x7C49D0,
        [0x8B6F70] = 0x7C4B50,
        [0x8B78C0] = 0x7C54A0,
        [0x8BD580] = 0x7CB140,
        [0x8BD840] = 0x7CB400,
        [0x8BDBD0] = 0x7CB790,
        [0x8BDF40] = 0x7CBB00
    }.ToImmutableDictionary();

    public ushort Effect { get; }
    public uint Entry => Effect == 14 ? 0x8B68C0u : 0x7C4460u;
    public uint Initializer => Effect == 14 ? 0x8B68A0u : 0x7C4440u;
    public uint RootDescriptor => ModuleAddress(0x273B0B8);
    public uint RootBank => ModuleAddress(0x273AFF0);
    public uint ModuleBase => Effect == 14 ? 0x273AE80u : 0x25EC518u;
    public int ModuleLength => Effect == 14 ? 0x7A20 : 0x7A40;
    public uint AuraAddress => Effect == 14 ? 0x1801FD0u : 0x1771F5Cu;
    public uint DarkAddress => Effect == 14 ? 0x18069C4u : 0x1776950u;
    public bool SingleSelectedTarget => Effect == 74;
    public uint? CasterDirectory => Effect == 74 ? 0x15863B0u : null;
    public uint? SavedCasterRow => Effect == 74 ? 0x25F3F00u : null;

    public ImmutableArray<NativeBattleCureFamilyPool> Pools =>
    [
        new(RootDescriptor, RootBank, 100, 2),
        new(ModuleAddress(0x2741878), ModuleAddress(0x2741888), 88, 4),
        new(ModuleAddress(0x273AFC0), ModuleAddress(0x273AEE8), 72, 3),
        new(ModuleAddress(0x2742810), ModuleAddress(0x2742450), 320, 3),
        new(ModuleAddress(0x27415D8), ModuleAddress(0x2740B48), 676, 4),
        new(ModuleAddress(0x273B0C8), ModuleAddress(0x273B0D8), 112, 10),
        new(ModuleAddress(0x273B540), ModuleAddress(0x273D490), 280, 50),
        new(ModuleAddress(0x2741868), ModuleAddress(0x27415E8), 64, 10)
    ];

    public static NativeBattleDeathFamilyProfile ForEffect(ushort effect)
    {
        return effect switch
        {
            14 => Death,
            74 => LevelDeath,
            _ => throw new NotSupportedException("Native Death family requires a supported original effect.")
        };
    }

    public uint ModuleAddress(uint deathAddress)
    {
        if (deathAddress < 0x273AE80 || deathAddress >= 0x27428A0)
        {
            throw new ArgumentOutOfRangeException(nameof(deathAddress));
        }
        return Effect == 14 ? deathAddress : checked(deathAddress - 0x273AE80 + ModuleBase + (deathAddress >= 0x2742868 ? 32u : 0u));
    }

    public int ModuleOffset(uint deathAddress) => checked((int)(ModuleAddress(deathAddress) - ModuleBase));

    public uint Callback(uint deathCallback)
    {
        if (!LevelCallbacks.TryGetValue(deathCallback, out uint levelCallback))
        {
            throw new NotSupportedException("Native Death family callback has no proved original counterpart.");
        }
        return Effect == 14 ? deathCallback : levelCallback;
    }

    public uint ReferenceCallback(uint callback)
    {
        if (Effect == 14 && LevelCallbacks.ContainsKey(callback))
        {
            return callback;
        }
        if (Effect == 74)
        {
            foreach ((uint reference, uint actual) in LevelCallbacks)
            {
                if (actual == callback)
                {
                    return reference;
                }
            }
        }
        throw new NotSupportedException("Native Death family task has an unsupported original callback.");
    }

    public NativeBattleDeathState RequireState(NativeBattleEffectMemory memory)
    {
        return (Effect == 14 ? memory.Death14 : memory.LevelDeath74) ?? throw new NotSupportedException("Native Death family requires its separately retained module state.");
    }

    public NativeBattleEffectMemory WithState(NativeBattleEffectMemory memory, NativeBattleDeathState state)
    {
        (byte[] _, bool[] known) = state.Module.ReadSnapshot(4, 4);
        if (known.Any(value => value))
        {
            state = state with { Module = state.Module.MarkUnknown(4, 4) };
        }
        return Effect == 14 ? memory with { Death14 = state } : memory with { LevelDeath74 = state };
    }

    public NativeBattleModulePoolTask AdvanceTargetLoop(NativeBattleModulePoolTask task)
    {
        if (task.ReadByte(0x63) != 0)
        {
            return task;
        }
        int selector = SingleSelectedTarget ? 0x2B : 0x2A;
        int limit = SingleSelectedTarget ? unchecked((short)task.ReadWord(0x5A)) - 1 : unchecked((short)task.ReadWord(0x58));
        if (unchecked((sbyte)task.ReadByte(selector)) < limit)
        {
            return task.WriteByte(selector, unchecked((byte)(task.ReadByte(selector) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1)));
        }
        return task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    }
}
