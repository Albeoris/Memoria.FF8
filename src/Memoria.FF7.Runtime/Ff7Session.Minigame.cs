using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    internal Ff7HighwayStart? HighwayStart { get; private set; }

    private void AdvanceMinigame(FieldHostRequest request)
    {
        FieldMinigameCommand command = (FieldMinigameCommand)request.Command;
        string boundary;
        try
        {
            if (command.Game == 0)
            {
                FieldVariables variables = Machine.Variables;
                byte mode = variables.ReadByte(FieldBank.Persistent1Byte, 0x73);
                ushort highScore = unchecked((ushort)variables.ReadWord(FieldBank.Persistent2Word, 0x6D));
                bool arcade = mode == 1;
                if (mode > 1)
                {
                    throw new NotSupportedException($"FFVII highway mode {mode} is unsupported.");
                }

                if (HighwayStart is { } prepared)
                {
                    if (prepared.Request != request || prepared.Arcade != arcade || prepared.HighScore != highScore)
                    {
                        throw new InvalidDataException("The prepared FFVII highway no longer matches its native departure owner.");
                    }
                }
                else
                {
                    HighwayStart = Ff7HighwayStart.Prepare(request, arcade, highScore, Ff7HighwayResources.Load(installation, arcade));
                }
                boundary = "FFVII highway requires its native prop and rider initialization.";
            }
            else
            {
                boundary = $"FFVII minigame {command.Game} requires its native gameplay module before returning to map {command.Return.Map}.";
            }
        }
        catch (Exception error) when (error is NotSupportedException or InvalidDataException or IOException)
        {
            throw MinigameFault(request, error);
        }
        throw MinigameFault(request, new NotSupportedException(boundary));
    }

    private FieldExecutionException MinigameFault(FieldHostRequest request, Exception error)
    {
        return new(new(Machine.ProgramIdentity, Machine.Tick, request.Entity, request.Priority, request.ProgramCounter, 0x20), error.Message, error);
    }
}
