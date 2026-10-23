"""Renderiza um extra de EP01_C07 e publica separadamente, sem sobrescrever.

Use a .venv_edicao. render recebe o offset medido pelo operador e conserva a
proveniencia da tomada; nao confere imagens nem afirma aprovacao estetica.
publicar --manifesto copia o render revisado para os dois destinos SEM_LEGENDAS.
Nenhuma montagem ou versao anterior e substituida.
"""
from __future__ import annotations

import argparse
import asyncio
from datetime import datetime, timezone
import json
from pathlib import Path
import re
import shutil
import sys
import uuid

import finalizar_ep03
import montar_video_episodio_limpo as tomadas

editor = tomadas.editor
RAIZ = Path(__file__).resolve().parents[1]
TIPO = "extra_c07_render_v1"
PADRAO = "EP01_C07_BRIGA_COMICA_EXTRA.mp4"


def referencia(path: Path) -> dict:
    path = editor.exigir_sem_vinculos(path.absolute())
    if not path.is_file():
        raise FileNotFoundError(path)
    return {"arquivo": str(path), "sha256": editor.hash_arquivo(path)}


def conferir(ref: dict) -> Path:
    path = Path(ref["arquivo"])
    if referencia(path) != ref:
        raise ValueError("Arquivo alterado: " + str(path))
    return path


def nome_extra(value: str) -> str:
    if (not re.fullmatch(r"EP01_C07_[A-Za-z0-9_]+\.mp4", value)
            or "EXTRA" not in value[:-4].upper()):
        raise ValueError("Nome simples deve iniciar EP01_C07_, conter EXTRA e terminar .mp4.")
    return value


def destinos(dados: Path, nome: str) -> list[Path]:
    nome_extra(nome)
    return [editor.exigir_sem_vinculos(pasta / nome) for pasta in (
        RAIZ / "videos/EP01_SEM_LEGENDAS",
        dados / "Serie/A_CALOTEIRA_1_TEMP/EP01/EP01_SEM_LEGENDAS")]


def exigir_destinos_livres(paths: list[Path]) -> None:
    for path in paths:
        if path.exists():
            raise FileExistsError("Ja existe, escolha outro --nome: " + str(path))


