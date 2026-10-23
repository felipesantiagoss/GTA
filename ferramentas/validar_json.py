"""Contrato JSON v1 do Diretor, sem pacotes e sem simular resultado no GTA."""

from __future__ import annotations

from decimal import Decimal
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

from validar_cena import ErroCena, GESTOS, PLANOS, ler_xml


RAIZ = Path(__file__).resolve().parents[1]
CODIGO = re.compile(r"[A-Z][A-Z0-9_]{0,47}")
MODELO = re.compile(r"[a-z0-9_]{1,64}")
ID_PRODUCAO = re.compile(r"EP(0[1-9]|1[0-5])_C(0[1-9]|[1-9][0-9])")
IDS_SUPLEMENTARES = frozenset(("EP01_EXTRA_IRACI_BAR", "EP01_EXTRA_DINJER_DEDO"))
ID_TESTE = re.compile(r"teste_(?:json|coreografia|fila_(?:0[1-9]|[1-9][0-9])|elenco_(?:pobre|rica))")
RECURSO_CORPO = re.compile(r"[a-z][a-z0-9_]{0,47}")
NOME_ANIMACAO = re.compile(r"[A-Za-z0-9_@.\-]{1,160}")
GRUPO_PAREADO = re.compile(r"[A-Za-z][A-Za-z0-9_]{0,47}")
FONTE_FACIAL = r"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V\menyooStuff\PedAnimList.txt"
SHA_FACIAL = "2C537060BD97FCF0DACA407DB1436A5D54E4B8DD5274DBC9EF0D8466EF6F21E4"
FACIAIS = {
    "normal": ("variations@normal", "mood_normal_1", 59848, 59762),
    "chateado": ("base", "mood_sulk_1", 59793, 59736),
    "feliz": ("variations@happy", "mood_happy_1", 59841, 59755),
    "machucado": ("variations@injured", "mood_injured_1", 59845, 59759),
    "comraiva": ("variations@angry", "mood_angry_1", 59839, 59753),
    "estressado": ("variations@stressed", "mood_stressed_1", 59854, 59768),
    "convencido": ("base", "mood_smug_1", 59831, 59707),
}
MODELOS_OBJETOS = {
    "celular": "prop_npc_phone_02", "papel": "p_cs_papers_01", "envelope": "prop_cs_envolope_01",
    "documentos": "prop_cs_documents_01", "sacola": "prop_cs_shopping_bag", "bandeja": "prop_food_bs_tray_01",
    "comprimidos": "prop_cs_pills", "prancheta": "p_amb_clipboard_01", "ferramenta_jardim": "prop_tool_shovel",
    "taca": "prop_wine_glass", "cadeira_simples": "prop_chair_01a", "poltrona": "prop_armchair_01",
    "garrafa_cerveja": "prop_amb_beer_bottle",
    "banco": "prop_bench_01a", "banco_sono_c1_02": "prop_bench_02", "mesa": "prop_table_01", "mesa_grande_c8_autor": "v_res_m_dinetble_replace", "mesa_limpeza_bar": "prop_table_07", "bancada_limpeza_candidata": "prop_ff_counter_02", "mesa_banco_candidata": "prop_table_03", "cadeira_casa": "v_res_fa_chair01",
    "cama_casa": "v_res_d_bed", "cama_quarto": "v_res_msonbed", "cama_simples": "v_res_tre_bed1",
    "bandeja_servico": "prop_food_bs_tray_03", "bandeja_champanhe_autor": "h4_prop_h4_champ_tray_01b", "container_serie": "prop_container_01a", "caixas_serie": "prop_box_wood02a_pu", "caixas_madeira_candidata": "prop_box_wood02a",
    "guarda_roupa": "v_res_tre_wardrobe", "sapato_mao": "prop_cs_amanda_shoe", "laptop": "prop_laptop_01a", "monitor_crt": "prop_monitor_01b", "teclado_pc": "prop_keyboard_01a", "gabinete_pc": "v_res_pctower", "monitor_crt_a": "prop_monitor_01a", "monitor_crt_b": "v_res_monitor", "monitor_crt_c": "prop_rub_monitor", "monitor_crt_d": "prop_ld_monitor_01", "monitor_crt_e": "prop_monitor_01c", "monitor_crt_f": "prop_monitor_01d", "monitor_crt_g": "v_res_lest_monitor", "monitor_crt_h": "v_res_monitorsquare", "monitor_crt_i": "m23_2_prop_m32_crt_mon_01a",
    "foto_quadro": "prop_cs_photoframe_01", "bloco_anotacao": "prop_notepad_01", "lapis": "prop_pencil_01",
    "dinheiro": "prop_cash_pile_01", "lapide": "prop_gravestones_01a", "caixa_remedio": "v_res_tt_pharm2",
    "carta_simples": "prop_amanda_note_01", "papel_dobrado_autoral": "prop_amanda_note_01", "bolsa_sono84_preview": "prop_ld_handbag", "sacola_papel_sono84_preview": "prop_paper_bag_small", "guarda_roupa_aberto_candidato": "v_24_wrd_mesh_wardrobe",
    "armario_classico_candidato": "v_res_m_armoire",
    "porta_armario": "prop_wardrobe_door_01",
    "documento_script": "p_cs_script_s",
    "papel_preview86_nota_b": "prop_amanda_note_01b",
    "papel_preview86_nota_s": "p_amanda_note_01_s",
    "papel_preview86_rcss": "p_rcss_folded",
    "papel_preview86_x17": "xm_prop_x17_note_paper_01a",
    "papel_preview86_rolled": "prop_cs_rolled_paper",
    "papel_preview86_envelope": "p_cash_envelope_01_s",
    "papel_preview86_file_a": "h4_prop_h4_files_paper_01a",
    "papel_preview86_file_b": "h4_prop_h4_files_paper_01b",
    "papel_preview86_ng01": "ng_proc_paper_01a",
    "papel_preview86_ng02": "ng_proc_paper_02a",
    "papel_preview86_ng03": "ng_proc_paper_03a",
    "papel_preview86_ng031": "ng_proc_paper_03a001",
    "papel_preview86_cs01": "p_cs_papers_01",
    "papel_preview86_cs02": "p_cs_papers_02",
    "papel_preview86_cs03": "p_cs_papers_03",
    "papel_preview86_pamphlet": "prop_cs_pamphlet_01",
    "papel_preview86_pamphlet_s": "p_cs_pamphlet_01_s",
    "papel_preview86_brochure": "sf_prop_sf_brochure_01a",
    "cadeira_entrada_autor": "ex_prop_offchair_exec_02",
    "caixas_ensaio88_wood01a": "prop_box_wood01a",
    "caixas_negocio_madeira_01a": "prop_box_wood01a",
    "caixas_ensaio88_wood03a": "prop_box_wood03a",
    "caixas_ensaio88_wood04a": "prop_box_wood04a",
    "caixas_ensaio88_wood05a": "prop_box_wood05a",
    "caixas_ensaio88_wood05b": "prop_box_wood05b",
}
RECURSOS_CAIXAS_TECNICAS88 = frozenset(('caixas_ensaio88_wood01a','caixas_ensaio88_wood03a','caixas_ensaio88_wood04a','caixas_ensaio88_wood05a','caixas_ensaio88_wood05b'))
APOIOS_OBJETOS = frozenset(("cadeira_simples", "poltrona", "banco", "banco_sono_c1_02", "mesa", "mesa_grande_c8_autor", "mesa_limpeza_bar", "bancada_limpeza_candidata", "mesa_banco_candidata", "cadeira_casa", "cama_casa", "cama_quarto", "cama_simples"))
POSES = {
    "sentada_cadeira_f": ("sentada", "amb@prop_human_seat_chair@female@arms_folded@base", "base"),
    "sentada_cadeira_livre_f": ("sentada", "amb@prop_human_seat_chair@female@proper@base", "base"),
    "sentado_cadeira_m": ("sentada", "amb@prop_human_seat_chair@male@generic@base", "base"),
    "sentada_meio_fio_f": ("sentada", "amb@world_human_seat_steps@female@hands_by_sides@base", "base"),
    "deitada_cama_f": ("deitada", "mp_bedmid", "f_sleep_l_loop_bighouse"),
    "sono_f": ("deitada", "mp_sleep", "sleep_loopfemale"),
    "sono_banco_tracy_f": ("deitada", "timetable@tracy@sleep@", "base"),
    "office_cell_sleep_f": ("deitada", "anim@scripted@freemode@ig6_office_cell_sleep@female@", "base_pose_02"),
    "sentada_chao_f": ("chao", "amb@world_human_picnic@female@base", "base"),
}
TRANSICOES_POSE = {
    "levantar_cadeira_f": ("sentada_cadeira_f", "amb@prop_human_seat_chair@female@arms_folded@exit", "exit_fwd"),
    "levantar_cadeira_livre_f": ("sentada_cadeira_livre_f", "amb@prop_human_seat_chair@female@proper@exit", "exit_forward"),
    "levantar_cadeira_m": ("sentado_cadeira_m", "amb@prop_human_seat_chair@male@generic@exit", "exit_forward"),
    "levantar_chao_f": ("sentada_chao_f", "amb@world_human_picnic@female@exit", "exit"),
}
TRANSICOES_POSE.update({
    "sentar_cadeira_f": ("sentada_cadeira_f", "amb@prop_human_seat_chair@female@arms_folded@enter", "enter_fwd"),
    "sentar_cadeira_livre_f": ("sentada_cadeira_livre_f", "amb@prop_human_seat_chair@female@proper@enter", "enter_fwd"),
    "sentar_cadeira_m": ("sentado_cadeira_m", "amb@prop_human_seat_chair@male@generic@enter", "enter_forward"),
    "sentar_chao_f": ("sentada_chao_f", "amb@world_human_picnic@female@enter", "enter"),
})
VEICULOS = {"FAGGIO": ("MOTO", "faggio"), "BATI": ("MOTO", "bati"), "SANCHEZ": ("MOTO", "sanchez"),
    "BUS": ("ONIBUS", "bus"), "COACH": ("ONIBUS", "coach"), "TAILGATER": ("CARRO", "tailgater"),
    "SUPERD": ("CARRO", "superd"), "PACKER": ("CAMINHAO", "packer"), "HAULER": ("CAMINHAO", "hauler")}
CORPO_DURANTE_FALA = frozenset((("cellphone@female", "cellphone_call_listen_base"), ("cellphone@", "cellphone_call_listen_base"),
    ("mp_ped_interaction", "hugs_guy_a"), ("mp_ped_interaction", "hugs_guy_b"), ("mp_common", "givetake1_a"),
    ("mp_common", "givetake1_b"), ("anim@mp_player_intcelebrationmale@cut_throat", "cut_throat"),
    ("anim@heists@box_carry@", "idle"), ("anim@move_f@waitress", "idle"), ("missmic3", "newspaper_idle_loop_dave")))
GESTOS_SENTADOS = frozenset((("mp_common", "givetake1_a"), ("mp_common", "givetake1_b"), ("mp_common_miss", "put_away_coke")))
POSES_BRACOS_LIVRES = frozenset(("sentada_cadeira_livre_f", "sentado_cadeira_m"))
CLIMAS = frozenset(("limpo", "ensolarado", "nublado", "encoberto", "chuva", "trovoada", "nevoa", "smog", "abrindo", "neutro"))
PLANOS_AMBIENTE = frozenset(("LONGE", "MEDIA", "BAIXA", "ALTA"))


def _erro(campo, mensagem):
    raise ErroCena(f"{campo}: {mensagem}.")


def _objeto(valor, campo, obrigatorios, opcionais=()):
    if not isinstance(valor, dict):
        _erro(campo, "informe um objeto")
    faltam = set(obrigatorios) - valor.keys()
    extras = valor.keys() - (set(obrigatorios) | set(opcionais))
    if faltam:
        _erro(campo, "campo(s) obrigatório(s) ausente(s): " + ", ".join(sorted(faltam)))
    if extras:
        _erro(campo, "campo(s) desconhecido(s): " + ", ".join(sorted(extras)))


def _texto(valor, campo, tamanho):
    if not isinstance(valor, str) or not valor.strip() or len(valor) > tamanho:
        _erro(campo, f"use texto de 1 a {tamanho} caracteres")
    if any(0xD800 <= ord(letra) <= 0xDFFF for letra in valor):
        _erro(campo, "texto contém caractere Unicode incompleto")
    if any(ord(letra) < 32 and letra not in "\t\n\r" or ord(letra) in (0xFFFE, 0xFFFF) for letra in valor):
        _erro(campo, "texto contém caractere proibido no XML interno")


def _numero(valor, campo, minimo, maximo, milissegundos=False):
    if isinstance(valor, bool) or not isinstance(valor, (int, float, Decimal)):
        _erro(campo, "informe um número JSON")
    numero = Decimal(str(valor))
    if not numero.is_finite() or not Decimal(str(minimo)) <= numero <= Decimal(str(maximo)):
        _erro(campo, f"use número finito entre {minimo} e {maximo}")
    if milissegundos and numero:
        partes = numero.as_tuple()
        zeros_finais = 0
        for digito in reversed(partes.digits):
            if digito != 0:
                break
            zeros_finais += 1
        if partes.exponent + zeros_finais < -3:
            _erro(campo, "use resolução de milissegundos, no máximo três casas decimais efetivas")
    return numero


def _inteiro(valor, campo, minimo, maximo):
    if type(valor) is not int or not minimo <= valor <= maximo:
        _erro(campo, f"use inteiro entre {minimo} e {maximo}")


