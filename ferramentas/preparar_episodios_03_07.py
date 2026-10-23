"""Preparo seletivo de EP03–EP07; não publica runtime nem altera o compilador geral.

Vozes vêm dos planos conferidos do roteiro. Só os adaptadores integrais abaixo
produzem JSON; o inventário separa capacidades ainda sem controle físico.
"""
from __future__ import annotations

import argparse
from copy import deepcopy
from datetime import datetime, timezone
from decimal import Decimal
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import uuid

from preparar_piloto import RAIZ, ErroPiloto, _json, _serie
from preparar_cenas_serie import _base, _pesquisa, _prop, construir_rascunho
from exportar_edicao_ep01 import verificar_fidelidade
from runtime_diretor import exigir_sem_vinculos
from validar_json import validar_cena_json

EXISTENTES = {"EP03_C06", "EP04_C04", "EP04_C05", "EP05_C05", "EP06_C03"}
NOVOS = {f"EP{episode:02d}_C{number:02d}" for episode, count in ((3, 7), (4, 6), (5, 5), (6, 6), (7, 5))
         for number in range(1, count + 1)} - EXISTENTES


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest().upper()


def segundos(ms: int):
    return ms // 1000 if ms % 1000 == 0 else ms / 1000


def ms(value) -> int:
    return int(Decimal(str(value)) * 1000)


def duracao(cena: dict) -> int:
    return sum(ms(b["duracaoSegundos"]) for b in cena["roteiro"])


def marca(direita=0, frente=0, altura=0):
    return {"direita": direita, "frente": frente, "altura": altura}


def andar(ator, inicio, tempo, direita, frente, velocidade=1, recurso=None):
    action = {"tipo": "andar", "personagem": ator, "inicioSegundos": inicio,
              "duracaoSegundos": tempo, "destino": marca(direita, frente), "velocidade": velocidade}
    if recurso:
        action["recurso"] = recurso
    return action


def clipe(ator, recurso, inicio, tempo, loop=True, inteiro=True, manter=False):
    return {"tipo": "clipe", "personagem": ator, "inicioSegundos": inicio,
            "duracaoSegundos": tempo, "recurso": recurso, "loop": loop,
            "corpoInteiro": inteiro, "manterPoseFinal": manter}


def expressao(ator, inicio, tempo, facial=None, olhar=None):
    item = {"personagem": ator, "inicioSegundos": inicio, "duracaoSegundos": tempo}
    if facial:
        item["expressao"] = facial
    if olhar:
        item["olhar"] = olhar
    return item


def pausa(tempo, plano="LONGE"):
    return {"tipo": "pausa", "duracaoSegundos": tempo, "plano": plano}


def camera_programada(cena, cortes, final=None):
    cena["camera"] = {"modo": "programado", "planoInicial": cortes[0][1],
        "personagem": cortes[0][2], "planoFinal": final or cortes[-1][1],
        "cortes": [{"inicioSegundos": t, "plano": p, "personagem": a} for t, p, a in cortes]}
    for item in cena["roteiro"]:
        item.pop("plano", None)


def objeto_na_mao(ident, recurso, ator, mao="direita"):
    item = _prop(ident, recurso, 0, 0)
    item["assentarNoChao"] = False
    item["attachmentInicial"] = {"personagem": ator, "mao": mao,
        "offset": {"x": 0, "y": 0, "z": 0}, "rotacao": {"x": 0, "y": 0, "z": 0}}
    return item


def porta(ator, tipo, inicio, tempo, destino=None):
    item = {"personagem": ator, "tipo": tipo, "inicioSegundos": inicio, "duracaoSegundos": tempo}
    if tipo == "bater":
        item.update(batidas=3, recurso="bater_porta")
    if destino is not None:
        item.update(destino=marca(*destino), velocidade=1)
    return item


