"""Adaptadores de atuação íntegra e dependências das95 cenas, só no workspace.

Emitir um rascunho exige mapear a ação inteira. Pesquisa de animação não resolve
cadeira, porta, veículo ou interior ausentes. Nenhuma saída torna elenco pronto,
publica no perfil ou declara teste visual/gravação.
"""
from __future__ import annotations

import argparse
from copy import deepcopy
import csv
from decimal import Decimal
import io
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

from preparar_piloto import RAIZ, ErroPiloto, _adicionar, _confinado, _hash, _json, _preparar_destinos, _serie, montar_rascunho
from preparar_serie import gerar_srt
from runtime_diretor import ErroRuntime, exigir_sem_vinculos

# Cada whitelist vincula um adaptador à ação literal; editar a origem exige revisão.
IMOVEIS = {
    "EP02_C07": "**Ação:** Djanira parada olhando o horizonte. Câmera baixa, olhando para cima — ela parece maior.",
    "EP03_C06": "**Ação:** Close na Djanira, parada, sem reação.",
    "EP09_C03": "**Ação:** Djanira parada na calçada, olhando o prédio enorme. Câmera baixa — o prédio gigante, ela minúscula.",
}
ANDANCAS = {
    "EP04_C04": "**Ação:** Djanira andando sozinha na estrada, câmera o mais longe possível.",
    "EP08_C02": "**Ação:** Djanira andando descalça na beira da estrada, ao nascer do sol. Câmera o mais longe possível.",
    "EP10_C03": "**Ação:** Djanira andando sozinha na rua vazia, ainda escuro.",
    "EP10_C04": "**Ação:** Djanira andando embaixo do viaduto à noite.",
}
DANCAS = ("danca_djanira_candidata", "danca_dinjer_candidata", "danca_iraci_candidata", "danca_chefe_candidata")
EPISODIO_UM = {
    "EP01_C01": ["**Ação:** Avião voando. A Djanira cai do avião, despenca e bate na água do canal. Emerge encharcada, sobe pela rampa de concreto e corre."],
    "EP01_C02": ["**Ação:** Djanira andando devagar no meio da plantação, trabalhando. Câmera bem afastada, ela pequena no quadro."],
    "EP01_C03": ["**Ação:** As duas sentadas ou em pé na frente da casa. Djanira ajeita algo para a avó. Sem diálogo — só convivência."],
    "EP01_C04": ["**Ação:** Djanira deixa a avó na entrada da clínica, se despede e vai embora. A avó entra."],
    "EP01_C06": ["**Ação:** Dinjer descendo de um ônibus e andando até o terminal, com jeito de passarela, confiante."],
    "EP01_C07": ["**Ação:** Dinjer passa por três colegas. Eles falam dela. Ela para, vira e responde. Termina em empurrão e confusão coreografada.",
                  "**Ação final:** 2 segundos de empurra-empurra. Corte seco."],
    "EP01_C09": ["**Ação:** Djanira sozinha, olhando a moto da Dinjer ir embora."],
}
CHAMADAS = {
    "EP02_C04": "**Ação:** A avó chama a Djanira para perto e avisa. Djanira ouve de pé.",
    "EP05_C05": "**Ação:** A avó chama a Djanira. Djanira ouve de pé, já decidida.",
}
CELULARES_EPISODIO_DOIS = {
    "EP02_C03": "**Ação:** Djanira no escuro, mexendo no celular, esperando resposta. Expressão `estressado`.",
    "EP02_C05": "**Ação:** Djanira com o celular na mão. Rosto fecha. Expressão muda para `com raiva`.",
    "EP02_C06": "**Ação:** A avó mostra o celular dela para a Djanira. As duas olham a tela.",
}
INTERPRETADAS = {
    "EP08_C04": "**Ação:** A avó desvia o olhar, envergonhada.",
    "EP10_C07": "**Ação:** Close no rosto dela. Um sorriso mínimo, o primeiro em três anos.",
    "EP15_C06": "**Ação:** Djanira parada no meio da sala, entre o pai e a amiga. Sem reação.",
}
POSES_INICIAIS = {
    "EP04_C05": "**Ação:** A avó deitada, muito mal. Djanira sentada do lado.",
    "EP06_C03": "**Ação:** Djanira vê, de longe, a Dinjer sentada com o Chefe e os capangas. Não é festa — é negócio.",
    "EP09_C04": "**Ação:** Djanira sentada na frente de um atendente.",
    "EP12_C02": "**Ação:** Djanira sentada sozinha na área VIP — na mesma mesa onde o Chefe sentava no Episódio 6.",
}
MOTIVOS = {
    "sentar": "Apoio/assento/mesa ou chão real e posição corporal; entrada/saída e fala sem gesto standing desfazer a pose.",
    "deitar": "Cama/banco real e alinhamento; transições, sacola quando prevista e facial doente/sono compatível.",
    "voo_queda": "Aeronave, assentos, rota aérea, água/rampa e queda invencível; nenhuma marca terrestre simula ponto aéreo.",
    "agua": "GET_WATER_HEIGHT, saída/rampa real, nadar/escalar e trajeto físico; não executar pose aquática no chão.",
    "briga": "Par coreografado, distância/fase e2s de ação final; ensaio técnico não monta o barraco completo de4atores.",
    "entrega": "Prop autêntico, osso/mão e fase de transferência entre donos; clipes A/B não provam o papel de cada ator.",
    "papel": "Papel/envelope real, abrir/ler/entregar e close nas mãos; jornal candidato não equivale a papel dobrado.",
    "porta": "Porta/portão real, aproximação e direção; abertura/fechamento físico e três batidas quando declaradas.",
    "expressao": "Mapear normal/chateado/feliz/machucado/comraiva/estressado/convencido por clipes reais; não substituir por neutro.",
    "olhar": "Alvo e altura conforme roteiro; chão/costas/desvio/horizonte não equivalem sempre ao olhar automático do ouvinte.",
    "elenco_adicional": "Resolver gerente/segurança/colegas/pai/amiga citados fora do elenco declarado, sem inventar identidade.",
    "populacao_propria": "Figurantes próprios preservados da limpeza; população removida não representa gente na calçada.",
    "onibus": "Ônibus real, assento/porta e desembarque físico; andar ao lado de ônibus não substitui descer.",
    "carro": "Modelo real escolhido, motorista, portas, entrada/saída e rota do morro; nunca simular com personagem parado.",
    "caminhao": "Caminhões e portões reais e três tomadas separadas; cenário vazio sozinho não reproduz a montagem.",
    "interior": "Acesso real e marca interna; fachada é alternativa explícita e não comprovação de interior.",
    "montagem_locacoes": "EP05_C03 exige duas tomadas/locais separados; nenhuma dupla colocada no mesmo ponto representa ligação à distância.",
    "marca_elevada": "Degrau real e altura de Djanira comprovada; não elevar Z para fazê-la flutuar.",
    "quadro_reutilizavel": "Salvar enquadramento real de EP06_C02 e reutilizar exatamente em EP12_C01.",
    "montagem": "Subtomadas com duração/alvos próprios e ordem do CapCut; uma câmera parada não substitui montagem.",
    "reutilizacao": "Reutilizar arquivo da tomada original; sem vídeo existente não declarar reaproveitamento concluído.",
    "bandeja": "Bandeja compatível, attachment/mãos e serviço/pose; prop fast-food candidato não comprova serviço de jantar.",
    "sacola": "Sacola real na mão/abraço e poses; aparências de ped não criam o objeto da história.",
    "trabalho": "Atuação de capinar e ferramenta real; clipe de jardinagem não comprova capina/enxada.",
    "limpeza": "Mesa, mão, pano/objeto e movimento até a foto; clipe isolado não executa toda a cena.",
    "ligacao": "Telefone/mão, atender/conversar/desligar, toque e ação simultânea; celular de texto não substitui ligação.",
    "remedio": "Caixa de remédio vazia real; comprimidos candidatos não correspondem a uma caixa vazia.",
    "recurso_sapato": "Sapatos na mão e fuga; conflito com figurino permanente de Dinjer precisa de adaptação declarada.",
}

