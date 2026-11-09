using System.Security.Cryptography;

namespace Memoria.FF7.Events;

/// <summary>Original section-relative offsets; entries may alias and never imply script lengths.</summary>
public sealed class FieldProgram
{
    private readonly byte[] code;
    private readonly ushort[][] entries;

    public FieldProgram(byte[] section, IReadOnlyList<ushort[]> entityEntries)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(entityEntries);
        if (section.Length is < 1 or > 65536 || entityEntries.Count is < 1 or > 255)
        {
            throw new ArgumentException("Invalid FFVII section size or entity count.");
        }
        code = (byte[])section.Clone();
        if (entityEntries.Any(row => row is null))
        {
            throw new ArgumentException("Entity entry tables cannot be null.");
        }
        entries = entityEntries.Select(row => (ushort[])row.Clone()).ToArray();
        foreach (ushort[] row in entries)
        {
            if (row.Length != 32 || row.Any(offset => offset >= code.Length))
            {
                throw new ArgumentException("Each entity needs 32 valid section-relative entries.");
            }
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(code.Length)));
        hash.AppendData(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(entries.Length)));
        hash.AppendData(code);
        foreach (ushort[] row in entries)
        {
            foreach (ushort offset in row)
            {
                hash.AppendData([(byte)offset, (byte)(offset >> 8)]);
            }
        }
        Identity = Convert.ToHexString(hash.GetHashAndReset());
    }

    public static FieldProgram FromPcScripts(Memoria.FF7.Formats.PcFieldScripts scripts)
    {
        ArgumentNullException.ThrowIfNull(scripts);
        return new FieldProgram(scripts.Data.ToArray(), scripts.Entities.Select(entity => entity.ScriptOffsets.ToArray()).ToArray()) { FieldScale = scripts.Scale };
    }

    public string Identity { get; }
    public short FieldScale { get; private init; } = 512;
    public int EntityCount => entries.Length;
    internal int Length => code.Length;
    internal ushort Entry(int entity, int script) => entries[entity][script];
    internal byte Read(int offset)
    {
        if ((uint)offset >= code.Length)
        {
            throw new InvalidDataException($"FFVII bytecode read outside section at {offset:X4}.");
        }
        return code[offset];
    }
}

public enum FieldSyncState : byte
{
    None,
    Waiting,
    Done
}

public sealed record FieldEntitySnapshot(int ProgramCounter, int Priority, ushort WaitCounter, int[] SavedProgramCounters, FieldSyncState[] SyncStates, int[] SyncOwners, bool ModelBound = false);
public sealed record FieldInputState(ushort Held = 0, ushort Pressed = 0, ushort Released = 0, ushort RawHeld = 0, ushort RawPressed = 0, ushort RawReleased = 0, int Horizontal = 0, int Vertical = 0, bool Run = false);
public sealed record FieldCycleCursor(int Entity, int HandlersCompleted, int ProgramCounter);
public sealed record FieldVmSnapshot(int Version, string Profile, string ProgramIdentity, long Tick, bool Initialized, byte[] PersistentVariables, byte[] MapVariables, FieldEntitySnapshot[] Entities, long NextRequestId = 1, FieldHostRequest[]? PendingRequests = null, FieldInputState? Input = null, FieldCycleCursor? CycleCursor = null);
public sealed record FieldFaultContext(string ProgramIdentity, long Tick, int Entity, int Priority, int ProgramCounter, byte Opcode);

public sealed class FieldExecutionException : Exception
{
    public FieldExecutionException(FieldFaultContext context, string message, Exception? inner = null) : base($"FFVII field {context.ProgramIdentity}, tick {context.Tick}, entity {context.Entity}, priority {context.Priority}, PC {context.ProgramCounter:X4}, opcode {context.Opcode:X2}: {message}", inner)
    {
        Context = context;
    }

    public FieldFaultContext Context { get; }
}
