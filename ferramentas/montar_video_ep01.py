"""Edicao local de tomadas reais com WinRT; voces do Windows sao provisorias.

Nenhuma tomada, legenda, catalogo ou documento existente e modificado. Saidas
ficam em uma pasta nova de backups/montagem-video-ep01. O inicio do conteudo no
MP4 precisa ser medido na imagem: nao e inferido de ctime nem dos 3s de contagem.
Execute com ferramentas/.venv_edicao/Scripts/python.exe.
"""
from __future__ import annotations

import argparse
import asyncio
import copy
from datetime import datetime, timedelta, timezone
from decimal import Decimal
import hashlib
import io
import json
from pathlib import Path
import re
import shutil
import struct
import sys
import uuid
import wave
import xml.etree.ElementTree as ET

from exportar_edicao_ep01 import DURACOES_MS, verificar_fidelidade
from organizar_capturas import _boxes, validar_mp4
from preparar_edicao import _nome, _srt, ler_planos
from runtime_diretor import exigir_sem_vinculos, ler_pasta_dados
from validar_json import validar_cena_json

RAIZ = Path(__file__).resolve().parents[1]
SAIDAS = RAIZ / "backups" / "montagem-video-ep01"
VOZES_EP01_VERIFICADAS = SAIDAS / "20261005T061727219385Z-vozes-a13fb6fd" / "resultado.json"
CARTELA_C02 = {"cena": "EP01_C02", "textoOriginal": "QUATRO SEMANAS ANTES",
               "inicioCenaMs": 0, "duracaoMs": 4000,
               "posicaoPixels": {"x": 600, "y": 36, "largura": 720, "altura": 100}}
CARTELA_PREVIA = {"cena": "PREVIA_EP02", "textoOriginal": "NO PRÓXIMO EPISÓDIO",
                  "inicioCenaMs": 0, "duracaoMs": 5000,
                  "posicaoPixels": {"x": 600, "y": 36, "largura": 720, "altura": 100}}
EDICAO_C01 = {"tipo": "recortes_reais_C01_14s", "conteudoBrutoMs": 60000,
              "duracaoSelecionadaMs": 14000, "reducaoTimelineMs": 46000,
              "semAlterarVelocidade": True,
              "acoesObrigatorias": ["voo", "saida", "queda", "emersao", "rampa", "corrida"]}


def hash_arquivo(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest().upper()


def ler_json(path: Path):
    def pares(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError("Chave duplicada: " + key)
            result[key] = value
        return result
    return json.loads(path.read_text("utf-8-sig"), object_pairs_hook=pares,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError(value)))


def novo_trabalho(tipo: str) -> Path:
    exigir_sem_vinculos(SAIDAS)
    folder = SAIDAS / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
                       + "-" + tipo + "-" + uuid.uuid4().hex[:8])
    folder.mkdir(parents=True, exist_ok=False)
    return folder


def escrever(path: Path, data: bytes) -> None:
    with path.open("xb") as stream:
        stream.write(data)


def salvar_json(path: Path, value) -> None:
    escrever(path, (json.dumps(value, ensure_ascii=False, allow_nan=False, indent=2)
                   + "\n").encode("utf-8"))


def ms(value) -> int:
    exact = Decimal(str(value)) * 1000
    if exact != exact.to_integral_value():
        raise ValueError("Tempo precisa de resolucao exata de milissegundos.")
    return int(exact)


def metadados_mp4(path: Path) -> dict:
    exigir_sem_vinculos(path)
    result = {"arquivo": str(path), "bytes": path.stat().st_size,
              "sha256": hash_arquivo(path), "decodificado": False, "trilhas": []}
    with path.open("rb") as stream:
        result["duracaoMs"] = validar_mp4(stream)
        def boxes(start, end):
            return list(_boxes(stream, start, end))
        def raw(start, end):
            stream.seek(start)
            return stream.read(end - start)
        def duracao(start, end):
            data = raw(start, end)
            index = 12 if data[0] == 0 else 20
            scale = struct.unpack_from(">I", data, index)[0]
            duration = struct.unpack_from(">I" if data[0] == 0 else ">Q", data, index + 4)[0]
            return duration / scale
        top = boxes(0, result["bytes"])
        result["boxes"] = [kind.decode("ascii") for kind, _, _ in top]
        for kind, start, end in top:
            if kind != b"moov":
                continue
            for kind2, start2, end2 in boxes(start, end):
                if kind2 == b"mvhd":
                    result["duracaoSegundos"] = duracao(start2, end2)
                if kind2 != b"trak":
                    continue
                track = {}
                for kind3, start3, end3 in boxes(start2, end2):
                    if kind3 == b"tkhd":
                        data = raw(start3, end3)
                        track["largura"], track["altura"] = [v / 65536 for v in struct.unpack_from(">II", data, len(data) - 8)]
                    if kind3 != b"mdia":
                        continue
                    for kind4, start4, end4 in boxes(start3, end3):
                        if kind4 == b"mdhd":
                            track["duracaoSegundos"] = duracao(start4, end4)
                        if kind4 == b"hdlr":
                            track["tipo"] = raw(start4, end4)[8:12].decode("ascii")
                        if kind4 != b"minf":
                            continue
                        for kind5, start5, end5 in boxes(start4, end4):
                            if kind5 != b"stbl":
                                continue
                            for kind6, start6, end6 in boxes(start5, end5):
                                if kind6 == b"stsd":
                                    track["codec"] = boxes(start6 + 8, end6)[0][0].decode("ascii")
                                if kind6 == b"stsz":
                                    track["amostras"] = struct.unpack_from(">I", raw(start6, end6), 8)[0]
                result["trilhas"].append(track)
    return result


def tomadas_reais(dados: Path) -> list[dict]:
    """Aceitar apenas resultados salvos do organizador com hashes coincidentes."""
    output = []
    log = (dados / "logs/DiretorGTA.log").read_text("utf-8-sig")
    parts = {}
    for line in log.splitlines():
        if "[SUBTOMADA_GRAVACAO]" in line:
            match = re.search(r"parte=(hospital|cassino);.*? SRT=(.*?);", line)
            if match:
                parts[match.group(2)] = match.group(1)
    for result_path in sorted((dados / "logs/capturas").glob("*/resultado.xml")):
        outcome = ET.parse(result_path).getroot()
        session_path = result_path.with_name("sessao.xml")
        if outcome.get("status") != "salvo" or not session_path.is_file():
            continue
        session = ET.parse(session_path).getroot()
        code = session.get("cenaId", "")
        if not re.fullmatch(r"(?:EP01_C0[1-9]|EP02_C0[12]|VINHETA_01)", code):
            continue
        if session.get("estado") != "concluida" or outcome.get("id") != session.get("id"):
            raise ValueError("Resultado/session de captura divergente: " + str(result_path))
        episode, number = (0, 0) if code == "VINHETA_01" else (int(code[2:4]), int(code[6:8]))
        if session.get("episodio") != str(episode) or session.get("numero") != str(number):
            raise ValueError("ID da captura diverge do episodio/numero: " + code)
        video, srt = Path(outcome.attrib["video"]), Path(outcome.attrib["legenda"])
        exigir_sem_vinculos(video)
        exigir_sem_vinculos(srt)
        episode_folder = "COMPARTILHADOS" if code == "VINHETA_01" else "EP" + code[2:4]
        expected = dados / "Serie/A_CALOTEIRA_1_TEMP" / episode_folder
        if video.parent != expected or srt.parent != expected or video.stem != srt.stem:
            raise ValueError("Tomada fora da pasta do seu episodio/compartilhados ou sem legenda correspondente.")
        if hash_arquivo(video) != outcome.get("sha256Video") or hash_arquivo(srt) != outcome.get("sha256Legenda"):
            raise ValueError("Hash da tomada mudou: " + str(video))
        source_srt = session.find("srt")
        part = parts.get(source_srt.get("caminho", "")) if source_srt is not None else None
        output.append({"id": code, "subtomada": part if code == "EP01_C05" else "principal",
                       "sessao": session.get("id"), "armadaUtc": session.get("armadaUtc"),
                       "video": str(video), "legenda": str(srt), "sha256": outcome.get("sha256Video"),
                       "sha256Legenda": outcome.get("sha256Legenda"),
                       "inicioConteudoNoVideoMs": None, "medidoNaImagem": False})
    return output


def preparar_extras_editoriais(dados: Path, production: dict, captures: list[dict]) -> dict:
    """Planejar extras reais sem criar voz, video ou tempo de preenchimento."""
    def parte(code: str, length: int, source_path: Path) -> dict:
        scene = validar_cena_json(source_path, dados / "catalogos")
        verificar_fidelidade(scene, production["planos"][code])
        total = sum(ms(b["duracaoSegundos"]) for b in scene["roteiro"])
        candidates = sorted((x for x in captures if x["id"] == code and x["subtomada"] == "principal"),
                            key=lambda x: x["armadaUtc"])
        if total < length:
            raise ValueError("Fonte do extra editorial e menor que seu recorte: " + code)
        if code == "VINHETA_01" and (total != 20000 or len(scene["roteiro"]) != 4 or
                any(b["tipo"] != "pausa" or ms(b["duracaoSegundos"]) != 5000 for b in scene["roteiro"])):
            raise ValueError("VINHETA_01 precisa das quatro dancas originais de5s, sem falas.")
        return {"id": code, "subtomada": "principal", "duracaoSelecionadaMs": length,
                "fonte": {"json": str(source_path), "sha256": hash_arquivo(source_path), "conteudoBrutoMs": total},
                "tomada": candidates[-1] if candidates else None, "inicioConteudoNoVideoMs": None,
                "inicioRelativoConteudoSelecionadoMs": None, "medidoNaImagem": False,
                "conteudoConferidoNaImagem": False, "provaOffsetPTS": None}
    credits = production["planos"]["VINHETA_01"]["creditosOriginais"].splitlines()
    if len(credits) != 8:
        raise ValueError("Esperados os oito textos originais dos creditos.")
    cards = [{"cena": "VINHETA_01", "textoOriginal": text, "inicioCenaMs": i * 2500,
              "duracaoMs": 2500, "posicaoPixels": {"x": 600, "y": 36, "largura": 720, "altura": 100}}
             for i, text in enumerate(credits)]
    return {"previaEP02": {"id": "PREVIA_EP02", "duracaoMs": 5000, "semDublagem": True,
                           "cartela": copy.deepcopy(CARTELA_PREVIA), "partes": [
                               parte("EP02_C01", 2000, RAIZ / "producao/cenas_serie/EP02/EP02_C01.json"),
                               parte("EP02_C02", 3000, RAIZ / "producao/cenas_serie/EP02/EP02_C02.json")]},
            "vinheta": {"id": "VINHETA_01", "duracaoMs": 20000, "semDublagem": True,
                        "creditos": cards, "partes": [
                            parte("VINHETA_01", 20000, RAIZ / "producao/cenas_serie/compartilhados/VINHETA_01.json")]}}


