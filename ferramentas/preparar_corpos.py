"""Reduz pesquisa verificada a referências de clipes; nunca aprova o visual."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]


def preparar():
    research = ROOT / 'catalogos/acoes_serie_candidatas.json'
    data = json.loads(research.read_text(encoding='utf-8'))
    sources = {}
    resources = []
    for item in data['animacoes']:
        origin = item['origem']
        path = Path(origin['arquivo'])
        if path not in sources:
            raw = path.read_bytes()
            digest = hashlib.sha256(raw).hexdigest().upper()
            sources[path] = digest, raw.decode('utf-8-sig').splitlines()
        digest, lines = sources[path]
        if digest != origin['sha256'] or lines[origin['linha'] - 1].strip() != origin['texto']:
            raise ValueError('Fonte alterada: ' + str(path))
        expected = item['dicionario'] + ' ' + item['animacao']
        if origin['texto'] != expected:
            raise ValueError('Par de animação não corresponde à fonte: ' + item['codigo'])
        resources.append({'codigo': item['codigo'],
                          'tipo': 'clipset' if item['tipo'] == 'clipset_candidato' else 'clipe',
                          'dicionario': item['dicionario'], 'animacao': item['animacao'],
                          'origem': {'arquivo': str(path), 'linha': origin['linha'],
                                     'sha256': digest, 'texto': origin['texto']},
                          'restricoes': item['restricoes']})
    result = {'versao': 1, 'estado': 'não testado', 'animacoes': resources}
    return (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode('utf-8')


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--conferir', action='store_true')
    args = parser.parse_args(argv)
    try:
        raw = preparar()
        path = ROOT / 'catalogos/corpos_serie.json'
        if len(raw.decode('utf-8')) > 100000:
            raise ValueError('Catálogo excede limite do leitor C#.')
        if args.conferir:
            if not path.is_file() or path.read_bytes() != raw:
                raise ValueError('Catálogo derivado ausente/alterado.')
        elif not path.exists() or path.read_bytes() != raw:
            if path.exists():
                directory = ROOT / 'backups/catalogos' / datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ-corpos')
                directory.mkdir(parents=True)
                shutil.copy2(path, directory / path.name)
                if (directory / path.name).read_bytes() != path.read_bytes():
                    raise ValueError('Backup do catálogo não confere.')
            path.write_bytes(raw)
        print('46 referências conferidas nas fontes; duração/carregamento e atuação visual dependem do GTA.')
        return 0
    except (OSError, ValueError, KeyError, IndexError) as exc:
        print('Corpos: ' + str(exc), file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
