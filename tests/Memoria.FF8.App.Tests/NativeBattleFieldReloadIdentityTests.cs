using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleFieldReloadIdentityTests
{
    [Theory]
    [InlineData(NativeFieldRuntimeProfile.FF8EnExecutableHash)]
    [InlineData(NativeFieldRuntimeProfile.RemasteredEnImageHash)]
    public void ReloadReceiptsRetainAuthenticatedEditionAndRejectInvalidProvenance(string image)
    {
        string program = new('A', 64);
        NativeBattleFieldReloadState pending = new(1, image, 130, program, 307, 4, new string('B', 64), NativeBattleFieldReloadStage.ModelCachesReset);
        pending = JsonSerializer.Deserialize<NativeBattleFieldReloadState>(JsonSerializer.Serialize(pending))!;
        pending.Validate();
        NativeBattleFieldReloadCompletion completed = new(1, pending.ExecutableHash, pending.FieldId, pending.ProgramHash, pending.BattleEntryFrame, pending.NativeResult);
        completed = JsonSerializer.Deserialize<NativeBattleFieldReloadCompletion>(JsonSerializer.Serialize(completed))!;
        completed.Validate();
        Assert.Equal(image, completed.ExecutableHash);
        Assert.Throws<InvalidDataException>(() => (pending with { ExecutableHash = new string('0', 64) }).Validate());
        Assert.Throws<InvalidDataException>(() => (completed with { ExecutableHash = new string('0', 64) }).Validate());
        Assert.Throws<InvalidDataException>(() => (pending with { NativeResult = 3 }).Validate());
        Assert.Throws<InvalidDataException>(() => (completed with { BattleEntryFrame = 0 }).Validate());
    }
}
