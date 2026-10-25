using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private static readonly JsonSerializerOptions DiagnosticBattleJson = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    private void StartDiagnosticBattleHost()
    {
        DiagnosticBattleRequest request = JsonSerializer.Deserialize<DiagnosticBattleRequest>(File.ReadAllText(options.BattleTestPath!), DiagnosticBattleJson) ?? throw new InvalidDataException("Diagnostic battle request is empty.");
        request.Validate();
        session.StartNewGame();
        world = CreateWorld();
        world.StartDiagnosticBattle(request);
        gameMenus?.Close();
        if (request.Driver is { } driver)
        {
            StartDiagnosticDriver(driver);
        }
        Console.WriteLine(JsonSerializer.Serialize(new { DiagnosticInitialization = world.DiagnosticReceipt, Coverage = "PREPARATION_ONLY", Readiness = "Battle entry and case prerequisites must be verified by the driver; no acceptance verdict is available." }, DiagnosticBattleJson));
    }
}
