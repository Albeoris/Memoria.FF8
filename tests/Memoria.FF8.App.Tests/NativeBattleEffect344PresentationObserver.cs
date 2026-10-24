using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect344Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect344PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<byte> workerPhases = [];
    private readonly List<object> observations = [];
    private bool musicLoaded;
    private bool sequencePublished;
    public bool CameraPublished { get; private set; }
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect344State state = current.NativeEffectMemory!.Effect344!;
        state.Validate();
        NativeBattleModulePoolTask? before = Child(prior);
        NativeBattleModulePoolTask? child = Child(current);
        byte complete = state.Read(CompletionGlobal, 1)[0];
        foreach (NativeBattleAuxiliaryTask worker in current.NativeGraphics.Auxiliary.Slots.Where(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.MusicLoad501A70))
        {
            Assert.Equal(CompletionGlobal, BinaryPrimitives.ReadUInt32LittleEndian(worker.Payload.AsSpan().Slice(4, 4)));
            workerPhases.Add(worker.Payload[0]);
            Assert.Equal((byte)0, complete);
            NativeBattleMusicLoadPointers pointers = current.NativeMusicProcess!.LoadSource!;
            Assert.Equal(0xDEE398u, pointers.SoundBankAddress);
            Assert.Equal(0xE0DDB8u, pointers.MusicAddress);
            Assert.Equal(10528, pointers.MusicLength);
            Mark("effect344-worker-phase-" + worker.Payload[0], current, cold);
        }
        if (complete != 0)
        {
            NativeBattleMusicProcessState process = current.NativeMusicProcess!;
            byte buffer = process.Commands!.TargetBufferAddress switch
            {
                0x1CE075C => 0,
                0x1CDC750 => 1,
                _ => throw new InvalidDataException("Effect344 music requires its actual current destination buffer.")
            };
            Assert.Equal(new NativeMusicBufferIdentity(91, NativeMusicBufferProducer.Battle501A70), process.Buffers.ReadBuffer(buffer));
            musicLoaded = true;
            Mark("effect344-worker-completed", current, cold);
        }
        if (before is not null && (before.Flags & 1) != 0)
        {
            ushort age = before.ReadWord(12);
            if (age == 32 && child?.ReadWord(12) == 33)
            {
                NativeBattleEffect344Resources resources = effect.Binding.Effect344Resources!;
                Assert.Equal(resources.Camera.Data.ToArray(), current.NativeEffectMemory.Textures!.Animation!.Source.Data.ToArray());
                Assert.Equal(resources.Camera.Data.ToArray(), current.NativeCamera!.ControlResource!.Source.Data.ToArray());
                Assert.Equal((byte)20, current.NativeGraphics.Models[4].Sequence);
                CameraPublished = true;
                sequencePublished = true;
                Mark("effect344-camera-sequence20", current, cold);
            }
            if (age == 34 && child?.ReadWord(12) == 35)
            {
                Contacts++;
                Mark("effect344-contact34", current, cold);
            }
        }
        for (int index = 0; index < current.NativeGraphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask text = current.NativeGraphics.Effects.Slots[index];
            if ((text.Flags0 & 1) != 0 && text.Kind == NativeBattleEffectKind.DamageText5069B0 && text.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect344-contact-glyph", current, cold);
            }
        }
        if (effect.ReturnedDescriptor is > 0)
        {
            Assert.Equal(2, effect.Pools.Length);
            Assert.All(state.BankStorage, bank => Assert.Null(bank));
            for (int index = 0; index < Pools.Length; index++)
            {
                NativeBattleEffectTaskPool pool = effect.Pools[index];
                NativeBattleCureFamilyPool expected = Pools[index];
                Assert.Equal(expected.Descriptor, pool.DescriptorAddress);
                Assert.Equal(expected.Bank, pool.BankAddress);
                Assert.Equal(expected.Stride, pool.Stride);
                Assert.Equal((int)expected.Capacity, pool.Slots.Length);
            }
            Assert.Equal(0u, state.ReadDword(TargetGlobal));
            Assert.Equal(4u, state.ReadDword(CasterGlobal));
            ushort rootAge = effect.Pools[0].Slots[0].ReadWord(12);
            if (rootAge > 0)
            {
                Assert.Equal(((rootAge - 1) & 1) == 0 ? 0x24E0BC8u : 0x24E8BC8u, state.ReadDword(CursorGlobal));
            }
            if (child is not null && child.ReadWord(12) is 1 or 30 or 31 or 32 or 34 or 35)
            {
                Mark("effect344-child-age-" + child.ReadWord(12), current, cold);
            }
        }
        else
        {
            Assert.Empty(effect.Pools);
            Assert.All(state.BankStorage, bank => Assert.NotNull(bank));
            Assert.Equal((ushort)36, BinaryPrimitives.ReadUInt16LittleEndian(state.ReadBank(1).Read(12, 2)));
            Assert.True(BinaryPrimitives.ReadUInt16LittleEndian(state.ReadBank(0).Read(12, 2)) >= 36);
            Mark("effect344-banks-retained", current, cold);
        }
        observations.Add(new { current.Frame, Callback = current.NativeGraphics.Callback, ChildAge = child?.ReadWord(12), Completion = complete, Cursor = state.ReadDword(CursorGlobal), CameraPublished, Sequence = current.NativeGraphics.Models[4].Sequence, Contacts, ContactGlyphs });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.True(musicLoaded);
        Assert.True(CameraPublished);
        Assert.True(sequencePublished);
        Assert.Contains((byte)0, workerPhases);
        Assert.Contains((byte)1, workerPhases);
        Assert.Contains((byte)2, workerPhases);
        Assert.Contains("effect344-child-age-30", markers);
        Assert.Contains("effect344-child-age-31", markers);
        Assert.Contains("effect344-worker-completed", markers);
        Assert.Contains("effect344-camera-sequence20", markers);
        Assert.Contains("effect344-contact34", markers);
        Assert.Contains("effect344-banks-retained", markers);
        Assert.Empty(completed.NativeGraphics!.OriginalEffect!.Pools);
        File.WriteAllText(Path.Combine(output, "effect344-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), WorkerPhases = workerPhases.Order(), MusicLoaded = musicLoaded, CameraPublished, Sequence20Published = sequencePublished, Contacts, ContactGlyphs, Observations = observations }));
    }

    private static NativeBattleModulePoolTask? Child(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect?.Binding.Effect == Effect ? snapshot.NativeGraphics.OriginalEffect.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => task.Callback == ChildCallback) : null;

    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, snapshot);
        }
    }
}
