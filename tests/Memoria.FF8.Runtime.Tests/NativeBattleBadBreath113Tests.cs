using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath113Tests
{
    [Fact]
    public void Intrinsic_hand_overlay_preserves_its_material_and_existing_sway_packets()
    {
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 224 << 6);
        NativeBattleRawMesh mesh = new([new(new(0, 0, 0), 0), new(new(64, 0, 0), 0), new(new(0, 64, 0), 0)], [new(false, face.ToImmutableArray())]);
        byte?[] rows = new byte?[16];
        rows[0] = 0;
        NativeBattleCureActorGeometry body = new(mesh, new(rows.ToImmutableArray()));
        NativeBattleModelTextureOwner material = new("recipient/intrinsic-hands/revision");
        NativeBattleCureActorGeometry hand = body with { Layer = NativeBattleActorGeometryLayer.ActorSurface, MaterialOwner = material };
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1024, 4096);
        NativeBattleCameraProjection projection = new(matrix, 216, 0, 0);
        var wave = new short[4096].ToImmutableArray();
        byte[] bodyScratch = new byte[24];
        byte[] handScratch = new byte[24];
        NativeBattleGfGeometryBatch original = NativeBattleBadBreath113ActorDrawing.Emit(body, [matrix], projection, 0x808080, 0, 0, 0, wave, bodyScratch);
        NativeBattleGfGeometryBatch intrinsic = NativeBattleBadBreath113ActorDrawing.Emit(hand, [matrix], projection, 0x808080, 0, 0, 0, wave, handScratch);
        NativeBattleGeometryPrimitive originalPacket = Assert.Single(original.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!;
        NativeBattleGeometryPrimitive handPacket = Assert.Single(intrinsic.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!;
        Assert.Equal(originalPacket.Words.ToArray(), handPacket.Words.ToArray());
        Assert.Equal(bodyScratch, handScratch);
        Assert.Equal((ushort)256, BinaryPrimitives.ReadUInt16LittleEndian(bodyScratch.AsSpan(4)));
        Assert.Equal(new[] { 256, 256, 256, 0 }, originalPacket.VertexPayload4);
        Assert.Equal(80, originalPacket.Bucket);
        Assert.Null(originalPacket.ModelTextureOwner);
        Assert.Equal(material, handPacket.ModelTextureOwner);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Zero_stored_depth_sway_writes_only_the_rejection_word(ushort depth)
    {
        NativeBattleProjectedVertex prior = new(0x12345678, 0xABCD4321);
        NativeBattleProjectedVertex result = NativeBattleBadBreath113.Sway6CB7D0(new(0, depth, 0), 0, 0, 0, 0, new short[4096].ToImmutableArray(), prior);
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
        Assert.Equal(250u, result.DepthAndClip);
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
        NativeBattleGraphicsResource body = ContactModelResource();
        NativeGlobalRandomState modelRandom = new(1);
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner =>
        {
            NativeBattleGraphicsModel cold = NativeBattleGraphicsModels.ClearUnloadedModel(0, Enumerable.Repeat<byte?>(0, 32).ToImmutableArray(), view, NativeBattleModelRetainedFields.FromColdProcess(), NativeBattleSequenceState.ColdProcessLocals());
            NativeBattleGraphicsModel loaded = NativeBattleGraphicsModels.CompleteLoad(cold, owner.Side == BattleSide.Allies ? (byte)1 : (byte)61, 0, owner.Side == BattleSide.Allies, ref modelRandom, 0);
            loaded = loaded with
            {
                Transform = new(new(0, 0, 0), new(100, 0, 500), 0, 0, 4096),
                GeometryWorldMatrix = view,
                Sequence = 1,
                SequenceState = new NativeBattleSequenceState().InitializeControl5027D0(1, 0) with { Pose = 0, PoseFrame114 = 1, PoseLimit115 = 2, DecodedPose = body.PoseSource!.Sample(0, 1, 0) }
            };
            return NativeBattleBodyBoneMatrices.Publish508C90(loaded, body.PoseSource!);
        });
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25218C8, 0x2521880, 20, 1);
        (root, _) = root.Append(0x6CACD0);
        root.Slots[0].WriteDword(12, 0).WriteDword(16, 0);
        var effect = new NativeBattleOriginalEffectState(113, 112, new(113, 0x6CAC20, 0x6CAC30) { BadBreath113Resources = Resources() }, new(caster, 0, BattleModelTargets.From(recipients), header, 0, 0), root.DescriptorAddress)
        {
            Pools = [root]
        };
        graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        BattleDefinition definition = new()
        {
            Combatants = roster.Owners.Select(owner => new BattleCombatantDefinition
            {
                Id = owner switch { BattlePartyActorModel party => party.Actor.ActorId, BattleEnemyActorModel enemy => enemy.Actor.ActorId, _ => throw new InvalidOperationException() },
                Name = "Actor",
                IsPlayer = owner.Side == BattleSide.Allies,
                ModelId = owner.Side == BattleSide.Allies ? 1 : 61
            }).ToArray()
        };
        var session = new BattleSession(definition, BattleAtbMode.Normal);
        ImmutableArray<sbyte> saved = Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray();
        ImmutableArray<sbyte> battle = Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray();
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, saved, battle) { SummonColors = NativeBattleGteColorInterpolation.CodeConstants45F270 };
        ImmutableArray<NativeBattleGraphicsBinding> bindings = definition.Combatants.Select(actor => new NativeBattleGraphicsBinding(actor.Id, checked((byte)actor.ModelId), body)).ToImmutableArray();
        NativeBattleHostedTestFixture.BindProcess(session, graphics, new(rules, bindings, []));
        Field("presentationForeignActors").SetValue(session.Presentation!, new Dictionary<BattleModelOwner, NativeBattleForeignActor>());
        Field("presentationNativeTargets").SetValue(session.Presentation!, roster.Owners.ToDictionary(owner => owner, owner => new NativeBattleEffectModelBinding(owner, graphics.ActorModels.Get(owner), body)));
        Field("nativeActionPackets").SetValue(session.Presentation!, packets);
        Field("nativeCrtRandom").SetValue(session.Presentation!, new NativeCrtRandomState(17));
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeEffectMemory))!.SetValue(session.Presentation!, NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() });
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeCamera))!.SetValue(session.Presentation!, NativeBattleCameraState.FromColdProcess() with { Projection = new(view, 100, 160, 108) });
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGlobalSoundProcess))!.SetValue(session.Presentation!, NativeGlobalSoundProcess.FromBufferlessColdProcess());
        var applied = (List<NativeBattleContactPacket>)Field("presentationContacts").GetValue(session.Presentation!)!;
        MethodInfo advance = typeof(BattlePresentationProcess).GetMethod("AdvanceNativeBadBreath113", BindingFlags.Instance | BindingFlags.NonPublic)!;
        List<(int Age, BattleModelOwner Owner)> births = [];
        int peak = 0;
        int committedSounds = 0;
        for (int callback = 0; callback < 100 && graphics.OriginalEffect!.ReturnedDescriptor != 0; callback++)
        {
            NativeBattleModelLifecycleServices frameServices = NativeBattleHostedTestFixture.ReadModelServices(session.Presentation!, roster);
            Field("hostedFrameServices").SetValue(session.Presentation!, frameServices);
            typeof(BattlePresentationProcess).GetMethod("BeginNativeFrameStack", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session.Presentation!, []);
            try
            {
                graphics = Assert.IsType<NativeBattleGraphicsState>(advance.Invoke(session.Presentation!, [graphics]));
                NativeBattleHostedTestFixture.SetProperty(session.Presentation!, nameof(BattlePresentationProcess.NativeGraphics), graphics);
                committedSounds += frameServices.ReadOrderedReceipts().Count(receipt => receipt is NativeBattlePresentationSoundCommitted);
            }
            finally
            {
                typeof(BattlePresentationProcess).GetMethod("EndNativeFrameStack", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session.Presentation!, []);
                Field("hostedFrameServices").SetValue(session.Presentation!, null);
            }
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
        Assert.True(committedSounds > 0);
        Assert.Equal(0u, graphics.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal(new uint[] { 0x25218C8, 0x25218B8 }, graphics.OriginalEffect.Pools.Select(pool => pool.DescriptorAddress));
        Assert.All(graphics.ActorModels.Entries, entry => Assert.Equal(0, entry.State.Flags0 & 4));

        static FieldInfo Field(string name) => typeof(BattlePresentationProcess).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    private static NativeBattleGraphicsResource ContactModelResource()
    {
        byte[] skeleton = new byte[16 + 48 * 32];
        skeleton[0] = 32;
        skeleton[4] = 9;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 256);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattleDecodedPose pose = new(default, Enumerable.Repeat(new NativeBattleBonePose(default, new(1024, 1024, 1024)), 32).ToImmutableArray(), false);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), NativeBattleAnimationEncoder.Encode([ImmutableArray.Create(pose, pose)]));
        return new([5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 0, 0xA0, 0, 0xA1]) { PoseSource = source, AnimationFrames = [2], Geometry = [], LinkedGeometry = [] };
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
