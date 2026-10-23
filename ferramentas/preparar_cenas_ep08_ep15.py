"""Adaptadores EP08–EP15. Não publica runtime, não altera os13 figurinos nem opera GTA."""
from __future__ import annotations

import argparse
from copy import deepcopy
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import uuid

from preparar_serie import analisar_roteiro
from preparar_cenas_serie import _base, _prop, _pose, _vip_props, _pesquisa, construir_rascunho

RAIZ = Path(__file__).resolve().parents[1]
DJ, RICA, IRACI, DINJER, CHEFE = "DJANIRA_POBRE", "DJANIRA_RICA", "DONA_IRACI", "DINJER", "O_CHEFE"


def marca(direita=0, frente=0, altura=0):
    return {"direita": direita, "frente": frente, "altura": altura}


def ator(code, x=0, y=0, olhar=None, rotacao=None):
    value = {"personagem": code, "marca": marca(x, y), "olharPara": olhar}
    if rotacao is not None:
        value["rotacao"] = rotacao
    return value


def andar(code, start, dur, x, y, speed=1, recurso=None):
    result = {"tipo": "andar", "personagem": code, "inicioSegundos": start, "duracaoSegundos": dur,
              "destino": marca(x, y), "velocidade": speed}
    if recurso:
        result["recurso"] = recurso
    return result


def clipe(code, resource, start, dur, *, loop=True, inteiro=True, preservar=False, sentado=False):
    result = {"tipo": "clipe", "personagem": code, "recurso": resource, "inicioSegundos": start,
              "duracaoSegundos": dur, "loop": loop, "corpoInteiro": inteiro,
              "manterPoseFinal": loop, "preservarDuranteFala": preservar}
    if sentado:
        result["sobrePoseSentada"] = True
    return result


def vetor(x=0, y=0, z=0):
    return {"x": x, "y": y, "z": z}


def na_mao(ident, recurso, code, *, mao="direita", offset=None, rotacao=None, eventos=None):
    return {"id": ident, "recurso": recurso, "marca": marca(), "assentarNoChao": False,
            "visivelInicial": True,
            "attachmentInicial": {"personagem": code, "mao": mao, "offset": offset or vetor(), "rotacao": rotacao or vetor()},
            "eventos": eventos or []}


def bandeja(code=DINJER):
    # RPEmotes oficial foodtraya, não offsets escolhidos por aparência.
    return na_mao("BANDEJA_SERVICO", "bandeja_servico", code, offset=vetor(.01, -.04, -.139), rotacao=vetor(20.0000007, 0, 0))


def expressao(code, dur, face="normal", *, start=0, olhar=None):
    value = {"personagem": code, "inicioSegundos": start, "duracaoSegundos": dur, "expressao": face}
    if olhar:
        value["olhar"] = olhar
    return value


def objeto_olhar(ident):
    return {"tipo": "objeto", "alvo": ident}


def reserva(scene, seconds, *, prefix=False, plano="DOIS"):
    if seconds:
        value = {"tipo": "pausa", "duracaoSegundos": seconds, "plano": plano}
        scene["roteiro"].insert(0, value) if prefix else scene["roteiro"].append(value)


def camera(scene, cuts):
    scene["camera"].update(modo="programado", cortes=[{"inicioSegundos": t, "plano": p, "personagem": c} for t,p,c in cuts])


def veiculo(ident, resource, x=0, y=0, *, extra=False, assentos=None, acoes=None):
    return {"id": ident, "recurso": resource, "marca": marca(x, y), "rotacao": 0,
            "motoristaExtra": extra, "trajetoDireto": False, "assentosIniciais": assentos or [], "acoes": acoes or []}


def auto_acao(tipo, code, start, dur, *, seat=-1, rota=None, speed=4):
    action = {"tipo": tipo, "personagem": code, "inicioSegundos": start, "duracaoSegundos": dur, "assento": seat}
    if tipo == "dirigir":
        action.update(rota=rota, velocidade=speed, raioChegada=1.5, raioParada=1)
    return action


def porta(tipo, code, start, dur, **extra):
    return {"tipo": tipo, "personagem": code, "inicioSegundos": start, "duracaoSegundos": dur, **extra}


def mesa_dupla(scene, first=RICA, second=CHEFE, third=None):
    scene["elenco"] = [ator(first, -.55, 0, second), ator(second, .55, 0, first)]
    scene["objetos"] = [_prop("MESA_JANTAR", "mesa", 0, .8),
                        _prop("CADEIRA_DJANIRA", "cadeira_simples", -.55, 0, -90),
                        _prop("CADEIRA_CHEFE", "cadeira_simples", .55, 0, 90)]
    scene["poses"] = [_pose(first, "CADEIRA_DJANIRA", "sentada_cadeira_livre_f"),
                       _pose(second, "CADEIRA_CHEFE", "sentado_cadeira_m")]
    if third:
        scene["elenco"].append(ator(third, 0, 2.5, None))