# Propostas de API: estes grupos NÃO são aceitos como implementação runtime.
GRUPOS_IMPLEMENTACAO = {
    "interpretacao": {"capacidades": {"olhar", "expressao", "riso", "choro", "machucado", "dialogo"},
        "contrato": "Olhar ator/prop/ponto medido/horizonte/chão/costas; orientação do corpo separada da cabeça; facial real carregada. Override prevalece sobre LookAt automático e reinício restaura a marca/expressão. Fala conserva boca e gesto upper-body sem apagar pose primária.",
        "exemplos": ["EP08_C04: avó desvia envergonhada", "EP14_C05: Dinjer fala de costas sem se virar", "EP15_C05: Chefe não olha Djanira por12s"]},
    "apoios_e_pose": {"capacidades": {"sentar", "deitar", "ajoelhar", "marca_elevada", "ambiente_mobiliario"},
        "contrato": "Apoio real registrado com modelo/handle ou superfície, posição/heading capturados, ponto de assento/cama/degrau medido e espaço livre. Pose tem entrada/base/saída reais e hold durante fala; preflight compara duração real da entrada ao orçamento. Nunca elevar ped para simular degrau ou executar cama no chão.",
        "exemplos": ["EP13_C06: Dinjer sentada no chão durante fala", "EP04_C05/EP08_C03: avó deitada + Djanira sentada", "EP14_C02: Djanira em degrau real"]},
    "objetos_e_maos": {"capacidades": {"entrega", "papel", "bandeja", "sacola", "remedio", "recurso_sapato", "trabalho", "limpeza", "escrever", "bebida", "foto", "celular", "ligacao"},
        "contrato": "Prop próprio por código de modelo comprovado, attach/detach genérico em osso real com transform medido. Transferência A/B usa fase real do clipe e troca única de dono; papel abre/fecha/lê, sacola abraça, bandeja acompanha duas mãos. Modelo/clip candidato semanticamente diferente não substitui objeto. Telefone distingue texto/ligação e toque é faixa real ou CapCut explicitamente separado.",
        "exemplos": ["EP08_C05/EP11_C05: papel entregue", "EP12_C04: envelope + choro", "EP01_C02/EP05_C01: capina com ferramenta adequada"]},
    "rotas_portas_e_interiores": {"capacidades": {"caminhar", "andar_estilo", "porta", "interior", "esconder"},
        "contrato": "Rotas de waypoints relativos a marcas capturadas e chão/colisão; chegada medida sem warp. Porta/portão real identificado por modelo/posição, estado inicial restaurado; batidas têm contagem3 quando prevista. Interior carregado e acessível antes da montagem; cadeira/lateral/porta têm subpontos reais e acesso físico.",
        "exemplos": ["EP03_C02/EP14_C01: porta e batidas", "EP11_C01: entrada + figurantes", "EP07_C02: ator escondido atrás de apoio"]},
    "veiculos_terrestres": {"capacidades": {"onibus", "carro", "carona", "moto", "caminhao"},
        "contrato": "Veículo próprio com modelo e assentos reais, motorista/ocupantes, portas, entrada/saída e rota física capturada. Reset retorna veículos e atores à tomada sem afetar veículo/pessoas do usuário. Ônibus precisa efetivamente desembarcar, carros sair e caminhões ter três subtomadas.",
        "exemplos": ["EP01_C06/EP04_C01/EP06_C01: desembarque de ônibus", "EP11_C03: montagem de carro/andar", "EP13_C03: caminhões e portões"]},
    "pares_e_cascatas": {"capacidades": {"briga", "abraco", "ameaca"},
        "contrato": "Pares sincronizados por grupo com referência/rotação/tempo comum e fase real; aproximação, contato, reação e término explícitos. Não ligar CombatAI. Briga4atores inclui cada fala e empurrão/reação; abraço e ameaça requerem distância/olhar próprios.",
        "exemplos": ["EP01_C07: barraco4atores", "EP06_C04: sentar, abraço e saída", "EP06_C06: ameaça de pescoço"]},
    "voo_e_agua": {"capacidades": {"voo_queda", "agua"},
        "contrato": "Aeronave real, assentos/rota/altitude capturados ou derivados de marcador de água real; queda segura e invencível, GET_WATER_HEIGHT, saída/rampa e nadar/escalar físicos. Não simular avião parado no solo nem elevar coordenadas sem cenário autorizado.",
        "exemplos": ["EP01_C01: queda, água e chegada", "EP07_C03/C04: cabine/voo/bandeja", "EP07_C05: reaproveitar queda real"]},
    "extras_e_continuidade": {"capacidades": {"elenco_adicional", "populacao_propria", "figurino_conflito", "quadro_reutilizavel"},
        "contrato": "Figurantes próprios descritos no texto, com visual distinto registrado e preservados da limpeza. Wardrobe permanente imutável; variação temporária de sapatos documentada e restaurada. Salvar snapshot de câmera/ambiente e reutilizar quadro exatamente; não inferir presença por enumeração incompleta do elenco.",
        "exemplos": ["EP03_C07: gerente/amiga/colegas", "EP06_C02→EP12_C01: quadro idêntico", "EP10_C05: população da calçada"]},
    "montagem_e_reuso": {"capacidades": {"montagem", "montagem_locacoes", "reutilizacao", "edicao_externa", "danca"},
        "contrato": "Subtomadas com IDs/ordem/duração/alvos independentes e guia CapCut; duas pontas da ligação em locações separadas. Reuso aponta SHA de MP4 realmente capturado, cartelas/créditos e voz externa preservados; nunca declarar reuso concluído sem arquivo.",
        "exemplos": ["EP05_C03: ligação em dois locais", "EP13_C03/EP15_C08: montagem", "VINHETA_01: quatro danças e créditos"]},
}


def plano_implementacao(cenas: list[dict], pesquisa: dict) -> dict:
    """Matriz completa: incidência sobreposta, grupos necessários e fonte literal."""
    coverage = {x["id"]: x for x in pesquisa["coberturaRoteiro"]}
    mapping = {cap: name for name, spec in GRUPOS_IMPLEMENTACAO.items() for cap in spec["capacidades"]}
    rows, impacts = [], {name: [] for name in GRUPOS_IMPLEMENTACAO}
    for scene in cenas:
        caps = coverage[scene["id"]]["capacidades"]
        unknown = set(caps) - set(mapping)
        if unknown:
            raise ErroPiloto(f"{scene['id']}: capacidades sem plano de implementação: {sorted(unknown)}")
        groups = sorted({mapping[x] for x in caps})
        if not scene["runtimeRascunho"]:
            for group in groups:
                impacts[group].append(scene["id"])
        rows.append({"id": scene["id"], "runtimeRascunho": scene["runtimeRascunho"], "pronto": False,
            "origem": scene["origem"], "acoesOriginais": scene["acoesOriginais"], "notasOriginais": scene["notasOriginais"],
            "blocosOriginais": scene["roteiroOriginal"], "temposOriginais": scene["temposOriginais"],
            "gruposParaAtuacaoInteira": groups, "capacidadesNaPesquisa": caps,
            "criterio": "Adaptador só é emitido após todas as ações/poses/olhares/props/rotas da cena terem controller; em seguida recursos reais e teste visual continuam obrigatórios."})
    groups = []
    for name, spec in GRUPOS_IMPLEMENTACAO.items():
        impacted = impacts[name]
        alone = [r["id"] for r in rows if not r["runtimeRascunho"] and r["gruposParaAtuacaoInteira"] == [name]]
        groups.append({"codigo": name, "cenasPendentesAfetadas": impacted, "incidencia": len(impacted),
            "cenasComSoEsteGrupo": alone, "contratoProposto": spec["contrato"], "exemplos": spec["exemplos"],
            "estado": "API_proposta_ou_adapter_por_completar;nao_evidencia_de_execucao"})
    groups.sort(key=lambda x: (-x["incidencia"], x["codigo"]))
    return {"versao": 1, "tipoDocumento": "plano_implementacao_95", "totalCenas": 95,
        "rascunhosIntegrais": [x["id"] for x in cenas if x["runtimeRascunho"]],
        "cenasPendentes": [x["id"] for x in cenas if not x["runtimeRascunho"]], "grupos": groups, "cenas": rows,
        "prioridade": ["interpretacao", "apoios_e_pose", "objetos_e_maos", "rotas_portas_e_interiores", "extras_e_continuidade",
            "pares_e_cascatas", "veiculos_terrestres", "voo_e_agua", "montagem_e_reuso"],
        "aviso": "Incidências se sobrepõem e não equivalem a cenas completas desbloqueadas; cada cena tem lista de todos os grupos necessários. Os95IDs preservam fonte/linha/SHA/ações/vozes/tempos. Nenhum runtime é apresentado como gravado ou testado."}


def apoios_candidatos(pesquisa: dict) -> dict:
    """Modelos realmente listados; geometria/pose requerem medição no jogo."""
    source = exigir_sem_vinculos(Path(pesquisa["objetos"][0]["origem"]["arquivo"]))
    raw = source.read_bytes()
    sha = _hash(raw)
    if sha != pesquisa["objetos"][0]["origem"]["sha256"]:
        raise ErroPiloto("PropList mudou antes de derivar propostas de apoios.")
    lines = raw.decode("utf-8-sig", errors="replace").splitlines()
    requested = (("cadeira_simples", "prop_chair_01a", "assento"), ("poltrona", "prop_armchair_01", "assento"),
        ("banco", "prop_bench_01a", "assento"), ("mesa", "prop_table_01", "mesa"),
        ("cadeira_casa", "v_res_fa_chair01", "assento"), ("cama_casa", "v_res_d_bed", "cama"),
        ("cama_quarto", "v_res_msonbed", "cama"), ("cama_simples", "v_res_tre_bed1", "cama"))
    result = []
    for code, model, kind in requested:
        matching = [i + 1 for i, value in enumerate(lines) if value.strip() == model]
        if not matching:
            raise ErroPiloto(f"Apoio candidato não consta na lista real: {model}.")
        result.append({"codigoProposto": code, "modelo": model, "tipoProposto": kind, "pronto": False,
            "origem": {"arquivo": str(source), "linha": matching[0], "sha256": sha, "texto": model},
            "geometriaMedida": None, "pontoAssentoMedido": None, "cenaTestada": None,
            "limite": "Nome comprova existência na lista, não superfície, escala, alinhamento corporal, colisão, adequação visual ou carregamento no jogo."})
    return {"versao": 1, "tipoDocumento": "proposta_apoios_nao_executavel", "pronto": False, "apoios": result,
        "preflightNecessario": ["Validar modelo e criar somente objeto próprio; salvar hash/modelo/handle.",
            "GetModelDimensions fornece caixa; medir superfície útil e altura do assento/cama com raycast e pose real, pois centro/bounding-box não prova apoio humano.",
            "Registrar transform relativo a marca real e zona livre; não inventar coordenadas absolutas ou ponto de assento.",
            "Entrada/base/saída reais; duração conferida, fala upper-body sobre pose primária; testar contato/oclusão e reset.",
            "Limpar somente objeto próprio; restaurar ator/pose/câmera sem alterar cenário original."]}


