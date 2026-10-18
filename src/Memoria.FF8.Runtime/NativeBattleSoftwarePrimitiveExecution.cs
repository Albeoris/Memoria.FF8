namespace Memoria.FF8.Runtime;

/// <summary>One draw batch over the retained PC environment, software raster state and shared VRAM image.</summary>
public sealed class NativeBattleSoftwarePrimitiveExecution : INativeBattleLogicalExecutionContext
{
    private byte[]? image;
    private bool imageChanged;

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
    public uint LiveMode => State.Environment.Derived?.Mode ?? throw new NotSupportedException("Native primitive dispatch requires its actual published drawing environment.");

    public void ApplyEnvironment45B660(int buffer)
    {
        NativeSoftwareDrawingEnvironmentBuffers buffers = State.EnvironmentBuffers ?? throw new NotSupportedException("Native environment publication requires its retained pair of initialized DRAWENV records.");
        NativeSoftwareDrawingEnvironmentApplication application = buffers.Apply45B660(buffer, State.Environment);
        State = State with { EnvironmentBuffers = application.Buffers, Environment = application.Environment };
        if (application.Invalidation is { } rectangle)
        {
            PublishImage();
            Cache = (Cache ?? throw new NotSupportedException("Native environment publication requires its retained PC texture cache.")).Invalidate464850(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom, Textures);
        }
    }

    public void ExecuteSoftware(NativeBattleLogicalCommand command)
    {
        RequireOrdinaryDebug();
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
        image ??= Textures.ReadVram();
        State = State.DrawPolygon(image, command, SoftwareTable);
        imageChanged = true;
    }

    public void ExecuteNormal(NativeBattleLogicalCommand command, NativeBattlePrimitiveDrawMode mode, byte pass)
    {
        RequireOrdinaryDebug();
        if (command.Opcode is >= 0xE1 and <= 0xE6)
        {
            ExecuteState(command);
        }
        else
        {
            NativeSoftwareEnvironmentDerived environment = State.Environment.Derived ?? throw new NotSupportedException("Native normal drawing requires its published environment.");
            if (environment.Mode != 1 || environment.Rectangle != new NativeSoftwareDrawingRectangle(0, 0, 320, 216) || environment.DrawPositionX != 0 || environment.DrawPositionY != 0)
            {
                throw new NotSupportedException("Native normal geometry requires the viewport scale and offset outputs for this drawing environment.");
            }
            if ((command.Opcode & 0xE4) == 0x24)
            {
                ushort page = mode.TexturePage ?? throw new NotSupportedException("Native textured command requires its actual page WORD.");
                NativeSoftwareTextureState prepared = NativeSoftwareTexturePreparation.PrepareTPage45B960(State.Texture, page).State;
                ushort clut = unchecked((ushort)(command.Words[2] >> 16));
                State = State with { Texture = NativeSoftwareTexturePreparation.PrepareClut45B920(prepared, clut, 1024 * 512 * 2) };
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
        return mode with { TexturePage = (ushort)page };
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
                break;
            case 0xE4:
                NativeSoftwareEnvironmentUpdate update = NativeSoftwareDrawingEnvironment.WriteE445BA60(State.Environment, value);
                State = State with { Environment = update.State };
                if (update.Invalidation is { } rectangle)
                {
                    PublishImage();
                    Cache = (Cache ?? throw new NotSupportedException("Native E4 requires its retained PC texture cache.")).Invalidate464850(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom, Textures);
                }
                break;
            case 0xE5:
                State = State with { Environment = NativeSoftwareDrawingEnvironment.WriteE545BAD0(State.Environment, value) };
                break;
        }
    }

    private void PublishImage()
    {
        if (!imageChanged || image is null)
        {
            return;
        }
        byte[] previous = Textures.ReadVram();
        NativeBattleEffectArena bank = new(Textures.VramRegions);
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
            bank = bank.Write(first, image.AsSpan(first, end - first));
        }
        Textures = Textures with { VramRegions = bank.Regions };
        imageChanged = false;
    }

    private void RequireOrdinaryDebug()
    {
        if (State.Environment.Debug is not 0)
        {
            throw new NotSupportedException("Native primitive debug duplicate deliveries require their explicit retained execution contract.");
        }
    }
}
