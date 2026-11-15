using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFullCure280Tests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 1792)]
    [InlineData(8, 2048)]
    [InlineData(57, 2048)]
    [InlineData(58, 1792)]
    [InlineData(64, 256)]
    [InlineData(65, 0)]
    public void Original_director_flash_retains_its_two_eight_frame_ramps(short counter, int expected)
    {
        Assert.Equal(expected, NativeBattleFullCure280Geometry.Flash(counter));
    }

    [Fact]
    public void Ordinary_offsets_wrap_to_words_before_camera_rotation()
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 100);
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        NativeBattleGfAnimationNode node = new(0, 0, bytes.ToImmutableArray());
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattleActionMatrix actual = NativeBattleFullCure280Geometry.Compose(node, view, NativeBattleFullCure280State.FromColdProcess().Billboard, new(32760, 0, 0));
        Assert.Equal(unchecked((short)(32760 + 100)) + 10, actual.X);
        Assert.Equal(20, actual.Y);
        Assert.Equal(30, actual.Z);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Target_births_contacts_and_retirement_follow_the_selected_storage_profile(bool expanded)
    {
        BattleModelOwner caster = OriginalOwner(0);
        BattleModelOwner extra = new BattleForeignPartyModel(new("full-cure-extra"));
        BattleModelRoster roster = expanded ? new(OriginalRoster.Owners.Append(extra)) : OriginalRoster;
        NativeBattleModelAliases aliases = new(roster, Enumerable.Range(0, 7).Select(index => (NativeBattleModelSlot.Decode((byte)index), OriginalOwner(index))));
        BattleModelOwner[] recipients = expanded
            ? [caster, OriginalOwner(1), extra, OriginalOwner(2), extra, OriginalOwner(3), OriginalOwner(4)]
            : [caster, OriginalOwner(1), OriginalOwner(2)];
        var packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(caster, 2, 4, 0, 0, 280, null));
        foreach (BattleModelOwner recipient in recipients)
        {
            (packets, _) = packets.AppendContact(new(new(recipient, 1, 0, 1, 0, 7, 0), null));
        }
        (packets, _) = packets.Publish(recipients.Length);
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleModelTransform transform = new(new(0, 0, 0), new(10, 0, 500), 0, 0, 4096);
        NativeBattleGraphicsModel model = new(0) { Transform = transform };
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => model, expanded ? BattleContactTaskStorageContract.ExpandableArena : BattleContactTaskStorageContract.OriginalArena);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2508018, 0x2508028, 20, 1);
        (root, int rootSlot) = root.Append(0x67E7F0);
        root.Slots[rootSlot].WriteDword(12, 0).WriteDword(16, 0);
        var effect = new NativeBattleOriginalEffectState(280, 279, new(280, 0x67E730, 0x67E740) { FullCure280Resources = Resources() }, new(caster, 0, BattleModelTargets.From(recipients), header, 0, 0), 0x2508018)
        {
            Pools = [root],
            FullCure280 = new(0)
        };
        graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        var session = new BattleSession(new BattleDefinition
        {
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        var arena = NativeBattleEffectArena.Clear().Write(320 + 12, [0x81]);
        SetProperty(nameof(BattleSession.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess() with { Arena = arena, FullCure280 = NativeBattleFullCure280State.FromColdProcess() });
        SetProperty(nameof(BattleSession.NativeFileStaging), NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()));
        SetProperty(nameof(BattleSession.NativeStage), NativeBattleStageState.FromColdProcess());
        SetProperty(nameof(BattleSession.NativeCamera), NativeBattleCameraState.FromColdProcess() with { Projection = new(identity, 100, 160, 108) });
        Field("nativeActionPackets").SetValue(session, packets);
        var births = new List<(int Age, BattleModelOwner Owner)>();
        int callback = 0;
        var actors = roster.Owners.ToDictionary(owner => owner, owner => new NativeBattleForeignActor(owner, transform, new(10, 0, 500, 0), (selector, _) =>
        {
            if (selector == 0xF1)
            {
                births.Add((callback - 1, owner));
            }
            return transform.Position28;
        }) { BoneCount = 32 });
        Field("presentationForeignActors").SetValue(session, actors);
        var applied = (List<NativeBattleContactPacket>)Field("presentationContacts").GetValue(session)!;
        MethodInfo advance = typeof(BattleSession).GetMethod("AdvanceNativeFullCure280", BindingFlags.Instance | BindingFlags.NonPublic)!;
        NativeBattleEffectTaskPool? originalTargets = null;
        int peakSparks = 0;
        for (; callback < 100 && graphics.OriginalEffect!.ReturnedDescriptor != 0; callback++)
        {
            graphics = Assert.IsType<NativeBattleGraphicsState>(advance.Invoke(session, [graphics]));
            if (graphics.OriginalEffect!.Pools.Length == 4)
            {
                NativeBattleEffectTaskPool targets = graphics.OriginalEffect.Pools[2];
                NativeBattleEffectTaskPool sparks = graphics.OriginalEffect.Pools[3];
                Assert.Equal(expanded ? recipients.Length : 3, targets.Slots.Length);
                Assert.Equal(expanded ? 20 : 16, sparks.Slots.Length);
                Assert.Equal(expanded, NativeBattlePresentationTaskStorage.IsHostedBank(targets.BankAddress, 2268, targets.Slots.Length));
                Assert.Equal(expanded, NativeBattlePresentationTaskStorage.IsHostedBank(sparks.BankAddress, 20, sparks.Slots.Length));
                peakSparks = Math.Max(peakSparks, sparks.Slots.Count(task => (task.Flags & 1) != 0));
            }
            if (expanded && callback == 41)
            {
                originalTargets = graphics.OriginalEffect.Pools[2];
                graphics = graphics with { OriginalEffect = NativeBattleTestSnapshots.Independent(graphics.OriginalEffect) };
                NativeBattleEffectTaskPool copied = graphics.OriginalEffect.Pools[2];
                Assert.NotSame(originalTargets.RequireOwner(), copied.RequireOwner());
                Assert.Equal(originalTargets.BankAddress, copied.BankAddress);
                Assert.Equal(recipients.Skip(2), copied.Slots.Where(task => (task.Flags & 1) != 0 && task.ReadWord(22) == 1)
                    .Select(task => Assert.Single(task.ReadExecutionReference<BattleExecutionReference.OrderedContactRecipients>(12, 4).Recipients).Owner));
            }
            if (expanded && callback == 42)
            {
                Assert.Equal(5, originalTargets!.Slots.Count(task => (task.Flags & 1) != 0 && task.ReadWord(22) == 1));
                Assert.Equal(5, graphics.OriginalEffect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0 && task.ReadWord(22) == 2));
            }
        }
        int[] expectedAges = expanded ? [0, 20, 40, 40, 40, 40, 40] : [0, 20, 40];
        Assert.Equal(expectedAges, births.Select(birth => birth.Age));
        Assert.Equal(recipients, births.Select(birth => birth.Owner));
        Assert.Equal(Enumerable.Range(0, recipients.Length).Select(index => packets.ResolveContact(header, index)), applied.Select(packet => packet.Occurrence!.Value));
        Assert.Equal(0u, graphics.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal(expanded ? 20 : 4, peakSparks);
        Assert.Equal(expanded ? 2 : 4, graphics.OriginalEffect.Pools.Length);
        Assert.Equal(new byte[] { 0x81 }, arena.Read(320 + 12, 1));

        void SetProperty(string name, object value) => typeof(BattleSession).GetProperty(name)!.SetValue(session, value);
        static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    private static NativeBattleFullCure280Resources Resources()
    {
        byte[] layout = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(layout.AsSpan(4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(layout.AsSpan(20), 32);
        byte[] sprite = new byte[80];
        BinaryPrimitives.WriteUInt32LittleEndian(sprite, 0x08010524);
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 8);
        for (int frame = 0; frame <= 8; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frame * 2), 64);
        }
        return new(new(0x123400, layout.ToImmutableArray()), new(0, 0x123800, 0, 0, sprite.ToImmutableArray()), new("fixture", []), 0x5578A);
    }
}
