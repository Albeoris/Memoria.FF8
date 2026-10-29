#include "api_common.h"
#define SDL_MAIN_HANDLED
#include <SDL.h>
#include <string>
#include <vector>
#include <cstring>

extern "C" int Memoria_TextInput_SetCallback(void* window, void (*callback)(int, const char*));
static std::vector<std::string> commits;
static std::vector<std::string> compositions;
static void Capture(int committed, const char* text)
{
    (committed != 0 ? commits : compositions).emplace_back(text != nullptr ? text : "");
}
int main()
{
    SDL_SetMainReady();
    SDL_SetHint(SDL_HINT_VIDEODRIVER, "dummy");
    if (SDL_Init(SDL_INIT_VIDEO) != 0)
    {
        return 1;
    }
    SDL_Window* selected = SDL_CreateWindow("", 0, 0, 32, 32, SDL_WINDOW_HIDDEN);
    SDL_Window* other = SDL_CreateWindow("", 0, 0, 32, 32, SDL_WINDOW_HIDDEN);
    if (selected == nullptr || other == nullptr || Memoria_TextInput_SetCallback(selected, Capture) != 1)
    {
        return 2;
    }
    SDL_Event event{};
    while (SDL_PollEvent(&event))
    {
    }
    compositions.clear();
    event.type = SDL_TEXTEDITING;
    event.edit.windowID = SDL_GetWindowID(selected);
    std::strcpy(event.edit.text, u8"\u306B");
    SDL_PushEvent(&event);
    int expectedCompositions = 2;
    int expectedInputEvents = 3;
#if SDL_VERSION_ATLEAST(2, 0, 22)
    event = {};
    event.type = SDL_TEXTEDITING_EXT;
    event.editExt.windowID = SDL_GetWindowID(selected);
    event.editExt.text = SDL_strdup("extended composition longer than thirty-two UTF-8 bytes");
    SDL_PushEvent(&event);
    expectedCompositions++;
    expectedInputEvents++;
#endif
    event = {};
    event.type = SDL_TEXTINPUT;
    event.text.windowID = SDL_GetWindowID(other);
    std::strcpy(event.text.text, "wrong window");
    SDL_PushEvent(&event);
    event.text.windowID = SDL_GetWindowID(selected);
    std::strcpy(event.text.text, u8"\u65E5\U0001F600");
    SDL_PushEvent(&event);
    int inputEvents = 0;
    while (SDL_PollEvent(&event))
    {
#if SDL_VERSION_ATLEAST(2, 0, 22)
        if (event.type == SDL_TEXTEDITING_EXT)
        {
            inputEvents++;
            SDL_free(event.editExt.text);
        }
#endif
        if (event.type == SDL_TEXTINPUT || event.type == SDL_TEXTEDITING)
        {
            inputEvents++;
        }
    }
    bool valid = commits.size() == 1 && commits[0] == u8"\u65E5\U0001F600" && compositions.size() == expectedCompositions && compositions[0] == u8"\u306B" && compositions.back().empty() && inputEvents == expectedInputEvents;
    Memoria_TextInput_SetCallback(selected, nullptr);
    SDL_DestroyWindow(other);
    SDL_DestroyWindow(selected);
    SDL_Quit();
    return valid ? 0 : 3;
}
