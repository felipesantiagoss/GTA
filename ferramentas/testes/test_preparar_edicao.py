"""Texto/tempos e limites dos materiais de preparação; sem gerar áudio ou MP4."""
import csv
import hashlib
import io
import json
from pathlib import Path
import re
import sys
import tempfile
import unittest

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
from preparar_edicao import ErroEdicao, ler_planos, preparar_materiais, publicar_edicao


class PrepararEdicaoTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.producao = ler_planos()
        cls.arquivos = preparar_materiais(cls.producao)
        cls.csv = list(csv.DictReader(io.StringIO(cls.arquivos["dublagem_temporada.csv"].decode("utf-8-sig"))))

    def test_quinze_guias_ordem94_vinheta_reutilizada_e95_srt_intactos(self):
        indice = json.loads(self.arquivos["indice.json"])
        self.assertEqual(len(indice["episodios"]), 15)
        ids = []
        for ep in indice["episodios"]:
            p = f"EP{ep['numero']:02d}"
            timeline = json.loads(self.arquivos[f"{p}/{p}.timeline.json"])
            self.assertEqual([c["id"] for c in timeline["clipes"][:-1]], ep["cenas"])
            self.assertEqual(timeline["clipes"][-1]["id"], "VINHETA_01")
            ids.extend(ep["cenas"])
        self.assertEqual(len(ids), 94)
        bases = [p for p in self.arquivos if p.endswith(".base.srt")]
        self.assertEqual(len(bases), 95)
        for path in bases:
            codigo = Path(path).name.removesuffix(".base.srt")
            self.assertEqual(self.arquivos[path], self.producao["srtBase"][codigo])

    def test_todos197_blocos194_vozes_texto_original_tempos_e_audio_apenas_sugerido(self):
        self.assertEqual(len(self.csv), 197)
        vozes = [r for r in self.csv if r["arquivoAudioSugerido"]]
        self.assertEqual(len(vozes), 194)
        self.assertEqual(len({r["arquivoAudioSugerido"] for r in vozes}), 194)
        for row in self.csv:
            b = self.producao["planos"][row["cena"]]["roteiro"][int(row["bloco"]) - 1]
            self.assertEqual(row["textoOriginal"], b["texto"] or "")
            self.assertEqual(int(row["duracaoMs"]), b["duracaoMs"])
            self.assertEqual(int(row["entradaRelativaF7Ms"]), b["inicioMs"] + 3000)
            self.assertEqual(int(row["saidaRelativaF7Ms"]), b["fimMs"] + 3000)
            self.assertEqual(row["audioExiste"], "false")
            self.assertEqual(row["boca"], "true" if b["boca"] else "false")
        self.assertFalse(any(p.endswith((".mp4", ".wav", ".mp3")) for p in self.arquivos))

    def test_pausas_sem_voz_boca_legenda_preservam_intervalo(self):
        pausas = [r for r in self.csv if r["tipo"] == "pausa"]
        self.assertEqual([(r["cena"], int(r["duracaoMs"])) for r in pausas], [("EP14_C03", 4000), ("EP15_C03", 3000)])
        for row in pausas:
            self.assertEqual(row["arquivoAudioSugerido"], "")
            self.assertEqual(row["textoOriginal"], "")
            self.assertEqual(row["boca"], "false")
            ep = f"EP{int(row['episodio']):02d}"
            timeline = json.loads(self.arquivos[f"{ep}/{ep}.timeline.json"])
            inicio, fim = int(row["entradaEpisodioBaseMs"]), int(row["saidaEpisodioBaseMs"])
            self.assertFalse(any(c["inicioMs"] < fim and c["fimMs"] > inicio for c in timeline["legendas"]))

    def test_piloto_srtbase33s_continuidade_pos_trim30s_de_voz(self):
        self.assertIn(b"00:00:30,000 --> 00:00:33,000", self.arquivos["EP01/srt_base/EP01_C08.base.srt"])
        linhas = [r for r in self.csv if r["cena"] == "EP01_C08"]
        self.assertEqual(sum(int(r["duracaoMs"]) for r in linhas), 30000)
        self.assertEqual([int(r["inicioConteudoMs"]) for r in linhas], [0, 6000, 15000, 21000, 24000, 27000])
        guia = self.arquivos["EP01/GUIA_CAPCUT.md"].decode("utf-8")
        self.assertIn("remover apenas3s do começo do MP4 dá erro", guia)
        self.assertIn("data de criação do arquivo não sincroniza voz", guia)

    def test_recap_flashback_camera_credits_e_som_ambiente_preservados(self):
        self.assertIn("EP07_C04 fala 6", self.arquivos["EP08/GUIA_CAPCUT.md"].decode())
        self.assertIn("EP01_C05", self.arquivos["EP08/GUIA_CAPCUT.md"].decode())
        self.assertIn("mesmo enquadramento da EP06_C02", self.arquivos["EP12/GUIA_CAPCUT.md"].decode())
        self.assertIn("Quem pagou o GTA — FELIPE SANTIAGO", self.arquivos["EP01/GUIA_CAPCUT.md"].decode())
        self.assertIn("sem música, só som ambiente", self.arquivos["EP15/GUIA_CAPCUT.md"].decode())

    def test_hold12s_da_dinjer_sem_alterar_voz8s_ou_inventar_pausa(self):
        t = json.loads(self.arquivos["EP15/EP15.timeline.json"])
        hold = next(x for x in t["observacoes"] if x.get("tipo") == "camera_fixa_independente_da_voz")
        self.assertEqual((hold["inicioConteudoMs"], hold["fimConteudoMs"], hold["duracaoCameraMs"], hold["duracaoVozMs"]), (11000, 23000, 12000, 8000))
        self.assertFalse(hold["adicionarSilencio"])
        rows = [r for r in self.csv if r["cena"] == "EP15_C05"]
        self.assertEqual(sum(int(r["duracaoMs"]) for r in rows), 30000)
        self.assertFalse(any(r["tipo"] == "pausa" for r in rows))

    def test_srt_continuidade_ordem_sem_contagem_e_fim_da_ultima_voz(self):
        for ep in range(1, 16):
            prefixo = f"EP{ep:02d}"
            t = json.loads(self.arquivos[f"{prefixo}/{prefixo}.timeline.json"])
            srt = self.arquivos[f"{prefixo}/{prefixo}.continuidade_base.srt"].decode()
            self.assertEqual(srt.count(" --> "), len(t["legendas"]))
            for cue in t["legendas"]:
                self.assertIn(cue["legenda"], srt)
            ultimo = re.findall(r" --> (\d\d):(\d\d):(\d\d),(\d\d\d)", srt)[-1]
            h, m, sec, ms = map(int, ultimo)
            self.assertEqual(((h * 60 + m) * 60 + sec) * 1000 + ms, t["legendas"][-1]["fimMs"])
            self.assertTrue(t["excluiTemposIndefinidos"])
            self.assertFalse(t["sincronismoMedido"])

    def test_reprodutibilidade_hashes_backup_idempotencia_preserva_extra(self):
        self.assertEqual(self.arquivos, preparar_materiais(ler_planos()))
        indice = json.loads(self.arquivos["indice.json"])
        for item in indice["arquivosDerivados"]:
            self.assertEqual(hashlib.sha256(self.arquivos[item["arquivo"]]).hexdigest().upper(), item["sha256"])
        with tempfile.TemporaryDirectory(prefix="temp_edicao_", dir=RAIZ) as nome:
            raiz = Path(nome)
            arquivos = {"indice.json": b"antes", "EP01/guia.md": b"texto"}
            self.assertEqual(publicar_edicao(arquivos, raiz), 2)
            self.assertEqual(publicar_edicao(arquivos, raiz), 0)
            extra = raiz / "producao/edicao/nota_usuario.txt"
            extra.write_text("preservar", encoding="utf-8")
            self.assertEqual(publicar_edicao(arquivos | {"indice.json": b"depois"}, raiz), 1)
            backups = list((raiz / "backups").rglob("indice.json"))
            self.assertEqual(len(backups), 1)
            self.assertEqual(backups[0].read_bytes(), b"antes")
            self.assertEqual(extra.read_text(encoding="utf-8"), "preservar")
            with self.assertRaises(ErroEdicao):
                publicar_edicao({"../fora": b"bad"}, raiz)


if __name__ == "__main__":
    unittest.main()
