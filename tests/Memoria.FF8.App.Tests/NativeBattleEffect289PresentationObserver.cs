using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect289Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect289PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<int> mainAges = [];
    private readonly HashSet<int> mainFrames = [];
    private readonly HashSet<int> programmeFrames = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> particleSources = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly List<object> observations = [];
    private bool sourceWords;
    private bool detached;
    private bool retainedParticle;
    private NativeCrtRandomState? initialRandom;
    private NativeCrtRandomState? lastRandom;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect289State state = current.NativeEffectMemory!.Effect289!;
        initialRandom ??= prior.NativeCrtRandom;
        lastRandom = current.NativeCrtRandom;
        Assert.Equal((byte)50, current.NativeGraphics.Models[4].ModelId4);
        if (effect.ReturnedDescriptor is > 0)
        {
            Assert.Null(state.RootBankStorage);
            Assert.Equal(0x1D972C0u + 4u * 156, state.ReadDword(CasterGlobal));
            Assert.Equal(ArenaAddress, state.ReadDword(ArenaGlobal));
            Assert.True(effect.Pools.Length is 1 or 4);
            foreach ((NativeBattleEffectTaskPool pool, int index) in effect.Pools.Select((pool, index) => (pool, index)))
            {
                NativeBattleCureFamilyPool expected = Pools[index];
                Assert.Equal(expected.Descriptor, pool.DescriptorAddress);
                Assert.Equal(expected.Bank, pool.BankAddress);
                Assert.Equal(expected.Stride, pool.Stride);
                Assert.Equal(expected.Capacity, pool.Slots.Length);
                foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => task.Callback != 0))
                {
                    callbacks.Add(task.Callback);
                    if (index == 1)
                    {
                        int age = unchecked((short)task.ReadWord(0xC));
                        mainAges.Add(age);
                        Assert.Equal(0x1D972C0u, task.ReadDword(0x10));
                        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(MainProgrammeWorkspaceOffset - 12, MainProgrammeWorkspaceLength).ToArray().ToImmutableArray());
                        Assert.Equal(MainProgrammeAddress, workspace.ResourceAddress);
                        Assert.Equal(Math.Clamp(age - 10, 0, 20), workspace.Frame);
                        mainFrames.Add(workspace.Frame);
                        if (age is 1 or 3 or 9 or 11 or 15 or 16 or 22 or 30)
                        {
                            Mark("effect289-main-" + age, current, cold);
                        }
                    }
                    if (index == 2 && (task.Flags & 1) != 0)
                    {
                        particleSources.Add(task.ReadDword(0xC));
                    }
                    if (index == 3)
                    {
                        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(ChildProgrammeWorkspaceOffset - 12, ChildProgrammeWorkspaceLength).ToArray().ToImmutableArray());
                        Assert.Equal(ChildProgrammeAddress, workspace.ResourceAddress);
                        programmeFrames.Add(workspace.Frame);
                    }
                }
            }
            if (effect.Pools.Length == 4 && !sourceWords)
            {
                _ = state.MeshWStorage.Read(0, MeshWStorageLength);
                NativeBattleEffect289Resources resources = effect.Binding.Effect289Resources!;
                foreach (uint address in MeshAddresses)
                {
                    NativeBattleStaticMeshResource original = resources.ReadMesh(address);
                    NativeBattleStaticMeshResource actual = resources.ReadMesh(state, address);
                    for (int vertex = 0; vertex < MeshVertexCount; vertex++)
                    {
                        short z = BinaryPrimitives.ReadInt16LittleEndian(original.Bytes.AsSpan().Slice(12 + vertex * 8, 2));
                        short w = BinaryPrimitives.ReadInt16LittleEndian(actual.Bytes.AsSpan().Slice(14 + vertex * 8, 2));
                        Assert.Equal(unchecked((short)-z), w);
                        Assert.InRange(w, (short)0, (short)4080);
                    }
                }
                sourceWords = true;
                Mark("effect289-current-source-w", current, cold);
            }
            uint cursor = state.ReadDword(CursorGlobal);
            if (cursor != 0)
            {
                int end = checked((int)(cursor - ArenaAddress));
                int bank = end >= SecondPacketOffset ? 1 : 0;
                int start = bank == 0 ? FirstPacketOffset : SecondPacketOffset;
                Assert.InRange(end, start, start + PacketBankBytes);
                if (banks.Add(bank))
                {
                    Mark("effect289-packet-bank-" + bank, current, cold);
                }
            }
            foreach (NativeBattleLogicalCommand command in EffectCommands(current))
            {
                opcodes.Add(command.Opcode);
            }
        }
        if (prior.NativeGraphics!.OriginalEffect?.ReturnedDescriptor is > 0 && effect.ReturnedDescriptor == 0)
        {
            Assert.NotNull(state.RootBankStorage);
            Assert.Empty(effect.Pools);
            NativeBattleEffectArena arena = current.NativeEffectMemory.Arena!;
            int particleActive = Enumerable.Range(0, ParticleCount).Count(index => (BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(ParticleOffset + index * ParticleStride, 2)) & 1) != 0);
            Assert.True(particleActive > 0);
            retainedParticle = true;
            Assert.Equal(20, BinaryPrimitives.ReadInt32LittleEndian(arena.Read(MainOffset + MainProgrammeWorkspaceOffset + 4, 4)));
            detached = true;
            Mark("effect289-root-detached-with-retained-particle", current, cold);
        }
        observations.Add(new { current.Frame, current.NativeGraphics.Callback, effect.ReturnedDescriptor, Cursor = state.ReadDword(CursorGlobal), Random = current.NativeCrtRandom, SourceWords = sourceWords });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(sourceWords);
        Assert.True(detached);
        Assert.True(retainedParticle);
        Assert.NotNull(initialRandom);
        Assert.NotEqual(initialRandom, lastRandom);
        Assert.Equal(new[] { 0, 1 }, banks.Order().ToArray());
        Assert.Contains(20, mainFrames);
        Assert.Contains(15, programmeFrames);
        Assert.Contains(30, mainAges);
        Assert.Equal(new[] { MainCallback, RootCallback, 0x6783F0u, 0x678490u }.Order().ToArray(), callbacks.Order().ToArray());
        Assert.Equal(SpriteAddresses.Order().ToArray(), particleSources.Order().ToArray());
        Assert.NotEmpty(opcodes);
        foreach (string marker in new[] { "effect289-current-source-w", "effect289-main-11", "effect289-main-16", "effect289-main-30", "effect289-root-detached-with-retained-particle" })
        {
            Assert.Contains(marker, markers);
        }
        File.WriteAllText(Path.Combine(output, "effect289-observations.json"), JsonSerializer.Serialize(new { MainAges = mainAges.Order(), MainFrames = mainFrames.Order(), ProgrammeFrames = programmeFrames.Order(), Callbacks = callbacks.Order(), ParticleSources = particleSources.Order(), PacketBanks = banks.Order(), Opcodes = opcodes.Order(), Markers = markers.Order(), SourceWords = sourceWords, Detached = detached, RetainedParticle = retainedParticle, InitialRandom = initialRandom, LastRandom = lastRandom, Observations = observations }));
    }

    public static NativeBattleLogicalCommand[] EffectCommands(BattleSnapshot current)
    {
        uint cursor = current.NativeEffectMemory!.Effect289!.ReadDword(CursorGlobal);
        if (cursor == 0)
        {
            return [];
        }
        int end = checked((int)(cursor - ArenaAddress));
        int start = end >= SecondPacketOffset ? SecondPacketOffset : FirstPacketOffset;
        Assert.InRange(end, start, start + PacketBankBytes);
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).Select(item => item.Command).ToArray();
    }

    private void Mark(string name, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, current);
        }
    }
}
