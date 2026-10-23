"""Preparação da fila e recusa antes de alterar scripts do jogo, com operações simuladas."""

from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import call, patch

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
import diretor


class PublicacaoFila(unittest.TestCase):
    def setUp(self):
        self.temporario = tempfile.TemporaryDirectory(prefix="fila-cli-", dir=RAIZ / "ferramentas" / "testes")
        self.pasta = Path(self.temporario.name).resolve()
        self.assertTrue(self.pasta.is_relative_to(RAIZ))
        self.addCleanup(self.temporario.cleanup)
        self.patch_raiz = patch.object(diretor, "RAIZ", self.pasta)
        self.patch_raiz.start()
        self.addCleanup(self.patch_raiz.stop)
        (self.pasta / "configuracoes").mkdir()
        (self.pasta / "cenas").mkdir()

    def configurar(self, fila):
        (self.pasta / "configuracoes" / "DiretorGTA.ini").write_text(
            "[Fila]\nArquivo=" + fila + "\n[Cena]\nInicial=teste_json.json\n"
            "[Pastas]\nCenas=C:/perfil-ainda-nao-publicado/GTA_Diretor/cenas\n", encoding="utf-8")

    def test_workspace_valida_fila_antes_de_ler_runtime(self):
        self.configurar("fila_teste.json")
        with patch.object(diretor, "ler_pasta_dados", side_effect=AssertionError("runtime lido antes de publicar")), \
                patch.object(diretor, "validar") as validar:
            diretor.validar_cena_inicial(workspace=True)
        validar.assert_called_once_with(self.pasta / "cenas" / "fila_teste.json", self.pasta / "catalogos")

    def test_fila_vazia_conserva_cena_individual(self):
        self.configurar("  ")
        with patch.object(diretor, "validar") as validar:
            diretor.validar_cena_inicial(workspace=True)
        validar.assert_called_once_with(self.pasta / "cenas" / "teste_json.json", self.pasta / "catalogos")

    def test_fila_invalida_no_runtime_impede_copia_no_jogo(self):
        jogo = self.pasta / "jogo-simulado"
        jogo.mkdir()
        for nome in ("GTA5.exe", "ScriptHookV.dll", "dinput8.dll"):
            (jogo / nome).write_bytes(b"arquivo ficticio de teste; nao executado")
        source = self.pasta / "fonte-simulada.cs"
        source.write_bytes(b"fonte simulada")
        fontes = [{"origem": source, "relativo": Path("scripts/DiretorGTA.3.cs"), "conteudo": source.read_bytes()}]
        with patch.object(diretor, "exigir_jogo_fechado"), \
                patch.object(diretor, "fontes_instalacao", return_value=fontes), \
                patch.object(diretor, "validar"), \
                patch.object(diretor, "compilar"), \
                patch.object(diretor, "codigo_completo", return_value=source.read_bytes()), \
                patch.object(diretor, "publicar_dados") as publicar, \
                patch.object(diretor, "validar_cena_inicial", side_effect=[None, diretor.ErroDiretor("fila inválida")]) as inicial, \
                patch.object(diretor, "nova_pasta_backup") as backup:
            with self.assertRaisesRegex(diretor.ErroDiretor, "fila inválida"):
                diretor.instalar(jogo)
        self.assertEqual(inicial.call_args_list, [call(workspace=True), call()])
        publicar.assert_called_once_with()
        backup.assert_not_called()
        self.assertFalse((jogo / "scripts").exists())


if __name__ == "__main__":
    unittest.main()
