"""Primeira captura de producao apos Reload, com writer/GUID e entradas unicas.

gravar exige cena ja montada e camera conferida pelo operador. Nao monta, limpa,
recupera aplicativos, decodifica/exporta ou aprova imagem. inspecionar so le provas;
um processo interrompido nunca autoriza reenviar F7/start/stop automaticamente.
"""
from __future__ import annotations

import argparse
import configparser
import ctypes
from ctypes import wintypes
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import sys
import time
import uuid
import xml.etree.ElementTree as ET

PROJECT = Path(r"C:\GTA_Diretor")
DATA = Path(os.environ.get("USERPROFILE", r"C:\Users\felipe.santiago")) / "GTA_Diretor"
TECHNICAL = PROJECT / "backups/capturador-tecnico115/capturar_ensaio_tecnico.py"
TECHNICAL_SHA = "5DD0026D638F0186C51C1C5B58637B2E9FE52BBBD6FCC32A30F25D83F352ABFD"
EVENT = re.compile(r"^([^ ]+) \[([^]]+)\] (.*)$")
ERROR = re.compile(r"\[(?:ERRO[^\]]*|OPERACAO_PRODUCAO_ERRO|CAPTURA_CANCELADA|CAPTURA_RESULTADO|ATALHO_GRAVACAO_FALHOU)\]")
ID = re.compile(r"[0-9a-f]{32}\Z")


def utc():
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest().upper()


def atomic(path, data):
    temporary = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    with temporary.open("xb") as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)


