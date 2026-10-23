"""Clips e montagem silenciosos dos episodios, sem textos, vozes ou vinheta.

preparar/validar usam somente CPU. exportar solicita WinRT separadamente.
Os 94 IDs originais sao explicitos; duracoes vem dos snapshots das tomadas, nunca
do estado atual da producao. Uma selecao parcial pode ser preparada, mas nao
exportada. Nenhum original, SRT ou helper de EP01 e alterado.

Selecao v1: tipo=selecao_clips_limpos_episodio, episodio=EP02, recortes=[...].
Cada recorte conserva o esquema EP01 (tomada, fonteJsonSnapshot, inicioMs,
duracaoMs, medidoNaImagem) e provas PTS da mesma sessao/MP4/SRT.
provaSemTexto inclui ptsConferidos100ns e resultadosQuadros reais; a
conferencia visual e explicita no manifesto, nao inferida dos timestamps.
"""
from __future__ import annotations

import argparse
import asyncio
import copy
from datetime import datetime, timedelta, timezone
from decimal import Decimal
from pathlib import Path
import re
import sys
import uuid
import xml.etree.ElementTree as ET

import montar_video_ep01 as editor
from montar_video_ep01_limpo import MODO


TIPO = "recortes_episodio_limpos_CapCut"
TIPO_SELECAO = "selecao_clips_limpos_episodio"
CONTAGENS = (9, 7, 7, 6, 5, 6, 5, 6, 6, 7, 5, 5, 6, 5, 9)
EPISODIOS = {"EP%02d" % ep: tuple("EP%02d_C%02d" % (ep, n)
             for n in range(1, count + 1)) for ep, count in enumerate(CONTAGENS, 1)}
SUPLEMENTARES = {"EP01_EXTRA_IRACI_BAR": (1, 0), "EP01_EXTRA_DINJER_DEDO": (1, 0)}


def identidade_cena(code: str) -> tuple[str, int]:
    if not isinstance(code, str):
        raise ValueError("ID de cena ausente.")
    if code in SUPLEMENTARES:
        ep, number = SUPLEMENTARES[code]
        return "EP%02d" % ep, number
    episode = code[:4]
    if code not in EPISODIOS.get(episode, ()):
        raise ValueError("ID fora das 94 cenas originais e dos extras autorizados: " + code)
    return episode, int(code[6:8])


def cenas_episodio(episodio: str, extras=(), somente_extras: bool = False) -> tuple[str, ...]:
    if episodio not in EPISODIOS:
        raise ValueError("Episodio permitido explicitamente: EP01 a EP15.")
    if (not isinstance(extras, (list, tuple)) or len(set(extras)) != len(extras)
            or any(code not in SUPLEMENTARES or identidade_cena(code)[0] != episodio for code in extras)):
        raise ValueError("Extras devem ser IDs suplementares autorizados, unicos, do proprio episodio.")
    if type(somente_extras) is not bool or (somente_extras and not extras):
        raise ValueError("somenteExtras exige booleano e IDs suplementares explicitos.")
    if somente_extras:
        return tuple(extras)
    return EPISODIOS[episodio] + tuple(extras)


def novo_trabalho(tipo: str) -> Path:
    base = editor.exigir_sem_vinculos(editor.RAIZ / "backups/montagem-video-episodios")
    folder = base / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
                     + "-" + tipo + "-" + uuid.uuid4().hex[:8])
    folder.mkdir(parents=True, exist_ok=False)
    return folder


def _tempos(start, length) -> None:
    if type(start) is not int or type(length) is not int or start < 0 or length <= 0:
        raise ValueError("Recortes exigem milissegundos inteiros; duracao positiva.")


