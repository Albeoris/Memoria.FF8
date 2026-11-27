using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Remastered;

internal static class RemasteredGuestData
{
    private const uint SalaryAddress = 0xB8E474;
    private const uint DrawPointAddress = 0xB92328;
    private const uint MenuCardAddress = 0xB96508;
    private const uint MatchCardAddress = 0xC74D00;
    private const int CardTableSize = TripleTriadCatalog.CardCount * 8;
    private static readonly (uint Address, int Length)[] tableRanges =
    [
        (SalaryAddress, 64),
        (DrawPointAddress, 256),
        (MenuCardAddress, CardTableSize),
        (MatchCardAddress, CardTableSize)
    ];

    internal static GameTableOverrides Read(RemasteredGuestImage authenticated, Stream selected)
    {
        RemasteredGuestImage image = new(selected, leaveOpen: true, authenticated);
        image.ValidateDataChanges(authenticated, tableRanges);
        byte[] menuCards = image.Read(MenuCardAddress, CardTableSize);
        byte[] matchCards = image.Read(MatchCardAddress, CardTableSize);
        if (!menuCards.AsSpan().SequenceEqual(matchCards))
        {
            throw new InvalidDataException("Remastered DAT menu and match card definition tables disagree.");
        }
        byte[] originalCards = authenticated.Read(MenuCardAddress, CardTableSize);
        ImmutableArray<TripleTriadCard>.Builder cards = ImmutableArray.CreateBuilder<TripleTriadCard>(TripleTriadCatalog.CardCount);
        for (int id = 0; id < TripleTriadCatalog.CardCount; id++)
        {
            ReadOnlySpan<byte> row = menuCards.AsSpan(id * 8, 8);
            if (!row[5..].SequenceEqual(originalCards.AsSpan(id * 8 + 5, 3)))
            {
                throw new InvalidDataException($"Remastered DAT card {id} changes unsupported reward or reserved fields.");
            }
            cards.Add(TripleTriadCatalog.ReadCard(id, row));
        }
        byte[] drawPoints = image.Read(DrawPointAddress, 256);
        if (drawPoints.Any(point => (point & 63) >= 57))
        {
            throw new InvalidDataException("Remastered DAT draw-point table contains a spell outside the kernel magic table.");
        }
        NativeSalaryTable salary = GameAssets.ReadSalaryTable(NativeFieldRuntimeProfile.RemasteredEnImageHash, image.Read(SalaryAddress, 64));
        return new GameTableOverrides(salary, drawPoints.ToImmutableArray(), cards.MoveToImmutable());
    }
}
