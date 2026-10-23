"""Finaliza EP03 reaproveitando tomadas ja gravadas, sem regravar (ordem 08/10/2026).

c07     : monta EP03_C07_LIMPO.mp4 = A9_v5 (9 s) + A10 (10 s) + B11 (11 s).
final   : monta EP03_MONTAGEM_LIMPA.mp4 C01..C07; exige o bruto de C05 e seu offset.
Mesma composicao WinRT do exportador generico (volume 0, 1080p30, PRECISE).
Confere SHA256 de cada fonte antes do render; nunca sobrescreve um arquivo existente.
Limitacao registrada: na A9_v5 a mao fica encoberta no momento da batida; aceita
pela ordem de aproveitar o gravado e adiar melhorias, sem aprovacao estetica.
"""
from __future__ import annotations

import argparse
import asyncio
import hashlib
import json
import shutil
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import montar_video_ep01 as editor  # noqa: E402

RAIZ = Path(__file__).resolve().parents[1]
EP03 = Path.home() / "GTA_Diretor/Serie/A_CALOTEIRA_1_TEMP/EP03"
WORK = RAIZ / "videos/EP03_SEM_LEGENDAS"

C07 = [
    {"id": "EP03_C07_A9", "video": EP03 / "EP03_C07_v5.mp4", "inicioMs": 5767, "duracaoMs": 9000,
     "sha256": "39D686C0B1CC5FEBABB7C4DA2B1301F5AF4E7D5CCAC90A2E8B51D20095660A81",
     "sessao": "7057e76a334f42959b9ea0d65b5056f6",
     "prova": "backups/montagem-video-episodios/20261008T032245704216Z-detectar-contagem-cpu-9c58e306/resultado.json",
     "limitacao": "mao encoberta no momento da batida; sem contato/tres impactos comprovados; sem aprovacao estetica"},
    {"id": "EP03_C07_A10", "video": EP03 / "EP03_C07_v3.mp4", "inicioMs": 6667, "duracaoMs": 10000,
     "sha256": "55AEB95D728D9B6AC48A5A3347AA87720FC28E17B387888B75491CABB4973E1C",
     "sessao": "b2f53b1fd1be4e20a82e264777eedc73",
     "prova": "backups/montagem-video-episodios/20261008T024453691243Z-calibracao-cpu-fechar-7f072785/resultado.json"},
    {"id": "EP03_C07_B11", "video": EP03 / "EP03_C07.mp4", "inicioMs": 8617, "duracaoMs": 11000,
     "sha256": "88E452CF12CE5C0EE4DCC68F961118697C7C80B8D7488A25729386F90D6E2829",
     "sessao": "c0b54681360345bc916d61fcef06618d",
     "prova": "backups/montagem-video-episodios/20261008T014056999896Z-calibracao-cpu-fechar-cac30473/resultado.json"},
]

ANTES_C05 = [
    {"id": "EP03_C01", "video": EP03 / "EP03_C01_LIMPO.mp4", "inicioMs": 0, "duracaoMs": 13500,
     "sha256": "EC6C9B6D47CA130DF8B90513E82876FD1E1F1DCF601B41E729C27B2302BBC1C4"},
    {"id": "EP03_C02", "video": EP03 / "EP03_C02.mp4", "inicioMs": 6400, "duracaoMs": 6000,
     "sha256": "0FAFDC7786F96A9C6FBD2B735052130A4C48B462F382A6BECE8574E1D59BE7AD",
     "prova": "backups/montagem-video-episodios/20261006T102926747189Z-calibracao-cpu-fechar-297a1af8/resultado.json"},
    {"id": "EP03_C03", "video": EP03 / "EP03_C03_LIMPO.mp4", "inicioMs": 0, "duracaoMs": 8000,
     "sha256": "F091910C4201E2586A4611D25864C294B85E45567EDE54009A1BEA8D8A29F0C4"},
    {"id": "EP03_C04", "video": EP03 / "EP03_C04.mp4", "inicioMs": 6584, "duracaoMs": 9000,
     "sha256": "CDA9FFB6B087775515922C52F603897C16AB60087047321EAB9C238966E7E8B1",
     "prova": "backups/montagem-video-episodios/20261006T102928485934Z-calibracao-cpu-fechar-a79c847c/resultado.json"},
]
DEPOIS_C05 = [
    {"id": "EP03_C06", "video": EP03 / "EP03_C06_v2.mp4", "inicioMs": 6400, "duracaoMs": 11000,
     "sha256": "0C170AE0ACF2A3501D9689C6E9452DCFC2796D586E8E359A8051AD1145A82E70",
     "prova": "backups/montagem-video-episodios/20261006T095315484017Z-calibracao-cpu-fechar-129e9bb5/resultado.json"},
] + C07


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for bloco in iter(lambda: f.read(1 << 20), b""):
            h.update(bloco)
    return h.hexdigest().upper()


