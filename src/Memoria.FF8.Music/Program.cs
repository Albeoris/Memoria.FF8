using System.Text.Json;

namespace Memoria.FF8;

internal static class MusicProgram
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] is "--render" or "--play")
            {
                return AudioHarness.Run(args);
            }
            if (args.Length != 1)
            {
                throw new ArgumentException("The music worker requires its music directory.");
            }
            using var output = new PortableMusicOutput(args[0]);
            Reply(true);
            while (Console.ReadLine() is string line)
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(line);
                    JsonElement command = document.RootElement;
                    switch (command.GetProperty("Command").GetString())
                    {
                        case "Play":
                            output.Play(command.GetProperty("Path").GetString()!, command.GetProperty("Volume").GetInt32(), command.GetProperty("StartPoint").GetInt32());
                            break;
                        case "Volume":
                            output.Volume(command.GetProperty("Volume").GetInt32());
                            break;
                        case "Stop":
                            output.Stop();
                            break;
                        case "Pause":
                            output.Pause();
                            break;
                        case "Resume":
                            output.Resume();
                            break;
                        case "Playing":
                            Reply(output.IsPlaying);
                            continue;
                        case "Quit":
                            Reply(true);
                            return 0;
                        default:
                            throw new ArgumentException("Unknown music output command.");
                    }
                    Reply(true);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { Error = exception.Message }));
                }
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { Error = exception.Message }));
            return 1;
        }
    }

    private static void Reply(bool value)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { Result = value }));
    }
}
