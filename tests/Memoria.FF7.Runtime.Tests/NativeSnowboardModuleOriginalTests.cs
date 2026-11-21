using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardModuleOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SNOWBOARD_MODULE_SAVE") ?? throw new InvalidOperationException("Select a native snowboard module admission fixture.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        FieldHostRequest request = session.Host.PendingMinigame ?? throw new InvalidOperationException("The selected fixture has no native minigame owner.");
        if (request.Command is not FieldMinigameCommand { Game: 2, Return: { Map: 658, X: -201, Y: 3116, Triangle: 71, Direction: 64 } })
        {
            throw new InvalidOperationException("The selected native fixture does not own the required snowboard return packet.");
        }
        byte beforeResult = session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xCE);
        long generation = session.FieldGeneration;
        long fieldTick = session.Machine.Tick;
        using Ff7AutoPlayPolicy policy = new(session, (key, _) => "memory:" + key);
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.FieldGeneration != generation, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        bool active = false;
        byte? result = null;
        int route = -1;
        List<int> courses = new();
        bool frameValidated = false;
        for (int poll = 0; poll < 100000 && timer.Elapsed.TotalSeconds < 90 && controller.Running && controller.Error is null && session.FieldGeneration == generation; poll++)
        {
            if (session.Snowboard is { } snowboard)
            {
                if (!active && session.Machine.Capture().Entities[request.Entity].ProgramCounter != request.ProgramCounter)
                {
                    throw new InvalidOperationException("The native snowboard field request changed its suspended opcode boundary.");
                }
                active = true;
                route = snowboard.Course;
                if (!frameValidated && snowboard.Frame.Phase == Ff7SnowboardPhase.Playing)
                {
                    foreach (Ff7SnowboardModelInstance instance in snowboard.Frame.Instances)
                    {
                        foreach (var primitive in snowboard.Resources.Model.Objects[instance.ModelIndex].Primitives)
                        {
                            if (primitive.InputWordCount != (instance.NativePrimitiveKind == 2 ? 6 : 5))
                            {
                                throw new InvalidDataException("The native snowboard frame selected a primitive consumer with a different source record layout.");
                            }
                        }
                    }
                    frameValidated = true;
                }
                if (courses.Count == 0 || courses[^1] != route)
                {
                    courses.Add(route);
                }
                if (session.Host.PendingMinigame != request || session.CanSaveMemoria || session.Machine.Tick != fieldTick || session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xCE) != beforeResult)
                {
                    throw new InvalidOperationException("The active snowboard changed its field owner, allowed an active player snapshot or published a result before native retirement.");
                }
                result = snowboard.NativeData.Results[snowboard.Course];
            }
            controller.Advance(1);
        }
        if (!SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The native snowboard fixture changed during the bounded continuation.");
        }
        byte actualResult = session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xCE);
        if (controller.Error is not null && session.Snowboard is { } stopped)
        {
            Console.WriteLine($"Stopped input: heading={stopped.Simulation.Rider.Heading}, velocity={stopped.Simulation.Rider.Motion.Velocity}, canPush={stopped.Simulation.Rider.Script.CanPush}, target={Ff7SnowboardAutoPlay.ReadTarget(stopped.NativeData, stopped.Course, stopped.Segment, stopped.Simulation.Navigation.CurrentTriangle)}, held={Ff7SnowboardAutoPlay.ChooseInput(stopped):X4}");
        }
        Console.WriteLine($"Native snowboard module: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, generation={session.FieldGeneration}, active={active}, route={String.Join(",", courses)}, result={actualResult}, native={session.Snowboard?.NativeProgress}, error={controller.Failure?.Message ?? controller.Error}");
        if (!active || controller.Error is not null || session.Snowboard is not null || session.FieldGeneration != generation + 1 || session.MapId != 658 || actualResult is not (1 or 2 or 4 or 8) || result is not null && actualResult != result)
        {
            throw new InvalidOperationException("The bounded snowboard continuation has not completed its native course and encoded field return.", controller.Failure);
        }
    }
}
