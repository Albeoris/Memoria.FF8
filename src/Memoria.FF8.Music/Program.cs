using System.Text.Json;

namespace Memoria.FF8;

internal static class MusicProgram
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            using var output = new DirectMusicOutput(new nint(long.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture)), args[1]);
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

    private static void Reply(bool value) => Console.WriteLine(JsonSerializer.Serialize(new { Result = value }));
}
