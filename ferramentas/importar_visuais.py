"""Importar aparência real Menyoo; nunca criar roupas, rostos ou aprovação visual."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import re
import struct
import sys
import uuid
import xml.etree.ElementTree as ET
from xml.parsers import expat

from runtime_diretor import ErroRuntime, exigir_sem_vinculos


RAIZ = Path(__file__).resolve().parents[1]
MENYOO = Path("C:/Program Files (x86)/Steam/steamapps/common/Grand Theft Auto V/menyooStuff")
ORIGEM = MENYOO / "Outfit"
PED_LIST = MENYOO / "PedList.xml"
DESTINO = RAIZ / "catalogos" / "visuais"
CODIGOS = ("DJANIRA_POBRE", "DJANIRA_RICA", "DINJER", "DONA_IRACI", "O_CHEFE",
           "CAPANGA_1", "CAPANGA_2", "CAPANGA_3", "CAPANGA_4",
           "COLEGA_1", "COLEGA_2", "COLEGA_3", "ATENDENTE")
PAIS = ("ShapeFatherId", "ShapeMotherId", "ShapeOverrideId", "ToneFatherId", "ToneMotherId", "ToneOverrideId")
MISTURAS = ("ShapeVal", "ToneVal", "OverrideVal")
MAX_BYTES = 1024 * 1024
MAX_CHARS = 100000
MANIFESTO = "importacao_visuais.json"


class ErroVisual(ValueError):
    """Erro que conserva os arquivos originais e a aprovação pendente."""


def _erro(campo, texto):
    raise ErroVisual(f"{campo}: {texto}.")


def _hash(raw):
    return hashlib.sha256(raw).hexdigest().upper()


def _seguro(caminho):
    try:
        return exigir_sem_vinculos(Path(caminho))
    except ErroRuntime as erro:
        raise ErroVisual(str(erro)) from erro


def _confinado(caminho):
    caminho = _seguro(caminho)
    raiz = _seguro(RAIZ)
    if not caminho.is_relative_to(raiz) or caminho == raiz:
        _erro("destino", f"deve ficar dentro do workspace {raiz}, sem substituí-lo")
    return caminho


def _ler(caminho):
    caminho = _seguro(caminho)
    try:
        with caminho.open("rb") as arquivo:
            raw = arquivo.read(MAX_BYTES + 1)
    except OSError as erro:
        _erro(str(caminho), f"não foi possível ler o arquivo: {erro}")
    if len(raw) > MAX_BYTES:
        _erro(str(caminho), "arquivo excede 1 MiB")
    return raw


def _xml(raw):
    if len(raw) > MAX_BYTES:
        _erro("visual", "XML excede 1 MiB")
    parser = expat.ParserCreate()
    profundidade = caracteres = 0

    def proibido(*_):
        _erro("visual", "DTD e entidades externas são proibidos")

    def inicio(*_):
        nonlocal profundidade
        profundidade += 1
        if profundidade > 32:
            _erro("visual", "XML excede 32 níveis")

    def fim(*_):
        nonlocal profundidade
        profundidade -= 1

    def texto(conteudo):
        nonlocal caracteres
        caracteres += len(conteudo)
        if caracteres > MAX_CHARS:
            _erro("visual", "XML excede 100000 caracteres de conteúdo")

    parser.StartDoctypeDeclHandler = proibido
    parser.ExternalEntityRefHandler = proibido
    parser.StartElementHandler = inicio
    parser.EndElementHandler = fim
    parser.CharacterDataHandler = texto
    try:
        parser.Parse(raw, True)  # Respeita a declaração ISO-8859-1/UTF-8 do Menyoo.
        # Contar o documento completo, não só os valores, também em UTF-16.
        encoding = "utf-8-sig"
        if raw.startswith((b"\xff\xfe", b"\xfe\xff")):
            encoding = "utf-16"
        else:
            declaration = re.match(br'\s*<\?xml[^>]*encoding\s*=\s*[\'"]([^\'"]+)', raw)
            if declaration:
                encoding = declaration[1].decode("ascii")
        if len(raw.decode(encoding)) > MAX_CHARS:
            _erro("visual", "XML excede 100000 caracteres")
        return ET.fromstring(raw)
    except (expat.ExpatError, ET.ParseError, UnicodeError, LookupError) as erro:
        _erro("visual", f"XML inválido: {erro}")


def _filhos(node, campo, exigidos=None):
    if node.text and node.text.strip():
        _erro(campo, "texto solto não permitido")
    filhos = {}
    for child in node:
        if not isinstance(child.tag, str) or "{" in child.tag or ":" in child.tag:
            _erro(campo, "namespace não permitido")
        if child.tag in filhos:
            _erro(campo + "." + child.tag, "campo duplicado")
        if child.tail and child.tail.strip():
            _erro(campo, "texto solto não permitido")
        filhos[child.tag] = child
    if exigidos is not None:
        obrigatorios = set(exigidos)
        if set(filhos) != obrigatorios:
            _erro(campo, "campos ausentes/desconhecidos: " + ", ".join(sorted(set(filhos) ^ obrigatorios)))
    return filhos


def _sem_atributos(node, campo):
    if node.attrib:
        _erro(campo, "atributos desconhecidos")


def _um(filhos, nome, campo):
    if nome not in filhos:
        _erro(campo + "." + nome, "campo obrigatório ausente")
    return filhos[nome]


def _escalar(node, campo):
    _sem_atributos(node, campo)
    if list(node):
        _erro(campo, "esperado valor sem subcampos")
    return (node.text or "").strip()


def _int(valor, campo, minimo, maximo):
    if len(valor) > 11 or not re.fullmatch(r"-?[0-9]+", valor):
        _erro(campo, f"esperado inteiro entre {minimo} e {maximo}")
    numero = int(valor)
    if not minimo <= numero <= maximo:
        _erro(campo, f"esperado inteiro entre {minimo} e {maximo}")
    return numero


def _real(valor, campo, minimo, maximo):
    if not re.fullmatch(r"-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?", valor):
        _erro(campo, f"esperado número finito entre {minimo} e {maximo}")
    numero = float(valor)
    if not math.isfinite(numero) or not minimo <= numero <= maximo:
        _erro(campo, f"esperado número finito entre {minimo} e {maximo}")
    return struct.unpack("<f", struct.pack("<f", numero))[0]


def hash_modelo(nome):
    """Jenkins do identificador ASCII conferido; nenhum hash escolhido à mão."""
    if not isinstance(nome, str) or not re.fullmatch(r"[a-z0-9_]{1,64}", nome):
        _erro("visual.modelo", "identificador inválido")
    value = 0
    for letter in nome.encode("ascii"):
        value = (value + letter) & 0xFFFFFFFF
        value = (value + (value << 10)) & 0xFFFFFFFF
        value ^= value >> 6
    value = (value + (value << 3)) & 0xFFFFFFFF
    value ^= value >> 11
    return (value + (value << 15)) & 0xFFFFFFFF


def _model_hash(root):
    text = _escalar(_um(root, "ModelHash", "visual"), "visual.ModelHash")
    if re.fullmatch(r"0[xX][0-9A-Fa-f]{1,8}", text):
        return int(text, 16)
    return _int(text, "visual.ModelHash", 0, 0xFFFFFFFF)


def _pares(node, campo, quantidade, acessorios=False):
    _sem_atributos(node, campo)
    filhos = _filhos(node, campo, (f"_{i}" for i in range(quantidade)))
    resultado = []
    for i in range(quantidade):
        field = f"{campo}._{i}"
        partes = _escalar(filhos[f"_{i}"], field).split(",")
        if len(partes) != 2:
            _erro(field, "use o par drawable,textura original do Menyoo")
        minimo = -1 if acessorios else 0
        pair = [_int(partes[0].strip(), field + ".drawable", minimo, 10000),
                _int(partes[1].strip(), field + ".textura", minimo, 10000)]
        if acessorios and (pair[0] == -1 and pair[1] not in (-1, 0) or pair[0] >= 0 and pair[1] < 0):
            _erro(field, "acessório ausente exige -1,-1 ou -1,0; presente exige textura não negativa")
        resultado.append(pair)
    return resultado


def _cabeca(node):
    campo = "visual.PedProperties.HeadFeatures"
    if node.attrib != {"WasInArray": "true"}:
        _erro(campo, "exige WasInArray=true; ajuste/salve Head Features no Menyoo")
    filhos = _filhos(node, campo, ("ShapeAndSkinTone", "HairColour", "HairColourStreaks", "EyeColour", "FacialFeatures", "Overlays"))
    blend = filhos["ShapeAndSkinTone"]
    _sem_atributos(blend, campo + ".ShapeAndSkinTone")
    dna = _filhos(blend, campo + ".ShapeAndSkinTone", (*PAIS, *MISTURAS, "IsP"))
    parents = [_int(_escalar(dna[key], campo + "." + key), campo + "." + key, 0, 45) for key in PAIS]
    mixes = [_real(_escalar(dna[key], campo + "." + key), campo + "." + key, 0, 1) for key in MISTURAS]
    flag = _escalar(dna["IsP"], campo + ".IsP")
    is_parent = flag == "true" or (flag != "false" and _int(flag, campo + ".IsP", 0, 1) == 1)
    cores = [_int(_escalar(filhos[key], campo + "." + key), campo + "." + key, 0, maximum)
             for key, maximum in (("HairColour", 255), ("HairColourStreaks", 255), ("EyeColour", 32))]
    features = filhos["FacialFeatures"]
    _sem_atributos(features, campo + ".FacialFeatures")
    itens = _filhos(features, campo + ".FacialFeatures", (f"_{i}" for i in range(20)))
    features = [_real(_escalar(itens[f"_{i}"], campo + f".FacialFeatures._{i}"), campo + f".FacialFeatures._{i}", -1, 1) for i in range(20)]
    overlays = filhos["Overlays"]
    _sem_atributos(overlays, campo + ".Overlays")
    itens = _filhos(overlays, campo + ".Overlays", (f"_{i}" for i in range(13)))
    overlays = []
    for i in range(13):
        item = itens[f"_{i}"]
        field = campo + f".Overlays._{i}"
        if set(item.attrib) != {"index", "colour", "colourSecondary", "opacity"} or list(item) or item.text:
            _erro(field, "exige somente index/colour/colourSecondary/opacity, sem conteúdo")
        overlays.append([_int(item.attrib["index"], field + ".index", 0, 255),
                         _int(item.attrib["colour"], field + ".colour", -1, 255),
                         _int(item.attrib["colourSecondary"], field + ".colourSecondary", -1, 255),
                         _real(item.attrib["opacity"], field + ".opacity", 0, 1)])
    return {"pais": parents, "misturas": mixes, "isParent": is_parent, "cabelo": cores[0],
            "mechas": cores[1], "olhos": cores[2], "tracos": features, "overlays": overlays}


def fingerprint_rosto(dados):
    lines = ["VisualDiretor.Face.v1", dados["modelo"], ",".join(map(str, dados["componentes"][0]))]
    head = dados["rosto"]
    if head is not None:
        def hex_float(valor):
            return struct.pack("<f", 0.0 if valor == 0 else valor).hex().upper()
        lines.extend((",".join(map(str, head["pais"])), ",".join(map(hex_float, head["misturas"])),
                      "1" if head["isParent"] else "0", ",".join(map(hex_float, head["tracos"])), str(head["olhos"])))
    return _hash(("\n".join(lines) + "\n").encode("utf-8"))


def mesma_identidade_djanira(primeira, segunda):
    """DNA, traços, olhos, máscara e pele fixos; produção4/5/8 pode variar."""
    if primeira["rosto"] is None or segunda["rosto"] is None:
        return False
    return (primeira["faceSHA256"] == segunda["faceSHA256"]
            and primeira["componentes"][1] == segunda["componentes"][1]
            and all(a == b for i, (a, b) in enumerate(zip(primeira["rosto"]["overlays"],
                                                        segunda["rosto"]["overlays"]))
                    if i not in (4, 5, 8)))


def validar_visual_bytes(raw, expected_model):
    hash_modelo(expected_model)
    root = _xml(raw)
    if root.tag != "OutfitPedData":
        _erro("visual", "esperado OutfitPedData sem namespace")
    _sem_atributos(root, "visual")
    nodes = _filhos(root, "visual")
    _int(_escalar(_um(nodes, "Type", "visual"), "visual.Type"), "visual.Type", 1, 1)
    model_hash = _model_hash(nodes)
    if model_hash != hash_modelo(expected_model):
        _erro("visual.ModelHash", f"o modelo difere de {expected_model}")
    ped = _um(nodes, "PedProperties", "visual")
    _sem_atributos(ped, "visual.PedProperties")
    ped = _filhos(ped, "visual.PedProperties")
    components = _pares(_um(ped, "PedComps", "visual.PedProperties"), "visual.PedProperties.PedComps", 12)
    props = _pares(_um(ped, "PedProps", "visual.PedProperties"), "visual.PedProperties.PedProps", 10, True)
    freemode = expected_model in ("mp_f_freemode_01", "mp_m_freemode_01")
    if freemode and "HeadFeatures" not in ped:
        _erro("visual.PedProperties.HeadFeatures", "freemode exige o rosto completo salvo no Menyoo")
    # XML legado pode conter um bloco genérico: em NPCs o rosto é do próprio modelo.
    # Nenhum dado de HeadFeatures é aplicado nesses modelos.
    dados = {"modelo": expected_model, "modelHash": model_hash, "sha256": _hash(raw), "freemode": freemode,
             "componentes": components, "acessorios": props, "rosto": _cabeca(ped["HeadFeatures"]) if freemode else None}
    dados["faceSHA256"] = fingerprint_rosto(dados)
    return dados


def ler_visual(caminho, expected_model="mp_f_freemode_01"):
    return validar_visual_bytes(_ler(caminho), expected_model)


def _modelos(ped_list):
    root = _xml(_ler(ped_list))
    if root.tag != "PedList":
        _erro("PedList", "raiz diferente de PedList")
    models = {}
    for category in root:
        if category.tag != "Category" or category.get("name") == "Animal":
            continue
        for ped in category:
            name = ped.get("name", "")
            if ped.tag == "Ped" and re.fullmatch(r"[a-z0-9_]{1,64}", name) and not name.startswith("a_c_"):
                models.setdefault(hash_modelo(name), set()).add(name)
    return models


def _ler_codigo(raw, codigo, models):
    if codigo in CODIGOS[:3]:
        return validar_visual_bytes(raw, "mp_f_freemode_01")
    nodes = _filhos(_xml(raw), "visual")
    candidates = models.get(_model_hash(nodes), set())
    if len(candidates) != 1:
        _erro(codigo + ".ModelHash", "modelo humano não encontrado unicamente no PedList.xml local")
    return validar_visual_bytes(raw, next(iter(candidates)))


def _destino_status():
    perfil = os.environ.get("USERPROFILE")
    if perfil:
        perfil = _seguro(perfil)
        if perfil.is_dir():
            if any(part.casefold().startswith("onedrive") for part in perfil.parts):
                _erro("perfil", "os visuais do runtime ficam fora do OneDrive")
            return _seguro(perfil / "GTA_Diretor" / "catalogos" / "visuais")
    return _seguro(DESTINO)


def _catalogo_status(destino):
    caminho = _seguro(destino.parent / "personagens.json")
    if not caminho.is_file():
        return {}
    try:
        dados = json.loads(_ler(caminho).decode("utf-8-sig"), object_pairs_hook=_json_pares,
                           parse_constant=lambda value: _erro("personagens", f"número proibido: {value}"))
    except (UnicodeError, json.JSONDecodeError) as erro:
        _erro("personagens", f"catálogo inválido: {erro}")
    if not isinstance(dados, dict) or type(dados.get("versao")) is not int or dados["versao"] != 1 or not isinstance(dados.get("personagens"), list):
        _erro("personagens", "esperado catálogo versão1 com lista personagens")
    result, seen = {}, set()
    for entrada in dados["personagens"]:
        if not isinstance(entrada, dict) or not isinstance(entrada.get("codigo"), str):
            _erro("personagens", "entrada sem código de personagem")
        codigo = entrada["codigo"]
        if codigo in seen:
            _erro("personagens." + codigo, "código duplicado")
        seen.add(codigo)
        if codigo not in CODIGOS:
            continue
        arquivo, sha = entrada.get("visualArquivo"), entrada.get("visualSHA256")
        if arquivo is None and sha is None:
            continue
        if arquivo != codigo + ".xml" or not isinstance(sha, str) or not re.fullmatch(r"[A-F0-9]{64}", sha):
            _erro("personagens." + codigo, "visualArquivo/visualSHA256 exige nome canônico e hash uppercase completo")
        modelo = entrada.get("modelo")
        hash_modelo(modelo)
        result[codigo] = (modelo, sha)
    return result


def status_visuais(origem=ORIGEM, destino=None, ped_list=PED_LIST):
    # Somente leitura pode examinar o runtime. Importação mantém sua escrita confinada.
    origem, destino = _seguro(origem), _seguro(_destino_status() if destino is None else destino)
    expected = _catalogo_status(destino)
    entries, loaded = [], {}
    models = None
    for codigo in CODIGOS:
        path = _seguro(origem / (codigo + ".xml"))
        local = _seguro(destino / (codigo + ".xml"))
        escolhido = local if local.is_file() else path
        item = {"codigo": codigo, "arquivoMenyoo": str(path), "arquivoVisual": str(escolhido),
                "presente": escolhido.is_file(), "aprovado": False}
        if not item["presente"]:
            item["estado"] = "pendente: nenhum XML real importado ou gerado no jogo"
        else:
            try:
                if codigo not in CODIGOS[:3] and models is None:
                    models = _modelos(ped_list)
                visual = _ler_codigo(_ler(escolhido), codigo, models or {})
                if codigo in expected:
                    modelo, sha = expected[codigo]
                    if visual["modelo"] != modelo or visual["sha256"] != sha:
                        _erro(codigo, "o modelo/hash do XML difere do catálogo canônico")
                loaded[codigo] = visual
                item.update(modelo=visual["modelo"], sha256=visual["sha256"], faceSHA256=visual["faceSHA256"],
                            hashCatalogoConferido=codigo in expected, estado="arquivo válido; teste visual pendente")
            except ErroVisual as erro:
                item.update(estado="arquivo inválido", erro=str(erro))
        item["importado"] = False
        item["registrado"] = False
        if codigo in loaded and local.is_file():
            item["registrado"] = _hash(_ler(local)) == loaded[codigo]["sha256"]
            item["importado"] = path.is_file() and item["registrado"]
        entries.append(item)
    same = None
    if "DJANIRA_POBRE" in loaded and "DJANIRA_RICA" in loaded:
        same = mesma_identidade_djanira(loaded["DJANIRA_POBRE"], loaded["DJANIRA_RICA"])
    return {"versao": 1, "destino": str(destino), "personagens": entries, "identidadeDjaniraConferida": same,
            "essenciais": list(CODIGOS[:3]), "aprovacaoVisual": "pendente"}


def _json_pares(pares):
    result = {}
    for key, value in pares:
        if key in result:
            _erro("manifesto." + key, "campo duplicado")
        result[key] = value
    return result


def _manifesto_anterior(path):
    if not path.exists():
        return {}
    try:
        data = json.loads(_ler(path).decode("utf-8-sig"), object_pairs_hook=_json_pares)
    except (UnicodeError, json.JSONDecodeError) as erro:
        _erro("manifesto", f"JSON inválido: {erro}")
    if not isinstance(data, dict) or type(data.get("versao")) is not int or data["versao"] != 1 or not isinstance(data.get("personagens"), list):
        _erro("manifesto", "versão/lista de personagens inválidas")
    result = {}
    for item in data["personagens"]:
        if not isinstance(item, dict) or item.get("codigo") not in CODIGOS or item["codigo"] in result:
            _erro("manifesto.personagens", "código inválido ou duplicado")
        result[item["codigo"]] = item
    return result


def _escrever(path, raw):
    path = _confinado(path)
    temp = _confinado(path.with_name(path.name + ".tmp-" + uuid.uuid4().hex))
    try:
        with temp.open("xb") as stream:
            stream.write(raw)
            stream.flush()
            os.fsync(stream.fileno())
        if _hash(_ler(temp)) != _hash(raw):
            _erro(str(temp), "SHA256 do temporário divergiu")
        os.replace(temp, path)
        if _hash(_ler(path)) != _hash(raw):
            _erro(str(path), "SHA256 do arquivo salvo divergiu")
    finally:
        # Apagar apenas o temporário exclusivo deste chamado, após conferir confinamento.
        if temp.exists():
            _confinado(temp).unlink()


def importar_visuais(origem=ORIGEM, destino=DESTINO, ped_list=PED_LIST):
    origem, destino = _seguro(origem), _confinado(destino)
    if destino == origem or origem.is_relative_to(destino):
        _erro("destino", "separe a pasta importada dos originais do Menyoo")
    visuals, sources, raw_by_code = {}, {}, {}
    models = None
    # Conferir todos os arquivos presentes e a identidade antes de criar/mudar qualquer saída.
    for codigo in CODIGOS:
        path = _seguro(origem / (codigo + ".xml"))
        if not path.is_file():
            continue
        if codigo not in CODIGOS[:3] and models is None:
            models = _modelos(ped_list)
        raw = _ler(path)
        visuals[codigo] = _ler_codigo(raw, codigo, models or {})
        sources[codigo], raw_by_code[codigo] = path, raw
    if not visuals:
        _erro("visuais", "nenhum XML real encontrado para importar; a geração automática ocorre no jogo")
    pair = {}
    for codigo in CODIGOS[:2]:
        if codigo in visuals:
            pair[codigo] = visuals[codigo]
        elif _seguro(destino / (codigo + ".xml")).is_file():
            pair[codigo] = ler_visual(destino / (codigo + ".xml"))
    same = len(pair) == 2
    if same and not mesma_identidade_djanira(pair[CODIGOS[0]], pair[CODIGOS[1]]):
        _erro("DJANIRA_RICA", "o rosto difere de DJANIRA_POBRE; conserve DNA, traços, olhos, máscara e overlays da pele; somente maquiagem4/5/8 pode diferir")
    manifest_path = _confinado(destino / MANIFESTO)
    old_entries = _manifesto_anterior(manifest_path)
    outputs = {destino / (code + ".xml"): raw for code, raw in raw_by_code.items()}
    stamp = datetime.now(timezone.utc).isoformat()
    for code, visual in visuals.items():
        old_entries[code] = {"codigo": code, "modelo": visual["modelo"], "visualArquivo": code + ".xml",
                             "visualSHA256": visual["sha256"], "faceSHA256": visual["faceSHA256"], "aprovado": False,
                             "estado": "importado; aparência ainda não aprovada", "origem": str(sources[code]), "dataUtc": stamp}
    manifest = {"versao": 1, "dataUtc": stamp, "personagens": [old_entries[code] for code in CODIGOS if code in old_entries],
                "identidadeDjaniraConferida": same, "aprovacaoVisual": "pendente"}
    outputs[manifest_path] = (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    old_bytes = {path: _ler(path) for path in outputs if _seguro(path).is_file()}
    for code, path in sources.items():
        if _hash(_ler(path)) != visuals[code]["sha256"]:
            _erro(str(path), "o original mudou durante a preparação; repita a importação")
    backup = None
    if old_bytes:
        backup = _confinado(RAIZ / "backups" / "visuais" / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-" + uuid.uuid4().hex))
        backup.mkdir(parents=True, exist_ok=False)
        entries = []
        for path, raw in old_bytes.items():
            saved = _confinado(backup / path.name)
            with saved.open("xb") as stream:
                stream.write(raw)
            if _hash(_ler(saved)) != _hash(raw):
                _erro(str(saved), "backup SHA256 divergiu")
            entries.append({"arquivo": str(path), "backup": str(saved), "sha256": _hash(raw)})
        with _confinado(backup / "manifesto.json").open("x", encoding="utf-8", newline="\n") as stream:
            json.dump({"versao": 1, "arquivos": entries}, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
    _confinado(destino).mkdir(parents=True, exist_ok=True)
    _confinado(destino)
    for path, raw in outputs.items():
        path = _confinado(path)
        if path in old_bytes:
            if _hash(_ler(path)) != _hash(old_bytes[path]):
                _erro(str(path), "o destino mudou após o backup; não substituir")
        elif path.exists():
            _erro(str(path), "um arquivo surgiu durante a importação; não substituir")
        _escrever(path, raw)
    return {"importados": list(visuals), "pendentes": [code for code in CODIGOS if code not in old_entries],
            "destino": str(destino), "backup": str(backup) if backup else None, "manifesto": manifest}


def main(argv=None):
    parser = argparse.ArgumentParser(description="Importar somente os visuais reais salvos no Menyoo, sem aprová-los.")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--status", action="store_true", help="somente conferir; padrão")
    group.add_argument("--importar", action="store_true", help="copiar os XML reais presentes, com backup/hash")
    parser.add_argument("--origem", type=Path, default=ORIGEM)
    parser.add_argument("--destino", type=Path, default=None,
                        help="status: runtime do perfil; importar: catalogos/visuais do workspace")
    parser.add_argument("--ped-list", type=Path, default=PED_LIST)
    args = parser.parse_args(argv)
    try:
        data = importar_visuais(args.origem, DESTINO if args.destino is None else args.destino, args.ped_list) if args.importar else status_visuais(args.origem, args.destino, args.ped_list)
        if args.importar:
            print("Importados: " + ", ".join(data["importados"]) + ". Aprovação visual permanece pendente.")
            print("Destino: " + data["destino"])
        else:
            print("Conferindo: " + data["destino"])
            for item in data["personagens"]:
                print(item["codigo"] + ": " + item["estado"] + ("; " + item["erro"] if "erro" in item else ""))
            if data["identidadeDjaniraConferida"] is False:
                print("DJANIRA: rostos diferentes; não importar antes de corrigir.")
            print("Essenciais primeiro: DJANIRA_POBRE, DJANIRA_RICA e DINJER. Geração automática ocorre no jogo; teste visual permanece pendente.")
        return 0
    except (ErroVisual, OSError) as erro:
        print("Erro: " + str(erro), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
