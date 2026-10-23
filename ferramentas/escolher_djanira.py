"""Salva a opção da Djanira rica escolhida no jogo, com prova e journal.

Recebe somente o pedido estruturado de uma opção aprovada pelo usuário,
pela escolha no jogo ou por autorização expressa no chat. Não sorteia,
normaliza ou reserializa os XMLs; os demais figurinos e a semente são preservados.
"""
from __future__ import annotations

import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import json
import math
import os
from pathlib import Path
import re
import sys
import time
import uuid

import corrigir_figurinos as transacao
import importar_visuais as visuais
from runtime_diretor import exigir_sem_vinculos, ler_pasta_dados

RAIZ = Path(__file__).resolve().parents[1]
RICA = "catalogos/visuais/DJANIRA_RICA.xml"
SEMENTE = "catalogos/visuais/elenco-semente.xml"
PADRAO_PEDIDO = re.compile(r"escolha_djanira_([0-9a-f]{32})\.json", re.I)
PADRAO_PROVA = re.compile(r"conferencia_visual_DJANIRA_RICA_[0-9a-f]{32}\.xml", re.I)
HEX = re.compile(r"[0-9A-Fa-f]{64}")


class EscolhaOcupada(ValueError):
    """Outra execução ainda possui o pedido; não publicar seu resultado."""


def _caminho(path, root):
    return transacao.confinado(Path(path), Path(root))


