"""Fila v1: validar todas as cenas antes de selecionar, sem modificar arquivos."""

from __future__ import annotations

from pathlib import Path
import re

from runtime_diretor import ErroRuntime, exigir_sem_vinculos
from validar_cena import ErroCena
from validar_json import ID_PRODUCAO, ID_TESTE, _erro, _inteiro, _ler, _objeto, _texto, validar_cena_json


def validar_fila(caminho: Path, cenas: Path | None = None, catalogos: Path | None = None) -> dict:
    """Devolver o descritor original somente após conferir cada referência, em ordem."""
    try:
        caminho = exigir_sem_vinculos(Path(caminho))
        pasta_cenas = exigir_sem_vinculos(Path(cenas) if cenas is not None else caminho.parent)
        pasta_catalogos = exigir_sem_vinculos(Path(catalogos) if catalogos is not None else pasta_cenas.parent / "catalogos")
    except ErroRuntime as erro:
        _erro("Fila.arquivo", str(erro))
    dados = _ler(caminho)
    _objeto(dados, "Fila", ("versao", "episodio", "nome", "cenas"))
    _inteiro(dados["versao"], "Fila.versao", 1, 1)
    _inteiro(dados["episodio"], "Fila.episodio", 0, 15)
    _texto(dados["nome"], "Fila.nome", 100)
    episodio = dados["episodio"]
    nome_esperado = "fila_teste.json" if episodio == 0 else f"fila_EP{episodio:02d}.json"
    nomes_permitidos = {"fila_teste.json", "fila_elenco.json"} if episodio == 0 else {nome_esperado}
    if episodio == 1:
        nomes_permitidos.add("fila_piloto.json")
    if caminho.name not in nomes_permitidos:
        _erro("Fila.arquivo", "para esse episódio o arquivo deve se chamar " + nome_esperado)
    arquivos = dados["cenas"]
    if caminho.name == "fila_piloto.json" and arquivos != ["EP01_C08.json"]:
        _erro("Fila.cenas", "fila_piloto permite somente EP01_C08.json")
    if not isinstance(arquivos, list) or not 1 <= len(arquivos) <= 20:
        _erro("Fila.cenas", "informe uma lista de 1 a 20 arquivos JSON")
    if not pasta_cenas.is_dir():
        _erro("Fila.cenas", "a pasta de cenas não existe: " + str(pasta_cenas))
    vistos = set()
    for indice, nome in enumerate(arquivos):
        campo = f"Fila.cenas[{indice}]"
        if not isinstance(nome, str) or re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*\.json", nome) is None:
            _erro(campo, "use somente nome simples de arquivo .json, sem caminho, unidade, separadores ou '..'")
        chave = nome.casefold()
        if chave in vistos:
            _erro(campo, "arquivo repetido, incluindo variação de maiúsculas/minúsculas")
        vistos.add(chave)
        identificador = nome[:-5]
        if episodio == 0:
            if ID_TESTE.fullmatch(identificador) is None:
                _erro(campo, "fila técnica permite teste_json.json, teste_fila_NN.json (01–99), teste_elenco_pobre.json ou teste_elenco_rica.json")
        else:
            correspondencia = ID_PRODUCAO.fullmatch(identificador)
            if correspondencia is None or int(correspondencia[1]) != episodio:
                _erro(campo, f"use cena EP{episodio:02d}_Cnn.json do mesmo episódio da fila")
        try:
            arquivo = exigir_sem_vinculos(pasta_cenas / nome)
            cena = validar_cena_json(arquivo, pasta_catalogos)
        except (ErroCena, ErroRuntime, OSError) as erro:
            _erro(campo + " (" + nome + ")", "cena inválida: " + str(erro))
        if cena["episodio"] != episodio:
            _erro(campo, "episódio do conteúdo difere do episódio da fila")
    return dados
