"""Ferramentas do Diretor de Cenas; Python 3.12, apenas biblioteca padrão."""

from __future__ import annotations

import argparse
import configparser
from collections import deque
import csv
from datetime import datetime, timezone
import hashlib
import io
import json
import locale
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import uuid
import xml.etree.ElementTree as ET
import zipfile

from validar_cena import CENA_PADRAO, ErroCena, validar_cena
from validar_json import validar_cena_json
from validar_fila import validar_fila
from runtime_diretor import exigir_sem_vinculos, ler_pasta_dados, publicar_runtime


RAIZ = Path(__file__).resolve().parents[1]
CENA_JSON_PADRAO = RAIZ / "cenas" / "teste_json.json"
PASTA_JOGO_PADRAO = Path(
    "C:/Program Files (x86)/Steam/steamapps/common/Grand Theft Auto V"
)
COMPILADOR = Path("C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe")
TASKLIST = Path("C:/Windows/System32/tasklist.exe")
VERSAO_SHVDN = "v3.7.0-nightly.191"
PASTA_PACOTE = RAIZ / "dependencias" / ("ScriptHookVDotNet-" + VERSAO_SHVDN)
ARQUIVO_ZIP = RAIZ / "dependencias" / ("ScriptHookVDotNet-" + VERSAO_SHVDN + ".zip")
SHA256_ZIP = "A3E07AFDFB6714A9E39D807791D757C0AF6FF07B547D521E1991F0057001A2F6"
FONTE_SHVDN = (
    "https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases/tag/"
    + VERSAO_SHVDN
)
ARQUIVOS_SHVDN = (
    "ScriptHookVDotNet.asi",
    "ScriptHookVDotNet2.dll",
    "ScriptHookVDotNet3.dll",
    "ScriptHookVDotNet.ini",
    "ScriptHookVDotNet.pdb",
    "ScriptHookVDotNet2.pdb",
    "ScriptHookVDotNet3.pdb",
)


class ErroDiretor(Exception):
    """Erro explicado ao usuário sem alegar confirmação dentro do jogo."""


def agora_utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def hash_bytes(conteudo: bytes) -> str:
    return hashlib.sha256(conteudo).hexdigest().upper()


def hash_arquivo(caminho: Path) -> str:
    with caminho.open("rb") as arquivo:
        return hashlib.file_digest(arquivo, "sha256").hexdigest().upper()


def exigir_arquivo(caminho: Path) -> None:
    if not caminho.is_file():
        raise ErroDiretor(f"Arquivo necessário não encontrado: {caminho}")


def nova_pasta_backup(tipo: str) -> Path:
    nome = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    destino = exigir_sem_vinculos(RAIZ / "backups" / tipo / (nome + "-" + uuid.uuid4().hex[:8]))
    destino.mkdir(parents=True, exist_ok=False)
    return destino


def escrever_json_novo(caminho: Path, dados: dict) -> None:
    # Modo x impede sobrescrever um manifesto já existente.
    with caminho.open("x", encoding="utf-8", newline="\n") as arquivo:
        json.dump(dados, arquivo, ensure_ascii=False, indent=2)
        arquivo.write("\n")


def executar_comando(argumentos: list[str], tempo_limite: int) -> subprocess.CompletedProcess:
    return subprocess.run(
        argumentos,
        cwd=RAIZ,
        capture_output=True,
        text=True,
        encoding=locale.getpreferredencoding(False),
        errors="replace",
        timeout=tempo_limite,
        check=False,
        shell=False,
    )


