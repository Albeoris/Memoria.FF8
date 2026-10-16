using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDrawPointCorpusContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeDrawPointCatalog catalog = assets.ReadNativeDrawPointCatalog();
        NativeDrawPointParticleTables particles = NativeDrawPointParticleTableReader.Read(File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")));
        using IFlArchive outer = FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", "field.fs"), FlArchiveRepresentation.Files);
        static byte[] Read(IFlArchive archive, string basename)
        {
            using Stream input = archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1] == basename).OpenForRead();
            using MemoryStream result = new();
            input.CopyTo(result);
            return result.ToArray();
        }
        IFlArchive Open(string name) => FlArchive.Open(new MemoryStream(Read(outer, name + ".fl"), false), new MemoryStream(Read(outer, name + ".fi"), false), new MemoryStream(Read(outer, name + ".fs"), false), false, FlArchiveRepresentation.Files);
        using IFlArchive map = Open("mapdata");
        string[] names = System.Text.Encoding.ASCII.GetString(Read(map, "maplist")).Replace("\r", "").Split('\n');
        int[] bounded = Enumerable.Range(102, 6).Concat(Enumerable.Range(129, 8)).Concat(Enumerable.Range(145, 6)).Concat(Enumerable.Range(154, 15)).ToArray();
        Dictionary<int, JsmProgram> originals = [];
        foreach (int fieldId in bounded)
        {
            using IFlArchive field = Open(names[fieldId]);
            originals[fieldId] = JsmProgram.Read(Read(field, names[fieldId] + ".jsm"));
        }
        int[] found = originals.Where(pair => pair.Value.Operations.Any(operation => operation.Opcode == Jsm.Opcode.DRAWPOINT)).Select(pair => pair.Key).Order().ToArray();
        Require(found.SequenceEqual([102, 103, 134, 148, 149, 155, 160]), "Bounded original location corpus changed its draw-point field ownership.");
        foreach (int absent in new[] { 106, 129, 130, 165 })
        {
            Require(!originals[absent].Operations.Any(operation => operation.Opcode is Jsm.Opcode.DRAWPOINT or Jsm.Opcode.SETDRAWPOINT), "An absent neighboring field acquired a fabricated original draw point.");
        }
        foreach ((int fieldId, string fieldName, int objectId, int initId, int talkId, int setOperation, int talkOperation, int raw, int pointByte, string magicName, int depletion, int x, int y, int z, int triangle) in new[]
        {
            (102, "bccent_1", 15, 135, 137, 2216, 2232, 7, 0xC7, "Thunder", 2, -485, -698, 0, 55),
            (134, "bdin4", 9, 36, 38, 183, 197, 9, 0xC1, "Fire", 2, 1495, 1261, 24, 24),
            (148, "bgbook_2", 17, 105, 107, 1961, 1977, 4, 0x9B, "Esuna", 3, -1494, 670, 0, 3),
            (160, "bggate_2", 16, 67, 69, 1057, 1071, 1, 0x55, "Cure", 2, -1695, -3948, 481, 306)
        })
        {
            JsmProgram original = originals[fieldId];
            EventScript init = original.Objects[objectId].Scripts.Single(script => script.Id == initId);
            EventScript talk = original.Objects[objectId].Scripts.Single(script => script.Id == talkId);
            Require(names[fieldId] == fieldName && original.Operations[setOperation].Opcode == Jsm.Opcode.SETDRAWPOINT && original.Operations[setOperation - 1].Opcode == Jsm.Opcode.PSHN_L && original.Operations[setOperation - 1].Parameter == 1 && original.Operations[setOperation + 1].Parameter == raw && original.Operations[setOperation + 2].Opcode == Jsm.Opcode.Unknown16 && original.Operations[talkOperation].Opcode == Jsm.Opcode.DRAWPOINT && original.Operations[talkOperation - 1].Parameter == raw, "Original setup/talk did not retain its shared native handlers, raw display operand or full point argument.");
            Require(original.Operations[setOperation - 2].Opcode == Jsm.Opcode.SET3 && original.Operations[setOperation - 2].Parameter == triangle && original.Operations.Skip(setOperation - 5).Take(3).Select(operation => operation.Parameter).SequenceEqual([x, y, z]), "Original draw point moved from its authentic raw anchor/triangle.");
            Require(catalog.PointBytes[raw - 1] == pointByte && catalog.MagicNames[pointByte & 63] == magicName && ((pointByte & 0x40) != 0 ? 2 : 3) == depletion, "Original location changed its executable magic/sign/persistence flags.");
            Require(original.Operations.Skip(talk.Start).Take(talk.End - talk.Start).Select(operation => operation.Opcode).SequenceEqual([Jsm.Opcode.LBL, Jsm.Opcode.UCOFF, Jsm.Opcode.PSHN_L, Jsm.Opcode.DRAWPOINT, Jsm.Opcode.UCON, Jsm.Opcode.RET]), "Original bounded talk script gained an unsupported contextual dependency.");
            FieldAssets fieldAssets = assets.LoadField(fieldId);
            (Jsm.Opcode, int)[] Initialization(int length, int label)
            {
                (Jsm.Opcode, int)[] operations = Enumerable.Repeat((Jsm.Opcode.RET, 0), length).ToArray();
                operations[0] = (Jsm.Opcode.LBL, label);
                return operations;
            }
            byte[] extracted = SyntheticObjects([2], [Initialization(init.End - init.Start, 0), [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Initialization(talk.End - talk.Start, 2)]);
            int sourceOffset = BinaryPrimitives.ReadUInt16LittleEndian(fieldAssets.Jsm.AsSpan(6));
            int destinationOffset = BinaryPrimitives.ReadUInt16LittleEndian(extracted.AsSpan(6));
            // Both original bodies are copied verbatim; only their relocated component labels differ. A declared self-REQ supplies the actual requested RET8 frame.
            fieldAssets.Jsm.AsSpan(sourceOffset + (init.Start + 1) * 4, (init.End - init.Start - 1) * 4).CopyTo(extracted.AsSpan(destinationOffset + 4));
            fieldAssets.Jsm.AsSpan(sourceOffset + (talk.Start + 1) * 4, (talk.End - talk.Start - 1) * 4).CopyTo(extracted.AsSpan(destinationOffset + (init.End - init.Start + 7) * 4));
            (GameSession Session, EventEngine Engine) Create()
            {
                GameSession session = GameSession.CreateColdProcess();
                session.StartNewGame();
                session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
                session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
                EventEngine engine = new(session) { AnimationFrameCount = fieldAssets.GetAnimationFrameCount, ReadNativeDrawPointCatalog = assets.ReadNativeDrawPointCatalog, DrawPointParticleTables = particles };
                engine.LoadField(fieldId, extracted, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                // Explicit initialization of this extracted visual component is not travel or reconstructed chronology.
                engine.RestartNativeDrawPointPresentationForMigration();
                return (session, engine);
            }
            (GameSession session, EventEngine engine) = Create();
            List<ScriptCommand> commands = [];
            engine.CommandExecuted += commands.Add;
            engine.Step();
            engine.Step();
            Require(engine.Scene.DrawPointReset is { SampledDisplayMask: 1 } reset && reset.RawPosition == new FieldFixedPosition(x << 12, y << 12, z << 12) && engine.Scene.DrawPointIndex == raw - 1 && engine.Scene.DrawPointInteraction is { Message: 7 } request && request.RawArgument == raw && request.PointByte == pointByte && engine.CurrentDialogue?.Text == catalog.NoEligibleMessage, "Extracted original location did not execute actual setup/index and shared no-eligible interaction.");
            Require(commands.Single(command => command.Name == "SETDRAWPOINT").Arguments.SequenceEqual([1]) && commands.Single(command => command.Name == "Unknown16").Arguments.SequenceEqual([raw]), "Extracted original component discarded native raw operands.");
            JsonElement saved = engine.Capture();
            (_, EventEngine restored) = Create();
            restored.Restore(saved);
            Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && restored.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Extracted original no-eligible message did not acknowledge normally.");
            engine.Step();
            restored.Step();
            Require(engine.Scene.DrawPointInteraction is null && engine.IsUserControlEnabled && commands.Last(command => command.Name == "DRAWPOINT").Arguments.SequenceEqual([raw]) && session.EventRandom!.DrawPoints!.Depletion.All(value => value == 0) && JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Extracted original continuation depleted a point without acquisition or changed after restore.");
        }
        Console.WriteLine("Original draw-point corpus:102/103,134,148/149,155,160 found;106/129/130/165 absent. Verbatim102/134/148/160 setup/talk bodies, anchors/raw operands/magic/persistence flags and shared interaction/restore passed.");
    }
}
