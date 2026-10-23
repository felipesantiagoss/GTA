"""Runner local de producao: preparar CPU; montar/gravar/lote somente pelo operador.

Reusa inbox Operacao v1, F7 normal e CapturaDiretor. Nao envia Win+Alt+R,
nao recupera erros, nao avalia imagens e nao executa detector/exportador.
A primeira captura manual apos Reload continua no fluxo existente do Diretor.
"""
from __future__ import annotations

import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import shutil
import sys
import time
import uuid
import xml.etree.ElementTree as ET

import montar_video_episodio_limpo as montagem

editor = montagem.editor
PROJECT = Path(__file__).resolve().parents[1]
FAILED = re.compile(r"\[(?:ERRO[^\]]*|OPERACAO_PRODUCAO_ERRO|CAPTURA_CANCELADA|CAPTURA_RESULTADO)\]")


EP07_PARTS = ("embarque_dinjer_chefe", "embarque_djanira_decolagem")

def source_path(dados, code, parte=None):
    if parte is None:
        return editor.exigir_sem_vinculos(dados / "cenas" / (code + ".json"))
    if code != "EP07_C03" or parte not in EP07_PARTS:
        raise ValueError("Parte permitida somente para EP07_C03 e caminhos literais conhecidos.")
    return editor.exigir_sem_vinculos(dados / "cenas/subtomadas/EP07_C03" / parte / "EP07_C03.json")

def require_part_mounted(log_path, dados, code, parte):
    source = source_path(dados, code, parte)
    if code != "EP07_C03":
        return None
    rows = log_path.read_text("utf-8-sig").splitlines()
    loaded = max((i for i, row in enumerate(rows) if "[SCRIPT_CARREGADO]" in row), default=-1)
    mount = max((i for i, row in enumerate(rows) if "[MONTAGEM_INICIADA]" in row), default=-1)
    if loaded < 0 or mount <= loaded:
        raise RuntimeError("Falta montagem C3 na instancia realmente carregada.")
    evidence = [row for row in rows[mount + 1:] if "[EP07_SUBTOMADA_MONTADA]" in row]
    if mount < 0 or not evidence or "Cena=EP07_C03;" not in rows[mount]:
        raise RuntimeError("Falta montagem nativa C3 com parte/fonte/hash.")
    wanted = parte or "integral"
    expected = str(source.relative_to(dados / "cenas")).replace("\\", "/")
    fields = dict(re.findall(r"(?:^|; )([A-Za-z0-9]+)=([^;]*)", evidence[-1].split("] ", 1)[1]))
    if (fields.get("Parte") != wanted or fields.get("Arquivo") != expected or
            fields.get("SHA256") != editor.hash_arquivo(source) or fields.get("DuracaoMs") != "45000"):
        raise RuntimeError("Parte/path/hash nativos diferem da fonte solicitada; nenhuma entrada.")
    return {"parte": wanted, "arquivo": expected, "sourceSHA256": fields["SHA256"], "evento": evidence[-1],
            "versaoCodigo": fields.get("Versao"), "integral": parte is None}

def verify_part_session(dados, session, code, parte, source_sha, saved=False):
    if parte is None:
        return None
    if not re.fullmatch(r"[0-9a-f]{32}", session or "") or not re.fullmatch(r"[0-9A-F]{64}", source_sha or ""):
        raise ValueError("GUID/hash da parte fora do contrato.")
    source_path(dados, code, parte)  # whitelist
    path = dados / "logs/capturas" / session / ("proveniencia-subtomada-salva.xml" if saved else "proveniencia-subtomada-armada.xml")
    node = read_xml(path)
    expected = source_path(dados, code, parte)
    snapshot = dados / "logs/capturas" / session / "source-snapshot.json"
    if (node.tag != "subtomada" or node.get("versao") != "1" or node.get("id") != session or
            node.get("cenaId") != code or node.get("parte") != parte or
            Path(node.get("arquivoFonte", "")) != expected or Path(node.get("snapshot", "")) != snapshot or
            node.get("sha256Fonte") != source_sha or editor.hash_arquivo(snapshot) != source_sha or
            node.get("duracaoConteudoMs") != "45000" or node.get("integral") != "false" or
            node.get("estado") != ("salva" if saved else "armada")):
        raise RuntimeError("Proveniencia native da parte nao corresponde ao GUID/fonte45s.")
    return {"arquivo": str(path), "sha256": editor.hash_arquivo(path), "parte": parte, "sessao": session}

