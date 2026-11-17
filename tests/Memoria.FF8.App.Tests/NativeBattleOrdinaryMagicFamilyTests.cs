using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed record NativeBattleOrdinaryMagicCase(string Id, ushort Effect, int EntryMaximumCallbacks, int ProducerMaximumCallbacks, int MaximumTicks, int ProcessTimeoutSeconds)
{
    public ushort? ActionId { get; init; }
}
internal sealed record NativeBattleOrdinaryMagicCases(NativeBattleOrdinaryMagicCase[] Cases);
