using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaModelFrame([property: JsonRequired] NativeBattleEffectMemory Memory, [property: JsonRequired] NativeBattleEffectTaskPool Pool, [property: JsonRequired] NativeBattleCactuarRawModelState RawModel, [property: JsonRequired] NativeBattleShivaGeometryState Geometry, [property: JsonRequired] int EntryCounter22BD010);
public sealed record NativeBattleShivaModelStep(NativeBattleShivaModelFrame Frame, int Result);
public sealed record NativeBattleShivaDistortionContext(ImmutableArray<byte> Bytes, ImmutableArray<bool> Known);
public sealed record NativeBattleShivaDistortionSetup(NativeBattleShivaModelFrame Frame, NativeBattleShivaDistortionContext Context);
public abstract record NativeBattleShivaModelOperation;
public sealed record NativeBattleShivaModelInitializer(uint Address) : NativeBattleShivaModelOperation;
public sealed record NativeBattleShivaModelOverlay(int First, int Second, int Third, int Fourth) : NativeBattleShivaModelOperation;
public sealed record NativeBattleShivaModelInputs(uint GraphicsFlags, NativeBattleEffectOwnedModelResource Resource, ImmutableArray<int> TextureValuesD3250C, Func<NativeBattleShivaModelFrame, NativeBattleVector, NativeBattleVector, NativeBattleShivaDistortionSetup> InitializeDistortion5C3130, Func<NativeBattleShivaModelFrame, int, int, NativeBattleShivaModelFrame> Texture5C4F80, Func<NativeBattleShivaModelFrame, NativeBattleShivaDistortionContext, NativeBattleShivaModelFrame> DrawDistortion5C5060, Func<NativeBattleShivaModelFrame, NativeBattleShivaModelFrame> DrawRaw5C4EE0, Func<NativeBattleShivaModelFrame, NativeBattleShivaModelOperation, NativeBattleShivaModelFrame> Execute);

/// <summary>5C4AD0's model timeline over its retained90h model/34h control and caller-owned draw services.</summary>
public static class NativeBattleShivaModelTask
{
    public static NativeBattleRawModelBinding Binding { get; } = new(0x22BCFD0, 4);