def _enum(valor, campo, opcoes):
    if not isinstance(valor, str) or valor not in opcoes:
        _erro(campo, "use " + ", ".join(sorted(opcoes)))


def _codigo(valor, campo):
    if not isinstance(valor, str) or CODIGO.fullmatch(valor) is None:
        _erro(campo, "use código de 1 a 48 letras maiúsculas, números ou _, começando por letra")


def _pares(pares):
    resultado = {}
    for chave, valor in pares:
        if chave in resultado:
            _erro("JSON." + chave, "chave duplicada")
        resultado[chave] = valor
    return resultado


def _constante(valor):
    _erro("JSON", f"constante {valor} proibida; use números finitos")


def _ler(caminho):
    try:
        conteudo = caminho.read_bytes()
        if len(conteudo) > 1024 * 1024:
            _erro(str(caminho), "arquivo excede 1 MiB")
        texto = conteudo.decode("utf-8-sig")
        if len(texto) > 100000:
            _erro(str(caminho), "arquivo excede 100000 caracteres")
        return json.loads(texto, object_pairs_hook=_pares, parse_float=Decimal, parse_constant=_constante)
    except (OSError, UnicodeError, json.JSONDecodeError, RecursionError) as erro:
        _erro(str(caminho), f"não foi possível ler JSON UTF-8 válido: {erro}")


def _catalogo(pasta, arquivo, lista):
    dados = _ler(pasta / arquivo)
    if not isinstance(dados, dict) or "versao" not in dados or lista not in dados:
        _erro(arquivo, "raiz exige versao e " + lista)
    _inteiro(dados["versao"], arquivo + ".versao", 1, 1)
    if not isinstance(dados[lista], list):
        _erro(arquivo + "." + lista, "informe uma lista")
    resultado = {}
    for i, item in enumerate(dados[lista]):
        campo = f"{arquivo}.{lista}[{i}]"
        if not isinstance(item, dict) or "codigo" not in item:
            _erro(campo, "entrada exige codigo")
        codigo = item["codigo"]
        _texto(codigo, campo + ".codigo", 48)
        if codigo in resultado:
            _erro(campo + ".codigo", "código duplicado")
        resultado[codigo] = item
    return resultado


def _marca(valor, campo):
    _objeto(valor, campo, ("direita", "frente"), ("altura",))
    for nome in ("direita", "frente"):
        _numero(valor[nome], campo + "." + nome, -10, 10)
    if "altura" in valor:
        _numero(valor["altura"], campo + ".altura", -2, 2)


def _plano(valor, campo):
    _enum(valor, campo, PLANOS)


def _alvo_camera(valor, campo, atores):
    if not atores:
        if valor is not None:
            _erro(campo, "sem elenco, use null para enquadrar o ambiente")
    elif not isinstance(valor, str) or valor not in atores:
        _erro(campo, "escolha um ator existente no elenco")


def _plano_cena(valor, campo, atores):
    _plano(valor, campo)
    if not atores and valor not in PLANOS_AMBIENTE:
        _erro(campo, "sem elenco, use LONGE, MEDIA, BAIXA ou ALTA")


def _metadados_visual(entrada, codigo, campo):
    arquivo, digest = entrada.get("visualArquivo"), entrada.get("visualSHA256")
    if ("visualArquivo" in entrada) != ("visualSHA256" in entrada):
        _erro(campo, "visualArquivo e visualSHA256 devem aparecer juntos")
    if "visualArquivo" in entrada:
        if arquivo != codigo + ".xml":
            _erro(campo + ".visualArquivo", f"use somente {codigo}.xml, sem caminho, em catalogos/visuais")
        if not isinstance(digest, str) or re.fullmatch(r"[0-9a-fA-F]{64}", digest) is None:
            _erro(campo + ".visualSHA256", "use SHA256 hexadecimal de 64 caracteres")


def _visual_pronto(entrada, codigo, pasta, campo):
    arquivo = entrada.get("visualArquivo")
    if arquivo is None:
        if entrada.get("modelo") in ("mp_f_freemode_01", "mp_m_freemode_01"):
            _erro(campo, "personagem freemode pronto exige visualArquivo e visualSHA256 aprovados")
        return
    caminho = pasta / "visuais" / arquivo
    try:
        from runtime_diretor import exigir_sem_vinculos, ErroRuntime
        caminho = exigir_sem_vinculos(caminho)
        if not caminho.is_file() or not caminho.resolve().is_relative_to((pasta / "visuais").resolve()):
            _erro(campo, "visual ausente ou fora de catalogos/visuais")
        digest = hashlib.sha256(caminho.read_bytes()).hexdigest().upper()
        if digest != entrada["visualSHA256"].upper():
            _erro(campo, "SHA256 do visual difere do catálogo")
        from importar_visuais import ler_visual
        ler_visual(caminho, expected_model=entrada["modelo"])
    except (OSError, ValueError, ErroRuntime, ImportError) as erro:
        _erro(campo, f"visual não validado: {erro}")


def _gestos_canonicos(pasta):
    caminho = pasta / "gestos_fala.xml"
    try:
        raiz = ler_xml(caminho)
    except (OSError, ET.ParseError) as erro:
        _erro(str(caminho), f"não foi possível ler o catálogo canônico: {erro}")
    if raiz.tag != "GestosFala" or raiz.attrib.get("versao") != "1":
        _erro(str(caminho), "catálogo canônico exige GestosFala versao=1")
    resultado = {}
    for gesto in raiz.findall("Gesto"):
        codigo = gesto.attrib.get("id")
        if not codigo or codigo in resultado:
            _erro(str(caminho), "ID de gesto ausente ou duplicado")
        resultado[codigo] = {}
        for sexo, elemento in (("homem", "Homem"), ("mulher", "Mulher")):
            variantes = gesto.findall(elemento)
            if len(variantes) != 1:
                _erro(str(caminho), "cada gesto exige uma variante Homem e uma Mulher")
            resultado[codigo][sexo] = {chave: variantes[0].attrib.get(chave) for chave in ("dicionario", "animacao")}
    return resultado


def _gesto(codigo, animacoes, canonicos, campo):
    if not isinstance(codigo, str) or codigo not in animacoes or animacoes[codigo].get("tipo") != "gesto":
        _erro(campo, "use código de gesto existente no catálogo")
    for sexo in ("homem", "mulher"):
        par = animacoes[codigo].get(sexo)
        if not isinstance(par, dict):
            _erro(campo, "par de gesto ausente no catálogo")
        for chave in ("dicionario", "animacao"):
            _texto(par.get(chave), campo + "." + sexo + "." + chave, 128)
        if codigo not in canonicos or any(par[chave] != canonicos[codigo][sexo][chave] for chave in ("dicionario", "animacao")):
            _erro(campo + "." + sexo, "par alterado; deve corresponder ao gestos_fala.xml canônico")


def _corpos(pasta):
    raiz = _ler(pasta / "corpos_serie.json")
    _objeto(raiz, "corpos_serie", ("versao", "estado", "animacoes"))
    _inteiro(raiz["versao"], "corpos_serie.versao", 1, 1)
    _texto(raiz["estado"], "corpos_serie.estado", 100)
    entradas = raiz["animacoes"]
    if not isinstance(entradas, list) or len(entradas) > 256:
        _erro("corpos_serie.animacoes", "use lista de 0 a 256 recursos")
    resultado = {}
    for i, entrada in enumerate(entradas):
        campo = f"corpos_serie.animacoes[{i}]"
        _objeto(entrada, campo, ("codigo", "tipo", "dicionario", "animacao", "origem", "restricoes"))
        codigo = entrada["codigo"]
        if not isinstance(codigo, str) or RECURSO_CORPO.fullmatch(codigo) is None or codigo in resultado:
            _erro(campo + ".codigo", "código minúsculo inválido ou repetido")
        _enum(entrada["tipo"], campo + ".tipo", ("clipe", "clipset"))
        for chave in ("dicionario", "animacao"):
            if not isinstance(entrada[chave], str) or NOME_ANIMACAO.fullmatch(entrada[chave]) is None:
                _erro(campo + "." + chave, "use o nome literal ASCII da lista de animações")
        if entrada["tipo"] == "clipset" and entrada["animacao"] != "walk":
            _erro(campo + ".animacao", "clipset de caminhada exige o par literal com walk")
        origem = entrada["origem"]
        _objeto(origem, campo + ".origem", ("arquivo", "linha", "sha256", "texto"))
        _texto(origem["arquivo"], campo + ".origem.arquivo", 1024)
        _caminho_fonte(origem["arquivo"], campo + ".origem.arquivo")
        _inteiro(origem["linha"], campo + ".origem.linha", 1, 500000)
        if not isinstance(origem["sha256"], str) or re.fullmatch(r"[0-9A-Fa-f]{64}", origem["sha256"]) is None:
            _erro(campo + ".origem.sha256", "use SHA256 hexadecimal de 64 caracteres")
        if origem["texto"] != entrada["dicionario"] + " " + entrada["animacao"]:
            _erro(campo + ".origem.texto", "o texto deve ser o par literal dicionário/clipe")
        restricoes = entrada["restricoes"]
        if not isinstance(restricoes, list) or len(restricoes) > 32:
            _erro(campo + ".restricoes", "use lista de 0 a 32 restrições")
        for texto in restricoes:
            _texto(texto, campo + ".restricoes", 500)
        resultado[codigo] = entrada
    return resultado


def _caminho_fonte(raw, campo, filename="PedAnimList.txt", require_menyoo=False):
    caminho = Path(raw)
    if (not caminho.is_absolute() or raw.startswith(("\\\\", "//")) or ":" in raw[2:]
            or any(letra in raw for letra in '<>"|?*\r\n\t\0')
            or any(parte in (".", "..") for parte in re.split(r"[\\/]", raw))
            or caminho.name.lower() != filename.lower()
            or require_menyoo and caminho.parent.name.lower() != "menyoostuff"):
        _erro(campo, "origem exige " + filename + " em caminho local absoluto, sem stream alternativo, . ou .." + (", sob menyooStuff" if require_menyoo else ""))
    return caminho


def _fontes_corpos(usados, filename="PedAnimList.txt", require_menyoo=False):
    from runtime_diretor import ErroRuntime, exigir_sem_vinculos
    fontes = {}
    for entrada in usados:
        origem = entrada["origem"]
        raw = origem["arquivo"]
        caminho = _caminho_fonte(raw, "recursos.origem", filename, require_menyoo)
        chave = str(caminho).casefold()
        digest = origem["sha256"].upper()
        if chave not in fontes:
            fontes[chave] = (caminho, digest, {})
        arquivo, hash_anterior, linhas = fontes[chave]
        if hash_anterior != digest:
            _erro("direcao.recurso", "hashes conflitantes para a mesma fonte")
        numero, par = origem["linha"], origem["texto"]
        if numero in linhas and linhas[numero] != par:
            _erro("direcao.recurso", "uma linha de origem não pode indicar pares diferentes")
        linhas[numero] = par
    for caminho, digest, linhas in fontes.values():
        try:
            exigir_sem_vinculos(caminho)
            with caminho.open("rb") as arquivo:
                conteudo = arquivo.read(64 * 1024 * 1024 + 1)
            if len(conteudo) > 64 * 1024 * 1024:
                _erro("direcao.recurso", "a lista de origem excede 64 MiB")
            if hashlib.sha256(conteudo).hexdigest().upper() != digest:
                _erro("direcao.recurso", "SHA256 da lista de origem diverge do catálogo")
            # Somente CR/LF são separadores de linha, como StreamReader.ReadLine no runtime.
            rows = conteudo.decode("utf-8-sig").replace("\r\n", "\n").replace("\r", "\n").split("\n")
            for numero, par in linhas.items():
                if numero > len(rows) or rows[numero - 1].strip() != par:
                    _erro("direcao.recurso", "o par literal diverge da linha de origem ou a linha não existe")
            exigir_sem_vinculos(caminho)
        except (OSError, UnicodeError, ErroRuntime) as erro:
            _erro("direcao.recurso", f"não foi possível conferir a fonte: {erro}")


