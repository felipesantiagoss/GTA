# Preparação dos catálogos da série

## Estado atual da retomada0.9.5

Os13 figurinos reais estão registrados no perfil desde0.9.2; revisões autorizadas0.9.4/0.9.5 concluídas com backup e hashes, sem regenerar identidade. [Continuidade atual](catalogos/figurinos_continuidade_0_9_5.json) conservaXMLs/modelos/roupas/DNA para temporada2. A prévia tem três grupos4/5/4, F6 monta, PageDown/PageUp navegam eF7 apresenta. Aprovação visual continua pendente. O catálogo do perfil tem recursos carregáveis `pronto=true;gerado_no_jogo;teste_visual_pendente`; o catálogo de preparação do workspace não deve substituir esse arquivo real.

Todos95IDs têm planos/vozes/SRTs base. Há26JSONs de atuação integral e69pendências, mais duas subtomadasC05 para edição. EP01_C08 está preparado com moto/celular e49s incluindo contagem. SóTESTE_AMBIENTE tem marca; ROCA_GRAPESEED ainda exigeF3 no jogo. Captura real eMP4 no episódio ainda não existem. O usuário não precisa criar/salvar roupa noMenyoo. Ver [README](README.md) para teclas/evidências atuais.

## Histórico da preparação inicial0.7

As seções abaixo registram o estado anterior à geração real e às revisões; suas pendências históricas não substituem o estado atual acima.

Os catálogos da Etapa 7 conservam os códigos do roteiro. Há **13 entradas de produção e dois atores de teste**, **21 locais de produção e uma marca de teste**, e **cinco intenções de gesto**. Personagens e figurinos da série continuam pendentes; nenhum identificador de roupa, rosto ou coordenada foi inventado. Nenhuma cena de episódio foi gerada nesta preparação.

| Arquivo | Conteúdo e autoridade |
|---|---|
| `catalogos/personagens.json` | Códigos, nomes, modelos candidatos ou pendentes, estado e origem. Somente `TESTE_HOMEM` e `TESTE_MULHER` têm `pronto=true`. |
| `catalogos/locais.json` | Os 21 códigos da Parte 2 e `TESTE_AMBIENTE`; sem XYZ. A marca real continua em `catalogos/locais_marcados.xml`. |
| `catalogos/animacoes.json` | Cinco gestos e estado por variante masculina/feminina, com os pares exatos de `catalogos/gestos_fala.xml`. O XML permanece canônico. |
| `catalogos/elenco.xml` | Evidência anterior dos dois modelos business, atualizada com a confirmação de boca e interações da Etapa 3. |

As raízes JSON contêm `versao:1` e a lista `personagens`, `locais` ou `animacoes`. `origem` é texto. Nos personagens, `modelo:null` indica que a pesquisa ainda não escolheu um modelo; o candidato freemode não libera uma aparência pendente. Nos locais, `pronto` é informação de preparação: uma coordenada só existe quando foi capturada no XML de marcas. Nos gestos, os códigos são `neutro`, `explicando`, `negando`, `implorando`, `irritado`; `homem` e `mulher` contêm `dicionario`, `animacao` e `estado`, mais `modeloTestado`, `cenaTeste`, `confirmadoEm` apenas quando já confirmados.

## Personagens e figurinos

| Código | Preparação ainda necessária |
|---|---|
| `DJANIRA_POBRE` | Mulher branca, ruiva, olhos azuis; short, camisa simples, chinelo, rabo de cavalo e sem maquiagem. Episódios 1–10. |
| `DJANIRA_RICA` | A mesma Djanira e o mesmo rosto; roupa cara, salto, joias e cabelo solto. Episódios 11–15. |
| `DINJER` | Mulher negra, afro volumoso, batom forte e uniforme de aeromoça com blazer, saia, lenço e salto. Figurino fixo durante a temporada. |
| `DONA_IRACI` | Pesquisar modelo de mulher idosa e roupa simples; preservar o visual aprovado. |
| `O_CHEFE` | Pesquisar homem branco de meia-idade, cabelo grisalho e terno escuro caro. |
| `CAPANGA_1`–`CAPANGA_4` | Pesquisar modelos de terno e manter cada visual fixo. `CAPANGA_1` também fala como dono da fazenda em `EP03_C07`; isso não exige um código ou figurino extra. |
| `COLEGA_1`–`COLEGA_3` | Pesquisar homens com roupa casual. Reaparecem em `EP04_C02`, apesar do resumo do elenco citar apenas o Episódio 1. |
| `ATENDENTE` | Pesquisar modelo genérico para o banco; preservar o mesmo figurino. |
| `TESTE_HOMEM` / `TESTE_MULHER` | `a_m_y_business_01` / `a_f_y_business_01`, nomes na legenda `Homem` / `Mulher`; prontos apenas para os testes técnicos. |

O roteiro indica `mp_f_freemode_01` para Djanira e Dinjer. Esse modelo existe no Menyoo instalado, `menyooStuff/PedList.xml`, linha 9, como `Freemode MP Female`; é **candidato**, sem aprovação visual. Os modelos business constam nas linhas 216 e 95. A lista é um recurso do jogo instalado, em `C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V\menyooStuff\PedList.xml`.