class Journal:
    """Estado imutavel por etapa + ponte XML atomica, antes de cada entrada."""
    def __init__(self, folder, scene):
        self.folder, self.sequence = folder, 0
        self.record = {"versao": 1, "tipo": "primeira_captura_producao", "id": uuid.uuid4().hex,
                       "cena": scene, "inicioUtc": utc(), "estado": "preflight",
                       "ferramentaSha256": digest(Path(__file__)), "entradas": {}, "eventos": [],
                       "aprovacaoVisual": False, "exportavel": False, "producaoCompleta": False}

    def save(self, state, **values):
        self.record.update(values)
        self.record.update(estado=state, checkpointUtc=utc())
        self.sequence += 1
        name = "estado-%04d.json" % self.sequence
        data = (json.dumps(self.record, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        with (self.folder / name).open("xb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        root = ET.Element("primeiraCaptura", {"versao": "1", "id": self.record["id"],
                "cena": self.record["cena"], "estado": state, "checkpointUtc": self.record["checkpointUtc"],
                "json": str(self.folder / name), "sha256Json": hashlib.sha256(data).hexdigest().upper()})
        if self.record.get("sessao"):
            root.set("sessao", self.record["sessao"])
        for key, value in self.record["entradas"].items():
            ET.SubElement(root, "entrada", {"nome": key, "tentada": str(value["tentada"]), "utc": value["utc"]})
        atomic(self.folder / "checkpoint.xml", ET.tostring(root, encoding="utf-8", xml_declaration=True))
        atomic(self.folder / "manifesto-primeira.json", data)

    def intent(self, name):
        if name in self.record["entradas"]:
            raise RuntimeError("Entrada ja tentada; nao repetir: " + name)
        self.record["entradas"][name] = {"tentada": True, "utc": utc()}
        self.save("antes_entrada_" + name)

    def native_copy(self, path, reader):
        if not path.is_file():
            return
        node = reader(path)
        if node.get("id") != self.record.get("sessao"):
            raise RuntimeError("XML nativo pertence a outra sessao")
        name = "native-%04d-%s" % (self.sequence, path.name)
        shutil.copy2(path, self.folder / name)
        if digest(path) != digest(self.folder / name):
            raise RuntimeError("XML mudou durante copia de prova")
        self.record.setdefault("xmlNativos", []).append({"origem": str(path), "copia": str(self.folder / name),
                                                         "sha256": digest(self.folder / name)})


def parsed(row):
    match = EVENT.fullmatch(row)
    return match.groups() if match else (None, None, None)


def require_first_capture_history(rows, loaded, mount):
    """Only canceled, fully cleaned prior manual arms can precede a first SAVE.

    This validates history only; it sends no input and grants no automatic retry.
    Writer inventory, current mount, source and live INI guards stay independent.
    """
    pending = None
    resolved = set()
    for index, row in enumerate(rows[loaded + 1:], loaded + 1):
        unused, tag, detail = parsed(row)
        if tag == "CAPTURA_SALVA" or tag == "CAPTURA_INICIO_CONFIRMADO" or (tag and tag.startswith("ATALHO_GRAVACAO")):
            raise RuntimeError("SAVE/automacao anterior nesta instancia; nao usar primeira captura manual")
        if tag == "CAPTURA_PREPARADA":
            match = re.match(r"Sessão=([0-9a-f]{32}); cena=([^;]+);", detail)
            if index >= mount or pending is not None or not match or match[1] in resolved:
                raise RuntimeError("Armamento anterior ainda ativo/repetido/sem GUID; nao enviar F7")
            pending = {"id": match[1], "cancelada": False}
        elif tag == "CAPTURA_CANCELADA":
            match = re.match(r"Sessão=([0-9a-f]{32});", detail)
            if not match or pending is None or match[1] != pending["id"] or pending["cancelada"]:
                raise RuntimeError("Cancelamento sem armamento anterior correspondente; nao enviar F7")
            pending["cancelada"] = True
        elif tag == "CAPTURA_PRIMEIRA_ACAO_PREPARADA":
            if pending is None or pending["cancelada"]:
                raise RuntimeError("Armamento manual sem sessao anterior correspondente; nao enviar F7")
        elif tag == "LIMPEZA" and detail == "Concluída=True; entidades restantes=0":
            if pending is not None and pending["cancelada"] and index < mount:
                resolved.add(pending["id"])
                pending = None
    if pending is not None:
        raise RuntimeError("Captura anterior exige cancelamento correspondente e limpeza concluida antes da montagem atual")
    return tuple(sorted(resolved))


def mount_evidence(log_path, scene, source, ini, *, first=True):
    rows = log_path.read_text("utf-8-sig").splitlines()
    loaded = max((i for i, row in enumerate(rows) if "[SCRIPT_CARREGADO]" in row), default=-1)
    mounts = [i for i, row in enumerate(rows) if i > loaded and "[MONTAGEM_INICIADA]" in row]
    if loaded < 0 or not mounts or "Cena=" + scene + ";" not in rows[mounts[-1]]:
        raise RuntimeError("Montagem mais recente da instancia nao pertence a cena")
    mount = mounts[-1]
    if not any("[CENA_MONTADA] Id=" + scene + ";" in row for row in rows[mount:]) or not any("[CAMERA_PRONTA]" in row for row in rows[mount:]):
        raise RuntimeError("Montagem/camera nativa ausente na instancia")
    if any(ERROR.search(row) for row in rows[mount:]):
        raise RuntimeError("A montagem atual registra erro; investigar antes de F7")
    if first:
        require_first_capture_history(rows, loaded, mount)
    mounted_ns = int(datetime.fromisoformat(rows[mount].split(" ", 1)[0]).timestamp() * 1_000_000_000)
    loaded_ns = int(datetime.fromisoformat(rows[loaded].split(" ", 1)[0]).timestamp() * 1_000_000_000)
    if source.stat().st_mtime_ns > mounted_ns:
        raise RuntimeError("Fonte runtime mudou apos montar; remonte antes de gravar")
    if ini.stat().st_mtime_ns > loaded_ns:
        raise RuntimeError("INI mudou apos Reload; configuracao em memoria nao comprovada")
    return {"scriptEvento": rows[loaded], "montagemEvento": rows[mount]}


class Protocol:
    def __init__(self, scene, duration):
        self.scene, self.duration = scene, duration
        self.session = None
        self.first = self.count = self.started = self.completed = self.saved = False
        self.allow_first = self.allow_count = self.allow_saved = False

    def accept(self, row):
        if ERROR.search(row):
            raise RuntimeError(row)
        unused, tag, text = parsed(row)
        if tag in ("SCRIPT_CARREGADO", "MONTAGEM_INICIADA", "CENA_VALIDADA", "CENA_MONTADA"):
            raise RuntimeError("Instancia/montagem mudou: " + row)
        if tag in ("ATALHO_GRAVACAO_INICIO", "ATALHO_GRAVACAO_FIM", "CAPTURA_INICIO_CONFIRMADO"):
            raise RuntimeError("Automacao nativa inesperada; nao enviar outro toggle: " + row)
        if tag == "CAPTURA_PREPARADA":
            match = re.search(r"Sessão=([0-9a-f]{32}); cena=([^;]+);", text)
            if not self.allow_first or self.session or not match or match[2] != self.scene:
                raise RuntimeError("Preparacao repetida/inesperada ou de outra cena")
            self.session = match[1]
        elif tag == "CAPTURA_PRIMEIRA_ACAO_PREPARADA":
            if not self.allow_first or self.first or not self.session or not text.startswith("Cena=" + self.scene + ";"):
                raise RuntimeError("Armamento manual inesperado")
            self.first = True
        elif tag == "CAPTURA_VINCULADA":
            if not self.allow_count or not text.startswith("Sessão=" + str(self.session) + "; cena=" + self.scene + ";"):
                raise RuntimeError("SRT vinculado a outra sessao/cena")
        elif tag == "CONTAGEM_INICIADA":
            if not self.allow_count or self.count or not text.startswith("Cena=" + self.scene + ";"):
                raise RuntimeError("Contagem inesperada/repetida")
            self.count = True
        elif tag == "ACAO_INICIADA":
            duration = re.search(r"duração_ms=(\d+)(?:;|$)", text)
            if not self.count or self.started or not text.startswith("Cena=" + self.scene + ";") or not duration or int(duration[1]) != self.duration:
                raise RuntimeError("Inicio nativo repetido/fora da cena/duracao")
            self.started = True
        elif tag == "ACAO_CONCLUIDA":
            elapsed = re.search(r"tempo_ms=(\d+)(?:;|$)", text)
            if not self.started or self.completed or not text.startswith("Cena=" + self.scene + ";") or not elapsed or int(elapsed[1]) < self.duration:
                raise RuntimeError("Conclusao nativa curta/repetida/sem inicio")
            self.completed = True
        elif tag == "CAPTURA_SALVA":
            if not self.allow_saved or not self.completed or self.saved or not text.startswith("Sessão=" + str(self.session) + ";"):
                raise RuntimeError("SAVE inesperado/repetido/de outra sessao")
            self.saved = True


def idle_delta(now, previous):
    return (now - previous) & 0xFFFFFFFF


class InputGuard:
    def __init__(self, gta, window, journal):
        self.gta, self.window, self.journal = gta, window, journal
        class LastInput(ctypes.Structure):
            _fields_ = [("cbSize", wintypes.UINT), ("dwTime", wintypes.DWORD)]
        self.LastInput = LastInput
        gta.u.GetLastInputInfo.argtypes = [ctypes.POINTER(LastInput)]
        gta.u.GetLastInputInfo.restype = wintypes.BOOL
        gta.k.GetTickCount.restype = wintypes.DWORD

    def idle(self):
        value = self.LastInput(ctypes.sizeof(self.LastInput), 0)
        if not self.gta.u.GetLastInputInfo(ctypes.byref(value)):
            raise RuntimeError("Windows nao confirmou ociosidade; nenhuma entrada")
        return idle_delta(self.gta.k.GetTickCount(), value.dwTime)

    def wait_idle(self, timeout=30):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if self.idle() >= 1200:
                self.gta.no_held_keys()
                if any(self.gta.u.GetAsyncKeyState(vk) & 0x8000 for vk in range(8, 255)):
                    raise RuntimeError("Tecla fisica pressionada; nenhuma tecla liberada pelo operador")
                return
            time.sleep(.1)
        raise TimeoutError("Usuario ativo; ociosidade1200ms nao ocorreu, nenhuma entrada")

    def focus(self):
        self.wait_idle()
        actual = self.gta.game_window()
        if actual["pid"] != self.window["pid"] or actual["hwnd"] != self.window["hwnd"]:
            raise RuntimeError("Processo/janela GTA mudou; nenhuma entrada")
        self.gta.focus_game_window(self.window)
        self.wait_idle()  # O clique normal de foco tambem conta como entrada Windows.
        if self.gta.foreground_hwnd() != self.window["hwnd"]:
            raise RuntimeError("Foco mudou; entrada cancelada")

    def require_quiet(self, name=None, stage=None):
        idle_ms = self.idle()
        # Preserve the original short-circuit decision: foreground is checked only when idle>=1200.
        foreground = self.gta.foreground_hwnd() if idle_ms >= 1200 else None
        if idle_ms < 1200 or foreground != self.window["hwnd"]:
            details = {"utc": utc(), "entrada": name, "etapa": stage, "idleMsNaGuarda": idle_ms,
                       "idleMinimoMs": 1200, "idleRecusado": idle_ms < 1200,
                       "foregroundNaDecisao": foreground, "esperada": dict(self.window),
                       "somenteDiagnostico": True, "entradaEnviada": False}
            try:
                actual = foreground if foreground is not None else self.gta.foreground_hwnd()
                details["foregroundObservadoAposRecusa"] = actual
                details["foreground"] = self.gta.describe(actual)
                details["focoRecusado"] = actual != self.window["hwnd"]
            except Exception as diagnostic_error:
                details["erroDiagnostico"] = str(diagnostic_error)
            self.journal.record["ultimaGuardaRecusada"] = details
            raise RuntimeError("Usuario/foco mudou antes da entrada; tecla cancelada; diagnostico=" +
                               json.dumps(details, ensure_ascii=False, separators=(",", ":")))
        self.gta.no_held_keys()
        if any(self.gta.u.GetAsyncKeyState(vk) & 0x8000 for vk in range(8, 255)):
            raise RuntimeError("Tecla fisica pressionada antes da entrada; cancelada")

    def send(self, name, keys, final_check=None, before_send=None):
        self.focus()
        if final_check:
            final_check()
        self.require_quiet(name, "antes_intent")
        self.journal.record.setdefault("guardasEntrada", []).append({"nome": name, "utc": utc(),
                "ociosoMs": self.idle(), "janela": self.gta.describe(self.window["hwnd"])})
        self.journal.intent(name)  # Persistir tentativa ANTES de SendInput, inclusive falha parcial.
        self.require_quiet(name, "depois_intent_antes_SendInput")
        if before_send:
            before_send()  # Sem IO/espera: marca tentativa somente quando a chamada sera feita.
        self.gta.press(self.window, keys)
        self.journal.save("entrada_" + name + "_enviada")


def capture_class(technical):
    class Capture(technical.Capture):
        def files(self):
            result = [self.cap._inside(p, self.folder) for p in self.folder.iterdir()
                      if p.suffix.casefold() == ".mp4" and p.is_file()]
            if len(result) > 5000:
                raise RuntimeError("Mais de5000 MP4s na pasta; associacao recusada")
            return result

        def recognize(self):
            fresh, unused = self.inventory()
            if any(not self.cap.MOVIE_NAME.fullmatch(p.name) for p in fresh):
                raise RuntimeError("MP4 novo de outra atividade; nao escolher captura")
            return super().recognize()

        def require_writer(self):
            fresh, writers = self.inventory()
            current = technical.identity(self.path) if self.path is not None and self.path.exists() else None
            if not technical.stop_allowed(self.start_sent, self.stop_sent, self.file_id, current,
                                           len(fresh), len(writers), self.path in writers):
                raise RuntimeError("Writer proprio mudou/parou/ambiguo; nenhum toggle")

        def stop_once(self, guard, journal):
            if self.path is None:
                self.recognize()
            if self.path is None:
                return False
            self.require_writer()
            def checked():
                self.require_writer()
            def attempted():
                self.stop_sent = True
            guard.send("parar_writer", [0x5B, 0x12, 0x52], checked, attempted)
            return True
    return Capture


def registered_captures(cap, settings):
    import winreg
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders") as key:
        try:
            declared = winreg.QueryValueEx(key, cap.CAPTURES_GUID)[0]
        except FileNotFoundError:
            description = r"Software\Microsoft\Windows\CurrentVersion\Explorer\FolderDescriptions" + chr(92) + cap.CAPTURES_GUID
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, description) as desc:
                relative = winreg.QueryValueEx(desc, "RelativePath")[0]
                parent = winreg.QueryValueEx(desc, "ParentFolder")[0]
            if relative != "Captures" or str(parent).casefold() != cap.VIDEO_GUID.casefold():
                raise RuntimeError("PastaCaptures sem referencia local oficial Windows")
            declared = str(Path(os.path.expandvars(winreg.QueryValueEx(key, "My Video")[0])) / "Captures")
    actual = cap._path(os.path.expandvars(declared))
    if actual != cap._path(os.path.expandvars(settings.get("Gravacao", "PastaCapturas"))) or not actual.is_dir():
        raise RuntimeError("Pasta de capturas INI diverge do registro real Windows")
    return actual