def _pesquisa(raiz: Path) -> dict:
    path = _confinado(raiz / "catalogos/acoes_serie_candidatas.json", raiz)
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if data.get("versao") != 1 or data.get("totalCenas") != 95:
        raise ErroPiloto("Catálogo de pesquisa deve referenciar as95cenas da origem.")
    cache = {}
    for kind, filename in (("animacoes", "PedAnimList.txt"), ("objetos", "PropList.txt")):
        seen = set()
        for item in data[kind]:
            if item["codigo"] in seen:
                raise ErroPiloto("Código de recurso duplicado na pesquisa.")
            seen.add(item["codigo"])
            origin = item["origem"]
            source = exigir_sem_vinculos(Path(origin["arquivo"]))
            if source.name != filename or source.parent.name != "menyooStuff":
                raise ErroPiloto("Origem de recurso não corresponde à lista local do Menyoo.")
            if source not in cache:
                raw = source.read_bytes()
                cache[source] = (_hash(raw), raw.decode("utf-8-sig", errors="replace").splitlines())
            digest, lines = cache[source]
            number = origin["linha"]
            if digest != origin["sha256"] or type(number) is not int or not 1 <= number <= len(lines) or lines[number - 1].strip() != origin["texto"]:
                raise ErroPiloto(f"Origem/linha/SHA256 não conferem para {item['codigo']}.")
            expected = item["dicionario"] + " " + item["animacao"] if kind == "animacoes" else item["modelo"]
            if expected != origin["texto"]:
                raise ErroPiloto(f"Recurso diverge da linha canônica: {item['codigo']}.")
    data["sha256ArquivoPesquisa"] = _hash(path.read_bytes())
    return data


def _segundos(ms: int):
    return ms // 1000 if ms % 1000 == 0 else ms / 1000


def _base(plano: dict) -> dict:
    atores = plano["adaptacao"]["elencoEfetivo"]
    camera = plano["original"]["camera"]
    preset = camera if camera in {"LONGE", "MEDIA", "PERTO", "BAIXA", "ALTA", "ATRAS", "OLHOS", "DOIS"} else "DOIS" if len(atores) >= 2 else "MEDIA"
    elenco = []
    for i, code in enumerate(atores):
        marca = {"direita": 0 if len(atores) == 1 else -1.1 if i == 0 else 1.1, "frente": 0, "altura": 0}
        look = next((x for x in atores if x != code), None)
        elenco.append({"personagem": code, "marca": marca, "olharPara": look})
    roteiro = []
    for bloco in plano["roteiro"]:
        tipo = "narracao" if bloco["tipo"] in ("narracao", "narrador") else bloco["tipo"]
        if plano["id"] == "EP01_C07" and tipo == "acao_final":
            tipo = "pausa"  # Os2s declarados executam a coreografia pelo helper; nunca fala.
        if tipo not in ("narracao", "fala", "pausa"):
            raise ErroPiloto(f"{plano['id']}: bloco de ação exige adaptador específico.")
        plano_trecho = "MEDIA" if camera == "auto" and tipo == "fala" else preset
        item = {"tipo": tipo, "duracaoSegundos": _segundos(bloco["duracaoMs"]), "plano": plano_trecho}
        if tipo != "pausa":
            item["texto"] = bloco["texto"]
        if tipo == "narracao":
            item["narrador"] = "Narrador"
        elif tipo == "fala":
            code = next((x for x in atores if x.startswith("DJANIRA_")), None) if bloco["quemOriginal"] == "DJANIRA" else bloco["quemOriginal"]
            item.update(personagem=code, gesto=bloco["gestoOriginal"])
        elif plano["id"] == "EP14_C03":
            item["personagem"] = "DINJER"  # Instrução literal: câmera fica nela durante os4s.
        roteiro.append(item)
    restante = plano["tempos"]["reservaSemBlocoDefinidoMs"]
    if restante:
        item = {"tipo": "pausa", "duracaoSegundos": _segundos(restante), "plano": preset}
        if plano["id"] == "EP14_C03":
            item["personagem"] = "DINJER"
        roteiro.append(item)
    return {"versao": 1, "id": plano["id"], "episodio": plano["episodio"], "numero": plano["numero"], "nome": plano["nome"],
            "ambiente": {"local": plano["original"]["locaisCodigos"][0], "hora": plano["original"]["hora"],
                         "clima": plano["original"]["clima"], "raioLimpeza": 40, "distanciaFrente": 4},
            "elenco": elenco, "camera": {"modo": "automatico", "planoInicial": preset,
                      "personagem": atores[0] if atores else None, "planoFala": "MEDIA" if camera == "auto" else preset,
                      "planoNarracao": preset, "planoFinal": preset}, "roteiro": roteiro, "acoes": []}


def _prop(ident: str, recurso: str, direita, frente, rotacao=0) -> dict:
    return {"id": ident, "recurso": recurso, "marca": {"direita": direita, "frente": frente, "altura": 0},
            "rotacao": rotacao, "assentarNoChao": True, "visivelInicial": True}


def _pose(ator: str, objeto: str, recurso: str, tipo="sentada") -> dict:
    return {"personagem": ator, "objeto": objeto, "recurso": recurso, "tipo": tipo}


def _vip_props() -> list[dict]:
    # A mesma geometria relativa/modelo/IDs é usada em EP06_C03 e EP12_C02.
    return [_prop("MESA_VIP", "mesa", 0, 1.1),
        _prop("CADEIRA_DINJER", "cadeira_simples", -1.1, 0, -90),
        _prop("CADEIRA_CHEFE", "cadeira_simples", 1.1, 0, 90),
        _prop("CADEIRA_CAPANGA_1", "cadeira_simples", -1.1, 2.2, -90),
        _prop("CADEIRA_CAPANGA_2", "cadeira_simples", 1.1, 2.2, 90)]


def construir_subtomadas_ep01(raiz: Path = RAIZ) -> dict[str, bytes]:
    """C05 tem dois locais reais: fontes separadas preservam o mesmo ID e a voz8s.

    Nenhum teleporte une as tomadas e este preparo não afirma um MP4 final pronto.
    O usuário monta/grava cada local; no CapCut usa3s da primeira+5s da segunda,
    com a narração original8s uma única vez. Vídeos brutos usam versões normais.
    """
    plano = next(x for x in _serie(raiz)["planos"] if x["id"] == "EP01_C05")
    expected = "**Ação:** A avó sai da clínica olhando para os lados, pega um caminho diferente e entra no cassino. Expressão muda de `machucado` para `feliz`."
    if [x["raw"] for x in plano["acoesOriginais"]] != [expected] or [x["duracaoMs"] for x in plano["roteiro"]] != [8000]:
        raise ErroPiloto("EP01_C05: ação/voz original mudou; revisar subtomadas antes de gerar.")
    files, specs = {}, []
    from preparar_piloto import gerar_srt_piloto
    for part, location, preset, start, end in (("hospital", "HOSPITAL_SANDY", "ATRAS", 2, -2),
                                              ("cassino", "CASSINO_FORA", "LONGE", -2, 2)):
        cena = _base(plano)
        cena.update(nome="O SEGREDO DA AVÓ — " + part, encenacao="serie_v1")
        cena["ambiente"].update(local=location, distanciaFrente=1)
        cena["elenco"][0]["marca"] = {"direita": 0, "frente": start, "altura": 0}
        cena["direcao"] = [{"tipo": "andar", "personagem": "DONA_IRACI", "inicioSegundos": 0, "duracaoSegundos": 8,
                            "destino": {"direita": 0, "frente": end, "altura": 0}, "velocidade": 0.7}]
        if part == "hospital":
            cena["interpretacao"] = [{"personagem": "DONA_IRACI", "inicioSegundos": 0, "duracaoSegundos": 2,
                "expressao": "machucado", "olhar": {"tipo": "marca", "destino": {"direita": -3, "frente": 0, "altura": 1.6}}},
                {"personagem": "DONA_IRACI", "inicioSegundos": 2, "duracaoSegundos": 2, "expressao": "machucado",
                 "olhar": {"tipo": "marca", "destino": {"direita": 3, "frente": 0, "altura": 1.6}}},
                {"personagem": "DONA_IRACI", "inicioSegundos": 4, "duracaoSegundos": 4, "expressao": "feliz"}]
        else:
            cena["interpretacao"] = [{"personagem": "DONA_IRACI", "inicioSegundos": 0, "duracaoSegundos": 8, "expressao": "feliz"}]
        cena["camera"] = {"modo": "automatico", "planoInicial": preset, "personagem": "DONA_IRACI",
                          "planoNarracao": preset, "planoFala": "MEDIA", "planoFinal": preset}
        for trecho in cena["roteiro"]:
            trecho["plano"] = preset
        path = f"producao/subtomadas/EP01_C05/{part}/EP01_C05.json"
        files[path] = _json(cena)
        files[path.replace(".json", ".base.srt")] = gerar_srt_piloto(cena).encode("utf-8")
        specs.append({"parte": part, "local": location, "arquivo": path, "sha256": _hash(files[path]),
                      "id": "EP01_C05", "conteudoMs": 8000, "contagemMs": 3000, "capturaDesdeF7Ms": 11000})
    files["producao/subtomadas/EP01_C05/manifesto.json"] = _json({"versao": 1, "id": "EP01_C05", "origem": plano["origem"],
        "acoesOriginais": plano["acoesOriginais"], "roteiroOriginal": plano["roteiro"], "subtomadas": specs,
        "estado": "duas_fontes_carregaveis;passagens_reais_e_teste_visual_pendentes;montagem_final_externa",
        "pronto": False, "gravado": False, "mp4FinalGerado": False,
        "montagem": {"hospitalMs": 3000, "cassinoMs": 5000, "vozOriginalMs": 8000, "boca": False,
            "instrucao": "Gravar separadamente as duas fontes do mesmoID. Usar3s úteis do hospital+5s úteis do cassino no CapCut com uma única narração original8s; remover contagens. OsMP4brutos versionados não são uma cena única já montada."},
        "requisitos": ["HOSPITAL_SANDY limiar real aberto apontando para dentro; saída sem atravessar porta fechada.",
            "CASSINO_FORA limiar/vestíbulo real aberto apontando para dentro; fachada é alternativa literal, interior não é alegado acessível.",
            "Preflight3raycasts e chegada física; bloqueio interrompe em vez de ocultar/teleportar atriz."]})
    files["producao/subtomadas/EP01_C05/README.md"] = ("# EP01_C05 — duas tomadas do segredo da avó\n\n"
        "Hospital e cassino são capturados separadamente com o mesmo código EP01_C05. Cada fonte conserva a narração original de 8 segundos e inclui 3 segundos de contagem: 11 segundos brutos por tomada. Nada une os locais por teleporte durante a filmagem.\n\n"
        "A marca HOSPITAL_SANDY deve estar no limiar real aberto da clínica, com o jogador olhando para dentro. A avó começa 3 metros para dentro da marca e sai até 1 metro para fora, olhando para os dois lados; a expressão candidata passa de machucado para feliz. A marca CASSINO_FORA segue a mesma orientação: a avó começa 1 metro fora e caminha até 3 metros para dentro de uma passagem/vestíbulo realmente aberto. A alternativa pela fachada não confirma acesso ao interior do cassino. Três medições de colisão conferem cada passagem; porta fechada ou travessia incompleta interrompem a tomada.\n\n"
        "Com a produção EP01 e a cena EP01_C05 selecionadas, T alterna hospital/cassino fora da captura e F6 monta a seleção. Cada local exige sua própria marca real por F3, escolhida com L/K e cena limpa por F10. F7 restaura, conta 3 segundos e executa a tomada; Delete restaura para repetir. Os MP4/SRT ficam no episódio com versões normais EP01_C05, EP01_C05_v2 etc., sem sobrescrever tomadas antigas; o log SUBTOMADA_GRAVACAO registra a parte usada. A gravação continua pela Barra de Jogos na camada habilitada pelo Diretor.\n\n"
        "No CapCut, retirar as contagens e usar 3 segundos úteis do hospital seguidos de 5 segundos úteis do cassino. Aplicar a narração original de 8 segundos uma única vez e conservar o texto original na legenda final. Os SRT brutos pertencem a cada tomada e não são a legenda já ajustada dessa montagem. Nenhum MP4 final único foi gerado por este preparo.\n\n"
        "Recursos carregáveis, passagem, encaixe da câmera, expressões e adequação dramática aguardam teste no GTA; os figurinos registrados são reutilizados. EP01_C05 permanece pendente de montagem completa no índice da temporada. A ação literal e a voz de origem estão preservadas em manifesto.json, com os hashes das duas fontes JSON.\n").encode("utf-8")
    return files