def _direcao(dados, pasta, atores, falas, intervalos, duracao_cena):
    if "direcao" not in dados:
        return duracao_cena
    direcao = dados["direcao"]
    if not isinstance(direcao, list) or len(direcao) > 64:
        _erro("direcao", "informe lista de 0 a 64 ações de direção")
    corpos = _corpos(pasta)
    usados, grupos = [], {}
    for i, acao in enumerate(direcao):
        campo = f"direcao[{i}]"
        if not isinstance(acao, dict):
            _erro(campo, "informe um objeto")
        tipo = acao.get("tipo")
        _enum(tipo, campo + ".tipo", ("clipe", "andar", "virar", "celular"))
        obrigatorios = {"tipo", "personagem", "inicioSegundos", "duracaoSegundos"}
        if tipo in ("clipe", "celular"):
            obrigatorios.add("recurso")
            opcionais = {"loop", "corpoInteiro", "grupoPareado", "manterPoseFinal"} | ({"sobrePoseSentada", "preservarDuranteFala"} if tipo == "clipe" else set())
        elif tipo == "andar":
            obrigatorios.add("destino")
            opcionais = {"velocidade", "recurso", "clipeDurantePercurso", "preservarDuranteFala"}
        else:
            opcionais = {"alvo", "destino"}
        _objeto(acao, campo, obrigatorios, opcionais)
        ator = acao["personagem"]
        _codigo(ator, campo + ".personagem")
        if ator not in atores:
            _erro(campo + ".personagem", "ator não pertence ao elenco")
        inicio = _numero(acao["inicioSegundos"], campo + ".inicioSegundos", 0, 180, True)
        fim = inicio + _numero(acao["duracaoSegundos"], campo + ".duracaoSegundos", 0.1, 180, True)
        if fim > 180:
            _erro(campo + ".duracaoSegundos", "fim da direção ultrapassa 180 segundos")
        recurso = None
        if "recurso" in acao:
            codigo = acao["recurso"]
            if codigo == "office_cell_sleep_f":
                _erro(campo + ".recurso", "office_cell_sleep_f apenas pose tecnico84, nunca direcao/andar/producao")
            if not isinstance(codigo, str) or RECURSO_CORPO.fullmatch(codigo) is None or codigo not in corpos:
                _erro(campo + ".recurso", "use código minúsculo existente no catálogo de corpos")
            recurso = corpos[codigo]
            esperado = "clipset" if tipo == "andar" else "clipe"
            if recurso["tipo"] != esperado:
                _erro(campo + ".recurso", "o recurso precisa ser do tipo " + esperado)
            usados.append(recurso)
        if tipo in ("clipe", "celular"):
            for nome in ("loop", "corpoInteiro", "manterPoseFinal", "sobrePoseSentada", "preservarDuranteFala"):
                if nome in acao and type(acao[nome]) is not bool:
                    _erro(campo + "." + nome, "use booleano true ou false")
            if acao.get("preservarDuranteFala", False) and (tipo != "clipe" or
                    (recurso["dicionario"], recurso["animacao"]) not in CORPO_DURANTE_FALA):
                _erro(campo + ".preservarDuranteFala", "exige clipe real da whitelist de corpo físico durante fala")
            if acao.get("sobrePoseSentada", False) and (acao.get("corpoInteiro", True) or acao.get("loop", True) or
                    acao.get("manterPoseFinal", True) or "grupoPareado" in acao or
                    (recurso["dicionario"], recurso["animacao"]) not in GESTOS_SENTADOS):
                _erro(campo + ".sobrePoseSentada", "exige clipe permitido, corpoInteiro/loop/manterPoseFinal false, sem grupoPareado")
            if "grupoPareado" in acao:
                grupo = acao["grupoPareado"]
                if (not isinstance(grupo, str) or GRUPO_PAREADO.fullmatch(grupo) is None
                        or tipo != "clipe" or not acao.get("corpoInteiro", True)):
                    _erro(campo + ".grupoPareado", "use identificador ASCII de até 48 caracteres somente em clipes de corpo inteiro")
                grupos.setdefault(grupo, []).append((ator, inicio, fim, recurso["dicionario"], acao.get("loop", True), acao.get("manterPoseFinal", True)))
        elif tipo == "andar":
            _marca(acao["destino"], campo + ".destino")
            if "velocidade" in acao:
                _numero(acao["velocidade"], campo + ".velocidade", 0.2, 2)
            if "preservarDuranteFala" in acao and type(acao["preservarDuranteFala"]) is not bool:
                _erro(campo + ".preservarDuranteFala", "use booleano true ou false")
            if "clipeDurantePercurso" in acao:
                code = acao["clipeDurantePercurso"]
                expected = {"servir_champanhe_autor": ("anim@move_f@waitress", "idle"), "carregar_bandeja": ("anim@heists@box_carry@", "idle"), "ligacao_f": ("cellphone@female", "cellphone_call_listen_base"), "ligacao_m": ("cellphone@", "cellphone_call_listen_base"), "carregar_sacola_autor": ("move_weapon@jerrycan@generic", "idle")}
                extra = corpos.get(code)
                if code not in expected or not extra or extra["tipo"] != "clipe" or (extra["dicionario"], extra["animacao"]) != expected[code]:
                    _erro(campo + ".clipeDurantePercurso", "exige par real de bandeja, ligação ou sacola durante andar")
                usados.append(extra)
            elif "preservarDuranteFala" in acao:
                _erro(campo + ".preservarDuranteFala", "andar s? preserva gesto com canal corporal expl?cito")
        else:
            if ("alvo" in acao) == ("destino" in acao):
                _erro(campo, "virar exige exatamente alvo ou destino relativo")
            if "alvo" in acao:
                _codigo(acao["alvo"], campo + ".alvo")
                if acao["alvo"] not in atores or acao["alvo"] == ator:
                    _erro(campo + ".alvo", "escolha outro ator do elenco")
            else:
                _marca(acao["destino"], campo + ".destino")
        for anterior_inicio, anterior_fim in intervalos.setdefault(ator, []):
            if inicio < anterior_fim and fim > anterior_inicio:
                _erro(campo, "direção coincide com outra direção ou ação legada do mesmo ator")
        intervalos[ator].append((inicio, fim))
        if tipo != "virar" and not acao.get("preservarDuranteFala", False) and any(ator == falante and inicio < fala_fim and fim > fala_inicio for falante, fala_inicio, fala_fim in falas):
            _erro(campo, "direção corporal não pode coincidir com a fala desse ator")
        duracao_cena = max(duracao_cena, fim)
    for grupo, pares in grupos.items():
        if len(pares) != 2 or pares[0][0] == pares[1][0] or pares[0][1:] != pares[1][1:]:
            _erro("direcao.grupoPareado." + grupo, "o par exige exatamente 2 atores distintos, mesmo dicionário, intervalo, loop e pose final")
    if usados:
        _fontes_corpos(usados)
    return duracao_cena


def _fontes_faciais():
    usados = []
    for sufixo, clipe, homem, mulher in FACIAIS.values():
        for sexo, linha in (("male", homem), ("female", mulher)):
            dicionario = "facials@gen_" + sexo + "@" + sufixo
            usados.append({"origem": {"arquivo": FONTE_FACIAL, "sha256": SHA_FACIAL, "linha": linha, "texto": dicionario + " " + clipe}})
    _fontes_corpos(usados)


def _interpretacao(dados, atores, duracao_cena):
    if "interpretacao" not in dados:
        return duracao_cena
    entradas = dados["interpretacao"]
    if not isinstance(entradas, list) or len(entradas) > 64:
        _erro("interpretacao", "informe lista de 0 a 64 instruções")
    intervalos = {}
    for i, entrada in enumerate(entradas):
        campo = f"interpretacao[{i}]"
        _objeto(entrada, campo, ("personagem", "inicioSegundos", "duracaoSegundos"), ("expressao", "olhar"))
        ator = entrada["personagem"]
        _codigo(ator, campo + ".personagem")
        if ator not in atores:
            _erro(campo + ".personagem", "ator não pertence ao elenco")
        inicio = _numero(entrada["inicioSegundos"], campo + ".inicioSegundos", 0, 180, True)
        fim = inicio + _numero(entrada["duracaoSegundos"], campo + ".duracaoSegundos", 0.1, 180, True)
        if fim > 180:
            _erro(campo + ".duracaoSegundos", "fim da interpretação ultrapassa 180 segundos")
        if "expressao" in entrada:
            _enum(entrada["expressao"], campo + ".expressao", FACIAIS)
        tipo = "padrao"
        if "olhar" in entrada:
            olhar = entrada["olhar"]
            _objeto(olhar, campo + ".olhar", ("tipo",), ("alvo", "destino"))
            tipo = olhar["tipo"]
            _enum(tipo, campo + ".olhar.tipo", ("padrao", "ator", "objeto", "chao", "horizonte", "marca", "desvio"))
            requeridos = {"tipo"} | ({"alvo"} if tipo in ("ator", "objeto") else {"destino"} if tipo == "marca" else set())
            _objeto(olhar, campo + ".olhar", requeridos)
            if tipo == "ator":
                _codigo(olhar["alvo"], campo + ".olhar.alvo")
                if olhar["alvo"] not in atores or olhar["alvo"] == ator:
                    _erro(campo + ".olhar.alvo", "escolha outro ator do elenco")
            elif tipo == "objeto":
                _codigo(olhar["alvo"], campo + ".olhar.alvo")
                objetos = dados.get("objetos", [])
                if not isinstance(objetos, list) or not any(isinstance(objeto, dict) and objeto.get("id") == olhar["alvo"] for objeto in objetos):
                    _erro(campo + ".olhar.alvo", "declare esse objeto próprio em objetos")
            elif tipo == "marca":
                _marca(olhar["destino"], campo + ".olhar.destino")
        if "expressao" not in entrada and tipo == "padrao":
            _erro(campo, "declare ao menos uma expressão ou um olhar específico")
        for anterior_inicio, anterior_fim in intervalos.setdefault(ator, []):
            if inicio < anterior_fim and fim > anterior_inicio:
                _erro(campo, "interpretações simultâneas para o mesmo ator não são permitidas")
        intervalos[ator].append((inicio, fim))
        duracao_cena = max(duracao_cena, fim)
    if entradas:
        _fontes_faciais()
    return duracao_cena


def _recursos_objetos(pasta):
    raiz = _ler(pasta / "objetos_serie.json")
    _objeto(raiz, "objetos_serie", ("versao", "objetos"))
    _inteiro(raiz["versao"], "objetos_serie.versao", 1, 1)
    entradas = raiz["objetos"]
    if not isinstance(entradas, list) or len(entradas) > 80:
        _erro("objetos_serie.objetos", "informe lista de 0 a 80 recursos")
    resultado = {}
    for i, entrada in enumerate(entradas):
        campo = f"objetos_serie.objetos[{i}]"
        _objeto(entrada, campo, ("codigo", "tipo", "modelo", "origem", "estado"))
        codigo = entrada["codigo"]
        if not isinstance(codigo, str) or codigo not in MODELOS_OBJETOS or codigo in resultado:
            _erro(campo + ".codigo", "recurso desconhecido ou repetido")
        if entrada["modelo"] != MODELOS_OBJETOS[codigo]:
            _erro(campo + ".modelo", "modelo diverge do recurso conferido do helper")
        apoio = codigo in APOIOS_OBJETOS or codigo == "cadeira_entrada_autor"
        if entrada["tipo"] != ("apoio" if apoio else "prop"):
            _erro(campo + ".tipo", "tipo diverge da classificação do recurso")
        _texto(entrada["estado"], campo + ".estado", 100000)
        origem = entrada["origem"]
        _objeto(origem, campo + ".origem", ("arquivo", "linha", "sha256", "texto"), ("fonte",))
        if "fonte" in origem and origem["fonte"] != ("apoios" if apoio else "objetos"):
            _erro(campo + ".origem.fonte", "fonte precisa corresponder ao tipo de recurso")
        _texto(origem["arquivo"], campo + ".origem.arquivo", 100000)
        _caminho_fonte(origem["arquivo"], campo + ".origem.arquivo", "PropList.txt", True)
        _inteiro(origem["linha"], campo + ".origem.linha", 1, 500000)
        if not isinstance(origem["sha256"], str) or re.fullmatch(r"[0-9A-Fa-f]{64}", origem["sha256"]) is None:
            _erro(campo + ".origem.sha256", "use SHA256 hexadecimal de 64 caracteres")
        if origem["texto"] != entrada["modelo"]:
            _erro(campo + ".origem.texto", "texto de origem precisa ser o modelo literal")
        resultado[codigo] = entrada
    return resultado


def _vetor_attachment(valor, campo, limite, comprimento=False):
    _objeto(valor, campo, ("x", "y", "z"))
    partes = [_numero(valor[coord], campo + "." + coord, -limite, limite) for coord in ("x", "y", "z")]
    if comprimento and sum(parte * parte for parte in partes) > Decimal(str(limite)) ** 2:
        _erro(campo, "o ajuste completo deve ficar até 0,5 m da mão")


def _attachment_objeto(valor, campo, atores):
    _objeto(valor, campo, ("personagem", "mao", "offset", "rotacao"))
    _codigo(valor["personagem"], campo + ".personagem")
    if valor["personagem"] not in atores:
        _erro(campo + ".personagem", "ator não pertence ao elenco")
    _enum(valor["mao"], campo + ".mao", ("direita", "esquerda"))
    _vetor_attachment(valor["offset"], campo + ".offset", .5, True)
    _vetor_attachment(valor["rotacao"], campo + ".rotacao", 180)
    return valor["personagem"]


