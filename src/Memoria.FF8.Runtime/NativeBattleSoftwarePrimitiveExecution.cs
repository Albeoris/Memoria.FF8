using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>One draw batch over the retained PC environment, software raster state and shared VRAM image.</summary>
public sealed class NativeBattleSoftwarePrimitiveExecution : INativeBattleLogicalExecutionContext
{
    private byte[]? image;
    private bool imageChanged;
    private readonly List<NativeBattlePcTextureWindowPixels> textureSnapshots = [];
    private readonly Dictionary<NativeBattlePcTextureWindowPixels, int> textureSnapshotIndices = [];
    private readonly Dictionary<(int X, int Y, int Width, int Height), List<int>> textureSnapshotRegions = [];
    private NativeSoftwareDrawingEnvironmentState? normalEnvironment;
    private NativeSoftwareViewportInputs? normalViewportInputs;
    private NativeSoftwareNormalViewportDerived? normalViewport;

    public NativeBattleSoftwarePrimitiveExecution(NativeBattleSoftwarePrimitiveState state, NativeBattleEffectTextureState textures, NativeSoftwarePrimitiveTable table, bool hardwareEnabled, NativeBattlePcTextureWindowCache cache)
    {
        State = state;
        Textures = textures;
        SoftwareTable = table;
        HardwareEnabled = hardwareEnabled;
        Cache = cache;
    }

    public NativeBattleSoftwarePrimitiveState State { get; private set; }
    public NativeBattleEffectTextureState Textures { get; private set; }
    public NativeSoftwarePrimitiveTable SoftwareTable { get; }
    public bool HardwareEnabled { get; }
    public NativeBattlePcTextureWindowCache? Cache { get; set; }
    public ImmutableArray<NativeBattlePcTextureWindowPixels> TextureSnapshots => textureSnapshots.ToImmutableArray();
    public uint LiveMode => State.Environment.Derived?.Mode ?? throw new NotSupportedException("Native primitive dispatch requires its actual published drawing environment.");
    public uint LiveDebug => State.Environment.Debug ?? throw new NotSupportedException("Native primitive debug delivery requires its retained debug DWORD.");
    public uint LiveDispatchFlags => State.DispatchFlags1CA8880 ?? throw new NotSupportedException("Native normal dispatch requires its retained texture-dispatch flags.");

    public void BeginOrdering() => State = State with { DispatchFlags1CA8880 = 0 };

    public void FlushSoftwareImage() => PublishImage();

    public void BeginPass(byte pass)
    {
        if (pass == 1)
        {
            PublishImage();
        }
    }

    public void ApplyEnvironment45B660(int buffer)
    {
        NativeSoftwareDrawingEnvironmentBuffers buffers = State.EnvironmentBuffers ?? throw new NotSupportedException("Native environment publication requires its retained pair of initialized DRAWENV records.");
        NativeSoftwareDrawingEnvironmentApplication application = buffers.Apply45B660(buffer, State.Environment);
        State = State with { EnvironmentBuffers = application.Buffers, Environment = application.Environment };
        if (application.Invalidation is { } rectangle)
        {
            NativeBattleEffectTextureState priorTextures = Textures;
            PublishImage();
            Cache = (Cache ?? throw new NotSupportedException("Native environment publication requires its retained PC texture cache.")).Invalidate464850(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom, priorTextures);
        }
    }