def transferir(ident, resource, giver, receiver, start, *, sentado=False):
    event = {"tipo": "transferir", "inicioSegundos": start+1, "personagem": receiver, "mao": "direita",
             "offset": vetor(), "rotacao": vetor(), "esperaMaximaSegundos": 2}
    prop = na_mao(ident, resource, giver, eventos=[event])
    return prop, [clipe(giver, "entrega_a", start, 3.5, loop=False, inteiro=not sentado, preservar=True, sentado=sentado),
                  clipe(receiver, "entrega_b", start, 3.5, loop=False, inteiro=not sentado, preservar=True, sentado=sentado)]


def levantamento(code, resource, start=0, dur=4):
    return {"tipo": "levantar", "personagem": code, "recurso": resource, "inicioSegundos": start, "duracaoSegundos": dur}


def adaptar(plano, pesquisa):
    sid = plano["id"]
    existing, _ = construir_rascunho(plano, pesquisa, RAIZ)
    scene = deepcopy(existing) if existing is not None else _base(plano)
    scene.update(encenacao="serie_v1")
    scene.setdefault("direcao", [])
    scene.setdefault("interpretacao", [])
    notas, pendencias = [], []
    total = sum(x["duracaoSegundos"] for x in scene["roteiro"])
    # As duas primeiras cenas precedem a transformação; autorização atual da direção.
    if sid in ("EP11_C01", "EP11_C02"):
        for entry in scene["elenco"]:
            if entry["personagem"] == RICA: entry["personagem"] = DJ
            if entry.get("olharPara") == RICA: entry["olharPara"] = DJ
        for part in scene["roteiro"]:
            if part.get("personagem") == RICA: part["personagem"] = DJ
        if scene["camera"].get("personagem") == RICA: scene["camera"]["personagem"] = DJ
        notas.append("Exceção autorizada: figurino pobre nos dois trechos anteriores à transformação deEP11C03;13XMLs preservados.")

    if sid == "EP08_C01":
        scene["elenco"] = [ator(DJ)]
        scene["saidaAgua"] = True
        reserva(scene,26,plano="LONGE")
        scene["interpretacao"] = [expressao(DJ,32,"machucado")]
        notas.append("Bruto32s permite emersão/rampa físicas; voz6s integral e recortes externos de6s após getters reais.")
    elif sid == "EP08_C03":
        scene["ambiente"].update(local="CASA_VO_INTERIOR",distanciaFrente=1)
        scene["elenco"] = [ator(IRACI,-1.1,-1,DJ),ator(DJ,1.1,-1,IRACI)]
        scene["objetos"] = [_prop("CAMA_IRACI","cama_quarto",-1.1,-1,90),_prop("CADEIRA_DJANIRA","cadeira_simples",1.1,-1,-90)]
        scene["poses"] = [_pose(IRACI,"CAMA_IRACI","deitada_cama_f","deitada"),_pose(DJ,"CADEIRA_DJANIRA","sentada_cadeira_livre_f")]
        scene["interpretacao"] = [expressao(IRACI,total,"machucado"),expressao(DJ,total,"chateado")]
        scene["transicoesPose"] = [{"personagem":DJ,"tipo":"sentar","recurso":"sentar_cadeira_livre_f","inicioSegundos":2,"duracaoSegundos":5}]
        notas.append("Djanira inicia ereta no ponto medido do enter_fwd, senta durante o chamado2–7s e precisa confirmar pelve/pés antes da sua fala14s.")
        notas.append("CASA_VO_INTERIOR exige a submarca real do quarto; distanciaFrente1 e frente-1 anulam o avanço para apoiar cama/cadeira no piso capturado, sem XYZ inventado.")
    elif sid == "EP08_C04":
        scene["camera"]["inverterEixo"] = True
    elif sid == "EP08_C05":
        scene["elenco"] = [ator(IRACI,-.45,0,DJ),ator(DJ,.45,0,IRACI)]
        prop, actions = transferir("PAPEL_IRACI","carta_simples",IRACI,DJ,0)
        scene["objetos"],scene["direcao"] = [prop],actions
        scene["interpretacao"] = [expressao(DJ,total,"normal",olhar=objeto_olhar("PAPEL_IRACI"))]
        pendencias.append("Modelo papel existente não comprova dobra visível; abertura posterior precisa recurso confirmado.")
    elif sid == "EP08_C06":
        scene["elenco"] = [ator(DJ,rotacao=0)]
        scene["camera"]["inverterEixo"] = True
        scene["objetos"] = [na_mao("PAPEL_IRACI","carta_simples",DJ)]
        notas.append("Marca na porta do quarto capturada olhando para dentro; costas do ator e câmera são relativas, corte preto externo.")
    elif sid == "EP09_C01":
        scene["ambiente"]["distanciaFrente"] = 1
        scene["elenco"] = [ator(DJ,0,-1,None,0)]
        scene["interpretacao"] = [expressao(DJ,total,"chateado",olhar={"tipo":"marca","destino":marca(0,1,.6)})]
        pendencias.append("Capturar marca real diante de túmulo existente emIGREJA_CHAPARRAL; nenhum túmulo/coordenada simulado.")
    elif sid=="EP12_C05":
        scene["objetos"] = [_prop("MESA_GORJETA","mesa",0,.5)]
        for ident, recurso in (("ENVELOPE_GORJETA","envelope"),("DINHEIRO_GORJETA","dinheiro"),("BILHETE_GORJETA","carta_simples")):
            scene["objetos"].append({"id":ident,"recurso":recurso,"marca":marca(),"assentarNoChao":False,
                                     "visivelInicial":True,"apoioInicial":"MESA_GORJETA","eventos":[]})
        scene["direcao"] = []
        scene["interpretacao"] = [expressao(DINJER,total,"normal",olhar=objeto_olhar("ENVELOPE_GORJETA"))]
        scene["camera"].update(modo="automatico",objeto="ENVELOPE_GORJETA",fovObjeto=30,elevacaoObjetoGraus=45)
        scene["camera"].pop("cortes",None)
        notas.append("Original pede close já preparado de envelope/dinheiro/bilhete; não exige abertura ou leitura corporal nesta tomada. Objetos próprios usam superfície real medida da mesa.")
        notas.append("Texto externo original no CapCut: Duas semanas. Juro. Narração7s integral e boca de Dinjer desligada.")
        pendencias.append("Confirmar por imagem envelope realmente aberto e dinheiro/bilhete visíveis dentro, com encaixe medido; o modelo atual e o close da AABB não comprovam interior aberto.")
    elif sid in ("EP09_C02","EP11_C05"):
        who = RICA if sid=="EP11_C05" else DJ
        scene["objetos"] = [na_mao("PAPEL_HERANCA","carta_simples",who)]
        scene["direcao"] = [clipe(who,"ler_papel_candidato",0,total)]
        scene["interpretacao"] = [expressao(who,total,"normal",olhar=objeto_olhar("PAPEL_HERANCA"))]
        if sid=="EP11_C05":
            scene["elenco"] = [ator(who,-.45,0,"CAPANGA_4"),ator("CAPANGA_4",.45,0,who)]
            prop, acts = transferir("ENVELOPE_HERANCA","envelope","CAPANGA_4",who,0)
            scene["objetos"].append(prop);scene["direcao"] = acts+[clipe(who,"ler_papel_candidato",3.5,total-7),clipe(who,"rir_candidato",total-3.5,3.5,loop=False)]
            scene["interpretacao"] = [expressao(who,total-3.5,"normal",olhar=objeto_olhar("PAPEL_HERANCA")),expressao(who,3.5,"feliz",start=total-3.5)]
        pendencias.append("Desdobrar/abrir folha/envelope ainda sem par+mesh primário confirmado; leitura real do recurso candidato não prova essa transição.")
    elif sid in ("EP09_C05","EP09_C06"):
        scene["objetos"] = [na_mao("SACOLA_DJANIRA","sacola",DJ)]
        scene["interpretacao"] = [expressao(DJ,total,"chateado")]
        if sid=="EP09_C05":
            scene["elenco"] = [ator(DJ,0,-3,None,0)]
            scene["direcao"] = [andar(DJ,0,total,0,5,.8)]
        else:
            scene["objetos"] = [na_mao("SACOLA_DJANIRA","sacola",DJ,offset=vetor(.24,.03,-.04),rotacao=vetor(0,-90,10))]
            scene["direcao"] = [{"tipo":"clipe","personagem":DJ,"inicioSegundos":0,"duracaoSegundos":total,
                                  "recurso":"carregar_sacola_autor","loop":True,"corpoInteiro":False,"manterPoseFinal":False}]
    elif sid=="EP10_C01":
        scene["objetos"] = [_prop("BANCO_PRAIA","banco",0,0),na_mao("SACOLA_DJANIRA","sacola",DJ,mao="esquerda")]
        scene["poses"] = [_pose(DJ,"BANCO_PRAIA","sono_f","deitada")]
        scene["poses"][0]["abracarObjeto"] = "SACOLA_DJANIRA"
        notas.append("Abraço exige duas mãos e tronco a até0.35m do volume real da sacola anexada, antes de Ready e em cada Tick; sono sozinho não comprova contato.")
        pendencias.append("Alinhamento de corpo/banco e contato das duas mãos/tronco na sacola precisam getters e imagem reais; nenhum offset deitado arbitrário.")
    elif sid=="EP10_C02":
        scene["elenco"] = [ator(DJ,0,0,None,0)]
        scene["interpretacao"] = [expressao(DJ,total,"chateado",olhar={"tipo":"horizonte"})]
        notas.append("Marca de ponto de ônibus real; conserva figurino pobre registrado em vez de gerar uniforme extra.")
    elif sid=="EP10_C05":
        scene["elenco"] = [ator(DJ,0,-3)]+[ator("COLEGA_"+str(i),x,-2 if i%2 else 2) for i,x in ((1,-3),(2,3),(3,-4.5))]
        scene["direcao"] = [andar(DJ,0,9,0,4,1.4)]+[andar("COLEGA_"+str(i),0,9,x,3 if i%2 else -3,.8) for i,x in ((1,-3),(2,3),(3,-4.5))]
        notas.append("Três figurantes próprios reutilizam XMLs registrados; ninguém recebe boca por narração.")
    elif sid=="EP10_C06":
        scene["objetos"] = [na_mao("PAPEL_CALENDARIO","bloco_anotacao",DJ,mao="esquerda",offset=vetor(.1,.02,.05),rotacao=vetor(10,0,0)),
            na_mao("LAPIS_DJANIRA","lapis",DJ,offset=vetor(.11,-.02,.001),rotacao=vetor(-120,0,0))]
        scene["direcao"] = [clipe(DJ,"anotar_bloco",0,total)]
        scene["interpretacao"] = [expressao(DJ,total,"normal",olhar=objeto_olhar("PAPEL_CALENDARIO"))]
        notas.append("Autoria RPEmotes notepad: mão esquerda18905 e lápis no58866; texto/calendário noCapCut, gesto real de anotação.")
    elif sid=="EP11_C01":
        scene["elenco"] = [ator(DJ,0,-2,None,0),ator("CAPANGA_4",2,0,DJ)]
        scene["direcao"] = [andar(DJ,0,8,0,3,.8)]
        scene["interpretacao"] = [expressao("CAPANGA_4",8,"normal",olhar={"tipo":"ator","alvo":DJ})]
        notas.append("CAPANGA_4 ocupa somente papel técnico de segurança; visual fixo preservado, teste visual pendente.")
    elif sid=="EP11_C02":
        scene["elenco"] = [ator(DJ,-.8,0,"ATENDENTE"),ator("ATENDENTE",.8,0,None,0)]
        scene["objetos"] = [_prop("MESA_BANCO","mesa",0,.7),_prop("TELA_BANCO","laptop",.7,.8)]
        scene["interpretacao"] = [expressao("ATENDENTE",5,"normal",olhar=objeto_olhar("TELA_BANCO")),expressao("ATENDENTE",total-5,"feliz",start=5,olhar=objeto_olhar("TELA_BANCO"))]
        screen=next(o for o in scene["objetos"] if o["id"]=="TELA_BANCO")
        screen.update(assentarNoChao=False,apoioInicial="MESA_BANCO")
        notas.append("Laptop assentado antescontagem na superfície real medida do apoio próprio; câmera deve mostrar a tela e preservar rostoATENDENTE oculto conforme original.")
    elif sid in ("EP11_C03","EP12_C01"):
        scene["elenco"] = [ator(RICA)]
        if sid=="EP12_C01":scene["elenco"].append(ator("CAPANGA_4",2,2,None,0))
        scene["veiculos"] = [veiculo("CARRO_LUXO","SUPERD",0,-5,extra=True,assentos=[{"personagem":RICA,"assento":0}],acoes=[
            auto_acao("dirigir",None,0,4,rota=[marca(0,0)],speed=3),auto_acao("sair",RICA,4,4,seat=0)])]
        scene["direcao"] = [andar(RICA,8,6 if sid=="EP11_C03" else 4,0,4,.8,"andar_salto_candidato")]
        if sid=="EP12_C01":
            reserva(scene,4);scene["portas"] = [porta("abrir","CAPANGA_4",4,4)]
            notas.append("Restaurar câmera registradaEP06_C02_BASE e manter a mesma marcaBOATE_FORA; sem recriar o enquadramento por aproximação.")
        else:
            camera(scene,[(0,"LONGE",RICA),(4,"MEDIA",RICA),(8,"BAIXA",RICA),(11,"LONGE",RICA)])
    elif sid=="EP11_C04":
        scene["poses"] = [_pose(RICA,None,"sentada_chao_f","chao")]
        scene["interpretacao"] = [expressao(RICA,total,"chateado")]
    elif sid=="EP12_C03":
        scene["objetos"] = _vip_props()+[bandeja()]
        scene["elenco"] = [ator(RICA,1.1,0,DINJER),ator(DINJER,-1.1,0,RICA)]
        scene["poses"] = [_pose(RICA,"CADEIRA_CHEFE","sentada_cadeira_livre_f")]
        scene["objetos"] = [o for o in scene["objetos"] if o["id"] in ("MESA_VIP","CADEIRA_CHEFE","BANDEJA_SERVICO")]
        scene["elenco"][1]["marca"] = marca(-1.1,-3)
        arrival=andar(DINJER,0,4,-1.1,0,.7);arrival.update(clipeDurantePercurso="carregar_bandeja",preservarDuranteFala=True)
        scene["direcao"] = [arrival,clipe(DINJER,"carregar_bandeja",4,total-4,inteiro=False,preservar=True)]
        notas.append("Chegada física3m em4s com bandeja secundária e boca durantefala, mantendo a pose sentada deDjanira.")
    elif sid=="EP12_C04":
        scene["elenco"] = [ator(RICA,-.55,0,DINJER),ator(DINJER,.55,0,RICA)]
        scene["objetos"] = [_prop("MESA_ENVELOPE","mesa",0,.6),na_mao("ENVELOPE_DINJER","envelope",RICA,eventos=[
            {"tipo":"colocar","inicioSegundos":1,"personagem":RICA,"recurso":"entrega_a","apoio":"MESA_ENVELOPE","esperaMaximaSegundos":2}])]
        scene["direcao"] = [clipe(RICA,"entrega_a",0,3.5,loop=False,inteiro=False),andar(RICA,3.5,5,-.55,-4,.9),
            clipe(DINJER,"choro_patricia",9,total-9)]
        scene["interpretacao"] = [expressao(DINJER,total,"chateado",olhar=objeto_olhar("ENVELOPE_DINJER"))]
        pendencias.append("Pegar/abrir envelope/count dinheiro na mesa ainda exige contato/recursos próprios; leitura/choro não declaram essas ações prontas.")
    elif sid=="EP13_C01":
        scene["elenco"] = [ator(DINJER,0,-3,None,0)]
        scene["camera"].update(planoInicial="MEDIA",planoNarracao="MEDIA",planoFinal="MEDIA")
        camera(scene,[(t,"MEDIA",DINJER) for t in range(8)])
        scene["direcao"] = [andar(DINJER,0,8,0,2,.7)]
        scene["interpretacao"] = [expressao(DINJER,8,"estressado",olhar={"tipo":"horizonte"})]
        notas.append('Camera MEDIA recalculada a cada1s nos cortes0-7 acompanha a posicao real de Dinjer; LONGE entrou na parede e intervalo2s deixou ator na borda.')
    elif sid=="EP13_C02":
        codes=["CAPANGA_1","CAPANGA_2","CAPANGA_3"]
        scene["elenco"] = [ator(DINJER,-3,1)]+[ator(c,i*1.2,0,"CAPANGA_4") for i,c in enumerate(codes)]+[ator("CAPANGA_4",0,-.9,"CAPANGA_1")]
        scene["objetos"],scene["direcao"] = [],[]
        for i,c in enumerate(codes):
            prop,actions=transferir("ENVELOPE_CAPANGA_"+str(i+1),"envelope","CAPANGA_4",c,i*5)
            scene["objetos"].append(prop);scene["direcao"].extend(actions)
        for i,c in enumerate(codes[1:],1):
            scene["direcao"].extend([andar("CAPANGA_4",i*5-1.5,1.5,i*1.2,-.9,.8),
                {"tipo":"virar","personagem":c,"inicioSegundos":i*5-1.5,"duracaoSegundos":1.5,"alvo":"CAPANGA_4"}])
        scene["direcao"].sort(key=lambda d:d["inicioSegundos"])
        reserva(scene,4)
        notas.append("Entregas0–3.5/5–8.5/10–13.5s intercalam caminhada1.2m e giro dos receptores; bruto14s com voz10s para cortes reais.")
    elif sid=="EP13_C03":
        scene["veiculos"] = [veiculo("CAMINHAO_1","PACKER",-3,-8,extra=True,acoes=[auto_acao("dirigir",None,0,6,rota=[marca(-3,12)],speed=4)]),
            veiculo("CAMINHAO_2","HAULER",3,-8,extra=True,acoes=[auto_acao("dirigir",None,6,6,rota=[marca(3,12)],speed=4)])]
        scene["portas"] = [porta("fechar",None,12,3)]
        reserva(scene,2,plano="LONGE")
        notas.append("Capturar três subtomadas reais: galpão vazio, caminhões saindo e portão fechando; montagem externa de13s conserva voz, sem elenco artificial.")
    elif sid=="EP13_C04":
        scene["objetos"] = [na_mao("TELEFONE_CHEFE","celular",CHEFE)]
        paths=[andar(CHEFE,0,4,1.5,0,.6),andar(CHEFE,4,4,-1.5,0,.7)]
        for path in paths:path.update(clipeDurantePercurso="ligacao_m",preservarDuranteFala=True)
        scene["direcao"] = paths
        scene["interpretacao"] = [expressao(CHEFE,8,"comraiva")]
        notas.append("Chefe percorre os dois lados durante a ligação8s: andar primário e listenbase secundário mantêm a boca e o telefone próprio.")
    elif sid=="EP13_C05":
        scene["objetos"] = [_prop("MESA_LIMPEZA","mesa",0,0)]
        scene["direcao"] = [clipe(DINJER,"limpar_mesa",0,4),andar(DINJER,4,4,0,2,.7)]
        scene["interpretacao"] = [expressao(DINJER,4,"normal"),expressao(DINJER,8,"chateado",start=4,olhar={"tipo":"marca","destino":marca(0,3,1.2)})]
        camera(scene,[(0,"MEDIA",DINJER),(8,"PERTO",DINJER)])
        scene["camera"]["planoFinal"] = "PERTO"
        notas.append("A foto emoldurada será composta fora do jogo, como autoriza o roteiro, com um print real de DJANIRA_RICA; nenhum quadro genérico é criado no chão.")
        pendencias.append("Capturar a marca BOATE_DENTRO orientada para uma parede real, conferir o ponto relativo de olhar e o enquadramento; extrair print da rica original com SHA/PTS e compor o retrato na parede no CapCut.")
    elif sid=="EP13_C06":
        scene["poses"] = [_pose(DINJER,None,"sentada_chao_f","chao")]
        scene["interpretacao"] = [expressao(DINJER,total,"chateado",olhar={"tipo":"chao"})]
        scene["camera"].update(planoInicial="MEDIA",planoFala="MEDIA",planoNarracao="MEDIA",planoFinal="MEDIA")
        for part in scene["roteiro"]:part["plano"]="MEDIA"
        notas.append("Câmera MEDIA autorizada após feixes da boate ocultarem a tomada ALTA real; poses, dois blocos6s e identidade permanecem. Novo enquadramento exige imagem antes de gravação aprovada.")
    elif sid=="EP14_C01":
        scene["portas"] = [porta("bater",DINJER,0,7,batidas=3,recurso="bater_porta")]
        scene["interpretacao"] = [expressao(DINJER,7,"estressado")]
        notas.append("Mantém figurino/maquiagem fixos; registrar conflito original sem variante silenciosa.")
    elif sid=="EP14_C02":
        pendencias.append("Marca deve ter degrau real sobDjanira, com alturas dos dois atores medidas; offsetZmanual não simula plataforma.")
    elif sid=="EP14_C05":
        scene["elenco"] = [ator(RICA,-1.1,0,None),ator(DINJER,1.1,0,None,0)]
        scene["interpretacao"] = [expressao(DINJER,total,"chateado",olhar={"tipo":"horizonte"}),expressao(RICA,total,"normal")]
    elif sid=="EP15_C01":
        scene["veiculos"] = [veiculo("CARRO_LUXO","SUPERD",0,-8,assentos=[{"personagem":RICA,"assento":-1}],acoes=[
            auto_acao("dirigir",RICA,0,7,rota=[marca(0,0)],speed=3)])]
        notas.append("Marca e eixo devem estar na rua real do morro; ground/collision e chegada nativos, semaltura inventada.")
    elif sid in ("EP15_C02","EP15_C03"):
        mesa_dupla(scene,third=DINJER)
        scene["objetos"].append(bandeja());scene["direcao"] = [clipe(DINJER,"carregar_bandeja",0,total,inteiro=False,preservar=True)]
        scene["interpretacao"] = [expressao(DINJER,total,"estressado")]
        if sid=="EP15_C03":
            scene["objetos"].append(na_mao("PAPEL_HERANCA","carta_simples",RICA,eventos=[{"tipo":"colocar","inicioSegundos":1,"personagem":RICA,
                "recurso":"entrega_a","apoio":"MESA_JANTAR","esperaMaximaSegundos":2}]))
            scene["direcao"].append(clipe(RICA,"entrega_a",0,3.5,loop=False,inteiro=False,preservar=True,sentado=True))
            scene["interpretacao"].append(expressao(CHEFE,3,"estressado",start=3))
            pendencias.append("Retirar papel do bolso exige contato de retirada anterior, não começar propjána mão; gesto machucado cry60561 precisa avaliação visual.")
    elif sid=="EP15_C04":
        mesa_dupla(scene)
        reserva(scene,4,prefix=True)
        scene["transicoesPose"] = [levantamento(CHEFE,"levantar_cadeira_m")]
        scene["direcao"] = [andar(CHEFE,4,4,.55,3,.7),{"tipo":"virar","personagem":CHEFE,"inicioSegundos":8,"duracaoSegundos":1,"destino":marca(.55,8)}]
        scene["interpretacao"] = [expressao(CHEFE,total+4-9,"normal",start=9,olhar={"tipo":"horizonte"})]
        notas.append("Chefe caminha até a janela orientada pela marca, gira para frente relativa e fica de costas paraRica olhando horizonte após9s; janela real ainda exige conferência no local.")
        notas.append("Prefixo técnico4s para saída real; falas5/2/12/15 intactas, cortes externos depois dos getters.")
    elif sid=="EP15_C05":
        scene["objetos"] = [_prop("CADEIRA_DJANIRA","cadeira_simples",-1.1,0,-90)]
        scene["poses"] = [_pose(RICA,"CADEIRA_DJANIRA","sentada_cadeira_livre_f")]
        reserva(scene,4,prefix=True)
        scene["transicoesPose"] = [levantamento(RICA,"levantar_cadeira_livre_f")]
        scene["direcao"] = [{"tipo":"virar","personagem":RICA,"inicioSegundos":4,"duracaoSegundos":1,"alvo":DINJER}]
        # A fala4 inicia após4+4+2+5=15s. Manter nela por12s, inclusive no silêncio de ouvinte.
        camera(scene,[(0,"DOIS",RICA),(4,"MEDIA",RICA),(8,"MEDIA",DINJER),(10,"MEDIA",RICA),(15,"PERTO",DINJER),(27,"MEDIA",DINJER),(33,"DOIS",RICA)])
        notas.append("Texto/duração8s da fala4 preservados; cameraDINJER mantém12s a partir dela, como instrução literal; não alongar fala.")
    elif sid=="EP15_C07":
        scene["direcao"] = [andar(RICA,0,5,-1.1,3,.7)]
        scene["elenco"][0]["olharPara"] = None
        scene["interpretacao"] = [expressao(RICA,total,"normal",olhar={"tipo":"horizonte"})]
        notas.append("Djanira começa a sair durantefala doChefe, fala5s de costas sem novo movimento primário; semolharparatrás.")
    elif sid=="EP15_C08":
        scene["elenco"] = [ator(CHEFE,0,0),ator(DINJER,2.5,0)]
        scene["objetos"] = [_prop("CADEIRA_CHEFE","cadeira_simples",0,0),bandeja()]
        scene["poses"] = [_pose(CHEFE,"CADEIRA_CHEFE","sentado_cadeira_m")]
        scene["direcao"] = [clipe(DINJER,"carregar_bandeja",0,total,inteiro=False)]
        camera(scene,[(0,"LONGE",CHEFE),(4,"MEDIA",DINJER),(8,"LONGE",CHEFE)])
    elif sid=="EP15_C09":
        scene["veiculos"] = [veiculo("CARRO_LUXO","SUPERD",1,0,acoes=[auto_acao("entrar",RICA,0,5),
            auto_acao("dirigir",RICA,5,14,rota=[marca(1,-10),marca(1,-25)],speed=3)])]
        notas.append("Condução física no morro real; corte preto externo, narr10+narrador9 preservados sem compressão.")
    if sid in ("EP08_C05","EP08_C06","EP09_C02","EP11_C05","EP15_C03"):
        notas.append("Carta usa prop_amanda_note_01 da PropList9538, alias carta_simples já catalogado; modelo, dobra, leitura e contato em mão ainda exigem imagem/getters, sem alegar transições prontas.")
    if sid == "EP11_C05":
        pendencias.append("Sequência física ainda deve separar recebimento do envelope e retirada da carta: dois props na mão direita desde0s não representam pegar o papel depois de receber.")
    # Enquadramentos de diálogo sobre pose preservam a base; narração nunca aciona boca.
    return scene, notas, pendencias


