using Memoria.AutoPlay;

int steps = 0;
int advanced = AutoPlayScheduler.Advance(10, () => steps < 3, () => steps++, TimeSpan.FromSeconds(1));
Check(steps == 3 && advanced == 3, "Paused/stopped adapter exceeded its allowed steps.");
steps = 0;
AutoPlayScheduler.Advance(2, () => true, () => steps++, TimeSpan.FromSeconds(1));
Check(steps == 2, "Step budget was not preserved.");
steps = 0;
AutoPlayScheduler.Advance(20, () => true, () => steps++, TimeSpan.Zero);
Check(steps == 1, "Wall budget did not yield to the graphical host.");
List<int> recorded = [];
for (int action = 0; action < 40; action++)
{
    AutoPlayRecording.AppendRecent(recorded, action);
}
Check(recorded.Count == 32 && recorded[0] == 8 && recorded[^1] == 39, "Recent action recording exceeded its storage bound or changed order.");
Check(AutoPlayRecording.LimitActionText(new string('x', 600)).Length == 512, "Recorded action text exceeded its shared bound.");
AutoPlayTransitionCycleGuard<(string Field, int Story)> transitions = new();
for (int index = 0; index < 6; index++)
{
    Check(!transitions.Observe((index % 2 == 0 ? "a" : "b", 72)), "Transition guard rejected fewer than three complete round trips.");
}
Check(transitions.Observe(("a", 72)), "Transition guard accepted three unchanged two-field round trips.");
transitions.Reset();
for (int index = 0; index < 30; index++)
{
    Check(!transitions.Observe(("a", 72)), "Transition guard rejected ordinary same-field activity.");
}
transitions.Reset();
for (int index = 0; index < 12; index++)
{
    Check(!transitions.Observe((index % 2 == 0 ? "a" : "b", 72 + index / 4)), "Transition guard rejected a route with native story handoffs.");
}
string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".checkpoint");
try
{
    File.WriteAllBytes(path, [1, 2, 3, 4]);
    bool saved = false;
    try
    {
        AutoPlayCheckpoint.Save<int>(path, () => throw new InvalidOperationException("Uninitialized field cannot capture."), (_, _) => saved = true);
        throw new Exception("Capture failure did not propagate.");
    }
    catch (InvalidOperationException)
    {
    }
    Check(!saved && File.ReadAllBytes(path).SequenceEqual(new byte[] { 1, 2, 3, 4 }), "Capture failure replaced the valid standalone checkpoint.");
    AutoPlayCheckpoint.Save(path, () => 8, (destination, value) => File.WriteAllBytes(destination, [(byte)value]));
    Check(File.ReadAllBytes(path).SequenceEqual(new byte[] { 8 }), "Captured value did not reach the selected codec.");
}
finally
{
    File.Delete(path);
}
AutoPlayTourTests.Run();
Console.WriteLine("PASS shared autoplay scheduling, atomic saves and optional-area tour contracts");

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }
}
