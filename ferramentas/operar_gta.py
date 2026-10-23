"""Entrada normal do Windows para o GTA aberto, com processo/foco conferidos."""
import argparse
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import time

GAME = Path(r"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V\GTA5.exe")
u = ctypes.WinDLL("user32", use_last_error=True)
k = ctypes.WinDLL("kernel32", use_last_error=True)
u.GetForegroundWindow.restype = wintypes.HWND
u.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
u.SetForegroundWindow.argtypes = [wintypes.HWND]
u.WindowFromPoint.argtypes = [wintypes.POINT]
u.WindowFromPoint.restype = wintypes.HWND
u.GetAncestor.argtypes = [wintypes.HWND, wintypes.UINT]
u.GetAncestor.restype = wintypes.HWND
u.GetKeyboardLayout.argtypes = [wintypes.DWORD]
u.GetKeyboardLayout.restype = wintypes.HANDLE
u.VkKeyScanExW.argtypes = [wintypes.WCHAR, wintypes.HANDLE]
u.VkKeyScanExW.restype = ctypes.c_short
u.MapVirtualKeyExW.argtypes = [wintypes.UINT, wintypes.UINT, wintypes.HANDLE]
k.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
k.OpenProcess.restype = wintypes.HANDLE
k.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
k.CloseHandle.argtypes = [wintypes.HANDLE]


class Mouse(ctypes.Structure):
    _fields_ = [("dx", wintypes.LONG), ("dy", wintypes.LONG), ("data", wintypes.DWORD),
                ("flags", wintypes.DWORD), ("time", wintypes.DWORD), ("extra", ctypes.c_size_t)]


class Keyboard(ctypes.Structure):
    _fields_ = [("vk", wintypes.WORD), ("scan", wintypes.WORD), ("flags", wintypes.DWORD),
                ("time", wintypes.DWORD), ("extra", ctypes.c_size_t)]


class Hardware(ctypes.Structure):
    _fields_ = [("msg", wintypes.DWORD), ("low", wintypes.WORD), ("high", wintypes.WORD)]


class Value(ctypes.Union):
    _fields_ = [("mi", Mouse), ("ki", Keyboard), ("hi", Hardware)]


class Input(ctypes.Structure):
    _anonymous_ = ("value",)
    _fields_ = [("type", wintypes.DWORD), ("value", Value)]


u.SendInput.argtypes = [wintypes.UINT, ctypes.POINTER(Input), ctypes.c_int]
u.SendInput.restype = wintypes.UINT
KEYS = {"F3": 0x72, "F4": 0x73, "F5": 0x74, "F6": 0x75, "F7": 0x76, "F9": 0x78,
        "F10": 0x79, "Home": 0x24, "End": 0x23, "Enter": 0x0D,
        "Escape": 0x1B, "PageDown": 0x22, "PageUp": 0x21, "Delete": 0x2E}


def describe(hwnd):
    pid = wintypes.DWORD()
    thread = u.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    title = ctypes.create_unicode_buffer(1024)
    u.GetWindowTextW(hwnd, title, len(title))
    process = k.OpenProcess(0x1000, False, pid.value)
    executable = ""
    if process:
        try:
            buf = ctypes.create_unicode_buffer(32768)
            size = wintypes.DWORD(len(buf))
            if k.QueryFullProcessImageNameW(process, 0, buf, ctypes.byref(size)):
                executable = buf.value
        finally:
            k.CloseHandle(process)
    return {"hwnd": int(hwnd), "pid": pid.value, "thread": thread, "title": title.value,
            "exe": executable, "visible": bool(u.IsWindowVisible(hwnd))}


def game_window():
    windows = []
    callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

    @callback_type
    def callback(hwnd, unused):
        info = describe(hwnd)
        if info["visible"] and info["exe"].casefold() == str(GAME).casefold():
            windows.append(info)
        return True

    u.EnumWindows(callback, 0)
    if len(windows) != 1:
        raise RuntimeError("A janela visível do GTA5 não é única; nenhuma entrada enviada.")
    return windows[0]


