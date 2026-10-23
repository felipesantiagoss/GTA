"""Contrato JSON: dados aprovados, referências e rejeições antes de montar."""

from copy import deepcopy
from decimal import Decimal
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
from validar_cena import ErroCena
from validar_json import validar_cena_json


class ContratoJson(unittest.TestCase):
    def setUp(self):
        self.cena = json.loads((RAIZ / "cenas" / "teste_json.json").read_text(encoding="utf-8"))
        self.temporario = tempfile.TemporaryDirectory(prefix="json-", dir=RAIZ / "ferramentas" / "testes")
        self.pasta = Path(self.temporario.name).resolve()
        self.assertTrue(self.pasta.is_relative_to(RAIZ))
        self.addCleanup(self.temporario.cleanup)
        self.catalogos = self.pasta / "catalogos"
        self.catalogos.mkdir()
        for nome in ("personagens.json", "locais.json", "animacoes.json", "gestos_fala.xml"):
            shutil.copyfile(RAIZ / "catalogos" / nome, self.catalogos / nome)

    def validar(self, cena=None, nome="teste_json.json", texto=None):
        caminho = self.pasta / nome
        caminho.write_text(texto if texto is not None else json.dumps(cena or self.cena, ensure_ascii=False), encoding="utf-8")
        return validar_cena_json(caminho, self.catalogos)

    def catalogo(self, nome):
        return json.loads((self.catalogos / nome).read_text(encoding="utf-8"))

    def gravar_catalogo(self, nome, valor):
        (self.catalogos / nome).write_text(json.dumps(valor, ensure_ascii=False), encoding="utf-8")

    def acao(self, tipo="esperar", ator="TESTE_HOMEM", inicio=0, duracao=1, **campos):
        return dict(tipo=tipo, personagem=ator, inicioSegundos=inicio, duracaoSegundos=duracao, **campos)

    def test_equivalencia_com_xml_aprovado_e_catalogos(self):
        dados = self.validar()
        xml = ET.parse(RAIZ / "cenas" / "teste_ambiente.xml").getroot()
        personagens = {p["codigo"]: p for p in self.catalogo("personagens.json")["personagens"]}
        for ator, original in zip(dados["elenco"], xml.find("Personagens")):
            entrada = personagens[ator["personagem"]]
            self.assertEqual((entrada["nome"], entrada["modelo"]), (original.attrib["nome"], original.attrib["modelo"]))
            for eixo in ("direita", "frente", "altura"):
                self.assertEqual(Decimal(str(ator["marca"][eixo])), Decimal(original.attrib[eixo]))
        self.assertEqual([t["texto"] for t in dados["roteiro"]], [(t.text or "").strip() for t in xml.find("Roteiro")])
        self.assertEqual([t.get("gesto") for t in dados["roteiro"]], [t.attrib.get("gesto") for t in xml.find("Roteiro")])
        self.assertEqual([t.get("plano") for t in dados["roteiro"]], [t.attrib.get("plano") for t in xml.find("Roteiro")])
        self.assertEqual(sum(t["duracaoSegundos"] for t in dados["roteiro"]), 36)
        self.assertEqual(dados["acoes"], [])

    def test_json_estrito_duplicadas_nan_comentarios_e_tamanho(self):
        texto = json.dumps(self.cena, ensure_ascii=False)
        invalidos = (texto.replace('"versao": 1', '"versao": 1, "versao": 1', 1),
                     texto.replace('"raioLimpeza": 40', '"raioLimpeza": NaN', 1),
                     texto.replace('"raioLimpeza": 40', '"raioLimpeza": Infinity', 1),
                     "// comentário\n" + texto, texto[:-1] + ",}", " " * 100001)
        for conteudo in invalidos:
            with self.subTest(conteudo=conteudo[:60]), self.assertRaises(ErroCena):
                self.validar(texto=conteudo)

    def test_identidade_metadados_e_basename(self):
        for cena, nome in ((dict(self.cena, episodio=True), "teste_json.json"),
                           (dict(self.cena, episodio=1), "teste_json.json"),
                           (dict(self.cena, id="EP01_C08", episodio=1, numero=7), "EP01_C08.json"),
                           (dict(self.cena, id="EP16_C01", episodio=15, numero=1), "EP16_C01.json"),
                           (self.cena, "outro.json")):
            with self.subTest(nome=nome, id=cena["id"]), self.assertRaises(ErroCena):
                self.validar(cena, nome)
        self.validar(dict(self.cena, id="EP15_C99", episodio=15, numero=99), "EP15_C99.json")

    def test_tipos_campos_desconhecidos_e_milissegundos(self):
        casos = (("ambiente", "raioLimpeza", True), ("ambiente", "hora", "24:00"),
                 ("ambiente", "x", 0), ("camera", "inverterEixo", 1),
                 ("camera", "planoFinal", "INVENTADO"))
        for objeto, campo, valor in casos:
            with self.subTest(objeto=objeto, campo=campo), self.assertRaises(ErroCena):
                cena = deepcopy(self.cena)
                cena[objeto][campo] = valor
                self.validar(cena)
        for valor in (True, "6", 0.5001):
            cena = deepcopy(self.cena)
            cena["roteiro"][1]["duracaoSegundos"] = valor
            with self.subTest(valor=valor), self.assertRaises(ErroCena):
                self.validar(cena)
        texto = json.dumps(self.cena).replace('"duracaoSegundos": 6.0', '"duracaoSegundos": 6.0000000000000000000000000001', 1)
        with self.assertRaisesRegex(ErroCena, "milissegundos"):
            self.validar(texto=texto)

    def test_catalogos_referencias_pendentes_e_modelos(self):
        cena = deepcopy(self.cena)
        cena["ambiente"]["local"] = "ROCA_GRAPESEED"
        self.validar(cena)  # Local pendente é permitido; nenhuma coordenada é fabricada.
        cena["ambiente"]["local"] = "LOCAL_AUSENTE"
        with self.assertRaisesRegex(ErroCena, "catálogo de locais"):
            self.validar(cena)
        dados = self.catalogo("personagens.json")
        ator = next(p for p in dados["personagens"] if p["codigo"] == "TESTE_HOMEM")
        ator["pronto"] = False
        self.gravar_catalogo("personagens.json", dados)
        with self.assertRaisesRegex(ErroCena, "não pronto"):
            self.validar()
        ator["pronto"] = True
        ator["modelo"] = "MODELO_INVALIDO"
        self.gravar_catalogo("personagens.json", dados)
        with self.assertRaisesRegex(ErroCena, "modelo inválido"):
            self.validar()

    def test_elenco_e_narracao_nao_recebem_capacidades_inventadas(self):
        for objeto, campo, valor in (("elenco", "modelo", "a_m_y_business_01"),
                                     ("narracao", "personagem", "TESTE_HOMEM"),
                                     ("narracao", "gesto", "neutro"),
                                     ("fala", "narrador", "Narrador")):
            cena = deepcopy(self.cena)
            alvo = cena["elenco"][0] if objeto == "elenco" else cena["roteiro"][0 if objeto == "narracao" else 1]
            alvo[campo] = valor
            with self.subTest(objeto=objeto, campo=campo), self.assertRaises(ErroCena):
                self.validar(cena)

    def test_gesto_alterado_nao_substitui_par_canonico(self):
        dados = self.catalogo("animacoes.json")
        gesto = next(g for g in dados["animacoes"] if g["codigo"] == "neutro")
        gesto["homem"]["animacao"] = "clip_inventado"
        self.gravar_catalogo("animacoes.json", dados)
        with self.assertRaisesRegex(ErroCena, "canônico"):
            self.validar()

    def test_texto_xml_interno_acentos_e_caracteres_proibidos(self):
        cena = deepcopy(self.cena)
        for caractere in ("\x00", "\x08", "\x0b", "\x0c", "\x1f", "\ufffe", "\uffff", "\ud800"):
            cena["roteiro"][1]["texto"] = "Texto" + caractere
            with self.subTest(codigo=ord(caractere)), self.assertRaisesRegex(ErroCena, r"roteiro\[1\]\.texto"):
                self.validar(texto=json.dumps(cena, ensure_ascii=True))
        cena["roteiro"][1]["texto"] = "Olá, João.\nOutro trecho\tcom acentos."
        self.validar(cena)

    def test_atores_proximos_e_fronteira_de_meio_metro(self):
        cena = deepcopy(self.cena)
        cena["elenco"][0]["marca"] = {"direita": 0, "frente": 0}
        cena["elenco"][1]["marca"] = {"direita": 0.499, "frente": 0.499}
        with self.assertRaisesRegex(ErroCena, "muito próximos"):
            self.validar(cena)
        cena["elenco"][1]["marca"]["direita"] = 0.5
        self.validar(cena)

    def test_acoes_programadas_fixture_sem_roteiro(self):
        cena = deepcopy(self.cena)
        cena["roteiro"] = []
        cena["acoes"] = [self.acao("andarAte", destino={"direita": 1, "frente": 0}),
                         self.acao("virarPara", inicio=1, alvo="TESTE_MULHER"),
                         self.acao("animacao", inicio=2, animacao="neutro"),
                         self.acao(inicio=3, duracao=3), self.acao(ator="TESTE_MULHER", duracao=6)]
        cena["camera"]["modo"] = "programado"
        cena["camera"]["cortes"] = [{"inicioSegundos": 0, "plano": "DOIS", "personagem": "TESTE_HOMEM"},
                                      {"inicioSegundos": 3, "plano": "PERTO", "personagem": "TESTE_MULHER"}]
        self.validar(cena)
        cena["acoes"] = []
        with self.assertRaisesRegex(ErroCena, "maior que zero"):
            self.validar(cena)

    def test_acoes_sobrepostas_e_interferencia_na_fala(self):
        cena = deepcopy(self.cena)
        cena["acoes"] = [self.acao(inicio=3)]  # Esperar pode coincidir com a fala.
        self.validar(cena)
        cena["acoes"].append(self.acao(inicio=3.5))
        with self.assertRaisesRegex(ErroCena, "simultâneas"):
            self.validar(cena)
        for tipo, extras in (("andarAte", {"destino": {"direita": 0, "frente": 1}}),
                              ("virarPara", {"alvo": "TESTE_MULHER"}),
                              ("animacao", {"animacao": "neutro"})):
            cena["acoes"] = [self.acao(tipo, inicio=3, **extras)]
            with self.subTest(tipo=tipo), self.assertRaisesRegex(ErroCena, "sobreposta à fala"):
                self.validar(cena)
        cena["acoes"] = [self.acao("animacao", ator="TESTE_MULHER", inicio=3, animacao="neutro")]
        self.validar(cena)  # Ação em outro ator não invade a fala do homem.

    def test_acoes_especificas_limites_e_catalogo(self):
        invalidas = [self.acao("combate"), self.acao(alvo="TESTE_MULHER"),
                    self.acao("andarAte"), self.acao("virarPara", alvo="TESTE_HOMEM"),
                    self.acao("animacao", animacao="inventada"), self.acao(inicio=179.999, duracao=1),
                    self.acao(inicio=True), self.acao(duracao=0.1001),
                    self.acao("andarAte", destino={"direita": 0, "frente": 1}, velocidade=2.01)]
        for acao in invalidas:
            cena = deepcopy(self.cena)
            cena["acoes"] = [acao]
            with self.subTest(acao=acao), self.assertRaises(ErroCena):
                self.validar(cena)

    def test_cortes_nao_alongam_cena_e_exigem_ordem_e_eixo(self):
        cena = deepcopy(self.cena)
        corte = {"inicioSegundos": 0, "plano": "DOIS", "personagem": "TESTE_HOMEM"}
        cena["camera"]["cortes"] = [corte]
        with self.assertRaisesRegex(ErroCena, "proibido"):
            self.validar(cena)
        cena["camera"]["modo"] = "programado"
        self.validar(cena)
        for inicio in (0, 36, 180, 0.0001):
            cena["camera"]["cortes"] = [corte, dict(corte, inicioSegundos=inicio)]
            with self.subTest(inicio=inicio), self.assertRaises(ErroCena):
                self.validar(cena)
        cena["camera"]["cortes"] = [dict(corte, inicioSegundos=1)]
        with self.assertRaises(ErroCena):
            self.validar(cena)


if __name__ == "__main__":
    unittest.main(verbosity=2)
