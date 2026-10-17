using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayVisitGuardTests
{
    [Fact]
    public void ExecutedPersistentFlagAndDeclaredPartyChangesInvalidateThePortableGuard()
    {
        byte[] source = Program([(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.POPM_B, 720)], [], [(Jsm.Opcode.PSHM_B, 720), (Jsm.Opcode.POPI_L, 0)]);
        var current = Create(source, NativeEventMemory.Empty.Write(720, 1, NativeScalarValue.Exact(1)));
        AutoPlayTarget[] targets = [Target("talk")];
        AutoPlayVisitGuard before = Guard(current.Engine, source, targets);
        Assert.Equal(new NativePersistentPredicateRead("PSHM_B", 720, 1), Assert.Single(before.Reads));

        current.Engine.Step();

        Assert.Null(current.Engine.LastFault);
        Assert.Equal(2, current.Engine.EventMemory.Read(720, 1).RequireExact());
        Assert.NotEqual(before.Fingerprint, current.Engine.ReadPersistentPredicateFingerprint(before.Reads));
        AutoPlayVisitGuard after = Guard(current.Engine, source, targets);
        Assert.Equal(before.ProgramHash, after.ProgramHash);
        current.Engine.Scene.Party[1] = 1;
        Assert.NotEqual(after.Fingerprint, current.Engine.ReadPersistentPredicateFingerprint(after.Reads));
    }

    [Fact]
    public void DisabledTargetInitializationGuardsAreCapturedWithoutExecutingInitialization()
    {
        byte[] source = Program([(Jsm.Opcode.PSHM_B, 720), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.JPF, 2), (Jsm.Opcode.TALKOFF, 0)], [], []);
        var current = Create(source, NativeEventMemory.Empty.Write(720, 1, NativeScalarValue.Exact(1)));
        JsonElement before = current.Engine.Capture();
        int callbacks = 0;
        current.Engine.OperationCompleted += () => callbacks++;

        AutoPlayVisitGuard guard = Guard(current.Engine, source, []);

        Assert.Equal(720, Assert.Single(guard.Reads).Address);
        Assert.True(current.Engine.Actors[0].TalkEnabled);
        Assert.Equal(0, callbacks);
        Assert.True(JsonElement.DeepEquals(before, current.Engine.Capture()));
    }

    [Fact]
    public void LocalBindingPreviousFieldTimerAndUnrepresentedReadsRemainOutsideThePortableGuard()
    {
        byte[] source = Program([], [], [(Jsm.Opcode.PSHM_B, 720), (Jsm.Opcode.PSHM_B, 1024), (Jsm.Opcode.PSHM_B, 194), (Jsm.Opcode.PSHM_W, 84), (Jsm.Opcode.PSHM_L, 83), (Jsm.Opcode.PSHM_B, 86), (Jsm.Opcode.PSHM_B, 216), (Jsm.Opcode.PSHM_B, 1300)]);
        NativeEventMemory memory = NativeEventMemory.Empty.Write(720, 1, NativeScalarValue.Exact(1));
        var first = Create(source, memory, previousField: 100);
        var second = Create(source, memory, previousField: 101);
        first.Session.SetEventMemory(first.Engine.EventMemory.Write(1024, 1, NativeScalarValue.Exact(7)));
        second.Session.SetEventMemory(second.Engine.EventMemory.Write(1024, 1, NativeScalarValue.Exact(9)));
        AutoPlayVisitGuard guard = Guard(first.Engine, source, [Target("talk")]);

        Assert.Equal(720, Assert.Single(guard.Reads).Address);
        Assert.Equal(guard.Fingerprint, Guard(second.Engine, source, [Target("talk")]).Fingerprint);
        Assert.Equal(guard.Fingerprint, second.Engine.ReadPersistentPredicateFingerprint(guard.Reads));
    }

    [Fact]
    public void PureSelfAdditionIsExcludedInBothInitializationAndInteractionClosures()
    {
        (Jsm.Opcode, int)[] update = [(Jsm.Opcode.PSHM_B, 1300), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 0), (Jsm.Opcode.POPM_B, 1300)];
        byte[] source = Program(update, [], update, [(Jsm.Opcode.PSHM_B, 1300), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.CAL, 9), (Jsm.Opcode.POPM_B, 1400)]);
        var current = Create(source, NativeEventMemory.Empty.Write(1300, 1, NativeScalarValue.Exact(1)));

        Assert.Empty(Guard(current.Engine, source, [Target("talk")]).Reads);
        AutoPlayVisitGuard predicate = Guard(current.Engine, source, [Target("talk"), Target("push")]);
        Assert.Equal(1300, Assert.Single(predicate.Reads).Address);
    }

    [Fact]
    public void TargetUnionCanGrowAndUnknownReadsCanAcquireAnExactOwnerWithoutChangingSourceIdentity()
    {
        byte[] source = Program([], [], [(Jsm.Opcode.PSHM_B, 1300)], [(Jsm.Opcode.PSHSM_W, 1310)]);
        var current = Create(source, NativeEventMemory.Empty.Write(1300, 1, NativeScalarValue.Exact(1)));
        AutoPlayVisitGuard first = Guard(current.Engine, source, [Target("talk")]);
        AutoPlayVisitGuard deferred = Guard(current.Engine, source, [Target("talk"), Target("push")]);
        Assert.Equal(first.Reads.ToArray(), deferred.Reads.ToArray());
        current.Session.SetEventMemory(current.Engine.EventMemory.Write(1310, 2, NativeScalarValue.Exact(65535)));

        AutoPlayVisitGuard available = Guard(current.Engine, source, [Target("talk"), Target("push")]);

        Assert.Equal(new[] { 1300, 1310 }, available.Reads.Select(read => read.Address));
        Assert.Equal(2, available.Reads[1].Width);
        Assert.Equal("PSHSM_W", available.Reads[1].Opcode);
        Assert.NotEqual(first.Fingerprint, available.Fingerprint);
        Assert.Equal(first.ProgramHash, available.ProgramHash);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(source)), available.ProgramHash);
        Assert.Equal(600, available.FieldId);
        Assert.Equal(1, available.Version);
    }

    [Fact]
    public void LiteralRequestClosureContributesItsOriginalPersistentRead()
    {
        byte[] source = Program([], [], [(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQ, 0)], [(Jsm.Opcode.PSHM_B, 720)]);
        var current = Create(source, NativeEventMemory.Empty.Write(720, 1, NativeScalarValue.Exact(2)));

        AutoPlayVisitGuard guard = Guard(current.Engine, source, [Target("talk")]);

        Assert.Equal(720, Assert.Single(guard.Reads).Address);
    }

    [Fact]
    public void ActualCurrentFieldCaptureUsesTheOriginalProgramAndPreservesItsAllocation()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayVisitGuardTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        IReadOnlyList<AutoPlayTarget> targets = world.DiscoverAutoPlayTargets();
        JsonElement before = world.Events.Capture();
        int callbacks = 0;
        world.Events.OperationCompleted += () => callbacks++;

        AutoPlayVisitGuard guard = Assert.IsType<AutoPlayVisitGuard>(world.CaptureAutoPlayVisitGuard(targets));
        AutoPlayVisitGuard repeated = Assert.IsType<AutoPlayVisitGuard>(world.CaptureAutoPlayVisitGuard(targets));

        Assert.Equal(world.Events.FieldId, guard.FieldId);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(world.Field!.Jsm)), guard.ProgramHash);
        Assert.Equal(guard.Fingerprint, repeated.Fingerprint);
        Assert.Equal(guard.Reads.ToArray(), repeated.Reads.ToArray());
        Assert.Contains(guard.Reads, read => read.Address == 720);
        Assert.DoesNotContain(guard.Reads, read => read.Address < 1280 && read.Address + read.Width > 1024 || read.Address is 84 or 86 or 194 or 195 or 196);
        Assert.Equal(0, callbacks);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
    }

    private static AutoPlayVisitGuard Guard(EventEngine engine, byte[] source, IReadOnlyList<AutoPlayTarget> targets)
    {
        return Assert.IsType<AutoPlayVisitGuard>(GameWorld.CaptureAutoPlayVisitGuard(engine, source, targets));
    }

    private static AutoPlayTarget Target(string kind)
    {
        return new AutoPlayTarget($"600:{kind}:0:0", kind == "talk" ? AutoPlayTargetKind.Talk : AutoPlayTargetKind.Contact, 0, 0, 0, 0, 0);
    }

    private static (GameSession Session, EventEngine Engine) Create(byte[] source, NativeEventMemory memory, int? previousField = null)
    {
        GameSession session = new(new GameState { EventMemory = memory });
        EventEngine engine = new(session);
        engine.LoadField(600, source, [], previousFieldId: previousField);
        engine.Scene.Party[0] = 0;
        engine.Scene.Party[1] = 255;
        engine.Scene.Party[2] = 255;
        return (session, engine);
    }

    private static byte[] Program(params (Jsm.Opcode Opcode, int Parameter)[][] scripts)
    {
        List<(Jsm.Opcode Opcode, int Parameter)> operations = [];
        List<ushort> positions = [];
        for (int index = 0; index < scripts.Length; index++)
        {
            positions.Add(checked((ushort)operations.Count));
            operations.Add((Jsm.Opcode.LBL, index));
            operations.AddRange(scripts[index]);
            operations.Add((Jsm.Opcode.RET, 8));
        }
        positions.Add(checked((ushort)operations.Count));
        int offset = 10 + positions.Count * 2;
        byte[] bytes = new byte[offset + operations.Count * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)offset));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), checked((ushort)(scripts.Length - 1)));
        for (int index = 0; index < positions.Count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2), positions[index]);
        }
        for (int index = 0; index < operations.Count; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L or Jsm.Opcode.CAL or Jsm.Opcode.REQ or Jsm.Opcode.LBL ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + index * 4), encoded);
        }
        return bytes;
    }
}
