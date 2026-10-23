"""Gravador OBS para a primeira tomada nativa do Diretor, sem Win+Alt+R.

Nao inicia OBS. Usa websocket autenticado local e conserva as guardas de foco,
writer proprio, sessao/SRT e organizacao de capturar_primeira_producao.py.
status faz somente consultas. gravar envia F7 normalmente e Start/StopRecord
explicitamente; o sidecar identifica OBS em vez de alegar gravacao Game Bar.
Execute com ferramentas/.venv_obs/Scripts/python.exe.
"""
from __future__ import annotations

import argparse
import base64
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import sys
import time
import uuid

import capturar_primeira_producao as primeira
import operar_gta as gta

ROOT = Path(__file__).resolve().parents[1]
CONFIG = ROOT / "dependencias/obs-studio/config/obs-studio/plugin_config/obs-websocket/config.json"


def utc() -> str:
    return datetime.now(timezone.utc).isoformat()


class OBS:
    def __init__(self, config: Path = CONFIG):
        import websocket
        settings = json.loads(config.read_text("utf-8-sig"))
        if settings.get("auth_required") is not True:
            raise RuntimeError("OBS deve exigir autenticacao.")
        port = settings["server_port"]
        if type(port) is not int or not 1 <= port <= 65535:
            raise ValueError("Porta OBS invalida.")
        self.ws = websocket.create_connection("ws://127.0.0.1:%d" % port, timeout=15,
            http_no_proxy=["127.0.0.1", "localhost"], subprotocols=["obswebsocket.json"])
        hello = json.loads(self.ws.recv())
        if hello.get("op") != 0 or not hello.get("d", {}).get("authentication"):
            self.close()
            raise RuntimeError("OBS nao enviou Hello autenticado esperado.")
        auth = hello["d"]["authentication"]
        secret = base64.b64encode(hashlib.sha256((settings["server_password"] + auth["salt"]).encode()).digest()).decode()
        response = base64.b64encode(hashlib.sha256((secret + auth["challenge"]).encode()).digest()).decode()
        self.ws.send(json.dumps({"op": 1, "d": {"rpcVersion": 1,
            "authentication": response, "eventSubscriptions": 0}}))
        if json.loads(self.ws.recv()).get("op") != 2:
            self.close()
            raise RuntimeError("OBS recusou identificacao; nao repetir captura.")

    def request(self, name: str, data: dict | None = None) -> dict:
        identity = uuid.uuid4().hex
        self.ws.send(json.dumps({"op": 6, "d": {"requestType": name,
            "requestId": identity, "requestData": data or {}}}))
        while True:
            message = json.loads(self.ws.recv())
            if message.get("op") != 7 or message.get("d", {}).get("requestId") != identity:
                continue
            result = message["d"]
            if result["requestStatus"].get("result") is not True:
                raise RuntimeError("OBS recusou %s (codigo %s)." % (name, result["requestStatus"].get("code")))
            return result.get("responseData", {})

    def close(self):
        self.ws.close()


class OBSRecorder:
    def __init__(self, obs: OBS, folder: Path):
        self.obs, self.folder = obs, folder
        self.start_attempted = self.stop_attempted = False
        self.stopped_output = None
        self.events = []
        self.record("gravador_preparado", gravador="OBSStudioWebSocket5", atalhoWindowsEnviado=False)

    def record(self, event: str, **data):
        self.events.append({"evento": event, "utc": utc(), **data})
        path = self.folder / ("obs-evento-%02d.json" % len(self.events))
        primeira.atomic(path, (json.dumps(self.events[-1], ensure_ascii=False, indent=2) + "\n").encode("utf-8"))

    def wait_active(self, active: bool, timeout: float = 20) -> dict:
        """Start/StopRecord podem responder antes da transicao; so repetir consultas."""
        deadline = time.monotonic() + timeout
        while True:
            status = self.obs.request("GetRecordStatus")
            if status.get("outputActive") is active:
                return status
            if time.monotonic() >= deadline:
                raise TimeoutError("OBS nao confirmou outputActive=%s em %ss; comando nao repetido." % (active, timeout))
            time.sleep(.15)

    def preflight(self):
        status = self.obs.request("GetRecordStatus")
        if status.get("outputActive") is not False:
            raise RuntimeError("OBS ja esta gravando; nao iniciar nem parar gravacao alheia.")
        directory = self.obs.request("GetRecordDirectory")
        if Path(directory["recordDirectory"]) != Path.home() / "Videos/Captures":
            raise RuntimeError("OBS nao grava na pasta real Captures.")
        mode = self.obs.request("GetProfileParameter", {
            "parameterCategory": "Output", "parameterName": "Mode"})
        record_type = self.obs.request("GetProfileParameter", {
            "parameterCategory": "AdvOut", "parameterName": "RecType"})
        if mode.get("parameterValue") != "Advanced" or record_type.get("parameterValue") != "Standard":
            raise RuntimeError("OBS precisa usar Output.Mode=Advanced e AdvOut.RecType=Standard.")
        filename = self.obs.request("GetProfileParameter", {
            "parameterCategory": "Output", "parameterName": "FilenameFormatting"})
        if not str(filename.get("parameterValue", "")).startswith("GTA5_OBS_EP01_C07_"):
            raise RuntimeError("Prefixo OBS em memoria nao e GTA5_OBS_EP01_C07_.")
        file_format = self.obs.request("GetProfileParameter", {
            "parameterCategory": "AdvOut", "parameterName": "RecFormat2"})
        if file_format.get("parameterValue") != "mp4":
            raise RuntimeError("OBS precisa usar MP4 normal, nao MKV/hibrido.")
        video = self.obs.request("GetVideoSettings")
        if ((video["baseWidth"], video["baseHeight"], video["outputWidth"], video["outputHeight"])
                != (1920, 1080, 1920, 1080) or video["fpsNumerator"] != video["fpsDenominator"] * 30):
            raise RuntimeError("OBS precisa de video 1920x1080/30fps.")
        scene = self.obs.request("GetCurrentProgramScene")
        if scene.get("sceneName") != "GTA C07":
            raise RuntimeError("Cena OBS selecionada nao e GTA C07.")
        special = self.obs.request("GetSpecialInputs")
        if any(value is not None for value in special.values()):
            raise RuntimeError("OBS tem fontes globais de audio; revisar antes de gravar.")
        self.record("preflight_inativo", status=status, directory=directory, video=video,
            scene=scene, audioGlobal=special, filename=filename, formato=file_format,
            modo=mode, tipoGravacao=record_type)

    def shortcut_replacement(self):
        if not self.start_attempted:
            self.preflight()
            self.start_attempted = True
            self.record("StartRecord_tentativa_unica")
            reply = self.obs.request("StartRecord")
            status = self.wait_active(True)
            self.record("StartRecord_resposta", reply=reply, status=status)
            if status.get("outputActive") is not True:
                raise RuntimeError("OBS nao confirmou gravacao ativa apos StartRecord.")
            return
        if self.stop_attempted:
            raise RuntimeError("StopRecord ja tentado; nao repetir.")
        status = self.obs.request("GetRecordStatus")
        if status.get("outputActive") is not True:
            raise RuntimeError("OBS nao esta ativo; nao enviar parada nem outro start.")
        self.stop_attempted = True
        self.record("StopRecord_tentativa_unica", status=status)
        reply = self.obs.request("StopRecord")
        status = self.wait_active(False)
        self.record("StopRecord_resposta", reply=reply, status=status)
        if status.get("outputActive") is not False:
            raise RuntimeError("OBS nao confirmou parada.")
        output = Path(reply.get("outputPath", ""))
        if (output.parent != Path.home() / "Videos/Captures" or output.suffix.lower() != ".mp4"
                or not output.name.startswith("GTA5_OBS_EP01_C07_")):
            raise RuntimeError("Caminho retornado por OBS nao corresponde ao writer C07 esperado.")
        self.stopped_output = output


