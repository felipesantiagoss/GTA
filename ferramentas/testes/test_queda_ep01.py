"""Contrato real da sequência; XML sintético isolado não é figurino da série."""
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
from validar_json import validar_cena_json
from test_visual import fixture, raw


class QuedaContractTests(unittest.TestCase):
    def setUp(self):
        self.scene = json.loads((RAIZ / "producao/cenas_serie/EP01/EP01_C01.json").read_text(encoding="utf-8"))
        parent = RAIZ / "backups/testes-queda-ep01"
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="caso-", dir=parent)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.catalogs = self.root / "catalogos"
        self.catalogs.mkdir()
        for name in ("personagens.json", "locais.json", "animacoes.json", "corpos_serie.json", "gestos_fala.xml"):
            shutil.copyfile(RAIZ / "catalogos" / name, self.catalogs / name)
        visual = self.catalogs / "visuais/DJANIRA_POBRE.xml"
        visual.parent.mkdir()
        visual.write_bytes(raw(fixture()))
        path = self.catalogs / "personagens.json"
        catalog = json.loads(path.read_text(encoding="utf-8"))
        entry = next(x for x in catalog["personagens"] if x["codigo"] == "DJANIRA_POBRE")
        entry.update(pronto=True, visualArquivo=visual.name,
                     visualSHA256=hashlib.sha256(visual.read_bytes()).hexdigest().upper())
        path.write_text(json.dumps(catalog, ensure_ascii=False), encoding="utf-8")

    def validate(self, scene=None):
        path = self.root / "EP01_C01.json"
        path.write_text(json.dumps(self.scene if scene is None else scene, ensure_ascii=False), encoding="utf-8")
        return validar_cena_json(path, self.catalogs)

    def test_fonte_completa_aceita_voz14_e_reserva46_sem_nova_captura_ou_modelo(self):
        result = self.validate()
        self.assertEqual([x["duracaoSegundos"] for x in result["roteiro"]], [14, 46])
        self.assertEqual(result["ambiente"]["local"], "CANAL_LS")
        self.assertEqual([x["personagem"] for x in result["elenco"]], ["DJANIRA_POBRE"])

    def test_rejeita_troca_de_tempos_mesmo_mantendo_bruto60(self):
        changed = deepcopy(self.scene)
        changed["roteiro"][0]["duracaoSegundos"] = 13
        changed["roteiro"][1]["duracaoSegundos"] = 47
        with self.assertRaisesRegex(ErroCena, "EP01_C01 preserva"):
            self.validate(changed)

    def test_rejeita_voz_cortada_ou_transformada_em_dialogo(self):
        changed = deepcopy(self.scene)
        changed["roteiro"][0]["texto"] = "Meu nome é Djanira."
        with self.assertRaisesRegex(ErroCena, "narração original"):
            self.validate(changed)
        changed = deepcopy(self.scene)
        changed["roteiro"][0] = {"tipo": "fala", "personagem": "DJANIRA_POBRE", "duracaoSegundos": 14,
            "gesto": "neutro", "texto": self.scene["roteiro"][0]["texto"]}
        with self.assertRaisesRegex(ErroCena, "EP01_C01 preserva"):
            self.validate(changed)

    def test_rejeita_saida_da_agua_e_offsets_que_seriam_ignorados(self):
        changed = deepcopy(self.scene)
        changed["ambiente"]["local"] = "ROCA_GRAPESEED"
        with self.assertRaisesRegex(ErroCena, "EP01_C01 exige"):
            self.validate(changed)
        changed = deepcopy(self.scene)
        changed["elenco"][0]["marca"]["frente"] = 1
        with self.assertRaisesRegex(ErroCena, "offsets precisam ser zero"):
            self.validate(changed)

    def test_rejeita_acao_externa_e_cortes_que_conflitam_com_backend_fisico(self):
        changed = deepcopy(self.scene)
        changed["acoes"] = [{"tipo": "esperar", "personagem": "DJANIRA_POBRE", "inicioSegundos": 0, "duracaoSegundos": 1}]
        with self.assertRaisesRegex(ErroCena, "nenhuma ação"):
            self.validate(changed)
        changed = deepcopy(self.scene)
        changed["camera"].update(modo="programado", cortes=[{"inicioSegundos": 0, "plano": "PERTO", "personagem": "DJANIRA_POBRE"}])
        with self.assertRaisesRegex(ErroCena, "câmera própria"):
            self.validate(changed)

    def test_rejeita_remover_encenacao_mesmo_com_tempos_e_elenco_corretos(self):
        changed = deepcopy(self.scene)
        del changed["encenacao"]
        del changed["direcao"]
        with self.assertRaisesRegex(ErroCena, "EP01_C01 exige"):
            self.validate(changed)


if __name__ == "__main__":
    unittest.main()
