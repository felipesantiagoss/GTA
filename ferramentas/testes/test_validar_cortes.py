"""Contrato de cortes por fala, compatibilidade e preservação do teste aprovado."""

from copy import deepcopy
from pathlib import Path
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
from validar_cena import ErroCena, PLANOS, validar_cena


class ContratoCortes(unittest.TestCase):
    def setUp(self):
        self.cena = ET.parse(RAIZ / "cenas" / "teste_cortes.xml").getroot()

    def validar_documento(self, cena):
        with tempfile.TemporaryDirectory(prefix="cortes-", dir=RAIZ / "ferramentas" / "testes") as pasta:
            caminho = Path(pasta) / "cena.xml"
            caminho.write_bytes(ET.tostring(cena, encoding="utf-8"))
            validar_cena(caminho)

    def test_exemplos_anteriores_e_atual(self):
        for nome in ("teste_dupla.xml", "teste_falas.xml", "teste_camera.xml", "teste_cortes.xml"):
            with self.subTest(nome=nome):
                validar_cena(RAIZ / "cenas" / nome)

    def test_planos_por_tipo_e_por_trecho(self):
        for plano in PLANOS:
            cena = deepcopy(self.cena)
            for campo in ("planoFala", "planoNarracao", "planoFinal"):
                cena.find("Camera").set(campo, plano)
            cena.find("Roteiro/Fala").set("plano", plano)
            cena.find("Roteiro/Narracao").set("plano", plano)
            with self.subTest(plano=plano):
                self.validar_documento(cena)
        for campo in ("automatica", "planoFala", "planoNarracao", "planoFinal"):
            del self.cena.find("Camera").attrib[campo]
        self.validar_documento(self.cena)

    def test_rejeita_opcoes_invalidas_de_corte(self):
        casos = [("Camera", "automatica", v) for v in ("1", "sim", "TRUE", "")]
        casos += [("Camera", campo, v) for campo in ("planoFala", "planoNarracao", "planoFinal")
                  for v in ("", "perto", "IMAGINARIO")]
        casos += [(trecho, "plano", v) for trecho in ("Roteiro/Fala", "Roteiro/Narracao")
                  for v in ("", "perto", "IMAGINARIO")]
        for caminho, campo, valor in casos:
            cena = deepcopy(self.cena)
            cena.find(caminho).set(campo, valor)
            with self.subTest(caminho=caminho, campo=campo, valor=valor), self.assertRaises(ErroCena):
                self.validar_documento(cena)

    def test_eixo_do_primeiro_par_aceita_alvo_terceiro(self):
        terceiro = deepcopy(self.cena.find("Personagens/Personagem"))
        terceiro.set("id", "terceiro")
        terceiro.set("direita", "5")
        self.cena.find("Personagens").append(terceiro)
        self.cena.find("Roteiro/Fala").set("personagem", "terceiro")
        self.validar_documento(self.cena)
        self.cena.find("Camera").set("automatica", "false")
        self.validar_documento(self.cena)
        del self.cena.find("Camera").attrib["automatica"]
        self.validar_documento(self.cena)
        self.cena.remove(self.cena.find("Camera"))
        self.validar_documento(self.cena)
        self.cena.find("Roteiro/Fala").set("personagem", "ausente")
        with self.assertRaises(ErroCena):
            self.validar_documento(self.cena)

    def test_plano_na_narracao_preserva_separacao_do_dialogo(self):
        narracao = self.cena.find("Roteiro/Narracao")
        narracao.set("plano", "LONGE")
        narracao.set("narrador", "Narrador de São João")
        self.validar_documento(self.cena)
        for campo in ("personagem", "gesto"):
            cena = deepcopy(self.cena)
            cena.find("Roteiro/Narracao").set(campo, "homem")
            with self.subTest(campo=campo), self.assertRaises(ErroCena):
                self.validar_documento(cena)

    def test_amostra_preserva_atores_falas_gestos_e_tempos(self):
        anterior = ET.parse(RAIZ / "cenas" / "teste_camera.xml").getroot()
        self.assertEqual([e.attrib for e in anterior.find("Personagens")],
                         [e.attrib for e in self.cena.find("Personagens")])
        def falas(cena):
            return [(e.tag, e.get("personagem"), e.get("gesto"), e.get("duracaoSegundos"), (e.text or "").strip())
                    for e in cena.find("Roteiro")]
        self.assertEqual(falas(anterior), falas(self.cena))
        self.assertEqual([(e.get("personagem"), e.get("plano", "MEDIA")) for e in self.cena.findall("Roteiro/Fala")],
                         [("homem", "MEDIA"), ("mulher", "MEDIA"), ("homem", "MEDIA"), ("mulher", "PERTO"), ("homem", "PERTO")])


if __name__ == "__main__":
    unittest.main(verbosity=2)