def prove_arm(journal, cap, seq, capture):
    path = DATA / "logs/capturas" / journal.record["sessao"] / "armada.xml"
    root = seq.read_xml(path)
    before = root.find("antes")
    if (root.tag != "captura" or root.get("id") != journal.record["sessao"] or
            root.get("estado") != "preparada" or root.get("cenaId") != journal.record["cena"] or
            cap._path(root.get("pastaDados", "")) != DATA or
            cap._path(root.get("pastaCapturas", "")) != capture.folder or before is None):
        raise RuntimeError("Armada.xml nao confirma GUID/cena/pastas/baseline")
    names = [item.get("nome", "").casefold() for item in before]
    if len(names) != len(set(names)) or set(names) != set(capture.baseline):
        raise RuntimeError("Baseline nativo difere do observado antes de F7")
    journal.native_copy(path, seq.read_xml)


def finish_proof(journal, seq, montagem, capture, source, duration):
    if digest(source) != journal.record["fonteOriginal"]["sha256"]:
        raise RuntimeError("Fonte runtime mudou durante a tomada; preservar sem promover")
    take = montagem.tomada_real(DATA, journal.record["cena"], journal.record["sessao"])
    if take["duracaoAcaoMs"] != duration:
        raise RuntimeError("Duracao da tomada difere do snapshot prearm")
    native = DATA / "logs/capturas" / journal.record["sessao"]
    result = seq.read_xml(native / "resultado.xml")
    manifest_path = seq.editor.exigir_sem_vinculos(Path(result.get("manifesto", "")))
    manifest = seq.editor.ler_json(manifest_path)
    identity = manifest.get("IdentidadeOrigem", {})
    if (Path(manifest.get("FonteVideo", "")) != capture.path or
            tuple(identity.get(key) for key in ("Disco", "Arquivo", "CriacaoNs")) != capture.file_id or
            manifest.get("Id") != journal.record["sessao"] or manifest.get("SHA256Video") != take["sha256"]):
        raise RuntimeError("Origem/identidade/hash do organizador nao corresponde ao writer proprio")
    for name in ("armada.xml", "iniciada.xml", "sessao.xml", "resultado.xml"):
        journal.native_copy(native / name, seq.read_xml)
    shutil.copy2(manifest_path, journal.folder / "manifesto-organizador.json")
    return take


