using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public enum NativeWorldMusicSelectionOrigin
{
    ExecutedStore,
    ExecutedClear,
    RetainedPhysicalByte
}
public sealed record NativeWorldMusicSelectionMigration(int Version, string Rule, string SourceSnapshotSha256);
public sealed record NativeWorldMusicSelection(int? FieldId, int? ObjectId, int? ScriptId, int? Operation, long? Frame, int? RawOperand, byte Music, NativeWorldMusicSelectionOrigin Origin = NativeWorldMusicSelectionOrigin.ExecutedStore, NativeWorldMusicSelectionMigration? Migration = null)
{
    public const string MigrationRule = "ff8-en-retained-exact-byte268-migration-v1";

    public static NativeWorldMusicSelection FromRetainedPhysicalByte(byte music, string sourceSnapshotSha256)
    {
        NativeWorldMusicSelection selection = new(null, null, null, null, null, null, music, NativeWorldMusicSelectionOrigin.RetainedPhysicalByte, new(1, MigrationRule, sourceSnapshotSha256));
        selection.Validate();
        return selection;
    }

    public void Validate()
    {
        if (Origin == NativeWorldMusicSelectionOrigin.RetainedPhysicalByte)
        {
            if (FieldId is not null || ObjectId is not null || ScriptId is not null || Operation is not null || Frame is not null || RawOperand is not null || Migration is not { Version: 1, Rule: MigrationRule, SourceSnapshotSha256: { Length: 64 } hash } || !hash.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException("Retained world music requires its explicit exact-byte migration and no invented writer history.");
            }
        }
        else if (Origin is not (NativeWorldMusicSelectionOrigin.ExecutedStore or NativeWorldMusicSelectionOrigin.ExecutedClear) || FieldId is null or < 72 || ObjectId is null or < 0 || ScriptId is null or < 0 || Operation is null or < 0 || Frame is null or < 0 || Migration is not null || Origin == NativeWorldMusicSelectionOrigin.ExecutedStore && (RawOperand is not int raw || Music != unchecked((byte)raw)) || Origin == NativeWorldMusicSelectionOrigin.ExecutedClear && (RawOperand is not null || Music != 0))
        {
            throw new InvalidDataException("World music selection lost its actual BYTE268 store or CLEAR producer.");
        }
    }
}
public enum NativeWorldMusicOperation
{
    Keep,
    Stop,
    Play
}
public sealed record NativeWorldMusicLoad(int Version, int SourceFieldId, long SourceFrame, WorldTransition Packet, byte RequestedMusic, byte PriorMusic, byte PriorBank, byte Bank, NativeWorldMusicOperation Operation, byte? Music, NativeWorldMusicResource? Resource, int InitialVolume, int TargetVolume, int DurationMilliseconds);

