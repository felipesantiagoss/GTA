"""Mapa local e independente da série; não grava marcas nem modifica o JPEG."""
from __future__ import annotations

import argparse
import base64
import hashlib
import html
import json
import math
import os
from pathlib import Path
import shutil
import struct
import tempfile
from datetime import datetime, timezone
import uuid

RAIZ = Path(__file__).resolve().parent.parent
ESTADOS = {"Local salvo no jogo": "saved", "Região indicada": "region",
           "Acesso/teste pendente": "pending"}
CORES = {"saved": "#14794a", "region": "#1768c2", "pending": "#b45309"}
NOMES = ("MAPA_LOCACOES_A_CALOTEIRA.html", "MAPA_LOCACOES_A_CALOTEIRA.svg")


def _jpeg_size(jpeg: bytes) -> tuple[int, int]:
    if jpeg[:2] != b"\xff\xd8":
        raise ValueError("A imagem precisa ser um JPEG original válido.")
    pos = 2
    while pos < len(jpeg):
        if jpeg[pos] != 255:
            break
        while pos < len(jpeg) and jpeg[pos] == 255:
            pos += 1
        if pos >= len(jpeg):
            break
        marker = jpeg[pos]
        pos += 1
        if marker in (0xD9, 0xDA):
            break
        if marker == 1 or 0xD0 <= marker <= 0xD8:
            continue
        if pos + 2 > len(jpeg):
            break
        size = struct.unpack_from(">H", jpeg, pos)[0]
        if size < 2 or pos + size > len(jpeg):
            break
        if marker in (0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7,
                      0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF) and size >= 8:
            height, width = struct.unpack_from(">HH", jpeg, pos + 3)
            if width and height:
                return width, height
        pos += size
    raise ValueError("Não foi possível conferir as dimensões do JPEG.")


def validar_mapa(data: dict, codigos: set[str], jpeg: bytes) -> None:
    campos = {"versao", "imagem", "largura", "altura", "locais"}
    metadados = {"titulo", "origem", "precisao", "catalogoMarcas"}
    if not isinstance(data, dict) or not campos <= set(data) or set(data) - campos - metadados or type(data["versao"]) is not int or data["versao"] != 1:
        raise ValueError("O mapa precisa seguir o formato versão 1.")
    if any(not isinstance(data[x], str) or not data[x].strip() for x in metadados & set(data)):
        raise ValueError("Os metadados do mapa precisam ser textos não vazios.")
    if not isinstance(data["imagem"], str) or Path(data["imagem"]).name != data["imagem"] or Path(data["imagem"]).suffix.lower() not in (".jpg", ".jpeg"):
        raise ValueError("A imagem deve ser um nome simples de JPEG no projeto.")
    if any(type(data[x]) is not int or not 1 <= data[x] <= 10000 for x in ("largura", "altura")):
        raise ValueError("Dimensões da imagem inválidas.")
    if _jpeg_size(jpeg) != (data["largura"], data["altura"]):
        raise ValueError("As dimensões declaradas não correspondem ao JPEG.")
    if not isinstance(data["locais"], list) or len(data["locais"]) != 21 or len(codigos) != 21:
        raise ValueError("O mapa e o catálogo precisam conter os 21 locais da série.")
    campos_local = {"numero", "codigo", "nome", "regiao", "x", "y", "episodio1", "marcaReal", "estado", "observacao"}
    numeros, encontrados = set(), set()
    for item in data["locais"]:
        if not isinstance(item, dict) or set(item) != campos_local:
            raise ValueError("Um local tem campos ausentes ou desconhecidos.")
        numero, codigo = item["numero"], item["codigo"]
        if type(numero) is not int or not 1 <= numero <= 21 or numero in numeros:
            raise ValueError("Os números dos locais devem ser únicos, de 1 a 21.")
        if not isinstance(codigo, str) or codigo not in codigos or codigo in encontrados:
            raise ValueError("Código de local ausente do catálogo ou duplicado.")
        numeros.add(numero)
        encontrados.add(codigo)
        for key, limit in (("nome", 100), ("regiao", 100), ("observacao", 1000)):
            if not isinstance(item[key], str) or not item[key].strip() or len(item[key]) > limit:
                raise ValueError("Nome, região ou observação inválidos.")
        if any(type(item[x]) not in (int, float) or not math.isfinite(item[x]) or not 0 <= item[x] <= 1 for x in ("x", "y")):
            raise ValueError("Os pontos no mapa devem ser números finitos entre 0 e 1.")
        if any(type(item[x]) is not bool for x in ("episodio1", "marcaReal")):
            raise ValueError("Episódio 1 e marca real precisam ser valores booleanos.")
        if item["estado"] not in ESTADOS or item["marcaReal"] != (item["estado"] == "Local salvo no jogo"):
            raise ValueError("O estado do local deve distinguir marca salva, região e acesso pendente.")
    if encontrados != codigos or numeros != set(range(1, 22)):
        raise ValueError("O mapa precisa corresponder exatamente aos 21 códigos do catálogo.")


