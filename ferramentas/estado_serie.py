"""Conferir o planejamento e os recursos reais, sem criar cenas ou aprovações."""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

from runtime_diretor import exigir_sem_vinculos, ler_pasta_dados

RAIZ = Path(__file__).resolve().parents[1]


def estado(workspace: Path = RAIZ, dados: Path | None = None) -> dict:
    workspace = exigir_sem_vinculos(workspace)
    dados = exigir_sem_vinculos(dados or ler_pasta_dados(workspace, exigir_perfil=True))
    temporada = workspace / "producao" / "temporada"
    index = json.loads((temporada / "indice.json").read_text(encoding="utf-8"))
    for record in index["arquivosDerivados"]:
        path = exigir_sem_vinculos(temporada / record["arquivo"])
        if not path.is_relative_to(temporada) or not path.is_file():
            raise RuntimeError("Derivado ausente ou fora da temporada: " + str(path))
        digest = hashlib.sha256(path.read_bytes()).hexdigest().upper()
        if digest != record["sha256"].upper():
            raise RuntimeError("Derivado mudou; confira antes de preparar a série: " + str(path))
    catalog = json.loads((dados / "catalogos" / "personagens.json").read_text(encoding="utf-8-sig"))
    direction_dir = workspace / "producao" / "cenas_serie"
    direction = json.loads((direction_dir / "indice.json").read_text(encoding="utf-8"))
    for record in direction["arquivos"]:
        path = exigir_sem_vinculos(direction_dir / record["arquivo"])
        if not path.is_relative_to(direction_dir) or not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest().upper() != record["sha256"].upper():
            raise RuntimeError("Preparação de atuação mudou; conferir a cena: " + str(path))
    actors = [actor for actor in catalog["personagens"] if not actor["codigo"].startswith("TESTE_")]
    marks_file = exigir_sem_vinculos(dados / "catalogos" / "locais_marcados.xml")
    marks = set()
    if marks_file.is_file():
        raw = marks_file.read_bytes()
        if len(raw) > 1000000 or b"<!DOCTYPE" in raw.upper() or b"<!ENTITY" in raw.upper():
            raise RuntimeError("Catálogo de marcas inválido ou com DTD.")
        marks = {entry.attrib["id"] for entry in ET.fromstring(raw).findall("Local")}
    places = json.loads((dados / "catalogos" / "locais.json").read_text(encoding="utf-8-sig"))
    production_places = [place["codigo"] for place in places["locais"] if place["codigo"] != "TESTE_AMBIENTE"]
    families = Counter()
    for plan_file in temporada.rglob("*.plano.json"):
        plan = json.loads(plan_file.read_text(encoding="utf-8"))
        for requirement in plan["requisitos"]:
            families[requirement["familia"]] += 1
    series_dir = exigir_sem_vinculos(dados / "Serie" / "A_CALOTEIRA_1_TEMP")
    movies = []
    if series_dir.is_dir():
        for movie in series_dir.rglob("*.mp4"):
            movie = exigir_sem_vinculos(movie)
            if movie.parent.name.startswith("EP"):
                movies.append(str(movie.relative_to(series_dir)))
    return {
        "serie": index["serie"], "piloto": index["piloto"],
        "episodiosPlanejados": len(index["episodios"]), "planos": index["contagem"],
        "rascunhosIntegrais": direction["rascunhosIntegrais"],
        "cenasPendentesDeAtuacao": direction["pendentesAtuacao"],
        "personagensPendentes": [actor["codigo"] for actor in actors if not actor["pronto"]],
        "locaisPendentes": [code for code in production_places if code not in marks],
        "locaisCapturados": [code for code in production_places if code in marks],
        "videosEmPastasDeEpisodio": movies,
        "requisitosPorFamilia": dict(sorted(families.items())),
        "pastaDados": str(dados), "planejamento": str(temporada),
        "observacao": "Planos e legendas base não são cenas executáveis. MP4 existente não comprova aprovação visual.",
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dados", type=Path)
    args = parser.parse_args(argv)
    try:
        print(json.dumps(estado(dados=args.dados), ensure_ascii=False, indent=2))
        return 0
    except (OSError, ValueError, RuntimeError, ET.ParseError, KeyError) as error:
        print("Estado da série: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