def dependencies():
    sys.path.insert(0, str(PROJECT / "ferramentas"))
    import operar_sequencia_capturas as seq
    import montar_video_episodio_limpo as montagem
    import organizar_capturas as cap
    import operar_gta as gta
    if digest(TECHNICAL) != TECHNICAL_SHA:
        raise RuntimeError("Writer115 mudou; revisar antes de usar")
    spec = importlib.util.spec_from_file_location("capturador_tecnico115_primeira", TECHNICAL)
    technical = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(technical)
    return seq, montagem, cap, gta, technical


def log_class(technical):
    class Log(technical.Log):
        def wait(self, predicate, timeout):
            deadline = time.monotonic() + timeout
            while time.monotonic() < deadline:
                for row in self.poll():
                    if ERROR.search(row):
                        raise RuntimeError(row)
                result = predicate(self.events)
                if result is not None and result is not False:
                    return result
                time.sleep(.15)
            raise TimeoutError("Prazo do status esgotado; operacao nao repetida")
    return Log


def capture_first(scene, folder, parte=None):
    seq, montagem, cap, gta, technical = dependencies()
    montagem.identidade_cena(scene)
    journal = Journal(folder, scene)
    capture = guard = log = protocol = None
    source = cap._path(str(seq.source_path(DATA, scene, parte)))
    ini = cap._path(str(PROJECT / "configuracoes/DiretorGTA.ini"))
    with seq.exclusive(DATA):
        try:
            settings = configparser.ConfigParser(interpolation=None, strict=True)
            settings.read(ini, encoding="utf-8-sig")
            if (not settings.getboolean("Gravacao", "Habilitada", fallback=False) or
                    settings.get("Gravacao", "Modo", fallback="") != "manual" or
                    not settings.getboolean("Gravacao", "AutomatizarAposPrimeira", fallback=False) or
                    settings.get("Teclas", "Acao", fallback="F7") != "F7"):
                raise RuntimeError("INI exige Habilitada=true, Modo=manual, AutomatizarAposPrimeira=true e F7")
            log_path = DATA / "logs/DiretorGTA.log"
            evidence = mount_evidence(log_path, scene, source, ini)
            part_proof = seq.require_part_mounted(log_path, DATA, scene, parte)
            log = log_class(technical)(log_path)
            status = seq.command(DATA, folder, log, "status")
            seq.require_ready(status, scene)
            from validar_json import validar_cena_json
            validar_cena_json(source, DATA / "catalogos")
            snapshot, duration, original = seq.current_source(DATA, scene, folder, parte)
            if not 1 <= duration <= 180000:
                raise RuntimeError("Duracao fora do limite de captura")
            shutil.copy2(ini, folder / "DiretorGTA.prearm.ini")
            ini_sha = digest(ini)
            if digest(folder / "DiretorGTA.prearm.ini") != ini_sha:
                raise RuntimeError("INI mudou durante snapshot")
            capture = capture_class(technical)(cap, gta, registered_captures(cap, settings))
            window = gta.game_window()
            guard = InputGuard(gta, window, journal)
            protocol = Protocol(scene, duration)
            journal.save("snapshot_prearm_conferido", status=status, provaMontagem=evidence,
                    fonteJsonSnapshot=snapshot, fonteOriginal=original, iniSha256=ini_sha,
                    duracaoMs=duration, gameWindow=window, baseline={k: list(v) for k, v in capture.baseline.items()},
                    hashFonteConferidoAntesDeArmar=True, hashFonteNativeDuranteCaptura=False,
                    parte=parte, provenienciaSubtomada=part_proof)

            def pump():
                for row in log.poll():
                    protocol.accept(row)
                journal.record["eventos"] = list(log.events)

            def unchanged(first):
                pump()
                if mount_evidence(log_path, scene, source, ini, first=first) != evidence:
                    raise RuntimeError("Instancia/montagem mudou depois do snapshot")
                if digest(source) != original["sha256"] or digest(ini) != ini_sha:
                    raise RuntimeError("Fonte/INI mudou desde prearm")

            def no_writer():
                unchanged(True)
                if capture.inventory() != ([], []):
                    raise RuntimeError("Writer/fonte nova anterior ao start; nao enviar atalho")

            protocol.allow_first = True
            guard.send("f7_armar", [gta.KEYS["F7"]], no_writer)
            deadline = time.monotonic() + 12
            while not protocol.first:
                pump()
                if time.monotonic() >= deadline:
                    raise TimeoutError("CAPTURA_PRIMEIRA_ACAO_PREPARADA ausente; nao repetir F7")
                time.sleep(.1)
            journal.record["sessao"] = protocol.session
            prove_arm(journal, cap, seq, capture)
            native_arm = seq.verify_part_session(DATA, protocol.session, scene, parte, original["sha256"])
            journal.save("armada_manual_nativa_confirmada", nativeArm=native_arm)

            def before_start():
                unchanged(False)
                if capture.inventory() != ([], []):
                    raise RuntimeError("Writer surgiu antes do start; nao enviar outro atalho")
                capture.start_ns = time.time_ns()
                journal.record["startNs"] = capture.start_ns
            def start_attempted():
                capture.start_sent = True
            guard.send("iniciar_writer", [0x5B, 0x12, 0x52], before_start, start_attempted)
            deadline = time.monotonic() + 30
            while not capture.recognize():
                pump()
                if time.monotonic() >= deadline:
                    raise TimeoutError("MP4novo >=80B/em escrita nao surgiu em30s; nao repetir start")
                time.sleep(.15)
            journal.save("writer_proprio_confirmado", writer={"arquivo": str(capture.path),
                    "identidade": list(capture.file_id), "tamanho": capture.path.stat().st_size,
                    "prova": "baseline+creationAfterStart+identity+share5_32_33+size>=80"})

            def before_count():
                unchanged(False)
                capture.require_writer()
                prove_arm(journal, cap, seq, capture)
                protocol.allow_count = True
            capture.f7_sent = True
            guard.send("f7_contagem", [gta.KEYS["F7"]], before_count)
            deadline = time.monotonic() + duration / 1000 + 30
            while not protocol.completed:
                pump()
                if time.monotonic() >= deadline:
                    raise TimeoutError("Acao nao concluiu no prazo; nao repetir F7")
                capture.require_writer()
                time.sleep(.1)
            native = DATA / "logs/capturas" / protocol.session
            journal.native_copy(native / "iniciada.xml", seq.read_xml)
            journal.native_copy(native / "sessao.xml", seq.read_xml)
            journal.save("acao_nativa_concluida_aguarda_parada")
            hold = time.monotonic() + 1
            while time.monotonic() < hold:
                pump()
                capture.require_writer()
                time.sleep(.05)
            if not capture.stop_once(guard, journal):
                raise RuntimeError("Parada unica do writer proprio nao enviada")
            protocol.allow_saved = True
            journal.save("parada_unica_enviada", ultimoQuadroEsperaMs=1000)
            deadline = time.monotonic() + 110
            while not protocol.saved:
                pump()
                if time.monotonic() >= deadline:
                    raise TimeoutError("CAPTURA_SALVA ausente; nao repetir toggle nem organizador")
                time.sleep(.15)
            take = finish_proof(journal, seq, montagem, capture, source, duration)
            native_save = seq.verify_part_session(DATA, protocol.session, scene, parte, original["sha256"], saved=True)
            if parte:
                for name in ("proveniencia-subtomada-armada.xml", "proveniencia-subtomada-salva.xml"):
                    journal.native_copy(native / name, seq.read_xml)
            journal.record["nativeSave"] = native_save
            journal.record["hashFonteNativeDuranteCaptura"] = False
            journal.record["hashFonteNativeAntesArmEAposSave"] = parte is not None
            journal.save("captura_real_salva_aguarda_revisao_cpu", tomada=take,
                    proximoComandoCPU=[sys.executable, str(PROJECT / "ferramentas/calibrar_contagem_template.py"),
                        "detectar-contagem", "--cena", scene, "--sessao", protocol.session, "--fonte", snapshot["arquivo"]])
            return 0, journal
        except (Exception, KeyboardInterrupt) as error:
            journal.save("falhou_preservar_e_investigar", erro=str(error) or type(error).__name__)
            return 1, journal
        finally:
            if capture is not None and capture.start_sent and not capture.stop_sent and guard is not None:
                try:
                    stopped = capture.stop_once(guard, journal)
                    journal.save("falhou_parada_propria_tentada", paradaFinalmente=stopped)
                except Exception as error:
                    journal.save("falhou_sem_toggle_adicional", erroParada=str(error))
            if log is not None:
                try:
                    for row in log.poll():
                        journal.record["eventos"].append(row)
                except Exception as error:
                    journal.record["erroLogFinal"] = str(error)
            if capture is not None:
                journal.record["entradasWriter"] = {"inicioTentado": capture.start_sent,
                        "f7ContagemTentada": "f7_contagem" in journal.record["entradas"],
                        "paradaTentada": capture.stop_sent, "semRepeticao": True}
            journal.save(journal.record["estado"], finalizadoUtc=utc())


