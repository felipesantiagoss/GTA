"""Contratos físicos de entrega/guardar: arquivos sintéticos, nenhuma chamada ao GTA."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import sys
import unittest

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / 'ferramentas'))
sys.path.insert(0, str(Path(__file__).parent))
import test_poses_json
from validar_cena import ErroCena


class GestosSentadosTests(unittest.TestCase):
    gravar_corpos = test_poses_json.PosesJsonTests.gravar_corpos
    validar = test_poses_json.PosesJsonTests.validar
    recusa = test_poses_json.PosesJsonTests.recusa
    objeto = staticmethod(test_poses_json.PosesJsonTests.objeto)
    pose = staticmethod(test_poses_json.PosesJsonTests.pose)

    def setUp(self):
        test_poses_json.PosesJsonTests.setUp(self)
        corpos_reais = json.loads((RAIZ / 'catalogos/corpos_serie.json').read_text(encoding='utf-8'))['animacoes']
        for codigo in ('entrega_a', 'entrega_b', 'guardar_envelope'):
            self.corpos['animacoes'].append(deepcopy(next(r for r in corpos_reais if r['codigo'] == codigo)))
        self.fonte.write_text('\n'.join(r['dicionario'] + ' ' + r['animacao'] for r in self.corpos['animacoes']) + '\n', encoding='utf-8')
        digest = hashlib.sha256(self.fonte.read_bytes()).hexdigest().upper()
        for i, recurso in enumerate(self.corpos['animacoes'], 1):
            recurso['origem'].update(arquivo=str(self.fonte), linha=i, sha256=digest)
        self.gravar_corpos()
        props_reais = json.loads((RAIZ / 'catalogos/objetos_serie.json').read_text(encoding='utf-8'))['objetos']
        for codigo in ('envelope', 'garrafa_cerveja'):
            self.props['objetos'].append(deepcopy(next(r for r in props_reais if r['codigo'] == codigo)))
        prop_source = self.fontes / 'PropList.txt'
        prop_source.write_text('\n'.join(r['modelo'] for r in self.props['objetos']) + '\n', encoding='utf-8')
        digest = hashlib.sha256(prop_source.read_bytes()).hexdigest().upper()
        for i, recurso in enumerate(self.props['objetos'], 1):
            recurso['origem'].update(arquivo=str(prop_source), linha=i, sha256=digest)
        (self.catalogos / 'objetos_serie.json').write_text(json.dumps(self.props, ensure_ascii=False), encoding='utf-8')
        self.cena['poses'] = [self.pose(recurso='sentada_cadeira_livre_f'),
                              self.pose(personagem='TESTE_HOMEM', objeto='CADEIRA2', recurso='sentado_cadeira_m')]
        self.cena['direcao'] = [self.gesto('TESTE_HOMEM', 'entrega_a', 1, 3),
                               self.gesto('TESTE_MULHER', 'entrega_b', 1, 3),
                               self.gesto('TESTE_MULHER', 'guardar_envelope', 4, 3)]
        self.envelope = dict(id='ENVELOPE', recurso='envelope', marca=dict(direita=0, frente=0, altura=0),
                             attachmentInicial=self.attachment('TESTE_HOMEM'),
                             eventos=[dict(tipo='transferir', inicioSegundos=1, esperaMaximaSegundos=2, **self.attachment('TESTE_MULHER')),
                                      dict(tipo='guardar', inicioSegundos=4, personagem='TESTE_MULHER', recurso='guardar_envelope', esperaMaximaSegundos=2)])
        self.cena['objetos'] = [self.objeto(), dict(self.objeto(), id='CADEIRA2'), self.envelope]

    @staticmethod
    def attachment(ator):
        return dict(personagem=ator, mao='direita', offset=dict(x=0, y=0, z=0), rotacao=dict(x=0, y=0, z=0))

    @staticmethod
    def gesto(ator, recurso, inicio, duracao):
        return dict(tipo='clipe', personagem=ator, recurso=recurso, inicioSegundos=inicio, duracaoSegundos=duracao,
                    sobrePoseSentada=True, corpoInteiro=False, loop=False, manterPoseFinal=False)

    def test_duas_poses_e_transferencia_guardar_na_mesma_narracao(self):
        self.validar()

    def test_protecao_primaria_nao_e_liberada_por_booleano_generico(self):
        original = deepcopy(self.cena)
        for key, value in (('loop', True), ('corpoInteiro', True), ('manterPoseFinal', True),
                           ('grupoPareado', 'par'), ('sobrePoseSentada', 'true')):
            self.cena = deepcopy(original)
            self.cena['direcao'][0][key] = value
            self.recusa('sobrePoseSentada')
        self.cena = deepcopy(original)
        self.cena['direcao'][0].pop('loop')
        self.recusa('sobrePoseSentada')

    def test_pose_incompativel_ou_ausente_e_recusada(self):
        original = deepcopy(self.cena)
        for recurso in ('sentada_cadeira_f', 'sentada_meio_fio_f', 'deitada_cama_f'):
            self.cena = deepcopy(original)
            self.cena['poses'][0].update(recurso=recurso, tipo='deitada' if recurso == 'deitada_cama_f' else 'sentada')
            self.recusa('direção corporal')
        for poses in ([], original['poses'][1:]):
            self.cena = deepcopy(original)
            self.cena['poses'] = deepcopy(poses)
            self.recusa('pose sentada')
        self.cena = deepcopy(original)
        self.cena.pop('poses')
        self.recusa('pose sentada')

    def test_janela_limitada_precisa_dos_dois_gestos_ativos(self):
        original = deepcopy(self.cena)
        for wait in (0, 2.001, True, .0001):
            self.cena = deepcopy(original)
            self.cena['objetos'][-1]['eventos'][0]['esperaMaximaSegundos'] = wait
            self.recusa()
        for index in (0, 1):
            self.cena = deepcopy(original)
            self.cena['direcao'][index]['duracaoSegundos'] = 1.999
            self.recusa('janela inteira')
        self.cena = deepcopy(original)
        self.cena['objetos'][-1]['eventos'][0]['inicioSegundos'] = .999
        self.recusa('janela inteira')

    def test_guardar_exige_envelope_e_dono_real_apos_transferencia(self):
        original = deepcopy(self.cena)
        for changes in ({'personagem': 'TESTE_HOMEM'}, {'recurso': 'entrega_b'}, {'mao': 'direita'},
                        {'esperaMaximaSegundos': 0}):
            self.cena = deepcopy(original)
            self.cena['objetos'][-1]['eventos'][1].update(changes)
            self.recusa()
        self.cena = deepcopy(original)
        self.cena['objetos'][-1]['recurso'] = 'garrafa_cerveja'
        self.recusa('envelope')
        self.cena = deepcopy(original)
        self.cena['objetos'][-1]['eventos'].insert(1, dict(tipo='soltar', inicioSegundos=3.5))
        self.recusa('dono')

    def test_pendente_nao_ultrapassa_proximo_evento_ou_fim_da_cena(self):
        self.envelope['eventos'][1]['inicioSegundos'] = 3
        self.recusa('próxima ação')
        self.envelope['eventos'][1]['inicioSegundos'] = 4
        self.envelope['eventos'].append(dict(tipo='mostrar', inicioSegundos=6))
        self.recusa('próxima ação')

    def test_nomes_livres_nao_trocam_par_canonico_na_fonte(self):
        recurso = next(r for r in self.corpos['animacoes'] if r['codigo'] == 'guardar_envelope')
        recurso.update(dicionario='mp_common', animacao='givetake1_a')
        recurso['origem']['texto'] = 'mp_common givetake1_a'
        self.gravar_corpos()
        self.recusa('SHA|literal|put_away_coke')

    def test_espera_nao_afrouxa_eventos_antigos(self):
        for tipo in ('ocultar', 'mostrar', 'soltar'):
            self.envelope['eventos'] = [dict(tipo=tipo, inicioSegundos=1, esperaMaximaSegundos=1)]
            self.recusa('espera')


if __name__ == '__main__':
    unittest.main()