public sealed partial class EventEngine
{
    private string? UnsupportedWorldMusicSelection(Fiber fiber, JsmOperation operation)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        if (operation.Opcode is not (Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L) || operation.Parameter > 268 || (long)operation.Parameter + width <= 268)
        {
            return null;
        }
        return width == 1 && operation.Parameter == 268 && fiber.Stack.Count > 0 && fiber.Stack.PeekValue().IsExact ? null : "World music selection requires its exact native BYTE268 writer; overlapping or uncertain stores are unsupported.";
    }

    private bool TryWriteWorldMusicSelection(Fiber fiber, JsmOperation operation, out int[] args)
    {
        args = [];
        if (operation.Opcode != Jsm.Opcode.POPM_B || operation.Parameter != 268)
        {
            return false;
        }
        args = Pop(fiber, 1);
        byte music = unchecked((byte)args[0]);
        Scene.WorldMusicSelection = new NativeWorldMusicSelection(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, args[0], music);
        return true;
    }

    public Action PrepareWorldMusic(WorldTransition packet, Func<byte, NativeWorldMusicResource> resolveResource)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(resolveResource);
        NativeWorldMusicSelection selection = Scene.WorldMusicSelection ?? throw new NotSupportedException("World music needs its executed BYTE268 selection; the snapshot is missing required native state");
        selection.Validate();
        NativeMusicProcessState process = Scene.MusicProcess ?? throw new NotSupportedException("World music requires the current native music process.");
        if (UnsupportedNativeMusicAliases(EventMemory) is string aliases)
        {
            throw new NotSupportedException(aliases);
        }
        //5420C0/542370 consume the retained channel music and bank, independently
        //of field MUSICLOAD's pending1D9CDE0/B213/B214. World loading neither
        //requires nor clears that pending owner, even when it replaces the bank.
        if (services.CurrentScene != SceneKind.Field || NativeRuntimeProfile is not { IsVerified: true } || process.Bank201 is not byte bank || bank > 1 || Scene.MusicChannel108?.MusicId is not byte prior || EventMemory.Cells.Any(pair => pair.Key <= 268 && (long)pair.Key + pair.Value.Width > 268))
        {
            throw new NotSupportedException("World music requires authenticated native bank, channel and unaliased BYTE268 authority.");
        }
        NativeWorldMusicOperation kind = NativeWorldMusicOperation.Keep;
        NativeWorldMusicResource? resource = null;
        byte? music = prior;
        byte nextBank = bank;
        if (selection.Music == unchecked((sbyte)prior) && PendingWorldMusicSelection(unchecked((sbyte)packet.Mode), selection.Music, prior) >= 0)
        {
            throw new NotSupportedException("Native world entry requires a subsequent asynchronous music selection even though its initial track matches.");
        }
        if (selection.Music != unchecked((sbyte)prior))
        {
            int entity = unchecked((sbyte)packet.Mode);
            bool ordinary = unchecked((uint)entity) < 10 || entity == 48;
            if (selection.Music == 24 || selection.Music == 41 && entity is not (49 or 50) || ordinary && selection.Music is 81 or 89)
            {
                music = selection.Music == 24 ? (byte)24 : (byte)41;
                resource = resolveResource(music.Value);
                resource.Validate();
                if (resource.MusicId != music)
                {
                    throw new InvalidDataException("World music resource differs from its native loader selection.");
                }
                kind = NativeWorldMusicOperation.Play;
                nextBank ^= 1;
            }
            else if (selection.Music == 0)
            {
                kind = NativeWorldMusicOperation.Stop;
                music = null;
            }
            else
            {
                throw new NotSupportedException($"World music selection{selection.Music}/entity{entity} requires its unimplemented native asynchronous loader.");
            }
        }
        var request = new NativeWorldMusicLoad(1, FieldId, Frame, packet, selection.Music, prior, bank, nextBank, kind, music, resource, kind == NativeWorldMusicOperation.Play ? 0 : 127, 127, kind == NativeWorldMusicOperation.Play ? 60 : 0);
        NativeMusicChannelState primary = Scene.MusicChannel108;
        return () =>
        {
            if (services is not GameSession session || session.State.Scene != SceneKind.WorldMap || session.State.WorldArrival != packet || !ReferenceEquals(Scene.MusicProcess, process) || Scene.MusicChannel108 != primary || Scene.WorldMusicSelection != selection)
            {
                throw new InvalidOperationException("Prepared world music no longer matches its current native owner.");
            }
            Scene.MusicProcess = process with { Bank201 = nextBank };
            if (kind == NativeWorldMusicOperation.Play)
            {
                Scene.MusicChannel108 = new NativeMusicChannelState(0, music);
                Scene.LoadedMusic = music!.Value;
                Scene.MusicPlaying = true;
            }
            else if (kind == NativeWorldMusicOperation.Stop)
            {
                Scene.MusicChannel108 = primary with { MusicId = 255 };
                Scene.MusicPlaying = false;
            }
            Scene.WorldMusicLoad = request;
            AudioCommandExecuted?.Invoke(request);
        };
    }

    private static int PendingWorldMusicSelection(int entity, byte requested, byte prior)
    {
        // Native5447A0 runs when5420C0 did not retain a new preloaded buffer.
        if (requested == 0)
        {
            return -1;
        }
        if (entity == 49)
        {
            return 2;
        }
        if (requested == 35)
        {
            return prior == 35 ? -1 : 3;
        }
        if (requested == 59)
        {
            return prior == 59 ? -1 : 4;
        }
        if (unchecked((uint)entity) >= 10 && entity != 48 && (entity < 32 || entity > 40) && (entity < 16 || entity > 22))
        {
            return entity == 50 && prior != 89 ? 5 : -1;
        }
        return prior == 41 ? -1 : 0;
    }

    private void ValidateWorldMusicCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.WorldMusicSelection is NativeWorldMusicSelection selection)
        {
            selection.Validate();
            if (saved.EventMemory.Cells.Any(pair => pair.Key <= 268 && (long)pair.Key + pair.Value.Width > 268))
            {
                throw new InvalidDataException("Saved world music selection lost its executed BYTE268 writer.");
            }
            if (selection.Origin != NativeWorldMusicSelectionOrigin.RetainedPhysicalByte)
            {
                Jsm.Opcode opcode = selection.Origin == NativeWorldMusicSelectionOrigin.ExecutedClear ? Jsm.Opcode.CLEAR : Jsm.Opcode.POPM_B;
                ValidateMusicRequestSite(saved, selection.FieldId!.Value, selection.ObjectId!.Value, selection.ScriptId!.Value, selection.Operation!.Value, selection.Frame!.Value, opcode);
                if (opcode == Jsm.Opcode.POPM_B && selection.FieldId == saved.FieldId && program!.Operations[selection.Operation.Value].Parameter != 268)
                {
                    throw new InvalidDataException("Saved world music selection points to a different memory operand.");
                }
            }
        }
        if (saved.Scene.WorldMusicLoad is not NativeWorldMusicLoad load)
        {
            return;
        }
        load.Resource?.Validate();
        bool play = load.Operation == NativeWorldMusicOperation.Play;
        int entity = unchecked((sbyte)load.Packet.Mode);
        bool ordinary = unchecked((uint)entity) < 10 || entity == 48;
        byte? expectedMusic = load.RequestedMusic == 24 ? (byte)24 : load.RequestedMusic == 41 && entity is not (49 or 50) || ordinary && load.RequestedMusic is 81 or 89 ? (byte)41 : null;
        if (load.Version != 1 || load.SourceFieldId < 72 || load.SourceFrame < 0 || !Enum.IsDefined(load.Operation) || load.PriorBank > 1 || load.Bank != (play ? load.PriorBank ^ 1 : load.PriorBank) || load.Bank != saved.Scene.MusicProcess?.Bank201 || play != (load.Resource is not null) || play && (load.Music != load.Resource!.MusicId || load.Music != expectedMusic || load.InitialVolume != 0 || load.DurationMilliseconds != 60) || !play && (load.InitialVolume != 127 || load.DurationMilliseconds != 0) || load.Operation == NativeWorldMusicOperation.Keep && (load.RequestedMusic != unchecked((sbyte)load.PriorMusic) || load.Music != load.PriorMusic || PendingWorldMusicSelection(entity, load.RequestedMusic, load.PriorMusic) >= 0) || load.Operation == NativeWorldMusicOperation.Stop && load.RequestedMusic != 0 || load.TargetVolume != 127 || (load.Operation == NativeWorldMusicOperation.Stop) != (load.Music is null))
        {
            throw new InvalidDataException("Saved world music load lost its native bank/resource/gain authority.");
        }
    }
}
