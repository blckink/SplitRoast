#include <windows.h>
#include <stdio.h>

int main() {
    HMODULE combase = LoadLibraryA("combase.dll");
    if (combase) {
        printf("combase loaded\n");
        void* pRoGetActivationFactory = GetProcAddress(combase, "RoGetActivationFactory");
        void* pWindowsGetStringRawBuffer = GetProcAddress(combase, "WindowsGetStringRawBuffer");
        if (pRoGetActivationFactory) printf("RoGetActivationFactory found\n");
        if (pWindowsGetStringRawBuffer) printf("WindowsGetStringRawBuffer found\n");
    }
    return 0;
}