def posicionar(locais: list[dict], largura: int, altura: int) -> list[dict]:
    """Desloca somente os números, preservando a âncora indicada na imagem."""
    scale = min(largura, altura) / 3200
    gap, margin = 150 * scale, 65 * scale
    placed = []
    for item in sorted(locais, key=lambda x: x["numero"]):
        ax, ay = item["x"] * largura, item["y"] * altura
        chosen = None
        for radius in range(110, 3210, 90):
            for step in range(16):
                angle = -math.pi / 2 + step * math.pi / 8
                x = min(largura - margin, max(margin, ax + math.cos(angle) * radius * scale))
                y = min(altura - margin, max(margin, ay + math.sin(angle) * radius * scale))
                if all(math.hypot(x - p["px"], y - p["py"]) >= gap for p in placed):
                    chosen = (x, y)
                    break
            if chosen:
                break
        if chosen is None:
            raise ValueError("Não há espaço para distribuir os números sem sobreposição.")
        placed.append({**item, "px": chosen[0], "py": chosen[1]})
    return placed


def _esc(value: object) -> str:
    return html.escape(str(value), quote=True)


def _mapa(data: dict, jpeg: bytes, interactive: bool) -> str:
    width, height = data["largura"], data["altura"]
    scale = min(width, height) / 3200
    embedded = base64.b64encode(jpeg).decode("ascii")
    parts = [f'<image width="{width}" height="{height}" href="data:image/jpeg;base64,{embedded}"/>']
    for item in posicionar(data["locais"], width, height):
        number, code = item["numero"], _esc(item["codigo"])
        state = ESTADOS[item["estado"]]
        ax, ay, x, y = item["x"] * width, item["y"] * height, item["px"], item["py"]
        attrs = f' tabindex="0" role="button" aria-label="{number}. {_esc(item["nome"])}. {_esc(item["regiao"])}. {_esc(item["estado"])}"' if interactive else ""
        parts.append(f'<g class="pin {state}" data-codigo="{code}" data-numero="{number}"{attrs}>')
        parts.append(f'<title>{number}. {_esc(item["nome"])} · {_esc(item["regiao"])} · {_esc(item["estado"])}</title>')
        parts.append(f'<line x1="{ax:.3f}" y1="{ay:.3f}" x2="{x:.3f}" y2="{y:.3f}" stroke="#fff" stroke-width="{12*scale:.3f}"/>')
        parts.append(f'<line x1="{ax:.3f}" y1="{ay:.3f}" x2="{x:.3f}" y2="{y:.3f}" stroke="#152d42" stroke-width="{5*scale:.3f}"/>')
        parts.append(f'<circle cx="{ax:.3f}" cy="{ay:.3f}" r="{9*scale:.3f}" fill="#fff" stroke="#152d42" stroke-width="{4*scale:.3f}"/>')
        parts.append(f'<circle class="badge" cx="{x:.3f}" cy="{y:.3f}" r="{48*scale:.3f}" fill="{CORES[state]}" stroke="#fff" stroke-width="{8*scale:.3f}"/>')
        parts.append(f'<text x="{x:.3f}" y="{y:.3f}" dy=".35em" text-anchor="middle" fill="#fff" font-size="{64*scale:.3f}" font-weight="700" font-family="Segoe UI,Arial,sans-serif">{number}</text></g>')
    return "\n".join(parts)


