#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shellapi.h>
#include <string>
#include <vector>
// Tiny native export-template launcher; .NET/native dependencies remain in Runtime.
static std::wstring Quote(const wchar_t* value) {
    std::wstring result=L"\""; unsigned slashes=0;
    for(const wchar_t* p=value;*p;++p) {
        if(*p==L'\\'){++slashes;continue;}
        if(*p==L'"'){result.append(slashes*2+1,L'\\');result+=L'"';}
        else{result.append(slashes,L'\\');result+=*p;}
        slashes=0;
    }
    result.append(slashes*2,L'\\');return result+L"\"";
}
int WINAPI wWinMain(HINSTANCE,HINSTANCE,PWSTR,int) {
    std::vector<wchar_t> path(32768);DWORD size=GetModuleFileNameW(nullptr,path.data(),static_cast<DWORD>(path.size()));
    if(!size||size==path.size())return 1;
    std::wstring root(path.data(),size);root.resize(root.find_last_of(L"\\/"));
    std::wstring player=root+L"\\Runtime\\ByteEngine.Player.exe";
    int count=0;auto args=CommandLineToArgvW(GetCommandLineW(),&count);std::wstring command=Quote(player.c_str());
    if(args){for(int i=1;i<count;i++)command+=L" "+Quote(args[i]);LocalFree(args);}
    STARTUPINFOW start{};start.cb=sizeof(start);PROCESS_INFORMATION process{};
    if(!CreateProcessW(player.c_str(),command.data(),nullptr,nullptr,FALSE,0,nullptr,root.c_str(),&start,&process)) {
        MessageBoxW(nullptr,L"The game runtime could not start. Keep the Runtime folder and Game.bytepak beside this launcher.",L"ByteEngine game",MB_OK|MB_ICONERROR);return 1;
    }
    CloseHandle(process.hThread);WaitForSingleObject(process.hProcess,INFINITE);DWORD code=1;GetExitCodeProcess(process.hProcess,&code);CloseHandle(process.hProcess);return static_cast<int>(code);
}
