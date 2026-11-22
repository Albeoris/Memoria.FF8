using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCondorOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CONDOR_SAVE") ?? throw new InvalidOperationException("Select a native Condor departure fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        Ff7CondorResources resources = Ff7CondorResources.Load(installation);
        foreach ((short x, short y, int expected) in new (short, short, int)[] { (288, 992, 241), (112, 662, 253), (304, 427, 184), (304, 426, 205), (304, 416, -1), (64, 992, 27) })
        {
            int actual = Enumerable.Range(0, resources.TerrainCells.Count).Where(index => Ff7CondorGeometry.Contains(resources.TerrainCells[index].Span, (short)(x - 256), (short)(y - 512))).DefaultIfEmpty(-1).First();
            if (actual != expected)
            {
                throw new InvalidOperationException($"Native Condor terrain vector {x},{y}: expected {expected}, observed {actual}.");
            }
        }
        using Ff7Session session = Ff7Session.Restore(installation, source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The Condor module must not fabricate a field choice."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        int maximumEnemies = 0;
        bool combat = false;
        bool overrun = false;
        Ff7CondorSnapshot? last = null;
        while (controller.Error is null && session.Battle is null && timer.Elapsed.TotalSeconds < 30)
        {
            controller.Advance(1);
            if (session.Condor is { } condor)
            {
                last = condor.Capture();
                maximumEnemies = Math.Max(maximumEnemies, last.Enemies.Length);
                combat |= last.Phase == Ff7CondorPhase.Combat;
                overrun |= last.Phase == Ff7CondorPhase.Overrun;
            }
        }
        Console.WriteLine($"Native Condor: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, phase={last?.Phase}, enemies={maximumEnemies}, first={last?.Enemies.FirstOrDefault()}, field={session.MapId}, tick={session.Machine.Tick}, battle={session.Battle?.Formation.FormationId}, result={session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xAC)}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !combat || !overrun || maximumEnemies == 0 || session.Condor is not null || session.MapId != 356 || session.Battle?.Formation.FormationId != 290 || session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xAC) != 1 || session.Progress.Gil != source.Progress.Gil || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The native Condor enemy wave did not execute through its actual field-owned commander battle.", controller.Failure);
        }
    }
}
