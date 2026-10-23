"""Fila JSON: referências completas, ordem declarada e entradas inválidas."""

from copy import deepcopy
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
from validar_json import validar_cena_json


class ValidarFilaTests(unittest.TestCase):
    def setUp(self):
        owned_parent = RAIZ / "backups" / "testes-fila"
        owned_parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="caso-", dir=owned_parent)
        self.root = Path(self.temporary.name)
        self.assertTrue(self.root.resolve().is_relative_to(RAIZ.resolve()))
        self.scenes = self.root / "cenas"
        self.catalogs = self.root / "catalogos"
        self.scenes.mkdir()
        self.catalogs.mkdir()
        for name in ("teste_fila_01.json", "teste_fila_02.json", "fila_teste.json"):
            shutil.copy2(RAIZ / "cenas" / name, self.scenes / name)
        for name in ("personagens.json", "locais.json", "animacoes.json", "gestos_fala.xml"):
            shutil.copy2(RAIZ / "catalogos" / name, self.catalogs / name)
        self.path = self.scenes / "fila_teste.json"
        self.queue = json.loads(self.path.read_text(encoding="utf-8"))

    def tearDown(self):
        # Remover somente a pasta nova deste caso, conferida dentro do workspace.
        self.assertTrue(self.root.resolve().is_relative_to(RAIZ.resolve()))
        self.temporary.cleanup()

    def save(self, path, data):
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    def reject(self, queue):
        self.save(self.path, queue)
        with self.assertRaises(ErroCena):
            validar_fila(self.path)

    def test_fixtures_de_doze_segundos_com_elenco_e_gestos_aprovados(self):
        self.assertEqual(validar_fila(self.path), self.queue)
        for name in self.queue["cenas"]:
            scene = validar_cena_json(self.scenes / name, self.catalogs)
            self.assertEqual(sum(item["duracaoSegundos"] for item in scene["roteiro"]), 12)
            self.assertEqual((scene["episodio"], scene["numero"]), (0, 0))
            self.assertEqual(scene["ambiente"]["local"], "TESTE_AMBIENTE")
            self.assertEqual([actor["personagem"] for actor in scene["elenco"]], ["TESTE_HOMEM", "TESTE_MULHER"])
            self.assertEqual([item["gesto"] for item in scene["roteiro"] if item["tipo"] == "fala"], ["neutro", "explicando"])
            self.assertEqual((scene["camera"]["planoInicial"], scene["camera"]["planoFala"], scene["camera"]["planoFinal"]), ("DOIS", "MEDIA", "DOIS"))
            self.assertFalse(scene["camera"]["inverterEixo"])
        second = json.loads((self.scenes / "teste_fila_02.json").read_text(encoding="utf-8"))
        self.assertEqual(second["acoes"][0]["destino"], {"direita": -0.8, "frente": 1.0, "altura": 0.0})
        self.assertEqual(second["acoes"][0]["inicioSegundos"] + second["acoes"][0]["duracaoSegundos"], second["roteiro"][0]["duracaoSegundos"])

    def test_conserva_ordem_de_gravacao_declarada(self):
        self.queue["cenas"].reverse()
        self.save(self.path, self.queue)
        before = {path: path.read_bytes() for path in self.scenes.iterdir()}
        self.assertEqual(validar_fila(self.path)["cenas"], ["teste_fila_02.json", "teste_fila_01.json"])
        self.assertEqual({path: path.read_bytes() for path in self.scenes.iterdir()}, before)

    def test_cena_posterior_invalida_rejeita_toda_a_fila(self):
        path = self.scenes / "teste_fila_02.json"
        scene = json.loads(path.read_text(encoding="utf-8"))
        scene["roteiro"][1]["gesto"] = "inexistente"
        self.save(path, scene)
        with self.assertRaises(ErroCena):
            validar_fila(self.path)

    def test_personagem_pendente_rejeita_a_fila(self):
        path = self.catalogs / "personagens.json"
        catalog = json.loads(path.read_text(encoding="utf-8"))
        next(actor for actor in catalog["personagens"] if actor["codigo"] == "TESTE_HOMEM")["pronto"] = False
        self.save(path, catalog)
        with self.assertRaises(ErroCena):
            validar_fila(self.path)

    def test_arquivo_de_cena_ausente_rejeitado(self):
        self.queue["cenas"].append("teste_fila_99.json")
        self.reject(self.queue)

    def test_duplicados_inclusive_caixa_rejeitados(self):
        for name in ("teste_fila_01.json", "TESTE_FILA_01.JSON"):
            with self.subTest(name=name):
                queue = deepcopy(self.queue)
                queue["cenas"].append(name)
                self.reject(queue)

    def test_caminhos_unidades_ads_e_extensoes_rejeitados(self):
        for name in ("../teste_fila_01.json", "sub/teste_fila_01.json", "sub\\teste_fila_01.json",
                     "C:\\fora\\teste_fila_01.json", "teste_fila_01.json:stream", "..", "teste_fila_01.xml"):
            with self.subTest(name=name):
                queue = deepcopy(self.queue)
                queue["cenas"] = [name]
                self.reject(queue)

    def test_campos_obrigatorios_e_desconhecidos(self):
        for name in ("versao", "episodio", "nome", "cenas"):
            with self.subTest(missing=name):
                queue = deepcopy(self.queue)
                del queue[name]
                self.reject(queue)
        queue = deepcopy(self.queue)
        queue["id"] = "fila_teste"
        self.reject(queue)

    def test_tipos_e_limites_da_raiz(self):
        for name, value in (("versao", True), ("versao", 2), ("versao", "1"),
                            ("episodio", True), ("episodio", -1), ("episodio", 16),
                            ("nome", ""), ("nome", "x" * 101), ("cenas", "teste_fila_01.json"), ("cenas", [])):
            with self.subTest(field=name, value=value):
                queue = deepcopy(self.queue)
                queue[name] = value
                self.reject(queue)

    def test_limite_vinte_cenas_aceita_vinte_e_rejeita_vinte_e_uma(self):
        template = json.loads((self.scenes / "teste_fila_01.json").read_text(encoding="utf-8"))
        names = []
        for number in range(1, 22):
            scene = deepcopy(template)
            scene["id"] = "teste_fila_{:02d}".format(number)
            name = scene["id"] + ".json"
            self.save(self.scenes / name, scene)
            names.append(name)
        self.queue["cenas"] = names[:20]
        self.save(self.path, self.queue)
        self.assertEqual(len(validar_fila(self.path)["cenas"]), 20)
        self.queue["cenas"] = names
        self.reject(self.queue)

    def test_filas_nao_misturam_episodios_ou_cenas_tecnicas(self):
        queue = deepcopy(self.queue)
        queue["cenas"] = ["EP01_C01.json"]
        self.reject(queue)
        production_path = self.scenes / "fila_EP01.json"
        for name in ("teste_fila_01.json", "EP02_C01.json"):
            with self.subTest(name=name):
                self.save(production_path, {"versao": 1, "episodio": 1, "nome": "Entrada incompatível", "cenas": [name]})
                with self.assertRaises(ErroCena):
                    validar_fila(production_path)

    def test_nome_da_fila_e_id_tecnico_devem_corresponder(self):
        for name in ("outra.json", "fila_EP01.json"):
            with self.subTest(name=name):
                path = self.scenes / name
                self.save(path, self.queue)
                with self.assertRaises(ErroCena):
                    validar_fila(path)
        for name in ("teste_fila_00.json", "teste_fila_100.json"):
            with self.subTest(name=name):
                queue = deepcopy(self.queue)
                queue["cenas"] = [name]
                self.reject(queue)

    def test_json_estrito_duplicados_comentarios_e_nao_finitos(self):
        values = (
            '{"versao":1,"versao":1,"episodio":0,"nome":"Fila","cenas":["teste_fila_01.json"]}',
            json.dumps(self.queue) + " // comentário",
            '{"versao":1,"episodio":NaN,"nome":"Fila","cenas":["teste_fila_01.json"]}',
            '{"versao":1,"episodio":Infinity,"nome":"Fila","cenas":["teste_fila_01.json"]}',
        )
        for text in values:
            with self.subTest(text=text):
                self.path.write_text(text, encoding="utf-8")
                with self.assertRaises(ErroCena):
                    validar_fila(self.path)

    def test_limite_de_caracteres(self):
        self.path.write_text(" " * 100001, encoding="utf-8")
        with self.assertRaises(ErroCena):
            validar_fila(self.path)

    def test_cenas_e_catalogos_explicitos(self):
        self.assertEqual(validar_fila(self.path, self.scenes, self.catalogs), self.queue)

    def test_fila_de_producao_temporaria_conserva_c08_antes_de_c02(self):
        # Apenas metadados de unidade numa pasta temporária; nenhuma cena da série é produzida.
        template = json.loads((self.scenes / "teste_fila_01.json").read_text(encoding="utf-8"))
        names = []
        for number in (8, 2):
            scene = deepcopy(template)
            scene["id"] = "EP01_C{:02d}".format(number)
            scene["episodio"] = 1
            scene["numero"] = number
            name = scene["id"] + ".json"
            self.save(self.scenes / name, scene)
            names.append(name)
        path = self.scenes / "fila_EP01.json"
        self.save(path, {"versao": 1, "episodio": 1, "nome": "Metadados de unidade", "cenas": names})
        self.assertEqual(validar_fila(path)["cenas"], ["EP01_C08.json", "EP01_C02.json"])

    def test_tecnico_exige_episodio_e_numero_zero(self):
        path = self.scenes / "teste_fila_01.json"
        original = json.loads(path.read_text(encoding="utf-8"))
        for field in ("episodio", "numero"):
            with self.subTest(field=field):
                scene = deepcopy(original)
                scene[field] = 1
                self.save(path, scene)
                with self.assertRaises(ErroCena):
                    validar_fila(self.path)


if __name__ == "__main__":
    unittest.main()
