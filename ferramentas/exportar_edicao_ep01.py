"""Complemento de edição do EP1 a partir dos dez JSONs executáveis reais.

Não altera roteiro, guias/base anteriores nem arquivos do perfil. MP4/áudio e
offsets medidos permanecem ausentes até as capturas e o alinhamento no CapCut.
"""
from __future__ import annotations

import argparse
import csv
from decimal import Decimal
import hashlib
import io
import json
from pathlib import Path
import sys

from preparar_edicao import ErroEdicao, _nome, _srt, ler_planos, publicar_edicao
from runtime_diretor import ErroRuntime, exigir_sem_vinculos, ler_pasta_dados
from validar_cena import ErroCena
from validar_json import validar_cena_json

RAIZ = Path(__file__).resolve().parents[1]
PREFIXO = "EP01/runtime/"
DURACOES_MS = {1: 60000, 2: 10000, 3: 9000, 4: 7000, 5: 8000,
               6: 9000, 7: 24000, 8: 46000, 9: 7000}
COLUNAS = ("cena", "subtomada", "blocoRuntime", "blocoOriginal", "chaveVoz",
           "tipo", "tipoRuntime", "quemOriginal", "nomeVoz", "textoOriginal", "duracaoMs",
           "inicioConteudoMs", "fimConteudoMs", "entradaRelativaF7Ms", "saidaRelativaF7Ms",
           "boca", "usarVozUmaVez", "audioExiste", "mp4Real", "offsetManualMp4ParaF7Ms")


