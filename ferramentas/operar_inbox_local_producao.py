"""Um comando de exploracao por vez, pelas inboxes nativas existentes.

Somente o operador ROOT pode executar este CLI enquanto GTA/GPU/decoders estao
disponiveis. --somente-preparar e importacao nao operam GUI nem escrevem inbox.
Nao envia teclas, nao abre menus e nao tenta recuperar/reenviar uma operacao.
"""
from __future__ import annotations

import argparse
from contextlib import contextmanager
import configparser
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import sys
import time
import uuid
import xml.etree.ElementTree as ET

PROJECT = Path('C:/GTA_Diretor')
PRODUCTION_ACTIONS = ('status', 'visitar', 'marcar', 'limpar')
LOCAL_ACTIONS = ('status', 'sondar', 'andar', 'virar', 'olhar', 'abrirPorta', 'vias')
FIELDS = {'status': (), 'sondar': (), 'abrirPorta': (), 'vias': (), 'andar': ('frente', 'lado', 'velocidade', 'timeoutSegundos'),
          'virar': ('delta',), 'olhar': ('headingRelativo', 'pitch')}


def utc():
    return datetime.now(timezone.utc).isoformat()


def digest(raw):
    return hashlib.sha256(raw).hexdigest().upper()


def plain(path):
    path = Path(path).absolute()
    for item in (path, *path.parents):
        try:
            info = item.lstat()
        except FileNotFoundError:
            continue
        if stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & 0x400:
            raise RuntimeError('Caminho com vinculo/reparse recusado: ' + str(item))
    return path


def strict_json(raw):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('Chave JSON duplicada: ' + key)
            result[key] = value
        return result
    def invalid(value):
        raise ValueError('Numero JSON nao finito: ' + value)
    return json.loads(raw.decode('utf-8-sig'), object_pairs_hook=unique, parse_constant=invalid)


def xml(raw, limit=1048576):
    if len(raw) > limit or b'<!DOCTYPE' in raw.upper() or b'<!ENTITY' in raw.upper():
        raise ValueError('XML fora do contrato ou com DTD/entidade.')
    return ET.fromstring(raw)


def identity(value):
    if not re.fullmatch('[0-9a-f]{32}', value):
        raise ValueError('ID deve ser GUID32 hexadecimal gerado por este operador.')
    return value


def runtime_path(project=PROJECT):
    cfg = configparser.ConfigParser(interpolation=None)
    cfg.read(plain(project / 'configuracoes/Instalacao.ini'), encoding='utf-8-sig')
    workspace = plain(os.path.expandvars(cfg['Projeto']['Pasta']))
    if workspace != plain(project):
        raise RuntimeError('Ponte pertence a outro workspace.')
    return plain(os.path.expandvars(cfg['Projeto']['Dados']))


def location_codes(project=PROJECT):
    source = plain(project / 'src/DiretorGTA.3.cs').read_text(encoding='utf-8-sig')
    match = re.search(r'productionLocations\s*=\s*\{([^}]+)\}', source)
    if not match:
        raise ValueError('Lista real de locais do Main nao encontrada.')
    return set(re.findall(r'"([A-Z0-9_]+)"', match.group(1))) - {'AEROPORTO_AR'}


def number(value, lower, upper, name, positive=False):
    if not isinstance(value, str) or not re.fullmatch(r'-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?', value):
        raise ValueError(name + ' exige numero decimal simples, sem NaN/infinito/expoente.')
    try:
        parsed = Decimal(value)
    except InvalidOperation:
        raise ValueError(name + ' nao e numero valido.')
    if not parsed.is_finite() or parsed < Decimal(str(lower)) or parsed > Decimal(str(upper)) or positive and parsed <= 0:
        raise ValueError(name + ' fora dos limites nativos.')
    return parsed


