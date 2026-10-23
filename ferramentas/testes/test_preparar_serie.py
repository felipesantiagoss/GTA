"""Fidelidade do roteiro e proteção da pré-produção; nenhum teste executa GTA."""
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
from preparar_serie import (CONTAGENS, ErroPreparacao, analisar_roteiro,
                            arquivos_producao, construir_runtime, gerar_srt,
                            publicar_planos)


class PrepararSerieTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fonte = RAIZ / "ROTEIRO_A_CALOTEIRA_DO_OESTE_15_EPISODIOS.md"
        cls.raw = cls.fonte.read_bytes()
        cls.linhas = cls.raw.decode("utf-8-sig").splitlines(keepends=True)
        cls.serie = analisar_roteiro(cls.fonte)
        cls.planos = {p["id"]: p for p in cls.serie["planos"]}
        cls.arquivos = arquivos_producao(cls.serie)

    def test_contagens_ordem_ids_reais_e_quinze_manifestos(self):
        ids_fonte = re.findall(r"^### (EP\d{2}_C\d{2}|VINHETA_01) ·", self.raw.decode("utf-8-sig"), re.M)
        self.assertEqual(list(self.planos), ids_fonte)
        self.assertEqual(len(self.planos), 95)
        self.assertEqual(len(self.serie["episodios"]), 15)
        self.assertEqual([len(e["ordemCenas"]) for e in self.serie["episodios"]], list(CONTAGENS))
        indice = json.loads(self.arquivos["indice.json"])
        self.assertEqual(indice["contagem"], {"episodios": 15, "cenasEpisodios": 94, "vinhetas": 1,
                                             "planos": 95, "comDialogo": 29, "semDialogo": 65})
        for e in self.serie["episodios"]:
            manifesto = json.loads(self.arquivos[f"EP{e['episodio']:02d}/manifesto.json"])
            self.assertEqual(manifesto["ordemCenas"], e["ordemCenas"])

    def test_toda_origem_preservada_bloco_linha_hash_e_complementos(self):
        self.assertEqual(self.serie["fontes"]["roteiro"]["sha256"], hashlib.sha256(self.raw).hexdigest().upper())
        trechos = [p["origem"] for p in self.planos.values()] + self.serie["materialNaoCenaOriginal"]
        reconstruida = ""
        linha = 1
        for trecho in sorted(trechos, key=lambda t: t["linhaInicio"]):
            self.assertEqual(trecho["linhaInicio"], linha)
            raw = "".join(self.linhas[trecho["linhaInicio"] - 1:trecho["linhaFim"]])
            self.assertEqual(trecho["raw"], raw)
            self.assertEqual(trecho["sha256BlocoUtf8"], hashlib.sha256(raw.encode()).hexdigest().upper())
            reconstruida += trecho["raw"]
            linha = trecho["linhaFim"] + 1
        self.assertEqual(reconstruida, self.raw.decode("utf-8-sig"))
        self.assertEqual(linha, 1696)

    def test_todas_falas_narracoes_narradores_tempos_e_textos_literais(self):
        # Conferência independente pela linha original de cada entrada e de todo
        # cabeçalho/tabela do roteiro real, não apenas contagem do parser.
        linhas_cobertas = set()
        for p in self.planos.values():
            cursor = 0
            for b in p["roteiro"]:
                self.assertEqual(b["inicioMs"], cursor)
                self.assertEqual(b["fimMs"], cursor + b["duracaoMs"])
                cursor = b["fimMs"]
                if b["tipo"] == "fala":
                    raw = self.linhas[b["linhaInicio"] - 1].strip()
                    celulas = [x.strip() for x in raw.strip("|").split("|")]
                    self.assertEqual(b["texto"], celulas[2][1:-1])
                    self.assertEqual(b["quemOriginal"], celulas[1])
                    self.assertEqual(b["gestoOriginal"], celulas[4])
                    self.assertEqual(b["duracaoMs"], int(celulas[3][:-1]) * 1000)
                    self.assertTrue(b["boca"])
                    linhas_cobertas.add(b["linhaInicio"])
                elif b["tipo"] in ("narracao", "narrador"):
                    self.assertEqual(b["texto"], self.linhas[b["linhaTexto"] - 1].strip()[3:-1])
                    self.assertEqual(b["duracaoMs"], int(re.search(r"(\d+)s", self.linhas[b["linhaInicio"] - 1]).group(1)) * 1000)
                    self.assertFalse(b["boca"])
                    linhas_cobertas.add(b["linhaInicio"])
            for n in range(p["origem"]["linhaInicio"], p["origem"]["linhaFim"] + 1):
                raw = self.linhas[n - 1].strip()
                if re.match(r"\*\*(NARRAÇÃO|NARRADOR)\*\*", raw) or re.match(r'\| \d+ \| .+ \| ".*" \| \d+s \|', raw):
                    self.assertIn(n, linhas_cobertas)

    def test_piloto_completo_trinta_segundos_srt_ate_trinta_tres(self):
        p = self.planos["EP01_C08"]
        self.assertEqual(p["original"]["duracaoEstimadaMs"], 28000)
        self.assertEqual(p["tempos"]["blocosDeclaradosMs"], 30000)
        self.assertEqual(p["tempos"]["capturaPlanejadaMs"], 33000)
        self.assertEqual([b["duracaoMs"] for b in p["roteiro"]], [6000, 9000, 6000, 3000, 3000, 3000])
        self.assertEqual(p["roteiro"][4]["texto"], "Então tá bom. Vou fazer o Pix.")
        self.assertIn("Dinjer chega de moto, desce, anda com jeito de passarela", p["acoesOriginais"][0]["raw"])
        srt = gerar_srt(p)
        self.assertIn("00:00:03,000 --> 00:00:09,000", srt)
        self.assertIn("00:00:30,000 --> 00:00:33,000", srt)
        recursos = [r["codigo"] for r in p["requisitos"]]
        for recurso in ["visual:DJANIRA_POBRE", "visual:DINJER", "local:ROCA_GRAPESEED", "veiculo:moto", "objeto:celular"]:
            self.assertIn(recurso, recursos)

    def test_pausas_contadas_uma_vez_e_sem_legendas_ou_boca(self):
        for codigo, duracao, total in [("EP14_C03", 4000, 18000), ("EP15_C03", 3000, 26000)]:
            p = self.planos[codigo]
            pausas = [b for b in p["roteiro"] if b["tipo"] == "pausa"]
            self.assertEqual(len(pausas), 1)
            pausa = pausas[0]
            self.assertEqual(pausa["duracaoMs"], duracao)
            self.assertFalse(pausa["boca"])
            self.assertIsNone(pausa["texto"])
            self.assertEqual(p["tempos"]["blocosDeclaradosMs"], total)
            srt = gerar_srt(p)
            self.assertNotIn("silêncio", srt)
            cues = re.findall(r"(\d\d:\d\d:\d\d,\d\d\d) --> (\d\d:\d\d:\d\d,\d\d\d)", srt)
            def ms(t):
                h, m, sec, mil = map(int, re.split(r"[:,]", t))
                return ((h * 60 + m) * 60 + sec) * 1000 + mil
            self.assertIn(duracao, [ms(cues[i + 1][0]) - ms(cues[i][1]) for i in range(len(cues) - 1)])

    def test_estimativas_nao_cortam_narrador_e_acao_final(self):
        self.assertEqual(self.planos["EP02_C07"]["tempos"]["conteudoPlanejadoMs"], 13000)
        self.assertEqual(self.planos["EP02_C07"]["roteiro"][-1]["tipo"], "narrador")
        self.assertEqual(self.planos["EP15_C09"]["tempos"]["conteudoPlanejadoMs"], 19000)
        p = self.planos["EP01_C07"]
        self.assertEqual(p["roteiro"][-1]["tipo"], "acao_final")
        self.assertEqual(p["roteiro"][-1]["duracaoMs"], 2000)
        self.assertEqual(p["tempos"]["conteudoPlanejadoMs"], 20000)

    def test_reaproveitamento_imagem_camera_fala_e_vinheta(self):
        self.assertEqual(self.planos["EP07_C05"]["reaproveitamentos"][0]["cena"], "EP01_C01")
        self.assertTrue(self.planos["EP07_C05"]["reaproveitamentos"][0]["opcionalRegravar"])
        self.assertEqual(self.planos["EP08_C04"]["reaproveitamentos"][0]["cena"], "EP01_C05")
        camera = self.planos["EP12_C01"]["reaproveitamentos"][0]
        self.assertEqual((camera["cena"], camera["tipo"]), ("EP06_C02", "enquadramento"))
        recap = self.serie["episodios"][7]["montagemEditorial"][0]
        self.assertIn({"cena": "EP07_C04", "falaOriginal": 6}, recap["referenciasOriginais"])
        vinheta = self.planos["VINHETA_01"]
        self.assertEqual(vinheta["vinheta"]["reutilizarEpisodios"], list(range(1, 16)))
        self.assertEqual([b["duracaoMs"] for b in vinheta["vinheta"]["tomadasFisicas"]], [5000] * 4)
        self.assertIn("Quem pagou o GTA — FELIPE SANTIAGO", vinheta["creditosOriginais"])
        self.assertEqual(gerar_srt(vinheta), "")

    def test_todos_srt_terminam_na_ultima_voz_sem_descartar_texto(self):
        for p in self.planos.values():
            vozes = [b for b in p["roteiro"] if b["texto"] is not None]
            srt = gerar_srt(p)
            self.assertEqual(srt.count(" --> "), len(vozes))
            for voz in vozes:
                self.assertIn(voz["texto"], srt)
            if vozes:
                fim = re.findall(r" --> (\d\d):(\d\d):(\d\d),(\d\d\d)", srt)[-1]
                h, m, sec, mil = map(int, fim)
                ms = ((h * 60 + m) * 60 + sec) * 1000 + mil
                self.assertEqual(ms, vozes[-1]["fimMs"] + 3000)
                self.assertEqual(ms, p["tempos"]["fimUltimaLegendaMs"])

    def test_adaptacoes_figurinistas_preservam_elenco_e_textos_originais(self):
        for id in ("EP11_C01", "EP11_C02"):
            p = self.planos[id]
            self.assertIn("DJANIRA_POBRE", p["original"]["elenco"])
            self.assertIn("DJANIRA_RICA", p["adaptacao"]["elencoEfetivo"])
            self.assertIn("DJANIRA_POBRE", p["origem"]["raw"])
            self.assertTrue(p["adaptacao"]["regras"])
        p = self.planos["EP03_C07"]
        self.assertIn("sem terno", p["original"]["elenco"])
        self.assertEqual(p["adaptacao"]["regras"][0]["destino"], "terno fixo")
        self.assertEqual(p["roteiro"][0]["quemOriginal"], "CAPANGA_1")
        self.assertTrue(any(r["familia"] == "figurantes" and "gerente" in r["descricao"] for r in self.planos["EP13_C02"]["requisitos"]))

    def test_producao_deterministica_hashes_csv_e_sem_json_runtime(self):
        outra = arquivos_producao(analisar_roteiro(self.fonte))
        self.assertEqual(self.arquivos, outra)
        self.assertEqual(sum(n.endswith(".plano.json") for n in outra), 95)
        self.assertEqual(sum(n.endswith(".base.srt") for n in outra), 95)
        self.assertFalse(any(n.startswith("cenas/") or n.endswith(".mp4") for n in outra))
        indice = json.loads(outra["indice.json"])
        for a in indice["arquivosDerivados"]:
            self.assertEqual(hashlib.sha256(outra[a["arquivo"]]).hexdigest().upper(), a["sha256"])
        linhas = list(csv.DictReader(io.StringIO(outra["relatorio_producao.csv"].decode("utf-8-sig"))))
        self.assertEqual(len(linhas), 95)
        self.assertEqual([l["id"] for l in linhas], list(self.planos))
        self.assertTrue(all(l["pronto"] == "false" and l["gravado"] == "false" for l in linhas))

    def test_runtime_bloqueado_sem_recursos_mesmo_com_etiqueta_alterada(self):
        for p in self.planos.values():
            self.assertFalse(p["execucao"]["pronto"])
            self.assertIsNone(p["execucao"]["runtime"])
        p = json.loads(json.dumps(self.planos["EP01_C08"]))
        with self.assertRaisesRegex(ErroPreparacao, "exportação runtime bloqueada"):
            construir_runtime(p)
        p["execucao"]["pronto"] = True
        for r in p["requisitos"]:
            r.update(estado="testado", confirmadoNoJogo=True, recursoVerificado="etiqueta fictícia")
        with self.assertRaisesRegex(ErroPreparacao, "adaptador de atuação integral"):
            construir_runtime(p)

    def test_publicacao_idempotente_backup_sha_e_preserva_arquivos_extras(self):
        # TemporaryDirectory só remove esta pasta privada criada pelo teste,
        # sempre dentro do workspace autorizado; nunca toca runtime/Game Bar.
        with tempfile.TemporaryDirectory(prefix="temp_serie_", dir=RAIZ) as nome:
            raiz = Path(nome).resolve()
            destino = raiz / "producao" / "temporada"
            amostra = {"EP01/EP01_C08.plano.json": b"original\n", "indice.json": b"indice\n"}
            self.assertEqual(publicar_planos(amostra, destino, raiz)["alterados"], 2)
            usuario = destino / "nota_usuario.txt"
            usuario.write_text("preservar", encoding="utf-8")
            self.assertEqual(publicar_planos(amostra, destino, raiz)["alterados"], 0)
            atualizada = amostra | {"EP01/EP01_C08.plano.json": b"nova\n"}
            resultado = publicar_planos(atualizada, destino, raiz)
            self.assertEqual(resultado["alterados"], 1)
            backup = resultado["backups"][0]
            self.assertEqual((raiz / backup["backup"]).read_bytes(), b"original\n")
            self.assertEqual(backup["sha256"], hashlib.sha256(b"original\n").hexdigest().upper())
            self.assertEqual(usuario.read_text(encoding="utf-8"), "preservar")
            with self.assertRaisesRegex(ErroPreparacao, "Destino permitido"):
                publicar_planos(amostra, raiz / "cenas", raiz)
            with self.assertRaisesRegex(ErroPreparacao, "inseguro"):
                publicar_planos({"../fora.json": b"bad"}, destino, raiz)


if __name__ == "__main__":
    unittest.main()