def write_json(path, value):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    os.replace(temporary, path)


def reference(path):
    path = editor.exigir_sem_vinculos(Path(path).absolute())
    return {'arquivo': str(path), 'sha256': editor.hash_arquivo(path)}


def work():
    base = editor.exigir_sem_vinculos(PROJECT / 'backups/operacao-sequencial')
    folder = base / (datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ') + '-' + uuid.uuid4().hex[:8])
    folder.mkdir(parents=True, exist_ok=False)
    return folder


def read_xml(path):
    raw = editor.exigir_sem_vinculos(path).read_bytes()
    if len(raw) > 1048576 or b'<!DOCTYPE' in raw.upper() or b'<!ENTITY' in raw.upper():
        raise ValueError('XML fora do contrato; nenhuma operacao enviada.')
    return ET.fromstring(raw)


class Log:
    def __init__(self, path):
        self.path = editor.exigir_sem_vinculos(path)
        self.offset = self.path.stat().st_size
        self.partial = b''
        self.events = []
        self.discard_checkpoint_partial = False

    def poll(self):
        if self.path.stat().st_size < self.offset:
            raise RuntimeError('Log reiniciado/truncado durante a operacao.')
        with self.path.open('rb') as stream:
            stream.seek(self.offset)
            raw = stream.read()
            self.offset = stream.tell()
        if self.discard_checkpoint_partial:
            _, separator, raw = raw.partition(b'\n')
            if not separator:
                return self.events
            self.discard_checkpoint_partial = False
        rows = (self.partial + raw).split(b'\n')
        self.partial = rows.pop()
        for row in rows:
            line = row.decode('utf-8-sig').rstrip('\r')
            self.events.append(line)
            if FAILED.search(line):
                raise RuntimeError('STOP: ' + line)
        return self.events

    def checkpoint(self):
        # Consumir/validar erros anteriores antes do unico F7; aguardar apenas novas linhas.
        self.poll()
        if self.partial and FAILED.search(self.partial.decode('utf-8-sig', errors='ignore')):
            raise RuntimeError('STOP: erro parcial anterior ao F7.')
        self.events.clear()
        # Uma linha iniciada antes do checkpoint continua antiga, mesmo que seu LF chegue depois.
        if self.partial:
            self.partial = b''
            self.discard_checkpoint_partial = True

    def wait(self, predicate, timeout):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            result = predicate(self.poll())
            if result is not None and result is not False:
                return result
            time.sleep(.15)
        raise TimeoutError('Prazo do runner esgotado; nao envia recuperacao nem outro atalho.')


@contextmanager
def exclusive(dados):
    path = editor.exigir_sem_vinculos(dados / 'logs/operacao_sequencial.lock')
    token = uuid.uuid4().hex
    with path.open('x', encoding='ascii') as stream:
        stream.write(token)
    try:
        yield
    finally:
        if path.read_text('ascii') == token:
            path.unlink()


def command(dados, folder, log, action, code=None, timeout=15):
    identity = uuid.uuid4().hex
    inbox = editor.exigir_sem_vinculos(dados / 'operacao_producao.xml')
    if inbox.exists():
        previous = read_xml(inbox)
        old_id = previous.get('id', '')
        if not re.fullmatch('[0-9a-f]{32}', old_id) or not (dados / 'logs' / ('operacao_producao_' + old_id + '.xml')).is_file():
            raise RuntimeError('Inbox anterior sem resultado: STOP, nao sobrescrever operacao pendente.')
        shutil.copy2(inbox, folder / ('inbox-anterior-' + identity + '.xml'))
    attributes = {'versao': '1', 'id': identity, 'acao': action}
    if code:
        montagem.identidade_cena(code)
        attributes['cena'] = code
    raw = ET.tostring(ET.Element('Operacao', attributes), encoding='utf-8', xml_declaration=True)
    (folder / ('operacao-' + identity + '.xml')).write_bytes(raw)
    temporary = inbox.with_name('operacao_producao_' + identity + '.tmp')
    with temporary.open('xb') as stream:
        stream.write(raw)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, inbox)
    response = dados / 'logs' / ('operacao_producao_' + identity + '.xml')

    def arrived(events):
        try:
            if not response.is_file():
                return None
            result = read_xml(response)
        except (ET.ParseError, OSError):
            return None  # XML incompleto/em escrita; mesmo prazo15s, sem reenviar operacao.
        if result.tag != 'Resultado' or result.get('id') != identity or result.get('acao') != action or result.get('estado') != 'concluida':
            raise RuntimeError('Operacao recusada: ' + ET.tostring(result, encoding='unicode'))
        return result
    result = log.wait(arrived, timeout)
    shutil.copy2(response, folder / response.name)
    return result.text or ''