def build_command(channel, action, fields, command_id=None, project=PROJECT, check_resources=True):
    command_id = identity(command_id or uuid.uuid4().hex)
    command = {'versao': 1, 'id': command_id, 'acao': action}
    if channel == 'producao':
        if action not in PRODUCTION_ACTIONS:
            raise ValueError('Acao XML nao suportada neste operador.')
        allowed = {'local'} if action == 'marcar' else {'local', 'referencia'} if action == 'visitar' else set()
        if set(fields) - allowed:
            raise ValueError('Campos extras XML recusados.')
        if action in ('marcar', 'visitar'):
            code = fields.get('local')
            if not isinstance(code, str) or code not in location_codes(project):
                raise ValueError('Local nao terrestre ou ausente do Main real.')
            command['local'] = code
        ref = fields.get('referencia', '')
        if ref not in ('', 'autor', 'viaProxima'):
            raise ValueError('Referencia XML exige autor/viaProxima ou vazio.')
        if ref:
            command['referencia'] = ref
        if check_resources and action == 'visitar' and ref == 'autor':
            source = plain(project / 'producao/locacoes_referencia' / (code + '.xml'))
            node = xml(source.read_bytes(), 4096)
            if node.tag != 'Referencia' or node.get('local') != code or not node.get('origem', '').startswith('https://'):
                raise ValueError('Referencia de autor sem origem primaria/local correspondente.')
            for child in node:
                if child.tag != 'Ipl' or not re.fullmatch('[a-z0-9_]{1,96}', child.get('nome', '')):
                    raise ValueError('IPL da referencia invalido.')
            for key, low, high in (('x', -15000, 15000), ('y', -15000, 15000), ('z', -3000, 3000), ('direcao', 0, 360)):
                measured = number(node.get(key), low, high, key)
                if key == 'direcao' and measured == 360:
                    raise ValueError('direcao deve ser menor que360.')
    elif channel == 'local':
        if action not in LOCAL_ACTIONS or set(fields) != set(FIELDS[action]):
            raise ValueError('Campos JSON devem corresponder exatamente a acao nativa.')
        bounds = {'frente': (-15, 15), 'lado': (-15, 15), 'velocidade': (0, 3), 'timeoutSegundos': (.5, 15),
                  'delta': (-180, 180), 'headingRelativo': (-180, 180), 'pitch': (-60, 60)}
        for name in FIELDS[action]:
            command[name] = number(fields[name], *bounds[name], name, positive=name == 'velocidade')
        if action == 'andar':
            if not re.fullmatch(r'[0-9]+(?:\.[0-9]{1,3})?', fields['timeoutSegundos']):
                raise ValueError('timeoutSegundos deve ter ate3 casas e resolucao exata de ms.')
    else:
        raise ValueError('Canal deve ser producao/local.')
    return command


def serialize(channel, command):
    if channel == 'producao':
        return ET.tostring(ET.Element('Operacao', {key: str(value) for key, value in command.items()}),
                           encoding='utf-8', xml_declaration=True)
    pairs = []
    for key, value in command.items():
        token = format(value, 'f') if isinstance(value, Decimal) else json.dumps(value, ensure_ascii=True)
        pairs.append(json.dumps(key) + ':' + token)
    return ('{' + ','.join(pairs) + '}\n').encode('ascii')


def inbox_path(runtime, channel):
    return plain(runtime / ('operacao_producao.xml' if channel == 'producao' else 'operacao_local.json'))


def response_path(runtime, channel, command_id):
    identity(command_id)
    return plain(runtime / 'logs' / ('operacao_' + channel + '_' + command_id + ('.xml' if channel == 'producao' else '.json')))


def read_response(raw, channel, command_id, action):
    if channel == 'producao':
        node = xml(raw)
        if node.tag != 'Resultado':
            raise ValueError('Resposta XML nao e Resultado.')
        result = dict(node.attrib, mensagem=node.text or '')
    else:
        if len(raw) > 1048576:
            raise ValueError('Resposta JSON maior que limite.')
        result = strict_json(raw)
    if not isinstance(result, dict) or result.get('id') != command_id or result.get('acao') != action:
        raise ValueError('Resposta pertence a outro GUID/acao.')
    if result.get('estado') not in ('concluida', 'erro', 'rejeitada', 'cancelada'):
        raise ValueError('Resultado ainda nao e terminal; receipt aceita nao conclui operacao.')
    return result


