"""Prévia dos treze XMLs registrados, em três grupos; nunca altera figurinos."""
from pathlib import Path
import json
from validar_json import validar_cena_json
from runtime_diretor import ler_pasta_dados

RAIZ = Path(__file__).resolve().parents[1]
GRUPOS = (
    ("Djanira pobre e rica, Dinjer e dona Iraci", ("DJANIRA_POBRE", "DJANIRA_RICA", "DINJER", "DONA_IRACI")),
    ("O Chefe e os quatro capangas", ("O_CHEFE", "CAPANGA_1", "CAPANGA_2", "CAPANGA_3", "CAPANGA_4")),
    ("Os três colegas e o atendente", ("COLEGA_1", "COLEGA_2", "COLEGA_3", "ATENDENTE")),
)

def preparar():
    catalogos = ler_pasta_dados(RAIZ, exigir_perfil=True) / "catalogos"
    nomes = {p["codigo"]: p["nome"] for p in json.loads((catalogos / "personagens.json").read_text(encoding="utf-8-sig"))["personagens"]}
    for n, (titulo, codigos) in enumerate(GRUPOS, 91):
        cena = {
            "versao": 1, "id": f"teste_fila_{n}", "episodio": 0, "numero": 0,
            "nome": f"Elenco {n-90}/3 — {titulo}",
            "ambiente": {"local": "TESTE_AMBIENTE", "hora": "17:00", "clima": "limpo"},
            "elenco": [{"personagem": c, "marca": {"direita": (i-(len(codigos)-1)/2)*1.6, "frente": 0}} for i,c in enumerate(codigos)],
            "camera": {"modo": "automatico", "planoInicial": "DOIS", "personagem": codigos[0], "planoNarracao": "DOIS", "planoFinal": "DOIS"},
            "roteiro": [{"tipo": "pausa", "duracaoSegundos": 4, "plano": "DOIS", "personagem": codigos[0]}] +
                       [{"tipo": "fala", "personagem": c, "texto": "Este é o visual de " + nomes[c] + ".", "duracaoSegundos": 4, "gesto": "neutro", "plano": "MEDIA"} for c in codigos if c != "ATENDENTE"] +
                       ([{"tipo": "pausa", "duracaoSegundos": 4, "plano": "DOIS", "personagem": "ATENDENTE"}] if "ATENDENTE" in codigos else []),
        }
        # Atendente permanece de costas, como exige o roteiro. Não recebe close.
        if "ATENDENTE" in codigos:
            cena["elenco"][-1]["olharPara"] = "COLEGA_1"
            cena["elenco"][-1]["marca"] = {"direita": -2.4, "frente": -1.8}
        destino = RAIZ / "cenas" / (cena["id"] + ".json")
        raw = (json.dumps(cena, ensure_ascii=False, indent=2)+"\n").encode("utf-8")
        if destino.exists():
            if destino.read_bytes() != raw:
                raise ValueError("Prévia existente diferente; preservada: " + str(destino))
        else:
            with destino.open("xb") as stream: stream.write(raw)
        validar_cena_json(destino, catalogos=catalogos)
        print("Validado: " + str(destino))

if __name__ == "__main__":
    preparar()
