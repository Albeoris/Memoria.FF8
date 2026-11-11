using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverNativeCureProgramTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_Cure_self_target_uses_native_actor_redraw_contact_and_arena_packets_across_cold_restore()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? hostRoot = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(hostRoot), "Select both original game installations.");
        using GameAssets game = new(root!);
        using BattleAssets assets = new(root!);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(game.ReadNewGameCharacterVitals(), assets, 0);
        BattleNativeAction cure = Assert.Single(kit.Spells, spell => spell.ActionId == 21);
        Assert.Equal((1, 3, 18, 20), ((int)cure.MagicId, (int)cure.AttackType, (int)cure.Power, (int)cure.TargetFlags!));
        BattleModel body = assets.LoadPlayer(0);
        BattleModel weapon = assets.ResolveWeaponModel(0, kit.Weapon, body)!;
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = kit.Weapon });
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(0, 0)
        {
            Transform = new(new(0, 0, 2200), new(0, 0, 2200), 0, 0, 4096),
            LinkedTransform = new(default, default),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        }, 0, 0, ref random);
        model = NativeBattleSequenceMachine.Advance(NativeBattleGraphicsModels.SelectSequence(model, 1), resource, new(0, 0)).Model;
        model = model with { Flags2 = (ushort)(model.Flags2!.Value & ~4) };
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
        CrossoverNativeCureProgram program = new(game, resource);
        CrossoverNativeCureState state = program.Begin(model, new(0, 0, 0, 1, 0, 100, 0), NativeCrtRandomState.FromColdProcess());
        Ff7Installation installation = Ff7Installation.Discover(hostRoot!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer arena = new(archive, formation.LocationId);
        CrossoverGunbladeHostPlacement placement = new(new(0, 0, 1700), 0);
        NativeBattleCameraProjection camera = CrossoverNativeSpriteRenderer.Camera(scene.Cameras[0], placement.Matrix);
        CrossoverNativeSpriteRenderer compositor = new(assets.ReadNativePrimitiveTables(), game.ReadNativeEffectMeshes(), game.ReadNativeBattleGraphicsRules().SummonColors!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        arena.Render(gpu, 320, 240, new(scene.Cameras[0], []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        int contacts = 0;
        int contactCallback = 0;
        int sounds = 0;
        int triangles = 0;
        int changed = 0;
        bool hiddenCold = false;
        bool contactCold = false;
        bool actorViewportChecked = false;
        for (int callback = 1; callback <= 300; callback++)
        {
            CrossoverNativeCureStep next = program.Advance(state, camera);
            state = next.State;
            if (next.Contact)
            {
                contacts++;
                contactCallback = callback;
            }
            sounds += next.Sounds.Length;
            if (!hiddenCold && (state.Model.Flags0 & 4) != 0 || !contactCold && state.ContactPublished)
            {
                string snapshot = JsonSerializer.Serialize(CrossoverNativeCureProgram.Capture(state));
                CrossoverNativeCureState isolated = CrossoverNativeCureProgram.Restore(CrossoverNativeCureProgram.Capture(state));
                CrossoverNativeCureState restored = CrossoverNativeCureProgram.Restore(JsonSerializer.Deserialize<CrossoverNativeCureState>(snapshot)!);
                Assert.Equal(JsonSerializer.Serialize(CrossoverNativeCureProgram.Capture(program.Advance(isolated, camera).State)), JsonSerializer.Serialize(CrossoverNativeCureProgram.Capture(program.Advance(restored, camera).State)));
                hiddenCold |= (state.Model.Flags0 & 4) != 0;
                contactCold |= state.ContactPublished;
                state = CrossoverNativeCureProgram.Restore(JsonSerializer.Deserialize<CrossoverNativeCureState>(snapshot)!);
            }
            if (!actorViewportChecked && (state.Model.Flags0 & 4) != 0 && state.Ordering.Packets.Any(packet => packet.Reference.Bank != "20DFAB8" && (byte)(packet.Words[1] >> 24) is 0x24 or 0x26 or 0x2C or 0x2E))
            {
                actorViewportChecked = CrossoverEffectViewportCapture.CheckProjection((renderer, width, height) => compositor.DrawOrdering(renderer, state.Ordering, state.Frame.Memory.Textures!, width, height, CrossoverGunbladeHostPlacement.OriginToHostScale, ReadPhysical));
            }
            CrossoverFf8BattleActorVisual visual = new(body, weapon, state.Model, placement.Matrix) { BodyTextures = state.Model.BodyTextureSlots, Textures = state.Frame.Memory.Textures };
            arena.Render(new OverlayRenderer(gpu, () => triangles += compositor.DrawOrdering(gpu, state.Ordering, state.Frame.Memory.Textures!, 320, 240, CrossoverGunbladeHostPlacement.OriginToHostScale, ReadPhysical)), 320, 240, new(scene.Cameras[0], []) { Guest = visual });
            Color[] frame = GpuTestDevice.Read(gpu.Target);
            changed = Math.Max(changed, baseline.Zip(frame).Count(pair => pair.First != pair.Second));
            if (state.Complete)
            {
                Assert.True(hiddenCold && contactCold && actorViewportChecked && triangles > 0 && changed > 8);
                Assert.Equal(1, contacts);
                Assert.Equal(19, contactCallback);
                Assert.Equal(51, callback);
                Assert.Equal(0, state.Model.Flags0 & 4);
                Assert.True(sounds > 0);
                Assert.Same(state, program.Advance(state, camera).State);
                output.WriteLine($"Original Cure contact {contactCallback}, release {callback}, native actor/effect triangles {triangles}, changed GPU pixels {changed}, sounds {sounds}.");
                return;
            }
        }
        throw new InvalidOperationException("The original Cure component did not release its native pools.");

        byte[] ReadPhysical(NativeBattlePrimitiveReference reference, int count) => reference.Bank == "20DFAB8" ? state.Frame.Memory.Arena!.Read(reference.ByteOffset, count) : (state.Frame.Memory.PrimitiveProcess! with { PacketBank = reference.Bank, PacketBuffer = reference.Buffer }).ReadCommonPacket(reference.ByteOffset, count);
    }

    [Fact]
    public void Cure_foreign_triangle_overlay_keeps_native_eighth_pixel_texture_window()
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 8192, 4096);
        NativeBattleGeometryPrimitive overlay = Assert.IsType<NativeBattleGeometryPrimitive>(NativeBattleCureActorPackets.ProjectTriangleOverlay8D7430(new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), matrix, matrix, new(matrix, 512, 20, 15), 0, 17));
        Assert.Equal(528, overlay.Bucket);
        Assert.Equal(0x36808080u, overlay.Words[1]);
        Assert.Equal(0x3C54C040u, overlay.Words[3]);
        Assert.Equal(0x00B8C072u, overlay.Words[6]);
        Assert.Equal(0xF240u, overlay.Words[9]);
        Assert.Null(NativeBattleCureActorPackets.ProjectTriangleOverlay8D7430(new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), matrix, matrix, new(matrix, 512, 160, 120), 0, 17));
    }

    private sealed class OverlayRenderer(ITriangleRenderer inner, Action overlay) : ITriangleRenderer
    {
        public void Begin(int width, int height, System.Numerics.Vector4 clearColor, float clearDepth = 1) => inner.Begin(width, height, clearColor, clearDepth);
        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material) => inner.Draw(a, b, c, material);
        public void End()
        {
            overlay();
            inner.End();
        }
    }
}
