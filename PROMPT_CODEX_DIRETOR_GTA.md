# Diretor de Cenas para GTA V (modo história) — prompt inicial para o Codex

Leia tudo antes de começar. Este documento explica quem eu sou, o que já está instalado, o que você deve construir, em que ordem, e as regras. Não pule etapas.

---

## 1. Quem sou eu e o que eu quero

- Sou iniciante em programação de jogos e mods. Produzo vídeos curtos de histórias (TikTok, formato vertical 9:16) usando o GTA V como "estúdio de gravação".
- As vozes eu resolvo fora do jogo: parte com IA de voz, parte com a minha própria dublagem. Edito no iPad, pelo CapCut.
- **Objetivo final:** eu descrevo uma cena em português para você — lugar, hora, clima, personagens, roupas, objetos, carros, ações, falas e enquadramento — e você gera um **arquivo de cena**. No jogo, eu aperto uma tecla, a cena se monta e acontece sozinha, e **eu só gravo**.
- Quero que você construa e mantenha esse sistema, que vou chamar de **Diretor de Cenas**, em etapas pequenas e testáveis.
- **Primeiro objetivo (prioridade agora):** um teste simples, com **um homem e uma mulher conversando** (um diálogo curto), para eu ver se está tudo funcionando. Isso corresponde às Etapas 0 a 3 da seção 6. Ao terminar a Etapa 3, **pare e me pergunte** se quero continuar com as próximas etapas.

## 2. Como falar comigo

- Português simples, passo a passo, como para um leigo. Quando eu precisar agir no jogo ou no Windows, um passo por vez.
- Antes de instalar qualquer coisa ou fazer algo que exija administrador, explique o que é, por que é necessário e de onde vem (link oficial). Espere o meu "ok".
- **Você não vê o jogo.** Nunca diga que algo "funcionou" só porque compilou ou rodou sem erro. Diga exatamente o que eu devo fazer e o que devo ver na tela, peça que eu teste e leia os logs do script.
- Quando algo que eu pedir não existir no jogo (um movimento, uma roupa, um objeto), diga claramente e proponha a alternativa mais próxima.

## 3. Onde você está rodando

- Esta conversa deve estar aberta no **VS Code do notebook Dell (Windows)**, onde o GTA está instalado. Confirme isso no início: sistema operacional e se a pasta do jogo existe. Se você estiver em macOS, pare e me avise, porque aí você não tem acesso ao jogo.
- Pasta do projeto: `%USERPROFILE%\GTA_Diretor`, fora do OneDrive.
- **Não use o OneDrive da empresa** (aparece como "Felipe - CWI Software") para nada deste projeto.

## 4. O que já está instalado e funcionando (confirmado por mim)

### Computador
- Dell Pro 14 (PC14250), **Windows 11 Enterprise** 64 bits. Provavelmente é um computador gerenciado pela empresa: não desative antivírus nem políticas de segurança, e peça antes de qualquer ação de administrador.
- Intel Core 7 250U, 16 GB de RAM, **vídeo Intel integrado** (classe Iris Xe; 128 MB dedicados + memória compartilhada). O desempenho é limitado: evite cenas pesadas, use poucos personagens e objetos por padrão e deixe isso ajustável.
- Dois monitores. O jogo roda em "janela sem bordas" e eu troco de tela com Alt+Tab. No Rockstar Games Launcher está marcado "Desabilitar tecla Windows".
- Controle de PS5 (DualSense) por Bluetooth, funcionando via Steam Input.

### Jogo
- **GTA V Legacy** (não o Enhanced), comprado na **Steam**. O produto "Grand Theft Auto V Enhanced" da Steam inclui o Legacy; **só o Legacy está instalado** (Steam app 271590).
- Pasta do jogo (confirme o caminho exato; é onde fica o `GTA5.exe`): algo como `C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V`. Para achar: Steam → Biblioteca → botão direito no GTA V Legacy → Gerenciar → Procurar arquivos locais.
- Conta Rockstar vinculada. Modo história com o prólogo e a missão "Franklin e Lamar" concluídos; mapa livre.
- **BattlEye desativado** no Rockstar Games Launcher (Configurações). Uso **somente o modo história**. Nunca entrar no GTA Online com mods.

