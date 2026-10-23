"""Detectar a fronteira da contagem com template BMP real, somente em CPU.

Grava apenas ultimo numero1, primeiro quadro sem o template e meio/fim.
Ausencia do template NAO aprova limpeza nem conteudo: conferir os quatro BMPs
e usar fechar do calibrador existente com as flags explicitas do operador.
"""
from __future__ import annotations

import argparse
import copy
import ctypes as c
from ctypes import wintypes as w
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import struct
import sys
import time
import uuid
import xml.etree.ElementTree as ET

import calibrar_tomada_limpa as base

e, m = base.e, base.m
TEMPLATE_RESULTADO = e.RAIZ / 'backups/montagem-video-episodios/20261006T030345683167Z-calibracao-cpu-janela-7652fe1a/resultado-quadros.json'
TEMPLATE_PTS = 61999666


def rgb(pixels: bytes, width: int, height: int, stride: int, x: int, y: int):
    row = y if stride > 0 else height - 1 - y
    start = row * abs(stride) + x * 4
    return pixels[start:start + 3]


def branco(value, threshold=225):
    return min(value) >= threshold and max(value) - min(value) <= 35


def ler_template(result_path: Path, pts: int) -> dict:
    result_ref = base.referencia(result_path)
    result = e.ler_json(base.ler_referencia(result_ref))
    if result.get('metodo') != base.METODO or result.get('gpuSolicitada') is not False:
        raise ValueError('Template exige resultado SourceReader software real.')
    matches = [x for x in result['quadros'] if x.get('ptsReal100ns') == pts]
    if len(matches) != 1:
        raise ValueError('PTS exato do template precisa existir uma vez na prova real.')
    frame = matches[0]; bmp = base.ler_referencia(frame).read_bytes()
    start = struct.unpack_from('<I', bmp, 10)[0]
    width, signed_height = struct.unpack_from('<ii', bmp, 18)
    if bmp[:2] != b'BM' or struct.unpack_from('<HHI', bmp, 26) != (1, 32, 0):
        raise ValueError('Template aceita somente BMP RGB32 original.')
    height = abs(signed_height); stride = width * 4 * (1 if signed_height < 0 else -1)
    if not 0 < width <= 8192 or not 0 < height <= 8192 or len(bmp) < start + width * height * 4:
        raise ValueError('BMP template incompleto.')
    pixels = bmp[start:]
    # Posicao do numero no ScaledDraw atual: centro horizontal e Y virtual300/720.
    left, top = width // 2 - width // 32, height * 300 // 720
    right, bottom = width // 2 + width // 32, min(height, top + height // 10)
    candidates = {(x, y) for y in range(top, bottom) for x in range(left, right)
                  if branco(rgb(pixels, width, height, stride, x, y))}
    components = []
    while candidates:
        seed = candidates.pop(); component = {seed}; stack = [seed]
        while stack:
            x, y = stack.pop()
            for neighbor in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
                if neighbor in candidates:
                    candidates.remove(neighbor); component.add(neighbor); stack.append(neighbor)
        xs, ys = zip(*component)
        if (len(component) >= 64 and max(ys)-min(ys) >= height // 54
                and max(xs)-min(xs) < width // 24
                and abs((max(xs)+min(xs))/2-width/2) < width/64):
            components.append(component)
    if not components:
        raise ValueError('Glyph branco central do numero1 nao foi localizado no template real.')
    foreground = max(components, key=len)
    outline = set()
    for x, y in foreground:
        for dx in range(-3, 4):
            for dy in range(-3, 4):
                nx, ny = x + dx, y + dy
                if 0 <= nx < width and 0 <= ny < height and max(rgb(pixels,width,height,stride,nx,ny)) <= 75:
                    outline.add((nx, ny))
    if len(outline) < 20:
        raise ValueError('Template real nao tem contorno preto suficiente para rejeitar fundo branco.')
    return {'largura':width,'altura':height,'branco':sorted(foreground),'preto':sorted(outline),
            'resultadoOriginal':result_ref,'quadroOriginal':copy.deepcopy(frame),
            'maskSha256':hashlib.sha256(json.dumps([sorted(foreground),sorted(outline)]).encode()).hexdigest().upper()}