async def render(cortes: list[dict], destino: Path) -> dict:
    from winrt.windows.media.editing import MediaClip, MediaComposition, MediaTrimmingPreference
    from winrt.windows.media.mediaproperties import MediaEncodingProfile, VideoEncodingQuality
    from winrt.windows.media.transcoding import TranscodeFailureReason
    from winrt.windows.storage import CreationCollisionOption, StorageFile, StorageFolder

    total = sum(c["duracaoMs"] for c in cortes)
    composition = MediaComposition()
    for c in cortes:
        if sha256(c["video"]) != c["sha256"]:
            raise ValueError("Fonte alterada: " + c["id"])
        clip = await MediaClip.create_from_file_async(await StorageFile.get_file_from_path_async(str(c["video"])))
        fim = timedelta(milliseconds=c["inicioMs"] + c["duracaoMs"])
        if fim > clip.original_duration:
            raise ValueError("Faltam quadros reais: " + c["id"])
        clip.trim_time_from_start = timedelta(milliseconds=c["inicioMs"])
        clip.trim_time_from_end = clip.original_duration - fim
        clip.volume = 0
        if clip.trimmed_duration != timedelta(milliseconds=c["duracaoMs"]):
            raise ValueError("Getter de recorte WinRT divergiu: " + c["id"])
        composition.clips.append(clip)
    if abs(composition.duration - timedelta(milliseconds=total)) > timedelta(microseconds=len(cortes)):
        raise ValueError("Duracao da composicao divergiu.")
    profile = MediaEncodingProfile.create_mp4(VideoEncodingQuality.H_D1080P)
    profile.video.frame_rate.numerator, profile.video.frame_rate.denominator = 30, 1
    profile.video.bitrate = 8000000
    pasta = await StorageFolder.get_folder_from_path_async(str(destino.parent))
    arquivo = await pasta.create_file_async(destino.name, CreationCollisionOption.FAIL_IF_EXISTS)
    print("Render: " + destino.name, flush=True)
    falha = await composition.render_to_file_with_profile_async(arquivo, MediaTrimmingPreference.PRECISE, profile)
    if falha != TranscodeFailureReason.NONE:
        raise ValueError("Windows recusou render: " + str(falha))
    meta = editor.metadados_mp4(destino)
    trilha = next(t for t in meta["trilhas"] if t.get("tipo") == "vide")
    if abs(meta["duracaoMs"] - total) > 100 or (trilha["largura"], trilha["altura"]) != (1920, 1080):
        raise ValueError("MP4 final divergiu: " + destino.name)
    return {"arquivo": str(destino), "duracaoMs": meta["duracaoMs"], "somaRecortesMs": total, "sha256": sha256(destino)}


def publicar(gerado: Path, nome: str) -> list[dict]:
    copias = []
    for pasta in (WORK, EP03):
        alvo = pasta / nome
        if alvo.exists():
            raise FileExistsError("Ja existe, nao sobrescrevo: " + str(alvo))
        shutil.copy2(gerado, alvo)
        copias.append({"arquivo": str(alvo), "sha256": sha256(alvo)})
    if len({c["sha256"] for c in copias}) != 1:
        raise ValueError("Copias divergentes de " + nome)
    return copias


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("acao", choices=["c07", "final"])
    p.add_argument("--c05-video", type=Path)
    p.add_argument("--c05-inicio-ms", type=int)
    a = p.parse_args(argv)
    if a.acao == "c07":
        cortes, nome = C07, "EP03_C07_LIMPO.mp4"
    else:
        if not a.c05_video or a.c05_inicio_ms is None:
            p.error("final exige --c05-video e --c05-inicio-ms")
        c05 = {"id": "EP03_C05", "video": a.c05_video, "inicioMs": a.c05_inicio_ms, "duracaoMs": 12000,
               "sha256": sha256(a.c05_video)}
        cortes, nome = ANTES_C05 + [c05] + DEPOIS_C05, "EP03_MONTAGEM_LIMPA.mp4"
    trabalho = RAIZ / "backups/finalizar-ep03" / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-" + a.acao)
    trabalho.mkdir(parents=True)
    resultado = asyncio.run(render(cortes, trabalho / nome))
    resultado["copias"] = publicar(trabalho / nome, nome)
    resultado["recortes"] = [{**c, "video": str(c["video"])} for c in cortes]
    resultado["semRegravacao"] = True
    (trabalho / "resultado.json").write_text(json.dumps(resultado, ensure_ascii=False, indent=1), encoding="utf-8")
    print(json.dumps({k: resultado[k] for k in ("duracaoMs", "somaRecortesMs", "sha256", "copias")}, indent=1))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
