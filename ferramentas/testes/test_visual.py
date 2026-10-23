"""XML/importação/fingerprint em CPU; dados sintéticos não são figurinos da série."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock
import xml.etree.ElementTree as ET

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
import importar_visuais as visuais
from importar_visuais import ErroVisual, hash_modelo, importar_visuais, ler_visual, status_visuais, validar_visual_bytes


def fixture(model="mp_f_freemode_01", head=True):
    """Contrato mínimo sintético; nenhum índice é proposto para uso no GTA."""
    root = ET.Element("OutfitPedData")
    ET.SubElement(root, "Type").text = "1"
    ET.SubElement(root, "ModelHash").text = hex(hash_modelo(model))
    ped = ET.SubElement(root, "PedProperties")
    components, props = ET.SubElement(ped, "PedComps"), ET.SubElement(ped, "PedProps")
    for i in range(12):
        ET.SubElement(components, f"_{i}").text = "0,0"
    for i in range(10):
        ET.SubElement(props, f"_{i}").text = "-1,-1"
    if head:
        node = ET.SubElement(ped, "HeadFeatures", {"WasInArray": "true"})
        dna = ET.SubElement(node, "ShapeAndSkinTone")
        for name in visuais.PAIS:
            ET.SubElement(dna, name).text = "0"
        for name in visuais.MISTURAS:
            ET.SubElement(dna, name).text = "0.5"
        ET.SubElement(dna, "IsP").text = "0"
        for name in ("HairColour", "HairColourStreaks", "EyeColour"):
            ET.SubElement(node, name).text = "0"
        features = ET.SubElement(node, "FacialFeatures")
        for i in range(20):
            ET.SubElement(features, f"_{i}").text = "0.0"
        overlays = ET.SubElement(node, "Overlays")
        for i in range(13):
            ET.SubElement(overlays, f"_{i}", {"index": "255", "colour": "0", "colourSecondary": "0", "opacity": "1"})
    return root


def raw(root):
    return ET.tostring(root, encoding="utf-8", xml_declaration=True)


class VisualTests(unittest.TestCase):
    def setUp(self):
        parent = RAIZ / "backups" / "testes-visual"
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="caso-", dir=parent)
        self.root = Path(self.temp.name)
        self.assertTrue(self.root.resolve().is_relative_to(RAIZ.resolve()))
        self.patch_root = mock.patch.object(visuais, "RAIZ", self.root)
        self.patch_root.start()
        self.source = self.root / "menyoo" / "Outfit"
        self.source.mkdir(parents=True)
        self.dest = self.root / "catalogos" / "visuais"
        self.ped_list = self.root / "PedList.xml"
        self.ped_list.write_text('<PedList><Category name="AmbientMale"><Ped name="a_m_m_business_01" /></Category>'
                                 '<Category name="Animal"><Ped name="a_c_cat_01" /></Category></PedList>', encoding="utf-8")

    def tearDown(self):
        self.patch_root.stop()
        self.assertTrue(self.root.resolve().is_relative_to(RAIZ.resolve()))
        self.temp.cleanup()

    def save(self, code, tree=None):
        path = self.source / (code + ".xml")
        path.write_bytes(raw(fixture() if tree is None else tree))
        return path

    def trio(self):
        return {code: self.save(code) for code in visuais.CODIGOS[:3]}

    def import_all(self):
        return importar_visuais(self.source, self.dest, self.ped_list)

    def reject(self, tree, model="mp_f_freemode_01"):
        with self.assertRaises(ErroVisual):
            validar_visual_bytes(raw(tree), model)

    def test_xml_real_menyoo_hash_original_e_jenkins_sem_mudar_fonte(self):
        path = visuais.MENYOO / "Outfit" / "GhostRider.xml"
        if not path.is_file():
            self.skipTest("Exemplo do autor não está instalado")
        before = path.read_bytes()
        visual = ler_visual(path, expected_model="mp_m_freemode_01")
        self.assertEqual(visual["modelHash"], 0x705E61F2)  # Hash registrado nesse XML original.
        self.assertEqual(visual["sha256"], hashlib.sha256(before).hexdigest().upper())
        self.assertEqual(path.read_bytes(), before)
        self.assertEqual((len(visual["componentes"]), len(visual["acessorios"]), len(visual["rosto"]["tracos"])), (12, 10, 20))

    def test_status_treze_pendentes_sem_criar_arquivos(self):
        result = status_visuais(self.source, self.dest, self.ped_list)
        self.assertEqual([item["codigo"] for item in result["personagens"]], list(visuais.CODIGOS))
        self.assertTrue(all(not item["presente"] and not item["aprovado"] for item in result["personagens"]))
        self.assertIsNone(result["identidadeDjaniraConferida"])
        self.assertFalse(self.dest.exists())
        self.assertEqual(list(self.source.iterdir()), [])

    def test_importacao_ausente_nao_fabrica_saida(self):
        with self.assertRaisesRegex(ErroVisual, "nenhum XML real"):
            self.import_all()
        self.assertFalse(self.dest.exists())

    def test_freemode_exige_rosto_completo_e_nao_cache_falso(self):
        base = fixture()
        for path in ("PedProperties/HeadFeatures", "PedProperties/HeadFeatures/FacialFeatures/_19", "PedProperties/HeadFeatures/Overlays/_12"):
            tree = deepcopy(base)
            parent_path, name = path.rsplit("/", 1)
            parent = tree.find(parent_path)
            parent.remove(parent.find(name))
            self.reject(tree)
        tree = deepcopy(base)
        tree.find("PedProperties/HeadFeatures").set("WasInArray", "false")
        self.reject(tree)

    def test_dtd_entidades_externas_utf8_utf16_sao_proibidas(self):
        text = '<?xml version="1.0" encoding="UTF-8"?><!DOCTYPE OutfitPedData [<!ENTITY x SYSTEM "file:///segredo">]><OutfitPedData>&x;</OutfitPedData>'
        for payload in (text.encode("utf-8"), text.replace("UTF-8", "UTF-16").encode("utf-16")):
            with self.subTest(encoding=payload[:5]), self.assertRaisesRegex(ErroVisual, "DTD"):
                validar_visual_bytes(payload, "mp_f_freemode_01")

    def test_namespace_duplicadas_unknown_ou_subcampos_rejeitados(self):
        base = fixture()
        variants = []
        tree = deepcopy(base); tree.append(deepcopy(tree.find("ModelHash"))); variants.append(tree)
        tree = deepcopy(base); ET.SubElement(tree.find("PedProperties/PedComps"), "_12").text = "0,0"; variants.append(tree)
        tree = deepcopy(base); ET.SubElement(tree.find("PedProperties/HeadFeatures"), "Inventado"); variants.append(tree)
        tree = deepcopy(base); ET.SubElement(tree.find("Type"), "inner"); variants.append(tree)
        tree = deepcopy(base); tree.set("xmlns", "urn:nao"); variants.append(tree)
        for tree in variants:
            self.reject(tree)

    def test_tipo_modelo_nao_finite_e_faixas(self):
        paths = (("Type", "3"), ("PedProperties/HeadFeatures/FacialFeatures/_0", "NaN"),
                 ("PedProperties/HeadFeatures/ShapeAndSkinTone/ShapeVal", "1e999"),
                 ("PedProperties/HeadFeatures/ShapeAndSkinTone/ToneVal", "-0.1"),
                 ("PedProperties/HeadFeatures/ShapeAndSkinTone/ShapeFatherId", "46"),
                 ("PedProperties/HeadFeatures/FacialFeatures/_0", "1.1"),
                 ("PedProperties/HeadFeatures/EyeColour", "33"))
        for path, value in paths:
            with self.subTest(path=path, value=value):
                tree = fixture(); tree.find(path).text = value; self.reject(tree)
        tree = fixture(); tree.find("PedProperties/HeadFeatures/Overlays/_0").set("opacity", "Infinity"); self.reject(tree)
        with self.assertRaises(ErroVisual):
            validar_visual_bytes(raw(fixture()), "mp_m_freemode_01")

    def test_acessorios_mantem_indices_raw_menyoo_sem_mais_um_no_arquivo(self):
        tree = fixture()
        tree.find("PedProperties/PedProps/_0").text = "0,3"
        result = validar_visual_bytes(raw(tree), "mp_f_freemode_01")
        self.assertEqual(result["acessorios"][0], [0, 3])
        for value in ("-1,2", "0,-1", "-2,0", "0,0,0"):
            tree.find("PedProperties/PedProps/_0").text = value
            self.reject(tree)

    def test_historico_de_alternativas_nao_substitui_aparencia_nem_identidade(self):
        tree = fixture()
        baseline = validar_visual_bytes(raw(tree), "mp_f_freemode_01")
        metadata = ET.SubElement(tree, "DiretorVisual")
        ET.SubElement(metadata, "PoliticaAlternativas").text = "AlternativasVisualDiretor.v1"
        ET.SubElement(metadata, "FaceSHA256").text = "F" * 64
        ET.SubElement(metadata, "QuantidadeAjustes").text = "1"
        adjustment = ET.SubElement(ET.SubElement(metadata, "Ajustes"), "Ajuste")
        for name, value in (("Tipo", "componente"), ("Peca", "4"), ("Esperado", "222,1"),
                            ("Observado", "0,0"), ("Novo", "0,0"), ("Tentativa", "1")):
            ET.SubElement(adjustment, name).text = value
        self.trio()
        source = self.save("DJANIRA_POBRE", tree)
        before = source.read_bytes()
        actual = validar_visual_bytes(before, "mp_f_freemode_01")
        self.assertEqual(actual["componentes"][4], [0, 0])
        self.assertEqual(actual["faceSHA256"], baseline["faceSHA256"])
        self.assertTrue(visuais.mesma_identidade_djanira(baseline, actual))
        self.import_all()
        self.assertEqual(source.read_bytes(), before)
        self.assertEqual((self.dest / "DJANIRA_POBRE.xml").read_bytes(), before)

    def test_fingerprint_ignora_roupas_penteado_maquiagem_e_formatacao(self):
        pobre, rica = fixture(), fixture()
        rica.find("PedProperties/PedComps/_2").text = "9,2"
        rica.find("PedProperties/PedComps/_11").text = "5,1"
        rica.find("PedProperties/HeadFeatures/HairColour").text = "3"
        rica.find("PedProperties/HeadFeatures/Overlays/_4").set("index", "1")
        rica.find("PedProperties/HeadFeatures/ShapeAndSkinTone/ShapeVal").text = "5e-1"
        rica.find("PedProperties/HeadFeatures/FacialFeatures/_0").text = "-0.000"
        a = validar_visual_bytes(raw(pobre), "mp_f_freemode_01")
        b = validar_visual_bytes(raw(rica), "mp_f_freemode_01")
        self.assertEqual(a["faceSHA256"], b["faceSHA256"])
        self.assertNotEqual(a["sha256"], b["sha256"])

    def test_fingerprint_detecta_dna_tracos_olhos_e_componente_facial(self):
        baseline = validar_visual_bytes(raw(fixture()), "mp_f_freemode_01")["faceSHA256"]
        for path, value in (("PedProperties/HeadFeatures/ShapeAndSkinTone/ToneMotherId", "1"),
                            ("PedProperties/HeadFeatures/FacialFeatures/_19", "0.1"),
                            ("PedProperties/HeadFeatures/EyeColour", "1"), ("PedProperties/PedComps/_0", "1,0")):
            tree = fixture(); tree.find(path).text = value
            self.assertNotEqual(validar_visual_bytes(raw(tree), "mp_f_freemode_01")["faceSHA256"], baseline)

    def test_djanira_compartilha_overlays_e_mascara_mas_pode_mudar_cabelo_roupa(self):
        base = validar_visual_bytes(raw(fixture()), "mp_f_freemode_01")
        tree = fixture(); tree.find("PedProperties/PedComps/_2").text = "9,2"
        tree.find("PedProperties/HeadFeatures/HairColour").text = "3"
        changed = validar_visual_bytes(raw(tree), "mp_f_freemode_01")
        self.assertTrue(visuais.mesma_identidade_djanira(base, changed))
        for path, attr, value in (("PedProperties/HeadFeatures/Overlays/_3", "index", "1"),
                                  ("PedProperties/HeadFeatures/Overlays/_2", "opacity", "0.5"),
                                  ("PedProperties/PedComps/_1", None, "1,0")):
            with self.subTest(path=path):
                tree = fixture()
                if attr: tree.find(path).set(attr, value)
                else: tree.find(path).text = value
                changed = validar_visual_bytes(raw(tree), "mp_f_freemode_01")
                self.assertEqual(base["faceSHA256"], changed["faceSHA256"])
                self.assertFalse(visuais.mesma_identidade_djanira(base, changed))

    def test_overlays_divergentes_rejeitam_importacao_antes_de_escrever(self):
        self.trio()
        tree = fixture(); tree.find("PedProperties/HeadFeatures/Overlays/_7").set("index", "1")
        self.save("DJANIRA_RICA", tree)
        with self.assertRaisesRegex(ErroVisual, "overlays"):
            self.import_all()
        self.assertFalse(self.dest.exists())

    def test_producao_pode_variar_mas_os_quatro_overlays_da_pele_permanecem_iguais(self):
        base = validar_visual_bytes(raw(fixture()), "mp_f_freemode_01")
        tree = fixture()
        for slot in (4, 5, 8):
            tree.find(f"PedProperties/HeadFeatures/Overlays/_{slot}").set("opacity", "0.12")
        changed = validar_visual_bytes(raw(tree), "mp_f_freemode_01")
        self.assertTrue(visuais.mesma_identidade_djanira(base, changed))
        for slot in (0, 3, 6, 7):
            with self.subTest(slot=slot):
                skin = fixture()
                skin.find(f"PedProperties/HeadFeatures/Overlays/_{slot}").set("opacity", "0.12")
                self.assertFalse(visuais.mesma_identidade_djanira(
                    base, validar_visual_bytes(raw(skin), "mp_f_freemode_01")))

    def test_importa_bytes_autenticos_sha_manifesto_sem_aprovar(self):
        sources = self.trio()
        original = {code: path.read_bytes() for code, path in sources.items()}
        result = self.import_all()
        self.assertEqual(result["importados"], list(visuais.CODIGOS[:3]))
        self.assertEqual(len(result["pendentes"]), 10)
        self.assertTrue(result["manifesto"]["identidadeDjaniraConferida"])
        for item in result["manifesto"]["personagens"]:
            code = item["codigo"]
            self.assertFalse(item["aprovado"])
            self.assertNotIn("pronto", item)
            self.assertEqual((self.dest / (code + ".xml")).read_bytes(), original[code])
            self.assertEqual(sources[code].read_bytes(), original[code])
            self.assertEqual(item["visualSHA256"], hashlib.sha256(original[code]).hexdigest().upper())

    def test_rosto_divergente_rejeita_tudo_antes_de_backup_ou_escrita(self):
        self.trio()
        tree = fixture(); tree.find("PedProperties/HeadFeatures/EyeColour").text = "1"
        self.save("DJANIRA_RICA", tree)
        with self.assertRaisesRegex(ErroVisual, "o rosto difere"):
            self.import_all()
        self.assertFalse(self.dest.exists())
        self.assertFalse((self.root / "backups").exists())

    def test_substituicao_backup_conferido_e_nenhum_tmp_remanescente(self):
        self.trio(); self.import_all()
        before = {p.name: p.read_bytes() for p in self.dest.iterdir()}
        tree = fixture(); tree.find("PedProperties/PedComps/_11").text = "4,1"
        self.save("DINJER", tree)
        result = self.import_all()
        backup = Path(result["backup"])
        report = json.loads((backup / "manifesto.json").read_text(encoding="utf-8"))
        self.assertEqual(len(report["arquivos"]), 4)
        for entry in report["arquivos"]:
            saved = Path(entry["backup"])
            self.assertEqual(saved.read_bytes(), before[saved.name])
            self.assertEqual(entry["sha256"], hashlib.sha256(saved.read_bytes()).hexdigest().upper())
        self.assertFalse(list(self.dest.glob("*.tmp-*")))
        self.assertEqual((self.dest / "DINJER.xml").read_bytes(), (self.source / "DINJER.xml").read_bytes())

    def test_importacao_parcial_preserva_outros_e_confere_djanira_anterior(self):
        self.save("DJANIRA_POBRE")
        first = self.import_all()
        self.assertFalse(first["manifesto"]["identidadeDjaniraConferida"])
        self.source.joinpath("DJANIRA_POBRE.xml").unlink()
        self.save("DJANIRA_RICA")
        second = self.import_all()
        self.assertTrue(second["manifesto"]["identidadeDjaniraConferida"])
        self.assertEqual([i["codigo"] for i in second["manifesto"]["personagens"]], list(visuais.CODIGOS[:2]))
        self.assertTrue((self.dest / "DJANIRA_POBRE.xml").is_file())

    def test_nativo_modelo_conferido_no_pedlist_e_sem_personalizacao_facial(self):
        self.save("O_CHEFE", fixture("a_m_m_business_01", head=False))
        result = self.import_all()
        self.assertEqual(result["manifesto"]["personagens"][0]["modelo"], "a_m_m_business_01")
        self.assertFalse(result["manifesto"]["personagens"][0]["aprovado"])

    def test_nativo_ignora_headfeatures_generico_de_exportacao_legada(self):
        tree = fixture("a_m_m_business_01", head=False)
        ET.SubElement(tree.find("PedProperties"), "HeadFeatures", {"WasInArray": "false"})
        result = validar_visual_bytes(raw(tree), "a_m_m_business_01")
        self.assertFalse(result["freemode"])
        self.assertIsNone(result["rosto"])

    def test_isparent_menyoo_booleano_ou_numero_tem_fingerprint_equivalente(self):
        fingerprints = []
        for flag in ("0", "false"):
            tree = fixture(); tree.find("PedProperties/HeadFeatures/ShapeAndSkinTone/IsP").text = flag
            fingerprints.append(validar_visual_bytes(raw(tree), "mp_f_freemode_01")["faceSHA256"])
        self.assertEqual(fingerprints[0], fingerprints[1])

    def test_nativo_nao_listado_e_animal_rejeitados(self):
        for model in ("a_m_m_inventado_01", "a_c_cat_01"):
            self.save("O_CHEFE", fixture(model, head=False))
            with self.assertRaisesRegex(ErroVisual, "PedList"):
                self.import_all()
            self.assertFalse(self.dest.exists())

    def test_metadados_tarefas_armas_posicoes_anexos_nunca_sao_aplicados(self):
        tree = fixture()
        ET.SubElement(tree, "PositionRotation").text = "999,999,999"
        ET.SubElement(tree.find("PedProperties"), "CurrentWeapon").text = "0xFFFFFFFF"
        ET.SubElement(tree, "SpoonerAttachments").append(ET.Element("Attachment", {"isAttached": "true"}))
        result = validar_visual_bytes(raw(tree), "mp_f_freemode_01")
        baseline = validar_visual_bytes(raw(fixture()), "mp_f_freemode_01")
        self.assertEqual(result["faceSHA256"], baseline["faceSHA256"])
        self.assertEqual(set(result), set(baseline))
        self.assertNotEqual(result["sha256"], baseline["sha256"])

    def test_destino_escapando_e_vinculo_sao_recusados(self):
        self.trio()
        with self.assertRaises(ErroVisual):
            importar_visuais(self.source, self.root / ".." / "fora", self.ped_list)
        with self.assertRaises(ErroVisual):
            importar_visuais(self.source, self.root.parent / "fora", self.ped_list)

    def test_manifesto_corrompido_nao_sobrescreve_visuais(self):
        self.trio(); self.import_all()
        path = self.dest / visuais.MANIFESTO
        path.write_text('{"versao":1,"versao":1}', encoding="utf-8")
        before = {p.name: p.read_bytes() for p in self.dest.iterdir()}
        with self.assertRaises(ErroVisual):
            self.import_all()
        self.assertEqual({p.name: p.read_bytes() for p in self.dest.iterdir()}, before)

    def test_status_padrao_le_perfil_fora_workspace_sem_escrever(self):
        perfil = self.root / "perfil"
        destino = perfil / "GTA_Diretor" / "catalogos" / "visuais"
        destino.mkdir(parents=True)
        workspace = self.root / "workspace"
        workspace.mkdir()
        for codigo in visuais.CODIGOS[:3]:
            (destino / (codigo + ".xml")).write_bytes(raw(fixture()))
        changed = fixture(); changed.find("PedProperties/HeadFeatures/EyeColour").text = "1"
        self.save("DJANIRA_RICA", changed)  # Menyoo não substitui o XML canônico do runtime.
        before = {p: p.read_bytes() for p in perfil.rglob("*") if p.is_file()}
        with mock.patch.dict("os.environ", {"USERPROFILE": str(perfil)}), mock.patch.object(visuais, "RAIZ", workspace):
            result = status_visuais(self.source, ped_list=self.ped_list)
            self.assertEqual(result["destino"], str(destino))
            self.assertTrue(result["identidadeDjaniraConferida"])
            self.assertEqual(sum(item["registrado"] for item in result["personagens"]), 3)
            with self.assertRaisesRegex(ErroVisual, "workspace"):
                importar_visuais(self.source, destino, self.ped_list)
        self.assertEqual({p: p.read_bytes() for p in perfil.rglob("*") if p.is_file()}, before)
        self.assertFalse((workspace / "backups").exists())

    def test_status_confere_hash_catalogo_e_rejeita_xml_trocado(self):
        self.dest.mkdir(parents=True)
        visual_path = self.dest / "DJANIRA_POBRE.xml"
        visual_path.write_bytes(raw(fixture()))
        sha = hashlib.sha256(visual_path.read_bytes()).hexdigest().upper()
        catalogo = self.dest.parent / "personagens.json"
        catalogo.write_text(json.dumps({"versao": 1, "personagens": [
            {"codigo": "DJANIRA_POBRE", "modelo": "mp_f_freemode_01", "visualArquivo": "DJANIRA_POBRE.xml", "visualSHA256": sha}]}), encoding="utf-8")
        valid = status_visuais(self.source, self.dest, self.ped_list)["personagens"][0]
        self.assertTrue(valid["hashCatalogoConferido"])
        changed = fixture(); changed.find("PedProperties/PedComps/_11").text = "1,0"
        visual_path.write_bytes(raw(changed))
        invalid = status_visuais(self.source, self.dest, self.ped_list)["personagens"][0]
        self.assertEqual(invalid["estado"], "arquivo inválido")
        self.assertIn("hash", invalid["erro"])

    def test_status_sem_userprofile_utiliza_workspace_sem_criar_destino(self):
        with mock.patch.dict("os.environ", {"USERPROFILE": ""}), mock.patch.object(visuais, "DESTINO", self.dest):
            result = status_visuais(self.source, ped_list=self.ped_list)
        self.assertEqual(result["destino"], str(self.dest))
        self.assertFalse(self.dest.exists())

    def test_limites_tamanho_profundidade(self):
        with self.assertRaises(ErroVisual):
            validar_visual_bytes(b"x" * (visuais.MAX_BYTES + 1), "mp_f_freemode_01")
        tree = fixture(); parent = ET.SubElement(tree, "Ignorado")
        for _ in range(33):
            parent = ET.SubElement(parent, "Nivel")
        self.reject(tree)


if __name__ == "__main__":
    unittest.main()
