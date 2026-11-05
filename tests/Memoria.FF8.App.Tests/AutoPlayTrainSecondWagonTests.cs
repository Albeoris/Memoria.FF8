using System.Reflection;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayTrainSecondWagonTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void OriginalSecondWagonDigitRetainsTheParentResultContract(int expected)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        JsmProgram program = JsmProgram.Read(assets.LoadField(903).Jsm);
        EventEngine engine = Create(program, expected);
        object fiber = Fiber(program, 10, 67, 1791 + (expected - 1) * 8);
        int requests = 0;
        engine.AutoPlayCodeEntry = request =>
        {
            Assert.Equal(new AutoPlayCodeEntryRequest(AutoPlayCodePuzzle.Train, expected, true, true), request);
            requests++;
            return request.ExpectedAnswer;
        };
        Assert.Equal(Jsm.Opcode.REQSW, program.Operations[1790 + (expected - 1) * 8].Opcode);
        Assert.True((bool)Invoke(engine, "TryApplyAutoPlayTrainDigit", fiber, 11, 71 + expected, 1, Jsm.Opcode.REQSW)!);
        Assert.Equal(1, requests);
        Assert.Equal(1, engine.EventMemory.Read(1036, 1).RequireExact());
        Assert.Equal(1, engine.EventMemory.Read(1037, 1).RequireExact());
        Assert.Equal(expected, engine.EventMemory.Read(1033, 1).RequireExact());
        Assert.Equal(0, engine.EventMemory.Read(1038, 1).RequireExact());
        Assert.Equal(0, engine.EventMemory.Read(1041, 1).RequireExact());
        Assert.Equal(0, engine.EventMemory.Read(1042, 1).RequireExact());
    }

    [Theory]
    [InlineData(360, 660, 589, true)]
    [InlineData(359, 660, 589, true)]
    [InlineData(358, 660, 589, false)]
    [InlineData(360, 359, 589, true)]
    [InlineData(360, 358, 589, false)]
    [InlineData(360, 200, 592, false)]
    [InlineData(360, 660, 602, false)]
    public void SecondWagonDescentRequiresAWholePatrolWaitFromBothCurrentFibers(int blueRemaining, int redRemaining, int redPosition, bool safe)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        JsmProgram program = JsmProgram.Read(assets.LoadField(903).Jsm);
        Assert.Equal(Jsm.Opcode.WAIT, program.Operations[471].Opcode);
        Assert.Equal(360, program.Operations[470].Parameter);
        Assert.Equal(Jsm.Opcode.WAIT, program.Operations[588].Opcode);
        Assert.Equal(660, program.Operations[587].Parameter);
        Assert.Equal(Jsm.Opcode.WAIT, program.Operations[591].Opcode);
        Assert.Equal(200, program.Operations[590].Parameter);
        EventEngine engine = Create(program, 1);
        System.Collections.IList fibers = (System.Collections.IList)Get(engine, "fibers");
        foreach ((int owner, int script, int position, int remaining) in new[] { (3, 28, 472, blueRemaining), (4, 34, redPosition, redRemaining) })
        {
            object fiber = Fiber(program, owner, script, position);
            Set(fiber, "WaitReason", WaitReason.Frames);
            Set(fiber, "WaitUntil", (long)remaining);
            fibers.Add(fiber);
        }
        Assert.Equal(safe, (bool)Invoke(engine, "HasAutoPlaySecondWagonPatrolWindow")!);
        Assert.Equal(0, engine.EventMemory.Read(1042, 1).RequireExact());
    }

    [Theory]
    [InlineData(259, true)]
    [InlineData(258, false)]
    [InlineData(200, false)]
    public void FinalCodeReservesTheDigitPhaseBeforeNativePatrolCaptureStops(int remaining, bool safe)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        JsmProgram program = JsmProgram.Read(assets.LoadField(903).Jsm);
        EventEngine engine = Create(program, 1, 4);
        System.Collections.IList fibers = (System.Collections.IList)Get(engine, "fibers");
        foreach ((int owner, int script, int position) in new[] { (3, 28, 472), (4, 34, 589) })
        {
            object fiber = Fiber(program, owner, script, position);
            Set(fiber, "WaitReason", WaitReason.Frames);
            Set(fiber, "WaitUntil", (long)remaining);
            fibers.Add(fiber);
        }
        Assert.Equal(safe, (bool)Invoke(engine, "HasAutoPlaySecondWagonPatrolWindow")!);
    }

    [Fact]
    public void OriginalStandaloneSecondWagonCompletesFiveCodesBeforeEitherPatrolDeadline()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.AutoPlayTrainSecondWagonTests.1");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(AutoPlayCheckpointStore.Load(source));
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        world.EnsureField();
        EventEngine engine = world.Events;
        Assert.Equal(903, session.State.FieldId);
        Assert.Equal(0, engine.EventMemory.Read(1038, 1).RequireExact());
        long firstFrame = engine.Frame;
        engine.AutoPlayCodeEntry = request => request.ExpectedAnswer;
        long? patrolDeadline = null;
        long? enteredAt = null;
        int descents = 0;
        int clearedVisits = 0;
        bool wasHanging = false;
        long longestVisit = 0;
        long lastCodeAcceptedAfter = 0;
        engine.CommandExecuted += command =>
        {
            if (command is { FieldId: 903, ObjectId: 9, ScriptId: 58, Operation: 1340 })
            {
                Assert.NotNull(patrolDeadline);
                Assert.NotNull(enteredAt);
                Assert.True(engine.Frame < patrolDeadline.Value);
                lastCodeAcceptedAfter = engine.Frame - enteredAt.Value;
                output.WriteLine($"Accepted code {engine.EventMemory.Read(1038, 1).RequireExact()} at {engine.Frame}, {lastCodeAcceptedAfter} callbacks after descent.");
            }
            if (command is { FieldId: 903, ObjectId: 6, ScriptId: 46, Operation: 1073 })
            {
                System.Collections.IEnumerable fibers = (System.Collections.IEnumerable)Get(engine, "fibers");
                patrolDeadline = fibers.Cast<object>().Where(fiber => !(bool)Get(fiber, "Complete") && (int)fiber.GetType().GetProperty("ObjectId")!.GetValue(fiber)! is 3 or 4 && (WaitReason)Get(fiber, "WaitReason") == WaitReason.Frames).Min(fiber => (long)Get(fiber, "WaitUntil"));
                enteredAt = engine.Frame;
                descents++;
                output.WriteLine($"Descent {descents} at {engine.Frame}, earliest patrol deadline {patrolDeadline}, accepted codes {engine.EventMemory.Read(1038, 1).RequireExact()}, timer {session.ExamTimer}.");
            }
        };
        for (int callback = 0; callback < 10000 && engine.EventMemory.Read(1038, 1).RequireExact() < 5; callback++)
        {
            if (engine.CurrentDialogue is { CanConfirm: true })
            {
                if (engine.CurrentDialogue.FirstChoice is not null)
                {
                    Assert.Equal((1, 17, 260, 5), (engine.CurrentDialogue.ObjectId, engine.CurrentDialogue.ScriptId, engine.CurrentDialogue.Operation, engine.CurrentDialogue.MessageId));
                    Assert.True(engine.TryApplyAutoPlayDirectCodeEntry());
                }
                else
                {
                    engine.ConfirmDialogue();
                }
            }
            world.AdvanceAutoPlayInputFrame();
            Assert.Null(engine.LastFault);
            if (session.State.FieldId != 903)
            {
                output.WriteLine($"Exited at {engine.Frame}, codes {engine.EventMemory.Read(1038, 1).RequireExact()}, capture flag {engine.EventMemory.Read(1042, 1).RequireExact()}, mission captures {engine.EventMemory.Read(729, 1).RequireExact()}, descents {descents}, ascents {clearedVisits}, longest visit {longestVisit}, deadline {patrolDeadline}, timer {session.ExamTimer}.");
            }
            Assert.Equal(903, session.State.FieldId);
            Assert.Equal(0, engine.EventMemory.Read(1042, 1).RequireExact());
            Assert.Equal(0, engine.EventMemory.Read(729, 1).RequireExact());
            bool hanging = engine.EventMemory.Read(1041, 1).RequireExact() != 0;
            if (wasHanging && !hanging)
            {
                Assert.NotNull(patrolDeadline);
                Assert.NotNull(enteredAt);
                Assert.True(engine.Frame < patrolDeadline.Value, "The physical ascent finished after a retained guard deadline.");
                longestVisit = Math.Max(longestVisit, engine.Frame - enteredAt.Value);
                clearedVisits++;
                output.WriteLine($"Cleared hanging at {engine.Frame}, visit {engine.Frame - enteredAt.Value} callbacks, codes {engine.EventMemory.Read(1038, 1).RequireExact()}.");
            }
            wasHanging = hanging;
        }
        Assert.Equal(5, engine.EventMemory.Read(1038, 1).RequireExact());
        Assert.Equal(5, descents);
        Assert.Equal(4, clearedVisits);
        Assert.InRange(longestVisit, 1L, 358L);
        Assert.Equal(original, File.ReadAllBytes(source));
        Assert.InRange(lastCodeAcceptedAfter, 1L, 258L);
        output.WriteLine($"Five codes completed in {engine.Frame - firstFrame} native callbacks; longest completed descent/code/ascent visit {longestVisit} callbacks; final code accepted after {lastCodeAcceptedAfter} callbacks; captures {engine.EventMemory.Read(1042, 1).RequireExact()}.");
    }

    private static EventEngine Create(JsmProgram program, int expected, int completed = 0)
    {
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 903 });
        EventEngine engine = new(session, NativeEventMemory.Empty.Write(1033, 1, NativeScalarValue.Exact(expected)).Write(1038, 1, NativeScalarValue.Exact(completed)));
        typeof(EventEngine).GetProperty(nameof(EventEngine.FieldId))!.SetValue(engine, 903);
        Set(engine, "program", program);
        return engine;
    }

    private static object Fiber(JsmProgram program, int owner, int scriptId, int position)
    {
        Type type = typeof(EventEngine).GetNestedType("Fiber", BindingFlags.NonPublic)!;
        EventScript script = program.Objects[owner].Scripts.Single(item => item.Id == scriptId);
        object fiber = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [owner, script, 1, 1L, false], null)!;
        Set(fiber, "Position", position);
        return fiber;
    }

    private static object? Invoke(object target, string name, params object[] arguments)
    {
        return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);
    }

    private static object Get(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(target)!;
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
