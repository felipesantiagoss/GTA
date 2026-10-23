"""Preparar/exportar recortes silenciosos para o CapCut, sem textos ou vinheta.

Reutiliza a validacao das tomadas e dos PTS do editor existente. Preparar usa
somente CPU; exportar usa o render WinRT e deve ser executado separadamente.
Nao remove texto ja gravado nos pixels: cada recorte exige conferencia explicita
de fonteSemTextoConferidaNaImagem antes de exportar. Originais sao preservados.
"""
from __future__ import annotations

import argparse
import asyncio
import copy
from datetime import timedelta
from decimal import Decimal
from pathlib import Path
import re
import sys

import montar_video_ep01 as editor


TIPO = "recortes_EP01_limpos_CapCut"
TIPO_SELECAO = "selecao_clips_limpos_EP01"
MODO = {"vozAdicionada": False, "cartelasAdicionadas": False,
        "legendasAdicionadas": False, "srtGerado": False,
        "vinhetaIncluida": False, "audioOriginalSilenciado": True,
        "velocidadeOriginal": True}


def conferir_selecao(recortes: list[dict]) -> None:
    if not isinstance(recortes, list) or not recortes:
        raise ValueError("Selecao precisa de recortes reais.")
    previous, used = {}, set()
    last_group = None
    for cut in recortes:
        if not isinstance(cut, dict) or not re.fullmatch(r"EP01_C0[1-9]|EP02_C0[12]", str(cut.get("id", ""))):
            raise ValueError("Somente cenas de EP01/previa EP02; MOV referencia e vinheta nao sao fontes.")
        if cut.get("medidoNaImagem") is not True:
            raise ValueError("Inicio/duracao precisam ser medidos na imagem: " + cut["id"])
        start, length = cut.get("inicioMs"), cut.get("duracaoMs")
        if type(start) is not int or type(length) is not int or start < 0 or length <= 0:
            raise ValueError("Tempos de recorte precisam de milissegundos inteiros positivos.")
        snapshot = cut.get("fonteJsonSnapshot")
        if not isinstance(snapshot, dict) or not snapshot.get("arquivo") or not snapshot.get("sha256"):
            raise ValueError("Snapshot JSON da captura ausente: " + cut["id"])
        path = editor.exigir_sem_vinculos(Path(snapshot["arquivo"]))
        if editor.hash_arquivo(path) != snapshot["sha256"]:
            raise ValueError("Snapshot JSON alterado: " + cut["id"])
        scene = editor.ler_json(path)
        if scene.get("id") != cut["id"]:
            raise ValueError("Snapshot pertence a outra cena.")
        content = sum(Decimal(str(block["duracaoSegundos"])) * 1000 for block in scene["roteiro"])
        if content != content.to_integral_value() or length > content:
            raise ValueError("Recorte excede conteudo do snapshot JSON.")
        take = cut.get("tomada")
        if not isinstance(take, dict) or take.get("id") != cut["id"] or take.get("subtomada") != cut.get("subtomada"):
            raise ValueError("Identidade da tomada diverge do recorte.")
        session = take.get("sessao")
        if not isinstance(session, str) or not re.fullmatch(r"[0-9a-f]{32}", session):
            raise ValueError("Sessao real ausente.")
        if start < previous.get(session, 0):
            raise ValueError("Recortes da mesma tomada fora de ordem ou sobrepostos.")
        previous[session] = start + length
        group = cut.get("grupoEditorial", cut["id"])
        if not isinstance(group, str) or not re.fullmatch(r"EP01_C0[1-9]|EP02_C0[12]|PREVIA_EP02", group):
            raise ValueError("Grupo de clip invalido.")
        if group != last_group:
            if group in used:
                raise ValueError("Partes de um clip precisam ficar juntas na selecao.")
            used.add(group)
            last_group = group


