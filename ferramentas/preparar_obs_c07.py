"""Prepara somente arquivos do OBS portatil oficial; nao abre app nem captura."""
from __future__ import annotations

import ctypes
import hashlib
import json
from pathlib import Path
import secrets
import uuid

import operar_gta as gta

ROOT = Path(__file__).resolve().parents[1]
OBS = ROOT / "dependencias/obs-studio"
CONFIG = OBS / "config/obs-studio"
ZIP_HASH = "4D6E40E3AB155F56B30DE517380566A206D74B63CDF5AD49AA596924768F97E1"


def write(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8", newline="\n") as stream:
        stream.write(content)


def write_json(path: Path, content: dict) -> None:
    write(path, json.dumps(content, ensure_ascii=False, indent=2) + "\n")


def main() -> None:
    archive = ROOT / "dependencias/OBS-Studio-32.2.2-Windows-x64.zip"
    with archive.open("rb") as stream:
        if hashlib.file_digest(stream, "sha256").hexdigest().upper() != ZIP_HASH:
            raise ValueError("ZIP nao corresponde ao digest oficial GitHub.")
    window = gta.game_window()  # Apenas EnumWindows e consultas; nenhum foco/input.
    window_class = ctypes.create_unicode_buffer(1024)
    gta.u.GetClassNameW(window["hwnd"], window_class, len(window_class))
    selected = ":".join((window["title"], window_class.value, Path(window["exe"]).name))
    capture_path = Path.home() / "Videos/Captures"
    if not capture_path.is_dir():
        raise ValueError("Pasta real de Captures ausente.")
    write(OBS / "portable_mode.txt", "")
    write(CONFIG / "global.ini", "[General]\nLastVersion=537002496\n\n[Locations]\n")
    write(CONFIG / "user.ini", """[General]
FirstRun=true
Language=pt-BR
ConfirmOnExit=true
EnableAutoUpdates=false

[Basic]
Profile=GTA C07
ProfileDir=GTA_C07
SceneCollection=GTA C07
SceneCollectionFile=GTA_C07
ConfigOnNewProfile=false

[BasicWindow]
PreviewEnabled=false
SysTrayEnabled=true
SysTrayWhenStarted=true
WarnBeforeStartingStream=true
WarnBeforeStoppingStream=true
WarnBeforeStoppingRecord=true
RecordWhenStreaming=false
KeepRecordingWhenStreamStops=false
""")
    def profile(name: str, encoder: str) -> str:
        return f"""[General]
Name={name}

[Video]
BaseCX=1920
BaseCY=1080
OutputCX=1920
OutputCY=1080
FPSType=0
FPSCommon=30
ScaleType=bicubic
ColorFormat=NV12
ColorSpace=709
ColorRange=Partial

[Output]
Mode=Advanced
FilenameFormatting=GTA5_OBS_EP01_C07_%CCYY-%MM-%DD_%hh-%mm-%ss
OverwriteIfExists=false

[AdvOut]
RecType=Standard
RecFilePath={capture_path.as_posix()}
RecFormat2=mp4
RecEncoder={encoder}
RecAudioEncoder=ffmpeg_aac
RecTracks=1
RecUseRescale=false
RecRB=false
Track1Name=Silencio
Track1Bitrate=128

[Audio]
SampleRate=48000
ChannelSetup=Stereo
DesktopAudioDevice1=disabled
DesktopAudioDevice2=disabled
AuxAudioDevice1=disabled
AuxAudioDevice2=disabled
AuxAudioDevice3=disabled

[Panels]
CookieId={secrets.token_hex(16)}
"""
    write(CONFIG / "basic/profiles/GTA_C07/basic.ini", profile("GTA C07", "obs_x264"))
    write_json(CONFIG / "basic/profiles/GTA_C07/recordEncoder.json", {
        "rate_control": "CRF", "crf": 20, "preset": "ultrafast",
        "profile": "high", "keyint_sec": 2})
    write(CONFIG / "basic/profiles/GTA_C07_QSV/basic.ini", profile("GTA C07 QSV candidato", "obs_qsv11_v2"))
    write_json(CONFIG / "basic/profiles/GTA_C07_QSV/recordEncoder.json", {
        "rate_control": "CQP", "qpi": 20, "qpp": 22, "qpb": 24,
        "target_usage": "TU4", "profile": "high", "keyint_sec": 2})
    source_uuid, scene_uuid = str(uuid.uuid4()), str(uuid.uuid4())
    capture = {"name": "GTA Legacy", "uuid": source_uuid, "id": "window_capture",
        "versioned_id": "window_capture", "settings": {"window": selected,
        "method": 2, "priority": 1, "cursor": False, "capture_audio": False,
        "client_area": True, "force_sdr": True, "compatibility": False},
        "mixers": 0, "muted": True, "volume": 0.0, "enabled": True,
        "flags": 0, "hotkeys": {}, "filters": [], "private_settings": {}}
    scene = {"name": "GTA C07", "uuid": scene_uuid, "id": "scene", "versioned_id": "scene",
        "settings": {"id_counter": 1, "items": [{"name": "GTA Legacy", "source_uuid": source_uuid,
            "id": 1, "visible": True, "locked": True, "rot": 0.0,
            "pos": {"x": 0.0, "y": 0.0}, "scale": {"x": 1.0, "y": 1.0},
            "align": 5, "bounds_type": 0, "bounds_align": 0,
            "bounds": {"x": 0.0, "y": 0.0}, "crop_left": 0,
            "crop_top": 0, "crop_right": 0, "crop_bottom": 0}]},
        "mixers": 0, "muted": True, "volume": 0.0, "enabled": True,
        "hotkeys": {}, "filters": [], "private_settings": {}}
    write_json(CONFIG / "basic/scenes/GTA_C07.json", {
        "name": "GTA C07", "current_scene": "GTA C07", "current_program_scene": "GTA C07",
        "scene_order": [{"name": "GTA C07"}], "sources": [capture, scene],
        "groups": [], "transitions": [], "current_transition": "Cut", "transition_duration": 0,
        "quick_transitions": [], "saved_projectors": [], "preview_locked": True,
        "DesktopAudioDevice1": None, "DesktopAudioDevice2": None,
        "AuxAudioDevice1": None, "AuxAudioDevice2": None, "AuxAudioDevice3": None})
    ws_path = CONFIG / "plugin_config/obs-websocket/config.json"
    write_json(ws_path, {"first_load": False, "server_enabled": True, "server_port": 4455,
        "alerts_enabled": False, "auth_required": True, "server_password": secrets.token_urlsafe(32)})
    # Nome do diretorio de modulo e obs-websocket (obs_module_config_path).
    write_json(OBS / "controle-local.json", {"url": "ws://127.0.0.1:4455",
        "passwordFile": str(ws_path), "passwordKey": "server_password",
        "perfil": "GTA C07", "colecao": "GTA C07", "cena": "GTA C07",
        "captura": "GTA Legacy", "janela": selected, "websocketBind": "interfaces IPv4; autenticacao obrigatoria",
        "firewallAlterado": False, "OBSIniciado": False,
        "QSVDisponivelConfirmado": False, "perfilQSV": "GTA C07 QSV candidato",
        "arquivoPrefixo": "GTA5_OBS_EP01_C07_", "pastaCapturas": str(capture_path),
        "zipOficialSha256": ZIP_HASH,
        "limite": "Arquivos preparados; conferir fonte, proporcao e encoder antes de gravar."})
    print(json.dumps({"configurado": str(OBS), "colecao": "GTA C07", "perfil": "GTA C07",
        "janela": selected, "video": "1920x1080/30fps; mp4; sem fontes de audio",
        "websocket": "autenticado; senha em arquivo local, nao exibida", "OBSIniciado": False}))


if __name__ == "__main__":
    main()