O Codex providencia pesquisa, opções e captura/importação do visual aprovado. A decisão de aparência fica com o usuário quando necessária no Menyoo; ele não precisa procurar IDs técnicos ou digitar XYZ. A identidade de Djanira deve ser capturada uma vez e reutilizada nos dois figurinos. Nenhum rosto genérico será marcado como sua identidade definitiva. Não liberar `pronto=true` só por existir o modelo: faltam aprovação visual e suporte real para aplicar seus componentes de aparência no Diretor.

A regra atual é **só Djanira trocar de roupa**. As indicações divergentes de roupa no roteiro permanecem registradas em `PLANEJAMENTO_CENAS_E_CAMERA.md`; não criar variantes silenciosas. A dívida permanece **R$ 1.000**.

## Locais

`TESTE_AMBIENTE` é a única marca real presente no XML nesta preparação. Sua origem é `capturado_no_jogo`; JSON referencia o código e não duplica a posição. Os 21 códigos da série foram copiados da Parte 2, sem atribuir a eles a marca do teste.

Não é necessário marcar os 21 locais agora. Para a primeira cena completa, será necessária **`ROCA_GRAPESEED`**. Quando chegar esse passo, o Codex configura o código de marca e orienta o usuário a posicionar o personagem no ponto desejado e usar a tecla de marcação. A marca registra posição e direção reais; não exige coordenadas digitadas nem cria waypoint.

Depois, agrupar as marcações por região, seguindo o planejamento existente: Grapeseed; Sandy Shores/Grand Senora; Great Chaparral; Vinewood; canal/Cypress Flats; centro/Strawberry; La Puerta/aeroporto; Del Perro/Rockford Hills. Isso é uma rota de preparação, não um pedido para percorrer tudo nesta etapa.

O roteiro coloca a boate em Hawick; a divergência para Bahama Mamas West em Del Perro já está documentada no planejamento. `BOATE_DENTRO` continua condicionado ao teste do interior e à compatibilidade no GTA Legacy; fachada é alternativa. `AEROPORTO_AR` exige o teste próprio da queda de avião, com invencibilidade, antes de cenas aéreas.

## Gestos conferidos

Os pares abaixo foram conferidos novamente no `menyooStuff/PedAnimList.txt` instalado. Presença na lista comprova o identificador, não a aparência em um modelo novo.

| Gesto | Homem: dicionário / clipe | Mulher: dicionário / clipe | Linhas homem / mulher | Confirmação visual atual |
|---|---|---|---|---|
| `neutro` | `amb@world_human_hang_out_street@male_a@base` / `base` | `amb@world_human_hang_out_street@female_arm_side@base` / `base` | 54883 / 54865 | Homem do teste; mulher pendente. |
| `explicando` | `gestures@m@standing@casual` / `gesture_hand_down` | `gestures@f@standing@casual` / `gesture_hand_down` | 60469 / 60137 | Mulher do teste; homem pendente. |
| `negando` | `gestures@m@standing@casual` / `gesture_head_no` | `gestures@f@standing@casual` / `gesture_head_no` | 60485 / 60153 | Homem do teste; mulher pendente. |
| `implorando` | `gestures@m@standing@casual` / `gesture_why` | `gestures@f@standing@casual` / `gesture_why` | 60492 / 60160 | Mulher do teste; homem pendente. Intensidade de súplica na série pendente. |
| `irritado` | `gestures@m@standing@casual` / `gesture_damn` | `gestures@f@standing@casual` / `gesture_damn` | 60470 / 60138 | Homem do teste; mulher pendente. |

O estado é conservado por sexo e modelo, conforme o XML. Aprovar boca/cortes do teste não aprova todas as variantes de corpo ou os modelos freemode. A facial de fala `mp_facial/mic_chatter` e os faciais neutros continuam no XML e foram confirmados nos dois modelos business; `animacoes.json` contém somente os cinco gestos deste contrato. `machucado` exige tratamento separado antes das cenas que o usam; não substituir por uma intenção disponível sem informar.

## Próxima cena completa

A primeira é **`EP01_C08` — O PEDIDO**, em `ROCA_GRAPESEED`, 17:00, clima limpo, com `DJANIRA_POBRE` e `DINJER`. A linha de tempo do roteiro contém **6 s de narração + 24 s de diálogo = 30 s**, apesar de `dur` dizer 28 s; conservar os tempos, mais 3 s de contagem e qualquer ação extra necessária.

Antes de gerar essa cena, preparar a identidade/figurino das duas, marcar a roça e implementar/testar a chegada de moto, o andar e a ação de celular/Pix declarados. Não reduzir a cena a dois modelos business nem fingir que o contrato atual já executa moto e celular. O apoio para gravar/organizar a tomada e o formato completo também precisam estar prontos. Esta etapa cria a base dos catálogos e o teste JSON; não gera `EP01_C08`, outros episódios ou a gravação da série.
