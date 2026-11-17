using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>One draw batch over the retained PC environment, software raster state and shared VRAM image.</summary>
public sealed class NativeBattleSoftwarePrimitiveExecution : INativeBattleLogicalExecutionContext
{
    private byte[]? image;
    private bool imageChanged;
    private readonly Dictionary<object, byte[]> modelTextureImages = new(ReferenceEqualityComparer.Instance);
    private readonly List<NativeBattlePcTextureWindowPixels> textureSnapshots = [];
    private readonly Dictionary<NativeBattlePcTextureWindowPixels, int> textureSnapshotIndices = [];
    private readonly Dictionary<(int X, int Y, int Width, int Height), List<int>> textureSnapshotRegions = [];
    private NativeSoftwareDrawingEnvironmentState? normalEnvironment;
    private NativeSoftwareViewportInputs? normalViewportInputs;
    private NativeSoftwareNormalViewportDerived? normalViewport;
    private readonly Dictionary<NativeBattleSceneCapture, (NativeBattleEffectTextureState Owner, NativeBattleSceneCaptureSample Sample)> sceneCaptureSamples = [];
    private readonly Dictionary<(ushort Clut, int Length), (NativeBattleEffectTextureState Owner, ImmutableArray<ushort> Palette)> sceneCapturePalettes = [];
    private readonly Dictionary<ImmutableArray<ushort>, ImmutableArray<ushort>> scenePaletteContents = new(ScenePaletteContentComparer.Instance);

    private const int MaximumRetainedModelModes = 256;
    private Dictionary<RetainedModelMode, NativeBattlePrimitiveDrawMode>? retainedModelModes;
    private RetainedModelMode lastModelMode;
    private NativeBattlePrimitiveDrawMode? lastPublishedModelMode;

    private readonly record struct RetainedModelMode(
        int TextureIndex, ushort? Page, uint? WindowBits, NativeBattlePcTextureWindowSelection Selection,
        NativeSoftwareNormalViewportDerived? Viewport, NativeSoftwareViewportInputs? ViewportInputs, NativeBattleSpriteFilterState? SpriteFilter)
    {
        public override int GetHashCode()
        {
            return HashCode.Combine(TextureIndex, Page, WindowBits, Selection.Row, Selection.Slot);
        }
    }

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

