"""Derivar18 códigos de props/apoios das fontes reais, sem publicar no perfil."""
from __future__ import annotations

import argparse
from pathlib import Path
import sys

from preparar_cenas_serie import _pesquisa, apoios_candidatos
from preparar_piloto import RAIZ, ErroPiloto, _adicionar, _confinado, _json
from runtime_diretor import ErroRuntime, exigir_sem_vinculos

MODELOS = {
    "celular": "prop_npc_phone_02", "papel": "p_cs_papers_01", "envelope": "prop_cs_envolope_01",
    "documentos": "prop_cs_documents_01", "sacola": "prop_cs_shopping_bag", "bandeja": "prop_food_bs_tray_01",
    "comprimidos": "prop_cs_pills", "prancheta": "p_amb_clipboard_01", "ferramenta_jardim": "prop_tool_shovel",
    "taca": "prop_wine_glass", "cadeira_simples": "prop_chair_01a", "poltrona": "prop_armchair_01",
    "banco": "prop_bench_01a", "mesa": "prop_table_01", "cadeira_casa": "v_res_fa_chair01",
    "cama_casa": "v_res_d_bed", "cama_quarto": "v_res_msonbed", "cama_simples": "v_res_tre_bed1",
}


def gerar_catalogo(raiz: Path = RAIZ) -> dict:
    raiz = exigir_sem_vinculos(raiz)
    pesquisa = _pesquisa(raiz)
    apoios = apoios_candidatos(pesquisa)["apoios"]
    sources = {x["modelo"]: ("prop", x["origem"]) for x in pesquisa["objetos"]}
    for apoio in apoios:
        if apoio["modelo"] in sources:
            raise ErroPiloto("Modelo de apoio duplicado no catálogo de objetos.")
        sources[apoio["modelo"]] = ("apoio", apoio["origem"])
    if set(sources) != set(MODELOS.values()) or len(sources) != 18:
        raise ErroPiloto("Catálogo deve conter exatamente os18modelos reais do contrato.")
    return {"versao": 1, "objetos": [{"codigo": code, "tipo": sources[model][0], "modelo": model,
        "origem": sources[model][1], "estado": "fonte_conferida;teste_visual_pendente"} for code, model in MODELOS.items()]}


def preparar(raiz: Path = RAIZ, conferir: bool = False) -> dict:
    raiz = exigir_sem_vinculos(raiz)
    data = gerar_catalogo(raiz)
    raw = _json(data)
    path = _confinado(raiz / "catalogos/objetos_serie.json", raiz)
    if path.exists():
        if not path.is_file() or path.read_bytes() != raw:
            raise ErroPiloto("Catálogo existente diverge; edição preservada. Faça revisão/backup antes de regenerar.")
    elif conferir:
        raise ErroPiloto("Catálogo de objetos ausente; nada foi criado na conferência.")
    else:
        _adicionar(path, raw, raiz)
    return data


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Derivar18props/apoios comprovados por arquivo/linha/SHA, no workspace.")
    parser.add_argument("--conferir", action="store_true", help="conferir sem escrever ou sobrescrever")
    args = parser.parse_args(argv)
    try:
        result = preparar(conferir=args.conferir)
        print(f"{len(result['objetos'])}modelos/linhas/hashes conferidos; teste visual e adequação dos apoios pendentes. Sem publicação.")
        return 0
    except (ErroPiloto, ErroRuntime, OSError, ValueError, KeyError) as error:
        print(f"Não foi possível preparar objetos: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