def corresponde(template: dict, frame: tuple) -> tuple[bool, float, float]:
    pts, duration, width, height, stride, pixels = frame
    if (width, height) != (template['largura'], template['altura']):
        raise ValueError('Resolucao difere do template real; nao escalar glyph nem inventar correspondencia.')
    white = sum(branco(rgb(pixels,width,height,stride,x,y),210) for x,y in template['branco']) / len(template['branco'])
    dark = sum(max(rgb(pixels,width,height,stride,x,y)) <= 90 for x,y in template['preto']) / len(template['preto'])
    return white >= .90 and dark >= .65, white, dark


def decodificar_janela(video: Path, first_ms: int, last_ms: int):
    """Mesmo ABI/GUID/configuracao software do extrator PTS existente; sem BMP intermediario."""
    class GUID(c.Structure):
        _fields_=[('a',c.c_uint32),('b',c.c_uint16),('d',c.c_uint16),('e',c.c_ubyte*8)]
    class PVValue(c.Union):
        _fields_=[('value',c.c_int64),('padding',c.c_byte*16)]
    class PROPVARIANT(c.Structure):
        _fields_=[('vt',c.c_uint16),('r1',c.c_uint16),('r2',c.c_uint16),('r3',c.c_uint16),('data',PVValue)]
    def guid(value):return GUID.from_buffer_copy(uuid.UUID(value).bytes_le)
    def check(hr, name):
        if hr < 0:raise OSError(f'{name}: HRESULT0x{hr & 0xffffffff:08X}')
    def method(pointer,index,argtypes,*args,result=c.c_long):
        table=c.cast(pointer,c.POINTER(c.POINTER(c.c_void_p))).contents
        return c.WINFUNCTYPE(result,c.c_void_p,*argtypes)(table[index])(pointer,*args)
    def release(pointer):
        if pointer:method(pointer,2,[],result=c.c_ulong)
    null=guid('00000000-0000-0000-0000-000000000000')
    processing=guid('fb394f3d-ccf1-42ee-bbb3-f9b845d5681d')
    major=guid('48eba18e-f8c9-4687-bf11-0a74c9f96a8f')
    subtype=guid('f7e34c9a-42e8-4714-b74b-cb29d72c35e5')
    video_type=guid('73646976-0000-0010-8000-00aa00389b71')
    rgb32=guid('00000016-0000-0010-8000-00aa00389b71')
    frame_size=guid('1652c33d-d6b2-4012-b834-72030849a37d')
    stride_key=guid('644b4e48-1e02-4516-b0eb-c01ca9d49ac6')
    first_video=0xfffffffc
    mf=c.WinDLL('mfplat.dll'); reader_dll=c.WinDLL('mfreadwrite.dll'); ole=c.OleDLL('ole32.dll')
    ole.CoInitializeEx.argtypes=[c.c_void_p,w.DWORD];ole.CoInitializeEx.restype=c.c_long
    mf.MFStartup.argtypes=[w.ULONG,w.DWORD];mf.MFStartup.restype=c.c_long
    mf.MFShutdown.argtypes=[];mf.MFShutdown.restype=c.c_long
    mf.MFCreateAttributes.argtypes=[c.POINTER(c.c_void_p),w.UINT];mf.MFCreateAttributes.restype=c.c_long
    mf.MFCreateMediaType.argtypes=[c.POINTER(c.c_void_p)];mf.MFCreateMediaType.restype=c.c_long
    reader_dll.MFCreateSourceReaderFromURL.argtypes=[w.LPCWSTR,c.c_void_p,c.POINTER(c.c_void_p)]
    reader_dll.MFCreateSourceReaderFromURL.restype=c.c_long
    check(ole.CoInitializeEx(None,0),'CoInitializeEx');started=False
    attrs,reader,media,current=[c.c_void_p() for _ in range(4)]
    try:
        check(mf.MFStartup(0x00020070,0),'MFStartup');started=True
        check(mf.MFCreateAttributes(c.byref(attrs),1),'MFCreateAttributes')
        check(method(attrs,21,[c.POINTER(GUID),w.UINT],c.byref(processing),1),'SoftwareVideoProcessing')
        check(reader_dll.MFCreateSourceReaderFromURL(str(video),attrs,c.byref(reader)),'Abrir video')
        check(method(reader,4,[w.DWORD,w.BOOL],0xfffffffe,False),'Desselecionar trilhas')
        check(method(reader,4,[w.DWORD,w.BOOL],first_video,True),'Selecionar video')
        check(mf.MFCreateMediaType(c.byref(media)),'MFCreateMediaType')
        for key,value in ((major,video_type),(subtype,rgb32)):
            check(method(media,24,[c.POINTER(GUID),c.POINTER(GUID)],c.byref(key),c.byref(value)),'SetGUID')
        check(method(reader,7,[w.DWORD,c.c_void_p,c.c_void_p],first_video,None,media),'Selecionar RGB32')
        check(method(reader,6,[w.DWORD,c.POINTER(c.c_void_p)],first_video,c.byref(current)),'Tipo decodificado')
        dimensions,stride_raw=c.c_uint64(),w.UINT()
        check(method(current,8,[c.POINTER(GUID),c.POINTER(c.c_uint64)],c.byref(frame_size),c.byref(dimensions)),'Tamanho RGB32')
        check(method(current,7,[c.POINTER(GUID),c.POINTER(w.UINT)],c.byref(stride_key),c.byref(stride_raw)),'Stride RGB32')
        width,height=dimensions.value>>32,dimensions.value&0xffffffff;stride=c.c_int32(stride_raw.value).value
        if not 0<width<=8192 or not 0<height<=8192 or abs(stride)!=width*4:raise ValueError('Tamanho/stride RGB32 nao suportado.')
        position=PROPVARIANT();position.vt=20;position.data.value=first_ms*10000
        check(method(reader,8,[c.POINTER(GUID),c.POINTER(PROPVARIANT)],c.byref(null),c.byref(position)),'Seek inicial')
        previous=None;deadline=time.monotonic()+45
        while True:
            if time.monotonic()>deadline:raise TimeoutError('Leitura CPU limitada a45s.')
            stream,flags,timestamp,sample=w.DWORD(),w.DWORD(),c.c_int64(),c.c_void_p()
            check(method(reader,9,[w.DWORD,w.DWORD,c.POINTER(w.DWORD),c.POINTER(w.DWORD),c.POINTER(c.c_int64),c.POINTER(c.c_void_p)],
                         first_video,0,c.byref(stream),c.byref(flags),c.byref(timestamp),c.byref(sample)),'ReadSample')
            buffer=c.c_void_p(); frame=None
            try:
                if flags.value&2:return
                if not sample:continue
                pts,duration=c.c_int64(),c.c_int64()
                check(method(sample,35,[c.POINTER(c.c_int64)],c.byref(pts)),'PTS real')
                check(method(sample,37,[c.POINTER(c.c_int64)],c.byref(duration)),'Duracao real')
                if previous is not None and pts.value<=previous:raise ValueError('PTS real precisa crescer estritamente.')
                previous=pts.value
                if duration.value<=0:raise ValueError('Duracao real de amostra invalida.')
                if pts.value<first_ms*10000:continue
                if pts.value>last_ms*10000:return
                check(method(sample,41,[c.POINTER(c.c_void_p)],c.byref(buffer)),'Buffer RGB32')
                address,maximum,length=c.c_void_p(),w.DWORD(),w.DWORD()
                check(method(buffer,3,[c.POINTER(c.c_void_p),c.POINTER(w.DWORD),c.POINTER(w.DWORD)],c.byref(address),c.byref(maximum),c.byref(length)),'Lock')
                try:
                    needed=abs(stride)*height
                    if length.value<needed:raise ValueError('Amostra RGB32 incompleta.')
                    frame=(pts.value,duration.value,width,height,stride,c.string_at(address,needed))
                finally:check(method(buffer,4,[]),'Unlock')
            finally:
                release(buffer);release(sample)
            if frame is not None:yield frame
    finally:
        for pointer in (current,media,reader,attrs):release(pointer)
        if started:mf.MFShutdown()
        ole.CoUninitialize()


