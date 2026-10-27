using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class AutoPlayCommand
{
    public static int Run(string[] args)
    {
        string? output = null;
        string? resume = null;
        long? maxSteps = null;
        List<string> launch = [];
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument is "--output" or "--resume" or "--max-steps")
            {
                if (++index >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {argument}.");
                }
                switch (argument)
                {
                    case "--output":
                        output = Path.GetFullPath(args[index]);
                        break;
                    case "--resume":
                        resume = Path.GetFullPath(args[index]);
                        break;
                    case "--max-steps":
                        maxSteps = long.Parse(args[index]);
                        if (maxSteps < 1)
                        {
                            throw new ArgumentOutOfRangeException(nameof(args), "Maximum steps must be positive.");
                        }
                        break;
                }
            }
            else
            {
                launch.Add(argument);
            }
        }
        LaunchOptions options = LaunchOptions.Parse(launch.ToArray());
        if (options.StatePath is not null)
        {
            throw new ArgumentException("Use --resume <run.json> to resume both the standalone game snapshot and exploration coverage.");
        }
        output ??= resume is null ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.SavePath))!, "autoplay") : Directory.GetParent(Path.GetDirectoryName(resume)!)!.FullName;
        GameSession session = GameWorld.CreateColdProcess(options.GameDirectory);
        using GameResourceHost resources = new(options.GameDirectory, watch: false, modsDirectory: options.ModsDirectory);
        GameWorld? world = null;
        GameWorld GetWorld() => world ??= new(session, options.GameDirectory, new MovieClock(session, options.GameDirectory, options.MediaToolsDirectory, resources.ResolveGameFile), resources.Resources, validateWorldPresentation: false, resolveGameFile: resources.ResolveGameFile, modelOverrides: resources, readGameplayPolicy: () => resources.GameplayPolicy);
        void Restore(GameState state)
        {
            world?.Dispose();
            world = null;
            session.Restore(state);
        }
        AutoPlayController controller = new(session, GetWorld, Restore, output, createColdProcess: () => GameWorld.CreateColdProcess(options.GameDirectory));
        try
        {
            if (resume is null)
            {
                controller.Start();
            }
            else
            {
                controller.ResumeFrom(resume);
            }
            long lastReport = Environment.TickCount64;
            while (controller.Running)
            {
                try
                {
                    resources.Pump();
                    world?.SynchronizeGameplayTuning();
                }
                catch (Exception exception)
                {
                    controller.ReportHostFailure(exception);
                    break;
                }
                controller.Advance(4096);
                if (maxSteps is long limit && controller.State.Steps >= limit)
                {
                    controller.SaveAndStop();
                }
                if (Environment.TickCount64 - lastReport >= 1000)
                {
                    Console.WriteLine(controller.Status);
                    lastReport = Environment.TickCount64;
                }
            }
            Console.WriteLine(controller.Status);
            if (controller.Error is not null)
            {
                Console.Error.WriteLine(controller.Error);
            }
            if (controller.ResumeCommand is not null)
            {
                Console.WriteLine(controller.ResumeCommand);
            }
            controller.WritePerformanceProfile();
            return controller.Error is null ? 0 : 1;
        }
        finally
        {
            world?.Dispose();
        }
    }
}
