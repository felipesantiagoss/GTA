"""Poses iniciais: contrato e evidências de arquivo, sem executar GTA/DLL."""
from copy import deepcopy
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
from validar_json import POSES, _corpos, _fontes_corpos, validar_cena_json


class PosesJsonTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="temp_poses_json_", dir=RAIZ)
        self.addCleanup(self.temp.cleanup)
        self.raiz = Path(self.temp.name)
        self.catalogos = self.raiz / "catalogos"
        self.catalogos.mkdir()
        for nome in ("personagens.json", "locais.json", "animacoes.json", "gestos_fala.xml"):
            shutil.copyfile(RAIZ / "catalogos" / nome, self.catalogos / nome)
        self.fontes = self.raiz / "menyooStuff"
        self.fontes.mkdir()
        original = json.loads((RAIZ / "catalogos/corpos_serie.json").read_text(encoding="utf-8"))
        self.corpos = dict(original, animacoes=[deepcopy(r) for r in original["animacoes"] if r["codigo"] in POSES])
        self.fonte = self.fontes / "PedAnimList.txt"
        self.fonte.write_text("\n".join(r["dicionario"] + " " + r["animacao"] for r in self.corpos["animacoes"]) + "\n", encoding="utf-8")
        sha = hashlib.sha256(self.fonte.read_bytes()).hexdigest().upper()
        for i, recurso in enumerate(self.corpos["animacoes"], 1):
            recurso["origem"].update(arquivo=str(self.fonte), linha=i, sha256=sha)
        self.gravar_corpos()
        original_props = json.loads((RAIZ / "catalogos/objetos_serie.json").read_text(encoding="utf-8"))
        self.props = {"versao": 1, "objetos": [deepcopy(r) for r in original_props["objetos"] if r["codigo"] in ("cadeira_simples", "cama_casa", "celular")]}
        fonte_props = self.fontes / "PropList.txt"
        fonte_props.write_text("\n".join(r["modelo"] for r in self.props["objetos"]) + "\n", encoding="utf-8")
        sha_props = hashlib.sha256(fonte_props.read_bytes()).hexdigest().upper()
        for i, recurso in enumerate(self.props["objetos"], 1):
            recurso["origem"].update(arquivo=str(fonte_props), linha=i, sha256=sha_props)
        (self.catalogos / "objetos_serie.json").write_text(json.dumps(self.props, ensure_ascii=False), encoding="utf-8")
        self.cena = json.loads((RAIZ / "cenas/teste_json.json").read_text(encoding="utf-8"))
        self.cena.update(encenacao="serie_v1", direcao=[], acoes=[],
                         roteiro=[{"tipo": "narracao", "texto": "Contrato de pose; contato visual pendente.", "duracaoSegundos": 10}],
                         objetos=[self.objeto()], poses=[self.pose()])

    @staticmethod
    def objeto(**extras):
        return dict(id="CADEIRA", recurso="cadeira_simples", marca={"direita": .8, "frente": 0, "altura": 0}) | extras

    @staticmethod
    def pose(**extras):
        return dict(personagem="TESTE_MULHER", objeto="CADEIRA", recurso="sentada_cadeira_f", tipo="sentada") | extras

    def gravar_corpos(self):
        (self.catalogos / "corpos_serie.json").write_text(json.dumps(self.corpos, ensure_ascii=False), encoding="utf-8")

    def validar(self):
        caminho = self.raiz / (self.cena["id"] + ".json")
        caminho.write_text(json.dumps(self.cena, ensure_ascii=False), encoding="utf-8")
        return validar_cena_json(caminho, self.catalogos)

    def recusa(self, campo="."):
        with self.assertRaisesRegex(ErroCena, campo):
            self.validar()

    def test_cinco_pares_canonicamente_presentes_na_fonte_real(self):
        reais = _corpos(RAIZ / "catalogos")
        usados = []
        for codigo, (_, dicionario, clipe) in POSES.items():
            self.assertEqual((reais[codigo]["dicionario"], reais[codigo]["animacao"]), (dicionario, clipe))
            usados.append(reais[codigo])
        _fontes_corpos(usados, require_menyoo=True)
        for codigo, (tipo, _, _) in POSES.items():
            self.cena["poses"] = [self.pose(recurso=codigo, tipo=tipo,
                                           personagem="TESTE_HOMEM" if codigo == "sentado_cadeira_m" else "TESTE_MULHER")]
            self.validar()

    def test_superficie_xy_real_heading_e_campos_estritos(self):
        self.cena["poses"] = [self.pose(superficie={"direita": -10, "frente": 10}, rotacao=-180)]
        self.validar()
        for campo, valor in (("superficie", {"direita": 0, "frente": 0, "z": 1}),
                             ("superficie", {"direita": True, "frente": 0}),
                             ("superficie", {"direita": 10.001, "frente": 0}),
                             ("superficie", {"direita": 0}), ("superficie", None),
                             ("rotacao", 180.001), ("rotacao", False), ("rotacao", float("inf")),
                             ("dicionario", "mp_sleep"), ("modelo", "prop_chair_01a")):
            self.cena["poses"] = [self.pose(**{campo: valor})]
            self.recusa()

    def test_ator_e_objeto_proprios_unicos_sem_ids_livres(self):
        self.validar()
        for campo, valor in (("personagem", "teste_mulher"), ("personagem", "DINJER"),
                             ("personagem", []), ("objeto", "MAPA"), ("objeto", "cadeira"),
                             ("objeto", True)):
            self.cena["poses"] = [self.pose(**{campo: valor})]
            self.recusa()
        self.cena["poses"] = [self.pose(), self.pose()]
        self.recusa("uma pose")
        self.cena["poses"] = [self.pose() for _ in range(6)]
        self.recusa("5")
        self.cena["poses"] = [self.pose()]
        self.cena.pop("objetos")
        self.recusa("objeto")

    def test_apoio_fixo_visivel_sem_attachment_ou_ocultacao(self):
        self.cena["objetos"][0]["recurso"] = "celular"
        self.recusa("apoio")
        for campo, valor in (("visivelInicial", False), ("assentarNoChao", False),
                             ("eventos", [{"tipo": "ocultar", "inicioSegundos": 1}])):
            self.cena["objetos"] = [self.objeto(**{campo: valor})]
            self.recusa()
        self.cena["objetos"] = [self.objeto(eventos=[{"tipo": "mostrar", "inicioSegundos": 1}])]
        self.validar()

    def test_direcao_e_qualquer_acao_do_ator_pose_sao_proibidas(self):
        self.cena["acoes"] = [{"tipo": "esperar", "personagem": "TESTE_MULHER", "inicioSegundos": 0, "duracaoSegundos": 1}]
        self.recusa("ações legadas")
        self.cena["acoes"] = []
        self.cena["direcao"] = [{"tipo": "virar", "personagem": "TESTE_MULHER", "alvo": "TESTE_HOMEM", "inicioSegundos": 0, "duracaoSegundos": 1}]
        self.recusa("direção corporal")
        self.cena["direcao"][0].update(personagem="TESTE_HOMEM", alvo="TESTE_MULHER")
        self.validar()

    def test_fala_e_interpretacao_conservadas_sem_prolongar_duracao(self):
        self.cena["roteiro"] = [{"tipo": "fala", "personagem": "TESTE_MULHER", "gesto": "neutro", "texto": "Sentada, com boca e gesto secundário.", "duracaoSegundos": 1}]
        self.cena["interpretacao"] = [{"personagem": "TESTE_MULHER", "inicioSegundos": 0, "duracaoSegundos": 1, "olhar": {"tipo": "chao"}}]
        self.validar()
        self.cena["roteiro"] = []
        del self.cena["interpretacao"]
        self.recusa("duração")

    def test_tipos_recursos_e_par_canonico_nao_substituiveis(self):
        for campo, valor in (("recurso", "sentar_chao_candidato"), ("recurso", "SONO_F"),
                             ("recurso", {}), ("tipo", "deitada"), ("tipo", True)):
            self.cena["poses"] = [self.pose(**{campo: valor})]
            self.recusa()
        self.cena["poses"] = [self.pose()]
        recurso = next(r for r in self.corpos["animacoes"] if r["codigo"] == "sentada_cadeira_f")
        recurso.update(dicionario="mp_sleep", animacao="sleep_loopfemale")
        recurso["origem"]["texto"] = "mp_sleep sleep_loopfemale"
        self.gravar_corpos()
        self.recusa("par literal")

    def test_fonte_hash_linha_bytes_e_pasta_conferidos_antes_montar(self):
        original = deepcopy(self.corpos)
        for campo, valor in (("sha256", "0" * 64), ("linha", 500000),
                             ("arquivo", str(self.raiz / "PedAnimList.txt"))):
            self.corpos = deepcopy(original)
            recurso = next(r for r in self.corpos["animacoes"] if r["codigo"] == "sentada_cadeira_f")
            recurso["origem"][campo] = valor
            self.gravar_corpos()
            self.recusa("poses.recurso")
        self.corpos = original
        self.gravar_corpos()
        self.fonte.write_bytes(b"fonte alterada\n")
        self.recusa("SHA256")

    def test_omissao_e_lista_vazia_sem_efeito_somente_serie(self):
        self.cena["poses"] = []
        self.validar()
        del self.cena["objetos"]
        del self.cena["direcao"]
        for staging in (None, "pedido_v1", "ensaio_coreografia_v1"):
            if staging is None: self.cena.pop("encenacao", None)
            else: self.cena["encenacao"] = staging
            self.recusa("poses")
        self.cena.pop("encenacao", None)
        self.cena.pop("poses")
        self.validar()


if __name__ == "__main__":
    unittest.main()