def salvar_quadro(frame: tuple, name: str, folder: Path) -> dict:
    pts,duration,width,height,stride,pixels=frame
    bmp=struct.pack('<2sIHHI',b'BM',54+len(pixels),0,0,54)
    bmp+=struct.pack('<IiiHHIIiiII',40,width,-height if stride>0 else height,1,32,0,len(pixels),0,0,0,0)+pixels
    path=folder/(name+f'_pts{pts}.bmp');e.escrever(path,bmp)
    return {'tempoSolicitadoMs':pts//10000,'ptsReal100ns':pts,'ptsRealMs':pts/10000,
            'duracao100ns':duration,**base.referencia(path)}


def localizar(video: Path, template: dict, first_ms: int, last_ms: int) -> tuple:
    if not 0<=first_ms<last_ms or not 2000<=last_ms-first_ms<=3000:
        raise ValueError('Janela CPU exige2–3s positivos, sem expandir automaticamente.')
    iterator=decodificar_janela(video,first_ms,last_ms)
    last,candidate,observed,absent=None,None,0,0
    timeline,scores=[],[]
    try:
        for frame in iterator:
            match,white,dark=corresponde(template,frame)
            timeline.append({'pts100ns':frame[0],'duracao100ns':frame[1]})
            scores.append({'pts100ns':frame[0],'template1':match,'branco':round(white,5),'contornoPreto':round(dark,5)})
            if match:
                last=frame;candidate=None;observed+=1;absent=0
            elif last is not None and observed>=2:
                if candidate is None:
                    if last[0]+last[1]!=frame[0]:raise ValueError('Fronteira nao adjacente nos PTS reais.')
                    candidate=frame
                absent+=1
                if absent>=3:return last,candidate,timeline,scores
    finally:iterator.close()
    raise ValueError('Numero1 real seguido de desaparecimento persistente nao encontrado; revise janela/template manualmente.')


