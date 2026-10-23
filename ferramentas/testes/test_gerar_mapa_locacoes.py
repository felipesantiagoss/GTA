"""Contrato do mapa, sem publicar no workspace nem tocar nas marcas do GTA.

A imagem sintética contém somente SOI/SOF/EOI: testa a leitura de dimensões,
não a decodificação de um JPEG. Toda escrita fica em TemporaryDirectory.
"""
from base64 import b64decode
from copy import deepcopy
from hashlib import sha256
from html.parser import HTMLParser
from itertools import combinations
import math
from pathlib import Path
import re
import struct
import sys
import tempfile
import unittest
from unittest import mock
import xml.etree.ElementTree as ET

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ferramentas"))
import gerar_mapa_locacoes as gerador
from gerar_mapa_locacoes import (
    posicionar, publicar, renderizar_html, renderizar_svg, validar_mapa,
)

HTML_NOME = "MAPA_LOCACOES_A_CALOTEIRA.html"
SVG_NOME = "MAPA_LOCACOES_A_CALOTEIRA.svg"
ESTADOS = ("Local salvo no jogo", "Região indicada", "Acesso/teste pendente")


def jpeg_sof(largura=3200, altura=3200):
    corpo = struct.pack(">BHHB", 8, altura, largura, 3)
    corpo += b"\x01\x11\x00\x02\x11\x01\x03\x11\x01"
    return b"\xff\xd8\xff\xc0" + struct.pack(">H", len(corpo) + 2) + corpo + b"\xff\xd9"


