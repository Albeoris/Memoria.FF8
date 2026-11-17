using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath113Tests
{
    [Fact]
    public void Zero_depth_sway_writes_only_the_rejection_word()
    {
        NativeBattleProjectedVertex prior = new(0x12345678, 0xABCD4321);
        NativeBattleProjectedVertex result = NativeBattleBadBreath113.Sway6CB7D0(new(0, 0, 0), 0, 0, 0, 0, new short[4096].ToImmutableArray(), prior);
        Assert.Equal(0x12345678u, result.Screen);
        Assert.Equal(0x00104321u, result.DepthAndClip);
    }

    [Fact]
    public void Sway_uses_camera_ir2_and_signed_word_screen_displacement()
    {
        short[] wave = new short[4096];
        wave[4095] = 4096;
        NativeBattleProjectedVertex result = NativeBattleBadBreath113.Sway6CB7D0(new(1024u | 800u << 16, 1000, 0x80020000), -1, 0, 1024, 3, wave.ToImmutableArray(), default);
        Assert.Equal(1536u | 800u << 16, result.Screen);
        Assert.Equal(1000u, result.DepthAndClip);
    }

    [Fact]
    public void Cone_direction_and_rise_height_preserve_original_integer_sign_rules()
    {
        Assert.Equal(new NativeBattleVector(0, 0, -4096), NativeBattleBadBreath113.Direction6CBB40(0, 0, 0));
        Assert.Equal(-1000, NativeBattleBadBreath113.RiseHeight(24000, -1000));
        Assert.Equal(-1, NativeBattleBadBreath113.RiseHeight(32767, -1));
        Assert.Equal(0, NativeBattleBadBreath113.RiseHeight(1, -1));
    }

    [Fact]
    public void Expanded_roles_keep_the_authored_hole_and_deliver_grouped_occurrences_once()
    {
        BattleModelOwner caster = OriginalOwner(3);
        BattleModelOwner roleZero = OriginalOwner(0);
        BattleModelOwner roleTwo = OriginalOwner(2);
        BattleModelOwner otherEnemy = OriginalOwner(4);
        BattleModelOwner addedEnemy = new BattleEnemyActorModel(new("added-enemy"));
        BattleModelOwner[] extras = [
            new BattlePartyActorModel(new("added-a")),
            new BattlePartyActorModel(new("added-b")),
            new BattlePartyActorModel(new("added-c"))
        ];
        BattleModelRoster roster = new(new[] { roleZero, roleTwo, caster, otherEnemy }.Concat(extras).Append(addedEnemy));
        NativeBattleModelAliases aliases = new(roster, new[] { 0, 2, 3, 4 }.Select(index => (NativeBattleModelSlot.Decode((byte)index), OriginalOwner(index))));
        var packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(caster, 8, 4, 0, 0, 113, null));
        BattleModelOwner[] recipients = [roleZero, extras[0], extras[0], roleTwo, extras[1], extras[2], otherEnemy, addedEnemy];
        foreach (BattleModelOwner recipient in recipients)
        {
            (packets, _) = packets.AppendContact(new(new(recipient, 1, 0, 0, 0, 7, 0), null));
        }
        (packets, _) = packets.Publish(recipients.Length);
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleGraphicsModel model = new(0)
        {
            Rotation14 = 0,
            Transform = new(new(0, 0, 0), new(100, 0, 500), 0, 0, 4096),
            GeometryWorldMatrix = view
        };
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => model);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25218C8, 0x2521880, 20, 1);
        (root, _) = root.Append(0x6CACD0);
        root.Slots[0].WriteDword(12, 0).WriteDword(16, 0);
        var effect = new NativeBattleOriginalEffectState(113, 112, new(113, 0x6CAC20, 0x6CAC30) { BadBreath113Resources = Resources() }, new(caster, 0, BattleModelTargets.From(recipients), header, 0, 0), root.DescriptorAddress)
        {
            Pools = [root]
        };
        graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        BattleDefinition definition = new() { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] };
        var session = new BattleSession(definition, BattleAtbMode.Normal);
        ImmutableArray<sbyte> saved = Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray();
        ImmutableArray<sbyte> battle = Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray();
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, saved, battle) { SummonColors = NativeBattleGteColorInterpolation.CodeConstants45F270 };
        NativeBattleHostedTestFixture.BindProcess(session, graphics, new(rules, [], []));
        Field("presentationForeignActors").SetValue(session.Presentation!, new Dictionary<BattleModelOwner, NativeBattleForeignActor>());
        NativeBattleGraphicsResource body = new([0, 0, 0, 0]);
        Field("presentationNativeTargets").SetValue(session.Presentation!, roster.Owners.ToDictionary(owner => owner, owner => new NativeBattleEffectModelBinding(owner, model, body)));
        Field("nativeActionPackets").SetValue(session.Presentation!, packets);
        Field("nativeCrtRandom").SetValue(session.Presentation!, new NativeCrtRandomState(17));
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeEffectMemory))!.SetValue(session.Presentation!, NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() });
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeCamera))!.SetValue(session.Presentation!, NativeBattleCameraState.FromColdProcess() with { Projection = new(view, 100, 160, 108) });
        var applied = (List<NativeBattleContactPacket>)Field("presentationContacts").GetValue(session.Presentation!)!;
        MethodInfo advance = typeof(BattlePresentationProcess).GetMethod("AdvanceNativeBadBreath113", BindingFlags.Instance | BindingFlags.NonPublic)!;
        List<(int Age, BattleModelOwner Owner)> births = [];
        int peak = 0;
        for (int callback = 0; callback < 100 && graphics.OriginalEffect!.ReturnedDescriptor != 0; callback++)
        {
            graphics = Assert.IsType<NativeBattleGraphicsState>(advance.Invoke(session.Presentation!, [graphics]));
            if (graphics.OriginalEffect!.Pools.Length == 4)
            {
                NativeBattleEffectTaskPool particles = graphics.OriginalEffect.Pools[2];
                NativeBattleEffectTaskPool targets = graphics.OriginalEffect.Pools[3];
                Assert.True(NativeBattlePresentationTaskStorage.IsHostedBank(particles.BankAddress, 0x2C, 117));
                Assert.True(NativeBattlePresentationTaskStorage.IsHostedBank(targets.BankAddress, 0xFC, 5));
                Assert.Equal(117, particles.Slots.Length);
                Assert.Equal(5, targets.Slots.Length);
                foreach (NativeBattleModulePoolTask born in targets.Slots.Where(task => (task.Flags & 1) != 0 && task.ReadWord(12) == 1))
                {
                    births.Add((callback - 1, born.ReadExecutionReference<BattleExecutionReference.Model>(14, 2).Owner));
                }
                peak = Math.Max(peak, particles.Slots.Count(task => (task.Flags & 1) != 0));
            }
        }
        Assert.Equal(new[] { 10, 26, 26, 26, 26 }, births.Select(birth => birth.Age));
        Assert.Equal(new[] { roleZero, roleTwo }.Concat(extras), births.Select(birth => birth.Owner));
        Assert.Equal(new[] { 0, 3, 1, 2, 4, 5 }.Select(index => packets.ResolveContact(header, index)), applied.Select(packet => packet.Occurrence!.Value));
        Assert.True(peak > 80);
        Assert.Equal(0u, graphics.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal(new uint[] { 0x25218C8, 0x25218B8 }, graphics.OriginalEffect.Pools.Select(pool => pool.DescriptorAddress));
        Assert.All(graphics.ActorModels.Entries, entry => Assert.Equal(0, entry.State.Flags0 & 4));

        static FieldInfo Field(string name) => typeof(BattlePresentationProcess).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    private static NativeBattleBadBreath113Resources Resources()
    {
        byte[] cloud = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(cloud.AsSpan(4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(cloud.AsSpan(20), 32);
        byte[] sprite = new byte[80];
        BinaryPrimitives.WriteUInt32LittleEndian(sprite, 0x00010524);
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 20);
        for (int frame = 0; frame <= 20; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frame * 2), 64);
        }
        return new(new(0x123400, cloud.ToImmutableArray()), [], [Sprite(0x12C118C), Sprite(0x12C1418), Sprite(0x12C1014)], new("fixture", []), 0x5578A, []);

        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, sprite.ToImmutableArray());
    }
}
