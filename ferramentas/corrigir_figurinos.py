"""Revisões autorizadas de figurinos existentes, sem sortear rostos/roupas.

Prepara arquivos a partir de um backup conferido e publica com journal e
rollback por hash. Os nomes das peças não comprovam a aparência no GTA.
"""
from __future__ import annotations

import argparse
from copy import deepcopy
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import uuid
import xml.etree.ElementTree as ET

from importar_visuais import CODIGOS, mesma_identidade_djanira, validar_visual_bytes
from runtime_diretor import exigir_sem_vinculos, ler_pasta_dados

RAIZ = Path(__file__).resolve().parents[1]
REVISAO = "figurinos-0.9.3"
ANTERIORES = {
    "DJANIRA_POBRE": "10B7A1BF8BFCCBE595B4170B294220510F92BD3A902B4E87CFE195D5DB2F6DBB",
    "DJANIRA_RICA": "9FF574E18710EA362021621E89956EE2A46373513CDA5B99E2664AF64D955CD5",
    "DINJER": "B60F65AED031AD025CB8031BAAB3DCE8BC160E19D0769645A628E47DFCF9BABF",
}
PECAS = {"DJANIRA_POBRE": {4: "14,0", 6: "5,0"}, "DINJER": {6: "6,0"}}
MANIFESTO = "catalogos/visuais/elenco-manifesto.xml"
CATALOGO = "catalogos/personagens.json"
REVISOES = {
    "djanira-rica-joias-0.9.6": {
        "anteriores": {"DJANIRA_RICA": "461A9F94D7C29D6F4497F6824E09EDCB771790094654D766A8F0A54902C3F5EF"},
        "pecas": {"DJANIRA_RICA": {3: "12,0", 4: "63,0", 7: "7,0", 11: "112,1"}},
        "props": {"DJANIRA_RICA": {2: "6,0", 7: "1,0"}},
        "sobrancelhas": False, "cabelo": {},
        "pedido": "Usuário rejeitou o vestido simples da Djanira rica; pediu roupa melhor e joias. Rever somente roupa/joias da rica, mantendo identidade, cabelo, salto e demais12visuais.",
        "descricao": {"DJANIRA_RICA": {"11=112,1": "Gold Embroidered Dress",
            "3=12,0;4=63,0;8=3,0": "Base corporal do preset DLC_MP_VAL2_F_OUTFIT_0; textura gold substitui cream na mesma geometria",
            "7=7,0": "Gold Necklace", "prop2=6,0": "Gold Diamond Drops", "prop7=1,0": "Gold Diamond Cuff",
            "6=6,0": "Black Patent Heels existentes preservados", "cabelo": "Long bob cobre e identidade existentes preservados"}}},
    REVISAO: {"anteriores": ANTERIORES, "pecas": PECAS, "sobrancelhas": True,
              "cabelo": {}, "pedido": "Djanira: sobrancelhas e pernas. Dinjer: somente salto. Rostos e demais peças preservados.",
              "descricao": {"DJANIRA_POBRE": {"4=14,0": "Blue-Gray Shorts", "6=5,0": "Gray Flip-Flops", "sobrancelha": "21; cor5; opacidade1"},
                            "DJANIRA_RICA": {"sobrancelha": "21; cor5; opacidade1"},
                            "DINJER": {"6=6,0": "Black Patent Heels"}}},
    "djanira-rica-0.9.4": {
        "anteriores": {"DJANIRA_RICA": "871ECE8226CFC5178A009194825DC2906F77D67839FF65949A60B8F7DD0EB8A6"},
        # Preset Rockstar DLC_MP_VWD_F_OUTFIT_HIGHROLL_0: braços/pernas,
        # camada interna e salto originais. Só a textura do mesmo vestido
        # muda de crimson7 para black2. Remover resíduos militares/decal.
        "pecas": {"DJANIRA_RICA": {2: "10,2", 3: "11,0", 4: "15,0", 6: "6,0",
                                    7: "0,0", 8: "3,0", 10: "0,0", 11: "322,2"}},
        "sobrancelhas": False,
        "cabelo": {"DJANIRA_RICA": {"HairColour": "24", "HairColourStreaks": "24"}},
        "pedido": "Usuário rejeitou Djanira rica e pediu visual bonito com salto; refazer roupas e cabelo, mantendo o mesmo rosto da pobre e os demais personagens.",
        "descricao": {"DJANIRA_RICA": {"11=322,2": "Black High Roller Dress",
                        "3=11,0;4=15,0;8=3,0": "Camadas corporais do preset original High Roller; componente4 é a base sob o vestido, não roupa visível final",
                        "6=6,0": "Black Patent Heels", "2=10,2": "Long Bob Auburn",
                        "corCabelo24": "RGB162,80,47/#A2502F; cobre natural é uma escolha estética",
                        "props2=13,0": "Brincos existentes preservados", "7=0,0;10=0,0": "Remover acessórios/decal anteriores"}}},
    "elenco-papeis-0.9.5": {
        "anteriores": {
            "DJANIRA_POBRE": "46DAA4020850DB749E8CCB61E43A8C69F8E703815E83433977E108EBA2C6D151",
            "DINJER": "04CB125A63D49D13AC67FA193BE78ABEE0898021B1ABE1806A2132B246F45C6E"},
        # Pobre: trio torso/undershirt/top do preset HEIST_F_OUTFIT_C_2.
        # Dinjer: blazer/White Shirt/braços do HEIST_F_OUTFIT_L_1;
        # textura navy conserva a geometria. Saia/scarf têm nomes reais,
        # mas a combinação completa continua dependendo da prévia no GTA.
        "pecas": {
            "DJANIRA_POBRE": {2: "4,2", 7: "0,0", 8: "3,0", 10: "0,0", 11: "0,11"},
            "DINJER": {2: "79,0", 3: "1,0", 4: "7,2", 7: "13,3", 8: "38,0", 10: "0,0", 11: "6,2"}},
        "sobrancelhas": False,
        "cabelo": {"DJANIRA_POBRE": {"HairColour": "24", "HairColourStreaks": "24"}},
        "pedido": "Retomada: terminar os13 figurinos com roupas coerentes com o papel; rever pobre e Dinjer sem alterar identidades ou demais personagens.",
        "descricao": {
            "DJANIRA_POBRE": {"2=4,2": "Ponytail Auburn", "corCabelo24": "RGB162,80,47/#A2502F; cobre igual à variante rica",
                "3=0,0;8=3,0;11=0,11": "Gray T-Shirt; trio de malhas do preset DLC_MP_HEIST_F_OUTFIT_C_2",
                "4=14,0;6=5,0": "Short e chinelo da correção0.9.3 preservados", "7=0,0;10=0,0": "Retirar acessórios/decal aleatórios"},
            "DINJER": {"2=79,0": "Afro; cor61/#020202 existente preservada", "3=1,0;8=38,0;11=6,2": "Navy Tux + White Shirt; trio de malhas do preset DLC_MP_HEIST_F_OUTFIT_L_1 com textura navy",
                "4=7,2": "Navy Pencil", "7=13,3": "Red Bow Scarf", "6=6,0": "Black Patent Heels da correção0.9.3 preservados", "10=0,0": "Retirar decal aleatório"}}},
}