def _objetos(dados, pasta, atores, duracao_cena):
    if dados.get("id") == "teste_fila_88" and "objetos" not in dados:
        _erro("objetos", "ensaio88 exige duas caixas proprias declaradas")
    if "objetos" not in dados:
        return
    entradas = dados["objetos"]
    if not isinstance(entradas, list) or len(entradas) > 8:
        _erro("objetos", "informe lista de 0 a 8 objetos")
    paper86 = {"papel_preview86_nota_b", "papel_preview86_nota_s", "papel_preview86_rcss", "papel_preview86_x17", "papel_preview86_rolled", "papel_preview86_envelope", "papel_preview86_file_a", "papel_preview86_file_b", "papel_preview86_ng01", "papel_preview86_ng02", "papel_preview86_ng03", "papel_preview86_ng031", "papel_preview86_cs01", "papel_preview86_cs02", "papel_preview86_cs03", "papel_preview86_pamphlet", "papel_preview86_pamphlet_s", "papel_preview86_brochure"}
    if dados.get("id") == "teste_fila_86" or any(isinstance(o, dict) and o.get("recurso") in paper86 for o in entradas):
        if (dados.get("id") != "teste_fila_86" or dados.get("episodio") != 0 or dados.get("numero") != 0 or
                set(atores) != {"DJANIRA_POBRE"} or len(entradas) != 1 or
                dados.get("acoes", []) or dados.get("direcao", []) or dados.get("poses", []) or
                dados.get("transicoesPose", []) or dados.get("veiculos", []) or dados.get("portas", [])):
            _erro("objetos", "preview86 exige Djanira parada e um papel proprio tecnico")
        item86 = entradas[0]
        if item86.get("id") != "PAPEL_PROVA" or item86.get("recurso") not in paper86 or item86.get("eventos", []) or "apoioInicial" in item86:
            _erro("objetos", "preview86 exige um modelo literal sem eventos; nunca producao")
    exclusivo88 = any(isinstance(o, dict) and o.get("recurso") in RECURSOS_CAIXAS_TECNICAS88 for o in entradas)
    if dados.get("id") == "teste_fila_88" or exclusivo88:
        if (dados.get("id") != "teste_fila_88" or dados.get("episodio") != 0 or dados.get("numero") != 0 or
                dados.get("ambiente", {}).get("local") != "AEROPORTO_HANGAR" or
                set(atores) != {"DJANIRA_POBRE"} or len(entradas) != 2 or
                dados.get("acoes", []) or dados.get("direcao", []) or dados.get("poses", []) or
                dados.get("transicoesPose", []) or dados.get("veiculos", []) or dados.get("portas", [])):
            _erro("objetos", "caixas tecnicas: somente teste_fila_88/Hangar, Djanira parada e duas caixas sem outras acoes")
        if {o.get("id") for o in entradas if isinstance(o, dict)} != {"CAIXAS_NEGOCIO_1", "CAIXAS_NEGOCIO_2"}:
            _erro("objetos", "ensaio88 exige os dois codigos proprios literais")
        aliases = RECURSOS_CAIXAS_TECNICAS88 | {"caixas_madeira_candidata"}
        if any(o.get("recurso") not in aliases or not o.get("assentarNoChao", True) or
               not o.get("visivelInicial", True) or o.get("eventos", []) or
               any(k in o for k in ("attachmentInicial", "apoioInicial", "superficieInicial")) for o in entradas):
            _erro("objetos", "ensaio88 exige pares literais livres/visiveis/assentados sem eventos/attachment/apoio")
        if len({o["recurso"] for o in entradas}) != 1:
            _erro("objetos", "ensaio88 exige o mesmo recurso/modelo nas duas caixas")
    producao_c2 = any(isinstance(o, dict) and o.get("recurso") == "caixas_negocio_madeira_01a" for o in entradas)
    if producao_c2:
        if (dados.get("id") != "EP07_C02" or dados.get("episodio") != 7 or dados.get("numero") != 2 or
                duracao_cena != Decimal(16) or dados.get("ambiente", {}).get("local") != "GALPAO_CYPRESS" or
                set(atores) != {"O_CHEFE", "CAPANGA_1", "CAPANGA_2", "DJANIRA_POBRE"} or len(entradas) != 3 or
                dados.get("acoes", []) or dados.get("direcao", []) or dados.get("poses", []) or
                dados.get("transicoesPose", []) or dados.get("veiculos", []) or dados.get("portas", [])):
            _erro("objetos", "wood01a producao somente EP07_C02/GALPAO_CYPRESS/16s/ChefeCAP1CAP2Djanira/umcontainer+duascaixas")
        esperados_c2 = {"CONTAINER_CYPRESS": "container_serie", "CAIXAS_NEGOCIO_1": "caixas_negocio_madeira_01a", "CAIXAS_NEGOCIO_2": "caixas_negocio_madeira_01a"}
        if {o.get("id") for o in entradas if isinstance(o, dict)} != set(esperados_c2):
            _erro("objetos", "wood01a EP07_C02 exige tres codigos proprios literais")
        if any(not isinstance(o, dict) or o.get("recurso") != esperados_c2.get(o.get("id")) or not isinstance(o.get("marca"), dict) or
               o.get("marca", {}).get("altura") != 0 or o.get("rotacao", 0) != (90 if o.get("id") == "CONTAINER_CYPRESS" else 0) or
               not o.get("assentarNoChao", True) or not o.get("visivelInicial", True) or o.get("eventos", []) or
               any(k in o for k in ("attachmentInicial", "apoioInicial", "superficieInicial")) for o in entradas):
            _erro("objetos", "wood01a EP07_C02 pares literais livres/visiveis/chao sem altura/tilt/eventos/attachment/apoio")
    recursos = _recursos_objetos(pasta)
    ids, usados = set(), []
    for i, entrada in enumerate(entradas):
        campo = f"objetos[{i}]"
        _objeto(entrada, campo, ("id", "recurso", "marca"), ("rotacao", "assentarNoChao", "visivelInicial", "attachmentInicial", "eventos", "apoioInicial", "superficieInicial"))
        codigo = entrada["id"]
        _codigo(codigo, campo + ".id")
        if codigo in ids:
            _erro(campo + ".id", "objeto repetido")
        ids.add(codigo)
        recurso = entrada["recurso"]
        if not isinstance(recurso, str) or recurso not in recursos:
            _erro(campo + ".recurso", "use código minúsculo existente no catálogo de objetos")
        if recurso in ("bolsa_sono84_preview","sacola_papel_sono84_preview") and (
                dados.get("id") != "teste_fila_84" or dados.get("episodio") != 0 or dados.get("numero") != 0 or
                duracao_cena != Decimal(10) or len(entradas) != 2 or codigo != "SACOLA_CALIBRACAO84" or
                dados["ambiente"].get("local") != "CENTRO_LEGION" or set(atores) != {"DJANIRA_POBRE"} or
                entrada.get("assentarNoChao",True) or not entrada.get("visivelInicial",True) or
                "attachmentInicial" not in entrada or entrada.get("eventos",[]) or
                any(k in entrada for k in ("apoioInicial","superficieInicial"))):
            _erro(campo + ".recurso", "compact own handbag preview exclusive technical84/CENTRO/10s/Poor/sole bench plus bag; never production")
        if recurso == "papel_dobrado_autoral" and (
                dados.get("id") != "EP08_C05" or dados.get("episodio") != 8 or dados.get("numero") != 5 or
                duracao_cena != Decimal(12) or codigo != "PAPEL_IRACI" or len(entradas) != 3 or
                dados["ambiente"].get("local") != "CASA_VO_INTERIOR" or set(atores) != {"DONA_IRACI", "DJANIRA_POBRE"} or
                entrada.get("assentarNoChao", True) or not entrada.get("visivelInicial", True) or
                "attachmentInicial" not in entrada or any(k in entrada for k in ("apoioInicial", "superficieInicial"))):
            _erro(campo + ".recurso", "authored visual exclusive EP08_C05/CASA_VO_INTERIOR/PAPEL_IRACI/12s/two registered actors/three own props; proxy is not folded collider")
        if recurso == "mesa_grande_c8_autor" and (
                dados.get("id") != "EP15_C08" or dados.get("episodio") != 15 or dados.get("numero") != 8 or
                duracao_cena != Decimal(12) or codigo != "MESA_JANTAR" or len(entradas) != 2 or
                dados["ambiente"].get("local") != "MANSAO_CHEFE" or set(atores) not in ({"O_CHEFE"},{"DINJER"}) or
                not entrada.get("assentarNoChao",True) or not entrada.get("visivelInicial",True) or entrada.get("eventos",[]) or
                entrada.get("marca",{}).get("altura",0) != 0 or
                any(k in entrada for k in ("attachmentInicial","apoioInicial","superficieInicial"))):
            _erro(campo + ".recurso", "large table exclusive EP15_C08/MANSAO_CHEFE/12s/one Chefe-or-Dinjer/MESA_JANTAR+own chair-or-tray")
        if recurso == "banco_sono_c1_02" and (
                not ((dados.get("id") == "EP10_C01" and dados.get("episodio") == 10 and dados.get("numero") == 1) or
                     (dados.get("id") == "teste_fila_84" and dados.get("episodio") == 0 and dados.get("numero") == 0
                      and len(entradas) in (1,2) and dados["ambiente"].get("local") == "CENTRO_LEGION")) or
                duracao_cena != Decimal(10) or codigo != "BANCO_PRAIA" or
                dados["ambiente"].get("local") not in ("PRAIA_DELPERRO", "CENTRO_LEGION") or set(atores) != {"DJANIRA_POBRE"}):
            _erro(campo + ".recurso", "banco_sono_c1_02 exclusivo EP10_C01/PRAIA_DELPERRO-or-CENTRO_LEGION/DJANIRA_POBRE/BANCO_PRAIA/10s")
        if recurso == "celular" and any(acao["tipo"] == "celular" for acao in dados.get("direcao", [])):
            _erro(campo + ".recurso", "não combinar objeto celular com direção do tipo celular na mesma cena; isso duplicaria o telefone")
        apoio = recursos[recurso]["tipo"] == "apoio"
        usados.append(recursos[recurso])
        _marca(entrada["marca"], campo + ".marca")
        if "rotacao" in entrada:
            _numero(entrada["rotacao"], campo + ".rotacao", -180, 180)
        for nome in ("assentarNoChao", "visivelInicial"):
            if nome in entrada and type(entrada[nome]) is not bool:
                _erro(campo + "." + nome, "use booleano true ou false")
        if apoio and (not entrada.get("assentarNoChao", True) or not entrada.get("visivelInicial", True) or "attachmentInicial" in entrada):
            _erro(campo, "móvel de apoio exige chão, visibilidade inicial e nenhum attachment")
        if "apoioInicial" in entrada:
            _codigo(entrada["apoioInicial"], campo + ".apoioInicial")
            target = next((o for o in entradas if o.get("id") == entrada["apoioInicial"]), None)
            if (apoio or target is None or target["id"] == codigo or target["recurso"] not in APOIOS_OBJETOS or
                    entrada.get("assentarNoChao", True) or not entrada.get("visivelInicial", True) or "attachmentInicial" in entrada or
                    not target.get("assentarNoChao", True) or not target.get("visivelInicial", True) or "attachmentInicial" in target or
                    any(e.get("tipo") != "mostrar" for e in target.get("eventos", []))):
                _erro(campo, "initial support requires visible prop, own fixed support, no floor/attachment")
            if "superficieInicial" in entrada:
                _objeto(entrada["superficieInicial"], campo + ".superficieInicial", ("direita", "frente"))
                for axis in ("direita", "frente"):
                    _numero(entrada["superficieInicial"][axis], campo + ".superficieInicial." + axis, -10, 10)
        elif "superficieInicial" in entrada:
            _erro(campo, "superficieInicial requires apoioInicial")
        dono = _attachment_objeto(entrada["attachmentInicial"], campo + ".attachmentInicial", atores) if "attachmentInicial" in entrada else None
        mao_dono = entrada.get("attachmentInicial", {}).get("mao")
        pickup_supported = "apoioInicial" in entrada
        pickup_visible = entrada.get("visivelInicial", True)
        eventos = entrada.get("eventos", [])
        if not isinstance(eventos, list) or len(eventos) > 32:
            _erro(campo + ".eventos", "informe lista de 0 a 32 eventos")
        anterior = Decimal(-1)
        for j, evento in enumerate(eventos):
            evento_campo = f"{campo}.eventos[{j}]"
            _objeto(evento, evento_campo, ("tipo", "inicioSegundos"), ("personagem", "mao", "offset", "rotacao", "recurso", "esperaMaximaSegundos", "apoio", "superficie"))
            tipo = evento["tipo"]
            _enum(tipo, evento_campo + ".tipo", ("anexar", "transferir", "soltar", "ocultar", "mostrar", "guardar", "colocar", "pegar"))
            inicio = _numero(evento["inicioSegundos"], evento_campo + ".inicioSegundos", 0, 180, True)
            if inicio <= anterior or inicio >= duracao_cena:
                _erro(evento_campo + ".inicioSegundos", "eventos exigem tempos únicos crescentes antes do fim da cena; não prolongam a tomada")
            anterior = inicio
            espera = Decimal(0)
            if "esperaMaximaSegundos" in evento:
                if tipo not in ("transferir", "guardar", "colocar", "pegar"):
                    _erro(evento_campo, "espera só se aplica a transferir/guardar")
                espera = _numero(evento["esperaMaximaSegundos"], evento_campo + ".esperaMaximaSegundos", .001, 2, True)
                seguinte = _numero(eventos[j + 1].get("inicioSegundos"), evento_campo + ".proximo.inicioSegundos", 0, 180, True) if j + 1 < len(eventos) and isinstance(eventos[j + 1], dict) else duracao_cena
                if inicio + espera >= duracao_cena or inicio + espera >= seguinte:
                    _erro(evento_campo, "janela de contato deve terminar antes da próxima ação desse objeto e do fim da cena")
            if tipo in ("anexar", "transferir"):
                if apoio:
                    _erro(evento_campo, "móvel de apoio não pode ser anexado à mão")
                _objeto(evento, evento_campo, ("tipo", "inicioSegundos", "personagem", "mao", "offset", "rotacao"), ("esperaMaximaSegundos",) if tipo == "transferir" else ())
                novo = _attachment_objeto({k: evento[k] for k in ("personagem", "mao", "offset", "rotacao")}, evento_campo, atores)
                if tipo == "anexar" and dono is not None:
                    _erro(evento_campo, "anexar exige objeto sem dono")
                if tipo == "transferir" and (dono is None or dono == novo):
                    _erro(evento_campo, "transferir exige dono anterior diferente")
                if espera:
                    corpos = _corpos(pasta)
                    for participante in (dono, novo):
                        gesto = _gesto_objeto(dados, participante, inicio, espera, None, evento_campo)
                        par = (corpos[gesto["recurso"]]["dicionario"], corpos[gesto["recurso"]]["animacao"])
                        if par not in (("mp_common", "givetake1_a"), ("mp_common", "givetake1_b")):
                            _erro(evento_campo, "transferência exige os dois clipes givetake1 catalogados")
                dono = novo
                mao_dono = evento["mao"]
                pickup_supported = False
            elif tipo == "pegar":
                _objeto(evento, evento_campo, ("tipo", "inicioSegundos", "personagem", "mao", "recurso", "esperaMaximaSegundos"), ("offset", "rotacao"))
                _enum(evento["recurso"], evento_campo + ".recurso", ("entrega_a", "entrega_b"))
                _codigo(evento["personagem"], evento_campo + ".personagem")
                if evento["personagem"] not in atores:
                    _erro(evento_campo, "pegar exige ator proprio")
                _enum(evento["mao"], evento_campo + ".mao", ("direita", "esquerda"))
                for nome, limite, comprimento in (("offset", .5, True), ("rotacao", 180, False)):
                    if nome in evento:
                        _vetor_attachment(evento[nome], evento_campo + "." + nome, limite, comprimento)
                if apoio or dono is not None or not pickup_visible or not pickup_supported or not espera:
                    _erro(evento_campo, "pegar exige prop visivel sem dono em apoio inicial medido ou ultimo colocar")
                gesto = _gesto_objeto(dados, evento["personagem"], inicio, espera, evento["recurso"], evento_campo)
                corpo = _corpos(pasta)[gesto["recurso"]]
                if (corpo["dicionario"], corpo["animacao"]) not in (("mp_common", "givetake1_a"), ("mp_common", "givetake1_b")):
                    _erro(evento_campo, "pegar exige gesto real givetake catalogado")
                dono, mao_dono = evento["personagem"], evento["mao"]
                pickup_supported = False
            elif tipo == "colocar":
                _objeto(evento, evento_campo, ("tipo", "inicioSegundos", "personagem", "recurso", "apoio", "esperaMaximaSegundos"), ("mao", "superficie"))
                _codigo(evento["personagem"], evento_campo + ".personagem")
                _codigo(evento["apoio"], evento_campo + ".apoio")
                _enum(evento.get("mao", "direita"), evento_campo + ".mao", ("direita", "esquerda"))
                suporte = next((o for o in entradas if o.get("id") == evento["apoio"]), None)
                if (apoio or dono != evento["personagem"] or not espera or suporte is None or suporte["id"] == codigo or
                        suporte["recurso"] not in APOIOS_OBJETOS or evento.get("mao", "direita") != mao_dono or
                        not suporte.get("assentarNoChao", True) or not suporte.get("visivelInicial", True) or "attachmentInicial" in suporte):
                    _erro(evento_campo, "colocar exige objeto na mão do dono e apoio próprio imóvel/visível")
                gesto = _gesto_objeto(dados, dono, inicio, espera, evento["recurso"], evento_campo)
                corpo = _corpos(pasta)[gesto["recurso"]]
                if (corpo["dicionario"], corpo["animacao"]) not in (("mp_common", "givetake1_a"), ("mp_common", "givetake1_b")):
                    _erro(evento_campo, "colocar exige gesto real givetake catalogado")
                if "superficie" in evento:
                    _objeto(evento["superficie"], evento_campo + ".superficie", ("direita", "frente"))
                    for eixo in ("direita", "frente"):
                        _numero(evento["superficie"][eixo], evento_campo + ".superficie." + eixo, -10, 10)
                dono = None
                pickup_supported = True
            elif tipo == "guardar":
                _objeto(evento, evento_campo, ("tipo", "inicioSegundos", "personagem", "recurso", "esperaMaximaSegundos"))
                _codigo(evento["personagem"], evento_campo + ".personagem")
                if recurso != "envelope" or evento["personagem"] != dono or not espera:
                    _erro(evento_campo, "guardar exige envelope do próprio dono e esperaMaximaSegundos limitada")
                gesto = _gesto_objeto(dados, dono, inicio, espera, evento["recurso"], evento_campo)
                corpo = _corpos(pasta)[gesto["recurso"]]
                if (corpo["dicionario"], corpo["animacao"]) != ("mp_common_miss", "put_away_coke"):
                    _erro(evento_campo, "guardar exige o clipe candidato put_away_coke conferido")
                dono = None
                pickup_supported = False
            else:
                _objeto(evento, evento_campo, ("tipo", "inicioSegundos"))
                if tipo == "soltar":
                    if dono is None:
                        _erro(evento_campo, "soltar exige dono anterior")
                    dono = None
                    pickup_supported = False
                if tipo == "ocultar":
                    pickup_visible = False
                if tipo == "mostrar":
                    pickup_visible = True
    _validar_maos_pegar(entradas, duracao_cena)
    try:
        if usados:
            _fontes_corpos(usados, "PropList.txt", True)
    except ErroCena as erro:
        _erro("objetos.recurso", str(erro))


