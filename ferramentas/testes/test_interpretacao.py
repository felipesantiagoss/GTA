"""Contrato de rosto/olhar e fonte real. Não executa animações nem altera o elenco."""
from copy import deepcopy
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
from validar_cena import ErroCena
from validar_json import FACIAIS, FONTE_FACIAL, SHA_FACIAL, _fontes_faciais, validar_cena_json


class InterpretacaoTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="temp_interpretacao_", dir=RAIZ)
        self.addCleanup(self.temp.cleanup)
        self.raiz = Path(self.temp.name)
        self.catalogos = self.raiz / "catalogos"
        self.catalogos.mkdir()
        for nome in ("personagens.json", "locais.json", "animacoes.json", "gestos_fala.xml", "corpos_serie.json"):
            shutil.copyfile(RAIZ / "catalogos" / nome, self.catalogos / nome)
        self.cena = json.loads((RAIZ / "cenas/teste_json.json").read_text(encoding="utf-8"))
        self.cena.update(encenacao="serie_v1", direcao=[], roteiro=[{"tipo": "narracao", "texto": "Teste de interpretação.", "duracaoSegundos": 10}], acoes=[])
        self.entrada = dict(personagem="TESTE_HOMEM", inicioSegundos=0, duracaoSegundos=10, expressao="normal")
        self.cena["interpretacao"] = [deepcopy(self.entrada)]

    def validar(self):
        path = self.raiz / (self.cena["id"] + ".json")
        path.write_text(json.dumps(self.cena, ensure_ascii=False), encoding="utf-8")
        return validar_cena_json(path, self.catalogos)

    def recusa(self, campo=None):
        with self.assertRaisesRegex(ErroCena, campo or "."):
            self.validar()

    def test_sete_expressoes_e14pares_reais_com_catalogo_coerente(self):
        _fontes_faciais()
        catalogo = json.loads((RAIZ / "catalogos/interpretacao_serie.json").read_text(encoding="utf-8"))
        self.assertEqual(catalogo["origem"]["arquivo"], FONTE_FACIAL)
        self.assertEqual(catalogo["origem"]["sha256"], SHA_FACIAL)
        self.assertEqual({x["codigo"] for x in catalogo["expressoes"]}, set(FACIAIS))
        for entrada in catalogo["expressoes"]:
            sufixo, clipe, homem, mulher = FACIAIS[entrada["codigo"]]
            for sexo, genero, linha in (("homem", "male", homem), ("mulher", "female", mulher)):
                self.assertEqual(entrada[sexo], {"dicionario": "facials@gen_" + genero + "@" + sufixo, "animacao": clipe, "linha": linha})
            self.assertEqual(entrada["estado"], "não testado")
            self.cena["interpretacao"][0]["expressao"] = entrada["codigo"]
            self.validar()

    def test_todos_tipos_de_olhar_e_referencia_ao_elenco(self):
        for tipo in ("padrao", "ator", "chao", "horizonte", "marca", "desvio"):
            olhar = {"tipo": tipo}
            if tipo == "ator": olhar["alvo"] = "TESTE_MULHER"
            if tipo == "marca": olhar["destino"] = {"direita": 10, "frente": -10, "altura": 2}
            self.cena["interpretacao"] = [dict(self.entrada, olhar=olhar)]
            self.validar()
        for olhar in ({"tipo": "ator", "alvo": "TESTE_HOMEM"}, {"tipo": "ator", "alvo": "DINJER"},
                      {"tipo": "ator", "alvo": []}, {"tipo": "ator"}, {"tipo": "marca"},
                      {"tipo": "marca", "destino": {"direita": 0, "frente": 0, "altura": 2.01}},
                      {"tipo": "horizonte", "alvo": "TESTE_MULHER"}, {"tipo": "chao", "destino": {"direita": 0, "frente": 0}}):
            self.cena["interpretacao"] = [dict(self.entrada, olhar=olhar)]
            self.recusa("olhar")

    def test_exige_efeito_e_recusa_campos_clipes_livres_tipos(self):
        for extras in ({"expressao": "triste"}, {"expressao": None}, {"expressao": True}, {"expressao": ""},
                       {"dicionario": "mp_facial"}, {"animacao": "mic_chatter"}, {"personagem": []},
                       {"olhar": {"tipo": "camera"}}, {"olhar": {"tipo": "desvio", "extra": True}}):
            self.cena["interpretacao"] = [dict(self.entrada, **extras)]
            self.recusa()
        self.cena["interpretacao"] = [{k: v for k, v in self.entrada.items() if k != "expressao"}]
        self.recusa("efeito|expressão")
        self.cena["interpretacao"][0]["olhar"] = {"tipo": "padrao"}
        self.recusa("expressão")
        self.cena["interpretacao"][0]["olhar"] = {"tipo": "desvio"}
        self.validar()

    def test_intervalos_finitos_milissegundos_mesmo_ator_sem_sobrepor(self):
        for campo, valor in (("inicioSegundos", -.1), ("inicioSegundos", .0001), ("inicioSegundos", True),
                             ("duracaoSegundos", .099), ("duracaoSegundos", "10"), ("duracaoSegundos", 180.001),
                             ("duracaoSegundos", float("nan")), ("duracaoSegundos", float("inf"))):
            self.cena["interpretacao"] = [dict(self.entrada, **{campo: valor})]
            self.recusa()
        self.cena["interpretacao"] = [dict(self.entrada, duracaoSegundos=180)]
        self.validar()
        self.cena["interpretacao"] = [dict(self.entrada, inicioSegundos=179.9, duracaoSegundos=.1)]
        self.validar()
        self.cena["interpretacao"][0]["inicioSegundos"] = 180
        self.recusa("180")
        self.cena["interpretacao"] = [dict(self.entrada, duracaoSegundos=2), dict(self.entrada, inicioSegundos=1, duracaoSegundos=2, olhar={"tipo": "desvio"})]
        self.recusa("simultâneas")
        self.cena["interpretacao"][1]["inicioSegundos"] = 2
        self.validar()
        self.cena["interpretacao"][1]["inicioSegundos"] = 1
        self.cena["interpretacao"][1]["personagem"] = "TESTE_MULHER"
        self.validar()

    def test_fonte_adulterada_rejeita_antes_retorno_e_vazia_nao_pede_fonte(self):
        with patch("validar_json.SHA_FACIAL", "0" * 64):
            self.recusa("SHA256")
        with patch("validar_json.FONTE_FACIAL", str(self.raiz / "PedAnimList.txt")):
            self.recusa("fonte")
        self.cena["interpretacao"] = []
        with patch("validar_json._fontes_faciais", side_effect=AssertionError("não ler fonte sem instruções")):
            self.validar()

    def test_interpretacao_permitida_durante_fala_sem_substituir_boca(self):
        self.cena["roteiro"] = [{"tipo": "fala", "personagem": "TESTE_HOMEM", "gesto": "neutro", "texto": "Fala preservada.", "duracaoSegundos": 6}]
        self.cena["interpretacao"][0].update(expressao="machucado", olhar={"tipo": "horizonte"})
        dados = self.validar()
        self.assertEqual(dados["roteiro"][0]["tipo"], "fala")
        self.assertEqual(dados["roteiro"][0]["gesto"], "neutro")
        self.assertEqual(dados["roteiro"][0]["texto"], "Fala preservada.")

    def test_duracao_e_cortes_consideram_interpretacao_ultimo_fim(self):
        self.cena["roteiro"] = []
        self.cena["interpretacao"] = [dict(self.entrada, duracaoSegundos=2)]
        self.cena["camera"].update(modo="programado", cortes=[{"inicioSegundos": 0, "plano": "DOIS", "personagem": "TESTE_HOMEM"}, {"inicioSegundos": 1.999, "plano": "MEDIA", "personagem": "TESTE_MULHER"}])
        self.validar()
        self.cena["camera"]["cortes"][1]["inicioSegundos"] = 2
        self.recusa("cortes")

    def test_lista64_sem_elenco_e_chaves_duplicadas(self):
        self.cena["interpretacao"] = [dict(self.entrada, inicioSegundos=i, duracaoSegundos=.1) for i in range(64)]
        self.validar()
        self.cena["interpretacao"].append(dict(self.entrada, inicioSegundos=64, duracaoSegundos=.1))
        self.recusa("64")
        self.cena["interpretacao"] = [deepcopy(self.entrada)]
        self.cena["elenco"] = []
        self.recusa("elenco")

    def test_interpretacao_somente_serie_preserva_outros_perfis(self):
        self.validar()
        del self.cena["direcao"]
        for encenacao in ("pedido_v1", "ensaio_coreografia_v1", None):
            if encenacao is None:
                self.cena.pop("encenacao", None)
            else:
                self.cena["encenacao"] = encenacao
            self.recusa("interpretacao")


if __name__ == "__main__":
    unittest.main()