def duracao_snapshot(snapshot: dict, code: str) -> int:
    if not isinstance(snapshot, dict):
        raise ValueError("Snapshot JSON real ausente: " + code)
    path = editor.exigir_sem_vinculos(Path(snapshot["arquivo"]))
    if editor.hash_arquivo(path) != snapshot.get("sha256"):
        raise ValueError("Snapshot JSON alterado: " + code)
    scene = editor.ler_json(path)
    episode, number = identidade_cena(code)
    if (scene.get("id") != code or scene.get("episodio") != int(episode[2:4])
            or scene.get("numero") != number):
        raise ValueError("Snapshot pertence a outra cena/episodio.")
    script = scene.get("roteiro")
    if not isinstance(script, list) or not script:
        raise ValueError("Snapshot sem roteiro real.")
    lengths = [Decimal(str(block["duracaoSegundos"])) * 1000 for block in script]
    if any(not length.is_finite() or length <= 0 or length != length.to_integral_value()
           for length in lengths):
        raise ValueError("Duracoes do snapshot precisam de milissegundos exatos.")
    return int(sum(lengths))


def tomada_real(dados: Path, code: str, session_id: str) -> dict:
    """Ler a sessao escolhida diretamente; nao reavaliar tomadas historicas."""
    episode, number = identidade_cena(code)
    if not isinstance(session_id, str) or not re.fullmatch(r"[0-9a-f]{32}", session_id):
        raise ValueError("Sessao de captura invalida.")
    folder = editor.exigir_sem_vinculos(dados / "logs/capturas" / session_id)
    paths = [editor.exigir_sem_vinculos(folder / name) for name in ("sessao.xml", "resultado.xml")]
    session, outcome = [ET.parse(path).getroot() for path in paths]
    if (session.tag != "captura" or outcome.tag != "resultado" or
            session.get("id") != session_id or outcome.get("id") != session_id or
            session.get("estado") != "concluida" or outcome.get("status") != "salvo" or
            session.get("cenaId") != code or session.get("episodio") != str(int(episode[2:4])) or
            session.get("numero") != str(number)):
        raise ValueError("Tomada nao concluida/salva ou identidade divergente: " + code)
    video = editor.exigir_sem_vinculos(Path(outcome.attrib["video"]))
    srt = editor.exigir_sem_vinculos(Path(outcome.attrib["legenda"]))
    expected = dados / "Serie/A_CALOTEIRA_1_TEMP" / episode
    if (video.parent != expected or srt.parent != expected or video.stem != srt.stem or
            not re.fullmatch(re.escape(code) + r"(?:_v[2-9][0-9]*|_v1[0-9]+)?", video.stem)
            or video.suffix.lower() != ".mp4" or srt.suffix.lower() != ".srt"):
        raise ValueError("MP4/SRT fora da pasta ou versao da propria cena.")
    video_hash, srt_hash = editor.hash_arquivo(video), editor.hash_arquivo(srt)
    if video_hash != outcome.get("sha256Video") or srt_hash != outcome.get("sha256Legenda"):
        raise ValueError("Hash do MP4/SRT original mudou: " + code)
    source_srt = session.find("srt")
    if source_srt is None or source_srt.get("sha256") != srt_hash:
        raise ValueError("SRT salvo diverge da sessao de captura.")
    lines = (dados / "logs/DiretorGTA.log").read_text("utf-8-sig").splitlines()
    prepared = next((i for i, line in enumerate(lines)
                     if "[CAPTURA_PREPARADA]" in line and "Sessão=" + session_id + ";" in line), None)
    saved = next((i for i, line in enumerate(lines)
                 if "[CAPTURA_SALVA]" in line and "Sessão=" + session_id + ";" in line), None)
    if prepared is None or saved is None or saved <= prepared:
        raise ValueError("Logs originais da sessao preparada/salva ausentes.")
    between = lines[prepared:saved + 1]
    started = [line for line in between if "[ACAO_INICIADA] Cena=" + code + ";" in line]
    completed = [line for line in between if "[ACAO_CONCLUIDA] Cena=" + code + ";" in line]
    if len(started) != 1 or len(completed) != 1 or between.index(started[0]) >= between.index(completed[0]):
        raise ValueError("Acao real nao iniciou/concluiu uma vez nessa sessao.")
    match = re.search(r"duração_ms=([0-9]+)(?:;|$)", started[0])
    if match is None or int(match.group(1)) <= 0:
        raise ValueError("Duracao real declarada no log ausente.")
    take = {"id": code, "subtomada": "principal", "sessao": session_id,
            "video": str(video), "sha256": video_hash, "legenda": str(srt), "sha256Legenda": srt_hash,
            "duracaoAcaoMs": int(match.group(1)),
            "proveniencia": {"sessaoXml": {"arquivo": str(paths[0]), "sha256": editor.hash_arquivo(paths[0])},
                             "resultadoXml": {"arquivo": str(paths[1]), "sha256": editor.hash_arquivo(paths[1])},
                             "eventos": [lines[prepared], started[0], completed[0], lines[saved]]}}
    arm_path = folder / "proveniencia-subtomada-armada.xml"
    save_path = folder / "proveniencia-subtomada-salva.xml"
    part_events = [row for row in lines if "[CAPTURA_SUBTOMADA_ARMADA]" in row and "Sessao=" + session_id + ";" in row]
    if arm_path.exists() or save_path.exists() or part_events:
        if code != "EP07_C03" or not arm_path.is_file() or not save_path.is_file() or len(part_events) != 1:
            raise ValueError("Proveniencia parcial por GUID ausente/divergente; nao tratar como principal.")
        arm_path, save_path = [editor.exigir_sem_vinculos(path) for path in (arm_path, save_path)]
        nodes = []
        for path in (arm_path, save_path):
            content = path.read_bytes()
            if len(content) > 12000 or b"<!DOCTYPE" in content.upper() or b"<!ENTITY" in content.upper():
                raise ValueError("Sidecar de parte fora do contrato XML seguro.")
            nodes.append(ET.fromstring(content))
        arm, proof = nodes
        part = proof.get("parte")
        source = dados / "cenas/subtomadas/EP07_C03" / (part or "") / "EP07_C03.json"
        snapshot = editor.exigir_sem_vinculos(folder / "source-snapshot.json")
        attrs = ("versao", "id", "cenaId", "parte", "arquivoFonte", "sha256Fonte", "snapshot",
                 "versaoCodigo", "duracaoConteudoMs", "contagemMs", "dataUtc", "integral", "exigeMontagemDuasPartes")
        if (arm.tag != "subtomada" or proof.tag != "subtomada" or proof.get("versao") != "1" or
                proof.get("id") != session_id or proof.get("cenaId") != code or
                part not in ("embarque_dinjer_chefe", "embarque_djanira_decolagem") or
                any(arm.get(key) != proof.get(key) for key in attrs) or
                arm.get("estado") != "armada" or proof.get("estado") != "salva" or
                Path(proof.get("arquivoFonte", "")) != source or Path(proof.get("snapshot", "")) != snapshot or
                proof.get("integral") != "false" or proof.get("exigeMontagemDuasPartes") != "true" or
                proof.get("duracaoConteudoMs") != "45000" or proof.get("contagemMs") != "3000" or
                take["duracaoAcaoMs"] != 45000 or session.get("duracaoMs") != "48000" or
                editor.hash_arquivo(snapshot) != proof.get("sha256Fonte") or
                Path(proof.get("video", "")) != video or Path(proof.get("legenda", "")) != srt or
                proof.get("sha256Video") != video_hash or proof.get("sha256Legenda") != srt_hash):
            raise ValueError("Sidecar nao confirma fonte/parte/GUID/hashes/conteudo45s salvos.")
        take.update(subtomada=part, integral=False, exigeMontagemDuasPartes=True,
                    provenienciaSubtomada={"armadaXml": {"arquivo": str(arm_path), "sha256": editor.hash_arquivo(arm_path)},
                                          "salvaXml": {"arquivo": str(save_path), "sha256": editor.hash_arquivo(save_path)},
                                          "snapshot": {"arquivo": str(snapshot), "sha256": proof.get("sha256Fonte")},
                                          "parte": part, "versaoCodigo": proof.get("versaoCodigo"),
                                          "hashNativeAntesArmEAposSave": True, "hashDuranteCapturaRegistrado": False})
    return take


