using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect283Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect283PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<int> mainAges = [];
    private readonly HashSet<int> mainFrames = [];
    private readonly HashSet<int> secondaryFrames = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly List<object> observations = [];
    private bool bodyCommon;
    private bool window;
    private bool fade;
    private bool restored;
    private bool detached;
    private uint? savedColor;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect283State state = current.NativeEffectMemory!.Effect283!;
        NativeBattleGraphicsModel target = current.NativeGraphics.Models[4];
        if (effect.ReturnedDescriptor is > 0)
        {
            Assert.Null(state.RootBankStorage);
            Assert.Equal(0x1D972C0u + 4u * 156, state.ReadDword(CasterGlobal));
            Assert.Equal(ArenaAddress, state.ReadDword(ArenaGlobal));
            for (int index = 0; index < effect.Pools.Length; index++)
            {
                NativeBattleEffectTaskPool pool = effect.Pools[index];
                NativeBattleCureFamilyPool expected = Pools[index];
                Assert.Equal(expected.Descriptor, pool.DescriptorAddress);
                Assert.Equal(expected.Bank, pool.BankAddress);
                Assert.Equal(expected.Stride, pool.Stride);
                Assert.Equal(expected.Capacity, pool.Slots.Length);
                foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0))
                {
                    callbacks.Add(task.Callback);
                    if (index != 1)
                    {
                        continue;
                    }
                    int age = unchecked((short)task.ReadWord(0xC));
                    mainAges.Add(age);
                    Assert.Equal(0x1D972C0u + 4u * 156, task.ReadDword(0x28));
                    savedColor ??= task.ReadDword(0x14);
                    Assert.Equal(savedColor.Value, task.ReadDword(0x14));
                    NativeBattleGfAnimationWorkspace main = new(task.Payload.AsSpan().Slice(MainProgrammeWorkspaceOffset - 12, MainProgrammeWorkspaceLength).ToArray().ToImmutableArray());
                    NativeBattleGfAnimationWorkspace secondary = new(task.Payload.AsSpan().Slice(SecondaryProgrammeWorkspaceOffset - 12, SecondaryProgrammeWorkspaceLength).ToArray().ToImmutableArray());
                    Assert.Equal(MainProgrammeAddress, main.ResourceAddress);
                    Assert.Equal(SecondaryProgrammeAddress, secondary.ResourceAddress);
                    Assert.Equal(age, main.Frame);
                    Assert.Equal(Math.Max(0, age - 24), secondary.Frame);
                    mainFrames.Add(main.Frame);
                    secondaryFrames.Add(secondary.Frame);
                    if (age is >= 25 and <= 44)
                    {
                        fade |= (target.Color44!.Value & 0xFF000000) == 0x32000000;
                        bodyCommon |= !NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current).IsEmpty;
                    }
                    if (age == 45)
                    {
                        Assert.Equal(savedColor.Value, target.Color44);
                        Assert.Equal(0, target.Flags0 & 4);
                        restored = true;
                    }
                    if (age is 1 or 25 or 44 or 45 or 53 or 59)
                    {
                        Mark("effect283-main-" + age, current, cold);
                    }
                    observations.Add(new { current.Frame, current.NativeGraphics.Callback, Age = age, MainFrame = main.Frame, SecondaryFrame = secondary.Frame, Selector = unchecked((short)task.ReadWord(0xE)), TaskCallback = task.Callback, target.Flags0, target.Color44 });
                }
            }
            int cursor = checked((int)(state.ReadDword(CursorGlobal) - ArenaAddress));
            int bank = cursor >= SecondPacketOffset ? 1 : 0;
            int start = bank == 0 ? FirstPacketOffset : SecondPacketOffset;
            Assert.InRange(cursor, start, start + PacketBankBytes);
            banks.Add(bank);
            foreach (NativeBattleLogicalCommand command in EffectCommands(current))
            {
                opcodes.Add(command.Opcode);
                window |= command.Opcode == 0xE2;
            }
        }
        if (prior.NativeGraphics!.OriginalEffect?.ReturnedDescriptor is > 0 && effect.ReturnedDescriptor == 0)
        {
            Assert.NotNull(state.RootBankStorage);
            Assert.Empty(effect.Pools);
            Assert.Equal((ushort)60, BinaryPrimitives.ReadUInt16LittleEndian(state.RootBankStorage.Read(0xC, 2)));
            NativeBattleEffectArena arena = current.NativeEffectMemory.Arena!;
            Assert.Equal((ushort)60, BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(0xC, 2)));
            Assert.Equal(60, BinaryPrimitives.ReadInt32LittleEndian(arena.Read(MainProgrammeWorkspaceOffset + 4, 4)));
            Assert.Equal(36, BinaryPrimitives.ReadInt32LittleEndian(arena.Read(SecondaryProgrammeWorkspaceOffset + 4, 4)));
            detached = true;
            Mark("effect283-root-detached", current, cold);
        }
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(fade);
        Assert.True(restored);
        Assert.True(detached);
        Assert.True(window);
        Assert.True(bodyCommon);
        Assert.Equal(new[] { 0, 1 }, banks.Order().ToArray());
        Assert.Equal(new[] { RootCallback, MainCallback }, callbacks.Order().ToArray());
        Assert.Contains(59, mainFrames);
        Assert.Contains(35, secondaryFrames);
        foreach (string marker in new[] { "effect283-main-1", "effect283-main-25", "effect283-main-44", "effect283-main-45", "effect283-main-53", "effect283-main-59", "effect283-root-detached" })
        {
            Assert.Contains(marker, markers);
        }
        Assert.Equal(0, completed.NativeGraphics!.Models[4].Flags0 & 4);
        File.WriteAllText(Path.Combine(output, "effect283-observations.json"), JsonSerializer.Serialize(new { MainAges = mainAges.Order(), MainFrames = mainFrames.Order(), SecondaryFrames = secondaryFrames.Order(), Callbacks = callbacks.Order(), PacketBanks = banks.Order(), Opcodes = opcodes.Order(), Markers = markers.Order(), BodyCommon = bodyCommon, Window = window, Fade = fade, Restored = restored, Detached = detached, Observations = observations }));
    }

    public static NativeBattleLogicalCommand[] EffectCommands(BattleSnapshot current)
    {
        uint cursor = current.NativeEffectMemory!.Effect283!.ReadDword(CursorGlobal);
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