def prior_inbox(runtime, channel):
    path = inbox_path(runtime, channel)
    if not path.exists():
        return None
    raw = path.read_bytes()
    if len(raw) > 4096:
        raise ValueError('Inbox anterior excede4096bytes; preservar sem sobrescrever.')
    node = xml(raw, 4096) if channel == 'producao' else strict_json(raw)
    if channel == 'producao' and (node.tag != 'Operacao' or node.get('versao') != '1' or len(node) or
        set(node.attrib) - {'versao', 'id', 'acao', 'cena', 'local', 'referencia'}):
        raise ValueError('Inbox XML anterior malformada; preservar sem sobrescrever.')
    if channel == 'local' and (not isinstance(node, dict) or node.get('versao') != 1 or isinstance(node.get('versao'), bool)):
        raise ValueError('Inbox JSON anterior malformada; preservar sem sobrescrever.')
    old_id = node.get('id', '')
    old_action = node.get('acao', '')
    identity(old_id)
    result_path = response_path(runtime, channel, old_id)
    if not result_path.is_file():
        raise RuntimeError('Inbox ' + channel + ' anterior sem resultado final: ' + old_id + '; nao sobrescrever/reenviar.')
    result_raw = result_path.read_bytes()
    result = read_response(result_raw, channel, old_id, old_action)
    return {'path': path, 'raw': raw, 'id': old_id, 'action': old_action,
            'resultPath': result_path, 'resultRaw': result_raw, 'result': result}


