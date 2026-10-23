"""Operador de retomada: APIs normais, ociosidade e diário persistente.

Gravar usa o runner existente, após primeira captura conferida na instância.
Snapshot não envia input. Um comando por vez.
"""
import argparse
import ctypes
from ctypes import wintypes
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import struct
import time
import uuid

import operar_gta as gta

gta.KEYS = dict(gta.KEYS, J=0x4A, F11=0x7A, Insert=0x2D)

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'backups' / 'retomada-autonoma'


class LastInput(ctypes.Structure):
    _fields_ = [('cbSize', wintypes.UINT), ('dwTime', wintypes.DWORD)]


def idle_guard(timeout=30):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        last = LastInput(ctypes.sizeof(LastInput), 0)
        if not gta.u.GetLastInputInfo(ctypes.byref(last)):
            raise RuntimeError('Windows não informou ociosidade; entrada cancelada')
        elapsed = (int(gta.k.GetTickCount()) - last.dwTime) & 0xffffffff
        if elapsed >= 1200:
            gta.no_held_keys()
            return elapsed
        time.sleep(.1)
    raise RuntimeError('Usuário ativo; nenhuma entrada enviada')


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()


def folder():
    active = BASE / 'atual.txt'
    path = Path(active.read_text('utf-8').strip())
    if not path.is_dir() or not path.resolve().is_relative_to(BASE.resolve()):
        raise RuntimeError('Checkpoint de retomada inválido')
    return path


def record(value):
    value['utc'] = datetime.now(timezone.utc).isoformat()
    with (folder() / 'operador.jsonl').open('a', encoding='utf-8') as stream:
        stream.write(json.dumps(value, ensure_ascii=False) + '\n')


