"""Validação CPU de direção; fontes temporárias não são visuais testados no GTA."""
from copy import deepcopy
from decimal import Decimal
import hashlib
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
from validar_cena import ErroCena
from validar_fila import validar_fila
from validar_json import _corpos, _fontes_corpos, validar_cena_json


class DirecaoTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="temp_direcao_", dir=RAIZ)
        self.addCleanup(self.temp.cleanup)
        self.raiz = Path(self.temp.name)
        self.cenas = self.raiz / "cenas"
        self.cenas.mkdir()
        self.catalogos = self.raiz / "catalogos"
        self.catalogos.mkdir()
        for nome in ("personagens.json", "locais.json", "animacoes.json", "gestos_fala.xml"):
            shutil.copyfile(RAIZ / "catalogos" / nome, self.catalogos / nome)
        self.cena = json.loads((RAIZ / "cenas/teste_json.json").read_text(encoding="utf-8"))
        self.cena.update(encenacao="serie_v1", direcao=[], acoes=[], roteiro=[
            {"tipo": "narracao", "texto": "Teste de formato, sem alegar resultado visual.", "duracaoSegundos": 10}
        ])
        original = json.loads((RAIZ / "catalogos/corpos_serie.json").read_text(encoding="utf-8"))
        self.recursos = [deepcopy(next(item for item in original["animacoes"] if item["codigo"] == nome))
                         for nome in ("abraco_a", "abraco_b", "celular_texto_f", "andar_salto_candidato")]
        self.fonte = self.raiz / "PedAnimList.txt"
        self.fonte.write_text("\n".join(r["dicionario"] + " " + r["animacao"] for r in self.recursos) + "\n", encoding="utf-8")
        digest = hashlib.sha256(self.fonte.read_bytes()).hexdigest().upper()
        for numero, recurso in enumerate(self.recursos, 1):
            recurso["origem"].update(arquivo=str(self.fonte), linha=numero, sha256=digest)
        self.corpos = {"versao": 1, "estado": "não testado", "animacoes": self.recursos}
        self.gravar_corpos()

    def gravar_corpos(self):
        (self.catalogos / "corpos_serie.json").write_text(json.dumps(self.corpos, ensure_ascii=False), encoding="utf-8")

    def validar(self, cena=None):
        cena = self.cena if cena is None else cena
        caminho = self.cenas / (cena["id"] + ".json")
        caminho.write_text(json.dumps(cena, ensure_ascii=False), encoding="utf-8")
        return validar_cena_json(caminho, self.catalogos)

    @staticmethod
    def clipe(ator="TESTE_HOMEM", recurso="abraco_a", inicio=0, duracao=2, **extras):
        return dict(tipo="clipe", personagem=ator, recurso=recurso, inicioSegundos=inicio, duracaoSegundos=duracao, **extras)

    def recusa(self, mensagem=None):
        if mensagem:
            with self.assertRaisesRegex(ErroCena, mensagem):
                self.validar()
        else:
            with self.assertRaises(ErroCena):
                self.validar()

    def test_quatro_tipos_recursos_literal_e_sem_mutar_dados(self):
        self.cena["direcao"] = [self.clipe(),
            dict(tipo="andar", personagem="TESTE_HOMEM", inicioSegundos=2, duracaoSegundos=2, destino={"direita": -2, "frente": 3, "altura": 0}, recurso="andar_salto_candidato", velocidade=.8),
            dict(tipo="virar", personagem="TESTE_HOMEM", inicioSegundos=4, duracaoSegundos=1, alvo="TESTE_MULHER"),
            dict(tipo="celular", personagem="TESTE_HOMEM", inicioSegundos=5, duracaoSegundos=2, recurso="celular_texto_f", loop=False, corpoInteiro=False, manterPoseFinal=True)]
        antes = deepcopy(self.cena)
        self.assertEqual(self.validar(), json.loads(json.dumps(antes), parse_float=Decimal))
        self.assertEqual(self.cena, antes)

    def test_serie_exige_lista_e_outras_encenacoes_proibem_direcao(self):
        self.validar()
        del self.cena["direcao"]
        self.recusa("direcao")
        self.cena["direcao"] = []
        for valor in (None, "outra", "pedido_v1", "ensaio_coreografia_v1"):
            with self.subTest(encenacao=valor):
                self.cena["encenacao"] = valor
                self.recusa()
        del self.cena["encenacao"]
        self.recusa("direcao")

    def test_tipos_campos_e_booleanos_estritos(self):
        valido = self.clipe()
        for campo, valor in (("tipo", "task"), ("personagem", []), ("recurso", "ABRACO_A"), ("recurso", 1),
                             ("loop", 1), ("corpoInteiro", "false"), ("manterPoseFinal", None),
                             ("dicionario", "mp_ped_interaction"), ("destino", {"direita": 0, "frente": 0}), ("alvo", "TESTE_MULHER")):
            with self.subTest(campo=campo, valor=valor):
                self.cena["direcao"] = [dict(valido, **{campo: valor})]
                self.recusa()
        for campo in valido:
            self.cena["direcao"] = [dict(valido)]
            del self.cena["direcao"][0][campo]
            self.recusa()

    def test_tempos_finitos_milissegundos_e_fronteiras_180(self):
        self.cena["roteiro"] = []
        self.cena["direcao"] = [self.clipe(duracao=180)]
        self.validar()
        self.cena["direcao"] = [self.clipe(inicio=179.9, duracao=.1)]
        self.validar()
        for inicio, duracao in ((-.001, 2), (180, .1), (0, .099), (0, 180.001), (.0001, 1), (True, 1), (0, "2"), (0, float("inf")), (0, float("nan"))):
            with self.subTest(inicio=inicio, duracao=duracao):
                self.cena["direcao"] = [self.clipe(inicio=inicio, duracao=duracao)]
                self.recusa()

    def test_cortes_usam_fim_da_direcao_sem_alongar_tomada(self):
        self.cena["roteiro"] = []
        self.cena["direcao"] = [self.clipe(inicio=4, duracao=2)]
        self.cena["camera"].update(modo="programado", cortes=[
            {"inicioSegundos": 0, "plano": "DOIS", "personagem": "TESTE_HOMEM"},
            {"inicioSegundos": 5.999, "plano": "MEDIA", "personagem": "TESTE_HOMEM"}])
        self.validar()
        self.cena["camera"]["cortes"][1]["inicioSegundos"] = 6
        self.recusa("cortes")

    def test_sobreposicao_mesmo_ator_legado_e_direcao(self):
        self.cena["direcao"] = [self.clipe(), self.clipe(inicio=1)]
        self.recusa("mesmo ator")
        self.cena["direcao"] = [self.clipe(), self.clipe(inicio=2)]
        self.validar()
        self.cena["acoes"] = [{"tipo": "esperar", "personagem": "TESTE_HOMEM", "inicioSegundos": 1, "duracaoSegundos": 2}]
        self.recusa("mesmo ator")
        self.cena["acoes"][0]["personagem"] = "TESTE_MULHER"
        self.validar()

    def test_fala_proibe_corpo_celular_e_andar_mas_permite_virar(self):
        self.cena["roteiro"] = [{"tipo": "fala", "personagem": "TESTE_HOMEM", "gesto": "neutro", "texto": "Teste de fala.", "duracaoSegundos": 2}]
        for acao in (self.clipe(corpoInteiro=False),
                     dict(tipo="celular", personagem="TESTE_HOMEM", inicioSegundos=0, duracaoSegundos=2, recurso="celular_texto_f"),
                     dict(tipo="andar", personagem="TESTE_HOMEM", inicioSegundos=0, duracaoSegundos=2, destino={"direita": 1, "frente": 2})):
            self.cena["direcao"] = [acao]
            self.recusa("fala")
        self.cena["direcao"] = [dict(tipo="virar", personagem="TESTE_HOMEM", inicioSegundos=0, duracaoSegundos=2, alvo="TESTE_MULHER")]
        self.validar()
        self.cena["direcao"] = [self.clipe(ator="TESTE_MULHER")]
        self.validar()

    def test_grupos_pareados_exatamente_dois_mesmos_tempos_dict_flags(self):
        valido = [self.clipe(grupoPareado="abraco_01"), self.clipe(ator="TESTE_MULHER", recurso="abraco_b", grupoPareado="abraco_01")]
        self.cena["direcao"] = deepcopy(valido)
        self.validar()
        for campo, valor in (("inicioSegundos", .1), ("duracaoSegundos", 3), ("recurso", "celular_texto_f"), ("loop", False), ("manterPoseFinal", False), ("corpoInteiro", False), ("grupoPareado", "á")):
            with self.subTest(campo=campo):
                self.cena["direcao"] = deepcopy(valido)
                self.cena["direcao"][1][campo] = valor
                self.recusa()
        self.cena["direcao"] = valido[:1]
        self.recusa("2 atores")
        self.cena["direcao"] = [dict(tipo="celular", personagem="TESTE_HOMEM", recurso="celular_texto_f", inicioSegundos=0, duracaoSegundos=2, grupoPareado="par")]
        self.recusa("grupoPareado")

    def test_andar_valida_marca_velocidade_tipo_de_recurso(self):
        valido = dict(tipo="andar", personagem="TESTE_HOMEM", inicioSegundos=0, duracaoSegundos=2, destino={"direita": 10, "frente": -10, "altura": 2}, velocidade=2)
        self.cena["direcao"] = [valido]
        self.validar()
        for extras in ({"recurso": "abraco_a"}, {"velocidade": .19}, {"velocidade": True}, {"loop": True},
                       {"destino": {"direita": 10.001, "frente": 0}}, {"destino": {"direita": 0, "frente": 0, "altura": 2.01}}):
            self.cena["direcao"] = [dict(valido, **extras)]
            self.recusa()
        self.cena["direcao"] = [self.clipe(recurso="andar_salto_candidato")]
        self.recusa("tipo")

    def test_virar_alvo_distinto_e_sem_campos_de_clipe(self):
        valido = dict(tipo="virar", personagem="TESTE_HOMEM", inicioSegundos=0, duracaoSegundos=2, alvo="TESTE_MULHER")
        for extras in ({"alvo": "TESTE_HOMEM"}, {"alvo": "DINJER"}, {"alvo": []}, {"recurso": "abraco_a"}, {"manterPoseFinal": True}):
            self.cena["direcao"] = [dict(valido, **extras)]
            self.recusa()

    def test_catalogo_corpos_estrito_codigo_tipo_origem_restricoes(self):
        self.cena["direcao"] = [self.clipe()]
        original = deepcopy(self.corpos)
        for alvo, campo, valor in (("root", "extra", 1), ("root", "versao", True), ("entry", "codigo", "ABRACO"),
                                   ("entry", "tipo", "task"), ("entry", "dicionario", "nome com espaço"),
                                   ("entry", "restricoes", [False]), ("origin", "linha", 1.0),
                                   ("origin", "sha256", "F" * 63), ("origin", "texto", "outro clipe"), ("origin", "extra", True)):
            with self.subTest(alvo=alvo, campo=campo):
                self.corpos = deepcopy(original)
                obj = self.corpos if alvo == "root" else self.corpos["animacoes"][0] if alvo == "entry" else self.corpos["animacoes"][0]["origem"]
                obj[campo] = valor
                self.gravar_corpos()
                self.recusa()
        self.corpos = deepcopy(original)
        self.corpos["animacoes"].append(deepcopy(self.corpos["animacoes"][0]))
        self.gravar_corpos()
        self.recusa("repetido")

    def test_fonte_hash_linha_path_e_bom_reais_antes_validar(self):
        self.cena["direcao"] = [self.clipe()]
        self.validar()
        origem = self.recursos[0]["origem"]
        original = deepcopy(origem)
        for chave, valor in (("sha256", "0" * 64), ("linha", 2), ("linha", 500000), ("arquivo", "PedAnimList.txt"), ("arquivo", str(self.fonte) + ":stream")):
            with self.subTest(chave=chave, valor=valor):
                origem.update(original)
                origem[chave] = valor
                self.gravar_corpos()
                self.recusa()
        origem.update(original)
        self.fonte.write_bytes(b"\xef\xbb\xbf" + self.fonte.read_bytes())
        digest = hashlib.sha256(self.fonte.read_bytes()).hexdigest().upper()
        for recurso in self.recursos:
            recurso["origem"]["sha256"] = digest
        self.gravar_corpos()
        self.validar()
        self.fonte.unlink()
        self.recusa("fonte")

    def test_limites_64_direcoes_e_vinheta_sem_boca(self):
        self.cena["roteiro"] = []
        self.cena["direcao"] = [self.clipe(inicio=i, duracao=.1) for i in range(64)]
        self.validar()
        self.cena["direcao"].append(self.clipe(inicio=64, duracao=.1))
        self.recusa("64")
        self.cena.update(id="VINHETA_01", direcao=[], roteiro=[{"tipo": "narracao", "texto": "Vinheta em off.", "duracaoSegundos": 2}])
        self.validar()
        self.cena["elenco"] = []
        self.cena["camera"] = {"modo": "automatico", "planoInicial": "LONGE", "personagem": None}
        self.validar()
        self.cena["direcao"] = [self.clipe()]
        self.recusa("elenco")

    def coreografia(self):
        cena = json.loads((RAIZ / "producao/testes/teste_coreografia.json").read_text(encoding="utf-8"))
        # Somente catálogo temporário: os testes não ativam o elenco de produção.
        path = self.catalogos / "personagens.json"
        dados = json.loads(path.read_text(encoding="utf-8"))
        for entrada in dados["personagens"]:
            if entrada["codigo"] in ("CAPANGA_1", "DJANIRA_POBRE"):
                entrada.update(pronto=True, modelo="a_f_y_business_01" if entrada["codigo"] == "DJANIRA_POBRE" else "s_m_m_highsec_01")
                entrada.pop("visualArquivo", None)
                entrada.pop("visualSHA256", None)
        path.write_text(json.dumps(dados, ensure_ascii=False), encoding="utf-8")
        return cena

    def test_coreografia_fixture_estrita_e_fila_tecnica_validada(self):
        self.cena = self.coreografia()
        self.validar()
        fila = {"versao": 1, "episodio": 0, "nome": "Ensaio técnico", "cenas": ["teste_coreografia.json"]}
        path = self.cenas / "fila_teste.json"
        path.write_text(json.dumps(fila), encoding="utf-8")
        self.assertEqual(validar_fila(path, self.cenas, self.catalogos), fila)
        self.cena["roteiro"][0]["duracaoSegundos"] = 9
        self.validar_sem_checar(self.cena)
        with self.assertRaisesRegex(ErroCena, "teste_coreografia"):
            validar_fila(path, self.cenas, self.catalogos)

    def validar_sem_checar(self, dados):
        (self.cenas / (dados["id"] + ".json")).write_text(json.dumps(dados), encoding="utf-8")

    def test_coreografia_recusa_local_marcas_elenco_roteiro_e_acoes(self):
        original = self.coreografia()
        alteracoes = [lambda c: c["ambiente"].update(local="ROCA_GRAPESEED"),
                      lambda c: c["elenco"].reverse(),
                      lambda c: c["elenco"][0]["marca"].update(direita=-.6),
                      lambda c: c["elenco"][0]["marca"].update(frente=.1),
                      lambda c: c["roteiro"][0].update(duracaoSegundos=9),
                      lambda c: c["roteiro"].append(deepcopy(c["roteiro"][0])),
                      lambda c: c.update(acoes=[{"tipo": "esperar", "personagem": "CAPANGA_1", "inicioSegundos": 0, "duracaoSegundos": 1}]),
                      lambda c: c.pop("encenacao"), lambda c: c.update(direcao=[])]
        for alterar in alteracoes:
            self.cena = deepcopy(original)
            alterar(self.cena)
            self.recusa()
        self.cena = deepcopy(original)
        self.cena["episodio"] = 1
        self.recusa("id")

    def test_catalogo_canonico49_origens_reais_conferidas(self):
        recursos = _corpos(RAIZ / "catalogos")
        self.assertEqual(len(recursos), 49)
        _fontes_corpos(list(recursos.values()))

    def test_schemas_incluem_coreografia_e_quatro_tipos_direcao(self):
        schema = json.loads((RAIZ / "esquemas/cena-v1.schema.json").read_text(encoding="utf-8"))
        props = schema["properties"]
        self.assertEqual(set(props["encenacao"]["enum"]), {"serie_v1", "pedido_v1", "ensaio_coreografia_v1"})
        variantes = props["direcao"]["items"]["oneOf"]
        self.assertEqual({v["properties"]["tipo"]["const"] for v in variantes}, {"clipe", "andar", "virar", "celular"})
        self.assertTrue(all(v["additionalProperties"] is False for v in variantes))
        import re
        self.assertIsNotNone(re.fullmatch(props["id"]["pattern"], "teste_coreografia"))
        fila = json.loads((RAIZ / "esquemas/fila-v1.schema.json").read_text(encoding="utf-8"))
        pattern = fila["allOf"][0]["then"]["properties"]["cenas"]["items"]["pattern"]
        self.assertIsNotNone(re.fullmatch(pattern, "teste_coreografia.json"))


if __name__ == "__main__":
    unittest.main()
