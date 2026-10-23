"""Validação da cena técnica v1, incluindo falas da Etapa 3, sem pacotes externos.

Confere as regras do contrato v1; não implementa um processador XSD genérico.
Modelos, superfícies e resultado visual só podem ser confirmados no jogo.
"""

from __future__ import annotations

import argparse
from decimal import Decimal, InvalidOperation
import io
import math
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET


RAIZ = Path(__file__).resolve().parents[1]
CENA_PADRAO = RAIZ / "cenas" / "teste_dupla.xml"
CLIMAS = frozenset(("CLEAR", "EXTRASUNNY", "CLOUDS", "OVERCAST", "RAIN", "THUNDER", "FOGGY", "SMOG", "CLEARING", "NEUTRAL"))
GESTOS = frozenset(("neutro", "explicando", "negando", "implorando", "irritado"))
PLANOS = frozenset(("LONGE", "DOIS", "MEDIA", "PERTO", "BAIXA", "ALTA", "ATRAS", "OLHOS"))
PLANOS_AMBIENTE = frozenset(("LONGE", "MEDIA", "BAIXA", "ALTA"))
PADRAO_ID = re.compile(r"[a-z][a-z0-9_]{0,47}")
PADRAO_CODIGO_LOCAL = re.compile(r"[A-Z][A-Z0-9_]{0,47}")
PADRAO_MODELO = re.compile(r"[a-z0-9_]{1,64}")
PADRAO_INTEIRO = re.compile(r"[+-]?[0-9]+")
PADRAO_NUMERO = re.compile(r"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?")


class ErroCena(ValueError):
    """Contrato inválido, com mensagem para quem prepara a cena."""


def estrutura(elemento: ET.Element, tag: str, atributos: set[str], aceita_texto: bool = False) -> None:
    if elemento.tag != tag:
        raise ErroCena(f"Esperado <{tag}>; encontrado <{elemento.tag}>.")
    faltantes = atributos.difference(elemento.attrib)
    desconhecidos = set(elemento.attrib).difference(atributos)
    if faltantes:
        raise ErroCena(f"<{tag}>: atributo(s) obrigatório(s) ausente(s): {', '.join(sorted(faltantes))}.")
    if desconhecidos:
        raise ErroCena(f"<{tag}>: atributo(s) desconhecido(s): {', '.join(sorted(desconhecidos))}.")
    if not aceita_texto and elemento.text and elemento.text.strip():
        raise ErroCena(f"<{tag}> não aceita texto; use os atributos previstos.")
    for filho in elemento:
        if filho.tail and filho.tail.strip():
            raise ErroCena(f"Texto inesperado depois de <{filho.tag}> em <{tag}>.")


def sem_filhos(elemento: ET.Element) -> None:
    if len(elemento):
        raise ErroCena(f"<{elemento.tag}> não aceita elementos internos.")


def identificador(valor: str, campo: str) -> None:
    if PADRAO_ID.fullmatch(valor) is None:
        raise ErroCena(f"{campo}: use 1 a 48 letras minúsculas, números ou _, começando por uma letra.")


def nome_nao_vazio(valor: str, campo: str) -> None:
    if not valor.strip():
        raise ErroCena(f"{campo}: o nome não pode ficar vazio.")


def inteiro(valor: str, campo: str, minimo: int, maximo: int) -> None:
    valor = valor.strip()
    if PADRAO_INTEIRO.fullmatch(valor) is None:
        raise ErroCena(f"{campo}: informe um número inteiro entre {minimo} e {maximo}.")
    try:
        numero = int(valor)
    except ValueError as erro:
        raise ErroCena(f"{campo}: o número inteiro é inválido.") from erro
    if not minimo <= numero <= maximo:
        raise ErroCena(f"{campo}: o valor deve ficar entre {minimo} e {maximo}.")


def numero_real(valor: str, campo: str, minimo: float, maximo: float) -> None:
    valor = valor.strip()
    if PADRAO_NUMERO.fullmatch(valor) is None:
        raise ErroCena(f"{campo}: informe um número entre {minimo:g} e {maximo:g}, usando ponto decimal.")
    numero = float(valor)
    if not math.isfinite(numero) or not minimo <= numero <= maximo:
        raise ErroCena(f"{campo}: o valor deve ser finito e ficar entre {minimo:g} e {maximo:g}.")


