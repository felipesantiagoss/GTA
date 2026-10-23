"""Preparar a fala de verificação da Etapa 7, com backup e validação."""

from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import uuid

from runtime_diretor import exigir_sem_vinculos, ler_pasta_dados
from validar_json import validar_cena_json


def main():
    workspace = exigir_sem_vinculos(Path(__file__).resolve().parents[1])
    dados = ler_pasta_dados(workspace, exigir_perfil=True)
    cena = exigir_sem_vinculos(dados / "cenas" / "teste_json.json")
    if not cena.is_relative_to(dados):
        raise ValueError("O teste deve estar dentro da pasta de dados do perfil.")
    validar_cena_json(cena, dados / "catalogos")
    original = cena.read_bytes()
    documento = json.loads(original.decode("utf-8-sig"))
    indice = next(i for i, item in enumerate(documento["roteiro"]) if item["tipo"] == "fala")
    texto = "Agora estou testando o arquivo JSON."
    if documento["roteiro"][indice]["texto"] == texto:
        print("A primeira fala do teste JSON já está preparada.")
        return
    if documento["roteiro"][indice]["texto"] != "Olá. Estamos testando o movimento da boca e os gestos.":
        raise ValueError("A primeira fala foi editada; a preparação não irá substituí-la.")
    documento["roteiro"][indice]["texto"] = texto
    novo = (json.dumps(documento, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")
    identificador = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-" + uuid.uuid4().hex[:8]
    backup = exigir_sem_vinculos(workspace / "backups" / "teste-json" / identificador)
    backup.mkdir(parents=True)
    with (backup / "anterior.json").open("xb") as stream:
        stream.write(original)
    candidato = backup / "teste_json.json"
    with candidato.open("xb") as stream:
        stream.write(novo)
    validar_cena_json(candidato, dados / "catalogos")
    if (backup / "anterior.json").read_bytes() != original or candidato.read_bytes() != novo:
        raise ValueError("O backup da cena não confere.")
    exigir_sem_vinculos(cena)
    if cena.read_bytes() != original:
        raise ValueError("A cena mudou durante a preparação; publicação interrompida.")
    temporario = exigir_sem_vinculos(cena.parent / (".teste-json-" + uuid.uuid4().hex + ".tmp"))
    try:
        with temporario.open("xb") as stream:
            stream.write(novo)
        if temporario.read_bytes() != novo:
            raise ValueError("A cópia temporária não confere.")
        exigir_sem_vinculos(cena)
        if cena.read_bytes() != original:
            raise ValueError("A cena mudou durante a preparação; publicação interrompida.")
        os.replace(temporario, cena)
        if cena.read_bytes() != novo:
            raise ValueError("A cena publicada não confere.")
    finally:
        temporario.unlink(missing_ok=True)
    manifesto = {
        "Cena": str(cena), "Campo": f"roteiro[{indice}].texto", "Texto": texto,
        "SHA256Anterior": hashlib.sha256(original).hexdigest().upper(),
        "SHA256Depois": hashlib.sha256(novo).hexdigest().upper(),
        "BackupAnterior": str(backup / "anterior.json"), "Estado": "preparado",
    }
    with (backup / "manifesto.json").open("x", encoding="utf-8") as stream:
        json.dump(manifesto, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    print("Primeira fala preparada: Homem: " + texto)
    print("Arquivo: " + str(cena))
    print("Backup: " + str(backup))


if __name__ == "__main__":
    main()
