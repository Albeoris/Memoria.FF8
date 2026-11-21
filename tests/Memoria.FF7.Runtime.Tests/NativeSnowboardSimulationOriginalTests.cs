using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardSimulationOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select an installed original resource profile."));
        Ff7SnowboardNativeData native = Ff7PcExecutableData.LoadSnowboardData(installation);
        Ff7SnowboardSimulation simulation = new(native, 1);
        Vector3 initial = simulation.Rider.Motion.Position;
        int introTicks = 0;
        for (; introTicks < 600 && (simulation.Rider.Script.ActionWord & 255) == 16; introTicks++)
        {
            simulation.AdvanceIntro(0x1000, 0, introTicks);
        }
        if (simulation.Rider.Motion.Position != initial || (simulation.Rider.Script.ActionWord & 255) == 16)
        {
            throw new InvalidOperationException($"The native snowboard stationary intro did not reach its real rider-input action boundary: initial={initial}, position={simulation.Rider.Motion.Position}, action={simulation.Rider.Script.ActionWord}, pc={simulation.Rider.Script.ProgramCounter}, remaining={simulation.Rider.Script.Remaining}, canPush={simulation.Rider.Script.CanPush}.");
        }
        for (int tick = 0; tick < 120; tick++)
        {
            simulation.AdvanceMotion(ending: false);
            simulation.AdvanceRider(0x1000, 0, tick + introTicks);
        }
        if (simulation.Rider.Motion.Position == initial || simulation.Navigation.CurrentSegment == 0 || !float.IsFinite(simulation.Rider.Motion.Position.X) || !float.IsFinite(simulation.Rider.Motion.Position.Y) || !float.IsFinite(simulation.Rider.Motion.Position.Z))
        {
            throw new InvalidOperationException("The native snowboard motion components did not advance over their installed course geometry.");
        }
        if (introTicks != 287 || simulation.Rider.Motion.Position != new Vector3(3364935, 167936, -8712471) || simulation.Rider.Motion.Speed != 389 || simulation.Navigation.CurrentCourse != 0 || simulation.Navigation.CurrentSegment != 1 || simulation.Navigation.CurrentTriangle != 31 || (simulation.Rider.Script.ActionWord & 255) != 2)
        {
            throw new InvalidOperationException("The installed snowboard setup, intro and 120 motion callbacks disagree with the original native trajectory.");
        }
        Console.WriteLine($"Native snowboard motion components only: introCallbacks={introTicks}, position={simulation.Rider.Motion.Position}, speed={simulation.Rider.Motion.Speed}, course={simulation.Navigation.CurrentCourse}, segment={simulation.Navigation.CurrentSegment}, triangle={simulation.Navigation.CurrentTriangle}, action={simulation.Rider.Script.ActionWord & 255}. Scene objects and field return are separate required contracts.");
    }
}