def conferir_tomadas(recortes: list[dict], dados: Path) -> None:
    captures = editor.tomadas_reais(dados)
    originals = {}
    for cut in recortes:
        take = cut["tomada"]
        actual = next((row for row in captures if row["sessao"] == take.get("sessao")
                       and row["id"] == cut["id"]
                       and row["subtomada"] == cut["subtomada"]), None)
        if actual is None or any(take.get(key) != actual[key]
                                 for key in ("video", "sha256", "legenda", "sha256Legenda")):
            raise ValueError("Recorte nao corresponde a uma captura concluida: " + cut["id"])
        video = editor.exigir_sem_vinculos(Path(take["video"]))
        if str(video) not in originals:
            originals[str(video)] = editor.metadados_mp4(video)
        original = originals[str(video)]
        if original["sha256"] != take["sha256"]:
            raise ValueError("MP4 original mudou: " + cut["id"])
        if editor.hash_arquivo(editor.exigir_sem_vinculos(Path(take["legenda"]))) != take["sha256Legenda"]:
            raise ValueError("SRT de proveniencia mudou: " + cut["id"])
        if cut["inicioMs"] < 0 or cut["duracaoMs"] <= 0 or cut["inicioMs"] + cut["duracaoMs"] > original["duracaoMs"]:
            raise ValueError("Recorte ultrapassa os quadros reais: " + cut["id"])


def recortes_do_plano(plano: dict, incluir_previa: bool) -> list[dict]:
    if plano.get("versao") == 1 and plano.get("tipo") == TIPO_SELECAO:
        if incluir_previa:
            raise ValueError("Selecao generica declara a previa diretamente nos recortes.")
        return copy.deepcopy(plano.get("recortes"))
    if plano.get("tipo") != "montagem_local_EP01":
        raise ValueError("Esperado plano calibrado ou selecao_clips_limpos_EP01 versao1.")
    # Plano selecionado e historico: validar suas medicoes, sem exigir duracoes
    # da producao corrente. Os snapshots abaixo conservam a fonte de cada take.
    recortes = editor.validar_recortes(plano, plano)
    recortes += editor.validar_extras_editoriais(plano, plano, incluir_previa, False)
    for cut in recortes:
        if cut["id"].startswith("EP01_"):
            source = next(row for row in plano["fontes"] if row["id"] == cut["id"] and row["subtomada"] == cut["subtomada"])
        else:
            source = next(row["fonte"] for row in plano["extrasEditoriais"]["previaEP02"]["partes"] if row["id"] == cut["id"])
        cut["medidoNaImagem"] = True
        cut["fonteJsonSnapshot"] = copy.deepcopy(source.get("jsonSnapshot", {"arquivo": source["json"], "sha256": source["sha256"]}))
    return recortes


def preparar_manifesto(plano_path: Path, dados: Path, incluir_previa: bool = False) -> dict:
    plano_path = editor.exigir_sem_vinculos(plano_path.absolute())
    plan_hash = editor.hash_arquivo(plano_path)
    plano = editor.ler_json(plano_path)
    recortes = recortes_do_plano(plano, incluir_previa)
    conferir_selecao(recortes)
    conferir_tomadas(recortes, dados)
    if editor.hash_arquivo(plano_path) != plan_hash:
        raise ValueError("Plano mudou durante a leitura.")
    grupos = []
    for index, cut in enumerate(recortes, 1):
        cut["numeroRecorte"] = index
        cut["fonteSemTextoConferidaNaImagem"] = False
        group = cut.get("grupoEditorial", cut["id"])
        if not grupos or grupos[-1]["id"] != group:
            grupos.append({"id": group, "arquivo": group + "_LIMPO.mp4",
                           "duracaoMs": 0, "recortes": []})
        grupos[-1]["duracaoMs"] += cut["duracaoMs"]
        grupos[-1]["recortes"].append(index)
    return {"versao": 1, "tipo": TIPO, "modo": copy.deepcopy(MODO),
            "planoSelecionado": str(plano_path), "sha256PlanoSelecionado": plan_hash,
            "incluirPreviaEP02": incluir_previa, "recortes": recortes, "clips": grupos,
            "duracaoMontagemMs": sum(cut["duracaoMs"] for cut in recortes),
            "renderExecutado": False,
            "observacao": "Conferir novas fontes sem texto nos pixels; este helper nao apaga legendas ja gravadas."}