def renderizar_svg(data: dict, jpeg: bytes) -> str:
    width, height = data["largura"], data["altura"]
    scale = min(width, height) / 3200
    side = 1400 * scale
    parts = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width+side:g} {height}" role="img" aria-labelledby="titulo descricao">',
             '<title id="titulo">Os 21 locais de A Caloteira do Oeste</title>',
             '<desc id="descricao">Números apontam regiões aproximadas; a legenda distingue locais salvos e recursos pendentes.</desc>',
             '<rect width="100%" height="100%" fill="#f7fafb"/>', _mapa(data, jpeg, False),
             f'<g transform="translate({width+90*scale:g} 0)" font-family="Segoe UI,Arial,sans-serif" fill="#142c3f">',
             f'<text x="0" y="{105*scale:g}" font-size="{34*scale:g}" letter-spacing="3">A CALOTEIRA DO OESTE</text>',
             f'<text x="0" y="{198*scale:g}" font-size="{72*scale:g}" font-weight="700">Os lugares da série</text>',
             f'<text x="0" y="{266*scale:g}" font-size="{36*scale:g}">21 locais · números ligados à região</text>']
    for index, item in enumerate(sorted(data["locais"], key=lambda x: x["numero"])):
        y = (390 + index * 126) * scale
        state = ESTADOS[item["estado"]]
        name = item["nome"]
        if len(name) > 43:
            split = name.rfind(" ", 0, 44)
            split = split if split > 15 else 43
            lines = [name[:split], name[split:].strip()]
        else:
            lines = [name]
        parts.append(f'<g class="legend-row" data-codigo="{_esc(item["codigo"])}" transform="translate(0 {y:g})"><title>{_esc(name)}</title>')
        parts.append(f'<circle cx="{30*scale:g}" cy="{22*scale:g}" r="{31*scale:g}" fill="{CORES[state]}"/><text x="{30*scale:g}" y="{22*scale:g}" dy=".35em" text-anchor="middle" fill="white" font-size="{38*scale:g}" font-weight="700">{item["numero"]}</text>')
        for n, line in enumerate(lines):
            parts.append(f'<text x="{88*scale:g}" y="{(34+n*36)*scale:g}" font-size="{38*scale:g}" font-weight="600">{_esc(line)}</text>')
        parts.append(f'<text x="{88*scale:g}" y="{(80 if len(lines)==1 else 108)*scale:g}" font-size="{27*scale:g}">{_esc(item["regiao"])} · {_esc(item["estado"])}{ " · EP1" if item["episodio1"] else ""}</text></g>')
    parts.extend([f'<text x="0" y="{3070*scale:g}" font-size="{29*scale:g}">Pontos aproximados; não substituem a marca salva no GTA.</text>',
                  f'<text x="0" y="{3120*scale:g}" font-size="{29*scale:g}">Mapa-base: TreeFitty / iGTA · imagem original preservada.</text>', '</g></svg>'])
    return "\n".join(parts)


