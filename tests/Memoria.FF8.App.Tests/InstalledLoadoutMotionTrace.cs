using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Opt-in compact callback observations; complete meshes and live process snapshots are never written.</summary>
internal static class InstalledLoadoutMotionTrace
{
    private const int MaximumRows = 6000;
    private const int MaximumBytes = 2 * 1024 * 1024 - 512;
    private static readonly Dictionary<string, Trace> Traces = [];
    private static readonly MethodInfo PoseResource = typeof(BattlePresentationProcess).GetMethod("NativeGraphicsPoseResource", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo ActiveFamily = typeof(BattleSession).GetMethod("ReadActivePresentationLimitFamily", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly PropertyInfo Invocation = typeof(BattleSession).GetProperty("CurrentExpandedHostedInvocation", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static void CaptureIfRequested(GameWorld world, string actorId, int recipient, BattleLimitFamily? family)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_MOTION_TRACE");
        string? selectedRecipient = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_MOTION_RECIPIENT");
        string? selectedFamily = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_MOTION_FAMILY");
        string label = family?.ToString() ?? "Ordinary";
        if (string.IsNullOrWhiteSpace(directory) || selectedRecipient is not null && selectedRecipient != recipient.ToString() || selectedFamily is not null && selectedFamily != label)
        {
            return;
        }
        BattleSession battle = world.Battle!;
        if (battle.Presentation is not { EntryReady: true } process || battle.PreparedPresentation is not { } prepared)
        {
            return;
        }
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"motion-{recipient:D2}-{label}.jsonl");
        if (!Traces.TryGetValue(path, out Trace? trace))
        {
            Assert.False(File.Exists(path), "Use a distinct directory for each bounded motion trace.");
            var body = world.BattleResources.LoadPlayer(recipient);
            var joints = BattleHumanoidRigProfiles.SemanticPairs(recipient, body, recipient, body).Where(pair => pair.Joint is "root" or "foot-a" or "foot-b" or "hand-a" or "hand-b").ToArray();
            trace = new(joints);
            Traces.Add(path, trace);
        }
        if (trace.Stopped || trace.LastCallback == process.Graphics.Callback)
        {
            return;
        }
        if (trace.Rows >= MaximumRows)
        {
            Stop(path, trace, "Row budget reached");
            return;
        }
        BattleModelOwner owner = battle.ReadPresentationModelOwner(actorId);
        NativeBattleGraphicsModel model = process.Graphics.ActorModels.Get(owner);
        NativeBattleGraphicsResource resource = (NativeBattleGraphicsResource)PoseResource.Invoke(process, [process.Graphics, owner])!;
        NativeBattlePoseSource source = resource.PoseSource!;
        NativeBattleBodyBoneMatrices? published = model.CurrentBodyBoneMatrices;
        var independentlyStaged = model.SequenceState?.DecodedPose is { } decoded ? NativeBattlePoseMatrices.StageBoneMatrices(source, decoded) : default;
        BattleSession.ExpandedHostedInvocation? invocation = (BattleSession.ExpandedHostedInvocation?)Invocation.GetValue(battle);
        List<NativeBattleLogicalCommandReference> commands = [];
        foreach (ref readonly NativeBattleLogicalResolvedCommand command in prepared.Primitives.LastCommands)
        {
            if (!command.Command.Pilot.IsEmpty && (command.Command.Pilot.ModelPose is not null || command.Command.PresentationActorId == actorId || command.Command.Pilot.Identity.ModelOwner == owner))
            {
                commands.Add(command.Command);
            }
        }
        var groups = commands.GroupBy(command => new { command.PresentationActorId, command.Pilot.Identity.Effect, command.Pilot.Identity.Bank, command.Pilot.Identity.Slot, command.Pilot.Identity.Generation, command.Pilot.Identity.ResourceGeneration, Owner = command.Pilot.Identity.ModelOwner?.ToString() }).ToArray();
        var row = new
        {
            battle.Frame,
            Callback = process.Graphics.Callback,
            Prepared = prepared.Callback,
            Selected = battle.Combatants.Single(actor => actor.Id == actorId).ActiveNativeLimitFamily?.ToString(),
            ActualFamily = ActiveFamily.Invoke(battle, [owner])?.ToString(),
            Invocation = invocation is null ? null : new { invocation.Command.ActorId, invocation.Command.NativeQueuedCommand, Action = invocation.Command.NativeAction?.ActionId, Reaction = invocation.Command.NativePlayerReaction?.ToString() },
            Effect = process.Graphics.OriginalEffect?.Binding.Effect,
            Duel = battle.GetNativeDuel(actorId)?.Phase.ToString(),
            model.ModelId4,
            model.Flags0,
            model.Flags2,
            model.Flags8,
            Masked = (model.Flags0 & 0x200) != 0,
            Role = model.EquipmentPresentationRole.ToString(),
            model.ActiveAnimationBank,
            model.ActiveAnimationDirectory,
            model.ActiveStagedResourceRow100,
            model.Sequence,
            model.SequenceFrame,
            Pose = model.SequenceState?.Pose,
            PoseFrame = model.SequenceState?.PoseFrame114,
            PoseLimit = model.SequenceState?.PoseLimit115,
            Decoded = Hash(JsonSerializer.SerializeToUtf8Bytes(model.SequenceState?.DecodedPose)),
            Translation = model.SequenceState?.DecodedPose?.Translation,
            Bank = Hash(source.Animations.AsSpan()),
            Skeleton = Hash(source.Skeleton.AsSpan()),
            RetargetBank = source.RetargetBinding is { } binding ? Hash(binding.SourceMotion.Animations.AsSpan()) : null,
            model.Transform,
            model.GeometryWorldMatrix,
            PublishedLocalJoints = trace.Joints.Select(joint => new { joint.Joint, Bone = joint.Recipient, Point = published is null ? null : new[] { published.Matrices[joint.Recipient].X, published.Matrices[joint.Recipient].Y, published.Matrices[joint.Recipient].Z } }),
            StagedSelectedPoseJoints = trace.Joints.Select(joint => new { joint.Joint, Bone = joint.Recipient, Point = independentlyStaged.IsDefault ? null : new[] { independentlyStaged[joint.Recipient].X, independentlyStaged[joint.Recipient].Y, independentlyStaged[joint.Recipient].Z } }),
            ActionAliases = source.ActionBoneAliases.IsDefaultOrEmpty ? null : Convert.ToHexString(source.ActionBoneAliases.AsSpan()),
            Camera = process.NativeCamera?.Projection?.View,
            Projection = process.NativeCamera?.Projection is { } projection ? new { projection.FocalLength, projection.OffsetX, projection.OffsetY } : null,
            EmissionGroupsTruncated = groups.Length > 24,
            Emissions = groups.Take(24).Select(group => new { group.Key, Commands = group.Count(), ModelPose = ObservePose(group.First().Pilot.ModelPose), Bounds = Bounds(group.SelectMany(command => Enumerable.Range(0, command.Pilot.CornerCount).Select(command.Pilot.Corner))) })
        };
        string line = JsonSerializer.Serialize(row) + Environment.NewLine;
        int bytes = Encoding.UTF8.GetByteCount(line);
        if (trace.Bytes + bytes > MaximumBytes)
        {
            Stop(path, trace, "Byte budget reached");
            return;
        }
        File.AppendAllText(path, line);
        trace.Rows++;
        trace.Bytes += bytes;
        trace.LastCallback = process.Graphics.Callback;
    }

    private static object? ObservePose(NativeBattlePilotModelPose? pose)
    {
        if (pose is null)
        {
            return null;
        }
        var corners = pose.Geometry().SelectMany(geometry => Enumerable.Range(0, geometry.CornerCount).Select(geometry.Corner)).ToArray();
        return new { FirstMeshVertices = pose.Positions(0).Length, Bounds = Bounds(corners), Hash = Hash(System.Runtime.InteropServices.MemoryMarshal.AsBytes(corners.AsSpan())) };
    }

    private static float[]? Bounds(IEnumerable<System.Numerics.Vector3> source)
    {
        System.Numerics.Vector3[] points = source.ToArray();
        return points.Length == 0 ? null : [points.Min(point => point.X), points.Min(point => point.Y), points.Min(point => point.Z), points.Max(point => point.X), points.Max(point => point.Y), points.Max(point => point.Z)];
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes))[..16];

    private static void Stop(string path, Trace trace, string reason)
    {
        trace.Stopped = true;
        File.WriteAllText(path + ".limit.json", JsonSerializer.Serialize(new { Reason = reason, trace.Rows, trace.Bytes }));
    }

    private sealed class Trace(NativeBattleSemanticBonePair[] joints)
    {
        internal NativeBattleSemanticBonePair[] Joints { get; } = joints;
        internal int Rows;
        internal int Bytes;
        internal long LastCallback = -1;
        internal bool Stopped;
    }
}
