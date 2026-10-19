using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191ActorSnapshot([property: JsonRequired] ImmutableArray<byte> Position, [property: JsonRequired] ImmutableArray<byte> Scale);

public sealed record NativeBattleDoomtrain191State([property: JsonRequired] NativeBattlePackedMatrix32 BaseMatrix, [property: JsonRequired] NativeBattlePackedMatrix32 CameraBackup, [property: JsonRequired] NativeBattlePackedMatrix32 BaseView, [property: JsonRequired] uint ArenaAddress, [property: JsonRequired] ImmutableArray<uint> WorkPointers, [property: JsonRequired] int Caster, [property: JsonRequired] int FirstTarget, [property: JsonRequired] ImmutableArray<int> Targets, [property: JsonRequired] ImmutableArray<NativeBattleDoomtrain191ActorSnapshot?> ActorSnapshots, [property: JsonRequired] byte StreamReady, [property: JsonRequired] int VibrationHandle, [property: JsonRequired] uint LookupCounter)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoomtrain191RigBank? Rigs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoomtrain191ModelMemory? Model { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoomtrain191TrailMemory? Trails { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoomtrain191KnockbackMemory? Knockback { get; init; }

    public static NativeBattleDoomtrain191State FromColdProcess()
    {
        NativeBattlePackedMatrix32 zero = new(new byte[32].ToImmutableArray(), Enumerable.Repeat(true, 32).ToImmutableArray());
        NativeBattleDoomtrain191ActorSnapshot snapshot = new(new byte[8].ToImmutableArray(), new byte[8].ToImmutableArray());
        return new(zero, zero, zero, 0, Enumerable.Repeat(uint.MaxValue, 7).ToImmutableArray(), 0, 0, [], Enumerable.Repeat<NativeBattleDoomtrain191ActorSnapshot?>(snapshot, 7).ToImmutableArray(), 0, 0, 0) { Rigs = new([zero, zero], [zero, zero]), Model = NativeBattleDoomtrain191ModelMemory.FromColdProcess(), Trails = NativeBattleDoomtrain191TrailMemory.FromColdProcess(), Knockback = NativeBattleDoomtrain191KnockbackMemory.FromColdProcess() };
    }

    public void Validate()
    {
        Model?.Validate();
        Trails?.Validate();
        Knockback?.Validate();
        BaseMatrix.Validate();
        CameraBackup.Validate();
        BaseView.Validate();
        if (Rigs is { } rigs)
        {
            if (rigs.World.IsDefault || rigs.World.Length != 2 || rigs.View.IsDefault || rigs.View.Length != 2)
            {
                throw new InvalidDataException("Native Doomtrain requires its two retained rig world/view matrices.");
            }
            foreach (NativeBattlePackedMatrix32 matrix in rigs.World.Concat(rigs.View))
            {
                matrix.Validate();
            }
        }
        if (WorkPointers.IsDefault || WorkPointers.Length != 7 || Caster is < 0 or >= 7 || FirstTarget is < 0 or >= 7 || Targets.IsDefault || Targets.Any(actor => actor is < 0 or >= 7) || ActorSnapshots.IsDefault || ActorSnapshots.Length != 7 || ActorSnapshots.Any(snapshot => snapshot is not null && (snapshot.Position.IsDefault || snapshot.Position.Length != 8 || snapshot.Scale.IsDefault || snapshot.Scale.Length != 8)))
        {
            throw new InvalidDataException("Native Doomtrain requires its retained matrices, work pointers, actor snapshots and camera cursor.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoomtrain191State? Doomtrain191 { get; init; }
}

public sealed record NativeBattleDoomtrain191Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public sealed record NativeBattleDoomtrain191LeafStep(NativeBattleDoomtrain191Frame Frame, int Result);
public sealed record NativeBattleDoomtrain191Inputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<ushort, NativeBattleVector> ReadCentroid, Func<NativeBattlePackedMatrix32> ReadCameraMatrix, Func<NativeBattleCameraPose> ReadCameraPose, Func<uint> ReadGraphicsFlags, Func<int> PollFile, Func<NativeBattleDoomtrain191Frame, NativeBattleDoomtrain191Operation, NativeBattleDoomtrain191Frame> Execute, Func<NativeBattleDoomtrain191Frame, int, NativeBattleDoomtrain191LeafStep> AdvanceLeaf);

public abstract record NativeBattleDoomtrain191Operation;
public sealed record NativeBattleDoomtrain191Invoke(uint Address) : NativeBattleDoomtrain191Operation;
public sealed record NativeBattleDoomtrain191Sound(uint Descriptor) : NativeBattleDoomtrain191Operation;
public sealed record NativeBattleDoomtrain191Load(short FileId, int? ArenaOffset) : NativeBattleDoomtrain191Operation;
public sealed record NativeBattleDoomtrain191Tim(int ArenaOffset) : NativeBattleDoomtrain191Operation;
public sealed record NativeBattleDoomtrain191StreamStart(uint? Descriptor) : NativeBattleDoomtrain191Operation;
public sealed record NativeBattleDoomtrain191StreamPlay : NativeBattleDoomtrain191Operation;
public sealed record NativeBattleDoomtrain191StartVibration : NativeBattleDoomtrain191Operation;
public sealed record NativeBattleDoomtrain191StopVibration : NativeBattleDoomtrain191Operation;
public sealed record NativeBattleDoomtrain191Contacts : NativeBattleDoomtrain191Operation;
public sealed record NativeBattleDoomtrain191CameraPublish(NativeBattleGuardianCameraStep Step) : NativeBattleDoomtrain191Operation;