def _quadros(proof: dict, take: dict) -> dict[int, dict]:
    # O leitor compartilhado normaliza ptsReal100ns sem alterar a prova CPU.
    if isinstance(proof, dict) and "hashesResultadosQuadros" in proof:
        refs = proof["hashesResultadosQuadros"]
        if not isinstance(refs, list) or [ref.get("arquivo") for ref in refs] != proof.get("resultadosQuadros"):
            raise ValueError("Referencias/hash dos resultados CPU divergem.")
        for ref in refs:
            path = editor.exigir_sem_vinculos(Path(ref["arquivo"]))
            if editor.hash_arquivo(path) != ref.get("sha256"):
                raise ValueError("Resultado de quadros CPU foi alterado.")
    return editor.quadros_com_prova_c01(proof, take)


def conferir_offset(proof: dict, take: dict) -> int:
    frames = _quadros(proof, take)
    last, first = (proof.get(key) for key in
                   ("ultimoQuadroContagemPts100ns", "primeiroQuadroLimpoPts100ns"))
    previous = editor.quadro_c01_conferido(frames, last)
    editor.quadro_c01_conferido(frames, first)
    if first != last + previous["duracao100ns"]:
        raise ValueError("Prova exige ultimo quadro de contagem e primeiro limpo ADJACENTES reais.")
    return (first + 9999) // 10000