### Mods instalados na pasta do jogo (junto do GTA5.exe)
- **Script Hook V** v3889.0 / 1158.13 (dev-c.com): `ScriptHookV.dll` + `dinput8.dll` (carregador de plugins .asi).
- **Menyoo 2.0** v2.4.4 (gta5-mods.com): `Menyoo.asi` + pasta `menyooStuff`. Abre com **F8**. A interface está em português (tradução "Menyoo PT-BR" da Zona Comics, em `menyooStuff\Language`).
- Existe uma pasta sobrando, `6f8580-MenyooSP`, com os textos da extração. É inofensiva.
- Arquivos úteis do Menyoo: `menyooStuff\PedList`, `PedAnimList` (lista de animações), `PropList` (objetos), `PedSpeechList`, `TimecycModifiers`, e as pastas `Outfit` (roupas salvas) e `Spooner` (cenas salvas em XML).
- Já testei pelo Menyoo: criar NPCs (Object Spooner), sequência de tarefas (Seguir entidade, Virar para entidade, Falar com ped, Pausa), trocar meu modelo para `mp_f_freemode_01`, editar rosto e roupas, e salvar roupas.

### Gravação
- **Rockstar Editor:** F1 começa a gravar, F2 para. Os vídeos exportados vão para `%LOCALAPPDATA%\Rockstar Games\GTA V\videos\rendered`.
- A Steam também tem gravação de jogo, como alternativa.
- Formato final: vertical 9:16. Corto e edito no CapCut, no iPad.

### Ainda NÃO instalado
- **ScriptHookVDotNet**, necessário para os scripts em C#. Para versões do jogo 1.0.3258.0 ou mais novas é preciso a versão **nightly**: https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases
  - Instalação: `ScriptHookVDotNet.asi` + `ScriptHookVDotNet2.dll` / `ScriptHookVDotNet3.dll` na pasta do jogo e criar a pasta `scripts`.
  - Arquivos `.3.cs` usam a API v3. Console com **F4**; para recarregar, digite `Reload()`.
- **OpenIV**, necessário só mais tarde, para texturas personalizadas (placas, cartazes, adesivos). Fonte oficial: openiv.com. **Não é necessário para o primeiro objetivo.**
- SDK do .NET, Git, Python e outras ferramentas: **verifique o que já existe** antes de propor instalar.

## 5. O que você deve construir

- **A. Script do jogo "Diretor de Cenas":** em C#, usando a API v3 do ScriptHookVDotNet e o .NET Framework, compatível com o SHVDN.
  - Se não houver SDK do .NET, lembre que o SHVDN compila arquivos `.cs` na hora, mas com poucas bibliotecas disponíveis. Verifique quais estão disponíveis e escolha um formato e uma leitura de arquivos compatíveis (por exemplo, um leitor de JSON simples próprio, ou XML).
  - Com SDK, compile uma DLL para o .NET Framework 4.8.
- **B. Formato do arquivo de cena:** JSON (ou o formato que você justificar), com **esquema**, **exemplos comentados** e **validação** com mensagens de erro claras em português.
- **C. Catálogos:** usados para transformar meus pedidos em cenas. Elenco/personagens, figurinos, peças de roupa, locais, animações e cenários testados, objetos (props), veículos, climas e horários. Tudo com nomes e descrições em português.
- **D. Ferramentas no Windows (PowerShell):** validar cena, instalar/atualizar o script no jogo, compilar e ler os logs.
- **E. `AGENTS.md` na raiz do projeto:** com as regras e o fluxo para futuras conversas, para que qualquer conversa nova do Codex saiba transformar meu pedido em cena.

### Requisitos do script do jogo

**Teclas**
- Configuráveis em arquivo `.ini`.
- Os padrões não podem conflitar com: F1/F2 (Rockstar Editor), F4 (console do SHVDN), F8 (Menyoo), F12 (captura da Steam), Shift+Tab (sobreposição da Steam) e Alt+Tab.
- Funções mínimas:
  - **Montar cena:** prepara tudo, parado.
  - **Ação:** começa as ações e as falas.
  - **Reiniciar tomada:** volta todos às posições iniciais.
  - **Limpar:** remove tudo que o script criou.
  - **Próxima / anterior cena.**