def validar_manifesto(manifesto: dict, dados: Path) -> dict:
    if manifesto.get("versao") != 1 or manifesto.get("tipo") != TIPO or manifesto.get("modo") != MODO:
        raise ValueError("Esperado manifesto de clips limpos sem voz/textos/vinheta.")
    if type(manifesto.get("incluirPreviaEP02")) is not bool:
        raise ValueError("Selecao da previa precisa ser booleana.")
    path = editor.exigir_sem_vinculos(Path(manifesto["planoSelecionado"]))
    if editor.hash_arquivo(path) != manifesto.get("sha256PlanoSelecionado"):
        raise ValueError("Plano selecionado mudou; preparar novo manifesto.")
    esperado = preparar_manifesto(path, dados, manifesto["incluirPreviaEP02"])
    for key in ("clips", "duracaoMontagemMs"):
        if manifesto.get(key) != esperado[key]:
            raise ValueError("Ordem ou duracao dos clips foi alterada: " + key)
    actual_cuts = manifesto.get("recortes")
    if not isinstance(actual_cuts, list) or len(actual_cuts) != len(esperado["recortes"]):
        raise ValueError("Lista de recortes incompleta.")
    for actual, expected in zip(actual_cuts, esperado["recortes"]):
        if not isinstance(actual, dict) or actual.get("fonteSemTextoConferidaNaImagem") is not True:
            raise ValueError("Fonte sem texto ainda nao conferida: recorte " + str(expected["numeroRecorte"]))
        compared = copy.deepcopy(actual)
        compared["fonteSemTextoConferidaNaImagem"] = False
        if compared != expected:
            raise ValueError("Fonte, PTS ou recorte selecionado alterado.")
    return manifesto


async def exportar(manifesto: dict, dados: Path, folder: Path, montar: bool = False) -> dict:
    validar_manifesto(manifesto, dados)
    # Imports e render somente depois das validacoes; preparar nao solicita GPU.
    from winrt.windows.media.editing import MediaClip, MediaComposition, MediaTrimmingPreference
    from winrt.windows.media.mediaproperties import MediaEncodingProfile, VideoEncodingQuality
    from winrt.windows.media.transcoding import TranscodeFailureReason
    from winrt.windows.storage import CreationCollisionOption, StorageFile, StorageFolder

    cuts = manifesto["recortes"]
    groups = copy.deepcopy(manifesto["clips"])
    if montar:
        groups.append({"id": "EP01_MONTAGEM_LIMPA", "arquivo": "EP01_MONTAGEM_LIMPA.mp4",
                       "duracaoMs": manifesto["duracaoMontagemMs"],
                       "recortes": [cut["numeroRecorte"] for cut in cuts]})
    storage = await StorageFolder.get_folder_from_path_async(str(folder))
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
        # WinRT mede em 100ns; timedelta arredonda para microssegundos. Cada
        # recorte já foi conferido acima; a soma pode arredondar mais uma vez.
        expected_duration = timedelta(milliseconds=group["duracaoMs"])
        rounding_limit = timedelta(microseconds=len(group["recortes"]))
        if abs(composition.duration - expected_duration) > rounding_limit:
            raise ValueError("Duracao nativa do clip divergiu: " + group["id"])
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
    conferir_tomadas(cuts, dados)
    return {"tipo": TIPO, "modo": copy.deepcopy(MODO), "renderExecutado": True,
            "duracaoMontagemMs": manifesto["duracaoMontagemMs"], "arquivos": outputs,
            "originaisPreservados": True}


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="acao", required=True)
    prepare = commands.add_parser("preparar", help="Somente CPU; criar manifesto com conferencia visual pendente")
    prepare.add_argument("--plano", type=Path, required=True,
                         help="Plano calibrado ou selecao generica com recortes e fonteJsonSnapshot")
    prepare.add_argument("--incluir-previa-ep02", action="store_true")
    render = commands.add_parser("exportar", help="Render WinRT dos clips em pasta nova; requer fontes limpas conferidas")
    render.add_argument("--manifesto", type=Path, required=True)
    render.add_argument("--montar", action="store_true", help="Gerar tambem montagem silenciosa dos clips, sem vinheta")
    args = parser.parse_args(argv)
    folder = editor.novo_trabalho("clips-capcut-limpos-" + args.acao)
    try:
        dados = editor.ler_pasta_dados(editor.RAIZ, exigir_perfil=True)
        if args.acao == "preparar":
            value = preparar_manifesto(args.plano, dados, args.incluir_previa_ep02)
            editor.salvar_json(folder / "EP01.clips-limpos.manifesto.json", value)
        else:
            path = editor.exigir_sem_vinculos(args.manifesto.absolute())
            value = asyncio.run(exportar(editor.ler_json(path), dados, folder, args.montar))
        editor.salvar_json(folder / "resultado.json", value)
        print("Concluido: " + str(folder / "resultado.json"), flush=True)
        return 0
    except Exception as error:
        editor.salvar_json(folder / "erro.json", {"tipo": type(error).__name__, "erro": str(error),
                                                   "originaisPreservados": True})
        print("Clips limpos interrompidos: " + str(error), file=sys.stderr, flush=True)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