def atomic_new_or_replace(path, raw, previous=None):
    path = plain(path)
    temporary = plain(path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp'))
    with temporary.open('xb') as stream:
        stream.write(raw)
        stream.flush()
        os.fsync(stream.fileno())
    try:
        now = path.read_bytes() if path.exists() else None
        if now != previous:
            raise RuntimeError('Inbox/artefato mudou desde snapshot; comando nao publicado.')
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()  # Somente o temporario proprio; nao remove inbox/comando anterior.


def durable_new(path, raw):
    path = plain(path)
    with path.open('xb') as stream:
        stream.write(raw)
        stream.flush()
        os.fsync(stream.fileno())


def journal(folder, value):
    row = dict(value, utc=utc())
    with (folder / 'operador.jsonl').open('a', encoding='utf-8') as stream:
        stream.write(json.dumps(row, ensure_ascii=False, default=str) + '\n')
        stream.flush()
        os.fsync(stream.fileno())


@contextmanager
def exclusive(runtime):
    # Mesmo lock do runner de montagem/gravação: recusar, nunca apagar lock alheio.
    path = plain(runtime / 'logs/operacao_sequencial.lock')
    token = uuid.uuid4().hex
    durable_new(path, token.encode('ascii'))
    try:
        yield
    finally:
        if path.read_bytes() == token.encode('ascii'):
            path.unlink()


def new_work(project=PROJECT):
    folder = plain(project / 'backups/operacao-exploracao' /
        (datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ') + '-' + uuid.uuid4().hex[:8]))
    folder.mkdir(parents=True, exist_ok=False)
    return folder


def windows_guard(folder, project=PROJECT):
    # Importacao apenas na execucao ROOT: testes/preparar nunca chamam Windows.
    sys.path.insert(0, str(project / 'ferramentas'))
    import retomar_producao as resume
    idle = resume.idle_guard(timeout=15)
    window = resume.gta.game_window()
    resume.gta.focus_game_window(window)
    resume.gta.no_held_keys()
    if resume.gta.foreground_hwnd() != window['hwnd']:
        raise RuntimeError('Foco GTA nao confirmado; nenhuma inbox escrita.')
    journal(folder, {'etapa': 'guard_windows', 'operador': '/root', 'idleAntesMs': idle,
                     'janela': window, 'apiNormalExistente': True})
    def check():
        actual = resume.gta.describe(window['hwnd'])
        if resume.gta.foreground_hwnd() != window['hwnd'] or actual['pid'] != window['pid'] or actual['exe'].casefold() != str(resume.gta.GAME).casefold():
            raise RuntimeError('Foco/processo mudou; nenhuma repeticao/recuperacao automatica.')
        resume.gta.no_held_keys()
    return check


def wait_result(runtime, channel, command_id, action, folder, timeout=15, guard=lambda: None):
    if not 0 < timeout <= 15:
        raise ValueError('Espera deve ser maior que0 e ate15s; nao reenviar ao expirar.')
    path = response_path(runtime, channel, command_id)
    receipt = plain(runtime / 'logs' / ('operacao_local_' + command_id + '.aceita.json')) if channel == 'local' else None
    deadline = time.monotonic() + timeout
    last_parse = None
    while time.monotonic() < deadline:
        guard()
        if receipt and receipt.exists() and not (folder / receipt.name).exists():
            try:
                raw = receipt.read_bytes()
                ack = strict_json(raw)
                if ack.get('id') != command_id or ack.get('acao') != action or ack.get('estado') != 'aceita':
                    raise ValueError('Receipt pertence a outra operacao.')
                durable_new(folder / receipt.name, raw)
                journal(folder, {'etapa': 'receipt_aceita', 'id': command_id, 'concluida': False})
            except (OSError, json.JSONDecodeError) as exc:
                last_parse = str(exc)  # Esperar apenas fechamento desta mesma resposta.
        if path.exists():
            try:
                raw = path.read_bytes()
                result = read_response(raw, channel, command_id, action)
            except (OSError, ET.ParseError, json.JSONDecodeError) as exc:
                last_parse = str(exc)
            else:
                durable_new(folder / path.name, raw)
                journal(folder, {'etapa': 'resultado_nativo', 'id': command_id, 'resultado': result,
                                 'arquivo': str(path), 'sha256': digest(raw)})
                return result
        time.sleep(.1)
    raise TimeoutError('GUID ' + command_id + ' sem resultado final em15s; inbox preservada; use aguardar, sem reenviar. Ultima leitura=' + str(last_parse))


def snapshot_prior(folder, prior, channel):
    if prior:
        durable_new(folder / ('inbox-anterior-' + channel + ('.xml' if channel == 'producao' else '.json')), prior['raw'])
        durable_new(folder / ('resultado-anterior-' + prior['resultPath'].name), prior['resultRaw'])


def save_log_tail(runtime, folder, offset):
    log = plain(runtime / 'logs/DiretorGTA.log')
    if not log.exists():
        return
    with log.open('rb') as stream:
        if log.stat().st_size < offset:
            raise RuntimeError('Log foi truncado durante operacao; checkpoint nao corresponde.')
        stream.seek(offset)
        tail = stream.read()
    durable_new(folder / 'DiretorGTA.novo.log', tail)


def execute(channel, command, runtime, folder, timeout=15, guard_factory=windows_guard, project=PROJECT):
    if response_path(runtime, channel, command['id']).exists() or channel == 'local' and plain(runtime / 'logs' / ('operacao_local_' + command['id'] + '.aceita.json')).exists():
        raise RuntimeError('GUID ja possui resultado; nunca reenviar idempotente.')
    log = plain(runtime / 'logs/DiretorGTA.log')
    offset = log.stat().st_size
    with exclusive(runtime):
        prior = prior_inbox(runtime, channel)
        # Uma operacao fisica no outro canal tambem impede mutacoes concorrentes.
        other = 'local' if channel == 'producao' else 'producao'
        if command['acao'] != 'status':
            prior_inbox(runtime, other)
        snapshot_prior(folder, prior, channel)
        guard = guard_factory(folder, project)
        guard()
        raw = serialize(channel, command)
        path = inbox_path(runtime, channel)
        if channel == 'producao' and command['acao'] == 'marcar':
            marks = plain(runtime / 'catalogos/locais_marcados.xml')
            durable_new(folder / 'locais_marcados.antes.xml', marks.read_bytes())
        durable_new(folder / ('comando-' + command['id'] + ('.xml' if channel == 'producao' else '.json')), raw)
        journal(folder, {'etapa': 'antes_publicar', 'canal': channel, 'id': command['id'],
                         'comando': command, 'sha256': digest(raw), 'logOffset': offset,
                         'retryPermitido': False, 'guardasNativasHistoriaPausaFocoMantidasSemBypass': True})
        guard()
        try:
            atomic_new_or_replace(path, raw, prior['raw'] if prior else None)
            journal(folder, {'etapa': 'inbox_publicada_uma_vez', 'canal': channel, 'id': command['id']})
            result = wait_result(runtime, channel, command['id'], command['acao'], folder, timeout, guard)
            if channel == 'producao' and command['acao'] == 'marcar' and result['estado'] == 'concluida':
                marks = plain(runtime / 'catalogos/locais_marcados.xml')
                durable_new(folder / 'locais_marcados.depois.xml', marks.read_bytes())
                nodes = [ET.tostring(node, encoding='utf-8') for node in xml(marks.read_bytes()).iter()
                         if node.get('id') == command['local']]
                if len(nodes) != 1:
                    raise RuntimeError('Resultado marcar nao produziu um unico no da marca solicitada.')
                durable_new(folder / (command['local'] + '.marca-real.xml'), nodes[0])
            return result
        finally:
            save_log_tail(runtime, folder, offset)


def parser():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--timeout', type=float, default=15, help='Espera de resultado<=15s; nenhuma repeticao de comando.')
    ap.add_argument('--somente-preparar', action='store_true', help='Somente backup local; nao escreve runtime nem usa GUI.')
    sub = ap.add_subparsers(dest='canal', required=True)
    prod = sub.add_parser('producao')
    prod.add_argument('acao', choices=PRODUCTION_ACTIONS)
    prod.add_argument('--local')
    prod.add_argument('--referencia', choices=('autor', 'viaProxima'))
    local = sub.add_parser('local')
    local.add_argument('acao', choices=LOCAL_ACTIONS)
    for flag in ('frente', 'lado', 'velocidade', 'timeoutSegundos', 'delta', 'headingRelativo', 'pitch'):
        local.add_argument('--' + flag)
    wait = sub.add_parser('aguardar', help='Consulta somente leitura de GUID existente; nao publica nem reenvia.')
    wait.add_argument('--origem', choices=('producao', 'local'), required=True)
    wait.add_argument('--id', required=True)
    wait.add_argument('--acao', required=True)
    return ap


def main(argv=None):
    ap = parser()
    args = ap.parse_args(argv)
    if not 0 < args.timeout <= 15:
        ap.error('--timeout deve ser maior que0 e ate15.')
    folder = None
    command = None
    try:
        runtime = runtime_path()
        folder = new_work()
        journal(folder, {'etapa': 'iniciar', 'operador': '/root', 'somentePreparar': args.somente_preparar,
                         'projeto': str(PROJECT), 'dados': str(runtime), 'cli': argv or sys.argv[1:]})
        if args.canal == 'aguardar':
            identity(args.id)
            allowed = PRODUCTION_ACTIONS if args.origem == 'producao' else LOCAL_ACTIONS
            if args.acao not in allowed:
                raise ValueError('Acao de aguardar nao pertence ao canal.')
            if args.somente_preparar:
                raise ValueError('aguardar ja e somente leitura; nao usa somente-preparar.')
            result = wait_result(runtime, args.origem, args.id, args.acao, folder, args.timeout)
        else:
            fields = {key: value for key, value in vars(args).items()
                      if key not in ('canal', 'acao', 'timeout', 'somente_preparar') and value is not None}
            command = build_command(args.canal, args.acao, fields)
            if args.somente_preparar:
                durable_new(folder / ('comando-preparado.xml' if args.canal == 'producao' else 'comando-preparado.json'), serialize(args.canal, command))
                result = {'estado': 'preparada_nao_publicada', 'id': command['id'], 'acao': command['acao']}
            else:
                result = execute(args.canal, command, runtime, folder, args.timeout)
        summary = {'resultado': result, 'backup': str(folder), 'nenhumComandoReenviado': True,
                   'visitaNaoConfirmaMarcaOuPiso': args.canal == 'producao' and args.acao == 'visitar'}
        durable_new(folder / 'resultado-operador.json', (json.dumps(summary, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
        print(json.dumps(summary, ensure_ascii=False))
        return 0 if result['estado'] in ('concluida', 'preparada_nao_publicada') else 2
    except Exception as exc:
        summary = {'estado': 'erro_ou_pendente', 'erro': str(exc), 'backup': str(folder) if folder else None,
                   'id': command.get('id') if command else getattr(args, 'id', None),
                   'nenhumComandoReenviado': True, 'inboxPreservada': True}
        if folder:
            journal(folder, dict(summary, etapa='erro_operador'))
            durable_new(folder / 'resultado-operador.json', (json.dumps(summary, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
        print(json.dumps(summary, ensure_ascii=False))
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