def conferir_quadros_limpos(cut: dict) -> None:
    proof = cut.get("provaSemTexto")
    frames = _quadros(proof, cut["tomada"])
    points = proof.get("ptsConferidos100ns")
    if (not isinstance(points, list) or len(points) < 2 or
            any(type(point) is not int for point in points) or points != sorted(set(points))):
        raise ValueError("Conferencia limpa exige pelo menos dois quadros PTS ordenados reais.")
    start, end = cut["inicioMs"] * 10000, (cut["inicioMs"] + cut["duracaoMs"]) * 10000
    for point in points:
        frame = editor.quadro_c01_conferido(frames, point)
        if point >= end or point + frame["duracao100ns"] <= start:
            raise ValueError("Quadro limpo conferido esta fora do recorte.")
    if points[0] > start + 10000000 or points[-1] < end - 10000000:
        raise ValueError("Faltam quadros conferidos no primeiro/ultimo segundo do recorte.")


def criar_recorte(dados: Path, code: str, session_id: str, snapshot: dict,
                   prova_offset: dict, prova_sem_texto: dict,
                   duracao_ms: int | None = None, relativo_ms: int = 0) -> dict:
    """Vincular uma calibracao visual existente; nao extrair/inventar PTS."""
    take = tomada_real(dados, code, session_id)
    content = duracao_snapshot(snapshot, code)
    cut = {"id": code, "subtomada": "principal", "tomada": take,
           "fonteJsonSnapshot": copy.deepcopy(snapshot),
           "provaOffsetPTS": copy.deepcopy(prova_offset),
           "provaSemTexto": copy.deepcopy(prova_sem_texto),
           "inicioRelativoAoConteudoMs": relativo_ms,
           "inicioMs": conferir_offset(prova_offset, take) + relativo_ms,
           "duracaoMs": content - relativo_ms if duracao_ms is None else duracao_ms,
           "medidoNaImagem": True}
    episode, _ = identidade_cena(code)
    conferir_selecao([cut], dados, episode, [code] if code in SUPLEMENTARES else ())
    return cut


