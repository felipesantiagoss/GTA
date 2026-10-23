"""Publicar dados editáveis sem sobrescrever tomadas, marcas ou edições do perfil."""

from __future__ import annotations

import configparser
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import stat
import tempfile
import uuid


class ErroRuntime(RuntimeError):
    """Falha segura na publicação dos dados; nenhuma permissão é contornada."""


def _hash(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest().upper()


def exigir_sem_vinculos(path: Path) -> Path:
    """Examinar a grafia original antes de resolve, incluindo raiz e ancestrais."""
    path = Path(path).expanduser()
    if ".." in path.parts:
        raise ErroRuntime(f"O caminho contém saída por '..': {path}")
    path = Path(os.path.abspath(path))
    for component in reversed((path, *path.parents)):
        try:
            info = component.lstat()
        except FileNotFoundError:
            continue
        if stat.S_ISLNK(info.st_mode) or (
            getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT
        ):
            raise ErroRuntime(f"Vínculo, junction ou reparse point proibido: {component}")
    return path


def _confinado(path: Path, root: Path) -> Path:
    root = exigir_sem_vinculos(root)
    path = exigir_sem_vinculos(path)
    if not path.is_relative_to(root):
        raise ErroRuntime(f"O caminho sai da pasta autorizada {root}: {path}")
    return path


def _mkdir(path: Path, root: Path) -> None:
    path = _confinado(path, root)
    path.mkdir(parents=True, exist_ok=True)
    _confinado(path, root)
    if not path.is_dir():
        raise ErroRuntime(f"O destino não é uma pasta: {path}")


def _perfil(perfil: Path | None) -> Path:
    if perfil is None:
        value = os.environ.get("USERPROFILE")
        if not value:
            raise ErroRuntime("USERPROFILE não está disponível; não publicar em um perfil presumido.")
        perfil = Path(value)
    perfil = exigir_sem_vinculos(perfil)
    if not perfil.is_dir():
        raise ErroRuntime(f"A pasta do perfil não existe: {perfil}")
    if any(part.casefold().startswith("onedrive") for part in perfil.parts):
        raise ErroRuntime("Os dados do Diretor devem ficar fora do OneDrive.")
    return perfil


def ler_pasta_dados(workspace: Path, exigir_perfil: bool = False, perfil: Path | None = None) -> Path:
    workspace = exigir_sem_vinculos(workspace)
    bootstrap = _confinado(workspace / "configuracoes" / "Instalacao.ini", workspace)
    config = configparser.ConfigParser(interpolation=None)
    try:
        with bootstrap.open("r", encoding="utf-8-sig") as stream:
            config.read_file(stream)
        value = config.get("Projeto", "Dados", fallback=None)
    except (configparser.Error, OSError) as error:
        raise ErroRuntime(f"Não foi possível ler a ponte INI: {bootstrap}: {error}") from error
    if value is None:
        if exigir_perfil:
            raise ErroRuntime("Projeto/Dados ausente: configure %USERPROFILE%\\GTA_Diretor antes de publicar.")
        return workspace  # Compatibilidade para ler logs das versões anteriores.
    expanded = os.path.expandvars(value.strip())
    if not Path(expanded).is_absolute():
        raise ErroRuntime("Projeto/Dados deve ser um caminho absoluto, com USERPROFILE disponível.")
    result = exigir_sem_vinculos(Path(expanded))
    if exigir_perfil and result != _perfil(perfil) / "GTA_Diretor":
        raise ErroRuntime("Projeto/Dados deve apontar exatamente para %USERPROFILE%\\GTA_Diretor.")
    if any(part.casefold().startswith("onedrive") for part in result.parts):
        raise ErroRuntime("Os dados do Diretor devem ficar fora do OneDrive.")
    return result


def _arquivos(workspace: Path):
    for name in ("cenas", "catalogos", "logs"):
        directory = _confinado(workspace / name, workspace)
        if not directory.is_dir():
            raise ErroRuntime(f"Pasta de dados de origem ausente: {directory}")
        for base, directories, files in os.walk(directory, followlinks=False):
            directories.sort()
            files.sort()
            for child in directories:
                _confinado(Path(base) / child, workspace)
            for child in files:
                path = _confinado(Path(base) / child, workspace)
                if not path.is_file():
                    raise ErroRuntime(f"A origem não é um arquivo comum: {path}")
                yield path


def _copiar_novo(source: Path, destination: Path, root: Path, expected: str) -> None:
    source = exigir_sem_vinculos(source)
    destination = _confinado(destination, root)
    if destination.exists():
        raise ErroRuntime(f"Um destino apareceu durante a preparação; não será sobrescrito: {destination}")
    _mkdir(destination.parent, root)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(prefix=".diretor-seed-", suffix=".tmp", dir=destination.parent,
                                         mode="wb", delete=False) as output:
            temporary = Path(output.name)
            with source.open("rb") as stream:
                while block := stream.read(1024 * 1024):
                    output.write(block)
        if _hash(temporary) != expected:
            raise ErroRuntime(f"A fonte mudou ou a cópia não confere; repita a publicação: {source}")
        _confinado(destination, root)
        if os.name == "nt":
            # No Windows rename recusa destino existente; nunca usar replace em um seed.
            os.rename(temporary, destination)
        else:
            os.link(temporary, destination)  # Criação exclusiva também nos testes fora do Windows.
            temporary.unlink()
        temporary = None
        _confinado(destination, root)
        if _hash(destination) != expected:
            raise ErroRuntime(f"O hash do arquivo publicado não confere: {destination}")
    finally:
        if temporary is not None:
            _confinado(temporary, root)
            temporary.unlink(missing_ok=True)


def _json_novo(path: Path, data: dict, root: Path) -> None:
    path = _confinado(path, root)
    with path.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def publicar_runtime(workspace: Path, dados: Path | None = None, perfil: Path | None = None) -> dict:
    workspace = exigir_sem_vinculos(workspace)
    perfil = _perfil(perfil)
    if dados is None:
        dados = ler_pasta_dados(workspace, exigir_perfil=True, perfil=perfil)
    dados = exigir_sem_vinculos(dados)
    if dados != perfil / "GTA_Diretor":
        raise ErroRuntime("A publicação deve usar a pasta GTA_Diretor diretamente no perfil.")
    if dados.is_relative_to(workspace) or workspace.is_relative_to(dados):
        raise ErroRuntime("Workspace e runtime não podem conter um ao outro.")

    records = []
    for source in _arquivos(workspace):
        relative = source.relative_to(workspace)
        destination = _confinado(dados / relative, dados)
        if destination.exists() and not destination.is_file():
            raise ErroRuntime(f"O destino não é um arquivo comum: {destination}")
        source_hash = _hash(source)
        before = _hash(destination) if destination.is_file() else None
        records.append({"Arquivo": str(relative), "Origem": str(source), "Destino": str(destination),
                        "SHA256Origem": source_hash, "SHA256DestinoAntes": before,
                        "Acao": "criado" if before is None else "igual" if before == source_hash else "preservado"})

    identifier = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-" + uuid.uuid4().hex[:8]
    backup = _confinado(workspace / "backups" / "runtime" / identifier, workspace)
    _mkdir(backup, workspace)
    # Todos os snapshots são conferidos antes da primeira cópia para o perfil.
    for record in records:
        relative = Path(record["Arquivo"])
        snapshot = backup / "fontes" / relative
        _copiar_novo(Path(record["Origem"]), snapshot, backup, record["SHA256Origem"])
        record["BackupOrigem"] = str(snapshot)
        record["BackupDestinoAnterior"] = None
        if record["SHA256DestinoAntes"] is not None:
            previous = backup / "anteriores" / relative
            _copiar_novo(Path(record["Destino"]), previous, backup, record["SHA256DestinoAntes"])
            record["BackupDestinoAnterior"] = str(previous)
    manifest = backup / "manifesto.json"
    _json_novo(manifest, {"Estado": "preparada", "DataUTC": datetime.now(timezone.utc).isoformat(),
                        "PastaDados": str(dados), "Workspace": str(workspace), "Arquivos": records}, backup)
    try:
        for record in records:
            source = exigir_sem_vinculos(Path(record["Origem"]))
            if _hash(source) != record["SHA256Origem"]:
                raise ErroRuntime(f"A origem mudou após o snapshot; repita a publicação: {source}")
        for name in ("cenas", "catalogos", "logs"):
            _mkdir(dados / name, dados)
        for record in records:
            destination = _confinado(Path(record["Destino"]), dados)
            if record["Acao"] == "criado":
                _copiar_novo(Path(record["BackupOrigem"]), destination, dados, record["SHA256Origem"])
            # Destinos já existentes são conservados, mesmo quando diferem do workspace.
            if not destination.is_file():
                raise ErroRuntime(f"Um destino desapareceu durante a publicação: {destination}")
            record["SHA256DestinoDepois"] = _hash(destination)
        _json_novo(backup / "conclusao.json", {"Estado": "sucesso", "DataUTC": datetime.now(timezone.utc).isoformat(),
                                             "PastaDados": str(dados), "Arquivos": records}, backup)
    except Exception as error:
        _json_novo(backup / "falha.json", {"Estado": "falhou", "Erro": str(error)}, backup)
        raise ErroRuntime(f"Publicação de dados interrompida: {error}. Backup: {backup}") from error
    result = {"PastaDados": str(dados), "Manifesto": str(manifest)}
    for action, label in (("criado", "Criados"), ("preservado", "Preservados"), ("igual", "Iguais")):
        result[label] = sum(record["Acao"] == action for record in records)
    return result
