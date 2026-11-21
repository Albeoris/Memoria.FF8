using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    internal Ff7AutoPlayWorldControlObservation? ReadAutoPlayChocoboControlCandidate()
    {
        if (Resources.World != 0 || !initialized || mainState != 1 || !movementEnabled || pendingWorldBattle is not null || dialogue is { Completed: false } || fieldDeparture is not null || mapTransfer is not null || chocobo is not { RiderModel: >= 0 and <= 2, DismountPhase: 0, ReleaseRequested: false } mounted || controlledModel != mounted.Model || !entities.TryGetValue(controlledModel, out Ff7WorldEntity? bird) || bird.AttachedModel != mounted.RiderModel || !Machine.Capture().Models.Any(model => model.Model == mounted.RiderModel && model.Detached) || (0x221B0F03u & (1u << (bird.Terrain & 31))) == 0)
        {
            return null;
        }
        string fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Machine.ProgramIdentity, Resources.World, WorldMapState, controlledModel, mounted.RiderModel, bird.X, bird.Z, Terrain = bird.Terrain & 31 })));
        return new($"control:chocobo:dismount:{mounted.Model}:{mounted.RiderModel}", mounted.Model, mounted.RiderModel.Value, fingerprint);
    }

    internal bool ProveAutoPlayChocoboDismount(Ff7AutoPlayWorldControlObservation expected)
    {
        if (ReadAutoPlayChocoboControlCandidate() != expected)
        {
            return false;
        }
        byte[] copy = Machine.CopyPersistent();
        using Ff7WorldRuntime branch = new(inputInstallation, copy, entrySelector, copy.AsSpan(256 + 9, 3).ToArray(), Resources.World, entryModule);
        branch.Restore(Process);
        HashSet<(int Model, int Phase, int Held, int X, int Z)> visited = [];
        while (branch.controlledModel == expected.Model)
        {
            Ff7WorldEntity bird = branch.RequireEntity(branch.controlledModel);
            if (branch.pendingWorldBattle is not null || branch.fieldDeparture is not null || branch.mapTransfer is not null || branch.chocobo is not { RiderModel: not null } state || !visited.Add((branch.controlledModel, state.DismountPhase, state.CancelHeldTicks, bird.X, bird.Z)))
            {
                return false;
            }
            ushort held = state.DismountPhase == 0 && state.CancelHeldTicks == 0 ? (ushort)0x40 : (ushort)0;
            branch.SetButtons(held, (ushort)(held & ~branch.input.Held));
            branch.SetMovement(0, 0, false);
            branch.Advance();
        }
        return branch.controlledModel == expected.RiderModel && branch.input.GroundTriangle is not null;
    }

    private Ff7AutoplayInput ChooseAutoPlayChocoboControlInput(Ff7AutoPlayWorldControlObservation expected)
    {
        if (controlledModel == expected.RiderModel)
        {
            return default;
        }
        if (controlledModel != expected.Model || chocobo is not { RiderModel: not null } state || state.RiderModel != expected.RiderModel || expected.Id != $"control:chocobo:dismount:{expected.Model}:{expected.RiderModel}")
        {
            throw new NotSupportedException("The native Chocobo dismount owner changed before control returned.");
        }
        if (state.DismountPhase != 0)
        {
            return default;
        }
        if (state.CancelHeldTicks >= 15 || ReadAutoPlayChocoboControlCandidate() != expected)
        {
            throw new NotSupportedException("The native Chocobo dismount preconditions changed before input was accepted.");
        }
        return state.CancelHeldTicks == 0 ? new(Held: 0x40, Run: false) : new(Run: false);
    }
}