    public static NativeBattleShivaModelStep Advance5C4AD0(NativeBattleShivaModelFrame frame, int slot, NativeBattleShivaModelInputs inputs)
    {
        ValidateFrame(frame);
        if ((uint)slot >= frame.Pool.Slots.Length || frame.Pool.Slots[slot] is not { Callback: 0x5C4AD0 } task || (task.Flags & 1) == 0 || inputs.Resource.Address != NativeBattleEffectNodeMemory.ArenaAddress)
        {
            throw new InvalidDataException("Native Shiva model requires its allocated5C4AD0 task and actual544 resource loaded at arena zero.");
        }
        int counter = unchecked((short)task.ReadWord(0x0C));
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        if (counter == 0)
        {
            NativeBattleCactuarRawModelState model = InitializeMatrix(frame.RawModel);
            model = NativeBattleCactuarRawModel.Initialize5A8F40(model, inputs.Resource, NativeBattleEffectNodeMemory.ArenaAddress, 3, Binding);
            task = task.WriteWord(0x20, 1250).WriteWord(0x22, unchecked((ushort)-250));
            frame = WriteTask(frame with { RawModel = model }, slot, task);
        }
        if (counter < 5)
        {
            if (!paused && counter == 0)
            {
                frame = Select(frame, inputs, 0);
            }
            frame = WriteModelDword(frame, 0x58, unchecked((uint)(int)unchecked((short)frame.Pool.Slots[slot].ReadWord(0x20))));
            NativeBattleShivaDistortionSetup initialized = inputs.InitializeDistortion5C3130(frame, new(0, 0, 0), new(0, -4096, 0));
            frame = initialized.Frame;
            NativeBattleShivaDistortionContext context = BindProjectedScratch(initialized.Context);
            frame = inputs.Texture5C4F80(frame, 1, 0);
            frame = inputs.DrawDistortion5C5060(frame, context);
            if (!paused)
            {
                task = frame.Pool.Slots[slot];
                task = task.WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + task.ReadWord(0x22))));
                frame = WriteTask(frame, slot, task);
            }
        }
        else if (counter < 59)
        {
            frame = WriteModelDword(frame, 0x58, 0);
            if (counter >= 53)
            {
                if (inputs.TextureValuesD3250C.IsDefault || inputs.TextureValuesD3250C.Length != 6)
                {
                    throw new NotSupportedException("Native Shiva model requires its actual six D3250C texture operands.");
                }
                frame = inputs.Texture5C4F80(frame, 1, inputs.TextureValuesD3250C[counter - 53]);
            }
            frame = inputs.DrawRaw5C4EE0(frame);
        }
        else if (counter < 139)
        {
            if (!paused)
            {
                switch (counter)
                {
                    case 59:
                        frame = Append5C1370(Select(frame, inputs, 1), 0x5C5950);
                        break;
                    case 60:
                        frame = inputs.Execute(frame, new NativeBattleShivaModelOverlay(0, 1, 8, 255));
                        break;
                    case 102:
                        frame = inputs.Execute(frame, new NativeBattleShivaModelInitializer(0x5C5BB0));
                        break;
                    case 103:
                        frame = inputs.Texture5C4F80(frame, 0, 1);
                        break;
                    case 105:
                        frame = inputs.Texture5C4F80(frame, 0, 0);
                        break;
                    case 108:
                        frame = Append5C1370(frame, 0x5C5D90);
                        break;
                    case 109:
                        frame = Append5C1370(frame, 0x5C62F0);
                        break;
                    case 117:
                        frame = Append5C1370(frame, 0x5C6040);
                        break;
                }
            }
            frame = DrawAndAdvance(frame, inputs, !paused);
        }
        else if (counter < 145)
        {
            frame = inputs.DrawRaw5C4EE0(frame);
        }
        else if (counter < 150)
        {
            frame = DrawAndAdvance(frame, inputs, !paused);
        }
        else if (counter < 199)
        {
            if (!paused)
            {
                switch (counter)
                {
                    case 150:
                        frame = Select(frame, inputs, 2);
                        break;
                    case 151:
                        frame = Append5C1370(frame, 0x5C6690);
                        break;
                    case 163:
                        frame = inputs.Execute(frame, new NativeBattleShivaModelInitializer(0x5C7180));
                        break;
                    case 164:
                        frame = inputs.Execute(frame, new NativeBattleShivaModelInitializer(0x5C6FA0));
                        break;
                    case 168:
                        frame = inputs.Execute(frame, new NativeBattleShivaModelInitializer(0x5C7570));
                        break;
                    case 196:
                        frame = Append5C1370(frame, 0x5C7CA0);
                        break;
                }
            }
            frame = DrawAndAdvance(frame, inputs, !paused);
        }
        ValidateFrame(frame);
        if (paused)
        {
            return new(frame, 0);
        }
        task = frame.Pool.Slots[slot];
        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        frame = WriteTask(frame, slot, task);
        bool complete = unchecked((short)task.ReadWord(0x0C)) >= 199;
        return new(complete ? frame with { EntryCounter22BD010 = frame.EntryCounter22BD010 | 1 } : frame, complete ? 2 : 0);
    }

    private static NativeBattleShivaModelFrame DrawAndAdvance(NativeBattleShivaModelFrame frame, NativeBattleShivaModelInputs inputs, bool advance)
    {
        frame = inputs.DrawRaw5C4EE0(frame);
        if (advance)
        {
            frame = frame with { RawModel = NativeBattleCactuarRawModel.Advance5A8FD0(frame.RawModel, inputs.Resource, Binding).State };
        }
        return frame;
    }

    private static NativeBattleShivaModelFrame Select(NativeBattleShivaModelFrame frame, NativeBattleShivaModelInputs inputs, short animation)
    {
        return frame with { RawModel = NativeBattleCactuarRawModel.Select5A8FB0(frame.RawModel, inputs.Resource, animation, Binding) };
    }

    private static NativeBattleShivaModelFrame Append5C1370(NativeBattleShivaModelFrame frame, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Pool;
        (pool, int slot) = pool.Append(callback);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0x0C, 0).WriteWord(0x0E, 0)) };
        return frame with { Pool = pool };
    }

    private static NativeBattleCactuarRawModelState InitializeMatrix(NativeBattleCactuarRawModelState state)
    {
        byte[] bytes = state.Model.ToArray();
        bytes.AsSpan(0x40, 16).Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x40), 4096);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x48), 4096);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x50), 4096);
        bytes.AsSpan(0x54, 12).Clear();
        return state with { Model = bytes.ToImmutableArray() };
    }

    private static NativeBattleShivaModelFrame WriteModelDword(NativeBattleShivaModelFrame frame, int offset, uint value)
    {
        byte[] bytes = frame.RawModel.Model.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        return frame with { RawModel = frame.RawModel with { Model = bytes.ToImmutableArray() } };
    }

    private static NativeBattleShivaModelFrame WriteTask(NativeBattleShivaModelFrame frame, int slot, NativeBattleModulePoolTask task)
    {
        return frame with { Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(slot, task with { Next = frame.Pool.Slots[slot].Next }) } };
    }

    private static NativeBattleShivaDistortionContext BindProjectedScratch(NativeBattleShivaDistortionContext context)
    {
        if (context is null || context.Bytes.IsDefault || context.Known.IsDefault || context.Bytes.Length != 0x1A8 || context.Known.Length != 0x1A8)
        {
            throw new InvalidDataException("Native Shiva distortion requires its actual transient1A8-byte temporary context.");
        }
        byte[] bytes = context.Bytes.ToArray();
        bool[] known = context.Known.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), NativeBattleEffectNodeMemory.ArenaAddress + 0x33000);
        Array.Fill(known, true, 4, 4);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static void ValidateFrame(NativeBattleShivaModelFrame frame)
    {
        frame.Pool.Validate();
        if (frame.Pool.DescriptorAddress != 0x22BC198 || frame.Pool.BankAddress != 0x22BC1A8 || frame.Pool.Stride != 0x24 || frame.Pool.Slots.Length != 100 || frame.RawModel is null || frame.RawModel.Model.IsDefault || frame.RawModel.Model.Length != 0x90 || frame.RawModel.Control.IsDefault || frame.RawModel.Control.Length != 0x34 || frame.RawModel.ProjectedScratch is not null || frame.Geometry is null || frame.Geometry.Current is null || frame.Geometry.Previous is null)
        {
            throw new InvalidDataException("Native Shiva model requires its retained physical task, model/control, geometry owners and shared arena scratch.");
        }
    }
}
