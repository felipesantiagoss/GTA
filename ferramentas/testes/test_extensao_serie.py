"""Contrato0–5 atores, alvos/pausas e barreira de visuais, sem executar GTA."""
from copy import deepcopy
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
from validar_cena import ErroCena, validar_cena
from validar_json import validar_cena_json


class ExtensaoSerieTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="temp_extensao_", dir=RAIZ)
        self.addCleanup(self.temp.cleanup)
        self.raiz = Path(self.temp.name)
        self.cats = self.raiz / "catalogos"
        shutil.copytree(RAIZ / "catalogos", self.cats)
        self.base = json.loads((RAIZ / "cenas" / "teste_json.json").read_text(encoding="utf-8"))
        personagens = self.ler_personagens()
        original = deepcopy(next(p for p in personagens["personagens"]
                                 if p["codigo"] == self.base["elenco"][0]["personagem"]))
        for i in range(1, 7):
            ator = deepcopy(original)
            ator.update(codigo=f"EXTRA_TESTE_{i}", nome=f"Extra técnico{i}")
            personagens["personagens"].append(ator)
        self.salvar_personagens(personagens)

    def ler_personagens(self):
        return json.loads((self.cats / "personagens.json").read_text(encoding="utf-8"))

    def salvar_personagens(self, dados):
        (self.cats / "personagens.json").write_text(json.dumps(dados, ensure_ascii=False), encoding="utf-8")

    def validar_json(self, cena):
        arquivo = self.raiz / (cena["id"] + ".json")
        arquivo.write_text(json.dumps(cena, ensure_ascii=False), encoding="utf-8")
        return validar_cena_json(arquivo, self.cats)

    def cena_quantidade(self, quantidade):
        cena = deepcopy(self.base)
        cena["elenco"] = [{"personagem": f"EXTRA_TESTE_{i + 1}",
                           "marca": {"direita": -4 + i * 1.5, "frente": 0}, "olharPara": None}
                          for i in range(quantidade)]
        cena["camera"] = {"modo": "automatico", "planoInicial": "MEDIA" if quantidade else "LONGE",
                          "personagem": "EXTRA_TESTE_1" if quantidade else None}
        cena["roteiro"] = [{"tipo": "narracao", "texto": "Narração técnica, sem boca.", "duracaoSegundos": 5}]
        cena["acoes"] = []
        return cena

    def validar_xml(self, cena):
        arquivo = self.raiz / "tecnico.xml"
        arquivo.write_bytes(ET.tostring(cena, encoding="utf-8"))
        validar_cena(arquivo)

    def xml_quantidade(self, quantidade):
        cena = ET.parse(RAIZ / "cenas" / "teste_camera.xml").getroot()
        elenco = cena.find("Personagens")
        original = deepcopy(elenco[0])
        elenco.clear()
        for i in range(quantidade):
            ator = deepcopy(original)
            ator.set("id", f"ator_{i}")
            ator.set("direita", str(-4 + i * 1.5))
            del ator.attrib["olharPara"]
            elenco.append(ator)
        camera = cena.find("Camera")
        if quantidade:
            camera.set("personagem", "ator_0")
        else:
            del camera.attrib["personagem"]
            for campo in ("plano", "planoNarracao", "planoFinal"):
                camera.set(campo, "LONGE")
        roteiro = cena.find("Roteiro")
        roteiro.clear()
        ET.SubElement(roteiro, "Narracao", {"duracaoSegundos": "5"}).text = "Narração sem boca."
        return cena

    def test_compatibilidade_dos_dois_atores_aprovados(self):
        self.validar_json(deepcopy(self.base))
        for nome in ("teste_dupla", "teste_falas", "teste_camera", "teste_cortes", "teste_ambiente"):
            validar_cena(RAIZ / "cenas" / (nome + ".xml"))

    def test_quantidades_zero_a_cinco_e_seis_rejeitado_json_xml(self):
        for n in range(6):
            with self.subTest(quantidade=n):
                self.validar_json(self.cena_quantidade(n))
                self.validar_xml(self.xml_quantidade(n))
        with self.assertRaisesRegex(ErroCena, "0 a 5"):
            self.validar_json(self.cena_quantidade(6))
        with self.assertRaisesRegex(ErroCena, "0 a 5"):
            self.validar_xml(self.xml_quantidade(6))

    def test_solo_olhar_ausente_null_mas_nao_si_proprio(self):
        cena = self.cena_quantidade(1)
        self.validar_json(cena)
        del cena["elenco"][0]["olharPara"]
        self.validar_json(cena)
        cena["elenco"][0]["olharPara"] = "EXTRA_TESTE_1"
        with self.assertRaisesRegex(ErroCena, "olharPara"):
            self.validar_json(cena)
        cena["elenco"][0]["olharPara"] = "AUSENTE"
        with self.assertRaisesRegex(ErroCena, "olharPara"):
            self.validar_json(cena)

    def test_camera_fala_e_corte_podem_usar_quinto_ator(self):
        cena = self.cena_quantidade(5)
        cena["camera"]["personagem"] = "EXTRA_TESTE_5"
        cena["roteiro"] = [{"tipo": "fala", "personagem": "EXTRA_TESTE_5", "texto": "Quinto ator.", "gesto": "neutro", "duracaoSegundos": 2}]
        self.validar_json(cena)
        cena["camera"].update(modo="programado", cortes=[{"inicioSegundos": 0, "plano": "MEDIA", "personagem": "EXTRA_TESTE_5"}])
        self.validar_json(cena)
        cena["camera"]["cortes"][0]["personagem"] = "AUSENTE"
        with self.assertRaisesRegex(ErroCena, "personagem"):
            self.validar_json(cena)

    def test_sem_elenco_apenas_planos_ambientais_e_alvo_null(self):
        for plano in ("LONGE", "MEDIA", "BAIXA", "ALTA"):
            cena = self.cena_quantidade(0)
            cena["camera"].update(planoInicial=plano, planoNarracao=plano, planoFinal=plano,
                                  modo="programado", cortes=[{"inicioSegundos": 0, "plano": plano, "personagem": None}])
            cena["roteiro"][0]["plano"] = plano
            self.validar_json(cena)
        for campo in ("planoInicial", "planoFala", "planoNarracao", "planoFinal"):
            for invalido in ("DOIS", "PERTO", "ATRAS", "OLHOS"):
                cena = self.cena_quantidade(0)
                cena["camera"][campo] = invalido
                with self.subTest(campo=campo, plano=invalido), self.assertRaisesRegex(ErroCena, "sem elenco"):
                    self.validar_json(cena)
        cena = self.cena_quantidade(0)
        cena["camera"]["personagem"] = "EXTRA_TESTE_1"
        with self.assertRaisesRegex(ErroCena, "null"):
            self.validar_json(cena)
        cena = self.cena_quantidade(1)
        cena["camera"]["personagem"] = None
        with self.assertRaisesRegex(ErroCena, "ator existente"):
            self.validar_json(cena)

    def test_vinheta_id_metadata_e_nome_arquivo(self):
        cena = self.cena_quantidade(4)
        cena.update(id="VINHETA_01", episodio=0, numero=0)
        self.validar_json(cena)
        for campo in ("episodio", "numero"):
            invalida = deepcopy(cena)
            invalida[campo] = 1
            with self.assertRaisesRegex(ErroCena, "episodio=0"):
                self.validar_json(invalida)

    def test_pausa_permite_alvo_mas_proibe_texto_voz_boca_gesto(self):
        cena = self.cena_quantidade(2)
        pausa = {"tipo": "pausa", "duracaoSegundos": 4, "plano": "PERTO", "personagem": "EXTRA_TESTE_2"}
        cena["roteiro"] = [pausa]
        self.validar_json(cena)
        for campo, valor in (("texto", "não deve aparecer"), ("gesto", "neutro"), ("narrador", "Narrador"),
                              ("boca", False), ("legenda", "")):
            invalida = deepcopy(cena)
            invalida["roteiro"][0][campo] = valor
            with self.subTest(campo=campo), self.assertRaisesRegex(ErroCena, "desconhecido"):
                self.validar_json(invalida)
        cena["roteiro"][0]["personagem"] = "AUSENTE"
        with self.assertRaisesRegex(ErroCena, "ator existente"):
            self.validar_json(cena)
        cena = self.cena_quantidade(0)
        cena["roteiro"] = [{"tipo": "pausa", "duracaoSegundos": 4, "plano": "LONGE", "personagem": None}]
        self.validar_json(cena)

    def test_pausa_xml_vazia_referencia_valida_e_sem_texto(self):
        cena = self.xml_quantidade(1)
        roteiro = cena.find("Roteiro")
        roteiro.clear()
        pausa = ET.SubElement(roteiro, "Pausa", {"duracaoSegundos": "4", "plano": "PERTO", "personagem": "ator_0"})
        self.validar_xml(cena)
        pausa.text = "silêncio"
        with self.assertRaisesRegex(ErroCena, "não aceita texto"):
            self.validar_xml(cena)
        pausa.text = None
        pausa.set("personagem", "ausente")
        with self.assertRaisesRegex(ErroCena, "personagem existente"):
            self.validar_xml(cena)

    def test_pausa_conta_no_limite_e_na_sobreposicao_da_fala(self):
        cena = deepcopy(self.base)
        ator = cena["elenco"][0]["personagem"]
        fala = {"tipo": "fala", "personagem": ator, "texto": "Texto técnico.", "gesto": "neutro", "duracaoSegundos": 2}
        cena["roteiro"] = [deepcopy(fala), {"tipo": "pausa", "duracaoSegundos": 3}, deepcopy(fala)]
        cena["acoes"] = [{"tipo": "andarAte", "personagem": ator, "inicioSegundos": 2, "duracaoSegundos": 3,
                           "destino": {"direita": -1, "frente": 1}}]
        self.validar_json(cena)
        cena["acoes"][0]["inicioSegundos"] = 1
        with self.assertRaisesRegex(ErroCena, "sobreposta"):
            self.validar_json(cena)
        cena["acoes"] = []
        cena["roteiro"] = [{"tipo": "pausa", "duracaoSegundos": 30}] * 7
        with self.assertRaisesRegex(ErroCena, "180"):
            self.validar_json(cena)

    def test_colisoes_referencias_e_tipos_rigidos_preservados(self):
        cena = self.cena_quantidade(5)
        cena["elenco"][4]["marca"] = deepcopy(cena["elenco"][0]["marca"])
        with self.assertRaisesRegex(ErroCena, "próximos"):
            self.validar_json(cena)
        for valor in (True, "4", 0.0001):
            cena = self.cena_quantidade(0)
            cena["roteiro"] = [{"tipo": "pausa", "duracaoSegundos": valor}]
            with self.subTest(duracao=valor), self.assertRaises(ErroCena):
                self.validar_json(cena)

    def test_visual_freemode_pronto_exige_evidencia_real_sem_aprovar_catalogo(self):
        personagens = self.ler_personagens()
        ator = next(p for p in personagens["personagens"] if p["codigo"] == "EXTRA_TESTE_1")
        ator["modelo"] = "mp_f_freemode_01"
        self.salvar_personagens(personagens)
        with self.assertRaisesRegex(ErroCena, "freemode pronto exige"):
            self.validar_json(self.cena_quantidade(1))
        ator.update(visualArquivo="EXTRA_TESTE_1.xml", visualSHA256="0" * 64)
        self.salvar_personagens(personagens)
        with self.assertRaisesRegex(ErroCena, "visual"):
            self.validar_json(self.cena_quantidade(1))
        ator["pronto"] = False
        self.salvar_personagens(personagens)
        with self.assertRaisesRegex(ErroCena, "ainda não pronto"):
            self.validar_json(self.cena_quantidade(1))

    def test_metadados_visuais_pareados_basename_hash(self):
        for arquivo, digest in (("../EXTRA_TESTE_1.xml", "0" * 64), ("OUTRO.xml", "0" * 64),
                                ("EXTRA_TESTE_1.xml", "xyz"), ("EXTRA_TESTE_1.xml", True)):
            personagens = self.ler_personagens()
            ator = next(p for p in personagens["personagens"] if p["codigo"] == "EXTRA_TESTE_1")
            ator.update(visualArquivo=arquivo, visualSHA256=digest)
            self.salvar_personagens(personagens)
            with self.subTest(arquivo=arquivo, digest=digest), self.assertRaisesRegex(ErroCena, "visual"):
                self.validar_json(self.cena_quantidade(1))
        ator.pop("visualSHA256")
        self.salvar_personagens(personagens)
        with self.assertRaisesRegex(ErroCena, "juntos"):
            self.validar_json(self.cena_quantidade(1))


if __name__ == "__main__":
    unittest.main()
