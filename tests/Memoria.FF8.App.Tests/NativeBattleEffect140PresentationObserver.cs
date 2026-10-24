using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect140Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect140PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly List<object> observations = [];
    private bool ownedBody;
    private bool privateGeometry;
    private bool vramCopy;
    private bool sourceBinding;
    private bool modelPose;
    private bool completion;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        CurrentFadeCommands = [];
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        if (graphics.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleEffect140State state = current.NativeEffectMemory!.Effect140!;
        state.Validate();
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main is null ? -1 : unchecked((short)main.ReadWord(12));
        int oldAge = before is null ? -1 : unchecked((short)before.ReadWord(12));
        foreach (NativeBattleModulePoolTask task in tasks.Values)
        {
            callbacks.Add(task.Callback);
        }
        if (state.StaticSourcesBound && !sourceBinding)
        {
            sourceBinding = true;
            Mark("effect140-current-bound-model-and-W", current, cold);
        }
        if (age is 1 or 59 or 61 or 82 or 101 or 126 or 150 or 161 or 194 or 195 or 235 or 245 or 254)
        {
            Mark("effect140-before-main-age-" + age, current, cold);
        }
        if (main is not null && age >= 102 && age <= 237)
        {
            modelPose = true;
            Mark("effect140-current-owned-pose", current, cold);
        }
        if (oldAge == 245 && age == 246)
        {
            Contacts++;
            Mark("effect140-current-contact-list", current, cold);
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 3 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect140-contact-glyph", current, cold);
            }
        }
        NativeBattleLogicalCommand[] privateCommands = PrivateCommands(current, state);
        CurrentFadeCommands = privateCommands.Where(command => command.Opcode == 0x2A && command.Words.Length == 5 && command.Words[1] == 0 && command.Words[2] == 320 && command.Words[3] == 216u << 16 && command.Words[4] == (320u | 216u << 16)).ToArray();
        NativeBattleLogicalCommand[] body = oldAge is >= 101 and <= 236 ? BodyCommands(current, effect.Binding.Effect140Resources!) : [];
        CurrentCommands = privateCommands.Concat(body).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
        if (body.Length > 0)
        {
            ownedBody = true;
            Mark("effect140-current-owned-body-common", current, cold);
        }
        if (privateCommands.Any(command => command.Opcode is 0x24 or 0x26 or 0x2C or 0x2E or 0x34 or 0x36 or 0x3C or 0x3E))
        {
            privateGeometry = true;
            Mark("effect140-current-private-geometry", current, cold);
        }
        if (privateCommands.Any(command => command.Opcode == 0x80))
        {
            vramCopy = true;
            Mark("effect140-current-vram-scanline-copy", current, cold);
        }
        if (state.Read(CompletionGlobal, 1)[0] != 0)
        {
            completion = true;
            Mark("effect140-current-common-texture-completion", current, cold);
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Pools = effect.Pools.Length, PrivateCommands = privateCommands.Length, OwnedBodyCommands = body.Length, Contacts, ContactGlyphs });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(sourceBinding);
        Assert.True(modelPose);
        Assert.True(privateGeometry);
        Assert.True(ownedBody);
        Assert.True(vramCopy);
        Assert.True(completion);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.Contains(MainCallback, callbacks);
        Assert.True(packetBanks.Count > 0);
        File.WriteAllText(Path.Combine(output, "effect140-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), PacketBanks = packetBanks.Order(), SourceBinding = sourceBinding, CurrentOwnedPose = modelPose, PrivateGeometry = privateGeometry, OwnedBody = ownedBody, VramCopy = vramCopy, CommonTextureCompletion = completion, Contacts, ContactGlyphs, Observations = observations }));
    }

    private NativeBattleLogicalCommand[] PrivateCommands(BattleSnapshot current, NativeBattleEffect140State state)
    {
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        uint first = arena + 0x3758;
        uint second = arena + 0x11758;
        if (arena == 0 || cursor < first)
        {
            return [];
        }
        uint start = cursor >= second ? second : first;
        Assert.InRange(cursor, start, start + 0x10000);
        NativeBattleLogicalCommand[] commands = Commands(current).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).ToArray();
        if (commands.Length > 0)
        {
            packetBanks.Add(start == first ? 0 : 1);
        }
        return commands;
    }

    private readonly record struct Face(bool Quad, uint First, uint Second, ushort Third, ushort Fourth);
    private static NativeBattleLogicalCommand[] BodyCommands(BattleSnapshot current, NativeBattleEffect140Resources resources)
    {
        HashSet<Face> faces = [];
        foreach (NativeBattleRawPolygon polygon in resources.Model.Geometry.SelectMany(mesh => mesh.Polygons))
        {
            ReadOnlySpan<byte> bytes = polygon.Bytes.AsSpan();
            faces.Add(new(polygon.Quad, Dword(bytes, 8), Dword(bytes, 12) & 0x01FFFFFF, polygon.Quad ? unchecked((ushort)Dword(bytes, 16)) : unchecked((ushort)(Dword(bytes, 4) >> 16)), polygon.Quad ? unchecked((ushort)(Dword(bytes, 16) >> 16)) : (ushort)0));
        }
        return Commands(current).Where(Matches).ToArray();

        bool Matches(NativeBattleLogicalCommand command)
        {
            if ((command.Packet.Bank is not "off_B6D070" and not "off_B6D094") || command.WordOffset != 1 || command.VertexPayload4.Length != 4)
            {
                return false;
            }
            if ((command.Opcode is 0x24 or 0x26) && command.Words.Length == 7)
            {
                return faces.Contains(new(false, command.Words[2], command.Words[4] & 0x01FFFFFF, unchecked((ushort)command.Words[6]), 0));
            }
            return (command.Opcode is 0x2C or 0x2E) && command.Words.Length == 9 && faces.Contains(new(true, command.Words[2], command.Words[4] & 0x01FFFFFF, unchecked((ushort)command.Words[6]), unchecked((ushort)command.Words[8])));
        }
    }

    private static uint Dword(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static IEnumerable<NativeBattleLogicalCommand> Commands(BattleSnapshot snapshot) => snapshot.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command);
    private static Dictionary<uint, NativeBattleModulePoolTask> Tasks(BattleSnapshot snapshot)
    {
        Dictionary<uint, NativeBattleModulePoolTask> result = [];
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } effect)
        {
            return result;
        }
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            HashSet<int> visited = [];
            int? index = pool.Head;
            while (index is int selected)
            {
                Assert.InRange(selected, 0, pool.Slots.Length - 1);
                Assert.True(visited.Add(selected));
                NativeBattleModulePoolTask task = pool.Slots[selected];
                result.Add(checked(pool.BankAddress + (uint)(selected * pool.Stride)), task);
                index = task.Next;
            }
        }
        return result;
    }

    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, snapshot);
        }
    }
}
