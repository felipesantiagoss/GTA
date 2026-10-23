"""Associar uma captura nova à tomada, preservando arquivos antigos e ambiguidades.

Não inicia gravação, não envia teclas e não altera configurações do Windows.
"""

from __future__ import annotations

import argparse
from contextlib import contextmanager
import ctypes
from ctypes import wintypes
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import re
import stat
import struct
import subprocess
import sys
import time
import uuid
import xml.etree.ElementTree as ET

from runtime_diretor import ErroRuntime, exigir_sem_vinculos


class ErroCaptura(RuntimeError):
    """Recusa segura; os MP4 originais permanecem disponíveis."""


class Ocupado(ErroCaptura):
    pass


class Cancelado(ErroCaptura):
    pass


CAPTURES_GUID = "{EDC0FE71-98D8-4F4A-B920-C8DC133CB165}"
VIDEO_GUID = "{18989B1D-99B5-455B-841C-AB7C74E4DDFC}"
SCENE_TEST = re.compile(r"teste_(?:json|dupla|falas|camera|cortes|ambiente|coreografia|elenco_(?:pobre|rica)|fila_(?:0[1-9]|[1-9][0-9]))\Z")
SCENE_PRODUCTION = re.compile(r"EP(0[1-9]|1[0-5])_C(0[1-9]|[1-9][0-9])\Z")
SCENE_SUPPLEMENTAL = frozenset(("EP01_EXTRA_IRACI_BAR", "EP01_EXTRA_DINJER_DEDO"))
MOVIE_NAME = re.compile(r"(?:GTA5|Grand Theft Auto V)(?:[ _.-].*)?\.mp4\Z", re.IGNORECASE)
SHA256 = re.compile(r"[0-9A-Fa-f]{64}\Z")
ROOT_ATTRIBUTES = {"versao", "id", "estado", "cenaId", "episodio", "numero", "pastaDados",
                   "pastaCapturas", "armadaUtc", "contagemUtc", "fimUtc", "prazoUtc", "duracaoMs"}


def _utc(value: str) -> datetime:
    if not re.fullmatch(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,7})?Z", value):
        raise ErroCaptura("Horário inválido: use ISO 8601 UTC terminado em Z.")
    try:
        return datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as error:
        raise ErroCaptura("Data UTC inválida na sessão.") from error