HTML = r'''<!doctype html>
<html lang="pt-BR"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Os lugares da série — A Caloteira do Oeste</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#e8eff0;color:#142c3f;font-family:Segoe UI,Arial,sans-serif}button{font:inherit;cursor:pointer}button:focus-visible{outline:3px solid #175eb6;outline-offset:3px}header{height:100px;padding:18px 28px;background:#142c3f;color:#fff;display:flex;justify-content:space-between;align-items:center;gap:20px}header small{letter-spacing:2px;color:#a7d5df}h1{font-size:26px;margin:4px 0 0}header p{font-size:14px;color:#d8e8ed}.layout{display:grid;grid-template-columns:minmax(0,1fr) 390px;height:calc(100vh - 100px);min-height:560px}.map-stage{position:relative;overflow:hidden;background:#43b8d2;display:flex;flex-direction:column}.map-stage svg{width:100%;height:100%;display:block;min-height:0;flex:1}.pin{cursor:pointer;outline:none}.pin line,.pin text{pointer-events:none}.pin.selected .badge,.pin:focus .badge{stroke:#ffd767;stroke-width:16}.pin.selected .badge{filter:drop-shadow(0 0 10px #172633)}.tools{position:absolute;left:16px;top:16px;display:flex;gap:6px}.tools button{height:38px;min-width:40px;padding:0 12px;border:1px solid #c4d6de;border-radius:8px;background:#fff;color:#142c3f;font-weight:600;box-shadow:0 2px 8px #1233}.note{position:static;flex:none;margin:0 14px 12px;background:#fffffff0;border-radius:8px;padding:9px 12px;font-size:12px;line-height:1.45;max-width:620px}.sidebar{padding:20px;background:#f7fafb;overflow-y:auto;border-left:1px solid #d4e0e4}.filter{display:flex;gap:8px}.filter button{border:1px solid #c6d6df;border-radius:20px;background:#fff;padding:8px 14px}.filter button[aria-pressed=true]{background:#142c3f;color:white;border-color:#142c3f}.count{font-size:12px;color:#576e7d}.detail{margin:15px 0;padding:16px;border:1px solid #c8dce2;background:#fff;border-radius:12px;min-height:160px}.detail h2{font-size:20px;margin:0 0 7px}.detail p{font-size:13px;line-height:1.5;margin:7px 0}.status{font-size:12px;font-weight:600}.saved{--state:#14794a}.region{--state:#1768c2}.pending{--state:#b45309}.status{color:var(--state)}.local-list{display:grid;gap:7px}.local-item{display:grid;grid-template-columns:32px 1fr;gap:10px;text-align:left;background:white;border:1px solid #d7e2e6;border-radius:9px;padding:11px;color:#142c3f}.local-item[aria-pressed=true]{border-color:#1768c2;box-shadow:inset 3px 0 #1768c2;background:#edf5ff}.number{align-self:start;background:var(--state);color:white;width:30px;height:30px;display:grid;place-items:center;border-radius:50%;font-size:14px;font-weight:700}.local-item strong{font-size:14px;display:block}.local-item small{font-size:12px;display:block;margin-top:4px;color:#5e7180}.key{font-size:11px;color:#677d89;margin-top:15px}.credits{font-size:11px;color:#677d89;line-height:1.5;margin-top:20px}@media(max-width:900px){header{height:auto;min-height:100px;padding:16px;align-items:flex-start}header p{display:none}.layout{grid-template-columns:1fr;height:auto}.map-stage{height:70vh;min-height:460px}.sidebar{border-left:0;border-top:1px solid #d4e0e4}.local-list{grid-template-columns:repeat(auto-fit,minmax(260px,1fr))}}@media(prefers-reduced-motion:reduce){*{scroll-behavior:auto}}
</style>
<header><div><small>A CALOTEIRA DO OESTE</small><h1>Os lugares da série</h1></div><p>Selecione um número para ver o local.<br>Os pontos indicam regiões aproximadas.</p></header>
<main class="layout"><section class="map-stage" aria-label="Mapa dos locais"><svg id="mapa-svg" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 __WIDTH__ __HEIGHT__" aria-label="Mapa com os 21 locais">__MAPA__</svg><div class="tools"><button id="zoom-in" aria-label="Ampliar mapa">+</button><button id="zoom-out" aria-label="Reduzir mapa">−</button><button id="reset">Ver mapa inteiro</button></div><div class="note">As linhas ligam cada número à sua região. Os pontos são aproximados; a marca salva no GTA é o ponto de filmagem.</div></section><aside class="sidebar" aria-label="Legenda dos 21 locais"><div class="filter" role="group" aria-label="Filtrar locais"><button id="todos" aria-pressed="true">Todos</button><button id="ep1" aria-pressed="false">Episódio 1</button></div><p class="count" id="count">21 locais</p><section class="detail" id="detail" aria-live="polite"><h2 id="detail-name"></h2><p id="detail-region"></p><p class="status" id="detail-state"></p><p id="detail-note"></p></section><nav class="local-list" aria-label="Escolher local">__LISTA__</nav><p class="key">No mapa, Tab escolhe um número e Enter abre o local.</p><p class="credits">Mapa-base: TreeFitty / iGTA.<br>A imagem original foi incorporada sem alterações.</p></aside></main>
<script>
const locais=__DADOS__, largura=__WIDTH__, altura=__HEIGHT__;
const mapa=document.getElementById('mapa-svg');let selected=null,filter=false,zoom=1,cx=largura/2,cy=altura/2;
function view(){const w=largura/zoom,h=altura/zoom;cx=Math.max(w/2,Math.min(largura-w/2,cx));cy=Math.max(h/2,Math.min(altura-h/2,cy));mapa.setAttribute('viewBox',`${cx-w/2} ${cy-h/2} ${w} ${h}`)}
function select(code,focus=true){const local=locais.find(x=>x.codigo===code);if(!local)return;selected=code;document.querySelectorAll('.pin,.local-item').forEach(el=>{const yes=el.dataset.codigo===code;el.classList.toggle('selected',yes);if(el.classList.contains('local-item'))el.setAttribute('aria-pressed',String(yes))});document.getElementById('detail-name').textContent=`${local.numero}. ${local.nome}`;document.getElementById('detail-region').textContent=local.regiao+(local.episodio1?' · Episódio 1':'');const status=document.getElementById('detail-state');status.textContent=local.estado;status.className='status '+(local.marcaReal?'saved':local.estado==='Acesso/teste pendente'?'pending':'region');document.getElementById('detail-note').textContent=local.observacao;if(focus){zoom=Math.max(zoom,2);cx=local.x*largura;cy=local.y*altura;view()}}
function filterMap(ep1){filter=ep1;document.getElementById('todos').setAttribute('aria-pressed',String(!ep1));document.getElementById('ep1').setAttribute('aria-pressed',String(ep1));let count=0;locais.forEach(local=>{const visible=!ep1||local.episodio1;if(visible)count++;document.querySelectorAll(`[data-codigo="${local.codigo}"]`).forEach(el=>{el.style.display=visible?'':'none';if(el.classList.contains('pin')){el.setAttribute('tabindex',visible?'0':'-1');el.setAttribute('aria-hidden',String(!visible))}})});document.getElementById('count').textContent=count+' locais'+(ep1?' no Episódio 1':'');if(ep1&&!locais.find(x=>x.codigo===selected)?.episodio1){const next=locais.find(x=>x.episodio1);if(next)select(next.codigo,false)}}
document.querySelectorAll('.pin,.local-item').forEach(el=>{el.addEventListener('click',()=>select(el.dataset.codigo));if(el.classList.contains('pin'))el.addEventListener('keydown',event=>{if(event.key==='Enter'||event.key===' '){event.preventDefault();select(el.dataset.codigo)}})});
document.getElementById('todos').addEventListener('click',()=>filterMap(false));document.getElementById('ep1').addEventListener('click',()=>filterMap(true));document.getElementById('zoom-in').addEventListener('click',()=>{zoom=Math.min(5,zoom*1.35);view()});document.getElementById('zoom-out').addEventListener('click',()=>{zoom=Math.max(1,zoom/1.35);view()});document.getElementById('reset').addEventListener('click',()=>{zoom=1;cx=largura/2;cy=altura/2;view()});select(locais.find(x=>x.codigo==='ROCA_GRAPESEED')?.codigo||locais[0].codigo,false);
</script></html>'''


