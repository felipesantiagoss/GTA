// Backend independente. Não cria elenco, não anima boca e não executa combate da IA.
// Catálogos comprovam nomes; compatibilidade visual, duração/offsets ainda exigem ensaio no GTA.
public sealed class AcoesSerieDiretor : System.IDisposable
{
    public sealed class ActorAction
    {
        public string Tipo, Ator, Dicionario, Clipe, Alvo, GrupoPareado, ClipSet;
        public string OrigemArquivo, OrigemSha256;
        public string ClipeDurantePercurso, PercursoDicionario, PercursoClipe, PercursoOrigemArquivo, PercursoOrigemSha256;
        public int PercursoOrigemLinha;
        public int InicioMs, DuracaoMs, OrigemLinha;
        public GTA.Math.Vector3 Deslocamento, Rotacao;
        public float Velocidade;
        public bool Loop, CorpoInteiro, ManterPoseFinal, SobrePoseSentada, PreservarDuranteFala;
        public string Recurso;
        public ActorAction() { Velocidade = 1f; CorpoInteiro = true; }
    }

    private sealed class ActorState
    {
        internal string Id;
        internal GTA.Ped Ped;
        internal int Handle, Model;
        internal System.IntPtr Address;
        internal GTA.Math.Vector3 Position;
        internal float Heading;
        internal bool Frozen, MovementSet, MarkCaptured;
    }
    private sealed class ActionState
    {
        internal ActorAction Spec;
        internal ActorState Actor, Target;
        internal GTA.Math.Vector3 Destination;
        internal string PhoneCode;
        internal float ClipDuration;
        internal bool Started, Finished, PhasePendingLogged;
        internal int StartedAt;
        internal long NextWalkLogMs;
        internal float MaxPhase;
        internal bool PercursoStarted, PercursoPhasePendingLogged;
        internal int PercursoStartedAt;
    }
    private sealed class SourceCheck
    {
        internal string Path, Hash;
        internal readonly System.Collections.Generic.Dictionary<int, string> Lines =
            new System.Collections.Generic.Dictionary<int, string>();
    }

    private readonly System.Action<string, string> log;
    private readonly ObjetosDiretor resources;
    private readonly System.Collections.Generic.Dictionary<string, ActorState> actors =
        new System.Collections.Generic.Dictionary<string, ActorState>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.List<ActionState> actions = new System.Collections.Generic.List<ActionState>();
    private readonly System.Collections.Generic.Dictionary<string, int> taskRevisions = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, bool> dictionaries =
        new System.Collections.Generic.Dictionary<string, bool>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, bool> clipSets =
        new System.Collections.Generic.Dictionary<string, bool>(System.StringComparer.Ordinal);
    private System.Diagnostics.Stopwatch loading;
    private PosesSerieDiretor seatedPoses;
    private VeiculosSerieDiretor ownVehicles;
    private GTA.Math.Vector3 anchor, right, forward;
    private bool prepared, ready, disposed, resetDone, failed, dismissalHandoffTelemetry;
    private bool pocket98AttachmentStart;
    private int previousElapsed = -1, endMs;
    private const int TimeoutMs = 5000, ActionLimit = 64, ActorLimit = 8;

    public AcoesSerieDiretor(System.Action<string, string> log) { this.log = log; resources = new ObjetosDiretor(log); }
    public void EnableDismissalHandoffTelemetry(string sceneId)
    {
        Active();
        if (prepared || sceneId != "EP13_C02") throw Error("telemetria de demissao somente EP13_C02 antes Prepare");
        dismissalHandoffTelemetry = true;
    }
    public void EnablePocket98AttachmentStart(string sceneId, string sourceSHA)
    {
        Active();
        if(prepared||pocket98AttachmentStart||sceneId!="teste_fila_98"||sourceSHA!=PapelBolso98Diretor.ExactSourceHash)
            throw Error("attachment-start flag only exact technical98/sourceE1 before Prepare");
        pocket98AttachmentStart=true;
        Log("BOLSO98_ATTACHMENT_START_OPTIN", "Scene=teste_fila_98; SourceSHA256="+sourceSHA+"; PreparePending=True; OnlyTwoClips=True; HypothesisOnly=True; ProductionApproval=False");
    }
    private static bool IsPocket98AttachmentStartClip(ActorAction spec)
    {
        if(spec==null||spec.Ator!="djanira_rica"||spec.Tipo!="clipe"||spec.Loop||spec.CorpoInteiro||spec.ManterPoseFinal||
            spec.SobrePoseSentada||spec.PreservarDuranteFala||!System.String.IsNullOrEmpty(spec.GrupoPareado)||
            !System.String.IsNullOrEmpty(spec.Alvo)||!System.String.IsNullOrEmpty(spec.ClipeDurantePercurso)||
            spec.Deslocamento!=GTA.Math.Vector3.Zero||spec.Rotacao!=GTA.Math.Vector3.Zero) return false;
        return spec.Recurso=="retirar_bolso_f_trial97"&&spec.Dicionario==PapelBolso98Diretor.PocketDict&&
            spec.Clipe==PapelBolso98Diretor.PocketClip&&spec.InicioMs==0&&spec.DuracaoMs==5000||
            spec.Recurso=="entrega_a"&&spec.Dicionario=="mp_common"&&spec.Clipe=="givetake1_a"&&
            spec.InicioMs==5000&&spec.DuracaoMs==3500;
    }
    private void RequirePocket98AttachmentStartContract()
    {
        ActorState actor;
        if(!pocket98AttachmentStart||actors.Count!=1||!actors.TryGetValue("djanira_rica",out actor)||actions.Count!=2||endMs!=8500||
            actor.Model!=unchecked((int)GTA.StringHash.AtStringHash("mp_f_freemode_01"))||
            !IsPocket98AttachmentStartClip(actions[0].Spec)||!IsPocket98AttachmentStartClip(actions[1].Spec)||
            actions[0].Spec.InicioMs!=0||actions[1].Spec.InicioMs!=5000)
            throw Error("attachment-start flag requires sole registered Rich and exact two upper-body technical98 clips");
    }
    public bool IsReady { get { return ready && !failed && !disposed; } }
    public bool IsFinished
    {
        get { if (!IsReady || !resetDone || previousElapsed < endMs) return false;
            foreach (ActionState state in actions) if (!state.Finished) return false; return true; }
    }
    public int TotalDurationMs { get { return endMs; } }
    // Consulta antes de StartSegment: a animação física declarada ocupa o corpo,
    // enquanto o chamador continua a boca. Não libera qualquer clipe arbitrário.
    public bool ControlsBody(string actorId, int elapsedMs)
    {
        if (!IsReady || !resetDone || System.String.IsNullOrEmpty(actorId)) return false;
        foreach (ActionState state in actions)
            if (state.Spec.PreservarDuranteFala && !state.Finished &&
                System.String.Equals(state.Spec.Ator, actorId, System.StringComparison.OrdinalIgnoreCase) &&
                elapsedMs >= state.Spec.InicioMs && elapsedMs < state.Spec.InicioMs + state.Spec.DuracaoMs)
                return true;
        return false;
    }
    public static bool CanControlSpeechBody(string dictionary, string clip)
    {
        return (dictionary == "cellphone@female" || dictionary == "cellphone@") && clip == "cellphone_call_listen_base" ||
            dictionary == "anim@heists@box_carry@" && clip == "idle" ||
            dictionary == "anim@move_f@waitress" && clip == "idle" ||
            dictionary == "missmic3" && clip == "newspaper_idle_loop_dave" ||
            dictionary == "mp_ped_interaction" && (clip == "hugs_guy_a" || clip == "hugs_guy_b") ||
            dictionary == "mp_common" && (clip == "givetake1_a" || clip == "givetake1_b") ||
            dictionary == "anim@mp_player_intcelebrationmale@cut_throat" && clip == "cut_throat";
    }
    public static bool CanComposeWalkClip(string resource, string dictionary, string clip)
    {
        return resource == "carregar_bandeja" && dictionary == "anim@heists@box_carry@" && clip == "idle" ||
            resource == "servir_champanhe_autor" && dictionary == "anim@move_f@waitress" && clip == "idle" ||
            resource == "ligacao_f" && dictionary == "cellphone@female" && clip == "cellphone_call_listen_base" ||
            resource == "ligacao_m" && dictionary == "cellphone@" && clip == "cellphone_call_listen_base" ||
            resource == "carregar_sacola_autor" && dictionary == "move_weapon@jerrycan@generic" && clip == "idle";
    }
    public void BindPoses(PosesSerieDiretor poses)
    { Active(); if (previousElapsed >= 0) throw Error("BindPoses só antes da ação"); seatedPoses = poses; }
    public void BindVehicles(VeiculosSerieDiretor vehicles)
    { Active(); if (previousElapsed >= 0) throw Error("BindVehicles só antes da ação"); ownVehicles = vehicles; }
    public int GetTaskRevision(string actor)
    { int value; return taskRevisions.TryGetValue(actor, out value) ? value : 0; }
    private void TaskChanged(string actor)
    { taskRevisions[actor] = GetTaskRevision(actor) + 1; }
    public GTA.Entity[] OwnedEntities
    {
        get
        {
            var list = new System.Collections.Generic.List<GTA.Entity>();
            if (!resources.IsReady) return list.ToArray();
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (ActionState state in actions)
                if (!System.String.IsNullOrEmpty(state.PhoneCode) && seen.Add(state.PhoneCode)) list.Add(resources.GetProp(state.PhoneCode));
            return list.ToArray();
        }
    }
    public int[] OwnedHandles
    {
        get { var list = new System.Collections.Generic.List<int>(); foreach (GTA.Entity entity in OwnedEntities) list.Add(entity.Handle); return list.ToArray(); }
    }