def ler_xml(caminho: Path) -> ET.Element:
    conteudo = caminho.read_bytes()
    if len(conteudo) > 1024 * 1024:
        raise ErroCena("O arquivo de cena excede 1 MiB; divida a cena em arquivos menores.")
    # Remover NUL apenas para a inspeção também detecta declarações em UTF-16/32.
    inspecao = conteudo.replace(b"\x00", b"")
    if re.search(rb"<!\s*(?:DOCTYPE|ENTITY)\b", inspecao, flags=re.IGNORECASE):
        raise ErroCena("A cena não pode conter DOCTYPE nem declarações de entidades XML.")
    leitor = ET.iterparse(io.BytesIO(conteudo), events=("start-ns", "end"))
    for evento, _ in leitor:
        if evento == "start-ns":
            raise ErroCena("A cena v1 não aceita namespaces XML nem atributos xmlns.")
    return leitor.root


def validar_cena(caminho: Path) -> None:
    if caminho.suffix.lower() == ".json":
        # Importação local evita ciclo: ambos os validadores usam ErroCena.
        if caminho.name.casefold().startswith("fila_"):
            from validar_fila import validar_fila
            validar_fila(caminho)
            return
        from validar_json import validar_cena_json
        catalogos = caminho.parent.parent / "catalogos"
        validar_cena_json(caminho, catalogos if catalogos.is_dir() else RAIZ / "catalogos")
        return
    cena = ler_xml(caminho)
    estrutura(cena, "Cena", {"versao", "id", "nome"})
    if cena.attrib["versao"] != "1":
        raise ErroCena("Cena.versao: a única versão aceita nesta etapa é 1.")
    identificador(cena.attrib["id"], "Cena.id")
    nome_nao_vazio(cena.attrib["nome"], "Cena.nome")
    base = ["Ambiente", "Local", "Personagens"]
    if [filho.tag for filho in cena] not in (base, base + ["Camera"], base + ["Roteiro"], base + ["Camera", "Roteiro"]):
        raise ErroCena("<Cena> deve conter <Ambiente>, <Local>, <Personagens> e, opcionalmente, <Camera> e <Roteiro>, nesta ordem.")
    ambiente, local, personagens = list(cena)[:3]
    atributos_ambiente = {"hora", "minuto", "clima", "densidadeTransito", "densidadePedestres", "bloquearPolicia"}
    if "limparArea" in ambiente.attrib:
        atributos_ambiente.add("limparArea")
    estrutura(ambiente, "Ambiente", atributos_ambiente)
    sem_filhos(ambiente)
    inteiro(ambiente.attrib["hora"], "Ambiente.hora", 0, 23)
    inteiro(ambiente.attrib["minuto"], "Ambiente.minuto", 0, 59)
    if ambiente.attrib["clima"] not in CLIMAS:
        raise ErroCena("Ambiente.clima: use um clima do catálogo: " + ", ".join(sorted(CLIMAS)) + ".")
    numero_real(ambiente.attrib["densidadeTransito"], "Ambiente.densidadeTransito", 0, 1)
    numero_real(ambiente.attrib["densidadePedestres"], "Ambiente.densidadePedestres", 0, 1)
    if ambiente.attrib["bloquearPolicia"] not in ("true", "false"):
        raise ErroCena("Ambiente.bloquearPolicia: use true ou false.")
    if "limparArea" in ambiente.attrib and ambiente.attrib["limparArea"] not in ("true", "false"):
        raise ErroCena("Ambiente.limparArea: use true ou false; ausente mantém a limpeza desativada.")
    atributos_local = {"referencia", "distanciaFrente"}
    if "id" in local.attrib:
        atributos_local.add("id")
    estrutura(local, "Local", atributos_local)
    sem_filhos(local)
    if local.attrib["referencia"] not in ("jogador", "marcado"):
        raise ErroCena("Local.referencia: use jogador ou marcado.")
    if local.attrib["referencia"] == "marcado":
        if "id" not in local.attrib:
            raise ErroCena("Local.id: atributo obrigatório quando referencia=marcado; use uma marca capturada no jogo.")
        if PADRAO_CODIGO_LOCAL.fullmatch(local.attrib["id"]) is None:
            raise ErroCena("Local.id: use 1 a 48 letras maiúsculas, números ou _, começando por uma letra.")
    elif "id" in local.attrib:
        raise ErroCena("Local.id: proibido quando referencia=jogador; remova o id ou use marcado.")
    numero_real(local.attrib["distanciaFrente"], "Local.distanciaFrente", 1, 10)
    estrutura(personagens, "Personagens", set())
    if len(personagens) > 5:
        raise ErroCena("<Personagens> deve conter de 0 a 5 personagens.")
    ids = set()
    for personagem in personagens:
        opcionais = {"olharPara"} if "olharPara" in personagem.attrib else set()
        estrutura(personagem, "Personagem", {"id", "nome", "modelo", "direita", "frente", "altura"} | opcionais)
        sem_filhos(personagem)
        pid = personagem.attrib["id"]
        identificador(pid, "Personagem.id")
        if pid in ids:
            raise ErroCena(f"Personagem.id duplicado: {pid}.")
        ids.add(pid)
        nome_nao_vazio(personagem.attrib["nome"], f"Personagem {pid}.nome")
        if PADRAO_MODELO.fullmatch(personagem.attrib["modelo"]) is None:
            raise ErroCena(f"Personagem {pid}.modelo: use 1 a 64 letras minúsculas, números ou _.")
        numero_real(personagem.attrib["direita"], f"Personagem {pid}.direita", -10, 10)
        numero_real(personagem.attrib["frente"], f"Personagem {pid}.frente", -10, 10)
        numero_real(personagem.attrib["altura"], f"Personagem {pid}.altura", -2, 2)
        if "olharPara" in personagem.attrib:
            identificador(personagem.attrib["olharPara"], f"Personagem {pid}.olharPara")
    for personagem in personagens:
        pid, alvo = personagem.attrib["id"], personagem.attrib.get("olharPara")
        if alvo is None:
            continue
        if alvo not in ids:
            raise ErroCena(f"Personagem {pid}.olharPara: não existe personagem com o id {alvo}.")
        if alvo == pid:
            raise ErroCena(f"Personagem {pid}.olharPara: escolha outro personagem.")
    for indice, personagem in enumerate(personagens):
        for anterior in personagens[:indice]:
            if abs(Decimal(personagem.attrib["direita"]) - Decimal(anterior.attrib["direita"])) < Decimal("0.5") and abs(Decimal(personagem.attrib["frente"]) - Decimal(anterior.attrib["frente"])) < Decimal("0.5"):
                raise ErroCena(f"Personagem {personagem.attrib['id']}: personagens muito próximos; separe ao menos0.5m em direita ou frente.")
    camera = cena.find("Camera")
    camera_automatica = True
    if camera is not None:
        opcionais = {campo for campo in ("automatica", "planoFala", "planoNarracao", "planoFinal") if campo in camera.attrib}
        if "personagem" in camera.attrib:
            opcionais.add("personagem")
        estrutura(camera, "Camera", {"plano", "inverterEixo", "deslocLado", "deslocAltura", "aproximar", "transicaoSuave"} | opcionais)
        sem_filhos(camera)
        if camera.attrib["plano"] not in PLANOS:
            raise ErroCena("Camera.plano: use " + ", ".join(sorted(PLANOS)) + ".")
        if ids and camera.attrib.get("personagem") not in ids:
            raise ErroCena("Camera.personagem: escolha um personagem existente no elenco da cena.")
        if not ids and "personagem" in camera.attrib:
            raise ErroCena("Camera.personagem: sem elenco, omita o atributo para enquadrar o ambiente.")
        if not ids and camera.attrib["plano"] not in PLANOS_AMBIENTE:
            raise ErroCena("Camera.plano: sem elenco, use LONGE, MEDIA, BAIXA ou ALTA.")
        for campo in ("inverterEixo", "transicaoSuave"):
            if camera.attrib[campo] not in ("true", "false"):
                raise ErroCena(f"Camera.{campo}: use true ou false.")
        if "automatica" in camera.attrib:
            if camera.attrib["automatica"] not in ("true", "false"):
                raise ErroCena("Camera.automatica: use true ou false.")
            camera_automatica = camera.attrib["automatica"] == "true"
        for campo in ("planoFala", "planoNarracao", "planoFinal"):
            if campo in camera.attrib and camera.attrib[campo] not in PLANOS:
                raise ErroCena(f"Camera.{campo}: use " + ", ".join(sorted(PLANOS)) + ".")
            if not ids and campo in camera.attrib and camera.attrib[campo] not in PLANOS_AMBIENTE:
                raise ErroCena(f"Camera.{campo}: sem elenco, use LONGE, MEDIA, BAIXA ou ALTA.")
        for campo in ("deslocLado", "deslocAltura", "aproximar"):
            numero_real(camera.attrib[campo], f"Camera.{campo}", -2, 2)
    roteiro = cena.find("Roteiro")
    if roteiro is not None:
        estrutura(roteiro, "Roteiro", set())
        if not 1 <= len(roteiro) <= 20:
            raise ErroCena("<Roteiro> deve conter de 1 a 20 falas ou narrações.")
        duracao_total = Decimal(0)
        for indice, trecho in enumerate(roteiro, start=1):
            if trecho.tag not in ("Fala", "Narracao", "Pausa"):
                raise ErroCena(f"Roteiro, trecho {indice}: use <Fala>, <Narracao> ou <Pausa>.")
            pausa = trecho.tag == "Pausa"
            atributos = {"duracaoSegundos"} | ({"personagem", "gesto"} if trecho.tag == "Fala" else ({"personagem"} if pausa and "personagem" in trecho.attrib else {"narrador"} if not pausa and "narrador" in trecho.attrib else set()))
            if "plano" in trecho.attrib:
                atributos.add("plano")
                if trecho.attrib["plano"] not in PLANOS:
                    raise ErroCena(f"Roteiro, trecho {indice}.plano: use " + ", ".join(sorted(PLANOS)) + ".")
                if not ids and trecho.attrib["plano"] not in PLANOS_AMBIENTE:
                    raise ErroCena(f"Roteiro, trecho {indice}.plano: sem elenco, use LONGE, MEDIA, BAIXA ou ALTA.")
            estrutura(trecho, trecho.tag, atributos, aceita_texto=not pausa)
            sem_filhos(trecho)
            texto = trecho.text or ""
            if not pausa and (not texto.strip() or len(texto) > 350):
                raise ErroCena(f"Roteiro, trecho {indice}: o texto precisa ter de 1 a 350 caracteres.")
            valor = trecho.attrib["duracaoSegundos"].strip()
            if re.fullmatch(r"[+]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)", valor) is None:
                raise ErroCena(f"Roteiro, trecho {indice}.duracaoSegundos: use um decimal com ponto, de 0.5 a 30.")
            try:
                duracao = Decimal(valor)
            except InvalidOperation as erro:
                raise ErroCena(f"Roteiro, trecho {indice}: duração inválida.") from erro
            if not Decimal("0.5") <= duracao <= Decimal(30) or duracao * 1000 != (duracao * 1000).to_integral_value():
                raise ErroCena(f"Roteiro, trecho {indice}.duracaoSegundos: de 0.5 a 30 segundos, com precisão de milissegundos.")
            duracao_total += duracao
            if pausa and "personagem" in trecho.attrib and trecho.attrib["personagem"] not in ids:
                raise ErroCena(f"Roteiro, pausa {indice}.personagem: escolha um personagem existente no elenco.")
            if trecho.tag == "Narracao" and "narrador" in trecho.attrib:
                nome = trecho.attrib["narrador"]
                if not nome.strip() or len(nome) > 48:
                    raise ErroCena(f"Roteiro, trecho {indice}.narrador: use um nome de 1 a 48 caracteres.")
            if trecho.tag == "Fala":
                if trecho.attrib["personagem"] not in ids:
                    raise ErroCena(f"Roteiro, fala {indice}.personagem: o personagem não existe no elenco da cena.")
                if trecho.attrib["gesto"] not in GESTOS:
                    raise ErroCena(f"Roteiro, fala {indice}.gesto: use " + ", ".join(sorted(GESTOS)) + ".")
        if duracao_total > 180:
            raise ErroCena("Roteiro: a duração total do teste não pode ultrapassar 180 segundos.")


def main() -> int:
    parser = argparse.ArgumentParser(description="Validar estrutura e referências de uma cena JSON/XML ou fila v1.")
    parser.add_argument("arquivo", type=Path, nargs="?", default=CENA_PADRAO, help="Arquivo de cena ou fila JSON, ou cena XML; padrão: cenas/teste_dupla.xml.")
    argumentos = parser.parse_args()
    try:
        caminho = argumentos.arquivo.expanduser().resolve()
        validar_cena(caminho)
        tipo = "Fila" if caminho.suffix.lower() == ".json" and caminho.name.casefold().startswith("fila_") else "Cena"
        print(f"{tipo}: {caminho}")
        print("Estrutura válida; modelos/coordenadas exigem teste no jogo.")
        return 0
    except ET.ParseError as erro:
        print(f"Cena inválida: XML malformado: {erro}.", file=sys.stderr)
    except (ErroCena, OSError, ValueError) as erro:
        print(f"Cena inválida: {erro}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