def _validar_maos_pegar(entradas, duracao_cena):
    spans, has_pickup = [], False
    for entry in entradas:
        attachment = entry.get("attachmentInicial")
        current = None if attachment is None else (attachment["personagem"], attachment["mao"], Decimal(0), False)
        for event in entry.get("eventos", []):
            kind = event["tipo"]
            start = Decimal(str(event["inicioSegundos"]))
            acquire = kind in ("anexar", "transferir", "pegar")
            if current is not None and (acquire or kind in ("colocar", "guardar", "soltar")):
                spans.append((entry["id"], *current, start + Decimal(str(event.get("esperaMaximaSegundos", 0)))))
                current = None
            if acquire:
                current = (event["personagem"], event["mao"], start, kind == "pegar")
                has_pickup = has_pickup or kind == "pegar"
        if current is not None:
            spans.append((entry["id"], *current, duracao_cena))
    if not has_pickup:
        return
    for index, a in enumerate(spans):
        for b in spans[index + 1:]:
            if (a[0] != b[0] and a[1:3] == b[1:3]
                    and a[3] < b[5] and b[3] < a[5]):
                _erro("objetos.eventos.pegar", "pegar conflita com outro prop na mesma mao: " + a[0] + "/" + b[0])


def _gesto_objeto(dados, ator, inicio, espera, recurso, campo):
    for gesto in dados.get("direcao", []):
        if (gesto["personagem"] == ator and gesto["tipo"] == "clipe"
                and (gesto.get("sobrePoseSentada", False)
                     or gesto["recurso"] in ("entrega_a", "entrega_b"))
                and (recurso is None or gesto["recurso"] == recurso)
                and Decimal(str(gesto["inicioSegundos"])) <= inicio
                and Decimal(str(gesto["inicioSegundos"])) + Decimal(str(gesto["duracaoSegundos"])) >= inicio + espera):
            return gesto
    _erro(campo, "a janela inteira exige gesto de entrega catalogado ou gesto sentado compatível declarado para esse ator")


def _poses(dados, pasta, atores):
    if "poses" not in dados:
        if any(acao.get("sobrePoseSentada", False) for acao in dados.get("direcao", [])):
            _erro("direcao.sobrePoseSentada", "exige pose sentada declarada para esse ator")
        return
    entradas = dados["poses"]
    if not isinstance(entradas, list) or len(entradas) > 5:
        _erro("poses", "informe lista de 0 a 5 poses iniciais")
    if not entradas:
        if any(acao.get("sobrePoseSentada", False) for acao in dados.get("direcao", [])):
            _erro("direcao.sobrePoseSentada", "exige pose sentada declarada para esse ator")
        return
    corpos = _corpos(pasta)
    objetos = {objeto["id"]: objeto for objeto in dados.get("objetos", [])}
    usados, definidos = [], set()
    for i, entrada in enumerate(entradas):
        campo = f"poses[{i}]"
        _objeto(entrada, campo, ("personagem", "objeto", "recurso", "tipo"), ("superficie", "rotacao", "abracarObjeto"))
        ator = entrada["personagem"]
        _codigo(ator, campo + ".personagem")
        if ator not in atores:
            _erro(campo + ".personagem", "ator não pertence ao elenco")
        if ator in definidos:
            _erro(campo + ".personagem", "um ator admite somente uma pose inicial")
        definidos.add(ator)
        fim_saida = next((t["inicioSegundos"] + t["duracaoSegundos"] for t in dados.get("transicoesPose", []) if t["personagem"] == ator and t["tipo"] == "levantar"), Decimal(181))
        if any(acao["personagem"] == ator and acao["inicioSegundos"] < fim_saida for acao in dados.get("acoes", [])):
            _erro(campo + ".personagem", "pose inicial não pode receber ações legadas no mesmo ator")
        objeto_id = entrada["objeto"]
        if entrada["tipo"] == "chao":
            if objeto_id is not None or "superficie" in entrada:
                _erro(campo + ".objeto", "chão usa objeto=null e piso real da marca, sem superfície manual")
        else:
            _codigo(objeto_id, campo + ".objeto")
            if objeto_id not in objetos:
                _erro(campo + ".objeto", "declare esse objeto próprio em objetos")
            apoio = objetos[objeto_id]
            if (apoio["recurso"] not in APOIOS_OBJETOS or not apoio.get("assentarNoChao", True)
                    or not apoio.get("visivelInicial", True) or "attachmentInicial" in apoio):
                _erro(campo + ".objeto", "pose exige móvel de apoio próprio, assentado, visível e sem attachment")
            if any(evento["tipo"] != "mostrar" for evento in apoio.get("eventos", [])):
                _erro(campo + ".objeto", "o apoio da pose não pode receber evento que o mova ou oculte")
        recurso = entrada["recurso"]
        if not isinstance(recurso, str) or recurso not in POSES:
            _erro(campo + ".recurso", "use uma das seis poses canônicas do helper")
        if recurso == "office_cell_sleep_f" and (
                dados.get("id") != "teste_fila_84" or dados.get("episodio") != 0 or dados.get("numero") != 0
                or set(atores) != {"DJANIRA_POBRE"} or ator != "DJANIRA_POBRE" or len(entradas) != 1
                or (len(objetos) != 1 and not (len(objetos) == 2 and any(p.get("id") == "SACOLA_CALIBRACAO84" and p.get("recurso") in ("bolsa_sono84_preview","sacola_papel_sono84_preview") for p in objetos.values()))) or objeto_id != "BANCO_PRAIA" or apoio is None or apoio["recurso"] != "banco_sono_c1_02"
                or entrada["tipo"] != "deitada" or entrada.get("rotacao") != 90 or "abracarObjeto" in entrada):
            _erro(campo + ".recurso", "office_cell_sleep_f somente pose tecnica84/bench02/Djanira, optional exact compact preview, never production")
        if recurso == "sono_banco_tracy_f" and (
                dados.get("id") != "EP10_C01" or set(atores) != {"DJANIRA_POBRE"} or ator != "DJANIRA_POBRE"
                or len(entradas) != 1 or objeto_id != "BANCO_PRAIA" or apoio is None or apoio["recurso"] != "banco_sono_c1_02"
                or entrada["tipo"] != "deitada" or entrada.get("abracarObjeto") != "SACOLA_DJANIRA"):
            _erro(campo + ".recurso", "Tracy sleep loop exclusive EP10_C01 measured bench02/Djanira/bag")
        tipo, dicionario, clipe = POSES[recurso]
        _enum(entrada["tipo"], campo + ".tipo", ("sentada", "deitada", "chao"))
        if entrada["tipo"] != tipo and not (entrada["tipo"] == "chao" and recurso == "sentada_meio_fio_f"):
            _erro(campo + ".tipo", "o tipo diverge do recurso de pose")
        if "abracarObjeto" in entrada:
            abracado_id = entrada["abracarObjeto"]
            _codigo(abracado_id, campo + ".abracarObjeto")
            abracado = objetos.get(abracado_id)
            attachment = abracado.get("attachmentInicial", {}) if abracado is not None else {}
            if (recurso not in ("sono_f", "sono_banco_tracy_f") or entrada["tipo"] != "deitada" or abracado_id == objeto_id
                    or abracado is None or abracado["recurso"] != "sacola"
                    or not abracado.get("visivelInicial", True) or attachment.get("personagem") != ator
                    or attachment.get("mao", "direita") not in ("direita", "esquerda") or abracado.get("eventos", [])):
                _erro(campo + ".abracarObjeto", "sono_f deitada exige sacola própria visível, anexada ao mesmo ator, sem eventos")
        origem = corpos.get(recurso)
        if (origem is None or origem["tipo"] != "clipe" or origem["dicionario"] != dicionario
                or origem["animacao"] != clipe):
            _erro(campo + ".recurso", "o catálogo deve conservar o par literal da pose canônica")
        usados.append(origem)
        for acao in dados.get("direcao", []):
            if acao["personagem"] != ator or acao["inicioSegundos"] >= fim_saida:
                continue
            corpo = corpos.get(acao.get("recurso"), {})
            if (not acao.get("sobrePoseSentada", False) or recurso not in POSES_BRACOS_LIVRES or
                    (corpo.get("dicionario"), corpo.get("animacao")) not in GESTOS_SENTADOS):
                _erro(campo + ".personagem", "pose inicial não pode receber direção corporal fora do gesto sentado compatível")
        if "superficie" in entrada:
            superficie = entrada["superficie"]
            _objeto(superficie, campo + ".superficie", ("direita", "frente"))
            for eixo in ("direita", "frente"):
                _numero(superficie[eixo], campo + ".superficie." + eixo, -10, 10)
        if "rotacao" in entrada:
            _numero(entrada["rotacao"], campo + ".rotacao", -180, 180)
    for acao in dados.get("direcao", []):
        if acao.get("sobrePoseSentada", False) and acao["personagem"] not in definidos:
            _erro("direcao.sobrePoseSentada", "exige pose sentada declarada para esse ator")
    try:
        _fontes_corpos(usados, require_menyoo=True)
    except ErroCena as erro:
        _erro("poses.recurso", str(erro))


