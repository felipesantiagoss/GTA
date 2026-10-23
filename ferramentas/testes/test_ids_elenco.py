"""Allowlist técnica de prévia do elenco; não ativa visuais da série."""
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


class IdsElencoTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="temp_ids_elenco_", dir=RAIZ)
        self.addCleanup(self.temp.cleanup)
        self.raiz = Path(self.temp.name)
        self.cenas = self.raiz / "cenas"
        self.cenas.mkdir()
        self.catalogos = self.raiz / "catalogos"
        shutil.copytree(RAIZ / "catalogos", self.catalogos)
        self.cena = json.loads((RAIZ / "cenas/teste_json.json").read_text(encoding="utf-8"))

    def gravar(self, codigo):
        cena = deepcopy(self.cena)
        cena.update(id=codigo, episodio=0, numero=0)
        path = self.cenas / (codigo + ".json")
        path.write_text(json.dumps(cena, ensure_ascii=False), encoding="utf-8")
        return path

    def test_somente_dois_ids_exatos_de_previa_e_metadata_zero(self):
        for codigo in ("teste_elenco_pobre", "teste_elenco_rica"):
            path = self.gravar(codigo)
            validar_cena_json(path, self.catalogos)
            dados = json.loads(path.read_text(encoding="utf-8"))
            dados["episodio"] = 1
            path.write_text(json.dumps(dados), encoding="utf-8")
            with self.assertRaises(ErroCena):
                validar_cena_json(path, self.catalogos)
        for codigo in ("teste_elenco_01", "teste_elenco_extra", "teste_elenco_POBRE", "teste_outro"):
            with self.subTest(id=codigo), self.assertRaises(ErroCena):
                validar_cena_json(self.gravar(codigo), self.catalogos)

    def test_fila_elenco_basename_allowlist_e_ordem_preservada(self):
        for codigo in ("teste_elenco_pobre", "teste_elenco_rica"):
            self.gravar(codigo)
        dados = {"versao": 1, "episodio": 0, "nome": "Prévia técnica", "cenas": ["teste_elenco_rica.json", "teste_elenco_pobre.json"]}
        path = self.cenas / "fila_elenco.json"
        path.write_text(json.dumps(dados, ensure_ascii=False), encoding="utf-8")
        self.assertEqual(validar_fila(path, self.cenas, self.catalogos), dados)
        outro = self.cenas / "fila_elenco_extra.json"
        outro.write_text(json.dumps(dados), encoding="utf-8")
        with self.assertRaises(ErroCena):
            validar_fila(outro, self.cenas, self.catalogos)
        dados["cenas"] = ["teste_elenco_extra.json"]
        path.write_text(json.dumps(dados), encoding="utf-8")
        with self.assertRaises(ErroCena):
            validar_fila(path, self.cenas, self.catalogos)


if __name__ == "__main__":
    unittest.main()