def require_ready(message, code):
    values = dict(re.findall(r'(?:^|; )([A-Za-z]+)=([^;]*)', message))
    if values.get('cena') != code or values.get('pronta') != 'True' or values.get('elenco') != 'True':
        raise RuntimeError('STOP: status nao confirma a cena pronta/elenco: ' + message)



def wait_ready(dados, folder, log, code, deadline):
    # Consultas novas de status sao somente leitura; nunca repete montar/F7.
    while time.monotonic() < deadline:
        message = command(dados, folder, log, 'status', timeout=min(15, max(0, deadline - time.monotonic())))
        values = dict(re.findall(r'(?:^|; )([A-Za-z]+)=([^;]*)', message))
        if values.get('cena') != code or values.get('pronta') not in ('True', 'False') or values.get('elenco') not in ('True', 'False'):
            raise RuntimeError('STOP: status invalido ou pertence a outra cena: ' + message)
        if values['pronta'] == 'True' and values['elenco'] == 'True':
            require_ready(message, code)  # A garantia final continua estrita.
            return message
        time.sleep(.15)
    raise TimeoutError('Prazo do runner esgotado aguardando todas as barreiras prontas; nao repete montar/F7.')


def current_source(dados, code, folder, parte=None):
    path = source_path(dados, code, parte)
    ref = reference(path)
    destination = folder / (code + '.snapshot-prearm.json')
    shutil.copy2(path, destination)
    snapshot = reference(destination)
    if snapshot['sha256'] != ref['sha256'] or path.stat().st_mtime_ns != destination.stat().st_mtime_ns:
        raise RuntimeError('Snapshot nao conserva bytes/mtime da fonte antes de armar.')
    duration = montagem.duracao_snapshot(snapshot, code)
    return snapshot, duration, ref


def require_source_mounted(log_path, code, source):
    lines = log_path.read_text('utf-8-sig').splitlines()
    mounted = [row for row in lines if '[MONTAGEM_INICIADA] Cena=' + code + ';' in row]
    if not mounted:
        raise RuntimeError('Montagem correspondente nao consta no log.')
    at = datetime.fromisoformat(mounted[-1].split(' ', 1)[0]).timestamp()
    if source.stat().st_mtime_ns > int(at * 1_000_000_000):
        raise RuntimeError('Fonte runtime mudou depois de montar; remonte antes de gravar.')


def require_automatic(log_path):
    lines = log_path.read_text('utf-8-sig').splitlines()
    loaded = [i for i, line in enumerate(lines) if '[SCRIPT_CARREGADO]' in line]
    if not loaded or not any('[CAPTURA_SALVA]' in line for line in lines[loaded[-1] + 1:]):
        raise RuntimeError('STOP: falta primeira captura manual conferida nesta instancia apos Reload.')


def mount(dados, code, folder, parte=None):
    import operar_gta
    window = operar_gta.game_window()
    operar_gta.focus_game_window(window)
    operar_gta.no_held_keys()
    log = Log(dados / 'logs/DiretorGTA.log')
    command(dados, folder, log, 'limpar')
    command(dados, folder, log, 'montar', code)
    deadline = time.monotonic() + 35
    log.wait(lambda events: any('[MONTAGEM_INICIADA] Cena=' + code + ';' in row for row in events)
             and next((row for row in events if '[CAMERA_PRONTA]' in row), None), max(0, deadline - time.monotonic()))
    wait_ready(dados, folder, log, code, deadline)
    if parte is not None:
        source_path(dados, code, parte)
        # Canonical inbox resets C3 to Integral. Cada T e unico e documentado; nao reenviar em timeout.
        for step in range(1 if parte == EP07_PARTS[0] else 2):
            previous = len(log.events)
            write_json(folder / ("antes-T-%d.json" % step), {"cena": code, "parte": parte, "entradaUnica": "T", "etapa": step})
            operar_gta.focus_game_window(window)
            operar_gta.no_held_keys()
            operar_gta.press(window, [0x54])
            log.wait(lambda events: any("[MONTAGEM_INICIADA] Cena=EP07_C03;" in row for row in events[previous:]) and
                     any("[CAMERA_PRONTA]" in row for row in events[previous:]), 35)
            wait_ready(dados, folder, log, code, time.monotonic() + 15)
    part_proof = require_part_mounted(log.path, dados, code, parte)
    return {'cena': code, 'parte': parte, 'provenienciaSubtomada': part_proof, 'estado': 'montada_getters_prontos', 'eventos': log.events,
            'cameraAvaliada': False, 'gravada': False}