def preparar_plano(dados: Path) -> dict:
    production = ler_planos()
    captures = tomadas_reais(dados)
    sources, voices, cues, sequence = [], [], [], []
    cursor = 0
    for number in range(1, 10):
        code = f"EP01_C{number:02d}"
        source_paths = [(RAIZ / f"producao/cenas_serie/EP01/{code}.json", "principal")]
        if number == 5:
            source_paths = [(RAIZ / f"producao/subtomadas/{code}/{part}/{code}.json", part)
                            for part in ("hospital", "cassino")]
        selected_parts = []
        reference_scene = None
        original = None
        for path, part in source_paths:
            scene = validar_cena_json(path, dados / "catalogos")
            current_original = verificar_fidelidade(scene, production["planos"][code])
            total = sum(ms(block["duracaoSegundos"]) for block in scene["roteiro"])
            if total != DURACOES_MS[number]:
                raise ValueError("Duracao atual inesperada em " + code)
            if reference_scene is not None and [(b["tipo"], b["duracaoSegundos"], b.get("texto")) for b in reference_scene["roteiro"]] != [(b["tipo"], b["duracaoSegundos"], b.get("texto")) for b in scene["roteiro"]]:
                raise ValueError("Subtomadas C05 precisam da mesma voz original unica.")
            reference_scene, original = scene, current_original
            candidates = sorted((x for x in captures if x["id"] == code and x["subtomada"] == part),
                                key=lambda x: x["armadaUtc"])
            selected = candidates[-1] if candidates else None
            length = (3000 if part == "hospital" else 5000) if number == 5 else total
            selected_parts.append({"subtomada": part, "duracaoSelecionadaMs": length,
                                   "inicioRelativoConteudoSelecionadoMs": None if number == 5 else 0,
                                   "tomada": selected, "inicioConteudoNoVideoMs": None,
                                   "medidoNaImagem": False})
            sources.append({"id": code, "subtomada": part, "json": str(path),
                            "sha256": hash_arquivo(path), "conteudoBrutoMs": total})
        assert reference_scene is not None and original is not None
        local, original_index = 0, 0
        for block in reference_scene["roteiro"]:
            end = local + ms(block["duracaoSegundos"])
            if block["tipo"] != "pausa":
                item = original[original_index]
                original_index += 1
                who = item["quemOriginal"]
                voice = "Microsoft Daniel" if who.startswith("NARRADOR") or who.startswith("COLEGA_") else "Microsoft Maria"
                label = "Narrador" if block["tipo"] != "fala" else _nome(who, "fala")
                voices.append({"id": f"{code}_B{original_index:02d}", "cena": code, "tipo": item["tipo"],
                               "quemOriginal": who, "textoOriginal": item["texto"],
                               "inicioCenaMs": local, "fimCenaMs": end, "inicioEpisodioMs": cursor + local,
                               "fimEpisodioMs": cursor + end, "duracaoMs": end - local,
                               "vozProvisoria": voice, "boca": block["tipo"] == "fala"})
                cues.append({"inicioMs": cursor + local, "fimMs": cursor + end,
                             "legenda": label + ": " + item["texto"]})
            local = end
        sequence.append({"id": code, "inicioMs": cursor, "fimMs": cursor + local,
                         "duracaoMs": local, "partes": selected_parts})
        cursor += local
    return {"versao": 1, "tipo": "montagem_local_EP01", "temporada": 1, "episodio": 1,
            "fonteRoteiroSHA256": hash_arquivo(RAIZ / "ROTEIRO_A_CALOTEIRA_DO_OESTE_15_EPISODIOS.md"),
            "fontes": sources, "ordemNarrativa": sequence, "vozes": voices, "legendas": cues,
            "cartelas": [copy.deepcopy(CARTELA_C02)],
            "extrasEditoriais": preparar_extras_editoriais(dados, production, captures),
            "duracaoNoveCenasMs": cursor, "contagemNaMontagemMs": 0,
            "duracaoNoveCenasMaisPreviaMs": cursor + 5000,
            "duracaoIntegralComPreviaVinhetaMs": cursor + 25000,
            "todasTomadasExistem": all(p["tomada"] for s in sequence for p in s["partes"]),
            "todosOffsetsMedidos": False, "episodioFinalCompleto": False,
            "limitacoes": ["Vozes sinteticas provisorias; atuacao/estetica nao aprovadas.",
                           "C01 preserva 14s de voz +46s sem voz para fisica; nao congela quadro para preencher cena ausente.",
                           "C05 usa 3s hospital +5s cassino, medidos em duas tomadas reais; voz8s unica.",
                           "Cada MP4 tem inicio de conteudo proprio medido na imagem; 3s nao sao um offset automatico.",
                           "Cartela QUATRO SEMANAS ANTES sobreposta nos primeiros4s deC02; nao acrescenta duracao.",
                           "Previa EP02 de5s +VINHETA_01 real20s com creditos8 formam205s; extras ausentes impedem montagem integral."]}


def edicao_original_159(plano: dict) -> bool:
    if "edicaoC01" not in plano:
        return False
    if plano["edicaoC01"] != EDICAO_C01:
        raise ValueError("Edicao C01 precisa preservar bruto60s, recortes14s e as seis acoes originais.")
    return True


def transformar_plano_159(tecnico: dict) -> dict:
    """Copiar o plano tecnico180s, preservando tomadas/calibracoes e a voz original."""
    if edicao_original_159(tecnico):
        raise ValueError("Plano159 ja transformado; nao deslocar sua linha do tempo novamente.")
    plan = copy.deepcopy(tecnico)
    sequence = plan.get("ordemNarrativa")
    if (plan.get("duracaoNoveCenasMs") != 180000 or not isinstance(sequence, list) or
            [s.get("id") for s in sequence] != [f"EP01_C{n:02d}" for n in range(1, 10)]):
        raise ValueError("Transformacao159 exige as nove cenas do plano tecnico180s.")
    cursor = 0
    for row in sequence:
        if row.get("inicioMs") != cursor or row.get("fimMs") != cursor + row.get("duracaoMs", 0):
            raise ValueError("Linha do tempo tecnica inconsistente: " + row["id"])
        cursor = row["fimMs"]
    first = sequence[0]
    if (cursor != 180000 or first["duracaoMs"] != 60000 or len(first.get("partes", [])) != 1 or
            first["partes"][0].get("subtomada") != "principal" or
            first["partes"][0].get("duracaoSelecionadaMs") != 60000):
        raise ValueError("C01 precisa conservar o bruto tecnico60s antes da edicao14s.")
    first.update(fimMs=14000, duracaoMs=14000)
    first["partes"][0]["duracaoSelecionadaMs"] = 14000
    for row in sequence[1:]:
        row["inicioMs"] -= 46000
        row["fimMs"] -= 46000
    for voice in plan["vozes"]:
        if voice["cena"] == "EP01_C01":
            if voice["inicioCenaMs"] != 0 or voice["fimCenaMs"] != 14000 or voice["duracaoMs"] != 14000:
                raise ValueError("C01 exige a narracao original unica de14s, sem corte de voz.")
        else:
            voice["inicioEpisodioMs"] -= 46000
            voice["fimEpisodioMs"] -= 46000
    if sum(v["cena"] == "EP01_C01" for v in plan["vozes"]) != 1:
        raise ValueError("C01 exige exatamente uma voz original.")
    for cue in plan["legendas"]:
        if cue["inicioMs"] >= 60000:
            cue["inicioMs"] -= 46000
            cue["fimMs"] -= 46000
        elif cue["fimMs"] > 14000:
            raise ValueError("Legenda de C01 ultrapassa sua voz original14s.")
    plan.update(edicaoC01=copy.deepcopy(EDICAO_C01), recortesC01=[],
                duracaoNoveCenasMs=134000, duracaoNoveCenasMaisPreviaMs=139000,
                duracaoIntegralComPreviaVinhetaMs=159000, todosOffsetsMedidos=False,
                episodioFinalCompleto=False)
    plan["limitacoes"] = [x for x in plan.get("limitacoes", [])
                          if not x.startswith("C01 preserva") and "formam205s" not in x] + [
        "C01 final usa recortes reais ordenados das seis acoes somando14s, sem acelerar; voz14s unica intacta.",
        "RecortesC01 pendentes bloqueiam montagem: preencher somente com PTS/quadros/sessao/hashes reais.",
        "Nove cenas134s +previa5s +vinheta20s formam159s originais; plano205s e historico tecnico."]
    return plan