def sha(raw):
    return hashlib.sha256(raw).hexdigest().upper()


def confinado(path, root):
    root, path = exigir_sem_vinculos(root), exigir_sem_vinculos(path)
    if not path.is_relative_to(root) or path == root:
        raise ValueError(f"Caminho fora da pasta permitida: {path}")
    return path


def substituir(raw, antigo, novo):
    if raw.count(antigo) != 1:
        raise ValueError("A revisão exige uma ocorrência exata do valor anterior.")
    return raw.replace(antigo, novo, 1)


def revisar_xml(code, raw, revisao=REVISAO):
    spec = REVISOES[revisao]
    if sha(raw) != spec["anteriores"][code]:
        raise ValueError(f"{code}: figurino diferente da versão autorizada; preservado.")
    before = ET.fromstring(raw)
    expected = deepcopy(before)
    for slot, pair in spec["pecas"].get(code, {}).items():
        path = f"PedProperties/PedComps/_{slot}"
        old = expected.find(path).text
        raw = substituir(raw, f"<_{slot}>{old}</_{slot}>".encode(),
                          f"<_{slot}>{pair}</_{slot}>".encode())
        expected.find(path).text = pair
    for slot, pair in spec.get("props", {}).get(code, {}).items():
        path = f"PedProperties/PedProps/_{slot}"
        old = expected.find(path).text
        raw = substituir(raw, f"<_{slot}>{old}</_{slot}>".encode(),
                          f"<_{slot}>{pair}</_{slot}>".encode())
        expected.find(path).text = pair
    for tag, value in spec["cabelo"].get(code, {}).items():
        node = expected.find("PedProperties/HeadFeatures/" + tag)
        raw = substituir(raw, f"<{tag}>{node.text}</{tag}>".encode(),
                          f"<{tag}>{value}</{tag}>".encode())
        node.text = value
    if spec["sobrancelhas"] and code.startswith("DJANIRA_"):
        path = "PedProperties/HeadFeatures/Overlays/_2"
        old = expected.find(path)
        # A cor explícita é igual nas duas variantes, independentemente do cabelo.
        needle = ET.tostring(old, encoding="utf-8").strip()
        new = deepcopy(old)
        new.set("colour", "5")
        new.set("colourSecondary", "5")
        raw = substituir(raw, needle, ET.tostring(new, encoding="utf-8").strip())
        old.set("colour", "5")
        old.set("colourSecondary", "5")
    if ET.tostring(expected) != ET.tostring(ET.fromstring(raw)):
        raise ValueError(f"{code}: a revisão alterou campos fora do pedido.")
    old_visual = validar_visual_bytes(ET.tostring(before), "mp_f_freemode_01")
    new_visual = validar_visual_bytes(raw, "mp_f_freemode_01")
    if old_visual["faceSHA256"] != new_visual["faceSHA256"]:
        raise ValueError(f"{code}: identidade facial alterada.")
    return raw


