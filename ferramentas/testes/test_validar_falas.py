"""Contrato de falas: rejeições, duração e integridade do catálogo real do Menyoo."""

from copy import deepcopy
from decimal import Decimal
from pathlib import Path
import re
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
from validar_cena import ErroCena, validar_cena


class ContratoFalas(unittest.TestCase):
    def setUp(self):
        self.cena = ET.parse(RAIZ / "cenas" / "teste_falas.xml").getroot()

    def validar_documento(self, cena):
        with tempfile.NamedTemporaryFile(suffix=".xml", dir=RAIZ / "ferramentas" / "testes", delete=False) as arquivo:
            caminho = Path(arquivo.name)
            arquivo.write(ET.tostring(cena, encoding="utf-8"))
        try:
            validar_cena(caminho)
        finally:
            self.assertTrue(caminho.resolve().is_relative_to(RAIZ))
            caminho.unlink()

    def test_exemplos_e_tempos(self):
        validar_cena(RAIZ / "cenas" / "teste_dupla.xml")
        self.validar_documento(self.cena)
        tempos = []
        cursor = Decimal(0)
        for trecho in self.cena.find("Roteiro"):
            fim = cursor + Decimal(trecho.attrib["duracaoSegundos"])
            tempos.append((trecho.tag, cursor, fim))
            cursor = fim
        self.assertEqual(cursor, 36)
        self.assertEqual([t[1] for t in tempos], [0, 3, 9, 15, 21, 27, 33])
        self.assertEqual([t[2] for t in tempos], [3, 9, 15, 21, 27, 33, 36])
        self.assertEqual(tempos[0][0], "Narracao")
        self.assertEqual(tempos[-1][0], "Narracao")

    def test_duracoes_invalidas(self):
        for valor in ("0", "-1", "0.499", "30.001", "NaN", "Infinity", "1,5", "1e1", "0.5001"):
            with self.subTest(valor=valor):
                cena = deepcopy(self.cena)
                cena.find("Roteiro/Fala").set("duracaoSegundos", valor)
                with self.assertRaises(ErroCena):
                    self.validar_documento(cena)

    def test_milissegundos(self):
        for valor in ("0.5", "0.501", "1.234", "30", "1.0000"):
            with self.subTest(valor=valor):
                cena = deepcopy(self.cena)
                cena.find("Roteiro/Fala").set("duracaoSegundos", valor)
                self.validar_documento(cena)

    def test_referencias_e_intencoes(self):
        for campo, valor in (("personagem", "nao_existe"), ("gesto", "machucado"), ("gesto", "")):
            with self.subTest(campo=campo, valor=valor):
                cena = deepcopy(self.cena)
                cena.find("Roteiro/Fala").set(campo, valor)
                with self.assertRaises(ErroCena):
                    self.validar_documento(cena)

    def test_narracao_nao_declara_gesto_ou_personagem(self):
        for campo in ("gesto", "personagem"):
            with self.subTest(campo=campo):
                cena = deepcopy(self.cena)
                cena.find("Roteiro/Narracao").set(campo, "homem")
                with self.assertRaises(ErroCena):
                    self.validar_documento(cena)

    def test_limites_e_texto(self):
        for texto in ("", "   ", "x" * 351):
            with self.subTest(tamanho=len(texto)):
                cena = deepcopy(self.cena)
                cena.find("Roteiro/Fala").text = texto
                with self.assertRaises(ErroCena):
                    self.validar_documento(cena)
        cena = deepcopy(self.cena)
        ET.SubElement(cena.find("Roteiro/Fala"), "Inesperado")
        with self.assertRaises(ErroCena):
            self.validar_documento(cena)
        for quantidade in (0, 21):
            cena = deepcopy(self.cena)
            roteiro = cena.find("Roteiro")
            trecho = deepcopy(roteiro[1])
            roteiro.clear()
            for _ in range(quantidade):
                roteiro.append(deepcopy(trecho))
            with self.assertRaises(ErroCena):
                self.validar_documento(cena)

    def test_limite_total(self):
        roteiro = self.cena.find("Roteiro")
        trecho = deepcopy(roteiro[1])
        trecho.set("duracaoSegundos", "30")
        roteiro.clear()
        for _ in range(6):
            roteiro.append(deepcopy(trecho))
        self.validar_documento(self.cena)  # 180 segundos é aceito.
        trecho.set("duracaoSegundos", "0.5")
        roteiro.append(trecho)
        with self.assertRaises(ErroCena):
            self.validar_documento(self.cena)

    def test_catalogo_tem_pares_reais_no_menyoo(self):
        caminho = Path("C:/Program Files (x86)/Steam/steamapps/common/Grand Theft Auto V/menyooStuff/PedAnimList.txt")
        if not caminho.is_file():
            self.skipTest("Lista local do Menyoo ausente; não comprovar identificadores.")
        lista = caminho.read_text(encoding="utf-8-sig")
        catalogo = ET.parse(RAIZ / "catalogos" / "gestos_fala.xml").getroot()
        self.assertEqual({g.attrib["id"] for g in catalogo.findall("Gesto")},
                         {"neutro", "explicando", "negando", "implorando", "irritado"})
        for recurso in list(catalogo.findall("Gesto/Homem")) + list(catalogo.findall("Gesto/Mulher")) + list(catalogo.findall("Facial")):
            par = re.escape(recurso.attrib["dicionario"]) + r"\s+" + re.escape(recurso.attrib["animacao"]) + r"(?=\s|$)"
            with self.subTest(par=par):
                self.assertRegex(lista, par)


if __name__ == "__main__":
    unittest.main(verbosity=2)