def novo_trabalho(acao: str) -> Path:
    folder = RAIZ / "backups/extra-c07" / (
        datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-" + acao + "-" + uuid.uuid4().hex[:8])
    editor.exigir_sem_vinculos(folder)
    folder.mkdir(parents=True, exist_ok=False)
    return folder


def render(args, dados: Path, folder: Path) -> dict:
    nome_extra(args.nome)
    exigir_destinos_livres(destinos(dados, args.nome))
    if args.inicio_ms < 0 or args.duracao_ms <= 0:
        raise ValueError("Inicio deve ser >=0 e duracao >0, em milissegundos inteiros.")
    take = tomadas.tomada_real(dados, "EP01_C07", args.sessao)
    video_ref = referencia(args.video)
    if video_ref != {"arquivo": take["video"], "sha256": take["sha256"]}:
        raise ValueError("--video deve ser o MP4 original comprovado pela sessao C07.")
    fonte_ref = referencia(args.fonte)
    duracao_fonte = tomadas.duracao_snapshot(fonte_ref, "EP01_C07")
    if duracao_fonte != take["duracaoAcaoMs"]:
        raise ValueError("Duracao integral do snapshot difere da acao registrada na sessao.")
    if args.duracao_ms > duracao_fonte:
        raise ValueError("Recorte ultrapassa a duracao de acao da tomada.")
    metadata = editor.metadados_mp4(Path(video_ref["arquivo"]))
    if args.inicio_ms + args.duracao_ms > metadata["duracaoMs"]:
        raise ValueError("Recorte ultrapassa a duracao real do MP4.")
    snapshot = folder / "EP01_C07.source-snapshot.json"
    shutil.copy2(args.fonte, snapshot)
    snapshot_ref = referencia(snapshot)
    if snapshot_ref["sha256"] != fonte_ref["sha256"]:
        raise ValueError("Snapshot mudou durante a copia.")
    cut = {"id": "EP01_C07", "video": Path(video_ref["arquivo"]),
           "sha256": video_ref["sha256"], "inicioMs": args.inicio_ms,
           "duracaoMs": args.duracao_ms}
    result = asyncio.run(finalizar_ep03.render([cut], folder / args.nome))
    conferir(video_ref)
    conferir(fonte_ref)
    if tomadas.tomada_real(dados, "EP01_C07", args.sessao) != take:
        raise ValueError("Proveniencia da tomada mudou durante o render.")
    return {"tipo": TIPO, "cena": "EP01_C07", "nome": args.nome,
            "tomada": take, "videoOriginal": video_ref,
            "fonteFornecida": fonte_ref, "fonteSnapshot": snapshot_ref,
            "duracaoFonteMs": duracao_fonte, "inicioMs": args.inicio_ms,
            "duracaoSelecionadaMs": args.duracao_ms,
            "extra": True, "cenaIntegral": False,
            "recorteMenorQueAcao": args.duracao_ms < duracao_fonte,
            "offsetMedidoPorEstaFerramenta": False,
            "conferenciaVisual": "nao realizada pela ferramenta",
            "hashFonteDuranteCapturaComprovado": False,
            "limiteFonte": "Snapshot e duracao conferidos; hashes atuais nao provam a fonte durante a filmagem.",
            "render": result, "renderArquivo": referencia(folder / args.nome),
            "publicado": False, "originaisPreservados": True}


def publicar(args, dados: Path, folder: Path) -> dict:
    manifest_ref = referencia(args.manifesto)
    manifest = editor.ler_json(Path(manifest_ref["arquivo"]))
    if (manifest.get("tipo") != TIPO or manifest.get("cena") != "EP01_C07"
            or manifest.get("extra") is not True or manifest.get("cenaIntegral") is not False):
        raise ValueError("Manifesto nao e um extra C07 gerado por esta ferramenta.")
    targets = destinos(dados, manifest["nome"])
    exigir_destinos_livres(targets)
    take = tomadas.tomada_real(dados, "EP01_C07", manifest["tomada"]["sessao"])
    if take != manifest["tomada"]:
        raise ValueError("Proveniencia da sessao mudou desde o render.")
    for key in ("videoOriginal", "fonteFornecida", "fonteSnapshot", "renderArquivo"):
        conferir(manifest[key])
    if tomadas.duracao_snapshot(manifest["fonteSnapshot"], "EP01_C07") != take["duracaoAcaoMs"]:
        raise ValueError("Snapshot e sessao divergiram.")
    video = Path(manifest["renderArquivo"]["arquivo"])
    if video.name != manifest["nome"] or video.parent != args.manifesto.absolute().parent:
        raise ValueError("Render deve permanecer ao lado do manifesto original.")
    metadata = editor.metadados_mp4(video)
    if (metadata["sha256"] != manifest["render"]["sha256"]
            or abs(metadata["duracaoMs"] - manifest["duracaoSelecionadaMs"]) > 100):
        raise ValueError("Hash/duracao do render divergiram do manifesto.")
    result = {"tipo": "extra_c07_publicacao_v1", "manifesto": manifest_ref,
              "renderArquivo": manifest["renderArquivo"], "copias": [],
              "publicado": False, "originaisPreservados": True}
    for index, target in enumerate(targets, 1):
        target.parent.mkdir(parents=True, exist_ok=True)
        # xb recusa overwrite mesmo se o destino surgir depois da checagem.
        with video.open("rb") as source, target.open("xb") as destination:
            shutil.copyfileobj(source, destination, length=1024 * 1024)
        copied = referencia(target)
        if copied["sha256"] != manifest["renderArquivo"]["sha256"]:
            raise ValueError("Copia divergiu: " + str(target))
        result["copias"].append(copied)
        editor.salvar_json(folder / ("copia-%d.json" % index), copied)
    conferir(manifest["renderArquivo"])
    result["publicado"] = True
    return result


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="acao", required=True)
    sub = commands.add_parser("render", help="Renderiza somente em backup novo; revisar antes de publicar.")
    sub.add_argument("--video", type=Path, required=True)
    sub.add_argument("--inicio-ms", type=int, required=True)
    sub.add_argument("--duracao-ms", type=int, default=38000)
    sub.add_argument("--sessao", required=True)
    sub.add_argument("--fonte", type=Path, required=True, help="Snapshot JSON da tomada, nao a cena atual alterada.")
    sub.add_argument("--nome", default=PADRAO)
    sub = commands.add_parser("publicar", help="Copia um render conferido, recusando destinos existentes.")
    sub.add_argument("--manifesto", type=Path, required=True)
    args = parser.parse_args(argv)
    folder = novo_trabalho(args.acao)
    try:
        dados = editor.ler_pasta_dados(RAIZ, exigir_perfil=True)
        value = render(args, dados, folder) if args.acao == "render" else publicar(args, dados, folder)
        path = folder / ("manifesto.json" if args.acao == "render" else "publicacao.json")
        editor.salvar_json(path, value)
        print(json.dumps({"resultado": str(path), "publicado": value["publicado"]}, ensure_ascii=False))
        return 0
    except Exception as error:
        editor.salvar_json(folder / "erro.json", {"tipo": type(error).__name__, "erro": str(error),
            "originaisPreservados": True, "limite": "Falha de copia pode deixar destino novo incompleto; nenhum arquivo e apagado."})
        print("Extra C07 interrompido: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
