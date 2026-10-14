using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDressContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterAppearance initial = assets.ReadNewGameCharacterAppearance();
        Require(initial.InitOutSha256 == "4D0D23473AB54AC078B0C77A1672D970BF51E7824BAC1724027B5766B706C9EA" && initial.Records.All(record => record.Dress == 0), "Authentic init.out costume bytes or provenance changed.");
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.PSHN_L, 257), (Jsm.Opcode.SETDRESS, 0), (Jsm.Opcode.POPM_B, 1000), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        (GameSession Session, EventEngine Engine) Create(NativeCharacterAppearance? appearance, byte[] program)
        {
            GameSession session = new(new GameState { CharacterAppearance = appearance, CharacterVitals = assets.ReadNewGameCharacterVitals() });
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(100, program, []);
            return (session, engine);
        }
        (GameSession session, EventEngine engine) = Create(initial, content);
        bool observed = false;
        engine.CommandExecuted += command =>
        {
            if (command.Name == "SETDRESS")
            {
                Require(command.Arguments.SequenceEqual([256, 257]) && engine.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 77, "SETDRESS lost original DWORD arguments or consumed unrelated stack operands.");
                observed = true;
            }
        };
        engine.Step();
        Require(observed && session.CharacterAppearance!.Records[0].Dress == 1 && session.CharacterAppearance.Records.Skip(1).SequenceEqual(initial.Records.Skip(1)), "SETDRESS did not narrow character and costume to low BYTE and update only the matched saved record.");
        JsonElement snapshot = engine.Capture();
        (GameSession restoredSession, EventEngine restored) = Create(initial, content);
        restored.Restore(snapshot);
        Require(JsonElement.DeepEquals(snapshot, restored.Capture()) && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.CharacterAppearance), JsonSerializer.SerializeToElement(restoredSession.CharacterAppearance)), "Independent VM restore lost costume bytes or initialization provenance.");
        engine.Step();
        restored.Step();
        Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Ordinary callback after restored SETDRESS diverged.");
        NativeCharacterAppearance committed = session.CharacterAppearance!;
        engine.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
        Require(session.CharacterAppearance == committed && engine.Capture().GetProperty("CharacterAppearance").GetProperty("Records")[0].GetProperty("Dress").GetInt32() == 1, "A fresh field allocation reset persistent saved-record costume bytes.");

        foreach ((NativeCharacterAppearance? Appearance, int Character) source in new (NativeCharacterAppearance?, int)[] { (null, 0), (initial, 255) })
        {
            byte[] refusedProgram = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.PSHN_L, source.Character), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETDRESS, 0), (Jsm.Opcode.POPM_B, 1000), (Jsm.Opcode.RET, 0));
            (GameSession refusedSession, EventEngine refused) = Create(source.Appearance, refusedProgram);
            try
            {
                refused.Step();
                throw new InvalidOperationException("SETDRESS accepted missing provenance or an unmapped saved-record identity.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement failed = refused.Capture();
            Require(refused.LastFault?.Opcode == "SETDRESS" && failed.GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 4 && failed.GetProperty("Fibers")[0].GetProperty("Stack").EnumerateArray().Select(item => item.GetInt32()).SequenceEqual([1, source.Character, 77]) && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Appearance), JsonSerializer.SerializeToElement(refusedSession.CharacterAppearance)), "Refused SETDRESS changed the PC, stack or persistent costume bytes.");
        }
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        RejectingDressServices rejectedServices = new(initial, vitals);
        EventEngine rejected = new(rejectedServices, new Dictionary<int, int>());
        rejected.LoadField(100, content, []);
        JsonElement owners = JsonSerializer.SerializeToElement(new { rejectedServices.CharacterAppearance, rejectedServices.CharacterVitals });
        try
        {
            rejected.Step();
            throw new InvalidOperationException("SETDRESS ignored rejected appearance publication.");
        }
        catch (EventScriptException exception) when (exception.InnerException is NotSupportedException && rejected.LastFault?.Opcode == "SETDRESS")
        {
        }
        JsonElement rejection = rejected.Capture();
        Require(rejectedServices.Attempts == 1 && rejection.GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 4 && rejection.GetProperty("Fibers")[0].GetProperty("Stack").EnumerateArray().Select(item => item.GetInt32()).SequenceEqual([257, 256, 77]) && JsonElement.DeepEquals(owners, JsonSerializer.SerializeToElement(new { rejectedServices.CharacterAppearance, rejectedServices.CharacterVitals })), "Rejected SETDRESS publication advanced its PC, consumed stack or changed any saved owner.");
    }

    private sealed class RejectingDressServices(NativeCharacterAppearance appearance, NativeCharacterVitals vitals) : IEventServices
    {
        public NativeCharacterAppearance CharacterAppearance => appearance;
        public NativeCharacterVitals CharacterVitals => vitals;
        public bool IsMoviePlaying => false;
        public int Attempts { get; private set; }
        public void SetCharacterAppearance(NativeCharacterAppearance next)
        {
            Attempts++;
            throw new NotSupportedException("Component service rejects native costume publication.");
        }
        public void RequestMovie(int index)
        {
            throw new NotSupportedException();
        }
        public void RequestField(int fieldId, int entryPoint)
        {
            throw new NotSupportedException();
        }
        public void ShowDialog(string text)
        {
            throw new NotSupportedException();
        }
    }
}