def renderizar_html(data: dict, jpeg: bytes) -> str:
    items = sorted(data["locais"], key=lambda x: x["numero"])
    rows = []
    for item in items:
        state = ESTADOS[item["estado"]]
        rows.append(f'<button class="local-item {state}" data-codigo="{_esc(item["codigo"])}" aria-pressed="false"><span class="number">{item["numero"]}</span><span><strong>{_esc(item["nome"])}</strong><small>{_esc(item["regiao"])}</small><small>{_esc(item["estado"])}</small></span></button>')
    encoded = json.dumps(items, ensure_ascii=False, allow_nan=False).replace("<", "\\u003c").replace("&", "\\u0026")
    replacements = {"__WIDTH__": str(data["largura"]), "__HEIGHT__": str(data["altura"]),
                    "__MAPA__": _mapa(data, jpeg, True), "__LISTA__": "\n".join(rows), "__DADOS__": encoded}
    result = HTML
    for key, value in replacements.items():
        result = result.replace(key, value)
    return result


def publicar(saida: Path, html: str, svg: str) -> dict[str, str]:
    saida = Path(saida)
    if saida.is_symlink():
        raise ValueError("A pasta de saída não pode ser um vínculo.")
    saida.mkdir(parents=True, exist_ok=True)
    previous, staged, written = {}, {}, []
    contents = dict(zip(NOMES, (html.encode("utf-8"), svg.encode("utf-8"))))
    for name in NOMES:
        path = saida / name
        if path.is_symlink() or (path.exists() and (not path.is_file() or path.stat().st_nlink != 1)):
            raise ValueError("Uma saída existente não é um arquivo comum independente.")
        previous[name] = path.read_bytes() if path.exists() else None
    backup = None
    if any(value is not None for value in previous.values()):
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-" + uuid.uuid4().hex[:8]
        backup = saida / "backups" / "mapa-locacoes" / stamp
        backup.mkdir(parents=True, exist_ok=False)
        for name, old in previous.items():
            if old is not None:
                shutil.copy2(saida / name, backup / name)
                if (backup / name).read_bytes() != old:
                    raise ValueError("O backup do mapa não corresponde ao arquivo anterior.")
    try:
        for name, content in contents.items():
            with tempfile.NamedTemporaryFile(dir=saida, prefix="." + name + ".", suffix=".tmp", delete=False) as handle:
                staged[name] = Path(handle.name)
                handle.write(content)
                handle.flush()
                os.fsync(handle.fileno())
        for name, temp in staged.items():
            path = saida / name
            if path.is_symlink() or (path.read_bytes() if path.exists() else None) != previous[name]:
                raise ValueError("O mapa existente mudou durante a geração; edição preservada.")
            os.replace(temp, path)
            written.append(name)
            if path.read_bytes() != contents[name]:
                raise ValueError("Falha ao conferir o mapa gerado.")
    except BaseException:
        for name in reversed(written):
            path = saida / name
            if path.exists() and not path.is_symlink() and path.read_bytes() == contents[name]:
                if previous[name] is None:
                    path.unlink()
                else:
                    restore = saida / ("." + name + "." + uuid.uuid4().hex + ".restore")
                    restore.write_bytes(previous[name])
                    os.replace(restore, path)
        raise
    finally:
        for temp in staged.values():
            if temp.exists():
                temp.unlink()
    return {"html": str((saida / NOMES[0]).resolve()), "svg": str((saida / NOMES[1]).resolve()),
            "backup": str(backup.resolve()) if backup else ""}


