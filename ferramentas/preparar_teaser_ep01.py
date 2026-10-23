"""Duas cenas integrais do EP2 para as imagens do teaser do EP1.

Complemento seletivo: conserva planos, roteiro e guias já entregues. Não publica
dados/código no GTA, não cria MP4/áudio e não declara interiores acessíveis.
"""
from __future__ import annotations

import argparse
from copy import deepcopy
from datetime import datetime, timezone
from decimal import Decimal
import hashlib
import json
from pathlib import Path
import shutil
import sys
import tempfile
import xml.etree.ElementTree as ET

from preparar_cenas_serie import _base, _pose, _prop
from preparar_edicao import ler_planos, _srt
from preparar_piloto import RAIZ, ErroPiloto, _json, gerar_srt_piloto
from runtime_diretor import exigir_sem_vinculos, ler_pasta_dados
from validar_json import validar_cena_json

ACAO = {
    "EP02_C01": "**Ação:** Dinjer bebendo, dançando, tirando foto. Expressão `convencido`.",
    "EP02_C02": "**Ação:** Dinjer sentada numa mesa reservada com quatro homens de terno. Um deles entrega um envelope para ela. Ela guarda rápido.",
}
RESERVA_C01_MS = 4000


def _hash(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest().upper()


def _ler(path: Path):
    def pares(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ErroPiloto(f"{path}: chave duplicada {key}; arquivo preservado.")
            result[key] = value
        return result
    def constante(value):
        raise ErroPiloto(f"{path}: número não finito{value}; arquivo preservado.")
    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=pares,
                      parse_constant=constante)


def _attachment(ator, mao="direita"):
    return {"personagem": ator, "mao": mao,
            "offset": {"x": 0, "y": 0, "z": 0}, "rotacao": {"x": 0, "y": 0, "z": 0}}


def _clipe(ator, recurso, inicio, duracao, *, loop=False, sobre_pose=False, celular=False, manter_pose=False):
    item = {"tipo": "celular" if celular else "clipe", "personagem": ator,
            "inicioSegundos": inicio, "duracaoSegundos": duracao,
            "recurso": recurso, "loop": loop, "corpoInteiro": not (sobre_pose or celular),
            "manterPoseFinal": manter_pose}
    if sobre_pose:
        item["sobrePoseSentada"] = True
    return item


def construir_cenas(planos: dict) -> tuple[dict, dict]:
    """A origem literal protege ações/vozes; tempos físicos são declarados à parte."""
    for ident, duracao in (("EP02_C01", 8000), ("EP02_C02", 9000)):
        plano = planos[ident]
        if ([x["raw"] for x in plano["acoesOriginais"]] != [ACAO[ident]]
                or [(x["tipo"], x["duracaoMs"]) for x in plano["roteiro"]] != [("narracao", duracao)]
                or plano["original"]["locaisCodigos"] != ["BOATE_DENTRO"]):
            raise ErroPiloto(f"{ident}: ação, local ou voz original mudou; revisar o adaptador.")
    c1 = _base(planos["EP02_C01"])
    c1.update(encenacao="serie_v1")
    c1["camera"] = {"modo": "programado", "planoInicial": "MEDIA", "personagem": "DINJER",
        "planoFinal": "PERTO", "cortes": [
            {"inicioSegundos": 0, "plano": "MEDIA", "personagem": "DINJER"},
            {"inicioSegundos": 4, "plano": "MEDIA", "personagem": "DINJER"},
            {"inicioSegundos": 8, "plano": "PERTO", "personagem": "DINJER"}]}
    c1["roteiro"][0].pop("plano", None)
    c1["roteiro"].append({"tipo": "pausa", "duracaoSegundos": RESERVA_C01_MS / 1000})
    c1["direcao"] = [_clipe("DINJER", "beber_gole_f", 0, 4, manter_pose=True),
        _clipe("DINJER", "danca_dinjer_candidata", 4, 4, loop=True),
        _clipe("DINJER", "foto_celular_f", 8, 4, loop=True, celular=True)]
    garrafa = _prop("BEBIDA_DINJER", "garrafa_cerveja", 0, 0)
    garrafa.update(assentarNoChao=False, attachmentInicial=_attachment("DINJER"),
        eventos=[{"tipo": "soltar", "inicioSegundos": 4}])
    c1["objetos"] = [garrafa]
    c1["interpretacao"] = [{"personagem": "DINJER", "inicioSegundos": 0,
        "duracaoSegundos": 12, "expressao": "convencido"}]

    c2 = _base(planos["EP02_C02"])
    c2.update(encenacao="serie_v1")
    # Os dois primeiros assentos são próximos: contato será medido nos ossos
    # reais durante a entrega. Distância candidata não falsifica contato.
    lugares = [("DINJER", -0.45, 0, -90), ("CAPANGA_1", 0.45, 0, 90),
        ("CAPANGA_2", -1.1, 2.2, -90), ("CAPANGA_3", 1.1, 2.2, 90),
        ("CAPANGA_4", 0, 2.9, 180)]
    for ator, (_, x, y, _) in zip(c2["elenco"], lugares):
        ator["marca"] = {"direita": x, "frente": y, "altura": 0}
        ator["olharPara"] = "CAPANGA_1" if ator["personagem"] == "DINJER" else "DINJER"
    c2["objetos"] = [_prop("MESA_RESERVADA", "mesa", 0, 1.1)]
    c2["poses"] = []
    for ator, x, y, rot in lugares:
        cadeira = "CADEIRA_" + ator
        c2["objetos"].append(_prop(cadeira, "cadeira_simples", x, y, rot))
        recurso = "sentada_cadeira_livre_f" if ator == "DINJER" else "sentado_cadeira_m"
        c2["poses"].append(_pose(ator, cadeira, recurso))
    envelope = _prop("ENVELOPE_DINJER", "envelope", 0.45, 0)
    envelope.update(assentarNoChao=False, attachmentInicial=_attachment("CAPANGA_1"),
        eventos=[{"tipo": "transferir", "inicioSegundos": 1, "esperaMaximaSegundos": 2,
                  **_attachment("DINJER")},
                 {"tipo": "guardar", "inicioSegundos": 4, "personagem": "DINJER",
                  "recurso": "guardar_envelope", "esperaMaximaSegundos": 2}])
    c2["objetos"].append(envelope)
    c2["direcao"] = [_clipe("CAPANGA_1", "entrega_a", 1, 3, sobre_pose=True),
        _clipe("DINJER", "entrega_b", 1, 3, sobre_pose=True),
        _clipe("DINJER", "guardar_envelope", 4, 3, sobre_pose=True)]
    c2["camera"] = {"modo": "programado", "planoInicial": "LONGE", "personagem": "DINJER",
        "planoFinal": "MEDIA", "cortes": [
            {"inicioSegundos": 0, "plano": "LONGE", "personagem": "DINJER"},
            {"inicioSegundos": 1, "plano": "MEDIA", "personagem": "DINJER"}]}
    c2["roteiro"][0].pop("plano", None)
    return {"EP02_C01": c1, "EP02_C02": c2}, construir_fora(planos["EP02_C01"])


def construir_fora(plano: dict) -> dict:
    nota = "⚠️ Se o interior não abrir, use `BOATE_FORA` com ela entrando pela porta, à noite, com neon."
    if [x["raw"] for x in plano["notasOriginais"]] != [nota]:
        raise ErroPiloto("EP02_C01: fallback literal mudou; não presumir autorização para fachada.")
    fora = _base(plano)
    fora.update(nome="A BALADA — entrada pela fachada", encenacao="serie_v1")
    fora["ambiente"].update(local="BOATE_FORA", distanciaFrente=1)
    fora["elenco"][0]["marca"] = {"direita": 0, "frente": -2, "altura": 0}
    fora["direcao"] = [{"tipo": "andar", "personagem": "DINJER", "inicioSegundos": 0,
        "duracaoSegundos": 8, "destino": {"direita": 0, "frente": 2, "altura": 0}, "velocidade": 0.7}]
    fora["interpretacao"] = [{"personagem": "DINJER", "inicioSegundos": 0,
        "duracaoSegundos": 8, "expressao": "convencido"}]
    fora["camera"] = {"modo": "automatico", "planoInicial": "MEDIA", "personagem": "DINJER",
        "planoNarracao": "MEDIA", "planoFinal": "PERTO"}
    fora["roteiro"][0]["plano"] = "MEDIA"
    return fora


def verificar_fidelidade(cena: dict, plano: dict):
    blocos = [x for x in cena["roteiro"] if x["tipo"] != "pausa"]
    originais = plano["roteiro"]
    if len(blocos) != len(originais):
        raise ErroPiloto(f"{cena['id']}: quantidade de vozes mudou.")
    for bloco, origem in zip(blocos, originais):
        if (bloco["tipo"] != origem["tipo"] or bloco.get("texto") != origem["texto"]
                or Decimal(str(bloco["duracaoSegundos"])) * 1000 != origem["duracaoMs"]):
            raise ErroPiloto(f"{cena['id']}: texto/tipo/duração original não preservado.")
    if [x["personagem"] for x in cena["elenco"]] != plano["adaptacao"]["elencoEfetivo"]:
        raise ErroPiloto(f"{cena['id']}: elenco original não preservado.")


def _validar_fontes(cenas: dict, fora: dict, catalogos: Path, raiz: Path):
    """Validar XMLs reais do perfil com catálogo técnico novo em cópia privada."""
    with tempfile.TemporaryDirectory(prefix="diretor-teaser-") as tmp:
        destino = Path(tmp)
        cat = destino / "catalogos"
        shutil.copytree(exigir_sem_vinculos(catalogos), cat)
        for nome, chave in (("corpos_serie.json", "animacoes"), ("objetos_serie.json", "objetos")):
            atual, novo = _ler(cat / nome), _ler(raiz / "catalogos" / nome)
            por_codigo = {x["codigo"]: x for x in atual[chave]}
            for item in novo[chave]:
                if item["codigo"] in por_codigo and por_codigo[item["codigo"]] != item:
                    raise ErroPiloto(f"Catálogo editado no perfil diverge: {nome}/{item['codigo']}; arquivo preservado.")
                if item["codigo"] not in por_codigo:
                    atual[chave].append(deepcopy(item))
            (cat / nome).write_bytes(_json(atual))
        for rotulo, cena in [*cenas.items(), ("EP02_C01_fora", fora)]:
            arquivo = destino / "cenas" / rotulo / (cena["id"] + ".json")
            arquivo.parent.mkdir(parents=True, exist_ok=True)
            arquivo.write_bytes(_json(cena))
            validar_cena_json(arquivo, cat)


def preparar_arquivos(raiz: Path = RAIZ) -> dict[str, bytes]:
    origem = ler_planos(raiz / "producao/temporada")
    planos = origem["planos"]
    cenas, fora = construir_cenas(planos)
    files, specs = {}, []
    for ident, cena in cenas.items():
        verificar_fidelidade(cena, planos[ident])
        path = f"producao/cenas_serie/EP02/{ident}.json"
        raw = _json(cena)
        files[path] = raw
        files[path.replace(".json", ".base.srt")] = gerar_srt_piloto(cena).encode("utf-8")
        doc = _ler(raiz / f"producao/cenas_serie/EP02/{ident}.execucao.json")
        if doc["origem"] != planos[ident]["origem"] or doc["roteiroOriginal"] != planos[ident]["roteiro"]:
            raise ErroPiloto(f"{ident}: descritor diverge da origem imutável.")
        total = sum(int(Decimal(str(x["duracaoSegundos"])) * 1000) for x in cena["roteiro"])
        pendencias = [x for x in doc["pendencias"]
                     if x["tipo"] != "atuacao_integral" and x["codigo"] != "teste:" + ident]
        pendencias += [{"codigo": "teste:" + ident, "tipo": "teste_no_jogo", "descricao":
            "Contato físico/compatibilidade dos objetos/clipes/câmera e acesso real BOATE_DENTRO precisam passar no GTA; não é confirmação de resultado visual."}]
        if ident == "EP02_C01":
            notas = ["Gole feminino real não-loop até4s e pose final conservada, dança4s e foto4s com telefone próprio; clipes/props existem na fonte Menyoo. Voz original8s sem boca seguida de reserva silenciosa explícita4s para terminar as ações. GET_ANIM_DURATION rejeita gole maior que4s; não é duração medida fora do jogo e reserva adicional será necessária se o clipe real exceder esse orçamento.",
                "Garrafa própria solta fisicamente ao terminar o gole, antes da dança; gravidade sem reposicionamento. Expressão convencido mantém o clipe facial real. Encaixe de garrafa/telefone e ação de beber/fotografar precisam ser vistos.",
                "A alternativa literal BOATE_FORA tem fonte separada do mesmoID, selecionável porT; entrada real de4m em8s, sem afirmar que a boate está aberta."]
        else:
            notas = ["Cinco atores sentados em cinco cadeiras próprias à mesa reservada; assentos medidos no GTA. Dinjer usa base feminina com braços livres e três gestos upper-body específicos; nenhuma tarefa remove a pose primária.",
                "Envelope próprio parte deCAPANGA_1, transferência é observada entre1–3s e só ocorre com gestos reais ativos e mão/objeto até0,35m. A/B continuam candidatos ao papel visual: fora de alcance ou clipe incompatível interrompe, não move ator/objeto para fingir entrega.",
                "Guardar exige o gesto real ativo e mão próxima do tronco em fase conferida pelo backend. Nunca usa ocultar sozinho para declarar que o envelope foi guardado. Voz original9s sem boca; duração real dos gestos deve caber no orçamento ou montagem falha."]
        doc.update(estado="rascunho_integral_pendente_recursos_e_teste", pronto=False, gravado=False,
            runtimeRascunho=ident + ".json", runtimeSHA256=_hash(raw), mapeamentoDeclarado=notas,
            pendencias=pendencias, objetosRuntime=deepcopy(cena.get("objetos", [])), posesRuntime=deepcopy(cena.get("poses", [])),
            temposRascunho={"conteudoMs": total, "contagemMs": 3000, "capturaDesdeF7Ms": total + 3000,
                "blocosOriginaisMs": planos[ident]["tempos"]["blocosDeclaradosMs"],
                "tempoSemVozAdicionalMs": total - planos[ident]["tempos"]["blocosDeclaradosMs"]})
        files[path.replace(".json", ".execucao.json")] = _json(doc)
        specs.append({"id": ident, "arquivo": path, "sha256": _hash(raw), "local": "BOATE_DENTRO",
            "conteudoMs": total, "contagemMs": 3000, "capturaDesdeF7Ms": total + 3000,
            "mp4Real": None, "audioReal": None, "offsetManualMp4ParaF7Ms": None})
    verificar_fidelidade(fora, planos["EP02_C01"])
    fallback = "producao/subtomadas/EP02_C01/fora/EP02_C01.json"
    files[fallback] = _json(fora)
    files[fallback.replace(".json", ".base.srt")] = gerar_srt_piloto(fora).encode("utf-8")
    files["producao/subtomadas/EP02_C01/manifesto.json"] = _json({"versao": 1, "id": "EP02_C01",
        "origem": planos["EP02_C01"]["origem"], "notaAlternativaLiteral": planos["EP02_C01"]["notasOriginais"],
        "alternativa": {"parte": "fora", "arquivo": fallback, "sha256": _hash(files[fallback]),
            "local": "BOATE_FORA", "conteudoMs": 8000, "capturaDesdeF7Ms": 11000},
        "pronto": False, "gravado": False, "requisitos": [
            "F3 no limiar real da porta da boate olhando para dentro, acesso realmente aberto, noite/neon vistos.",
            "Dinjer inicia2m fora e caminha4m através da passagem; chegada física<=0,40m, bloqueio/tempo insuficiente causa erro sem teleporte.",
            "T alterna dentro/fora no mesmoID fora da captura/ação; o log registra a variante. Não editar JSON do usuário nem forçar abertura de interior."]})

    selecao_path = "producao/cenas_serie/selecao.xml"
    xml = ET.fromstring((raiz / selecao_path).read_bytes())
    for ident, cena in cenas.items():
        entradas = [x for x in xml.findall("./Episodio/Cena") if x.attrib.get("id") == ident]
        if len(entradas) != 1:
            raise ErroPiloto(f"Seleção precisa conter um único{ident}.")
        entradas[0].set("arquivo", f"EP02/{ident}.json")
        entradas[0].set("sha256", _hash(_json(cena)))
    ET.indent(xml, space="  ")
    files[selecao_path] = ET.tostring(xml, encoding="utf-8", xml_declaration=True) + b"\n"
    indice_path = "producao/cenas_serie/indice.json"
    indice = _ler(raiz / indice_path)
    ordem = [x for e in indice["episodios"] for x in e["ordemOriginal"]] + ["VINHETA_01"]
    integrais = set(indice["rascunhosIntegrais"]) | set(cenas)
    indice["rascunhosIntegrais"] = [x for x in ordem if x in integrais]
    indice["pendentesAtuacao"] = [x for x in ordem if x not in integrais]
    recursos = []
    for item in indice["recursosFaltantes"]:
        copia = deepcopy(item)
        if copia["tipo"] == "atuacao_integral":
            copia["cenas"] = [x for x in copia["cenas"] if x not in cenas]
        if copia["cenas"]:
            recursos.append(copia)
    indice["recursosFaltantes"] = recursos
    por_path = {x["arquivo"]: x for x in indice["arquivos"]}
    prefixo = "producao/cenas_serie/"
    for path, raw in files.items():
        if path.startswith(prefixo) and path != indice_path:
            rel = path.removeprefix(prefixo)
            por_path[rel] = {"arquivo": rel, "sha256": _hash(raw)}
    indice["arquivos"] = [por_path[k] for k in sorted(por_path)]
    indice["complementos"] = sorted(set(indice.get("complementos", [])) | {"producao/edicao/EP01/runtime/teaser/manifesto.json"})
    files[indice_path] = _json(indice)
    manifesto = {"versao": 1, "tipoDocumento": "complemento_teaser_EP01", "origemRoteiro": origem["indice"]["fontes"]["roteiro"],
        "fontes": specs, "alternativaBOATE_FORA": {"arquivo": fallback, "sha256": _hash(files[fallback])},
        "estado": "execucao_implementada;teste_visual_e_captura_pendentes", "pronto": False, "gravado": False,
        "usoCapCut": {"duracaoTeaserOriginalMs": 5000, "imagens": ["EP02_C01", "EP02_C02"],
            "acoesLiteraisDoTeaser": ["Dinjer na boate bebendo.", "Dinjer sentada com quatro homens de terno."],
            "intervalosCandidatosConteudoMs": {"EP02_C01": [0, 4000], "EP02_C02": [0, 9000]},
            "instrucao": "Selecionar5s úteis: Dinjer bebendo e Dinjer sentada com os quatro homens. Retirar as contagens. As vozes originais8/9s pertencem às cenas integrais doEP2 e não devem ser truncadas para caber no teaser. O teaser não declara voz própria; anúncio original permanece no fechamentoEP01_C09. Nenhum áudio foi gerado.",
            "cortePorImagemMs": None, "sincronizarPorCtime": False},
        "limiteCompilerAnterior": "Complemento seletivo independente; planos/95descritores e guias anteriores fora dos dois IDs são preservados. O compilador anterior não inclui esta nova capacidade; não regenerar históricos sobre estas entradas.",
        "arquivos": [{"arquivo": p, "sha256": _hash(r)} for p, r in sorted(files.items())]}
    prefixo_edicao = "producao/edicao/EP01/runtime/teaser/"
    files[prefixo_edicao + "manifesto.json"] = _json(manifesto)
    for ident, cena in cenas.items():
        cue = {"inicioMs": 3000, "fimMs": 3000 + planos[ident]["roteiro"][0]["duracaoMs"],
            "legenda": "Narrador: " + cena["roteiro"][0]["texto"]}
        files[prefixo_edicao + ident + ".srt"] = _srt([cue])
    return files


def publicar_seletivo(files: dict[str, bytes], raiz: Path, conferir=False) -> int:
    alterados = [(exigir_sem_vinculos(raiz / name), raw) for name, raw in files.items()
        if not (raiz / name).is_file() or (raiz / name).read_bytes() != raw]
    if conferir:
        if alterados:
            raise ErroPiloto("Complemento ausente/divergente: " + ", ".join(str(p) for p, _ in alterados))
        return 0
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    backup = raiz / "backups/teaser-ep01" / stamp
    existentes = [(p, raw) for p, raw in alterados if p.is_file()]
    for path, _ in existentes:
        target = backup / path.relative_to(raiz)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
    for path, raw in alterados:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
    return len(alterados)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--conferir", action="store_true")
    parser.add_argument("--catalogos", type=Path)
    args = parser.parse_args(argv)
    try:
        planos = ler_planos()["planos"]
        cenas, fora = construir_cenas(planos)
        catalogos = args.catalogos or ler_pasta_dados(RAIZ) / "catalogos"
        _validar_fontes(cenas, fora, catalogos, RAIZ)
        files = preparar_arquivos()
        count = publicar_seletivo(files, RAIZ, args.conferir)
        print(f"Teaser: 2cenas integrais + fachada alternativa; {count} arquivos atualizados. Sem publicação no GTA/MP4/áudio; teste visual pendente.")
        return 0
    except (ValueError, OSError, KeyError) as exc:
        print(f"Teaser não preparado: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
