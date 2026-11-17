using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayStochasticPresentationTests
{
    internal static void Run()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_STOCHASTIC_PRESENTATION_SAVE") ?? throw new InvalidOperationException("Select an external random-dialogue boundary.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), source);
        FieldScriptAnalysis analysis = session.Machine.AnalyzeStochasticPresentation(16, 1, session.ReadSpatialRandomDomain());
        if (!analysis.Complete)
        {
            throw new InvalidOperationException(analysis.Reason);
        }
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected random dialogue choice."));
        policy.State.FrontierIdleSteps = 0;
        policy.State.FrontierProgressKey = null;
        bool accepted = false;
        bool returned = false;
        bool dialogue = false;
        bool shop = false;
        int draws = 0;
        HashSet<long> receipts = [];
        session.InteractionScriptRequested += (entity, script, admitted) => accepted |= entity == 16 && script == 1 && admitted;
        session.NativeTickCompleted += tick =>
        {
            returned |= tick.ScriptReturns.Any(receipt => receipt.Entity == 16 && receipt.Priority == 1 && receipt.ProgramCounter == 0x753);
        };
        byte initialIndex = session.Progress.FieldRandomIndex;
        byte stride = session.ReadSpatialRandomDomain().Stride;
        session.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (request.Entity != 16 || request.Priority != 1)
            {
                return;
            }
            if (request.Command is FieldRandomCommand && receipts.Add(request.Id))
            {
                draws++;
            }
            dialogue |= request.Command is FieldDialogueCommand { Text: 52 or 113 };
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 2048 && controller.Running && !returned; step++)
        {
            controller.Advance(1);
            shop |= session.Shop?.ShopId == 41;
        }
        if (!accepted || !returned || !dialogue || !shop || draws != 1 || stride == 0 || session.Progress.FieldRandomIndex != unchecked((byte)(initialIndex + stride)) || controller.Error is not null)
        {
            throw new InvalidOperationException($"Random presentation did not complete its native event: accepted={accepted}, returned={returned}, dialogue={dialogue}, shop={shop}, draws={draws}, steps={controller.Steps}; {controller.Error ?? policy.State.Status}");
        }
        if (!bytes.SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException("The original stochastic presentation source changed.");
        }
        Console.WriteLine($"PASS original native random dialogue, one draw, shop and return after {controller.Steps} callbacks; source unchanged");
    }
}
