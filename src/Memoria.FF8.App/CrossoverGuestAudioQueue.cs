using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal enum CrossoverGuestAudioKind { Module, Fire, Auxiliary, Trigger }
internal sealed record CrossoverGuestAudioReceipt(Guid EncounterId, long Ordinal, long HostTick, CrossoverGuestAudioKind Kind, NativeBattleModuleSound Sound)
{
    public SessionGame Origin => SessionGame.Ff8;
}

internal sealed record CrossoverGuestAudioState(NativeGlobalSoundProcess Process, long NextOrdinal, long AcknowledgedOrdinal, ImmutableArray<CrossoverGuestAudioReceipt> Pending)
{
    public static CrossoverGuestAudioState Create() => new(NativeGlobalSoundProcess.FromBufferlessColdProcess(), 1, 0, []);

    public void Validate(Guid encounter, long hostTick)
    {
        Process.Validate();
        if (NextOrdinal < 1 || AcknowledgedOrdinal < 0 || AcknowledgedOrdinal >= NextOrdinal || Pending.IsDefault || Pending.Length != NextOrdinal - AcknowledgedOrdinal - 1)
        {
            throw new InvalidDataException("The guest sound queue has invalid delivery ordinals.");
        }
        for (int index = 0; index < Pending.Length; index++)
        {
            CrossoverGuestAudioReceipt receipt = Pending[index];
            if (receipt.EncounterId != encounter || receipt.Ordinal != AcknowledgedOrdinal + index + 1 || receipt.HostTick < 0 || receipt.HostTick > hostTick || !Enum.IsDefined(receipt.Kind) || receipt.Sound.Pan is null)
            {
                throw new InvalidDataException("The guest sound queue contains an unbound native event.");
            }
            _ = receipt.Sound.ReadCommittedVoice();
        }
    }

    public CrossoverGuestAudioState Append(Guid encounter, long hostTick, CrossoverGuestAudioKind kind, NativeBattleModuleSound sound)
    {
        (NativeGlobalSoundProcess process, NativeBattleModuleSound committed) = sound.Commit46B2A0(Process);
        CrossoverGuestAudioReceipt receipt = new(encounter, NextOrdinal, hostTick, kind, committed);
        return this with { Process = process, NextOrdinal = checked(NextOrdinal + 1), Pending = Pending.Add(receipt) };
    }

    public CrossoverGuestAudioState Acknowledge(long throughOrdinal)
    {
        if (throughOrdinal < 0)
        {
            throw new InvalidDataException("Guest sound acknowledgement cannot be negative.");
        }
        if (throughOrdinal <= AcknowledgedOrdinal)
        {
            return this;
        }
        if (throughOrdinal >= NextOrdinal)
        {
            throw new InvalidDataException("Guest sound acknowledgement exceeds the produced event sequence.");
        }
        return this with { AcknowledgedOrdinal = throughOrdinal, Pending = Pending.Where(receipt => receipt.Ordinal > throughOrdinal).ToImmutableArray() };
    }
}
