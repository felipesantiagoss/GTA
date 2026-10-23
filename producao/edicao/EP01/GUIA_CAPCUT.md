# EP01 — "O EMPRÉSTIMO"

Material de preparação para dublagem/CapCut. Áudios sugeridos ainda não existem; vídeos ainda não são confirmados por este guia.

A ordem abaixo é a do roteiro. A vinheta gravada uma única vez entra no final. Planos/SRTs fonte permanecem intactos.

## Referência de tempo e sincronismo

A entrada de voz no CSV tem três referências: conteúdo(sem contagem), F7(com3s de contagem) e episódio preliminar. O SRT base é relativo a F7, sem inferir o começo do MP4.

No CapCut, identifique o início real da ação após a contagem e retire toda a preparação anterior. Se a captura começou antes deF7, remover apenas3s do começo do MP4 dá erro. Use imagem/contagem para ajustar o offset de cada tomada; data de criação do arquivo não sincroniza voz.

O SRT de continuidade assume clipes com as durações planejadas abaixo, já sem contagem. ANTERIORMENTE/cartelas/flashbacks extras, transições e recortes de duração indefinida ficam fora dessa conta. Ao inseri-los, desloque/revise os tempos no CapCut. Não tratar esta duração como medida de um filme real.

Reservas de tempo do cabeçalho não são falas nem silêncios escritos. Vozes e pausas explícitas nunca devem ser comprimidas para caber na estimativa.

## Ordem de montagem

| Cena | Entrada base | Duração planejada | SRT fonte |
|---|---|---|---|
| EP01_C01 | 00:00:00,000 | 14s | `EP01/srt_base/EP01_C01.base.srt` |
| EP01_C02 | 00:00:14,000 | 10s | `EP01/srt_base/EP01_C02.base.srt` |
| EP01_C03 | 00:00:24,000 | 9s | `EP01/srt_base/EP01_C03.base.srt` |
| EP01_C04 | 00:00:33,000 | 7s | `EP01/srt_base/EP01_C04.base.srt` |
| EP01_C05 | 00:00:40,000 | 8s | `EP01/srt_base/EP01_C05.base.srt` |
| EP01_C06 | 00:00:48,000 | 9s | `EP01/srt_base/EP01_C06.base.srt` |
| EP01_C07 | 00:00:57,000 | 20s | `EP01/srt_base/EP01_C07.base.srt` |
| EP01_C08 | 00:01:17,000 | 30s | `EP01/srt_base/EP01_C08.base.srt` |
| EP01_C09 | 00:01:47,000 | 7s | `EP01/srt_base/EP01_C09.base.srt` |
| VINHETA_01 | 00:01:54,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**NO PRÓXIMO EPISÓDIO** *(montagem de 5s, cortes do Ep 2)*:
Dinjer na boate bebendo. Dinjer sentada com quatro homens de terno.

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

Correspondência derivada para a montagem anunciada: EP02_C01, EP02_C02. Conferir o trecho escolhido, preservando a nota original.

### EP01_C01

**Ação:** Avião voando. A Djanira cai do avião, despenca e bate na água do canal. Emerge encharcada, sobe pela rampa de concreto e corre.

⚠️ **Cena técnica.** Invencibilidade LIGADA. Trate como etapa separada.

### EP01_C02

**Ação:** Djanira andando devagar no meio da plantação, trabalhando. Câmera bem afastada, ela pequena no quadro.

**CARTELA NA TELA:** `QUATRO SEMANAS ANTES`


### EP01_C03

**Ação:** As duas sentadas ou em pé na frente da casa. Djanira ajeita algo para a avó. Sem diálogo — só convivência.

### EP01_C04

**Ação:** Djanira deixa a avó na entrada da clínica, se despede e vai embora. A avó entra.

### EP01_C05

**Ação:** A avó sai da clínica olhando para os lados, pega um caminho diferente e entra no cassino. Expressão muda de `machucado` para `feliz`.

⚠️ Se o interior do cassino não abrir, grave só a **fachada** com ela entrando. Funciona igual.

> 💡 **O público vê o que a Djanira não vê.** A narração continua inocente de propósito. Não mude.

### EP01_C06

**Ação:** Dinjer descendo de um ônibus e andando até o terminal, com jeito de passarela, confiante.

### EP01_C07

**Ação:** Dinjer passa por três colegas. Eles falam dela. Ela para, vira e responde. Termina em empurrão e confusão coreografada.
**Ação final:** 2 segundos de empurra-empurra. Corte seco.

⚠️ **Não use combate real.** Use animações de luta encadeadas, que repetem igual todas as vezes.

### EP01_C08

**Ação:** Dinjer chega de moto, desce, anda com jeito de passarela até a Djanira e para na frente dela. As duas conversam. No fim, Djanira mexe no celular fazendo o Pix.

- Estimativa 28000ms menor que blocos declarados 30000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

> 🔴 **Esta é a cena de teste do sistema.** Gere esta primeiro.

### EP01_C09

**Ação:** Djanira sozinha, olhando a moto da Dinjer ir embora.

### VINHETA_01 — reutilizar no final

Uma gravação, quatro tomadas de5s; danças ainda exigem clipes reais/testados. Créditos montados no CapCut:

```
A CALOTEIRA DO OESTE
Uma novela de FELIPE SANTIAGO
Roteiro — FELIPE SANTIAGO
Direção — FELIPE SANTIAGO
Fotografia — FELIPE SANTIAGO
Dono do computador — FELIPE SANTIAGO
Quem pagou o GTA — FELIPE SANTIAGO
Agradecimento especial — FELIPE SANTIAGO
```

## Dublagem

O CSV inclui todos os blocos originais, inclusive pausas/ação final sem voz. ArquivoAudioSugerido é somente um nome reservado na preparação: nenhum WAV/MP3 foi gerado. Narração e NARRADOR ficam emoff, sem boca; diálogos usam a atuação de fala. Não dublar os silêncios.

## Conferência antes da exportação

- Conferir visual/ações/props/câmera na tomada real, sem substituir ações ausentes.
- Alinhar cada voz ao marcador visual do clipe e conferir os silêncios.
- Rever o SRT depois de inserir recap/cartelas/flashbacks/transições.
- Conferir o vídeo horizontal16:9 e o nome real da tomada; preservar regravações `_v2` etc.
