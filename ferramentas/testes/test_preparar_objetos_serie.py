"""Origem real de18modelos e preservação dos catálogos existentes."""
from copy import deepcopy
import json
from pathlib import Path
import re
import shutil
import sys
import tempfile
import unittest

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
import preparar_objetos_serie as objects
from preparar_piloto import ErroPiloto


class ObjetosTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = objects.gerar_catalogo()

    def temporary_root(self):
        owned = tempfile.TemporaryDirectory(prefix="objetos_owned_", dir=RAIZ / "ferramentas/testes")
        root = Path(owned.name).resolve()
        self.assertTrue(root.is_relative_to(RAIZ))
        self.addCleanup(owned.cleanup)
        (root / "catalogos").mkdir()
        shutil.copy2(RAIZ / "catalogos/acoes_serie_candidatas.json", root / "catalogos/acoes_serie_candidatas.json")
        return root

    def test_18_modelos_reais_linhas_hashes_e_nenhuma_medida_fabricada(self):
        items = self.catalog["objetos"]
        self.assertEqual(len(items), 18)
        self.assertEqual(sum(x["tipo"] == "apoio" for x in items), 8)
        self.assertEqual(sum(x["tipo"] == "prop" for x in items), 10)
        cache = {}
        for item in items:
            path = Path(item["origem"]["arquivo"])
            if path not in cache:
                cache[path] = path.read_text(encoding="utf-8-sig").splitlines()
            self.assertEqual(cache[path][item["origem"]["linha"] - 1].strip(), item["modelo"])
            self.assertEqual(item["origem"]["texto"], item["modelo"])
            self.assertEqual(item["estado"], "fonte_conferida;teste_visual_pendente")
            self.assertNotIn("pronto", item)
            self.assertNotIn("pontoAssento", item)
        self.assertEqual(len({x["codigo"] for x in items}), 18)

    def test_binding_csharp_e_catalogo_python_sem_alias_de_modelo_silencioso(self):
        text = (RAIZ / "src/ObjetosSerieDiretor.cs").read_text(encoding="utf-8")
        region = text.split("Models =", 1)[1].split("private static readonly", 1)[0]
        models = dict(re.findall(r'\{ "([a-z0-9_]+)", "([a-z0-9_]+)" \}', region))
        self.assertEqual(models, objects.MODELOS)

    def test_catalogo_novo_e_idempotente_conferencia_nao_escreve(self):
        root = self.temporary_root()
        with self.assertRaisesRegex(ErroPiloto, "ausente"):
            objects.preparar(root, conferir=True)
        self.assertFalse((root / "catalogos/objetos_serie.json").exists())
        objects.preparar(root)
        path = root / "catalogos/objetos_serie.json"
        raw, modified = path.read_bytes(), path.stat().st_mtime_ns
        objects.preparar(root)
        objects.preparar(root, conferir=True)
        self.assertEqual(path.read_bytes(), raw)
        self.assertEqual(path.stat().st_mtime_ns, modified)

    def test_catalogo_editado_permanece_intacto_erro_antes_qualquer_mutacao(self):
        root = self.temporary_root()
        path = root / "catalogos/objetos_serie.json"
        edited = b'{"versao":1,"objetos":[],"edicaoManual":true}\n'
        path.write_bytes(edited)
        for check in (True, False):
            with self.assertRaisesRegex(ErroPiloto, "edição preservada"):
                objects.preparar(root, conferir=check)
            self.assertEqual(path.read_bytes(), edited)

    def test_origem_alterada_nao_cria_catalogo(self):
        root = self.temporary_root()
        path = root / "catalogos/acoes_serie_candidatas.json"
        pesquisa = json.loads(path.read_text(encoding="utf-8"))
        pesquisa["objetos"][0]["origem"]["sha256"] = "0" * 64
        # Apenas fixture própria do teste; nenhum catálogo real ou arquivo do GTA é alterado.
        path.write_text(json.dumps(pesquisa), encoding="utf-8")
        with self.assertRaisesRegex(ErroPiloto, "SHA256 não conferem"):
            objects.preparar(root)
        self.assertFalse((root / "catalogos/objetos_serie.json").exists())


if __name__ == "__main__":
    unittest.main()