def conferir_telefone_ep05_c03(cuts: list[dict]) -> dict:
    """Excecao literal de duas locacoes: somente as cinco falas e tomadas autorizadas."""
    sources = {
        "46b43bef436d4cd5a7d79654bb3490d3": ("DJANIRA_POBRE", "2D8C1DA076FFBBE26B1FE7F633DC16188143D4846F39B51510EE570E3E9D6A50"),
        "1d90b9fa38194e97a59366095e7734e0": ("DINJER", "72569E9BC378B74CD66C7C4055485836BAA916BBA64D00456C97E49DAD0C5590"),
    }
    execution = editor.exigir_sem_vinculos(editor.RAIZ / "producao/cenas_serie/EP05/EP05_C03.execucao.json")
    execution_hash = "EFB027EC18BE34497653C67FE5ECCC287482C227F93C19C12354B606AB70FBEF"
    if editor.hash_arquivo(execution) != execution_hash:
        raise ValueError("EP05_C03 execucao original mudou; conferir fidelidade antes de alterar excecao.")
    original = editor.ler_json(execution)
    origin = original["origem"]
    script = editor.exigir_sem_vinculos(editor.RAIZ / "ROTEIRO_A_CALOTEIRA_DO_OESTE_15_EPISODIOS.md")
    script_hash = "E8FBF15EAAFBC8F0D74CBF39E9B9CBD30F8E9226B94749047762B6745274F36C"
    if origin.get("sha256Arquivo") != script_hash or editor.hash_arquivo(script) != script_hash:
        raise ValueError("Roteiro imutavel original divergiu da execucao EP05_C03.")
    blocks = original["roteiroOriginal"]
    schedule = [(0, 7000, "DINJER"), (7000, 4000, "DJANIRA_POBRE"),
                (11000, 10000, "DINJER"), (21000, 5000, "DJANIRA_POBRE"), (26000, 8000, "DINJER")]
    if len(cuts) != 5 or len(blocks) != 5:
        raise ValueError("Telefone EP05_C03 exige exatamente cinco falas contiguas 7/4/10/5/8s.")
    scenes = {}
    for cut, (start, duration, actor) in zip(cuts, schedule):
        take = cut.get("tomada", {})
        session = take.get("sessao")
        if cut.get("id") != "EP05_C03" or session not in sources or sources[session][0] != actor:
            raise ValueError("Telefone: ator/fonte/sessao nao corresponde a fala original.")
        snapshot = cut.get("fonteJsonSnapshot", {})
        if snapshot.get("sha256") != sources[session][1] or duracao_snapshot(snapshot, "EP05_C03") != 34000:
            raise ValueError("Telefone: somente os dois snapshots integrais autorizados de 34s.")
        if (cut.get("inicioRelativoAoConteudoMs", 0), cut.get("duracaoMs")) != (start, duration):
            raise ValueError("Telefone: conservar inicios e duracoes contiguos das cinco falas originais.")
        if session not in scenes:
            scene = editor.ler_json(Path(snapshot["arquivo"]))
            if [entry["personagem"] for entry in scene.get("elenco", [])] != [actor] or len(scene["roteiro"]) != 5:
                raise ValueError("Telefone: cada fonte deve conter somente sua propria atriz.")
            for block, (block_start, block_duration, block_actor), filmed in zip(blocks, schedule, scene["roteiro"]):
                original_actor = "DJANIRA_POBRE" if block["quemOriginal"] == "DJANIRA" else block["quemOriginal"]
                if (block["inicioMs"], block["duracaoMs"], original_actor) != (block_start, block_duration, block_actor):
                    raise ValueError("Telefone: cronologia da execucao original incoerente.")
                expected = {"tipo": "pausa", "duracaoSegundos": block_duration // 1000, "plano": "MEDIA"}
                if block_actor == actor:
                    expected.update(tipo="fala", texto=block["texto"], personagem=actor, gesto=block["gestoOriginal"])
                if filmed != expected:
                    raise ValueError("Telefone: fala/texto/gesto ou pausa da outra atriz divergiu do original.")
            scenes[session] = copy.deepcopy(snapshot)
    if set(scenes) != set(sources):
        raise ValueError("Telefone: conservar as duas sessoes distintas, sem reutilizar uma atriz na outra fala.")
    return {"execucaoOriginal": {"arquivo": str(execution), "sha256": execution_hash},
            "roteiroImutavel": {"arquivo": str(script), "sha256": script_hash},
            "fontesPorSessao": scenes, "falasOriginaisMs": [7000, 4000, 10000, 5000, 8000],
            "duracaoMs": 34000, "semAlterarTextoOrdemGestos": True}


def conferir_selecao(recortes: list[dict], dados: Path, episodio: str = "EP02", extras=(), somente_extras: bool = False) -> tuple[list[dict], list[str]]:
    expected = cenas_episodio(episodio, extras, somente_extras)
    if not isinstance(recortes, list) or not recortes:
        raise ValueError("Selecao precisa de tomadas reais, sem preenchimento.")
    used, previous, groups, capture_cache, snapshots = [], {}, [], {}, {}
    for number, cut in enumerate(recortes, 1):
        if not isinstance(cut, dict) or cut.get("id") not in expected:
            raise ValueError("Somente IDs originais e extras declarados do proprio episodio sao fontes.")
        code = cut["id"]
        if cut.get("subtomada") != "principal" or cut.get("grupoEditorial", code) != code:
            raise ValueError("Conservar o grupo da propria cena e tomada principal.")
        if cut.get("medidoNaImagem") is not True:
            raise ValueError("Offset precisa de medicao visual real.")
        _tempos(cut.get("inicioMs"), cut.get("duracaoMs"))
        take = cut.get("tomada")
        if not isinstance(take, dict) or take.get("id") != code or take.get("subtomada") != "principal":
            raise ValueError("Identidade da tomada diverge do recorte.")
        session_id = take.get("sessao")
        if not isinstance(session_id, str):
            raise ValueError("Sessao de captura ausente.")
        key = (code, session_id)
        if key not in capture_cache:
            actual = tomada_real(dados, *key)
            if actual.get("integral") is False or actual.get("subtomada") != "principal":
                raise ValueError("EP07_C03 parte editorial nao e cena integral. Exige excecao especifica duas fontes/45s; fluxo generico bloqueado.")
            actual["metadados"] = editor.metadados_mp4(Path(actual["video"]))
            if actual["metadados"]["sha256"] != actual["sha256"]:
                raise ValueError("MP4 original mudou durante a leitura.")
            capture_cache[key] = actual
        actual = capture_cache[key]
        if any(take.get(field) != actual[field] for field in ("video", "sha256", "legenda", "sha256Legenda")):
            raise ValueError("Tomada nao corresponde a sessao/MP4/SRT registrados.")
        content = duracao_snapshot(cut.get("fonteJsonSnapshot"), code)
        if content != actual["duracaoAcaoMs"]:
            raise ValueError("Snapshot nao corresponde a duracao registrada da acao real.")
        snapshot_key = cut["fonteJsonSnapshot"]["sha256"]
        if code in snapshots and snapshot_key not in snapshots[code] and code != "EP05_C03":
            raise ValueError("Partes da mesma cena usam snapshots diferentes.")
        snapshots.setdefault(code, set()).add(snapshot_key)
        offset = conferir_offset(cut.get("provaOffsetPTS"), take)
        relative = cut.get("inicioRelativoAoConteudoMs", 0)
        if type(relative) is not int or relative < 0 or cut["inicioMs"] != offset + relative:
            raise ValueError("Inicio precisa ser ceil do primeiro quadro limpo mais trecho relativo exato.")
        if relative + cut["duracaoMs"] > content:
            raise ValueError("Recorte ultrapassa a acao do snapshot.")
        if cut["inicioMs"] < previous.get(session_id, 0):
            raise ValueError("Recortes da mesma tomada sobrepostos ou fora de ordem.")
        previous[session_id] = cut["inicioMs"] + cut["duracaoMs"]
        if previous[session_id] > actual["metadados"]["duracaoMs"]:
            raise ValueError("Recorte ultrapassa MP4 real.")
        conferir_quadros_limpos(cut)
        if not used or used[-1] != code:
            if code in used or (used and expected.index(code) < expected.index(used[-1])):
                raise ValueError("Cenas devem manter ordem narrativa e partes contiguas.")
            used.append(code)
            groups.append({"id": code, "arquivo": code + "_LIMPO.mp4", "duracaoMs": 0, "recortes": []})
        groups[-1]["duracaoMs"] += cut["duracaoMs"]
        groups[-1]["recortes"].append(number)
        if groups[-1]["duracaoMs"] > content:
            raise ValueError("Soma de partes excede a duracao da propria cena.")
    if len(snapshots.get("EP05_C03", ())) > 1:
        telephone = [cut for cut in recortes if cut["id"] == "EP05_C03"]
        proof = conferir_telefone_ep05_c03(telephone)
        next(group for group in groups if group["id"] == "EP05_C03")["provaMontagemTelefone"] = proof
    return groups, [code for code in expected if code not in used]


def preparar_manifesto(plano_path: Path, dados: Path, episodio: str = "EP02", parcial: bool = False) -> dict:
    cenas_episodio(episodio)
    path = editor.exigir_sem_vinculos(plano_path.absolute())
    plan_hash = editor.hash_arquivo(path)
    plan = editor.ler_json(path)
    if plan.get("versao") != 1 or plan.get("tipo") != TIPO_SELECAO or plan.get("episodio") != episodio:
        raise ValueError("Esperada selecao_clips_limpos_episodio v1 de " + episodio)
    cuts = copy.deepcopy(plan.get("recortes"))
    groups, pending = conferir_selecao(cuts, dados, episodio, plan.get("extras", ()), plan.get("somenteExtras", False))
    if pending and not parcial:
        raise ValueError("Montagem final exige todas as cenas declaradas reais; faltam " + ", ".join(pending))
    for number, cut in enumerate(cuts, 1):
        cut["numeroRecorte"] = number
        cut["fonteSemTextoConferidaNaImagem"] = False
        cut["conteudoConferidoNaImagem"] = False
    if editor.hash_arquivo(path) != plan_hash:
        raise ValueError("Selecao mudou durante a leitura.")
    manifest = {"versao": 1, "tipo": TIPO, "episodio": episodio, "modo": copy.deepcopy(MODO),
            "planoSelecionado": str(path), "sha256PlanoSelecionado": plan_hash,
            "recortes": cuts, "clips": groups, "pendentes": pending,
            "duracaoMontagemMs": sum(cut["duracaoMs"] for cut in cuts),
            "parcial": bool(pending), "renderExecutado": False, "gpuSolicitada": False,
            "observacao": "Provas mostram apenas quadros amostrados; confirmar conteudo/fontes limpas antes de exportar."}
    if plan.get("somenteExtras", False):
        manifest["somenteExtras"] = True
    return manifest


def validar_manifesto(manifesto: dict, dados: Path) -> dict:
    if (manifesto.get("versao") != 1 or manifesto.get("tipo") != TIPO
            or manifesto.get("modo") != MODO):
        raise ValueError("Esperado manifesto silencioso sem cartelas/SRT/vinheta.")
    expected = preparar_manifesto(Path(manifesto["planoSelecionado"]), dados, manifesto.get("episodio"), parcial=True)
    if expected["pendentes"]:
        raise ValueError("Exportacao bloqueada: faltam cenas reais completas.")
    compared = copy.deepcopy(manifesto)
    cuts = compared.get("recortes")
    if not isinstance(cuts, list) or len(cuts) != len(expected["recortes"]):
        raise ValueError("Lista de recortes incompleta.")
    for cut in cuts:
        if cut.get("fonteSemTextoConferidaNaImagem") is not True or cut.get("conteudoConferidoNaImagem") is not True:
            raise ValueError("Fonte limpa/conteudo ainda nao conferidos na imagem.")
        cut["fonteSemTextoConferidaNaImagem"] = False
        cut["conteudoConferidoNaImagem"] = False
    if compared != expected:
        raise ValueError("Manifesto mudou: fontes, PTS, hashes, modo, ordem ou duracoes.")
    return manifesto


def conferir_duracao_composicao(actual: timedelta, length_ms: int, number_cuts: int) -> None:
    # Mesma quantizacao delimitada usada no exportador EP01 conferido: WinRT
    # mede 100ns; timedelta arredonda para us. Cada clip e verificado exato.
    if abs(actual - timedelta(milliseconds=length_ms)) > timedelta(microseconds=number_cuts):
        raise ValueError("Duracao nativa divergiu da soma exata dos recortes.")


async def exportar(manifesto: dict, dados: Path, folder: Path, montar: bool = False) -> dict:
    validar_manifesto(manifesto, dados)
    from winrt.windows.media.editing import MediaClip, MediaComposition, MediaTrimmingPreference
    from winrt.windows.media.mediaproperties import MediaEncodingProfile, VideoEncodingQuality
    from winrt.windows.media.transcoding import TranscodeFailureReason
    from winrt.windows.storage import CreationCollisionOption, StorageFile, StorageFolder

    cuts = manifesto["recortes"]
    groups = copy.deepcopy(manifesto["clips"])
    if montar:
        code = manifesto["episodio"] + ("_EXTRAS_MONTAGEM_LIMPA" if manifesto.get("somenteExtras", False) else "_MONTAGEM_LIMPA")
        groups.append({"id": code, "arquivo": code + ".mp4", "duracaoMs": manifesto["duracaoMontagemMs"],
                       "recortes": [cut["numeroRecorte"] for cut in cuts]})
    storage = await StorageFolder.get_folder_from_path_async(str(editor.exigir_sem_vinculos(folder.absolute())))
    outputs = []
    for group in groups:
        composition = MediaComposition()
        for number in group["recortes"]:
            cut = cuts[number - 1]
            video = editor.exigir_sem_vinculos(Path(cut["tomada"]["video"]))
            clip = await MediaClip.create_from_file_async(await StorageFile.get_file_from_path_async(str(video)))
            end = timedelta(milliseconds=cut["inicioMs"] + cut["duracaoMs"])
            if end > clip.original_duration:
                raise ValueError("Faltam quadros reais: " + cut["id"])
            clip.trim_time_from_start = timedelta(milliseconds=cut["inicioMs"])
            clip.trim_time_from_end = clip.original_duration - end
            clip.volume = 0
            if clip.trimmed_duration != timedelta(milliseconds=cut["duracaoMs"]):
                raise ValueError("Getter de recorte WinRT divergiu: " + cut["id"])
            composition.clips.append(clip)
        conferir_duracao_composicao(composition.duration, group["duracaoMs"], len(group["recortes"]))
        profile = MediaEncodingProfile.create_mp4(VideoEncodingQuality.H_D1080P)
        profile.video.frame_rate.numerator, profile.video.frame_rate.denominator = 30, 1
        profile.video.bitrate = 8000000
        destination = await storage.create_file_async(group["arquivo"], CreationCollisionOption.FAIL_IF_EXISTS)
        print("Render limpo: " + group["arquivo"], flush=True)
        failure = await composition.render_to_file_with_profile_async(destination, MediaTrimmingPreference.PRECISE, profile)
        if failure != TranscodeFailureReason.NONE:
            raise ValueError("Windows recusou render: " + str(failure))
        output = editor.metadados_mp4(folder / group["arquivo"])
        track = next(t for t in output["trilhas"] if t.get("tipo") == "vide")
        if abs(output["duracaoMs"] - group["duracaoMs"]) > 100 or (track["largura"], track["altura"]) != (1920, 1080):
            raise ValueError("Getter do MP4 limpo divergiu: " + group["id"])
        outputs.append({**group, "mp4": output})
    validar_manifesto(manifesto, dados)
    return {"tipo": TIPO, "episodio": manifesto["episodio"], "modo": copy.deepcopy(MODO),
            "renderExecutado": True, "duracaoMontagemMs": manifesto["duracaoMontagemMs"],
            "arquivos": outputs, "originaisPreservados": True}


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="acao", required=True)
    for command in ("preparar", "validar", "exportar"):
        sub = commands.add_parser(command)
        if command == "preparar":
            sub.add_argument("--episodio", choices=tuple(EPISODIOS), default="EP02")
            sub.add_argument("--plano", type=Path, required=True)
            sub.add_argument("--parcial", action="store_true", help="Permitir tomadas faltantes; bloqueia exportacao")
        else:
            sub.add_argument("--manifesto", type=Path, required=True)
        if command == "exportar":
            sub.add_argument("--montar", action="store_true")
    args = parser.parse_args(argv)
    folder = novo_trabalho("clips-limpos-" + args.acao)
    try:
        dados = editor.ler_pasta_dados(editor.RAIZ, exigir_perfil=True)
        if args.acao == "preparar":
            value = preparar_manifesto(args.plano, dados, args.episodio, args.parcial)
            editor.salvar_json(folder / (args.episodio + ".clips-limpos.manifesto.json"), value)
        else:
            manifesto = editor.ler_json(editor.exigir_sem_vinculos(args.manifesto.absolute()))
            if args.acao == "validar":
                validar_manifesto(manifesto, dados)
                value = {"validado": True, "episodio": manifesto["episodio"], "renderExecutado": False, "gpuSolicitada": False}
            else:
                value = asyncio.run(exportar(manifesto, dados, folder, args.montar))
        editor.salvar_json(folder / "resultado.json", value)
        print("Concluido: " + str(folder / "resultado.json"), flush=True)
        return 0
    except Exception as error:
        editor.salvar_json(folder / "erro.json", {"tipo": type(error).__name__, "erro": str(error), "originaisPreservados": True})
        print("Clips limpos interrompidos: " + str(error), file=sys.stderr, flush=True)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
