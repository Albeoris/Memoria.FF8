using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeContactOccurrenceTests
{
    private static readonly NativeBattleContactHalf Contact = new(3, 4, 0, 0, 0, 10, 0);
    private static readonly NativeBattleContactHalf Absent = new(255, 0, 0, 0, 0, 0, 0);
    private static readonly ImmutableArray<CrossoverNativeMagicTarget> Planned = [new(4, Contact), new(4, Contact), new(4, Contact)];

    [Fact]
    public void Equal_repeated_recipients_keep_their_actual_physical_identity_in_native_publication_order()
    {
        ImmutableArray<int> first = CrossoverNativeMagicPresentation.ReadContactOrdinals(Planned, [], [Packet(2), Packet(0)]);
        Assert.Equal([2, 0], first);
        Assert.Equal([1], CrossoverNativeMagicPresentation.ReadContactOrdinals(Planned, first, [Packet(1)]));
    }

    [Fact]
    public void Repeated_missing_or_unowned_physical_identity_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => CrossoverNativeMagicPresentation.ReadContactOrdinals(Planned, [2], [Packet(2)]));
        Assert.Throws<InvalidDataException>(() => CrossoverNativeMagicPresentation.ReadContactOrdinals(Planned, [], [Packet(1), Packet(1)]));
        Assert.Throws<InvalidDataException>(() => CrossoverNativeMagicPresentation.ReadContactOrdinals(Planned, [], [new(Contact, Absent)]));
        Assert.Throws<InvalidDataException>(() => CrossoverNativeMagicPresentation.ReadContactOrdinals(Planned, [], [Packet(3)]));
        Assert.Throws<InvalidDataException>(() => CrossoverNativeMagicPresentation.ReadContactOrdinals(Planned, [], [Packet(0) with { Primary = Contact with { ActorIndex = 4 } }]));
    }

    private static NativeBattleContactPacket Packet(int ordinal) => new(Contact, Absent) { PhysicalContactIndex = ordinal };
}