def _json(raw):
    def pares(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError("Pedido/registro JSON com chave duplicada.")
            result[key] = value
        return result

    def constante(_):
        raise ValueError("Número não finito no pedido/registro.")

    if len(raw) > 1024 * 1024:
        raise ValueError("Pedido/registro excede o limite de tamanho.")
    return json.loads(raw.decode("utf-8-sig"), object_pairs_hook=pares, parse_constant=constante)


def ler_pedido(raw):
    data = _json(raw)
    if (not isinstance(data, dict) or set(data) != {"versao", "opcao", "visualSHA256", "origemSHA256"}
            or type(data["versao"]) is not int or data["versao"] != 1
            or type(data["opcao"]) is not int or data["opcao"] not in (1, 2, 3)):
        raise ValueError("Pedido deve declarar versão1 e a opção1,2 ou3 escolhida no jogo.")
    for key in ("visualSHA256", "origemSHA256"):
        if not isinstance(data[key], str) or not HEX.fullmatch(data[key]):
            raise ValueError("Hash inválido no pedido.")
        data[key] = data[key].upper()
    return data


def _novo(path, raw, root):
    path = _caminho(path, root)
    path.parent.mkdir(parents=True, exist_ok=True)
    _caminho(path, root)
    temporary = _caminho(path.with_name(path.name + ".tmp-" + uuid.uuid4().hex), root)
    try:
        with temporary.open("xb") as stream:
            stream.write(raw)
            stream.flush()
            os.fsync(stream.fileno())
        _caminho(path, root)
        # Move/CreateNew exclusivos: nunca publicar um JSON parcialmente escrito.
        if os.name == "nt":
            os.rename(temporary, path)
        else:
            os.link(temporary, path)
            temporary.unlink()
    finally:
        if temporary.exists():
            _caminho(temporary, root).unlink()


@contextmanager
def _exclusivo(profile, request):
    path = _caminho(profile / "logs/escolha_djanira_em_andamento.lock", profile)
    token = (request.name + "\n" + uuid.uuid4().hex).encode("ascii")
    try:
        _novo(path, token, profile)
    except FileExistsError as error:
        raise EscolhaOcupada("Outra escolha está em andamento; nenhum figurino foi alterado.") from error
    try:
        yield
    finally:
        if _caminho(path, profile).read_bytes() == token:
            path.unlink()


def _int(value):
    if not isinstance(value, str) or not re.fullmatch(r"-?[0-9]{1,10}", value):
        raise ValueError("Inteiro ausente ou inválido na prova dos getters.")
    return int(value)


def _float(value):
    if not isinstance(value, str) or len(value) > 40:
        raise ValueError("Valor decimal ausente na prova dos getters.")
    value = float(value)
    if not math.isfinite(value):
        raise ValueError("Valor decimal não finito na prova dos getters.")
    return value


def conferir_prova(raw, visual, option):
    proof = visuais._xml(raw)  # Parser compartilhado: sem DTD/entidades/expansão.
    head = visual["rosto"]
    if (head is None or proof.tag != "ConferenciaVisual" or proof.get("versao") != "1"
            or proof.get("codigo") != "DJANIRA_RICA"
            or proof.get("cena") != "teste_fila_" + str(93 + option)
            or proof.get("modelo") != visual["modelo"]
            or proof.get("visualSHA256") != visual["sha256"]
            or proof.get("faceArquivoSHA256") != visual["faceSHA256"]
            or _int(proof.get("olhosGetter")) != head["olhos"]):
        raise ValueError("Prova não corresponde à opção, ao arquivo, à identidade ou aos olhos.")
    pieces = {"Componente": {}, "Acessorio": {}, "Overlay": {}}
    dna = []
    for item in proof:
        if list(item) or (item.text and item.text.strip()):
            raise ValueError("Subcampos inesperados na prova dos getters.")
        if item.tag == "DNAGetter":
            dna.append(item)
        elif item.tag in pieces:
            slot = _int(item.get("slot"))
            if slot in pieces[item.tag]:
                raise ValueError("Slot duplicado na prova dos getters.")
            pieces[item.tag][slot] = item
        else:
            raise ValueError("Campo desconhecido na prova dos getters.")
    for tag, count, expected in (("Componente", 12, visual["componentes"]),
                                 ("Acessorio", 10, visual["acessorios"])):
        if set(pieces[tag]) != set(range(count)):
            raise ValueError("A prova exige os12 componentes e os10 acessórios.")
        for slot, pair in enumerate(expected):
            item = pieces[tag][slot]
            drawable, texture = _int(item.get("drawable")), _int(item.get("textura"))
            # Em prop ausente, GTA pode retornar textura0 ou-1: ela não é uma peça.
            if (drawable != pair[0] or (drawable >= 0 and texture != pair[1])
                    or (drawable < 0 and texture not in (-1, 0))):
                raise ValueError(f"Getter divergente: {tag}, slot{slot}.")
    if len(dna) != 1 or set(pieces["Overlay"]) != set(range(13)):
        raise ValueError("A prova exige DNAGetter e os13 índices de overlay reais.")
    try:
        parents = [_int(n) for n in dna[0].get("pais", "").split(",")]
        mixes = [_float(n) for n in dna[0].get("misturas", "").split(",")]
    except (ValueError, TypeError) as error:
        raise ValueError("DNAGetter incompleto ou inválido.") from error
    if (parents != head["pais"] or len(mixes) != 3
            or any(abs(a - b) > 0.00001 for a, b in zip(mixes, head["misturas"]))
            or dna[0].get("isParent") != ("true" if head["isParent"] else "false")):
        raise ValueError("DNAGetter divergiu do rosto preservado.")
    for slot, expected in enumerate(head["overlays"]):
        item = pieces["Overlay"][slot]
        if (_int(item.get("indiceGetter")) != expected[0]
                or _int(item.get("corXML")) != expected[1]
                or _int(item.get("segundaCorXML")) != expected[2]
                or abs(_float(item.get("opacidadeXML")) - expected[3]) > 0.00001):
            raise ValueError(f"Overlay{slot} divergiu do XML/getter declarado.")
    return proof


def _opcao(raw, request, current):
    visual = visuais.validar_visual_bytes(raw, current["DJANIRA_RICA"]["modelo"])
    if visual["sha256"] != request["visualSHA256"]:
        raise ValueError("O arquivo da opção divergiu do hash escolhido.")
    meta = visuais._xml(raw).find("DiretorOpcao")
    if meta is None:
        raise ValueError("A opção não contém sua origem registrada no jogo.")
    fields = visuais._filhos(meta, "DiretorOpcao", ("Numero", "Preset", "SHA256CanonicoOrigem", "Estado", "Fonte"))
    values = {name: visuais._escalar(node, "DiretorOpcao." + name) for name, node in fields.items()}
    if values["Numero"] != str(request["opcao"]) or values["SHA256CanonicoOrigem"] != request["origemSHA256"]:
        raise ValueError("Número/origem da opção não corresponde ao pedido.")
    if not all(values.values()):
        raise ValueError("Metadados da opção incompletos.")
    if (not visuais.mesma_identidade_djanira(current["DJANIRA_POBRE"], visual)
            or not visuais.mesma_identidade_djanira(current["DJANIRA_RICA"], visual)):
        raise ValueError("A opção alterou a identidade; figurinos preservados.")
    return visual, values


def _prova(profile, visual, option):
    errors = []
    for path in sorted((profile / "logs").iterdir(), key=lambda p: p.name, reverse=True):
        if not PADRAO_PROVA.fullmatch(path.name):
            continue
        raw = _caminho(path, profile).read_bytes()
        try:
            root = visuais._xml(raw)
            if root.get("visualSHA256") != visual["sha256"]:
                continue
            conferir_prova(raw, visual, option)
            return path, raw
        except (ValueError, TypeError) as error:
            errors.append(str(error))
    detail = errors[0] if errors else "nenhum relatório da opção escolhida"
    raise ValueError("Falta conferência real dos getters: " + detail)


def _ler_conjunto(profile):
    names = [f"catalogos/visuais/{code}.xml" for code in visuais.CODIGOS]
    names += [transacao.MANIFESTO, transacao.CATALOGO, SEMENTE]
    names += sorted(p.relative_to(profile).as_posix() for p in (profile / "catalogos").glob("figurinos_continuidade_*.json"))
    return {name: _caminho(profile / name, profile).read_bytes() for name in names}


def _validar_base(contents, request):
    current = transacao.verificar_conjunto(contents)
    seed = visuais._xml(contents[SEMENTE])
    manifest = visuais._xml(contents[transacao.MANIFESTO])
    if (seed.tag != "sementeElenco" or seed.get("versao") != "1" or not seed.get("seed")
            or seed.get("seed") != manifest.get("seed")):
        raise ValueError("Semente e manifesto divergentes; elenco preservado.")
    if transacao.sha(contents[RICA]) != request["origemSHA256"]:
        raise ValueError("O figurino canônico mudou depois de gerar a opção; preservado.")
    return current, seed.get("seed")


def _preparar(profile, request_path, request_raw, request, backup, package, guid):
    contents = _ler_conjunto(profile)
    current, seed = _validar_base(contents, request)
    option_path = _caminho(profile / "catalogos/visuais/opcoes" /
                          f"DJANIRA_RICA_opcao{request['opcao']}_{request['visualSHA256']}.xml", profile)
    option_raw = option_path.read_bytes()
    visual, meta = _opcao(option_raw, request, current)
    proof_path, proof_raw = _prova(profile, visual, request["opcao"])
    outputs = {RICA: option_raw}
    for name in (transacao.MANIFESTO, transacao.CATALOGO):
        outputs[name] = transacao.substituir(contents[name], request["origemSHA256"].encode(), request["visualSHA256"].encode())
    after = transacao.verificar_conjunto(contents | outputs)
    # Request, opção e prova também entram no inventário protegido da publicação.
    for path, raw in ((request_path, request_raw), (option_path, option_raw), (proof_path, proof_raw)):
        contents[path.relative_to(profile).as_posix()] = raw
    backup.mkdir(parents=True, exist_ok=False)
    preserved = []
    for name, raw in contents.items():
        _novo(backup / "perfil" / name, raw, backup)
        preserved.append({"relativo": name, "sha256": transacao.sha(raw)})
    files = []
    package.mkdir()
    for name in (RICA, transacao.MANIFESTO, transacao.CATALOGO):
        _novo(package / "novos" / name, outputs[name], package)
        files.append({"relativo": name, "antes": transacao.sha(contents[name]), "depois": transacao.sha(outputs[name])})
    plan = {"revisao": "escolha-djanira-0.9.7-" + guid, "perfil": str(profile), "estado": "preparada",
            "dataUtc": datetime.now(timezone.utc).isoformat(), "arquivos": files, "preservados": preserved,
            "aprovacaoVisual": "Roupa da Djanira rica aprovada pelo usuário; demais figurinos permanecem pendentes."}
    continuity = {"versao": "0.9.7", "revisao": plan["revisao"], "dataUtc": plan["dataUtc"], "seed": seed,
                  "sementeSHA256": transacao.sha(contents[SEMENTE]), "personagens": after,
                  "escolha": request, "metadadosOpcao": meta, "pedido": str(request_path),
                  "pedidoSHA256": transacao.sha(request_raw), "opcaoArquivo": str(option_path),
                  "provaGetters": str(proof_path), "provaGettersSHA256": transacao.sha(proof_raw),
                  "backup": str(backup), "alteracoes": files, "aprovacaoVisual": plan["aprovacaoVisual"],
                  "limitesProva": "DNA, olhos, peças e índices de overlay têm getters; traços, opacidade e cores provêm do XML/setters.",
                  "temporada2": "Reutilizar estes13 XMLs e a semente registrada; não regenerar rostos ou figurinos."}
    _novo(package / "continuidade.json", transacao.json_bytes(continuity), package)
    _novo(backup / "manifesto.json", transacao.json_bytes({"arquivos": preserved}), backup)
    _novo(package / "plano.json", transacao.json_bytes(plan), package)
    return plan


def _executar(profile, request_path, request_raw, request, guid):
    backup = _caminho(RAIZ / "backups/escolha-djanira" / guid, RAIZ / "backups")
    package = _caminho(backup / "escolha-0.9.7", backup)
    continuity_path = _caminho(profile / "catalogos" / ("figurinos_continuidade_0_9_7_" + guid + ".json"), profile)
    if not backup.exists():
        plan = _preparar(profile, request_path, request_raw, request, backup, package, guid)
    else:
        plan = _json(_caminho(package / "plano.json", package).read_bytes())
        if (plan.get("revisao") != "escolha-djanira-0.9.7-" + guid or plan.get("perfil") != str(profile)
                or _caminho(backup / "perfil" / request_path.relative_to(profile), backup).read_bytes() != request_raw):
            raise ValueError("O backup pertence a outro pedido; nenhum arquivo foi sobrescrito.")
    baseline = {r["relativo"]: _caminho(backup / "perfil" / r["relativo"], backup).read_bytes() for r in plan["preservados"]}
    for r in plan["preservados"]:
        if transacao.sha(baseline[r["relativo"]]) != r["sha256"]:
            raise ValueError("Backup protegido alterado; operação interrompida.")
    current, seed = _validar_base(baseline, request)
    candidate = _caminho(package / "novos" / RICA, package).read_bytes()
    visual, _ = _opcao(candidate, request, current)
    continuity_raw = _caminho(package / "continuidade.json", package).read_bytes()
    continuity = _json(continuity_raw)
    staged = {name: _caminho(package / "novos" / name, package).read_bytes()
              for name in (RICA, transacao.MANIFESTO, transacao.CATALOGO)}
    after = transacao.verificar_conjunto(baseline | staged)
    if (continuity.get("escolha") != request or continuity.get("backup") != str(backup)
            or continuity.get("personagens") != after or continuity.get("seed") != seed
            or continuity.get("sementeSHA256") != transacao.sha(baseline[SEMENTE])
            or continuity.get("pedidoSHA256") != transacao.sha(request_raw)):
        raise ValueError("Registro de continuidade divergente do pedido.")
    proof_path = _caminho(Path(continuity["provaGetters"]), profile)
    proof_raw = baseline[proof_path.relative_to(profile).as_posix()]
    if transacao.sha(proof_raw) != continuity["provaGettersSHA256"]:
        raise ValueError("Hash da prova dos getters divergiu.")
    conferir_prova(proof_raw, visual, request["opcao"])
    if continuity_path.exists() and continuity_path.read_bytes() != continuity_raw:
        raise ValueError("Um registro de continuidade anterior foi preservado.")
    key = plan["revisao"]
    if key in transacao.REVISOES:
        raise ValueError("Revisão já em uso.")
    expected_changed = {RICA, transacao.MANIFESTO, transacao.CATALOGO}
    original_verifier = transacao.verificar_conjunto
    original_writer = transacao.escrever

    def verificar_protegidos(contents):
        checked = original_verifier(contents)
        # O apply chama o verificador antes e depois das escritas, dentro do
        # journal/rollback. Conferir seed, prova e outros arquivos nas duas fases.
        for r in plan["preservados"]:
            if (r["relativo"] not in expected_changed
                    and transacao.sha(_caminho(profile / r["relativo"], profile).read_bytes()) != r["sha256"]):
                raise ValueError("Arquivo protegido mudou durante a escolha; alteração externa preservada.")
        return checked

    def escrever_com_espera(path, content, root):
        before = transacao.sha(path.read_bytes()) if path.exists() else None
        for attempt in range(4):
            try:
                return original_writer(path, content, root)
            except OSError as error:
                # Apenas sharing/lock violation. AccessDenied/política não recebe retry.
                if getattr(error, "winerror", None) not in (32, 33) or attempt == 3:
                    raise
                time.sleep(0.025)
                _caminho(path, root)
                observed = transacao.sha(path.read_bytes()) if path.exists() else None
                if observed not in (before, transacao.sha(content)):
                    raise ValueError("Edição concorrente preservada durante espera pelo arquivo.") from error

    transacao.REVISOES[key] = {"anteriores": {"DJANIRA_RICA": request["origemSHA256"]}}
    transacao.verificar_conjunto = verificar_protegidos
    transacao.escrever = escrever_com_espera
    try:
        finished = transacao.aplicar(package)
    finally:
        transacao.verificar_conjunto = original_verifier
        transacao.escrever = original_writer
        del transacao.REVISOES[key]
    if finished["estado"] != "concluida":
        raise ValueError("Publicação não concluída; consulte o journal do backup.")
    for r in plan["preservados"]:
        if r["relativo"] not in expected_changed and transacao.sha(_caminho(profile / r["relativo"], profile).read_bytes()) != r["sha256"]:
            raise ValueError("Arquivo protegido mudou durante a escolha; alteração externa preservada.")
    if not continuity_path.exists():
        _novo(continuity_path, continuity_raw, profile)
    final = _ler_conjunto(profile)
    transacao.verificar_conjunto(final)
    if transacao.sha(final[RICA]) != request["visualSHA256"]:
        raise ValueError("Hash final da roupa não corresponde à escolha.")
    return {"versao": 1, "status": "salvo", "mensagem": "Roupa escolhida salva; identidade e outros12 figurinos preservados.",
            **{key: request[key] for key in ("opcao", "visualSHA256", "origemSHA256")},
            "backup": str(backup), "continuidade": str(continuity_path)}


def _local_pedido(path):
    profile = ler_pasta_dados(RAIZ, exigir_perfil=True)
    path = _caminho(path, profile)
    match = PADRAO_PEDIDO.fullmatch(path.name)
    if path.parent != profile / "logs" or not match:
        raise ValueError("A escolha exige pedido no diretório logs do perfil; origem pode ser tecla ou aprovação expressa no chat.")
    result = _caminho(path.with_name(path.stem + "_resultado.json"), profile)
    return profile, path, result, match[1].lower()


def selecionar(path):
    profile, path, result_path, guid = _local_pedido(path)
    with _exclusivo(profile, path):
        raw = path.read_bytes()
        request = ler_pedido(raw)
        previous_raw = None
        if result_path.exists():
            previous_raw = result_path.read_bytes()
            previous = _json(previous_raw)
            if previous.get("status") not in ("salvo", "registro_pendente"):
                raise ValueError("Este pedido já terminou com erro; um novo pedido conserva seu resultado.")
            if (type(previous.get("versao")) is not int or previous["versao"] != 1
                    or any(previous.get(key) != request[key] for key in ("opcao", "visualSHA256", "origemSHA256"))):
                raise ValueError("Resultado anterior divergente foi preservado antes da publicação.")
            expected_backup = _caminho(RAIZ / "backups/escolha-djanira" / guid, RAIZ / "backups")
            expected_continuity = _caminho(profile / "catalogos" / ("figurinos_continuidade_0_9_7_" + guid + ".json"), profile)
            if previous.get("backup") != str(expected_backup) or previous.get("continuidade") != str(expected_continuity):
                raise ValueError("Resultado anterior tem origem divergente; preservado.")
            journal = _json(_caminho(expected_backup / "escolha-0.9.7/plano.json", expected_backup).read_bytes())
            if (journal.get("estado") != "concluida"
                    or transacao.sha(_caminho(profile / RICA, profile).read_bytes()) != request["visualSHA256"]):
                raise ValueError("Resultado anterior não corresponde à publicação concluída; preservado.")
        result = _executar(profile, path, raw, request, guid)
        result_raw = transacao.json_bytes(result)
        if result_path.exists():
            observed = result_path.read_bytes()
            if previous_raw is not None and previous.get("status") == "registro_pendente" and observed == previous_raw:
                backup = _caminho(Path(result["backup"]), RAIZ / "backups")
                saved = _caminho(backup / "resultados" / (transacao.sha(previous_raw) + ".json"), backup)
                if saved.exists():
                    if saved.read_bytes() != previous_raw:
                        raise ValueError("Backup do resultado pendente divergente; preservado.")
                else:
                    _novo(saved, previous_raw, backup)
                if result_path.read_bytes() != previous_raw:
                    raise ValueError("Resultado alterado durante a retomada; edição preservada.")
                transacao.escrever(result_path, result_raw, profile)
            elif observed != result_raw:
                raise ValueError("Resultado anterior divergente foi preservado.")
        else:
            _novo(result_path, result_raw, profile)
        return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pedido", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        result = selecionar(args.pedido)
        print(result["mensagem"])
        return 0
    except (OSError, ValueError, RuntimeError, KeyError, TypeError) as error:
        message = " ".join(str(error).split())[:300]
        if isinstance(error, EscolhaOcupada):
            print("Escolha em andamento: " + message, file=sys.stderr)
            return 1
        published = False
        try:
            profile, request_path, result_path, guid = _local_pedido(args.pedido)
            backup = _caminho(RAIZ / "backups/escolha-djanira" / guid, RAIZ / "backups")
            journal_path = _caminho(backup / "escolha-0.9.7/plano.json", backup)
            if journal_path.exists():
                request = ler_pedido(request_path.read_bytes())
                journal = _json(journal_path.read_bytes())
                published = (journal.get("estado") == "concluida"
                             and transacao.sha(_caminho(profile / RICA, profile).read_bytes()) == request["visualSHA256"])
            result = {"versao": 1, "status": "erro", "mensagem": message}
            if published:
                result.update(status="registro_pendente", **{key: request[key] for key in ("opcao", "visualSHA256", "origemSHA256")},
                              backup=str(backup), continuidade=str(profile / "catalogos" / ("figurinos_continuidade_0_9_7_" + guid + ".json")))
            if not result_path.exists():
                _novo(result_path, transacao.json_bytes(result), profile)
        except (OSError, ValueError, RuntimeError, TypeError, KeyError):
            pass  # Não escrever em um caminho não autorizado para reportar sua rejeição.
        prefix = "Roupa publicada; registro pendente. Repita o mesmo --pedido: " if published else "Escolha não salva: "
        print(prefix + message, file=sys.stderr)
        return 2 if published else 1


if __name__ == "__main__":
    raise SystemExit(main())