def no_held_keys():
    if any(u.GetAsyncKeyState(vk) & 0x8000 for vk in (0x10, 0x11, 0x12, 0x5B, 0x5C)):
        raise RuntimeError("Há uma tecla modificadora pressionada; entrada cancelada.")


def foreground_hwnd():
    # NULL durante troca de janela significa foco desconhecido; nunca autoriza entrada.
    return int(u.GetForegroundWindow() or 0)


def focus(window, icon):
    no_held_keys()
    hwnd = window["hwnd"]
    if foreground_hwnd() == hwnd:
        return
    if u.SetForegroundWindow(hwnd):
        time.sleep(0.3)
    if foreground_hwnd() != hwnd and icon:
        # Clique de interface comum no ícone observado em captura de tela.
        point = wintypes.POINT(*icon)
        ancestor = u.GetAncestor(u.WindowFromPoint(point), 2)
        name = ctypes.create_unicode_buffer(256)
        u.GetClassNameW(ancestor, name, len(name))
        if name.value not in ("Shell_TrayWnd", "Shell_SecondaryTrayWnd"):
            raise RuntimeError("O ponto observado não está na barra de tarefas; clique cancelado.")
        previous = wintypes.POINT()
        u.GetCursorPos(ctypes.byref(previous))
        if not u.SetCursorPos(point.x, point.y):
            raise RuntimeError("Não foi possível posicionar o cursor.")
        try:
            events = (Input * 2)()
            events[0].type = events[1].type = 0
            events[0].mi = Mouse(0, 0, 0, 2, 0, 0)
            events[1].mi = Mouse(0, 0, 0, 4, 0, 0)
            if u.SendInput(2, events, ctypes.sizeof(Input)) != 2:
                raise RuntimeError("Clique normal recusado pelo Windows.")
            time.sleep(0.5)
        finally:
            u.SetCursorPos(previous.x, previous.y)
    if foreground_hwnd() != hwnd:
        raise RuntimeError("O GTA não recebeu foco; nenhuma tecla enviada.")
    if describe(hwnd)["exe"].casefold() != str(GAME).casefold():
        raise RuntimeError("O processo da janela mudou; entrada cancelada.")


def focus_game_window(window):
    u.IsIconic.argtypes = [wintypes.HWND]
    u.IsIconic.restype = wintypes.BOOL
    if u.IsIconic(window["hwnd"]):
        if describe(window["hwnd"])["exe"].casefold() != str(GAME).casefold():
            raise RuntimeError("Processo mudou; restauracao cancelada.")
        u.ShowWindow.argtypes = [wintypes.HWND, ctypes.c_int]
        u.ShowWindow.restype = wintypes.BOOL
        u.ShowWindow(window["hwnd"], 9)  # SW_RESTORE: reabrir a janela minimizada.
        time.sleep(0.5)
        if u.IsIconic(window["hwnd"]):
            raise RuntimeError("Janela permaneceu minimizada; entrada cancelada.")
    # Fallback humano: clicar somente no ponto que pertence ao GTA visivel.
    try:
        focus(window, None)
        return
    except RuntimeError:
        if describe(window["hwnd"])["exe"].casefold() != str(GAME).casefold():
            raise RuntimeError("Processo mudou; clique cancelado.")
    no_held_keys()
    rect = wintypes.RECT()
    u.GetWindowRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]
    u.GetWindowRect.restype = wintypes.BOOL
    if not u.GetWindowRect(window["hwnd"], ctypes.byref(rect)):
        raise RuntimeError("Janela nao tem retangulo valido; clique cancelado.")
    point = wintypes.POINT(rect.left + 45, rect.top + 35)
    if int(u.GetAncestor(u.WindowFromPoint(point), 2) or 0) != window["hwnd"]:
        raise RuntimeError("Ponto nao pertence ao GTA; clique cancelado.")
    previous = wintypes.POINT()
    u.GetCursorPos(ctypes.byref(previous))
    if not u.SetCursorPos(point.x, point.y):
        raise RuntimeError("Windows recusou posicionamento do cursor.")
    try:
        events = (Input * 2)()
        events[0].type = events[1].type = 0
        events[0].mi = Mouse(0, 0, 0, 2, 0, 0)
        events[1].mi = Mouse(0, 0, 0, 4, 0, 0)
        if u.SendInput(2, events, ctypes.sizeof(Input)) != 2:
            raise RuntimeError("Clique normal recusado; nao repetir.")
        time.sleep(0.5)
    finally:
        u.SetCursorPos(previous.x, previous.y)
    focus(window, None)


