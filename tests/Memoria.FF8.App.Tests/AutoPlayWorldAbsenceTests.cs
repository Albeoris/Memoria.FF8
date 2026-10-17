using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWorldAbsenceTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(33, false)]
    [InlineData(66, false)]
    public void OnlyAProvedAbsentNormalFootObjectProducesAReceipt(byte entityType, bool absent)
    {
        NativeWorldAllocationClause clause = Clause(4);

        AutoPlayWorldAbsenceReceipt? receipt = GameWorld.CreateAutoPlayWorldAbsenceReceipt(entityType, 170, clause);

        Assert.Equal(absent, receipt is not null);
        if (receipt is not null)
        {
            Assert.Equal(1, receipt.Version);
            Assert.Equal(entityType, receipt.EntityType);
            Assert.Equal(170, receipt.Story);
            Assert.Equal(64, receipt.AllocationFingerprint.Length);
        }
    }

    [Fact]
    public void UnknownAllocationOrAStoryOutsideItsSelectedRangeCannotCertifyAbsence()
    {
        Assert.Null(GameWorld.CreateAutoPlayWorldAbsenceReceipt(0, 170, null));
        Assert.Null(GameWorld.CreateAutoPlayWorldAbsenceReceipt(0, 99, Clause(4)));
        Assert.Null(GameWorld.CreateAutoPlayWorldAbsenceReceipt(0, 200, Clause(4)));
        Assert.Null(GameWorld.CreateAutoPlayWorldAbsenceReceipt(0, 170, Clause(0)));
    }

    [Fact]
    public void TheFingerprintIncludesStoryRequestedModeAndTheCompleteSelectedAllocation()
    {
        NativeWorldAllocationClause clause = Clause(4);
        AutoPlayWorldAbsenceReceipt original = Assert.IsType<AutoPlayWorldAbsenceReceipt>(GameWorld.CreateAutoPlayWorldAbsenceReceipt(0, 170, clause));
        NativeWorldAllocationClause[] changedClauses =
        [
            clause with { Index = clause.Index + 1 },
            clause with { Offset = clause.Offset + 4 },
            Clause(4, 7)
        ];
        Assert.Equal(original, GameWorld.CreateAutoPlayWorldAbsenceReceipt(0, 170, Clause(4)));
        Assert.NotEqual(original.AllocationFingerprint, GameWorld.CreateAutoPlayWorldAbsenceReceipt(0, 171, clause)!.AllocationFingerprint);
        Assert.NotEqual(original.AllocationFingerprint, GameWorld.CreateAutoPlayWorldAbsenceReceipt(2, 170, clause)!.AllocationFingerprint);
        Assert.All(changedClauses, changed => Assert.NotEqual(original.AllocationFingerprint, GameWorld.CreateAutoPlayWorldAbsenceReceipt(0, 170, changed)!.AllocationFingerprint));
        Assert.Equal(new ushort[] { 0xFF01, 0, 0xFF02, 100, 0xFF03, 200, 0xFF04, 0, 0xFF13, 4, 0xFF05, 0 }, clause.Words);
    }

    [Fact]
    public void TheOriginalStoryAllocationDistinguishesTheAbsentRequestedFootFromItsPresentAlternative()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeWorldAllocationClause clause = assets.ReadNativeWorldAllocationClause(170);

        Assert.False(clause.HasRequestedFootObject(0));
        Assert.True(clause.HasRequestedFootObject(1));
        Assert.NotNull(GameWorld.CreateAutoPlayWorldAbsenceReceipt(0, 170, clause));
        Assert.Null(GameWorld.CreateAutoPlayWorldAbsenceReceipt(1, 170, clause));
        Assert.Null(GameWorld.CreateAutoPlayWorldAbsenceReceipt(33, 170, clause));
    }

    [Fact]
    public void AChangedAllocationWithTheRequestedBodyPresentHasAComparableNewFingerprint()
    {
        AutoPlayWorldAbsenceReceipt absent = Assert.IsType<AutoPlayWorldAbsenceReceipt>(GameWorld.CreateAutoPlayWorldAbsenceReceipt(0, 170, Clause(4)));
        NativeWorldAllocationClause present = Clause(0, 4);

        string fingerprint = GameWorld.ComputeAutoPlayWorldAllocationFingerprint(0, 170, present);

        Assert.NotEmpty(fingerprint);
        Assert.NotEqual(absent.AllocationFingerprint, fingerprint);
        Assert.Null(GameWorld.CreateAutoPlayWorldAbsenceReceipt(0, 170, present));
    }

    [Fact]
    public void InspectingTheActualRequestPreservesItsIssuingPcOperandsAndNativeOwners()
    {
        string snapshot = TestResources.FixturePath("App.Tests.AutoPlayWorldAbsenceTests.1");
        byte[] original = File.ReadAllBytes(snapshot);
        GameSession session = new(GameSession.Load(snapshot));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        NativeWorldTransitionRequest? observedRequest = null;
        AutoPlayWorldAbsenceReceipt? observedReceipt = null;
        JsonElement? before = null;
        JsonElement? after = null;
        GameState? beforeState = null;
        GameState? afterState = null;
        world.BeforeWorldEntry += request =>
        {
            observedRequest = request;
            before = world.Events.Capture();
            beforeState = session.State;
            observedReceipt = world.InspectAutoPlayWorldAbsence(request);
            after = world.Events.Capture();
            afterState = session.State;
            throw new RequestObservedException();
        };

        RequestObservedException? interrupted = null;
        for (int callback = 0; callback < 16 && interrupted is null; callback++)
        {
            try
            {
                Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            }
            catch (RequestObservedException exception)
            {
                interrupted = exception;
            }
        }

        Assert.NotNull(interrupted);
        NativeWorldTransitionRequest actual = Assert.IsType<NativeWorldTransitionRequest>(observedRequest);
        Assert.Equal(NativeWorldTransitionOrigin.Script, actual.Origin);
        Assert.Equal(159, actual.SourceFieldId);
        Assert.Equal(13, actual.SourceObjectId);
        Assert.Equal(104, actual.SourceScriptId);
        Assert.Equal(2138, actual.SourceOperation);
        Assert.Equal(world.Events.FieldId, actual.SourceFieldId);
        Assert.Equal(world.Events.Frame, actual.SourceFrame);
        AutoPlayWorldAbsenceReceipt receipt = Assert.IsType<AutoPlayWorldAbsenceReceipt>(observedReceipt);
        Assert.Equal(170, receipt.Story);
        Assert.Equal(0, receipt.EntityType);
        Assert.Equal(receipt.AllocationFingerprint, world.ReadAutoPlayWorldAbsenceFingerprint(receipt));
        Assert.Same(beforeState, afterState);
        Assert.True(JsonElement.DeepEquals(before!.Value, after!.Value));
        Assert.True(JsonElement.DeepEquals(after.Value, world.Events.Capture()));
        JsonElement issuing = after.Value.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == actual.SourceObjectId && fiber.GetProperty("ScriptId").GetInt32() == actual.SourceScriptId && !fiber.GetProperty("Complete").GetBoolean());
        Assert.Equal(actual.SourceOperation, issuing.GetProperty("Position").GetInt32());
        Assert.True(issuing.GetProperty("Stack").GetArrayLength() >= 3);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(original, File.ReadAllBytes(snapshot));
    }

    private static NativeWorldAllocationClause Clause(params ushort[] shapes)
    {
        List<ushort> words = [0xFF01, 0, 0xFF02, 100, 0xFF03, 200, 0xFF04, 0];
        foreach (ushort shape in shapes)
        {
            words.Add(0xFF13);
            words.Add(shape);
        }
        words.Add(0xFF05);
        words.Add(0);
        return new(2, 64, words.Count * 2, 100, 200, words.ToImmutableArray());
    }

    private sealed class RequestObservedException : Exception
    {
    }
}
