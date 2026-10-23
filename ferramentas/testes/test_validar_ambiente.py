"""Locais marcados e limpeza opcional, sem simular execução no GTA."""

from copy import deepcopy
from decimal import Decimal
from pathlib import Path
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
from validar_cena import ErroCena, validar_cena


class ContratoAmbiente(unittest.TestCase):
    def setUp(self):
        self.cena = ET.parse(RAIZ / "cenas" / "teste_cortes.xml").getroot()

    def validar_documento(self, cena):
        with tempfile.TemporaryDirectory(prefix="ambiente-", dir=RAIZ / "ferramentas" / "testes") as pasta:
            caminho = Path(pasta) / "cena.xml"
            caminho.write_bytes(ET.tostring(cena, encoding="utf-8"))
            validar_cena(caminho)

    def marcar_local(self, cena, codigo="TESTE_AMBIENTE"):
        local = cena.find("Local")
        local.set("referencia", "marcado")
        local.set("id", codigo)

    def test_cinco_exemplos_e_conteudo_aprovado_preservado(self):
        for nome in ("teste_dupla.xml", "teste_falas.xml", "teste_camera.xml",
                     "teste_cortes.xml", "teste_ambiente.xml"):
            with self.subTest(nome=nome):
                validar_cena(RAIZ / "cenas" / nome)
        ambiente = ET.parse(RAIZ / "cenas" / "teste_ambiente.xml").getroot()
        self.assertEqual(ambiente.find("Local").attrib,
                         {"referencia": "marcado", "id": "TESTE_AMBIENTE", "distanciaFrente": "4"})
        novos_atributos = dict(ambiente.find("Ambiente").attrib)
        self.assertEqual(novos_atributos.pop("limparArea"), "true")
        self.assertEqual(novos_atributos, self.cena.find("Ambiente").attrib)
        for caminho in ("Personagens", "Camera", "Roteiro"):
            antes, depois = self.cena.find(caminho), ambiente.find(caminho)
            self.assertEqual(antes.attrib, depois.attrib)
            self.assertEqual([(p.tag, p.attrib, (p.text or "").strip()) for p in antes],
                             [(p.tag, p.attrib, (p.text or "").strip()) for p in depois])
        self.assertEqual(sum(Decimal(t.attrib["duracaoSegundos"]) for t in ambiente.find("Roteiro")), 36)

    def test_condicao_marcado_exige_id_e_jogador_proibe_id(self):
        self.marcar_local(self.cena)
        self.validar_documento(self.cena)
        del self.cena.find("Local").attrib["id"]
        with self.assertRaisesRegex(ErroCena, "Local.id.*obrigatório"):
            self.validar_documento(self.cena)
        self.cena.find("Local").set("referencia", "jogador")
        self.cena.find("Local").set("id", "TESTE_AMBIENTE")
        with self.assertRaisesRegex(ErroCena, "Local.id.*proibido"):
            self.validar_documento(self.cena)
        del self.cena.find("Local").attrib["id"]
        self.cena.find("Local").set("referencia", "coordenadas")
        with self.assertRaisesRegex(ErroCena, "Local.referencia"):
            self.validar_documento(self.cena)

    def test_codigos_de_marcas_maiusculos_e_limite(self):
        for codigo in ("A", "TESTE_AMBIENTE", "ROCA_GRAPESEED", "A" * 48):
            with self.subTest(codigo=codigo):
                self.marcar_local(self.cena, codigo)
                self.validar_documento(self.cena)
        for codigo in ("", "teste_ambiente", "1TESTE", "TESTE-AREA", " TESTE", "TESTE ", "A" * 49):
            with self.subTest(codigo=codigo):
                self.marcar_local(self.cena, codigo)
                with self.assertRaisesRegex(ErroCena, "Local.id"):
                    self.validar_documento(self.cena)

    def test_limpeza_opcional_e_booleanos_literais(self):
        self.validar_documento(self.cena)
        for valor in ("true", "false"):
            self.cena.find("Ambiente").set("limparArea", valor)
            self.validar_documento(self.cena)
        for valor in ("1", "0", "True", "sim", "", " true "):
            with self.subTest(valor=valor):
                self.cena.find("Ambiente").set("limparArea", valor)
                with self.assertRaisesRegex(ErroCena, "Ambiente.limparArea"):
                    self.validar_documento(self.cena)
        self.cena.find("Ambiente").set("limparArea", "true")
        self.cena.find("Ambiente").set("raio", "40")
        with self.assertRaisesRegex(ErroCena, "desconhecido"):
            self.validar_documento(self.cena)

    def test_distancia_antiga_e_coordenadas_proibidas(self):
        self.marcar_local(self.cena)
        for valor in ("1", "4", "10"):
            self.cena.find("Local").set("distanciaFrente", valor)
            self.validar_documento(self.cena)
        for valor in ("0.999", "10.001", "12", "NaN"):
            with self.subTest(valor=valor):
                self.cena.find("Local").set("distanciaFrente", valor)
                with self.assertRaisesRegex(ErroCena, "Local.distanciaFrente"):
                    self.validar_documento(self.cena)
        self.cena.find("Local").set("distanciaFrente", "4")
        for campo in ("x", "y", "z", "heading"):
            with self.subTest(campo=campo):
                cena = deepcopy(self.cena)
                cena.find("Local").set(campo, "0")
                with self.assertRaisesRegex(ErroCena, "desconhecido"):
                    self.validar_documento(cena)


if __name__ == "__main__":
    unittest.main(verbosity=2)
