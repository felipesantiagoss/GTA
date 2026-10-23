"""Preparar EP01_C08 fiel à origem, sem fabricar recursos nem substituir edições.

Rascunhos ficam no workspace. Publicação é exclusiva no runtime do perfil e só
adiciona arquivos após validar XMLs reais, continuidade facial e marca capturada.
Streaming, chão, condução e pega do telefone são conferências posteriores no GTA.
"""
from __future__ import annotations

import argparse
import csv
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation
import hashlib
import io
import json
import os
from pathlib import Path
import re
import sys
import tempfile
import uuid

from importar_visuais import ler_visual, mesma_identidade_djanira
from preparar_serie import ELENCO, PROMPT, ROTEIRO, VEICULOS_POR_CENA, analisar_roteiro
from runtime_diretor import ErroRuntime, exigir_sem_vinculos
from validar_cena import ErroCena, ler_xml
from validar_json import _catalogo, validar_cena_json
from validar_fila import validar_fila

RAIZ = Path(__file__).resolve().parents[1]
SHA_ROTEIRO = "E8FBF15EAAFBC8F0D74CBF39E9B9CBD30F8E9226B94749047762B6745274F36C"
ID = "EP01_C08"
PREFIXO_MS, SUFIXO_MS, CONTAGEM_MS = 12000, 4000, 3000


class ErroPiloto(ValueError):
    """Recurso ou publicação não comprovados; nenhuma simulação é permitida."""


