# Application icons

These transparent silver lion icons are generated from the title screen's existing vector contours with `TitleArtworkRenderer.CreateSilverCrestImage`. The full crest is centered in a square with a 3% margin. No installed game resources, fonts or external artwork tools are needed.

From the product root, regenerate all files with the configured .NET SDK:

```powershell
dotnet run --project tools/Memoria.ApplicationIcons -c Release -- Artwork/Application
```

The PNG set contains 16, 20, 24, 32, 40, 48, 64, 96, 128, 256, 512 and 1024 pixel sizes. `application.png` is the 256 pixel window/desktop icon. The ICO contains every PNG size through 256 pixels, including Windows scale factors. The ICNS contains standard and Retina representations through 1024 pixels. The 32-bit V4 BMP preserves alpha and is embedded as `Icon.bmp`, which MonoGame Native loads through its own windowing library.

Windows launcher, dispatcher, graphics hosts and music worker embed the ICO in their executable resources. Launcher windows use the same icon through Avalonia. Game windows set their platform icon when supported. macOS uses the ICNS through its application bundle; Linux desktop integration is described in the developer build instructions.