def capture(dados, code, folder, parte=None):
    log = Log(dados / 'logs/DiretorGTA.log')
    require_automatic(log.path)
    wait_ready(dados, folder, log, code, time.monotonic() + 15)
    part_proof = require_part_mounted(log.path, dados, code, parte)
    require_source_mounted(log.path, code, source_path(dados, code, parte))
    snapshot, duration, original = current_source(dados, code, folder, parte)
    # Foco normal autorizado; press() continua exigindo a janela real do GTA.
    import operar_gta
    window = operar_gta.game_window()
    operar_gta.focus_game_window(window)
    operar_gta.no_held_keys()
    log.checkpoint()
    operar_gta.press(window, [operar_gta.KEYS['F7']])

    def prepared(events):
        for row in events:
            if '[CAPTURA_PRIMEIRA_ACAO_PREPARADA]' in row:
                raise RuntimeError('STOP: Diretor exige arm/manual; F7 enviado uma vez, nao repetir.')
            if '[CAPTURA_PREPARADA]' in row:
                match = re.search(r'Sess\u00e3o=([0-9a-f]{32}); cena=([^;]+);', row)
                if not match or match.group(2) != code:
                    raise RuntimeError('Sessao armada pertence a outra cena.')
                return match.group(1)
        return None
    session = log.wait(prepared, 12)
    native_arm = verify_part_session(dados, session, code, parte, original["sha256"])
    log.wait(lambda events: next((row for row in events if '[CAPTURA_INICIO_CONFIRMADO]' in row), None), 32)
    log.wait(lambda events: next((row for row in events if '[ACAO_INICIADA] Cena=' + code + ';' in row), None), 12)
    log.wait(lambda events: next((row for row in events if '[ACAO_CONCLUIDA] Cena=' + code + ';' in row), None), duration / 1000 + 30)
    log.wait(lambda events: next((row for row in events if '[CAPTURA_SALVA] Sess\u00e3o=' + session + ';' in row), None), 90)
    if reference(Path(original['arquivo']))['sha256'] != original['sha256']:
        raise RuntimeError('Fonte runtime mudou durante a tomada; preservada, requer revisao de proveniencia.')
    native_save = verify_part_session(dados, session, code, parte, original["sha256"], saved=True)
    take = montagem.tomada_real(dados, code, session)
    if take['duracaoAcaoMs'] != duration:
        raise RuntimeError('Duracao nativa difere do snapshot prearm; nao promover.')
    for name in ('armada.xml', 'iniciada.xml', 'sessao.xml', 'resultado.xml', 'manifesto-original.json', 'proveniencia-subtomada-armada.xml', 'proveniencia-subtomada-salva.xml'):
        source = dados / 'logs/capturas' / session / name
        if source.is_file():
            shutil.copy2(editor.exigir_sem_vinculos(source), folder / name)
    return {'cena': code, 'estado': 'captura_real_salva_aguarda_detector_e_root', 'tomada': take,
            'fonteJsonSnapshot': snapshot, 'hashFonteConferidoAntesDeArmar': True,
            'hashFonteRegistradoPeloNativeDuranteCaptura': False,
            'hashFonteNativeAntesArmEAposSave': parte is not None,
            'parte': parte, 'provenienciaSubtomada': part_proof, 'nativeArm': native_arm, 'nativeSave': native_save, 'eventos': log.events,
            'offsetPtsMs': None, 'aprovacaoVisual': False, 'semTextos': False, 'exportavel': False,
            'proximoComandoCPU': [sys.executable, str(PROJECT / 'ferramentas/calibrar_contagem_template.py'),
                                 'detectar-contagem', '--cena', code, '--sessao', session,
                                 '--fonte', snapshot['arquivo']]}


