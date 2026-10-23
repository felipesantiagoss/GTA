# EP08 — "O QUE A VÓ GUARDOU"

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
| EP08_C01 | 00:00:00,000 | 6s | `EP08/srt_base/EP08_C01.base.srt` |
| EP08_C02 | 00:00:06,000 | 11s | `EP08/srt_base/EP08_C02.base.srt` |
| EP08_C03 | 00:00:17,000 | 42s | `EP08/srt_base/EP08_C03.base.srt` |
| EP08_C04 | 00:00:59,000 | 30s | `EP08/srt_base/EP08_C04.base.srt` |
| EP08_C05 | 00:01:29,000 | 12s | `EP08/srt_base/EP08_C05.base.srt` |
| EP08_C06 | 00:01:41,000 | 14s | `EP08/srt_base/EP08_C06.base.srt` |
| VINHETA_01 | 00:01:55,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE**: EP07_C04 (o aviso do Chefe) · EP07_C04 fala 6 ("desculpa, amiga") · EP07_C05 (a queda)

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP08_C01

**Ação:** Djanira sai da água do canal, encharcada, subindo a rampa de concreto com dificuldade. Expressão `machucado`.

- Roupa rasgada/descalça não autoriza criar um terceiro figurino de Djanira; efeito molhado/machucado exige recurso próprio. preservar origem e implementar regra do prompt explicitamente

### EP08_C02

**Ação:** Djanira andando descalça na beira da estrada, ao nascer do sol. Câmera o mais longe possível.

- Djanira descalça conflita com o figurino fixo; registrar a adaptação sem alterar o roteiro. preservar origem e implementar regra do prompt explicitamente

### EP08_C03

**Ação:** A avó deitada, muito pior. Chama a Djanira para perto e conta tudo.

> 🔴 **A cena mais importante do primeiro bloco.** Cortes lentos, planos fechados, deixe respirar.

### EP08_C04

**Ação:** A avó desvia o olhar, envergonhada.

> 💡 **Insira aqui o flashback da EP01_C05** — a avó saindo do hospital e entrando no cassino. Agora o público entende.

> 💡 **Insira aqui o flashback da EP01_C05** — a avó saindo do hospital e entrando no cassino. Agora o público entende.

### EP08_C05

**Ação:** A avó entrega um papel dobrado na mão da Djanira. Close nas mãos.

### EP08_C06

**Ação:** Djanira parada na porta do quarto, de costas para a câmera. **Não mostre a avó.** Corte para o preto.

- Estimativa 7000ms menor que blocos declarados 14000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

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
