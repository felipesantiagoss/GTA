"""Pré-produção fiel do roteiro: planos auditáveis, nunca cenas GTA incompletas.

Só usa a biblioteca padrão. Os arquivos .plano.json não seguem o schema de
cena executável. A origem e as instruções originais são preservadas literalmente;
requisitos/adaptações são uma segunda camada, explicitamente derivada.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import sys
from datetime import datetime, timezone
import uuid


RAIZ = Path(__file__).resolve().parents[1]
ROTEIRO = "ROTEIRO_A_CALOTEIRA_DO_OESTE_15_EPISODIOS.md"
PROMPT = "PROMPT_CODEX_CENAS_E_CAMERA.md"
CONTAGEM_MS = 3000
CONTAGENS = (9, 7, 7, 6, 5, 6, 5, 6, 6, 7, 5, 5, 6, 5, 9)
CENA_RE = re.compile(r"^### (EP(\d{2})_C(\d{2})|VINHETA_01) · (.+)$")
EP_RE = re.compile(r"^# 🎬 EPISÓDIO (\d+) — (.+)$")
VOZ_RE = re.compile(r"^\*\*(NARRAÇÃO|NARRADOR)\*\* — (?:(.+?) — )?(\d+)s(.*)$")
CAMERAS = {"LONGE", "DOIS", "MEDIA", "PERTO", "BAIXA", "ALTA", "ATRAS", "OLHOS"}
ELENCO = ("DJANIRA_POBRE", "DJANIRA_RICA", "DINJER", "DONA_IRACI", "O_CHEFE",
          "CAPANGA_1", "CAPANGA_2", "CAPANGA_3", "CAPANGA_4",
          "COLEGA_1", "COLEGA_2", "COLEGA_3", "ATENDENTE")

# Descrições de famílias, não IDs de modelos, animações ou coordenadas GTA.
FAMILIAS = {
    "visuais": "Identidade, rosto, roupa fixa, cabelo, acessórios e visual aprovado no modelo real.",
    "ambiente": "Marcas reais, sublocais/interiores, hora/clima, limpeza seletiva e restauração.",
    "corpo": "Poses e transições: sentar/deitar/ajoelhar/levantar, trabalho, dança, reação e luta coreografada.",
    "movimento": "Rotas e estilos de andar; correr, água/rampa/queda; sem teleporte para fingir chegada.",
    "fala": "Boca, gesto, expressão e olhar compatíveis com o visual, pose e ações concorrentes.",
    "veiculos": "Modelo verificado, motorista/assentos, portas, embarque/desembarque, rota e limpeza.",
    "objetos": "Modelos e ossos verificados, segurar/usar/entregar/abrir e objetos de cenário.",
    "figurantes": "Presenças descritas fora do elenco; perfis reais e figurantes controlados.",
    "camera": "Alvos sem ator/individuais/grupos, terceiro interlocutor, objetos, eixo, movimento e reutilização exata.",
    "edicao": "Cartelas, créditos, som/vozes, telefone em locais separados, montagem e reaproveitamento no CapCut.",
    "gravacao": "Game Bar real, vínculo da tomada, MP4/SRT versionados e organização antes da automação de teclado.",
}
VEICULOS_POR_CENA = {
    "EP01_C01": ["avião particular"], "EP01_C06": ["ônibus"],
    "EP01_C08": ["moto"], "EP01_C09": ["moto"], "EP03_C01": ["carros que passam"],
    "EP04_C01": ["ônibus"], "EP06_C01": ["ônibus"],
    "EP07_C03": ["avião particular"], "EP07_C04": ["avião particular/interior"],
    "EP07_C05": ["avião particular (tomada reaproveitada)"],
    "EP11_C03": ["carro caro"], "EP12_C01": ["carro caro"],
    "EP13_C03": ["caminhões"], "EP15_C01": ["carro"], "EP15_C09": ["carro"],
}
PRESENCAS_POR_CENA = {
    "EP01_C09": ["Dinjer saindo na moto; ausente do elenco declarado"],
    "EP04_C01": ["pessoas bem vestidas no terminal; sem perfis no elenco"],
    "EP04_C03": ["colegas saindo com Dinjer; ausentes do elenco declarado"],
    "EP06_C02": ["segurança e fila da boate; sem perfis no elenco"],
    "EP11_C01": ["segurança do banco; sem perfil no elenco"],
    "EP12_C01": ["segurança abrindo a porta; sem perfil no elenco"],
    "EP13_C02": ["gerente entregando envelopes; sem perfil no elenco"],
    "EP10_C05": ["pessoas na calçada; sem perfis no elenco"],
    "EP15_C06": ["pai e amiga presentes na ação; ausentes do elenco declarado"],
}
OBJETOS_RE = (
    ("celular", r"celular|telefone|ao telefone|Pix"),
    ("envelope", r"envelope"), ("papel", r"papel|bilhete"),
    ("dinheiro físico", r"conta,|dinheiro"), ("sacola", r"sacola"),
    ("sapatos na mão", r"sapatos na mão"), ("bandeja", r"bandeja"),
    ("bebida/copo", r"bebendo|servindo|champanhe|jantar"),
    ("remédio/caixa vazia", r"caixa de remédio"),
    ("ferramenta de capina", r"capinando|trabalhando"),
    ("ferramenta de limpeza", r"limpando"),
    ("móveis/poses de apoio", r"sentad|deitada|dormindo|mesa|banco,|guarda-roupa"),
    ("porta/portão", r"porta|portão|portões"),
    ("contêiner/caixas de cenário", r"contêiner|caixas"),
)
CONFLITOS_FIXOS = {
    "EP03_C07": [("capanga_terno", "CAPANGA_1 mantém o terno fixo do prompt; preservar a indicação original 'sem terno' e sua fala, sem criar variante silenciosa.")],
    "EP07_C01": [("figurino_fixo", "Roupa de igreja conflita com DJANIRA_POBRE fixo; o prompt prevalece, ação/texto original permanecem.")],
    "EP08_C01": [("figurino_fixo", "Roupa rasgada/descalça não autoriza criar um terceiro figurino de Djanira; efeito molhado/machucado exige recurso próprio.")],
    "EP08_C02": [("figurino_fixo", "Djanira descalça conflita com o figurino fixo; registrar a adaptação sem alterar o roteiro.")],
    "EP10_C02": [("figurino_fixo", "Uniforme de faxina conflita com DJANIRA_POBRE fixo do prompt.")],
    "EP03_C05": [("figurino_dinjer", "Dinjer descalça com sapatos na mão conflita com uniforme fixo; nenhuma troca é aprovada implicitamente.")],
    "EP14_C01": [("figurino_dinjer", "A indicação sem maquiagem conflita com visual fixo de Dinjer.")],
    "EP15_C03": [("gesto_machucado", "Intenção 'machucado' fora dos cinco gestos catalogados; pesquisar e testar sem substituir silenciosamente.")],
    "EP15_C05": [("gesto_machucado", "Intenção 'machucado' fora dos cinco gestos catalogados; pesquisar e testar sem substituir silenciosamente."),
                 ("camera_12s", "Nota pede manter a câmera na fala 4 por 12s; a voz dura 8s. Preservar ambos, não estender/comprimir a fala.")],
    "EP15_C08": [("montagem_tempos", "Duas tomadas de 4s totalizam 8s; narração de 10s e estimativa de 12s permanecem separadas, montagem a definir.")],
    "EP09_C01": [("presenca_narrada", "A ação exige Djanira sozinha; a narração cita outras pessoas. Não criar figurantes contrariando a ação.")],
    "EP11_C03": [("troca_figurino", "O prompt determina DJANIRA_RICA desde o início do episódio11, embora este bloco diga que a troca só acontece aqui.")],
}


class ErroPreparacao(ValueError):
    """Erro de origem, plano ou exportação, com mensagem em português."""


def sha256(dados: bytes) -> str:
    return hashlib.sha256(dados).hexdigest().upper()


def _fonte(caminho: Path) -> tuple[dict, list[str]]:
    dados = caminho.read_bytes()
    try:
        texto = dados.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise ErroPreparacao(f"Fonte {caminho.name}: esperado texto UTF-8 válido.") from exc
    linhas = texto.splitlines(keepends=True)
    return {"arquivo": caminho.name, "sha256": sha256(dados), "linhas": len(linhas),
            "utf8Bom": dados.startswith(b"\xef\xbb\xbf")}, linhas


def _trecho(linhas: list[str], inicio: int, fim: int, fonte: dict) -> dict:
    raw = "".join(linhas[inicio:fim])
    return {"arquivo": fonte["arquivo"], "sha256Arquivo": fonte["sha256"],
            "linhaInicio": inicio + 1, "linhaFim": fim,
            "sha256BlocoUtf8": sha256(raw.encode("utf-8")), "raw": raw}


def _texto_citado(linha: str, numero: int) -> str:
    texto = linha.removeprefix("> ").strip()
    if not (texto.startswith('"') and texto.endswith('"')):
        raise ErroPreparacao(f"Linha {numero}: esperado texto literal entre aspas.")
    return texto[1:-1]


def _rotulo(quem: str, tipo: str) -> str:
    if tipo != "fala":
        return "Narrador"
    return {"DJANIRA": "Djanira", "DINJER": "Dinjer", "DONA_IRACI": "Dona Iraci",
            "O_CHEFE": "O Chefe", "ATENDENTE": "Atendente"}.get(quem, quem.replace("_", " ").capitalize())


def _metadados(linhas: list[str], inicio: int) -> dict:
    a, b = linhas[inicio + 1].strip(), linhas[inicio + 2].strip()
    ambiente = re.fullmatch(r"`local:` (.+) · `hora:` (\d{2}:\d{2}) · `clima:` (.+)", a)
    elenco = re.fullmatch(r"`elenco:` (.+) · `cam:` (.+) · `dur:` (\d+)s · `gravar:` (SIM|NÃO)", b)
    if ambiente is None or elenco is None:
        raise ErroPreparacao(f"Linha {inicio + 1}: metadados de cena não reconhecidos; não gerar plano parcial.")
    local, hora, clima = ambiente.groups()
    atores, camera, dur, gravar = elenco.groups()
    codigos = re.findall(r"\b(?:DJANIRA_POBRE|DJANIRA_RICA|DINJER|DONA_IRACI|O_CHEFE|CAPANGA_[1-4]|COLEGA_[1-3]|ATENDENTE)\b", atores)
    if atores != "*(nenhum)*" and not codigos:
        raise ErroPreparacao(f"Linha {inicio + 3}: elenco sem códigos reconhecidos.")
    planos = camera.split(" → ")
    if camera != "auto" and any(x not in CAMERAS for x in planos):
        raise ErroPreparacao(f"Linha {inicio + 3}: câmera desconhecida.")
    return {"local": local, "locaisCodigos": list(dict.fromkeys(re.findall(r"\b[A-Z][A-Z0-9_]+\b", local))),
            "hora": hora, "clima": clima, "elenco": atores, "elencoCodigos": codigos,
            "camera": camera, "planosSemTemposDefinidos": [] if camera == "auto" else planos,
            "duracaoEstimadaMs": int(dur) * 1000, "gravar": gravar,
            "linhasMetadados": [inicio + 2, inicio + 3]}


def _blocos_temporais(linhas: list[str], inicio: int, fim: int) -> tuple[list[dict], list[dict], list[dict]]:
    blocos, acoes, cartelas = [], [], []
    cursor = 0
    i = inicio + 3

    def adicionar(bloco: dict, duracao: int) -> None:
        nonlocal cursor
        bloco.update({"inicioMs": cursor, "fimMs": cursor + duracao, "duracaoMs": duracao})
        blocos.append(bloco)
        cursor += duracao

    while i < fim:
        linha = linhas[i].strip()
        voz = VOZ_RE.match(linha)
        if voz:
            tipo_fonte, quem, segundos, indicacoes = voz.groups()
            j = i + 1
            while j < fim and not linhas[j].strip():
                j += 1
            if j == fim or not linhas[j].strip().startswith('> "'):
                raise ErroPreparacao(f"Linha {i + 1}: bloco de voz sem texto literal.")
            tipo = "narrador" if tipo_fonte == "NARRADOR" or (quem or "").startswith("NARRADOR") else "narracao"
            quem = quem or "NARRADOR"
            adicionar({"tipo": tipo, "tipoOriginal": tipo_fonte, "quemOriginal": quem,
                       "texto": _texto_citado(linhas[j].strip(), j + 1), "boca": False,
                       "cabecalhoOriginal": linha, "textoOriginal": linhas[j].strip(),
                       "indicacoesOriginais": indicacoes, "linhaInicio": i + 1, "linhaTexto": j + 1}, int(segundos) * 1000)
            i = j + 1
            continue
        if linha.startswith("|"):
            colunas = [c.strip() for c in linha.strip("|").split("|")]
            if len(colunas) == 5 and re.fullmatch(r"\d+s", colunas[3]):
                ordem, quem, texto, segundos, gesto = colunas
                duracao = int(segundos[:-1]) * 1000
                if ordem == "—" and quem == "*(silêncio)*":
                    adicionar({"tipo": "pausa", "boca": False, "texto": None,
                               "indicacoesOriginais": texto, "linhaInicio": i + 1, "raw": linha}, duracao)
                elif ordem.isdigit() and texto.startswith('"') and texto.endswith('"'):
                    adicionar({"tipo": "fala", "tipoOriginal": "DIÁLOGO", "numeroOriginal": int(ordem),
                               "quemOriginal": quem, "texto": texto[1:-1], "textoOriginal": texto,
                               "gestoOriginal": gesto, "boca": True, "linhaInicio": i + 1, "raw": linha}, duracao)
                else:
                    raise ErroPreparacao(f"Linha {i + 1}: linha de diálogo desconhecida.")
        if linha.startswith("**Ação:") or linha.startswith("**Ação final:"):
            acao = {"linha": i + 1, "raw": linha, "tipo": "final" if "Ação final" in linha else "principal",
                    "estadoImplementacao": "pendente", "implementacao": None}
            acoes.append(acao)
            if acao["tipo"] == "final":
                segundos = re.search(r"(\d+) segundos", linha)
                if not segundos:
                    raise ErroPreparacao(f"Linha {i + 1}: duração de ação final não reconhecida.")
                adicionar({"tipo": "acao_final", "boca": False, "texto": None, "linhaInicio": i + 1,
                           "indicacoesOriginais": linha}, int(segundos.group(1)) * 1000)
        if "CARTELA" in linha or linha.startswith("**TEXTO NA TELA"):
            cartela = {"linha": i + 1, "raw": linha, "realizacao": "CapCut", "tempoDefinido": False}
            if i + 1 < fim and linhas[i + 1].strip().startswith('> "'):
                cartela["texto"] = _texto_citado(linhas[i + 1].strip(), i + 2)
                cartela["textoOriginal"] = linhas[i + 1].strip()
            cartelas.append(cartela)
        i += 1
    return blocos, acoes, cartelas


def _reaproveitamentos(linhas: list[str], inicio: int, fim: int, cena: str) -> list[dict]:
    referencias = []
    for i in range(inicio, fim):
        linha = linhas[i].strip()
        if not re.search(r"reaproveit|flashback|mesm[oa] (?:enquadramento|câmera)|guarde|salve|enquadramento", linha, re.I):
            continue
        for codigo in re.findall(r"\bEP\d{2}_C\d{2}\b", linha):
            referencias.append({"cena": codigo, "linha": i + 1, "raw": linha,
                                "tipo": "enquadramento" if "enquadramento" in linha.lower() else "imagem",
                                "opcionalRegravar": "regravar" in linha.lower()})
    # Esta relação aparece com IDs abreviados no texto ('Episódio 6, Cena 2').
    if cena == "EP12_C01" and not any(x["cena"] == "EP06_C02" for x in referencias):
        for i in range(inicio, fim):
            if "enquadramento" in linhas[i].lower():
                referencias.append({"cena": "EP06_C02", "linha": i + 1, "raw": linhas[i].strip(),
                                    "tipo": "enquadramento", "opcionalRegravar": False,
                                    "derivacao": "Episódio 6, Cena 2 corresponde a EP06_C02; conservar posição, distância e hora."})
    return referencias


def _requisitos(plano: dict) -> list[dict]:
    original = plano["original"]
    requisitos = []

    def incluir(codigo: str, familia: str, descricao: str, origem: str) -> None:
        requisitos.append({"codigo": codigo, "familia": familia, "descricao": descricao,
                           "origem": origem, "estado": "pendente", "recursoVerificado": None,
                           "confirmadoNoJogo": False})

    for ator in plano["adaptacao"]["elencoEfetivo"]:
        incluir("visual:" + ator, "visuais", f"Visual de produção {ator}, com rosto/figurino fixos aprovados.", "elenco original e regra do prompt")
    for local in original["locaisCodigos"]:
        incluir("local:" + local, "ambiente", f"Marca real {local}; interior/sublocais e rotas compatíveis com a ação.", "metadados de local")
    if plano["id"] == "EP05_C03":
        incluir("local:AEROPORTO_TERMINAL", "ambiente", "Marca real separada para Dinjer na cena de telefone.", "nota literal da cena")
    incluir("camera:composicao", "camera", "Compor em16:9 todos os alvos/planos/olhares descritos, sem encobrir instruções específicas.", "câmera e notas originais")
    for n, acao in enumerate(plano["acoesOriginais"], 1):
        incluir(f"acao:{n}", "corpo", "Implementar integralmente: " + acao["raw"], f"linha {acao['linha']}")
    if not plano["acoesOriginais"]:
        incluir("acao:bloqueio", "corpo", "Resolver poses, marcas e atuação pelas notas originais; não substituir por atores parados genéricos.", "notas da cena")
    corporal = "\n".join(a["raw"] for a in plano["acoesOriginais"])
    for objeto, padrao in OBJETOS_RE:
        if re.search(padrao, corporal, re.I):
            incluir("objeto:" + objeto, "objetos", objeto + ": modelo/uso/attachment real e testado.", "descrição de ação original; classificação derivada")
    for veiculo in VEICULOS_POR_CENA.get(plano["id"], []):
        incluir("veiculo:" + veiculo, "veiculos", veiculo + ": modelo, assentos/portas, motorista, percurso e restauração.", "descrição de ação original; classificação derivada")
    for presenca in PRESENCAS_POR_CENA.get(plano["id"], []):
        incluir("presenca:" + presenca, "figurantes", presenca, "ação original; falta de perfil declarada")
    if any(b["tipo"] == "fala" for b in plano["roteiro"]):
        incluir("fala:visual_e_pose", "fala", "Boca, gesto e olhar testados nos modelos/poses reais de cada interlocutor.", "blocos DIÁLOGO")
    if re.search(r"andando|anda |caminh|corre|correndo|sub[eio]|desce|entra|sai|levanta", corporal, re.I):
        incluir("movimento:rotas", "movimento", "Capturar percursos/transições e estilos de andar apropriados; não inventar posições.", "ações originais; classificação derivada")
    if plano["id"] in ("EP01_C01", "EP07_C05", "EP08_C01"):
        incluir("movimento:queda_agua", "movimento", "Teste técnico separado: avião/queda, invencibilidade, água, saída e rampa conforme a cena.", "ação técnica e reaproveitamento originais")
    if original["camera"] == "auto" and any(b["tipo"] == "fala" and b["quemOriginal"] not in {x.replace("_POBRE", "").replace("_RICA", "") for x in original["elencoCodigos"][:2]} for b in plano["roteiro"]):
        incluir("camera:terceiro_interlocutor", "camera", "Eixo e alvo explícitos para interlocutores além dos dois primeiros; contrato atual não os executa automaticamente.", "elenco e diálogo originais")
    if len(original["elencoCodigos"]) not in (2, 3, 4):
        incluir("elenco:quantidade", "visuais", "Suportar a quantidade original de atores sem inventar/omitir personagens.", "elenco original")
    if plano["reaproveitamentos"]:
        incluir("edicao:reaproveitamento", "edicao", "Vincular a tomada/frame original indicado; não gravar uma substituição sem registrá-la.", "notas originais")
    if plano["cartelas"] or "CapCut" in plano["origem"]["raw"] or plano["id"] == "VINHETA_01":
        incluir("edicao:material", "edicao", "Montar textos/cartelas/créditos no CapCut conservando o conteúdo original.", "instruções originais")
    if re.search(r"montagem|tomadas|telefone", corporal, re.I) or plano["id"] == "VINHETA_01":
        incluir("edicao:multiplas_tomadas", "edicao", "Plano separado de tomadas/subplanos, com sua montagem e tempo; não fingir uma única atuação contínua.", "ações e notas originais")
    incluir("gravacao:tomada", "gravacao", "Captura real Game Bar e associação segura MP4/SRT; nenhuma tomada existe ainda neste plano.", "regras do prompt")
    return requisitos


def _plano(linhas: list[str], inicio: int, fim: int, fonte: dict) -> dict:
    match = CENA_RE.fullmatch(linhas[inicio].strip())
    assert match is not None
    codigo, episodio, numero, nome = match.groups()
    original = _metadados(linhas, inicio)
    blocos, acoes, cartelas = _blocos_temporais(linhas, inicio, fim)
    ep = int(episodio) if episodio else 0
    efetivo = ["DJANIRA_RICA" if x == "DJANIRA_POBRE" and ep >= 11 else x for x in original["elencoCodigos"]]
    adaptacoes = []
    conflitos = [{"codigo": codigo_c, "descricao": texto, "resolucao": "preservar origem e implementar regra do prompt explicitamente"}
                 for codigo_c, texto in CONFLITOS_FIXOS.get(codigo, [])]
    if efetivo != original["elencoCodigos"]:
        adaptacoes.append({"tipo": "figurino", "origem": original["elencoCodigos"], "destino": efetivo,
                           "autoridade": PROMPT, "motivo": "Djanira rica nos episódios11–15, mantendo o mesmo rosto."})
        conflitos.append({"codigo": "djanira_ep11", "descricao": "Elenco/ação do roteiro usa Djanira pobre no episódio11; prompt determina rica desde o início.",
                          "resolucao": "DJANIRA_RICA; elenco e texto originais continuam arquivados"})
    if codigo == "EP03_C07":
        adaptacoes.append({"tipo": "figurino", "personagem": "CAPANGA_1", "original": "sem terno",
                           "destino": "terno fixo", "autoridade": PROMPT, "motivo": "Somente Djanira troca de figurino."})
    fim_blocos = max((b["fimMs"] for b in blocos), default=0)
    estimada = original["duracaoEstimadaMs"]
    planejada = max(fim_blocos, estimada)
    if fim_blocos > estimada:
        conflitos.append({"codigo": "duracao_estimada", "descricao": f"Estimativa {estimada}ms menor que blocos declarados {fim_blocos}ms.",
                          "resolucao": "Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes."})
    origem = _trecho(linhas, inicio, fim, fonte)
    notas = [{"linha": i + 1, "raw": linhas[i].strip()} for i in range(inicio + 3, fim)
             if linhas[i].strip().startswith(("> 💡", "> ⚠️", "> 🔴", "⚠️"))]
    plano = {"versao": 1, "tipoDocumento": "plano_producao", "id": codigo,
             "episodio": ep, "numero": int(numero) if numero else 0, "nome": nome,
             "origem": origem, "original": original,
             "adaptacao": {"elencoEfetivo": efetivo, "regras": adaptacoes},
             "acoesOriginais": acoes, "notasOriginais": notas, "cartelas": cartelas,
             "roteiro": blocos,
             "tempos": {"estimativaOriginalMs": estimada, "blocosDeclaradosMs": fim_blocos,
                         "conteudoPlanejadoMs": planejada, "reservaSemBlocoDefinidoMs": max(0, estimada - fim_blocos),
                         "contagemMs": CONTAGEM_MS, "capturaPlanejadaMs": planejada + CONTAGEM_MS,
                         "fimUltimaLegendaMs": max((b["fimMs"] + CONTAGEM_MS for b in blocos if b["texto"] is not None), default=0),
                         "criterio": "Vozes/pausas sequenciais na ordem original; ações podem ser simultâneas. Estimativa não corta blocos. Reserva não cria falas ou ação."},
             "reaproveitamentos": _reaproveitamentos(linhas, inicio, fim, codigo),
             "conflitos": conflitos,
             "execucao": {"estado": "pendente", "pronto": False, "gravado": False, "runtime": None,
                          "motivo": "Plano fiel de pré-produção; recursos e atuação completa ainda precisam de implementação e teste."}}
    if codigo == "VINHETA_01":
        plano["vinheta"] = {"gravarUmaVez": True, "reutilizarEpisodios": list(range(1, 16)),
                            "tomadasFisicas": [{"personagem": ator, "inicioMs": i * 5000,
                                                "fimMs": (i + 1) * 5000, "duracaoMs": 5000,
                                                "intencaoDancaOriginal": intencao, "animacaoVerificada": None}
                                               for i, (ator, intencao) in enumerate(zip(original["elencoCodigos"],
                                                   ["curtindo", "requebrando", "duro de dançar", "deslizando"]))]}
        fence = origem["raw"].split("```", 2)
        if len(fence) != 3:
            raise ErroPreparacao("VINHETA_01: créditos literais não encontrados.")
        plano["creditosOriginais"] = fence[1].strip("\r\n")
    plano["requisitos"] = _requisitos(plano)
    return plano


def analisar_roteiro(caminho: Path, prompt: Path | None = None) -> dict:
    """Lê integralmente a origem e retorna todos os planos, sem escrever arquivos."""
    fonte, linhas = _fonte(Path(caminho))
    prompt = Path(prompt) if prompt else Path(caminho).parent / PROMPT
    fonte_prompt, linhas_prompt = _fonte(prompt)
    # A leitura integral integra o hash de autoridade, sem aplicar o fluxo antigo por engano.
    if not linhas_prompt:
        raise ErroPreparacao("Prompt de prioridade vazio.")
    inicios = [i for i, l in enumerate(linhas) if CENA_RE.fullmatch(l.strip())]
    planos, spans = [], []
    for inicio in inicios:
        fim = next((i for i in range(inicio + 3, len(linhas)) if linhas[i].strip() == "---"), len(linhas))
        if any(i != inicio and inicio < i < fim for i in inicios):
            raise ErroPreparacao(f"Linha {inicio + 1}: separador de cena ausente.")
        planos.append(_plano(linhas, inicio, fim, fonte))
        spans.append((inicio, fim))
    por_id = {p["id"]: p for p in planos}
    if len(por_id) != len(planos) or len(planos) != 95:
        raise ErroPreparacao(f"Contagem inesperada: {len(planos)} planos/{len(por_id)} IDs únicos; esperado94 +1 vinheta.")
    if [sum(p["episodio"] == ep for p in planos) for ep in range(1, 16)] != list(CONTAGENS):
        raise ErroPreparacao("Contagem por episódio difere da origem conhecida; revisar o roteiro antes de gerar.")
    for ep, quantidade in enumerate(CONTAGENS, 1):
        if [p["id"] for p in planos if p["episodio"] == ep] != [f"EP{ep:02d}_C{n:02d}" for n in range(1, quantidade + 1)]:
            raise ErroPreparacao(f"Episódio{ep}: IDs ou ordem original inesperados.")
    episodios = []
    headers = [(i, EP_RE.fullmatch(l.strip())) for i, l in enumerate(linhas) if EP_RE.fullmatch(l.strip())]
    for indice, (inicio_ep, match) in enumerate(headers):
        assert match is not None
        ep, nome = int(match.group(1)), match.group(2)
        fim_ep = headers[indice + 1][0] if indice + 1 < len(headers) else next(i for i, l in enumerate(linhas) if l.startswith("# 🎞️"))
        ids = [p["id"] for p in planos if p["episodio"] == ep]
        partes_editoriais = []
        for i in range(inicio_ep, fim_ep):
            if "ANTERIORMENTE" in linhas[i] or "NO PRÓXIMO EPISÓDIO" in linhas[i]:
                fim = next((j for j in range(i + 1, fim_ep) if linhas[j].strip() == "---"), fim_ep)
                raw = _trecho(linhas, i, fim, fonte)
                referencias = [{"cena": m.group(1), "falaOriginal": int(m.group(2)) if m.group(2) else None}
                               for m in re.finditer(r"(EP\d{2}_C\d{2})(?: fala (\d+))?", raw["raw"])]
                partes_editoriais.append({"tipo": "anteriormente" if "ANTERIORMENTE" in linhas[i] else "proximo_episodio",
                                          "origem": raw, "referenciasOriginais": referencias,
                                          "realizacao": "CapCut", "gravarNovamente": False,
                                          "referenciasDerivadas": ["EP02_C01", "EP02_C02"] if "NO PRÓXIMO" in linhas[i] and ep == 1 else []})
        titulo_extra = next((linhas[i].strip()[3:] for i in range(inicio_ep + 1, min(inicio_ep + 5, fim_ep)) if linhas[i].startswith("## ")), None)
        episodios.append({"versao": 1, "tipoDocumento": "manifesto_episodio", "episodio": ep,
                          "nomeOriginal": nome, "subtituloOriginal": titulo_extra,
                          "ordemCenas": ids, "cenas": [{"id": x, "plano": x + ".plano.json", "srtBase": x + ".base.srt"} for x in ids],
                          "montagemEditorial": partes_editoriais, "vinheta": "VINHETA_01",
                          "estado": "preparacao", "runtime": None, "gravado": False,
                          "origem": _trecho(linhas, inicio_ep, fim_ep, fonte) | {"raw": None}})
    if len(episodios) != 15:
        raise ErroPreparacao("Esperados15 cabeçalhos reais de episódio.")
    for plano in planos:
        for ref in plano["reaproveitamentos"]:
            if ref["cena"] not in por_id:
                raise ErroPreparacao(f"{plano['id']}: referência ausente {ref['cena']}.")
    complementos, cursor = [], 0
    for inicio, fim in spans:
        if cursor < inicio:
            complementos.append(_trecho(linhas, cursor, inicio, fonte))
        cursor = fim
    if cursor < len(linhas):
        complementos.append(_trecho(linhas, cursor, len(linhas), fonte))
    locais = []
    for i, linha in enumerate(linhas):
        local = re.match(r"\| `([A-Z][A-Z0-9_]+)` \| (.+) \| (.+) \| (.+) \|", linha.strip())
        if local:
            codigo, lugar, onde, uso = local.groups()
            locais.append({"codigo": codigo, "lugarOriginal": lugar, "regiaoOriginal": onde,
                           "usoDeclaradoOriginal": uso, "linha": i + 1, "marcaReal": None})
    if len(locais) != 21:
        raise ErroPreparacao(f"Esperados21 locais do roteiro; encontrados{len(locais)}.")
    return {"versao": 1, "serie": "A_CALOTEIRA_1_TEMP", "fontes": {"roteiro": fonte, "promptPrioritario": fonte_prompt},
            "planos": planos, "episodios": episodios, "locaisOriginais": locais,
            "materialNaoCenaOriginal": complementos, "familiasCapacidades": FAMILIAS,
            "piloto": "EP01_C08", "estado": "preparacao", "runtime": None,
            "conflitosGerais": [
                "Autorização mais recente permite preparar todos os15; roteiro/prompt antigos exigiam um por vez.",
                "Piloto priorizado EP01_C08; referências antigas EP01_C04 ficam preservadas na origem.",
                "Djanira: pobreEP1–10, ricaEP11–15; somente ela troca figurino, mesma identidade facial.",
                "Capangas definidos sem fala na Parte1, mas CAPANGA_1 fala emEP03_C07; conservar a fala explícita e o terno fixo.",
                "Colegas citados como apenasEp1 também aparecemEp4; conservar aparições e registrar divergência.",
                "Atendente sempre de costas/sem rosto; câmera automática precisa respeitar essa regra.",
                "Boate/interiores e localização Hawick não são confirmados; localizar e testar antes de marcar. Fachada é planoB explícito.",
                "EP10 anuncia oito tomadas, mas contém sete IDs; não inventar cena08.",
                "Parte5 diz cinco principais; são quatro identidades principais e cinco perfis com os dois figurinos de Djanira.",
                "A fala sobre o pai morto é preservada; a revelação final não autoriza reescrever narração anterior.",
                "Limpeza de população não elimina figurantes dirigidos previstos na ação; estes exigem perfis/controladores próprios.",
                "Cenas narradas sem diálogo não exigem boca; NARRADOR não cria ator. Silêncios não recebem fala/legenda.",
            ]}


def gerar_srt(plano: dict) -> str:
    def tempo(ms: int) -> str:
        return f"{ms // 3600000:02d}:{ms // 60000 % 60:02d}:{ms // 1000 % 60:02d},{ms % 1000:03d}"
    cues = []
    for bloco in plano["roteiro"]:
        if bloco["texto"] is None:
            continue
        n = len(cues) + 1
        inicio, fim = bloco["inicioMs"] + CONTAGEM_MS, bloco["fimMs"] + CONTAGEM_MS
        cues.append(f"{n}\n{tempo(inicio)} --> {tempo(fim)}\n{_rotulo(bloco['quemOriginal'], bloco['tipo'])}: {bloco['texto']}\n")
    return "\n".join(cues)


def construir_runtime(plano: dict) -> dict:
    """Barreira explícita: não há adaptador de ações completas da série ainda.

    Alterar pronto em um JSON não cria modelo/roupa, marca, moto, telefone,
    coreografia ou câmera. A exportação só poderá existir junto com adaptadores
    reais e validação das evidências. Este método não escreve cenas substitutas.
    """
    pendencias = [r["codigo"] for r in plano.get("requisitos", [])
                  if r.get("estado") != "testado" or r.get("confirmadoNoJogo") is not True or not r.get("recursoVerificado")]
    motivo = "; ".join(pendencias) if pendencias else "adaptador de atuação integral/catálogo/capacidades ainda não implementado"
    raise ErroPreparacao(f"{plano.get('id', 'Plano')}: exportação runtime bloqueada: {motivo}. Não gerar atores parados ou posições inventadas.")


def _json(dados: dict) -> bytes:
    return (json.dumps(dados, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def arquivos_producao(serie: dict) -> dict[str, bytes]:
    """Gera bytes determinísticos; nenhuma data/UUID aparece nos planos."""
    arquivos = {}
    planos = serie["planos"]
    for plano in planos:
        pasta = f"EP{plano['episodio']:02d}" if plano["episodio"] else "extras"
        arquivos[f"{pasta}/{plano['id']}.plano.json"] = _json(plano)
        arquivos[f"{pasta}/{plano['id']}.base.srt"] = gerar_srt(plano).encode("utf-8")
    for episodio in serie["episodios"]:
        arquivos[f"EP{episodio['episodio']:02d}/manifesto.json"] = _json(episodio)
    indice = {k: v for k, v in serie.items() if k not in ("planos", "episodios")}
    indice["tipoDocumento"] = "indice_pre_producao"
    indice["episodios"] = [{"numero": e["episodio"], "nome": e["nomeOriginal"],
                            "manifesto": f"EP{e['episodio']:02d}/manifesto.json", "cenas": e["ordemCenas"]} for e in serie["episodios"]]
    indice["extras"] = [{"id": "VINHETA_01", "plano": "extras/VINHETA_01.plano.json", "srtBase": "extras/VINHETA_01.base.srt"}]
    indice["contagem"] = {"episodios": 15, "cenasEpisodios": 94, "vinhetas": 1, "planos": 95,
                          "comDialogo": sum(any(b["tipo"] == "fala" for b in p["roteiro"]) for p in planos),
                          "semDialogo": sum(p["episodio"] > 0 and not any(b["tipo"] == "fala" for b in p["roteiro"]) for p in planos)}
    indice["arquivosDerivados"] = [{"arquivo": caminho, "sha256": sha256(dados)} for caminho, dados in sorted(arquivos.items())]
    arquivos["indice.json"] = _json(indice)
    csv_texto = io.StringIO(newline="")
    writer = csv.writer(csv_texto)
    writer.writerow(["id", "episodio", "nome", "linhaInicio", "linhaFim", "localOriginal", "elencoOriginal", "elencoEfetivo", "cameraOriginal",
                     "estimativaOriginalMs", "blocosDeclaradosMs", "conteudoPlanejadoMs", "capturaPlanejadaMs", "fimSrtMs", "falas", "pausas",
                     "reaproveitamentos", "conflitos", "requisitosPendentes", "estado", "pronto", "gravado"])
    for p in planos:
        t = p["tempos"]
        writer.writerow([p["id"], p["episodio"], p["nome"], p["origem"]["linhaInicio"], p["origem"]["linhaFim"],
                         p["original"]["local"], p["original"]["elenco"], ";".join(p["adaptacao"]["elencoEfetivo"]), p["original"]["camera"],
                         t["estimativaOriginalMs"], t["blocosDeclaradosMs"], t["conteudoPlanejadoMs"], t["capturaPlanejadaMs"], t["fimUltimaLegendaMs"],
                         sum(b["tipo"] == "fala" for b in p["roteiro"]), sum(b["tipo"] == "pausa" for b in p["roteiro"]),
                         ";".join(r["cena"] for r in p["reaproveitamentos"]), ";".join(c["codigo"] for c in p["conflitos"]),
                         ";".join(r["codigo"] for r in p["requisitos"]), "pendente", "false", "false"])
    arquivos["relatorio_producao.csv"] = csv_texto.getvalue().encode("utf-8-sig")
    checklist = ["# Pré-produção — A Caloteira do Oeste", "", "Gerado do roteiro original; nenhum plano equivale a cena executável ou vídeo gravado.", "",
                 f"Origem: `{serie['fontes']['roteiro']['arquivo']}`; SHA256 `{serie['fontes']['roteiro']['sha256']}`.",
                 f"Prompt prioritário: SHA256 `{serie['fontes']['promptPrioritario']['sha256']}`.", "",
                 "95 planos (94 cenas +1 vinheta), 15 manifestos e SRTs base. O SRT vazio da vinheta é intencional: créditos são montados no CapCut, sem inventar fala/tempo de crédito.", "",
                 "São pastas de planejamento no workspace. As pastas de MP4 do runtime só são criadas quando a primeira cena real do episódio for carregada.", "",
                 "EP01_C08: narração6s +diálogo24s =30s; contagem3s; última legenda33s. A estimativa28s foi preservada separadamente.", "",
                 "Pendências do piloto: visuais DJANIRA_POBRE/DINJER aprovados, marca real ROCA_GRAPESEED, moto/rota/desembarque, andar de passarela, celular/Pix, atuação integral, câmera e captura real.", "",
                 "Ações originais estão integrais em cada plano; classificações de famílias/recursos são derivadas, sem IDs, roupas, clipes ou posições inventadas.", "",
                 "## Famílias a implementar e confirmar", ""]
    checklist.extend(f"- [ ] {nome}: {descricao}" for nome, descricao in FAMILIAS.items())
    checklist += ["", "## Divergências preservadas", ""] + [f"- {x}" for x in serie["conflitosGerais"]]
    checklist += ["", "## Ordem original dos episódios e cenas", "", "| Cena | Recursos pendentes | Tempo conteúdo/captura | Estado |", "|---|---|---|---|"]
    checklist.extend(f"| {p['id']} | {len(p['requisitos'])} | {p['tempos']['conteudoPlanejadoMs']/1000:g}s / {p['tempos']['capturaPlanejadaMs']/1000:g}s | pendente; não gravado |" for p in planos)
    checklist += ["", "## Exportação executável", "", "`construir_runtime` bloqueia a exportação. Mudar uma etiqueta para pronto não implementa ações. Nenhum arquivo deste diretório deve ser copiado para cenas como se fosse runtime JSON.", "",
                  "O índice fornece IDs/ordem/caminhos e hashes para leitura do C#. Montagem GTA exige outro contrato validado e recursos completos; `runtime` permanece nulo.", ""]
    arquivos["CHECKLIST_PRODUCAO.md"] = "\n".join(checklist).encode("utf-8")
    return arquivos


def publicar_planos(arquivos: dict[str, bytes], destino: Path, raiz: Path = RAIZ) -> dict:
    """Escreve apenas a área de produção; backup SHA antes de qualquer alteração.

    Não remove arquivos extras do usuário e não toca cenas, marcas, SRTs de tomadas
    ou MP4 do runtime. Conteúdo idêntico não é regravado.
    """
    raiz = Path(raiz).resolve()
    destino = Path(destino).absolute()
    esperado = raiz / "producao" / "temporada"
    if destino != esperado:
        raise ErroPreparacao(f"Destino permitido somente {esperado}.")
    alvos = {}
    for rel, dados in arquivos.items():
        relativo = Path(rel)
        if relativo.is_absolute() or ".." in relativo.parts:
            raise ErroPreparacao("Caminho derivado inseguro.")
        alvo = destino / relativo
        for ancestral in [alvo, *alvo.parents]:
            if ancestral == raiz.parent:
                break
            if ancestral.is_symlink() or (hasattr(ancestral, "is_junction") and ancestral.is_junction()):
                raise ErroPreparacao(f"Destino com redirecionamento: {ancestral}.")
        if alvo.resolve().is_relative_to(esperado) is False:
            raise ErroPreparacao("Caminho derivado fora de produção.")
        if alvo.exists() and not alvo.is_file():
            raise ErroPreparacao(f"Destino não é arquivo regular: {alvo}.")
        if not alvo.exists() or alvo.read_bytes() != dados:
            alvos[alvo] = dados
    existentes = [p for p in alvos if p.exists()]
    manifesto = {"modo": "planos_pre_producao", "fontesDerivadas": len(arquivos), "alterados": len(alvos), "backups": []}
    if existentes:
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-serie-" + uuid.uuid4().hex[:8]
        pasta_backup = raiz / "backups" / "projeto" / stamp
        for alvo in existentes:
            rel = alvo.relative_to(raiz)
            copia = pasta_backup / rel
            copia.parent.mkdir(parents=True, exist_ok=True)
            old = sha256(alvo.read_bytes())
            shutil.copy2(alvo, copia)
            if sha256(copia.read_bytes()) != old:
                raise ErroPreparacao(f"Backup não conferiu: {alvo}.")
            manifesto["backups"].append({"arquivo": str(rel), "backup": str(copia.relative_to(raiz)), "sha256": old})
        (pasta_backup / "manifesto.json").write_bytes(_json(manifesto))
    for alvo, dados in alvos.items():
        alvo.parent.mkdir(parents=True, exist_ok=True)
        temporario = alvo.with_name(alvo.name + ".tmp-" + uuid.uuid4().hex)
        try:
            temporario.write_bytes(dados)
            os.replace(temporario, alvo)
        finally:
            if temporario.exists():
                temporario.unlink()
        if sha256(alvo.read_bytes()) != sha256(dados):
            raise ErroPreparacao(f"Arquivo gravado não conferiu: {alvo}.")
    return manifesto


def main() -> int:
    parser = argparse.ArgumentParser(description="Preparar planos fiéis dos15 episódios; não exporta cenas GTA incompletas.")
    parser.add_argument("--conferir", action="store_true", help="conferir origens/planos sem escrever")
    args = parser.parse_args()
    try:
        serie = analisar_roteiro(RAIZ / ROTEIRO, RAIZ / PROMPT)
        arquivos = arquivos_producao(serie)
        if args.conferir:
            destino = RAIZ / "producao" / "temporada"
            diferentes = [nome for nome, dados in arquivos.items() if not (destino / nome).is_file() or (destino / nome).read_bytes() != dados]
            if diferentes:
                raise ErroPreparacao(f"{len(diferentes)} derivados ausentes/diferentes. Execute sem --conferir para gerar com backup prévio.")
            print("95 planos/15 manifestos/SRTs base conferidos com as fontes. Runtime e gravação pendentes.")
        else:
            resultado = publicar_planos(arquivos, RAIZ / "producao" / "temporada")
            print(f"95 planos e15 manifestos preparados; {resultado['alterados']} arquivos derivados gravados. Piloto:30s +3s, legenda até33s. Nenhuma cena GTA ou gravação confirmada.")
        return 0
    except (ErroPreparacao, OSError) as exc:
        print("Erro de preparação: " + str(exc), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