def keyboard_event(vk, down):
    event = Input(type=1)
    event.ki = Keyboard(vk, 0, 0 if down else 2, 0, 0)
    if u.SendInput(1, ctypes.byref(event), ctypes.sizeof(Input)) != 1:
        raise RuntimeError("Entrada normal recusada pelo Windows; não repetir nem contornar.")


def shortcut_event(vk, down, layout):
    # Win e uma tecla estendida; usar seus scancodes fisicos na mesma API normal.
    scan = u.MapVirtualKeyExW(vk, 0, layout)
    if not scan:
        raise RuntimeError("Tecla sem scancode neste layout; entrada cancelada.")
    flags = 8 | (1 if vk in (0x5B, 0x5C, 0xA3, 0xA5) else 0) | (0 if down else 2)
    event = Input(type=1)
    event.ki = Keyboard(0, scan, flags, 0, 0)
    if u.SendInput(1, ctypes.byref(event), ctypes.sizeof(Input)) != 1:
        raise RuntimeError("Entrada normal recusada pelo Windows; nao repetir.")


def press(window, keys):
    for _focus_wait in range(10):
        if foreground_hwnd() != 0:
            break
        time.sleep(0.05)
    if foreground_hwnd() != window["hwnd"]:
        raise RuntimeError("Foco mudou antes da tecla; cancelado.")
    held = []
    physical_shortcut = any(vk in (0x5B, 0x5C) for vk in keys)
    layout = u.GetKeyboardLayout(window["thread"]) if physical_shortcut else None
    send = (lambda vk, down: shortcut_event(vk, down, layout)) if physical_shortcut else keyboard_event
    try:
        for vk in keys:
            send(vk, True)
            held.append(vk)
        time.sleep(0.16)
    finally:
        for vk in reversed(held):
            send(vk, False)
    time.sleep(0.18)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("estado", "tecla", "reload-texto", "gravar-atalho", "gamebar"))
    parser.add_argument("key", nargs="?", choices=tuple(KEYS))
    parser.add_argument("--icone", nargs=2, type=int)
    args = parser.parse_args()
    window = game_window()
    if args.action == "estado":
        print(json.dumps({"game": window, "foreground": describe(u.GetForegroundWindow())}, ensure_ascii=False))
        return
    focus(window, args.icone)
    if args.action == "tecla":
        if not args.key:
            parser.error("tecla exige nome")
        press(window, [KEYS[args.key]])
    elif args.action == "gravar-atalho":
        press(window, [0x5B, 0x12, 0x52])
    elif args.action == "gamebar":
        press(window, [0x5B, 0x47])
    else:
        # Único texto permitido. O chamador precisa conferir o console F4 antes.
        layout = u.GetKeyboardLayout(window["thread"])
        for char in "Reload()":
            code = u.VkKeyScanExW(char, layout)
            if code < 0 or (code >> 8) & ~1:
                raise RuntimeError("Layout não permite esse texto com Shift; cancelado.")
            press(window, ([0x10] if code & 0x100 else []) + [code & 0xFF])
    print(json.dumps({"sent": args.action, "key": args.key, "game_pid": window["pid"],
                      "GTA_foreground": foreground_hwnd() == window["hwnd"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
