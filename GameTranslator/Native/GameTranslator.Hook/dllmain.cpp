// GameTranslator.Hook - POC: user32!DrawTextW çağrılarını yakalayıp sözlükten Türkçe karşılığı çizer.
// Bağımlılık: MinHook (https://github.com/TsudaKageyu/minhook). x86/x64 oyunun mimarisiyle aynı derlenmeli.
// NOT: Yalnızca tek oyunculu / anti-cheat'siz oyunlarda ve kendi sorumluluğunuzda kullanın.
#include <windows.h>
#include <MinHook.h>
#include <fstream>
#include <string>
#include <unordered_map>

static std::unordered_map<std::wstring, std::wstring> g_dict;
using DrawTextW_t = int (WINAPI*)(HDC, LPCWSTR, int, LPRECT, UINT);
static DrawTextW_t g_orig = nullptr;

static std::wstring Utf8ToW(const std::string& s) {
    if (s.empty()) return {};
    int n = MultiByteToWideChar(CP_UTF8, 0, s.data(), (int)s.size(), nullptr, 0);
    std::wstring w(n, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, s.data(), (int)s.size(), w.data(), n);
    return w;
}

static void LoadDictionary(HMODULE self) {            // tr_dictionary.tsv: orijinal<TAB>çeviri
    wchar_t p[MAX_PATH]; GetModuleFileNameW(self, p, MAX_PATH);
    std::wstring path(p); path = path.substr(0, path.find_last_of(L'\\') + 1) + L"tr_dictionary.tsv";
    std::ifstream f(path); std::string line;
    while (std::getline(f, line)) {
        if (!line.empty() && line.back() == '\r') line.pop_back();
        auto t = line.find('\t'); if (t == std::string::npos) continue;
        g_dict[Utf8ToW(line.substr(0, t))] = Utf8ToW(line.substr(t + 1));
    }
}

static int WINAPI HookDrawTextW(HDC dc, LPCWSTR text, int n, LPRECT rc, UINT fmt) {
    if (text && (n == -1 || n > 0)) {
        auto it = g_dict.find(n == -1 ? std::wstring(text) : std::wstring(text, n));
        if (it != g_dict.end()) return g_orig(dc, it->second.c_str(), (int)it->second.size(), rc, fmt);
    }
    return g_orig(dc, text, n, rc, fmt);
}

static DWORD WINAPI InitThread(LPVOID self) {
    LoadDictionary((HMODULE)self);
    if (MH_Initialize() != MH_OK) return 1;
    MH_CreateHookApi(L"user32", "DrawTextW", (LPVOID)&HookDrawTextW, (LPVOID*)&g_orig);
    MH_EnableHook(MH_ALL_HOOKS);
    return 0;
}

BOOL APIENTRY DllMain(HMODULE h, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) { DisableThreadLibraryCalls(h); CreateThread(nullptr, 0, InitThread, h, 0, nullptr); }
    else if (reason == DLL_PROCESS_DETACH) { MH_DisableHook(MH_ALL_HOOKS); MH_Uninitialize(); }
    return TRUE;
}
