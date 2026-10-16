using System.Buffers.Binary;
using System.Collections.Frozen;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record EventScript(int Id, int Start, int End, FrozenSet<int> ReachableOperations, FrozenSet<int> TerminalSuccessors)
{
    public bool ContainsOperation(int operation) => ReachableOperations.Contains(operation);

    public bool ContainsPosition(int position, bool complete) => ContainsOperation(position) || complete && TerminalSuccessors.Contains(position);
}
public sealed record EventObject(int Index, int Label, IReadOnlyList<EventScript> Scripts, int? BackgroundModuleIndex = null, bool IsArea = false, bool IsDoor = false);

/// <summary>Validated bytecode ranges correlated with Albeoris's source-backed operations.</summary>
public sealed class JsmProgram
{
    private JsmProgram(IReadOnlyList<EventObject> objects, IReadOnlyList<JsmOperation> operations, IReadOnlyList<int> nativeObjects, int nativeActorCount)
    {
        Objects = objects;
        Operations = operations;
        NativeObjects = nativeObjects;
        NativeActorCount = nativeActorCount;
    }

    public IReadOnlyList<EventObject> Objects { get; }
    public IReadOnlyList<JsmOperation> Operations { get; }
    public IReadOnlyList<int> NativeObjects { get; }
    public int NativeActorCount { get; }

    public static JsmProgram Read(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length < 8)
        {
            throw new InvalidDataException("JSM header is incomplete.");
        }
        int groupCount = content[0] + content[1] + content[2] + content[3];
        int scriptsOffset = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(4));
        int operationsOffset = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(6));
        if (8 + groupCount * 2 > scriptsOffset || scriptsOffset > operationsOffset || operationsOffset > content.Length || (content.Length - operationsOffset) % 4 != 0)
        {
            throw new InvalidDataException("JSM group, script or operation boundaries are invalid.");
        }
        int operationCount = (content.Length - operationsOffset) / 4;
        if (operationCount > ushort.MaxValue + 1)
        {
            throw new InvalidDataException("JSM operations exceed the original unsigned WORD instruction-pointer domain.");
        }
        IReadOnlyList<JsmOperation> operations = JsmDocument.ReadOperations(content);
        if (operations.Count != operationCount)
        {
            throw new InvalidDataException("Albeoris JSM operation count does not match the validated source.");
        }
        ushort[] groups = new ushort[groupCount];
        for (int index = 0; index < groupCount; index++)
        {
            groups[index] = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(8 + index * 2));
        }
        // Native 0x52BC00 consumes raw door groups before areas, then modules and objects.
        // Its pointer table puts objects first, followed by doors, modules and areas.
        ushort[] nativeGroups = groups.Skip(content[0] + content[1] + content[2]).Concat(groups.Take(content[1])).Concat(groups.Skip(content[0] + content[1]).Take(content[2])).Concat(groups.Skip(content[1]).Take(content[0])).ToArray();
        HashSet<ushort> areaGroups = groups.Skip(content[1]).Take(content[0]).ToHashSet();
        HashSet<ushort> doorGroups = groups.Take(content[1]).ToHashSet();
        Array.Sort(groups, (left, right) => (left >> 7).CompareTo(right >> 7));
        int scriptCount = groups.Sum(group => (group & 127) + 1);
        if (scriptsOffset + (scriptCount + 1) * 2 > operationsOffset)
        {
            throw new InvalidDataException("JSM script position table is incomplete.");
        }
        List<EventObject> objects = [];
        HashSet<int> labels = [];
        int positionIndex = 0;
        foreach (ushort group in groups)
        {
            int label = group >> 7;
            List<EventScript> scripts = [];
            for (int index = 0; index <= (group & 127); index++)
            {
                int start = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(scriptsOffset + positionIndex * 2)) & 0x7FFF;
                int end = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(scriptsOffset + (positionIndex + 1) * 2)) & 0x7FFF;
                if (start >= end || end > operationCount || !labels.Add(label + index))
                {
                    throw new InvalidDataException($"Invalid or overlapping JSM script label/range {label + index}: {start}..{end}.");
                }
                ValidateScript(content, operationsOffset, label + index, start, end, operationCount);
                scripts.Add(CreateScript(label + index, start, end, operations));
                positionIndex++;
            }
            int moduleIndex = -1;
            for (int index = 0; index < content[2]; index++)
            {
                ushort rawModule = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(8 + (content[0] + content[1] + index) * 2));
                if ((rawModule >> 7) == label)
                {
                    moduleIndex = index;
                    break;
                }
            }
            objects.Add(new EventObject(objects.Count, label, scripts, moduleIndex < 0 ? null : moduleIndex, areaGroups.Contains(group), doorGroups.Contains(group)));
        }
        return new JsmProgram(objects, operations, nativeGroups.Select(group => objects.Single(obj => obj.Label == group >> 7).Index).ToArray(), content[3]);
    }

    public static int JumpTarget(int operation, int parameter) => unchecked((ushort)(operation + unchecked((short)parameter)));

    private static EventScript CreateScript(int label, int start, int end, IReadOnlyList<JsmOperation> operations)
    {
        HashSet<int> reachable = [];
        HashSet<int> terminalSuccessors = [];
        Stack<int> pending = new([start]);
        while (pending.TryPop(out int index))
        {
            if ((uint)index >= operations.Count)
            {
                throw new InvalidDataException($"JSM script {label} reaches operation {index} outside the original global code.");
            }
            if (!reachable.Add(index))
            {
                continue;
            }
            JsmOperation operation = operations[index];
            if (operation.Opcode is Jsm.Opcode.RET or Jsm.Opcode.HALT)
            {
                terminalSuccessors.Add(index + 1);
                continue;
            }
            if (operation.Opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF)
            {
                pending.Push(JumpTarget(index, operation.Parameter));
            }
            if (operation.Opcode != Jsm.Opcode.JMP)
            {
                pending.Push(index + 1);
            }
        }
        return new EventScript(label, start, end, reachable.ToFrozenSet(), terminalSuccessors.ToFrozenSet());
    }

    private static void ValidateScript(byte[] content, int operationsOffset, int label, int start, int end, int operationCount)
    {
        for (int index = start; index < end; index++)
        {
            int encoded = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(operationsOffset + index * 4));
            Jsm.Opcode opcode = (encoded & unchecked((int)0xFF000000)) == 0 ? (Jsm.Opcode)encoded : (Jsm.Opcode)((uint)encoded >> 24);
            int parameter = (encoded << 8) >> 8;
            if (index == start && opcode != Jsm.Opcode.LBL || index == end - 1 && opcode != Jsm.Opcode.RET)
            {
                throw new InvalidDataException($"JSM script {label} must begin with LBL and end with RET.");
            }
            if (opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF)
            {
                int target = JumpTarget(index, parameter);
                if ((uint)target >= operationCount)
                {
                    throw new InvalidDataException($"JSM script {label} jump at {index} targets {target} outside the original global code.");
                }
            }
        }
    }
}