def construir_rascunho(plano: dict, pesquisa: dict, raiz: Path = RAIZ) -> tuple[dict | None, list[str]]:
    """Retornar None quando faltar atuação; não gerar substituto estacionário."""
    ident = plano["id"]
    actions = [x["raw"] for x in plano["acoesOriginais"]]
    if ident == "EP01_C08":
        return montar_rascunho(raiz), ["pedido_v1 implementa chegada12s, voz30s e celular4s; teste visual pendente."]
    if ident in EPISODIO_UM:
        if actions != EPISODIO_UM[ident]:
            raise ErroPiloto(f"{ident}: ação literal mudou; revisar a adaptação do Episódio 1.")
        cena = _base(plano)
        cena.update(encenacao="serie_v1", direcao=[])
        if ident == "EP01_C01":
            if [(x["tipo"], x["duracaoMs"]) for x in plano["roteiro"]] != [("narracao", 14000)]:
                raise ErroPiloto("EP01_C01: narração original14s mudou; revisar o orçamento físico antes de gerar.")
            cena["ambiente"].update(local="CANAL_LS", distanciaFrente=1)
            cena["camera"] = {"modo": "automatico", "planoInicial": "LONGE", "personagem": "DJANIRA_POBRE",
                "planoNarracao": "LONGE", "planoFala": "MEDIA", "planoFinal": "OLHOS"}
            cena["roteiro"][0]["plano"] = "LONGE"
            cena["roteiro"].append({"tipo": "pausa", "duracaoSegundos": 6, "plano": "OLHOS"})
            return cena, ["Remontagem autorizada: avião em voo contínuo, saída física, queda e água; sem subida de rampa ou corrida. A ação literal original com rampa/fuga permanece na fonte e esta mudança de escopo fica explícita. Narração original14s sem boca, seguida de reserva silenciosa6s; bruto20s+contagem3s, sem comprimir a voz.",
                "Somente CANAL_LS real na água é requerida nesta remontagem; os três subpontos antigos de rampa/fuga não são dependências desta tomada. Corredor de água, soltura e impacto continuam medidos no GTA; não cria XYZ absoluto, saída fictícia ou teleporte durante a ação.",
                "Trecho AEROPORTO_AR deriva do ponto/direção reais de CANAL_LS: voo ativo35m/s à altura relativa configurada no helper acima da água medida, com câmera externa própria e contagem fixa. O avião continua voando após a saída. Invencibilidade/flags de afogamento e validações físicas permanecem obrigatórias; ensaio separado antes da produção, resultado visual pendente."]
        if ident == "EP01_C02":
            cena["direcao"] = [{"tipo": "andar", "personagem": "DJANIRA_POBRE", "inicioSegundos": 0,
                "duracaoSegundos": 4, "destino": {"direita": 0, "frente": 2, "altura": 0}, "velocidade": 0.7},
                {"tipo": "clipe", "personagem": "DJANIRA_POBRE", "inicioSegundos": 4, "duracaoSegundos": 16,
                  "recurso": "trabalho_plantacao_f", "loop": True, "corpoInteiro": True}]
            cena["roteiro"].append({"tipo": "pausa", "duracaoSegundos": 10, "plano": "MEDIA", "personagem": "DJANIRA_POBRE"})
            return cena, ["Remontagem20s: caminhada lenta2m durante0–4s, seguida de16s trabalhando manualmente na plantação com o clipe feminino real de jardinagem. Narração original10s sem boca e reserva silenciosa10s; câmeraLONGE até10s, depoisMEDIA, plano finalLONGE. Não acrescenta enxada nem chama a ação de capina; cartelaQUATRO SEMANAS ANTES no CapCut. Trabalho/terreno/semântica precisam do teste visual."]
        if ident == "EP01_C03":
            for ator, x in zip(cena["elenco"], (-0.35, 0.35)):
                ator["marca"]["direita"] = x
            bag = _prop("SACOLA_IRACI", "sacola", 0.35, 0)
            bag["assentarNoChao"] = False
            attachment = lambda code: {"personagem": code, "mao": "direita", "offset": {"x": 0, "y": 0, "z": 0},
                                       "rotacao": {"x": 0, "y": 0, "z": 0}}
            bag["attachmentInicial"] = attachment("DONA_IRACI")
            bag["eventos"] = [{"tipo": "transferir", "inicioSegundos": 1, "esperaMaximaSegundos": 0.7, **attachment("DJANIRA_POBRE")},
                               {"tipo": "transferir", "inicioSegundos": 4, "esperaMaximaSegundos": 1.5, **attachment("DONA_IRACI")}]
            cena["objetos"] = [bag]
            cena["direcao"] = [{"tipo": "clipe", "personagem": code, "inicioSegundos": 0, "duracaoSegundos": 6,
                                "recurso": resource, "loop": True, "corpoInteiro": False}
                               for code, resource in (("DJANIRA_POBRE", "entrega_b"), ("DONA_IRACI", "entrega_a"))]
            cena["camera"] = {"modo": "programado", "planoInicial": "DOIS", "personagem": "DJANIRA_POBRE", "planoFinal": "MEDIA",
                "cortes": [{"inicioSegundos": 0, "plano": "DOIS", "personagem": "DJANIRA_POBRE"},
                           {"inicioSegundos": 6, "plano": "MEDIA", "personagem": "DONA_IRACI"}]}
            for trecho in cena["roteiro"]:
                trecho.pop("plano", None)
            return cena, ["Alternativa em pé explicitamente permitida no original. Djanira ajeita a sacola da avó: recebe na janela1-1,7s e devolve na janela4-5,5s; prop real/clipes de mãos, contato máximo0,35m medido no momento de cada transferência. Distância inicial0,7m candidata a ensaio; fora do alcance interrompe sem teleportar objeto/ator. Após6s ambas convivem, câmeraDOIS→MEDIA, voz9s sem boca. Não é confirmação de encaixe das mãos."]
        if ident == "EP01_C06":
            cena["interpretacao"] = [{"personagem": "DINJER", "inicioSegundos": 0, "duracaoSegundos": 9, "expressao": "convencido"}]
            cena["camera"] = {"modo": "programado", "planoInicial": "LONGE", "personagem": "DINJER", "planoFinal": "LONGE",
                "cortes": [{"inicioSegundos": 0, "plano": "LONGE", "personagem": "DINJER"},
                           {"inicioSegundos": 3, "plano": "ATRAS", "personagem": "DINJER"}],
                "ajustes": {"aproximar": -2, "lado": 0.6, "altura": 0.2}}
            for trecho in cena["roteiro"]:
                trecho.pop("plano", None)
            return cena, ["Remontagem mantém narração original9s sem boca e contagem3s: ônibusbus próprio, desembarque físico com limite5s dentro dos9s e caminhada real estilo move_f@posh@ até o terminal, com chão/chegada medidos e sem warp. CâmeraLONGE estabelece ônibus/desembarque, ATRAS acompanha Dinjer desde3s; ajustes aproximar−2/lado0,6/altura0,2, plano finalLONGE. Expressão candidata confiante; passagem/veículo/rota precisam ficar livres e falhas físicas interrompem a tomada."]
        if ident == "EP01_C04":
            cena["ambiente"]["distanciaFrente"] = 1
            cena["elenco"][0]["marca"] = {"direita": -0.5, "frente": -4, "altura": 0}
            cena["elenco"][1]["marca"] = {"direita": 0.3, "frente": -4, "altura": 0}
            cena["camera"].update(planoInicial="LONGE", planoNarracao="LONGE", planoFinal="LONGE", inverterEixo=True)
            cena["roteiro"][0]["plano"] = "LONGE"
            cena["roteiro"].append({"tipo": "pausa", "duracaoSegundos": 4, "plano": "LONGE"})
            cena["direcao"] = [{"tipo": "andar", "personagem": "DJANIRA_POBRE", "inicioSegundos": 0, "duracaoSegundos": 3,
                "destino": {"direita": -0.5, "frente": -2, "altura": 0}, "velocidade": 0.9},
                {"tipo": "andar", "personagem": "DONA_IRACI", "inicioSegundos": 0, "duracaoSegundos": 3,
                 "destino": {"direita": 0.3, "frente": -2, "altura": 0}, "velocidade": 0.9},
                {"tipo": "andar", "personagem": "DONA_IRACI", "inicioSegundos": 4, "duracaoSegundos": 7,
                 "destino": {"direita": 0.3, "frente": 2, "altura": 0}, "velocidade": 0.7},
                {"tipo": "andar", "personagem": "DJANIRA_POBRE", "inicioSegundos": 6, "duracaoSegundos": 5,
                 "destino": {"direita": -0.5, "frente": -3.5, "altura": 0}, "velocidade": 1}]
            return cena, ["Remontagem na entrada FRONT real de HOSPITAL_SANDY, direção para dentro e distânciaFrente1m. Ambas começam4m antes da âncora e aproximam-se2m em0–3s; Djanira se despede4–6s pelo helper, enquanto Iraci entra4m em4–11s. Djanira se afasta1,5m em6–11s. Narração original7s sem boca e reserva silenciosa4s; câmeraLONGE com inverterEixo=true. Passagem e chão reais são sondados; porta fechada/colisão/chegada inválida interrompem, sem esconder ator, desbloquear porta ou teleportar. Preserva a ação e exige avaliação visual da nova tomada."]
        if ident == "EP01_C07":
            if [x["duracaoMs"] for x in plano["roteiro"]] != [3000, 9000, 3000, 3000, 2000]:
                raise ErroPiloto("EP01_C07: falas ou ação final2s mudaram; revisar coreografia.")
            slots = {"DINJER": (0, 0), "COLEGA_1": (0.85, 0.45), "COLEGA_2": (-0.65, 0.65), "COLEGA_3": (-1.3, 0.15)}
            partners = {"DINJER": "COLEGA_1", "COLEGA_1": "DINJER", "COLEGA_2": "COLEGA_3", "COLEGA_3": "COLEGA_2"}
            for ator in cena["elenco"]:
                x, y = slots[ator["personagem"]]
                ator["marca"] = {"direita": x, "frente": y, "altura": 0}
                ator["olharPara"] = partners[ator["personagem"]]
            cena["roteiro"].insert(0, {"tipo": "pausa", "duracaoSegundos": 4, "plano": "LONGE", "personagem": "DINJER"})
            cena["roteiro"][-1]["duracaoSegundos"] = 12
            cena["camera"].update(modo="programado", planoFinal="DOIS", inverterEixo=True, cortes=[
                {"inicioSegundos": 0, "plano": "DOIS", "personagem": "DINJER"},
                {"inicioSegundos": 4, "plano": "MEDIA", "personagem": "COLEGA_1"},
                {"inicioSegundos": 7, "plano": "MEDIA", "personagem": "DINJER"},
                {"inicioSegundos": 16, "plano": "MEDIA", "personagem": "COLEGA_2"},
                {"inicioSegundos": 19, "plano": "MEDIA", "personagem": "DINJER"},
                {"inicioSegundos": 22, "plano": "DOIS", "personagem": "DINJER"}])
            return cena, ["Remontagem autorizada em 22/10/2026, 34s + contagem 3s: prefixo silencioso 4s e quatro falas originais 3/9/3/3s intactas. A ação final reserva 10s de briga simultânea em duas duplas, DINJER/COLEGA_1 e COLEGA_2/COLEGA_3, com os quatro alternando ataques e reações; segue-se um fecho cômico de 2s. Marcas em arco frontal e câmera programada com inverterEixo=true preservadas; DOIS desde 22s mostra as duas duplas juntas. O helper encadeia clipes coreografados por fases nativas e blending, com reações sobrepostas e raízes protegidas, sem combate de IA nem paradas forçadas entre golpes. Figurinos e roteiro original preservados; configuração técnica não registra aprovação visual do usuário."]
        cena["elenco"].append({"personagem": "DINJER", "marca": {"direita": 2, "frente": -1, "altura": 0}, "olharPara": None})
        cena["interpretacao"] = [{"personagem": "DJANIRA_POBRE", "inicioSegundos": 0, "duracaoSegundos": 7,
                                   "olhar": {"tipo": "ator", "alvo": "DINJER"}}]
        return cena, ["Dinjer é presença adicional exigida pela ação literal, embora não conste no metadado do elenco; mantém seuXML e parte na moto própriafaggio durante os7s. Rota física11m/3m/s com parada medida fora do enquadramento, sem warp durante gravação. Djanira permanece sozinha emPERTO olhando a amiga partir; vozNarrador não cria personagem nem mexe boca. Moto/câmera/olhar pendentes de teste."]
    if ident in IMOVEIS:
        if actions != [IMOVEIS[ident]]:
            raise ErroPiloto(f"{ident}: ação literal mudou; revisar adaptador de imobilidade.")
        return _base(plano), ["Ator imóvel conforme a ação original, no eixo da marca real; direção do horizonte/prédio depende da captura F3."]
    if ident in ANDANCAS:
        if actions != [ANDANCAS[ident]]:
            raise ErroPiloto(f"{ident}: ação literal mudou; revisar rota.")
        cena = _base(plano)
        tempo = plano["tempos"]["conteudoPlanejadoMs"]
        if tempo < 4000:
            raise ErroPiloto("Rota de4m não cabe no tempo original; não comprimir voz nem teleportar.")
        cena.update(encenacao="serie_v1", direcao=[{"tipo": "andar", "personagem": cena["elenco"][0]["personagem"],
                    "inicioSegundos": 0, "duracaoSegundos": _segundos(tempo),
                    "destino": {"direita": 0, "frente": 4, "altura": 0}, "velocidade": 1}])
        notas = ["Rota cênica explícita4m para frente do eixo capturado, durante a voz original; chão/colisão reais e chegada sem warp conferidos pelo backend."]
        if ident == "EP08_C02":
            notas.append("Conflito registrado: descalça no roteiro; figurino DJANIRA_POBRE registrado permanece fixo pelo pedido prioritário.")
        return cena, notas
    if ident in CHAMADAS:
        if actions != [CHAMADAS[ident]]:
            raise ErroPiloto(f"{ident}: chamada literal mudou; revisar aproximação/escuta.")
        cena = _base(plano)
        if ident == "EP02_C04":
            if [x["duracaoMs"] for x in plano["roteiro"]] != [5000, 4000, 9000] or plano["roteiro"][0]["quemOriginal"] != "DONA_IRACI":
                raise ErroPiloto("EP02_C04: janela de aproximação mudou; não sobrepor caminhada à fala de Djanira.")
            djanira = next(x for x in cena["elenco"] if x["personagem"] == "DJANIRA_POBRE")
            djanira["marca"].update(direita=-0.35, frente=-3)
            next(x for x in cena["elenco"] if x["personagem"] == "DONA_IRACI")["marca"]["direita"] = 0.35
            cena.update(encenacao="serie_v1", direcao=[{"tipo": "andar", "personagem": "DJANIRA_POBRE",
                "inicioSegundos": 0, "duracaoSegundos": 5,
                "destino": {"direita": -0.35, "frente": 0, "altura": 0}, "velocidade": 1}])
            return cena, ["A chamada verbal original acompanha aproximação real3m pelo eixo frontal durante os5s da fala da avó; marcas laterais±0,35m conservam o corredor de chão real. Djanira chega antes da própria fala. Sem warp, corte de voz ou mudança do lado no eixo."]
        return cena, ["A avó chama verbalmente na fala original; Djanira permanece de pé ouvindo e responde, conforme ação. Gestos das três falas preservados; duas narrações sem boca. Todos31s preservados, apesar da estimativa18s."]
    if ident in CELULARES_EPISODIO_DOIS:
        if actions != [CELULARES_EPISODIO_DOIS[ident]]:
            raise ErroPiloto(f"{ident}: ação literal mudou; revisar telefone/expressão/olhar.")
        expected = [("fala", 4000), ("narracao", 12000)] if ident == "EP02_C06" else [("narracao", 8000 if ident == "EP02_C03" else 12000)]
        if [(x["tipo"], x["duracaoMs"]) for x in plano["roteiro"]] != expected:
            raise ErroPiloto(f"{ident}: os blocos originais mudaram; não comprimir voz.")
        cena = _base(plano)
        owner = "DONA_IRACI" if ident == "EP02_C06" else "DJANIRA_POBRE"
        phone_id = "CELULAR_IRACI" if owner == "DONA_IRACI" else "CELULAR_DJANIRA"
        phone = _prop(phone_id, "celular", 0, 0)
        phone.update(assentarNoChao=False, attachmentInicial={"personagem": owner, "mao": "direita",
            "offset": {"x": 0, "y": 0, "z": 0}, "rotacao": {"x": 0, "y": 0, "z": 0}})
        duration = 16 if ident == "EP02_C06" else 8 if ident == "EP02_C03" else 12
        cena.update(encenacao="serie_v1", objetos=[phone], direcao=[{"tipo": "clipe", "personagem": owner,
            "inicioSegundos": 4 if ident == "EP02_C06" else 0, "duracaoSegundos": 12 if ident == "EP02_C06" else duration,
            "recurso": "celular_texto_f", "loop": True, "corpoInteiro": True, "manterPoseFinal": False}])
        look = {"tipo": "objeto", "alvo": phone_id}
        if ident == "EP02_C03":
            cena["interpretacao"] = [{"personagem": owner, "inicioSegundos": 0, "duracaoSegundos": 8,
                "expressao": "estressado", "olhar": look}]
            return cena, ["Celular próprio na mão direita e clipe feminino real de leitura durante8s, com facial estressado e olhar para o centro medido do objeto móvel. Cena às21h conforme origem; ausência de resposta é contada na narração, sem interface/texto inventado ou boca. Encaixe da mão, escuridão e intensidade facial precisam do teste visual."]
        if ident == "EP02_C05":
            cena["interpretacao"] = [{"personagem": owner, "inicioSegundos": 0, "duracaoSegundos": 2,
                "expressao": "normal", "olhar": look}, {"personagem": owner, "inicioSegundos": 2,
                "duracaoSegundos": 10, "expressao": "comraiva", "olhar": look}]
            return cena, ["Celular próprio e leitura corporal real durante12s; facial passa de normal para comraiva aos2s, adaptação temporal explícita da mudança de expressão sem reduzir a voz. Olhar acompanha o objeto medido, planoPERTO e narração sem boca. Não desenha tela de bloqueio nem cartela; encaixe e expressão aguardam teste visual."]
        if plano["roteiro"][0]["quemOriginal"] != "DONA_IRACI" or plano["roteiro"][0]["gestoOriginal"] != "explicando":
            raise ErroPiloto("EP02_C06: fala/gesto originais da avó mudaram.")
        for actor in cena["elenco"]:
            actor["marca"].update(direita=-0.35 if actor["personagem"] == owner else 0.35, frente=0)
            actor["olharPara"] = None
        cena["camera"] = {"modo": "programado", "planoInicial": "MEDIA", "personagem": "DJANIRA_POBRE", "inverterEixo": True,
            "planoFinal": "PERTO", "cortes": [{"inicioSegundos": 0, "plano": "MEDIA", "personagem": "DJANIRA_POBRE"},
                {"inicioSegundos": 4, "plano": "PERTO", "personagem": "DJANIRA_POBRE"}]}
        for segment in cena["roteiro"]:
            segment.pop("plano", None)
        cena["interpretacao"] = [{"personagem": actor["personagem"], "inicioSegundos": 0,
            "duracaoSegundos": 16, "olhar": look.copy()} for actor in cena["elenco"]]
        return cena, ["A avó apresenta o telefone próprio preso à mão durante a fala/gestoexplicando0–4s; leitura corporal real4–16s começa após o gesto para não competir com ele. As duas, lado a lado±0,35m, acompanham o mesmo centro medido do celular em movimento. FalaIraci4s com boca e narração12s sem boca intactas, MEDIA→PERTO aos4s.",
            "Não existe par literal de mostrar tela confirmado na lista: apresentação usa o gesto explicando original e o telefone físico, seguida de leitura compartilhada. Alcance da cabeça, orientação da tela/encaixe e apresentação visual precisam do GTA; getter inválido, objeto invisível ou identidade trocada abortam, sem alvo estático fictício."]
    if ident in INTERPRETADAS:
        if actions != [INTERPRETADAS[ident]]:
            raise ErroPiloto(f"{ident}: interpretação literal mudou; revisar olhar/expressão/presenças.")
        cena = _base(plano)
        cena.update(encenacao="serie_v1", direcao=[])
        if ident == "EP08_C04":
            if [x["duracaoMs"] for x in plano["roteiro"]] != [8000, 3000, 4000, 15000]:
                raise ErroPiloto("EP08_C04: tempos das quatro falas mudaram.")
            cena["interpretacao"] = [{"personagem": "DONA_IRACI", "inicioSegundos": 0, "duracaoSegundos": 30,
                                      "expressao": "chateado", "olhar": {"tipo": "desvio"}}]
            return cena, ["Desvio real de cabeça durante30s prevalece sobre LookAt automático, inclusive como ouvinte; facial chateado é candidato técnico para a vergonha, boca conserva prioridade nas falas. Sem mudar as quatro falas/gestos.",
                          "FlashbackEP01_C05 permanece reaproveitamento separado no CapCut; exige vídeo real daquela cena, não substitui nem interrompe estas falas. Adequação dramática/expressão pendentes de teste."]
        if ident == "EP10_C07":
            if [x["duracaoMs"] for x in plano["roteiro"]] != [4000, 6000]:
                raise ErroPiloto("EP10_C07: narração4s/narrador6s mudaram.")
            cena["interpretacao"] = [{"personagem": "DJANIRA_POBRE", "inicioSegundos": 0, "duracaoSegundos": 4, "expressao": "feliz"}]
            return cena, ["Facial feliz durante os4s do close narrado, voltando ao normal no narrador6s; sem riso corporal, boca ou fala nova. Intensidade de sorriso mínimo é candidata visual a conferir; todos10s de voz preservados."]
        if plano["adaptacao"]["elencoEfetivo"] != ["DJANIRA_RICA"] or [x["duracaoMs"] for x in plano["roteiro"]] != [14000]:
            raise ErroPiloto("EP15_C06: presença/voz original mudou.")
        cena["elenco"] = [{"personagem": code, "marca": {"direita": x, "frente": 0, "altura": 0}, "olharPara": None}
                           for code, x in (("DJANIRA_RICA", 0), ("O_CHEFE", -2.2), ("DINJER", 2.2))]
        cena["interpretacao"] = [{"personagem": "DJANIRA_RICA", "inicioSegundos": 0, "duracaoSegundos": 14, "expressao": "normal"}]
        return cena, ["PaiO_CHEFE e amigaDINJER são presenças exigidas na ação literal, embora ausentes no metadado de elenco; registrados como adicionais da ação, sem alterar o original. Djanira permanece exatamente entre ambos em três marcas relativas distintas, sem reação/boca durante14s.",
                      "Bloqueio lateral0/−2,2/+2,2m é coreografia relativa declarada, no eixo real da sala; capturar espaço interno suficiente e preservar a continuidade do pai à janela daEP15_C04. Não inventa XYZ absoluto, não renova os figurinos."]
    if ident in POSES_INICIAIS:
        if actions != [POSES_INICIAIS[ident]]:
            raise ErroPiloto(f"{ident}: pose/apoio/ação literal mudou; revisar adaptador integral.")
        expected = {"EP04_C05": ("deitada_cama_f", "sentada_cadeira_f"),
            "EP09_C04": ("sentada_cadeira_f",), "EP06_C03": ("sentada_cadeira_f", "sentado_cadeira_m"),
            "EP12_C02": ("sentada_cadeira_f",)}[ident]
        available = {x["codigo"] for x in pesquisa["animacoes"]}
        if not set(expected) <= available:
            raise ErroPiloto(f"{ident}: pose exige recursos canônicos reais conferidos na fonte.")
        cena = _base(plano)
        cena.update(encenacao="serie_v1", direcao=[])
        nota_base = "Poses iniciais são montadas fora da captura em props próprios, chão/bounds/raycast reais; pelvis/cabeça/pés/joelhos precisam passar no preflight. Nada senta/deita por teleporte durante a ação. Fala usa somente camada superior/secondary; compatibilidade visual da malha/pose permanece pendente."
        if ident == "EP09_C04":
            cena["objetos"] = [_prop("CADEIRA_DJANIRA", "cadeira_simples", -1.1, 0, -90)]
            cena["poses"] = [_pose("DJANIRA_POBRE", "CADEIRA_DJANIRA", "sentada_cadeira_f")]
            return cena, [nota_base, "Djanira realmente sentada em cadeira diante do atendente; cinco falas23s e reserva1s preservadas. Cena24s/captura27s, sem adicionar mesa/tela inexistente à ação."]
        if ident == "EP04_C05":
            cena["objetos"] = [_prop("CAMA_IRACI", "cama_casa", -1.1, 0), _prop("CADEIRA_DJANIRA", "cadeira_simples", 1.1, 0, 90)]
            cena["poses"] = [_pose("DONA_IRACI", "CAMA_IRACI", "deitada_cama_f", "deitada"),
                              _pose("DJANIRA_POBRE", "CADEIRA_DJANIRA", "sentada_cadeira_f")]
            cena["interpretacao"] = [{"personagem": "DONA_IRACI", "inicioSegundos": 0, "duracaoSegundos": 14, "expressao": "machucado"}]
            return cena, [nota_base, "Cama verdadeira para Iraci e cadeira ao lado para Djanira; as duas falas7s continuam com boca/gestos. Facial de dor é candidato para doença/muito mal, boca prioritária; não acrescenta feridas/figurino ou diagnóstico. Overlay durante deitada precisa passar no teste, sem omitir a fala."]
        cena["objetos"] = _vip_props()
        if ident == "EP06_C03":
            slots = {"DJANIRA_POBRE": (-6, 1.1), "DINJER": (-1.1, 0), "O_CHEFE": (1.1, 0),
                     "CAPANGA_1": (-1.1, 2.2), "CAPANGA_2": (1.1, 2.2)}
            for ator in cena["elenco"]:
                x, y = slots[ator["personagem"]]; ator["marca"] = {"direita": x, "frente": y, "altura": 0}
                ator["olharPara"] = "DINJER" if ator["personagem"] in ("DJANIRA_POBRE", "O_CHEFE") else "O_CHEFE"
            cena["poses"] = [_pose("DINJER", "CADEIRA_DINJER", "sentada_cadeira_f")]
            cena["poses"] += [_pose(code, "CADEIRA_" + code.replace("O_", ""), "sentado_cadeira_m") for code in ("O_CHEFE", "CAPANGA_1", "CAPANGA_2")]
            cena["interpretacao"] = [{"personagem": "DJANIRA_POBRE", "inicioSegundos": 0, "duracaoSegundos": 13,
                                      "olhar": {"tipo": "ator", "alvo": "DINJER"}}]
            cena["camera"] = {"modo": "programado", "planoInicial": "LONGE", "personagem": "DINJER", "planoFinal": "PERTO",
                               "cortes": [{"inicioSegundos": 0, "plano": "LONGE", "personagem": "DINJER"},
                                          {"inicioSegundos": 8, "plano": "PERTO", "personagem": "DINJER"}]}
            for trecho in cena["roteiro"]:
                trecho.pop("plano", None)
            return cena, [nota_base, "MesaVIP com quatro cadeiras/atores sentados; Djanira vê de5m além da mesa, olhando Dinjer. Cinco atores, cinco móveis,13s de voz original. LONGE→PERTO é preservado com corte declarado8s, pois o original não dá o instante do corte.",
                          "Layout/modelos/IDs da mesa são idênticos emEP12_C02; mesma marcaBOATE_DENTRO real conserva a continuidade. Interior/acesso/espaço são recursos pendentes; fachada com portas abertas só como alternativa explicitamente registrada na nota original, nunca alegada interior confirmado."]
        cena["elenco"][0]["marca"] = {"direita": 1.1, "frente": 0, "altura": 0}
        cena["poses"] = [_pose("DJANIRA_RICA", "CADEIRA_CHEFE", "sentada_cadeira_f")]
        return cena, [nota_base, "Djanira sozinha, sentada na posição do Chefe à mesma mesaVIP, com modelos/IDs/transforms relativos idênticos aos daEP06_C03.6s originais preservados, câmeraLONGE; cadeira não substitui a mesa. Mesmo marcadorBOATE_DENTRO e acesso interno real ainda precisam ser conferidos."]
    if ident == "EP14_C04":
        if actions != ["**Ação:** Dinjer cai de joelhos."] or [x["duracaoMs"] for x in plano["roteiro"]] != [7000, 7000, 9000, 2000, 4000]:
            raise ErroPiloto("EP14_C04: ação/tempos originais mudaram; revisar joelhos e fala.")
        recurso = next((x for x in pesquisa["animacoes"] if x["codigo"] == "ajoelhar_entrada_f"), None)
        if recurso is None or (recurso["dicionario"], recurso["animacao"]) != ("missheist_jewel", "manageress_kneel_intro"):
            raise ErroPiloto("EP14_C04 exige o clipe real de entrada de joelhos da fonte conferida.")
        cena = _base(plano)
        cena["roteiro"].insert(0, {"tipo": "pausa", "duracaoSegundos": 8, "plano": "DOIS", "personagem": "DINJER"})
        cena.update(encenacao="serie_v1", direcao=[{"tipo": "clipe", "personagem": "DINJER",
            "inicioSegundos": 0, "duracaoSegundos": 8, "recurso": "ajoelhar_entrada_f", "loop": False,
            "corpoInteiro": True, "manterPoseFinal": True}])
        return cena, ["Prefixo explícito8s sem voz/legenda executa a entrada real de joelhos; pose final primária mantida durante as falas upper-body. Backend confere duração real do clipe antes da ação; jamais truncar a entrada para caber.",
                      "As cinco falas originais somam29s e continuam integrais após o prefixo: conteúdo37s e capturaF740s. Estimativa original28s preservada como conflito; sem fala inventada. Semântica da pose, contato com chão e gestos sobre joelhos aguardam teste no jogo."]
    if ident == "EP14_C03":
        if actions or [x["duracaoMs"] for x in plano["roteiro"]] != [10000, 2000, 4000, 2000]:
            raise ErroPiloto("EP14_C03 mudou: revisar três falas e silêncio original4s.")
        return _base(plano), ["4s de silêncio original após fala2; câmera PERTO em Dinjer, sem boca/legenda. Reserva estimada6s conserva quadro; não cria fala."]
    if ident == "VINHETA_01":
        if actions != ["**Ação:** Os quatro dançando, um de cada vez, 5 segundos cada. Danças diferentes por personagem."]:
            raise ErroPiloto("Vinheta mudou; revisar dança/créditos.")
        resources = {x["codigo"]: x for x in pesquisa["animacoes"]}
        if any(x not in resources for x in DANCAS):
            raise ErroPiloto("Quatro clipes reais da vinheta não estão documentados na pesquisa.")
        cena = _base(plano)
        for ator, direita in zip(cena["elenco"], (-9, -3, 3, 9)):
            ator["marca"].update(direita=direita)
            ator["olharPara"] = None
        cena["roteiro"] = [{"tipo": "pausa", "duracaoSegundos": 5, "plano": "MEDIA", "personagem": a["personagem"]} for a in cena["elenco"]]
        cena.update(encenacao="serie_v1", direcao=[{"tipo": "clipe", "personagem": a["personagem"],
                    "inicioSegundos": i * 5, "duracaoSegundos": 5, "recurso": DANCAS[i], "loop": True, "corpoInteiro": True}
                    for i, a in enumerate(cena["elenco"])])
        return cena, ["Quatro danças/clipes documentados de5s com planosMEDIA individuais; sem fala/legenda. Intenção estética dos estilos continua candidata, sujeita ao teste.",
                      "Marcas relativas -9/-3/+3/+9m afastam os outros atores do quadro; exige18m de praia plana real na marca, enquadramento16:9 e streaming conferidos.",
                      "Os oito créditos originais entram no CapCut; vinheta reutilizada nos15episódios, nenhuma dança/MP4 gravado ainda."]
    return None, []


