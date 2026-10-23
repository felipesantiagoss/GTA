"""Atuação representada, falas íntegras e nenhuma exportação substituta."""
from copy import deepcopy
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
import preparar_cenas_serie as compiler
from preparar_piloto import ErroPiloto


class SerieTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.result = compiler.compilar_temporada()
        cls.docs = {x["id"]: x for x in cls.result["cenas"]}
        cls.pesquisa = compiler._pesquisa(RAIZ)
        cls.planos = {x["id"]: x for x in compiler._serie(RAIZ)["planos"]}

    def scene(self, ident):
        folder = "compartilhados" if ident == "VINHETA_01" else ident[:4]
        return json.loads(self.result["arquivos"][f"{folder}/{ident}.json"])

    def test_95_planos_ordem15_episodios_e_nada_pronto_ou_gravado(self):
        index = self.result["indice"]
        self.assertEqual(len(self.docs), 95)
        self.assertEqual(len(index["episodios"]), 15)
        self.assertEqual(sum(len(x["ordemOriginal"]) for x in index["episodios"]), 94)
        self.assertTrue(all(not x["pronto"] and not x["gravado"] for x in self.docs.values()))
        self.assertEqual(len(index["rascunhosIntegrais"]), 27)
        self.assertEqual(len(index["pendentesAtuacao"]), 68)

    def test_todas_falas_acoes_silencios_cartelas_reusos_literais(self):
        for ident, doc in self.docs.items():
            plano = self.planos[ident]
            self.assertEqual(doc["origem"], plano["origem"])
            self.assertEqual(doc["roteiroOriginal"], plano["roteiro"])
            self.assertEqual(doc["acoesOriginais"], plano["acoesOriginais"])
            self.assertEqual(doc["cartelas"], plano["cartelas"])
            self.assertEqual(doc["reaproveitamentos"], plano["reaproveitamentos"])
            self.assertEqual(doc["temposOriginais"], plano["tempos"])
        self.assertIn("12 segundos", self.docs["EP15_C05"]["notasOriginais"][0]["raw"])

    def test_cenas_incompletas_nao_exportam_actor_parado_como_substituto(self):
        files = self.result["arquivos"]
        for ident in ("EP01_C05", "EP02_C02", "EP05_C03", "EP07_C04", "EP11_C02", "EP13_C03", "EP15_C05"):
            self.assertIsNone(self.docs[ident]["runtimeRascunho"])
            self.assertNotIn(f"{ident[:4]}/{ident}.json", files)
            self.assertTrue(any(x["tipo"] == "atuacao_integral" for x in self.docs[ident]["pendencias"]))

    def test_andancas_movem_de_verdade_destino_relativo_e_tempos_originais(self):
        for ident in compiler.ANDANCAS:
            scene = self.scene(ident)
            direction = scene["direcao"][0]
            self.assertEqual(scene["encenacao"], "serie_v1")
            self.assertEqual(direction["tipo"], "andar")
            self.assertEqual(direction["destino"], {"direita": 0, "frente": 4, "altura": 0})
            self.assertEqual(direction["duracaoSegundos"] * 1000, self.planos[ident]["tempos"]["conteudoPlanejadoMs"])
            self.assertEqual([x["texto"] for x in scene["roteiro"] if "texto" in x], [x["texto"] for x in self.planos[ident]["roteiro"] if x["texto"]])
            self.assertFalse(any(x["tipo"] == "fala" for x in scene["roteiro"]))

    def test_ep1_c02_trabalho_manual_depois_da_caminhada_sem_boca_ou_ferramenta_fabricada(self):
        scene = self.scene("EP01_C02")
        self.assertEqual([(x["tipo"], x["inicioSegundos"], x["duracaoSegundos"]) for x in scene["direcao"]],
                         [("andar", 0, 4), ("clipe", 4, 6)])
        self.assertEqual(scene["direcao"][1]["recurso"], "trabalho_plantacao_f")
        self.assertTrue(scene["direcao"][1]["loop"])
        self.assertEqual(scene["camera"]["planoInicial"], "LONGE")
        self.assertFalse(any(x["tipo"] == "fala" for x in scene["roteiro"]))
        self.assertNotIn("objetos", scene)
        self.assertEqual(self.docs["EP01_C02"]["cartelas"], self.planos["EP01_C02"]["cartelas"])

    def test_ep1_c01_conserva_voz14_e_declara_reserva46_sem_coordenadas_inventadas(self):
        scene = self.scene("EP01_C01")
        self.assertEqual(scene["ambiente"]["local"], "CANAL_LS")
        self.assertEqual([x["duracaoSegundos"] for x in scene["roteiro"]], [14, 46])
        self.assertEqual(scene["roteiro"][0]["tipo"], "narracao")
        self.assertEqual(scene["roteiro"][0]["texto"], self.planos["EP01_C01"]["roteiro"][0]["texto"])
        self.assertEqual(scene["roteiro"][1]["tipo"], "pausa")
        self.assertNotIn("texto", scene["roteiro"][1])
        self.assertEqual(scene["direcao"], [])
        self.assertEqual(self.docs["EP01_C01"]["temposRascunho"], {"conteudoMs": 60000, "contagemMs": 3000,
            "capturaDesdeF7Ms": 63000, "blocosOriginaisMs": 14000, "tempoSemVozAdicionalMs": 46000})
        self.assertIn("00:00:03,000 --> 00:00:17,000", self.result["arquivos"]["EP01/EP01_C01.base.srt"].decode("utf-8"))
        marks = {x["codigo"] for x in self.docs["EP01_C01"]["pendencias"] if x["codigo"].startswith("marca:")}
        self.assertEqual(marks, {"marca:CANAL_LS", "marca:CANAL_LS_RAMPA_ENTRADA", "marca:CANAL_LS_RAMPA_TOPO", "marca:CANAL_LS_FUGA"})
        self.assertIn("ensaio:queda_CANAL_LS", {x["codigo"] for x in self.docs["EP01_C01"]["pendencias"]})
        self.assertEqual(scene["camera"]["planoFinal"], "OLHOS")
        self.assertFalse(self.docs["EP01_C01"]["pronto"])
        self.assertFalse(self.docs["EP01_C01"]["gravado"])

    def test_ep1_c03_sacola_real_recebida_e_devolvida_com_narracao_integral(self):
        scene = self.scene("EP01_C03")
        bag = scene["objetos"][0]
        self.assertEqual((bag["recurso"], bag["attachmentInicial"]["personagem"]), ("sacola", "DONA_IRACI"))
        self.assertEqual([(x["tipo"], x["inicioSegundos"], x["personagem"]) for x in bag["eventos"]],
                         [("transferir", 2, "DJANIRA_POBRE"), ("transferir", 4, "DONA_IRACI")])
        self.assertEqual([x["recurso"] for x in scene["direcao"]], ["entrega_b", "entrega_a"])
        self.assertTrue(all(not x["corpoInteiro"] for x in scene["direcao"]))
        self.assertEqual([x["plano"] for x in scene["camera"]["cortes"]], ["DOIS", "MEDIA"])
        self.assertEqual(scene["roteiro"][0]["texto"], self.planos["EP01_C03"]["roteiro"][0]["texto"])
        self.assertEqual(self.docs["EP01_C03"]["temposRascunho"]["conteudoMs"], 9000)

    def test_ep1_barraco_simultaneo34s_duplas_arco_eixo_preserva_quatro_falas_originais(self):
        scene = self.scene("EP01_C07")
        self.assertEqual(len(scene["elenco"]), 4)
        self.assertEqual(scene["direcao"], [])
        self.assertEqual(scene["encenacao"], "serie_v1")
        self.assertEqual([x["duracaoSegundos"] for x in scene["roteiro"]], [4, 3, 9, 3, 3, 12])
        self.assertEqual([x["texto"] for x in scene["roteiro"] if x["tipo"] == "fala"],
                         [x["texto"] for x in self.planos["EP01_C07"]["roteiro"] if x["tipo"] == "fala"])
        self.assertEqual(scene["roteiro"][-1]["tipo"], "pausa")
        self.assertEqual(self.docs["EP01_C07"]["temposRascunho"]["conteudoMs"], 34000)
        self.assertEqual(self.docs["EP01_C07"]["temposRascunho"]["capturaDesdeF7Ms"], 37000)
        self.assertEqual({x["personagem"]: x["marca"] for x in scene["elenco"]}, {
            "DINJER": {"direita": 0, "frente": 0, "altura": 0},
            "COLEGA_1": {"direita": 0.85, "frente": 0.45, "altura": 0},
            "COLEGA_2": {"direita": -0.65, "frente": 0.65, "altura": 0},
            "COLEGA_3": {"direita": -1.3, "frente": 0.15, "altura": 0}})
        self.assertEqual({x["personagem"]: x["olharPara"] for x in scene["elenco"]},
                         {"DINJER": "COLEGA_1", "COLEGA_1": "DINJER", "COLEGA_2": "COLEGA_3", "COLEGA_3": "COLEGA_2"})
        self.assertTrue(scene["camera"]["inverterEixo"])
        self.assertEqual(scene["camera"]["cortes"][-1],
                         {"inicioSegundos": 22, "plano": "DOIS", "personagem": "DINJER"})
        blocks = self.result["arquivos"]["EP01/EP01_C07.base.srt"].decode("utf-8").strip().split("\n\n")
        self.assertEqual(len(blocks), 4)
        self.assertEqual([block.splitlines()[1] for block in blocks], [
            "00:00:07,000 --> 00:00:10,000", "00:00:10,000 --> 00:00:19,000",
            "00:00:19,000 --> 00:00:22,000", "00:00:22,000 --> 00:00:25,000"])
        for block, original in zip(blocks, self.planos["EP01_C07"]["roteiro"][:4]):
            self.assertIn(original["texto"], block)

    def test_ep1_onibus_partida_e_presencas_implicitas_sem_alterar_original(self):
        terminal, end = self.scene("EP01_C06"), self.scene("EP01_C09")
        self.assertEqual([x["plano"] for x in terminal["camera"]["cortes"]], ["LONGE", "MEDIA"])
        self.assertEqual(self.docs["EP01_C09"]["elencoAdicionalDaAcao"], ["DINJER"])
        self.assertEqual(self.docs["EP01_C09"]["original"]["elencoCodigos"], ["DJANIRA_POBRE"])
        self.assertEqual(end["interpretacao"][0]["olhar"], {"tipo": "ator", "alvo": "DINJER"})
        for ident, scene in (("EP01_C06", terminal), ("EP01_C09", end)):
            self.assertEqual(scene["direcao"], [])
            self.assertTrue(all(x["tipo"] == "narracao" for x in scene["roteiro"]))
            self.assertEqual([x["texto"] for x in scene["roteiro"]], [x["texto"] for x in self.planos[ident]["roteiro"]])

    def test_ep1_clinica_travessia_fisica_e_despedida_com_passagem_real_pendente(self):
        scene = self.scene("EP01_C04")
        self.assertEqual(scene["ambiente"]["distanciaFrente"], 1)
        actions = {x["personagem"]: x for x in scene["direcao"]}
        self.assertEqual(actions["DONA_IRACI"]["destino"], {"direita": 0.3, "frente": 2, "altura": 0})
        self.assertEqual(actions["DJANIRA_POBRE"]["inicioSegundos"], 2)
        self.assertLess(actions["DJANIRA_POBRE"]["destino"]["frente"], scene["elenco"][0]["marca"]["frente"])
        self.assertTrue(any(x["codigo"] == "passagem:HOSPITAL_SANDY" for x in self.docs["EP01_C04"]["pendencias"]))
        self.assertEqual(self.docs["EP01_C04"]["temposRascunho"]["conteudoMs"], 7000)
        self.assertEqual(scene["roteiro"][0]["texto"], self.planos["EP01_C04"]["roteiro"][0]["texto"])

    def test_ep1_c05_subtomadas_mesmo_id_vozes_preservadas_montagem_externa_explicitada(self):
        files = compiler.construir_subtomadas_ep01()
        manifest = json.loads(files["producao/subtomadas/EP01_C05/manifesto.json"])
        self.assertFalse(manifest["gravado"])
        self.assertFalse(manifest["mp4FinalGerado"])
        self.assertEqual((manifest["montagem"]["hospitalMs"], manifest["montagem"]["cassinoMs"], manifest["montagem"]["vozOriginalMs"]),
                         (3000, 5000, 8000))
        self.assertFalse(manifest["montagem"]["boca"])
        for spec in manifest["subtomadas"]:
            scene = json.loads(files[spec["arquivo"]])
            self.assertEqual(scene["id"], "EP01_C05")
            self.assertEqual(scene["ambiente"]["local"], spec["local"])
            self.assertEqual(spec["sha256"], compiler._hash(files[spec["arquivo"]]))
            self.assertEqual(scene["roteiro"][0]["texto"], self.planos["EP01_C05"]["roteiro"][0]["texto"])
            self.assertEqual(scene["roteiro"][0]["duracaoSegundos"], 8)
            self.assertEqual(scene["direcao"][0]["duracaoSegundos"], 8)
            self.assertEqual(spec["capturaDesdeF7Ms"], 11000)
        self.assertIsNone(self.docs["EP01_C05"]["runtimeRascunho"])
        self.assertEqual(len(self.docs["EP01_C05"]["subtomadasPreparadas"]), 2)

    def test_ep14_c03_tres_falas_silencio4_foco_dinjer_e_reserva_estimada(self):
        scene = self.scene("EP14_C03")
        self.assertEqual([x["duracaoSegundos"] for x in scene["roteiro"]], [10, 2, 4, 2, 6])
        silence = scene["roteiro"][2]
        self.assertEqual((silence["tipo"], silence["personagem"], silence["plano"]), ("pausa", "DINJER", "PERTO"))
        self.assertNotIn("texto", silence)
        self.assertEqual([x["texto"] for x in scene["roteiro"] if "texto" in x], [x["texto"] for x in self.planos["EP14_C03"]["roteiro"] if x["texto"]])

    def test_vinheta20s_quatro_clipes_reais_cortes_individuais_sem_voz(self):
        scene = self.scene("VINHETA_01")
        self.assertEqual([x["recurso"] for x in scene["direcao"]], list(compiler.DANCAS))
        self.assertEqual([x["inicioSegundos"] for x in scene["direcao"]], [0, 5, 10, 15])
        self.assertEqual([x["duracaoSegundos"] for x in scene["roteiro"]], [5, 5, 5, 5])
        self.assertTrue(all(x["tipo"] == "pausa" and "texto" not in x for x in scene["roteiro"]))
        self.assertEqual([x["personagem"] for x in scene["roteiro"]], [x["personagem"] for x in scene["elenco"]])
        self.assertEqual(len({x["marca"]["direita"] for x in scene["elenco"]}), 4)
        self.assertEqual(self.result["arquivos"]["compartilhados/VINHETA_01.base.srt"], b"")

    def test_recursos_faltantes_com_ids_concretos_e_source_provenance(self):
        resources = {x["codigo"]: x for x in self.result["indice"]["recursosFaltantes"]}
        self.assertNotIn("EP01_C01", resources["capacidade:voo_queda"]["cenas"])
        self.assertIn("EP01_C01", resources["ensaio:queda_CANAL_LS"]["cenas"])
        self.assertNotIn("EP04_C05", resources["capacidade:deitar"]["cenas"])
        self.assertIn("EP10_C01", resources["capacidade:deitar"]["cenas"])
        self.assertIn("EP05_C03", resources["capacidade:montagem_locacoes"]["cenas"])
        self.assertEqual(len(self.pesquisa["animacoes"]), 46)
        self.assertEqual(len(self.pesquisa["objetos"]), 10)
        self.assertTrue(all(x["origem"]["linha"] > 0 and len(x["origem"]["sha256"]) == 64 for x in self.pesquisa["animacoes"]))

    def test_alterar_acao_ou_nome_recurso_nao_ganha_exportacao(self):
        plano = deepcopy(self.planos["EP04_C04"])
        plano["acoesOriginais"][0]["raw"] += " E dirige um carro."
        with self.assertRaisesRegex(ErroPiloto, "ação literal mudou"):
            compiler.construir_rascunho(plano, self.pesquisa)
        changed = deepcopy(self.pesquisa)
        changed["animacoes"] = [x for x in changed["animacoes"] if x["codigo"] != compiler.DANCAS[0]]
        with self.assertRaisesRegex(ErroPiloto, "clipes reais"):
            compiler.construir_rascunho(self.planos["VINHETA_01"], changed)

    def test_chamada_aproximacao_durante_fala_da_avo_sem_eixo_ou_fala_propria_sobreposta(self):
        scene = self.scene("EP02_C04")
        movement = scene["direcao"][0]
        actors = {x["personagem"]: x for x in scene["elenco"]}
        self.assertEqual(actors["DJANIRA_POBRE"]["marca"], {"direita": -4, "frente": 0, "altura": 0})
        self.assertEqual(movement["destino"], {"direita": -1.1, "frente": 0, "altura": 0})
        self.assertLess(movement["destino"]["direita"], actors["DONA_IRACI"]["marca"]["direita"])
        self.assertEqual(scene["roteiro"][0]["personagem"], "DONA_IRACI")
        self.assertEqual(scene["roteiro"][0]["duracaoSegundos"], movement["duracaoSegundos"])
        self.assertEqual([x["texto"] for x in scene["roteiro"]], [x["texto"] for x in self.planos["EP02_C04"]["roteiro"]])
        srt = self.result["arquivos"]["EP02/EP02_C04.base.srt"].decode("utf-8")
        self.assertIn("Dona Iraci: Minha filha, chega aqui.", srt)
        self.assertIn("00:00:08,000 --> 00:00:12,000\nDjanira:", srt)

    def test_chamada_verbal_preserva31segundos_tres_falas_e_duas_narracoes(self):
        scene = self.scene("EP05_C05")
        self.assertNotIn("direcao", scene)
        self.assertEqual([x["duracaoSegundos"] for x in scene["roteiro"]], [3, 4, 11, 6, 7])
        self.assertEqual([x["texto"] for x in scene["roteiro"]], [x["texto"] for x in self.planos["EP05_C05"]["roteiro"]])
        self.assertEqual(scene["camera"]["planoFala"], "MEDIA")
        self.assertTrue(all(x["plano"] == "MEDIA" for x in scene["roteiro"] if x["tipo"] == "fala"))
        self.assertTrue(all(x["narrador"] == "Narrador" for x in scene["roteiro"] if x["tipo"] == "narracao"))
        self.assertEqual(self.docs["EP05_C05"]["temposRascunho"]["conteudoMs"], 31000)

    def test_joelhos_entrada_sem_voz_hold_e_todas_falas_originais(self):
        scene = self.scene("EP14_C04")
        self.assertEqual(scene["roteiro"][0], {"tipo": "pausa", "duracaoSegundos": 8, "plano": "DOIS", "personagem": "DINJER"})
        action = scene["direcao"][0]
        self.assertEqual(action["recurso"], "ajoelhar_entrada_f")
        self.assertTrue(action["manterPoseFinal"])
        self.assertFalse(action["loop"])
        self.assertTrue(action["corpoInteiro"])
        self.assertEqual([x["duracaoSegundos"] for x in scene["roteiro"][1:]], [7, 7, 9, 2, 4])
        self.assertEqual([x["texto"] for x in scene["roteiro"][1:]], [x["texto"] for x in self.planos["EP14_C04"]["roteiro"]])
        self.assertEqual(self.docs["EP14_C04"]["temposRascunho"]["capturaDesdeF7Ms"], 40000)
        srt = self.result["arquivos"]["EP14/EP14_C04.base.srt"].decode("utf-8")
        self.assertTrue(srt.startswith("1\n00:00:11,000 --> 00:00:18,000\nDinjer:"))

    def test_selecao95_sem_runtime_incompleto_nome_fonte_e_hash_do_arquivo(self):
        root = ET.fromstring(self.result["arquivos"]["selecao.xml"])
        self.assertEqual(root.tag, "Temporada")
        self.assertEqual(root.attrib["roteiroSha256"], self.planos["EP01_C01"]["origem"]["sha256Arquivo"])
        self.assertEqual(len(root.findall("Episodio")), 15)
        entries = root.findall("Episodio/Cena") + root.findall("Compartilhada")
        self.assertEqual(len(entries), 95)
        for entry in entries:
            doc = self.docs[entry.attrib["id"]]
            self.assertEqual(entry.attrib["nome"], self.planos[doc["id"]]["nome"])
            if doc["runtimeRascunho"]:
                path = entry.attrib["arquivo"]
                self.assertEqual(entry.attrib["sha256"], compiler._hash(self.result["arquivos"][path]))
            else:
                self.assertEqual((entry.attrib["arquivo"], entry.attrib["sha256"]), ("", ""))
        with self.assertRaisesRegex(ErroPiloto, "95IDs únicos"):
            compiler.gerar_xml_selecao(list(self.docs.values())[:-1], root.attrib["roteiroSha256"])

    def test_plano95_dependencias_sobrepostas_e_apoios_sem_coordenadas_fabricadas(self):
        plan = json.loads(self.result["arquivos"]["PLANO_IMPLEMENTACAO_95.json"])
        self.assertEqual(len(plan["cenas"]), 95)
        self.assertEqual(len(plan["cenasPendentes"]), 68)
        rows = {x["id"]: x for x in plan["cenas"]}
        self.assertIn("objetos_e_maos", rows["EP15_C02"]["gruposParaAtuacaoInteira"])
        self.assertIn("apoios_e_pose", rows["EP15_C02"]["gruposParaAtuacaoInteira"])
        self.assertIn("rotas_portas_e_interiores", rows["EP15_C02"]["gruposParaAtuacaoInteira"])
        self.assertEqual(rows["EP15_C02"]["acoesOriginais"], self.planos["EP15_C02"]["acoesOriginais"])
        supports = json.loads(self.result["arquivos"]["apoios_propostos.json"])
        self.assertEqual(len(supports["apoios"]), 8)
        self.assertTrue(all(not x["pronto"] and x["pontoAssentoMedido"] is None and x["geometriaMedida"] is None for x in supports["apoios"]))

    def test_vergonha_desvia_olhar_inclusive_ouvindo_preserva_quatro_falas_e_flashback(self):
        scene = self.scene("EP08_C04")
        self.assertEqual(scene["encenacao"], "serie_v1")
        self.assertEqual(scene["direcao"], [])
        effect = scene["interpretacao"][0]
        self.assertEqual(effect["personagem"], "DONA_IRACI")
        self.assertEqual((effect["inicioSegundos"], effect["duracaoSegundos"]), (0, 30))
        self.assertEqual(effect["olhar"], {"tipo": "desvio"})
        self.assertEqual([x["texto"] for x in scene["roteiro"]], [x["texto"] for x in self.planos["EP08_C04"]["roteiro"]])
        self.assertIn("EP01_C05", self.docs["EP08_C04"]["notasOriginais"][0]["raw"])
        self.assertIn("CapCut", " ".join(self.docs["EP08_C04"]["mapeamentoDeclarado"]))

    def test_sorriso_e_apenas_facial4s_sem_boca_ou_riso_corporal_narrador6s_preservado(self):
        scene = self.scene("EP10_C07")
        self.assertEqual(scene["camera"]["planoInicial"], "PERTO")
        self.assertEqual(scene["direcao"], [])
        self.assertEqual(scene["interpretacao"], [{"personagem": "DJANIRA_POBRE", "inicioSegundos": 0, "duracaoSegundos": 4, "expressao": "feliz"}])
        self.assertTrue(all(x["tipo"] == "narracao" for x in scene["roteiro"]))
        self.assertEqual([x["duracaoSegundos"] for x in scene["roteiro"]], [4, 6])
        self.assertEqual(self.docs["EP10_C07"]["temposRascunho"]["conteudoMs"], 10000)

    def test_djanira_entre_pai_e_amiga_nao_remove_presencas_por_metadado_incompleto(self):
        scene = self.scene("EP15_C06")
        self.assertEqual(self.planos["EP15_C06"]["original"]["elencoCodigos"], ["DJANIRA_RICA"])
        self.assertEqual(self.docs["EP15_C06"]["elencoAdicionalDaAcao"], ["O_CHEFE", "DINJER"])
        actors = {x["personagem"]: x for x in scene["elenco"]}
        self.assertLess(actors["O_CHEFE"]["marca"]["direita"], actors["DJANIRA_RICA"]["marca"]["direita"])
        self.assertLess(actors["DJANIRA_RICA"]["marca"]["direita"], actors["DINJER"]["marca"]["direita"])
        resources = {x["codigo"] for x in self.docs["EP15_C06"]["pendencias"]}
        self.assertIn("visual:O_CHEFE", resources)
        self.assertIn("visual:DINJER", resources)
        self.assertEqual(scene["roteiro"][0]["texto"], self.planos["EP15_C06"]["roteiro"][0]["texto"])
        self.assertEqual(scene["interpretacao"][0]["expressao"], "normal")

    def test_poses_iniciais_exigem_recursos_reais_sem_movimento_filmado_nem_falas_suprimidas(self):
        for ident in compiler.POSES_INICIAIS:
            scene = self.scene(ident)
            self.assertEqual(scene["direcao"], [])
            self.assertEqual(scene["encenacao"], "serie_v1")
            self.assertEqual([x["texto"] for x in scene["roteiro"] if "texto" in x],
                             [x["texto"] for x in self.planos[ident]["roteiro"] if x["texto"]])
            objects = {x["id"]: x for x in scene["objetos"]}
            for pose in scene["poses"]:
                self.assertIn(pose["objeto"], objects)
                self.assertTrue(objects[pose["objeto"]]["assentarNoChao"])
                self.assertNotIn("altura", pose)
            self.assertEqual(self.docs[ident]["acoesOriginais"], self.planos[ident]["acoesOriginais"])
        modified = deepcopy(self.pesquisa)
        modified["animacoes"] = [x for x in modified["animacoes"] if x["codigo"] != "deitada_cama_f"]
        with self.assertRaisesRegex(ErroPiloto, "recursos canônicos"):
            compiler.construir_rascunho(self.planos["EP04_C05"], modified)
        changed = deepcopy(self.planos["EP09_C04"])
        changed["acoesOriginais"][0]["raw"] += " E levanta."
        with self.assertRaisesRegex(ErroPiloto, "ação literal mudou"):
            compiler.construir_rascunho(changed, self.pesquisa)

    def test_avo_deitada_doente_djanira_sentada_ao_lado_e_fala_integral(self):
        scene = self.scene("EP04_C05")
        poses = {x["personagem"]: x for x in scene["poses"]}
        self.assertEqual((poses["DONA_IRACI"]["tipo"], poses["DONA_IRACI"]["recurso"]), ("deitada", "deitada_cama_f"))
        self.assertEqual(poses["DJANIRA_POBRE"]["tipo"], "sentada")
        self.assertEqual([x["duracaoSegundos"] for x in scene["roteiro"]], [7, 7])
        self.assertEqual(scene["interpretacao"][0]["expressao"], "machucado")

    def test_vip_mesma_mesa_ep06_ep12_quatro_sentados_vs_djanira_sozinha(self):
        before, after = self.scene("EP06_C03"), self.scene("EP12_C02")
        self.assertEqual(before["objetos"], after["objetos"])
        self.assertEqual(len(before["elenco"]), 5)
        self.assertEqual(len(before["poses"]), 4)
        self.assertEqual(len(after["elenco"]), 1)
        self.assertEqual(after["poses"][0]["objeto"], "CADEIRA_CHEFE")
        self.assertEqual([x["plano"] for x in before["camera"]["cortes"]], ["LONGE", "PERTO"])
        self.assertTrue(all("plano" not in x for x in before["roteiro"]))
        self.assertEqual(before["interpretacao"][0]["olhar"], {"tipo": "ator", "alvo": "DINJER"})
        self.assertEqual(after["camera"]["planoInicial"], "LONGE")

    def test_meio_fio_chao_e_chega_senta_ainda_exigem_recurso_adequado_e_transicao(self):
        for ident in ("EP03_C03", "EP08_C03", "EP11_C04", "EP13_C06"):
            self.assertIsNone(self.docs[ident]["runtimeRascunho"])


if __name__ == "__main__":
    unittest.main()
