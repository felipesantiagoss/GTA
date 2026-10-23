"""Props JSON e cadeia de donos; testes de arquivo, sem executar GTA/DLL."""
from copy import deepcopy
import hashlib
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
from validar_json import _fontes_corpos, _recursos_objetos, validar_cena_json


class ObjetosJsonTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="temp_objetos_json_", dir=RAIZ)
        self.addCleanup(self.temp.cleanup)
        self.raiz = Path(self.temp.name)
        self.catalogos = self.raiz / "catalogos"
        self.catalogos.mkdir()
        for nome in ("personagens.json", "locais.json", "animacoes.json", "gestos_fala.xml", "corpos_serie.json"):
            shutil.copyfile(RAIZ / "catalogos" / nome, self.catalogos / nome)
        original = json.loads((RAIZ / "catalogos/objetos_serie.json").read_text(encoding="utf-8"))
        self.recursos = [deepcopy(next(r for r in original["objetos"] if r["codigo"] == codigo)) for codigo in ("celular", "papel", "cadeira_simples")]
        self.fonte = self.raiz / "menyooStuff" / "PropList.txt"
        self.fonte.parent.mkdir()
        self.fonte.write_text("\n".join(r["modelo"] for r in self.recursos) + "\n", encoding="utf-8")
        digest = hashlib.sha256(self.fonte.read_bytes()).hexdigest().upper()
        for i, recurso in enumerate(self.recursos, 1):
            recurso["origem"].update(arquivo=str(self.fonte), linha=i, sha256=digest)
        self.catalogo = {"versao": 1, "objetos": self.recursos}
        self.gravar_catalogo()
        self.cena = json.loads((RAIZ / "cenas/teste_json.json").read_text(encoding="utf-8"))
        self.cena.update(encenacao="serie_v1", direcao=[], acoes=[], roteiro=[{"tipo": "narracao", "texto": "Teste de contrato; não comprova contato visual.", "duracaoSegundos": 10}], objetos=[self.objeto()])

    @staticmethod
    def objeto(id="TELEFONE", recurso="celular", **extras):
        return dict(id=id, recurso=recurso, marca={"direita": 0, "frente": 1, "altura": 0}, **extras)

    @staticmethod
    def attachment(ator="TESTE_HOMEM", mao="direita"):
        return dict(personagem=ator, mao=mao, offset={"x": 0, "y": 0, "z": 0}, rotacao={"x": 0, "y": 0, "z": 0})

    def evento(self, tipo, inicio, ator="TESTE_HOMEM"):
        result = dict(tipo=tipo, inicioSegundos=inicio)
        if tipo in ("anexar", "transferir"):
            result.update(self.attachment(ator))
        return result

    def gravar_catalogo(self):
        (self.catalogos / "objetos_serie.json").write_text(json.dumps(self.catalogo, ensure_ascii=False), encoding="utf-8")

    def validar(self):
        path = self.raiz / (self.cena["id"] + ".json")
        path.write_text(json.dumps(self.cena, ensure_ascii=False), encoding="utf-8")
        return validar_cena_json(path, self.catalogos)

    def recusa(self, campo=None):
        with self.assertRaisesRegex(ErroCena, campo or "."):
            self.validar()

    def test_bindings19_origens_reais_e_fonte_opcional_nos_apoios(self):
        recursos = _recursos_objetos(RAIZ / "catalogos")
        self.assertEqual(len(recursos), 19)
        _fontes_corpos(list(recursos.values()), "PropList.txt", True)
        self.assertNotIn("fonte", recursos["cadeira_simples"]["origem"])
        self.assertEqual(recursos["celular"]["origem"]["fonte"], "objetos")
        self.validar()
        self.recursos[2]["origem"]["fonte"] = "apoios"
        self.gravar_catalogo()
        self.cena["objetos"] = [self.objeto(recurso="cadeira_simples")]
        self.validar()

    def test_objetos_somente_serie_e_tipos_campos_sem_modelo_livre(self):
        self.validar()
        for campo, valor in (("id", "minusculo"), ("id", True), ("recurso", "CELULAR"), ("modelo", "prop_npc_phone_02"),
                             ("source", str(self.fonte)), ("rotacao", 181), ("rotacao", {"z": 0}), ("visivelInicial", 1), ("assentarNoChao", None)):
            self.cena["objetos"] = [self.objeto(**{campo: valor})]
            self.recusa()
        self.cena["objetos"] = []
        del self.cena["direcao"]
        for staging in (None, "pedido_v1", "ensaio_coreografia_v1"):
            if staging is None: self.cena.pop("encenacao", None)
            else: self.cena["encenacao"] = staging
            self.recusa("objetos")

    def test_cadeia_anexar_transferir_ocultar_mostrar_soltar_reanexar(self):
        eventos = [self.evento("anexar", 0), self.evento("ocultar", 1), self.evento("mostrar", 2),
                   self.evento("transferir", 3, "TESTE_MULHER"), self.evento("soltar", 4), self.evento("anexar", 5)]
        self.cena["objetos"][0]["eventos"] = eventos
        self.validar()
        self.cena["objetos"][0]["attachmentInicial"] = self.attachment()
        self.cena["objetos"][0]["eventos"] = eventos[1:]
        self.validar()

    def test_cadeias_impossiveis_rejeitam_antes_da_leitura_da_fonte(self):
        invalidos = [[self.evento("soltar", 1)], [self.evento("transferir", 1, "TESTE_MULHER")],
                     [self.evento("anexar", 1), self.evento("anexar", 2)],
                     [self.evento("anexar", 1), self.evento("transferir", 2)],
                     [self.evento("anexar", 1), self.evento("soltar", 2), self.evento("soltar", 3)]]
        for eventos in invalidos:
            self.cena["objetos"][0]["eventos"] = eventos
            with patch("validar_json._fontes_corpos", side_effect=AssertionError("fonte lida antes de rejeitar dados")):
                self.recusa("dono")

    def test_attachment_exige_ator_mao_vetores_e_norma_meio_metro(self):
        valido = self.attachment()
        valido["offset"] = {"x": .3, "y": .4, "z": 0}
        valido["rotacao"] = {"x": -180, "y": 180, "z": 0}
        self.cena["objetos"][0]["attachmentInicial"] = valido
        self.validar()
        for campo, valor in (("personagem", "DINJER"), ("personagem", []), ("mao", "pe"),
                             ("offset", {"x": .5, "y": .5, "z": 0}), ("offset", {"x": 0, "y": 0}),
                             ("offset", {"x": True, "y": 0, "z": 0}), ("rotacao", {"x": 0, "y": 0, "z": 180.001})):
            self.cena["objetos"][0]["attachmentInicial"] = dict(valido, **{campo: valor})
            self.recusa("attachmentInicial")

    def test_apoios_precisam_assentados_visiveis_sem_attachments(self):
        self.cena["objetos"] = [self.objeto(recurso="cadeira_simples")]
        self.validar()
        for extras in ({"visivelInicial": False}, {"assentarNoChao": False}, {"attachmentInicial": self.attachment()},
                       {"eventos": [self.evento("anexar", 1)]}):
            self.cena["objetos"] = [self.objeto(recurso="cadeira_simples", **extras)]
            self.recusa("apoio")

    def test_tempos_ordenados_unicos_milisegundos_antes_do_fim(self):
        self.cena["objetos"][0]["eventos"] = [self.evento("ocultar", 0), self.evento("mostrar", 9.999)]
        self.validar()
        for eventos in ([self.evento("ocultar", 10)], [self.evento("ocultar", 11)], [self.evento("ocultar", -.001)],
                        [self.evento("ocultar", .0001)], [self.evento("ocultar", True)],
                        [self.evento("ocultar", 1), self.evento("mostrar", 1)], [self.evento("ocultar", 2), self.evento("mostrar", 1)]):
            self.cena["objetos"][0]["eventos"] = eventos
            self.recusa()
        self.cena["roteiro"] = []
        self.cena["objetos"][0]["eventos"] = [self.evento("ocultar", 1)]
        self.recusa("fim")

    def test_eventos_rejeitam_campos_de_attachment_e_nao_impedem_clipes(self):
        self.cena["objetos"][0]["attachmentInicial"] = self.attachment()
        self.cena["objetos"][0]["eventos"] = [dict(self.evento("soltar", 1), personagem="TESTE_HOMEM")]
        self.recusa("desconhecido")
        self.cena["objetos"][0]["eventos"] = [self.evento("soltar", 1)]
        self.cena["direcao"] = [dict(tipo="clipe", personagem="TESTE_HOMEM", inicioSegundos=0, duracaoSegundos=2, recurso="celular_texto_f", corpoInteiro=False)]
        self.validar()

    def test_nao_duplica_telefone_criado_pela_direcao_celular(self):
        self.cena["direcao"] = [dict(tipo="celular", personagem="TESTE_HOMEM", inicioSegundos=0, duracaoSegundos=2, recurso="celular_texto_f")]
        self.recusa("duplicaria")
        self.cena["direcao"][0]["personagem"] = "TESTE_MULHER"
        self.recusa("duplicaria")
        self.cena["objetos"][0]["recurso"] = "papel"
        self.validar()

    def test_limites8_props32_eventos_e_mesmo_ms_entre_objs(self):
        self.cena["objetos"] = [self.objeto(id="PROP_" + str(i), eventos=[self.evento("ocultar", 1)]) for i in range(8)]
        self.validar()
        self.cena["objetos"].append(self.objeto(id="PROP_8"))
        self.recusa("8")
        self.cena["objetos"] = [self.objeto(), self.objeto()]
        self.recusa("repetido")
        self.cena["objetos"] = [self.objeto(eventos=[self.evento("ocultar", i / 10) for i in range(32)])]
        self.validar()
        self.cena["objetos"][0]["eventos"].append(self.evento("ocultar", 3.2))
        self.recusa("32")

    def test_catalogo_adulterado_modelo_tipo_fonte_hash_linha(self):
        original = deepcopy(self.catalogo)
        for nivel, campo, valor in (("root", "extra", 1), ("item", "modelo", "prop_wine_glass"), ("item", "tipo", "apoio"),
                                    ("item", "codigo", "inventado"), ("item", "pronto", True),
                                    ("origem", "fonte", "apoios"), ("origem", "texto", "outro_modelo"),
                                    ("origem", "linha", 2), ("origem", "linha", 1.0),
                                    ("origem", "sha256", "0" * 64), ("origem", "extra", True)):
            self.catalogo = deepcopy(original)
            obj = self.catalogo if nivel == "root" else self.catalogo["objetos"][0] if nivel == "item" else self.catalogo["objetos"][0]["origem"]
            obj[campo] = valor
            self.gravar_catalogo()
            self.recusa()

    def test_caminho_fonte_local_instalada_sem_ads_rede_ou_links(self):
        origem = self.recursos[0]["origem"]
        original = str(self.fonte)
        for caminho in ("PropList.txt", original + ":stream", str(self.raiz / "PropList.txt"),
                        str(self.fonte.parent / ".." / "menyooStuff" / "PropList.txt"), r"\\servidor\menyooStuff\PropList.txt"):
            origem["arquivo"] = caminho
            self.gravar_catalogo()
            self.recusa("origem")
        origem["arquivo"] = original
        self.gravar_catalogo()
        self.fonte.unlink()
        self.recusa("fonte")


if __name__ == "__main__":
    unittest.main()