def gerar_xml_selecao(cenas: list[dict], roteiro_sha256: str) -> bytes:
    """Índice de navegação dos95IDs, incluindo cenas ainda sem runtime íntegro."""
    if len(cenas) != 95 or len({x["id"] for x in cenas}) != 95:
        raise ErroPiloto("Seleção requer os95IDs únicos da fonte completa.")
    if len(roteiro_sha256) != 64 or any(x not in "0123456789ABCDEF" for x in roteiro_sha256):
        raise ErroPiloto("Seleção requer SHA256 real do roteiro.")
    root = ET.Element("Temporada", versao="1", roteiroSha256=roteiro_sha256)
    for episode in range(1, 16):
        planos = [x for x in cenas if x["episodio"] == episode]
        if not planos or [x["numero"] for x in planos] != list(range(1, len(planos) + 1)):
            raise ErroPiloto(f"EP{episode:02d}: seleção deve conservar a ordem numerada integral.")
        parent = ET.SubElement(root, "Episodio", numero=str(episode), total=str(len(planos)))
        for scene in planos:
            filename = f"EP{episode:02d}/{scene['runtimeRascunho']}" if scene["runtimeRascunho"] else ""
            ET.SubElement(parent, "Cena", id=scene["id"], numero=str(scene["numero"]),
                          nome=scene["original"].get("nome", scene.get("nome", scene["id"])),
                          arquivo=filename, sha256=scene["runtimeSHA256"] or "")
    shared = [x for x in cenas if x["episodio"] == 0]
    if len(shared) != 1 or shared[0]["id"] != "VINHETA_01":
        raise ErroPiloto("Seleção compartilhada deve conter somente VINHETA_01.")
    scene = shared[0]
    ET.SubElement(root, "Compartilhada", id="VINHETA_01", numero="0", nome=scene.get("nome", "VINHETA"),
                  arquivo="compartilhados/VINHETA_01.json" if scene["runtimeRascunho"] else "",
                  sha256=scene["runtimeSHA256"] or "")
    ET.indent(root, space="  ")
    return ET.tostring(root, encoding="utf-8", xml_declaration=True) + b"\n"


