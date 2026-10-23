"""Dublagem/CapCut dos15 episódios, derivados dos planos fiéis, sem áudio/MP4.

A continuidade é uma linha de tempo preliminar de clipes após retirar a
preparação/contagem pela referência real de F7. Ctime não sincroniza vozes.
"""
from __future__ import annotations

import argparse
import csv
from datetime import datetime, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import sys
import unicodedata
import uuid

from preparar_serie import CONTAGENS, ErroPreparacao, RAIZ, ROTEIRO, sha256


class ErroEdicao(ValueError):
    pass


COLUNAS = ("episodio", "cena", "bloco", "tipo", "personagemCodigoOriginal", "nomeVoz",
           "textoOriginal", "duracaoMs", "inicioConteudoMs", "fimConteudoMs",
           "entradaRelativaF7Ms", "saidaRelativaF7Ms", "entradaEpisodioBaseMs", "saidaEpisodioBaseMs",
           "arquivoAudioSugerido", "audioExiste", "boca", "linhaOriginal", "observacao")


def _json(dados) -> bytes:
    return (json.dumps(dados, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def _ler(path: Path):
    try:
        def pares(items):
            result = {}
            for k, v in items:
                if k in result:
                    raise ErroEdicao(f"{path.name}: chave duplicada {k}.")
                result[k] = v
            return result
        return json.loads(path.read_bytes().decode("utf-8-sig"), object_pairs_hook=pares,
                          parse_constant=lambda x: (_ for _ in ()).throw(ErroEdicao("Número não finito proibido.")))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ErroEdicao(f"Plano de origem não pôde ser lido: {path}: {exc}") from exc


def _sem_vinculos(path: Path, raiz: Path):
    if not path.absolute().is_relative_to(raiz.absolute()):
        raise ErroEdicao("Caminho fora da raiz de planejamento.")
    for parte in [path, *path.parents]:
        if parte == raiz.parent:
            break
        if parte.is_symlink() or (hasattr(parte, "is_junction") and parte.is_junction()):
            raise ErroEdicao(f"Caminho de planejamento redirecionado: {parte}.")
    return path


def ler_planos(pasta: Path = RAIZ / "producao" / "temporada") -> dict:
    pasta = Path(pasta).absolute()
    _sem_vinculos(pasta, pasta.parent.parent)
    indice = _ler(pasta / "indice.json")
    if indice.get("tipoDocumento") != "indice_pre_producao" or indice.get("contagem", {}).get("planos") != 95:
        raise ErroEdicao("Esperado índice real de95 planos de pré-produção.")
    if sha256((RAIZ / ROTEIRO).read_bytes()) != indice["fontes"]["roteiro"]["sha256"]:
        raise ErroEdicao("O roteiro original mudou; reconciliar os95 planos antes de preparar a edição.")
    for entrada in indice["arquivosDerivados"]:
        rel = Path(entrada["arquivo"])
        if rel.is_absolute() or ".." in rel.parts:
            raise ErroEdicao("Referência insegura no índice de produção.")
        arquivo = _sem_vinculos(pasta / rel, pasta)
        if sha256(arquivo.read_bytes()) != entrada["sha256"]:
            raise ErroEdicao(f"Plano/SRT/manifesto alterado: {rel}; reconciliar a origem antes de preparar edição.")
    episodios, planos, bases = [], {}, {}
    for e in indice["episodios"]:
        numero = e["numero"]
        if not 1 <= numero <= 15:
            raise ErroEdicao("Número de episódio inválido no índice.")
        manifesto = _ler(_sem_vinculos(pasta / e["manifesto"], pasta))
        if manifesto["ordemCenas"] != e["cenas"]:
            raise ErroEdicao("Ordem do manifesto diverge do índice.")
        for entrada in manifesto["cenas"]:
            arquivo = pasta / f"EP{numero:02d}" / entrada["plano"]
            plano = _ler(_sem_vinculos(arquivo, pasta))
            if plano["id"] != entrada["id"] or plano["episodio"] != numero or plano["id"] in planos:
                raise ErroEdicao("ID/episódio repetido ou divergente no plano.")
            planos[plano["id"]] = plano
            bases[plano["id"]] = _sem_vinculos(arquivo.with_name(entrada["srtBase"]), pasta).read_bytes()
        episodios.append(manifesto)
    vinheta = _ler(_sem_vinculos(pasta / "extras" / "VINHETA_01.plano.json", pasta))
    planos["VINHETA_01"] = vinheta
    bases["VINHETA_01"] = (pasta / "extras" / "VINHETA_01.base.srt").read_bytes()
    if [len(e["ordemCenas"]) for e in episodios] != list(CONTAGENS) or len(planos) != 95:
        raise ErroEdicao("Contagem/ordem dos15 episódios não corresponde aos94 +1 planos.")
    return {"indice": indice, "indiceSHA256": sha256((pasta / "indice.json").read_bytes()),
            "episodios": episodios, "planos": planos, "srtBase": bases}


def _nome(quem: str, tipo: str) -> str:
    if tipo == "narrador":
        return "Narrador (voz masculina)"
    if tipo == "narracao":
        return "Djanira (narração)" if quem == "DJANIRA" else quem
    return {"DJANIRA": "Djanira", "DINJER": "Dinjer", "DONA_IRACI": "Dona Iraci", "O_CHEFE": "O Chefe",
            "ATENDENTE": "Atendente"}.get(quem, quem.replace("_", " ").capitalize())


def _codigo_audio(quem: str) -> str:
    plain = unicodedata.normalize("NFKD", quem).encode("ascii", "ignore").decode().upper()
    return re.sub("[^A-Z0-9_]+", "_", plain).strip("_")


def _tempo(ms: int) -> str:
    return f"{ms // 3600000:02d}:{ms // 60000 % 60:02d}:{ms // 1000 % 60:02d},{ms % 1000:03d}"


def _srt(cues: list[dict]) -> bytes:
    texto = "\n".join(f"{n}\n{_tempo(c['inicioMs'])} --> {_tempo(c['fimMs'])}\n{c['legenda']}\n"
                       for n, c in enumerate(cues, 1))
    return texto.encode("utf-8")


def _csv(linhas: list[dict]) -> bytes:
    text = io.StringIO(newline="")
    writer = csv.DictWriter(text, fieldnames=COLUNAS)
    writer.writeheader()
    writer.writerows(linhas)
    return text.getvalue().encode("utf-8-sig")


def preparar_materiais(producao: dict) -> dict[str, bytes]:
    planos, arquivos, todas_linhas = producao["planos"], {}, []
    indice = {"versao": 1, "tipoDocumento": "material_edicao_pre_producao", "serie": "A_CALOTEIRA_1_TEMP",
              "origemIndiceSHA256": producao["indiceSHA256"], "fontes": producao["indice"]["fontes"],
              "audioRealGerado": False, "videoRealGerado": False, "sincronismoAutomatico": False,
              "criterioTimeline": "Ordem original, clipes após retirada manual da preparação/contagem pela referência realF7; tempos planejados, sem recaps/cartelas de duração indefinida.",
              "episodios": []}
    for episodio in producao["episodios"]:
        ep = episodio["episodio"]
        prefixo = f"EP{ep:02d}"
        cursor, linhas, cues, clipes = 0, [], [], []
        guia = [f"# {prefixo} — {episodio['nomeOriginal']}", "", "Material de preparação para dublagem/CapCut. Áudios sugeridos ainda não existem; vídeos ainda não são confirmados por este guia.", "",
                "A ordem abaixo é a do roteiro. A vinheta gravada uma única vez entra no final. Planos/SRTs fonte permanecem intactos.", "",
                "## Referência de tempo e sincronismo", "",
                "A entrada de voz no CSV tem três referências: conteúdo(sem contagem), F7(com3s de contagem) e episódio preliminar. O SRT base é relativo a F7, sem inferir o começo do MP4.", "",
                "No CapCut, identifique o início real da ação após a contagem e retire toda a preparação anterior. Se a captura começou antes deF7, remover apenas3s do começo do MP4 dá erro. Use imagem/contagem para ajustar o offset de cada tomada; data de criação do arquivo não sincroniza voz.", "",
                "O SRT de continuidade assume clipes com as durações planejadas abaixo, já sem contagem. ANTERIORMENTE/cartelas/flashbacks extras, transições e recortes de duração indefinida ficam fora dessa conta. Ao inseri-los, desloque/revise os tempos no CapCut. Não tratar esta duração como medida de um filme real.", "",
                "Reservas de tempo do cabeçalho não são falas nem silêncios escritos. Vozes e pausas explícitas nunca devem ser comprimidas para caber na estimativa.", "",
                "## Ordem de montagem", "", "| Cena | Entrada base | Duração planejada | SRT fonte |", "|---|---|---|---|"]
        for codigo in [*episodio["ordemCenas"], "VINHETA_01"]:
            plano = planos[codigo]
            duracao = plano["tempos"]["conteudoPlanejadoMs"]
            clipes.append({"id": codigo, "inicioEpisodioBaseMs": cursor, "fimEpisodioBaseMs": cursor + duracao,
                           "duracaoPlanejadaMs": duracao, "mp4Real": None, "offsetManualMp4ParaConteudoMs": None,
                           "retirarContagemPelaImagem": True, "arquivoSrtBase": codigo + ".base.srt"})
            base_path = f"compartilhados/{codigo}.base.srt" if codigo == "VINHETA_01" else f"{prefixo}/srt_base/{codigo}.base.srt"
            arquivos[base_path] = producao["srtBase"][codigo]
            guia.append(f"| {codigo} | {_tempo(cursor)} | {duracao / 1000:g}s | `{base_path}` |")
            for n, b in enumerate(plano["roteiro"], 1):
                vocal = b["texto"] is not None
                quem = b.get("quemOriginal", "")
                inicio, fim = cursor + b["inicioMs"], cursor + b["fimMs"]
                audio = (f"audio_sugerido/{codigo}_B{n:02d}_{b['tipo'].upper()}_{_codigo_audio(quem)}.wav" if vocal else "")
                linha = {"episodio": ep, "cena": codigo, "bloco": n, "tipo": b["tipo"],
                         "personagemCodigoOriginal": quem, "nomeVoz": _nome(quem, b["tipo"]) if vocal else "",
                         "textoOriginal": b["texto"] if vocal else "", "duracaoMs": b["duracaoMs"],
                         "inicioConteudoMs": b["inicioMs"], "fimConteudoMs": b["fimMs"],
                         "entradaRelativaF7Ms": b["inicioMs"] + 3000, "saidaRelativaF7Ms": b["fimMs"] + 3000,
                         "entradaEpisodioBaseMs": inicio, "saidaEpisodioBaseMs": fim,
                         "arquivoAudioSugerido": audio, "audioExiste": "false", "boca": "true" if b["boca"] else "false",
                         "linhaOriginal": b["linhaInicio"],
                         "observacao": b.get("indicacoesOriginais", "") if not vocal else "Narração emoff; sem boca" if b["tipo"] != "fala" else "Diálogo com boca e gesto; texto literal"}
                linhas.append(linha)
                if vocal:
                    rotulo = "Narrador" if b["tipo"] != "fala" else _nome(quem, "fala")
                    cues.append({"cena": codigo, "bloco": n, "inicioMs": inicio, "fimMs": fim,
                                 "legenda": rotulo + ": " + b["texto"]})
            cursor += duracao
        guia += ["", "## Reaproveitamento, cartelas e edição", ""]
        for montagem in episodio["montagemEditorial"]:
            guia += [montagem["origem"]["raw"].strip(), "",
                     "Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.", ""]
            if montagem["referenciasDerivadas"]:
                guia += ["Correspondência derivada para a montagem anunciada: " + ", ".join(montagem["referenciasDerivadas"]) + ". Conferir o trecho escolhido, preservando a nota original.", ""]
        observacoes = []
        for codigo in episodio["ordemCenas"]:
            plano = planos[codigo]
            guia += [f"### {codigo}", ""]
            guia += [a["raw"] for a in plano["acoesOriginais"]] + [""]
            if plano["cartelas"] or plano["reaproveitamentos"] or plano["conflitos"] or "CapCut" in plano["origem"]["raw"]:
                for cartela in plano["cartelas"]:
                    guia += [cartela["raw"], cartela.get("textoOriginal", ""), ""]
                for ref in plano["reaproveitamentos"]:
                    guia += [ref["raw"], ""]
                for conflito in plano["conflitos"]:
                    guia += ["- " + conflito["descricao"] + " " + conflito["resolucao"], ""]
            for nota in plano["notasOriginais"]:
                guia += [nota["raw"], ""]
            observacoes.extend({"cena": codigo, **c} for c in plano["conflitos"])
        if ep == 15:
            p = planos["EP15_C05"]
            fala4 = next(b for b in p["roteiro"] if b.get("numeroOriginal") == 4)
            hold = {"cena": "EP15_C05", "tipo": "camera_fixa_independente_da_voz", "personagem": "DINJER",
                    "inicioConteudoMs": fala4["inicioMs"], "fimConteudoMs": fala4["inicioMs"] + 12000,
                    "duracaoCameraMs": 12000, "duracaoVozMs": fala4["duracaoMs"], "adicionarSilencio": False,
                    "origem": next(n["raw"] for n in p["notasOriginais"] if "12" in n["raw"])}
            observacoes.append(hold)
            guia += ["### EP15_C05 — câmera na Dinjer", "", hold["origem"], "",
                     f"Manter a câmera por12s desde o começo da fala4 ({_tempo(hold['inicioConteudoMs'])}–{_tempo(hold['fimConteudoMs'])} do conteúdo). A voz dessa fala dura8s. O quadro atravessa falas seguintes; não adicionar4s de silêncio nem alterar o texto/duração das vozes.", ""]
        guia += ["### VINHETA_01 — reutilizar no final", "", "Uma gravação, quatro tomadas de5s; danças ainda exigem clipes reais/testados. Créditos montados no CapCut:", "", "```", planos["VINHETA_01"]["creditosOriginais"], "```", "",
                 "## Dublagem", "", "O CSV inclui todos os blocos originais, inclusive pausas/ação final sem voz. ArquivoAudioSugerido é somente um nome reservado na preparação: nenhum WAV/MP3 foi gerado. Narração e NARRADOR ficam emoff, sem boca; diálogos usam a atuação de fala. Não dublar os silêncios.", "",
                 "## Conferência antes da exportação", "", "- Conferir visual/ações/props/câmera na tomada real, sem substituir ações ausentes.",
                 "- Alinhar cada voz ao marcador visual do clipe e conferir os silêncios.",
                 "- Rever o SRT depois de inserir recap/cartelas/flashbacks/transições.",
                 "- Conferir o vídeo horizontal16:9 e o nome real da tomada; preservar regravações `_v2` etc.", ""]
        timeline = {"versao": 1, "tipoDocumento": "timeline_edicao_preliminar", "episodio": ep,
                    "nomeOriginal": episodio["nomeOriginal"], "origemIndiceSHA256": producao["indiceSHA256"],
                    "duracaoPlanejadaClipesMs": cursor, "clipes": clipes, "legendas": cues,
                    "montagemEditorial": episodio["montagemEditorial"], "observacoes": observacoes,
                    "audioReal": False, "mp4Real": False, "sincronismoMedido": False,
                    "excluiTemposIndefinidos": True, "vinhetaReutilizada": "VINHETA_01"}
        arquivos[f"{prefixo}/GUIA_CAPCUT.md"] = "\n".join(guia).encode("utf-8")
        arquivos[f"{prefixo}/{prefixo}.timeline.json"] = _json(timeline)
        arquivos[f"{prefixo}/{prefixo}.vozes.csv"] = _csv(linhas)
        arquivos[f"{prefixo}/{prefixo}.continuidade_base.srt"] = _srt(cues)
        todas_linhas.extend(linhas)
        indice["episodios"].append({"numero": ep, "nome": episodio["nomeOriginal"], "guia": f"{prefixo}/GUIA_CAPCUT.md",
                                   "timeline": f"{prefixo}/{prefixo}.timeline.json", "vozes": f"{prefixo}/{prefixo}.vozes.csv",
                                   "srtContinuidade": f"{prefixo}/{prefixo}.continuidade_base.srt",
                                   "duracaoPlanejadaClipesMs": cursor, "cenas": episodio["ordemCenas"], "estado": "preparacao"})
    arquivos["dublagem_temporada.csv"] = _csv(todas_linhas)
    indice["contagem"] = {"episodios": 15, "planosOriginais": 95, "blocosOriginais": len(todas_linhas),
                          "blocosVoz": sum(bool(r["arquivoAudioSugerido"]) for r in todas_linhas),
                          "pausas": sum(r["tipo"] == "pausa" for r in todas_linhas)}
    indice["arquivosDerivados"] = [{"arquivo": p, "sha256": sha256(b)} for p, b in sorted(arquivos.items())]
    arquivos["indice.json"] = _json(indice)
    return arquivos


def publicar_edicao(arquivos: dict[str, bytes], raiz: Path = RAIZ) -> int:
    raiz = Path(raiz).absolute()
    destino = raiz / "producao" / "edicao"
    mudancas = {}
    for nome, conteudo in arquivos.items():
        rel = Path(nome)
        if rel.is_absolute() or ".." in rel.parts:
            raise ErroEdicao("Caminho inseguro de material derivado.")
        alvo = _sem_vinculos(destino / rel, raiz)
        if alvo.exists() and not alvo.is_file():
            raise ErroEdicao(f"Destino não é arquivo: {alvo}.")
        if not alvo.is_file() or alvo.read_bytes() != conteudo:
            mudancas[alvo] = conteudo
    existentes = [p for p in mudancas if p.exists()]
    if existentes:
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-edicao-" + uuid.uuid4().hex[:8]
        backup = raiz / "backups" / "projeto" / stamp
        manifest = []
        for path in existentes:
            rel = path.relative_to(raiz)
            copia = backup / rel
            copia.parent.mkdir(parents=True, exist_ok=True)
            digest = sha256(path.read_bytes())
            shutil.copy2(path, copia)
            if sha256(copia.read_bytes()) != digest:
                raise ErroEdicao("Backup de edição não conferiu.")
            manifest.append({"arquivo": str(rel), "sha256": digest, "backup": str(copia.relative_to(raiz))})
        (backup / "manifesto.json").write_bytes(_json(manifest))
    for path, bytes_ in mudancas.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        temp = path.with_name(path.name + ".tmp-" + uuid.uuid4().hex)
        try:
            with temp.open("xb") as stream:
                stream.write(bytes_); stream.flush(); os.fsync(stream.fileno())
            _sem_vinculos(path, raiz)
            os.replace(temp, path)
        finally:
            if temp.exists():
                temp.unlink()
        if sha256(path.read_bytes()) != sha256(bytes_):
            raise ErroEdicao("Material de edição gravado não conferiu.")
    return len(mudancas)


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--conferir", action="store_true", help="conferir materiais sem alterar arquivos")
    args = parser.parse_args(argv)
    try:
        arquivos = preparar_materiais(ler_planos())
        if args.conferir:
            raiz = RAIZ / "producao" / "edicao"
            diferentes = [p for p, b in arquivos.items() if not (raiz / p).is_file() or (raiz / p).read_bytes() != b]
            if diferentes:
                raise ErroEdicao(f"{len(diferentes)} materiais ausentes/diferentes.")
            print("Dublagem/CapCut dos15 episódios conferidos; áudio, vídeo e sincronismo real pendentes.")
        else:
            quantidade = publicar_edicao(arquivos)
            print(f"Materiais dos15 episódios preparados; {quantidade} arquivos derivados. Nenhum áudio/MP4 gerado; alinhar no CapCut pelo marcador visual.")
        return 0
    except (ErroEdicao, ErroPreparacao, OSError, KeyError, ValueError) as exc:
        print("Erro de edição: " + str(exc), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