def construir(plano: dict, pesquisa: dict, raiz: Path = RAIZ, parte: str = "principal"):
    ident = plano["id"]
    if parte != "principal" and not (ident == "EP05_C03" and parte == "dinjer" or ident == "EP07_C05" and parte == "empurrao"):
        raise ErroPiloto("Subtomada não autorizada para este ID.")
    if ident in EXISTENTES:
        # Adaptadores canônicos anteriores permanecem intactos por padrão.
        cena, notas = construir_rascunho(plano, pesquisa, raiz)
        if ident == "EP04_C05":
            # Calibracao real76: apoio da cama e encosto atras de Djanira.
            cena["ambiente"].update(local="CASA_VO_INTERIOR", distanciaFrente=1)
            for actor in cena["elenco"]:
                actor["marca"] = marca(-1.1 if actor["personagem"] == "DONA_IRACI" else 1.1, -1)
            cena["camera"]["inverterEixo"] = True
            for item in cena["objetos"]:
                item["marca"]["frente"] = -1
                if item["id"] == "CAMA_IRACI":
                    item["recurso"] = "cama_quarto"
                else:
                    item["rotacao"] = -90
            for pose in cena["poses"]:
                pose["rotacao"] = 180
            notas.append("Calibracao tecnica real0.9.76 da CASA_VO_INTERIOR: cama_quarto/v_res_msonbed, atores e apoios frente-1, cadeira rotacao-90 e ambas poses180; camera inverterEixo=true. Encosto fica atras de Djanira e cabeca de Iraci no travesseiro. Duas falas7s e figurinos originais preservados; adequacao visual da nova tomada exige revisao dos quadros, sem aprovar gosto.")
        if ident == "EP05_C05":
            for actor in cena["elenco"]:
                actor["marca"] = marca(-.35 if actor["personagem"] == "DONA_IRACI" else .35, 0)
            notas.append("Marcas próximas±0,35m no chão real daCASA_VO já usado noEP02; conserva todas cinco vozes31s e gesto original, sem alterar o XML.")
        return cena, notas
    if ident not in NOVOS:
        return None, ["A ação original ainda exige controle físico específico; nenhum substituto estacionário é emitido."]
    cena = _base(plano)
    cena.update(encenacao="serie_v1", direcao=[])
    cena["ambiente"]["distanciaFrente"] = 1
    if ident == "EP07_C05" and parte == "empurrao":
        cena.update(empurraoAviao=True, roteiro=[pausa(5, "PERTO")])
        cena["ambiente"]["local"] = "CANAL_LS"
        cena["elenco"] = [{"personagem": actor, "marca": marca(x, 0), "olharPara": None}
            for actor, x in (("DJANIRA_POBRE", -.7), ("O_CHEFE", 0), ("DINJER", .7))]
        cena["camera"].update(planoInicial="PERTO", personagem="O_CHEFE", planoNarracao="PERTO", planoFinal="PERTO")
        return cena, ["Inserto silencioso5s, mesmoID/modelovelum/altura35/velocidade35 sobre marca realCANAL_LS. Chefepiloto/Djanira0/Dinjer1; clipe literalshove61481 secundário preserva pilotagem. Getters exigem mão avançar≥6cm em direção ao ombro real e aproximar≤35cm durante fase ativa, além do gesto completo. Câmera própria enquadra mão/ombro; semLeaveVehicle neste inserto. Recortar contato útil~2s e depois usar queda realEP01 ou bruto principalEP07_C05; todas13s de voz original ficam no material principal, uma vez na montagem. Contato/adequação visual ainda precisam teste real."]
    if ident == "EP03_C01":
        cena["roteiro"].append(pausa(3, "LONGE"))
        cena["elenco"][0].update(marca=marca(0, -2), olharPara=None)
        cena["direcao"] = [andar("DJANIRA_POBRE", 0, 3, 0, 0, .8),
            clipe("DJANIRA_POBRE", "acenar_f", 3, 4, False, False, True),
            andar("DJANIRA_POBRE", 7, 3, 0, 2, .8)]
        cena["veiculos"] = [{"id": "CARRO_PASSAGEM_" + str(i), "recurso": resource,
            "marca": marca(x, -15), "rotacao": 0, "motoristaExtra": True, "trajetoDireto": True,
            "acoes": [{"tipo": "dirigir", "personagem": None, "inicioSegundos": 0,
                "duracaoSegundos": 10, "rota": [marca(x, 15)], "velocidade": 5,
                "raioChegada": 2.5, "raioParada": 1}]} for i, resource, x in ((1, "TAILGATER", 4), (2, "SUPERD", 7.5))]
        return cena, ["Caminha2m, acena com o clipe feminino real completo em até4s e retoma2m. Dois carros próprios percorrem30m nas faixas laterais com motoristas internos; não depender de população apagada. Narração7s+reserva3s=10s, contagem3s separada; chão/rota/tráfego/aceno exigem getters e imagem."]
    if ident in {"EP03_C02", "EP03_C04"}:
        cena["elenco"][0].update(marca=marca(), olharPara=None)
        cena["portas"] = [porta("DJANIRA_POBRE", "bater", 0, 5 if ident == "EP03_C02" else 7)]
        if ident == "EP03_C04":
            cena["interpretacao"] = [expressao("DJANIRA_POBRE", 0, 9, "estressado")]
        return cena, ["Três aproximações/contatos/recuos reais da mão ao painel identificado; clipe instalado76544. Restante do bloco é espera, sem atender. Porta real na frente e orientação/alcance conferidos, nenhuma parede é tratada como porta. Voz original" + ("6s" if ident == "EP03_C02" else "9s") + " sem boca preservada.",
            "Noite requer iluminação visível da casa; música/televisão citadas em voz são faixas externas, não áudio inventado nem interior declarado aberto."]
    if ident == "EP03_C03":
        cena["elenco"][0].update(marca=marca(), olharPara=None)
        cena["poses"] = [{"personagem": "DJANIRA_POBRE", "objeto": None,
            "recurso": "sentada_meio_fio_f", "tipo": "chao"}]
        return cena, ["Pose feminina real de meio-fio em superfície/degrau real da marca, sem banco substituto. Corpo/pés e desnível precisam ser medidos pelo helper de poses; narração8s intacta. Televisão citada exige áudio externo, sem acionar boca."]
    if ident == "EP03_C05":
        for actor in cena["elenco"]:
            actor.update(marca=marca(0, .8) if actor["personagem"] == "DINJER" else marca(2, -4), olharPara="DINJER" if actor["personagem"] != "DINJER" else None)
            if actor["personagem"] == "DINJER":
                actor["rotacao"] = 180
        cena["objetos"] = [objeto_na_mao("SAPATO_DINJER_D", "sapato_mao", "DINJER"),
            objeto_na_mao("SAPATO_DINJER_E", "sapato_mao", "DINJER", "esquerda")]
        cena["portas"] = [porta("DINJER", "abrir", 0, 2), porta("DINJER", "atravessar", 2, 3, (0, -2))]
        cena["direcao"] = [andar("DINJER", 5, 7, 0, -9, 2)]
        return cena, ["Subponto fundos real daCASA_DINJER orientado para dentro: abrir painel real0–2s, cruzar passagem2–5s, correr7m5–12s. Djanira permanece na rua acompanhando com olhar; nenhum teleporte na ação. Dois sapatos próprios nas mãos; helper transitório35/0 fonteqb-radialmenu e getters retira/restaura só componente6 daDinjer sem modificar13XMLs. Narração12s intacta, rotação180 daDinjer enfrenta o limiar por dentro."]
    if ident == "EP03_C07":
        cena["elenco"] = [{"personagem": a, "marca": marca(x, y), "olharPara": look,
            **({"rotacao": 180} if a == "DONA_IRACI" else {})} for a, x, y, look in (
                ("DONA_IRACI", .35, 1.2, "CAPANGA_1"), ("DJANIRA_POBRE", -.35, 1.2, "DONA_IRACI"),
                ("CAPANGA_1", 0, -.8, "DONA_IRACI"))]
        cena["roteiro"].insert(0, pausa(9, "LONGE"))
        cena["portas"] = [porta("CAPANGA_1", "bater", 0, 6), porta("DONA_IRACI", "abrir", 6, 1),
            porta("DONA_IRACI", "atravessar", 7, 2, (.35, -.2))]
        cena["interpretacao"] = [expressao("DJANIRA_POBRE", 0, 30, "chateado", {"tipo": "ator", "alvo": "DONA_IRACI"})]
        return cena, ["Bate três vezes6s; a avó abre fisicamente1s e atravessa2s; Djanira ouve do lado interno real. Prefixo silencioso9s+fala10/narração5/narrador6=30s úteis. Casa precisa de limiar real transitável, sem criar entrada pelo muro. CAPANGA_1 reutiliza seu XML permanente como dono dafazenda; conflito semterno original é declarado, sem roupa extra ou fala alterada."]
    if ident in {"EP04_C01", "EP06_C01"}:
        total = 12 if ident == "EP04_C01" else 11
        cena["elenco"][0].update(marca=marca(), olharPara=None)
        if ident == "EP04_C01":
            cena["objetos"] = [objeto_na_mao("PAPEL_DJANIRA", "papel", "DJANIRA_POBRE", "esquerda")]
            for a, x, begin, end in (("COLEGA_1", 3, -3, 3), ("COLEGA_2", 4.5, 3, -3)):
                cena["elenco"].append({"personagem": a, "marca": marca(x, begin), "olharPara": None})
                cena["direcao"].append(andar(a, 0, 12, x, end, 1.2))
        else:
            cena["interpretacao"] = [expressao("DJANIRA_POBRE", 5, 6, olhar={"tipo": "marca", "destino": marca(0, 8, 2)})]
        camera_programada(cena, [(0, "LONGE", "DJANIRA_POBRE"), (5, "LONGE" if total == 12 else "BAIXA", "DJANIRA_POBRE")])
        return cena, ["Wrapper ônibus próprio igualEP01_C06: assento real, saída até5s, caminhada2m com heading do destino após saída e chegada dentro da voz" + str(total) + "s. Ônibus à esquerda−2,8m libera lado da porta; sem warp durante ação. " + ("Dois colegasXML próprios circulam pelo terminal e papel físico fica na mão; estilo bemvestido precisa imagem, não população fingida." if total == 12 else "PlanoBAIXA após5s e olhar elevado relativo da marca mantêm edifícios reais na composição; traje permanente conservado.")]
    if ident == "EP04_C06":
        cena["elenco"][0].update(marca=marca(), olharPara=None)
        cena["objetos"] = [objeto_na_mao("CAIXA_REMEDIO", "caixa_remedio", "DJANIRA_POBRE")]
        cena["interpretacao"] = [expressao("DJANIRA_POBRE", 0, 16, "machucado", {"tipo": "objeto", "alvo": "CAIXA_REMEDIO"})]
        return cena, ["Caixa pequena física v_res_tt_pharm2 identificada pelo autorForge como caixa de papelão; candidata a embalagem de remédio sem fabricar comprimidos. Narração8+narrador8=16s e machucado facial/olhar para caixa conservados. Mesh/semântica de vazia dependem da imagem real, não do nome do modelo; não afirmar abertura ou conteúdo a partir do getter."]
    if ident == "EP05_C01":
        cena["elenco"][0].update(marca=marca(), olharPara=None)
        cena["direcao"] = [clipe("DJANIRA_POBRE", "trabalho_plantacao_f", 0, 12)]
        cena["interpretacao"] = [expressao("DJANIRA_POBRE", 0, 12, "chateado")]
        return cena, ["Trabalho manual no solo pelo mesmo clipe feminino de plantação já executado noEP01_C02, contínuo12s no sol. Adaptação técnica da capina como retirada manual de ervas: não trocar pá por enxada nem inventar ferramenta; semântica visual precisa avaliação real. Narração12s e dívida/dias/valores originais intactos."]
    if ident == "EP05_C03":
        speaker = "DINJER" if parte == "dinjer" else "DJANIRA_POBRE"
        cena["elenco"] = [{"personagem": speaker, "marca": marca(), "olharPara": None}]
        cena["ambiente"]["local"] = "AEROPORTO_TERMINAL" if speaker == "DINJER" else "ROCA_GRAPESEED"
        # A posição dos cinco blocos34s é a referência única, com silêncio no trecho da interlocutora.
        full = _base(plano)["roteiro"]
        cena["roteiro"] = [deepcopy(block) if block.get("personagem") == speaker else pausa(block["duracaoSegundos"], "MEDIA") for block in full]
        cena["objetos"] = [objeto_na_mao("CELULAR_LIGACAO", "celular", speaker)]
        body = clipe(speaker, "ligacao_f", 0, 34)
        body["preservarDuranteFala"] = True
        cena["direcao"] = [body]
        cena["camera"].update(personagem=speaker, planoInicial="MEDIA", planoFinal="MEDIA", planoFala="MEDIA")
        return cena, ["Duas subtomadas do mesmoID, cada34s, cada mulher em seu local real com telefone ao ouvido. Somente as próprias falas acionam boca; pausas7/4/10/5/8 preservam posições do diálogo completo na edição. Clipes ligação preservados durante fala viaControlsBody, sem gesto explicando substituir telefone. Vozes originais usadas uma vez no corte alternado, nunca colocar as duas na mesma marca."]
    if ident == "EP05_C04":
        cena["elenco"][0].update(marca=marca(), olharPara=None)
        cena["objetos"] = [_prop("ARMARIO_DJANIRA", "guarda_roupa", 0, 1.3)]
        cena["interpretacao"] = [expressao("DJANIRA_POBRE", 0, 8, "chateado", {"tipo": "objeto", "alvo": "ARMARIO_DJANIRA"})]
        return cena, ["Guarda-roupa físico real v_res_tre_wardrobe identificado pelo autorqb-houses; Djanira olha o centro medido8s, sem boca. Mesh/compartimento aberto/quase vazio ainda precisam teste visual: getters só confirmam modelo/colisão/olhar e não permitem afirmar portas abertas. Se o modelo for fechado, a tomada exige móvel aberto legítimo antes de ser considerada integral; nenhuma ocultação simula abertura. Texto8s intacto."]
    if ident == "EP06_C02":
        cena["elenco"] = [{"personagem": a, "marca": marca(x, y), "olharPara": None,
            **({"rotacao": 180} if a == "CAPANGA_1" else {})} for a, x, y in (
                ("DJANIRA_POBRE", 0, 0), ("CAPANGA_1", 0, 1.2), ("COLEGA_1", -.6, -.8), ("COLEGA_2", .6, -1.6))]
        cena["interpretacao"] = [expressao("DJANIRA_POBRE", 0, 7, "chateado"),
            expressao("CAPANGA_1", 0, 3, olhar={"tipo": "marca", "destino": marca(0, 0, 1.6)}),
            expressao("CAPANGA_1", 3, 4, olhar={"tipo": "marca", "destino": marca(0, 0, -.5)})]
        return cena, ["Fila própria com dois colegas e segurançaCAPANGA_1 preservandoXML. Segurança vê rosto3s e pés4s por alvos relativos medidos, Djanira chateada7s. Salvar câmera realEP06_C02 para reaplicarEXATA noEP12_C01; manter mesma marca/rotação/layout e comparar câmera pelo snapshot, não somente pelo nomeMEDIA. Figurantes adicionais vêm daação original, sem IDs novos."]
    if ident == "EP06_C04":
        cena["elenco"] = [{"personagem": a, "marca": marca(x, y), "olharPara": look} for a, x, y, look in (
            ("DINJER", -.35, 0, "DJANIRA_POBRE"), ("DJANIRA_POBRE", .35, -.3, "DINJER"), ("O_CHEFE", 1.1, 2, "DJANIRA_POBRE"))]
        cena["objetos"] = [_prop("CADEIRA_DINJER", "cadeira_simples", -.35, 0, -90),
            _prop("CADEIRA_CHEFE", "cadeira_simples", 1.1, 2, 180), _prop("MESA_CHEFE", "mesa", .4, 2)]
        cena["poses"] = [{"personagem": "DINJER", "objeto": "CADEIRA_DINJER", "recurso": "sentada_cadeira_livre_f", "tipo": "sentada"},
            {"personagem": "O_CHEFE", "objeto": "CADEIRA_CHEFE", "recurso": "sentado_cadeira_m", "tipo": "sentada"}]
        cena["transicoesPose"] = [{"personagem": "DINJER", "tipo": "levantar", "inicioSegundos": 0, "duracaoSegundos": 4, "recurso": "levantar_cadeira_livre_f"},
            {"personagem": "O_CHEFE", "tipo": "levantar", "inicioSegundos": 14, "duracaoSegundos": 4, "recurso": "levantar_cadeira_m"}]
        cena["roteiro"].insert(0, pausa(20, "LONGE"))
        cena["direcao"] = [andar("DINJER", 4, 3, -.35, -.3, 1.1),
            {"tipo": "virar", "personagem": "DINJER", "inicioSegundos": 7, "duracaoSegundos": 1, "alvo": "DJANIRA_POBRE"},
            {"tipo": "virar", "personagem": "DJANIRA_POBRE", "inicioSegundos": 7, "duracaoSegundos": 1, "alvo": "DINJER"},
            clipe("DINJER", "abraco_a", 8, 6, False, True, True), clipe("DJANIRA_POBRE", "abraco_b", 8, 6, False, True, True),
            andar("DINJER", 14, 4, -.35, 1, .9), andar("DJANIRA_POBRE", 14, 4, .35, 1, .9),
            {"tipo": "virar", "personagem": "O_CHEFE", "inicioSegundos": 18, "duracaoSegundos": 2, "alvo": "DJANIRA_POBRE"}]
        cena["interpretacao"] = [expressao("DINJER", 0, 66, "feliz")]
        return cena, ["Saída real da cadeira0–4, aproximação4–7, volta7–8, dois clipes de abraço8–14 e caminhada física até mesa14–18; Chefe levanta14–18 e volta18–20. Prefixo20s+sete falas46s=66s, contagem3s separada. Sem PlayAnimationAdvanced/reposição durante abraço; contato mãos/tronco/fases precisam guarda corporal e imagem. Poses de cadeira deixam de reaplicar após saída confirmada, boca/intenções originais conservadas."]
    if ident == "EP07_C02":
        cena["elenco"] = [{"personagem": a, "marca": marca(x, y), "olharPara": look} for a, x, y, look in (
            ("O_CHEFE", -1.1, 4, "CAPANGA_1"), ("CAPANGA_1", 1.1, 4, "O_CHEFE"),
            ("CAPANGA_2", 2.2, 4, "O_CHEFE"), ("DJANIRA_POBRE", 0, -1.2, None))]
        cena["objetos"] = [_prop("CONTAINER_CYPRESS", "container_serie", 0, 1.2, 90),
            _prop("CAIXAS_NEGOCIO_1", "caixas_serie", -2.2, 4.2), _prop("CAIXAS_NEGOCIO_2", "caixas_serie", 2.8, 4.2)]
        camera_programada(cena, [(0, "LONGE", "O_CHEFE"), (8, "PERTO", "DJANIRA_POBRE")])
        cena["interpretacao"] = [expressao("DJANIRA_POBRE", 0, 16, "estressado", {"tipo": "desvio"})]
        return cena, ["Container físico real e duas caixas próprias entre Djanira e os três homens. A boundingbox/colisão deve confirmar que ela fica fora do mesh e atrás do contêiner, não invisível ou dentro do prop. Luz dahora19:30 conservada; planoLONGE fala8s→PERTODjanira narra8s. Figurinos permanentes deChefe/capangas reutilizados e todas vozes16s intactas."]
    if ident == "EP07_C03":
        for actor, x in zip(cena["elenco"], (-2, 2, -3.5)):
            actor.update(marca=marca(x, -1), olharPara=None)
        cena["roteiro"].extend([pausa(30, "LONGE"), pausa(5, "LONGE")])
        cena["interpretacao"] = [expressao("DJANIRA_POBRE", 0, 45, "feliz"), expressao("DINJER", 0, 45, "chateado")]
        return cena, ["Os três embarcam por tarefas normais semWarpIn; Chefe piloto em−1, Djanira0, Dinjer1. Velum idênticoEP01_C01: pista relativa120m plana e volume sem colisor são sondados antes da ação; missão normal deve provar altura≥10m e velocidade≥20m/s. Narração10s+reserva35s=45s; não afirmar decolagem por native enviado ou mover Z para fazê-la. Traje igreja permanece conflito documentado com13XML fixos."]
    if ident == "EP07_C04":
        cena["ambiente"]["local"] = "CANAL_LS"
        for actor, x in zip(cena["elenco"], (-.7, .7, 0)):
            actor.update(marca=marca(x, 0), olharPara=None)
        cena["objetos"] = [objeto_na_mao("TACA_JANTAR", "taca", "O_CHEFE")]
        cena["interpretacao"] = [expressao("O_CHEFE", 0, 12, "feliz"), expressao("O_CHEFE", 12, 22, "normal"),
            expressao("DINJER", 30, 2, "chateado")]
        return cena, ["Voo real sobre âncoraCANAL_LS capturada, mesmavelum/altura35/velocidade35EP01; não fabrica marca aéreaAEROPORTO_AR. Adaptação autorizada: Chefe sentado no piloto−1, Djanira0 e Dinjer1 traseiro real, em vez de caminhar numa cabine sem piso navegável. Serviço secundário de taça doChefe paraDjanira exige clipes A/B completos≤6s e contato≤0,35m; se falhar aborta sem transferência por relógio. Seis falas32s+reservaoriginal2s=34s; naúltimafalaDinjer olha chão, Chefe para sorrir aos12s. Close próprio por falante mantém a física do voo e o modelo idêntico."]
    if ident == "EP07_C05":
        cena["ambiente"]["local"] = "CANAL_LS"
        cena["elenco"][0].update(marca=marca(), olharPara=None)
        cena["roteiro"].append(pausa(7, "OLHOS"))
        return cena, ["Queda física pelo mesmo backend/modelovelum/altura35/velocidade35 já gravado emEP01_C01, água realCANAL_LS e avião seguindo voo; originalmanda reaproveitar essa mesma queda. Narração6+narrador7=13s mais reserva7=20s; nenhum novo trajeto de rampa obrigatório. Inserto de mão/braço doChefe próximoombro/porta deve preceder a imagem reutilizada daqueda na edição; sair voluntariamente do assento não prova empurrão. Inserto e julgamento visual continuam dependências explícitas, não transferir ator para outra tomada durante ação."]
    if ident == "EP04_C02":
        offsets = {"DJANIRA_POBRE": (-1.1, 0), "DINJER": (1.1, -3),
                   "COLEGA_1": (2.4, -3), "COLEGA_2": (3.7, -3)}
        for ator in cena["elenco"]:
            code = ator["personagem"]
            ator["marca"] = marca(*offsets[code])
            ator["olharPara"] = "DJANIRA_POBRE" if code != "DJANIRA_POBRE" else "DINJER"
        cena["roteiro"].insert(0, pausa(4))
        cena["direcao"] = [andar(a, 0, 4, offsets[a][0], 0, 1)
            for a in ("DINJER", "COLEGA_1", "COLEGA_2")]
        cena["interpretacao"] = [expressao("DINJER", 0, 4, "estressado", {"tipo": "ator", "alvo": "DJANIRA_POBRE"}),
            expressao("DINJER", 4, 27, "convencido", {"tipo": "ator", "alvo": "DJANIRA_POBRE"})]
        for a in ("COLEGA_1", "COLEGA_2"):
            cena["interpretacao"].append(expressao(a, 0, 31, olhar={"tipo": "ator", "alvo": "DJANIRA_POBRE"}))
        cena["camera"].update(planoInicial="LONGE", planoNarracao="LONGE", planoFinal="DOIS")
        return cena, ["Chegada física de Dinjer e dois colegas em4s silenciosos, seguida das cinco falas originais5/5/4/9/4s:31s úteis+contagem3s. Colegas param e olham Djanira; Dinjer passa de estressada a convencida ao perceber a plateia. XMLs permanentes preservados; caminhada, faciais e câmera aguardam avaliação no GTA."]
    if ident == "EP04_C03":
        # Os colegas são citados na ação original, embora omitidos no metadado elenco.
        codes = ("DINJER", "DJANIRA_POBRE", "COLEGA_1", "COLEGA_2")
        starts = {"DINJER": (1.1, 0), "DJANIRA_POBRE": (-1.1, 0),
                  "COLEGA_1": (2.4, 0), "COLEGA_2": (3.7, 0)}
        cena["elenco"] = [{"personagem": a, "marca": marca(*starts[a]),
            "olharPara": "DINJER" if a == "DJANIRA_POBRE" else None} for a in codes]
        for a in ("DINJER", "COLEGA_1", "COLEGA_2"):
            cena["direcao"].extend([clipe(a, "rir_candidato", 0, 3),
                andar(a, 3, 6, starts[a][0], 5, 1.2, "andar_passarela_candidato" if a == "DINJER" else None)])
        cena["interpretacao"] = [expressao("DJANIRA_POBRE", 0, 9, "chateado", {"tipo": "ator", "alvo": "DINJER"})]
        for a in ("DINJER", "COLEGA_1", "COLEGA_2"):
            cena["interpretacao"].append(expressao(a, 0, 9, "feliz"))
        return cena, ["Riso corporal real dos três durante0–3s, seguido de saída física5m durante3–9s; Djanira permanece e acompanha Dinjer com o olhar. Montagem sequencial declarada conserva narração9s, sem sobrepor duas tarefas primárias ou acionar boca por voz em off. Dois colegas originais implícitos reutilizam seus XMLs; intensidade da risada e passarela precisam de teste visual."]
    if ident == "EP05_C02":
        phone = _prop("CELULAR_DJANIRA", "celular", 0, 0)
        phone["assentarNoChao"] = False
        phone["attachmentInicial"] = {"personagem": "DJANIRA_POBRE", "mao": "direita",
            "offset": {"x": 0, "y": 0, "z": 0}, "rotacao": {"x": 0, "y": 0, "z": 0}}
        cena["elenco"][0]["olharPara"] = None
        cena["objetos"] = [phone]
        cena["roteiro"].append(pausa(3, "MEDIA"))
        cena["direcao"] = [clipe("DJANIRA_POBRE", "celular_texto_f", 0, 2),
            clipe("DJANIRA_POBRE", "ligacao_entrada_f", 2, 3, False, True, True),
            clipe("DJANIRA_POBRE", "ligacao_f", 5, 3)]
        cena["interpretacao"] = [expressao("DJANIRA_POBRE", 0, 2, "estressado",
            {"tipo": "objeto", "alvo": "CELULAR_DJANIRA"})]
        return cena, ["Celular próprio no ossoPHRightHand já usado na0.9.64; olha2s, atende com clipe completo em até3s e ouve3s. Narração5s original+reserva silenciosa3s=8s úteis. GET_ANIM_DURATION recusa entrada maior que3s em vez de truncar. Toque deve ser faixa externa sincronizada à ação; este JSON não inventa áudio nem torna celular de texto uma conversa sem gesto."]
    if ident == "EP06_C05":
        cena["elenco"][0].update(marca=marca(0, 2), olharPara=None)
        cena["direcao"] = [andar("DJANIRA_POBRE", 0, 8, 0, -2, 0.8)]
        cena["interpretacao"] = [expressao("DJANIRA_POBRE", 0, 8, "feliz")]
        return cena, ["MarcaBOATE_FORA no limiar realmente aberto e orientada para dentro: Djanira inicia2m dentro e sai4m andando, sorrindo, em8s. Chão/colisão/chegada são medidos; passagem fechada precisa interromper, sem teleporte durante ação. Esta fonte não afirma acesso por porta fechada nem substitui saída por personagem parado. Narração8s preservada."]
    if ident == "EP06_C06":
        cena["direcao"] = [clipe("O_CHEFE", "ameaca_pescoco_m", 0, 4, False, True, True)]
        cena["interpretacao"] = [expressao("O_CHEFE", 0, 4, olhar={"tipo": "ator", "alvo": "DINJER"}),
            expressao("DINJER", 4, 3, "chateado", {"tipo": "chao"})]
        camera_programada(cena, [(0, "PERTO", "O_CHEFE"), (4, "PERTO", "DINJER")])
        return cena, ["Gesto masculino realcut_throat completo0–4s; corte4s para Dinjer olhando fisicamente para o chão até7s, sem falas. Narrador original7s em off; getters de duração/fase impedem cortar o gesto. Inclinação visível da cabeça e leitura dramática aguardam teste no GTA."]
    if ident == "EP07_C01":
        cena["elenco"][0].update(marca=marca(0, -4), olharPara=None)
        cena["direcao"] = [andar("DJANIRA_POBRE", 0, 9, 0, 0, 0.8)]
        cena["interpretacao"] = [expressao("DJANIRA_POBRE", 0, 9, "feliz")]
        return cena, ["Chegada real4m ao hangar com expressãofeliz durante narração9s; marcaAEROPORTO_HANGAR e rota seca acessível conferidas na montagem. FigurinoDJANIRA_POBRE permanente é reutilizado; roupa de igreja na origem permanece documentada como conflito, sem modificar o XML."]
    raise AssertionError(ident)


