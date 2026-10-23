"""Calibracao CPU por sessao: poucos quadros, adjacencia real e aprovacao explicita.

amostrar conserva fonte/originais e deixa a prova pendente; janela extrai apenas
amostras PTS reais ja decodificadas. fechar exige PTS vistos e flags explicitas.
selecao une recortes conferidos, sem renderizar nem abrir aplicativos.
"""
from __future__ import annotations

import argparse
import copy
from datetime import datetime, timezone
from pathlib import Path
import struct
import sys
import xml.etree.ElementTree as ET

import montar_video_episodio_limpo as m

e = m.editor
METODO = "MediaFoundationSourceReaderSoftwareRGB32"


def referencia(path: Path) -> dict:
    path = e.exigir_sem_vinculos(path.absolute())
    return {"arquivo": str(path), "sha256": e.hash_arquivo(path)}


def ler_referencia(ref: dict) -> Path:
    path = e.exigir_sem_vinculos(Path(ref["arquivo"]))
    if e.hash_arquivo(path) != ref.get("sha256"):
        raise ValueError("Artefato original/prova mudou: " + str(path))
    return path


def extrair(video: Path, tempos: list[int], folder: Path) -> dict:
    result = e.extrair_quadros_pts(video, sorted(set(tempos)), folder)
    if result.get("metodo") != METODO or result.get("gpuSolicitada") is not False:
        raise ValueError("Somente SourceReader RGB32 software e permitido.")
    result["contato"] = gerar_contato(result["quadros"], folder)
    path = folder / "resultado-quadros.json"
    e.salvar_json(path, result)
    return referencia(path)