def compilar_temporada(raiz: Path = RAIZ) -> dict:
    raiz = exigir_sem_vinculos(raiz)
    serie, pesquisa = _serie(raiz), _pesquisa(raiz)
    coverage = {x["id"]: x for x in pesquisa["coberturaRoteiro"]}
    if set(coverage) != {p["id"] for p in serie["planos"]}:
        raise ErroPiloto("Pesquisa não cobre os95IDs exatos.")
    candidates = {x["codigo"]: x for x in pesquisa["animacoes"]}
    cenas, files, recursos = [], {}, {}
    for plano in serie["planos"]:
        ident, researched = plano["id"], coverage[plano["id"]]
        if researched["origem"]["sha256"] != plano["origem"]["sha256Arquivo"]:
            raise ErroPiloto(f"{ident}: SHA de origem da pesquisa diverge.")
        if [x["texto"] for x in researched["acoesLiterais"]] != [x["raw"] for x in plano["acoesOriginais"]]:
            raise ErroPiloto(f"{ident}: pesquisa alterou ação original.")
        scene, notas = construir_rascunho(plano, pesquisa, raiz)
        elenco_runtime = [x["personagem"] for x in scene["elenco"]] if scene is not None else plano["adaptacao"]["elencoEfetivo"]
        pendentes = [{"codigo": "visual:" + code, "tipo": "recurso_real", "descricao": "XML/modelo/hash real registrado; confirmação visual pendente, sem nova geração de figurino."}
                     for code in elenco_runtime]
        locais_runtime = ["CANAL_LS"] if ident == "EP01_C01" and scene is not None else plano["original"]["locaisCodigos"]
        pendentes += [{"codigo": "marca:" + code, "tipo": "recurso_real", "descricao": "F3 real na locação/subponto indicado; coordenadas não são inventadas nem copiadas de outro local."} for code in locais_runtime]
        if ident == "EP01_C01" and scene is not None:
            pendentes.append({"codigo": "ensaio:queda_CANAL_LS", "tipo": "teste_no_jogo", "descricao": "Remontagem autorizada20s sem rampa/fuga: confirmar ensaio técnico separado, voo contínuo, saída física, invencibilidade e impacto na água. Somente CANAL_LS é requerida; compilação não confirma física ou aparência."})
        if ident == "EP01_C04":
            pendentes.append({"codigo": "passagem:HOSPITAL_SANDY", "tipo": "recurso_real", "descricao": "Limiar real aberto apontando para dentro; raycasts/colisão e travessia da avó precisam passar no GTA. Porta fechada impede montagem; recurso não confirmado por existir JSON."})
        if scene is None:
            for cap in researched["capacidades"]:
                pendentes.append({"codigo": "capacidade:" + cap, "tipo": "atuacao_integral",
                                  "descricao": MOTIVOS.get(cap, "Mapear integralmente a intenção original por recurso/controlador real, sem substituir por pose parada."),
                                  "candidatos": [x for x in researched["candidatos"] if candidates[x]["categoria"] == cap]})
            pendentes.append({"codigo": "adaptador:" + ident, "tipo": "atuacao_integral", "descricao": "Sequência inteira ainda sem adaptador; recursos pesquisados isolados não produzem a cena completa."})
        doc = {"versao": 1, "tipoDocumento": "compilacao_cena", "id": ident, "episodio": plano["episodio"], "numero": plano["numero"], "nome": plano["nome"],
               "estado": "rascunho_integral_pendente_recursos_e_teste" if scene is not None else "pendente_atuacao_integral",
               "pronto": False, "gravado": False, "origem": plano["origem"], "original": plano["original"], "adaptacao": plano["adaptacao"],
               "roteiroOriginal": plano["roteiro"], "temposOriginais": plano["tempos"], "acoesOriginais": plano["acoesOriginais"],
               "notasOriginais": plano["notasOriginais"], "cartelas": plano["cartelas"], "reaproveitamentos": plano["reaproveitamentos"],
               "mapeamentoDeclarado": notas, "pendencias": pendentes,
               "candidatosDocumentados": [deepcopy(candidates[x]) for x in researched["candidatos"]],
               "elencoRuntime": elenco_runtime,
               "elencoAdicionalDaAcao": [x for x in elenco_runtime if x not in plano["adaptacao"]["elencoEfetivo"]],
               "runtimeRascunho": ident + ".json" if scene is not None else None,
               "runtimeSHA256": _hash(_json(scene)) if scene is not None else None,
               "limite": "Rascunhos permanecem na produção do workspace. Catálogo/marca/streaming/chão/semântica visual reais precisam ser conferidos; não publicar substitutos de ações."}
        if scene is not None:
            content_ms = sum(int(Decimal(str(x["duracaoSegundos"])) * 1000) for x in scene["roteiro"])
            doc["temposRascunho"] = {"conteudoMs": content_ms, "contagemMs": 3000, "capturaDesdeF7Ms": content_ms + 3000,
                                    "blocosOriginaisMs": plano["tempos"]["blocosDeclaradosMs"],
                                    "tempoSemVozAdicionalMs": content_ms - plano["tempos"]["blocosDeclaradosMs"]}
            doc["objetosRuntime"] = deepcopy(scene.get("objetos", []))
            doc["posesRuntime"] = deepcopy(scene.get("poses", []))
        if ident == "EP01_C05":
            subtomadas = construir_subtomadas_ep01(raiz)
            doc["subtomadasPreparadas"] = [{"parte": part, "arquivo": path, "sha256": _hash(subtomadas[path])}
                for part in ("hospital", "cassino") for path in [f"producao/subtomadas/EP01_C05/{part}/EP01_C05.json"]]
            doc["limiteSubtomadas"] = "Duas capturas reais separadas do mesmoID,11s cada; editar3s hospital+5s cassino com voz original8s uma só. Não éMP4 final único montado nem interior confirmado."
        folder = f"EP{plano['episodio']:02d}" if plano["episodio"] else "compartilhados"
        files[f"{folder}/{ident}.execucao.json"] = _json(doc)
        files[f"{folder}/{ident}.base.srt"] = gerar_srt(plano).encode("utf-8")
        if scene is not None:
            files[f"{folder}/{ident}.json"] = _json(scene)
            from preparar_piloto import gerar_srt_piloto
            files[f"{folder}/{ident}.base.srt"] = gerar_srt_piloto(scene).encode("utf-8")
        for p in pendentes:
            resource = recursos.setdefault(p["codigo"], {"codigo": p["codigo"], "tipo": p["tipo"], "descricao": p["descricao"], "cenas": []})
            resource["cenas"].append(ident)
        cenas.append(doc)
    files["selecao.xml"] = gerar_xml_selecao(cenas, serie["fontes"]["roteiro"]["sha256"])
    roadmap = plano_implementacao(cenas, pesquisa)
    files["PLANO_IMPLEMENTACAO_95.json"] = _json(roadmap)
    files["apoios_propostos.json"] = _json(apoios_candidatos(pesquisa))
    roadmap_text = ["# Recursos para completar as95 cenas", "", roadmap["aviso"], "",
        f"{len(roadmap['rascunhosIntegrais'])}rascunhos integrais e {len(roadmap['cenasPendentes'])}cenas com atuação ainda por completar. Todos aguardam recursos físicos/visuais e confirmação no jogo.", "",
        "A ordem técnica proposta começa com interpretação, apoios/poses e objetos/mãos; rotas/portas, figurantes, pares, veículos/voo e montagem completam as dependências restantes. Recursos sobrepostos exigem o conjunto inteiro por cena.", ""]
    for group in roadmap["grupos"]:
        roadmap_text += [f"## {group['codigo']}", "", group["contratoProposto"], "",
            f"Incidência: {group['incidencia']}cenas pendentes. IDs: {', '.join(group['cenasPendentesAfetadas'])}.", "",
            "Cenas sem outros grupos na pesquisa: " + (", ".join(group["cenasComSoEsteGrupo"]) or "nenhuma") + ".", ""]
    roadmap_text += ["## Critério de conclusão", "", "Cada um dos95 descritores preserva ações literais, notas, vozes, tempos, linhas e SHA da origem. O JSON do plano contém todas as famílias necessárias por ID; nenhum adaptador elimina uma ação por falta de recurso. Props e apoios propostos têm fonte real, mas só medições no jogo poderão torná-los pontos físicos de encenação.", ""]
    files["PLANO_IMPLEMENTACAO_95.md"] = ("\n".join(roadmap_text) + "\n").encode("utf-8")
    resumo = {"versao": 1, "tipoDocumento": "indice_compilacao", "fonte": serie["fontes"],
              "pesquisaSHA256": pesquisa["sha256ArquivoPesquisa"], "totalCenas": 95,
              "episodios": [{"numero": e["episodio"], "ordemOriginal": e["ordemCenas"], "montagemEditorial": e["montagemEditorial"]} for e in serie["episodios"]],
              "vinheta": "VINHETA_01", "rascunhosIntegrais": [x["id"] for x in cenas if x["runtimeRascunho"]],
              "pendentesAtuacao": [x["id"] for x in cenas if not x["runtimeRascunho"]], "recursosFaltantes": list(recursos.values()),
              "estado": "preparacao;teste_no_jogo_pendente", "pronto": False, "gravado": False,
              "arquivos": [{"arquivo": name, "sha256": _hash(raw)} for name, raw in sorted(files.items())]}
    files["indice.json"] = _json(resumo)
    stream = io.StringIO(newline="")
    writer = csv.writer(stream)
    writer.writerow(("ID", "Estado", "RecursoFaltante", "Tipo", "Descricao", "RuntimeRascunho"))
    for doc in cenas:
        for pending in doc["pendencias"]:
            writer.writerow((doc["id"], doc["estado"], pending["codigo"], pending["tipo"], pending["descricao"], doc["runtimeRascunho"] or ""))
    files["recursos_faltantes.csv"] = stream.getvalue().encode("utf-8")
    guide = ["# Compilação da temporada", "", f"95descritores; {len(resumo['rascunhosIntegrais'])}rascunhos integrais com atuação representada; {len(resumo['pendentesAtuacao'])}pendentes de adaptador integral.", "",
             "Nenhuma cena é declarada pronta/gravada. Saídas ficam exclusivamente no workspace. OsJSON que existem representam atuação inteira, sem substituir ação por ator parado; pesquisa/compilação não prova semântica visual dos clipes candidatos.", "",
             "Rascunhos: " + ", ".join(resumo["rascunhosIntegrais"]), "",
             "Falhas de origem/hash/recurso rejeitam geração. Publicação no perfil fica para o Diretor após validação real. Queda/voo/água, veículos/props/apoios/portas/interiores e atuação pareada permanecem explícitos nos95descritores e CSV.", ""]
    for resource in resumo["recursosFaltantes"]:
        guide += [f"- `{resource['codigo']}`: {resource['descricao']} Cenas: {', '.join(resource['cenas'])}."]
    files["RECURSOS_FALTANTES.md"] = ("\n".join(guide) + "\n").encode("utf-8")
    return {"indice": resumo, "cenas": cenas, "arquivos": files}


