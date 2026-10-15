using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private PreparedWorldEntry? preparedWorldEntry;

    private Action PrepareNativeWorldEntry(NativeWorldTransitionRequest request)
    {
        ValidateWorldDeparture(request);
        GameState departure = session.State;
        preparedWorldEntry?.Dispose();
        PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, assets.GameDirectory, request.Transition);
        Action publishMusic;
        try
        {
            using var musicAssets = new WorldAssets(assets.GameDirectory);
            publishMusic = Events.PrepareWorldMusic(request.Transition, musicAssets.ReadMusicResource);
        }
        catch
        {
            ticket.Dispose();
            throw;
        }
        preparedWorldEntry = ticket;
        ReplayWorldAllocation allocation = new(1, request.Transition, ticket.ProgramHash, assets.NativeExecutableHash ?? throw new NotSupportedException("World allocation requires its authenticated current native executable."));
        allocation.Validate();
        bool committed = false;
        return () =>
        {
            if (committed || !ReferenceEquals(preparedWorldEntry, ticket) || !ReferenceEquals(session.State, departure))
            {
                throw new InvalidOperationException("The original prepared world request has already been used or replaced.");
            }
            ValidateWorldDeparture(request);
            session.CommitPreparedWorldEntry(request.Transition, ticket.InitialState, request.ControlsAtDeparture, ticket.ValidateAdoption, ticket.InitialSavedWorld, allocation);
            worldScene = ticket.Adopt(session);
            worldScene.PrepareFieldEntry = PrepareNativeWorldFieldEntry;
            ConfigureWorldSalaryEffects(worldScene);
            committed = true;
            preparedWorldEntry = null;
            ticket.Dispose();
            publishMusic();
        };
    }

    private void ConfigureWorldSalaryEffects(WorldMapScene scene)
    {
        scene.PrepareSalaryEffects = Events.PrepareSalaryEffects;
        scene.PublishSalaryEffects = Events.PublishSalaryEffects;
    }

    private Action PrepareNativeWorldFieldEntry(WorldFieldSelection selection, WorldState finalWorld, short mapLocationId, NativeWorldSalaryState salary)
    {
        GameState departure = session.State;
        if (departure.Scene != SceneKind.WorldMap || worldScene is null || departure.World is null || departure.WorldControls is null || departure.SavedWorld is null)
        {
            throw new NotSupportedException("World field entry requires its current original foot scene and native saved/control provenance.");
        }
        FieldAssets destination = assets.LoadField(selection.Entry.FieldId);
        if (destination.Information is null || selection.Entry.Triangle >= destination.Walkmesh.Triangles.Count || selection.Entry.X == short.MaxValue)
        {
            throw new NotSupportedException("World field entry requires the original explicit-triangle packet and destination INF.");
        }
        int? height = FieldPartyTraceGeometry.ResolveTriangleHeight(destination.Walkmesh, selection.Entry.Triangle, selection.Entry.X, selection.Entry.Y);
        if (height is null)
        {
            throw new InvalidDataException("Original world field entry references an invalid destination triangle plane.");
        }
        WorldEntryVariables inputs = WorldEntryVariables.Read(departure);
        FieldTransition transition = new(selection.Entry.FieldId, selection.Entry.X, selection.Entry.Y, selection.Entry.Triangle, selection.Entry.Direction);
        if (finalWorld.NativeScriptedRuntime is not null)
        {
            NativeSavedWorldScriptedReturn scriptedReceipt = finalWorld.NativeScriptedRuntime.Return ?? throw new NotSupportedException("Boat field entry has no actual endpoint saved globals.");
            if (selection.Entry.Index != unchecked((byte)scriptedReceipt.Packet.Extra) || transition != scriptedReceipt.FieldEntry || mapLocationId != scriptedReceipt.MapLocationId)
            {
                throw new InvalidDataException("Prepared scripted arrival differs from its original terminal row or endpoint map metadata.");
            }
            return () => session.CommitPreparedScriptedFieldEntry(departure, finalWorld, transition, salary, candidate => ValidatePreparedField(candidate));
        }
        if (finalWorld.NativeCarRuntime is not null)
        {
            NativeSavedWorldCarReturn carReceipt = NativeSavedWorldCarReturn.Create(finalWorld, checked((ushort)departure.FieldId), inputs.StoryWord, mapLocationId);
            return () => session.CommitPreparedCarFieldEntry(departure, finalWorld, transition, carReceipt, salary, candidate => ValidatePreparedField(candidate));
        }
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(finalWorld, checked((ushort)departure.FieldId), inputs.StoryWord, mapLocationId);
        return () => session.CommitPreparedWorldFieldEntry(departure, finalWorld, transition, receipt, salary, candidate =>
        {
            ValidatePreparedField(candidate);
        });

        void ValidatePreparedField(GameState candidate)
        {
            if (candidate.FieldArrival != transition || candidate.PreviousFieldId != departure.FieldId || destination.Id != candidate.FieldId)
            {
                throw new InvalidOperationException("Prepared native world field packet changed before publication.");
            }
        }
    }

    private void ValidateWorldDeparture(NativeWorldTransitionRequest request)
    {
        if (request.Version != 1 || !Enum.IsDefined(request.Origin) || session.State.Scene != SceneKind.Field || Field?.Id != request.SourceFieldId || session.State.FieldId != request.SourceFieldId || Events.FieldId != request.SourceFieldId || Events.Frame != request.SourceFrame || Events.Scene.NativeFieldLoadVersion != 1 || Events.Scene.NativeActorLayoutVersion != 1 || session.WorldControls != request.ControlsAtDeparture || worldScene is not null)
        {
            throw new NotSupportedException("World entry does not match its original canonical field departure.");
        }
        NativeWorldControlState loader = request.ControlsAtDeparture.WithWorldEntryMenuFlag();
        NativeWorldControlState issue = request.Origin == NativeWorldTransitionOrigin.Script ? loader : request.ControlsAtDeparture;
        if (request.ControlsAtIssue != issue || request.ControlsAtLoader != loader || (request.Origin == NativeWorldTransitionOrigin.Gateway) != (request.Gateway is not null))
        {
            throw new NotSupportedException("World entry controls or source metadata disagree with the original request.");
        }
    }

    private static InfGateway CreateInfGateway(FieldGateway gateway, int index)
    {
        byte[] record = new byte[32];
        short[] words = [(short)gateway.Start.X, (short)gateway.Start.Y, (short)gateway.Start.Z, (short)gateway.End.X, (short)gateway.End.Y, (short)gateway.End.Z, gateway.DestinationX, gateway.DestinationY, gateway.DestinationTriangle, unchecked((short)gateway.DestinationField)];
        for (int word = 0; word < words.Length; word++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(word * 2), words[word]);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(20), gateway.Extra20);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(24), gateway.Extra24);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(28), gateway.Extra28);
        return new InfGateway(gateway.Start, gateway.End, new FieldTransition(gateway.DestinationField, gateway.DestinationX, gateway.DestinationY, gateway.DestinationTriangle, gateway.Direction), index, record);
    }
}