def _json(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def _hash(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest().upper()


def _ms(value) -> int:
    return int(Decimal(str(value)) * 1000)


def verificar_fidelidade(cena: dict, plano: dict) -> list[dict]:
    """Comparar vozes com os planos da fonte, ignorando apenas pausas técnicas."""
    atuais = [b for b in cena["roteiro"] if b["tipo"] != "pausa"]
    originais = [b for b in plano["roteiro"] if b["texto"] is not None]
    if len(atuais) != len(originais):
        raise ErroEdicao(cena["id"] + ": quantidade de vozes difere da origem.")
    for atual, original in zip(atuais, originais):
        tipo_runtime = "narracao" if original["tipo"] == "narrador" else original["tipo"]
        if (atual["tipo"] != tipo_runtime or atual["texto"] != original["texto"]
                or _ms(atual["duracaoSegundos"]) != original["duracaoMs"]):
            raise ErroEdicao(cena["id"] + ": texto/tipo/duração de voz diverge do roteiro original.")
        if atual["tipo"] == "fala":
            esperado = original["quemOriginal"]
            if esperado == "DJANIRA":
                esperado = "DJANIRA_POBRE"
            if atual["personagem"] != esperado:
                raise ErroEdicao(cena["id"] + ": interlocutor de voz diverge da origem.")
    return originais


def _fontes(raiz: Path):
    for number in range(1, 10):
        code = f"EP01_C{number:02d}"
        if number == 5:
            for part in ("hospital", "cassino"):
                yield raiz / "producao/subtomadas/EP01_C05" / part / (code + ".json"), part
        else:
            yield raiz / "producao/cenas_serie/EP01" / (code + ".json"), "principal"


def preparar_materiais_runtime(catalogos: Path, raiz: Path = RAIZ) -> dict[str, bytes]:
    raiz = exigir_sem_vinculos(raiz)
    catalogos = exigir_sem_vinculos(catalogos)
    producao = ler_planos(raiz / "producao/temporada")
    fontes, arquivos, linhas, vozes_finais = [], {}, [], {}
    # Todas as fontes são validadas contra os XMLs/hashes reais antes de escrever.
    for path, part in _fontes(raiz):
        exigir_sem_vinculos(path)
        raw = path.read_bytes()
        scene = validar_cena_json(path, catalogos)
        if path.read_bytes() != raw:
            raise ErroEdicao("Fonte mudou durante a validação: " + str(path))
        code = scene["id"]
        original = verificar_fidelidade(scene, producao["planos"][code])
        number = scene["numero"]
        total = sum(_ms(b["duracaoSegundos"]) for b in scene["roteiro"])
        if total != DURACOES_MS[number]:
            raise ErroEdicao(code + ": duração adaptada difere da integração atual.")
        label = code if part == "principal" else code + "_" + part
        cursor, voice_index, cues, pauses = 0, 0, [], []
        for block_index, block in enumerate(scene["roteiro"], 1):
            end = cursor + _ms(block["duracaoSegundos"])
            if block["tipo"] == "pausa":
                pauses.append({"inicioConteudoMs": cursor, "fimConteudoMs": end, "semVoz": True})
            else:
                source = original[voice_index]
                voice_index += 1
                key = f"{code}_B{voice_index:02d}"
                row = {"cena": code, "subtomada": part, "blocoRuntime": block_index,
                       "blocoOriginal": voice_index, "chaveVoz": key, "tipo": source["tipo"], "tipoRuntime": block["tipo"],
                       "quemOriginal": source.get("quemOriginal", ""),
                       "nomeVoz": _nome(source.get("quemOriginal", ""), source["tipo"]),
                       "textoOriginal": block["texto"], "duracaoMs": end - cursor,
                       "inicioConteudoMs": cursor, "fimConteudoMs": end,
                       "entradaRelativaF7Ms": cursor + 3000, "saidaRelativaF7Ms": end + 3000,
                       "boca": block["tipo"] == "fala", "usarVozUmaVez": True,
                       "audioExiste": False, "mp4Real": None, "offsetManualMp4ParaF7Ms": None}
                linhas.append(row)
                if key not in vozes_finais:
                    vozes_finais[key] = {"chaveVoz": key, "cena": code, "tipo": source["tipo"], "tipoRuntime": block["tipo"],
                        "quemOriginal": source.get("quemOriginal", ""), "textoOriginal": block["texto"],
                        "duracaoMs": end - cursor, "audioReal": None, "usarUmaVez": True}
                name = "Narrador" if block["tipo"] != "fala" else _nome(source["quemOriginal"], "fala")
                cues.append({"inicioMs": cursor + 3000, "fimMs": end + 3000,
                             "legenda": name + ": " + block["texto"]})
            cursor = end
        srt = f"srt_brutos/{label}.srt"
        arquivos[PREFIXO + srt] = _srt(cues)
        fontes.append({"id": code, "subtomada": part, "rotuloBruto": label,
            "arquivoJson": path.relative_to(raiz).as_posix(), "sha256Json": _hash(raw),
            "local": scene["ambiente"]["local"], "conteudoMs": total, "contagemMs": 3000,
            "capturaMinimaDesdeF7Ms": total + 3000, "srtRelativoF7": srt,
            "pausasTecnicas": pauses, "mp4Real": None, "offsetManualMp4ParaF7Ms": None,
            "offsetManualMp4ParaConteudoMs": None, "selecionarTomadaPeloLog": code == "EP01_C05"})
    sequence, cursor = [], 0
    for number in range(1, 10):
        code = f"EP01_C{number:02d}"
        total = DURACOES_MS[number]
        pieces = ([{"rotuloBruto": code + "_hospital", "duracaoSelecionadaPlanejadaMs": 3000,
                    "inicioConteudoSelecionadoMs": None},
                   {"rotuloBruto": code + "_cassino", "duracaoSelecionadaPlanejadaMs": 5000,
                    "inicioConteudoSelecionadoMs": None}] if number == 5 else
                  [{"rotuloBruto": code, "duracaoSelecionadaPlanejadaMs": total,
                    "inicioConteudoSelecionadoMs": 0}])
        sequence.append({"id": code, "inicioEpisodioPlanejadoMs": cursor,
            "fimEpisodioPlanejadoMs": cursor + total, "duracaoMontagemPlanejadaMs": total,
            "partes": pieces, "chavesVoz": [k for k in vozes_finais if k.startswith(code + "_")]})
        cursor += total
    timeline = {"versao": 1, "tipoDocumento": "complemento_edicao_EP01_runtime", "episodio": 1,
        "fontesValidadas": len(fontes), "fonteRoteiroSHA256": producao["indice"]["fontes"]["roteiro"]["sha256"],
        "audioRealGerado": False, "mp4RealGerado": False, "sincronismoMedido": False,
        "duracaoNoveCenasPlanejadaMs": cursor, "fontesBrutas": fontes, "ordemNarrativa": sequence,
        "vozesMontagemFinal": list(vozes_finais.values()),
        "adaptacoes": [
            {"id": "EP01_C01", "vozOriginalMs": 14000, "reservaSemVozMs": 46000,
             "conteudoBrutoMs": 60000, "observacao": "Conservar toda a física; silêncio adicional não comprime a voz nem garante conclusão no jogo."},
            {"id": "EP01_C07", "vozesOriginaisMs": 18000, "introducaoSemVozMs": 4000,
             "coreografiaSemVozMs": 2000, "conteudoBrutoMs": 24000},
            {"id": "EP01_C08", "vozesOriginaisMs": 30000, "chegadaSemVozMs": 12000,
             "celularSemVozMs": 4000, "conteudoBrutoMs": 46000},
            {"id": "EP01_C05", "brutos": 2, "conteudoPorBrutoMs": 8000,
             "montagemFinalMs": 8000, "vozOriginalUnicaMs": 8000,
             "observacao": "Escolher pela imagem 3s de saída do hospital e 5s de entrada do cassino. Não repetir a voz; versões _v2 não identificam automaticamente o local. Usar log SUBTOMADA_GRAVACAO."}],
        "sincronismo": "Medir na imagem o início de F7/ação. MP4 pode começar antes de F7; não retirar apenas 3s de seu início nem inferir offset por ctime. Cada regravação tem offset próprio.",
        "extraCompartilhado": {"id": "VINHETA_01", "duracaoPlanejadaMs": 20000, "mp4Real": None,
            "acao": "Capturar uma vez ou reutilizar tomada real; inserir no fim conforme roteiro."},
        "montagemEditorialOriginal": producao["episodios"][0]["montagemEditorial"],
        "limites": ["Complemento; roteiro, SRTs base e guia preliminar anteriores permanecem preservados.",
                    "Os 180s das nove cenas são planejamento com as reservas atuais, sem MP4 medido.",
                    "Vinheta, cartelas, créditos e anúncio do próximo episódio precisam de edição/capturas próprias; não entram automaticamente nos 180s.",
                    "Os dois SRTs brutos de C05 repetem a referência original para orientar cada captura. Na montagem final usar a voz/legenda8s apenas uma vez."]}
    text = io.StringIO(newline="")
    writer = csv.DictWriter(text, fieldnames=COLUNAS)
    writer.writeheader(); writer.writerows(linhas)
    arquivos[PREFIXO + "EP01.runtime.vozes.csv"] = text.getvalue().encode("utf-8-sig")
    arquivos[PREFIXO + "EP01.runtime.montagem.json"] = _json(timeline)
    return arquivos


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalogos", type=Path, help="catálogos reais do perfil; padrão é o runtime configurado")
    parser.add_argument("--conferir", action="store_true", help="comparar complemento sem regravar")
    args = parser.parse_args(argv)
    try:
        catalogos = args.catalogos or ler_pasta_dados(RAIZ, exigir_perfil=True) / "catalogos"
        files = preparar_materiais_runtime(catalogos)
        if args.conferir:
            changed = [p for p, raw in files.items() if not (RAIZ / "producao/edicao" / p).is_file()
                       or (RAIZ / "producao/edicao" / p).read_bytes() != raw]
            if changed:
                raise ErroEdicao(f"{len(changed)} complementos ausentes/divergentes; arquivos preservados.")
            count = 0
        else:
            count = publicar_edicao(files)
        print(f"EP1: 10 JSONs reais validados; {count} complementos escritos; vozes/tempos fiéis. Nenhum áudio/MP4 gerado.")
        return 0
    except (ErroCena, ErroEdicao, ErroRuntime, OSError, ValueError, KeyError) as error:
        print("Exportação EP1: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
