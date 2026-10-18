using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A transient physical view of the canonical graphics models, preserving unknown retained bytes.</summary>
public static class NativeBattleScanPhysicalModels
{
    public static ImmutableArray<NativeBattleScanByteOwner> Capture(ImmutableArray<NativeBattleGraphicsModel> models)
    {
        if (models.IsDefault || models.Length != 7)
        {
            throw new InvalidDataException("Native Scan requires its seven canonical physical graphics models.");
        }
        ImmutableArray<NativeBattleScanByteOwner>.Builder rows = ImmutableArray.CreateBuilder<NativeBattleScanByteOwner>(7);
        for (int index = 0; index < 7; index++)
        {
            NativeBattleGraphicsModel model = models[index];
            if (model is null || model.Index != index)
            {
                throw new InvalidDataException("Native Scan graphics models differ from physical bank order.");
            }
            NativeBattleScanByteOwner row = new(new byte[156].ToImmutableArray(), new bool[156].ToImmutableArray());
            row = row.WriteWord(0, model.Flags0);
            if (model.ModelId4 is byte category)
            {
                row = row.WriteByte(4, category);
            }
            if (model.Rotation12 is short pitch)
            {
                row = row.WriteWord(0x0C, unchecked((ushort)pitch));
            }
            if (model.Rotation16 is short roll)
            {
                row = row.WriteWord(0x10, unchecked((ushort)roll));
            }
            if (model.Rotation14 is short yaw)
            {
                row = row.WriteWord(0x0E, unchecked((ushort)yaw));
            }
            if (model.Transform is { } transform)
            {
                row = WriteVector(row, 0x14, transform.Initial20);
                row = WriteVector(row, 0x1C, transform.Position28).WriteWord(0x22, unchecked((ushort)transform.Vertical34)).WriteWord(0x24, unchecked((ushort)transform.Angle36)).WriteWord(0x26, unchecked((ushort)transform.Scale38));
                row = WriteVector(row, 0x94, transform.Scale148).WriteWord(0x9A, unchecked((ushort)transform.Scale154));
            }
            if (model.Color40 is uint color)
            {
                row = row.WriteDword(0x28, color);
            }
            if (model.Color44 is uint secondColor)
            {
                row = row.WriteDword(0x2C, secondColor);
            }
            if (model.ScaleVectorAddress48 is uint scalePointer)
            {
                row = row.WriteDword(0x30, scalePointer);
            }
            if (model.GeometryWorldMatrix is { } matrix)
            {
                matrix.Validate();
                for (int component = 0; component < 9; component++)
                {
                    row = row.WriteWord(0x40 + 2 * component, unchecked((ushort)matrix.Rotation[component]));
                }
                row = row.WriteDword(0x54, unchecked((uint)matrix.X)).WriteDword(0x58, unchecked((uint)matrix.Y)).WriteDword(0x5C, unchecked((uint)matrix.Z));
            }
            if (model.HasLinkedModelRing140 is not null || model.LinkedModelIndex140 is not null)
            {
                int? linked = model.ReadLinkedModelIndex140();
                row = row.WriteDword(0x8C, linked is int next ? checked(0x1D972C0u + 156u * (uint)next) : 0);
            }
            rows.Add(row);
        }
        return rows.MoveToImmutable();
    }

    public static ImmutableArray<NativeBattleGraphicsModel> Apply(ImmutableArray<NativeBattleGraphicsModel> models, ImmutableArray<NativeBattleScanByteOwner> rows)
    {
        if (models.IsDefault || models.Length != 7 || rows.IsDefault || rows.Length != 7)
        {
            throw new InvalidDataException("Native Scan model publication requires the same seven physical rows and canonical models.");
        }
        for (int index = 0; index < 7; index++)
        {
            NativeBattleGraphicsModel model = models[index];
            NativeBattleScanByteOwner row = rows[index];
            row.RequireLength(156);
            if (model.Index != index)
            {
                throw new InvalidDataException("Native Scan model publication changed physical model order.");
            }
            model = model with { Flags0 = row.ReadWord(0) };
            if (Known(0x0C, 2))
            {
                model = model with { Rotation12 = S(0x0C) };
            }
            if (Known(0x0E, 2))
            {
                model = model with { Rotation14 = S(0x0E) };
            }
            if (Known(0x10, 2))
            {
                model = model with { Rotation16 = S(0x10) };
            }
            if (model.Transform is { } transform)
            {
                model = model with { Transform = transform with { Position28 = new(S(0x1C), S(0x1E), S(0x20)), Vertical34 = S(0x22) } };
            }
            if (Known(0x30, 4))
            {
                model = model with { ScaleVectorAddress48 = row.ReadDword(0x30) };
            }
            if (model.GeometryWorldMatrix is { } matrix)
            {
                model = model with { GeometryWorldMatrix = matrix with { Rotation = Enumerable.Range(0, 9).Select(component => S(0x40 + component * 2)).ToImmutableArray(), X = unchecked((int)row.ReadDword(0x54)), Y = unchecked((int)row.ReadDword(0x58)), Z = unchecked((int)row.ReadDword(0x5C)) } };
            }
            models = models.SetItem(index, model);

            bool Known(int offset, int count) => row.Known.AsSpan(offset, count).IndexOf(false) < 0;

            short S(int offset) => unchecked((short)row.ReadWord(offset));
        }
        return models;
    }

    private static NativeBattleScanByteOwner WriteVector(NativeBattleScanByteOwner row, int offset, NativeBattleVector value) => row.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
}