def estado_sem_quadros(dados: Path, code: str, session: str, source: Path, folder: Path) -> dict:
    take=m.tomada_real(dados,code,session)
    original=base.referencia(source);source=base.ler_referencia(original)
    record=ET.parse(base.ler_referencia(take['proveniencia']['sessaoXml'])).getroot()
    armed=datetime.fromisoformat(record.attrib['armadaUtc'].replace('Z','+00:00'))
    mtime=datetime.fromtimestamp(source.stat().st_mtime,timezone.utc)
    if mtime>armed:raise ValueError('Fonte posterior a captura; forneca snapshot historico real.')
    copied=folder/(code+'.source-snapshot.json');e.escrever(copied,source.read_bytes());snapshot=base.referencia(copied)
    if e.hash_arquivo(source)!=original['sha256'] or m.duracao_snapshot(snapshot,code)!=take['duracaoAcaoMs']:
        raise ValueError('Fonte mudou ou duracao diverge da tomada real.')
    for name in ('sessao.xml','resultado.xml'):
        e.escrever(folder/name,(dados/'logs/capturas'/session/name).read_bytes())
    outcome=ET.parse(folder/'resultado.xml').getroot()
    manifest=e.exigir_sem_vinculos(Path(outcome.attrib['manifesto']))
    e.escrever(folder/'manifesto-organizador-original.json',manifest.read_bytes())
    return {'versao':1,'tipo':'calibracao_tomada_limpa_cpu','id':code,'tomada':take,'fonteJsonSnapshot':snapshot,
            'provenienciaFonte':{'fonteFornecida':original,'snapshotCopiadoUtc':datetime.now(timezone.utc).isoformat(),
                'fonteMtimeUtc':mtime.isoformat(),'mtimeAnteriorArmada':True,'hashDuranteCapturaRegistrado':False,
                'limite':'Snapshot copiado apos tomada; mtime anterior corrobora, nao prova hash durante captura.',
                'contagemUtcRegistrada':record.get('contagemUtc'),'contagemDuracaoMs':3000},
            'duracaoAcaoMs':take['duracaoAcaoMs'],'resultadosQuadros':[],
            'medidoNaImagem':False,'aprovacaoVisual':False,'fonteSemTextoConferidaNaImagem':False,
            'renderExecutado':False,'gpuSolicitada':False}


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('acao',choices=('detectar-contagem',))
    parser.add_argument('--calibracao',type=Path)
    parser.add_argument('--cena');parser.add_argument('--sessao');parser.add_argument('--fonte',type=Path)
    parser.add_argument('--template-resultado',type=Path,default=TEMPLATE_RESULTADO)
    parser.add_argument('--template-pts-100ns',type=int,default=TEMPLATE_PTS)
    parser.add_argument('--de-ms',type=int,default=5000);parser.add_argument('--ate-ms',type=int,default=8000)
    args=parser.parse_args(argv);folder=m.novo_trabalho('detectar-contagem-cpu')
    try:
        dados=e.ler_pasta_dados(e.RAIZ,exigir_perfil=True)
        if args.calibracao:
            if args.cena or args.sessao or args.fonte:raise ValueError('Use calibracao existente OU cena/sessao/fonte reais.')
            state=copy.deepcopy(base.conferir_calibracao(args.calibracao,dados))
        else:
            if not args.cena or not args.sessao or not args.fonte:raise ValueError('Informe cena/sessao/fonte reais.')
            state=estado_sem_quadros(dados,args.cena,args.sessao,args.fonte,folder)
        video=e.exigir_sem_vinculos(Path(state['tomada']['video']));metadata=e.metadados_mp4(video)
        if metadata['sha256']!=state['tomada']['sha256'] or args.ate_ms>=metadata['duracaoMs']:
            raise ValueError('Video/hashes/janela divergentes da tomada real.')
        template=ler_template(args.template_resultado,args.template_pts_100ns)
        began=time.monotonic()
        last,first,timeline,scores=localizar(video,template,args.de_ms,args.ate_ms)
        offset=(first[0]+9999)//10000
        ends=[offset+state['duracaoAcaoMs']//2,offset+state['duracaoAcaoMs']-200]
        if max(ends)>=metadata['duracaoMs']:raise ValueError('Janela completa de acao nao cabe no bruto real.')
        boundary=[salvar_quadro(last,'ultimo-count',folder),salvar_quadro(first,'primeiro-sem-template',folder)]
        end_result=e.extrair_quadros_pts(video,ends,folder)
        samples={x['pts100ns']:x for x in timeline}
        for item in end_result['amostrasDecodificadas']:
            if item['pts100ns'] in samples and samples[item['pts100ns']]!=item:raise ValueError('Amostras reais divergentes.')
            samples[item['pts100ns']]=item
        result={'original':metadata,'metodo':base.METODO,'gpuSolicitada':False,
                'gettersFormato':{'largura':first[2],'altura':first[3],'stride':first[4]},
                'quadros':boundary+end_result['quadros'],'amostrasDecodificadas':[samples[x] for x in sorted(samples)],
                'seeksSolicitadosMs':[args.de_ms]+end_result['seeksSolicitadosMs'],
                'selecao':'fronteira em leitura sequencial; meio/fim primeiro PTS>=pedido',
                'template':{k:v for k,v in template.items() if k not in ('branco','preto')},
                'pixelsBrancosTemplate':len(template['branco']),'pixelsPretosTemplate':len(template['preto']),
                'scoresJanela':scores,'contato':base.gerar_contato(boundary+end_result['quadros'],folder)}
        if e.hash_arquivo(video)!=metadata['sha256']:raise ValueError('Original mudou durante leitura.')
        e.salvar_json(folder/'resultado-quadros.json',result)
        state['resultadosQuadros'].append(base.referencia(folder/'resultado-quadros.json'))
        state.update(medidoNaImagem=False,aprovacaoVisual=False,fonteSemTextoConferidaNaImagem=False)
        state['deteccaoContagem']={'ultimoQuadroContagemPts100ns':last[0],
            'primeiroQuadroSemTemplatePts100ns':first[0],'primeiroQuadroLimpoCandidatoPts100ns':first[0],
            'offsetCandidatoMs':offset,'adjacenciaReal100ns':last[0]+last[1]==first[0],
            'confirmacaoSemTemplate':3,'conferenciaVisualPendente':True,'tempoCPUDecodificacaoSegundos':round(time.monotonic()-began,3),
            'limite':'Ausencia do glyph1 nao aprova conteudo, HUD, texto ou enquadramento. Conferir manualmente e fechar com flags explicitas.'}
        e.salvar_json(folder/'resultado.json',state)
        print(json.dumps({'calibracao':str(folder/'resultado.json'),'quadros':str(folder/'resultado-quadros.json'),
                          'contato':str(folder/'contato-pts.bmp'),**state['deteccaoContagem']},ensure_ascii=True),flush=True)
        return 0
    except Exception as error:
        e.salvar_json(folder/'erro.json',{'tipo':type(error).__name__,'erro':str(error),'gpuSolicitada':False,'renderExecutado':False})
        print('Deteccao interrompida: '+str(error),file=sys.stderr,flush=True);return 1


if __name__=='__main__':raise SystemExit(main())