def codigo_completo() -> bytes:
    """Uma fonte SHVDN: script e helpers de câmera, ambiente, locais e JSON."""
    caminhos = tuple(RAIZ / "src" / nome for nome in
                     ("DiretorGTA.3.cs", "CameraDiretor.cs", "AmbienteDiretor.cs", "LocaisDiretor.cs", "CenaJsonDiretor.cs", "CapturaDiretor.cs", "VisualDiretor.cs", "AtalhoGravacaoDiretor.cs", "ObjetosDiretor.cs", "ElencoAutomaticoDiretor.cs", "PreparacaoElencoDiretor.cs", "PilotoPedidoDiretor.cs", "EnsaioAcaoDiretor.cs", "AcoesSerieDiretor.cs", "TemporadaDiretor.cs", "InterpretacaoDiretor.cs", "CenaQuedaDiretor.cs", "ObjetosSerieDiretor.cs", "VeiculosSerieDiretor.cs", "PosesSerieDiretor.cs", "EpisodioUmDiretor.cs", "VariantesDjaniraDiretor.cs", "OperacaoLocalDiretor.cs", "OperacaoProducaoDiretor.cs", "EpisodiosTresSeteDiretor.cs", "PortasSerieDiretor.cs", "CenaVooTemporadaDiretor.cs", "CenaEmpurraoAviaoDiretor.cs", "FigurinoTransitorioDiretor.cs", "CenaTemporadaDiretor.cs", "ArmarioSerieDiretor.cs", "LuzesBoateDiretor.cs", "EntradaSentadaAutorDiretor.cs", "CartaFisicaDiretor.cs", "MesaColisaoTrialDiretor.cs", "LimpezaMesaDiretor.cs", "GorjetaFisicaTrialDiretor.cs"))
    partes = []
    for indice, caminho in enumerate(caminhos):
        exigir_sem_vinculos(caminho)
        exigir_arquivo(caminho)
        parte = caminho.read_text(encoding="utf-8-sig").rstrip()
        if indice and re.search(r"(?m)^\s*using\s+(?!\()[^;\r\n]+;", parte):
            raise ErroDiretor(f"Helper deve usar nomes completos, sem diretivas using: {caminho}")
        partes.append(parte)
    combinado = "\n\n".join(partes) + "\n"
    classes = re.findall(r"(?m)^\s*(?:(?:public|internal|sealed|abstract|partial)\s+)*class\s+[A-Za-z_]\w*\s*:\s*(?:GTA\.)?Script\b", combinado)
    if len(classes) != 1:
        raise ErroDiretor("A fonte combinada deve conter exatamente uma classe que herda Script.")
    return combinado.encode("utf-8")


def compilar() -> Path:
    api = PASTA_PACOTE / "ScriptHookVDotNet3.dll"
    origem = exigir_sem_vinculos(RAIZ / "dist" / "DiretorGTA.3.cs")
    destino = exigir_sem_vinculos(RAIZ / "dist" / "DiretorGTA.dll")
    for caminho in (COMPILADOR, api):
        exigir_arquivo(caminho)
    if destino.is_symlink() or destino.is_junction():
        raise ErroDiretor(f"O arquivo de saída é um vínculo; revise antes de compilar: {destino}")
    destino.parent.mkdir(parents=True, exist_ok=True)
    conteudo = codigo_completo()
    if origem.exists() and origem.read_bytes() != conteudo:
        backup_fonte = nova_pasta_backup("compilacao") / origem.name
        shutil.copy2(origem, backup_fonte)
        if hash_arquivo(backup_fonte) != hash_arquivo(origem):
            raise ErroDiretor("O backup da fonte combinada não confere.")
    if origem.is_symlink() or origem.is_junction():
        raise ErroDiretor("A fonte combinada é um vínculo; interrompido.")
    origem.write_bytes(conteudo)
    if destino.exists():
        exigir_arquivo(destino)
        backup = nova_pasta_backup("compilacao") / destino.name
        shutil.copy2(destino, backup)
        if hash_arquivo(backup) != hash_arquivo(destino):
            raise ErroDiretor(f"O backup da compilação não confere: {backup}")

    argumentos = [
        str(COMPILADOR),
        "/nologo",
        "/codepage:65001",
        "/target:library",
        "/platform:anycpu",
        "/optimize+",
        "/langversion:5",
        "/warn:4",
        "/warnaserror+",
        "/reference:" + str(api),
        "/reference:System.dll",
        "/reference:System.Core.dll",
        "/reference:System.Windows.Forms.dll",
        "/reference:System.Xml.dll",
        "/reference:System.Drawing.dll",
        "/out:" + str(destino),
        str(origem),
    ]
    resultado = executar_comando(argumentos, 180)
    if resultado.stdout.strip():
        print(resultado.stdout.rstrip())
    if resultado.stderr.strip():
        print(resultado.stderr.rstrip(), file=sys.stderr)
    if resultado.returncode != 0:
        raise ErroDiretor(
            f"Falha na compilação (código {resultado.returncode}). "
            "Corrija os erros acima antes de instalar."
        )
    exigir_arquivo(destino)
    print(f"Compilação concluída: {destino}")
    print(f"SHA256: {hash_arquivo(destino)}")
    print("O funcionamento no jogo ainda precisa do seu teste.")
    return destino


