using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRinoa89ProgramsTests
{
    [Theory]
    [InlineData(0x14A09A0u, 112, 4)]
    [InlineData(0x14A6370u, 104, 20)]
    [InlineData(0x14A6600u, 536, 47)]
    [InlineData(0x14A9FF0u, 236, 189)]
    public void Original_body_programmes_execute_only_their_reached_windows_and_restore_next_draw(uint address, int clear, int calls)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRinoa89Resources resources = game.ReadNativeRinoa89Resources();
        NativeBattleGfAnimationSource source = resources.Program(address);
        NativeBattleEffectMemory memory = new(NativeBattleEffectArena.Clear(), 0, 0, ImmutableArray.CreateRange(new NativeBattleEffectHeapAllocation?[256]), 0, 0, 0, 0);
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown().Write(NativeBattleRinoa89Resources.StaticAddress, resources.StaticImage.Data.AsSpan()));
        NativeBattleRinoa89Frame frame = new(memory, null!, staging, new(1));
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleRinoa89ProgramContext context = new(view, 0x20DFAB8 + 100492, 0);
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xA5, 772).ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, clear);
        int nodes = 0;
        for (int call = 0; call < calls; call++)
        {
            NativeBattleRinoa89Frame? cloned = call == calls / 2 ? JsonSerializer.Deserialize<NativeBattleRinoa89Frame>(JsonSerializer.Serialize(frame)) : null;
            NativeBattleGfAnimationWorkspace? clonedWorkspace = cloned is not null ? JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(workspace)) : null;
            List<NativeBattleGfGeometryBatch> actual = [];
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) =>
            {
                NativeBattleRinoa89ProgramDraw draw = NativeBattleRinoa89ProgramDrawing.Draw6FBB80(frame, source, node, context, projection, colors);
                frame = draw.Frame;
                actual.Add(draw.Packets);
                nodes++;
                return current;
            });
            if (cloned is not null && clonedWorkspace is not null)
            {
                NativeBattleRinoa89Frame restored = cloned;
                List<NativeBattleGfGeometryBatch> expected = [];
                NativeBattleGfAnimationAdvance cloneNext = clonedWorkspace.Advance701970(source, 0, (current, node) =>
                {
                    NativeBattleRinoa89ProgramDraw draw = NativeBattleRinoa89ProgramDrawing.Draw6FBB80(restored, source, node, context, projection, colors);
                    restored = draw.Frame;
                    expected.Add(draw.Packets);
                    return current;
                });
                Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(cloneNext));
                Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(restored));
                Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(expected));
            }
            workspace = next.Workspace;
        }
        Assert.True(nodes > 0);
    }
}
