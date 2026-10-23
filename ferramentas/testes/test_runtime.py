"""Regressões de preservação e confinamento; destinos temporários dentro do workspace."""

import json
import os
from pathlib import Path
import stat
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

WORKSPACE = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(WORKSPACE / "ferramentas"))

import runtime_diretor
from runtime_diretor import ErroRuntime, exigir_sem_vinculos, ler_pasta_dados, publicar_runtime


class RuntimeTests(unittest.TestCase):
    def setUp(self):
        owned_parent = WORKSPACE / "backups" / "testes-runtime"
        owned_parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="caso-", dir=owned_parent)
        self.root = Path(self.temporary.name)
        self.assertTrue(self.root.resolve().is_relative_to(WORKSPACE.resolve()))
        self.workspace = self.root / "projeto"
        self.profile = self.root / "perfil"
        self.profile.mkdir()
        self.data = self.profile / "GTA_Diretor"
        for folder in ("cenas", "catalogos", "logs", "configuracoes"):
            (self.workspace / folder).mkdir(parents=True, exist_ok=True)
        (self.workspace / "cenas" / "teste_json.json").write_text('{"versao":1}', encoding="utf-8")
        (self.workspace / "cenas" / "teste_ambiente_v2.srt").write_text("SRT anterior", encoding="utf-8")
        (self.workspace / "catalogos" / "locais_marcados.xml.backup-real.xml").write_text("marca anterior", encoding="utf-8")
        (self.workspace / "logs" / "DiretorGTA.log").write_text("log real legado", encoding="utf-8")
        (self.workspace / "configuracoes" / "Instalacao.ini").write_text(
            "[Projeto]\nDados=" + str(self.data) + "\n", encoding="utf-8")

    def tearDown(self):
        # Apagar somente a pasta nova do teste, já conferida dentro do workspace.
        self.assertTrue(self.root.absolute().is_relative_to(WORKSPACE.absolute()))
        self.temporary.cleanup()

    def publish(self):
        return publicar_runtime(self.workspace, perfil=self.profile)

    def test_copia_dados_reais_com_hashes_e_backup(self):
        result = self.publish()
        self.assertEqual(result["Criados"], 4)
        self.assertEqual((self.data / "logs" / "DiretorGTA.log").read_text(), "log real legado")
        record = json.loads(Path(result["Manifesto"]).read_text(encoding="utf-8"))
        for item in record["Arquivos"]:
            self.assertTrue(Path(item["BackupOrigem"]).is_file())
        conclusion = json.loads(Path(result["Manifesto"]).with_name("conclusao.json").read_text(encoding="utf-8"))
        self.assertTrue(all(item["SHA256Origem"] == item["SHA256DestinoDepois"] for item in conclusion["Arquivos"]))

    def test_preserva_json_editado_srt_logs_e_captura(self):
        self.publish()
        scene = self.data / "cenas" / "teste_json.json"
        subtitle = self.data / "cenas" / "teste_ambiente_v2.srt"
        log = self.data / "logs" / "DiretorGTA.log"
        scene.write_text("JSON editado pelo usuario", encoding="utf-8")
        subtitle.write_text("legenda da tomada", encoding="utf-8")
        log.write_text("log novo do runtime", encoding="utf-8")
        capture = self.data / "Serie" / "EP01" / "EP01_C08_v2.mp4"
        capture.parent.mkdir(parents=True)
        capture.write_bytes(b"captura existente")
        result = self.publish()
        self.assertEqual(result["Preservados"], 3)
        self.assertEqual(scene.read_text(), "JSON editado pelo usuario")
        self.assertEqual(subtitle.read_text(), "legenda da tomada")
        self.assertEqual(log.read_text(), "log novo do runtime")
        self.assertEqual(capture.read_bytes(), b"captura existente")
        manifest = json.loads(Path(result["Manifesto"]).read_text(encoding="utf-8"))
        scene_record = next(item for item in manifest["Arquivos"] if item["Arquivo"] == str(Path("cenas") / "teste_json.json"))
        self.assertEqual(Path(scene_record["BackupDestinoAnterior"]).read_text(), "JSON editado pelo usuario")

    def test_repeticao_igual_nao_sobrescreve_arquivo(self):
        self.publish()
        scene = self.data / "cenas" / "teste_json.json"
        before = scene.stat().st_mtime_ns
        result = self.publish()
        self.assertEqual(result["Criados"], 0)
        self.assertEqual(result["Iguais"], 4)
        self.assertEqual(scene.stat().st_mtime_ns, before)

    def test_novo_seed_aparece_sem_substituir_anteriores(self):
        self.publish()
        new_file = self.workspace / "catalogos" / "animacoes.json"
        new_file.write_text('{"versao":1}', encoding="utf-8")
        result = self.publish()
        self.assertEqual(result["Criados"], 1)
        self.assertTrue((self.data / "catalogos" / "animacoes.json").is_file())

    def test_rejeita_saida_por_ponto_ponto(self):
        with self.assertRaises(ErroRuntime):
            publicar_runtime(self.workspace, self.data / ".." / "fora", self.profile)
        self.assertFalse(self.data.exists())

    def test_rejeita_destino_fora_do_perfil(self):
        with self.assertRaises(ErroRuntime):
            publicar_runtime(self.workspace, self.root / "fora", self.profile)
        self.assertFalse((self.root / "fora").exists())

    def test_rejeita_reparse_na_raiz_antes_de_copiar(self):
        original = Path.lstat
        def fake_lstat(path, *args, **kwargs):
            if path == self.data:
                return SimpleNamespace(st_mode=stat.S_IFDIR, st_file_attributes=stat.FILE_ATTRIBUTE_REPARSE_POINT)
            return original(path, *args, **kwargs)
        with patch.object(Path, "lstat", fake_lstat):
            with self.assertRaises(ErroRuntime):
                self.publish()
        self.assertFalse(self.data.exists())

    def test_rejeita_reparse_ancestral(self):
        original = Path.lstat
        def fake_lstat(path, *args, **kwargs):
            if path == self.profile:
                return SimpleNamespace(st_mode=stat.S_IFDIR, st_file_attributes=stat.FILE_ATTRIBUTE_REPARSE_POINT)
            return original(path, *args, **kwargs)
        with patch.object(Path, "lstat", fake_lstat):
            with self.assertRaises(ErroRuntime):
                self.publish()
        self.assertFalse(self.data.exists())

    def test_rejeita_link_real_quando_windows_permite(self):
        target = self.root / "destino-link"
        target.mkdir()
        try:
            self.data.symlink_to(target, target_is_directory=True)
        except OSError:
            self.skipTest("Windows não autorizou criar symlink; metadados reparse testados separadamente.")
        with self.assertRaises(ErroRuntime):
            self.publish()
        self.assertEqual(list(target.iterdir()), [])

    def test_rejeita_dados_onedrive(self):
        cloud = self.root / "OneDrive - Empresa"
        cloud.mkdir()
        with self.assertRaises(ErroRuntime):
            publicar_runtime(self.workspace, cloud / "GTA_Diretor", cloud)

    def test_logs_respeitam_dados_bootstrap_e_legado(self):
        self.assertEqual(ler_pasta_dados(self.workspace), self.data)
        (self.workspace / "configuracoes" / "Instalacao.ini").write_text("[Projeto]\nPasta=origem\n")
        self.assertEqual(ler_pasta_dados(self.workspace), self.workspace)

    def test_sem_userprofile_nao_presume_home(self):
        with patch.dict(os.environ, {}, clear=True):
            with self.assertRaises(ErroRuntime):
                publicar_runtime(self.workspace)

    def test_log_legado_mudando_aborta_antes_da_primeira_copia_ao_perfil(self):
        original_copy = runtime_diretor._copiar_novo
        legacy_log = self.workspace / "logs" / "DiretorGTA.log"
        def changing_log(source, destination, root, expected):
            original_copy(source, destination, root, expected)
            if source == legacy_log:
                legacy_log.write_text("novo evento durante o snapshot", encoding="utf-8")
        with patch.object(runtime_diretor, "_copiar_novo", changing_log):
            with self.assertRaisesRegex(ErroRuntime, "repita a publicação"):
                self.publish()
        self.assertFalse(self.data.exists())
        failures = list((self.workspace / "backups" / "runtime").glob("*/falha.json"))
        self.assertEqual(len(failures), 1)

    def test_rejeita_reparse_em_catalogo_de_origem(self):
        original = Path.lstat
        def fake_lstat(path, *args, **kwargs):
            if path == self.workspace / "catalogos":
                return SimpleNamespace(st_mode=stat.S_IFDIR, st_file_attributes=stat.FILE_ATTRIBUTE_REPARSE_POINT)
            return original(path, *args, **kwargs)
        with patch.object(Path, "lstat", fake_lstat):
            with self.assertRaises(ErroRuntime):
                self.publish()
        self.assertFalse(self.data.exists())


if __name__ == "__main__":
    unittest.main()