def validar(caminho: Path, catalogos: Path | None = None) -> None:
    try:
        caminho = exigir_sem_vinculos(caminho)
        if caminho.suffix.casefold() == ".json":
            pasta_catalogos = exigir_sem_vinculos(catalogos or caminho.parent.parent / "catalogos")
            if caminho.name.casefold().startswith("fila_"):
                validar_fila(caminho, catalogos=pasta_catalogos)
            else:
                validar_cena_json(caminho, pasta_catalogos)
        elif caminho.suffix.casefold() == ".xml":
            validar_cena(caminho)
        else:
            raise ErroDiretor("A cena deve ser um arquivo .json ou .xml.")
    except (ErroCena, ET.ParseError, OSError) as erro:
        raise ErroDiretor(f"Cena ou fila inválida: {erro}") from erro
    tipo = "Fila" if caminho.suffix.casefold() == ".json" and caminho.name.casefold().startswith("fila_") else "Cena"
    print(f"{tipo} validada: {caminho}")
    print("Estrutura válida; aparência e montagem exigem teste no jogo.")


def validar_cena_inicial(workspace: bool = False) -> None:
    config = configparser.ConfigParser(interpolation=None)
    with exigir_sem_vinculos(RAIZ / "configuracoes" / "DiretorGTA.ini").open("r", encoding="utf-8-sig") as arquivo:
        config.read_file(arquivo)
    fila = config.get("Fila", "Arquivo", fallback="").strip()
    nome = fila or config.get("Cena", "Inicial", fallback="teste_json.json").strip()
    if not re.fullmatch(r"[A-Za-z0-9_-]+\.(?:json|xml)", nome, re.IGNORECASE):
        raise ErroDiretor("Cena.Inicial/Fila.Arquivo: use somente o nome simples de um arquivo .json ou .xml.")
    if fila and (not nome.startswith("fila_") or not nome.endswith(".json")):
        raise ErroDiretor("Fila.Arquivo: use fila_teste.json ou fila_EPnn.json; vazio mantém Cena.Inicial.")
    if workspace:
        # Preparar arquivos novos antes de ler qualquer dado ainda ausente no perfil.
        dados = RAIZ
        pasta = exigir_sem_vinculos(dados / "cenas")
    else:
        dados = ler_pasta_dados(RAIZ, exigir_perfil=True)
        pasta = exigir_sem_vinculos(Path(os.path.expandvars(config.get("Pastas", "Cenas", fallback=str(dados / "cenas")))))
        if pasta != dados / "cenas":
            raise ErroDiretor("Pastas/Cenas deve apontar para a pasta cenas dentro de Projeto/Dados.")
    validar(pasta / nome, dados / "catalogos")


def publicar_dados() -> dict:
    resultado = publicar_runtime(RAIZ)
    print(f"Dados publicados: {resultado['PastaDados']}")
    print(f"Novos: {resultado['Criados']}; editados preservados: {resultado['Preservados']}; iguais: {resultado['Iguais']}.")
    print(f"Manifesto e backups: {resultado['Manifesto']}")
    return resultado