**Leitura das cenas**
- Ler as cenas de uma pasta configurável **fora de Program Files** (por exemplo, `%USERPROFILE%\GTA_Diretor\cenas`). Assim, publicar uma cena não exige administrador; só instalar ou atualizar o script na pasta do jogo pode exigir.

**Ambiente**
- Posição/teleporte, hora (congelada), clima (fixo).
- Densidade de trânsito e de pedestres (inclusive zero), limpar a área, bloquear polícia e eventos aleatórios.

**Jogador**
- Pode ficar invisível e parado perto da cena, porque o Rockstar Editor grava o que está perto do jogador.
- Ou pode ser um dos atores, controlado por mim com o controle.

**Personagens**
- Qualquer modelo do jogo.
- Para o modelo do GTA Online (`mp_f_freemode_01` / `mp_m_freemode_01`): rosto (pais e mistura, traços, sobrancelha, maquiagem, barba), cabelo e cor, roupas (componentes e acessórios).
- Estilo de andar (movement clipset) e expressão facial.
- Importar roupas salvas pelo Menyoo (`menyooStuff\Outfit`) e/ou **capturar o personagem atual do jogador com uma tecla** e salvar no catálogo de elenco com um nome.

**Objetos**
- Criar objetos e prendê-los em ossos do personagem (por exemplo, celular ou copo na mão direita), com posição e rotação ajustáveis.

**Veículos**
- Modelo, cores, placa, motorista e passageiros, estacionado ou em movimento.

**Ações por personagem** (em sequência e/ou com tempo marcado)
- Andar ou correr até um ponto ou uma pessoa, parar.
- Virar para alguém, olhar.
- Animação (dicionário + clipe), cenário (ex.: `WORLD_HUMAN_STAND_MOBILE`, mexendo no celular).
- Entrar e sair de veículo, dirigir até um ponto.
- Esperar, falar.

**Falas**
- Cada fala tem personagem, texto e duração.
- Durante a fala, a **boca do personagem deve mexer** (por exemplo, a animação facial `mic_chatter` do dicionário `mp_facial`, a verificar), com um gesto opcional. O outro personagem olha para quem fala.
- Gerar também um arquivo de **legendas/tempos (.srt)** da cena, para eu alinhar as vozes no CapCut.

**Câmeras** (opcional por cena)
- Planos com posição, rotação e FOV; seguir ou apontar para um personagem; cortes com tempo.
- Esconder HUD e radar.
- Guia opcional de enquadramento **9:16** (liga/desliga).
- Modo "sem câmera", para quando eu usar o Rockstar Editor.

**Marcar local**
- Uma tecla que salva a posição e a rotação atuais (do jogador ou da câmera) com um nome no catálogo de locais. É assim que vamos catalogar os lugares.

**Log e mensagens**
- Log detalhado em arquivo (cada etapa, erros, modelos e animações que falharam), que você possa ler para diagnosticar.
- Mensagens curtas na tela, que possam ser desligadas.

**Robustez**
- Carregar modelos e animações com tempo limite; nunca travar o jogo; liberar memória; limpar tudo ao reiniciar.
- Poucas entidades por padrão, por causa do vídeo integrado.

### Catálogos e "linguagem natural"

- **Roupas:** quando eu pedir "mulher de vestido vermelho", você precisa saber qual peça do jogo é essa. Monte um catálogo de peças em português de forma **incremental**:
  1. roupas que eu montar no Menyoo e salvar com nome;
  2. uma ferramenta dentro do jogo para eu navegar pelas peças e salvar a atual com uma descrição;
  3. referências públicas confiáveis, sempre marcadas como "não testado" até eu confirmar.
- **Locais, animações e cenários:** mesma lógica. Os locais são marcados por mim no jogo. Só use nomes de animação e cenário que existam (confira em `menyooStuff\PedAnimList` ou em uma referência confiável de natives) e marque os que já foram testados.
- **Se faltar algo no catálogo, pergunte ou proponha opções. Não invente IDs, nomes de animação nem coordenadas.**

## 6. Etapas de entrega (uma de cada vez, com teste meu)

Em cada etapa: diga o que vai fazer, faça, diga exatamente o que eu devo fazer no jogo e o que devo ver, e **espere o meu retorno**.