async def gerar_cartela_texto(folder: Path, definition: dict, filename: str, font_size: int = 48) -> dict:
    """Desenhar somente texto com GDI em memoria; PNG pelo encoder do Windows."""
    import ctypes as ct
    from winrt.windows.graphics.imaging import BitmapEncoder, BitmapDecoder, BitmapPixelFormat, BitmapAlphaMode
    from winrt.windows.storage import StorageFolder, CreationCollisionOption, FileAccessMode
    if sys.platform != "win32":
        raise ValueError("Cartela usa somente APIs nativas do Windows.")
    width, height = definition["posicaoPixels"]["largura"], definition["posicaoPixels"]["altura"]
    text = definition["textoOriginal"]
    class Header(ct.Structure):
        _fields_ = [("size", ct.c_uint32), ("width", ct.c_int32), ("height", ct.c_int32),
                    ("planes", ct.c_uint16), ("bits", ct.c_uint16), ("compression", ct.c_uint32),
                    ("image", ct.c_uint32), ("x", ct.c_int32), ("y", ct.c_int32),
                    ("colors", ct.c_uint32), ("important", ct.c_uint32)]
    class Info(ct.Structure):
        _fields_ = [("header", Header), ("colors", ct.c_uint32 * 1)]
    class Size(ct.Structure):
        _fields_ = [("cx", ct.c_int32), ("cy", ct.c_int32)]
    gdi = ct.WinDLL("gdi32", use_last_error=True)
    signatures = {
        "CreateCompatibleDC": (ct.c_void_p, [ct.c_void_p]),
        "CreateDIBSection": (ct.c_void_p, [ct.c_void_p, ct.POINTER(Info), ct.c_uint,
                                           ct.POINTER(ct.c_void_p), ct.c_void_p, ct.c_uint]),
        "CreateFontW": (ct.c_void_p, [ct.c_int] * 13 + [ct.c_wchar_p]),
        "SelectObject": (ct.c_void_p, [ct.c_void_p, ct.c_void_p]),
        "SetTextColor": (ct.c_uint32, [ct.c_void_p, ct.c_uint32]),
        "SetBkMode": (ct.c_int, [ct.c_void_p, ct.c_int]),
        "GetTextExtentPoint32W": (ct.c_int, [ct.c_void_p, ct.c_wchar_p, ct.c_int, ct.POINTER(Size)]),
        "GetTextFaceW": (ct.c_int, [ct.c_void_p, ct.c_int, ct.c_wchar_p]),
        "TextOutW": (ct.c_int, [ct.c_void_p, ct.c_int, ct.c_int, ct.c_wchar_p, ct.c_int]),
        "DeleteObject": (ct.c_int, [ct.c_void_p]), "DeleteDC": (ct.c_int, [ct.c_void_p])}
    for name, (restype, argtypes) in signatures.items():
        getattr(gdi, name).restype, getattr(gdi, name).argtypes = restype, argtypes
    dc = gdi.CreateCompatibleDC(None)
    if not dc:
        raise OSError(ct.get_last_error(), "CreateCompatibleDC")
    bitmap = font = old_bitmap = old_font = None
    try:
        info = Info(Header(40, width, -height, 1, 32, 0, width * height * 4, 0, 0, 0, 0))
        pixels = ct.c_void_p()
        bitmap = gdi.CreateDIBSection(dc, ct.byref(info), 0, ct.byref(pixels), None, 0)
        if not bitmap or not pixels.value:
            raise OSError(ct.get_last_error(), "CreateDIBSection")
        old_bitmap = gdi.SelectObject(dc, bitmap)
        background = bytes((28, 24, 21, 255)) * width * height
        ct.memmove(pixels, background, len(background))
        font = gdi.CreateFontW(-font_size, 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 4, 0, "Segoe UI")
        if not font:
            raise OSError(ct.get_last_error(), "CreateFontW")
        old_font = gdi.SelectObject(dc, font)
        size, face = Size(), ct.create_unicode_buffer(128)
        if not gdi.GetTextExtentPoint32W(dc, text, len(text), ct.byref(size)):
            raise OSError(ct.get_last_error(), "GetTextExtentPoint32W")
        if size.cx > width - 40 or size.cy > height - 20:
            raise ValueError("Texto da cartela nao cabe; nenhuma letra sera cortada.")
        gdi.GetTextFaceW(dc, len(face), face)
        gdi.SetTextColor(dc, 0x00FFFFFF)
        gdi.SetBkMode(dc, 1)
        if not gdi.TextOutW(dc, (width - size.cx) // 2, (height - size.cy) // 2, text, len(text)):
            raise OSError(ct.get_last_error(), "TextOutW")
        data = ct.string_at(pixels, width * height * 4)
        font_info = {"familiaAplicada": face.value, "alturaPixels": font_size,
                     "textoLarguraPixels": size.cx, "textoAlturaPixels": size.cy}
    finally:
        if old_font:
            gdi.SelectObject(dc, old_font)
        if old_bitmap:
            gdi.SelectObject(dc, old_bitmap)
        if font:
            gdi.DeleteObject(font)
        if bitmap:
            gdi.DeleteObject(bitmap)
        gdi.DeleteDC(dc)
    path = folder / filename
    file = await (await StorageFolder.get_folder_from_path_async(str(folder))).create_file_async(
        path.name, CreationCollisionOption.FAIL_IF_EXISTS)
    stream = await file.open_async(FileAccessMode.READ_WRITE)
    try:
        encoder = await BitmapEncoder.create_async(BitmapEncoder.png_encoder_id, stream)
        encoder.set_pixel_data(BitmapPixelFormat.BGRA8, BitmapAlphaMode.IGNORE, width, height, 96, 96, data)
        await encoder.flush_async()
        await stream.flush_async()
    finally:
        stream.close()
    stream = await file.open_async(FileAccessMode.READ)
    try:
        decoder = await BitmapDecoder.create_async(stream)
        if (decoder.pixel_width, decoder.pixel_height) != (width, height):
            raise ValueError("Getter de PNG da cartela divergiu.")
    finally:
        stream.close()
    return {**copy.deepcopy(definition), "arquivo": str(path), "sha256": hash_arquivo(path),
            "larguraPNG": width, "alturaPNG": height, "fonte": font_info,
            "geracao": "GDI em memoria + BitmapEncoder PNG do Windows; sem UI nem imagem generativa"}


async def gerar_cartela_c02(folder: Path) -> dict:
    return await gerar_cartela_texto(folder, CARTELA_C02, "C02_QUATRO_SEMANAS_ANTES.png")


def ler_vozes_verificadas(plano: dict, manifesto: Path) -> dict[str, dict]:
    source = ler_json(exigir_sem_vinculos(manifesto))
    rows = source.get("vozes")
    if source.get("provisorias") is not True or not isinstance(rows, list):
        raise ValueError("Manifesto de vozes provisorias invalido.")
    by_id = {row.get("id"): row for row in rows}
    expected = {row["id"]: row for row in plano["vozes"]}
    if len(by_id) != len(rows) or set(by_id) != set(expected):
        raise ValueError("As17 vozes verificadas precisam corresponder ao plano integral.")
    for code, item in expected.items():
        row = by_id[code]
        original = dict(item)
        if edicao_original_159(plano) and item["cena"] != "EP01_C01":
            original["inicioEpisodioMs"] += 46000
            original["fimEpisodioMs"] += 46000
        if any(row.get(key) != value for key, value in original.items()):
            raise ValueError("Texto, tempo ou voz verificada mudou: " + code)
        if row.get("textoCortado") is not False or row.get("cabeNoTrecho") is not True or row.get("idioma") != "pt-BR":
            raise ValueError("Voz verificada nao preserva fala completa: " + code)
        audio = row.get("audio", {})
        path = exigir_sem_vinculos(Path(audio.get("arquivo", "")))
        if hash_arquivo(path) != audio.get("sha256"):
            raise ValueError("Hash de voz verificada divergiu: " + code)
        with wave.open(str(path), "rb") as stream:
            facts = {"quadros": stream.getnframes(), "amostrasHz": stream.getframerate(),
                     "canais": stream.getnchannels(), "bytesAmostra": stream.getsampwidth()}
            duration = stream.getnframes() / stream.getframerate() * 1000
        if any(audio.get(key) != value for key, value in facts.items()) or duration > item["duracaoMs"]:
            raise ValueError("Getter ou duracao de voz verificada divergiu: " + code)
    return by_id


def copiar_voz_verificada(row: dict, mapped: dict, folder: Path, manifesto: Path) -> dict:
    output = copy.deepcopy(row)
    source = exigir_sem_vinculos(Path(row["audio"]["arquivo"]))
    path = folder / (row["id"] + "_voz_provisoria_verificada.wav")
    with source.open("rb") as input_stream, path.open("xb") as output_stream:
        shutil.copyfileobj(input_stream, output_stream)
    if hash_arquivo(path) != row["audio"]["sha256"] or hash_arquivo(source) != row["audio"]["sha256"]:
        raise ValueError("A copia alterou a voz original verificada.")
    output.update(mapped)
    output["audio"]["arquivo"] = str(path)
    output.update(reutilizadaSemSintese=True, manifestoVozOriginal=str(manifesto),
                  arquivoVozOriginal=str(source))
    return output


async def adicionar_cartela_texto(composition, definition: dict, inicio_montagem_ms: int,
                                 folder: Path, filename: str, font_size: int = 48) -> dict:
    from winrt.windows.media.editing import MediaClip, MediaOverlay, MediaOverlayLayer
    from winrt.windows.foundation import Rect
    from winrt.windows.storage import StorageFile
    before = composition.duration
    card = await gerar_cartela_texto(folder, definition, filename, font_size)
    clip = await MediaClip.create_from_image_file_async(
        await StorageFile.get_file_from_path_async(card["arquivo"]), timedelta(milliseconds=definition["duracaoMs"]))
    pos = definition["posicaoPixels"]
    rectangle_values = [pos[k] for k in ("x", "y", "largura", "altura")]
    overlay = MediaOverlay(clip, Rect(*rectangle_values), 1.0)
    overlay.delay = timedelta(milliseconds=inicio_montagem_ms)
    overlay.audio_enabled = False
    layer = MediaOverlayLayer()
    layer.overlays.append(overlay)
    composition.overlay_layers.append(layer)
    rectangle = overlay.position
    getters = {"posicaoPixels": [rectangle.x, rectangle.y, rectangle.width, rectangle.height],
               "duracaoMs": round(overlay.clip.trimmed_duration.total_seconds() * 1000),
               "inicioMontagemMs": round(overlay.delay.total_seconds() * 1000),
               "opacidade": overlay.opacity, "audioHabilitado": overlay.audio_enabled}
    if (getters["posicaoPixels"] != rectangle_values or getters["duracaoMs"] != definition["duracaoMs"] or
            getters["inicioMontagemMs"] != inicio_montagem_ms or getters["opacidade"] != 1 or
            getters["audioHabilitado"] or composition.duration != before):
        raise ValueError("Getters da cartela ou duracao da composicao divergiram.")
    return {**card, "getters": getters, "alteraDuracaoDaMontagem": False}


async def adicionar_cartela_c02(composition, positions: dict, folder: Path) -> list[dict]:
    if "EP01_C02" not in positions:
        return []
    return [await adicionar_cartela_texto(composition, CARTELA_C02, positions["EP01_C02"],
                                         folder, "C02_QUATRO_SEMANAS_ANTES.png")]


async def bytes_stream(stream) -> bytes:
    from winrt.windows.storage.streams import DataReader
    reader = DataReader(stream.get_input_stream_at(0))
    try:
        count = await reader.load_async(stream.size)
        if count != stream.size:
            raise ValueError("Stream WinRT incompleto.")
        value = bytearray(count)
        reader.read_bytes(value)
        return bytes(value)
    finally:
        reader.detach_stream()
        reader.close()


async def sintetizar(item: dict, folder: Path, ajustar_taxa: bool) -> dict:
    from winrt.windows.media.speechsynthesis import SpeechSynthesizer
    synth = SpeechSynthesizer()
    voice = next((v for v in SpeechSynthesizer.all_voices if v.display_name == item["vozProvisoria"]
                  and v.language == "pt-BR"), None)
    if voice is None:
        raise ValueError("Voz pt-BR ausente: " + item["vozProvisoria"])
    synth.voice = voice
    rate, attempts = 1.0, []
    try:
        for attempt in range(1, 5):
            synth.options.speaking_rate = rate
            stream = await synth.synthesize_text_to_stream_async(item["textoOriginal"])
            try:
                data = await bytes_stream(stream)
            finally:
                stream.close()
            path = folder / (item["id"] + f"_voz_provisoria_t{attempt}.wav")
            escrever(path, data)
            with wave.open(io.BytesIO(data)) as audio:
                duration = audio.getnframes() / audio.getframerate()
                audio_info = {"amostrasHz": audio.getframerate(), "canais": audio.getnchannels(),
                              "bytesAmostra": audio.getsampwidth(), "quadros": audio.getnframes()}
            current = {"arquivo": str(path), "sha256": hash_arquivo(path), "duracaoMs": round(duration * 1000),
                       "taxaFala": rate, **audio_info}
            attempts.append(current)
            fits = duration * 1000 <= item["duracaoMs"]
            if fits or not ajustar_taxa:
                break
            candidate = rate * duration * 1000 / item["duracaoMs"] * 1.03
            if candidate > 2.5:
                break
            rate = round(candidate, 4)
        return {**item, "provisoria": True, "vozId": voice.id, "idioma": voice.language,
                "audio": current, "tentativas": attempts, "textoCortado": False,
                "cabeNoTrecho": fits, "silencioFinalMs": max(0, item["duracaoMs"] - current["duracaoMs"]),
                "aprovacaoAtuacao": "pendente"}
    finally:
        synth.close()


async def abrir_composicao(video: Path):
    from winrt.windows.media.editing import MediaClip, MediaComposition
    from winrt.windows.storage import StorageFile
    clip = await MediaClip.create_from_file_async(await StorageFile.get_file_from_path_async(str(video)))
    composition = MediaComposition()
    composition.clips.append(clip)
    return composition, clip


async def extrair_quadros(video: Path, tempos: list[int], folder: Path) -> dict:
    from winrt.windows.media.editing import VideoFramePrecision
    composition, clip = await abrir_composicao(video)
    samples = []
    for time_ms in tempos:
        if time_ms < 0 or timedelta(milliseconds=time_ms) >= clip.original_duration:
            raise ValueError("Amostra fora da duracao do video.")
        stream = await composition.get_thumbnail_async(timedelta(milliseconds=time_ms), 960, 540,
                                                       VideoFramePrecision.NEAREST_FRAME)
        try:
            data = await bytes_stream(stream)
        finally:
            stream.close()
        suffix = ".png" if data.startswith(b"\x89PNG") else ".jpg"
        path = folder / f"{video.stem}_{time_ms:06d}ms{suffix}"
        escrever(path, data)
        samples.append({"tempoSolicitadoMs": time_ms, "arquivo": str(path), "sha256": hash_arquivo(path)})
    return {"original": metadados_mp4(video), "gettersWinRT": {"duracao": str(clip.original_duration),
            "perfil": str(clip.get_video_encoding_properties().subtype)}, "quadros": samples}


def extrair_quadros_pts(video: Path, tempos: list[int], folder: Path) -> dict:
    """Decodificar em software e registrar PTS real; nao usar o tempo do thumbnail.

    Interfaces/GUIDs: microsoft/win32metadata, WinSDK mfapi.h, mfobjects.h,
    mfreadwrite.h. MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING e conversao RGB32
    em software; nenhum D3D manager ou hardware transform e solicitado.
    """
    import ctypes as c
    import time
    from ctypes import wintypes as w
    class GUID(c.Structure):
        _fields_ = [("a", c.c_uint32), ("b", c.c_uint16), ("d", c.c_uint16), ("e", c.c_ubyte * 8)]
    class PVValue(c.Union):
        _fields_ = [("value", c.c_int64), ("padding", c.c_byte * 16)]
    class PROPVARIANT(c.Structure):
        _fields_ = [("vt", c.c_uint16), ("r1", c.c_uint16), ("r2", c.c_uint16),
                    ("r3", c.c_uint16), ("data", PVValue)]
    def guid(value):
        return GUID.from_buffer_copy(uuid.UUID(value).bytes_le)
    def check(hr, operation):
        if hr < 0:
            raise OSError(f"{operation}: HRESULT 0x{hr & 0xffffffff:08X}")
    def method(pointer, index, argtypes, *args, result=c.c_long):
        vtable = c.cast(pointer, c.POINTER(c.POINTER(c.c_void_p))).contents
        function = c.WINFUNCTYPE(result, c.c_void_p, *argtypes)(vtable[index])
        return function(pointer, *args)
    def release(pointer):
        if pointer:
            method(pointer, 2, [], result=c.c_ulong)
    null = guid("00000000-0000-0000-0000-000000000000")
    processing = guid("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d")
    major = guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f")
    subtype = guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5")
    video_type = guid("73646976-0000-0010-8000-00aa00389b71")
    rgb32 = guid("00000016-0000-0010-8000-00aa00389b71")
    frame_size = guid("1652c33d-d6b2-4012-b834-72030849a37d")
    stride_key = guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6")
    first_video = 0xfffffffc
    original = metadados_mp4(video)
    requested = sorted(set(tempos))
    if not requested or requested[0] < 0 or requested[-1] >= original["duracaoMs"]:
        raise ValueError("Quadro solicitado fora do video real.")
    mf = c.WinDLL("mfplat.dll")
    reader_dll = c.WinDLL("mfreadwrite.dll")
    ole = c.OleDLL("ole32.dll")
    ole.CoInitializeEx.argtypes = [c.c_void_p, w.DWORD]
    ole.CoInitializeEx.restype = c.c_long
    mf.MFStartup.argtypes, mf.MFStartup.restype = [w.ULONG, w.DWORD], c.c_long
    mf.MFShutdown.argtypes, mf.MFShutdown.restype = [], c.c_long
    mf.MFCreateAttributes.argtypes, mf.MFCreateAttributes.restype = [c.POINTER(c.c_void_p), w.UINT], c.c_long
    mf.MFCreateMediaType.argtypes, mf.MFCreateMediaType.restype = [c.POINTER(c.c_void_p)], c.c_long
    reader_dll.MFCreateSourceReaderFromURL.argtypes = [w.LPCWSTR, c.c_void_p, c.POINTER(c.c_void_p)]
    reader_dll.MFCreateSourceReaderFromURL.restype = c.c_long
    initialized = ole.CoInitializeEx(None, 0)
    check(initialized, "CoInitializeEx")
    started = False
    attrs, reader, media, current = [c.c_void_p() for _ in range(4)]
    samples, timeline, seeks = [], [], []
    try:
        check(mf.MFStartup(0x00020070, 0), "MFStartup")
        started = True
        check(mf.MFCreateAttributes(c.byref(attrs), 1), "MFCreateAttributes")
        check(method(attrs, 21, [c.POINTER(GUID), w.UINT], c.byref(processing), 1), "Processamento software")
        check(reader_dll.MFCreateSourceReaderFromURL(str(video), attrs, c.byref(reader)), "Abrir video")
        check(method(reader, 4, [w.DWORD, w.BOOL], 0xfffffffe, False), "Desselecionar trilhas")
        check(method(reader, 4, [w.DWORD, w.BOOL], first_video, True), "Selecionar video")
        check(mf.MFCreateMediaType(c.byref(media)), "MFCreateMediaType")
        for key, value in ((major, video_type), (subtype, rgb32)):
            check(method(media, 24, [c.POINTER(GUID), c.POINTER(GUID)], c.byref(key), c.byref(value)), "SetGUID")
        check(method(reader, 7, [w.DWORD, c.c_void_p, c.c_void_p], first_video, None, media), "Selecionar RGB32")
        check(method(reader, 6, [w.DWORD, c.POINTER(c.c_void_p)], first_video, c.byref(current)), "Tipo decodificado")
        dimensions, stride_raw = c.c_uint64(), w.UINT()
        check(method(current, 8, [c.POINTER(GUID), c.POINTER(c.c_uint64)], c.byref(frame_size), c.byref(dimensions)), "Tamanho RGB32")
        width, height = dimensions.value >> 32, dimensions.value & 0xffffffff
        check(method(current, 7, [c.POINTER(GUID), c.POINTER(w.UINT)], c.byref(stride_key), c.byref(stride_raw)), "Stride RGB32")
        stride = c.c_int32(stride_raw.value).value
        if not (0 < width <= 8192 and 0 < height <= 8192) or abs(stride) != width * 4:
            raise ValueError("Tamanho/stride RGB32 nao suportado.")
        position = PROPVARIANT()
        position.vt, position.data.value = 20, requested[0] * 10000
        check(method(reader, 8, [c.POINTER(GUID), c.POINTER(PROPVARIANT)], c.byref(null), c.byref(position)), "Seek")
        seeks.append(requested[0])
        deadline, index, previous = time.monotonic() + 45, 0, None
        while index < len(requested):
            if time.monotonic() > deadline:
                raise TimeoutError("Leitura PTS limitada a45s.")
            stream_index, flags, timestamp, sample = w.DWORD(), w.DWORD(), c.c_int64(), c.c_void_p()
            check(method(reader, 9, [w.DWORD, w.DWORD, c.POINTER(w.DWORD), c.POINTER(w.DWORD), c.POINTER(c.c_int64), c.POINTER(c.c_void_p)],
                         first_video, 0, c.byref(stream_index), c.byref(flags), c.byref(timestamp), c.byref(sample)), "ReadSample")
            try:
                if flags.value & 2:
                    raise ValueError("Fim do video antes do quadro solicitado.")
                if not sample:
                    continue
                pts, duration = c.c_int64(), c.c_int64()
                check(method(sample, 35, [c.POINTER(c.c_int64)], c.byref(pts)), "PTS real")
                check(method(sample, 37, [c.POINTER(c.c_int64)], c.byref(duration)), "Duracao da amostra")
                if previous is not None and pts.value < previous:
                    raise ValueError("PTS decodificado nao monotono.")
                previous = pts.value
                timeline.append({"pts100ns": pts.value, "duracao100ns": duration.value})
                if pts.value < requested[index] * 10000:
                    continue
                buffer = c.c_void_p()
                try:
                    check(method(sample, 41, [c.POINTER(c.c_void_p)], c.byref(buffer)), "Buffer RGB32")
                    address, maximum, length = c.c_void_p(), w.DWORD(), w.DWORD()
                    check(method(buffer, 3, [c.POINTER(c.c_void_p), c.POINTER(w.DWORD), c.POINTER(w.DWORD)], c.byref(address), c.byref(maximum), c.byref(length)), "Lock")
                    try:
                        needed = abs(stride) * height
                        if length.value < needed:
                            raise ValueError("Amostra RGB32 incompleta.")
                        pixels = c.string_at(address, needed)
                    finally:
                        check(method(buffer, 4, []), "Unlock")
                    bmp = (struct.pack("<2sIHHI", b"BM", 54 + needed, 0, 0, 54)
                           + struct.pack("<IiiHHIIiiII", 40, width, -height if stride > 0 else height,
                                         1, 32, 0, needed, 0, 0, 0, 0) + pixels)
                    while index < len(requested) and pts.value >= requested[index] * 10000:
                        path = folder / f"{video.stem}_{requested[index]:06d}ms_pts{pts.value}.bmp"
                        escrever(path, bmp)
                        samples.append({"tempoSolicitadoMs": requested[index], "ptsReal100ns": pts.value,
                                        "ptsRealMs": pts.value / 10000, "duracao100ns": duration.value,
                                        "arquivo": str(path), "sha256": hash_arquivo(path)})
                        index += 1
                    if index < len(requested) and requested[index] * 10000 - pts.value > 10000000:
                        position.data.value = requested[index] * 10000
                        check(method(reader, 8, [c.POINTER(GUID), c.POINTER(PROPVARIANT)],
                                     c.byref(null), c.byref(position)), "Seek de segmento")
                        seeks.append(requested[index])
                        previous = None
                finally:
                    release(buffer)
            finally:
                release(sample)
        if hash_arquivo(video) != original["sha256"]:
            raise ValueError("Original mudou durante a leitura.")
        return {"original": original, "metodo": "MediaFoundationSourceReaderSoftwareRGB32",
                "gettersFormato": {"largura": width, "altura": height, "stride": stride},
                "selecao": "primeiro quadro cujo PTS e igual ou maior que o solicitado",
                "quadros": samples, "amostrasDecodificadas": timeline,
                "seeksSolicitadosMs": seeks, "gpuSolicitada": False}
    finally:
        for pointer in (current, media, reader, attrs):
            release(pointer)
        if started:
            mf.MFShutdown()
        ole.CoUninitialize()


async def renderizar_teste(plano: dict, code: str, video: Path, start_ms: int,
                           folder: Path, ajustar_taxa: bool) -> dict:
    from winrt.windows.media.editing import BackgroundAudioTrack, MediaTrimmingPreference
    from winrt.windows.media.mediaproperties import MediaEncodingProfile, VideoEncodingQuality
    from winrt.windows.media.transcoding import TranscodeFailureReason
    from winrt.windows.storage import CreationCollisionOption, StorageFile, StorageFolder
    row = next(s for s in plano["ordemNarrativa"] if s["id"] == code)
    if code == "EP01_C05":
        raise ValueError("C05 exige selecao de duas subtomadas; nao e um teste de tomada unica.")
    original = metadados_mp4(video)
    chosen = [p for p in tomadas_reais(ler_pasta_dados(RAIZ, exigir_perfil=True))
              if p["video"] == str(video) and p["id"] == code]
    if not chosen:
        raise ValueError("Video nao pertence a uma sessao concluida da cena pedida.")
    composition, clip = await abrir_composicao(video)
    before = clip.original_duration
    end = timedelta(milliseconds=start_ms + row["duracaoMs"])
    if start_ms < 0 or end > before:
        raise ValueError("Recorte ultrapassa tomada real; nenhuma extensao/falso quadro e criada.")
    clip.trim_time_from_start = timedelta(milliseconds=start_ms)
    clip.trim_time_from_end = before - end
    clip.volume = 0
    getter = {"originalMs": round(before.total_seconds() * 1000), "corteInicioMs": start_ms,
              "corteFimMs": round(clip.trim_time_from_end.total_seconds() * 1000),
              "duracaoComposicaoMs": round(composition.duration.total_seconds() * 1000)}
    if getter["duracaoComposicaoMs"] != row["duracaoMs"]:
        raise ValueError("Getter de recorte divergiu.")
    voices = []
    for item in (v for v in plano["vozes"] if v["cena"] == code):
        generated = await sintetizar(item, folder, ajustar_taxa)
        voices.append(generated)
        if not generated["cabeNoTrecho"]:
            salvar_json(folder / "voz_excede_tempo.json", generated)
            raise ValueError("Voz integral excede trecho; nao sera cortada: " + item["id"])
        track = await BackgroundAudioTrack.create_from_file_async(
            await StorageFile.get_file_from_path_async(generated["audio"]["arquivo"]))
        track.delay = timedelta(milliseconds=item["inicioCenaMs"])
        if track.original_duration > timedelta(milliseconds=item["duracaoMs"]):
            raise ValueError("Getter do audio excede trecho.")
        composition.background_audio_tracks.append(track)
    profile = MediaEncodingProfile.create_mp4(VideoEncodingQuality.H_D1080P)
    profile.video.frame_rate.numerator = 30
    profile.video.frame_rate.denominator = 1
    profile.video.bitrate = 8000000
    output_name = code + "_TESTE_VOZ_PROVISORIA.mp4"
    storage = await StorageFolder.get_folder_from_path_async(str(folder))
    destination = await storage.create_file_async(output_name, CreationCollisionOption.FAIL_IF_EXISTS)
    print("Render local iniciado: " + output_name, flush=True)
    failure = await composition.render_to_file_with_profile_async(destination, MediaTrimmingPreference.PRECISE, profile)
    if failure != TranscodeFailureReason.NONE:
        raise ValueError("Windows recusou render: " + str(failure))
    output = folder / output_name
    metadata = metadados_mp4(output)
    if abs(metadata["duracaoMs"] - row["duracaoMs"]) > 100:
        raise ValueError("MP4 renderizado divergiu da duracao exata.")
    video_track = next(t for t in metadata["trilhas"] if t.get("tipo") == "vide")
    if (video_track["largura"], video_track["altura"]) != (1920, 1080):
        raise ValueError("MP4 exportado nao esta em 1920x1080.")
    if hash_arquivo(video) != original["sha256"]:
        raise ValueError("Tomada original mudou durante o trabalho.")
    cues = [{"inicioMs": c["inicioMs"] - row["inicioMs"], "fimMs": c["fimMs"] - row["inicioMs"],
             "legenda": c["legenda"]} for c in plano["legendas"]
            if row["inicioMs"] <= c["inicioMs"] < row["fimMs"]]
    srt = folder / (output.stem + ".srt")
    escrever(srt, _srt(cues))
    samples = await extrair_quadros(output, [0, 3000, row["duracaoMs"] - 100], folder)
    return {"tipo": "teste_exportacao_local", "cena": code, "provisoria": True,
            "episodioFinalCompleto": False, "originalPreservado": original, "gettersRecorte": getter,
            "vozes": voices, "mp4Renderizado": metadata, "legenda": str(srt),
            "sha256Legenda": hash_arquivo(srt), "decodificacaoQuadros": samples,
            "inicioConteudoNoMP4Ms": start_ms, "origemOffset": "medicao na imagem fornecida na CLI",
            "limites": ["Teste de API e voz sintetica provisoria; nao aprova atuacao/enquadramento.",
                        "Nao e episodio completo; nenhuma tomada ausente foi substituida."]}


def quadros_com_prova_c01(proof: dict, take: dict) -> dict[int, dict]:
    """Ler apenas evidencias CPU existentes, vinculadas ao mesmo MP4/SRT/sessao."""
    if (not isinstance(proof, dict) or proof.get("sessao") != take.get("sessao") or
            proof.get("sha256Video") != take.get("sha256") or
            proof.get("sha256Legenda") != take.get("sha256Legenda")):
        raise ValueError("C01 exige prova PTS da mesma sessao e hashes MP4/SRT.")
    paths = proof.get("resultadosQuadros")
    if not isinstance(paths, list) or not paths:
        raise ValueError("C01 exige arquivos de quadros PTS reais.")
    frames = {}
    for path in paths:
        evidence = ler_json(exigir_sem_vinculos(Path(path)))
        if (evidence.get("metodo") != "MediaFoundationSourceReaderSoftwareRGB32" or
                evidence.get("gpuSolicitada") is not False or
                evidence.get("original", {}).get("arquivo") != take["video"] or
                evidence["original"].get("sha256") != take["sha256"]):
            raise ValueError("Quadros PTS de C01 nao correspondem ao bruto real.")
        for frame in evidence.get("quadros", []):
            pts, duration = frame.get("ptsReal100ns"), frame.get("duracao100ns")
            if (isinstance(pts, bool) or not isinstance(pts, int) or pts < 0 or
                    isinstance(duration, bool) or not isinstance(duration, int) or duration <= 0):
                raise ValueError("Quadro C01 sem PTS/duracao reais em100ns.")
            frames[pts] = {**frame, "pts100ns": pts}  # Chave interna; preservar o schema real e a prova original.
    return frames


def quadro_c01_conferido(frames: dict[int, dict], pts) -> dict:
    if isinstance(pts, bool) or not isinstance(pts, int) or pts not in frames:
        raise ValueError("Quadro PTS de C01 ausente da prova real.")
    frame = frames[pts]
    if hash_arquivo(exigir_sem_vinculos(Path(frame["arquivo"]))) != frame.get("sha256"):
        raise ValueError("Hash do quadro PTS de C01 divergiu.")
    return frame


def validar_recortes_c01(plano: dict, part: dict, source: dict) -> list[dict]:
    cuts = plano.get("recortesC01")
    if not isinstance(cuts, list) or len(cuts) < 2:
        raise ValueError("C01 exige multiplos recortes reais pendentes, somando14s; nao renderizar bruto60s.")
    take = part["tomada"]
    if (take.get("id") != "EP01_C01" or take.get("subtomada") != "principal" or
            not re.fullmatch(r"[0-9a-f]{32}", take.get("sessao", "")) or
            any(not re.fullmatch(r"[0-9A-Fa-f]{64}", take.get(k, "")) for k in ("sha256", "sha256Legenda"))):
        raise ValueError("C01 exige tomada principal real com sessao e hashes.")
    for file_key, hash_key in (("video", "sha256"), ("legenda", "sha256Legenda")):
        if hash_arquivo(exigir_sem_vinculos(Path(take[file_key]))) != take[hash_key]:
            raise ValueError("Bruto/legenda de C01 mudou antes da montagem.")
    if source.get("conteudoBrutoMs") != 60000 or part.get("inicioRelativoConteudoSelecionadoMs") != 0:
        raise ValueError("C01 conserva bruto60s; cada subclip declara seu proprio inicio relativo.")
    start = part["inicioConteudoNoVideoMs"]
    offset_proof = part.get("provaOffsetPTS")
    offset_frames = quadros_com_prova_c01(offset_proof, take)
    last = quadro_c01_conferido(offset_frames, offset_proof.get("ultimoQuadroContagemPts100ns"))
    first = quadro_c01_conferido(offset_frames, offset_proof.get("primeiroQuadroLimpoPts100ns"))
    if (last["pts100ns"] + last["duracao100ns"] != first["pts100ns"] or
            start != (first["pts100ns"] + 9999) // 10000):
        raise ValueError("C01 exige ultimo count/primeiro clean adjacentes e ceilPTS exato.")
    metadata = metadados_mp4(exigir_sem_vinculos(Path(take["video"])))
    result, actions, previous_end, total = [], [], 0, 0
    for i, cut in enumerate(cuts):
        if not isinstance(cut, dict) or cut.get("medidoNaImagem") is not True or cut.get("conteudoConferidoNaImagem") is not True:
            raise ValueError("Cada recorteC01 exige tempo e acao conferidos na imagem.")
        relative, length = cut.get("inicioRelativoConteudoSelecionadoMs"), cut.get("duracaoMs")
        if (isinstance(relative, bool) or not isinstance(relative, int) or relative < previous_end or
                isinstance(length, bool) or not isinstance(length, int) or length <= 0):
            raise ValueError("RecortesC01 devem ser positivos, ordenados e sem sobreposicao.")
        if relative + length > 60000 or start + relative + length > metadata["duracaoMs"]:
            raise ValueError("RecorteC01 ultrapassa os60s de conteudo ou quadros reais do MP4.")
        tags = cut.get("acoes")
        required = EDICAO_C01["acoesObrigatorias"]
        if (not isinstance(tags, list) or not tags or any(tag not in required for tag in tags) or
                tags != sorted(set(tags), key=required.index)):
            raise ValueError("RecorteC01 precisa de tags reais das seis acoes na ordem original.")
        for tag in tags:
            if not actions or actions[-1] != tag:
                actions.append(tag)
        proof = cut.get("provaPTS")
        frames = quadros_com_prova_c01(proof, take)
        first_cut = quadro_c01_conferido(frames, proof.get("inicioPts100ns"))
        last_cut = quadro_c01_conferido(frames, proof.get("fimPts100ns"))
        raw_start, raw_end = (start + relative) * 10000, (start + relative + length) * 10000
        if (not first_cut["pts100ns"] <= raw_start < first_cut["pts100ns"] + first_cut["duracao100ns"] or
                not last_cut["pts100ns"] < raw_end <= last_cut["pts100ns"] + last_cut["duracao100ns"]):
            raise ValueError("Extremos do recorteC01 nao estao nos frames PTS comprovados.")
        result.append({"id": "EP01_C01", "subtomada": "principal", "tomada": take,
                       "inicioMs": start + relative, "duracaoMs": length, "indiceRecorteC01": i,
                       "acoes": list(tags), "provaPTS": copy.deepcopy(proof)})
        previous_end = relative + length
        total += length
    if total != 14000 or actions != EDICAO_C01["acoesObrigatorias"]:
        raise ValueError("RecortesC01 exigem exatamente14s e voo/saida/queda/emersao/rampa/corrida na ordem original.")
    return result


def validar_recortes(plano: dict, atual: dict, cenas_parciais: list[str] | None = None) -> list[dict]:
    """Rejeitar planos adulterados, faltas e offsets nao medidos antes do render."""
    if plano.get("versao") != 1 or plano.get("tipo") != "montagem_local_EP01":
        raise ValueError("Esperado plano local EP01 versao1.")
    final159 = edicao_original_159(plano)
    if final159 and not edicao_original_159(atual):
        atual = transformar_plano_159(atual)
    fields = ("fontes", "fonteRoteiroSHA256", "vozes", "legendas", "duracaoNoveCenasMs")
    if final159:
        fields += ("duracaoNoveCenasMaisPreviaMs", "duracaoIntegralComPreviaVinhetaMs")
    for field in fields:
        if plano.get(field) != atual[field]:
            raise ValueError("Plano desatualizado ou voz/tempo/origem alterados: " + field)
    if "cartelas" in plano and plano["cartelas"] != atual["cartelas"]:
        raise ValueError("Texto ou tempo da cartela original foi alterado.")
    sequence = plano.get("ordemNarrativa")
    if not isinstance(sequence, list) or len(sequence) != 9:
        raise ValueError("Esperadas as nove cenas na ordem narrativa original.")
    if cenas_parciais is not None:
        allowed = [f"EP01_C{n:02d}" for n in range(1, 10)]
        if not 1 <= len(cenas_parciais) < 9 or cenas_parciais != [x for x in allowed if x in cenas_parciais]:
            raise ValueError("Parcial tecnica exige1a8IDs unicos na ordem narrativa.")
    recortes = []
    for scene, expected in zip(sequence, atual["ordemNarrativa"]):
        for field in ("id", "inicioMs", "fimMs", "duracaoMs"):
            if scene.get(field) != expected[field]:
                raise ValueError("Ordem ou tempo da cena alterados: " + field)
        if cenas_parciais is not None and scene["id"] not in cenas_parciais:
            continue
        parts = scene.get("partes")
        if not isinstance(parts, list) or len(parts) != len(expected["partes"]):
            raise ValueError("Partes incompletas de " + scene["id"])
        for part, expected_part in zip(parts, expected["partes"]):
            if any(part.get(field) != expected_part[field] for field in ("subtomada", "duracaoSelecionadaMs")):
                raise ValueError("Subtomada/tempo alterados de " + scene["id"])
            take = part.get("tomada")
            if not isinstance(take, dict) or not take.get("video"):
                raise ValueError("Tomada real ausente: " + scene["id"] + "/" + part["subtomada"])
            if part.get("medidoNaImagem") is not True:
                raise ValueError("Offset precisa de medicao na imagem: " + scene["id"])
            start, relative = part.get("inicioConteudoNoVideoMs"), part.get("inicioRelativoConteudoSelecionadoMs")
            if isinstance(start, bool) or not isinstance(start, int) or start < 0:
                raise ValueError("Inicio de conteudo invalido: " + scene["id"])
            if isinstance(relative, bool) or not isinstance(relative, int) or relative < 0:
                raise ValueError("Trecho selecionado nao medido: " + scene["id"])
            source = next((s for s in atual["fontes"] if s["id"] == scene["id"] and s["subtomada"] == part["subtomada"]), None)
            length = part["duracaoSelecionadaMs"]
            if final159 and scene["id"] == "EP01_C01":
                if source is None:
                    raise ValueError("Fonte original de C01 ausente.")
                recortes.extend(validar_recortes_c01(plano, part, source))
                continue
            if source is None or relative + length > source["conteudoBrutoMs"]:
                raise ValueError("Selecao ultrapassa conteudo narrativo de " + scene["id"])
            recortes.append({"id": scene["id"], "subtomada": part["subtomada"], "tomada": take,
                             "inicioMs": start + relative, "duracaoMs": length})
    return recortes


def validar_extras_editoriais(plano: dict, atual: dict, incluir_previa: bool, incluir_vinheta: bool,
                              cenas_parciais: list[str] | None = None) -> list[dict]:
    if not incluir_previa and not incluir_vinheta:
        return []
    if cenas_parciais is not None:
        raise ValueError("Previa/vinheta exigem as nove cenas completas; parcial tecnica fica separada.")
    if incluir_vinheta and not incluir_previa:
        raise ValueError("Vinheta integral de EP01 vem depois da previa original de5s.")
    provided = plano.get("extrasEditoriais")
    if not isinstance(provided, dict):
        raise ValueError("Plano sem extras medidos; atualizar antes de incluir previa/vinheta.")
    cuts = []
    names = ["previaEP02"] + (["vinheta"] if incluir_vinheta else [])
    for name in names:
        actual, expected = provided.get(name), atual["extrasEditoriais"][name]
        if not isinstance(actual, dict):
            raise ValueError("Extra editorial ausente: " + name)
        for key in ("id", "duracaoMs", "semDublagem", "cartela", "creditos"):
            if actual.get(key) != expected.get(key):
                raise ValueError("Texto, ordem ou tempo do extra original alterado: " + name + "/" + key)
        parts = actual.get("partes")
        if not isinstance(parts, list) or len(parts) != len(expected["partes"]):
            raise ValueError("Partes incompletas do extra: " + name)
        for part, source in zip(parts, expected["partes"]):
            for key in ("id", "subtomada", "duracaoSelecionadaMs", "fonte"):
                if part.get(key) != source[key]:
                    raise ValueError("Fonte/parte/tempo do extra alterado: " + name + "/" + key)
    for name in names:
        actual, expected = provided[name], atual["extrasEditoriais"][name]
        for part, source in zip(actual["partes"], expected["partes"]):
            code, take = part["id"], part.get("tomada")
            if not isinstance(take, dict) or not take.get("video"):
                raise ValueError("Tomada real do extra ausente: " + code)
            if take.get("id") != code or take.get("subtomada") != part["subtomada"]:
                raise ValueError("Tomada do extra pertence a outra cena/subtomada: " + code)
            if part.get("medidoNaImagem") is not True or part.get("conteudoConferidoNaImagem") is not True:
                raise ValueError("Extra exige offset e acao conferidos em quadros reais: " + code)
            start, relative = part.get("inicioConteudoNoVideoMs"), part.get("inicioRelativoConteudoSelecionadoMs")
            if any(isinstance(v, bool) or not isinstance(v, int) or v < 0 for v in (start, relative)):
                raise ValueError("Offset/trecho do extra nao medido: " + code)
            length = part["duracaoSelecionadaMs"]
            if relative + length > source["fonte"]["conteudoBrutoMs"]:
                raise ValueError("Recorte excede conteudo real do extra: " + code)
            proof = part.get("provaOffsetPTS")
            if not isinstance(proof, dict) or proof.get("sessao") != take.get("sessao"):
                raise ValueError("Prova PTS ausente ou de outra sessao: " + code)
            paths = proof.get("resultadosQuadros")
            if not isinstance(paths, list) or not paths:
                raise ValueError("Quadros PTS da medicao ausentes: " + code)
            frames = []
            for path in paths:
                evidence = ler_json(exigir_sem_vinculos(Path(path)))
                if (evidence.get("metodo") != "MediaFoundationSourceReaderSoftwareRGB32" or
                        evidence.get("gpuSolicitada") is not False or
                        evidence.get("original", {}).get("arquivo") != take["video"] or
                        evidence["original"].get("sha256") != take["sha256"]):
                    raise ValueError("Prova PTS nao corresponde ao MP4 real: " + code)
                frames.extend(evidence["quadros"])
            last, first = proof.get("ultimoQuadroContagemPtsMs"), proof.get("primeiroQuadroLimpoPtsMs")
            if any(isinstance(v, bool) or not isinstance(v, (int, float)) for v in (last, first)):
                raise ValueError("Limite contagem/acao PTS nao medido: " + code)
            if not first > last or not 0 <= start - first < 1:
                raise ValueError("Offset nao corresponde ao primeiro quadro limpo: " + code)
            boundary_frames = []
            for point in (last, first):
                frame = next((f for f in frames if f["ptsRealMs"] == point), None)
                if frame is None or hash_arquivo(exigir_sem_vinculos(Path(frame["arquivo"]))) != frame["sha256"]:
                    raise ValueError("Quadro PTS ausente/alterado: " + code)
                boundary_frames.append(frame)
            if abs(first - last - boundary_frames[0]["duracao100ns"] / 10000) > 0.001:
                raise ValueError("Falta o quadro imediatamente apos a contagem na prova PTS: " + code)
            cuts.append({"id": code, "grupoEditorial": actual["id"], "subtomada": part["subtomada"],
                         "tomada": take, "inicioMs": start + relative, "duracaoMs": length,
                         "provaOffsetPTS": copy.deepcopy(proof)})
    return cuts


async def renderizar_montagem(plano: dict, atual: dict, dados: Path, folder: Path,
                              ajustar_taxa: bool, cenas_parciais: list[str] | None = None,
                              manifesto_vozes: Path = VOZES_EP01_VERIFICADAS,
                              incluir_previa: bool = False, incluir_vinheta: bool = False) -> dict:
    cuts = validar_recortes(plano, atual, cenas_parciais)
    extra_cuts = validar_extras_editoriais(plano, atual, incluir_previa, incluir_vinheta, cenas_parciais)
    prepared_voices = ler_vozes_verificadas(plano, manifesto_vozes)
    from winrt.windows.media.editing import BackgroundAudioTrack, MediaClip, MediaComposition, MediaTrimmingPreference
    from winrt.windows.media.mediaproperties import MediaEncodingProfile, VideoEncodingQuality
    from winrt.windows.media.transcoding import TranscodeFailureReason
    from winrt.windows.storage import CreationCollisionOption, StorageFile, StorageFolder
    selected = [row for row in plano["ordemNarrativa"]
                if cenas_parciais is None or row["id"] in cenas_parciais]
    cursor, positions, montage_rows, cues = 0, {}, [], []
    for row in selected:
        positions[row["id"]] = cursor
        montage_rows.append({"id": row["id"], "inicioMontagemMs": cursor,
                             "fimMontagemMs": cursor + row["duracaoMs"],
                             "inicioEpisodioOriginalMs": row["inicioMs"], "duracaoMs": row["duracaoMs"]})
        cues.extend({"inicioMs": cursor + cue["inicioMs"] - row["inicioMs"],
                     "fimMs": cursor + cue["fimMs"] - row["inicioMs"], "legenda": cue["legenda"]}
                    for cue in plano["legendas"] if row["inicioMs"] <= cue["inicioMs"] < row["fimMs"])
        cursor += row["duracaoMs"]
    for extra in (["previaEP02"] if incluir_previa else []) + (["vinheta"] if incluir_vinheta else []):
        row = plano["extrasEditoriais"][extra]
        positions[row["id"]] = cursor
        montage_rows.append({"id": row["id"], "inicioMontagemMs": cursor,
                             "fimMontagemMs": cursor + row["duracaoMs"], "duracaoMs": row["duracaoMs"]})
        cursor += row["duracaoMs"]
    captures = tomadas_reais(dados)
    composition = MediaComposition()
    verified = []
    for cut in cuts + extra_cuts:
        take = cut["tomada"]
        actual = next((v for v in captures if v["sessao"] == take.get("sessao") and
                       v["id"] == cut["id"] and v["subtomada"] == cut["subtomada"]), None)
        if actual is None or any(take.get(field) != actual[field] for field in ("video", "sha256", "sha256Legenda")):
            raise ValueError("Tomada nao coincide com sessao real: " + cut["id"])
        video = exigir_sem_vinculos(Path(take["video"]))
        before = metadados_mp4(video)
        clip = await MediaClip.create_from_file_async(await StorageFile.get_file_from_path_async(str(video)))
        end = timedelta(milliseconds=cut["inicioMs"] + cut["duracaoMs"])
        if end > clip.original_duration:
            raise ValueError("Faltam quadros reais depois do offset medido: " + cut["id"])
        clip.trim_time_from_start = timedelta(milliseconds=cut["inicioMs"])
        clip.trim_time_from_end = clip.original_duration - end
        clip.volume = 0
        composition.clips.append(clip)
        verified.append({**cut, "original": before, "duracaoOriginalWinRT": str(clip.original_duration)})
    if round(composition.duration.total_seconds() * 1000) != cursor:
        raise ValueError("Getter de duracao total divergiu das cenas selecionadas.")
    cards = await adicionar_cartela_c02(composition, positions, folder)
    if incluir_previa:
        cards.append(await adicionar_cartela_texto(composition, plano["extrasEditoriais"]["previaEP02"]["cartela"],
                                                  positions["PREVIA_EP02"], folder, "PREVIA_NO_PROXIMO_EPISODIO.png"))
    if incluir_vinheta:
        for i, credit in enumerate(plano["extrasEditoriais"]["vinheta"]["creditos"]):
            cards.append(await adicionar_cartela_texto(composition, credit,
                         positions["VINHETA_01"] + credit["inicioCenaMs"], folder, f"VINHETA_CREDITO_{i + 1:02d}.png", 30))
    voices = []
    for item in plano["vozes"]:
        if item["cena"] not in positions:
            continue
        mapped = {**item, "inicioMontagemMs": positions[item["cena"]] + item["inicioCenaMs"],
                  "fimMontagemMs": positions[item["cena"]] + item["fimCenaMs"]}
        generated = copiar_voz_verificada(prepared_voices[item["id"]], mapped, folder, manifesto_vozes)
        voices.append(generated)
        if not generated["cabeNoTrecho"]:
            salvar_json(folder / (item["id"] + "_voz_excede.json"), generated)
            raise ValueError("Voz integral excede trecho; render recusado: " + item["id"])
        track = await BackgroundAudioTrack.create_from_file_async(
            await StorageFile.get_file_from_path_async(generated["audio"]["arquivo"]))
        track.delay = timedelta(milliseconds=mapped["inicioMontagemMs"])
        if track.original_duration > timedelta(milliseconds=item["duracaoMs"]):
            raise ValueError("Getter de audio ultrapassa fala: " + item["id"])
        composition.background_audio_tracks.append(track)
    profile = MediaEncodingProfile.create_mp4(VideoEncodingQuality.H_D1080P)
    profile.video.frame_rate.numerator = 30
    profile.video.frame_rate.denominator = 1
    profile.video.bitrate = 8000000
    final159 = edicao_original_159(plano)
    name = (f"EP01_{cursor // 1000}S_VOZ_PROVISORIA.mp4" if final159 and cenas_parciais is None else
            "EP01_205S_VOZ_PROVISORIA.mp4" if incluir_vinheta else
            "EP01_185S_VOZ_PROVISORIA.mp4" if incluir_previa else
            "EP01_NOVE_CENAS_VOZ_PROVISORIA.mp4" if cenas_parciais is None else
            "EP01_PARCIAL_TECNICA_VOZ_PROVISORIA.mp4")
    destination = await (await StorageFolder.get_folder_from_path_async(str(folder))).create_file_async(
        name, CreationCollisionOption.FAIL_IF_EXISTS)
    print("Render local iniciado: " + name, flush=True)
    failure = await composition.render_to_file_with_profile_async(destination, MediaTrimmingPreference.PRECISE, profile)
    if failure != TranscodeFailureReason.NONE:
        raise ValueError("Windows recusou render: " + str(failure))
    output = metadados_mp4(folder / name)
    if abs(output["duracaoMs"] - cursor) > 100:
        raise ValueError("MP4 nao tem duracao das cenas selecionadas.")
    video_track = next(t for t in output["trilhas"] if t.get("tipo") == "vide")
    if (video_track["largura"], video_track["altura"]) != (1920, 1080):
        raise ValueError("Montagem exportada nao esta em1920x1080.")
    for cut in verified:
        if hash_arquivo(Path(cut["tomada"]["video"])) != cut["original"]["sha256"]:
            raise ValueError("Tomada original mudou durante render.")
    srt = folder / (Path(name).stem + ".srt")
    escrever(srt, _srt(cues))
    return {"tipo": "montagem_nove_cenas" if cenas_parciais is None else "montagem_parcial_tecnica",
            "episodioFinalCompleto": False, "provisoria": True, "ordemMontagem": montage_rows,
            "estruturaIntegral159s": final159 and incluir_previa and incluir_vinheta and cursor == 159000,
            "estruturaIntegral205s": incluir_previa and incluir_vinheta and cursor == 205000,
            "edicaoOriginalC01": copy.deepcopy(EDICAO_C01) if final159 else None,
            "planoHistoricoTecnico": not final159,
            "duracaoComposicaoMs": cursor,
            "cenasAusentes": [row["id"] for row in plano["ordemNarrativa"] if row["id"] not in positions],
            "recortesReais": verified, "vozes": voices, "cartelas": cards,
            "manifestoVozesReutilizadas": str(manifesto_vozes),
            "mp4": output, "legenda": str(srt), "sha256Legenda": hash_arquivo(srt),
            "faltam": ([] if incluir_vinheta else ["vinheta", "creditos"]) +
                      ([] if incluir_previa else ["previaEP02"]) + ["avaliacaoVisual"] +
                      ([] if "EP01_C02" in positions else ["cartelaC02"])}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="acao", required=True)
    commands.add_parser("plano")
    commands.add_parser("plano159")
    commands.add_parser("cartela")
    frame = commands.add_parser("quadros")
    frame.add_argument("--video", type=Path, required=True)
    frame.add_argument("--tempos-ms", type=int, nargs="+", required=True)
    precise_frame = commands.add_parser("quadros-pts")
    precise_frame.add_argument("--video", type=Path, required=True)
    precise_frame.add_argument("--tempos-ms", type=int, nargs="+", required=True)
    voices = commands.add_parser("vozes")
    voices.add_argument("--cena", choices=[f"EP01_C{n:02d}" for n in range(1, 10)])
    voices.add_argument("--ajustar-taxa", action="store_true")
    test = commands.add_parser("teste")
    test.add_argument("--cena", choices=[f"EP01_C{n:02d}" for n in range(1, 10)], required=True)
    test.add_argument("--video", type=Path, required=True)
    test.add_argument("--inicio-conteudo-ms", type=int, required=True)
    test.add_argument("--ajustar-taxa", action="store_true")
    montage = commands.add_parser("montar")
    montage.add_argument("--plano", type=Path, required=True)
    montage.add_argument("--ajustar-taxa", action="store_true")
    montage.add_argument("--vozes-verificadas", type=Path, default=VOZES_EP01_VERIFICADAS)
    montage.add_argument("--incluir-previa-ep02", action="store_true")
    montage.add_argument("--incluir-vinheta", action="store_true")
    partial = commands.add_parser("montar-parcial")
    partial.add_argument("--plano", type=Path, required=True)
    partial.add_argument("--cenas", nargs="+", required=True)
    partial.add_argument("--ajustar-taxa", action="store_true")
    partial.add_argument("--vozes-verificadas", type=Path, default=VOZES_EP01_VERIFICADAS)
    args = parser.parse_args()
    folder = novo_trabalho(args.acao)
    print("Saidas novas: " + str(folder), flush=True)
    try:
        if args.acao == "quadros":
            value = asyncio.run(extrair_quadros(exigir_sem_vinculos(args.video.absolute()), args.tempos_ms, folder))
        elif args.acao == "quadros-pts":
            value = extrair_quadros_pts(exigir_sem_vinculos(args.video.absolute()), args.tempos_ms, folder)
        elif args.acao == "cartela":
            value = {"cartela": asyncio.run(gerar_cartela_c02(folder)), "renderExecutado": False}
        else:
            dados = ler_pasta_dados(RAIZ, exigir_perfil=True)
            plan = preparar_plano(dados)
            if args.acao == "plano159":
                plan = transformar_plano_159(plan)
            salvar_json(folder / "EP01.montagem.plano.json", plan)
            escrever(folder / "EP01.montagem.planejada.srt", _srt(plan["legendas"]))
            if args.acao in ("plano", "plano159"):
                value = plan
            elif args.acao == "vozes":
                async def all_voices():
                    return [await sintetizar(item, folder, args.ajustar_taxa) for item in plan["vozes"]
                            if args.cena is None or item["cena"] == args.cena]
                value = {"provisorias": True, "vozes": asyncio.run(all_voices())}
            elif args.acao == "teste":
                value = asyncio.run(renderizar_teste(plan, args.cena, exigir_sem_vinculos(args.video.absolute()),
                                                    args.inicio_conteudo_ms, folder, args.ajustar_taxa))
            else:
                calibrated = ler_json(exigir_sem_vinculos(args.plano.absolute()))
                value = asyncio.run(renderizar_montagem(calibrated, plan, dados, folder, args.ajustar_taxa,
                                                       args.cenas if args.acao == "montar-parcial" else None,
                                                       exigir_sem_vinculos(args.vozes_verificadas.absolute()),
                                                       getattr(args, "incluir_previa_ep02", False),
                                                       getattr(args, "incluir_vinheta", False)))
        salvar_json(folder / "resultado.json", value)
        print("Concluido: " + str(folder / "resultado.json"), flush=True)
        return 0
    except Exception as error:
        salvar_json(folder / "erro.json", {"tipo": type(error).__name__, "erro": str(error),
                                          "originaisPreservados": True})
        print("Edicao local interrompida: " + str(error), file=sys.stderr, flush=True)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
