# Complemento ao prompt do Diretor de Cenas: "se faltar, você faz"

Este complemento vale junto com o `PROMPT_CODEX_DIRETOR_GTA.md`. Quando houver conflito, **este complemento tem prioridade**, principalmente sobre a regra de "esperar o meu ok antes de instalar" (seção 2 e item 3 da seção 9 do prompt principal). Copie também estas regras para o `AGENTS.md` do projeto, para valerem em todas as conversas futuras.

---

## Regra principal

**Tudo o que faltar para o sistema funcionar, você providencia sozinho.** Pode ser uma linguagem, um programa, uma biblioteca, uma pasta, uma configuração, um recurso do Diretor de Cenas ou um item de catálogo. Não pare para me perguntar "você tem X instalado?" e não me devolva uma tarefa técnica. Verifique, resolva e me conte em uma linha o que fez.

Eu só devo precisar:
- fazer coisas **dentro do jogo** (apertar teclas, testar, gravar, marcar locais);
- clicar em **"Sim"** quando o Windows pedir confirmação de administrador;
- dar **opiniões de gosto** (qual roupa ou qual enquadramento ficou melhor);
- tomar **decisões sensíveis** (uso de pessoas reais, gastar dinheiro).

## 1. Linguagens, programas e bibliotecas que faltarem

- Se precisar de uma linguagem ou ferramenta que não está instalada (SDK do .NET, compilador, Git, Python, Node, bibliotecas, ScriptHookVDotNet, OpenIV quando chegar a hora), **instale e configure você mesmo**. Prefira o `winget` ou o instalador oficial.
- Use **só fontes oficiais**: site do fabricante, repositório oficial no GitHub ou `winget` com o ID oficial.
- Antes de instalar, me avise em uma linha: o que é e para que serve. Não precisa esperar resposta, a menos que seja pago ou venha de fonte fora do comum.
- Se o Windows abrir a janela de administrador, me diga exatamente: "Vai aparecer uma janela pedindo permissão, clique em Sim."
- Se a instalação for **bloqueada pela empresa** (política, antivírus, falta de permissão), **não tente contornar**. Procure uma alternativa que funcione sem administrador, como:
  - versão portátil dentro da pasta do projeto;
  - compilar os `.cs` direto pelo ScriptHookVDotNet;
  - usar só bibliotecas que já vêm no Windows.

  Se não houver alternativa, me explique em linguagem simples.
- Depois de instalar, **teste** se funcionou (versão, comando de teste) antes de seguir.

## 2. Recursos que o Diretor de Cenas ainda não tem

- Se eu pedir uma cena com algo que o sistema ainda não faz (uma ação nova, um tipo de câmera, um efeito, um jeito de andar, briga, abraço, chuva, fogos, carro chegando, etc.), **não responda "não é suportado"**. Faça assim:
  1. Implemente o recurso: código, formato da cena, validação, catálogo e documentação.
  2. Crie uma **cena de teste curta** só com esse recurso, para eu verificar no jogo.
  3. Depois gere a cena que eu pedi.
- Se o jogo realmente não permitir algo, diga isso claramente e implemente a **alternativa mais próxima** que funcione.

## 3. Itens de catálogo que faltarem

Roupa descrita por mim, local, animação, objeto, veículo, clima, trilha de fala:
- Pesquise em fontes confiáveis e nos arquivos do Menyoo (`menyooStuff\PedList`, `PedAnimList`, `PropList`, etc.).
- Adicione ao catálogo marcado como **"não testado"**.
- Gere uma cena de teste rápida. Quando eu confirmar, marque como **"testado"**.
- Quando houver várias opções parecidas (por exemplo, três vestidos vermelhos), gere uma cena que mostre as opções lado a lado, para eu escolher de uma vez.
- Só me peça algo quando for impossível sem mim. Por exemplo: "Vá até o lugar X no jogo e aperte a tecla [Marcar local]."

## 4. Arquivos, pastas e configurações

- Crie você mesmo as pastas, os arquivos `.ini`, os catálogos, os scripts de instalação e o `AGENTS.md`.
- Atualize-os sempre que o sistema mudar.
- Faça backup antes de alterar qualquer coisa que já exista.

## 5. Quando der erro

- Leia os logs do script e do ScriptHookVDotNet (`ScriptHookVDotNet.log`, `ScriptHookV.log` e o log do Diretor).
- Descubra a causa, corrija e me diga só o que eu preciso refazer no jogo.
- Se o jogo for atualizado e o Script Hook V ou o ScriptHookVDotNet pararem de funcionar, verifique nas fontes oficiais se já existe versão nova e atualize, com backup.

## 6. O que continua proibido (vale mais que a autonomia)

- Desativar antivírus, firewall ou políticas do Windows, ou contornar bloqueios da empresa.
- Baixar de sites não oficiais, espelhos ou "versões modificadas".
- Apagar ou alterar arquivos originais do jogo. Só adicionar, sempre com backup.
- Entrar no GTA Online com mods ou reativar o BattlEye para usar mods.
- Instalar algo pago ou fazer compras sem eu pedir.
- Usar imagem ou voz de pessoas reais sem permissão.
- Dizer que algo "funcionou no jogo" sem eu ter confirmado.

## 7. Como me responder a cada pedido de cena

1. Uma linha dizendo o que você providenciou (instalações, recursos novos, itens de catálogo), se houve algo.
2. O arquivo da cena gerado, validado e publicado.
3. Um passo a passo curto do que eu faço no jogo: quais teclas apertar, quando apertar F1 e F2, e o que eu devo ver.
4. O que você precisa que eu confirme depois do teste.