**Prioridade agora: Etapas 0 a 3**, que entregam o teste do diálogo entre um homem e uma mulher. Depois da Etapa 3, pare e me pergunte se quero continuar.

0. **Verificar o ambiente:** Windows, pasta do jogo, versão do GTA5.exe, ferramentas disponíveis. Listar o que falta instalar, com links oficiais.
1. **"Olá mundo":** o script carrega, mostra um aviso na tela e escreve no log.
2. **Cena mínima:** dois personagens do jogo num local, com hora e clima fixos. Montar e Limpar funcionando.
3. **Ações e falas:** um anda até o outro, os dois se viram e conversam com a boca mexendo. Gerar o `.srt`.
4. **Elenco próprio:** personagens do GTA Online com rosto e roupas (captura e/ou importação do Menyoo) e estilo de andar.
5. **Câmeras,** HUD oculto e guia 9:16.
6. **Objetos na mão e figurantes:** por exemplo, um personagem segurando um celular ou um copo, e pessoas ao fundo com cenários do jogo (conversando, mexendo no celular).
7. **Veículos:** chegar de carro, estacionar, descer, entrar.
8. **Fluxo completo:** pedido em português → arquivo de cena → validar → publicar. E o `AGENTS.md`.
9. **Depois (exige OpenIV):** texturas personalizadas, como placas, cartazes e adesivos. Explique antes e me guie na instalação.

## 7. Exemplo de pedido que vou fazer no futuro

> "Cena teste_dialogo: fim de tarde, céu limpo, calçada tranquila de um bairro residencial, sem trânsito. Um homem (personagem comum do jogo) está parado perto de um carro estacionado. Uma mulher chega andando, para na frente dele e os dois conversam:
> Mulher: 'Oi, você viu meu irmão por aqui?' (3 s)
> Homem: 'Vi sim, ele passou agora há pouco.' (3 s)
> Mulher: 'Valeu!' (1 s) — e vai embora andando.
> Câmera vertical: plano médio dos dois e depois close em quem estiver falando."

O que você deve fazer com um pedido assim:
1. Localizar tudo nos catálogos, ou me perguntar o que faltar.
2. Gerar o arquivo da cena, validar e publicar.
3. Me dizer, por exemplo: "No jogo: aperte [Montar], confira, aperte F1, aperte [Ação], espere X segundos, aperte F2."

## 8. Regras e limites

- **Só modo história.** Nunca GTA Online com mods. Não reativar o BattlEye.
- **Não alterar nem apagar arquivos originais do jogo;** só adicionar. Faça backup antes de mudar qualquer arquivo que já exista (por exemplo, os do Menyoo).
- **Baixar só de fontes oficiais:**
  - Script Hook V: dev-c.com
  - ScriptHookVDotNet: github.com/scripthookvdotnet
  - Menyoo: gta5-mods.com ou github.com/itsjustcurtis/MenyooSP
  - OpenIV: openiv.com

  Sites com nomes parecidos podem trazer vírus.
- **Não desative antivírus, firewall nem políticas do Windows.** Se algo for bloqueado, pare e me explique.
- **Diretrizes de mods da Rockstar (setembro de 2026):** uso não comercial dos mods; não usar imagem ou voz de pessoas reais sem permissão por escrito; não misturar personagens ou assets de outros jogos da Rockstar.
- **Meu projeto antigo no Blender** (pasta Tiktok_Videos, no Mac) não deve ser alterado. O roteiro oficial da série "A Caloteira do Oeste" está lá. Se eu trouxer cenas dele, não altere falas, nomes, valores (a dívida é R$ 1.000) nem a ordem; adaptações ficam separadas.
- **Honestidade:** separe "compilou/rodou" de "funcionou no jogo" (quem confirma sou eu), e separe fatos verificados de suposições.

## 9. Sua primeira resposta

1. Um resumo curto do que você entendeu, com no máximo 10 linhas.
2. As verificações seguras do ambiente que você vai fazer. Me diga os comandos antes de rodar.
3. A lista do que precisa ser instalado, com links oficiais e o motivo de cada item. Aguarde o meu "ok".
4. A proposta de estrutura de pastas do projeto.
5. Depois do meu "ok": a Etapa 1.