def _pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("JSON com chave duplicada: " + key)
        result[key] = value
    return result


def _json(path: Path) -> dict:
    def invalid(value):
        raise ValueError("JSON com número inválido: " + value)
    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=_pairs, parse_constant=invalid)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Gerar mapa HTML/SVG dos 21 locais, preservando o JPEG original.")
    parser.add_argument("--catalogo", type=Path, default=RAIZ / "catalogos" / "mapa_locacoes.json")
    parser.add_argument("--locais", type=Path, default=RAIZ / "catalogos" / "locais.json")
    parser.add_argument("--saida", type=Path, default=RAIZ)
    args = parser.parse_args(argv)
    try:
        if not args.saida.resolve().is_relative_to(RAIZ):
            raise ValueError("A saída do mapa deve ficar no workspace do projeto.")
        data, catalog = _json(args.catalogo), _json(args.locais)
        image_name = data.get("imagem") if isinstance(data, dict) else None
        if not isinstance(image_name, str) or Path(image_name).name != image_name:
            raise ValueError("A imagem precisa ser um nome simples no projeto.")
        image = RAIZ / image_name
        jpeg = image.read_bytes()
        original_hash = hashlib.sha256(jpeg).hexdigest()
        codes = {x["codigo"] for x in catalog["locais"] if x["codigo"] != "TESTE_AMBIENTE"}
        validar_mapa(data, codes, jpeg)
        html, svg = renderizar_html(data, jpeg), renderizar_svg(data, jpeg)
        if hashlib.sha256(image.read_bytes()).hexdigest() != original_hash:
            raise ValueError("O JPEG mudou durante a geração; saídas não publicadas.")
        result = publicar(args.saida, html, svg)
        print(json.dumps({"status": "gerado", "locais": 21, **result, "imagemPreservada": True}, ensure_ascii=False))
        return 0
    except (OSError, ValueError, KeyError, TypeError) as error:
        parser.exit(2, "Mapa não gerado: " + str(error) + "\n")


if __name__ == "__main__":
    raise SystemExit(main())