def gerar_contato(frames: list[dict], folder: Path) -> dict:
    """Mosaico de prova BMP em memoria CPU; originais decodificados intactos."""
    cell_w, cell_h, cols = 480, 270, 2
    width, height = cols * cell_w, ((len(frames) + cols - 1) // cols) * cell_h
    pixels, cells = bytearray(width * height * 4), []
    for index, frame in enumerate(frames):
        path = ler_referencia(frame)
        bmp = path.read_bytes()
        start = struct.unpack_from("<I", bmp, 10)[0]
        sw, sh = struct.unpack_from("<ii", bmp, 18)
        planes, bits, compression = struct.unpack_from("<HHI", bmp, 26)
        if (bmp[:2] != b"BM" or not 0 < sw <= 8192 or not 0 < abs(sh) <= 8192
                or (planes, bits, compression) != (1, 32, 0) or len(bmp) < start + sw * abs(sh) * 4):
            raise ValueError("Contato aceita somente BMP RGB32 real do SourceReader.")
        left, top = (index % cols) * cell_w, (index // cols) * cell_h
        for y in range(cell_h):
            sy = y * abs(sh) // cell_h
            sy = sy if sh < 0 else sh - 1 - sy
            for x in range(cell_w):
                source = start + (sy * sw + x * sw // cell_w) * 4
                target = ((top + y) * width + left + x) * 4
                pixels[target:target + 4] = bmp[source:source + 4]
        cells.append({"indice": index + 1, "esquerda": left, "topo": top,
                      "ptsReal100ns": frame["ptsReal100ns"], "original": str(path)})
    header = struct.pack("<2sIHHI", b"BM", 54 + len(pixels), 0, 0, 54)
    header += struct.pack("<IiiHHIIiiII", 40, width, -height, 1, 32, 0, len(pixels), 0, 0, 0, 0)
    path = folder / "contato-pts.bmp"
    e.escrever(path, header + pixels)
    return {**referencia(path), "celulas": cells, "redimensionamento": "vizinho mais proximo; somente prova CPU"}


def conferir_calibracao(path: Path, dados: Path) -> dict:
    state = e.ler_json(e.exigir_sem_vinculos(path.absolute()))
    if state.get("tipo") != "calibracao_tomada_limpa_cpu" or state.get("versao") != 1:
        raise ValueError("Esperada calibracao CPU real v1.")
    actual = m.tomada_real(dados, state["id"], state["tomada"]["sessao"])
    if actual != state["tomada"]:
        raise ValueError("Sessao/originais/logs mudaram desde a calibracao.")
    if m.duracao_snapshot(state["fonteJsonSnapshot"], state["id"]) != actual["duracaoAcaoMs"]:
        raise ValueError("Fonte historica nao corresponde a duracao da tomada.")
    for ref in state["resultadosQuadros"]:
        ler_referencia(ref)
    return state


def planejar_amostras(estimate: int, duration: int, video_duration: int) -> list[int]:
    if type(estimate) is not int or estimate < 0 or duration <= 0:
        raise ValueError("Estimativa deve ser milissegundo inteiro positivo.")
    # Tres pontos delimitam a transicao; dois verificam meio/fim. Estimativa
    # serve apenas para pedidos de imagem, nunca para aprovar um recorte.
    return sorted({max(0, min(video_duration - 1, point)) for point in
                   (estimate - 1000, estimate, estimate + 1000,
                    estimate + duration // 2, estimate + duration - 300)})


def amostrar(dados: Path, code: str, session: str, source: Path, estimate: int,
             folder: Path) -> dict:
    take = m.tomada_real(dados, code, session)
    source = e.exigir_sem_vinculos(source.absolute())
    content = source.read_bytes()
    original_ref = referencia(source)
    copied = folder / (code + ".source-snapshot.json")
    e.escrever(copied, content)
    snapshot = referencia(copied)
    duration = m.duracao_snapshot(snapshot, code)
    if duration != take["duracaoAcaoMs"] or e.hash_arquivo(source) != original_ref["sha256"]:
        raise ValueError("Fonte mudou ou diverge da duracao registrada.")
    record = ET.parse(ler_referencia(take["proveniencia"]["sessaoXml"])).getroot()
    armed = datetime.fromisoformat(record.attrib["armadaUtc"].replace("Z", "+00:00"))
    mtime = datetime.fromtimestamp(source.stat().st_mtime, timezone.utc)
    log = (dados / "logs/DiretorGTA.log").read_text("utf-8-sig").splitlines()
    prepared_events = [line for line in log if "[CENA_PRODUCAO_PUBLICADA]" in line
                       and "Id=" + code + ";" in line
                       and datetime.fromisoformat(line.split(" ", 1)[0].replace("Z", "+00:00")) <= armed]
    provenance = {"fonteFornecida": original_ref, "snapshotCopiadoUtc": datetime.now(timezone.utc).isoformat(),
                  "fonteMtimeUtc": mtime.isoformat(), "mtimeAnteriorArmada": mtime <= armed,
                  "hashDuranteCapturaRegistrado": False,
                  "limite": "Snapshot copiado apos a tomada; mtime anterior corrobora, nao prova hash durante captura.",
                  "ultimoEventoCenaPreparadaAnterior": prepared_events[-1] if prepared_events else None,
                  "contagemUtcRegistrada": record.get("contagemUtc"),
                  "contagemDuracaoMs": 3000,
                  "limiteRelogio": "Relogio/log delimita a acao, nao determina PTS zero da Game Bar."}
    if mtime > armed:
        raise ValueError("Fonte fornecida e posterior a captura; use snapshot historico real.")
    for name in ("sessao.xml", "resultado.xml"):
        e.escrever(folder / name, (dados / "logs/capturas" / session / name).read_bytes())
    outcome = ET.parse(folder / "resultado.xml").getroot()
    manifest = e.exigir_sem_vinculos(Path(outcome.attrib["manifesto"]))
    e.escrever(folder / "manifesto-organizador-original.json", manifest.read_bytes())
    metadata = e.metadados_mp4(Path(take["video"]))
    e.salvar_json(folder / "metadados-video.json", metadata)
    frames = folder / "quadros-iniciais"
    frames.mkdir()
    result_ref = extrair(Path(take["video"]), planejar_amostras(estimate, duration, metadata["duracaoMs"]), frames)
    return {"versao": 1, "tipo": "calibracao_tomada_limpa_cpu", "id": code,
            "tomada": take, "fonteJsonSnapshot": snapshot, "provenienciaFonte": provenance,
            "duracaoAcaoMs": duration, "inicioEstimadoMs": estimate,
            "resultadosQuadros": [result_ref], "medidoNaImagem": False,
            "aprovacaoVisual": False, "fonteSemTextoConferidaNaImagem": False,
            "renderExecutado": False, "gpuSolicitada": False}


def pontos_janela(state: dict, first_ms: int, last_ms: int) -> list[int]:
    if type(first_ms) is not int or type(last_ms) is not int or not 0 <= first_ms < last_ms or last_ms - first_ms > 400:
        raise ValueError("Janela adjacente exige intervalo real de ate 400 ms.")
    samples = {}
    for ref in state["resultadosQuadros"]:
        result = e.ler_json(ler_referencia(ref))
        if result.get("metodo") != METODO or result.get("gpuSolicitada") is not False:
            raise ValueError("Metadados nao vieram do extrator software.")
        for sample in result["amostrasDecodificadas"]:
            pts = sample["pts100ns"]
            if first_ms * 10000 <= pts <= last_ms * 10000:
                samples[pts] = sample
    if not 2 <= len(samples) <= 24:
        raise ValueError("Janela precisa conter de 2 a 24 amostras reais ja decodificadas.")
    return sorted({point // 10000 for point in samples})


def fechar(state: dict, dados: Path, last: int, first: int, clean: list[int],
           length: int | None, relative: int, clean_approved: bool, content_approved: bool) -> dict:
    if not clean_approved or not content_approved:
        raise ValueError("Conferencia visual de fonte limpa e conteudo deve ser explicita.")
    proof = {"sessao": state["tomada"]["sessao"], "sha256Video": state["tomada"]["sha256"],
             "sha256Legenda": state["tomada"]["sha256Legenda"],
             "resultadosQuadros": [str(ler_referencia(ref)) for ref in state["resultadosQuadros"]],
             "hashesResultadosQuadros": copy.deepcopy(state["resultadosQuadros"]),
             "ultimoQuadroContagemPts100ns": last, "primeiroQuadroLimpoPts100ns": first}
    clean_proof = {**proof, "ptsConferidos100ns": sorted(set(clean))}
    cut = m.criar_recorte(dados, state["id"], state["tomada"]["sessao"],
                         state["fonteJsonSnapshot"], proof, clean_proof, length, relative)
    return {"versao": 1, "tipo": "recorte_calibrado_tomada_limpa", "id": state["id"],
            "aprovacaoVisual": True, "semTextos": True, "provenienciaFonte": state["provenienciaFonte"],
            "inicioConteudoNoVideoMs": m.conferir_offset(proof, state["tomada"]), "recorte": cut,
            "renderExecutado": False, "gpuSolicitada": False}


def selecao(paths: list[Path], episode: str, dados: Path, folder: Path, partial: bool, extras_only: bool = False) -> dict:
    cuts, extras = [], []
    for path in paths:
        value = e.ler_json(e.exigir_sem_vinculos(path.absolute()))
        if value.get("tipo") != "recorte_calibrado_tomada_limpa" or value.get("aprovacaoVisual") is not True or value.get("semTextos") is not True:
            raise ValueError("Somente recortes reais explicitamente conferidos podem entrar na selecao.")
        cut = value["recorte"]
        if cut["id"] in m.SUPLEMENTARES and cut["id"] not in extras:
            extras.append(cut["id"])
        cuts.append(copy.deepcopy(cut))
    plan = {"versao": 1, "tipo": m.TIPO_SELECAO, "episodio": episode, "recortes": cuts}
    if extras:
        plan["extras"] = extras
    if extras_only:
        plan["somenteExtras"] = True
    path = folder / (episode + ".selecao-limpa.json")
    e.salvar_json(path, plan)
    manifest = m.preparar_manifesto(path, dados, episode, partial)
    for cut in manifest["recortes"]:
        cut["fonteSemTextoConferidaNaImagem"] = True
        cut["conteudoConferidoNaImagem"] = True
    if not manifest["parcial"]:
        m.validar_manifesto(manifest, dados)
    e.salvar_json(folder / (episode + ".clips-limpos.manifesto.json"), manifest)
    return manifest


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="acao", required=True)
    sub = commands.add_parser("amostrar")
    sub.add_argument("--cena", required=True)
    sub.add_argument("--sessao", required=True)
    sub.add_argument("--fonte", type=Path, required=True)
    sub.add_argument("--inicio-estimado-ms", type=int, default=6500)
    for name in ("janela", "quadros", "fechar"):
        sub = commands.add_parser(name)
        sub.add_argument("--calibracao", type=Path, required=True)
        if name == "janela":
            sub.add_argument("--de-ms", type=int, required=True)
            sub.add_argument("--ate-ms", type=int, required=True)
        elif name == "quadros":
            sub.add_argument("--tempos-ms", type=int, nargs="+", required=True)
        else:
            sub.add_argument("--ultimo-count-100ns", type=int, required=True)
            sub.add_argument("--primeiro-clean-100ns", type=int, required=True)
            sub.add_argument("--pts-limpos-100ns", type=int, nargs="+", required=True)
            sub.add_argument("--duracao-ms", type=int)
            sub.add_argument("--relativo-ms", type=int, default=0)
            sub.add_argument("--conferido-sem-texto", action="store_true")
            sub.add_argument("--conteudo-conferido", action="store_true")
    sub = commands.add_parser("selecao")
    sub.add_argument("--episodio", choices=tuple(m.EPISODIOS), required=True)
    sub.add_argument("--recortes", type=Path, nargs="+", required=True)
    sub.add_argument("--parcial", action="store_true")
    sub.add_argument("--somente-extras", action="store_true")
    args = parser.parse_args(argv)
    folder = m.novo_trabalho("calibracao-cpu-" + args.acao)
    try:
        dados = e.ler_pasta_dados(e.RAIZ, exigir_perfil=True)
        if args.acao == "amostrar":
            value = amostrar(dados, args.cena, args.sessao, args.fonte, args.inicio_estimado_ms, folder)
        elif args.acao == "selecao":
            value = selecao(args.recortes, args.episodio, dados, folder, args.parcial, args.somente_extras)
        else:
            state = conferir_calibracao(args.calibracao, dados)
            if args.acao in ("janela", "quadros"):
                points = pontos_janela(state, args.de_ms, args.ate_ms) if args.acao == "janela" else args.tempos_ms
                if not 1 <= len(points) <= 24 or (args.acao == "quadros" and len(points) > 5):
                    raise ValueError("Use no maximo cinco pedidos adicionais ou 24 amostras adjacentes.")
                ref = extrair(Path(state["tomada"]["video"]), points, folder)
                value = copy.deepcopy(state)
                value["resultadosQuadros"].append(ref)
            else:
                value = fechar(state, dados, args.ultimo_count_100ns, args.primeiro_clean_100ns,
                               args.pts_limpos_100ns, args.duracao_ms, args.relativo_ms,
                               args.conferido_sem_texto, args.conteudo_conferido)
        e.salvar_json(folder / "resultado.json", value)
        print("Concluido: " + str(folder / "resultado.json"), flush=True)
        return 0
    except Exception as error:
        e.salvar_json(folder / "erro.json", {"tipo": type(error).__name__, "erro": str(error),
                                             "renderExecutado": False, "gpuSolicitada": False})
        print("Calibracao interrompida: " + str(error), file=sys.stderr, flush=True)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