def _transicoes_pose(dados, pasta, atores, falas, intervalos, duracao):
    entradas = dados.get("transicoesPose", [])
    if not isinstance(entradas, list) or len(entradas) > 5:
        _erro("transicoesPose", "informe de0 a5saídas de poses próprias")
    if not entradas:
        return duracao
    bases = {p["personagem"]: p for p in dados.get("poses", [])}
    corpos, usados, vistos = _corpos(pasta), [], set()
    for i, entrada in enumerate(entradas):
        campo = f"transicoesPose[{i}]"
        _objeto(entrada, campo, ("personagem", "tipo", "recurso", "inicioSegundos", "duracaoSegundos"))
        ator, recurso = entrada["personagem"], entrada["recurso"]
        _codigo(ator, campo + ".personagem")
        if ator not in atores or ator in vistos:
            _erro(campo, "uma saída por ator próprio")
        vistos.add(ator)
        _enum(entrada["tipo"], campo + ".tipo", ("levantar", "sentar"))
        if recurso.startswith("sentar_") != (entrada["tipo"] == "sentar"):
            _erro(campo, "tipo/recurso da transi??o divergem")
        if recurso not in TRANSICOES_POSE:
            _erro(campo + ".recurso", "use saída canônica de cadeira/chão")
        base, dicionario, clipe = TRANSICOES_POSE[recurso]
        if bases.get(ator, {}).get("recurso") != base:
            _erro(campo, "a saída exige base inicial correspondente do mesmo ator")
        origem = corpos.get(recurso)
        if not origem or origem["tipo"] != "clipe" or (origem["dicionario"], origem["animacao"]) != (dicionario, clipe):
            _erro(campo + ".recurso", "catálogo deve conservar o par literal real")
        usados.append(origem)
        inicio = _numero(entrada["inicioSegundos"], campo + ".inicioSegundos", 0, 180, True)
        fim = inicio + _numero(entrada["duracaoSegundos"], campo + ".duracaoSegundos", .5, 8, True)
        if fim > 180:
            _erro(campo, "saída deve terminar até180s")
        if any(ator == falante and inicio < b and fim > a for falante, a, b in falas):
            _erro(campo, "saída primária exige intervalo sem fala do próprio ator")
        if any(inicio < b and fim > a for a, b in intervalos.setdefault(ator, [])):
            _erro(campo, "saída sobrepõe outra direção do mesmo ator")
        intervalos[ator].append((inicio, fim))
        duracao = max(duracao, fim)
    _fontes_corpos(usados, require_menyoo=True)
    return duracao


def _marca_veiculo(valor, campo):
    _objeto(valor, campo, ("direita", "frente", "altura"))
    xy = [_numero(valor[k], campo + "." + k, -150, 150) for k in ("direita", "frente")]
    _numero(valor["altura"], campo + ".altura", -60, 60)
    if sum(x*x for x in xy) > 22500:
        _erro(campo, "marca fora do raio de150m")
    return xy


def _veiculos(dados, pasta, atores, falas, intervalos, duracao):
    entradas = dados.get("veiculos", [])
    if not isinstance(entradas, list) or len(entradas) > 2:
        _erro("veiculos", "informe de0 a2veículos próprios")
    if not entradas:
        return duracao
    catalogo = _ler(pasta / "veiculos_serie_candidatos.json")
    _objeto(catalogo, "catalogos.veiculos", ("versao", "tipoDocumento", "estado", "pronto", "fonteDLL", "observacao", "veiculos"), ("limites", "motoristaExtra"))
    _inteiro(catalogo["versao"], "catalogos.veiculos.versao", 1, 1)
    recursos = {}
    for item in catalogo["veiculos"]:
        _objeto(item, "catalogos.veiculos.item", ("codigo", "modelo", "tipo", "pronto", "estado", "origem"))
        code = item["codigo"]
        if code in recursos or code not in VEICULOS or (item["tipo"], item["modelo"]) != VEICULOS[code]:
            _erro("catalogos.veiculos", "modelo/tipo/código deve conservar whitelist literal")
        recursos[code] = item
    ids, ocupantes, operacoes = set(), set(), []
    for i, entrada in enumerate(entradas):
        campo = f"veiculos[{i}]"
        _objeto(entrada, campo, ("id", "recurso", "marca", "acoes"), ("rotacao", "motoristaExtra", "trajetoDireto", "assentosIniciais"))
        code = entrada["id"]
        _codigo(code, campo + ".id")
        if code in ids or entrada["recurso"] not in recursos:
            _erro(campo, "ID único e recurso terrestre existente são necessários")
        ids.add(code)
        antes = _marca_veiculo(entrada["marca"], campo + ".marca")
        if "rotacao" in entrada:
            _numero(entrada["rotacao"], campo + ".rotacao", -180, 180)
        for flag in ("motoristaExtra", "trajetoDireto"):
            if flag in entrada and type(entrada[flag]) is not bool:
                _erro(campo + "." + flag, "use booleano")
        assentos = entrada.get("assentosIniciais", [])
        if not isinstance(assentos, list) or len(assentos) > 5:
            _erro(campo + ".assentosIniciais", "até5ocupantes iniciais próprios")
        numeros = set()
        for seat in assentos:
            _objeto(seat, campo + ".assentosIniciais", ("personagem", "assento"))
            actor, n = seat["personagem"], seat["assento"]
            _codigo(actor, campo + ".assentosIniciais.personagem")
            _inteiro(n, campo + ".assentosIniciais.assento", -1, 15)
            if actor not in atores or actor in ocupantes or n in numeros or (n == -1 and entrada.get("motoristaExtra", False)):
                _erro(campo, "ator/assento inicial inválido, duplicado ou reservado ao motorista extra")
            if any(p["personagem"] == actor for p in dados.get("poses", [])):
                _erro(campo, "ocupante inicial não pode ter pose de apoio/chão")
            ocupantes.add(actor); numeros.add(n)
        acoes = entrada["acoes"]
        if not isinstance(acoes, list) or len(acoes) > 32:
            _erro(campo + ".acoes", "até32operações por cena")
        for acao in acoes:
            _objeto(acao, campo + ".acoes", ("tipo", "personagem", "inicioSegundos", "duracaoSegundos"), ("assento", "rota", "velocidade", "raioChegada", "raioParada"))
            tipo, ator = acao["tipo"], acao["personagem"]
            _enum(tipo, campo + ".acoes.tipo", ("entrar", "sair", "dirigir"))
            if ator is None:
                if not entrada.get("motoristaExtra", False):
                    _erro(campo, "personagem=null exige motoristaExtra")
            else:
                _codigo(ator, campo + ".acoes.personagem")
                if ator not in atores:
                    _erro(campo, "ator da operação não pertence ao elenco")
            inicio = _numero(acao["inicioSegundos"], campo + ".acoes.inicioSegundos", 0, 180, True)
            fim = inicio + _numero(acao["duracaoSegundos"], campo + ".acoes.duracaoSegundos", .25, 60, True)
            if fim > 180 or len(operacoes) >= 32:
                _erro(campo, "até32operações que terminam até180s")
            if "assento" in acao:
                _inteiro(acao["assento"], campo + ".acoes.assento", -1, 15)
            if tipo == "dirigir":
                rota = acao.get("rota")
                if not isinstance(rota, list) or not 1 <= len(rota) <= 16:
                    _erro(campo + ".acoes.rota", "dirigir exige1–16waypoints relativos")
                if acao.get("assento", -1) != -1:
                    _erro(campo, "dirigir usa assento motorista=-1")
                _numero(acao.get("velocidade", 8), campo + ".acoes.velocidade", 1, 20)
                raio = _numero(acao.get("raioChegada", 1.5), campo + ".acoes.raioChegada", .5, 3)
                if "raioParada" in acao:
                    _numero(acao["raioParada"], campo + ".acoes.raioParada", .5, raio)
                for point in rota:
                    xy = _marca_veiculo(point, campo + ".acoes.rota")
                    if sum((x-y)**2 for x,y in zip(xy, antes)) > 6400:
                        _erro(campo + ".acoes.rota", "subdivida trechos em até80m")
                    antes = xy
            elif any(k in acao for k in ("rota", "velocidade", "raioChegada", "raioParada")):
                _erro(campo, "entrar/sair não aceita rota/velocidade/raios")
            if ator is not None:
                if any(ator == who and inicio < b and fim > a for who,a,b in falas):
                    _erro(campo, "operação de veículo coincide com fala do ator")
                if any(inicio < b and fim > a for a,b in intervalos.setdefault(ator, [])):
                    _erro(campo, "operação de veículo coincide com direção do ator")
                intervalos[ator].append((inicio,fim))
            for old_code, old_actor, old_tipo, a,b in operacoes:
                if inicio < b and fim > a and ((ator is not None and old_actor == ator) or (old_code == code and (tipo == "dirigir" or old_tipo == "dirigir"))):
                    _erro(campo, "operações conflitantes no mesmo ator/veículo")
            operacoes.append((code,ator,tipo,inicio,fim))
            duracao = max(duracao,fim)
    return duracao


def _portas(dados, pasta, atores, falas, intervalos, duracao):
    entradas = dados.get("portas", [])
    if not isinstance(entradas, list) or len(entradas) > 16:
        _erro("portas", "informe de0 a16opera??es reais")
    usados, corpos, operacoes = [], None, []
    poses = {p["personagem"]: p for p in dados.get("poses", [])}
    saidas = {p["personagem"]: Decimal(str(p["inicioSegundos"])) + Decimal(str(p["duracaoSegundos"])) for p in dados.get("transicoesPose", [])}
    for i, entrada in enumerate(entradas):
        campo = f"portas[{i}]"
        _objeto(entrada, campo, ("personagem", "tipo", "inicioSegundos", "duracaoSegundos"), ("batidas", "recurso", "destino", "velocidade"))
        tipo, ator = entrada["tipo"], entrada["personagem"]
        _enum(tipo, campo + ".tipo", ("bater", "abrir", "fechar", "atravessar"))
        if ator is not None or tipo not in ("abrir", "fechar"):
            _codigo(ator, campo + ".personagem")
            if ator not in atores:
                _erro(campo, "porta exige ator pr?prio; null s? abrir/fechar")
        inicio = _numero(entrada["inicioSegundos"], campo + ".inicioSegundos", 0, 180, True)
        fim = inicio + _numero(entrada["duracaoSegundos"], campo + ".duracaoSegundos", .1, 30, True)
        if fim > 180:
            _erro(campo, "porta termina depois de180s")
        if tipo == "bater":
            _objeto(entrada, campo, ("personagem", "tipo", "inicioSegundos", "duracaoSegundos", "batidas", "recurso"))
            _inteiro(entrada["batidas"], campo + ".batidas", 3, 3)
            _enum(entrada["recurso"], campo + ".recurso", ("bater_porta",))
            if corpos is None:
                corpos = _corpos(pasta)
            origem = corpos.get("bater_porta")
            if not origem or origem["tipo"] != "clipe" or (origem["dicionario"], origem["animacao"]) != ("timetable@jimmy@doorknock@", "knockdoor_idle"):
                _erro(campo, "batida exige par literal instalado")
            usados.append(origem)
        elif tipo == "atravessar":
            _objeto(entrada, campo, ("personagem", "tipo", "inicioSegundos", "duracaoSegundos", "destino"), ("velocidade",))
            _marca(entrada["destino"], campo + ".destino")
            if "velocidade" in entrada:
                _numero(entrada["velocidade"], campo + ".velocidade", .2, 2)
        else:
            _objeto(entrada, campo, ("personagem", "tipo", "inicioSegundos", "duracaoSegundos"))
        if ator is not None:
            if any(inicio < b and fim > a for a,b in intervalos.setdefault(ator, [])):
                _erro(campo, "porta sobrep?e a??o corporal/ve?culo do pr?prio ator")
            if ator in poses and saidas.get(ator, Decimal(181)) > inicio:
                _erro(campo, "porta exige levantar da pose antes de come?ar")
            if tipo in ("bater", "atravessar") and any(ator == who and inicio < b and fim > a for who,a,b in falas):
                _erro(campo, "batida/passagem exige intervalo sem fala do pr?prio ator")
            intervalos[ator].append((inicio,fim))
        if any(old == ator and inicio < b and fim > a for old,a,b in operacoes):
            _erro(campo, "portas simult?neas do mesmo ator/?ncora")
        operacoes.append((ator,inicio,fim))
        duracao = max(duracao,fim)
    _fontes_corpos(usados, require_menyoo=True)
    return duracao


def _hash_morph86(caminho, maximo):
    from runtime_diretor import exigir_sem_vinculos, ErroRuntime
    try:
        caminho = exigir_sem_vinculos(Path(caminho))
        raw = caminho.read_bytes()
    except (OSError, ErroRuntime) as exc:
        _erro("trial_morph86.fonte", str(exc))
    if not 0 < len(raw) <= maximo:
        _erro("trial_morph86.fonte", "tamanho da fonte/sidecar fora do limite")
    return hashlib.sha256(raw).hexdigest().upper()