def _iso(value: datetime) -> str:
    return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def _hash(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest().upper()


def _path(value: str) -> Path:
    if not value or not Path(value).is_absolute() or value.startswith(("\\\\", "//")):
        raise ErroCaptura("O caminho precisa ser absoluto em um disco local.")
    if os.name == "nt" and ":" in value[2:]:
        raise ErroCaptura("Fluxos alternativos de arquivo não são permitidos.")
    try:
        path = exigir_sem_vinculos(Path(value))
    except ErroRuntime as error:
        raise ErroCaptura(str(error)) from error
    if any(part.casefold().startswith("onedrive") for part in path.parts):
        raise ErroCaptura("Capturas e dados precisam ficar fora do OneDrive.")
    if os.name == "nt":
        if ctypes.windll.kernel32.GetDriveTypeW(str(path.anchor)) != 3:
            raise ErroCaptura("Somente discos locais fixos são aceitos para organizar capturas.")
    return path


def _inside(path: Path, root: Path) -> Path:
    checked = _path(str(path))
    if not checked.is_relative_to(_path(str(root))):
        raise ErroCaptura(f"O caminho sai da pasta autorizada: {path}")
    return checked


def _plain_name(value: str) -> bool:
    return bool(value and len(value) <= 240 and value not in (".", "..") and
                not any(char in value for char in '/\\:\x00<>"|?*') and
                not value.endswith((" ", ".")))


def _integer(value: str, low: int, high: int, label: str) -> int:
    if not re.fullmatch(r"0|[1-9][0-9]*", value):
        raise ErroCaptura(f"{label}: inteiro decimal inválido.")
    number = int(value)
    if not low <= number <= high:
        raise ErroCaptura(f"{label} fora do intervalo {low}–{high}.")
    return number


def _xml(path: Path) -> ET.Element:
    if path.stat().st_size > 1024 * 1024:
        raise ErroCaptura("XML da sessão excede 1 MiB.")
    try:
        raw = path.read_bytes().decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise ErroCaptura("O protocolo de captura precisa ser UTF-8.") from error
    if "\x00" in raw or "<!DOCTYPE" in raw.upper() or "<!ENTITY" in raw.upper():
        raise ErroCaptura("DTD e entidades não são permitidas no protocolo de captura.")
    try:
        return ET.fromstring(raw)
    except ET.ParseError as error:
        raise ErroCaptura(f"XML de captura inválido: {error}") from error


@dataclass(frozen=True)
class Sessao:
    path: Path
    identifier: str
    scene: str
    episode: int
    number: int
    data: Path
    captures: Path
    armed: datetime
    countdown: datetime
    finished: datetime
    deadline: datetime
    duration_ms: int
    subtitle: Path
    subtitle_hash: str
    baseline: frozenset[str]


def ler_sessao(path: Path, pasta_capturas: Path | None = None) -> Sessao:
    path = _path(str(path))
    root = _xml(path)
    if root.tag != "captura" or set(root.attrib) != ROOT_ATTRIBUTES:
        raise ErroCaptura("Raiz/atributos do XML diferem do contrato de captura v1.")
    if root.get("versao") != "1" or root.get("estado") != "concluida":
        raise ErroCaptura("A sessão precisa estar concluída e usar a versão 1.")
    identifier = root.attrib["id"]
    if not re.fullmatch(r"[0-9a-f]{32}", identifier):
        raise ErroCaptura("Identificador de tomada deve ser um GUID minúsculo sem hífens.")
    data = _path(root.attrib["pastaDados"])
    profile = os.environ.get("USERPROFILE")
    if not profile or data != _path(profile) / "GTA_Diretor":
        raise ErroCaptura("PastaDados precisa ser exatamente %USERPROFILE%\\GTA_Diretor.")
    if path != data / "logs" / "capturas" / identifier / "sessao.xml":
        raise ErroCaptura("A sessão precisa estar em logs/capturas/<id>/sessao.xml no runtime.")
    captures = _path(root.attrib["pastaCapturas"])
    if pasta_capturas is None:
        diagnostic = diagnosticar()
        real = diagnostic.get("PastaCapturas")
        if not real:
            raise ErroCaptura("A pasta real de capturas do Windows não pôde ser verificada.")
        pasta_capturas = Path(real)
    if captures != _path(str(pasta_capturas)) or not captures.is_dir():
        raise ErroCaptura("PastaCapturas difere da pasta real de capturas verificada no Windows.")
    if captures.is_relative_to(data) or data.is_relative_to(captures):
        raise ErroCaptura("As pastas de capturas e do Diretor precisam ser distintas.")
    episode = _integer(root.attrib["episodio"], 0, 15, "Episódio")
    number = _integer(root.attrib["numero"], 0, 99, "Número")
    scene = root.attrib["cenaId"]
    production = SCENE_PRODUCTION.fullmatch(scene)
    if production:
        if episode != int(production[1]) or number != int(production[2]):
            raise ErroCaptura("ID da cena, episódio e número não correspondem.")
    elif scene in SCENE_SUPPLEMENTAL:
        if episode != 1 or number != 0:
            raise ErroCaptura("Extra autorizado exige episodio=1 e numero=0.")
    elif not ((SCENE_TEST.fullmatch(scene) or scene == "VINHETA_01") and episode == number == 0):
        raise ErroCaptura("ID técnico/compartilhado ou episódio da captura inválido.")
    armed, countdown, finished, deadline = (_utc(root.attrib[key]) for key in
                                          ("armadaUtc", "contagemUtc", "fimUtc", "prazoUtc"))
    if not armed <= countdown <= finished <= deadline:
        raise ErroCaptura("A ordem dos horários da tomada é inválida.")
    if (deadline - finished).total_seconds() > 600 or (deadline - armed).total_seconds() > 3600:
        raise ErroCaptura("Janela de captura longa demais; arme uma tomada recente.")
    duration_ms = _integer(root.attrib["duracaoMs"], 3001, 183000, "Duração com contagem")
    children = list(root)
    if len(children) != 2 or {child.tag for child in children} != {"srt", "antes"}:
        raise ErroCaptura("A sessão precisa conter exatamente srt e antes.")
    subtitle_element = root.find("srt")
    before = root.find("antes")
    if set(subtitle_element.attrib) != {"caminho", "sha256"} or list(subtitle_element) or before.attrib:
        raise ErroCaptura("Atributos de srt/antes inválidos.")
    subtitle = _inside(_path(subtitle_element.attrib["caminho"]), data / "cenas")
    if subtitle.parent != data / "cenas" or not re.fullmatch(re.escape(scene) + r"(?:_v[2-9][0-9]*|_v1[0-9]+)?\.srt", subtitle.name):
        raise ErroCaptura("A legenda precisa ter o ID da cena e ficar diretamente em Dados/cenas.")
    subtitle_hash = subtitle_element.attrib["sha256"].upper()
    if not SHA256.fullmatch(subtitle_hash):
        raise ErroCaptura("SHA256 da legenda inválido.")
    names = set()
    for entry in before:
        if entry.tag != "arquivo" or set(entry.attrib) != {"nome", "tamanho", "criadoUtc", "alteradoUtc"} or list(entry):
            raise ErroCaptura("Entrada do snapshot anterior inválida.")
        name = entry.attrib["nome"]
        if not _plain_name(name) or not name.casefold().endswith(".mp4") or name.casefold() in names:
            raise ErroCaptura("Nome duplicado ou inválido no snapshot anterior.")
        _integer(entry.attrib["tamanho"], 0, 2**63 - 1, "Tamanho do snapshot")
        _utc(entry.attrib["criadoUtc"])
        _utc(entry.attrib["alteradoUtc"])
        names.add(name.casefold())
    if len(names) > 10000:
        raise ErroCaptura("Snapshot anterior excede 10000 arquivos.")
    return Sessao(path, identifier, scene, episode, number, data, captures, armed, countdown,
                  finished, deadline, duration_ms, subtitle, subtitle_hash, frozenset(names))


def diagnosticar() -> dict:
    """Leitura do perfil corrente; ausência de acesso não significa ausência da Game Bar."""
    result = {"Versao": 1, "Plataforma": os.name, "PastaCapturas": None,
              "GameBar": None, "Avisos": []}
    if os.name != "nt":
        result["Avisos"].append("A detecção da Game Bar exige Windows.")
        return result
    import winreg

    def registry(hive, key, name):
        try:
            with winreg.OpenKey(hive, key) as handle:
                return winreg.QueryValueEx(handle, name)[0]
        except OSError:
            return None

    shell = r"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"
    descriptions = r"Software\Microsoft\Windows\CurrentVersion\Explorer\FolderDescriptions" + "\\" + CAPTURES_GUID
    redirected = registry(winreg.HKEY_CURRENT_USER, shell, CAPTURES_GUID)
    videos = registry(winreg.HKEY_CURRENT_USER, shell, "My Video") or registry(winreg.HKEY_CURRENT_USER, shell, VIDEO_GUID)
    relative = registry(winreg.HKEY_LOCAL_MACHINE, descriptions, "RelativePath")
    parent = registry(winreg.HKEY_LOCAL_MACHINE, descriptions, "ParentFolder")
    candidate = None
    if isinstance(redirected, str):
        candidate = os.path.expandvars(redirected)
        result["OrigemPasta"] = "User Shell Folders/Captures"
    elif isinstance(videos, str) and relative == "Captures" and str(parent).casefold() == VIDEO_GUID.casefold():
        candidate = str(Path(os.path.expandvars(videos)) / relative)
        result["OrigemPasta"] = "FolderDescriptions/Captures + User Shell Folders/My Video"
    if candidate:
        try:
            folder = _path(candidate)
            if folder.is_dir():
                result["PastaCapturas"] = str(folder)
            else:
                result["Avisos"].append("A pasta de capturas registrada ainda não existe.")
        except ErroCaptura as error:
            result["Avisos"].append(str(error))
    else:
        result["Avisos"].append("A pasta real de capturas não foi verificada neste perfil/processo.")
    result["GameDVR_Enabled"] = registry(winreg.HKEY_CURRENT_USER, r"System\GameConfigStore", "GameDVR_Enabled")
    result["AllowGameDVRUsuario"] = registry(winreg.HKEY_CURRENT_USER, r"Software\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR")
    result["AllowGameDVRMaquina"] = registry(winreg.HKEY_LOCAL_MACHINE, r"Software\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR")
    command = "Get-AppxPackage -Name Microsoft.XboxGamingOverlay | Select-Object Name,Version,Status,InstallLocation | ConvertTo-Json -Compress"
    try:
        process = subprocess.run([str(Path(os.environ.get("SYSTEMROOT", r"C:\Windows")) / "System32" / "WindowsPowerShell" / "v1.0" / "powershell.exe"),
                                  "-NoProfile", "-NonInteractive", "-Command", command],
                                 capture_output=True, timeout=25, creationflags=subprocess.CREATE_NO_WINDOW)
        text = process.stdout.decode("utf-8-sig", errors="replace").strip()
        if process.returncode == 0 and text:
            result["GameBar"] = json.loads(text)
        else:
            result["Avisos"].append("Pacote Game Bar não verificado; esta consulta pode estar isolada pelo sandbox.")
    except (OSError, subprocess.TimeoutExpired, ValueError) as error:
        result["Avisos"].append(f"Consulta da Game Bar indisponível: {error}")
    result["GravacaoTestada"] = False
    return result


if os.name == "nt":
    import msvcrt

    KERNEL = ctypes.WinDLL("kernel32", use_last_error=True)
    INVALID_HANDLE = ctypes.c_void_p(-1).value

    class FileInfo(ctypes.Structure):
        _fields_ = [("attributes", wintypes.DWORD), ("created", wintypes.FILETIME),
                    ("accessed", wintypes.FILETIME), ("written", wintypes.FILETIME),
                    ("volume", wintypes.DWORD), ("size_hi", wintypes.DWORD),
                    ("size_lo", wintypes.DWORD), ("links", wintypes.DWORD),
                    ("index_hi", wintypes.DWORD), ("index_lo", wintypes.DWORD)]

    KERNEL.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, ctypes.c_void_p,
                                  wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    KERNEL.CreateFileW.restype = wintypes.HANDLE
    KERNEL.CloseHandle.argtypes = [wintypes.HANDLE]
    KERNEL.CloseHandle.restype = wintypes.BOOL
    KERNEL.GetFileInformationByHandle.argtypes = [wintypes.HANDLE, ctypes.POINTER(FileInfo)]
    KERNEL.GetFileInformationByHandle.restype = wintypes.BOOL
    KERNEL.GetFinalPathNameByHandleW.argtypes = [wintypes.HANDLE, wintypes.LPWSTR, wintypes.DWORD, wintypes.DWORD]
    KERNEL.GetFinalPathNameByHandleW.restype = wintypes.DWORD
    KERNEL.SetFileInformationByHandle.argtypes = [wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p, wintypes.DWORD]
    KERNEL.SetFileInformationByHandle.restype = wintypes.BOOL


@contextmanager
def _exclusive(path: Path, *, delete: bool = False, lock: bool = False):
    """No Windows share=0 e remoção pelo mesmo handle, sem reabrir por caminho."""
    path = _path(str(path))
    if os.name != "nt":
        import fcntl
        with path.open("a+b" if lock else "rb") as stream:
            try:
                fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError as error:
                raise Ocupado(f"Arquivo ocupado: {path.name}") from error
            yield stream, lambda: path.unlink()
        return
    access = 0x80000000 | (0x40000000 if lock else 0) | (0x00010000 if delete else 0)
    handle = KERNEL.CreateFileW(str(path), access, 0, None, 4 if lock else 3, 0x00200000, None)
    if handle == INVALID_HANDLE:
        error = ctypes.get_last_error()
        if error in (32, 33):
            raise Ocupado(f"Arquivo ainda ocupado: {path.name}")
        raise ErroCaptura(f"Não foi possível abrir exclusivamente {path.name}: {ctypes.WinError(error)}")
    try:
        info = FileInfo()
        if not KERNEL.GetFileInformationByHandle(handle, ctypes.byref(info)):
            raise ErroCaptura("Não foi possível confirmar a identidade do arquivo aberto.")
        if info.attributes & 0x400 or info.attributes & 0x10 or info.links != 1:
            raise ErroCaptura("Vínculos, pastas e arquivos com múltiplos hard links não são aceitos.")
        buffer = ctypes.create_unicode_buffer(32768)
        count = KERNEL.GetFinalPathNameByHandleW(handle, buffer, len(buffer), 0)
        actual = buffer.value.removeprefix("\\\\?\\")
        if not count or count >= len(buffer) or Path(actual) != path:
            raise ErroCaptura("A identidade/caminho do arquivo mudou ao abrir; captura preservada.")
        descriptor = msvcrt.open_osfhandle(handle, os.O_BINARY | (os.O_RDWR if lock else os.O_RDONLY))
    except BaseException:
        KERNEL.CloseHandle(handle)
        raise
    with os.fdopen(descriptor, "r+b" if lock else "rb") as stream:
        def remove_same_handle():
            if not delete:
                raise ErroCaptura("Este handle não tem autorização de remoção.")
            disposition = wintypes.BOOL(True)
            if not KERNEL.SetFileInformationByHandle(handle, 4, ctypes.byref(disposition), ctypes.sizeof(disposition)):
                raise ErroCaptura(f"A cópia foi preservada, mas o Windows recusou remover o original: {ctypes.WinError(ctypes.get_last_error())}")
        yield stream, remove_same_handle


def _stream_hash(stream) -> str:
    stream.seek(0)
    value = hashlib.file_digest(stream, "sha256").hexdigest().upper()
    stream.seek(0)
    return value


def _boxes(stream, start: int, end: int):
    offset = start
    for _ in range(10000):
        if offset == end:
            return
        if end - offset < 8:
            raise ErroCaptura("MP4 incompleto: cabeçalho final truncado.")
        stream.seek(offset)
        size, kind = struct.unpack(">I4s", stream.read(8))
        header = 8
        if size == 1:
            if end - offset < 16:
                raise ErroCaptura("MP4 incompleto: tamanho estendido truncado.")
            size = struct.unpack(">Q", stream.read(8))[0]
            header = 16
        elif size == 0:
            size = end - offset
        if size < header or offset + size > end:
            raise ErroCaptura("MP4 incompleto: uma caixa ultrapassa os bytes gravados.")
        yield kind, offset + header, offset + size
        offset += size
    raise ErroCaptura("MP4 excede o limite de caixas da conferência.")


def validar_mp4(stream, duracao_minima_ms: int = 0) -> int:
    """Conferência estrutural de caixas; não afirma que o vídeo/áudio foi assistido."""
    stream.seek(0, os.SEEK_END)
    length = stream.tell()
    found = set()
    movie_duration = None
    for kind, start, end in _boxes(stream, 0, length):
        if kind == b"ftyp" and end - start < 8:
            raise ErroCaptura("MP4 sem identificação de formato completa.")
        if kind in (b"moov", b"mdat") and end == start:
            raise ErroCaptura("MP4 sem metadados/dados de mídia completos.")
        if kind == b"moov":
            for child, child_start, child_end in _boxes(stream, start, end):
                if child != b"mvhd":
                    continue
                if movie_duration is not None:
                    raise ErroCaptura("MP4 contém durações de filme duplicadas.")
                stream.seek(child_start)
                data = stream.read(min(32, child_end - child_start))
                if not data or data[0] not in (0, 1):
                    raise ErroCaptura("MP4 possui versão mvhd não suportada.")
                required = 20 if data[0] == 0 else 32
                if len(data) < required:
                    raise ErroCaptura("MP4 possui duração mvhd incompleta.")
                scale_offset = 12 if data[0] == 0 else 20
                scale = struct.unpack_from(">I", data, scale_offset)[0]
                duration = struct.unpack_from(">I" if data[0] == 0 else ">Q", data, scale_offset + 4)[0]
                if scale == 0 or duration == 0 or duration == (2**(32 if data[0] == 0 else 64) - 1):
                    raise ErroCaptura("MP4 ainda não tem duração final confiável.")
                # Até 100 ms cobre arredondamento do relógio de mídia/últimos quadros.
                if duration * 1000 + 100 * scale < duracao_minima_ms * scale:
                    raise ErroCaptura("MP4 curto demais para a cena e a contagem; original preservado.")
                movie_duration = duration * 1000 // scale
        found.add(kind)
    if not {b"ftyp", b"moov", b"mdat"}.issubset(found) or movie_duration is None:
        raise ErroCaptura("MP4 ainda não finalizado: faltam ftyp, moov ou mdat.")
    stream.seek(0)
    return movie_duration


def _cancel(session: Sessao) -> None:
    marker = _inside(session.path.with_name("cancelada.xml"), session.data)
    if marker.exists():
        raise Cancelado("Tomada cancelada; vídeo e legenda originais foram preservados.")


def _candidates(session: Sessao) -> list[Path]:
    candidates = []
    _path(str(session.captures))
    for source in session.captures.iterdir():
        if not MOVIE_NAME.fullmatch(source.name) or source.name.casefold() in session.baseline:
            continue
        source = _inside(source, session.captures)
        information = source.stat()
        if not stat.S_ISREG(information.st_mode):
            raise ErroCaptura("Uma captura candidata não é um arquivo comum.")
        created = getattr(information, "st_birthtime", information.st_ctime)
        modified = information.st_mtime
        if (session.armed.timestamp() <= created <= session.deadline.timestamp() and
                session.finished.timestamp() - 0.1 <= modified <= session.deadline.timestamp()):
            candidates.append(source)
    return candidates


def _mkdir(path: Path, root: Path):
    _inside(path, root).mkdir(parents=True, exist_ok=True)
    _inside(path, root)


def _copy(stream, path: Path, root: Path, expected: str):
    path = _inside(path, root)
    stream.seek(0)
    with path.open("xb") as output:
        while block := stream.read(1024 * 1024):
            output.write(block)
        output.flush()
        os.fsync(output.fileno())
    if _hash(path) != expected:
        raise ErroCaptura(f"Os bytes copiados não conferem: {path.name}")
    stream.seek(0)


def _json_new(path: Path, value: dict, root: Path):
    path = _inside(path, root)
    with path.open("x", encoding="utf-8", newline="\n") as output:
        json.dump(value, output, ensure_ascii=False, indent=2)
        output.write("\n")


def _destination(session: Sessao) -> tuple[Path, Path, Path, int]:
    folder = "COMPARTILHADOS" if session.scene == "VINHETA_01" else "TESTES" if session.episode == 0 else f"EP{session.episode:02d}"
    destination = session.data / "Serie" / "A_CALOTEIRA_1_TEMP" / folder
    _mkdir(destination, session.data)
    for version in range(1, 10001):
        stem = session.scene + ("" if version == 1 else f"_v{version}")
        video = _inside(destination / (stem + ".mp4"), session.data)
        subtitle = _inside(destination / (stem + ".srt"), session.data)
        reservation = _inside(destination / (stem + ".reserva"), session.data)
        if video.exists() or subtitle.exists():
            continue
        try:
            with reservation.open("x", encoding="ascii") as output:
                output.write(session.identifier)
        except FileExistsError:
            continue
        if video.exists() or subtitle.exists():
            # Um escritor estranho apareceu: a reserva fica como evidência, sem sobrescrever.
            continue
        return video, subtitle, reservation, version
    raise ErroCaptura("Limite de versões da cena atingido.")


def _organize_file(session: Sessao, source: Path) -> dict:
    _cancel(session)
    with _exclusive(source, delete=True) as (movie, remove_movie), _exclusive(session.subtitle) as (srt, _):
        _cancel(session)
        if _stream_hash(srt) != session.subtitle_hash:
            raise ErroCaptura("A legenda mudou desde F7; nenhuma captura será associada.")
        movie_duration = validar_mp4(movie, session.duration_ms)
        movie_hash = _stream_hash(movie)
        original_identity = _identity(movie)
        # Novo exame inclui outros candidatos surgidos durante a finalização.
        if _candidates(session) != [source]:
            raise ErroCaptura("Captura ambígua: apareceram múltiplos candidatos; todos foram preservados.")
        backup = session.data / "backups" / "capturas" / (session.identifier + "-" + uuid.uuid4().hex[:8])
        _mkdir(backup, session.data)
        _copy(movie, backup / "original.mp4", session.data, movie_hash)
        _copy(srt, backup / "original.srt", session.data, session.subtitle_hash)
        video, subtitle, reservation, version = _destination(session)
        manifest = backup / "manifesto.json"
        record = {"Versao": 1, "Estado": "copias_preparadas", "Id": session.identifier,
                  "CenaId": session.scene, "Episodio": session.episode, "Numero": session.number,
                  "VersaoTomada": version, "FonteVideo": str(source), "FonteLegenda": str(session.subtitle),
                  "Video": str(video), "Legenda": str(subtitle), "SHA256Video": movie_hash,
                  "SHA256Legenda": session.subtitle_hash, "BackupVideo": str(backup / "original.mp4"),
                  "BackupLegenda": str(backup / "original.srt"), "ArmadaUTC": _iso(session.armed),
                  "ContagemUTC": _iso(session.countdown), "FimUTC": _iso(session.finished),
                  "PrazoUTC": _iso(session.deadline), "DuracaoMs": session.duration_ms,
                  "DuracaoMP4Ms": movie_duration, "IdentidadeOrigem": original_identity,
                  "LegendaAlinhadaAoF7": True, "InicioVideoRelativoF7": "nao_inferido"}
        _json_new(manifest, record, session.data)
        try:
            _cancel(session)
            _copy(movie, video, session.data, movie_hash)
            _copy(srt, subtitle, session.data, session.subtitle_hash)
            _cancel(session)
            if _stream_hash(movie) != movie_hash or _stream_hash(srt) != session.subtitle_hash:
                raise ErroCaptura("Uma origem mudou durante a cópia; original preservado.")
            _inside(source, session.captures)
            if _candidates(session) != [source]:
                raise ErroCaptura("Outro MP4 apareceu antes da conclusão; original preservado.")
            _cancel(session)
            result = {"status": "salvo", "mensagem": f"Tomada salva: {video.name}", "video": str(video),
                      "legenda": str(subtitle), "manifesto": str(manifest), "sha256Video": movie_hash,
                      "sha256Legenda": session.subtitle_hash, "versaoTomada": str(version)}
            # Ledger durável antes da remoção. Retry recupera o par se faltar resultado.xml.
            _json_new(backup / "conclusao.json", {"Estado": "par_conferido", **result}, session.data)
            if reservation.read_text(encoding="ascii") == session.identifier:
                reservation.unlink()
            _json_new(backup / "compromisso.json", {"Versao": 1, "Id": session.identifier,
                      "CenaId": session.scene, "FonteVideo": str(source), "FonteLegenda": str(session.subtitle),
                      "IdentidadeOrigem": original_identity, "Resultado": result}, session.data)
            _cancel(session)
            remove_movie()
            # Nenhuma escrita em arquivo depois de marcar a remoção pelo handle.
            return result
        except BaseException as error:
            _json_new(backup / "falha.json", {"Estado": "cancelado" if isinstance(error, Cancelado) else "erro",
                                            "Erro": str(error), "OriginalPreservado": True}, session.data)
            raise


def _identity(stream) -> dict:
    information = os.fstat(stream.fileno())
    return {"Disco": information.st_dev, "Arquivo": information.st_ino, "Tamanho": information.st_size,
            "CriacaoNs": getattr(information, "st_birthtime_ns", information.st_ctime_ns),
            "EscritaNs": information.st_mtime_ns}


def _recover_committed(session: Sessao) -> dict | None:
    directory = _inside(session.data / "backups" / "capturas", session.data)
    if not directory.is_dir():
        return None
    ledgers = []
    for folder in directory.glob(session.identifier + "-*"):
        journal = _inside(folder / "compromisso.json", session.data)
        if journal.is_file():
            ledgers.append(journal)
    if not ledgers:
        return None
    if len(ledgers) != 1:
        raise ErroCaptura("Múltiplos registros de conclusão para a mesma tomada; conferir backups.")
    journal = ledgers[0]
    record = json.loads(journal.read_text(encoding="utf-8"))
    result = record["Resultado"]
    if record["Id"] != session.identifier or record["CenaId"] != session.scene or record["FonteLegenda"] != str(session.subtitle):
        raise ErroCaptura("Registro de conclusão não corresponde à tomada atual.")
    if result["status"] != "salvo" or result["sha256Legenda"] != session.subtitle_hash:
        raise ErroCaptura("Registro de conclusão possui legenda/status divergente.")
    if _inside(_path(result["manifesto"]), session.data) != journal.with_name("manifesto.json"):
        raise ErroCaptura("Manifesto divergente no registro de conclusão.")
    for key, digest in (("video", "sha256Video"), ("legenda", "sha256Legenda")):
        file = _inside(_path(result[key]), session.data / "Serie" / "A_CALOTEIRA_1_TEMP")
        if _hash(file) != result[digest]:
            raise ErroCaptura("O par concluído foi alterado; origem e backups preservados.")
    source = _inside(_path(record["FonteVideo"]), session.captures)
    if source.parent != session.captures or not MOVIE_NAME.fullmatch(source.name) or source.name.casefold() in session.baseline:
        raise ErroCaptura("Origem divergente no registro de conclusão.")
    _cancel(session)
    if source.exists():
        with _exclusive(source, delete=True) as (stream, remove_source):
            if _identity(stream) != record["IdentidadeOrigem"] or _stream_hash(stream) != result["sha256Video"]:
                raise ErroCaptura("O nome de origem agora identifica outro arquivo; não remover.")
            _cancel(session)
            remove_source()
    return result


def _result_write(path: Path, session: Sessao, result: dict):
    path = _inside(path, session.data)
    node = ET.Element("resultado", {"versao": "1", "id": session.identifier, **result})
    raw = ET.tostring(node, encoding="utf-8", xml_declaration=True)
    if path.exists():
        # Retry autorizado conserva o resultado anterior antes da substituição atômica.
        backup = path.with_name("resultado-anterior-" + uuid.uuid4().hex + ".xml")
        with path.open("rb") as stream:
            _copy(stream, backup, session.data, _hash(path))
    temporary = path.with_name(".resultado-" + uuid.uuid4().hex + ".tmp")
    with temporary.open("xb") as stream:
        stream.write(raw)
        stream.flush()
        os.fsync(stream.fileno())
    _inside(path, session.data)
    os.replace(temporary, path)


def _previous_saved(session: Sessao) -> dict | None:
    path = _inside(session.path.with_name("resultado.xml"), session.data)
    if not path.exists():
        return None
    root = _xml(path)
    if root.tag != "resultado" or root.get("id") != session.identifier:
        raise ErroCaptura("Resultado existente pertence a outra tomada.")
    if root.get("status") != "salvo":
        return None
    for key, digest in (("video", "sha256Video"), ("legenda", "sha256Legenda")):
        file = _inside(_path(root.attrib[key]), session.data / "Serie" / "A_CALOTEIRA_1_TEMP")
        if _hash(file) != root.attrib[digest]:
            raise ErroCaptura("Uma tomada já salva foi alterada; não será sobrescrita.")
    if root.get("sha256Legenda") != session.subtitle_hash:
        raise ErroCaptura("A legenda não corresponde ao resultado já salvo.")
    return {key: value for key, value in root.attrib.items() if key not in ("versao", "id")}


def repetir(path: Path, *, pasta_capturas: Path | None = None, intervalo: float = 1.0,
            espera_maxima: float = 120.0) -> dict:
    """End retoma a mesma sessão concluída; não reconstrói snapshot nem escolhe outra tomada."""
    path = _path(str(path))
    if path.stat().st_size > 1024 * 1024:
        raise ErroCaptura("XML da sessão excede 1 MiB.")
    original = path.read_bytes()
    original_hash = hashlib.sha256(original).hexdigest().upper()
    session = ler_sessao(path, pasta_capturas)
    lock_path = _inside(session.path.with_name("organizador.lock"), session.data)
    try:
        with _exclusive(lock_path, lock=True):
            try:
                _cancel(session)
                # A recuperação idempotente de um par comprometido/salvo não exige
                # ampliar a janela de procura por outro MP4.
                previous = _previous_saved(session)
                if previous:
                    return previous
                committed = _recover_committed(session)
                if committed:
                    _result_write(session.path.with_name("resultado.xml"), session, committed)
                    return committed
                now = datetime.now(timezone.utc)
                latest = session.finished + timedelta(minutes=10)
                if now >= latest:
                    raise ErroCaptura("Passaram dez minutos desta tomada; o MP4 original permanece na pasta de capturas.")
                if _hash(path) != original_hash:
                    raise ErroCaptura("A sessão mudou durante a leitura; End preservou a edição e não ampliou o prazo.")
                deadline = min(now + timedelta(minutes=3), latest)
                if deadline > session.deadline:
                    root = ET.fromstring(original.decode("utf-8-sig"))
                    root.set("prazoUtc", _iso(deadline))
                    backup = _inside(path.with_name("sessao.backup-" + uuid.uuid4().hex + ".xml"), session.data)
                    _copy(io.BytesIO(original), backup, session.data, original_hash)
                    temporary = _inside(path.with_name(".sessao-" + uuid.uuid4().hex + ".tmp"), session.data)
                    with temporary.open("xb") as output:
                        output.write(ET.tostring(root, encoding="utf-8", xml_declaration=True))
                        output.flush()
                        os.fsync(output.fileno())
                    _cancel(session)
                    if _hash(path) != original_hash:
                        raise ErroCaptura("A sessão foi editada durante o backup; End preservou a edição e não substituiu o arquivo.")
                    os.replace(temporary, path)
            except (ErroCaptura, OSError, ValueError) as error:
                result = {"status": "cancelado" if isinstance(error, Cancelado) else "erro", "mensagem": str(error)}
                # Conservar o lock também ao publicar erro/cancelamento: outro
                # worker não pode concluir e ter seu resultado substituído aqui.
                _result_write(session.path.with_name("resultado.xml"), session, result)
                return result
    except Ocupado:
        return {"status": "aguardando", "mensagem": "Esta tomada já está sendo organizada por outro processo."}
    return organizar(path, pasta_capturas=pasta_capturas, intervalo=intervalo, espera_maxima=espera_maxima)


def organizar(path: Path, *, pasta_capturas: Path | None = None, intervalo: float = 1.0,
              espera_maxima: float = 120.0) -> dict:
    """Parâmetro pasta_capturas é só injeção de dependência nos testes; CLI resolve Windows."""
    session = ler_sessao(path, pasta_capturas)
    lock_path = _inside(session.path.with_name("organizador.lock"), session.data)
    try:
        with _exclusive(lock_path, lock=True):
            try:
                _cancel(session)
                previous = _previous_saved(session)
                if previous:
                    return previous
                committed = _recover_committed(session)
                if committed:
                    _result_write(session.path.with_name("resultado.xml"), session, committed)
                    return committed
                if not session.subtitle.is_file() or _hash(session.subtitle) != session.subtitle_hash:
                    raise ErroCaptura("Legenda original ausente ou com SHA256 diferente da tomada.")
                waiting_until = min(time.monotonic() + espera_maxima,
                                    time.monotonic() + max(0.0, (session.deadline - datetime.now(timezone.utc)).total_seconds()))
                signature = None
                stable = 0
                last_issue = "Pare a gravação com Win+Alt+R para concluir o MP4 desta tomada."
                while True:
                    _cancel(session)
                    candidates = _candidates(session)
                    if len(candidates) > 1:
                        raise ErroCaptura("Captura ambígua: há mais de um MP4 novo do GTA; nenhum foi movido.")
                    if candidates:
                        source = candidates[0]
                        information = source.stat()
                        current = (source.name.casefold(), information.st_size, information.st_mtime_ns)
                        stable = stable + 1 if signature == current else 1
                        signature = current
                        if stable >= 3:
                            try:
                                result = _organize_file(session, source)
                                _result_write(session.path.with_name("resultado.xml"), session, result)
                                return result
                            except Ocupado as error:
                                last_issue = str(error) + "; pare a gravação antes de organizar."
                            except ErroCaptura as error:
                                if "MP4" not in str(error):
                                    raise
                                last_issue = str(error)
                    else:
                        signature, stable = None, 0
                    if time.monotonic() >= waiting_until:
                        result = {"status": "aguardando", "mensagem": last_issue + " Pressione End para tentar novamente."}
                        _result_write(session.path.with_name("resultado.xml"), session, result)
                        return result
                    time.sleep(max(0.001, intervalo))
            except Cancelado as error:
                result = {"status": "cancelado", "mensagem": str(error)}
                _result_write(session.path.with_name("resultado.xml"), session, result)
                return result
            except (ErroCaptura, OSError, ValueError) as error:
                result = {"status": "erro", "mensagem": str(error)}
                _result_write(session.path.with_name("resultado.xml"), session, result)
                return result
    except Ocupado:
        # O segundo processo não escreve resultado.xml por cima do processo ativo.
        return {"status": "aguardando", "mensagem": "Esta tomada já está sendo organizada por outro processo."}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--diagnosticar", action="store_true", help="JSON de leitura da Game Bar e da pasta real.")
    group.add_argument("--sessao", type=Path, help="XML de uma tomada concluída, no runtime do perfil.")
    group.add_argument("--repetir", type=Path, help="Retomar o mesmo XML concluído, com backup do prazo e limite de dez minutos.")
    arguments = parser.parse_args(argv)
    try:
        result = diagnosticar() if arguments.diagnosticar else repetir(arguments.repetir) if arguments.repetir else organizar(arguments.sessao)
        print(json.dumps(result, ensure_ascii=False))
        return 0 if arguments.diagnosticar else {"salvo": 0, "erro": 1, "aguardando": 2, "cancelado": 3}[result["status"]]
    except (ErroCaptura, OSError, ValueError) as error:
        print(json.dumps({"status": "erro", "mensagem": str(error)}, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
