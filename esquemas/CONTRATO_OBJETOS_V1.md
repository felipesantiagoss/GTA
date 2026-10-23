# Recursos de cena: backend técnico v1

`src/ObjetosDiretor.cs` é um helper sem `using` e sem outra classe Script, para concatenação futura à fonte principal. Esta entrega prepara criação/restauração de recursos próprios; não integra ações de condução nem altera o contrato de cenas JSON existente. A amostra `producao/testes/teste_objetos_recursos.json` é um **descritor técnico**, não uma cena publicável pelo validador atual, e fica fora das pastas semeadas no runtime.

## API e ciclo

```text
ObjetosDiretor(Action<string,string> log)
RequestProp(codigo, modelo, Vector3 posicao, Vector3 rotacao, bool congelado)
RequestVehicle(codigo, modelo, Vector3 posicao, float heading, bool congelado, bool colocarNoChao=true)
Update()
IsReady / PendingCount / Count
GetProp(codigo) / GetVehicle(codigo)
AttachPhone(codigo, Ped ator)
AttachPhone(codigo, Ped ator, Vector3 offset, Vector3 rotacao)
HidePhone(codigo)
Reset()
Cleanup() / Dispose()
```

No máximo oito recursos. Códigos únicos: `[A-Z][A-Z0-9_]{0,63}`; modelos: `[a-z][a-z0-9_]{0,63}`. A integração deve resolver somente modelos conferidos no catálogo/fonte, sem hashes inventados. O helper verifica `IsInCdImage` e `IsProp`/`IsVehicle` no jogo. Posição/rotação são finitas e dadas pelo integrador a partir da marca real; o descritor técnico usa apenas deslocamentos relativos.

`Request*` registra a necessidade. `Update` chama `Model.Request()` sem timeout bloqueante, aguarda pelo menos um frame e verifica `IsLoaded`; cria no máximo um recurso por Tick. Timeout de cinco segundos para o modelo. Veículos que pedem chão aguardam colisão e `Vehicle.PlaceOnGround()` entre frames, também com cinco segundos de limite. Seu estado inicial registra a posição/rotação real depois de assentar. A câmera/F7 aguardam `IsReady`. Nenhuma chamada a Script.Wait/Task.Wait, condução, criação de ped, adoção de recurso do mapa ou alteração global de tráfego.

Prop/veículo próprio fica persistente e invencível. Telefone fica oculto, congelado e sem colisão até AttachPhone. `Reset` interrompe apenas o movimento físico desses recursos, desanexa, restaura posições/rotações/visibilidade/colisão e oculta o telefone; o main deve interromper as tarefas dos atores primeiro. Não reiniciar durante preparo. `Cleanup` remove apenas entidades criadas por este helper, verificando handle, tipo, modelo e endereço de memória. Delete zera o wrapper: se o handle original continuar existente, recuperar o wrapper e conservar o recurso para tentar de novo. Falha é explícita, sem limpeza ampla.

Veículo com qualquer ocupante não é reposicionado nem apagado. O main deve retirar os **atores que ele próprio criou** antes de Reset/Cleanup; jamais remover Franklin ou NPC de missão para satisfazer essa condição. Cleanup incompleto preserva referência/handle e informa o código pendente. Não confundir esse estado com limpeza completa. Modelos são liberados só quando não há entidade própria pendente. Cleanup pode ser chamado novamente; Dispose encerra a instância só depois da limpeza bem sucedida.

## Telefone e evidências

Modelo `prop_npc_phone_02`: Menyoo instalado, `menyooStuff/PropList.txt:12614`. `AttachPhone` exige esse modelo e ator existente, usa `Bone.SkelRightHand` e `Entity.AttachTo(EntityBone,...)` com colisão desligada, transformação relativa e sem marcar como dispensável na desanexação. Ajuste até 0,5 m da mão. Overload simples começa com offset/rotação zero: **referência técnica sem calibração visual**, não promessa de pega correta. A animação/mão do personagem determina a calibração final.

Pares de celular disponíveis no Menyoo: `cellphone@female/cellphone_text_in` linha55782, `cellphone_text_read_base` linha55788, `cellphone_text_out` linha55789. Eles não são executados por este helper; o motor de ações precisa carregar/temporizar a animação e impedir sobreposição incompatível. O roteiro do piloto exige mensagem e objeto real, portanto um gesto de conversa sozinho não substitui o uso do celular.

APIs conferidas por documentação XML/reflection da DLL oficial instalada `ScriptHookVDotNet-v3.7.0-nightly.191`: Model.Request/IsLoaded, Prop.CreateNoOffset, Vehicle.Create/PlaceOnGround, Entity.AttachTo/Detach/Delete/FromHandle, EntityBone.IsValid e Ped.Bones[Bone.SkelRightHand]. Compilação C#5 com avisos como erros verifica as assinaturas; nenhuma DLL de teste é executada.

`Bati`, `Sanchez` e `Faggio` constam em VehicleHash da mesma API. Não há VehicleList.txt no Menyoo instalado: são opções técnicas cuja disponibilidade é verificada em runtime, **não figurino/veículo escolhido nem resultado visual aprovado da série**. O descritor usa `faggio` como candidato de teste parado. Moto chegar, parar, desmontar e reagir precisa de extensão e teste próprios; este backend não afirma executar essa sequência.

## Descritor técnico

Raiz `versao:1`, `tipo:"teste_recursos"`, `id:"teste_objetos_recursos"`, `estado:"não integrado"`, `local:"TESTE_AMBIENTE"`, lista `recursos` e lista `verificacoes`. Cada recurso declara código/tipo/modelo, deslocamento relativo à marca real e estado visual. Nenhum XYZ global nem novo visual de personagem. Não incluir em fila/catálogo de produção nem publicar como cena até schema, parser, main e teste visual serem implementados.
