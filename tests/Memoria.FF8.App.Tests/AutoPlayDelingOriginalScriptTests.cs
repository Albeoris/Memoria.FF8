using System.Reflection;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayDelingOriginalScriptTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(199)]
    public void OriginalGuardAssemblesAndComparesTheWrongThenCorrectAutomaticAnswer(int expected)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(761);
        JsmProgram program = JsmProgram.Read(field.Jsm);
        EventScript script = program.Objects[15].Scripts.Single(item => item.Id == 145);
        Assert.Equal("AASK", program.Operations[2015].Opcode.ToString());
        Assert.Equal("JPF", program.Operations[2109].Opcode.ToString());
        AutoPlayDirectCodeEntryState policy = new();

        (EventEngine wrong, BranchDecision wrongComparison) = ExecuteAnswer(field, program, script, policy, expected);

        Assert.Equal((expected + 1) % 200, wrong.EventMemory.Read(1030, 1).RequireExact());
        Assert.NotEqual(expected, wrong.EventMemory.Read(1030, 1).RequireExact());
        Assert.False(wrongComparison.ConditionPassed);
        Assert.NotEqual(353, wrong.EventMemory.Read(256, 2).RequireExact());
        Assert.True(policy.DelingWrongCodeSubmitted);

        (EventEngine correct, BranchDecision correctComparison) = ExecuteAnswer(field, program, script, policy, expected);

        Assert.Equal(expected, correct.EventMemory.Read(1030, 1).RequireExact());
        Assert.True(correctComparison.ConditionPassed);
        Assert.Equal(353, correct.EventMemory.Read(256, 2).RequireExact());
        Assert.Equal(expected, correct.EventMemory.Read(728, 1).RequireExact());
        Assert.Empty(wrong.ActionLog);
        Assert.Empty(correct.ActionLog);
    }

    private static (EventEngine Engine, BranchDecision Comparison) ExecuteAnswer(FieldAssets field, JsmProgram program, EventScript script, AutoPlayDirectCodeEntryState policy, int expected)
    {
        // Isolate the original callback's answer component; field initialization and travel are outside this test.
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = field.Id });
        EventEngine engine = new(session, NativeEventMemory.Empty.Write(728, 1, NativeScalarValue.Exact(expected)));
        engine.AutoPlayCodeEntry = request => AutoPlayDirectCodeEntry.Answer(policy, request);
        typeof(EventEngine).GetProperty(nameof(EventEngine.FieldId))!.SetValue(engine, field.Id);
        Set(engine, "program", program);
        Set(engine, "programHash", Convert.ToHexString(SHA256.HashData(field.Jsm)));
        Set(engine, "messages", field.Messages);
        List<ActorState> actors = (List<ActorState>)Get(engine, "actors");
        actors.AddRange(program.Objects.Select(item => new ActorState(item.Index, item.Label)));
        Type fiberType = typeof(EventEngine).GetNestedType("Fiber", BindingFlags.NonPublic)!;
        object fiber = Activator.CreateInstance(fiberType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [15, script, 7, 1L, false], null)!;
        ((System.Collections.IList)Get(engine, "fibers")).Add(fiber);
        FieldInfo position = fiberType.GetField("Position")!;
        MethodInfo execute = typeof(EventEngine).GetMethod("Execute", BindingFlags.Instance | BindingFlags.NonPublic)!;
        position.SetValue(fiber, 1982);
        BranchDecision? comparison = null;
        engine.BranchEvaluated += branch =>
        {
            if (branch.Operation == 2109)
            {
                comparison = branch;
            }
        };
        for (int count = 0; count < 160; count++)
        {
            int operation = (int)position.GetValue(fiber)!;
            Assert.InRange(operation, 1982, 2111);
            position.SetValue(fiber, operation + 1);
            execute.Invoke(engine, [fiber, program.Operations[operation]]);
            if (engine.CurrentDialogue is { CanConfirm: true, FirstChoice: null })
            {
                engine.ConfirmDialogue();
            }
            if (operation == 2109 && comparison is { ConditionPassed: false } || operation == 2111)
            {
                Assert.NotNull(comparison);
                Assert.Equal(new[] { 1990, 2015, 2034, 2053 }, engine.DialogueOutcomes.Where(item => item.Choice is not null).Select(item => item.Operation));
                return (engine, comparison);
            }
        }
        throw new InvalidOperationException("The original guard answer component did not reach its native comparison.");
    }

    private static object Get(EventEngine engine, string name)
    {
        return typeof(EventEngine).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
    }

    private static void Set(EventEngine engine, string name, object value)
    {
        typeof(EventEngine).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, value);
    }
}
