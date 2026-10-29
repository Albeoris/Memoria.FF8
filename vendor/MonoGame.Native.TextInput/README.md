# Native Unicode and IME text input

`TextInput.cpp` adds `Memoria_TextInput_SetCallback(SDL_Window*, callback)` to the locked DX12 and Linux Vulkan runtimes. Existing MonoGame exports remain unchanged. The callback receives `(committed, utf8)`; zero identifies composition text and one identifies a complete UTF-8 text-input event. An empty composition ends composition. The caller copies callback strings before returning, retains the delegate while registered and detaches with a null callback before unloading.

The bridge watches the same statically linked SDL2 event queue as the graphics host, filters by the supplied SDL window ID and leaves events in the queue. It supports ordinary and extended composition events and passes complete committed UTF-8, including supplementary Unicode scalars that the stock native MonoGame character event drops. SDL3 services used for controllers, dialogs and clipboard cannot supply these window events.

Windows authoring observes its owning HWND with a Win32 subclass and IMM composition/result messages. On native Windows hosts `GameWindow.Handle` is an `SDL_Window*`, so the adapter resolves the focused current-process HWND on the GUI thread. The shared UI library contains no operating-system calls. The stock macOS native runtime has no composition bridge; the host reports that specific unsupported capability rather than treating committed character events as IME support.

Build the runtime using its existing recipe. The following optional smoke target uses SDL's dummy video driver and requires no display or game resources:

```powershell
cmake --build artifacts/native-dx12-build/build --config Release --target memoria_text_input_smoke
./artifacts/native-dx12-build/build/Release/memoria_text_input_smoke.exe
```

For the Linux recipe, build `memoria_text_input_smoke` in its configured CMake build directory and run that executable. The smoke checks composition, complete Japanese/emoji UTF-8, window filtering and preservation of queued SDL events. It does not establish a live IME candidate-window acceptance result.