def prepare(codes, folder):
    if not codes or len(codes) != len(set(codes)):
        raise ValueError('Informe IDs originais/extras unicos em ordem explicita.')
    sources = []
    for code in codes:
        episode, _ = montagem.identidade_cena(code)
        category = 'extras_serie' if code in montagem.SUPLEMENTARES else 'cenas_serie'
        source = PROJECT / 'producao' / category / episode / (code + '.json')
        ref = reference(source)
        sources.append({'cena': code, 'fonte': ref, 'duracaoMs': montagem.duracao_snapshot(ref, code)})
    return {'tipo': 'plano_runner_capturas_v1', 'cenas': sources, 'aprovacaoVisual': False}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dados', type=Path, default=Path.home() / 'GTA_Diretor')
    commands = parser.add_subparsers(dest='action', required=True)
    p = commands.add_parser('preparar', help='Somente CPU: plano com fontes/hashes/duracoes reais.')
    p.add_argument('--cena', action='append', required=True)
    for name in ('montar', 'gravar'):
        p = commands.add_parser(name, help='Operador: ' + ('aguardar camera/getters, sem gravar.' if name == 'montar' else 'F7 automatico uma vez; salvar sem aprovacao.'))
        p.add_argument('--cena', required=True)
        p.add_argument('--parte', choices=EP07_PARTS)
    p = commands.add_parser('lote', help='Operador: montar/gravar sequencial; STOP no primeiro erro; sem avaliacao.')
    p.add_argument('--plano', type=Path, required=True)
    args = parser.parse_args(argv)
    if getattr(args, 'parte', None) and args.cena != 'EP07_C03':
        parser.error('--parte exige EP07_C03')
    folder = work()
    outcome = {'tipo': 'resultado_runner_capturas_v1', 'comando': args.action, 'resultados': [], 'exportavel': False}
    try:
        if args.action == 'preparar':
            plan = prepare(args.cena, folder)
            write_json(folder / 'plano.json', plan)
            print(json.dumps({'plano': str(folder / 'plano.json'), 'sha256': editor.hash_arquivo(folder / 'plano.json')}, ensure_ascii=True))
            return 0
        dados = editor.exigir_sem_vinculos(args.dados.absolute())
        if args.action == 'lote':
            plan = editor.ler_json(editor.exigir_sem_vinculos(args.plano))
            if plan.get('tipo') != 'plano_runner_capturas_v1' or not isinstance(plan.get('cenas'), list) or not plan['cenas']:
                raise ValueError('Plano de runner invalido.')
            codes = [item['cena'] for item in plan['cenas']]
            if len(codes) != len(set(codes)):
                raise ValueError('Plano repetiu IDs; nenhuma montagem enviada.')
            for item in plan['cenas']:
                montagem.identidade_cena(item['cena'])
                episode, _ = montagem.identidade_cena(item['cena'])
                category = 'extras_serie' if item['cena'] in montagem.SUPLEMENTARES else 'cenas_serie'
                canonical = PROJECT / 'producao' / category / episode / (item['cena'] + '.json')
                if reference(canonical) != item['fonte'] or montagem.duracao_snapshot(item['fonte'], item['cena']) != item.get('duracaoMs'):
                    raise ValueError('Fonte/duracao planejada mudou ou nao e a fonte que montar usa; prepare novo plano.')
        else:
            codes = [args.cena]
            montagem.identidade_cena(args.cena)
        with exclusive(dados):
            for index, code in enumerate(codes):
                stage = folder / ('%02d-' % (index + 1) + code)
                stage.mkdir()
                if args.action in ('montar', 'lote'):
                    outcome['resultados'].append(mount(dados, code, stage, getattr(args, 'parte', None)))
                    write_json(folder / 'resultado.json', outcome)
                if args.action in ('gravar', 'lote'):
                    outcome['resultados'].append(capture(dados, code, stage, getattr(args, 'parte', None)))
                    write_json(folder / 'resultado.json', outcome)
        outcome['estado'] = 'concluido_sem_avaliacao_visual'
    except Exception as error:
        outcome['estado'] = 'STOP'
        outcome['erro'] = str(error)
        write_json(folder / 'resultado.json', outcome)
        print(json.dumps({'resultado': str(folder / 'resultado.json'), 'estado': 'STOP', 'erro': str(error)}, ensure_ascii=True))
        return 1
    write_json(folder / 'resultado.json', outcome)
    print(json.dumps({'resultado': str(folder / 'resultado.json'), 'estado': outcome['estado']}, ensure_ascii=True))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
