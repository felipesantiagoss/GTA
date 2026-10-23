"""Fidelidade de voz/tempo aos planos originais; XMLs sintéticos apenas em cópia."""
from copy import deepcopy
import csv
import hashlib
import io
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
from exportar_edicao_ep01 import PREFIXO, preparar_materiais_runtime, verificar_fidelidade
from preparar_edicao import ErroEdicao, ler_planos
from test_visual import fixture, raw


class ExportacaoRuntimeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        parent = RAIZ / "backups/testes-edicao-ep01"
        parent.mkdir(parents=True, exist_ok=True)
        cls.temp = tempfile.TemporaryDirectory(prefix="caso-", dir=parent)
        cls.addClassCleanup(cls.temp.cleanup)
        cls.catalogs = Path(cls.temp.name) / "catalogos"
        cls.catalogs.mkdir()
        for file in (RAIZ / "catalogos").glob("*.json"):
            shutil.copyfile(file, cls.catalogs / file.name)
        shutil.copyfile(RAIZ / "catalogos/gestos_fala.xml", cls.catalogs / "gestos_fala.xml")
        catalog_file = cls.catalogs / "personagens.json"
        catalog = json.loads(catalog_file.read_text(encoding="utf-8"))
        # Modelos existentes, em XML sintético isolado; não são os figurinos reais.
        required = {"DJANIRA_POBRE": "mp_f_freemode_01", "DINJER": "mp_f_freemode_01",
                    "DONA_IRACI": "a_f_o_genstreet_01", "COLEGA_1": "a_m_y_genstreet_01",
                    "COLEGA_2": "a_m_y_genstreet_02", "COLEGA_3": "a_m_y_stbla_01"}
        (cls.catalogs / "visuais").mkdir()
        for entry in catalog["personagens"]:
            if entry["codigo"] not in required:
                continue
            entry["modelo"] = required[entry["codigo"]]
            content = raw(fixture(entry["modelo"], head=entry["modelo"] == "mp_f_freemode_01"))
            name = entry["codigo"] + ".xml"
            (cls.catalogs / "visuais" / name).write_bytes(content)
            entry.update(pronto=True, visualArquivo=name,
                         visualSHA256=hashlib.sha256(content).hexdigest().upper())
        catalog_file.write_text(json.dumps(catalog, ensure_ascii=False), encoding="utf-8")
        cls.original = ler_planos()
        cls.files = preparar_materiais_runtime(cls.catalogs)
        cls.rows = list(csv.DictReader(io.StringIO(cls.files[PREFIXO + "EP01.runtime.vozes.csv"].decode("utf-8-sig"))))
        cls.timeline = json.loads(cls.files[PREFIXO + "EP01.runtime.montagem.json"])

    def test_dez_brutos_preservam_todas_vozes_da_fonte_inclusive_narrador_masculino(self):
        self.assertEqual(len(self.rows), 18)
        self.assertEqual(len(self.timeline["fontesBrutas"]), 10)
        self.assertEqual(len(self.timeline["vozesMontagemFinal"]), 17)
        for row in self.rows:
            voices = [b for b in self.original["planos"][row["cena"]]["roteiro"] if b["texto"] is not None]
            source = voices[int(row["blocoOriginal"]) - 1]
            self.assertEqual(row["textoOriginal"], source["texto"])
            self.assertEqual(int(row["duracaoMs"]), source["duracaoMs"])
            self.assertEqual(row["quemOriginal"], source.get("quemOriginal", ""))
            self.assertEqual(row["tipo"], source["tipo"])
            self.assertEqual(row["audioExiste"], "False")
            self.assertEqual(row["mp4Real"], "")
            self.assertEqual(row["offsetManualMp4ParaF7Ms"], "")
        closing = next(r for r in self.rows if r["cena"] == "EP01_C09")
        self.assertEqual(closing["nomeVoz"], "Narrador (voz masculina)")
        self.assertEqual(closing["boca"], "False")
        self.assertFalse(any(p.endswith((".mp4", ".wav", ".mp3")) for p in self.files))

    def test_reservas_reais_c05_voz_unica_e_offsets_sem_inferencia_do_video(self):
        order = self.timeline["ordemNarrativa"]
        self.assertEqual([c["id"] for c in order], [f"EP01_C{x:02d}" for x in range(1, 10)])
        self.assertEqual([c["duracaoMontagemPlanejadaMs"] for c in order],
                         [60000, 10000, 9000, 7000, 8000, 9000, 24000, 46000, 7000])
        self.assertEqual(self.timeline["duracaoNoveCenasPlanejadaMs"], 180000)
        self.assertEqual([p["duracaoSelecionadaPlanejadaMs"] for p in order[4]["partes"]], [3000, 5000])
        self.assertTrue(all(p["inicioConteudoSelecionadoMs"] is None for p in order[4]["partes"]))
        self.assertEqual(len(order[4]["chavesVoz"]), 1)
        self.assertEqual(len([r for r in self.rows if r["chaveVoz"] == order[4]["chavesVoz"][0]]), 2)
        self.assertIn(b"00:00:03,000 --> 00:00:17,000", self.files[PREFIXO + "srt_brutos/EP01_C01.srt"])
        self.assertIn(b"00:00:15,000 --> 00:00:21,000", self.files[PREFIXO + "srt_brutos/EP01_C08.srt"])
        self.assertIn(b"00:00:42,000 --> 00:00:45,000", self.files[PREFIXO + "srt_brutos/EP01_C08.srt"])
        self.assertNotIn(b"00:00:45,000 --> 00:00:49,000", self.files[PREFIXO + "srt_brutos/EP01_C08.srt"])
        self.assertTrue(all(f["offsetManualMp4ParaConteudoMs"] is None for f in self.timeline["fontesBrutas"]))

    def test_recusa_texto_tempo_ou_interlocutor_editado_sem_mudar_a_fonte(self):
        scene = json.loads((RAIZ / "producao/cenas_serie/EP01/EP01_C02.json").read_text(encoding="utf-8"))
        changed = deepcopy(scene)
        changed["roteiro"][0]["texto"] += " Texto editado."
        with self.assertRaisesRegex(ErroEdicao, "diverge"):
            verificar_fidelidade(changed, self.original["planos"][scene["id"]])
        changed = deepcopy(scene)
        changed["roteiro"][0]["duracaoSegundos"] = 9
        with self.assertRaisesRegex(ErroEdicao, "diverge"):
            verificar_fidelidade(changed, self.original["planos"][scene["id"]])
        scene = json.loads((RAIZ / "producao/cenas_serie/EP01/EP01_C08.json").read_text(encoding="utf-8"))
        changed = deepcopy(scene)
        changed["roteiro"][2]["personagem"] = "DJANIRA_POBRE"
        with self.assertRaisesRegex(ErroEdicao, "interlocutor"):
            verificar_fidelidade(changed, self.original["planos"][scene["id"]])


if __name__ == "__main__":
    unittest.main()
