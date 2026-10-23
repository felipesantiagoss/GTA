"""Fidelidade editorial e operações físicas indispensáveis às imagens do teaser."""
from copy import deepcopy
from pathlib import Path
import sys
import unittest

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
import preparar_teaser_ep01 as teaser
from preparar_edicao import ler_planos
from preparar_piloto import ErroPiloto


class TeaserTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.planos = ler_planos()["planos"]

    def test_voz_original_elenco_e_duracao_intactos_inclusive_alternativa(self):
        cenas, fora = teaser.construir_cenas(self.planos)
        for cena in [*cenas.values(), fora]:
            teaser.verificar_fidelidade(cena, self.planos[cena["id"]])
        self.assertEqual([x["duracaoSegundos"] for x in cenas["EP02_C01"]["roteiro"]], [8, 4])
        self.assertEqual([x["duracaoSegundos"] for x in cenas["EP02_C02"]["roteiro"]], [9])
        self.assertEqual([x["duracaoSegundos"] for x in fora["roteiro"]], [8])
        alterada = deepcopy(cenas["EP02_C01"])
        alterada["roteiro"][0]["duracaoSegundos"] = 7.5
        with self.assertRaisesRegex(ErroPiloto, "não preservado"):
            teaser.verificar_fidelidade(alterada, self.planos["EP02_C01"])
        alterada = deepcopy(cenas["EP02_C02"])
        alterada["roteiro"][0]["texto"] += " Outra fala."
        with self.assertRaisesRegex(ErroPiloto, "não preservado"):
            teaser.verificar_fidelidade(alterada, self.planos["EP02_C02"])

    def test_guarda_depende_de_gesto_e_transferencia_sem_substituto_por_ocultacao(self):
        cenas, _ = teaser.construir_cenas(self.planos)
        cena = cenas["EP02_C02"]
        self.assertEqual(len(cena["elenco"]), 5)
        self.assertEqual(len(cena["poses"]), 5)
        self.assertEqual(len(cena["objetos"]), 7)
        envelope = next(x for x in cena["objetos"] if x["recurso"] == "envelope")
        self.assertEqual(envelope["attachmentInicial"]["personagem"], "CAPANGA_1")
        self.assertEqual([x["tipo"] for x in envelope["eventos"]], ["transferir", "guardar"])
        for evento in envelope["eventos"]:
            direcao = next(x for x in cena["direcao"] if x["personagem"] == "DINJER"
                and x["inicioSegundos"] <= evento["inicioSegundos"]
                and x["inicioSegundos"] + x["duracaoSegundos"] >= evento["inicioSegundos"] + evento["esperaMaximaSegundos"])
            self.assertTrue(direcao["sobrePoseSentada"])
            self.assertFalse(direcao["corpoInteiro"])
            if evento["tipo"] == "guardar":
                self.assertEqual(evento["recurso"], direcao["recurso"])

    def test_adaptador_nao_absorve_acao_alterada_nem_fachada_sem_nota_literal(self):
        planos = deepcopy(self.planos)
        planos["EP02_C01"]["acoesOriginais"][0]["raw"] += " Entra no cassino."
        with self.assertRaisesRegex(ErroPiloto, "ação, local ou voz"):
            teaser.construir_cenas(planos)
        plano = deepcopy(self.planos["EP02_C01"])
        plano["notasOriginais"] = []
        with self.assertRaisesRegex(ErroPiloto, "fallback literal"):
            teaser.construir_fora(plano)


if __name__ == "__main__":
    unittest.main()
