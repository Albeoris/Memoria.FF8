using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeRenzokukenEntryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData((ushort)141, false, 5)]
    [InlineData((ushort)160, true, 4)]
    public void Original_ordinary_Renzokuken_entry_owns_native_control_and_source_replacement_across_cold_restore(ushort variant, bool finisherPending, int contacts)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root), "Select the original FFVIII installation.");
        using GameAssets game = new(root!);
        using BattleAssets battle = new(root!);
        NativeBattleGraphicsResource original = battle.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = 0 });
        NativeGlobalRandomState global = game.ReadNewGameGlobalRandom();
        NativeBattleGraphicsModel caster = NativeBattleGraphicsModels.CompleteLoad(new(0, 0)
        {
            Transform = new(new(0, 0, 2200), new(0, 0, 2200), 0, 0, 4096),
            LinkedTransform = new(default, default),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        }, 0, 0, ref global);
        caster = NativeBattleGraphicsModels.SelectSequence(caster, 1);
        caster = NativeBattleSequenceMachine.Advance(caster, original, new(0, 0)).Model;
        caster = caster with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(caster, original.PoseSource!) };
        NativeBattleRenzokuken160WeaponTip standingTip = NativeBattleRenzokuken160Controller.ReadWeaponTip5FFEE0(caster, original);
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        CrossoverNativeRenzokukenEntryState state = CrossoverNativeRenzokukenEntry.Begin(game, 4, variant, finisherPending, random);
        Assert.Equal(variant, state.Frame.Effect.Binding.Effect);
        Assert.Equal(4, state.HostTarget);
        Assert.Equal((byte)3, state.Packets.ReadContact(0).Primary.ActorIndex);
        Assert.Equal((byte)5, state.Packets.Headers[0].ReadByte(1));
        Assert.Equal(contacts, state.Resources.ActorResource.Timeline.InputThresholds.Length);
        NativeBattleRenzokuken160State native = variant == 141 ? NativeBattleRenzokukenVariantController.ReadState(state.Frame).Core : state.Frame.Memory.Renzokuken160!;
        Assert.Equal(finisherPending, native.InitialControl);
        Assert.Equal(3, native.FirstTarget);
        Assert.Equal(random, state.Frame.Random);
        CrossoverNativeRenzokukenPrelude first = CrossoverNativeRenzokukenEntry.AdvancePrelude(state, caster);
        NativeBattleRenzokuken160ActorFlags flags = Assert.Single(first.Operations.OfType<NativeBattleRenzokuken160ActorFlags>());
        Assert.Equal((ushort)(caster.Flags0 | 0x800), flags.Flags);
        Assert.Empty(first.Operations.OfType<NativeBattleRenzokuken160ActorResource>());
        caster = caster with { Flags0 = flags.Flags };
        string saved = JsonSerializer.Serialize(CrossoverNativeRenzokukenEntry.Capture(first.State));
        CrossoverNativeRenzokukenEntryState warm = CrossoverNativeRenzokukenEntry.Restore(CrossoverNativeRenzokukenEntry.Capture(first.State));
        CrossoverNativeRenzokukenEntryState cold = CrossoverNativeRenzokukenEntry.Restore(JsonSerializer.Deserialize<CrossoverNativeRenzokukenEntryState>(saved)!);
        CrossoverNativeRenzokukenPrelude second = CrossoverNativeRenzokukenEntry.AdvancePrelude(warm, caster);
        CrossoverNativeRenzokukenPrelude restored = CrossoverNativeRenzokukenEntry.AdvancePrelude(cold, caster);
        Assert.Equal(JsonSerializer.Serialize(CrossoverNativeRenzokukenEntry.Capture(second.State)), JsonSerializer.Serialize(CrossoverNativeRenzokukenEntry.Capture(restored.State)));
        NativeBattleRenzokuken160ActorResource swap = Assert.Single(restored.Operations.OfType<NativeBattleRenzokuken160ActorResource>());
        Assert.Equal(finisherPending ? (byte)2 : (byte)1, swap.Sequence);
        (NativeBattleGraphicsModel replaced, NativeBattleGraphicsResource replacement) = CrossoverNativeRenzokukenEntry.ReplaceSource(caster, original, swap);
        Assert.Equal(swap.Sequence, replaced.Sequence);
        Assert.Equal(variant, replaced.ResourceMagicId);
        Assert.Equal(standingTip, NativeBattleRenzokuken160Controller.ReadWeaponTip5FFEE0(replaced, replacement));
        Assert.True(replacement.Contains(swap.Sequence));
        Assert.NotNull(replacement.LinkedPoseSource);
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root), "Select the original FFVII installation for the foreign approach.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session host = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7BattleSession arena = new(formation, host.Progress, kernel, executable.NativeMateriaStatModifiers, 1, animations: animations);
        Ff7BattlePlacement source = arena.VisualPlacement(0);
        Ff7BattleActorState enemy = arena.Actors.Single(actor => actor.Actor.Slot == restored.State.HostTarget);
        ushort radius = animations.CollisionRadius(Ff7BattleAnimationResources.ModelPrefix(enemy.Identity, true));
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw);
        (_, short yaw, _, int distance) = placement.ResolveContact(arena.VisualPlacement(restored.State.HostTarget), radius);
        replaced = replaced with { Rotation14 = yaw };
        NativeBattleEffectMemory globals = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleSequenceCue? ab = null;
        for (int callback = 0; callback < 6 && ab is null; callback++)
        {
            NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(replaced, replacement, new(0, 0)
            {
                OriginalResource = original,
                ReadExternal = variable => variable switch
                {
                    17 => unchecked((short)distance),
                    18 or 19 => globals.ReadSequenceGlobal5044B0(variable),
                    _ => throw new NotSupportedException($"Unowned original sequence read {variable:X2}.")
                },
                WriteExternal = (variable, value) => globals = globals.WriteSequenceGlobal5048E0(variable, value)
            });
            replaced = step.Model;
            ab = step.Cues.SingleOrDefault(cue => cue.Kind == NativeBattleSequenceCueKind.TriggerWindow);
        }
        Assert.NotNull(ab);
        Assert.True(restored.State.Resources.ActorResource.Segments.SequenceEqual(ab.TriggerWindow!.Segments));
        Assert.Equal(128, Assert.Single(restored.Operations.OfType<NativeBattleRenzokuken160Stage>()).Intensity);
        NativeBattleRenzokuken160Sound sound = Assert.Single(restored.Operations.OfType<NativeBattleRenzokuken160Sound>());
        Assert.Equal((state.Resources.AppearanceSound, 0x8000, 128), (sound.Descriptor, sound.First, sound.Second));
        output.WriteLine($"Original Renzokuken{variant}: host4/alias3, native descriptor{state.Frame.Effect.ReturnedDescriptor:X}, {contacts} authored windows, replacement sequence{swap.Sequence}, original appearance sound and retained weapon tip {standingTip.Position} across cold prelude.");
    }
}