def inspect(checkpoint):
    raw = Path(checkpoint).read_bytes()
    if len(raw) > 1048576 or b"<!DOCTYPE" in raw.upper() or b"<!ENTITY" in raw.upper():
        raise ValueError("Checkpoint XML fora do contrato")
    root = ET.fromstring(raw)
    if root.tag != "primeiraCaptura" or root.get("versao") != "1" or not ID.fullmatch(root.get("id", "")):
        raise ValueError("Checkpoint de primeira captura invalido")
    target = Path(root.get("json", ""))
    if target.parent != Path(checkpoint).absolute().parent or digest(target) != root.get("sha256Json"):
        raise ValueError("Checkpoint JSON mudou/saiu da pasta")
    value = json.loads(target.read_text("utf-8"))
    return {"estado": value["estado"], "cena": value["cena"], "sessao": value.get("sessao"),
            "entradas": value["entradas"], "writer": value.get("writer"), "tomada": value.get("tomada"),
            "erro": value.get("erro"), "json": str(target), "sha256": digest(target),
            "modo": "somente_leitura_sem_entrada_sem_retentar"}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="acao", required=True)
    p = sub.add_parser("gravar", help="Operador unico: cena pronta, primeira apos Reload.")
    p.add_argument("--cena", required=True)
    p.add_argument("--parte", choices=("embarque_dinjer_chefe", "embarque_djanira_decolagem"))
    p = sub.add_parser("inspecionar", help="Somente leitura: verificar checkpoint apos interrupcao.")
    p.add_argument("--checkpoint", required=True, type=Path)
    args = parser.parse_args(argv)
    if args.acao == "inspecionar":
        print(json.dumps(inspect(args.checkpoint), ensure_ascii=False, indent=2))
        return 0
    if args.parte and args.cena != "EP07_C03":
        parser.error("--parte exige EP07_C03")
    folder = PROJECT / "backups/primeira-producao" / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-" + uuid.uuid4().hex[:8])
    folder.mkdir(parents=True, exist_ok=False)
    try:
        code, journal = capture_first(args.cena, folder, args.parte)
        print(json.dumps({"estado": journal.record["estado"], "checkpoint": str(folder / "checkpoint.xml"),
                          "erro": journal.record.get("erro"), "tomada": journal.record.get("tomada")}, ensure_ascii=False, indent=2))
        return code
    except Exception as error:
        value = {"erro": str(error), "pasta": str(folder),
                 "entradaEnviada": "verificar_checkpoint" if (folder / "checkpoint.xml").exists() else False}
        atomic(folder / "erro-preflight.json", (json.dumps(value) + "\n").encode())
        print(json.dumps(value, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