def _morfologia_papel86(caminho, pasta, dados, hash_antes):
    # Documento separado fixo: nenhuma chave nova e aceita nas cenas de producao.
    if dados["id"] != "teste_fila_86":
        return None
    sidecar = pasta / "trial_morph86.json"
    if not sidecar.exists():
        if hash_antes is not None:
            _erro("trial_morph86", "sidecar removido durante parse")
        return None
    props = dados.get("objetos", [])
    if len(props) != 1:
        _erro("trial_morph86", "somente PAPEL_PROVA nota_s proprio")
    prop = props[0]
    attachment = prop.get("attachmentInicial", {})
    if (hash_antes is None or prop.get("id") != "PAPEL_PROVA" or
        prop.get("recurso") not in ("papel_preview86_nota_s", "papel_preview86_pamphlet_s") or
        prop.get("assentarNoChao") is not False or prop.get("visivelInicial") is not True or
        prop.get("eventos") != [] or prop.get("apoioInicial") not in (None, "") or
        attachment.get("personagem") != "DJANIRA_POBRE" or attachment.get("mao") != "direita" or
        any(Decimal(str(value)) != 0 for key in ("offset", "rotacao") for value in attachment[key].values())):
        _erro("trial_morph86", "somente fonte86 nota_s propria e zero transform de mao")
    hash_sidecar = _hash_morph86(sidecar, 4096)
    raw = _ler(sidecar)
    _objeto(raw, "trial_morph86", ("versao", "id", "cena", "fonteSHA256", "segmento", "fase"))
    _inteiro(raw["versao"], "trial_morph86.versao", 1, 1)
    if not isinstance(raw["id"], str) or re.fullmatch(r"[0-9a-f]{32}", raw["id"]) is None:
        _erro("trial_morph86.id", "GUID32 minusculo unico exigido")
    if raw["cena"] != "teste_fila_86":
        _erro("trial_morph86.cena", "somenteID86 literal")
    source_hash = raw["fonteSHA256"]
    if (not isinstance(source_hash, str) or re.fullmatch(r"[0-9A-Fa-f]{64}", source_hash) is None or
        source_hash.upper() != hash_antes or source_hash.upper() != _hash_morph86(caminho, 1048576) or
        hash_sidecar != _hash_morph86(sidecar, 4096)):
        _erro("trial_morph86.fonteSHA256", "source/sidecar mudaram ou nao correspondem ao snapshot86")
    pamphlet = prop["recurso"] == "papel_preview86_pamphlet_s"
    _inteiro(raw["segmento"], "trial_morph86.segmento", 0, 3 if pamphlet else 6)
    _numero(raw["fase"], "trial_morph86.fase", 0, 1)
    source = Path(_recursos_objetos(pasta)[prop["recurso"]]["origem"]["arquivo"]).with_name("PedAnimList.txt")
    if _hash_morph86(source, 64 * 1024 * 1024) != "2C537060BD97FCF0DACA407DB1436A5D54E4B8DD5274DBC9EF0D8466EF6F21E4":
        _erro("trial_morph86.animSource", "hash da animlist real diverge da fonte primaria")
    lines = source.read_text("utf-8-sig").splitlines()
    origin_lines = (43912, 43922, 43929, 43935) if pamphlet else (24838, 24843, 24846, 24850, 24854, 24858, 24860)
    segment = raw["segmento"]
    expected = (f"ep_1_rcm_concat-{segment} p_cs_pamphlet_01_s-{segment}" if pamphlet else
                f"family_5_mcs_5_p4-{segment} p_amanda_note_01_s-{segment}")
    if lines[origin_lines[segment] - 1].strip() != expected:
        _erro("trial_morph86.animSource", "track literal diverge da linha primaria Menyoo")
    return {"id": raw["id"], "segmento": segment, "fase": raw["fase"],
            "sourceSHA256": source_hash.upper(), "sidecarSHA256": hash_sidecar,
            "NoNativeExecuted": True, "FoldApproved": False, "ProductionApproval": False}


