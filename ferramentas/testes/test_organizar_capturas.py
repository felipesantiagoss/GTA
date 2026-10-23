"""Tomadas sintéticas em pastas próprias: associação, locks e preservação.

As caixas MP4 destes testes são estruturais; não contêm uma gravação real do GTA.
"""

from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import stat
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch
import uuid
import xml.etree.ElementTree as ET

WORKSPACE = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(WORKSPACE / "ferramentas"))
import organizar_capturas as captures


def box(kind, payload):
    return struct.pack(">I4s", 8 + len(payload), kind) + payload


def movie_bytes(duration_ms=15100, version=0):
    header = struct.pack(">B3sIIII", 0, b"\x00\x00\x00", 0, 0, 1000, duration_ms) if version == 0 else \
             struct.pack(">B3sQQIQ", 1, b"\x00\x00\x00", 0, 0, 1000, duration_ms)
    return box(b"ftyp", b"isom\x00\x00\x00\x00isom") + box(b"moov", box(b"mvhd", header)) + box(b"mdat", b"synthetic-media")


MP4 = movie_bytes()


class CaptureTests(unittest.TestCase):
    def setUp(self):
        parent = WORKSPACE / "backups" / "testes-capturas"
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="caso-", dir=parent)
        self.root = Path(self.temporary.name)
        self.assertTrue(self.root.absolute().is_relative_to(WORKSPACE.absolute()))
        self.profile = self.root / "perfil"
        self.data = self.profile / "GTA_Diretor"
        self.captures = self.profile / "Videos" / "Captures"
        self.captures.mkdir(parents=True)
        (self.data / "cenas").mkdir(parents=True)
        self.environment = patch.dict(os.environ, {"USERPROFILE": str(self.profile)})
        self.environment.start()
        self.scene = "teste_fila_01"
        self.subtitle = self.data / "cenas" / (self.scene + ".srt")
        self.subtitle.write_text("1\n00:00:03,000 --> 00:00:07,000\nHomem: Fila 1.\n", encoding="utf-8")
        self.path, self.node = self.session()

    def tearDown(self):
        self.environment.stop()
        self.assertTrue(self.root.absolute().is_relative_to(WORKSPACE.absolute()))
        self.temporary.cleanup()

    def session(self, scene=None, episode=0, number=0):
        identifier = uuid.uuid4().hex
        scene = scene or self.scene
        path = self.data / "logs" / "capturas" / identifier / "sessao.xml"
        path.parent.mkdir(parents=True)
        now = datetime.now(timezone.utc)
        node = ET.Element("captura", {"versao": "1", "id": identifier, "estado": "concluida",
                                    "cenaId": scene, "episodio": str(episode), "numero": str(number),
                                    "pastaDados": str(self.data), "pastaCapturas": str(self.captures),
                                    "armadaUtc": captures._iso(now - timedelta(seconds=30)),
                                    "contagemUtc": captures._iso(now - timedelta(seconds=20)),
                                    "fimUtc": captures._iso(now - timedelta(seconds=5)),
                                    "prazoUtc": captures._iso(now + timedelta(seconds=120)), "duracaoMs": "15000"})
        ET.SubElement(node, "srt", {"caminho": str(self.subtitle), "sha256": captures._hash(self.subtitle)})
        ET.SubElement(node, "antes")
        self.save(path, node)
        return path, node

    def save(self, path=None, node=None):
        path = path or self.path
        node = node if node is not None else self.node
        ET.ElementTree(node).write(path, encoding="utf-8", xml_declaration=True)

    def movie(self, name="GTA5 2026-10-03 20-00-00.mp4", data=MP4):
        source = self.captures / name
        source.write_bytes(data)
        return source

    def run_capture(self, path=None, **kwargs):
        return captures.organizar(path or self.path, pasta_capturas=self.captures,
                                 intervalo=0.003, espera_maxima=kwargs.pop("espera_maxima", 0.2), **kwargs)

    def result_xml(self):
        return ET.parse(self.path.with_name("resultado.xml")).getroot()

    def baseline(self, source):
        ET.SubElement(self.node.find("antes"), "arquivo", {"nome": source.name, "tamanho": str(source.stat().st_size),
                      "criadoUtc": self.node.get("armadaUtc"), "alteradoUtc": self.node.get("armadaUtc")})
        self.save()

    def test_salva_par_backup_hash_e_preserva_srt_original(self):
        source = self.movie()
        original_srt = self.subtitle.read_bytes()
        result = self.run_capture()
        self.assertEqual(result["status"], "salvo", result)
        self.assertFalse(source.exists())
        self.assertEqual(Path(result["video"]).read_bytes(), MP4)
        self.assertEqual(Path(result["legenda"]).read_bytes(), original_srt)
        self.assertEqual(self.subtitle.read_bytes(), original_srt)
        self.assertEqual(Path(result["video"]).parent.name, "TESTES")
        manifest = json.loads(Path(result["manifesto"]).read_text(encoding="utf-8"))
        self.assertEqual(captures._hash(Path(manifest["BackupVideo"])), result["sha256Video"])
        self.assertEqual(captures._hash(Path(manifest["BackupLegenda"])), result["sha256Legenda"])
        self.assertEqual(manifest["InicioVideoRelativoF7"], "nao_inferido")
        self.assertTrue(Path(result["manifesto"]).with_name("conclusao.json").exists())
        self.assertEqual(self.result_xml().get("status"), "salvo")

    def test_video_antigo_no_snapshot_nao_e_movido_mesmo_alterado(self):
        old = self.movie("GTA5 antigo.mp4")
        self.baseline(old)
        old.write_bytes(MP4 + box(b"free", b"changed-old"))
        new = self.movie("Grand Theft Auto V novo.mp4")
        result = self.run_capture()
        self.assertEqual(result["status"], "salvo", result)
        self.assertTrue(old.exists())
        self.assertFalse(new.exists())

    def test_dois_candidatos_ambiguos_preserva_ambos_sem_destinos(self):
        first = self.movie("GTA5 primeiro.mp4")
        second = self.movie("Grand Theft Auto V segundo.mp4")
        result = self.run_capture()
        self.assertEqual(result["status"], "erro")
        self.assertIn("ambígua", result["mensagem"])
        self.assertEqual(first.read_bytes(), MP4)
        self.assertEqual(second.read_bytes(), MP4)
        self.assertFalse((self.data / "Serie").exists())

    def test_outro_aplicativo_nao_e_associado(self):
        other = self.movie("Navegador 2026-10-03.mp4")
        result = self.run_capture(espera_maxima=0.02)
        self.assertEqual(result["status"], "aguardando")
        self.assertTrue(other.exists())

    def test_modificacao_fora_da_janela_preserva_video(self):
        source = self.movie()
        stamp = datetime(2020, 1, 1, tzinfo=timezone.utc).timestamp()
        os.utime(source, (stamp, stamp))
        result = self.run_capture(espera_maxima=0.02)
        self.assertEqual(result["status"], "aguardando")
        self.assertTrue(source.exists())

    def test_video_parado_antes_fim_da_cena_nao_e_associado(self):
        source = self.movie()
        timestamp = captures._utc(self.node.get("contagemUtc")).timestamp() + 1
        os.utime(source, (timestamp, timestamp))
        result = self.run_capture(espera_maxima=0.02)
        self.assertEqual(result["status"], "aguardando", result)
        self.assertTrue(source.exists())

    def test_video_curto_demais_preservado_mesmo_nome_e_horario_validos(self):
        source = self.movie(data=movie_bytes(3000))
        result = self.run_capture(espera_maxima=0.03)
        self.assertEqual(result["status"], "aguardando", result)
        self.assertTrue(source.exists())
        self.assertFalse((self.data / "Serie").exists())

    def test_cancelamento_antes_organizar_preserva_tudo(self):
        source = self.movie()
        self.path.with_name("cancelada.xml").write_text("<cancelada />", encoding="utf-8")
        result = self.run_capture()
        self.assertEqual(result["status"], "cancelado")
        self.assertTrue(source.exists())
        self.assertTrue(self.subtitle.exists())
        self.assertFalse((self.data / "Serie").exists())

    def test_cancelamento_durante_copia_nao_remove_origem(self):
        source = self.movie()
        original_copy = captures._copy
        def copy_and_cancel(stream, destination, root, expected):
            original_copy(stream, destination, root, expected)
            if "Serie" in destination.parts and destination.suffix == ".mp4":
                self.path.with_name("cancelada.xml").write_text("<cancelada />", encoding="utf-8")
        with patch.object(captures, "_copy", side_effect=copy_and_cancel):
            result = self.run_capture()
        self.assertEqual(result["status"], "cancelado", result)
        self.assertTrue(source.exists())
        self.assertTrue(self.subtitle.exists())
        self.assertTrue(list((self.data / "backups" / "capturas").rglob("falha.json")))

    def test_versao_conjunta_quando_so_legenda_antiga_existe(self):
        folder = self.data / "Serie" / "A_CALOTEIRA_1_TEMP" / "TESTES"
        folder.mkdir(parents=True)
        old = folder / (self.scene + ".srt")
        old.write_text("legenda antiga", encoding="utf-8")
        source = self.movie()
        result = self.run_capture()
        self.assertEqual(result["status"], "salvo", result)
        self.assertEqual(result["versaoTomada"], "2")
        self.assertEqual(Path(result["video"]).stem, self.scene + "_v2")
        self.assertEqual(Path(result["legenda"]).stem, self.scene + "_v2")
        self.assertEqual(old.read_text(encoding="utf-8"), "legenda antiga")
        self.assertFalse(source.exists())

    def test_reserva_antiga_nao_e_reutilizada(self):
        folder = self.data / "Serie" / "A_CALOTEIRA_1_TEMP" / "TESTES"
        folder.mkdir(parents=True)
        reservation = folder / (self.scene + ".reserva")
        reservation.write_text("interrompida", encoding="ascii")
        self.movie()
        result = self.run_capture()
        self.assertEqual(result["versaoTomada"], "2", result)
        self.assertEqual(reservation.read_text(encoding="ascii"), "interrompida")

    def test_retry_salvo_idempotente_sem_consumir_outro_video(self):
        self.movie()
        first = self.run_capture()
        other = self.movie("GTA5 outra tomada.mp4")
        second = self.run_capture()
        self.assertEqual(first, second)
        self.assertTrue(other.exists())
        self.assertEqual(len(list((self.data / "Serie").rglob("*.mp4"))), 1)

    def test_retry_aguardando_preserva_resultado_anterior(self):
        first = self.run_capture(espera_maxima=0.01)
        self.assertEqual(first["status"], "aguardando")
        self.movie()
        second = self.run_capture()
        self.assertEqual(second["status"], "salvo", second)
        previous = list(self.path.parent.glob("resultado-anterior-*.xml"))
        self.assertEqual(len(previous), 1)
        self.assertEqual(ET.parse(previous[0]).getroot().get("status"), "aguardando")

    def test_end_apos_reload_amplia_prazo_sem_mudar_baseline_srt_ou_guid(self):
        old = self.movie("GTA5 captura antiga.mp4")
        self.baseline(old)
        now = datetime.now(timezone.utc)
        self.node.set("armadaUtc", captures._iso(now - timedelta(minutes=5)))
        self.node.set("contagemUtc", captures._iso(now - timedelta(minutes=4)))
        self.node.set("fimUtc", captures._iso(now - timedelta(minutes=3)))
        self.node.set("prazoUtc", captures._iso(now - timedelta(seconds=1)))
        self.save()
        original = self.path.read_bytes()
        source = self.movie("GTA5 tomada concluida.mp4")
        result = captures.repetir(self.path, pasta_capturas=self.captures, intervalo=0.003, espera_maxima=0.2)
        self.assertEqual(result["status"], "salvo", result)
        self.assertTrue(old.exists())
        self.assertFalse(source.exists())
        after = ET.parse(self.path).getroot()
        deadline = after.get("prazoUtc")
        self.assertGreater(captures._utc(deadline), now)
        self.assertLessEqual(captures._utc(deadline), captures._utc(after.get("fimUtc")) + timedelta(minutes=10))
        after.set("prazoUtc", self.node.get("prazoUtc"))
        self.assertEqual(ET.tostring(after), ET.tostring(self.node))
        backups = list(self.path.parent.glob("sessao.backup-*.xml"))
        self.assertEqual(len(backups), 1)
        self.assertEqual(backups[0].read_bytes(), original)

    def test_end_cancelado_nao_reabre_tomada_nem_altera_sessao(self):
        source = self.movie()
        original = self.path.read_bytes()
        self.path.with_name("cancelada.xml").write_text("<cancelada />", encoding="utf-8")
        result = captures.repetir(self.path, pasta_capturas=self.captures, intervalo=0.003, espera_maxima=0.02)
        self.assertEqual(result["status"], "cancelado", result)
        self.assertEqual(self.path.read_bytes(), original)
        self.assertTrue(source.exists())
        self.assertFalse(list(self.path.parent.glob("sessao.backup-*.xml")))

    def test_end_depois_dez_minutos_preserva_mp4_e_sessao(self):
        now = datetime.now(timezone.utc)
        for field, seconds in (("armadaUtc", 750), ("contagemUtc", 720), ("fimUtc", 700), ("prazoUtc", 600)):
            self.node.set(field, captures._iso(now - timedelta(seconds=seconds)))
        self.save()
        original = self.path.read_bytes()
        source = self.movie()
        result = captures.repetir(self.path, pasta_capturas=self.captures, intervalo=0.003, espera_maxima=0.02)
        self.assertEqual(result["status"], "erro", result)
        self.assertIn("dez minutos", result["mensagem"])
        self.assertEqual(self.path.read_bytes(), original)
        self.assertTrue(source.exists())

    def test_end_respeita_lock_outro_worker_sem_substituir_resultado(self):
        self.movie()
        session = captures.ler_sessao(self.path, self.captures)
        original = self.path.read_bytes()
        with captures._exclusive(session.path.with_name("organizador.lock"), lock=True):
            result = captures.repetir(self.path, pasta_capturas=self.captures, intervalo=0.003, espera_maxima=0.02)
        self.assertEqual(result["status"], "aguardando", result)
        self.assertEqual(self.path.read_bytes(), original)
        self.assertFalse(self.path.with_name("resultado.xml").exists())

    def test_end_publica_cancelamento_com_lock_ainda_exclusivo(self):
        self.path.with_name("cancelada.xml").write_text("<cancelada />", encoding="utf-8")
        original_write = captures._result_write
        checked = []
        def write_under_lock(path, session, result):
            with self.assertRaises(captures.Ocupado):
                with captures._exclusive(self.path.with_name("organizador.lock"), lock=True):
                    pass
            checked.append(result["status"])
            original_write(path, session, result)
        with patch.object(captures, "_result_write", side_effect=write_under_lock):
            result = captures.repetir(self.path, pasta_capturas=self.captures, intervalo=0.003, espera_maxima=0.02)
        self.assertEqual(result["status"], "cancelado", result)
        self.assertEqual(checked, ["cancelado"])

    def test_end_recusa_edicao_concorrente_antes_substituir_sessao(self):
        source = self.movie()
        now = datetime.now(timezone.utc)
        self.node.set("armadaUtc", captures._iso(now - timedelta(minutes=5)))
        self.node.set("contagemUtc", captures._iso(now - timedelta(minutes=4)))
        self.node.set("fimUtc", captures._iso(now - timedelta(minutes=3)))
        self.node.set("prazoUtc", captures._iso(now - timedelta(seconds=1)))
        self.save()
        original_copy = captures._copy
        changed = []
        def copy_and_edit(stream, destination, root, expected):
            original_copy(stream, destination, root, expected)
            if destination.name.startswith("sessao.backup-"):
                edited = self.path.read_bytes() + b"\n<!-- edicao concorrente -->\n"
                self.path.write_bytes(edited)
                changed.append(edited)
        with patch.object(captures, "_copy", side_effect=copy_and_edit):
            result = captures.repetir(self.path, pasta_capturas=self.captures, intervalo=0.003, espera_maxima=0.02)
        self.assertEqual(result["status"], "erro", result)
        self.assertIn("editada", result["mensagem"])
        self.assertEqual(self.path.read_bytes(), changed[0])
        self.assertTrue(source.exists())
        self.assertFalse((self.data / "Serie").exists())

    def test_end_valida_sessao_completa_antes_backup(self):
        self.node.set("inesperado", "1")
        self.save()
        original = self.path.read_bytes()
        with self.assertRaises(captures.ErroCaptura):
            captures.repetir(self.path, pasta_capturas=self.captures)
        self.assertEqual(self.path.read_bytes(), original)
        self.assertFalse(list(self.path.parent.glob("sessao.backup-*.xml")))

    def test_end_ja_salvo_idempotente_nao_consume_outra_captura(self):
        self.movie()
        first = self.run_capture()
        original = self.path.read_bytes()
        other = self.movie("GTA5 outra tomada.mp4")
        result = captures.repetir(self.path, pasta_capturas=self.captures, intervalo=0.003, espera_maxima=0.2)
        self.assertEqual(first, result)
        self.assertTrue(other.exists())
        self.assertEqual(self.path.read_bytes(), original)
        self.assertEqual(len(list((self.data / "Serie").rglob("*.mp4"))), 1)

    def test_end_nao_associa_mp4_de_outro_aplicativo(self):
        source = self.movie("Outro aplicativo 2026-10-04.mp4")
        result = captures.repetir(self.path, pasta_capturas=self.captures, intervalo=0.003, espera_maxima=0.02)
        self.assertEqual(result["status"], "aguardando", result)
        self.assertTrue(source.exists())
        self.assertFalse((self.data / "Serie").exists())

    def test_end_ambiguo_nao_escolhe_mais_recente(self):
        first = self.movie("GTA5 primeira.mp4")
        second = self.movie("Grand Theft Auto V segunda.mp4")
        result = captures.repetir(self.path, pasta_capturas=self.captures, intervalo=0.003, espera_maxima=0.02)
        self.assertEqual(result["status"], "erro", result)
        self.assertIn("ambígua", result["mensagem"])
        self.assertEqual(first.read_bytes(), MP4)
        self.assertEqual(second.read_bytes(), MP4)
        self.assertFalse((self.data / "Serie").exists())

    def test_end_recupera_compromisso_apos_falha_resultado_sem_v2(self):
        source = self.movie()
        with patch.object(captures, "_result_write", side_effect=OSError("Falha depois do compromisso")):
            with self.assertRaises(OSError):
                self.run_capture()
        self.assertFalse(source.exists())
        result = captures.repetir(self.path, pasta_capturas=self.captures, intervalo=0.003, espera_maxima=0.2)
        self.assertEqual(result["status"], "salvo", result)
        self.assertEqual(result["versaoTomada"], "1")
        self.assertEqual(len(list((self.data / "Serie").rglob("*.mp4"))), 1)
        self.assertEqual(self.result_xml().get("status"), "salvo")

    def test_falha_resultado_depois_commit_retry_recupera_sem_nova_versao(self):
        source = self.movie()
        with patch.object(captures, "_result_write", side_effect=OSError("Falha de escrita do resultado")):
            with self.assertRaises(OSError):
                self.run_capture()
        self.assertFalse(source.exists())
        self.assertEqual(len(list((self.data / "Serie").rglob("*.mp4"))), 1)
        result = self.run_capture()
        self.assertEqual(result["status"], "salvo", result)
        self.assertEqual(result["versaoTomada"], "1")
        self.assertEqual(len(list((self.data / "Serie").rglob("*.mp4"))), 1)

    def test_falha_remocao_depois_commit_retry_conclui_par_existente(self):
        from contextlib import contextmanager
        source = self.movie()
        original_exclusive = captures._exclusive
        @contextmanager
        def exclusive_without_delete(path, **kwargs):
            with original_exclusive(path, **kwargs) as (stream, remove):
                if path == source and kwargs.get("delete"):
                    def denied():
                        raise captures.ErroCaptura("Remoção recusada pelo sistema")
                    yield stream, denied
                else:
                    yield stream, remove
        with patch.object(captures, "_exclusive", side_effect=exclusive_without_delete):
            first = self.run_capture()
        self.assertEqual(first["status"], "erro", first)
        self.assertTrue(source.exists())
        second = self.run_capture()
        self.assertEqual(second["status"], "salvo", second)
        self.assertFalse(source.exists())
        self.assertEqual(second["versaoTomada"], "1")

    def test_legenda_alterada_nao_associa_video(self):
        source = self.movie()
        self.subtitle.write_text("legenda alterada", encoding="utf-8")
        result = self.run_capture()
        self.assertEqual(result["status"], "erro")
        self.assertTrue(source.exists())
        self.assertFalse((self.data / "Serie").exists())

    def test_mp4_incompleto_aguarda_sem_mover(self):
        source = self.movie(data=box(b"ftyp", b"isom\x00\x00\x00\x00") + struct.pack(">I4s", 10000, b"mdat") + b"x")
        result = self.run_capture(espera_maxima=0.03)
        self.assertEqual(result["status"], "aguardando", result)
        self.assertTrue(source.exists())
        self.assertFalse((self.data / "Serie").exists())

    @unittest.skipUnless(os.name == "nt", "Teste do compartilhamento real CreateFile do Windows")
    def test_gravador_com_handle_aberto_impede_mover_ate_fechar(self):
        source = self.movie()
        with captures._exclusive(source):
            result = self.run_capture(espera_maxima=0.03)
        self.assertEqual(result["status"], "aguardando", result)
        self.assertTrue(source.exists())
        retry = self.run_capture()
        self.assertEqual(retry["status"], "salvo", retry)

    def test_segundo_processo_mesma_sessao_nao_substitui_resultado(self):
        self.movie()
        session = captures.ler_sessao(self.path, self.captures)
        with captures._exclusive(session.path.with_name("organizador.lock"), lock=True):
            result = self.run_capture()
        self.assertEqual(result["status"], "aguardando")
        self.assertFalse(self.path.with_name("resultado.xml").exists())

    def test_duas_sessoes_disputando_um_video_somente_uma_salva(self):
        source = self.movie()
        other, _ = self.session()
        with ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(self.run_capture, [self.path, other]))
        self.assertEqual(sum(result["status"] == "salvo" for result in results), 1, results)
        self.assertEqual(len(list((self.data / "Serie").rglob("*.mp4"))), 1)
        self.assertFalse(source.exists())

    def test_producao_destino_ep01_e_metadados_coerentes(self):
        scene = "EP01_C08"
        subtitle = self.data / "cenas" / (scene + ".srt")
        subtitle.write_bytes(self.subtitle.read_bytes())
        self.node.set("cenaId", scene)
        self.node.set("episodio", "1")
        self.node.set("numero", "8")
        self.node.find("srt").set("caminho", str(subtitle))
        self.save()
        self.movie()
        result = self.run_capture()
        self.assertEqual(result["status"], "salvo", result)
        self.assertEqual(Path(result["video"]).parent.name, "EP01")
        self.assertEqual(Path(result["video"]).name, "EP01_C08.mp4")

    def test_vinheta_destino_compartilhados(self):
        subtitle = self.data / "cenas" / "VINHETA_01.srt"
        subtitle.write_bytes(self.subtitle.read_bytes())
        self.node.set("cenaId", "VINHETA_01")
        self.node.find("srt").set("caminho", str(subtitle))
        self.save()
        self.movie()
        result = self.run_capture()
        self.assertEqual(result["status"], "salvo", result)
        self.assertEqual(Path(result["video"]).parent.name, "COMPARTILHADOS")

    def test_id_episodio_incompativel_rejeitado(self):
        self.node.set("cenaId", "EP01_C08")
        self.save()
        with self.assertRaisesRegex(captures.ErroCaptura, "não correspondem"):
            captures.ler_sessao(self.path, self.captures)

    def test_path_escape_legenda_rejeitado(self):
        self.node.find("srt").set("caminho", str(self.data / "cenas" / ".." / "fora.srt"))
        self.save()
        with self.assertRaises(captures.ErroCaptura):
            captures.ler_sessao(self.path, self.captures)

    def test_pasta_capturas_nao_verificada_rejeitada(self):
        other = self.profile / "outra"
        other.mkdir()
        with self.assertRaisesRegex(captures.ErroCaptura, "difere"):
            captures.ler_sessao(self.path, other)

    def test_reparse_ancestral_recusado(self):
        from types import SimpleNamespace
        original = Path.lstat
        def information(path):
            result = original(path)
            if path == self.profile:
                return SimpleNamespace(st_mode=result.st_mode, st_file_attributes=stat.FILE_ATTRIBUTE_REPARSE_POINT)
            return result
        with patch.object(Path, "lstat", information):
            with self.assertRaisesRegex(captures.ErroCaptura, "reparse"):
                captures.ler_sessao(self.path, self.captures)

    def test_doctype_entidade_e_utf16_recusados(self):
        for raw in (b'<!DOCTYPE captura [<!ENTITY x "expansao">]><captura>&x;</captura>',
                    '<captura />'.encode("utf-16")):
            with self.subTest(raw=raw[:20]):
                self.path.write_bytes(raw)
                with self.assertRaises(captures.ErroCaptura):
                    captures.ler_sessao(self.path, self.captures)

    def test_campos_desconhecidos_e_duplicatas_snapshot_recusados(self):
        self.node.set("inesperado", "1")
        self.save()
        with self.assertRaises(captures.ErroCaptura):
            captures.ler_sessao(self.path, self.captures)
        del self.node.attrib["inesperado"]
        source = self.movie()
        self.baseline(source)
        self.baseline(source)
        with self.assertRaisesRegex(captures.ErroCaptura, "duplicado"):
            captures.ler_sessao(self.path, self.captures)

    def test_srt_versionado_real_e_conservado(self):
        subtitle = self.subtitle.with_name(self.scene + "_v12.srt")
        subtitle.write_bytes(self.subtitle.read_bytes())
        self.node.find("srt").set("caminho", str(subtitle))
        self.save()
        self.movie()
        result = self.run_capture()
        self.assertEqual(result["status"], "salvo", result)
        self.assertTrue(subtitle.exists())
        self.assertEqual(Path(result["legenda"]).stem, self.scene)

    def test_ids_tecnicos_xml_legados_aceitos_com_zero(self):
        for scene in ("teste_dupla", "teste_falas", "teste_camera", "teste_cortes", "teste_ambiente", "teste_coreografia"):
            with self.subTest(scene=scene):
                subtitle = self.data / "cenas" / (scene + ".srt")
                subtitle.write_bytes(self.subtitle.read_bytes())
                self.node.set("cenaId", scene)
                self.node.find("srt").set("caminho", str(subtitle))
                self.save()
                session = captures.ler_sessao(self.path, self.captures)
                self.assertEqual(session.scene, scene)

    def test_previews_elenco_explicitos_sao_testes_e_preservam_ids(self):
        for scene in ("teste_elenco_pobre", "teste_elenco_rica"):
            with self.subTest(scene=scene):
                subtitle = self.data / "cenas" / (scene + ".srt")
                subtitle.write_bytes(self.subtitle.read_bytes())
                self.node.set("cenaId", scene)
                self.node.find("srt").set("caminho", str(subtitle))
                self.save()
                session = captures.ler_sessao(self.path, self.captures)
                self.assertEqual((session.scene, session.episode, session.number), (scene, 0, 0))
        self.node.set("cenaId", "teste_elenco_outro")
        self.save()
        with self.assertRaises(captures.ErroCaptura):
            captures.ler_sessao(self.path, self.captures)

    def test_validacao_caixas_extensao_tamanho_zero_e_truncamento(self):
        extended = struct.pack(">I4sQ", 1, b"mdat", 17) + b"x"
        metadata = movie_bytes().split(b"mdat")[0][:-4]
        captures.validar_mp4(io.BytesIO(metadata + extended))
        captures.validar_mp4(io.BytesIO(metadata + struct.pack(">I4s", 0, b"mdat") + b"x"))
        self.assertEqual(captures.validar_mp4(io.BytesIO(movie_bytes(15200, version=1)), 15000), 15200)
        for raw in (MP4[:-1], MP4 + b"x", box(b"ftyp", b"isom\x00\x00\x00\x00")):
            with self.subTest(raw=raw[-12:]):
                with self.assertRaises(captures.ErroCaptura):
                    captures.validar_mp4(io.BytesIO(raw))


if __name__ == "__main__":
    unittest.main()
