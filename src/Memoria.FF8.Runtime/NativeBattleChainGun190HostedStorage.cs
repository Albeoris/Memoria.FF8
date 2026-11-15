using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using static Memoria.FF8.Runtime.NativeBattleChainGun190Profile;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleChainGun190HostedStorage(uint ChildAddress, ushort ChildCapacity, uint Address, ImmutableArray<NativeBattleEffectArena> Pages, ImmutableArray<(BattleContactOccurrence Occurrence, BattleModelOwner Owner)> Contacts)
{
    private const int PageBytes = NativeBattleEffectArena.Length / 48 * 48;

    internal int ShotCursor { get; init; }
    internal int ShotCount => Contacts.IsEmpty ? 0 : Math.Max(12, Contacts.Length);
    internal int RecordCapacity => Math.Max(RecordCount, ShotCount);
    internal int PositionedParticleCapacity => Math.Max(ParticleCount, checked(72 + ShotCount * 10));
    internal int DirectionalParticleCapacity => Math.Max(ParticleCount, checked(ShotCount * 12));
    internal uint PositionedParticles => checked(Address + (uint)RecordBytes);
    internal uint DirectionalParticles => checked(PositionedParticles + (uint)(PositionedParticleCapacity * ParticleStride));
    private int RecordBytes => AlignParticleBank(RecordCapacity * RecordStride);
    private static int AlignParticleBank(int bytes) => checked((bytes + ParticleStride - 1) / ParticleStride * ParticleStride);

    internal static (NativeBattleEffectMemory Memory, NativeBattleChainGun190HostedStorage Storage) Reserve(NativeBattleChainGun190Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Chain Gun requires its captured action parameters.");
        var contacts = packets.ReadHeaderContacts(parameters.Header).Select(packet =>
            (packet.Occurrence ?? throw new InvalidDataException("Chain Gun requires its reached contact occurrence."), packet.Primary.Recipient)).ToImmutableArray();
        int shots = contacts.IsEmpty ? 0 : Math.Max(12, contacts.Length);
        // The director, twelve muzzle children, one muzzle emitter, and four callbacks per shot.
        int childCount = Math.Max(ChildCount, checked(14 + 4 * shots));
        ushort capacity = checked((ushort)childCount);
        (NativeBattleEffectMemory childMemory, uint child) = NativeBattlePresentationTaskStorage.Reserve(frame.Memory, frame.Effect, packets.Roster, ChildStride, capacity);
        // Reserve every authored particle birth, so retirement timing cannot suppress later births.
        int bytes = checked(AlignParticleBank(Math.Max(RecordCount, shots) * RecordStride) + (Math.Max(ParticleCount, 72 + shots * 10) + Math.Max(ParticleCount, shots * 12)) * ParticleStride);
        (NativeBattleEffectMemory memory, uint address) = NativeBattlePresentationTaskStorage.ReserveByteRange(childMemory, frame.Effect, packets.Roster, bytes);
        // A page boundary is aligned to both supported record strides.
        NativeBattleWorkingMemory owner = new(bytes);
        var pages = ImmutableArray.CreateBuilder<NativeBattleEffectArena>();
        for (int offset = 0; offset < bytes; offset += PageBytes)
        {
            pages.Add(NativeBattleEffectArena.BorrowWorking(owner, offset, Math.Min(PageBytes, bytes - offset)));
        }
        return (memory, new(child, capacity, address, pages.ToImmutable(), contacts));
    }

    internal bool TryGetStorage(uint address, int length, [NotNullWhen(true)] out NativeBattleEffectArena? storage, out int offset)
    {
        storage = null;
        offset = 0;
        if (address < Address || length < 0)
        {
            return false;
        }
        uint relative = address - Address;
        uint page = relative / PageBytes;
        if (page >= (uint)Pages.Length)
        {
            return false;
        }
        offset = (int)(relative % PageBytes);
        NativeBattleEffectArena arena = Pages[(int)page];
        if (offset > arena.LogicalLength - length)
        {
            return false;
        }
        storage = arena;
        return true;
    }
}
