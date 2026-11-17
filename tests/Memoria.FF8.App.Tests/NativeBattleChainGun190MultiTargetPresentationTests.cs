using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleChainGun190Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleChainGun190MultiTargetPresentationTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Original_ChainGun190_counted_contacts_use_loaded_party_rigs_and_complete_through_cold_boundaries(int contactCount)
    {
        string? gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHAINGUN190_MULTI_EVIDENCE");
        Assert.SkipUnless(gameDirectory is not null && evidence is not null, "Select installed original resources and a distinct ChainGun190 multi-target evidence directory.");
        string output = Path.Combine(evidence!, "count" + contactCount);
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output);
        using GameAssets game = new(gameDirectory!);
        using BattleAssets assets = new(gameDirectory!);
        BattleSnapshot source = NativeBattleChainGun190Fixture.Prepare(game, assets, contactCount, output);
        NativeBattleGraphicsState initialGraphics = source.ReadFixturePresentation().NativeGraphics!;
        for (int actor = 0; actor < 3; actor++)
        {
            NativeBattleGraphicsModel model = ReadOriginalModelCell(initialGraphics, actor);
            NativeBattleGraphicsBinding binding = Assert.Single(source.ReadFixtureGraphicsDefinition()!.Bindings, binding => binding.ActorId == ReadOriginalModelActorId(initialGraphics, actor));
            NativeBattlePoseSource pose = binding.Resource.PoseSource!;
            Assert.NotNull(pose);
            Assert.True(pose.BoneCount > 0);
            Assert.NotNull(model.ActiveResourceRow100);
            Assert.NotNull(model.GeometryWorldMatrix);
            Assert.NotNull(model.Transform);
            Assert.Equal(pose.BoneCount, NativeBattleBodyBoneMatrices.Require(model, pose).Length);
        }
        string logical = LogicalOwners(source);
        BattleScenePresentation scene = new(assets, source.Definition);
        BattleSession battle = Restore(source);
        List<string> boundaries = [];
        HashSet<int> packetBanks = [];
        List<object> observations = [];
        int[] glyphs = new int[3];
        bool setup = false;
        bool beforeContacts = false;
        bool rendered = false;
        int contactCrossings = 0;
        int completions = 0;
        int changedPixels = 0;
        int ticks = 0;
        BattleSnapshot prior = source;
        string boundary = "queued";
        try
        {
            ColdNext("queued", source);
            for (; ticks < 800; ticks++)
            {
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                NativeBattleModulePoolTask? oldMain = MainTask(prior);
                boundary = "native-callback";
                battle.Tick();
                BattleSnapshot current = battle.CaptureForContinuation();
                NativeBattleOriginalEffectState? after = Selected(current);
                NativeBattleModulePoolTask? main = MainTask(current);
                Assert.Equal(logical, LogicalOwners(current));
                Assert.NotNull(current.ReadFixturePresentation().NativeCamera?.Projection?.View);
                if (after?.ReturnedDescriptor is > 0)
                {
                    uint cursor = current.ReadFixturePresentation().NativeEffectMemory!.ChainGun190!.ReadDword(CursorGlobal);
                    Assert.InRange(cursor, PacketBase, PacketBase + PacketBytes);
                    packetBanks.Add(checked((int)((cursor - PacketBase) / PacketBankBytes)));
                }
                if (!setup && main?.ReadWord(12) == 1)
                {
                    setup = true;
                    AssertSetupRecords(current, contactCount);
                    NativeBattleChainGun190State state = current.ReadFixturePresentation().NativeEffectMemory!.ChainGun190!;
                    Assert.NotNull(current.ReadFixturePresentation().NativeEffectMemory!.GeometryRegisters);
                    Assert.NotNull(current.ReadFixturePresentation().NativeEffectMemory!.ScriptedColorRegisters);
                    Assert.Equal(JsonSerializer.Serialize(game.ReadNativeChainGun190Resources().Camera), JsonSerializer.Serialize(current.ReadFixturePresentation().NativeEffectMemory!.Textures!.Animation!.Source));
                    observations.Add(new { Boundary = "setup-main1", ActualPhysicalThird = current.ReadFixturePresentation().NativeActionPackets!.ExportRetained().Contacts[2], CrtBeforeTick = prior.NativeCrtRandom, CrtAfterTick = current.NativeCrtRandom, GeometryBeforeTick = prior.ReadFixturePresentation().NativeEffectMemory!.GeometryRegisters, GeometryAfterTick = current.ReadFixturePresentation().NativeEffectMemory!.GeometryRegisters, ColorRegistersAfterTick = current.ReadFixturePresentation().NativeEffectMemory!.ScriptedColorRegisters, Records = state.ReadView(RecordsAddress, 12 * RecordStride), LoadedPartyModels = ReadOriginalModelCells(current.ReadFixturePresentation().NativeGraphics!).Take(3).ToArray(), Scope = "Actual complete graphics Tick CRT and GTE publication; exact effect-local RNG branches are covered by the dedicated Runtime tests" });
                    ColdNext("setup-main1", current);
                }
                if (!beforeContacts && main?.ReadWord(12) == 30)
                {
                    beforeContacts = true;
                    ColdNext("before-old30-contact", current);
                }
                if (oldMain?.ReadWord(12) == 30 && main?.ReadWord(12) == 31)
                {
                    contactCrossings++;
                    Assert.Equal(contactCount, current.ReadFixturePresentation().NativeActionPackets!.ReadHeaderContacts(after!.Parameters!.Header).Length);
                    observations.Add(new { Boundary = "old30-contact", current.Frame, current.ReadFixturePresentation().NativeGraphics!.Callback, Count = contactCount, DeclaredContacts = current.ReadFixturePresentation().NativeActionPackets!.ReadHeaderContacts(after.Parameters.Header) });
                }
                for (int slot = 0; slot < current.ReadFixturePresentation().NativeGraphics!.Effects.Slots.Length; slot++)
                {
                    NativeBattleEffectTask text = current.ReadFixturePresentation().NativeGraphics!.Effects.Slots[slot];
                    if ((text.Flags0 & 1) == 0 || text.Kind != NativeBattleEffectKind.DamageText5069B0 || (prior.ReadFixturePresentation().NativeGraphics!.Effects.Slots[slot].Flags0 & 1) != 0 || text.ModelOwner is not BattlePartyModelOwner)
                    {
                        continue;
                    }
                    var declared = current.ReadFixturePresentation().NativeActionPackets!.ReadHeaderContacts(new BattleActionHeaderReference(0));
                    NativeBattleContactPacket contact = Assert.Single(declared, item => item.Primary.Recipient == text.ModelOwner);
                    int targetOrdinal = declared.IndexOf(contact);
                    Assert.InRange(targetOrdinal, 0, contactCount - 1);
                    NativeBattleContactHalf half = contact.Primary;
                    NativeBattleEffectTask expected = Assert.Single(ExpectedOriginalDamageText(half, half.Amount6).Slots, task => (task.Flags0 & 1) != 0);
                    Assert.Equal(expected.Glyphs.ToArray(), text.Glyphs.ToArray());
                    Assert.Equal(expected.Color, text.Color);
                    Assert.Equal(expected.HalfWidth, text.HalfWidth);
                    glyphs[targetOrdinal]++;
                }
                if (!rendered && current.ReadFixturePresentation().NativeGraphics!.PrimitiveFrame is { } frame && frame.LastCommands.Any(command => command.Command.Packet.Bank == NativeBattleChainGun190PacketArena.PacketBank))
                {
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame plainFrame = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.Packet.Bank != NativeBattleChainGun190PacketArena.PacketBank).ToImmutableArray() };
                    BattleSnapshot plain = current.WithFixturePresentation(facts => facts with { NativeGraphics = current.ReadFixturePresentation().NativeGraphics! with { PrimitiveFrame = plainFrame } });
                    byte[] without = new BattleScenePresentation(assets, source.Definition).Render(BattleSession.Restore(plain), 320, 224).ToArray();
                    int changed = CountRgbChanges(pixels, without);
                    if (changed > 0)
                    {
                        changedPixels = changed;
                        rendered = true;
                        NativeBattleRestorationPresentationTests.Publish(output, "private-contribution", current, pixels);
                        NativeBattleRestorationPresentationTests.Publish(output, "private-contribution-without", plain, without);
                    }
                }
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                    Assert.Empty(after.Pools);
                    Assert.NotNull(current.ReadFixturePresentation().NativeEffectMemory!.ChainGun190!.RootBankStorage);
                    Assert.NotNull(current.ReadFixturePresentation().NativeEffectMemory!.ChainGun190!.ChildBankStorage);
                    ColdNext("root-drained", current);
                }
                if (setup && Released(current))
                {
                    break;
                }
            }
            Assert.True(setup, "The original controller never reached its setup/main1 boundary.");
            Assert.True(beforeContacts, "The original director never reached old30.");
            Assert.Equal(1, contactCrossings);
            Assert.Equal(1, completions);
            for (int actor = 0; actor < 3; actor++)
            {
                Assert.Equal(actor < contactCount ? 1 : 0, glyphs[actor]);
            }
            Assert.Contains(0, packetBanks);
            Assert.Contains(1, packetBanks);
            Assert.True(rendered && changedPixels > 0, "No original private packet changed the actual rendered RGB output.");
            BattleSnapshot completed = battle.Capture();
            Assert.True(Released(completed));
            Assert.Equal(logical, LogicalOwners(completed));
            ColdNext("completed", completed);
            File.WriteAllText(Path.Combine(output, "observations.json"), JsonSerializer.Serialize(observations));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect = 190, ContactCount = contactCount, ticks, contactCrossings, glyphs, completions, changedPixels, PacketBanks = packetBanks.Order().ToArray(), ColdBoundaries = boundaries, Scope = "Original Enemy8/action154/sequence14/formation155 and original loaded party0/1/2; explicitly declared counted post-formula contacts, actual cold physical third contact for count2, logical HP/status retained; no ordinary AOE formula or AI admission", BinarySha256 = NativeBattleFixtureInspection.CollectBinaryHashes() }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", prior, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "observations.json"), JsonSerializer.Serialize(observations));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Effect = 190, ContactCount = contactCount, boundary, ticks, contactCrossings, glyphs, completions, ColdBoundaries = boundaries, Error = error.ToString() }));
            throw;
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession value = BattleSession.Restore(snapshot);
            value.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return value;
        }

        void ColdNext(string name, BattleSnapshot snapshot)
        {
            boundary = "cold-" + name;
            NativeBattleRestorationPresentationTests.Publish(output, name, snapshot, null);
            BattleSession cold = Restore(NativeBattleFixtureInspection.ReadLogicalSnapshot(Path.Combine(output, name + ".battle.json")));
            BattleSession direct = Restore(snapshot);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            cold.Tick();
            direct.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = new BattleScenePresentation(assets, source.Definition).Render(cold, 320, 224).ToArray();
            Assert.Equal(scene.Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output, name + "-next", cold.Capture(), pixels);
            boundaries.Add(name);
        }
    }

    private static void AssertSetupRecords(BattleSnapshot snapshot, int contactCount)
    {
        NativeBattleActionPacketsState packets = snapshot.ReadFixturePresentation().NativeActionPackets!;
        Assert.Equal((byte)contactCount, packets.ReadHeader(new BattleActionHeaderReference(0)).ReadContactCount());
        Assert.Equal((byte)(contactCount == 2 ? 0 : 2), packets.ExportRetained().Contacts[2]!.Primary.ActorIndex);
        if (contactCount == 2)
        {
            Assert.Equal(new byte[24], NativeBattleContactPacketCodec.Encode(packets.ExportRetained().Contacts[2]!));
        }
        NativeBattleChainGun190State state = snapshot.ReadFixturePresentation().NativeEffectMemory!.ChainGun190!;
        int middle = contactCount == 2 ? 0 : 1;
        int high = contactCount == 2 ? 1 : 2;
        AssertInterpolated(2, middle, high);
        AssertInterpolated(7, high, 0);
        for (int index = 0; index < 12; index++)
        {
            Assert.Equal(1u, state.ReadDword(RecordsAddress + (uint)(index * RecordStride)));
        }
        void AssertInterpolated(int start, int from, int to)
        {
            NativeBattleVector first = ReadOriginalModelCell(snapshot.ReadFixturePresentation().NativeGraphics!, from).Transform!.Position28;
            NativeBattleVector last = ReadOriginalModelCell(snapshot.ReadFixturePresentation().NativeGraphics!, to).Transform!.Position28;
            short stepX = unchecked((short)((last.X - first.X) / 3));
            short stepZ = unchecked((short)((last.Z - first.Z) / 3));
            for (int index = 0; index < 2; index++)
            {
                uint address = RecordsAddress + (uint)((start + index) * RecordStride);
                Assert.Equal(uint.MaxValue, state.ReadDword(address + 4));
                Assert.Equal(unchecked((ushort)(first.X + stepX * (index + 1))), state.ReadWord(address + 8));
                Assert.Equal((ushort)0, state.ReadWord(address + 10));
                Assert.Equal(unchecked((ushort)(first.Z + stepZ * (index + 1))), state.ReadWord(address + 12));
            }
        }
    }

    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.ReadFixturePresentation().NativeGraphics?.OriginalEffect is { Binding.Effect: 190 } effect ? effect : null;
    private static NativeBattleModulePoolTask? MainTask(BattleSnapshot snapshot) => Selected(snapshot)?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == ChildDescriptor)?.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == MainCallback);
    private static string LogicalOwners(BattleSnapshot snapshot) => JsonSerializer.Serialize(snapshot.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, MaximumHp = snapshot.Definition.Combatants.Single(definition => definition.Id == actor.Id).MaximumHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle }));
    private static bool Released(BattleSnapshot snapshot) => snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.ReadFixturePresentation().NativeEffectMemory!.Active == 0 && !snapshot.ReadFixturePresentation().NativeGraphics!.Descriptors.Queue.Any(index => snapshot.ReadFixturePresentation().NativeGraphics!.Descriptors.Packets[index].Command == 104) && Selected(snapshot) is { ReturnedDescriptor: 0, Pools.IsEmpty: true };

    private static int CountRgbChanges(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int changed = 0;
        for (int offset = 0; offset < first.Length; offset += 4)
        {
            if (first[offset] != second[offset] || first[offset + 1] != second[offset + 1] || first[offset + 2] != second[offset + 2])
            {
                changed++;
            }
        }
        return changed;
    }
}