def verificar_voz(cena, plano, parte):
    if plano["id"] == "EP07_C05" and parte == "empurrao":
        if cena.get("empurraoAviao") is not True or duracao(cena) != 5000 or len(cena["roteiro"]) != 1 or cena["roteiro"][0]["tipo"] != "pausa":
            raise ErroPiloto("Inserto de empurrão deve conter somente5s silenciosos, sem duplicar/mudar as vozes originais.")
        return plano["roteiro"]
    if plano["id"] != "EP05_C03":
        return verificar_fidelidade(cena, plano)
    original = _base(plano)["roteiro"]
    actor = "DINJER" if parte == "dinjer" else "DJANIRA_POBRE"
    if len(original) != 5 or len(cena["roteiro"]) != 5 or duracao(cena) != 34000:
        raise ErroPiloto("EP05_C03: cinco posições originais34s obrigatórias em cada subtomada.")
    for actual, expected in zip(cena["roteiro"], original):
        if ms(actual["duracaoSegundos"]) != ms(expected["duracaoSegundos"]):
            raise ErroPiloto("EP05_C03: silêncio/fala mudou a posição da interlocutora.")
        if expected["personagem"] == actor:
            if any(actual.get(k) != expected[k] for k in ("tipo", "texto", "personagem", "gesto")):
                raise ErroPiloto("EP05_C03: fala própria difere da origem.")
        elif actual["tipo"] != "pausa" or any(k in actual for k in ("texto", "personagem", "gesto")):
            raise ErroPiloto("EP05_C03: voz da interlocutora deve ficar somente no outro bruto.")
    return plano["roteiro"]