    // O chamador fornece exclusivamente atores próprios, já assentados em marcas reais.
    // Deslocamento: X lateral, Y frontal, Z vertical, relativo à marca da cena.
    // Clipe não pareado começa na posição atual. GrupoPareado usa referência comum declarada.
    public void Prepare(System.Collections.Generic.Dictionary<string, GTA.Ped> ownedActors,
        System.Collections.Generic.IEnumerable<ActorAction> specifications, GTA.Math.Vector3 realAnchor,
        GTA.Math.Vector3 lateral, GTA.Math.Vector3 frontal)
    {
        Active();
        if (prepared || resources.Count != 0) throw Error("limpe as ações anteriores antes de preparar outra cena");
        if (ownedActors == null || specifications == null || ownedActors.Count == 0 || ownedActors.Count > ActorLimit)
            throw Error("forneça de um a oito atores próprios e uma lista de ações");
        Vector(realAnchor); Vector(lateral); Vector(frontal);
        float lateralLength = DistanceXY(lateral, GTA.Math.Vector3.Zero), frontalLength = DistanceXY(frontal, GTA.Math.Vector3.Zero);
        if (lateralLength < 0.01f || frontalLength < 0.01f) throw Error("os eixos da marca não estão disponíveis");
        right = new GTA.Math.Vector3(lateral.X / lateralLength, lateral.Y / lateralLength, 0f);
        forward = new GTA.Math.Vector3(frontal.X / frontalLength, frontal.Y / frontalLength, 0f);
        if (System.Math.Abs(right.X * forward.X + right.Y * forward.Y) > 0.05f) throw Error("os eixos precisam ser perpendiculares");
        anchor = realAnchor; prepared = true; ready = false; failed = false;
        var handles = new System.Collections.Generic.HashSet<int>();
        var phoneActors = new System.Collections.Generic.Dictionary<int, string>();
        try
        {
            foreach (var item in ownedActors)
            {
                Identifier(item.Key, "ator"); RequirePed(item.Value);
                if (!item.Value.IsPersistent || !handles.Add(item.Value.Handle)) throw Error("o elenco precisa conter atores persistentes distintos");
                actors.Add(item.Key, new ActorState { Id = item.Key, Ped = item.Value, Handle = item.Value.Handle,
                    Model = item.Value.Model.Hash, Address = item.Value.MemoryAddress, Position = item.Value.Position,
                    Heading = item.Value.Heading, Frozen = item.Value.IsPositionFrozen });
            }
            foreach (ActorAction incoming in specifications)
            {
                if (incoming == null || actions.Count >= ActionLimit) throw Error("limite de64 ações por tomada atingido ou ação nula");
                ActorAction spec = Copy(incoming);
                ValidateSpec(spec);
                ActorState actor;
                if (!actors.TryGetValue(spec.Ator, out actor)) throw Error("a ação referencia ator ausente: " + spec.Ator);
                var state = new ActionState { Spec = spec, Actor = actor, Destination = ToWorld(spec.Deslocamento) };
                if (!System.String.IsNullOrEmpty(spec.Alvo))
                {
                    if (!actors.TryGetValue(spec.Alvo, out state.Target) || state.Target.Handle == actor.Handle)
                        throw Error("o alvo precisa ser outro ator próprio");
                }
                if (spec.Tipo == "celular")
                {
                    if (!phoneActors.TryGetValue(actor.Handle, out state.PhoneCode))
                    {
                        state.PhoneCode = "ACAO_CELULAR_" + phoneActors.Count.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
                        phoneActors.Add(actor.Handle, state.PhoneCode);
                    }
                }
                foreach (ActionState earlier in actions)
                    if (earlier.Actor.Handle == actor.Handle && spec.InicioMs < earlier.Spec.InicioMs + earlier.Spec.DuracaoMs &&
                        earlier.Spec.InicioMs < spec.InicioMs + spec.DuracaoMs)
                        throw Error("ações sobrepostas no mesmo ator: " + spec.Ator);
                actions.Add(state); endMs = System.Math.Max(endMs, spec.InicioMs + spec.DuracaoMs);
            }
            if (actions.Count == 0) throw Error("a lista de ações está vazia");
            if(pocket98AttachmentStart)RequirePocket98AttachmentStartContract();
            ValidatePairs(); VerifySources();
            var requestedPhones = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (ActionState state in actions)
            {
                ActorAction spec = state.Spec;
                if (spec.Tipo == "clipe" || spec.Tipo == "celular")
                {
                    if (!dictionaries.ContainsKey(spec.Dicionario))
                    {
                        if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ANIM_DICT_EXIST, spec.Dicionario))
                            throw Error("o dicionário não existe neste GTA: " + spec.Dicionario);
                        bool alreadyLoaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, spec.Dicionario);
                        dictionaries.Add(spec.Dicionario, !alreadyLoaded);
                        GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, spec.Dicionario);
                    }
                }
                if (!System.String.IsNullOrEmpty(spec.ClipeDurantePercurso) && !dictionaries.ContainsKey(spec.PercursoDicionario))
                {
                    if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ANIM_DICT_EXIST, spec.PercursoDicionario))
                        throw Error("o dicionário secundário não existe neste GTA: " + spec.PercursoDicionario);
                    bool loaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, spec.PercursoDicionario);
                    dictionaries.Add(spec.PercursoDicionario, !loaded);
                    GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, spec.PercursoDicionario);
                }
                if (!System.String.IsNullOrEmpty(spec.ClipSet) && !clipSets.ContainsKey(spec.ClipSet))
                {
                    bool alreadyLoaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_SET_LOADED, spec.ClipSet);
                    clipSets.Add(spec.ClipSet, !alreadyLoaded);
                    GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_SET, spec.ClipSet);
                }
                if (!System.String.IsNullOrEmpty(state.PhoneCode) && requestedPhones.Add(state.PhoneCode))
                    resources.RequestProp(state.PhoneCode, "prop_npc_phone_02", state.Actor.Position, GTA.Math.Vector3.Zero, true);
            }
            loading = System.Diagnostics.Stopwatch.StartNew();
            Log("ACOES_SERIE_PREPARADAS", "Atores=" + actors.Count + "; ações=" + actions.Count + "; estado=teste_visual_pendente");
        }
        catch { failed = true; throw; } // Cleanup conserva handles dos recursos se a remoção falhar.
    }

    public void PollLoading()
    {
        try { PollLoadingCore(); }
        catch { failed = true; ready = false; throw; }
    }

    private void PollLoadingCore()
    {
        Active(); RequirePrepared(); if (ready) return; RequireActors(); resources.Update();
        bool loaded = resources.IsReady;
        foreach (string dictionary in dictionaries.Keys)
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, dictionary)) loaded = false;
        foreach (string set in clipSets.Keys)
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_SET_LOADED, set)) loaded = false;
        if (loaded)
        {
            foreach (ActionState state in actions)
            {
                if (!System.String.IsNullOrEmpty(state.Spec.ClipeDurantePercurso))
                {
                    float duration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, state.Spec.PercursoDicionario, state.Spec.PercursoClipe);
                    if (!Finite(duration) || duration <= 0f) throw Error("clipe secundário de percurso não disponível: " + state.Spec.PercursoDicionario + "/" + state.Spec.PercursoClipe);
                    Log("ACOES_SERIE_PERCURSO_RECURSO", "Ator=" + state.Spec.Ator + "; recurso=" + state.Spec.ClipeDurantePercurso + "; duração=" + duration.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; canal=secundário; loop=true");
                }
                if (state.Spec.Tipo != "clipe" && state.Spec.Tipo != "celular") continue;
                state.ClipDuration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, state.Spec.Dicionario, state.Spec.Clipe);
                if (!Finite(state.ClipDuration) || state.ClipDuration <= 0f) throw Error("clipe não disponível: " + state.Spec.Dicionario + "/" + state.Spec.Clipe);
                Log("ACOES_SERIE_CLIP_DURACAO", "Ator=" + state.Spec.Ator + "; dict=" + state.Spec.Dicionario +
                    "; clipe=" + state.Spec.Clipe + "; duracaoSegundos=" + state.ClipDuration.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; reservaMs=" + state.Spec.DuracaoMs + "; loop=" + state.Spec.Loop + "; manterPoseFinal=" + state.Spec.ManterPoseFinal + "; sentado=" + state.Spec.SobrePoseSentada);
            }
            // Medir todos os clipes antes de aplicar as mesmas guardas de reserva.
            foreach (ActionState state in actions)
            {
                if (state.Spec.Tipo != "clipe" && state.Spec.Tipo != "celular") continue;
                if (!state.Spec.Loop && state.Spec.ManterPoseFinal && state.Spec.DuracaoMs + 1f < state.ClipDuration * 1000f)
                    throw Error("o intervalo termina antes da transição corporal; amplie a introdução sem cortar a fala: " + state.Spec.Ator);
                if (!state.Spec.Loop && !state.Spec.ManterPoseFinal && state.Spec.DuracaoMs + 1f < state.ClipDuration * 1000f)
                    throw Error("o orçamento termina antes do clipe completo; amplie a reserva sem cortar a ação: " + state.Spec.Ator);
            }
            ready = true; Log("ACOES_SERIE_RECURSOS_PRONTOS", "Durações reais consultadas; interpretação e encaixe ainda não testados");
        }
        else if (loading.ElapsedMilliseconds >= TimeoutMs) throw Error("os recursos das ações não carregaram em cinco segundos");
    }

    // Reset fora da filmagem. Conservar aparências/DNA/faciais: este helper não chama natives da boca.
    public void Reset()
    {
        try { ResetCore(); }
        catch { failed = true; ready = false; throw; }
    }

    public void ReleaseActorsForReset()
    {
        Active(); RequireReady(); RequireActors();
        StopActions();
        var phones = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (ActionState state in actions)
            if (!System.String.IsNullOrEmpty(state.PhoneCode) && phones.Add(state.PhoneCode)) resources.HidePhone(state.PhoneCode);
        resetDone = false;
    }

    private void ResetCore()
    {
        Active(); RequireReady(); RequireActors();
        // Prepare pode ocorrer durante assentamento. A primeira Reset vem depois da câmera/chão.
        foreach (ActorState actor in actors.Values)
            if (!actor.MarkCaptured)
            {
                actor.Position = actor.Ped.Position; actor.Heading = actor.Ped.Heading;
                actor.MarkCaptured = true;
            }
        StopActions(); resources.Reset();
        foreach (ActorState actor in actors.Values)
        {
            actor.Ped.Task.ClearAllImmediately(); actor.Ped.Velocity = GTA.Math.Vector3.Zero;
            actor.Ped.IsPositionFrozen = true; actor.Ped.PositionNoOffset = actor.Position; actor.Ped.Heading = actor.Heading;
            actor.Ped.Task.StandStill(-1);
        }
        foreach (ActionState state in actions) { state.Started = false; state.Finished = false; state.StartedAt = 0; state.MaxPhase = 0f; state.PhasePendingLogged = false; state.NextWalkLogMs = 0; state.PercursoStarted = false; state.PercursoStartedAt = 0; state.PercursoPhasePendingLogged = false; }
        taskRevisions.Clear();
        previousElapsed = -1; resetDone = true;
        Log("ACOES_SERIE_REINICIADAS", "Marcas/tarefas/telefones próprios restaurados; aparência e boca sob responsabilidade do Diretor");
    }

    public void Update(int elapsedMs)
    {
        Active(); RequireReady();
        if (!resetDone || elapsedMs < 0 || elapsedMs < previousElapsed) throw Error("reinicie as ações antes de executar ou voltar o relógio");
        RequireActors(true, elapsedMs); previousElapsed = elapsedMs;
        try
        {
            foreach (ActionState state in actions)
            {
                ActorAction spec = state.Spec;
                if (state.Finished || elapsedMs < spec.InicioMs) continue;
                bool startedNow = false;
                if (!state.Started)
                {
                    if (elapsedMs >= spec.InicioMs + spec.DuracaoMs) throw Error("o início da ação foi perdido; reinicie a tomada: " + spec.Ator);
                    StartAction(state); state.Started = true; startedNow = true;
                }
                if (spec.Tipo == "andar") LogWalkState(state, startedNow ? "INICIO" : "PASSO", elapsedMs, false);
                if (!System.String.IsNullOrEmpty(spec.ClipeDurantePercurso) && !startedNow) UpdateWalkClip(state);
                if (elapsedMs >= spec.InicioMs + spec.DuracaoMs) { FinishAction(state); state.Finished = true; continue; }
                if (spec.SobrePoseSentada) seatedPoses.RequireSeatedOverlay(state.Actor.Ped, spec.Dicionario, spec.Clipe);
                bool heldAtEnd = spec.ManterPoseFinal && !spec.Loop &&
                    unchecked((uint)(GTA.Game.GameTime - state.StartedAt)) >= state.ClipDuration * 1000f;
                bool playing = (spec.Tipo == "clipe" || spec.Tipo == "celular") &&
                    GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, state.Actor.Handle, spec.Dicionario, spec.Clipe, 3);
                // A fase só é consultada no Tick seguinte ao envio da tarefa de animação.
                if (playing && !startedNow)
                {
                    float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, state.Actor.Handle, spec.Dicionario, spec.Clipe);
                    uint elapsedSinceStart = unchecked((uint)(GTA.Game.GameTime - state.StartedAt));
                    if (phase == -1f && elapsedSinceStart < 500u)
                    {
                        if (!state.PhasePendingLogged)
                        {
                            Log("CLIP_PHASE_AGUARDANDO", "ator=" + spec.Ator + "; dicionário=" + spec.Dicionario +
                                "; clipe=" + spec.Clipe + "; desde_início_ms=" + elapsedSinceStart);
                            state.PhasePendingLogged = true;
                        }
                        continue;
                    }
                    if (!Finite(phase) || phase < 0f || phase > 1f)
                        throw Error("fase real inválida do clipe: ator=" + spec.Ator +
                            "; fase=" + phase.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                            "; dicionário=" + spec.Dicionario + "; clipe=" + spec.Clipe + "; loop=" + spec.Loop +
                            "; tempo_ação_ms=" + elapsedMs + "; desde_início_ms=" + unchecked((uint)(GTA.Game.GameTime - state.StartedAt)));
                    state.MaxPhase = System.Math.Max(state.MaxPhase, phase);
                }
                bool completed = !spec.Loop && !spec.ManterPoseFinal && state.MaxPhase >= 0.9f &&
                    unchecked((uint)(GTA.Game.GameTime - state.StartedAt)) >= state.ClipDuration * 1000f;
                if ((spec.Tipo == "clipe" || spec.Tipo == "celular") && !heldAtEnd && elapsedMs - spec.InicioMs >= 500 &&
                    !playing && !completed)
                    throw Error("o GTA interrompeu a animação corporal: " + spec.Ator);
            }
        }
        catch { failed = true; throw; }
    }

    private void StartAction(ActionState state)
    {
        ActorAction spec = state.Spec; GTA.Ped ped = state.Actor.Ped;
        if (ped.IsInVehicle()) throw Error("a direção a pé só inicia após saída física do veículo próprio: " + spec.Ator);
        if ((spec.Tipo == "clipe" || spec.Tipo == "celular") && !spec.Loop &&
            spec.InicioMs + spec.DuracaoMs - previousElapsed + 1f < state.ClipDuration * 1000f)
            throw Error("início atrasado não deixa reserva para o clipe completo; reinicie: " + spec.Ator);
        state.StartedAt = GTA.Game.GameTime; state.MaxPhase = 0f; state.PhasePendingLogged = false;
        state.NextWalkLogMs = 0;
        if (spec.SobrePoseSentada)
        {
            if (seatedPoses == null) throw Error("gesto sentado exige BindPoses e pose preparada");
            seatedPoses.RequireSeatedOverlay(ped, spec.Dicionario, spec.Clipe);
        }
        else ped.Task.ClearAllImmediately();
        ped.Velocity = GTA.Math.Vector3.Zero;
        if (spec.Tipo == "andar")
        {
            ped.IsPositionFrozen = false;
            if (!System.String.IsNullOrEmpty(spec.ClipSet))
            { ped.SetMovementClipSet(new GTA.ClipSet(spec.ClipSet)); state.Actor.MovementSet = true; }
            ped.Task.GoStraightTo(state.Destination, -1, new GTA.PedMoveBlendRatio(spec.Velocidade), ped.Heading, 0.20f);
            if (!System.String.IsNullOrEmpty(spec.ClipeDurantePercurso))
            {
                // Apenas o canal secundário: a tarefa primária GoStraight permanece responsável pelo trajeto.
                GTA.Native.Function.Call(GTA.Native.Hash.TASK_PLAY_ANIM, state.Actor.Handle, spec.PercursoDicionario, spec.PercursoClipe,
                    4f, -4f, -1, (int)(GTA.AnimationFlags.Loop | GTA.AnimationFlags.UpperBodyOnly | GTA.AnimationFlags.Secondary), 0f, false, false, false);
                state.PercursoStarted = true; state.PercursoStartedAt = GTA.Game.GameTime; state.PercursoPhasePendingLogged = false;
            }
        }
        else if (spec.Tipo == "virar")
        {
            ped.IsPositionFrozen = false;
            if (state.Target != null) ped.Task.TurnTo(state.Target.Ped, spec.DuracaoMs);
            else ped.Task.TurnTo(state.Destination, spec.DuracaoMs);
        }
        else
        {
            bool phone = spec.Tipo == "celular";
            if (phone) resources.AttachPhone(state.PhoneCode, ped);
            GTA.AnimationFlags flags = (GTA.AnimationFlags)0;
            if (spec.Loop) flags |= GTA.AnimationFlags.Loop;
            if (spec.ManterPoseFinal) flags |= GTA.AnimationFlags.StayInEndFrame;
            if (phone || !spec.CorpoInteiro) flags |= GTA.AnimationFlags.UpperBodyOnly | GTA.AnimationFlags.Secondary;
            if(pocket98AttachmentStart)
            {
                RequirePocket98AttachmentStartContract();
                if(!IsPocket98AttachmentStartClip(spec))throw Error("unknown clip cannot receive technical98 attachment-start flag");
                flags |= GTA.AnimationFlags.ProcessAttachmentsOnStart;
                Log("BOLSO98_ATTACHMENT_START_FLAG", "Scene=teste_fila_98; SourceSHA256="+PapelBolso98Diretor.ExactSourceHash+
                    "; Actor="+spec.Ator+"; ActorHandle="+state.Actor.Handle+"; ActorAddress="+state.Actor.Address+
                    "; Dictionary="+spec.Dicionario+"; Clip="+spec.Clipe+"; Resource="+spec.Recurso+"; StartMs="+spec.InicioMs+
                    "; Flags="+(int)flags+"; ProcessAttachmentsOnStart=True; NativePlayOnly=True; ProcessEntityAttachmentsCalled=False; HandReanchor=False; TimelineShift=False; ContactToleranceUnchanged=True; HypothesisOnly=True; ProductionApproval=False");
            }
            ped.IsPositionFrozen = System.String.IsNullOrEmpty(spec.GrupoPareado);
            if (!System.String.IsNullOrEmpty(spec.GrupoPareado))
            {
                flags |= GTA.AnimationFlags.ExtractInitialOffset | GTA.AnimationFlags.OverridePhysics;
                GTA.Math.Vector3 rotation = spec.Rotacao;
                rotation.Z += forward.ToHeading();
                ped.Task.PlayAnimationAdvanced(new GTA.CrClipAsset(spec.Dicionario, spec.Clipe), state.Destination, rotation,
                    new GTA.AnimationBlendDelta(4f), new GTA.AnimationBlendDelta(4f), spec.DuracaoMs, flags,
                    0f, GTA.EulerRotationOrder.YXZ, GTA.AnimationIKControlFlags.None);
            }
            else
            {
                // EP14_C04: a entrada ajoelha nos8s, mas a postura continua
                // durante as falas. Um limite native8000 encerraria a primaria;
                // somente este recurso/par literal conserva o ultimo quadro
                // ate a proxima acao propria, Reset ou Cleanup. Os gestos de
                // fala continuam UpperBody|Secondary e a boca permanece livre.
                bool persistentKneel = spec.Tipo == "clipe" && spec.Recurso == "ajoelhar_entrada_f" &&
                    spec.Dicionario == "missheist_jewel" && spec.Clipe == "manageress_kneel_intro" &&
                    spec.CorpoInteiro && spec.ManterPoseFinal && !spec.Loop;
                int nativeDuration = persistentKneel || !spec.Loop && !spec.ManterPoseFinal ? -1 : spec.DuracaoMs;
                GTA.Native.Function.Call(GTA.Native.Hash.TASK_PLAY_ANIM, state.Actor.Handle, spec.Dicionario, spec.Clipe,
                    4f, -4f, nativeDuration, (int)flags, 0f, false, false, false);
            }
        }
        TaskChanged(spec.Ator);
    }

    private void FinishAction(ActionState state)
    {
        ActorAction spec = state.Spec; GTA.Ped ped = state.Actor.Ped;
        if ((spec.Tipo == "clipe" || spec.Tipo == "celular") && !spec.Loop &&
            (state.MaxPhase < 0.9f || unchecked((uint)(GTA.Game.GameTime - state.StartedAt)) + 1f < state.ClipDuration * 1000f))
            throw Error("o clipe não terminou fisicamente dentro da reserva; tomada interrompida sem truncar: " + spec.Ator);
        if (dismissalHandoffTelemetry && spec.Tipo == "clipe" && !spec.Loop && !spec.ManterPoseFinal &&
            spec.Dicionario == "mp_common" && (spec.Clipe == "givetake1_a" || spec.Clipe == "givetake1_b") &&
            spec.InicioMs == 0 && spec.DuracaoMs == 3500 &&
            (spec.Ator == "capanga_4" || spec.Ator == "capanga_1" || spec.Ator == "capanga_2" || spec.Ator == "capanga_3"))
            Log("EP13_ENTREGA_CLIPE_COMPLETO", "Ator=" + spec.Ator + "; Dictionary=" + spec.Dicionario + "; Clip=" + spec.Clipe +
                "; TempoMs=" + previousElapsed + "; ReservaMs=" + spec.DuracaoMs + "; NativeDurationMs=" +
                (state.ClipDuration * 1000f).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; MaxPhase=" + state.MaxPhase.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; SinceStartMs=" + unchecked((uint)(GTA.Game.GameTime - state.StartedAt)) + "; GuardUnchanged=true");
        if (spec.SobrePoseSentada) seatedPoses.RequireSeatedOverlay(ped, spec.Dicionario, spec.Clipe);
        StopWalkClip(state);
        if (spec.Tipo == "andar")
        {
            if (DistanceXY(ped.Position, state.Destination) > 0.40f || System.Math.Abs(ped.Position.Z - state.Destination.Z) > 1f)
            {
                LogWalkState(state, "FALHA_CHEGADA", previousElapsed, true);
                throw Error("o ator não chegou à marca no tempo previsto; tomada interrompida sem teleporte: " + spec.Ator);
            }
            LogWalkState(state, "CHEGADA", previousElapsed, true);
        }
        else if (spec.Tipo == "virar")
        {
            GTA.Math.Vector3 target = state.Target != null ? state.Target.Ped.Position : state.Destination;
            GTA.Math.Vector3 difference = target - ped.Position;
            if (DistanceXY(target, ped.Position) < 0.05f || AngleDifference(ped.Heading, difference.ToHeading()) > 15f)
                throw Error("o ator não terminou de virar no tempo previsto: " + spec.Ator);
        }
        if ((spec.Tipo == "clipe" || spec.Tipo == "celular") && !spec.ManterPoseFinal)
            ped.Task.StopScriptedAnimationTask(new GTA.CrClipAsset(spec.Dicionario, spec.Clipe));
        // C5 literal: a entrega fisica cobre os primeiros3.5s da fala9s de Iraci.
        // Enfileirar apenas a revisao corporal: o Main retoma implorando depois
        // de state.Finished=true neste mesmo Tick; boca e pose primaria continuam.
        if (spec.Tipo == "clipe" && spec.Recurso == "entrega_a" && spec.PreservarDuranteFala &&
            spec.SobrePoseSentada && !spec.Loop && !spec.ManterPoseFinal &&
            spec.Dicionario == "mp_common" && spec.Clipe == "givetake1_a" &&
            spec.InicioMs == 0 && spec.DuracaoMs == 3500 &&
            System.String.Equals(spec.Ator, "DONA_IRACI", System.StringComparison.OrdinalIgnoreCase))
            TaskChanged(spec.Ator);
        if (state.Actor.MovementSet)
        { ped.ResetMovementClipSet(); state.Actor.MovementSet = false; }
        if (spec.Tipo == "celular" && !spec.ManterPoseFinal) resources.HidePhone(state.PhoneCode);
        ped.Velocity = GTA.Math.Vector3.Zero; ped.IsPositionFrozen = true;
        if (spec.Tipo == "andar" || spec.Tipo == "virar") { if (System.String.IsNullOrEmpty(spec.ClipeDurantePercurso)) ped.Task.ClearAllImmediately(); ped.Task.StandStill(-1); TaskChanged(spec.Ator); }
    }

    private void UpdateWalkClip(ActionState state)
    {
        ActorAction spec = state.Spec;
        uint sinceStart = unchecked((uint)(GTA.Game.GameTime - state.PercursoStartedAt));
        bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, state.Actor.Handle, spec.PercursoDicionario, spec.PercursoClipe, 3);
        if (!playing)
        {
            if (sinceStart < 500u) return;
            throw Error("o GTA interrompeu o canal secundário durante a caminhada: " + spec.Ator + "; recurso=" + spec.ClipeDurantePercurso + "; desde_início_ms=" + sinceStart);
        }
        float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, state.Actor.Handle, spec.PercursoDicionario, spec.PercursoClipe);
        if (phase == -1f && sinceStart < 500u)
        {
            if (!state.PercursoPhasePendingLogged) { Log("PERCURSO_PHASE_AGUARDANDO", "ator=" + spec.Ator + "; recurso=" + spec.ClipeDurantePercurso); state.PercursoPhasePendingLogged = true; }
            return;
        }
        if (!Finite(phase) || phase < 0f || phase > 1f)
            throw Error("fase real inválida do canal secundário: ator=" + spec.Ator + "; fase=" + phase.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; dicionário=" + spec.PercursoDicionario + "; clipe=" + spec.PercursoClipe + "; loop=true; desde_início_ms=" + sinceStart);
    }
    private static void StopWalkClip(ActionState state)
    {
        if (!state.PercursoStarted || !StillOwn(state.Actor)) return;
        state.Actor.Ped.Task.StopScriptedAnimationTask(new GTA.CrClipAsset(state.Spec.PercursoDicionario, state.Spec.PercursoClipe));
        state.PercursoStarted = false;
    }

    // Somente getters: a telemetria nunca muda tarefas, destino, raio ou prazo de chegada.
    private void LogWalkState(ActionState state, string stage, int elapsedMs, bool force)
    {
        if (!force && (long)elapsedMs < state.NextWalkLogMs) return;
        state.NextWalkLogMs = (long)elapsedMs + 500;
        try
        {
            GTA.Ped ped = state.Actor.Ped;
            GTA.Math.Vector3 position = ped.Position, velocity = ped.Velocity;
            int actorInterior = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_INTERIOR_FROM_ENTITY, ped.Handle);
            GTA.ScriptTaskNameHash taskName; GTA.ScriptTaskStatus taskStatus;
            ped.GetCurrentScriptTaskNameHashAndStatus(out taskName, out taskStatus);
            float groundAtActor, groundAtDestination;
            bool actorGroundFound = GTA.World.GetGroundHeight(position + new GTA.Math.Vector3(0f, 0f, 2f), out groundAtActor, GTA.GetGroundHeightMode.Normal);
            bool destinationGroundFound = GTA.World.GetGroundHeight(state.Destination + new GTA.Math.Vector3(0f, 0f, 2f), out groundAtDestination, GTA.GetGroundHeightMode.Normal);
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture;
            Log("ACAO_ANDAR_GETTERS", "Ator=" + state.Spec.Ator + "; Etapa=" + stage + "; TempoMs=" + elapsedMs +
                "; DesdeInicioMs=" + unchecked((uint)(GTA.Game.GameTime - state.StartedAt)) +
                "; Handle=" + ped.Handle + "; InteriorReal=" + actorInterior + "; Pos=" + position + "; Destino=" + state.Destination +
                "; MarcaInicial=" + state.Actor.Position + "; Ancora=" + anchor + "; Deslocamento=" + state.Spec.Deslocamento +
                "; DistanciaXY=" + DistanceXY(position, state.Destination).ToString("R", culture) +
                "; DeltaZ=" + System.Math.Abs(position.Z - state.Destination.Z).ToString("R", culture) +
                "; Velocidade=" + ped.Speed.ToString("R", culture) + "; Velocity=" + velocity + "; Frozen=" + ped.IsPositionFrozen +
                "; GroundAtActorNativeNormal=" + (actorGroundFound ? groundAtActor.ToString("R", culture) : "nao_encontrado") +
                "; GroundAtDestinationNativeNormal=" + (destinationGroundFound ? groundAtDestination.ToString("R", culture) : "nao_encontrado") +
                "; CurrentScriptTaskNameHash=" + taskName + "(0x" + taskName.ToString("X") + "); CurrentScriptTaskStatus=" + taskStatus);
        }
        catch (System.Exception ex) { Log("ERRO_TELEMETRIA_ANDAR", "Ator=" + state.Spec.Ator + "; Etapa=" + stage + "; " + ex.Message); }
    }

    private void StopActions()
    {
        foreach (ActionState state in actions)
        {
            if (!StillOwn(state.Actor)) continue;
            StopWalkClip(state);
            if (state.Started && (state.Spec.Tipo == "clipe" || state.Spec.Tipo == "celular"))
                state.Actor.Ped.Task.StopScriptedAnimationTask(new GTA.CrClipAsset(state.Spec.Dicionario, state.Spec.Clipe));
        }
        foreach (ActorState actor in actors.Values)
            if (StillOwn(actor))
            {
                actor.Ped.Task.ClearAllImmediately(); actor.Ped.Velocity = GTA.Math.Vector3.Zero;
                if (actor.MovementSet) { actor.Ped.ResetMovementClipSet(); actor.MovementSet = false; }
            }
    }

    public void Cleanup()
    {
        if (disposed) return;
        ready = false; resetDone = false;
        StopActions();
        resources.Cleanup(); // se falhar, preservar registros próprios para outra tentativa.
        foreach (var item in dictionaries) if (item.Value) GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT, item.Key);
        foreach (var item in clipSets) if (item.Value) GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_SET, item.Key);
        foreach (ActorState actor in actors.Values) if (StillOwn(actor)) actor.Ped.IsPositionFrozen = actor.Frozen;
        actors.Clear(); actions.Clear(); dictionaries.Clear(); clipSets.Clear();
        prepared = false; failed = false; loading = null; previousElapsed = -1; endMs = 0;
        seatedPoses = null; ownVehicles = null;
    }
    public void Dispose() { Cleanup(); resources.Dispose(); disposed = true; }

    private void ValidateSpec(ActorAction spec)
    {
        Identifier(spec.Ator, "ator");
        if (spec.Tipo != "clipe" && spec.Tipo != "andar" && spec.Tipo != "virar" && spec.Tipo != "celular")
            throw Error("tipo não suportado; use clipe, andar, virar ou celular");
        if (spec.InicioMs < 0 || spec.DuracaoMs < 100 || spec.InicioMs > 180000 || spec.DuracaoMs > 180000 ||
            (long)spec.InicioMs + spec.DuracaoMs > 180000) throw Error("tempo da ação fora dos limites: duração mínima100ms, tomada até180s");
        Vector(spec.Deslocamento); Vector(spec.Rotacao);
        if (System.Math.Abs(spec.Deslocamento.X) > 100f || System.Math.Abs(spec.Deslocamento.Y) > 100f || System.Math.Abs(spec.Deslocamento.Z) > 20f)
            throw Error("deslocamento relativo fora dos limites da cena");
        if (!Finite(spec.Velocidade) || spec.Velocidade < 0.1f || spec.Velocidade > 3f)
            throw Error("velocidade proporcional deve ficar entre0,1 e3");
        if (!System.String.IsNullOrEmpty(spec.GrupoPareado))
        { Identifier(spec.GrupoPareado, "grupo"); if (spec.Tipo != "clipe" || !spec.CorpoInteiro) throw Error("grupo pareado exige clipes de corpo inteiro"); }
        if (spec.SobrePoseSentada && (spec.Tipo != "clipe" || spec.CorpoInteiro || spec.Loop || spec.ManterPoseFinal ||
            !System.String.IsNullOrEmpty(spec.GrupoPareado) || !PosesSerieDiretor.IsOverlayClip(spec.Dicionario, spec.Clipe)))
            throw Error("gesto sentado exige clipe catalogado secundário sem loop, pose final ou grupo pareado");
        bool walkClip = !System.String.IsNullOrEmpty(spec.ClipeDurantePercurso);
        if (walkClip && (spec.Tipo != "andar" || !CanComposeWalkClip(spec.ClipeDurantePercurso, spec.PercursoDicionario, spec.PercursoClipe)))
            throw Error("clipeDurantePercurso exige andar e par secundário literal de bandeja, ligação ou sacola conferido");
        if (!walkClip && (!System.String.IsNullOrEmpty(spec.PercursoDicionario) || !System.String.IsNullOrEmpty(spec.PercursoClipe) || !System.String.IsNullOrEmpty(spec.PercursoOrigemArquivo) || !System.String.IsNullOrEmpty(spec.PercursoOrigemSha256) || spec.PercursoOrigemLinha != 0))
            throw Error("evidência secundária exige clipeDurantePercurso");
        if (walkClip)
        {
            AnimationName(spec.PercursoDicionario); AnimationName(spec.PercursoClipe);
            if (spec.PercursoOrigemLinha < 1 || spec.PercursoOrigemLinha > 500000 || System.String.IsNullOrEmpty(spec.PercursoOrigemArquivo) ||
                !System.Text.RegularExpressions.Regex.IsMatch(spec.PercursoOrigemSha256 ?? "", "\\A[0-9A-Fa-f]{64}\\z"))
                throw Error("canal secundário exige arquivo, linha e SHA256 de origem conferidos");
        }
        if (spec.PreservarDuranteFala && !(spec.Tipo == "clipe" && CanControlSpeechBody(spec.Dicionario, spec.Clipe) || spec.Tipo == "andar" && walkClip))
            throw Error("preservarDuranteFala exige clipe físico de ligação, abraço, entrega ou ameaça conferido");
        if (!System.String.IsNullOrEmpty(spec.ClipSet))
        {
            if (spec.Tipo != "andar" || spec.Clipe != "walk" || spec.Dicionario != spec.ClipSet)
                throw Error("ClipSet de caminhada exige origem do par literal ClipSet/walk; não combinar com outro clipe");
            AnimationName(spec.ClipSet);
        }
        if (spec.Tipo == "clipe" || spec.Tipo == "celular" || !System.String.IsNullOrEmpty(spec.ClipSet))
        {
            AnimationName(spec.Dicionario); AnimationName(spec.Clipe);
            if (spec.OrigemLinha < 1 || spec.OrigemLinha > 500000 || System.String.IsNullOrEmpty(spec.OrigemArquivo) ||
                !System.Text.RegularExpressions.Regex.IsMatch(spec.OrigemSha256 ?? "", "\\A[0-9A-Fa-f]{64}\\z"))
                throw Error("animação exige arquivo, linha e SHA256 de origem conferidos");
        }
        if (!System.String.IsNullOrEmpty(spec.Alvo) && spec.Tipo != "virar") throw Error("Alvo só se aplica à ação virar");
    }

    private void ValidatePairs()
    {
        var groups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ActionState>>(System.StringComparer.Ordinal);
        foreach (ActionState state in actions)
        {
            string key = state.Spec.GrupoPareado; if (System.String.IsNullOrEmpty(key)) continue;
            System.Collections.Generic.List<ActionState> group;
            if (!groups.TryGetValue(key, out group)) { group = new System.Collections.Generic.List<ActionState>(); groups.Add(key, group); }
            group.Add(state);
        }
        foreach (var item in groups)
        {
            if (item.Value.Count < 2 || item.Value.Count > 4) throw Error("grupo pareado exige de dois a quatro atores");
            ActorAction first = item.Value[0].Spec;
            foreach (ActionState state in item.Value)
                if (state.Spec.InicioMs != first.InicioMs || state.Spec.DuracaoMs != first.DuracaoMs ||
                    state.Spec.Deslocamento != first.Deslocamento || state.Spec.Rotacao != first.Rotacao || state.Spec.Dicionario != first.Dicionario ||
                    state.Spec.Loop != first.Loop || state.Spec.ManterPoseFinal != first.ManterPoseFinal)
                    throw Error("clipes pareados exigem mesma referência, rotação, dicionário e tempos");
        }
    }

    private void VerifySources()
    {
        var sources = new System.Collections.Generic.Dictionary<string, SourceCheck>(System.StringComparer.OrdinalIgnoreCase);
        foreach (ActionState state in actions)
        foreach (ActorAction spec in SourceEvidence(state.Spec))
        {
            if (!System.IO.Path.IsPathRooted(spec.OrigemArquivo)) throw Error("a origem precisa de caminho absoluto");
            string path = System.IO.Path.GetFullPath(spec.OrigemArquivo);
            if (path.StartsWith("\\\\", System.StringComparison.Ordinal) || path.Substring(2).IndexOf(':') >= 0 ||
                !System.String.Equals(System.IO.Path.GetFileName(path), "PedAnimList.txt", System.StringComparison.OrdinalIgnoreCase))
                throw Error("use a PedAnimList.txt instalada, em caminho local sem stream alternativo");
            SourceCheck source;
            if (!sources.TryGetValue(path, out source)) { source = new SourceCheck { Path = path, Hash = spec.OrigemSha256.ToUpperInvariant() }; sources.Add(path, source); }
            if (source.Hash != spec.OrigemSha256.ToUpperInvariant()) throw Error("hashes conflitantes para a mesma fonte");
            string pair = spec.Dicionario + " " + spec.Clipe, previous;
            if (source.Lines.TryGetValue(spec.OrigemLinha, out previous) && previous != pair) throw Error("a mesma linha de origem foi usada para clipes diferentes");
            source.Lines[spec.OrigemLinha] = pair;
        }
        foreach (SourceCheck source in sources.Values)
        {
            NoLinks(source.Path);
            using (var stream = new System.IO.FileStream(source.Path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                if (stream.Length > 64L * 1024 * 1024) throw Error("a lista de origem ultrapassa64MB");
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    if (System.BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != source.Hash) throw Error("SHA256 da PedAnimList diverge da evidência");
                stream.Position = 0;
                using (var reader = new System.IO.StreamReader(stream, new System.Text.UTF8Encoding(false, true), true))
                {
                    int line = 0, found = 0; string row, expected;
                    while ((row = reader.ReadLine()) != null)
                    {
                        line++; if (line > 500000) throw Error("a lista ultrapassa o limite de linhas");
                        if (!source.Lines.TryGetValue(line, out expected)) continue;
                        if (row.Trim() != expected) throw Error("o par dicionário/clipe não corresponde à linha de origem");
                        found++; if (found == source.Lines.Count) break;
                    }
                    if (found != source.Lines.Count) throw Error("linha de origem ausente na PedAnimList");
                }
            }
            NoLinks(source.Path);
        }
    }

    private static System.Collections.Generic.IEnumerable<ActorAction> SourceEvidence(ActorAction spec)
    {
        if (spec.Tipo == "clipe" || spec.Tipo == "celular" || !System.String.IsNullOrEmpty(spec.ClipSet)) yield return spec;
        if (!System.String.IsNullOrEmpty(spec.ClipeDurantePercurso))
            yield return new ActorAction { Dicionario = spec.PercursoDicionario, Clipe = spec.PercursoClipe,
                OrigemArquivo = spec.PercursoOrigemArquivo, OrigemSha256 = spec.PercursoOrigemSha256, OrigemLinha = spec.PercursoOrigemLinha };
    }

    private static ActorAction Copy(ActorAction s)
    {
        return new ActorAction { Tipo = s.Tipo, Ator = s.Ator, Dicionario = s.Dicionario, Clipe = s.Clipe,
            Alvo = s.Alvo, GrupoPareado = s.GrupoPareado, ClipSet = s.ClipSet, OrigemArquivo = s.OrigemArquivo,
            OrigemSha256 = s.OrigemSha256, InicioMs = s.InicioMs, DuracaoMs = s.DuracaoMs, OrigemLinha = s.OrigemLinha,
            Deslocamento = s.Deslocamento, Rotacao = s.Rotacao, Velocidade = s.Velocidade, Loop = s.Loop,
            CorpoInteiro = s.CorpoInteiro, ManterPoseFinal = s.ManterPoseFinal, SobrePoseSentada = s.SobrePoseSentada,
            PreservarDuranteFala = s.PreservarDuranteFala, Recurso = s.Recurso,
            ClipeDurantePercurso = s.ClipeDurantePercurso, PercursoDicionario = s.PercursoDicionario, PercursoClipe = s.PercursoClipe,
            PercursoOrigemArquivo = s.PercursoOrigemArquivo, PercursoOrigemSha256 = s.PercursoOrigemSha256, PercursoOrigemLinha = s.PercursoOrigemLinha };
    }
    private GTA.Math.Vector3 ToWorld(GTA.Math.Vector3 offset) { return anchor + right * offset.X + forward * offset.Y + new GTA.Math.Vector3(0f, 0f, offset.Z); }
    private void RequireActors() { RequireActors(false, -1); }
    private void RequireActors(bool allowPendingVehicle, int elapsed)
    {
        foreach (ActorState actor in actors.Values)
        {
            if (!StillOwn(actor) || actor.Ped.IsDead) throw Error("a identidade/estado do ator mudou: " + actor.Id);
            if (!actor.Ped.IsInVehicle()) continue;
            if (!allowPendingVehicle || ownVehicles == null || !ownVehicles.IsReady || !ownVehicles.OwnsVehicleContaining(actor.Ped))
                throw Error("ator permanece em veículo sem direção própria válida: " + actor.Id);
            bool waiting = false, hasActions = false;
            foreach (ActionState state in actions)
            {
                if (state.Actor != actor) continue;
                hasActions = true;
                if (state.Finished) continue;
                if (state.Started || elapsed >= state.Spec.InicioMs)
                    throw Error("saída física não terminou antes da direção a pé: " + actor.Id);
                waiting = true;
            }
            // Figurante sem direção própria não recebe tarefa corporal aqui; ex.: passageira enquanto colegas andam.
            if (hasActions && !waiting) throw Error("ator em veículo sem ação a pé futura própria: " + actor.Id);
        }
    }
    private static bool StillOwn(ActorState actor)
    {
        if (actor == null || System.Object.ReferenceEquals(actor.Ped, null) || !actor.Ped.Exists() || actor.Ped.Handle != actor.Handle ||
            actor.Ped.MemoryAddress != actor.Address || actor.Ped.Model.Hash != actor.Model) return false;
        GTA.Ped player = GTA.Game.Player.Character; return System.Object.ReferenceEquals(player, null) || actor.Handle != player.Handle;
    }
    private static void RequirePed(GTA.Ped ped)
    {
        GTA.Ped player = GTA.Game.Player.Character;
        if (System.Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsDead || ped.IsInVehicle() ||
            (!System.Object.ReferenceEquals(player, null) && player.Handle == ped.Handle)) throw Error("o elenco precisa conter somente atores próprios vivos a pé, sem o jogador");
    }
    private static void NoLinks(string path)
    {
        string current = path;
        while (!System.String.IsNullOrEmpty(current))
        {
            if ((System.IO.File.Exists(current) || System.IO.Directory.Exists(current)) &&
                (System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0) throw Error("a origem não pode usar link/junction");
            current = System.IO.Path.GetDirectoryName(current);
        }
    }
    private static void Identifier(string value, string field)
    { if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? "", "\\A[A-Za-z][A-Za-z0-9_]{0,63}\\z")) throw Error("identificador inválido: " + field); }
    private static void AnimationName(string value)
    { if (System.String.IsNullOrEmpty(value) || value.Length > 160 || value.IndexOfAny(new char[] { '\r', '\n', '\t', '\0', ' ', '"', '<', '>' }) >= 0) throw Error("nome de animação inválido"); }
    private static void Vector(GTA.Math.Vector3 value) { if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Z)) throw Error("vetor precisa de valores finitos"); }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static float DistanceXY(GTA.Math.Vector3 a, GTA.Math.Vector3 b) { float x = a.X - b.X, y = a.Y - b.Y; return (float)System.Math.Sqrt(x * x + y * y); }
    private static float AngleDifference(float a, float b) { return System.Math.Abs(((a - b + 540f) % 360f) - 180f); }
    private void Active() { if (disposed) throw Error("o backend foi encerrado"); }
    private void RequirePrepared() { if (!prepared || failed) throw Error("prepare novamente os recursos após limpar o erro anterior"); }
    private void RequireReady() { if (!IsReady) throw Error("aguarde o carregamento das ações"); }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("Ações da série: " + message + "."); }
    private void Log(string kind, string message) { if (log != null) { try { log(kind, message); } catch (System.Exception) { } } }
}
