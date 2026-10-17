using System.Security.Cryptography;
using System.Text;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record EventPredicateValue(int Operation, string Opcode, int Address, int Width, int Value);
public sealed record EventPredicateState(string Fingerprint, IReadOnlyList<EventPredicateValue> Values, IReadOnlyList<int> DeferredOperations);

public sealed partial class EventEngine
{
    /// <summary>Inspects present canonical state without executing a script or establishing a missing memory owner.</summary>
    public EventPredicateState ReadPredicateState(IReadOnlyCollection<int> operationIndices, bool includeParty = true)
    {
        ArgumentNullException.ThrowIfNull(operationIndices);
        List<EventPredicateValue> values = [];
        List<int> deferred = [];
        NativeEventMemory memory = EventMemory;
        foreach (int index in operationIndices.Distinct().Order())
        {
            if (program is null || (uint)index >= program.Operations.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(operationIndices), "Predicate reads must identify operations in the current original program.");
            }
            JsmOperation operation = program.Operations[index];
            if (operation.Opcode is not (Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L))
            {
                throw new ArgumentException("Predicate state accepts only original typed memory-read operations.", nameof(operationIndices));
            }
            int width = NativeMemoryWidth(operation.Opcode);
            try
            {
                NativeEventMemoryOwner owner = ResolveMemoryOwner(operation.Parameter, width);
                bool represented = owner == NativeEventMemoryOwner.Generic && memory.HasRepresentedExactBytes(operation.Parameter, width);
                bool canonical = owner is NativeEventMemoryOwner.PartyBindings or NativeEventMemoryOwner.WorldEntry or NativeEventMemoryOwner.WorldControls or NativeEventMemoryOwner.Disc or NativeEventMemoryOwner.PreviousField or NativeEventMemoryOwner.PreviousWorldEntity or NativeEventMemoryOwner.FieldLoadReason or NativeEventMemoryOwner.Economy or NativeEventMemoryOwner.Cards or NativeEventMemoryOwner.Junction or NativeEventMemoryOwner.BattleMode or NativeEventMemoryOwner.SeeD or NativeEventMemoryOwner.DrawPoints;
                if (represented || canonical)
                {
                    NativeScalarValue read = ReadEventMemory(operation);
                    if (read.IsExact)
                    {
                        values.Add(new EventPredicateValue(index, operation.Opcode.ToString(), operation.Parameter, width, read.RequireExact()));
                        continue;
                    }
                }
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or ArgumentOutOfRangeException)
            {
                // Discovery defers unavailable dependencies; only original execution can require their missing contract.
            }
            deferred.Add(index);
        }
        StringBuilder identity = new();
        identity.Append(FieldId).Append(':').Append(programHash);
        if (includeParty)
        {
            identity.Append("|party:").AppendJoin(',', Scene.Party);
            if (Scene.PartyBindingMemory is { } bindings)
            {
                identity.Append("|bindings:").Append(bindings.At(0)).Append(',').Append(bindings.At(1)).Append(',').Append(bindings.At(2));
            }
        }
        foreach (EventPredicateValue value in values)
        {
            identity.Append('|').Append(value.Operation).Append(':').Append(value.Opcode).Append(':').Append(value.Address).Append('/').Append(value.Width).Append('=').Append(value.Value);
        }
        identity.Append("|deferred:").AppendJoin(',', deferred);
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString())));
        return new EventPredicateState(fingerprint, values, deferred);
    }

}
