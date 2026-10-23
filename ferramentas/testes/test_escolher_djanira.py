"""Escolha/transação em perfil sintético; não comprova aparência no GTA."""
from copy import deepcopy
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock
import uuid
import xml.etree.ElementTree as ET

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
import escolher_djanira as escolha
import corrigir_figurinos as transacao
import importar_visuais as visuais
from test_visual import fixture, raw


class EscolherDjaniraTests(unittest.TestCase):
    def setUp(self):
        parent = RAIZ / "backups/testes-escolha"
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="caso-", dir=parent)
        self.base = Path(self.temp.name)
        self.workspace = self.base / "workspace"
        self.user = self.base / "perfil"
        self.profile = self.user / "GTA_Diretor"
        (self.workspace / "configuracoes").mkdir(parents=True)
        (self.workspace / "backups").mkdir()
        (self.profile / "catalogos/visuais/opcoes").mkdir(parents=True)
        (self.profile / "logs").mkdir()
        (self.workspace / "configuracoes/Instalacao.ini").write_text(
            "[Projeto]\nDados=" + str(self.profile) + "\n", encoding="utf-8")
        self.patches = [mock.patch.object(escolha, "RAIZ", self.workspace),
                        mock.patch.object(transacao, "RAIZ", self.workspace),
                        mock.patch.dict(os.environ, {"USERPROFILE": str(self.user)})]
        for patch in self.patches:
            patch.start()
        self.addCleanup(self.cleanup)
        manifest = ET.Element("elencoGerado", {"versao": "1", "seed": "1234"})
        catalog = {"versao": 1, "personagens": []}
        for code in visuais.CODIGOS:
            tree = fixture()
            ET.SubElement(tree, "FixtureId").text = code
            content = raw(tree)
            visual = visuais.validar_visual_bytes(content, "mp_f_freemode_01")
            self.write("catalogos/visuais/" + code + ".xml", content)
            ET.SubElement(manifest, "visual", {"codigo": code, "modelo": visual["modelo"],
                "arquivo": code + ".xml", "sha256": visual["sha256"], "faceSHA256": visual["faceSHA256"]})
            catalog["personagens"].append({"codigo": code, "modelo": visual["modelo"],
                "visualArquivo": code + ".xml", "visualSHA256": visual["sha256"],
                "pronto": True, "estado": "gerado_no_jogo; teste_visual_pendente"})
        manifest.set("djaniraFaceSHA256", visual["faceSHA256"])
        self.write(transacao.MANIFESTO, raw(manifest))
        self.write(transacao.CATALOGO, transacao.json_bytes(catalog))
        self.write(escolha.SEMENTE, b'<sementeElenco versao="1" seed="1234"/>')
        self.write("catalogos/figurinos_continuidade_0_9_6.json", b'{"historico": "preservado"}\n')
        self.original = escolha._ler_conjunto(self.profile)
        self.old = transacao.sha(self.original[escolha.RICA])
        self.make_option(2)

    def cleanup(self):
        for patch in reversed(self.patches):
            patch.stop()
        self.assertTrue(self.base.resolve().is_relative_to(RAIZ.resolve()))
        self.temp.cleanup()

    def write(self, name, content):
        path = self.profile / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)
        return path

    def make_option(self, number, mutate=None):
        tree = ET.fromstring(self.original[escolha.RICA])
        tree.find("PedProperties/PedComps/_11").text = "12,1"
        tree.find("PedProperties/PedComps/_2").text = "4,0"
        for slot in (4, 5, 8):
            tree.find(f"PedProperties/HeadFeatures/Overlays/_{slot}").set("index", "1")
        if mutate:
            mutate(tree)
        meta = ET.SubElement(tree, "DiretorOpcao")
        for key, value in {"Numero": str(number), "Preset": "preset_sintetico",
                           "SHA256CanonicoOrigem": self.old, "Estado": "getters conferidos",
                           "Fonte": "https://example.invalid/fixture"}.items():
            ET.SubElement(meta, key).text = value
        self.candidate = raw(tree)
        self.visual = visuais.validar_visual_bytes(self.candidate, "mp_f_freemode_01")
        self.candidate_path = self.write(f"catalogos/visuais/opcoes/DJANIRA_RICA_opcao{number}_{self.visual['sha256']}.xml", self.candidate)
        self.proof_tree = self.proof(self.visual, number)
        self.proof_path = self.write("logs/conferencia_visual_DJANIRA_RICA_" + uuid.uuid4().hex + ".xml", raw(self.proof_tree))
        self.request = {"versao": 1, "opcao": number, "visualSHA256": self.visual["sha256"], "origemSHA256": self.old}
        self.request_path = self.write("logs/escolha_djanira_" + uuid.uuid4().hex + ".json", transacao.json_bytes(self.request))
        self.result_path = self.request_path.with_name(self.request_path.stem + "_resultado.json")

    @staticmethod
    def proof(visual, option):
        head = visual["rosto"]
        root = ET.Element("ConferenciaVisual", {"versao": "1", "codigo": "DJANIRA_RICA",
            "cena": "teste_fila_" + str(93 + option), "modelo": visual["modelo"],
            "visualSHA256": visual["sha256"], "faceArquivoSHA256": visual["faceSHA256"],
            "olhosGetter": str(head["olhos"])})
        for tag, values in (("Componente", visual["componentes"]), ("Acessorio", visual["acessorios"])):
            for slot, pair in enumerate(values):
                ET.SubElement(root, tag, {"slot": str(slot), "drawable": str(pair[0]), "textura": str(pair[1])})
        ET.SubElement(root, "DNAGetter", {"pais": ",".join(map(str, head["pais"])),
            "misturas": ",".join(map(str, head["misturas"])), "isParent": "true" if head["isParent"] else "false"})
        for slot, overlay in enumerate(head["overlays"]):
            ET.SubElement(root, "Overlay", {"slot": str(slot), "indiceGetter": str(overlay[0]),
                "corXML": str(overlay[1]), "segundaCorXML": str(overlay[2]), "opacidadeXML": str(overlay[3])})
        return root

    def assert_original(self):
        for name, content in self.original.items():
            self.assertEqual((self.profile / name).read_bytes(), content, name)

    def save_proof(self):
        self.proof_path.write_bytes(raw(self.proof_tree))

    def test_escolha_valida_salva_raw_treze_continuidade_sem_mudar_seed_ou_doze(self):
        result = escolha.selecionar(self.request_path)
        self.assertEqual(result["status"], "salvo")
        self.assertEqual((self.profile / escolha.RICA).read_bytes(), self.candidate)
        for name, content in self.original.items():
            if name not in {escolha.RICA, transacao.MANIFESTO, transacao.CATALOGO}:
                self.assertEqual((self.profile / name).read_bytes(), content, name)
        final = escolha._ler_conjunto(self.profile)
        visuals = transacao.verificar_conjunto(final)
        continuity = json.loads(Path(result["continuidade"]).read_text(encoding="utf-8"))
        self.assertEqual(continuity["personagens"], visuals)
        self.assertEqual(continuity["seed"], "1234")
        self.assertEqual(continuity["provaGettersSHA256"], transacao.sha(self.proof_path.read_bytes()))
        self.assertEqual(continuity["escolha"], self.request)
        backup = Path(result["backup"])
        for name, content in self.original.items():
            self.assertEqual((backup / "perfil" / name).read_bytes(), content)
        self.assertEqual(json.loads(self.result_path.read_text(encoding="utf-8")), result)

    def test_repetir_mesmo_pedido_e_idempotente_e_nao_cria_novo_backup(self):
        first = escolha.selecionar(self.request_path)
        snapshot = {path: path.read_bytes() for path in self.profile.rglob("*") if path.is_file()}
        self.assertEqual(escolha.selecionar(self.request_path), first)
        self.assertEqual(snapshot, {path: path.read_bytes() for path in self.profile.rglob("*") if path.is_file()})
        self.assertEqual(len(list((self.workspace / "backups/escolha-djanira").iterdir())), 1)

    def test_pedido_requer_tecla_estruturada_sem_chaves_duplicadas_tipos_bool_ou_extra(self):
        invalids = [dict(self.request, confirmado=True), dict(self.request, versao=True),
                    dict(self.request, opcao=True), dict(self.request, opcao=4), dict(self.request, visualSHA256="x")]
        for request in invalids:
            with self.subTest(request=request), self.assertRaises(ValueError):
                escolha.ler_pedido(transacao.json_bytes(request))
        duplicate = transacao.json_bytes(self.request).replace(b'"versao": 1', b'"versao": 1, "versao": 1')
        with self.assertRaisesRegex(ValueError, "duplicada"):
            escolha.ler_pedido(duplicate)
        wrong_path = self.write("logs/pedido.json", transacao.json_bytes(self.request))
        with self.assertRaisesRegex(ValueError, "tecla"):
            escolha.selecionar(wrong_path)
        self.assert_original()

    def test_sem_prova_native_nao_publica(self):
        self.proof_path.unlink()
        with self.assertRaisesRegex(ValueError, "getters"):
            escolha.selecionar(self.request_path)
        self.assert_original()

    def test_relatorio_antigo_sem_dna_nao_e_prova(self):
        self.proof_tree.remove(self.proof_tree.find("DNAGetter"))
        self.save_proof()
        with self.assertRaisesRegex(ValueError, "DNAGetter"):
            escolha.selecionar(self.request_path)
        self.assert_original()

    def test_prova_exige_22_pecas_sem_duplicados_e_cena_da_opcao(self):
        original = deepcopy(self.proof_tree)
        def mutate_missing(tree):
            tree.remove(tree.find("Componente"))
        def mutate_duplicate(tree):
            tree.append(deepcopy(tree.find("Acessorio")))
        def mutate_wrong_scene(tree):
            tree.set("cena", "teste_fila_91")
        def mutate_getter(tree):
            tree.find("Componente").set("drawable", "99")
        for mutation in (mutate_missing, mutate_duplicate, mutate_wrong_scene, mutate_getter):
            self.proof_tree = deepcopy(original)
            mutation(self.proof_tree)
            self.save_proof()
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                escolha.selecionar(self.request_path)
            self.assert_original()

    def test_dna_olhos_overlays_reais_nao_podem_divergir(self):
        original = deepcopy(self.proof_tree)
        cases = [("DNAGetter", "pais", "1,0,0,0,0,0"),
                 ("DNAGetter", "misturas", "NaN,0.5,0.5"),
                 ("DNAGetter", "isParent", "true"),
                 ("Overlay", "indiceGetter", "0"), ("Overlay", "opacidadeXML", "0.1")]
        for tag, attr, value in cases:
            self.proof_tree = deepcopy(original)
            self.proof_tree.find(tag).set(attr, value)
            self.save_proof()
            with self.subTest(attr=attr), self.assertRaises(ValueError):
                escolha.selecionar(self.request_path)
            self.assert_original()
        self.proof_tree = deepcopy(original)
        self.proof_tree.set("olhosGetter", "1")
        self.save_proof()
        with self.assertRaises(ValueError):
            escolha.selecionar(self.request_path)

    def test_maquiagem_4_5_8_permitida_mas_dna_envelhecimento_mascara_proibidos(self):
        mutations = [lambda t: setattr(t.find("PedProperties/HeadFeatures/ShapeAndSkinTone/ShapeFatherId"), "text", "1"),
                     lambda t: t.find("PedProperties/HeadFeatures/Overlays/_3").set("index", "1"),
                     lambda t: setattr(t.find("PedProperties/PedComps/_1"), "text", "1,0")]
        for mutation in mutations:
            self.make_option(2, mutation)
            with self.subTest(mutation=mutation), self.assertRaisesRegex(ValueError, "identidade"):
                escolha.selecionar(self.request_path)
            self.assert_original()

    def test_alteracao_canonica_posterior_e_preservada(self):
        changed = self.original[escolha.RICA] + b"\n"
        self.write(escolha.RICA, changed)
        with self.assertRaises(ValueError):
            escolha.selecionar(self.request_path)
        self.assertEqual((self.profile / escolha.RICA).read_bytes(), changed)

    def test_semente_e_opcao_alteradas_antes_commit_sao_preservadas(self):
        self.write(escolha.SEMENTE, b'<sementeElenco versao="1" seed="9999"/>')
        with self.assertRaisesRegex(ValueError, "Semente"):
            escolha.selecionar(self.request_path)
        self.assertEqual((self.profile / escolha.SEMENTE).read_bytes(), b'<sementeElenco versao="1" seed="9999"/>')
        self.write(escolha.SEMENTE, self.original[escolha.SEMENTE])
        self.candidate_path.write_bytes(self.candidate + b"\n")
        with self.assertRaisesRegex(ValueError, "hash escolhido"):
            escolha.selecionar(self.request_path)
        self.assertEqual(self.candidate_path.read_bytes(), self.candidate + b"\n")
        self.assert_original()

    def test_prop_ausente_textura_zero_aceita_indice_presente_divergente_rejeita(self):
        self.proof_tree.find("Acessorio").set("textura", "0")
        escolha.conferir_prova(raw(self.proof_tree), self.visual, self.request["opcao"])
        self.proof_tree.find("Acessorio").set("drawable", "0")
        with self.assertRaisesRegex(ValueError, "Getter divergente"):
            escolha.conferir_prova(raw(self.proof_tree), self.visual, self.request["opcao"])

    def test_registro_continuidade_preexistente_divergente_preservado(self):
        guid = escolha.PADRAO_PEDIDO.fullmatch(self.request_path.name)[1]
        continuity = self.write("catalogos/figurinos_continuidade_0_9_7_" + guid + ".json", b'{"preservar":true}')
        with self.assertRaisesRegex(ValueError, "continuidade anterior"):
            escolha.selecionar(self.request_path)
        self.assertEqual(continuity.read_bytes(), b'{"preservar":true}')
        self.assert_original()

    def test_falha_de_publicacao_reverte_tres_arquivos_reusa_journal(self):
        real_write = transacao.escrever
        failed = False
        def fail_once(path, content, root):
            nonlocal failed
            if path == self.profile / transacao.CATALOGO and not failed:
                failed = True
                raise OSError("falha simulada de escrita")
            return real_write(path, content, root)
        with mock.patch.object(transacao, "escrever", side_effect=fail_once), self.assertRaisesRegex(OSError, "simulada"):
            escolha.selecionar(self.request_path)
        self.assert_original()
        journal = next((self.workspace / "backups/escolha-djanira").glob("*/escolha-0.9.7/plano.json"))
        self.assertEqual(json.loads(journal.read_text(encoding="utf-8"))["estado"], "revertida")
        self.assertFalse(self.result_path.exists())
        self.assertEqual(escolha.selecionar(self.request_path)["status"], "salvo")

    def test_edicao_concorrente_nao_sobrescrita_rollback_preserva_terceiro(self):
        real_write = transacao.escrever
        external = b'{"edicaoConcorrente":"preservar"}'
        changed = False
        def change_before_write(path, content, root):
            nonlocal changed
            if path == self.profile / escolha.RICA and not changed:
                changed = True
                self.write(transacao.CATALOGO, external)
            return real_write(path, content, root)
        with mock.patch.object(transacao, "escrever", side_effect=change_before_write), self.assertRaisesRegex(ValueError, "concorrente"):
            escolha.selecionar(self.request_path)
        self.assertEqual((self.profile / escolha.RICA).read_bytes(), self.original[escolha.RICA])
        self.assertEqual((self.profile / transacao.MANIFESTO).read_bytes(), self.original[transacao.MANIFESTO])
        self.assertEqual((self.profile / transacao.CATALOGO).read_bytes(), external)
        journal = next((self.workspace / "backups/escolha-djanira").glob("*/escolha-0.9.7/plano.json"))
        self.assertEqual(json.loads(journal.read_text(encoding="utf-8"))["estado"], "recuperacao_pendente")

    def test_lock_existente_e_resultado_anterior_sao_preservados(self):
        lock = self.write("logs/escolha_djanira_em_andamento.lock", b"outra escolha")
        with self.assertRaisesRegex(ValueError, "andamento"):
            escolha.selecionar(self.request_path)
        self.assertEqual(lock.read_bytes(), b"outra escolha")
        lock.unlink()
        self.result_path.write_bytes(b'{"status":"erro","mensagem":"anterior"}')
        with self.assertRaisesRegex(ValueError, "já terminou"):
            escolha.selecionar(self.request_path)
        self.assertEqual(self.result_path.read_bytes(), b'{"status":"erro","mensagem":"anterior"}')
        self.assert_original()

    def test_seed_concorrente_preservada_e_escolha_revertida_pelo_journal(self):
        real_write = transacao.escrever
        changed = False
        external_seed = b'<sementeElenco versao="1" seed="7777"/>'
        def change_seed(path, content, root):
            nonlocal changed
            if path == self.profile / escolha.RICA and not changed:
                changed = True
                self.write(escolha.SEMENTE, external_seed)
            return real_write(path, content, root)
        with mock.patch.object(transacao, "escrever", side_effect=change_seed), self.assertRaisesRegex(ValueError, "protegido mudou"):
            escolha.selecionar(self.request_path)
        for name, content in self.original.items():
            self.assertEqual((self.profile / name).read_bytes(), external_seed if name == escolha.SEMENTE else content)
        journal = next((self.workspace / "backups/escolha-djanira").glob("*/escolha-0.9.7/plano.json"))
        self.assertEqual(json.loads(journal.read_text(encoding="utf-8"))["estado"], "revertida")
        self.assertFalse(self.result_path.exists())

    def test_cli_erro_grava_resultado_separado_e_exit1(self):
        self.proof_path.unlink()
        with mock.patch("sys.stderr"):
            self.assertEqual(escolha.main(["--pedido", str(self.request_path)]), 1)
        result = json.loads(self.result_path.read_text(encoding="utf-8"))
        self.assertEqual(result["status"], "erro")
        self.assert_original()

    def test_resultado_previo_salvo_divergente_rejeitado_antes_publicar(self):
        previous = {"versao": 1, "status": "salvo", "opcao": 1, "visualSHA256": "A" * 64,
                    "origemSHA256": self.old, "mensagem": "resultado anterior"}
        content = transacao.json_bytes(previous)
        self.result_path.write_bytes(content)
        with self.assertRaisesRegex(ValueError, "antes da publicação"):
            escolha.selecionar(self.request_path)
        self.assert_original()
        self.assertEqual(self.result_path.read_bytes(), content)
        self.assertFalse((self.workspace / "backups/escolha-djanira").exists())

    def test_lock_violation_tentativa_limitada_sem_retry_access_denied(self):
        real_write = transacao.escrever
        calls = 0
        def sharing_once(path, content, root):
            nonlocal calls
            if path == self.profile / transacao.MANIFESTO:
                calls += 1
                if calls == 1:
                    error = OSError("sharing violation simulado")
                    error.winerror = 32
                    raise error
            return real_write(path, content, root)
        with mock.patch.object(transacao, "escrever", side_effect=sharing_once):
            self.assertEqual(escolha.selecionar(self.request_path)["status"], "salvo")
        self.assertEqual(calls, 2)

    def test_access_denied_nao_tenta_contornar_e_reverte(self):
        real_write = transacao.escrever
        calls = 0
        def denied(path, content, root):
            nonlocal calls
            if path == self.profile / transacao.MANIFESTO:
                calls += 1
                error = OSError("access denied simulado")
                error.winerror = 5
                raise error
            return real_write(path, content, root)
        with mock.patch.object(transacao, "escrever", side_effect=denied), self.assertRaises(OSError):
            escolha.selecionar(self.request_path)
        self.assertEqual(calls, 1)
        self.assert_original()

    def test_criacao_exclusiva_atomica_nao_deixa_json_parcial_se_fsync_falha(self):
        path = self.profile / "logs/novo.json"
        with mock.patch.object(escolha.os, "fsync", side_effect=OSError("fsync simulado")), self.assertRaises(OSError):
            escolha._novo(path, b'{"completo":true}', self.profile)
        self.assertFalse(path.exists())
        self.assertEqual(list(path.parent.glob(path.name + ".tmp-*")), [])
        path.write_bytes(b'{"original":true}')
        with self.assertRaises(FileExistsError):
            escolha._novo(path, b'{"outro":true}', self.profile)
        self.assertEqual(path.read_bytes(), b'{"original":true}')

    def test_falha_continuidade_pos_commit_retorna_pendente_e_retoma_journal(self):
        real_new = escolha._novo
        failed = False
        def fail_continuity(path, content, root):
            nonlocal failed
            if path.parent == self.profile / "catalogos" and path.name.startswith("figurinos_continuidade_0_9_7_") and not failed:
                failed = True
                raise OSError("registro continuidade simulado")
            return real_new(path, content, root)
        with mock.patch.object(escolha, "_novo", side_effect=fail_continuity), mock.patch("sys.stderr"):
            self.assertEqual(escolha.main(["--pedido", str(self.request_path)]), 2)
        pending_raw = self.result_path.read_bytes()
        pending = json.loads(pending_raw)
        self.assertEqual(pending["status"], "registro_pendente")
        self.assertEqual((self.profile / escolha.RICA).read_bytes(), self.candidate)
        self.assertFalse(Path(pending["continuidade"]).exists())
        result = escolha.selecionar(self.request_path)
        self.assertEqual(result["status"], "salvo")
        self.assertTrue(Path(result["continuidade"]).exists())
        self.assertEqual(json.loads(self.result_path.read_bytes())["status"], "salvo")
        saved = Path(result["backup"]) / "resultados" / (transacao.sha(pending_raw) + ".json")
        self.assertEqual(saved.read_bytes(), pending_raw)

    def test_falha_resultado_pos_commit_retoma_sem_reescrever_os_treze(self):
        real_new = escolha._novo
        failed = False
        def fail_result(path, content, root):
            nonlocal failed
            if path == self.result_path and not failed:
                failed = True
                raise OSError("resultado simulado")
            return real_new(path, content, root)
        with mock.patch.object(escolha, "_novo", side_effect=fail_result), mock.patch("sys.stderr"):
            self.assertEqual(escolha.main(["--pedido", str(self.request_path)]), 2)
        self.assertEqual(json.loads(self.result_path.read_bytes())["status"], "registro_pendente")
        with mock.patch.object(transacao, "escrever", wraps=transacao.escrever) as writer:
            self.assertEqual(escolha.selecionar(self.request_path)["status"], "salvo")
        self.assertFalse(any(call.args[0] in [self.profile / name for name in self.original] for call in writer.call_args_list))

    def test_cli_lock_ocupado_nao_grava_resultado_de_outro_worker(self):
        self.write("logs/escolha_djanira_em_andamento.lock", b"outro worker")
        with mock.patch("sys.stderr"):
            self.assertEqual(escolha.main(["--pedido", str(self.request_path)]), 1)
        self.assertFalse(self.result_path.exists())
        self.assert_original()


if __name__ == "__main__":
    unittest.main()
