# Queda completa — EP01_C01

O rascunho conserva a narração original de 14 segundos, sem boca, e declara mais 46 segundos sem voz para a sequência física. A tomada bruta dura 60 segundos, com 3 segundos de contagem inicial. O roteiro original permanece intacto. O CapCut recebe o bruto e a voz original; a reserva não autoriza cortar palavras nem declarar toda a física concluída em 14 segundos.

O ensaio técnico separado de N deve ser testado primeiro. A cena completa usa Djanira própria, avião Velum e piloto próprios: voo, saída física do assento, queda, impacto na água, emersão, nado, subida da rampa e corrida. A sequência precisa terminar realmente antes de 60 segundos; impedimentos interrompem a tomada sem reposicionar a atriz durante a filmagem. Atores ficam invencíveis e protegidos contra afogamento durante o preparo e a ação; a limpeza restaura as flags anteriores.

Quatro marcas capturadas no GTA são obrigatórias. Os três subpontos pertencem a CANAL_LS e não alteram os 21 códigos de locação do roteiro:

| Código | Captura real |
|---|---|
| CANAL_LS | Nadando na superfície, olhando ao longo de um trecho aberto e reto do canal. O ponto/direção define a passagem aérea; o impulso do salto pode adiantar o ponto efetivo de impacto. |
| CANAL_LS_RAMPA_ENTRADA | Junto ao pé de uma rampa de concreto existente, ainda na água. |
| CANAL_LS_RAMPA_TOPO | A pé, no alto seco dessa mesma rampa. |
| CANAL_LS_FUGA | A pé, no fim do percurso seco da corrida. |

Com a cena limpa por F10, L/K selecionam a marca e F3 salva a posição/direção. Nenhuma coordenada é preenchida antes dessa captura. O corredor aéreo deriva do Heading real da água; o avião começa 70 metros antes e 30 metros acima da superfície medida. A preparação confere água em 228 pontos ao longo de 300 metros, com 12 metros de largura. Água e entrada devem estar a até 160 metros, rampa entre 1 e 25 metros/rise máximo 8 metros, e corrida entre 2 e 40 metros. A distância efetiva de nado precisa caber em 15 segundos; esses limites geométricos não prometem que qualquer marca nesse intervalo seja navegável.

O piso da rampa/fuga é medido ao longo dos trechos, em passos de até 2 metros; chão, normal, continuidade, rota seca e seis linhas de colisão precisam passar. Não são criados degraus, rampas ou entradas no mapa. O material concreto, a roupa molhada, a câmera de olhos e a navegação aquática exigem confirmação visual no GTA; probes não comprovam esses aspectos estéticos.

## Integração

`EpisodioUmDiretor.LoadQuedaSpec(locais, arquivoXml)` lê as quatro marcas antes de limpar a tomada montada. `Prepare(..., localId, quedaSpec)` aceita C01 com `localId=CANAL_LS`, uma única Djanira, fuga completa, voz14s e timeout60s. O main posiciona inicialmente a atriz na marca real de água, sem exigir chão naquele ponto; `encenacao=serie_v1` e duração60s são obrigatórias.

`PollLoading`, `OwnedEntities`/`OwnedHandles`, `bool ReleaseActorsForReset` e `bool Reset` incluem os recursos da queda. Reset espera a liberação dos assentos e a entrada dos ocupantes em frames seguintes, sem cancelar/reemitir a tarefa pendente. O scheduler do main conserva o limite contínuo de 5 segundos, fora da contagem/captura.

Inicializar a câmera normal antes de Reset da queda. `UsesOwnCamera=true` suspende update/controles/cortes de câmera normais, inclusive o plano final e os cortes por narração, enquanto mantém HUD oculto, legendas e captura. Reset cria/posiciona a câmera própria LONGE e congela o avião; a imagem permanece fixa nos 3 segundos de contagem. Update só inicia o voo depois dela; a câmera muda para OLHOS na queda e acompanha a atriz até a fuga. `IsFinished` e `IsSuccess` devem ser conferidos antes de concluir a captura; `ObservedDurationMs` registra o tempo físico.

Limpar o helper da queda antes da câmera normal. A câmera anterior é restaurada pelo backend; só avião/piloto próprios são removidos. Saída pendente conserva referências para repetir a limpeza. Nenhum native/GTA foi executado pelos testes de arquivos ou pela compilação.