def initialize():
    BASE.mkdir(exist_ok=True)
    path = BASE / (datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ') + '-' + uuid.uuid4().hex[:8])
    path.mkdir()
    records = []
    for rel in ('configuracoes/DiretorGTA.ini', 'RESUMO_CONTINUIDADE_CODEX.md'):
        source = ROOT / rel
        target = path / source.name
        shutil.copy2(source, target)
        assert sha(source) == sha(target)
        records.append({'arquivo': str(source), 'backup': str(target), 'sha256': sha(source)})
    (BASE / 'atual.txt').write_text(str(path), encoding='utf-8')
    record({'acao': 'checkpoint-inicial', 'arquivos': records})
    print(json.dumps({'backup': str(path)}, ensure_ascii=False))


def snapshot():
    window = gta.game_window()
    rect = wintypes.RECT()
    if not gta.u.GetWindowRect(window['hwnd'], ctypes.byref(rect)):
        raise RuntimeError('Retângulo da janela indisponível')
    path = folder() / ('tela-' + datetime.now(timezone.utc).strftime('%H%M%S%f') + '.bmp')
    grab_bmp(path, rect)
    value = {'acao': 'snapshot', 'arquivo': str(path), 'janela': window,
             'foreground': gta.describe(gta.u.GetForegroundWindow())}
    record(value)
    print(json.dumps(value, ensure_ascii=False))


def grab_bmp(path, rect):
    """Cópia normal do desktop visível por GDI; nenhum input/decoder."""
    gdi = ctypes.WinDLL('gdi32', use_last_error=True)
    gta.u.GetDC.argtypes = [wintypes.HWND]
    gta.u.GetDC.restype = wintypes.HDC
    gta.u.ReleaseDC.argtypes = [wintypes.HWND, wintypes.HDC]
    gdi.CreateCompatibleDC.argtypes = [wintypes.HDC]
    gdi.CreateCompatibleDC.restype = wintypes.HDC
    gdi.CreateCompatibleBitmap.argtypes = [wintypes.HDC, ctypes.c_int, ctypes.c_int]
    gdi.CreateCompatibleBitmap.restype = wintypes.HBITMAP
    gdi.SelectObject.argtypes = [wintypes.HDC, wintypes.HANDLE]
    gdi.SelectObject.restype = wintypes.HANDLE
    gdi.BitBlt.argtypes = [wintypes.HDC, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_int,
                          wintypes.HDC, ctypes.c_int, ctypes.c_int, wintypes.DWORD]
    gdi.GetDIBits.argtypes = [wintypes.HDC, wintypes.HBITMAP, wintypes.UINT, wintypes.UINT,
                             ctypes.c_void_p, ctypes.c_void_p, wintypes.UINT]
    gdi.DeleteObject.argtypes = [wintypes.HANDLE]
    gdi.DeleteDC.argtypes = [wintypes.HDC]
    w, h = rect.right - rect.left, rect.bottom - rect.top
    if not 0 < w <= 8192 or not 0 < h <= 8192:
        raise RuntimeError('Retângulo fora dos limites para snapshot')
    screen = gta.u.GetDC(None)
    dc = gdi.CreateCompatibleDC(screen)
    bmp = gdi.CreateCompatibleBitmap(screen, w, h)
    previous = gdi.SelectObject(dc, bmp)
    try:
        if not gdi.BitBlt(dc, 0, 0, w, h, screen, rect.left, rect.top, 0x00CC0020 | 0x40000000):
            raise RuntimeError('Windows recusou cópia da tela')
        gdi.SelectObject(dc, previous)
        info = struct.pack('<IiiHHIIiiII', 40, w, -h, 1, 32, 0, w * h * 4, 0, 0, 0, 0)
        header = ctypes.create_string_buffer(info)
        pixels = ctypes.create_string_buffer(w * h * 4)
        if gdi.GetDIBits(screen, bmp, 0, h, pixels, header, 0) != h:
            raise RuntimeError('Windows não forneceu todos os pixels')
        with path.open('xb') as stream:
            stream.write(struct.pack('<2sIHHI', b'BM', 54 + len(pixels.raw), 0, 0, 54))
            stream.write(info)
            stream.write(pixels.raw)
    finally:
        gdi.SelectObject(dc, previous)
        gdi.DeleteObject(bmp)
        gdi.DeleteDC(dc)
        gta.u.ReleaseDC(None, screen)


def physical(window, scan):
    if gta.foreground_hwnd() != window['hwnd']:
        raise RuntimeError('Foco mudou antes da tecla física')
    down = gta.Input(type=1)
    down.ki = gta.Keyboard(0, scan, 8, 0, 0)
    up = gta.Input(type=1)
    up.ki = gta.Keyboard(0, scan, 8 | 2, 0, 0)
    sent = False
    try:
        if gta.u.SendInput(1, ctypes.byref(down), ctypes.sizeof(gta.Input)) != 1:
            raise RuntimeError('Windows recusou input; não repetir')
        sent = True
        time.sleep(.16)
    finally:
        if sent and gta.u.SendInput(1, ctypes.byref(up), ctypes.sizeof(gta.Input)) != 1:
            raise RuntimeError('Windows recusou soltura da tecla própria')
    time.sleep(.3)


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('acao', choices=('iniciar', 'snapshot', 'foco', 'tecla', 'reload-texto', 'ensaio', 'montar', 'gravar'))
    ap.add_argument('valor', nargs='?')
    ap.add_argument('--parte', choices=('embarque_dinjer_chefe', 'embarque_djanira_decolagem'))
    a = ap.parse_args()
    if a.parte and (a.acao not in ('montar', 'gravar') or a.valor != 'EP07_C03'):
        ap.error('--parte exige montar/gravar EP07_C03')
    if a.acao == 'iniciar':
        initialize()
        return
    if a.acao == 'snapshot':
        snapshot()
        return
    if a.acao in ('montar', 'gravar'):
        import operar_sequencia_capturas as seq
        if not a.valor:
            ap.error('montar/gravar exige ID da cena')
        seq.montagem.identidade_cena(a.valor)
        original_press, original_focus = gta.press, gta.focus_game_window
        def guarded_runner_press(window, keys):
            idle_guard()
            return original_press(window, keys)
        def guarded_runner_focus(window):
            idle_guard()
            return original_focus(window)
        gta.press, gta.focus_game_window = guarded_runner_press, guarded_runner_focus
        record({'acao': a.acao + '-iniciar', 'cena': a.valor})
        result = seq.main([a.acao, '--cena', a.valor] + (['--parte', a.parte] if a.parte else []))
        record({'acao': a.acao + '-terminar', 'cena': a.valor, 'exitCode': result})
        raise SystemExit(result)
    if a.acao == 'ensaio':
        if a.valor not in ('87', '89'):
            ap.error('ensaio exige87 ou89')
        import importlib.util
        script = ROOT / 'backups/capturador-tecnico115/capturar_ensaio_tecnico.py'
        spec = importlib.util.spec_from_file_location('tecnico115', script)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        original_press, original_focus = gta.press, gta.focus_game_window
        def guarded_press(window, keys):
            idle_guard()
            return original_press(window, keys)
        def guarded_focus(window):
            idle_guard()
            return original_focus(window)
        gta.press, gta.focus_game_window = guarded_press, guarded_focus
        scene = 'teste_fila_' + a.valor
        config = Path(r'C:\Users\felipe.santiago\GTA_Diretor\catalogos') / mod.CONFIGS[scene]
        record({'acao': 'ensaio-iniciar', 'cena': scene, 'capturadorSha256': sha(script)})
        result = mod.main(['--cena', scene, '--config', str(config), '--timeout', '90'])
        record({'acao': 'ensaio-terminar', 'cena': scene, 'exitCode': result})
        raise SystemExit(result)
    idle = idle_guard()
    window = gta.game_window()
    gta.focus_game_window(window)
    if a.acao == 'tecla':
        if a.valor not in gta.KEYS:
            ap.error('Tecla inválida')
        if a.valor == 'Escape':
            physical(window, 0x01)
        else:
            gta.press(window, [gta.KEYS[a.valor]])
    elif a.acao == 'reload-texto':
        layout = gta.u.GetKeyboardLayout(window['thread'])
        for char in 'Reload()':
            code = gta.u.VkKeyScanExW(char, layout)
            if code < 0 or (code >> 8) & ~1:
                raise RuntimeError('Layout incompatível; texto cancelado')
            gta.press(window, ([0x10] if code & 0x100 else []) + [code & 0xff])
    record({'acao': a.acao, 'valor': a.valor, 'janela': window, 'idleAntesMs': idle,
            'focoConferido': gta.foreground_hwnd() == window['hwnd']})
    print(json.dumps({'acao': a.acao, 'valor': a.valor, 'pid': window['pid']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