def verificar_conjunto(contents):
    manifest = ET.fromstring(contents[MANIFESTO])
    catalog = json.loads(contents[CATALOGO].decode("utf-8-sig"))
    entries = {v["codigo"]: v for v in catalog["personagens"]}
    if len(entries) != len(catalog["personagens"]):
        raise ValueError("Códigos duplicados no catálogo.")
    if len(manifest) != len(CODIGOS) or {v.get("codigo") for v in manifest} != set(CODIGOS):
        raise ValueError("Manifesto diferente dos13 visuais reais.")
    visuals = {}
    for node in manifest:
        code = node.get("codigo")
        if node.tag != "visual" or node.get("arquivo") != code + ".xml":
            raise ValueError("Manifesto tem entrada inesperada.")
        raw = contents[f"catalogos/visuais/{code}.xml"]
        visual = validar_visual_bytes(raw, node.get("modelo"))
        entry = entries[code]
        if (node.get("sha256") != sha(raw) or node.get("faceSHA256") != visual["faceSHA256"]
                or entry["visualSHA256"] != sha(raw) or entry["modelo"] != visual["modelo"]
                or entry["visualArquivo"] != code + ".xml"):
            raise ValueError(f"{code}: XML, manifesto e catálogo divergentes.")
        visuals[code] = visual
    if (not mesma_identidade_djanira(visuals["DJANIRA_POBRE"], visuals["DJANIRA_RICA"])
            or manifest.get("djaniraFaceSHA256") != visuals["DJANIRA_POBRE"]["faceSHA256"]):
        raise ValueError("As Djaniras não conservam a mesma identidade.")
    return visuals


def escrever(path, raw, root):
    path = confinado(path, root)
    temp = confinado(path.with_name(path.name + ".tmp-" + uuid.uuid4().hex), root)
    try:
        with temp.open("xb") as stream:
            stream.write(raw)
            stream.flush()
            os.fsync(stream.fileno())
        exigir_sem_vinculos(path)
        os.replace(temp, path)
    finally:
        if temp.exists():
            confinado(temp, root).unlink()


