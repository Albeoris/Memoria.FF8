using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    internal static string ReadServiceNativeContext(Ff7SessionSave source, FieldChoiceServiceProof proof)
    {
        byte[] persistent = source.Vm.PersistentVariables.ToArray();
        Array.Clear(persistent, 16, 4);
        if (proof.FieldStepCounterIndependent)
        {
            Array.Clear(persistent, 768 + 6, 2);
        }
        byte[] map = source.Vm.MapVariables.ToArray();
        foreach (int index in proof.ScratchMapBytes.Concat(proof.PresentationMapBytes))
        {
            map[index] = 0;
        }
        return HashContext(ReadServiceNativeComponents(source, persistent, map));
    }

    internal static Dictionary<string, string> ReadServiceNativeComponents(Ff7SessionSave source, FieldChoiceServiceProof proof)
    {
        byte[] persistent = source.Vm.PersistentVariables.ToArray();
        Array.Clear(persistent, 16, 4);
        if (proof.FieldStepCounterIndependent)
        {
            Array.Clear(persistent, 768 + 6, 2);
        }
        byte[] map = source.Vm.MapVariables.ToArray();
        foreach (int index in proof.ScratchMapBytes.Concat(proof.PresentationMapBytes))
        {
            map[index] = 0;
        }
        return ReadServiceNativeComponents(source, persistent, map);
    }

    private static Dictionary<string, string> ReadServiceNativeComponents(Ff7SessionSave source, byte[] persistent, byte[] map)
    {
        return new(StringComparer.Ordinal)
        {
            ["Revision"] = HashContext("native-service-preference:3"),
            ["Resource"] = HashContext(new { source.ResourceProfile, source.FieldName, source.Vm.ProgramIdentity }),
            ["Progress"] = HashContext(source.Progress with { Clock = null, FieldRandomIndex = 0, Encounters = source.Progress.Encounters with { MovementCounter = 0 } }),
            ["Persistent"] = HashContext(persistent),
            ["Map"] = HashContext(map),
            ["BlockedTriangles"] = HashContext(source.Process.BlockedTriangles),
            ["Lines"] = HashContext(source.Process.Lines),
            ["Control"] = HashContext(new { source.Process.PlayerLocked, source.Process.MenuDisabled, source.Process.MapJumpDisabled, source.Process.BattlesDisabled }),
            ["Actors"] = HashContext(source.Process.Actors.Select(actor => new { actor.Slot, actor.Entity, actor.Model, actor.EntityBound, actor.PlayerControlled, actor.PartyCharacter, actor.TalkEnabled, actor.CollisionEnabled, actor.TalkRadius, actor.CollisionRadius }))
        };
    }

    internal static string ReadServiceGuestContext(CombinedSessionSave source) => HashContext(source with { Ff7 = null });

    private static string HashContext<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    private int? ReadCompletedServicePreference(Ff7SessionSave source, Ff7Dialogue prompt, AutoPlayChoiceCoverage coverage)
    {
        int? Miss(string reason)
        {
            state.ChoiceContinuationDiagnostic = $"preference-miss:{coverage.Key}:{reason}";
            return null;
        }
        if (nativeSession is null || state.ChoicePreferences is not { } preferences || !preferences.TryGetValue(coverage.Key, out Ff7AutoPlayChoicePreference? preference) || preference is null || !coverage.Finished || !coverage.CompletedOptions.Contains(preference.Selection) || preference.Selection < coverage.FirstOption || preference.Selection >= coverage.FirstOption + coverage.OptionCount || preference.Selection < prompt.FirstChoice || preference.Selection > prompt.LastChoice || (source.Progress.Clock?.CountdownSeconds ?? 0) != 0 || state.Active is not { Accepted: true } active)
        {
            return Miss("metadata-domain-owner-or-countdown");
        }
        FieldHostRequest? owner = source.Vm.PendingRequests?.SingleOrDefault(request => request.Id == prompt.RequestId && request.Entity == active.Entity && request.Priority == 1 && request.Command is FieldDialogueCommand);
        if (owner is null)
        {
            return Miss("pending-owner");
        }
        FieldChoiceServiceProof proof = nativeSession.ReadChoiceServiceProof(owner.Entity, owner.ProgramCounter, pendingSelection: true);
        if (!proof.Complete)
        {
            return Miss("certificate:" + proof.Reason);
        }
        Dictionary<string, string> components = ReadServiceNativeComponents(source, proof);
        if (HashContext(components) != preference.NativeContext)
        {
            string changed = preference.NativeComponents is { } previous ? string.Join(",", components.Where(component => !previous.TryGetValue(component.Key, out string? value) || value != component.Value).Select(component => component.Key)) : "legacy-components";
            return Miss("native:" + changed);
        }
        string? path = null;
        try
        {
            path = Write(coverage.Key + "-service-context", source);
            CombinedSessionSave current;
            using (FileStream input = File.OpenRead(path))
            {
                current = CombinedSessionCodec.Read(input);
            }
            if (current.Ff7 is null || !Same(source with { AutoPlay = null, AudioPresentation = null }, current.Ff7 with { AutoPlay = null, AudioPresentation = null }) || ReadServiceGuestContext(current) != preference.GuestContext)
            {
                return Miss(current.Ff7 is null ? "missing-native-readback" : !Same(source with { AutoPlay = null, AudioPresentation = null }, current.Ff7 with { AutoPlay = null, AudioPresentation = null }) ? "native-readback" : "guest");
            }
            state.ChoiceContinuationDiagnostic = $"preference-hit:{coverage.Key}:option:{preference.Selection}";
            return preference.Selection;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            return Miss("observation:" + error.GetType().Name);
        }
        finally
        {
            if (path is not null && !state.Coverage.Choices.Any(choice => choice.BeforeCheckpoint == path || choice.ContinuationCheckpoint == path) && state.PendingRestore?.Path != path && Path.GetFileNameWithoutExtension(path) is { Length: 64 } name && name.All(char.IsAsciiHexDigit))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    state.ChoiceContinuationDiagnostic = "context-cleanup:" + error.GetType().Name;
                }
            }
        }
    }
}