def capture(scene: str, obs: OBS, folder: Path) -> tuple[int, object]:
    recorder = OBSRecorder(obs, folder)
    recorder.preflight()
    original_press, original_journal = gta.press, primeira.Journal

    class JournalOBS(original_journal):
        def save(self, state, **values):
            self.record.update(gravador="OBSStudioWebSocket5", atalhoWindowsEnviado=False,
                sidecarsOBS=str(folder / "obs-evento-*.json"),
                limiteRotulos="Rotulos do runner de primeira captura sao reutilizados; writer operado por Start/StopRecord, sem WinAltR.")
            return super().save(state, **values)

    def press(window, keys):
        if list(keys) == [0x5B, 0x12, 0x52]:
            recorder.shortcut_replacement()
        else:
            original_press(window, keys)

    gta.press, primeira.Journal = press, JournalOBS
    try:
        code, journal = primeira.capture_first(scene, folder)
        if code == 0:
            writer = journal.record.get("writer", {}).get("arquivo")
            if not writer or recorder.stopped_output is None or Path(writer) != recorder.stopped_output:
                journal.save("captura_salva_obs_proveniencia_divergente",
                    erro="StopRecord.outputPath diverge do writer identificado pelo runner.")
                raise RuntimeError("Retorno OBS nao corresponde exatamente ao writer organizado.")
            journal.save(journal.record["estado"], obsStopOutputPath=str(recorder.stopped_output),
                obsWriterAssociado=True)
            recorder.record("retorno_OBS_writer_conferidos", arquivo=str(recorder.stopped_output))
        return code, journal
    finally:
        gta.press, primeira.Journal = original_press, original_journal
        recorder.record("wrapper_encerrado", inicioTentado=recorder.start_attempted,
            paradaTentada=recorder.stop_attempted, atalhoWindowsEnviado=False)


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("acao", choices=("status", "gravar"))
    parser.add_argument("--cena", default="EP01_C07", choices=("EP01_C07",))
    parser.add_argument("--obs-config", type=Path, default=CONFIG)
    args = parser.parse_args(argv)
    obs = OBS(args.obs_config)
    try:
        if args.acao == "status":
            print(json.dumps({name: obs.request(name) for name in (
                "GetVersion", "GetRecordStatus", "GetRecordDirectory", "GetVideoSettings",
                "GetCurrentProgramScene", "GetInputList", "GetSpecialInputs")}, ensure_ascii=False, indent=2))
            return 0
        folder = ROOT / "backups/primeira-producao-obs" / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-" + uuid.uuid4().hex[:8])
        folder.mkdir(parents=True, exist_ok=False)
        try:
            code, journal = capture(args.cena, obs, folder)
            print(json.dumps({"estado": journal.record["estado"], "checkpoint": str(folder / "checkpoint.xml"),
                "gravador": "OBSStudioWebSocket5", "atalhoWindowsEnviado": False,
                "erro": journal.record.get("erro"), "tomada": journal.record.get("tomada")}, ensure_ascii=False, indent=2))
            return code
        except Exception as error:
            primeira.atomic(folder / "erro-obs.json", (json.dumps({"erro": str(error),
                "gravador": "OBSStudioWebSocket5", "atalhoWindowsEnviado": False}, ensure_ascii=False) + "\n").encode())
            print("Captura OBS interrompida: " + str(error), file=sys.stderr)
            return 1
    finally:
        obs.close()


if __name__ == "__main__":
    raise SystemExit(main())