def preparar(raiz: Path = RAIZ, conferir: bool = False) -> dict:
    raiz = exigir_sem_vinculos(raiz)
    compiled = compilar_temporada(raiz)
    subtomadas = construir_subtomadas_ep01(raiz)
    out = _confinado(raiz / "producao/cenas_serie", raiz)
    if conferir:
        for name, raw in compiled["arquivos"].items():
            path = _confinado(out / name, out)
            if not path.is_file() or path.read_bytes() != raw:
                raise ErroPiloto(f"Derivado ausente/divergente: {path}; edição preservada.")
        for name, raw in subtomadas.items():
            path = _confinado(raiz / name, raiz)
            if not path.is_file() or path.read_bytes() != raw:
                raise ErroPiloto(f"Subtomada ausente/divergente: {path}; edição preservada.")
        return compiled["indice"]
    novos, _ = _preparar_destinos(compiled["arquivos"], out)
    subnovos, _ = _preparar_destinos(subtomadas, raiz)
    for path, raw in novos:
        _adicionar(path, raw, out)
    for path, raw in subnovos:
        _adicionar(path, raw, raiz)
    return compiled["indice"]


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Compilar95cenas: só adaptadores integrais em rascunhos, recursos faltantes explícitos.")
    parser.add_argument("--conferir", action="store_true", help="conferir os derivados existentes por conteúdo/hash, sem sobrescrever")
    args = parser.parse_args(argv)
    try:
        result = preparar(conferir=args.conferir)
        print(f"95cenas conferidas; {len(result['rascunhosIntegrais'])}rascunhos integrais; {len(result['pendentesAtuacao'])}pendentes de atuação. Sem publicação/alegação de teste no GTA.")
        return 0
    except (ErroPiloto, ErroRuntime, ValueError, KeyError, OSError) as error:
        print(f"Não foi possível preparar a temporada: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
