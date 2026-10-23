"""Promove uma candidata revisada, preservando fontes e atualizando só seu hash.

Não monta/grava, não altera catálogos ou flags de aprovação. Requer remontagem.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys
import uuid
import xml.etree.ElementTree as ET

from validar_json import validar_cena_json

ROOT = Path(__file__).resolve().parents[1]
DATA = Path.home() / 'GTA_Diretor'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--candidata', type=Path, required=True)
    p.add_argument('--esperado', required=True, help='SHA atual da fonte canônica')
    p.add_argument('--sha-candidata', required=True)
    a = p.parse_args()
    candidate = a.candidata.resolve(strict=True)
    if sha(candidate) != a.sha_candidata:
        raise ValueError('Candidata mudou depois da revisão')
    scene = json.loads(candidate.read_text('utf-8-sig'))
    code = scene['id']
    if not re.fullmatch(r'EP(0[1-9]|1[0-5])_C[0-9]{2}', code):
        raise ValueError('Somente cena narrativa de produção')
    validar_cena_json(candidate, DATA / 'catalogos')
    canonical = ROOT / 'producao/cenas_serie' / code[:4] / (code + '.json')
    runtime = DATA / 'cenas' / (code + '.json')
    index = ROOT / 'producao/cenas_serie/selecao.xml'
    if sha(canonical) != a.esperado:
        raise ValueError('Canônica diverge da base revisada; preservar e investigar')
    if runtime.exists() and sha(runtime) != a.esperado:
        raise ValueError('Runtime diverge da base revisada; preservar e investigar')
    original_index = index.read_bytes()
    rows = ET.fromstring(original_index).findall('.//Cena[@id="' + code + '"]')
    if len(rows) != 1 or rows[0].get('sha256') != a.esperado:
        raise ValueError('Índice diverge da base revisada; conferir antes da promoção')
    expression = (rb'(<Cena id="' + code.encode('ascii') +
                  rb'"[^\r\n]*sha256=")' + a.esperado.encode('ascii') + rb'(")')
    new_index, count = re.subn(expression,
        lambda m: m[1] + a.sha_candidata.encode('ascii') + m[2], original_index)
    if count != 1:
        raise ValueError('Não foi possível atualizar somente o hash dessa cena')
    folder = ROOT / 'backups/promocao-revisada' / (
        datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ') + '-' + code + '-' + uuid.uuid4().hex[:8])
    folder.mkdir(parents=True)
    targets = [canonical, runtime, index]
    records = []
    for i, target in enumerate(targets):
        saved = folder / ('%02d-' % i + target.name)
        existed = target.exists()
        if existed:
            shutil.copy2(target, saved)
            if sha(target) != sha(saved):
                raise ValueError('Backup divergiu; nenhuma alteração')
        records.append({'arquivo': str(target), 'existia': existed,
                        'backup': str(saved) if existed else None})
    journal = {'cena': code, 'candidata': str(candidate), 'sha256': a.sha_candidata,
               'estado': 'backups_conferidos', 'arquivos': records,
               'gravada': False, 'remontagemObrigatoria': True}
    def save(state):
        journal.update(estado=state, utc=datetime.now(timezone.utc).isoformat())
        (folder / 'checkpoint.json').write_text(
            json.dumps(journal, ensure_ascii=False, indent=2), encoding='utf-8')
    save('backups_conferidos')
    try:
        for target in (canonical, runtime):
            save('antes_copia_' + ('canonica' if target == canonical else 'runtime'))
            shutil.copy2(candidate, target)
            if sha(target) != a.sha_candidata:
                raise ValueError('Cópia divergiu')
        save('antes_indice')
        index.write_bytes(new_index)
        validar_cena_json(runtime, DATA / 'catalogos')
        save('promovida_validada_aguarda_remontagem_e_captura')
    except Exception:
        for row in reversed(records):
            target = Path(row['arquivo'])
            if row['existia']:
                shutil.copy2(row['backup'], target)
            elif target.exists():
                if target != runtime or sha(target) != a.sha_candidata:
                    raise RuntimeError('Arquivo novo mudou; rollback preserva para investigar')
                target.unlink()
        save('revertida_apos_falha')
        raise
    print(json.dumps({'checkpoint': str(folder / 'checkpoint.json'), 'estado': journal['estado']}))


if __name__ == '__main__':
    main()