def json_bytes(data):
    return (json.dumps(data, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def preparar(backup, revisao=REVISAO):
    spec = REVISOES[revisao]
    backup = confinado(backup, RAIZ / "backups")
    inventory = json.loads((backup / "manifesto.json").read_text(encoding="utf-8"))
    perfil = ler_pasta_dados(RAIZ, exigir_perfil=True)
    contents, preserved = {}, []
    for record in inventory["arquivos"]:
        if record["scope"] != "perfil":
            continue
        relative = record["relativo"]
        saved = confinado(backup / "perfil" / relative, backup)
        target = confinado(perfil / relative, perfil)
        raw = saved.read_bytes()
        if sha(raw) != record["sha256"] or target.read_bytes() != raw:
            raise ValueError(f"Arquivo mudou após o backup: {target}")
        contents[relative] = raw
        preserved.append({"relativo": relative, "sha256": sha(raw)})
    verificar_conjunto(contents)
    outputs = {}
    for code, old_hash in spec["anteriores"].items():
        relative = f"catalogos/visuais/{code}.xml"
        outputs[relative] = revisar_xml(code, contents[relative], revisao)
        for reference in (MANIFESTO, CATALOGO):
            raw = outputs.get(reference, contents[reference])
            outputs[reference] = substituir(raw, old_hash.encode(), sha(outputs[relative]).encode())
    visuals = verificar_conjunto(contents | outputs)
    package = confinado(backup / revisao, backup)
    package.mkdir(exist_ok=False)
    files = []
    # Recursos primeiro; referências por último. É uma transação recuperável,
    # não uma substituição atômica de todos os arquivos simultaneamente.
    order = [f"catalogos/visuais/{c}.xml" for c in spec["anteriores"]] + [MANIFESTO, CATALOGO]
    for relative in order:
        staged = confinado(package / "novos" / relative, package)
        staged.parent.mkdir(parents=True, exist_ok=True)
        staged.write_bytes(outputs[relative])
        files.append({"relativo": relative, "antes": sha(contents[relative]), "depois": sha(outputs[relative])})
    plan = {"revisao": revisao, "perfil": str(perfil), "estado": "preparada",
            "dataUtc": datetime.now(timezone.utc).isoformat(), "arquivos": files,
            "preservados": preserved, "aprovacaoVisual": "pendente"}
    (package / "plano.json").write_bytes(json_bytes(plan))
    # Registro completo dos13 figurinos: índices reais + revisão semântica
    # proposta. Os novos pares ainda precisam de conferência no GTA.
    continuity = {"revisao": revisao, "seed": ET.fromstring(contents[MANIFESTO]).get("seed"),
                  "aprovacaoVisual": "pendente", "personagens": visuals,
                  "alteracoes": files, "backup": str(backup),
                  "fontes": ["https://github.com/root-cause/v-clothingnames/blob/master/female_legs.json",
                             "https://github.com/root-cause/v-clothingnames/blob/master/female_shoes.json",
                             "https://github.com/citizenfx/natives/blob/master/PED/SetPedHeadOverlayColor.md"],
                  "pedido": spec["pedido"], "pecas": spec["descricao"],
                  "temporada2": "Reutilizar XMLs corrigidos e conferir hashes; não sortear outro rosto/figurino."}
    if revisao == "djanira-rica-joias-0.9.6":
        continuity["fontes"] += ["https://github.com/MnkyArts/charcreator/blob/main/data/outfits_female.json",
            "https://github.com/root-cause/v-clothingnames/blob/master/female_tops.json",
            "https://github.com/root-cause/v-clothingnames/blob/master/female_accessories.json",
            "https://github.com/root-cause/v-clothingnames/blob/master/props_female_ears.json",
            "https://github.com/root-cause/v-clothingnames/blob/master/props_female_bracelets.json"]
        continuity["presetBase"] = "DLC_MP_VAL2_F_OUTFIT_0: braços/pernas/undershirt; vestido112 textura2→1; sem chapéu, preservando salto6; joias por nome real, encaixe visual pendente"
        continuity["origemIndices"] = "Os12 outros XMLs foram preservados. Novas peças/joias da rica têm índices de fontes primárias; aplicação/getters/encaixe e aprovação visual pendentes no GTA."
    if revisao == "djanira-rica-0.9.4":
        continuity["fontes"] += ["https://github.com/MnkyArts/charcreator/blob/main/data/outfits_female.json",
                                  "https://github.com/root-cause/v-clothingnames/blob/master/female_tops.json",
                                  "https://github.com/root-cause/v-clothingnames/blob/master/female_hair.json",
                                  "https://github.com/DurtyFree/gta-v-data-dumps/blob/master/pedHairColors.json"]
        continuity["presetBase"] = "DLC_MP_VWD_F_OUTFIT_HIGHROLL_0; torso/pernas/undershirt/sapato exatos; vestido textura7→2 (mesma geometria)"
        continuity["origemIndices"] = "Os12 outros XMLs vêm do registro real do jogo. Os novos pares/cores da rica são revisão autorizada de dados primários; conferência de getters/aparência no GTA pendente."
    if revisao == "elenco-papeis-0.9.5":
        continuity["fontes"] += ["https://github.com/MnkyArts/charcreator/blob/main/data/outfits_female.json",
                                  "https://github.com/root-cause/v-clothingnames/blob/master/female_tops.json",
                                  "https://github.com/root-cause/v-clothingnames/blob/master/female_hair.json",
                                  "https://github.com/root-cause/v-clothingnames/blob/master/female_accessories.json",
                                  "https://github.com/root-cause/v-clothingnames/blob/master/female_undershirts.json",
                                  "https://github.com/DurtyFree/gta-v-data-dumps/blob/master/pedHairColors.json"]
        continuity["presetBase"] = {"DJANIRA_POBRE": "DLC_MP_HEIST_F_OUTFIT_C_2; trio torso/undershirt/top exato; short/chinelo registrados conservados",
                                    "DINJER": "DLC_MP_HEIST_F_OUTFIT_L_1; trio torso/undershirt/top, textura navy da mesma geometria; saia lápis e lenço identificados nos dados do jogo"}
        continuity["origemIndices"] = "Os11 outros XMLs foram preservados; novos pares de pobre/Dinjer são revisão autorizada com fontes, não índices capturados nesta publicação. Getters e aprovação visual pendentes no GTA."
        continuity["combinacaoDinjer"] = "Blazer com camisa, saia lápis e lenço corresponde ao papel proposto; a combinação e o afro precisam ser vistos na prévia."
    (package / "continuidade.json").write_bytes(json_bytes(continuity))
    return package


def aplicar(package):
    package = confinado(package, RAIZ / "backups")
    plan_path = confinado(package / "plano.json", package)
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    perfil = exigir_sem_vinculos(plan["perfil"])
    if perfil != ler_pasta_dados(RAIZ, exigir_perfil=True) or plan["revisao"] not in REVISOES:
        raise ValueError("Perfil/revisão diferente da configuração autorizada.")
    spec = REVISOES[plan["revisao"]]
    expected = {f"catalogos/visuais/{c}.xml" for c in spec["anteriores"]} | {MANIFESTO, CATALOGO}
    if {r["relativo"] for r in plan["arquivos"]} != expected or len(plan["arquivos"]) != len(expected):
        raise ValueError(f"A revisão deve alterar exatamente {len(expected)} arquivos.")
    files = []
    states = []
    for r in plan["arquivos"]:
        target = confinado(perfil / r["relativo"], perfil)
        old = confinado(package.parent / "perfil" / r["relativo"], package.parent).read_bytes()
        new = confinado(package / "novos" / r["relativo"], package).read_bytes()
        if sha(old) != r["antes"] or sha(new) != r["depois"]:
            raise ValueError("Backup ou saída preparada alterados; nada publicado.")
        current = sha(target.read_bytes())
        if current not in (r["antes"], r["depois"]):
            raise ValueError(f"Edição posterior preservada: {target}")
        files.append((r, target, old, new))
        states.append(current == r["depois"])
    for r in plan["preservados"]:
        if r["relativo"] not in expected and sha(confinado(perfil / r["relativo"], perfil).read_bytes()) != r["sha256"]:
            raise ValueError("Arquivo protegido mudou; interrompido sem sobrescrevê-lo.")
    if any(states) and plan["estado"] not in ("publicando", "concluida", "revertida", "recuperacao_pendente"):
        raise ValueError("Estado misto não pertence a uma publicação registrada.")
    if all(states) and plan["estado"] == "concluida":
        return plan
    contents = {r["relativo"]: confinado(perfil / r["relativo"], perfil).read_bytes() for r in plan["preservados"]}
    verificar_conjunto(contents | {r["relativo"]: new for r, _, _, new in files})
    plan["estado"] = "publicando"
    escrever(plan_path, json_bytes(plan), package)
    try:
        for r, target, _, new in files:
            current = sha(target.read_bytes())
            if current == r["depois"]:
                continue
            if current != r["antes"]:
                raise ValueError(f"Edição concorrente preservada: {target}")
            escrever(target, new, perfil)
        final = {r["relativo"]: confinado(perfil / r["relativo"], perfil).read_bytes() for r in plan["preservados"]}
        verificar_conjunto(final)
        if any(sha(target.read_bytes()) != r["depois"] for r, target, _, _ in files):
            raise ValueError("Um arquivo publicado divergiu do hash preparado.")
        plan["estado"] = "concluida"
        escrever(plan_path, json_bytes(plan), package)
    except Exception:
        plan["estado"] = "revertida"
        for r, target, old, _ in reversed(files):
            try:
                current = sha(target.read_bytes())
                if current == r["depois"]:
                    escrever(target, old, perfil)
                elif current != r["antes"]:
                    plan["estado"] = "recuperacao_pendente"
            except OSError:
                plan["estado"] = "recuperacao_pendente"
        escrever(plan_path, json_bytes(plan), package)
        raise
    return plan


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("acao", choices=("preparar", "aplicar"))
    parser.add_argument("pasta", type=Path)
    parser.add_argument("--revisao", choices=tuple(REVISOES), default=REVISAO,
                        help="Revisão solicitada, usada somente em preparar; aplicar lê o journal.")
    args = parser.parse_args()
    result = preparar(args.pasta, args.revisao) if args.acao == "preparar" else aplicar(args.pasta)
    print(result if isinstance(result, Path) else json.dumps(
        {"estado": result["estado"], "arquivos": len(result["arquivos"]),
         "perfil": result["perfil"], "aprovacaoVisual": result["aprovacaoVisual"]},
        ensure_ascii=False, indent=2))