def preparar(ids, raiz=RAIZ, catalogos=None, gravar=False):
    raiz = exigir_sem_vinculos(Path(raiz))
    catalogos = exigir_sem_vinculos(Path(catalogos or Path.home() / "GTA_Diretor/catalogos"))
    serie = _serie(raiz)
    planos = {p["id"]: p for p in serie["planos"] if 3 <= p["episodio"] <= 7}
    pesquisa = _pesquisa(raiz)
    requested = list(ids or sorted(NOVOS | EXISTENTES))
    if len(set(requested)) != len(requested) or any(i not in planos for i in requested):
        raise ErroPiloto("Seleção deve conter IDs distintos existentes de EP03–EP07.")
    outputs, rows = {}, []
    # Validar todo o lote numa pasta temporária antes de modificar uma fonte.
    with tempfile.TemporaryDirectory(prefix="diretor-ep03-07-") as temp:
        parts = [(ident, part) for ident in requested for part in ("principal", "dinjer", "empurrao")
            if part == "principal" or ident == "EP05_C03" and part == "dinjer" or ident == "EP07_C05" and part == "empurrao"]
        for ident, part in parts:
            cena, notas = construir(planos[ident], pesquisa, raiz, part)
            if cena is None:
                raise ErroPiloto(ident + ": atuação integral ainda não implementada.")
            verificar_voz(cena, planos[ident], part)
            raw = _json(cena)
            probe = Path(temp) / (ident + ".json")
            probe.write_bytes(raw)
            validar_cena_json(probe, catalogos)  # Lê decimais exatos; não normalizar os bytes da fonte.
            if probe.read_bytes() != raw:
                raise ErroPiloto(ident + ": arquivo mudou durante a validação.")
            relative = (Path("producao/subtomadas/EP07_C05/empurrao") / (ident + ".json") if part == "empurrao" else
                        Path("producao/subtomadas/EP05_C03/dinjer") / (ident + ".json") if part == "dinjer" else
                        Path("producao/cenas_serie") / f"EP{planos[ident]['episodio']:02d}" / (ident + ".json"))
            target = exigir_sem_vinculos(raiz / relative)
            outputs[target] = raw
            rows.append({"id": ident, "subtomada": part, "arquivo": relative.as_posix(), "sha256": sha(raw),
                "conteudoMs": duracao(cena), "contagemMs": 3000, "origem": planos[ident]["origem"],
                "adaptacao": notas, "estado": "fonte_validada;teste_visual_e_gravacao_pendentes"})
    original_phone = planos["EP05_C03"]["roteiro"]
    result = {"versao": 1, "cenas": rows, "escreveu": gravar,
        "roteiroSHA256": serie["fontes"]["roteiro"]["sha256"],
        "montagemTelefone": {"id": "EP05_C03", "duracaoMontadaMs": 34000,
            "cortarNaOrdemOriginal": [{"personagem": b["quemOriginal"], "inicioMs": b["inicioMs"], "fimMs": b["fimMs"],
                "textoOriginal": b["texto"], "duracaoMs": b["duracaoMs"], "subtomada": "dinjer" if b["quemOriginal"] == "DINJER" else "principal"} for b in original_phone],
            "vozUmaVez": True, "audioGerado": False, "videosGerados": False},
        "limitesVisuais": {"EP04_C06": "Mesh/semântica de caixa vazia ainda precisam imagem real.",
            "EP05_C04": "Compartimento de guarda-roupa aberto/quase vazio não foi comprovado pelo catálogo; avaliar modelo real antes de aceitar integral.",
            "EP07_C05": "Inserto silencioso separado tem controle físico+getter mão/ombro; contato real, adequação visual e corte para queda reaproveitada ainda pendentes. Nenhum LeaveVehicle é tratado como empurrão."}}
    if not gravar:
        return result
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-" + uuid.uuid4().hex[:8]
    backup = raiz / "backups/preparo-ep03-07" / stamp
    backup.mkdir(parents=True, exist_ok=False)
    previous = []
    # Backups byte a byte e SHA conferido antes da primeira mutação.
    for target, raw in outputs.items():
        old = target.read_bytes() if target.exists() else None
        if old == raw:
            previous.append({"arquivo": target.relative_to(raiz).as_posix(), "inalterado": True, "sha256": sha(raw)})
            continue
        relative = target.relative_to(raiz)
        row = {"arquivo": relative.as_posix(), "existia": old is not None, "sha256Antes": sha(old) if old is not None else None}
        if old is not None:
            copy = backup / "originais" / relative
            copy.parent.mkdir(parents=True, exist_ok=True)
            copy.write_bytes(old)
            if copy.read_bytes() != old:
                raise ErroPiloto("Backup não conferiu: " + str(target))
        previous.append(row)
    (backup / "antes.json").write_bytes(_json(previous))
    for target, raw in outputs.items():
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)
        if target.read_bytes() != raw:
            raise ErroPiloto("Fonte não conferiu após gravação: " + str(target))
    result["backup"] = str(backup)
    (backup / "resultado.json").write_bytes(_json(result))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ids", nargs="+", default=None)
    parser.add_argument("--catalogos", type=Path, default=None)
    parser.add_argument("--gravar", action="store_true")
    args = parser.parse_args()
    try:
        print(json.dumps(preparar(args.ids, catalogos=args.catalogos, gravar=args.gravar), ensure_ascii=False, indent=2))
    except Exception as exc:
        print(type(exc).__name__ + ": " + str(exc), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
