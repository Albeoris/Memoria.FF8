using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Retained software raster owners after the PC renderer's45B460 initialization; image words belong to the shared texture state.</summary>
public sealed record NativeBattleSoftwarePrimitiveState([property: JsonRequired] NativeSoftwareDrawingEnvironmentState Environment, [property: JsonRequired] NativeSoftwareTextureState Texture, [property: JsonRequired] NativeSoftwarePolygonRasterState Raster, [property: JsonRequired] NativeSoftwareReciprocalTable Reciprocals)
{
    public NativeSoftwareDrawingEnvironmentBuffers? EnvironmentBuffers { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSoftwareViewportInputs? ViewportInputs { get; init; }

    public NativeBattleSoftwarePrimitiveState Initialize500400() => this with { EnvironmentBuffers = NativeSoftwareDrawingEnvironmentBuffers.Initialize500400(EnvironmentBuffers?.CurrentGlobal) };

    public static NativeBattleSoftwarePrimitiveState FromInitializedRenderer()
    {
        NativeSoftwareEdgeBuffers left = new(new byte[40].ToImmutableArray(), new byte[20].ToImmutableArray());
        NativeSoftwareEdgeBuffers right = new(new byte[40].ToImmutableArray(), new byte[20].ToImmutableArray());
        // The portable PC backend explicitly selects its45B4C0 viewport at initialization.
        return new(NativeSoftwareDrawingEnvironmentState.FromColdProcess(), NativeSoftwareTextureState.FromColdProcess(), new(new(left, right), null), NativeSoftwareReciprocalTable.Initialize463280()) { ViewportInputs = new(640, 480, 0, 0) };
    }

    public NativeBattleSoftwarePrimitiveState DrawPolygon(byte[] image, NativeBattleLogicalCommand command, NativeSoftwarePrimitiveTable table)
    {
        NativeSoftwareEnvironmentDerived environment = Environment.Derived ?? throw new NotSupportedException("Native software drawing requires its executed environment publication.");
        byte[] packet = new byte[checked((command.Words.Length + 1) * 4)];
        for (int index = 0; index < command.Words.Length; index++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan((index + 1) * 4), command.Words[index]);
        }
        NativeSoftwarePolygonPacketDraw draw = NativeSoftwarePolygonPackets.Draw(image, Raster, Texture, command.Opcode, packet, NativeSoftwarePolygonPackets.CoordinateShift45D080(command.Flags20), environment.DrawOffsetX, environment.DrawOffsetY, environment.ClipBounds, table, Reciprocals);
        return this with { Texture = draw.Texture, Raster = draw.Raster };
    }
}

public sealed partial record NativeBattlePrimitiveProcess
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSoftwarePrimitiveState? Software { get; init; }
}

public sealed partial record NativeBattleGraphicsDefinition
{
    public NativeSoftwarePrimitiveTable? SoftwarePrimitiveTable { get; init; }
}