def validar_cena_json(caminho: Path, catalogos: Path | None = None) -> dict:
    caminho = Path(caminho)
    morph_pasta = Path(catalogos) if catalogos is not None else RAIZ / "catalogos"
    hash_morph86_antes = (_hash_morph86(caminho, 1048576) if caminho.name == "teste_fila_86.json" and
                         (morph_pasta / "trial_morph86.json").exists() else None)
    dados = _ler(caminho)
    _objeto(dados, "Cena", ("versao", "id", "episodio", "numero", "nome", "ambiente", "elenco", "camera", "roteiro"), ("acoes", "encenacao", "direcao", "interpretacao", "objetos", "poses", "transicoesPose", "veiculos", "portas", "saidaAgua", "empurraoAviao", "armarioModular"))
    _inteiro(dados["versao"], "versao", 1, 1)
    _inteiro(dados["episodio"], "episodio", 0, 15)
    _inteiro(dados["numero"], "numero", 0, 99)
    _texto(dados["nome"], "nome", 100)
    identificador = dados["id"]
    if isinstance(identificador, str) and (ID_TESTE.fullmatch(identificador) or identificador == "VINHETA_01"):
        if dados["episodio"] != 0 or dados["numero"] != 0:
            _erro("id", "cena técnica exige episodio=0 e numero=0")
    elif isinstance(identificador, str) and identificador in IDS_SUPLEMENTARES:
        if dados["episodio"] != 1 or dados["numero"] != 0:
            _erro("id", "extra autorizado exige episodio=1 e numero=0")
    else:
        match = ID_PRODUCAO.fullmatch(identificador) if isinstance(identificador, str) else None
        if match is None or (int(match[1]), int(match[2])) != (dados["episodio"], dados["numero"]):
            _erro("id", "use teste_json, teste_fila_NN (01–99), VINHETA_01 ou EPnn_Cnn correspondente a episodio 01–15 e numero 01–99")
    if caminho.name != identificador + ".json":
        _erro("arquivo", "nome deve ser igual ao id + .json")
    encenacao = dados.get("encenacao", "")
    if "encenacao" in dados:
        _enum(encenacao, "encenacao", ("pedido_v1", "ensaio_coreografia_v1", "serie_v1"))
    if encenacao == "serie_v1" and "direcao" not in dados:
        _erro("direcao", "serie_v1 exige a lista de direção, mesmo vazia")
    if encenacao != "serie_v1" and "direcao" in dados:
        _erro("direcao", "permitida somente com encenacao=serie_v1")
    if encenacao != "serie_v1" and "interpretacao" in dados:
        _erro("interpretacao", "permitida somente com encenacao=serie_v1")
    if encenacao != "serie_v1" and "objetos" in dados:
        _erro("objetos", "permitida somente com encenacao=serie_v1")
    if encenacao != "serie_v1" and "poses" in dados:
        _erro("poses", "permitida somente com encenacao=serie_v1")
    if encenacao != "serie_v1" and any(k in dados for k in ("transicoesPose", "veiculos", "portas", "saidaAgua", "empurraoAviao", "armarioModular")):
        _erro("encenacao", "veículos/transições exigem serie_v1")
    if identificador == "teste_coreografia" and encenacao != "ensaio_coreografia_v1":
        _erro("encenacao", "teste_coreografia exige ensaio_coreografia_v1")
    pasta = Path(catalogos) if catalogos is not None else RAIZ / "catalogos"
    personagens = _catalogo(pasta, "personagens.json", "personagens")
    for codigo, entrada in personagens.items():
        _metadados_visual(entrada, codigo, "personagens." + codigo)
    locais = _catalogo(pasta, "locais.json", "locais")
    animacoes = _catalogo(pasta, "animacoes.json", "animacoes")
    canonicos = _gestos_canonicos(pasta)
    ambiente = dados["ambiente"]
    _objeto(ambiente, "ambiente", ("local", "hora", "clima"), ("raioLimpeza", "distanciaFrente"))
    _codigo(ambiente["local"], "ambiente.local")
    if ambiente["local"] not in locais:
        _erro("ambiente.local", "código ausente do catálogo de locais")
    hora = ambiente["hora"]
    if not isinstance(hora, str) or re.fullmatch(r"(?:[01][0-9]|2[0-3]):[0-5][0-9]", hora) is None:
        _erro("ambiente.hora", "use HH:mm, de 00:00 a 23:59")
    _enum(ambiente["clima"], "ambiente.clima", CLIMAS)
    for campo, minimo, maximo in (("raioLimpeza", 5, 100), ("distanciaFrente", 1, 10)):
        if campo in ambiente:
            _numero(ambiente[campo], "ambiente." + campo, minimo, maximo)
    elenco = dados["elenco"]
    if not isinstance(elenco, list) or len(elenco) > 5:
        _erro("elenco", "informe lista de 0 a 5 atores")
    atores = []
    for i, ator in enumerate(elenco):
        campo = f"elenco[{i}]"
        _objeto(ator, campo, ("personagem", "marca"), ("olharPara", "rotacao"))
        if "rotacao" in ator:
            _numero(ator["rotacao"], campo + ".rotacao", -180, 180)
        codigo = ator["personagem"]
        _codigo(codigo, campo + ".personagem")
        if codigo not in personagens or codigo in atores:
            _erro(campo + ".personagem", "personagem ausente do catálogo ou repetido no elenco")
        entrada = personagens[codigo]
        if entrada.get("pronto") is not True:
            _erro(campo + ".personagem", "personagem ainda não pronto; preservar aprovação visual pendente")
        modelo = entrada.get("modelo")
        if not isinstance(modelo, str) or MODELO.fullmatch(modelo) is None:
            _erro(campo + ".personagem", "modelo inválido no catálogo")
        _texto(entrada.get("nome"), campo + ".nomeCatalogo", 100)
        _visual_pronto(entrada, codigo, pasta, campo + ".personagem")
        atores.append(codigo)
        _marca(ator["marca"], campo + ".marca")
    for i, ator in enumerate(elenco):
        for anterior in elenco[:i]:
            marca, outra = ator["marca"], anterior["marca"]
            if abs(Decimal(str(marca["direita"])) - Decimal(str(outra["direita"]))) < Decimal("0.5") and abs(Decimal(str(marca["frente"])) - Decimal(str(outra["frente"]))) < Decimal("0.5"):
                _erro(f"elenco[{i}].marca", "atores muito próximos; separe ao menos 0.5 m em direita ou frente")
    for i, ator in enumerate(elenco):
        olhar = ator.get("olharPara")
        if olhar is not None and (not isinstance(olhar, str) or olhar not in atores or olhar == ator["personagem"]):
            _erro(f"elenco[{i}].olharPara", "escolha outro ator do elenco")
    camera = dados["camera"]
    _objeto(camera, "camera", ("modo", "planoInicial", "personagem"),
            ("planoFala", "planoNarracao", "planoFinal", "inverterEixo", "transicaoSuave", "ajustes", "cortes", "objeto", "fovObjeto", "elevacaoObjetoGraus"))
    _enum(camera["modo"], "camera.modo", ("automatico", "programado"))
    for campo in ("planoInicial", "planoFala", "planoNarracao", "planoFinal"):
        if campo in camera:
            _plano_cena(camera[campo], "camera." + campo, atores)
    _alvo_camera(camera["personagem"], "camera.personagem", atores)
    if "objeto" in camera:
        _codigo(camera["objeto"], "camera.objeto")
        if camera["modo"] != "automatico":
            _erro("camera.objeto", "detalhe de objeto ocupa a tomada inteira no modo automatico")
    elif "fovObjeto" in camera or "elevacaoObjetoGraus" in camera:
        _erro("camera.objeto", "fov/elevacao do detalhe exigem objeto próprio declarado")
    for nome, minimo, maximo in (("fovObjeto", 15, 55), ("elevacaoObjetoGraus", 10, 80)):
        if nome in camera:
            _numero(camera[nome], "camera." + nome, minimo, maximo)
    for campo in ("inverterEixo", "transicaoSuave"):
        if campo in camera and type(camera[campo]) is not bool:
            _erro("camera." + campo, "use booleano true ou false")
    if "ajustes" in camera:
        _objeto(camera["ajustes"], "camera.ajustes", (), ("lado", "altura", "aproximar"))
        for campo, valor in camera["ajustes"].items():
            _numero(valor, "camera.ajustes." + campo, -2, 2)
    roteiro = dados["roteiro"]
    if not isinstance(roteiro, list) or len(roteiro) > 20:
        _erro("roteiro", "informe lista de 0 a 20 trechos")
    cursor = Decimal(0)
    falas = []
    for i, trecho in enumerate(roteiro):
        campo = f"roteiro[{i}]"
        if not isinstance(trecho, dict):
            _erro(campo, "informe um objeto")
        _enum(trecho.get("tipo"), campo + ".tipo", ("fala", "narracao", "pausa"))
        fala = trecho["tipo"] == "fala"
        pausa = trecho["tipo"] == "pausa"
        obrigatorios = {"tipo", "duracaoSegundos"} | (set() if pausa else {"texto"}) | ({"personagem", "gesto"} if fala else set())
        opcionais = {"plano"} | ({"personagem"} if pausa else {"narrador"} if not fala else set())
        _objeto(trecho, campo, obrigatorios, opcionais)
        if not pausa:
            _texto(trecho["texto"], campo + ".texto", 350)
        limite_trecho = (180 if identificador == "EP01_C01" and pausa else
                         33 if identificador == "teste_fila_87" and pausa and len(roteiro) == 1 and trecho["duracaoSegundos"] == 33 else 30)
        duracao = _numero(trecho["duracaoSegundos"], campo + ".duracaoSegundos", 0.5, limite_trecho, True)
        if "plano" in trecho:
            _plano_cena(trecho["plano"], campo + ".plano", atores)
        if fala:
            if trecho["personagem"] not in atores:
                _erro(campo + ".personagem", "ator não pertence ao elenco")
            _enum(trecho["gesto"], campo + ".gesto", tuple(GESTOS) + ("machucado",))
            _gesto(trecho["gesto"], animacoes, canonicos, campo + ".gesto")
            falas.append((trecho["personagem"], cursor, cursor + duracao))
        elif pausa:
            if "personagem" in trecho:
                _alvo_camera(trecho["personagem"], campo + ".personagem", atores)
        elif "narrador" in trecho:
            _texto(trecho["narrador"], campo + ".narrador", 48)
        cursor += duracao
    if cursor > 180:
        _erro("roteiro", "duração total não pode ultrapassar 180 segundos")
    acoes = dados.get("acoes", [])
    if not isinstance(acoes, list) or len(acoes) > 20:
        _erro("acoes", "informe lista de 0 a 20 ações")
    intervalos = {}
    duracao_cena = cursor
    for i, acao in enumerate(acoes):
        campo = f"acoes[{i}]"
        if not isinstance(acao, dict):
            _erro(campo, "informe um objeto")
        tipo = acao.get("tipo")
        _enum(tipo, campo + ".tipo", ("esperar", "andarAte", "virarPara", "animacao"))
        extras = {"andarAte": ({"destino"}, {"velocidade"}), "virarPara": ({"alvo"}, set()),
                  "animacao": ({"animacao"}, set()), "esperar": (set(), set())}[tipo]
        _objeto(acao, campo, {"tipo", "personagem", "inicioSegundos", "duracaoSegundos"} | extras[0], extras[1])
        ator = acao["personagem"]
        if ator not in atores:
            _erro(campo + ".personagem", "ator não pertence ao elenco")
        inicio = _numero(acao["inicioSegundos"], campo + ".inicioSegundos", 0, 180, True)
        fim = inicio + _numero(acao["duracaoSegundos"], campo + ".duracaoSegundos", 0.1, 30, True)
        if fim > 180:
            _erro(campo, "fim da ação não pode ultrapassar 180 segundos")
        if tipo == "andarAte":
            _marca(acao["destino"], campo + ".destino")
            if "velocidade" in acao:
                _numero(acao["velocidade"], campo + ".velocidade", 0.2, 2)
        elif tipo == "virarPara":
            if acao["alvo"] not in atores or acao["alvo"] == ator:
                _erro(campo + ".alvo", "escolha outro ator do elenco")
        elif tipo == "animacao":
            _gesto(acao["animacao"], animacoes, canonicos, campo + ".animacao")
        for anterior_inicio, anterior_fim in intervalos.setdefault(ator, []):
            if inicio < anterior_fim and fim > anterior_inicio:
                _erro(campo, "ações simultâneas para o mesmo ator não são permitidas")
        intervalos[ator].append((inicio, fim))
        if tipo != "esperar" and any(ator == falante and inicio < fala_fim and fim > fala_inicio for falante, fala_inicio, fala_fim in falas):
            _erro(campo, "ação corporal sobreposta à fala do mesmo ator")
        duracao_cena = max(duracao_cena, fim)
    duracao_cena = _direcao(dados, pasta, atores, falas, intervalos, duracao_cena)
    duracao_cena = _interpretacao(dados, atores, duracao_cena)
    _objetos(dados, pasta, atores, duracao_cena)
    if "objeto" in camera:
        alvo_objeto = next((p for p in dados.get("objetos", []) if p["id"] == camera["objeto"]), None)
        if alvo_objeto is None or not alvo_objeto.get("visivelInicial", True):
            _erro("camera.objeto", "declare objeto próprio inicialmente visível em objetos")
    duracao_cena = _transicoes_pose(dados, pasta, atores, falas, intervalos, duracao_cena)
    _poses(dados, pasta, atores)
    duracao_cena = _veiculos(dados, pasta, atores, falas, intervalos, duracao_cena)
    duracao_cena = _portas(dados, pasta, atores, falas, intervalos, duracao_cena)
    if "armarioModular" in dados:
        if (dados["armarioModular"] is not True or identificador != "EP05_C04" or encenacao != "serie_v1"
                or ambiente["local"] != "CASA_VO_INTERIOR" or atores != ["DJANIRA_POBRE"] or duracao_cena != 8
                or len(roteiro) != 1 or len(dados.get("objetos", [])) != 1 or len(dados.get("interpretacao", [])) != 1
                or any(dados.get(k, []) for k in ("acoes", "direcao", "poses", "transicoesPose", "veiculos", "portas"))
                or dados.get("saidaAgua", False) or dados.get("empurraoAviao", False) or "objeto" in camera):
            _erro("armarioModular", "somente EP05_C04 de8s, Djanira pobre no interior, uma folha propria e nenhum controlador corporal concorrente")
        original = "Minha melhor roupa. Eu abri o armário e fiquei olhando pra ele um tempo. Eu tinha duas roupas. Uma de trabalhar e uma de ir na igreja."
        if (roteiro[0]["tipo"] != "narracao" or roteiro[0]["duracaoSegundos"] != 8
                or roteiro[0].get("texto") != original or roteiro[0].get("narrador") != "Narrador"):
            _erro("roteiro", "armarioModular conserva a unica narracao original integral de8s")
        folha, face = dados["objetos"][0], dados["interpretacao"][0]
        if (folha["id"] != "ARMARIO_DJANIRA" or folha["recurso"] != "porta_armario"
                or not folha.get("assentarNoChao", True) or not folha.get("visivelInicial", True)
                or "attachmentInicial" in folha or "apoioInicial" in folha or folha.get("eventos", [])):
            _erro("objetos", "declare somente ARMARIO_DJANIRA/porta_armario visivel no chao, sem attachment, apoio ou eventos")
        if (face["personagem"] != "DJANIRA_POBRE" or face.get("expressao") != "chateado"
                or face["inicioSegundos"] != 0 or face["duracaoSegundos"] != 8 or "olhar" in face or elenco[0].get("olharPara") is not None):
            _erro("interpretacao", "chateado0-8s; helper controla olhar para conteudo real do armario")
    if "saidaAgua" in dados:
        if (dados["saidaAgua"] is not True or identificador != "EP08_C01" or ambiente["local"] != "CANAL_LS" or atores != ["DJANIRA_POBRE"]
                or not 15 <= duracao_cena <= 50 or any(dados.get(k, []) for k in ("acoes", "direcao", "objetos", "poses", "transicoesPose", "veiculos", "portas"))):
            _erro("saidaAgua", "somente EP08_C01, ?gua real, Djanira,15?50s e sem controladores corporais concorrentes")
        if any(abs(Decimal(str(elenco[0]["marca"].get(k,0)))) > Decimal("0.001") for k in ("direita", "frente", "altura")):
            _erro("elenco[0].marca", "sa?da da ?gua parte diretamente da marca real")
    if "empurraoAviao" in dados:
        if (dados["empurraoAviao"] is not True or identificador != "EP07_C05" or encenacao != "serie_v1" or ambiente["local"] != "CANAL_LS"
                or set(atores) != {"DJANIRA_POBRE", "O_CHEFE", "DINJER"} or len(roteiro) != 1 or roteiro[0]["tipo"] != "pausa"
                or duracao_cena != 5 or camera["modo"] != "automatico" or any(dados.get(k, []) for k in ("acoes", "direcao", "interpretacao", "objetos", "poses", "transicoesPose", "veiculos", "portas")) or dados.get("saidaAgua", False)):
            _erro("empurraoAviao", "only EP07_C05 five-second silent insert with original three actors at CANAL_LS, no competing controller")
    if not 0 < duracao_cena <= 180:
        _erro("Cena", "duração deve ser maior que zero e no máximo 180 segundos")
    if camera["modo"] == "automatico":
        if "cortes" in camera:
            _erro("camera.cortes", "proibido no modo automatico")
    else:
        cortes = camera.get("cortes")
        if not isinstance(cortes, list) or not 1 <= len(cortes) <= 20:
            _erro("camera.cortes", "modo programado exige lista de 1 a 20 cortes")
        anterior = Decimal(-1)
        for i, corte in enumerate(cortes):
            campo = f"camera.cortes[{i}]"
            _objeto(corte, campo, ("inicioSegundos", "plano", "personagem"))
            inicio = _numero(corte["inicioSegundos"], campo + ".inicioSegundos", 0, 180, True)
            if (i == 0 and inicio != 0) or inicio <= anterior or inicio >= duracao_cena:
                _erro(campo + ".inicioSegundos", "primeiro corte em 0; demais estritamente crescentes e antes do fim da cena")
            anterior = inicio
            _plano_cena(corte["plano"], campo + ".plano", atores)
            _alvo_camera(corte["personagem"], campo + ".personagem", atores)
    if identificador == "EP01_C01":
        if (encenacao != "serie_v1" or ambiente["local"] != "CANAL_LS" or atores != ["DJANIRA_POBRE"]
                or any(dados.get(field, []) for field in ("acoes", "direcao", "interpretacao", "objetos", "poses", "transicoesPose", "veiculos", "portas"))):
            _erro("encenacao", "EP01_C01 exige serie_v1, CANAL_LS, Djanira sozinha e nenhuma ação/prop/pose/interpretação adicional")
        if any(abs(Decimal(str(elenco[0]["marca"].get(field, 0)))) > Decimal("0.001") for field in ("direita", "frente", "altura")):
            _erro("elenco[0].marca", "EP01_C01 usa diretamente a marca real da água; offsets precisam ser zero")
        if (len(roteiro) != 2 or roteiro[0]["tipo"] != "narracao" or Decimal(str(roteiro[0]["duracaoSegundos"])) != 14
                or roteiro[1]["tipo"] != "pausa" or Decimal(str(roteiro[1]["duracaoSegundos"])) != 6):
            _erro("roteiro", "EP01_C01 preserva narração14s e reserva silenciosa explícita6s, sem boca")
        expected = "Meu nome é Djanira. O que fizeram comigo foi tão deplorável que, se eu tô contando essa história, é porque eu sobrevivi. E sobreviver não foi sorte, foi teimosia."
        if roteiro[0]["texto"] != expected:
            _erro("roteiro[0].texto", "EP01_C01 deve conservar a narração original integral")
        if camera["modo"] != "automatico" or camera["planoInicial"] != "LONGE" or camera.get("planoFinal") != "OLHOS":
            _erro("camera", "EP01_C01 usa câmera própria LONGE→OLHOS nas fases físicas; não aceita cortes substitutos")
    if encenacao == "pedido_v1":
        if (dados["id"] != "EP01_C08" or ambiente["local"] != "ROCA_GRAPESEED"
                or atores != ["DJANIRA_POBRE", "DINJER"] or acoes):
            _erro("encenacao", "pedido_v1 exige EP01_C08, ROCA_GRAPESEED e Djanira/Dinjer sem ações sobrepostas")
        for i, direita in enumerate((Decimal("-1.1"), Decimal("1.1"))):
            marca = elenco[i]["marca"]
            if (abs(Decimal(str(marca["direita"])) - direita) > Decimal("0.001")
                    or abs(Decimal(str(marca["frente"]))) > Decimal("0.001")
                    or abs(Decimal(str(marca.get("altura", 0)))) > Decimal("0.001")):
                _erro("encenacao", "pedido_v1 exige marcas direita -1.1/+1.1, frente=0 e altura=0")
        tempos = (12, 6, 9, 6, 3, 3, 3, 4)
        tipos = ("pausa", "narracao", "fala", "fala", "fala", "fala", "fala", "pausa")
        falantes = (None, None, "DINJER", "DJANIRA_POBRE", "DINJER", "DJANIRA_POBRE", "DINJER", None)
        if len(dados["roteiro"]) != 8:
            _erro("encenacao", "pedido_v1 exige oito blocos: introdução, seis blocos de voz e celular")
        for trecho, tipo, tempo, falante in zip(dados["roteiro"], tipos, tempos, falantes):
            if (trecho["tipo"] != tipo or Decimal(str(trecho["duracaoSegundos"])) != tempo
                    or falante is not None and trecho.get("personagem") != falante):
                _erro("encenacao", "pedido_v1 preserva introdução12s, narração6s, falas9/6/3/3/3s e celular4s")
    elif encenacao == "ensaio_coreografia_v1":
        if (identificador != "teste_coreografia" or ambiente["local"] != "TESTE_AMBIENTE"
                or atores != ["CAPANGA_1", "DJANIRA_POBRE"] or acoes):
            _erro("encenacao", "ensaio_coreografia_v1 exige teste_coreografia, TESTE_AMBIENTE, CAPANGA_1/DJANIRA_POBRE e nenhuma ação adicional")
        for i, direita in enumerate((Decimal("-0.55"), Decimal("0.55"))):
            marca = elenco[i]["marca"]
            if (abs(Decimal(str(marca["direita"])) - direita) > Decimal("0.001")
                    or abs(Decimal(str(marca["frente"]))) > Decimal("0.001")
                    or abs(Decimal(str(marca.get("altura", 0)))) > Decimal("0.001")):
                _erro(f"elenco[{i}].marca", "ensaio_coreografia_v1 exige direita -0.55/+0.55, frente=0 e altura=0")
        if len(roteiro) != 1 or roteiro[0]["tipo"] != "narracao" or duracao_cena != 10:
            _erro("roteiro", "ensaio_coreografia_v1 exige exatamente uma narração de 10 segundos, sem fala")
    _morfologia_papel86(caminho, pasta, dados, hash_morph86_antes)
    return dados
