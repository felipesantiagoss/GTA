# Interpretação por período — v1

`interpretacao` é opcional no JSON com `encenacao=serie_v1`, e proibida nos outros perfis. A lista tem de0 a64 instruções, sem alterar o texto nem o tipo dos blocos do roteiro. Cada instrução exige `personagem`, `inicioSegundos` e `duracaoSegundos`, com início0–180s, duração0,1–180s, resolução exata de milissegundos e fim até180s. A duração total inclui o último fim de interpretação. Períodos do mesmo ator não se sobrepõem; expressão e olhar simultâneos são declarados no mesmo objeto.

```json
"interpretacao": [
  {
    "personagem": "DONA_IRACI",
    "inicioSegundos": 0,
    "duracaoSegundos": 10,
    "expressao": "chateado",
    "olhar": {"tipo": "desvio"}
  }
]
```

Exemplo de formato; não representa uma tomada visualmente aprovada. A personagem precisa existir e estar pronta no catálogo; o local da cena precisa de marca real.

`expressao` aceita `normal`, `chateado`, `feliz`, `machucado`, `comraiva`, `estressado` e `convencido`. Campo ausente mantém a expressão neutra padrão do Diretor. Nenhum nome livre de animação é aceito. É obrigatório declarar uma expressão ou um olhar específico; apenas `olhar.tipo=padrao` não produz efeito e é recusado.

`olhar` exige `tipo`. Tipos e seus campos:

| Tipo | Efeito e campos |
|---|---|
| `padrao` | Libera o olhar automático; sem campos extras. |
| `ator` | Exige `alvo`, outro código do elenco. Usa a entidade real. |
| `chao` | Obtém altura real do chão à frente do ator. Aguarda colisão antes da tomada; não inventa altitude. |
| `horizonte` | Ponto10m adiante da cabeça, na direção atual do ator. |
| `desvio` | Ponto relativo à cabeça:1,5m à direita e4m à frente, na direção atual do ator. |
| `marca` | Exige `destino` com `direita`/`frente` entre−10 e10m e `altura` opcional entre−2 e2m. O ponto deriva da âncora real e do eixo da cena. |

O helper usa `Task.LookAt` e `ClearLookAt`, sem girar o corpo nem limpar as tarefas de caminhada, pose ou gesto. Limites de cabeça do modelo continuam físicos; um alvo atrás do corpo pode não ser alcançável. `horizonte` permite manter alguém de costas durante uma fala; orientação corporal continua responsabilidade das marcas e ações declaradas.

Um olhar específico prevalece sobre o olhar automático de ouvinte. Enquanto o ator fala, o helper não inicia animação facial: a boca pertence ao Diretor. Ao terminar a fala, a expressão ativa é reaplicada. Ao terminar um período, o olhar é limpo uma vez e a expressão volta ao neutro quando o ator não estiver falando. Reiniciar tomada aplica a interpretação inicial após restaurar as marcas.

API C#5, sem outra classe `Script`:

- `ActorSpec`: `Ator`, `Expressao`, `OlharTipo`, `Alvo`, `Deslocamento`, `InicioMs`, `DuracaoMs`.
- `Prepare(ownedActors, specs, anchor, right, forward)`, `PollLoading()`, `IsReady`, `ApplyIdle()` e `Update(elapsedMs, actorsSpeaking)`.
- `HasExplicitLook(actorId, elapsedMs)`, `HasExpression(actorId, elapsedMs)`, `InvalidateFace(actorId)` e `InvalidateLook(actorId)`. Os invalidadores só marcam a reaplicação para o próximo update; não iniciam tarefas nem faciais.
- `ReleaseEndedLooks(elapsedMs)` deve anteceder o novo `StartSegment`, para não cancelar um olhar automático recém-aplicado.
- `Reset()`, `Cleanup()` e `Dispose()`. O helper guarda handle/modelo/endereço e não atua no jogador ou em identidades substituídas. Libera apenas os dicionários que precisou solicitar; não remove os que já estavam carregados.
- `ValidateSpecs()` e `CheckSources()` são pré-validações de dados/arquivos, sem chamadas do jogo. O loader as executa antes de limpar a cena anterior, somente para lista não vazia.

Os sete candidatos possuem14 pares de homem/mulher em `catalogos/interpretacao_serie.json`. Origem: `menyooStuff/PedAnimList.txt` instalada, SHA256 `2C537060BD97FCF0DACA407DB1436A5D54E4B8DD5274DBC9EF0D8466EF6F21E4`. As linhas59707/59736/59753/59755/59759/59762/59768 e59793/59831/59839/59841/59845/59848/59854 foram lidas e os pares conferidos. Projeto do autor: [MenyooSP](https://github.com/itsjustcurtis/MenyooSP).

A tabela C# e a validação Python usam pares fixos; o JSON do catálogo documenta essa evidência e não autoriza substituir clipes. Mudança da lista instalada exige nova pesquisa/verificação de nomes, linhas e hash. Todos permanecem `não testado`: nomes como `mood_sulk_1` ou `mood_smug_1` não comprovam a leitura dramática nem lágrimas. Não há alteração de DNA, máscara, overlays, cabelo, roupa ou olhos. `machucado` aqui é expressão facial; não substitui automaticamente o gesto de corpo homônimo do roteiro.

Compilação e testes de parser comprovam contrato e origem. Compatibilidade do modelo, intensidade facial, olhar e continuidade de boca precisam de observação no GTA. Nenhum DLL/harness foi executado para simular essa confirmação.