def _hash(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest().upper()


def _json(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def _confinado(path: Path, root: Path) -> Path:
    path, root = exigir_sem_vinculos(path), exigir_sem_vinculos(root)
    if not path.is_relative_to(root) or any(p.casefold().startswith("onedrive") for p in path.parts):
        raise ErroPiloto(f"Caminho fora da pasta permitida ou no OneDrive: {path}.")
    return path


def _serie(raiz: Path) -> dict:
    raiz = exigir_sem_vinculos(raiz)
    source = _confinado(raiz / ROTEIRO, raiz)
    if _hash(source.read_bytes()) != SHA_ROTEIRO:
        raise ErroPiloto("SHA256 do roteiro original mudou; revisar a origem antes de preparar o piloto.")
    return analisar_roteiro(source, _confinado(raiz / PROMPT, raiz))


def _plano(raiz: Path) -> dict:
    plano = next(p for p in _serie(raiz)["planos"] if p["id"] == ID)
    saved = _confinado(raiz / "producao/temporada/EP01/EP01_C08.plano.json", raiz)
    registro = json.loads(saved.read_text(encoding="utf-8-sig"))
    for field in ("id", "original", "origem", "roteiro", "acoesOriginais"):
        if registro.get(field) != plano[field]:
            raise ErroPiloto(f"Plano EP01_C08 diverge do roteiro no campo {field}; preservar a origem.")
    if len(plano["roteiro"]) != 6 or sum(x["duracaoMs"] for x in plano["roteiro"]) != 30000:
        raise ErroPiloto("EP01_C08 precisa manter os seis blocos originais e seus30s.")
    return plano


def montar_rascunho(raiz: Path = RAIZ) -> dict:
    """Documento de cena reutilizável; existência não significa recursos prontos."""
    plano = _plano(Path(raiz))
    trechos = [{"tipo": "pausa", "duracaoSegundos": 12, "plano": "LONGE", "personagem": "DINJER"}]
    for original in plano["roteiro"]:
        trecho = {"tipo": original["tipo"], "texto": original["texto"],
                  "duracaoSegundos": original["duracaoMs"] // 1000}
        if original["tipo"] == "fala":
            trecho.update(personagem="DJANIRA_POBRE" if original["quemOriginal"] == "DJANIRA" else original["quemOriginal"],
                          gesto=original["gestoOriginal"], plano="MEDIA")
        elif original["tipo"] == "narracao":
            trecho.update(narrador="Narrador", plano="DOIS")
        else:
            raise ErroPiloto("Tipo original inesperado no piloto.")
        trechos.append(trecho)
    trechos.append({"tipo": "pausa", "duracaoSegundos": 4, "plano": "DOIS", "personagem": "DJANIRA_POBRE"})
    return {"versao": 1, "id": ID, "episodio": 1, "numero": 8, "nome": plano["nome"],
            "encenacao": "pedido_v1",
            "ambiente": {"local": plano["original"]["local"], "hora": plano["original"]["hora"],
                         "clima": plano["original"]["clima"], "raioLimpeza": 40, "distanciaFrente": 4},
            "elenco": [{"personagem": "DJANIRA_POBRE", "marca": {"direita": -1.1, "frente": 0, "altura": 0}, "olharPara": "DINJER"},
                       {"personagem": "DINJER", "marca": {"direita": 1.1, "frente": 0, "altura": 0}, "olharPara": "DJANIRA_POBRE"}],
            "camera": {"modo": "automatico", "planoInicial": "LONGE", "personagem": "DINJER",
                       "planoFala": "MEDIA", "planoNarracao": "DOIS", "planoFinal": "DOIS",
                       "inverterEixo": False, "transicaoSuave": False},
            "roteiro": trechos, "acoes": []}


def _fila() -> dict:
    return {"versao": 1, "episodio": 1, "nome": "Piloto: O PEDIDO", "cenas": [ID + ".json"]}


def _tempo(ms: int) -> str:
    return f"{ms // 3600000:02d}:{ms // 60000 % 60:02d}:{ms // 1000 % 60:02d},{ms % 1000:03d}"


def gerar_srt_piloto(cena: dict) -> str:
    from preparar_serie import _rotulo
    cursor, cues = CONTAGEM_MS, []
    for trecho in cena["roteiro"]:
        duration = int(Decimal(str(trecho["duracaoSegundos"])) * 1000)
        if trecho["tipo"] != "pausa":
            code = trecho.get("personagem", "")
            if code.startswith("DJANIRA_"):
                code = "DJANIRA"
            who = trecho.get("narrador", "Narrador") if trecho["tipo"] == "narracao" else _rotulo(code, "fala")
            cues.append(f"{len(cues) + 1}\n{_tempo(cursor)} --> {_tempo(cursor + duration)}\n{who}: {trecho['texto']}\n")
        cursor += duration
    return "\n".join(cues)


def _manifesto(raiz: Path, cena: dict) -> dict:
    plano = _plano(raiz)
    cursor, voz = PREFIXO_MS, []
    for bloco in plano["roteiro"]:
        voz.append({"tipo": bloco["tipo"], "quemOriginal": bloco["quemOriginal"], "texto": bloco["texto"],
                    "linhaOriginal": bloco["linhaInicio"], "inicioConteudoMs": cursor,
                    "fimConteudoMs": cursor + bloco["duracaoMs"], "inicioF7Ms": cursor + CONTAGEM_MS,
                    "fimF7Ms": cursor + bloco["duracaoMs"] + CONTAGEM_MS, "boca": bloco["boca"]})
        cursor += bloco["duracaoMs"]
    return {"versao": 1, "tipoDocumento": "rascunho_piloto", "id": ID,
            "estado": "pendente_recursos_reais_e_teste_no_jogo", "pronto": False, "gravado": False,
            "origem": plano["origem"], "original": plano["original"], "acoesOriginais": plano["acoesOriginais"],
            "cenaRascunhoSHA256": _hash(_json(cena)), "encenacao": "pedido_v1", "vozOriginal": voz,
            "tempos": {"prefixoSemVozMs": 12000, "vozOriginalMs": 30000, "sufixoSemVozMs": 4000,
                       "conteudoMs": 46000, "contagemMs": 3000, "capturaDesdeF7Ms": 49000,
                       "inicioPrimeiraLegendaF7Ms": 15000, "fimUltimaLegendaF7Ms": 45000},
            "adaptacoesExplicitas": ["12s sem voz para chegada de moto, descida e caminhada antes da narração.",
                                    "4s sem voz depois do último diálogo para Djanira usar o celular/Pix.",
                                    "Marcas relativas de conversa -1.1/+1.1m; coordenadas globais vêm somente de F3.",
                                    "Estimativa original28s não comprime os30s de voz; hora17:00/clima limpo preservados.",
                                    "Narração exibida como Narrador; quemOriginal=DJANIRA é preservado para dublagem. Boca só nas cinco falas. Pix é atuação; não há transferência financeira real."],
            "backend": {"fonte": "src/PilotoPedidoDiretor.cs", "verificacaoNoJogo": "F6: modelo, streaming, animações, chão e prontidão; F7: chegada real e descida sem warp.",
                        "moto": "faggio", "celular": "prop_npc_phone_02", "andar": "move_f@posh@",
                        "animacaoCelular": "cellphone@female/cellphone_text_read_base",
                        "estadoVisual": "pendente; os identificadores possuem origem técnica, não comprovam o resultado visual"},
            "publicacao": {"cena": "cenas/EP01_C08.json", "fila": "cenas/fila_piloto.json",
                           "politica": "Somente arquivos ausentes ou idênticos; preservar JSON editado, marcas, SRT e tomadas."},
            "extras": "Nenhum figurante/fala novo; as pausas são encenação explícita fora dos seis blocos originais."}


FAMILIAS = {
    "sentar": (r"sentad|sentar|senta\b|sentam\b|ajoelh|jantando|jantar|à mesa", "Capturar apoio/marca real; selecionar cenário ou clipe verificado; transições físicas, olhar/boca e reset da pose. Jantar exige confirmar a pose descrita na mesa, sem presumir um clipe."),
    "deitar": (r"deitad|deitar|dormindo|dorme\b|sono|cama|cai no chão|no chão", "Pose deitada/sono/queda com suporte real, invencibilidade, expressão e retorno seguro; pesquisar clipe antes de implementar."),
    "voar": (r"avião|avi[oõ]es|decola|voa|queda do avião", "Avião próprio, assentos, piloto/rota aérea, câmera e queda coreografada/invencível; reutilizar a tomada original quando declarado."),
    "combate": (r"briga|empurr|bate nela|soco|chute|corta.*garganta|garganta|mat[aá]-|esgana", "Animações coreografadas verificadas e reações temporizadas; nenhuma IA de combate real; respeitar duração após a fala."),
    "telefone": (r"celular|telefone|Pix|tira.*foto|bloquead", "Telefone próprio/mão/objeto real, clipe e olhar compatíveis; split-screen e conteúdo da tela ficam na edição."),
    "veiculo": (r"moto|ônibus|avião|carro|caminh[oõ]es|veículo", "Modelo/assentos/portas reais; embarque, condução, parada e desembarque sem teleportar durante a tomada; limpar só recursos próprios."),
    "objetos": (r"envelope|papel|bilhete|sacola|sapatos na mão|bandeja|champanhe|remédio|capinand|limpand|porta|portão", "Prop/modelo e osso verificados, transferência entre mãos, colisão e visibilidade; marcas/suportes reais; nenhuma escolha de índice inventada."),
    "movimento": (r"anda|caminh|corre|sai|entra|desce|levanta|caindo|cai |mergul|água|rampa", "Trajetos relativos à marca real e estilos carregados; chegada comprovada, erro em timeout, restauração física sem refazer atores."),
    "atuacao": (r"abraç|abraça|chora|dança|olha|r[ií]e|sorr|pose|machucad|ajoelh", "Clip/expressão/olhar compatíveis com os modelos e poses, boca somente no diálogo; evidência visual por variante."),
    "camera_edicao": (r"câmera|camera|cartela|montagem|take|flashback|reaproveit|CapCut|plano|zoom", "Presets/alvos/hold declarados, eixo e marcas de câmera; cortes/montagem/cartelas/reuso exato no CapCut, sem fabricar duração."),
}


def relatorio_capacidades(raiz: Path = RAIZ) -> dict:
    serie = _serie(Path(raiz))
    grupos = {k: {"mapeamentoProposto": v[1], "cenas": []} for k, v in FAMILIAS.items()}
    grupos["sem_match_especifico"] = {"mapeamentoProposto": "Revisar as ações/indicações originais; ausência de match não comprova capacidade implementada.", "cenas": []}
    cenas = []
    for plano in serie["planos"]:
        origem = [{"linha": a["linha"], "textoOriginal": a["raw"], "tipo": a["tipo"]} for a in plano["acoesOriginais"]]
        indicacoes = [{"linha": n["linha"], "textoOriginal": n["raw"]} for n in plano["notasOriginais"]]
        material = plano["nome"] + "\n" + "\n".join(x["textoOriginal"] for x in origem + indicacoes)
        familias = [nome for nome, (pattern, _) in FAMILIAS.items() if re.search(pattern, material, re.I)]
        if plano["id"] in VEICULOS_POR_CENA and "veiculo" not in familias:
            familias.append("veiculo")
        if not familias:
            familias = ["sem_match_especifico"]
        item = {"id": plano["id"], "episodio": plano["episodio"], "numero": plano["numero"],
                "nome": plano["nome"], "origem": {k: plano["origem"][k] for k in ("arquivo", "sha256Arquivo", "linhaInicio", "linhaFim")},
                "familias": familias, "acoesOriginais": origem, "indicacoesOriginais": indicacoes,
                "veiculosDeclarados": VEICULOS_POR_CENA.get(plano["id"], []),
                "estado": "mapeamento_proposto;atuacao_integral_nao_comprovada", "runtimeEmitido": False}
        cenas.append(item)
        for familia in familias:
            grupos[familia]["cenas"].append(item)
    return {"versao": 1, "tipoDocumento": "inventario_capacidades", "fonte": serie["fontes"]["roteiro"],
            "contagemPlanos": len(cenas), "cenasEpisodios": 94, "vinhetas": 1,
            "criterio": "Classificação derivada das ações/notas/títulos originais e referências explícitas de veículos; pode ter sobreposição. Jantar sugere apoio/pose a conferir, não um clipe comprovado. Os95 planos estão listados, inclusive sem match. Mapeamento não é implementação/teste.",
            "grupos": grupos, "cenas": cenas}


def arquivos_rascunho(raiz: Path = RAIZ) -> dict[str, bytes]:
    cena, report = montar_rascunho(raiz), relatorio_capacidades(raiz)
    csv_stream = io.StringIO(newline="")
    writer = csv.writer(csv_stream)
    writer.writerow(("ID", "Familias", "LinhaOriginal", "Tipo", "TextoOriginal", "Estado"))
    lines = ["# Capacidades das95 cenas/planos", "", "Inventário derivado; não declara ações implementadas ou teste no GTA. Texto original preservado no JSON/CSV.", ""]
    for item in report["cenas"]:
        for acao in item["acoesOriginais"]:
            writer.writerow((item["id"], ";".join(item["familias"]), acao["linha"], acao["tipo"], acao["textoOriginal"], item["estado"]))
    for family, group in report["grupos"].items():
        lines += [f"## {family} ({len(group['cenas'])} planos)", "", group["mapeamentoProposto"], ""]
        for item in group["cenas"]:
            lines.append(f"- **{item['id']}**: " + " ".join(f"Linha{x['linha']}: {x['textoOriginal']}" for x in item["acoesOriginais"]))
        lines.append("")
    lines += ["## EP01_C08", "", "pedido_v1: chegada12s, voz original30s, celular4s; conteúdo46s/contagem3s/captura49s. Hora17:00/clima limpo. Cena rascunho pendente de marcas/aparências reais e teste visual; condução/descida/celular serão conferidos em F6/F7.", ""]
    return {ID + ".json": _json(cena), "fila_piloto.json": _json(_fila()),
            "manifesto.json": _json(_manifesto(raiz, cena)), ID + ".base.srt": gerar_srt_piloto(cena).encode("utf-8"),
            "capacidades_95.json": _json(report), "capacidades_95.csv": csv_stream.getvalue().encode("utf-8"),
            "CAPACIDADES_95.md": ("\n".join(lines)).encode("utf-8")}


def _adicionar(path: Path, raw: bytes, root: Path) -> None:
    path = _confinado(path, root)
    path.parent.mkdir(parents=True, exist_ok=True)
    _confinado(path.parent, root)
    temp = None
    try:
        with tempfile.NamedTemporaryFile(mode="wb", prefix=".piloto-", suffix=".tmp", dir=path.parent, delete=False) as stream:
            temp = Path(stream.name)
            stream.write(raw)
            stream.flush()
            os.fsync(stream.fileno())
        if _hash(temp.read_bytes()) != _hash(raw):
            raise ErroPiloto("Hash do temporário não confere.")
        _confinado(path, root)
        if os.name == "nt":
            os.rename(temp, path)  # Windows recusa um destino existente.
        else:
            os.link(temp, path)  # Criação exclusiva também nos testes POSIX.
            temp.unlink()
        temp = None
        if _hash(path.read_bytes()) != _hash(raw):
            raise ErroPiloto(f"Hash publicado não confere: {path}.")
    finally:
        if temp is not None:
            _confinado(temp, root).unlink(missing_ok=True)


def _preparar_destinos(files: dict[str, bytes], root: Path) -> tuple[list[tuple[Path, bytes]], list[str]]:
    novos, iguais = [], []
    for name, raw in files.items():
        path = _confinado(root / name, root)
        if path.exists():
            if not path.is_file() or path.read_bytes() != raw:
                raise ErroPiloto(f"Arquivo existente/editado preservado; publicação abortada: {path}.")
            iguais.append(name)
        else:
            novos.append((path, raw))
    return novos, iguais


def preparar_rascunho(raiz: Path = RAIZ) -> dict:
    raiz = exigir_sem_vinculos(raiz)
    root = _confinado(raiz / "producao/piloto", raiz)
    files = arquivos_rascunho(raiz)
    novos, iguais = _preparar_destinos(files, root)
    for path, raw in novos:
        _adicionar(path, raw, root)
    return {"estado": "rascunho_pendente", "pasta": str(root), "adicionados": len(novos), "identicos": iguais,
            "cenaSHA256": _hash(files[ID + ".json"]), "pronto": False, "gravado": False}


def _dados(dados: Path | None, perfil: Path | None = None) -> Path:
    if perfil is None:
        value = os.environ.get("USERPROFILE")
        if not value:
            raise ErroPiloto("USERPROFILE ausente; não presumir um perfil.")
        perfil = Path(value)
    perfil = exigir_sem_vinculos(perfil)
    expected = _confinado(perfil / "GTA_Diretor", perfil)
    dados = exigir_sem_vinculos(dados if dados is not None else expected)
    if dados != expected or not dados.is_dir():
        raise ErroPiloto("Dados devem ser o runtime existente %USERPROFILE%/GTA_Diretor, fora do OneDrive.")
    return dados


def _marca_roca(catalogos: Path) -> dict:
    path = _confinado(catalogos / "locais_marcados.xml", catalogos)
    if path.stat().st_size > 1024 * 1024:
        raise ErroPiloto("Catálogo de marcas excede1MiB.")
    root = ler_xml(path)
    if root.tag != "LocaisMarcados" or root.attrib != {"versao": "1"} or (root.text or "").strip():
        raise ErroPiloto("Catálogo de marcas exige LocaisMarcados versao=1 sem namespace/conteúdo.")
    found, ids = None, set()
    for local in root:
        if local.tag != "Local" or set(local.attrib) != {"id", "x", "y", "z", "direcao", "dataUtc", "origem"} or len(local) or (local.text or "").strip() or (local.tail or "").strip():
            raise ErroPiloto("Marca possui campos/conteúdo inesperados.")
        code = local.attrib["id"]
        if re.fullmatch(r"[A-Z][A-Z0-9_]{0,47}", code) is None or code in ids or local.attrib["origem"] != "capturado_no_jogo":
            raise ErroPiloto("Marca duplicada/inválida ou sem origem capturado_no_jogo.")
        ids.add(code)
        try:
            for attr in ("x", "y", "z", "direcao"):
                value = Decimal(local.attrib[attr])
                if not value.is_finite() or (attr == "direcao" and not 0 <= value <= 360) or (attr != "direcao" and abs(value) > 20000):
                    raise ValueError("número fora do limite")
            date = local.attrib["dataUtc"]
            if not date.endswith(("Z", "+00:00")) or datetime.fromisoformat(date.replace("Z", "+00:00")).utcoffset().total_seconds() != 0:
                raise ValueError("data não UTC")
        except (InvalidOperation, ValueError, AttributeError) as error:
            raise ErroPiloto(f"Marca {code} contém número/data inválidos: {error}.") from error
        if code == "ROCA_GRAPESEED":
            found = {"id": code, "origem": local.attrib["origem"], "dataUtc": local.attrib["dataUtc"]}
    if found is None:
        raise ErroPiloto("ROCA_GRAPESEED não foi marcada no jogo; não publicar coordenadas presumidas.")
    return found


def validar_recursos(dados: Path | None = None, *, perfil: Path | None = None, raiz: Path = RAIZ) -> dict:
    """Conferência offline; não chama GTA, não ativa catálogo nem cria aparência."""
    dados = _dados(dados, perfil)
    catalogos = _confinado(dados / "catalogos", dados)
    paths = [_confinado(catalogos / name, dados) for name in ("personagens.json", "locais.json", "animacoes.json", "gestos_fala.xml", "locais_marcados.xml")]
    snapshots = {str(p.relative_to(dados)): _hash(p.read_bytes()) for p in paths}
    personagens = _catalogo(catalogos, "personagens.json", "personagens")
    visuais, fingerprints = {}, {}
    for code in ELENCO:
        entry = personagens.get(code)
        if not entry or entry.get("pronto") is not True:
            raise ErroPiloto(f"Elenco real ainda pendente: {code}; não tornar pronto por edição de etiqueta.")
        file, digest, model = entry.get("visualArquivo"), entry.get("visualSHA256"), entry.get("modelo")
        if file != code + ".xml" or not isinstance(digest, str) or re.fullmatch(r"[A-Fa-f0-9]{64}", digest) is None or not isinstance(model, str):
            raise ErroPiloto(f"{code} exige modelo/visualArquivo/SHA256 reais.")
        if code in ELENCO[:3] and model != "mp_f_freemode_01":
            raise ErroPiloto(f"{code} exige o modelo feminino freemode do contrato.")
        path = _confinado(catalogos / "visuais" / file, dados)
        visual = ler_visual(path, expected_model=model)
        if visual["sha256"] != digest.upper():
            raise ErroPiloto(f"SHA256 do visual {code} difere do catálogo.")
        visuais[code], fingerprints[code] = visual, visual["faceSHA256"]
        snapshots[path.relative_to(dados).as_posix()] = visual["sha256"]
    if not mesma_identidade_djanira(visuais["DJANIRA_POBRE"], visuais["DJANIRA_RICA"]):
        raise ErroPiloto("Djanira pobre/rica não conservam face, máscara e overlays; apenas roupa/cabelo podem mudar.")
    for code, name in (("DJANIRA_POBRE", "Djanira"), ("DINJER", "Dinjer")):
        if personagens[code].get("nome") != name:
            raise ErroPiloto(f"Nome original {name} deve ser preservado no catálogo.")
    marca = _marca_roca(catalogos)
    # O validador público confere todos os campos, pares canônicos e ambas as saídas.
    temp_parent = _confinado(Path(raiz) / "backups/validacao-piloto", Path(raiz))
    temp_parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="verificar-", dir=temp_parent) as temp:
        stage = Path(temp)
        (stage / (ID + ".json")).write_bytes(_json(montar_rascunho(raiz)))
        (stage / "fila_piloto.json").write_bytes(_json(_fila()))
        validar_cena_json(stage / (ID + ".json"), catalogos)
        validar_fila(stage / "fila_piloto.json", stage, catalogos)
    return {"estado": "recursos_offline_conferidos;teste_visual_pendente", "dados": str(dados),
            "marca": marca, "faceSHA256Djanira": fingerprints["DJANIRA_POBRE"],
            "visuaisSHA256": {c: visuais[c]["sha256"] for c in ELENCO}, "arquivosSHA256": snapshots,
            "nativos": "Disponibilidade, chão, streaming e execução são conferidos pelo backend pedido_v1 em F6/F7; não provados por Python.",
            "testeNoJogo": False, "gravado": False}


def publicar_piloto(dados: Path | None = None, *, perfil: Path | None = None, raiz: Path = RAIZ) -> dict:
    dados = _dados(dados, perfil)
    recursos = validar_recursos(dados, perfil=perfil, raiz=raiz)
    files = {"cenas/EP01_C08.json": _json(montar_rascunho(raiz)), "cenas/fila_piloto.json": _json(_fila())}
    novos, iguais = _preparar_destinos(files, dados)  # Todos os conflitos antes de qualquer publicação.
    for name, expected in recursos["arquivosSHA256"].items():
        if _hash(_confinado(dados / name, dados).read_bytes()) != expected:
            raise ErroPiloto(f"Recurso mudou durante a validação: {name}; repita sem sobrescrever.")
    if not novos:
        return {"estado": "ja_publicado;teste_visual_pendente", "adicionados": [], "identicos": iguais, "recursos": recursos}
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-" + uuid.uuid4().hex[:8]
    backup = _confinado(dados / "backups/piloto" / stamp, dados)
    manifest = {"versao": 1, "modo": "adicionar_sem_sobrescrever", "estado": "preparado", "recursos": recursos,
                "arquivos": [{"arquivo": path.relative_to(dados).as_posix(), "sha256": _hash(raw), "anterior": None} for path, raw in novos],
                "preservadosIdenticos": iguais, "backupsDeSubstituidos": [], "observacao": "Nenhum arquivo existente será alterado; manifesto novo, testes no GTA pendentes."}
    _adicionar(backup / "manifesto.json", _json(manifest), dados)
    completed = []
    for path, raw in novos:
        _adicionar(path, raw, dados)
        completed.append(path.relative_to(dados).as_posix())
    for name, raw in files.items():
        if _hash(_confinado(dados / name, dados).read_bytes()) != _hash(raw):
            raise ErroPiloto(f"Conferência final falhou: {name}; preserve o manifesto e os arquivos.")
    _adicionar(backup / "conclusao.json", _json({"estado": "publicado;teste_visual_pendente", "arquivos": manifest["arquivos"]}), dados)
    return {"estado": "publicado;teste_visual_pendente", "adicionados": completed, "identicos": iguais,
            "manifesto": str(backup / "manifesto.json"), "recursos": recursos}


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Preparar EP01_C08 com origem preservada; nenhuma execução do GTA.")
    parser.add_argument("--rascunho", action="store_true", help="gerar somente producao/piloto no workspace, com estado pendente")
    parser.add_argument("--dados", type=Path, help="runtime exato do perfil; padrão USERPROFILE/GTA_Diretor")
    args = parser.parse_args(argv)
    if args.rascunho and args.dados is not None:
        parser.error("--rascunho e --dados não podem ser combinados")
    try:
        result = preparar_rascunho() if args.rascunho else publicar_piloto(args.dados)
        print(f"{result['estado']}: EP01_C08; conteúdo46s + contagem3s. Confirmação visual pendente.")
        if "pasta" in result:
            print(result["pasta"])
        return 0
    except (ErroPiloto, ErroRuntime, ErroCena, ValueError, OSError) as error:
        print(f"Não foi possível preparar o piloto: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