    public void ExecuteSoftware(NativeBattleLogicalCommand command)
    {
        if (command.Opcode == 0x80)
        {
            if (SoftwareTable.Handler(command.Opcode) != 0x4670D0)
            {
                throw new NotSupportedException("Native software image transfer requires its actual shared transfer handler.");
            }
            ExecuteImageTransfer(command);
            return;
        }
        if (command.Opcode is >= 0xE1 and <= 0xE6)
        {
            uint expected = 0x467100u + 0x10u * (uint)(command.Opcode - 0xE1);
            if (SoftwareTable.Handler(command.Opcode) != expected)
            {
                throw new NotSupportedException("Native software state command requires its actual closed dispatch handler.");
            }
            if (command.Opcode is not (0xE2 or 0xE6))
            {
                ExecuteState(command);
            }
            return;
        }
        if (command.Opcode == 0x02)
        {
            if (SoftwareTable.Handler(command.Opcode) != 0x466EF0 || command.Words.Length != 3)
            {
                throw new NotSupportedException("Native software fill requires its actual dispatch handler and three payload DWORDs.");
            }
            byte[] packet = new byte[16];
            for (int index = 0; index < command.Words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4 + index * 4), command.Words[index]);
            }
            image ??= Textures.ReadVram();
            imageChanged |= NativeSoftwareRectangleFill.Fill466EF0(image, packet);
            return;
        }
        image ??= Textures.ReadVram();
        State = State.DrawPolygon(image, command, SoftwareTable);
        imageChanged = true;
    }

    public void ExecuteNormal(NativeBattleLogicalCommand command, NativeBattlePrimitiveDrawMode mode, byte pass)
    {
        if (pass != 0 && LiveDispatchFlags != 0)
        {
            throw new NotSupportedException("Native nonzero texture-dispatch flags require their selected normal-drawing branch.");
        }
        if (command.Opcode == 0x80)
        {
            if (pass == 0)
            {
                return;
            }
            if (pass != 1)
            {
                throw new NotSupportedException("Native image transfer requires its first-pass normal handler.");
            }
            ExecuteImageTransfer(command);
            return;
        }
        if (command.Opcode == 0x02)
        {
            if (pass == 0)
            {
                return;
            }
            if (pass != 1 || command.Words.Length != 3)
            {
                throw new NotSupportedException("Native normal fill requires its first-pass handler and three payload DWORDs.");
            }
            byte[] packet = new byte[16];
            for (int index = 0; index < command.Words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4 + index * 4), command.Words[index]);
            }
            image ??= Textures.ReadVram();
            imageChanged |= NativeSoftwareRectangleFill.Fill4623A0(image, packet);
            return;
        }
        if (command.Opcode is >= 0xE1 and <= 0xE6)
        {
            ExecuteState(command);
        }
        else
        {
            NativeSoftwareEnvironmentDerived environment = State.Environment.Derived ?? throw new NotSupportedException("Native normal drawing requires its published environment.");
            bool defaultEnvironment = environment.Mode == 1 && environment.Rectangle is { X: 0, Y: 0, Width: 320, Height: 216 } && environment.DrawPositionX == 0 && environment.DrawPositionY == 0;
            bool polygon = (command.Opcode & 0xE0) == 0x20;
            bool sprite = command.Opcode is 0x64 or 0x66;
            if (!defaultEnvironment && (State.ViewportInputs is null || !polygon && !sprite))
            {
                throw new NotSupportedException("Native normal geometry requires the viewport scale and offset outputs for this drawing environment.");
            }
            if ((command.Opcode & 0xE4) == 0x24 || sprite)
            {
                ushort page = mode.TexturePage ?? throw new NotSupportedException("Native textured command requires its actual page WORD.");
                NativeSoftwareTextureState prepared = sprite && pass == 0 ? State.Texture : NativeSoftwareTexturePreparation.PrepareTPage45B960(State.Texture, page).State;
                ushort clut = unchecked((ushort)(command.Words[2] >> 16));
                NativeSoftwareTextureState texture = NativeSoftwareTexturePreparation.PrepareClut45B920(prepared, clut, 1024 * 512 * 2);
                if (!ReferenceEquals(texture, State.Texture))
                {
                    State = State with { Texture = texture };
                }
            }
        }
    }

    public NativeBattlePrimitiveDrawMode SynchronizeDrawMode(NativeBattlePrimitiveDrawMode mode)
    {
        uint page = State.Texture.LastTPage ?? throw new NotSupportedException("Native primitive dispatch requires its retained shared texture page cache.");
        if (page > ushort.MaxValue)
        {
            throw new NotSupportedException("Native primitive page is outside the admitted packet WORD domain.");
        }
        return mode.TexturePage == (ushort)page ? mode : mode with { TexturePage = (ushort)page };
    }

    public NativeBattleLogicalResolvedCommand RetainTexturePixels(NativeBattleLogicalResolvedCommand command)
    {
        if (command.Pass != 0 && (command.Command.Opcode is 0x64 or 0x66))
        {
            command = command with { DrawMode = command.DrawMode with { SpriteFilter = State.SpriteFilter } };
        }
        NativeSoftwareNormalViewportDerived? normal = null;
        NativeSoftwareViewportInputs? inputs = null;
        if (command.Pass != 0 && ((command.Command.Opcode & 0xE0) is 0x20 or 0x40 || command.Command.Opcode is 0x64 or 0x66) && State.ViewportInputs is { } viewport)
        {
            if (!ReferenceEquals(normalEnvironment, State.Environment) || !ReferenceEquals(normalViewportInputs, viewport))
            {
                normalViewport = NativeSoftwareNormalViewport.Calculate45B6F0(State.Environment, viewport);
                normalEnvironment = State.Environment;
                normalViewportInputs = viewport;
            }
            normal = normalViewport!;
            inputs = viewport;
        }
        if (command.Pass == 0 || command.DrawMode.TextureSelection is not { } selection)
        {
            return normal is null ? command : command with { DrawMode = command.DrawMode with { NormalViewport = normal, ViewportInputs = inputs } };
        }
        PublishImage();
        NativeBattlePcTextureWindowCache cache = Cache ?? throw new NotSupportedException("Native textured draw requires its current resource cache.");
        NativeBattlePcTextureWindowPixels pixels = cache.PixelWords(selection) ?? NativeBattlePcTextureWindowPixels.Capture(selection.Row, new(selection.X, selection.Y, selection.Width, selection.Height), Textures);
        if (!textureSnapshotIndices.TryGetValue(pixels, out int index))
        {
            var region = (pixels.X, pixels.Y, pixels.Width, pixels.Height);
            if (!textureSnapshotRegions.TryGetValue(region, out List<int>? versions))
            {
                versions = [];
                textureSnapshotRegions.Add(region, versions);
            }
            int existing = versions.FindIndex(candidate => pixels.Data.AsSpan().SequenceEqual(textureSnapshots[candidate].Data.AsSpan()));
            index = existing < 0 ? -1 : versions[existing];
            if (index < 0)
            {
                index = textureSnapshots.Count;
                textureSnapshots.Add(pixels);
                versions.Add(index);
            }
            textureSnapshotIndices.Add(pixels, index);
        }
        return command with { DrawMode = normal is null ? command.DrawMode with { TexturePixelSnapshot = index } : command.DrawMode with { TexturePixelSnapshot = index, NormalViewport = normal, ViewportInputs = inputs } };
    }

    public void EndBatch()
    {
        PublishImage();
        State = State with { Environment = NativeSoftwareDrawingEnvironment.EndBatch45D080(State.Environment) };
    }

    private void ExecuteState(NativeBattleLogicalCommand command)
    {
        if (command.Words.Length != 1)
        {
            throw new InvalidDataException("Native drawing state command requires its single DWORD.");
        }
        uint value = command.Words[0];
        switch (command.Opcode)
        {
            case 0xE1:
                State = State with { Texture = NativeSoftwareTexturePreparation.PrepareTPage45B960(State.Texture, unchecked((ushort)value)).State };
                break;
            case 0xE3:
                State = State with { Environment = NativeSoftwareDrawingEnvironment.WriteE345BA30(State.Environment, value) };
                PublishCurrentEnvironmentFields();
                break;
            case 0xE4:
                NativeSoftwareEnvironmentUpdate update = NativeSoftwareDrawingEnvironment.WriteE445BA60(State.Environment, value);
                State = State with { Environment = update.State };
                PublishCurrentEnvironmentFields();
                if (update.Invalidation is { } rectangle)
                {
                    NativeBattleEffectTextureState priorTextures = Textures;
                    PublishImage();
                    Cache = (Cache ?? throw new NotSupportedException("Native E4 requires its retained PC texture cache.")).Invalidate464850(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom, priorTextures);
                }
                break;
            case 0xE5:
                State = State with { Environment = NativeSoftwareDrawingEnvironment.WriteE545BAD0(State.Environment, value) };
                PublishCurrentEnvironmentFields();
                break;
        }
    }

    private void PublishCurrentEnvironmentFields()
    {
        NativeSoftwareDrawingEnvironmentBuffers buffers = State.EnvironmentBuffers ?? throw new NotSupportedException("Native state commands require their retained DRAWENV global owner.");
        ImmutableArray<byte> current = buffers.CurrentGlobal ?? throw new NotSupportedException("Native state commands require the actual complete current DRAWENV record.");
        buffers.Validate();
        NativeSoftwareDrawingEnvironmentState environment = State.Environment;
        NativeSoftwareEnvironmentDerived derived = environment.Derived ?? throw new NotSupportedException("Native state commands require their recalculated environment outputs.");
        byte[] bytes = current.ToArray();
        Word(0, unchecked((ushort)environment.TopLeftX!.Value));
        Word(2, unchecked((ushort)environment.TopLeftY!.Value));
        Word(4, derived.Width);
        Word(6, derived.Height);
        Word(8, unchecked((ushort)environment.OffsetX!.Value));
        Word(10, unchecked((ushort)environment.OffsetY!.Value));
        State = State with { EnvironmentBuffers = buffers with { CurrentGlobal = bytes.ToImmutableArray() } };

        void Word(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
    }

    private void ExecuteImageTransfer(NativeBattleLogicalCommand command)
    {
        if (command.Words.Length != 4)
        {
            throw new InvalidDataException("Native image transfer requires its four payload DWORDs.");
        }
        PublishImage();
        NativeBattleEffectTextureState priorTextures = Textures;
        byte[] packet = new byte[20];
        for (int index = 0; index < command.Words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4 + index * 4), command.Words[index]);
        }
        image ??= Textures.ReadVram();
        imageChanged = true;
        NativeSoftwareImageTransfer.Transfer45BB00(image, packet, (left, top, right, bottom) =>
        {
            PublishTransferredImage(left, top, right, bottom);
            Cache = (Cache ?? throw new NotSupportedException("Native image transfer requires its retained texture cache.")).Invalidate464850(left, top, right, bottom, priorTextures);
        });
    }

    private void PublishTransferredImage(int left, int top, int right, int bottom)
    {
        if (!imageChanged || image is null)
        {
            return;
        }
        Textures.Validate();
        int first = image.Length;
        int end = 0;
        Span<byte> previous = stackalloc byte[1024 * 2];
        // The preceding full flush makes the transfer's clamped destination its only possible changed region.
        if (right >= left && bottom >= top)
        {
            int rowBytes = (right - left + 1) * 2;
            for (int row = top; row <= bottom; row++)
            {
                int offset = 2 * (row * 1024 + left);
                Span<byte> before = previous[..rowBytes];
                Textures.ReadVramInto(offset, before);
                ReadOnlySpan<byte> after = image.AsSpan(offset, rowBytes);
                if (after.SequenceEqual(before))
                {
                    continue;
                }
                int rowFirst = 0;
                while (after[rowFirst] == before[rowFirst])
                {
                    rowFirst++;
                }
                if (first == image.Length)
                {
                    first = offset + rowFirst;
                }
                int rowEnd = rowBytes;
                while (after[rowEnd - 1] == before[rowEnd - 1])
                {
                    rowEnd--;
                }
                end = offset + rowEnd;
            }
        }
        if (end > first)
        {
            Textures = Textures.PublishVramSpan(first, image.AsSpan(first, end - first));
        }
        imageChanged = false;
    }

    private void PublishImage()
    {
        if (!imageChanged || image is null)
        {
            return;
        }
        byte[] previous = Textures.ReadVram();
        int first = 0;
        while (first < image.Length && image[first] == previous[first])
        {
            first++;
        }
        int end = image.Length;
        while (end > first && image[end - 1] == previous[end - 1])
        {
            end--;
        }
        if (end > first)
        {
            Textures = Textures.PublishVramSpan(first, image.AsSpan(first, end - first));
        }
        imageChanged = false;
    }
}