def conferir_fidelidade(plano,scene):
    original = [p for p in _base(plano)["roteiro"] if p["tipo"] != "pausa"]
    actual = [p for p in scene["roteiro"] if p["tipo"] != "pausa"]
    if plano["id"] in ("EP11_C01","EP11_C02"):
        for p in original:
            if p.get("personagem")==RICA:p["personagem"]=DJ
    fields = ("tipo","texto","duracaoSegundos","personagem","gesto","narrador")
    if [{k:v for k,v in p.items() if k in fields} for p in actual] != [{k:v for k,v in p.items() if k in fields} for p in original]:
        raise ValueError(plano["id"]+": voz/duração/nome/gesto alterado")


def preparar(raiz=RAIZ, gravar=False):
    root=Path(raiz).resolve()
    series=analisar_roteiro(root/'ROTEIRO_A_CALOTEIRA_DO_OESTE_15_EPISODIOS.md')
    pesquisa=_pesquisa(root)
    plans=[p for p in series['planos'] if 8<=p['episodio']<=15]
    if len(plans)!=49:raise ValueError('Esperados49IDs reais deEP08–15')
    stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')+'-'+uuid.uuid4().hex[:8]
    backup=root/'backups/temporada-08-15'/('dados-'+stamp)
    results=[];outputs={}
    for plan in plans:
        scene,notes,pending=adaptar(plan,pesquisa);conferir_fidelidade(plan,scene)
        rel=Path('producao/cenas_serie')/f"EP{plan['episodio']:02d}"/(plan['id']+'.json')
        raw=(json.dumps(scene,ensure_ascii=False,indent=2)+'\n').encode('utf-8');outputs[rel]=raw
        results.append({'id':plan['id'],'arquivo':str(rel),'sha256':hashlib.sha256(raw).hexdigest().upper(),
            'origem':plan['origem'],'acoesOriginais':plan['acoesOriginais'],'blocosOriginais':plan['roteiro'],
            'conteudoOriginalMs':plan['tempos']['blocosDeclaradosMs'],'conteudoFonteMs':round(sum(p['duracaoSegundos'] for p in scene['roteiro'])*1000),
            'adaptaçõesTecnicas':notes,'pendenciasFisicas':pending,'vozesFieis':True,'gravado':False,'testeVisual':'pendente'})
    manifest={'versao':1,'tipoDocumento':'adaptadores_ep08_ep15','cenas':results,'total':49,
        'fontes':series['fontes'],'figurinossorteados':False,'publicadoRuntime':False,
        'contratosNovos':['veiculos','transicoesPose','poses.tipo=chao','elenco.rotacao','direcao.preservarDuranteFala','objetos.eventos.colocar','portas','saidaAgua'],
        'estado':'fontes_preparadas; pendencias físicas por cena impedem afirmar encenação integral; montagem e gravação reais pendentes'}
    outputs[Path('producao/temporada_08_15/manifesto.json')]=(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n').encode('utf-8')
    if gravar:
        backup.mkdir(parents=True,exist_ok=False);journal=[]
        # Backup de cada arquivo antes de qualquer mudança, depois escritos somente no escopo.
        for rel,raw in outputs.items():
            dst=root/rel;before=dst.read_bytes() if dst.exists() else None
            if before is not None and before!=raw:
                saved=backup/rel;saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(dst,saved)
                if saved.read_bytes()!=before:raise ValueError('Backup divergente: '+str(rel))
            journal.append({'arquivo':str(rel),'antesSHA256':hashlib.sha256(before).hexdigest().upper() if before is not None else None,
                'depoisSHA256':hashlib.sha256(raw).hexdigest().upper(),'mudou':before!=raw})
        (backup/'journal.json').write_text(json.dumps(journal,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        for rel,raw in outputs.items():
            dst=root/rel
            if dst.exists() and dst.read_bytes()==raw:continue
            dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes(raw)
        manifest['backup']=str(backup)
    return manifest


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--gravar',action='store_true',help='Salvar somente as49fontes EP08–15 e manifesto, com backups; nunca publicar runtime')
    args=parser.parse_args();result=preparar(gravar=args.gravar)
    print(json.dumps({'total':result['total'],'vozesFieis':all(p['vozesFieis'] for p in result['cenas']),
        'cenasComPendencias':sum(bool(p['pendenciasFisicas']) for p in result['cenas']),'backup':result.get('backup'),
        'manifesto':str(RAIZ/'producao/temporada_08_15/manifesto.json')},ensure_ascii=False,indent=2))
