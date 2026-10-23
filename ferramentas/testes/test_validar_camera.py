"""Contrato da câmera e identificação do narrador, sem simular teste visual."""

from copy import deepcopy
from pathlib import Path
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
from validar_cena import ErroCena, PLANOS, validar_cena


class ContratoCamera(unittest.TestCase):
    def setUp(self):
        self.cena = ET.parse(RAIZ / "cenas" / "teste_camera.xml").getroot()

    def validar_documento(self, cena):
        with tempfile.TemporaryDirectory(prefix="camera-", dir=RAIZ / "ferramentas" / "testes") as pasta:
            caminho = Path(pasta) / "cena.xml"
            caminho.write_bytes(ET.tostring(cena, encoding="utf-8"))
            validar_cena(caminho)

    def test_compatibilidade_e_camera_sem_roteiro(self):
        for nome in ("teste_dupla.xml", "teste_falas.xml", "teste_camera.xml"):
            validar_cena(RAIZ / "cenas" / nome)
        self.cena.remove(self.cena.find("Roteiro"))
        self.validar_documento(self.cena)

    def test_oito_planos_e_ajustes_finitos(self):
        for plano in PLANOS:
            with self.subTest(plano=plano):
                cena = deepcopy(self.cena)
                camera = cena.find("Camera")
                camera.set("plano", plano)
                camera.set("deslocLado", "-2")
                camera.set("deslocAltura", "2")
                camera.set("aproximar", "0.25")
                camera.set("inverterEixo", "true")
                camera.set("transicaoSuave", "true")
                self.validar_documento(cena)

    def test_camera_rejeita_valores_e_referencias_invalidas(self):
        casos = [("plano", "MUITO_PERTO"), ("personagem", "ausente"),
                 ("inverterEixo", "1"), ("transicaoSuave", "sim")]
        casos += [(campo, valor) for campo in ("deslocLado", "deslocAltura", "aproximar")
                  for valor in ("NaN", "Infinity", "-2.01", "2.01", "0,5")]
        for campo, valor in casos:
            with self.subTest(campo=campo, valor=valor):
                cena = deepcopy(self.cena)
                cena.find("Camera").set(campo, valor)
                with self.assertRaises(ErroCena):
                    self.validar_documento(cena)

    def test_camera_em_terceiro_e_quinto_ator_aceita_referencias_validas(self):
        terceiro = deepcopy(self.cena.find("Personagens/Personagem"))
        terceiro.set("id", "terceiro")
        terceiro.set("direita", "5")
        self.cena.find("Personagens").append(terceiro)
        self.cena.find("Camera").set("personagem", "terceiro")
        self.validar_documento(self.cena)
        for indice, direita in ((4, "6"), (5, "7")):
            ator = deepcopy(terceiro)
            ator.set("id", "ator_" + str(indice))
            ator.set("direita", direita)
            self.cena.find("Personagens").append(ator)
        self.cena.find("Camera").set("personagem", "ator_5")
        self.validar_documento(self.cena)
        self.cena.find("Camera").set("personagem", "ausente")
        with self.assertRaises(ErroCena):
            self.validar_documento(self.cena)
        self.cena.find("Camera").set("personagem", "ator_5")
        sexto = deepcopy(terceiro)
        sexto.set("id", "sexto")
        sexto.set("direita", "8")
        self.cena.find("Personagens").append(sexto)
        with self.assertRaises(ErroCena):
            self.validar_documento(self.cena)

    def test_ordem_e_atributos_da_camera(self):
        camera = self.cena.find("Camera")
        self.cena.remove(camera)
        self.cena.append(camera)
        with self.assertRaises(ErroCena):
            self.validar_documento(self.cena)
        for alteracao in ("falta", "extra", "filho"):
            cena = deepcopy(ET.parse(RAIZ / "cenas" / "teste_camera.xml").getroot())
            camera = cena.find("Camera")
            if alteracao == "falta":
                del camera.attrib["plano"]
            elif alteracao == "extra":
                camera.set("fovInventado", "20")
            else:
                ET.SubElement(camera, "Inesperado")
            with self.subTest(alteracao=alteracao), self.assertRaises(ErroCena):
                self.validar_documento(cena)

    def test_narrador_opcional_acentuado_e_sem_ator(self):
        narracao = self.cena.find("Roteiro/Narracao")
        narracao.set("narrador", "Narrador de São João")
        self.validar_documento(self.cena)
        del narracao.attrib["narrador"]
        self.validar_documento(self.cena)
        for nome in ("", "   ", "x" * 49):
            narracao.set("narrador", nome)
            with self.subTest(nome=nome), self.assertRaises(ErroCena):
                self.validar_documento(self.cena)

    def test_fala_nao_se_torna_narracao(self):
        self.cena.find("Roteiro/Fala").set("narrador", "Narrador")
        with self.assertRaises(ErroCena):
            self.validar_documento(self.cena)


if __name__ == "__main__":
    unittest.main(verbosity=2)