def exigir_jogo_fechado() -> None:
    exigir_arquivo(TASKLIST)
    for nome in ("GTA5.exe", "GTA5_Enhanced.exe"):
        resultado = executar_comando(
            [str(TASKLIST), "/FI", "IMAGENAME eq " + nome, "/FO", "CSV", "/NH"],
            30,
        )
        if resultado.returncode != 0 or resultado.stderr.strip():
            detalhe = resultado.stderr.strip() or resultado.stdout.strip()
            raise ErroDiretor(
                "Não foi possível consultar os processos do GTA; instalação interrompida. "
                + detalhe
            )
        # A mensagem INFO sem resultados pode ocupar várias linhas no Windows.
        if resultado.stdout.lstrip().upper().startswith("INFO"):
            continue
        for linha in csv.reader(io.StringIO(resultado.stdout)):
            if not linha or not any(campo.strip() for campo in linha):
                continue
            if len(linha) >= 2 and linha[0].strip().casefold() == nome.casefold():
                raise ErroDiretor(
                    "O GTA está aberto. Salve seu progresso e feche o jogo pelo menu antes de instalar."
                )
            # tasklist sem correspondências devolve INFO/INFORMAÇÕES, inclusive em /FO CSV.
            if len(linha) == 1 and linha[0].strip().upper().startswith("INFO"):
                continue
            raise ErroDiretor(
                "A consulta de processos devolveu uma resposta inesperada; instalação interrompida: "
                + ", ".join(linha)
            )


def fontes_instalacao() -> list[dict]:
    exigir_arquivo(ARQUIVO_ZIP)
    if hash_arquivo(ARQUIVO_ZIP) != SHA256_ZIP:
        raise ErroDiretor("O SHA256 do pacote oficial do ScriptHookVDotNet não confere.")
    fontes = []
    with zipfile.ZipFile(ARQUIVO_ZIP, "r") as pacote:
        for nome in ARQUIVOS_SHVDN:
            entradas = [entrada for entrada in pacote.infolist() if entrada.filename == nome]
            if len(entradas) != 1 or entradas[0].is_dir():
                raise ErroDiretor(f"Entrada ausente ou duplicada no pacote oficial: {nome}")
            origem = PASTA_PACOTE / nome
            exigir_arquivo(origem)
            conteudo = origem.read_bytes()
            if hash_bytes(conteudo) != hash_bytes(pacote.read(entradas[0])):
                raise ErroDiretor(
                    f"O arquivo extraído difere do ZIP oficial: {origem}. Instalação interrompida."
                )
            fontes.append({"origem": origem, "relativo": Path(nome), "conteudo": conteudo})
    for origem, relativo in (
        (RAIZ / "src" / "DiretorGTA.3.cs", Path("scripts") / "DiretorGTA.3.cs"),
        (RAIZ / "configuracoes" / "Instalacao.ini", Path("scripts") / "DiretorGTA.3.ini"),
    ):
        exigir_arquivo(origem)
        conteudo = codigo_completo() if relativo.name == "DiretorGTA.3.cs" else origem.read_bytes()
        fontes.append({"origem": origem, "relativo": relativo, "conteudo": conteudo})
    return fontes


def validar_destino(pasta_jogo: Path, destino: Path) -> None:
    pasta_jogo = exigir_sem_vinculos(pasta_jogo)
    destino = exigir_sem_vinculos(destino)
    if not destino.is_relative_to(pasta_jogo):
        raise ErroDiretor(f"O destino aponta para fora da pasta do jogo: {destino}")
    if destino.exists() and not destino.is_file():
        raise ErroDiretor(f"O destino existe e não é um arquivo: {destino}")


def verificar_estado_anterior(arquivo: dict) -> None:
    destino = exigir_sem_vinculos(arquivo["destino"])
    anterior = arquivo["hash_anterior"]
    if anterior is None:
        if destino.exists():
            raise ErroDiretor(f"Um arquivo apareceu após a preparação; instalação interrompida: {destino}")
    elif not destino.is_file() or hash_arquivo(destino) != anterior:
        raise ErroDiretor(f"Um arquivo mudou após o backup; instalação interrompida: {destino}")


