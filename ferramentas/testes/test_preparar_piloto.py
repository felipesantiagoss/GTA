"""Fidelidade e publicação exclusiva; aparências sintéticas só em temporários."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest import mock
import xml.etree.ElementTree as ET

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
import preparar_piloto as piloto
from importar_visuais import ler_visual
from preparar_serie import analisar_roteiro
from test_visual import fixture, raw


class PilotoTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.serie = analisar_roteiro(RAIZ / piloto.ROTEIRO, RAIZ / piloto.PROMPT)
        cls.plano = next(x for x in cls.serie["planos"] if x["id"] == piloto.ID)

    def setUp(self):
        parent = RAIZ / "backups/testes-piloto"
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="caso-", dir=parent)
        self.perfil = Path(self.temp.name)
        self.dados = self.perfil / "GTA_Diretor"
        self.cats = self.dados / "catalogos"
        self.cats.mkdir(parents=True)
        for name in ("personagens.json", "locais.json", "animacoes.json", "gestos_fala.xml", "locais_marcados.xml"):
            shutil.copyfile(RAIZ / "catalogos" / name, self.cats / name)

    def tearDown(self):
        self.assertTrue(self.perfil.resolve().is_relative_to(RAIZ.resolve()))
        self.temp.cleanup()

    def prontos(self):
        """Fixtures CPU reais em disco, sem propor índice/figurino para a série."""
        path = self.cats / "personagens.json"
        catalogo = json.loads(path.read_text(encoding="utf-8-sig"))
        visuais = self.cats / "visuais"
        visuais.mkdir(exist_ok=True)
        for entry in catalogo["personagens"]:
            if entry["codigo"] in piloto.ELENCO:
                file = entry["codigo"] + ".xml"
                content = raw(fixture())
                (visuais / file).write_bytes(content)
                entry.update(modelo="mp_f_freemode_01", pronto=True, visualArquivo=file,
                             visualSHA256=hashlib.sha256(content).hexdigest().upper(),
                             estado="fixture_CPU;nao_testado_no_jogo")
        path.write_text(json.dumps(catalogo, ensure_ascii=False), encoding="utf-8")
        tree = ET.parse(self.cats / "locais_marcados.xml")
        # Valor sintético declarado para testar contrato; não publica marca real.
        ET.SubElement(tree.getroot(), "Local", {"id": "ROCA_GRAPESEED", "x": "0", "y": "0", "z": "0",
                      "direcao": "180", "dataUtc": "2026-10-03T00:00:00Z", "origem": "capturado_no_jogo"})
        tree.write(self.cats / "locais_marcados.xml", encoding="utf-8", xml_declaration=True)

    def check(self):
        return piloto.validar_recursos(self.dados, perfil=self.perfil)

    def publish(self):
        return piloto.publicar_piloto(self.dados, perfil=self.perfil)

    def test_seis_blocos_textos_gestos_hora_e_tempos_integrais(self):
        cena = piloto.montar_rascunho()
        self.assertEqual(cena["encenacao"], "pedido_v1")
        self.assertEqual((cena["ambiente"]["hora"], cena["ambiente"]["clima"]), ("17:00", "limpo"))
        self.assertEqual(cena["ambiente"]["local"], "ROCA_GRAPESEED")
        self.assertEqual([x["personagem"] for x in cena["elenco"]], ["DJANIRA_POBRE", "DINJER"])
        self.assertEqual([x["marca"]["direita"] for x in cena["elenco"]], [-1.1, 1.1])
        self.assertEqual([x["olharPara"] for x in cena["elenco"]], ["DINJER", "DJANIRA_POBRE"])
        self.assertEqual([x["texto"] for x in cena["roteiro"] if "texto" in x], [x["texto"] for x in self.plano["roteiro"]])
        self.assertEqual([x["duracaoSegundos"] for x in cena["roteiro"]], [12, 6, 9, 6, 3, 3, 3, 4])
        self.assertEqual([x["gesto"] for x in cena["roteiro"] if x["tipo"] == "fala"], [x["gestoOriginal"] for x in self.plano["roteiro"] if x["tipo"] == "fala"])
        self.assertEqual([x["plano"] for x in cena["roteiro"]], ["LONGE", "DOIS", "MEDIA", "MEDIA", "MEDIA", "MEDIA", "MEDIA", "DOIS"])
        self.assertEqual(sum(x["duracaoSegundos"] for x in cena["roteiro"]), 46)
        self.assertEqual(cena["acoes"], [])

    def test_srt_cobre_so_vozes_contagem_e_prefixo_sem_legendas_silenciosas(self):
        srt = piloto.gerar_srt_piloto(piloto.montar_rascunho())
        self.assertEqual(srt.count(" --> "), 6)
        self.assertIn("00:00:15,000 --> 00:00:21,000", srt)
        self.assertIn("00:00:42,000 --> 00:00:45,000", srt)
        self.assertIn("Narrador: Ela não devia", srt)
        self.assertNotIn("00:00:49,000", srt)
        for bloco in self.plano["roteiro"]:
            self.assertIn(bloco["texto"], srt)
        self.assertNotIn("pausa", srt.lower())

    def test_inventario_todos95_ids_e_acoes_literais_e_familias(self):
        report = piloto.relatorio_capacidades()
        self.assertEqual(report["contagemPlanos"], 95)
        self.assertEqual([x["id"] for x in report["cenas"]], [x["id"] for x in self.serie["planos"]])
        for entrada, plano in zip(report["cenas"], self.serie["planos"]):
            self.assertEqual([a["textoOriginal"] for a in entrada["acoesOriginais"]], [a["raw"] for a in plano["acoesOriginais"]])
            self.assertFalse(entrada["runtimeEmitido"])
        self.assertIn("EP01_C08", [x["id"] for x in report["grupos"]["telefone"]["cenas"]])
        self.assertIn("EP01_C01", [x["id"] for x in report["grupos"]["voar"]["cenas"]])
        self.assertIn("EP01_C07", [x["id"] for x in report["grupos"]["combate"]["cenas"]])
        self.assertIn("EP07_C04", [x["id"] for x in report["grupos"]["sentar"]["cenas"]])

    def test_pendente_nao_cria_cena_fila_ou_manifesto(self):
        with self.assertRaisesRegex(piloto.ErroPiloto, "pendente"):
            self.publish()
        self.assertFalse((self.dados / "cenas").exists())
        self.assertFalse((self.dados / "backups").exists())

    def test_visuais_reais_em_disco_validados_sem_alegar_teste_gta(self):
        self.prontos()
        result = self.check()
        self.assertEqual(len(result["visuaisSHA256"]), 13)
        self.assertEqual(result["marca"]["id"], "ROCA_GRAPESEED")
        self.assertFalse(result["testeNoJogo"])
        self.assertFalse(result["gravado"])
        self.assertFalse((self.dados / "cenas").exists())

    def alterar_rica(self, node, texto=None, atributo=None):
        path = self.cats / "visuais/DJANIRA_RICA.xml"
        tree = ET.parse(path)
        element = tree.find(node)
        if texto is not None:
            element.text = texto
        if atributo:
            element.set(*atributo)
        tree.write(path, encoding="utf-8", xml_declaration=True)
        catalog = json.loads((self.cats / "personagens.json").read_text(encoding="utf-8"))
        next(x for x in catalog["personagens"] if x["codigo"] == "DJANIRA_RICA")["visualSHA256"] = hashlib.sha256(path.read_bytes()).hexdigest().upper()
        (self.cats / "personagens.json").write_text(json.dumps(catalog), encoding="utf-8")

    def test_continuidade_face_mascara_overlays_nao_so_hash_arquivo(self):
        self.prontos()
        path = self.cats / "visuais/DJANIRA_RICA.xml"
        face = ler_visual(path)["faceSHA256"]
        self.alterar_rica("PedProperties/HeadFeatures/Overlays/_3", atributo=("index", "0"))
        self.assertEqual(ler_visual(path)["faceSHA256"], face)
        with self.assertRaisesRegex(piloto.ErroPiloto, "overlays"):
            self.check()

    def test_hash_visual_divergente_nao_publica(self):
        self.prontos()
        path = self.cats / "visuais/DINJER.xml"
        path.write_bytes(path.read_bytes() + b"\n")
        with self.assertRaisesRegex(piloto.ErroPiloto, "SHA256"):
            self.publish()
        self.assertFalse((self.dados / "cenas").exists())

    def test_roca_ausente_e_marca_nao_finita_nao_publicam(self):
        self.prontos()
        path = self.cats / "locais_marcados.xml"
        tree = ET.parse(path)
        marca = tree.find("Local[@id='ROCA_GRAPESEED']")
        tree.getroot().remove(marca)
        tree.write(path, encoding="utf-8")
        with self.assertRaisesRegex(piloto.ErroPiloto, "não foi marcada"):
            self.publish()
        tree.getroot().append(marca)
        marca.set("x", "NaN")
        tree.write(path, encoding="utf-8")
        with self.assertRaisesRegex(piloto.ErroPiloto, "número/data"):
            self.publish()
        self.assertFalse((self.dados / "cenas").exists())

    def test_publicacao_preserva_cena_editada_antes_de_qualquer_mutacao(self):
        self.prontos()
        scenes = self.dados / "cenas"
        scenes.mkdir()
        path = scenes / "EP01_C08.json"
        content = b'{"edicao_usuario":true}\n'
        path.write_bytes(content)
        with self.assertRaisesRegex(piloto.ErroPiloto, "editado preservado"):
            self.publish()
        self.assertEqual(path.read_bytes(), content)
        self.assertFalse((scenes / "fila_piloto.json").exists())
        self.assertFalse((self.dados / "backups").exists())

    def test_publicacao_exclusiva_idempotente_preserva_marcas_catalogos_srt(self):
        self.prontos()
        scenes = self.dados / "cenas"
        scenes.mkdir()
        (scenes / "EP01_C08.srt").write_bytes(b"SRT de tomada anterior")
        before = {str(p.relative_to(self.dados)): p.read_bytes() for p in self.dados.rglob("*") if p.is_file()}
        result = self.publish()
        self.assertEqual(result["adicionados"], ["cenas/EP01_C08.json", "cenas/fila_piloto.json"])
        for name, content in before.items():
            self.assertEqual((self.dados / name).read_bytes(), content)
        manifest = json.loads(Path(result["manifesto"]).read_text(encoding="utf-8"))
        self.assertEqual(manifest["backupsDeSubstituidos"], [])
        self.assertTrue(Path(result["manifesto"]).with_name("conclusao.json").is_file())
        self.assertEqual(self.publish()["adicionados"], [])

    def test_dados_fora_perfil_e_onedrive_rejeitados(self):
        with self.assertRaisesRegex(piloto.ErroPiloto, "runtime existente"):
            piloto.validar_recursos(RAIZ, perfil=self.perfil)
        one = self.perfil / "OneDrive-empresa"
        (one / "GTA_Diretor").mkdir(parents=True)
        with self.assertRaisesRegex(piloto.ErroPiloto, "OneDrive"):
            piloto.validar_recursos(one / "GTA_Diretor", perfil=one)

    def test_origem_modificada_rejeitada_sem_escrever(self):
        source = self.perfil / piloto.ROTEIRO
        source.write_bytes((RAIZ / piloto.ROTEIRO).read_bytes() + b"\n")
        with self.assertRaisesRegex(piloto.ErroPiloto, "SHA256 do roteiro"):
            piloto.montar_rascunho(self.perfil)


if __name__ == "__main__":
    unittest.main()
