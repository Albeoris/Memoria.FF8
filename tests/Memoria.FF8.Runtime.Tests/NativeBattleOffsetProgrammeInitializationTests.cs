using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOffsetProgrammeInitializationTests
{
    [Theory]
    [InlineData((ushort)290)]
    [InlineData((ushort)308)]
    public void Default_initialization_preserves_arena_workspaces_and_opted_task_initialization_tracks_each_real_birth(ushort effectId)
    {
        const uint sourceAddress = 0x1000000;
        const uint arenaWorkspace = NativeBattleEffectNodeMemory.ArenaAddress + 0x1000;
        byte[] sourceBytes = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(sourceBytes.AsSpan(4), 16);
        NativeBattleGfAnimationSource source = new(sourceAddress, sourceBytes.ToImmutableArray());
        NativeBattleEffect290Resources resources290 = new(null!, null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty.Add(sourceAddress, source), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
        NativeBattleEffect308Resources resources308 = new(null!, null!, null!, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, ImmutableDictionary<int, NativeBattleGfAnimationSource>.Empty.Add(0, source), ImmutableDictionary<int, int>.Empty.Add(0, 32), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<byte, uint>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, 0, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleOriginalEffectState selected = new(effectId, effectId - 1, new(effectId, 1, 0), null, 0);
        void Initialize(NativeBattleEffectMemory currentMemory, NativeBattleOriginalEffectState currentEffect, uint address, bool? capture)
        {
            if (effectId == 290)
            {
                NativeBattleEffect290Frame frame = new(currentMemory, currentEffect, new(42), NativeBattleEffect290State.FromColdProcess());
                if (capture.HasValue)
                {
                    NativeBattleEffect290Programme.Initialize(frame, address, sourceAddress, 32, resources290, capture.Value);
                }
                else
                {
                    NativeBattleEffect290Programme.Initialize(frame, address, sourceAddress, 32, resources290);
                }
            }
            else
            {
                NativeBattleEffect308Frame frame = new(currentMemory, currentEffect, new(42), NativeBattleEffect308State.FromColdProcess());
                if (capture.HasValue)
                {
                    NativeBattleEffect308Programme.Initialize(frame, address, sourceAddress, 32, resources308, capture.Value);
                }
                else
                {
                    NativeBattleEffect308Programme.Initialize(frame, address, sourceAddress, 32, resources308);
                }
            }
        }
        Initialize(memory, selected, arenaWorkspace, null);
        byte[] defaultBytes = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(memory, [], arenaWorkspace, 32).Read(0, 32);
        Initialize(memory, selected, arenaWorkspace, false);
        Assert.Equal(defaultBytes, NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(memory, [], arenaWorkspace, 32).Read(0, 32));
        Assert.Equal(sourceAddress, BinaryPrimitives.ReadUInt32LittleEndian(defaultBytes));
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2000, 0x2100000, 256, 1);
        (pool, int slot) = pool.Append(1);
        selected = selected with { Pools = [pool] };
        uint workspace = pool.BankAddress + 24;
        Initialize(memory, selected, workspace, true);
        NativeBattlePilotVisualIdentity first = NativeBattleSelectedProgrammeVisuals.Workspace(effectId, pool.Slots[slot], workspace, sourceAddress, 0);
        byte[] firstBytes = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(memory, selected.Pools, workspace, 32).Read(0, 32);
        Initialize(memory, selected, workspace, true);
        NativeBattlePilotVisualIdentity second = NativeBattleSelectedProgrammeVisuals.Workspace(effectId, pool.Slots[slot], workspace, sourceAddress, 0);
        Assert.Equal(first.Generation, second.Generation);
        Assert.NotEqual(first.ResourceGeneration, second.ResourceGeneration);
        Assert.Equal(firstBytes, NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(memory, selected.Pools, workspace, 32).Read(0, 32));
    }
}