def instalar(pasta_jogo: Path, somente_script: bool = False) -> None:
    pasta_jogo = exigir_sem_vinculos(pasta_jogo)
    for nome in ("GTA5.exe", "ScriptHookV.dll", "dinput8.dll"):
        exigir_arquivo(pasta_jogo / nome)
    if not somente_script:
        exigir_jogo_fechado()
    conflito = pasta_jogo / "scripts" / "DiretorGTA.dll"
    if conflito.exists() or conflito.is_symlink():
        raise ErroDiretor(
            "Existe scripts/DiretorGTA.dll no jogo. Revise e faça backup antes de trocar para .3.cs, "
            "para não carregar o Diretor duas vezes."
        )
    arquivos = fontes_instalacao()
    if somente_script:
        # Com o jogo aberto, o runtime precisa estar íntegro e permanece intocado.
        for arquivo in arquivos:
            if arquivo["relativo"].name not in ARQUIVOS_SHVDN:
                continue
            destino = pasta_jogo / arquivo["relativo"]
            validar_destino(pasta_jogo, destino)
            if not destino.is_file() or hash_arquivo(destino) != hash_bytes(arquivo["conteudo"]):
                raise ErroDiretor(
                    f"O ScriptHookVDotNet instalado está ausente ou difere do pacote oficial: {destino}. "
                    "Feche o GTA e execute instalar antes de atualizar o script."
                )
        arquivos = [arquivo for arquivo in arquivos if arquivo["relativo"].parent == Path("scripts")]
    for arquivo in arquivos:
        destino = pasta_jogo / arquivo["relativo"]
        validar_destino(pasta_jogo, destino)
        arquivo["destino"] = destino
        arquivo["hash"] = hash_bytes(arquivo["conteudo"])
        arquivo["hash_anterior"] = hash_arquivo(destino) if destino.is_file() else None
        arquivo["ignorar"] = arquivo["hash_anterior"] == arquivo["hash"]
    validar(CENA_JSON_PADRAO, RAIZ / "catalogos")
    validar_cena_inicial(workspace=True)
    compilar()
    for arquivo in arquivos:
        if arquivo["relativo"].name == "DiretorGTA.3.cs" and arquivo["conteudo"] != codigo_completo():
            raise ErroDiretor("O código mudou durante a compilação; publicação interrompida. Repita após concluir a edição.")
    publicar_dados()
    validar_cena_inicial()
    for arquivo in arquivos:
        atual = codigo_completo() if arquivo["relativo"].name == "DiretorGTA.3.cs" else arquivo["origem"].read_bytes()
        if atual != arquivo["conteudo"]:
            raise ErroDiretor("Uma fonte ou configuração mudou durante a preparação; publicação interrompida. Repita após concluir a edição.")
    # Confere novamente antes de iniciar os backups e a instalação.
    if not somente_script:
        exigir_jogo_fechado()
    for arquivo in arquivos:
        verificar_estado_anterior(arquivo)

    pasta_backup = nova_pasta_backup("instalacao")
    registros = []
    # Nenhum destino é alterado antes de todos os backups terminarem e conferirem.
    for arquivo in arquivos:
        backup = None
        if not arquivo["ignorar"] and arquivo["hash_anterior"] is not None:
            backup = pasta_backup / "anteriores" / arquivo["relativo"]
            backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(arquivo["destino"], backup)
            if hash_arquivo(backup) != arquivo["hash_anterior"]:
                raise ErroDiretor(f"O backup não confere; instalação interrompida: {backup}")
        registros.append(
            {
                "Arquivo": str(arquivo["relativo"]),
                "Origem": str(arquivo["origem"]),
                "Destino": str(arquivo["destino"]),
                "SHA256": arquivo["hash"],
                "SHA256Anterior": arquivo["hash_anterior"],
                "BackupAnterior": str(backup) if backup else None,
                "Alterado": not arquivo["ignorar"],
            }
        )
    manifesto = pasta_backup / "manifesto.json"
    escrever_json_novo(
        manifesto,
        {
            "Estado": "preparada",
            "Modo": "atualizar-script" if somente_script else "instalar",
            "DataUTC": agora_utc(),
            "PastaJogo": str(pasta_jogo),
            "SHVDN": VERSAO_SHVDN,
            "Fonte": FONTE_SHVDN,
            "PacoteSHA256": SHA256_ZIP,
            "Arquivos": registros,
        },
    )
    if not somente_script:
        exigir_jogo_fechado()
    for arquivo in arquivos:
        verificar_estado_anterior(arquivo)
    try:
        for arquivo in arquivos:
            if arquivo["ignorar"]:
                continue
            destino = arquivo["destino"]
            destino.parent.mkdir(parents=True, exist_ok=True)
            validar_destino(pasta_jogo, destino)
            verificar_estado_anterior(arquivo)
            temporario = None
            try:
                with tempfile.NamedTemporaryFile(
                    mode="wb", prefix=".diretor-", suffix=".tmp", dir=destino.parent, delete=False
                ) as saida:
                    temporario = Path(saida.name)
                    saida.write(arquivo["conteudo"])
                if hash_arquivo(temporario) != arquivo["hash"]:
                    raise ErroDiretor(f"A cópia temporária não confere: {destino}")
                validar_destino(pasta_jogo, destino)
                verificar_estado_anterior(arquivo)
                temporario.replace(destino)
                temporario = None
            finally:
                if temporario is not None:
                    temporario.unlink(missing_ok=True)
        for arquivo in arquivos:
            if hash_arquivo(arquivo["destino"]) != arquivo["hash"]:
                raise ErroDiretor(f"A verificação da cópia falhou: {arquivo['destino']}")
    except Exception as erro:
        escrever_json_novo(
            pasta_backup / "falha.json",
            {"Estado": "falhou", "DataUTC": agora_utc(), "Erro": str(erro)},
        )
        raise ErroDiretor(f"A instalação foi interrompida: {erro}. Consulte o backup em {pasta_backup}.") from erro
    escrever_json_novo(
        pasta_backup / "conclusao.json",
        {"Estado": "sucesso", "DataUTC": agora_utc(), "ArquivosConferidos": len(arquivos)},
    )
    alterados = sum(not arquivo["ignorar"] for arquivo in arquivos)
    print(f"Instalação concluída: {alterados} arquivo(s) copiado(s), {len(arquivos)} hashes conferidos.")
    print(f"Manifesto e backups: {pasta_backup}")
    if somente_script:
        print("Código atualizado; no jogo recarregar pelo console F4: Reload(). Confirmação visual pendente.")
    else:
        print("O funcionamento no jogo ainda não foi confirmado. Use somente o modo história.")


