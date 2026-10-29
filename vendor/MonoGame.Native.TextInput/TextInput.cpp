#include "api_common.h"
#include <SDL.h>
#include <atomic>

// Additive host bridge: callbacks observe the SDL2 queue owned by MonoGame.
// They never consume events or synthesize gameplay input.
using MemoriaTextInputCallback = void (*)(int committed, const char* utf8);
static std::atomic<MemoriaTextInputCallback> textCallback{nullptr};
static std::atomic<Uint32> textWindow{0};

static int SDLCALL MemoriaTextWatch(void*, SDL_Event* event)
{
    auto callback = textCallback.load(std::memory_order_acquire);
    if (callback == nullptr)
    {
        return 1;
    }
    const auto window = textWindow.load(std::memory_order_relaxed);
    if (event->type == SDL_TEXTEDITING && event->edit.windowID == window)
    {
        callback(0, event->edit.text);
    }
#if SDL_VERSION_ATLEAST(2, 0, 22)
    else if (event->type == SDL_TEXTEDITING_EXT && event->editExt.windowID == window)
    {
        callback(0, event->editExt.text != nullptr ? event->editExt.text : "");
    }
#endif
    else if (event->type == SDL_TEXTINPUT && event->text.windowID == window)
    {
        callback(0, "");
        callback(1, event->text.text);
    }
    else if (event->type == SDL_WINDOWEVENT && event->window.windowID == window && event->window.event == SDL_WINDOWEVENT_FOCUS_LOST)
    {
        callback(0, "");
    }
    return 1;
}

MG_EXPORT int Memoria_TextInput_SetCallback(void* window, MemoriaTextInputCallback callback)
{
    textCallback.store(nullptr, std::memory_order_release);
    SDL_DelEventWatch(MemoriaTextWatch, nullptr);
    if (callback == nullptr)
    {
        return 1;
    }
    if (window == nullptr || (SDL_WasInit(SDL_INIT_VIDEO) & SDL_INIT_VIDEO) == 0)
    {
        return 0;
    }
    const auto id = SDL_GetWindowID(static_cast<SDL_Window*>(window));
    if (id == 0)
    {
        return 0;
    }
    textWindow.store(id, std::memory_order_relaxed);
    textCallback.store(callback, std::memory_order_release);
    SDL_AddEventWatch(MemoriaTextWatch, nullptr);
    SDL_StartTextInput();
    return 1;
}