def mapa_sintetico():
    locais = []
    for numero in range(1, 22):
        estado = ESTADOS[(numero - 1) % len(ESTADOS)]
        locais.append({
            "numero": numero,
            "codigo": "LOCAL_%02d" % numero,
            "nome": "Locação número %02d · ação" % numero,
            "regiao": "Região de teste",
            "x": 0.12 + ((numero - 1) % 5) * 0.17,
            "y": 0.12 + ((numero - 1) // 5) * 0.17,
            "episodio1": numero <= 8,
            "marcaReal": estado == ESTADOS[0],
            "estado": estado,
            "observacao": "Referência da região; sem coordenadas de teleporte.",
        })
    return {"versao": 1, "imagem": "MAPA_COMPLETO_GTA_V.jpg",
            "largura": 3200, "altura": 3200, "locais": locais}


class DocumentoHTML(HTMLParser):
    def __init__(self, texto):
        super().__init__(convert_charrefs=True)
        self.elementos = []
        self.feed(texto)
        self.close()

    def handle_starttag(self, tag, attrs):
        self.elementos.append((tag, dict(attrs)))

    def handle_startendtag(self, tag, attrs):
        self.handle_starttag(tag, attrs)

    def com_classe(self, nome):
        return [(tag, attrs) for tag, attrs in self.elementos
                if nome in attrs.get("class", "").split()]


class FixtureMapa(unittest.TestCase):
    def setUp(self):
        self.data = mapa_sintetico()
        self.codigos = {local["codigo"] for local in self.data["locais"]}
        self.jpeg = jpeg_sof()


class ValidacaoMapaTests(FixtureMapa):
    def test_aceita_metadados_documentais_opcionais_sem_alterar_seu_texto(self):
        self.data.update(titulo="Locações de A Caloteira do Oeste",
                         origem="Mapa fornecido pelo usuário",
                         precisao="Referências aproximadas; marcas reais no GTA",
                         catalogoMarcas="%USERPROFILE%/GTA_Diretor/catalogos/locais_marcados.xml")
        antes = deepcopy(self.data)
        validar_mapa(self.data, self.codigos, self.jpeg)
        self.assertEqual(self.data, antes)

    def test_metadados_opcionais_exigem_texto_nao_vazio(self):
        for campo in ("titulo", "origem", "precisao", "catalogoMarcas"):
            for valor in ("", "   ", 1, True, None, [], {}):
                with self.subTest(campo=campo, valor=valor):
                    data = deepcopy(self.data)
                    data[campo] = valor
                    with self.assertRaises(ValueError):
                        validar_mapa(data, self.codigos, self.jpeg)

    def test_recusa_metadado_desconhecido(self):
        self.data["campoNaoPrevisto"] = "texto válido em chave desconhecida"
        with self.assertRaises(ValueError):
            validar_mapa(self.data, self.codigos, self.jpeg)

    def test_aceita_os_21_codigos_e_tres_estados_sem_alterar_a_fonte(self):
        antes = deepcopy(self.data)
        self.assertIsNone(validar_mapa(self.data, self.codigos, self.jpeg))
        self.assertEqual(self.data, antes)

    def test_aceita_coordenadas_normalizadas_nos_limites_inclusive(self):
        for x, y in ((0, 0), (1, 1), (0.0, 1.0), (1.0, 0.0)):
            with self.subTest(x=x, y=y):
                self.data["locais"][0].update(x=x, y=y)
                validar_mapa(self.data, self.codigos, self.jpeg)

    def test_recusa_20_ou_22_locais(self):
        for total in (20, 22):
            with self.subTest(total=total):
                data = deepcopy(self.data)
                if total == 20:
                    data["locais"].pop()
                else:
                    extra = deepcopy(data["locais"][-1])
                    extra.update(numero=22, codigo="LOCAL_22")
                    data["locais"].append(extra)
                with self.assertRaises(ValueError):
                    validar_mapa(data, self.codigos, self.jpeg)

    def test_recusa_numeracao_duplicada_ou_fora_de_1_a_21(self):
        for numero in (0, 22, 2, True, "1", 1.0):
            with self.subTest(numero=numero):
                data = deepcopy(self.data)
                data["locais"][0]["numero"] = numero
                with self.assertRaises(ValueError):
                    validar_mapa(data, self.codigos, self.jpeg)

    def test_recusa_codigo_duplicado_e_substituicao_do_catalogo(self):
        for codigo in ("LOCAL_02", "NAO_CATALOGADO"):
            with self.subTest(codigo=codigo):
                data = deepcopy(self.data)
                data["locais"][0]["codigo"] = codigo
                with self.assertRaises(ValueError):
                    validar_mapa(data, self.codigos, self.jpeg)

    def test_exige_o_conjunto_exato_do_catalogo_mesmo_com_21_codigos(self):
        catalogo_diferente = (self.codigos - {"LOCAL_01"}) | {"LOCAL_FORA"}
        with self.assertRaises(ValueError):
            validar_mapa(self.data, catalogo_diferente, self.jpeg)

    def test_recusa_coordenadas_nao_finitas_fora_do_mapa_e_tipos_incorretos(self):
        valores = (float("nan"), float("inf"), float("-inf"),
                   -0.001, 1.001, True, False, "0.5", None)
        for eixo in ("x", "y"):
            for valor in valores:
                with self.subTest(eixo=eixo, valor=valor):
                    data = deepcopy(self.data)
                    data["locais"][0][eixo] = valor
                    with self.assertRaises(ValueError):
                        validar_mapa(data, self.codigos, self.jpeg)

    def test_exige_booleanos_reais(self):
        for campo in ("episodio1", "marcaReal"):
            for valor in (0, 1, "true", "false", None, []):
                with self.subTest(campo=campo, valor=valor):
                    data = deepcopy(self.data)
                    data["locais"][0][campo] = valor
                    with self.assertRaises(ValueError):
                        validar_mapa(data, self.codigos, self.jpeg)

    def test_recusa_estado_desconhecido_e_marca_real_sem_estado_salvo(self):
        for estado in ("Confirmado", "região indicada", "", None, ESTADOS[1], ESTADOS[2]):
            with self.subTest(estado=estado):
                data = deepcopy(self.data)
                data["locais"][0].update(estado=estado, marcaReal=True)
                with self.assertRaises(ValueError):
                    validar_mapa(data, self.codigos, self.jpeg)

    def test_confere_largura_e_altura_do_sof_com_o_json(self):
        for largura, altura in ((3199, 3200), (3200, 3199), (1600, 1600)):
            with self.subTest(largura=largura, altura=altura):
                with self.assertRaises(ValueError):
                    validar_mapa(self.data, self.codigos, jpeg_sof(largura, altura))

    def test_recusa_imagem_sem_soi_ou_sof_com_segmento_truncado(self):
        for jpeg in (b"", b"not a jpeg", b"\xff\xd8\xff\xd9", self.jpeg[:10]):
            with self.subTest(jpeg=jpeg):
                with self.assertRaises(ValueError):
                    validar_mapa(self.data, self.codigos, jpeg)


class DistribuicaoMapaTests(FixtureMapa):
    def conferir_distribuicao(self, locais, largura=3200, altura=3200):
        antes = deepcopy(locais)
        resultado = posicionar(locais, largura, altura)
        self.assertEqual(locais, antes)
        self.assertEqual(len(resultado), len(locais))
        self.assertEqual({p["codigo"] for p in resultado}, self.codigos)
        originais = {p["codigo"]: p for p in locais}
        for ponto in resultado:
            self.assertIsNot(ponto, originais[ponto["codigo"]])
            for campo, valor in originais[ponto["codigo"]].items():
                self.assertEqual(ponto[campo], valor)
            for eixo, limite in (("px", largura), ("py", altura)):
                self.assertIsInstance(ponto[eixo], (int, float))
                self.assertNotIsInstance(ponto[eixo], bool)
                self.assertTrue(math.isfinite(ponto[eixo]))
                self.assertGreaterEqual(ponto[eixo], 0)
                self.assertLessEqual(ponto[eixo], limite)
        for a, b in combinations(resultado, 2):
            distancia = math.hypot(a["px"] - b["px"], a["py"] - b["py"])
            self.assertGreaterEqual(distancia, 150 - 1e-6,
                                    "%s e %s colidiram" % (a["codigo"], b["codigo"]))
        self.assertEqual(posicionar(locais, largura, altura), resultado)
        return resultado

    def test_espalha_21_pontos_iguais_no_centro_sem_mover_as_ancoras(self):
        for local in self.data["locais"]:
            local.update(x=0.5, y=0.5)
        self.conferir_distribuicao(self.data["locais"])

    def test_espalha_21_pontos_iguais_nos_quatro_cantos_dentro_da_imagem(self):
        for x, y in ((0, 0), (1, 1), (0, 1), (1, 0)):
            with self.subTest(x=x, y=y):
                locais = deepcopy(self.data["locais"])
                for local in locais:
                    local.update(x=x, y=y)
                self.conferir_distribuicao(locais)

    def test_distribuicao_de_ancoras_distintas_conserva_metadados(self):
        self.conferir_distribuicao(self.data["locais"])


class RenderizacaoMapaTests(FixtureMapa):
    def conferir_jpeg_embutido(self, texto):
        imagens = re.findall(r"data:image/jpeg;base64,([A-Za-z0-9+/=]+)", texto)
        self.assertTrue(imagens, "O arquivo precisa conter a imagem para uso offline")
        for imagem in imagens:
            self.assertEqual(b64decode(imagem, validate=True), self.jpeg)

    def conferir_codigos(self, elementos):
        self.assertEqual(len(elementos), 21)
        self.assertEqual({attrs["data-codigo"] for _, attrs in elementos}, self.codigos)

    def test_html_tem_jpeg_21_pins_e_21_itens_da_lista_sem_mutar_dados(self):
        antes = deepcopy(self.data)
        texto = renderizar_html(self.data, self.jpeg)
        self.assertIsInstance(texto, str)
        documento = DocumentoHTML(texto)
        self.conferir_jpeg_embutido(texto)
        pins = documento.com_classe("pin")
        itens = documento.com_classe("local-item")
        self.conferir_codigos(pins)
        self.conferir_codigos(itens)
        self.assertTrue(all(tag == "g" for tag, _ in pins))
        self.assertTrue(all(tag == "button" for tag, _ in itens))
        self.assertEqual(self.data, antes)

    def test_svg_e_xml_valido_com_jpeg_21_pins_e_legenda_completa(self):
        antes = deepcopy(self.data)
        texto = renderizar_svg(self.data, self.jpeg)
        raiz = ET.fromstring(texto)
        self.assertEqual(raiz.tag.rsplit("}", 1)[-1], "svg")
        self.conferir_jpeg_embutido(texto)
        for classe in ("pin", "legend-row"):
            elementos = [(e.tag.rsplit("}", 1)[-1], e.attrib) for e in raiz.iter()
                         if classe in e.attrib.get("class", "").split()]
            self.conferir_codigos(elementos)
        self.assertEqual(self.data, antes)

    def test_html_nao_depende_de_imagens_scripts_ou_estilos_externos(self):
        texto = renderizar_html(self.data, self.jpeg)
        documento = DocumentoHTML(texto)
        for tag, attrs in documento.elementos:
            if tag in ("script", "img", "image", "iframe", "source"):
                for campo in ("src", "href", "xlink:href", "srcset"):
                    if attrs.get(campo):
                        self.assertTrue(attrs[campo].startswith(("data:", "#")),
                                        "Recurso externo: %s" % attrs[campo])
            if tag == "link" and "stylesheet" in attrs.get("rel", "").split():
                self.assertTrue(attrs.get("href", "").startswith("data:"))
        self.assertNotRegex(texto, r"(?i)@import\s+(?:url\(|['\"])")
        self.assertNotRegex(texto, r"(?i)url\(\s*['\"]?(?:https?:|//)")

    def test_textos_nao_podem_fechar_script_nem_injetar_html_ou_svg(self):
        normal = DocumentoHTML(renderizar_html(self.data, self.jpeg))
        ataque = ('</script><script data-prova="injetado">alert("teste")</script>'
                  '<img data-prova="injetado" src="https://evil.invalid/asset">'
                  '<g data-prova="injetado">ação & "aspas"</g>')
        for campo in ("nome", "regiao", "observacao"):
            with self.subTest(campo=campo):
                data = deepcopy(self.data)
                data["locais"][0][campo] = ataque
                html = renderizar_html(data, self.jpeg)
                documento = DocumentoHTML(html)
                self.assertFalse(any(a.get("data-prova") == "injetado"
                                     for _, a in documento.elementos))
                self.assertEqual(sum(t == "script" for t, _ in documento.elementos),
                                 sum(t == "script" for t, _ in normal.elementos))
                self.assertNotIn('</script><script data-prova="injetado">', html)
                svg = ET.fromstring(renderizar_svg(data, self.jpeg))
                self.assertFalse(any(e.attrib.get("data-prova") == "injetado"
                                     for e in svg.iter()))


class PublicacaoMapaTests(FixtureMapa):
    def test_backup_corrompido_impede_substituir_qualquer_saida(self):
        with tempfile.TemporaryDirectory(prefix="mapa-teste-") as temporario:
            saida = Path(temporario)
            antigos = {HTML_NOME: b"html original\r\n", SVG_NOME: b"svg original\r\n"}
            for nome, conteudo in antigos.items():
                (saida / nome).write_bytes(conteudo)

            def copia_corrompida(origem, destino, *args, **kwargs):
                Path(destino).write_bytes(b"bytes diferentes da origem")
                return str(destino)

            with mock.patch.object(gerador.shutil, "copy2", side_effect=copia_corrompida) as copia:
                with self.assertRaises(ValueError):
                    publicar(saida, "novo html", "novo svg")
                self.assertTrue(copia.called)
            for nome, conteudo in antigos.items():
                self.assertEqual((saida / nome).read_bytes(), conteudo)
            self.assertFalse(list(saida.glob("*.tmp")))

    def test_falha_na_segunda_substituicao_restaura_o_par_anterior(self):
        with tempfile.TemporaryDirectory(prefix="mapa-teste-") as temporario:
            saida = Path(temporario)
            antigos = {HTML_NOME: b"html antigo", SVG_NOME: b"svg antigo"}
            for nome, conteudo in antigos.items():
                (saida / nome).write_bytes(conteudo)
            substituir = gerador.os.replace
            falhou = False

            def falhar_svg(origem, destino):
                nonlocal falhou
                if Path(destino) == saida / SVG_NOME and not falhou:
                    falhou = True
                    raise OSError("Falha de escrita simulada somente no SVG")
                return substituir(origem, destino)

            with mock.patch.object(gerador.os, "replace", side_effect=falhar_svg):
                with self.assertRaises(OSError):
                    publicar(saida, "html novo", "svg novo")
            self.assertTrue(falhou)
            for nome, conteudo in antigos.items():
                self.assertEqual((saida / nome).read_bytes(), conteudo)
            self.assertFalse([p for p in saida.iterdir() if p.suffix in (".tmp", ".restore")])

    def test_primeira_publicacao_cria_utf8_no_destino_sem_alterar_jpeg(self):
        with tempfile.TemporaryDirectory(prefix="mapa-teste-") as temporario:
            raiz = Path(temporario)
            imagem = raiz / "MAPA_COMPLETO_GTA_V.jpg"
            imagem.write_bytes(self.jpeg)
            hash_antes = sha256(imagem.read_bytes()).hexdigest()
            saida = raiz / "saída com ação"
            html, svg = "<html>Locações · ação\n</html>", "<svg>Região · avó\n</svg>"
            resultado = publicar(saida, html, svg)
            self.assertEqual((saida / HTML_NOME).read_bytes(), html.encode("utf-8"))
            self.assertEqual((saida / SVG_NOME).read_bytes(), svg.encode("utf-8"))
            self.assertIsInstance(resultado, dict)
            self.assertTrue(resultado)
            self.assertTrue(all(isinstance(k, str) and isinstance(v, str)
                                for k, v in resultado.items()))
            self.assertEqual(sha256(imagem.read_bytes()).hexdigest(), hash_antes)

    def test_sobrescrita_guarda_bytes_exatos_do_par_anterior_em_backup(self):
        with tempfile.TemporaryDirectory(prefix="mapa-teste-") as temporario:
            saida = Path(temporario)
            anterior_html = b'\xef\xbb\xbf<html>vers\xc3\xa3o antiga</html>\r\n'
            anterior_svg = b'<svg>original\x00</svg>\r\n'
            (saida / HTML_NOME).write_bytes(anterior_html)
            (saida / SVG_NOME).write_bytes(anterior_svg)
            imagem = saida / "MAPA_COMPLETO_GTA_V.jpg"
            imagem.write_bytes(self.jpeg)
            preservado = saida / "arquivo_do_usuario.txt"
            preservado.write_bytes(b"preservar sem alterar\r\n")
            publicar(saida, "<html>nova ação</html>", "<svg>novo</svg>")
            backups = saida / "backups" / "mapa-locacoes"
            htmls = list(backups.rglob(HTML_NOME))
            svgs = list(backups.rglob(SVG_NOME))
            self.assertEqual(len(htmls), 1)
            self.assertEqual(len(svgs), 1)
            self.assertEqual(htmls[0].parent, svgs[0].parent)
            self.assertEqual(htmls[0].read_bytes(), anterior_html)
            self.assertEqual(svgs[0].read_bytes(), anterior_svg)
            self.assertEqual((saida / HTML_NOME).read_text(encoding="utf-8"),
                             "<html>nova ação</html>")
            self.assertEqual((saida / SVG_NOME).read_text(encoding="utf-8"), "<svg>novo</svg>")
            self.assertEqual(imagem.read_bytes(), self.jpeg)
            self.assertEqual(preservado.read_bytes(), b"preservar sem alterar\r\n")

    def test_publicacoes_sucessivas_preservam_cada_versao_sem_reusar_backup(self):
        with tempfile.TemporaryDirectory(prefix="mapa-teste-") as temporario:
            saida = Path(temporario)
            publicar(saida, "html versão 1", "svg versão 1")
            publicar(saida, "html versão 2", "svg versão 2")
            publicar(saida, "html versão 3", "svg versão 3")
            backups = saida / "backups" / "mapa-locacoes"
            htmls = list(backups.rglob(HTML_NOME))
            svgs = list(backups.rglob(SVG_NOME))
            self.assertEqual(len(htmls), 2)
            self.assertEqual(len(svgs), 2)
            self.assertEqual({p.read_bytes() for p in htmls},
                             {"html versão 1".encode(), "html versão 2".encode()})
            self.assertEqual({p.read_bytes() for p in svgs},
                             {"svg versão 1".encode(), "svg versão 2".encode()})
            self.assertEqual({p.parent for p in htmls}, {p.parent for p in svgs})
            self.assertEqual((saida / HTML_NOME).read_text(encoding="utf-8"), "html versão 3")
            self.assertEqual((saida / SVG_NOME).read_text(encoding="utf-8"), "svg versão 3")


if __name__ == "__main__":
    unittest.main()
