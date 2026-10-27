using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleSiren95State
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectOwnedModelState? ModelState { get; init; }
}

public sealed record NativeBattleSiren95ModelServices(Func<NativeBattleSiren95Frame, uint, NativeBattleEffectOwnedModelState, NativeBattleSiren95Frame> Draw, Func<NativeBattleSiren95Frame, uint, int, int, NativeBattleSiren95Frame> PlaySound, Func<NativeBattleSiren95Frame, NativeBattleSiren95Frame> PlayStream, Func<NativeBattleSiren95Frame, uint, bool, NativeBattleSiren95Frame> EmitParticles);

public static class NativeBattleSiren95Model
{
    public const uint ProjectedAddress746C10 = 0x258BFB0;

    public static NativeBattleEffectOwnedModelResource ReadCurrentResource(NativeBattleEffectMemory memory, uint address)
    {
        NativeBattleGfAnimationSource current = NativeBattleSiren95Io.ReadProgrammeSource(memory, address);
        using NativeBattleGfSourceReader reader = current.OpenReader();
        if (current.Length < 24 || reader.Int(0) != 4)
        {
            throw new InvalidDataException("Native Siren model requires its current four-section directory.");
        }
        int length = reader.Int(20);
        if (length < 24 || length > current.Length)
        {
            throw new InvalidDataException("Native Siren model declared extent leaves its current file bytes.");
        }
        ImmutableArray<byte> bounded = ImmutableCollectionsMarshal.AsImmutableArray(reader.ReadBytes(0, length));
        int[] sections = Enumerable.Range(0, 5).Select(index => NativeBattleDatDirectory.Section(bounded.AsSpan(), index)).ToArray();
        if (sections[4] < 24 || sections[4] > current.Length || sections[0] < 24 || sections.Zip(sections.Skip(1), (left, right) => left <= right).Any(ordered => !ordered))
        {
            throw new InvalidDataException("Native Siren model sections leave their current supplied file bytes.");
        }
        ImmutableArray<byte> bytes = bounded;
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        return new(address, bytes, pose, NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount));
    }

    public static NativeBattleSiren95Frame Initialize8DD0F0(NativeBattleSiren95Frame frame, uint address, NativeBattleEffectOwnedModelResource resource, ushort mode, uint defaultColor)
    {
        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(Read(frame, address), address, resource, mode, defaultColor);
        return Write(frame with { Memory = frame.Memory with { Siren95 = State(frame) with { ModelState = initialized.State } } }, address, initialized.Task);
    }

    public static NativeBattleSiren95Frame PublishProjected746C10(NativeBattleSiren95Frame frame, ImmutableArray<byte> bytes, int length)
    {
        if (bytes.IsDefault || length < 0 || length > bytes.Length || (length & 7) != 0)
        {
            throw new InvalidDataException("Native Siren projected stores require their actual written records.");
        }
        if (length == 0)
        {
            return frame;
        }
        return frame with { Memory = frame.Memory with { Siren95 = State(frame).Write(ProjectedAddress746C10, bytes.AsSpan().Slice(0, length)) } };
    }

    public static NativeBattleSiren95TaskStep Advance746B60(NativeBattleSiren95Frame frame, uint address, NativeBattleEffectOwnedModelResource resource, NativeBattleSiren95ModelServices services)
    {
        NativeBattleEffectOwnedModelState model = State(frame).ModelState ?? throw new NotSupportedException("Native Siren model requires its actual initialized pose owner.");
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x746B60 || model.TaskAddress != address)
        {
            throw new InvalidDataException("Native Siren model task contradicts its retained pose owner.");
        }
        NativeBattleEffectOwnedModel.Validate(task, model, resource);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteByte(0x58, 24).WriteByte(0x59, 24).WriteByte(0x5A, 24).WriteWord(0x4C, 0).WriteWord(0x4E, unchecked((ushort)-480)).WriteWord(0x50, 3264).WriteDword(0x60, unchecked(address + 0x114)).WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
                Select(0);
                Next();
                break;
            case 1:
                Advance(true);
                if (Spawned() >= 4)
                {
                    task = task.WriteByte(0x58, 32).WriteByte(0x59, 32).WriteByte(0x5A, 32);
                    Next();
                }
                break;
            case 2:
                uint director = State(frame).DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren model requires its director pointer.");
                if (unchecked((short)State(frame).ReadWord(director + 0x46)) >= 18)
                {
                    Store();
                    frame = services.PlaySound(frame, 0x153301C, 1, 128);
                    frame = services.PlayStream(frame);
                    task = Read(frame, address);
                    Select(1);
                    Next();
                }
                break;
            case 3:
                EmitTimed();
                Advance(true);
                byte color = unchecked((byte)(task.ReadByte(0x58) + 4));
                if (color >= 128)
                {
                    color = 128;
                    Next();
                }
                task = task.WriteByte(0x58, color).WriteByte(0x59, color).WriteByte(0x5A, color);
                break;
            case 4:
                EmitTimed();
                if (Advance(false))
                {
                    Select(2);
                    Next();
                }
                break;
            case 5:
                Emit(true);
                Advance(true);
                if (Spawned() >= 5)
                {
                    Next();
                }
                break;
            case 6:
                Advance(true);
                if (Spawned() >= 6)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
                    Next();
                }
                break;
            case 7:
                break;
            default:
                throw new NotSupportedException($"Native Siren model signed phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        Store();
        if ((task.ReadByte(0x26) & 4) == 0)
        {
            frame = services.Draw(frame, address, model);
        }
        task = Read(frame, address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(Write(frame, address, task), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        void Store()
        {
            frame = Write(frame with { Memory = frame.Memory with { Siren95 = State(frame) with { ModelState = model } } }, address, task);
        }
        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        short Spawned() => unchecked((short)State(frame).ReadWord((State(frame).DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren model requires its current director pointer.")) + 0x40));
        void Select(short animation)
        {
            NativeBattleEffectOwnedModelStep selected = NativeBattleEffectOwnedModel.Select8DD190(task, model, resource, animation);
            task = selected.Task;
            model = selected.State;
        }
        bool Advance(bool loop)
        {
            NativeBattleEffectOwnedModelStep advanced = loop ? NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(task, model, resource) : NativeBattleEffectOwnedModel.AdvanceOnce8DD220(task, model, resource);
            task = advanced.Task;
            model = advanced.State;
            return advanced.Completed;
        }
        void Emit(bool secondary)
        {
            Store();
            frame = services.EmitParticles(frame, address, secondary);
            task = Read(frame, address);
        }
        void EmitTimed()
        {
            short clock = unchecked((short)task.ReadWord(0x136));
            if (clock is >= 10 and <= 55)
            {
                Emit(false);
            }
            if (unchecked((short)task.ReadWord(0x136)) >= 30)
            {
                Emit(true);
            }
        }
    }

    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Write(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
    private static NativeBattleSiren95State State(NativeBattleSiren95Frame frame) => frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren model requires its current module owner.");
}