    public void ValidateModelTextureOwner(NativeBattleLogicalCommandReference command)
    {
        if (command.ModelTextureOwner is { } owner)
        {
            if (command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.ScreenSample })
            {
                throw new NotSupportedException("A model texture command cannot also sample the global screen capture.");
            }
            RequireModelTexture(owner);
            RequireGlobalCache().Validate();
        }
    }

    public NativeBattlePcTextureWindowCache? GetTextureCache(NativeBattleLogicalCommandReference command)
    {
        return command.ModelTextureOwner is { } owner ? WithCacheControls(RequireModelTexture(owner).Cache, RequireGlobalCache()) : Cache;
    }

    public void SetTextureCache(NativeBattleLogicalCommandReference command, NativeBattlePcTextureWindowCache? cache)
    {
        if (command.ModelTextureOwner is not { } owner)
        {
            Cache = cache;
            return;
        }
        NativeBattlePresentationTextureBank bank = RequireModelTexture(owner);
        NativeBattlePcTextureWindowCache actorCache = cache ?? throw new InvalidDataException("A model texture command requires its selected actor cache.");
        NativeBattlePcTextureWindowCache global = WithCacheControls(RequireGlobalCache(), actorCache);
        if (!ReferenceEquals(bank.Cache, actorCache))
        {
            Textures = Textures.WithPresentationTexture(owner.RequireActorId(), bank with { Cache = actorCache });
        }
        Cache = global;
    }

    public void BeginPass(byte pass)
    {
        if (pass == 1)
        {
            PublishImage();
            UpdateModelTextureCaches(static cache => cache.Prepare464BD0().Cache);
        }
    }

    public void EndPass(byte pass)
    {
        if (pass == 1)
        {
            UpdateModelTextureCaches(static cache => cache.Flush465930());
        }
    }

    private void UpdateModelTextureCaches(Func<NativeBattlePcTextureWindowCache, NativeBattlePcTextureWindowCache> update)
    {
        if (Textures.PresentationTextures is not { } banks)
        {
            return;
        }
        foreach ((string actorId, NativeBattlePresentationTextureBank bank) in banks.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            NativeBattlePcTextureWindowCache cache = update(bank.Cache);
            if (!ReferenceEquals(cache, bank.Cache))
            {
                Textures = Textures.WithPresentationTexture(actorId, bank with { Cache = cache });
            }
        }
    }

    private NativeBattlePresentationTextureBank RequireModelTexture(NativeBattleModelTextureOwner owner) => Textures.RequirePresentationTexture(owner.RequireActorId());

    private NativeBattlePcTextureWindowCache RequireGlobalCache() => Cache ?? throw new NotSupportedException("Native model texture commands require the retained global PC cache controls.");

    private static NativeBattlePcTextureWindowCache WithCacheControls(NativeBattlePcTextureWindowCache rowsOwner, NativeBattlePcTextureWindowCache controlsOwner)
    {
        return rowsOwner.ScaleU == controlsOwner.ScaleU && rowsOwner.ScaleV == controlsOwner.ScaleV && rowsOwner.SpriteTileCacheControl1CCFD90 == controlsOwner.SpriteTileCacheControl1CCFD90
            ? rowsOwner
            : rowsOwner with { ScaleU = controlsOwner.ScaleU, ScaleV = controlsOwner.ScaleV, SpriteTileCacheControl1CCFD90 = controlsOwner.SpriteTileCacheControl1CCFD90 };
    }

    public void ApplyEnvironment45B660(int buffer)
    {
        NativeSoftwareDrawingEnvironmentBuffers buffers = State.EnvironmentBuffers ?? throw new NotSupportedException("Native environment publication requires its retained pair of initialized DRAWENV records.");
        NativeSoftwareDrawingEnvironmentApplication application = buffers.Apply45B660(buffer, State.Environment);
        State = State with { EnvironmentBuffers = application.Buffers, Environment = application.Environment, SceneCapture = null };
        if (application.Invalidation is { } rectangle)
        {
            NativeBattleEffectTextureState priorTextures = Textures;
            PublishImage();
            Cache = (Cache ?? throw new NotSupportedException("Native environment publication requires its retained PC texture cache.")).Invalidate464850(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom, priorTextures);
        }
    }

    public void ExecuteSoftware(NativeBattleLogicalCommandReference command)
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
            if (SoftwareTable.Handler(command.Opcode) != 0x466EF0 || command.WordSpan.Length != 3)
            {
                throw new NotSupportedException("Native software fill requires its actual dispatch handler and three payload DWORDs.");
            }
            byte[] packet = new byte[16];
            for (int index = 0; index < command.WordSpan.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4 + index * 4), command.WordSpan[index]);
            }
            image ??= Textures.ReadVram();
            imageChanged |= NativeSoftwareRectangleFill.Fill466EF0(image, packet);
            return;
        }
        image ??= Textures.ReadVram();
        if (command.ModelTextureOwner is { } owner && (command.Opcode & 0xE4) == 0x24)
        {
            NativeBattleEffectTextureState textureSource = RequireModelTexture(owner).Textures;
            object publication = textureSource.VramPublicationIdentity;
            if (!modelTextureImages.TryGetValue(publication, out byte[]? textureImage))
            {
                textureImage = textureSource.ReadVram();
                modelTextureImages.Add(publication, textureImage);
            }
            State = State.DrawPolygon(image, textureImage, command, SoftwareTable);
        }
        else
        {
            State = State.DrawPolygon(image, command, SoftwareTable);
        }
        imageChanged = true;
    }

    public void ExecuteNormal(NativeBattleLogicalCommandReference command, NativeBattlePrimitiveDrawMode mode, byte pass)
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
            if (pass != 1 || command.WordSpan.Length != 3)
            {
                throw new NotSupportedException("Native normal fill requires its first-pass handler and three payload DWORDs.");
            }
            byte[] packet = new byte[16];
            for (int index = 0; index < command.WordSpan.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4 + index * 4), command.WordSpan[index]);
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
                NativeSoftwareTextureState prepared = sprite && pass == 0 ? State.Texture : NativeSoftwareTexturePreparation.PrepareTPageState45B960(State.Texture, page, out _);
                ushort clut = unchecked((ushort)(command.WordSpan[2] >> 16));
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
        NativeBattlePrimitiveDrawMode mode = RetainTextureDrawMode(command.Command, command.DrawMode, command.Pass, command.DrawMode.TextureSelection);
        return ReferenceEquals(mode, command.DrawMode) ? command : command with { DrawMode = mode };
    }

    public NativeBattleLogicalResolvedCommand RetainTexturePixels(NativeBattleLogicalCommandReference command, NativeBattlePrimitiveDrawMode mode, byte pass, NativeBattlePcTextureWindowSelection? selection)
    {
        return new(command, RetainTextureDrawMode(command, mode, pass, selection), pass);
    }

    private NativeBattlePrimitiveDrawMode RetainTextureDrawMode(NativeBattleLogicalCommandReference command, NativeBattlePrimitiveDrawMode mode, byte pass, NativeBattlePcTextureWindowSelection? selection)
    {
        bool sprite = pass != 0 && command.Opcode is 0x64 or 0x66;
        NativeBattleSpriteFilterState? spriteFilter = sprite ? State.SpriteFilter : mode.SpriteFilter;
        NativeSoftwareNormalViewportDerived? normal = null;
        NativeSoftwareViewportInputs? inputs = null;
        if (pass != 0 && ((command.Opcode & 0xE0) is 0x20 or 0x40 || sprite) && State.ViewportInputs is { } viewport)
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
        if (pass == 0 || selection is null)
        {
            return normal is null && !sprite ? mode : mode with { SpriteFilter = spriteFilter, NormalViewport = normal ?? mode.NormalViewport, ViewportInputs = normal is null ? mode.ViewportInputs : inputs };
        }

        PublishImage();
        NativeBattlePresentationTextureBank? bank = command.ModelTextureOwner is { } owner ? RequireModelTexture(owner) : null;
        NativeBattlePcTextureWindowCache global = RequireGlobalCache();
        NativeBattlePcTextureWindowCache cache = bank?.Cache ?? global;
        NativeBattleEffectTextureState textureSource = bank?.Textures ?? Textures;
        NativeBattlePcTextureWindowPixels? retainedPixels = cache.PixelWords(selection, out NativeBattlePrimitiveTextureWindow validatedRectangle);
        NativeBattlePcTextureWindowPixels pixels = retainedPixels ?? NativeBattlePcTextureWindowPixels.Capture(selection.Row, validatedRectangle, textureSource);
        if (!textureSnapshotIndices.TryGetValue(pixels, out int index))
        {
            var region = (pixels.X, pixels.Y, pixels.Width, pixels.Height);
            if (!textureSnapshotRegions.TryGetValue(region, out List<int>? versions))
            {
                versions = [];
                textureSnapshotRegions.Add(region, versions);
            }
            index = -1;
            foreach (int existing in versions)
            {
                if (SameTexturePixels(pixels, textureSnapshots[existing]))
                {
                    index = existing;
                    break;
                }
            }
            if (index < 0)
            {
                index = textureSnapshots.Count;
                textureSnapshots.Add(pixels);
                versions.Add(index);
            }
            textureSnapshotIndices.Add(pixels, index);
        }
        NativeBattleSceneCaptureSample? captureSample = command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.ScreenSample } capture && (command.Opcode & 0xE4) == 0x24 ? RetainSceneCapture(capture, command, mode.TexturePage) : mode.SceneCaptureSample;
        ImmutableArray<ushort>? copyPalette = pixels.SceneCopies is not null ? RetainScenePalette(command, mode.TexturePage, textureSource) : mode.SceneCopyPalette;
        NativeSoftwareNormalViewportDerived? retainedViewport = normal ?? mode.NormalViewport;
        NativeSoftwareViewportInputs? retainedInputs = normal is null ? mode.ViewportInputs : inputs;
        if (bank is not null && captureSample is null && copyPalette is null)
        {
            // The selected snapshot index identifies the published pixels and their provenance in this batch.
            RetainedModelMode key = new(index, mode.TexturePage, mode.TextureWindowBits, selection, retainedViewport, retainedInputs, spriteFilter);
            if (lastPublishedModelMode is not null && key == lastModelMode)
            {
                return lastPublishedModelMode;
            }
            retainedModelModes ??= [];
            if (!retainedModelModes.TryGetValue(key, out NativeBattlePrimitiveDrawMode? retained))
            {
                retained = PublishMode();
                if (retainedModelModes.Count < MaximumRetainedModelModes)
                {
                    retainedModelModes.Add(key, retained);
                }
            }
            lastModelMode = key;
            lastPublishedModelMode = retained;
            return retained;
        }
        return PublishMode();

        NativeBattlePrimitiveDrawMode PublishMode()
        {
            return mode with
            {
                TextureSelection = selection,
                TexturePixelSnapshot = index,
                SpriteFilter = spriteFilter,
                NormalViewport = retainedViewport,
                ViewportInputs = retainedInputs,
                SceneCaptureSample = captureSample,
                SceneCopyPalette = copyPalette
            };
        }
    }

    private static bool SameTexturePixels(NativeBattlePcTextureWindowPixels first, NativeBattlePcTextureWindowPixels second)
    {
        return first.Data.AsSpan().SequenceEqual(second.Data.AsSpan()) &&
            (first.SceneCopies is { } copies ? second.SceneCopies is { } other && copies.SequenceEqual(other) : second.SceneCopies is null);
    }

    private NativeBattleSceneCaptureSample RetainSceneCapture(NativeBattleSceneCapture capture, NativeBattleLogicalCommandReference command, ushort? texturePage)
    {
        capture = capture with { SampleCoordinates = null };
        capture.Validate();
        PublishImage();
        if (sceneCaptureSamples.TryGetValue(capture, out var retained) && ReferenceEquals(retained.Owner, Textures))
        {
            return retained.Sample with { Palette = RetainScenePalette(command, texturePage, Textures) };
        }
        ImmutableArray<int>.Builder snapshots = ImmutableArray.CreateBuilder<int>();
        for (int left = 0; left < capture.Width; left += 256)
        {
            int width = Math.Min(256, capture.Width - left);
            byte[] data = new byte[checked(width * capture.Height * 2)];
            for (int y = 0; y < capture.Height; y++)
            {
                Textures.ReadVramInto(2 * ((capture.Y + y) * 1024 + capture.X + left), data.AsSpan(y * width * 2, width * 2));
            }
            NativeBattlePcTextureWindowPixels pixels = new(capture.X + left, capture.Y, width, capture.Height, data.ToImmutableArray());
            pixels.Validate();
            snapshots.Add(textureSnapshots.Count);
            textureSnapshots.Add(pixels);
        }
        NativeBattleSceneCaptureSample sample = new(capture, snapshots.ToImmutable());
        sceneCaptureSamples[capture] = (Textures, sample);
        return sample with { Palette = RetainScenePalette(command, texturePage, Textures) };

    }

    private ImmutableArray<ushort> RetainScenePalette(NativeBattleLogicalCommandReference command, ushort? texturePage, NativeBattleEffectTextureState textureSource)
    {
        ushort page = texturePage ?? throw new InvalidDataException("Scene sampling requires its executed texture page.");
        int depth = page >> 7 & 3;
        if (depth == 3)
        {
            throw new NotSupportedException("Scene sampling requires a supported native texture depth.");
        }
        int length = depth == 0 ? 16 : depth == 1 ? 256 : 0;
        ushort clut = unchecked((ushort)(command.WordSpan[2] >> 16));
        int x = (clut & 63) * 16;
        int y = (clut & 0x7FFF) >> 6;
        if (sceneCapturePalettes.TryGetValue((clut, length), out var priorPalette) && ReferenceEquals(priorPalette.Owner, textureSource))
        {
            return priorPalette.Palette;
        }
        if (length > 0 && (x > 1024 - length || y >= 512))
        {
            throw new InvalidDataException("Scene sampling requires a complete native palette.");
        }
        ImmutableArray<ushort>.Builder palette = ImmutableArray.CreateBuilder<ushort>(length);
        for (int index = 0; index < length; index++)
        {
            palette.Add(textureSource.ReadVramWord(x + index, y));
        }
        ImmutableArray<ushort> retainedPalette = palette.MoveToImmutable();
        if (scenePaletteContents.TryGetValue(retainedPalette, out ImmutableArray<ushort> canonicalPalette))
        {
            retainedPalette = canonicalPalette;
        }
        else
        {
            scenePaletteContents.Add(retainedPalette, retainedPalette);
        }
        sceneCapturePalettes[(clut, length)] = (textureSource, retainedPalette);
        return retainedPalette;
    }

    private sealed class ScenePaletteContentComparer : IEqualityComparer<ImmutableArray<ushort>>
    {
        internal static readonly ScenePaletteContentComparer Instance = new();

        public bool Equals(ImmutableArray<ushort> first, ImmutableArray<ushort> second)
        {
            return first.AsSpan().SequenceEqual(second.AsSpan());
        }

        public int GetHashCode(ImmutableArray<ushort> palette)
        {
            HashCode hash = new();
            hash.Add(palette.Length);
            foreach (ushort word in palette)
            {
                hash.Add(word);
            }
            return hash.ToHashCode();
        }
    }

    public void EndBatch()
    {
        PublishImage();
        State = State with { Environment = NativeSoftwareDrawingEnvironment.EndBatch45D080(State.Environment), SceneCapture = null };
    }

    private void ExecuteState(NativeBattleLogicalCommandReference command)
    {
        if (command.WordSpan.Length != 1)
        {
            throw new InvalidDataException("Native drawing state command requires its single DWORD.");
        }
        uint value = command.WordSpan[0];
        switch (command.Opcode)
        {
            case 0xE1:
                State = State with { Texture = NativeSoftwareTexturePreparation.PrepareTPageState45B960(State.Texture, unchecked((ushort)value), out _) };
                break;
            case 0xE3:
                State = State with { Environment = NativeSoftwareDrawingEnvironment.WriteE345BA30(State.Environment, value), SceneCapture = command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.SourceEnvironment } capture ? capture : null };
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

    private void ExecuteImageTransfer(NativeBattleLogicalCommandReference command)
    {
        if (command.WordSpan.Length != 4)
        {
            throw new InvalidDataException("Native image transfer requires its four payload DWORDs.");
        }
        PublishImage();
        NativeBattleEffectTextureState priorTextures = Textures;
        ImmutableArray<NativeBattleSceneCopyRegion>? priorSceneCopies = Textures.SceneCopies;
        byte[] packet = new byte[20];
        for (int index = 0; index < command.WordSpan.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4 + index * 4), command.WordSpan[index]);
        }
        image ??= Textures.ReadVram();
        imageChanged = true;
        NativeSoftwareImageTransfer.Transfer45BB00(image, packet, (left, top, right, bottom) =>
        {
            PublishTransferredImage(left, top, right, bottom);
            Cache = (Cache ?? throw new NotSupportedException("Native image transfer requires its retained texture cache.")).Invalidate464850(left, top, right, bottom, priorTextures);
        });
        int sourceX = (int)(command.WordSpan[1] & 1023);
        int sourceY = (int)(command.WordSpan[1] >> 16 & 511);
        int destinationX = (int)(command.WordSpan[2] & 1023);
        int destinationY = (int)(command.WordSpan[2] >> 16 & 511);
        int width = Math.Min(Math.Min((int)(command.WordSpan[3] & 65535), 1024 - sourceX), 1024 - destinationX);
        int height = Math.Min(Math.Min((int)(command.WordSpan[3] >> 16), 512 - sourceY), 512 - destinationY);
        NativeBattleEffectVramCopy copy = new((short)sourceX, (short)sourceY, (short)width, (short)height, (short)destinationX, (short)destinationY);
        ImmutableArray<NativeBattleSceneCopyRegion>? sceneCopies = NativeBattleSceneCopyProvenance.Copy(priorSceneCopies, copy);
        if (width > 0 && height > 0 && command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.Copy } capture)
        {
            NativeBattleSceneCopyRegion region = new(capture, destinationX, destinationY, width, height, sourceX - capture.CopySourceX, sourceY - capture.CopySourceY);
            region.Validate();
            sceneCopies = NativeBattleSceneCopyProvenance.Overwrite(sceneCopies, destinationX, destinationY, width, height);
            sceneCopies = (sceneCopies ?? []).Add(region);
        }
        Textures = Textures with { SceneCopies = sceneCopies };
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