def ler_logs(pasta_jogo: Path, linhas: int) -> None:
    if linhas < 1:
        raise ErroDiretor("A quantidade de linhas deve ser maior que zero.")
    pasta_jogo = exigir_sem_vinculos(pasta_jogo)
    caminhos = (
        exigir_sem_vinculos(ler_pasta_dados(RAIZ) / "logs" / "DiretorGTA.log"),
        pasta_jogo / "ScriptHookVDotNet.log",
        pasta_jogo / "ScriptHookV.log",
    )
    for caminho in caminhos:
        print(f"\nArquivo: {caminho}")
        if not caminho.is_file():
            print("Ainda não há log neste caminho. Isso não confirma falha nem funcionamento.")
            continue
        with caminho.open("r", encoding="utf-8-sig", errors="replace") as arquivo:
            ultimas = deque(arquivo, maxlen=linhas)
        if ultimas:
            print("".join(ultimas), end="" if ultimas[-1].endswith("\n") else "\n")
        else:
            print("O arquivo de log está vazio; o teste no jogo ainda precisa ser confirmado.")


def main() -> int:
    parser = argparse.ArgumentParser(description="Compilar, publicar dados, instalar e ler logs do Diretor de Cenas.")
    comandos = parser.add_subparsers(dest="comando", required=True)
    comandos.add_parser("compilar", help="Verificar o script pelo compilador do .NET Framework.")
    validacao = comandos.add_parser("validar", help="Validar cena JSON/XML ou fila v1, sem pacotes adicionais.")
    validacao.add_argument("arquivo", type=Path, nargs="?", default=CENA_JSON_PADRAO)
    comandos.add_parser("publicar-runtime", help="Publicar dados no perfil com backups, preservando arquivos existentes.")
    serie = comandos.add_parser("preparar-serie", help="Preparar os 15 episódios a partir do roteiro, sem gerar cenas incompletas.")
    serie.add_argument("--conferir", action="store_true")
    edicao = comandos.add_parser("preparar-edicao", help="Preparar guias, vozes e continuidade dos 15 episódios.")
    edicao.add_argument("--conferir", action="store_true")
    cenas_serie = comandos.add_parser("preparar-cenas-serie", help="Preparar atuação das cenas completas e conferir dependências da temporada.")
    cenas_serie.add_argument("--conferir", action="store_true")
    piloto = comandos.add_parser("preparar-piloto", help="Preparar EP01_C08; publicar somente com recursos reais.")
    piloto.add_argument("--rascunho", action="store_true")
    comandos.add_parser("estado-serie", help="Conferir planos, personagens, locais e vídeos reais.")
    comandos.add_parser("diagnosticar-gravacao", help="Conferir Game Bar e pasta real, sem gravar nem mover vídeos.")
    comandos.add_parser("visuais", help="Conferir figurinos registrados; a criação acontece no jogo pelo F6.")
    instalacao = comandos.add_parser("instalar", help="Instalar com backup e verificação de hashes.")
    instalacao.add_argument("--pasta-jogo", type=Path, default=PASTA_JOGO_PADRAO)
    atualizacao = comandos.add_parser(
        "atualizar-script", help="Atualizar somente código e ponte INI; recarregar com F4 e Reload()."
    )
    atualizacao.add_argument("--pasta-jogo", type=Path, default=PASTA_JOGO_PADRAO)
    logs = comandos.add_parser("logs", help="Mostrar o final dos três logs existentes.")
    logs.add_argument("--pasta-jogo", type=Path, default=PASTA_JOGO_PADRAO)
    logs.add_argument("--linhas", type=int, default=80)
    argumentos = parser.parse_args()
    try:
        if argumentos.comando == "compilar":
            compilar()
        elif argumentos.comando == "validar":
            validar(argumentos.arquivo)
        elif argumentos.comando == "publicar-runtime":
            publicar_dados()
        elif argumentos.comando in ("preparar-serie", "preparar-edicao", "preparar-cenas-serie", "preparar-piloto", "estado-serie", "diagnosticar-gravacao", "visuais"):
            tool, options = {
                "preparar-serie": ("preparar_serie.py", ["--conferir"] if getattr(argumentos, "conferir", False) else []),
                "preparar-edicao": ("preparar_edicao.py", ["--conferir"] if getattr(argumentos, "conferir", False) else []),
                "preparar-cenas-serie": ("preparar_cenas_serie.py", ["--conferir"] if getattr(argumentos, "conferir", False) else []),
                "preparar-piloto": ("preparar_piloto.py", ["--rascunho"] if getattr(argumentos, "rascunho", False) else []),
                "estado-serie": ("estado_serie.py", []),
                "diagnosticar-gravacao": ("organizar_capturas.py", ["--diagnosticar"]),
                "visuais": ("importar_visuais.py", ["--status"]),
            }[argumentos.comando]
            result = executar_comando([sys.executable, "-X", "utf8", str(RAIZ / "ferramentas" / tool), *options], 180)
            if result.stdout: print(result.stdout.rstrip())
            if result.stderr: print(result.stderr.rstrip(), file=sys.stderr)
            return result.returncode
        elif argumentos.comando == "instalar":
            instalar(argumentos.pasta_jogo)
        elif argumentos.comando == "atualizar-script":
            instalar(argumentos.pasta_jogo, somente_script=True)
        else:
            ler_logs(argumentos.pasta_jogo, argumentos.linhas)
        return 0
    except PermissionError as erro:
        print(f"Erro: permissão negada ao acessar {erro.filename or str(erro)}.", file=sys.stderr)
    except subprocess.TimeoutExpired:
        print("Erro: o comando excedeu o tempo limite; a operação foi interrompida.", file=sys.stderr)
    except (ErroDiretor, OSError, zipfile.BadZipFile, RuntimeError, configparser.Error) as erro:
        print(f"Erro: {erro}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
