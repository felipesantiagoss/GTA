// CANDIDATO ISOLADO89 WARM-GRIP S0: ensaio, nao prova de abertura/dobra/retirada do bolso.
// Family5 registra Amanda_Note em CUTSCENE; o binding fora dela e experimental.
// Nenhum Script, setter XYZ do ator, atalho, cutscene global ou troca temporizada de modelo.
public sealed class CartaFisicaDiretor : System.IDisposable
{
    public sealed class EnsaioSpec
    {
        public int Segmento;
        public string Objetos = "nota_b"; // nota_s | nota_b | ambos
        public int JanelaMs = 9000;
    }
    public const string SceneId = "teste_fila_89";
    private const string Prefix = "family_5_mcs_5_p4-";
    private const string NoteS = "p_amanda_note_01_s", NoteB = "prop_amanda_note_01b";
    private readonly System.Action<string, string> log;
    private readonly string menyoo;
    private readonly bool[] dictWasLoaded = new bool[7], dictRequested = new bool[7];
    private readonly float[,] durations = new float[7, 3];
    private readonly System.Collections.Generic.List<Paper> papers = new System.Collections.Generic.List<Paper>();
    private GTA.Ped actor;
    private System.Func<GTA.Ped, bool> ownsActor;
    private EnsaioSpec spec;
    private int actorHandle, actorModel, health, scan, modelsAt, nextLog, startedAt, lastElapsed = -1;
    private System.IntPtr actorAddress;
    private bool prepared, ready, running, finished, disposed, wasFrozen, ownTaskStarted;
    private bool bodyObserved, modelSWasLoaded, modelBWasLoaded, modelSRequested, modelBRequested;
    private float bodyMaximum, originalHeading;
    private int warmStartedAt, stableAt, stableCount;
    private bool warmBindingPending;
    private int warmBindingStartedAt, warmBindingNextLogAt;
    private GTA.Math.Vector3 previousRoot, previousLeftHand, previousPaperGrip;
    private float previousHeading;
    private GTA.Prop ownTable;
    private int tableHandle, tableModel;
    private System.IntPtr tableAddress;
    private GTA.Math.Vector3 tablePosition, tableRotation, tableMinimum, tableMaximum, tableSurface;
    private GTA.Math.Vector3 stableReadingRootForSupport;
    private bool stableReadingRootMeasured;
    private bool tableModelRequested, tableModelWasLoaded, tableMeasured;
    private const string TableName = "prop_ff_counter_02";
    private GTA.Math.Vector3 originalRoot, origin, orientation;
    private GTA.FwSyncedScene scene;
    private System.Diagnostics.Stopwatch loading;
    private sealed class Paper
    {
        public GTA.Prop Entity;
        public int Handle, Model, Track;
        public System.IntPtr Address;
        public GTA.Math.Vector3 Minimum, Maximum, SceneOrigin;
        public GTA.FwSyncedScene Scene;
        public bool HandContactObserved;
        public float MinimumHandDistance = System.Single.MaxValue;
        public float MaximumPhase;
        public bool Observed;
        public int ReadingContactSamples, ReadingContactFailures;
        public float ReadingMaximumHandDistance;
    }
    public CartaFisicaDiretor(System.Action<string, string> writeLog, string pastaMenyoo)
    { log = writeLog; menyoo = pastaMenyoo; }
    public bool IsReady { get { return prepared && ready && !disposed; } }
    public bool IsFinished { get { return IsReady && finished; } }
    public bool TracksCompletos { get; private set; }
    public bool AberturaVisualConfirmada { get { return false; } }
    public bool ControlsBody(string actorId, int elapsedMs)
    { return IsReady && System.String.Equals(actorId, "djanira_pobre", System.StringComparison.OrdinalIgnoreCase); }

    public static EnsaioSpec LoadSpec(string path)
    {
        string json = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
        if (json.Length > 512) throw Error("configuracao tecnica excede512 caracteres");
        var fields = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
        var token = new System.Text.RegularExpressions.Regex(
            "\\G\\s*\\\"(?<k>[A-Za-z][A-Za-z0-9]*)\\\"\\s*:\\s*(?<v>\\\"[A-Za-z_0-9]+\\\"|0|[1-9][0-9]*)\\s*",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        int at = 0;
        while (at < json.Length && System.Char.IsWhiteSpace(json[at])) at++;
        if (at >= json.Length || json[at++] != '{') throw Error("objeto JSON plano exigido");
        bool end = false;
        for (int count = 0; count < 5; count++)
        {
            var match = token.Match(json, at);
            if (!match.Success) throw Error("campo/tipo/escape JSON tecnico invalido");
            string key = match.Groups["k"].Value;
            if (fields.ContainsKey(key)) throw Error("campo duplicado");
            fields.Add(key, match.Groups["v"].Value);
            at = match.Index + match.Length;
            if (at < json.Length && json[at] == '}') { at++; end = true; break; }
            if (at >= json.Length || json[at++] != ',') throw Error("separador JSON invalido");
        }
        while (at < json.Length && System.Char.IsWhiteSpace(json[at])) at++;
        if (!end || at != json.Length || fields.Count != 5 ||
            !fields.ContainsKey("versao") || !fields.ContainsKey("id") || !fields.ContainsKey("segmento") ||
            !fields.ContainsKey("objetos") || !fields.ContainsKey("janelaMs") ||
            fields["versao"] != "1" || fields["id"] != "\"" + SceneId + "\"")
            throw Error("contrato tecnico exclusivo teste_fila_89");
        int segment, window;
        if (!System.Int32.TryParse(fields["segmento"], out segment) ||
            !System.Int32.TryParse(fields["janelaMs"], out window)) throw Error("inteiros exigidos");
        string objects = fields["objetos"];
        if (objects.Length < 2 || objects[0] != '\"') throw Error("objetos deve ser string");
        var result = new EnsaioSpec { Segmento = segment, Objetos = objects.Substring(1, objects.Length - 2), JanelaMs = window };
        Validate(result); return result;
    }

    public void Prepare(string technicalSceneId, GTA.Ped ownActor, System.Func<GTA.Ped, bool> actorOwnership, EnsaioSpec request)
    {
        Active(); Story();
        if (prepared || actor != null || technicalSceneId != SceneId || ownActor == null || actorOwnership == null || !actorOwnership(ownActor))
            throw Error("ensaio exige ID tecnico e atriz propria preparada");
        Validate(request);
        if (request.Segmento != 0 || request.Objetos != "nota_s")
            throw Error("candidata de grip medido exclusiva segmento0 nota_s; outras tracks exigem evidencia nativa propria");
        VerifySources();
        spec = new EnsaioSpec { Segmento = request.Segmento, Objetos = request.Objetos, JanelaMs = request.JanelaMs };
        actor = ownActor; ownsActor = actorOwnership; actorHandle = actor.Handle; actorAddress = actor.MemoryAddress;
        actorModel = actor.Model.Hash; health = actor.Health; RequireActor();
        originalRoot = actor.Position; Check(originalRoot); originalHeading = actor.Heading; Check(originalHeading);
        wasFrozen = actor.IsPositionFrozen;
        modelSWasLoaded = new GTA.Model(NoteS).IsLoaded; modelBWasLoaded = new GTA.Model(NoteB).IsLoaded;
        tableModelWasLoaded = new GTA.Model(TableName).IsLoaded;
        for (int i = 0; i < 7; i++) dictWasLoaded[i] = new GTA.CrClipDictionary(Dictionary(i)).IsLoaded;
        prepared = true; loading = System.Diagnostics.Stopwatch.StartNew();
        Log("CARTA_TRIAL_INICIO", "Scene=" + SceneId + "; Segment=" + spec.Segmento + "; Objects=" + spec.Objetos +
            "; WindowMs=9000; Actor=" + actorHandle + "; ActorModel=" + actorModel + "; Health=" + health +
            "; Root=" + originalRoot + "; FemaleRetargetExperimental=true; BindingOutsideCutsceneExperimental=true; NoProductionApproval=true");
        InventoryModel(NoteS); InventoryModel(NoteB);
    }

    // Um dicionario e tres duracoes por frame; sem Wait/Script.Yield.
    public void PollLoading()
    {
        Active(); Story(); if (!prepared || ready) return;
        RequireActor();
        if (loading.ElapsedMilliseconds >= 15000) throw Error("streaming do ensaio nao concluiu em15s");
        if (modelsAt < 2 && (actor.Position - originalRoot).Length() > 0.05f) throw Error("atriz saiu da marca durante preparo");
        if (modelsAt >= 2 && (actor.Position - originalRoot).Length() > 3f) throw Error("pose authored de preparo saiu dos3m tecnicos");
        if (scan < 7)
        {
            var dictionary = new GTA.CrClipDictionary(Dictionary(scan));
            if (!dictionary.Exists)
            {
                Log("CARTA_TRIAL_DICT", "Segment=" + scan + "; Exists=false; NoProductionApproval=true");
                if (scan == spec.Segmento) throw Error("dicionario selecionado ausente no Legacy");
                scan++; return;
            }
            if (!dictionary.IsLoaded) { dictionary.Request(); dictRequested[scan] = true; return; }
            for (int track = 0; track < 3; track++)
            {
                float duration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, Dictionary(scan), Clip(scan, track));
                durations[scan, track] = duration;
                Log("CARTA_TRIAL_DURATION", "Segment=" + scan + "; Dictionary=" + Dictionary(scan) + "; Clip=" + Clip(scan, track) +
                    "; DictExists=true; DictLoaded=true; Seconds=" + Number(duration) + "; PositiveFinite=" + ValidDuration(duration) +
                    "; FitsFullNineSeconds=" + (ValidDuration(duration) && duration <= 9f) + "; Speed=1; ProductionApproval=false");
            }
            InventoryInitialOffsets(scan);
            scan++; return;
        }
        // Inventario completo primeiro; nenhuma criacao/Ready pode encurtar o par selecionado.
        if (spec.Objetos == "ambos") throw Error("rebase experimental exige um papel por ensaio; parentesco S/B nao comprovado");
        if (!SelectedDurationsFit())
        {
            Log("CARTA_TRIAL_DURATION_REJECT", "Segment=" + spec.Segmento + "; Objects=" + spec.Objetos +
                "; BodySeconds=" + Number(durations[spec.Segmento, 0]) + "; NoteSSeconds=" + Number(durations[spec.Segmento, 1]) +
                "; NoteBSeconds=" + Number(durations[spec.Segmento, 2]) + "; WindowMs=9000; FullTrackRequired=true; BeforeCreateReadyStart=true");
            throw Error("tracks selecionados nao cabem integralmente em9s ou duracao invalida; consultar inventario, sem acelerar/truncar");
        }
        if (modelsAt == 0)
        {
            if (!ValidDuration(durations[spec.Segmento, 0])) throw Error("clip corporal selecionado sem duracao finita positiva");
            if (UseS()) RequestModel(NoteS, 1);
            if (UseB()) RequestModel(NoteB, 2);
            var support = new GTA.Model(TableName);
            if (!support.IsInCdImage || !support.IsValid || !support.IsProp) throw Error("modelo da mesa propria indisponivel no Legacy");
            support.Request(); support.RequestCollision(); tableModelRequested = true;
            modelsAt = 1; return;
        }
        if ((UseS() && !new GTA.Model(NoteS).IsLoaded) || (UseB() && !new GTA.Model(NoteB).IsLoaded)) return;
        if (modelsAt == 1)
        {
            InventoryLoadedBounds(NoteS);
            InventoryLoadedBounds(NoteB);
            // Resolve a origem pelo offset REAL do clip para a raiz atual, sem mover a atriz.
            GTA.Math.Vector3 localRotation = InitialRotation(Clip(spec.Segmento, 0), GTA.Math.Vector3.Zero, GTA.Math.Vector3.Zero);
            Check(localRotation);
            if (System.Math.Abs(localRotation.X) > 0.1f || System.Math.Abs(localRotation.Y) > 0.1f)
                throw Error("inicio corporal authored inclinado; falta derivacao comprovada de rotacao composta");
            orientation = new GTA.Math.Vector3(0f, 0f, actor.Heading - localRotation.Z);
            GTA.Math.Vector3 offset = InitialPosition(Clip(spec.Segmento, 0), GTA.Math.Vector3.Zero, orientation);
            Check(offset); origin = originalRoot - offset; Check(origin);
            GTA.Math.Vector3 bodyStart = InitialPosition(Clip(spec.Segmento, 0), origin, orientation);
            GTA.Math.Vector3 bodyRotation = InitialRotation(Clip(spec.Segmento, 0), origin, orientation);
            Check(bodyStart); Check(bodyRotation);
            if ((bodyStart - originalRoot).Length() > 0.01f || Angle(bodyRotation.Z - actor.Heading) > 0.5f)
                throw Error("derivacao da origem nao reproduz a raiz/heading fisicos existentes");
            Log("CARTA_TRIAL_AUTHORED_ORIGIN", "Origin=" + origin + "; Rotation=" + orientation +
                "; ActualRoot=" + actor.Position + "; AuthoredBodyStart=" + bodyStart + "; AuthoredBodyRotation=" + bodyRotation +
                "; ActorPositionSetter=false; InitialPhase=0; NoSpeedup=true");
            if (UseS()) CreatePaper(NoteS, 1);
            if (UseB()) CreatePaper(NoteB, 2);
            BeginHeldCalibration(); modelsAt = 2; return;
        }
        if (modelsAt == 2)
        {
            // TASK/ENTITY Play podem efetivar/resetar a scene somente no Tick seguinte.
            // Escrever a pose parada uma vez depois do binding real, antes de medir grip.
            if (warmBindingPending)
            {
                if (!FinishWarmBinding()) return;
                return; // O primeiro getter estrito de Rate/Phase vem em outro Tick.
            }
            if (!HeldPoseStable(0.5f)) return;
            foreach (var paper in papers)
            {
                // PHLeftHand foi identificado nas curvas nativas119 de leitura(.25..75).
                // O grip e o centro do bounds REAL pequeno do papel, nao um offset inventado.
                GTA.Math.Vector3 hand = actor.Bones[GTA.Bone.PHLeftHand].Position, grip = PaperGrip(paper);
                Check(hand); Check(grip);
                GTA.Math.Vector3 shift = hand - grip; Check(shift);
                if (shift.Length() > 3f) throw Error("translacao medida do grip excede guard3m");
                paper.SceneOrigin += shift; Check(paper.SceneOrigin);
                paper.Scene.SetOrigin(paper.SceneOrigin, orientation, GTA.EulerRotationOrder.YXZ);
                Log("CARTA_TRIAL_GRIP_REBASE", "Phase=0.5; Hand=PHLeftHand; HandPoint=" + hand + "; BoundsCentre=" + grip +
                    "; ConstantTranslation=" + shift + "; PaperOrigin=" + paper.SceneOrigin +
                    "; SharedRotationPreserved=true; SceneSetOriginOnceDuringPreparation=true; ActorXYZSetter=false; Attachment=false; ProductionApproval=false");
            }
            ResetStability(); modelsAt = 3; return;
        }
        if (modelsAt == 3)
        {
            if (!HeldPoseStable(0.5f)) return;
            foreach (var paper in papers)
                if ((PaperGrip(paper) - actor.Bones[GTA.Bone.PHLeftHand].Position).Length() > 0.015f)
                    throw Error("translacao constante nao reproduziu o grip real depois de blend");
            // Medicao da raiz real phase.5 ja estavel, antes de voltar0. Somente preparo.
            stableReadingRootForSupport = actor.Position; Check(stableReadingRootForSupport); stableReadingRootMeasured = true;
            Log("CARTA_TRIAL_SUPPORT_PATH_ANCHOR", "Phase=.5; NativeRoot=" + stableReadingRootForSupport +
                "; InitialRoot=" + originalRoot + "; ExactBodyPlaying=true; WarmStable=true; FullPathProven=false; PreparationOnly=true");
            scene.Phase = 0f;
            foreach (var paper in papers) paper.Scene.Phase = 0f;
            ResetStability(); modelsAt = 4; return;
        }
        if (modelsAt == 4)
        {
            if (!HeldPoseStable(0f)) return;
            float rootGap = (actor.Position - originalRoot).Length(), headingGap = Angle(actor.Heading - originalHeading);
            Log("CARTA_TRIAL_RETORNO_FASE0", "Root=" + actor.Position + "; OriginalRoot=" + originalRoot +
                "; RootGap=" + Number(rootGap) + "; Heading=" + Number(actor.Heading) + "; OriginalHeading=" + Number(originalHeading) +
                "; HeadingGap=" + Number(headingGap) + "; ReturnByNativePhaseOnly=true; ActorXYZSetter=false; BeforeReadyCount=true");
            if (rootGap > 0.05f || headingGap > 0.5f) throw Error("phase0 nao retornou a raiz/heading da marca; nao teletransportar");
            if (!new GTA.Model(TableName).IsLoaded) return;
            CreateSupportFromRealFloor(); ResetStability(); modelsAt = 5; return;
        }
        if (modelsAt == 5)
        {
            int supportElapsed = unchecked((int)(GTA.Game.GameTime - warmStartedAt));
            if (supportElapsed < 0 || supportElapsed > 2500) throw Error("colisao/superficie propria da mesa nao confirmou em2.5s");
            LogTableBeforeGuard("PREPARATION_STAGE5");
            if (!OwnTable() || !ownTable.IsVisible || !ownTable.IsPositionFrozen || !ownTable.IsCollisionEnabled || ownTable.IsAttached() ||
                (ownTable.Position - tablePosition).Length() > 0.01f || RotationGap(ownTable.Rotation, tableRotation) > 0.01f)
                throw Error("mesa propria perdeu identidade/transform durante streaming fisico");
            if (!ownTable.HasPhysics) return;
            if (!MeasureInitialPaperSupport(true)) return;
            tableMeasured = true; modelsAt = 6;
        }
        RequireHeldScene(0f); RequireTable();
        if ((actor.Position - originalRoot).Length() > 0.05f || Angle(actor.Heading - originalHeading) > 0.5f)
            throw Error("atriz saiu da marca/heading antes de Ready");
        ready = true;
        Log("CARTA_TRIAL_PRONTO", "SelectedSegment=" + spec.Segmento + "; Props=" + papers.Count +
            "; CompleteWithin9=" + SelectedDurationsFit() + "; OpeningOrPocketRetrievalProven=false; NativeVideoRequired=true");
    }

    public void Start()
    {
        Active(); Story(); if (!IsReady || running || finished) throw Error("ensaio nao esta pronto para inicio unico");
        if (!SelectedDurationsFit()) throw Error("inicio recusado: tracks nao cabem integralmente em9s");
        RequireActor();
        if ((actor.Position - originalRoot).Length() > 0.05f) throw Error("atriz moveu desde preparo; remontar");
        RequirePapers();
        RequireHeldScene(0f); RequireTable();
        if (!MeasureInitialPaperSupport(true)) throw Error("plano da mesa propria perdeu ray real antes de iniciar");
        if (!tableMeasured || Angle(actor.Heading - originalHeading) > 0.5f) throw Error("apoio/retorno nativo nao confirmado antes de contagem");
        // As scenes ja estao blendadas em phase0, Rate0 desde PREPARO. Nenhuma nova tarefa/ancora na filmagem.
        scene.Rate = 1f;
        foreach (var paper in papers) paper.Scene.Rate = 1f;
        running = true; startedAt = GTA.Game.GameTime; nextLog = 0;
        Log("CARTA_TRIAL_PLAY", "Segment=" + spec.Segmento + "; SceneHandle=" + scene.Handle +
            "; BodyClip=" + Clip(spec.Segmento, 0) + "; Props=" + papers.Count + "; PlaybackRate=1; NoTimerModelSwap=true");
    }

    public void Update(int elapsedMs)
    {
        Active(); Story(); if (!running || finished) return;
        RequireActor(); RequirePapers(); RequireTable(); RequireBodyOutsideSupport();
        if (elapsedMs < lastElapsed || elapsedMs < 0) throw Error("tempo do ensaio regrediu");
        lastElapsed = elapsedMs;
        int ownElapsed = unchecked((int)(GTA.Game.GameTime - startedAt));
        if (ownElapsed < 0 || ownElapsed > 10000) throw Error("ensaio excedeu tempo real");
        if (scene == null || !scene.Exists()) throw Error("scene local foi interrompida");
        if ((actor.Position - originalRoot).Length() > 3f) throw Error("track corporal deslocou atriz fora dos3m tecnicos");
        float phase = scene.Phase; CheckPhase(phase);
        float bodyRate = scene.Rate; Check(bodyRate);
        if (System.Math.Abs(bodyRate - 1f) > 0.001f) throw Error("track corporal acelerado ou paralisado");
        bool bodyPlaying = Playing(actorHandle, 0);
        // O progresso do binding sincronizado vem da SUA scene, com clipe exato tocando.
        // GET_ENTITY_ANIM_CURRENT_TIME permanece diagnostico separado; neste binding retornou0.
        float bodyEntityPhase = bodyPlaying ? Current(actorHandle, 0) : 0f; CheckPhase(bodyEntityPhase);
        float bodyPhase = bodyPlaying ? phase : bodyMaximum;
        if (bodyPlaying)
        {
            if (bodyPhase + 0.001f < bodyMaximum) throw Error("phase corporal propria regrediu");
            bodyObserved |= bodyPhase > bodyMaximum + 0.0001f;
            bodyMaximum = System.Math.Max(bodyMaximum, bodyPhase);
        }
        if (!bodyPlaying && ownElapsed >= 750 && bodyMaximum < 0.98f) throw Error("track corporal nao tocou/progrediu");
        foreach (var paper in papers)
        {
            if (paper.Scene == null || !paper.Scene.Exists()) throw Error("scene propria do papel interrompida");
            CheckPhase(paper.Scene.Phase);
            float paperRate = paper.Scene.Rate;
            Check(paperRate);
            if (System.Math.Abs(paperRate - 1f) > 0.001f) throw Error("track proprio do papel acelerado ou paralisado");
            if ((paper.Entity.Position - actor.Position).Length() > 3f) throw Error("track do papel saiu dos3m tecnicos; rebase nao autoriza deslocamento distante");
            bool playing = Playing(paper.Handle, paper.Track);
            float current = playing ? paper.Scene.Phase : paper.MaximumPhase;
            if (playing)
            {
                CheckPhase(current);
                if (current + 0.001f < paper.MaximumPhase) throw Error("phase propria do papel regrediu");
                paper.Observed |= current > paper.MaximumPhase + 0.0001f;
                paper.MaximumPhase = System.Math.Max(paper.MaximumPhase, current);
                float handDistance = System.Math.Min((paper.Entity.Position - actor.Bones[GTA.Bone.PHRightHand].Position).Length(),
                    (paper.Entity.Position - actor.Bones[GTA.Bone.PHLeftHand].Position).Length());
                Check(handDistance);
                paper.MinimumHandDistance = System.Math.Min(paper.MinimumHandDistance, handDistance);
                if (current >= 0.05f && current <= 0.95f && handDistance <= 0.35f) paper.HandContactObserved = true;
                // Telemetria continua da leitura nao equivale a gosto/aprovacao e nao substitui imagem.
                if (current >= 0.25f && current <= 0.75f)
                {
                    paper.ReadingContactSamples++;
                    paper.ReadingMaximumHandDistance = System.Math.Max(paper.ReadingMaximumHandDistance, handDistance);
                    if (handDistance > 0.35f) paper.ReadingContactFailures++;
                }
                // Antes da retirada authored, a folha ainda deve estar no tampo real proprio.
                if (current <= 0.16f && !MeasureInitialPaperSupport(false)) throw Error("papel perdeu ray de apoio real antes da retirada authored");
            }
            if (!playing && ownElapsed >= 750 && paper.MaximumPhase < 0.98f)
                throw Error("track do papel nao tocou/progrediu: " + Clip(spec.Segmento, paper.Track));
        }
        if (elapsedMs >= nextLog)
        {
            nextLog = elapsedMs + 250;
            Log("CARTA_TRIAL_FRAME", "Elapsed=" + elapsedMs + "; OwnElapsed=" + ownElapsed + "; ScenePhase=" + Number(phase) +
                "; SceneRate=" + Number(bodyRate) + "; BodyPlaying=" + bodyPlaying + "; BodyPhase=" + Number(bodyPhase) +
                "; BodyEntityAnimPhase=" + Number(bodyEntityPhase) + "; PhaseSource=OwnedFwSyncedSceneWithExactClipPlaying" +
                "; Root=" + actor.Position + "; RightHand=" + actor.Bones[GTA.Bone.PHRightHand].Position +
                "; LeftHand=" + actor.Bones[GTA.Bone.PHLeftHand].Position + "; Health=" + actor.Health + "; ProductionApproval=false");
            foreach (var paper in papers) LogPaper(paper);
        }
        if (elapsedMs >= spec.JanelaMs)
        {
            TracksCompletos = SelectedDurationsFit() && bodyObserved && bodyMaximum >= 0.98f &&
                ownElapsed >= (int)System.Math.Ceiling(durations[spec.Segmento, 0] * 1000d);
            foreach (var paper in papers) TracksCompletos &= paper.Observed && paper.MaximumPhase >= 0.98f &&
                ownElapsed >= (int)System.Math.Ceiling(durations[spec.Segmento, paper.Track] * 1000d);
            finished = true;
            Log("CARTA_TRIAL_FIM", "Elapsed=" + elapsedMs + "; FullTracksObserved=" + TracksCompletos +
                "; BodyMaxPhase=" + Number(bodyMaximum) + "; OwnElapsed=" + ownElapsed +
                "; PhaseSource=OwnedFwSyncedSceneWithExactClipPlaying; FullRealDurationRequired=true" +
                "; OpeningVisualConfirmed=false; PocketRetrievalConfirmed=false" +
                "; IncompleteTrackIsNotSceneCompletion=true; TrialOnly=true; ProductionApproval=false");
        }
    }

    // Caller chama antes de remover seus atores; conserva helper se false. Nunca repoe XYZ do ator.
    public bool Cleanup()
    {
        if (disposed) return true;
        if (prepared && OwnActor())
        {
            // Este ped e integralmente proprio do chamador e a primaria pertence ao ensaio.
            if (ownTaskStarted && Playing(actorHandle, 0)) actor.Task.ClearAll();
            else if (ownTaskStarted && running && !finished && actor.CurrentScriptTaskStatus == GTA.ScriptTaskStatus.Performing) return false;
            actor.IsPositionFrozen = wasFrozen;
        }
        ownTaskStarted = false; warmBindingPending = false;
        if (OwnTable())
        {
            ownTable.Delete();
            if (ownTable.Exists()) return false;
        }
        ownTable = null; tableMeasured = false;
        for (int i = papers.Count - 1; i >= 0; i--)
        {
            var paper = papers[i];
            if (!OwnPaper(paper)) { papers.RemoveAt(i); continue; }
            if (Playing(paper.Handle, paper.Track) && !paper.Entity.StopSynchronizedAnim(new GTA.AnimationBlendDelta(8f), false)) return false;
            paper.Entity.Delete();
            if (paper.Entity.Exists()) return false;
            if (paper.Scene != null && paper.Scene.Exists()) paper.Scene.HoldsLastFrame = false;
            paper.Scene = null; papers.RemoveAt(i);
        }
        scene = null;
        for (int i = 0; i < 7; i++) if (dictRequested[i] && !dictWasLoaded[i])
        { new GTA.CrClipDictionary(Dictionary(i)).MarkAsNoLongerNeeded(); dictRequested[i] = false; }
        if (modelSRequested && !modelSWasLoaded) new GTA.Model(NoteS).MarkAsNoLongerNeeded();
        if (modelBRequested && !modelBWasLoaded) new GTA.Model(NoteB).MarkAsNoLongerNeeded();
        if (tableModelRequested && !tableModelWasLoaded) new GTA.Model(TableName).MarkAsNoLongerNeeded();
        modelSRequested = modelBRequested = tableModelRequested = false;
        running = ready = prepared = false;
        return true;
    }
    public void Dispose() { if (!Cleanup()) throw Error("limpeza propria pendente; conservar instancia"); disposed = true; }

    // Diagnostico107: cada segmento usa seu proprio body para derivar a origem.
    // Getters somente; nenhum modelo/cena/ator e criado, reposicionado ou aprovado.
    private void InventoryInitialOffsets(int segment)
    {
        try
        {
            GTA.Math.Vector3 localRotation = TrialInitialRotation(segment, Clip(segment, 0), GTA.Math.Vector3.Zero, GTA.Math.Vector3.Zero);
            Check(localRotation);
            GTA.Math.Vector3 derivedRotation = new GTA.Math.Vector3(0f, 0f, actor.Heading - localRotation.Z);
            GTA.Math.Vector3 bodyOffset = TrialInitialPosition(segment, Clip(segment, 0), GTA.Math.Vector3.Zero, derivedRotation);
            Check(bodyOffset);
            GTA.Math.Vector3 derivedOrigin = originalRoot - bodyOffset;
            Check(derivedOrigin);
            bool planarBody = System.Math.Abs(localRotation.X) <= 0.1f && System.Math.Abs(localRotation.Y) <= 0.1f;
            for (int track = 0; track < 3; track++)
            {
                GTA.Math.Vector3 start = TrialInitialPosition(segment, Clip(segment, track), derivedOrigin, derivedRotation);
                GTA.Math.Vector3 rotation = TrialInitialRotation(segment, Clip(segment, track), derivedOrigin, derivedRotation);
                Check(start); Check(rotation);
                GTA.Math.Vector3 delta = start - originalRoot;
                float distance = delta.Length();
                float xy = (float)System.Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
                Log("CARTA_TRIAL_INITIAL_OFFSET", "Segment=" + segment + "; Dictionary=" + Dictionary(segment) +
                    "; Clip=" + Clip(segment, track) + "; Track=" + track + "; InitialPhase=0" +
                    "; Root=" + originalRoot + "; DerivedOrigin=" + derivedOrigin + "; DerivedRotation=" + derivedRotation +
                    "; BodyLocalRotation=" + localRotation + "; BodyPlanar=" + planarBody +
                    "; InitialPosition=" + start + "; InitialRotation=" + rotation + "; DeltaRoot=" + delta +
                    "; DistanceRoot=" + Number(distance) + "; DistanceXY=" + Number(xy) +
                    "; WithinSelected3mGuard=" + (distance <= 3f) + "; SelectedSegment=" + (segment == spec.Segmento) +
                    "; ActorPositionSetter=false; BeforeCreate=true; ProductionApproval=false");
            }
        }
        catch (System.Exception ex)
        {
            // Unselected-track diagnostics do not alter the existing selected-track guards.
            Log("CARTA_TRIAL_INITIAL_OFFSET_DIAGNOSTIC_ERROR", "Segment=" + segment + "; Error=" + ex.Message +
                "; SelectedTrackGuardsUnchanged=true; ProductionApproval=false");
        }
    }
    private void InventoryLoadedBounds(string name)
    {
        var model = new GTA.Model(name);
        if (!model.IsLoaded)
        {
            Log("CARTA_TRIAL_MODEL_BOUNDS", "Model=" + name + "; Loaded=false; DimensionsQueried=false; ProductionApproval=false");
            return;
        }
        try
        {
            GTA.Math.Vector3 minimum, maximum;
            model.GetDimensions(out minimum, out maximum); Check(minimum); Check(maximum);
            Log("CARTA_TRIAL_MODEL_BOUNDS", "Model=" + name + "; Loaded=true; Min=" + minimum + "; Max=" + maximum +
                "; DimensionsQueried=true; EntityCreated=false; MeshSemanticProof=false; ProductionApproval=false");
        }
        catch (System.Exception ex)
        { Log("CARTA_TRIAL_MODEL_BOUNDS_DIAGNOSTIC_ERROR", "Model=" + name + "; Error=" + ex.Message + "; ProductionApproval=false"); }
    }
    private GTA.Math.Vector3 TrialInitialPosition(int segment, string clip, GTA.Math.Vector3 root, GTA.Math.Vector3 rotation)
    { return GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ANIM_INITIAL_OFFSET_POSITION, Dictionary(segment), clip,
        root.X, root.Y, root.Z, rotation.X, rotation.Y, rotation.Z, 0f, 2); }
    private GTA.Math.Vector3 TrialInitialRotation(int segment, string clip, GTA.Math.Vector3 root, GTA.Math.Vector3 rotation)
    { return GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ANIM_INITIAL_OFFSET_ROTATION, Dictionary(segment), clip,
        root.X, root.Y, root.Z, rotation.X, rotation.Y, rotation.Z, 0f, 2); }

    private void CreatePaper(string name, int track)
    {
        if (!ValidDuration(durations[spec.Segmento, track])) throw Error("track objeto selecionado sem duracao");
        // Ensaio de translacao CONSTANTE da track completa para a mao REAL; nao e attachment.
        GTA.Math.Vector3 authoredStart = InitialPosition(Clip(spec.Segmento, track), origin, orientation);
        GTA.Math.Vector3 hand = actor.Bones[GTA.Bone.PHRightHand].Position;
        Check(authoredStart); Check(hand);
        if ((hand - originalRoot).Length() > 3f) throw Error("mao inicial fora dos3m tecnicos");
        GTA.Math.Vector3 paperOrigin = origin + hand - authoredStart; Check(paperOrigin);
        GTA.Math.Vector3 start = InitialPosition(Clip(spec.Segmento, track), paperOrigin, orientation);
        GTA.Math.Vector3 rotation = InitialRotation(Clip(spec.Segmento, track), paperOrigin, orientation);
        Check(start); Check(rotation);
        if ((start - hand).Length() > 0.01f) throw Error("origem propria nao reproduz ancora real da mao");
        if ((start - originalRoot).Length() > 3f) throw Error("inicio authored do papel fora dos3m tecnicos");
        Log("CARTA_TRIAL_OBJECT_ORIGIN", "Track=" + Clip(spec.Segmento, track) + "; BodyOrigin=" + origin +
            "; AuthoredInitial=" + authoredStart + "; ActualInitialHand=" + hand + "; PaperSceneOrigin=" + paperOrigin +
            "; RebasedInitial=" + start + "; DeltaConstant=" + (paperOrigin - origin) +
            "; SharedBodyOriginRejected=true; PerFramePositionSetter=false; Attachment=false; OriginalRelativeTrajectoryPreserved=true; Experimental=true; ProductionApproval=false");
        GTA.Prop entity = GTA.Prop.CreateNoOffset(new GTA.Model(name), start, rotation, true);
        if (entity == null || !entity.Exists()) throw Error("modelo real nao foi criado: " + name);
        var paper = new Paper { Entity = entity, Handle = entity.Handle, Model = entity.Model.Hash, Address = entity.MemoryAddress, Track = track, SceneOrigin = paperOrigin };
        papers.Add(paper); entity.IsPositionFrozen = true; entity.IsCollisionEnabled = false;
        entity.Model.GetDimensions(out paper.Minimum, out paper.Maximum); Check(paper.Minimum); Check(paper.Maximum);
        if (!entity.HasDrawable) throw Error("objeto sem drawable para animar");
        Log("CARTA_TRIAL_MODEL_CREATED", "Model=" + name + "; Handle=" + paper.Handle + "; Track=" + Clip(spec.Segmento, track) +
            "; InitialPosition=" + start + "; InitialRotation=" + rotation + "; Min=" + paper.Minimum + "; Max=" + paper.Maximum +
            "; Drawable=" + entity.HasDrawable + "; HasSkeletonBeforePlay=" + entity.HasSkeleton + "; BoneCountBeforePlay=" + entity.Bones.Count +
            "; VertexMeshNotAccessibleThroughAPI=true; NoFoldClaim=true");
    }
    private void BeginHeldCalibration()
    {
        scene = GTA.FwSyncedScene.Create(origin, orientation, GTA.EulerRotationOrder.YXZ);
        if (scene == null) throw Error("scene corporal propria recusada no preparo");
        actor.IsPositionFrozen = false; ownTaskStarted = true; warmBindingPending = true;
        actor.Task.PlaySynchronizedScene(scene, new GTA.CrClipAsset(Dictionary(spec.Segmento), Clip(spec.Segmento, 0)),
            new GTA.AnimationBlendDelta(8f), new GTA.AnimationBlendDelta(8f),
            GTA.SyncedSceneFlags.AbortOnDeath | GTA.SyncedSceneFlags.OnAbortStopScene | GTA.SyncedSceneFlags.UseKinematicPhysics,
            (GTA.RagdollBlockingFlags)0, new GTA.AnimationBlendDelta(1.5f), (GTA.AnimationIKControlFlags)0);
        foreach (var paper in papers)
        {
            paper.Scene = GTA.FwSyncedScene.Create(paper.SceneOrigin, orientation, GTA.EulerRotationOrder.YXZ);
            if (paper.Scene == null) throw Error("scene propria do papel recusada no preparo");
            paper.Entity.IsPositionFrozen = false;
            if (!paper.Entity.PlaySynchronizedAnim(paper.Scene, new GTA.CrClipAsset(Dictionary(spec.Segmento), Clip(spec.Segmento, paper.Track)),
                new GTA.AnimationBlendDelta(8f), new GTA.AnimationBlendDelta(8f), (GTA.SyncedSceneFlags)0, new GTA.AnimationBlendDelta(1.5f)))
                throw Error("track papel recusado no preparo");
        }
        warmBindingStartedAt = GTA.Game.GameTime; warmBindingNextLogAt = 0;
        ResetStability();
        LogHeldSceneFields("AFTER_NATIVE_PLAY_CALLS", 0.5f);
        Log("CARTA_TRIAL_WARM_BEGIN", "PhaseRequested=0.5; RateRequested=0; RatePhaseWritten=false; AwaitExactNativeBinding=true; OwnBodyTask=true; UseKinematicPhysics=true; OwnPaperScene=true; PreparationOnly=true; NoBodyPositionSetter=true");
    }
    private bool FinishWarmBinding()
    {
        if (!warmBindingPending || modelsAt != 2 || running || ready) throw Error("binding pendente exclusivo do preparo");
        RequireActor(); RequirePapers();
        if (scene == null || !scene.Exists()) throw Error("scene propria corporal perdida antes do binding");
        foreach (var paper in papers)
            if (paper.Scene == null || !paper.Scene.Exists()) throw Error("scene propria papel perdida antes do binding");
        int elapsed = unchecked((int)(GTA.Game.GameTime - warmBindingStartedAt));
        if (elapsed < 0 || elapsed > 1000)
        {
            LogHeldSceneFields("BINDING_TIMEOUT", 0.5f);
            throw Error("clips exatos/physics corporal nao confirmaram em outro Tick dentro de1s");
        }
        bool exactPlaying = Playing(actorHandle, 0) && !papers.Exists(delegate(Paper paper) { return !Playing(paper.Handle, paper.Track); });
        bool physicsReady = actor.HasPhysics && actor.IsCollisionEnabled;
        if (elapsed >= warmBindingNextLogAt || (elapsed > 0 && exactPlaying && physicsReady))
        {
            warmBindingNextLogAt = elapsed + 100;
            LogHeldSceneFields("AWAIT_NATIVE_BINDING", 0.5f);
        }
        if (elapsed == 0 || !exactPlaying || !physicsReady) return false;
        // Configurar DEPOIS de ambos os Play terem chegado aos clipes exatos reais.
        // Nenhuma reaplicacao por frame: falha posterior de Phase/Rate continua abortando.
        scene.HoldsLastFrame = true; scene.IsLooped = false; scene.Rate = 0f; scene.Phase = 0.5f;
        foreach (var paper in papers)
        {
            paper.Scene.HoldsLastFrame = true; paper.Scene.IsLooped = false; paper.Scene.Rate = 0f; paper.Scene.Phase = 0.5f;
        }
        warmBindingPending = false; ResetStability();
        LogHeldSceneFields("AFTER_HELD_WRITE_ONCE", 0.5f);
        Log("CARTA_TRIAL_WARM_HELD_ONCE", "BindingElapsedMs=" + elapsed + "; ExactBodyAndPaperPlaying=true; ActorPhysics=true; ActorCollision=true; Phase=0.5; Rate=0; HeldSetOnceAfterNativeBinding=true; StrictGuardNextTick=true; PreparationOnly=true");
        return true;
    }
    private void LogHeldSceneFields(string stage, float expectedPhase)
    {
        bool bodyExists = scene != null && scene.Exists();
        Log("CARTA_TRIAL_HELD_SCENE_GETTERS", "Stage=" + stage + "; PreparationStage=" + modelsAt +
            "; ExpectedPhase=" + Number(expectedPhase) + "; BodySceneExists=" + bodyExists +
            "; BodyScenePhase=" + (bodyExists ? Number(scene.Phase) : "absent") +
            "; BodySceneRate=" + (bodyExists ? Number(scene.Rate) : "absent") +
            "; SceneExistsViaNativeRunningName=" + bodyExists +
            "; BodyExactPlaying=" + Playing(actorHandle, 0) +
            "; ActorPhysics=" + actor.HasPhysics + "; ActorCollision=" + actor.IsCollisionEnabled +
            "; ActorFrozen=" + actor.IsPositionFrozen + "; AwaitBinding=" + warmBindingPending + "; GettersBeforeGuard=true");
        foreach (var paper in papers)
        {
            bool exists = paper.Scene != null && paper.Scene.Exists();
            Log("CARTA_TRIAL_HELD_PAPER_GETTERS", "Stage=" + stage + "; Track=" + Clip(spec.Segmento, paper.Track) +
                "; Handle=" + paper.Handle + "; SceneExists=" + exists +
                "; ScenePhase=" + (exists ? Number(paper.Scene.Phase) : "absent") +
                "; SceneRate=" + (exists ? Number(paper.Scene.Rate) : "absent") +
                "; SceneExistsViaNativeRunningName=" + exists +
                "; ExactPlaying=" + Playing(paper.Handle, paper.Track) + "; Physics=" + paper.Entity.HasPhysics +
                "; Collision=" + paper.Entity.IsCollisionEnabled + "; Frozen=" + paper.Entity.IsPositionFrozen + "; GettersBeforeGuard=true");
        }
    }
    private void ResetStability()
    { warmStartedAt = GTA.Game.GameTime; stableAt = -1; stableCount = 0; }
    private bool HeldPoseStable(float expectedPhase)
    {
        RequireHeldScene(expectedPhase); RequireActor(); RequirePapers();
        int elapsed = unchecked((int)(GTA.Game.GameTime - warmStartedAt));
        if (elapsed < 0 || elapsed > 2500) throw Error("pose parada nao confirmou blend/getters estaveis em2.5s");
        if (elapsed < 750 || (stableAt >= 0 && elapsed - stableAt < 100)) return false;
        GTA.Math.Vector3 root = actor.Position, hand = actor.Bones[GTA.Bone.PHLeftHand].Position, grip = PaperGrip(papers[0]);
        Check(root); Check(hand); Check(grip); Check(actor.Heading);
        if (stableAt >= 0 && (root - previousRoot).Length() <= 0.01f && (hand - previousLeftHand).Length() <= 0.01f &&
            (grip - previousPaperGrip).Length() <= 0.01f && Angle(actor.Heading - previousHeading) <= 0.5f) stableCount++;
        else stableCount = 0;
        stableAt = elapsed; previousRoot = root; previousLeftHand = hand; previousPaperGrip = grip; previousHeading = actor.Heading;
        Log("CARTA_TRIAL_WARM_STABILITY", "Stage=" + modelsAt + "; Phase=" + Number(expectedPhase) + "; WaitMs=" + elapsed +
            "; StableSamples=" + stableCount + "; Root=" + root + "; Heading=" + Number(actor.Heading) + "; LeftHand=" + hand + "; PaperGrip=" + grip +
            "; ActorPhysics=" + actor.HasPhysics + "; ActorCollision=" + actor.IsCollisionEnabled + "; ActorFrozen=" + actor.IsPositionFrozen +
            "; BodyEntityPhase=" + Number(Current(actorHandle, 0)) + "; ExactBodyPlaying=" + Playing(actorHandle, 0) + "; ProductionApproval=false");
        return stableCount >= 3;
    }
    private void RequireHeldScene(float expectedPhase)
    {
        LogHeldSceneFields("BEFORE_STRICT_HELD_GUARD", expectedPhase);
        if (scene == null || !scene.Exists()) throw Error("scene propria corporal perdida durante preparo/contagem");
        float bodyPhase = scene.Phase, bodyRate = scene.Rate; CheckPhase(bodyPhase); Check(bodyRate);
        if (System.Math.Abs(bodyPhase - expectedPhase) > 0.001f || System.Math.Abs(bodyRate) > 0.001f)
            throw Error("scene corporal nao esta parada na phase exigida");
        foreach (var paper in papers)
        {
            if (paper.Scene == null || !paper.Scene.Exists()) throw Error("scene propria papel perdida durante preparo/contagem");
            float phase = paper.Scene.Phase, rate = paper.Scene.Rate; CheckPhase(phase); Check(rate);
            if (System.Math.Abs(phase - expectedPhase) > 0.001f || System.Math.Abs(rate) > 0.001f || System.Math.Abs(phase - bodyPhase) > 0.001f)
                throw Error("paper/body nao compartilham a pose parada exigida");
        }
        int elapsed = unchecked((int)(GTA.Game.GameTime - warmStartedAt));
        if (elapsed >= 500 && (!Playing(actorHandle, 0) || papers.Exists(delegate(Paper paper) { return !Playing(paper.Handle, paper.Track); })))
            throw Error("pose warm nao toca os clips exatos carregados");
    }
    private GTA.Math.Vector3 PaperGrip(Paper paper)
    {
        GTA.Math.Vector3 value = paper.Entity.GetOffsetPosition((paper.Minimum + paper.Maximum) * 0.5f); Check(value); return value;
    }
    private void CreateSupportFromRealFloor()
    {
        if (papers.Count != 1 || ownTable != null) throw Error("mesa exige exatamente um papel proprio");
        Paper paper = papers[0]; GTA.Math.Vector3 point = PaperGrip(paper); float floor;
        if (!MapFloor(point, originalRoot.Z + 0.25f, originalRoot.Z - 3f, actor, out floor))
            throw Error("piso real sob papel inicial nao encontrado; sem mesa suspensa");
        var model = new GTA.Model(TableName); model.GetDimensions(out tableMinimum, out tableMaximum); Check(tableMinimum); Check(tableMaximum);
        if (tableMaximum.X <= tableMinimum.X || tableMaximum.Y <= tableMinimum.Y || tableMaximum.Z <= tableMinimum.Z)
            throw Error("bounds reais da mesa invalidos");
        if (!stableReadingRootMeasured || running || ready) throw Error("apoio exige raiz authored realphase.5 medida somente no preparo");
        // EixoY aponta da raiz de leitura para a folha inicial. O lado estreito
        // da bancada fica perpendicular a aproximacao, sem mudar corpo/papel.
        GTA.Math.Vector3 away = point - stableReadingRootForSupport; away.Z = 0f; Check(away);
        float planarDistance = away.Length(); Check(planarDistance);
        if (planarDistance <= 0.01f) throw Error("raiz de leitura e folha nao permitem orientacao de apoio separada");
        away /= planarDistance;
        GTA.Math.Vector3 right = new GTA.Math.Vector3(away.Y, -away.X, 0f); Check(right);
        float lowX = float.MaxValue, highX = float.MinValue, lowY = float.MaxValue, highY = float.MinValue;
        for (int corner = 0; corner < 8; corner++)
        {
            GTA.Math.Vector3 localCorner = new GTA.Math.Vector3((corner & 1) == 0 ? paper.Minimum.X : paper.Maximum.X,
                (corner & 2) == 0 ? paper.Minimum.Y : paper.Maximum.Y, (corner & 4) == 0 ? paper.Minimum.Z : paper.Maximum.Z);
            GTA.Math.Vector3 delta = paper.Entity.GetOffsetPosition(localCorner) - point; Check(delta);
            float px = delta.X * right.X + delta.Y * right.Y, py = delta.X * away.X + delta.Y * away.Y;
            lowX = System.Math.Min(lowX, px); highX = System.Math.Max(highX, px);
            lowY = System.Math.Min(lowY, py); highY = System.Math.Max(highY, py);
        }
        // Margem1cm e a tolerancia do transform propria ja existente. A folha
        // inteira em seus bounds nativos precisa caber dentro da bancada.
        float targetX = (tableMinimum.X + tableMaximum.X) * 0.5f;
        float targetY = tableMinimum.Y - lowY + 0.01f;
        if (targetX + lowX < tableMinimum.X + 0.01f || targetX + highX > tableMaximum.X - 0.01f ||
            targetY + lowY < tableMinimum.Y || targetY + highY > tableMaximum.Y - 0.01f)
            throw Error("bounds reais da folha nao cabem inteiros no apoio separado; nao reduzir guarda corporal");
        float heading = (float)(System.Math.Atan2(right.Y, right.X) * 180.0 / System.Math.PI); Check(heading);
        tableRotation = new GTA.Math.Vector3(0f, 0f, heading);
        tablePosition = new GTA.Math.Vector3(point.X - right.X * targetX - away.X * targetY,
            point.Y - right.Y * targetX - away.Y * targetY, floor - tableMinimum.Z);
        RequireKnownSupportRootOutside(originalRoot, right, away, "phase0");
        RequireKnownSupportRootOutside(stableReadingRootForSupport, right, away, "phase.5");
        Log("CARTA_TRIAL_SUPPORT_LAYOUT", "Method=paper_initial_native8bounds_and_stable_body_root_phase.5; Away=" + away +
            "; Right=" + right + "; NativePaperLowX=" + Number(lowX) + "; NativePaperHighX=" + Number(highX) +
            "; NativePaperLowY=" + Number(lowY) + "; NativePaperHighY=" + Number(highY) +
            "; PaperTargetX=" + Number(targetX) + "; PaperTargetY=" + Number(targetY) + "; PlanarRootPaperDistance=" + Number(planarDistance) +
            "; PlacementDerived=true; PositionSetter=false; InitialPaperMoved=false; BodyMoved=false; FullPathProven=false; PerFrameBodyFootprintGuard=true; PreparationOnly=true");
        Check(tablePosition); Check(tableRotation);
        if ((tablePosition - originalRoot).Length() > 3f) throw Error("mesa propria derivada saiu dos3m tecnicos");
        ownTable = GTA.Prop.CreateNoOffset(model, tablePosition, tableRotation, true);
        if (ownTable == null || !ownTable.Exists()) throw Error("mesa propria nao criada");
        tableHandle = ownTable.Handle; tableAddress = ownTable.MemoryAddress; tableModel = ownTable.Model.Hash;
        ownTable.IsPositionFrozen = true; ownTable.IsCollisionEnabled = true; ownTable.IsInvincible = true;
        Log("CARTA_TRIAL_TABLE_CREATED", "Model=" + TableName + "; Handle=" + tableHandle + "; Position=" + tablePosition + "; Rotation=" + tableRotation +
            "; Min=" + tableMinimum + "; Max=" + tableMaximum + "; Floor=" + Number(floor) + "; InitialPaperGrip=" + point +
            "; FloorDerivedNotPaperHeight=true; RealOwnSurfaceStillRequired=true; ModelCollisionLoaded=" + model.IsCollisionLoaded + "; ProductionApproval=false");
    }
    private void RequireTable()
    {
        LogTableBeforeGuard("REQUIRE_TABLE");
        if (!OwnTable() || !ownTable.IsVisible || !ownTable.IsPositionFrozen || !ownTable.IsCollisionEnabled || !ownTable.HasPhysics || ownTable.IsAttached() ||
            (ownTable.Position - tablePosition).Length() > 0.01f || RotationGap(ownTable.Rotation, tableRotation) > 0.01f)
            throw Error("mesa propria perdeu identidade/transform/physics");
    }
    // Euler getters podem representar325graus como-35graus. Conservar0.01grau
    // de tolerancia vetorial; normalizar apenas a equivalencia de cada angulo.
    private static float RotationGap(GTA.Math.Vector3 actual, GTA.Math.Vector3 expected)
    {
        Check(actual); Check(expected);
        return new GTA.Math.Vector3(Angle(actual.X - expected.X), Angle(actual.Y - expected.Y), Angle(actual.Z - expected.Z)).Length();
    }
    private void LogTableBeforeGuard(string stage)
    {
        bool exists = ownTable != null && ownTable.Exists();
        if (!exists)
        {
            Log("CARTA_TRIAL_TABLE_BEFORE_GUARD", "Stage=" + stage + "; Exists=false; ExpectedHandle=" + tableHandle + "; GettersOnly=true");
            return;
        }
        GTA.Math.Vector3 actualPosition = ownTable.Position, actualRotation = ownTable.Rotation;
        Check(actualPosition); Check(actualRotation);
        Log("CARTA_TRIAL_TABLE_BEFORE_GUARD", "Stage=" + stage + "; Exists=true; OwnIdentity=" + OwnTable() +
            "; Handle=" + ownTable.Handle + "; ExpectedHandle=" + tableHandle +
            "; ModelHash=" + ownTable.Model.Hash + "; ExpectedModelHash=" + tableModel +
            "; Address=" + ownTable.MemoryAddress + "; ExpectedAddress=" + tableAddress +
            "; Visible=" + ownTable.IsVisible + "; Frozen=" + ownTable.IsPositionFrozen +
            "; Collision=" + ownTable.IsCollisionEnabled + "; HasPhysics=" + ownTable.HasPhysics +
            "; Attached=" + ownTable.IsAttached() + "; ModelCollisionLoaded=" + ownTable.Model.IsCollisionLoaded +
            "; Position=" + actualPosition + "; ExpectedPosition=" + tablePosition +
            "; PositionGap=" + Number((actualPosition - tablePosition).Length()) +
            "; Rotation=" + actualRotation + "; ExpectedRotation=" + tableRotation +
            "; RawRotationGap=" + Number((actualRotation - tableRotation).Length()) +
            "; NormalizedRotationGap=" + Number(RotationGap(actualRotation, tableRotation)) +
            "; PositionLimit=0.01m; RotationLimit=0.01deg; GettersOnly=true; BeforeTransformGuard=true; ProductionApproval=false");
    }
    private bool OwnTable()
    { return ownTable != null && ownTable.Exists() && ownTable.Handle == tableHandle && ownTable.MemoryAddress == tableAddress && ownTable.Model.Hash == tableModel; }
    private bool MeasureInitialPaperSupport(bool verifyFloor)
    {
        RequireTable(); RequirePapers();
        foreach (var paper in papers)
        {
            GTA.Math.Vector3 bottom = paper.Entity.GetOffsetPosition(new GTA.Math.Vector3(
                (paper.Minimum.X + paper.Maximum.X) * 0.5f, (paper.Minimum.Y + paper.Maximum.Y) * 0.5f, paper.Minimum.Z));
            GTA.Math.Vector3 local = ownTable.GetPositionOffset(bottom); Check(bottom); Check(local);
            GTA.ShapeTestResult hit;
            var ray = GTA.ShapeTest.StartExpensiveSyncTestLOSProbe(bottom + new GTA.Math.Vector3(0f, 0f, 0.35f),
                bottom - new GTA.Math.Vector3(0f, 0f, 0.5f), GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, actor, (GTA.ShapeTestOptions)0);
            GTA.Entity entity;
            bool ownHit = ray.GetResult(out hit) == GTA.ShapeTestStatus.Ready && hit.DidHit && hit.TryGetHitEntity(out entity) &&
                entity != null && entity.Handle == tableHandle && entity.MemoryAddress == tableAddress;
            float gap = float.NaN;
            if (ownHit) { Check(hit.HitPosition); Check(hit.SurfaceNormal); gap = bottom.Z - hit.HitPosition.Z; tableSurface = hit.HitPosition; }
            bool plane = ownHit && hit.SurfaceNormal.Z >= 0.98f && gap >= -0.02f && gap <= 0.08f &&
                local.X >= tableMinimum.X && local.X <= tableMaximum.X && local.Y >= tableMinimum.Y && local.Y <= tableMaximum.Y;
            Log("CARTA_TRIAL_INITIAL_SUPPORT", "Phase=" + Number(paper.Scene.Phase) + "; Bottom=" + bottom + "; OwnHit=" + ownHit +
                "; Surface=" + (ownHit ? tableSurface.ToString() : "not_measured") + "; Gap=" + Number(gap) + "; Plane=" + plane +
                "; PaperLocal=" + local + "; TablePosition=" + ownTable.Position + "; TableMin=" + tableMinimum + "; TableMax=" + tableMaximum +
                "; HasPhysics=" + ownTable.HasPhysics + "; NoBoundsMaximumAsSurface=true; ProductionApproval=false");
            if (!ownHit) return false; // Somente PREPARO aguarda streaming real limitado; acao/inicio rejeitam false.
            if (!plane) throw Error("papel inicial nao apoia no plano REAL da mesa propria; nao suspender mesa/mover atriz para disfarcar");
        }
        // A mesa e fixa e seu transform e verificado por frame; cinco probes de piso so no preparo/inicio.
        if (verifyFloor) for (int i = 0; i < 5; i++)
        {
            float x = (tableMinimum.X + tableMaximum.X) * 0.5f, y = (tableMinimum.Y + tableMaximum.Y) * 0.5f;
            if (i > 0) { x += (i == 1 || i == 2 ? -1f : 1f) * (tableMaximum.X - tableMinimum.X) * 0.45f;
                y += (i == 1 || i == 3 ? -1f : 1f) * (tableMaximum.Y - tableMinimum.Y) * 0.45f; }
            GTA.Math.Vector3 bottom = ownTable.GetOffsetPosition(new GTA.Math.Vector3(x, y, tableMinimum.Z)); float floor;
            if (!MapFloor(bottom, bottom.Z + 0.1f, bottom.Z - 0.35f, ownTable, out floor) || System.Math.Abs(bottom.Z - floor) > 0.08f)
                throw Error("bounds inferiores da mesa sem piso real plano; nao aceitar mesa flutuante/enterrada");
        }
        RequireBodyOutsideSupport();
        return true;
    }
    // Mesma guarda existente0.15m, agora durante toda a acao, nao so retirada<=.16.
    private void RequireBodyOutsideSupport()
    {
        if (!OwnTable()) throw Error("apoio proprio ausente antes da guarda corporal");
        GTA.Math.Vector3 rootLocal = ownTable.GetPositionOffset(actor.Position); Check(rootLocal);
        if (rootLocal.X > tableMinimum.X - 0.15f && rootLocal.X < tableMaximum.X + 0.15f &&
            rootLocal.Y > tableMinimum.Y - 0.15f && rootLocal.Y < tableMaximum.Y + 0.15f)
        {
            Log("CARTA_TRIAL_BODY_FOOTPRINT_FAIL", "Root=" + actor.Position + "; RootLocal=" + rootLocal +
                "; TablePosition=" + tablePosition + "; TableRotation=" + tableRotation + "; Min=" + tableMinimum +
                "; Max=" + tableMaximum + "; ExistingExpansion=.15; PerFrame=true; NoSetter=true");
            throw Error("root corporal invade footprint da mesa propria");
        }
    }
    private void RequireKnownSupportRootOutside(GTA.Math.Vector3 root, GTA.Math.Vector3 right, GTA.Math.Vector3 away, string label)
    {
        GTA.Math.Vector3 delta = root - tablePosition; Check(delta);
        float x = delta.X * right.X + delta.Y * right.Y, y = delta.X * away.X + delta.Y * away.Y;
        float clearance = System.Math.Max(System.Math.Max(tableMinimum.X - 0.15f - x, x - tableMaximum.X - 0.15f),
            System.Math.Max(tableMinimum.Y - 0.15f - y, y - tableMaximum.Y - 0.15f)); Check(clearance);
        Log("CARTA_TRIAL_KNOWN_BODY_SUPPORT", "Sample=" + label + "; NativeRoot=" + root + "; SupportLocalX=" + Number(x) +
            "; SupportLocalY=" + Number(y) + "; ExistingFootprintExpansion=.15; Clearance=" + Number(clearance) +
            "; OnlyKnownPhaseSample=true; FullPathProven=false; PreparationOnly=true");
        if (x > tableMinimum.X - 0.15f && x < tableMaximum.X + 0.15f &&
            y > tableMinimum.Y - 0.15f && y < tableMaximum.Y + 0.15f)
            throw Error("raiz authored conhecida nao cabe fora do apoio derivado; nenhuma alteracao durante acao");
    }
    private static bool MapFloor(GTA.Math.Vector3 point, float top, float bottom, GTA.Entity ignore, out float z)
    {
        z = float.NaN; Check(point); GTA.ShapeTestResult hit;
        var ray = GTA.ShapeTest.StartExpensiveSyncTestLOSProbe(new GTA.Math.Vector3(point.X, point.Y, top),
            new GTA.Math.Vector3(point.X, point.Y, bottom), GTA.IntersectFlags.Map, ignore, (GTA.ShapeTestOptions)0);
        if (ray.GetResult(out hit) != GTA.ShapeTestStatus.Ready || !hit.DidHit) return false;
        Check(hit.HitPosition); Check(hit.SurfaceNormal);
        if (hit.SurfaceNormal.Z < 0.98f || hit.HitPosition.Z > top + 0.01f || hit.HitPosition.Z < bottom - 0.01f) return false;
        z = hit.HitPosition.Z; return true;
    }
    private void LogPaper(Paper paper)
    {
        var result = new System.Text.StringBuilder("ModelHash=" + paper.Model + "; Track=" + Clip(spec.Segmento, paper.Track) +
            "; Playing=" + Playing(paper.Handle, paper.Track) + "; MaxPhase=" + Number(paper.MaximumPhase) +
            "; EntityAnimPhase=" + Number(Playing(paper.Handle, paper.Track) ? Current(paper.Handle, paper.Track) : 0f) +
            "; PhaseSource=OwnedFwSyncedSceneWithExactClipPlaying" +
            "; SceneOrigin=" + paper.SceneOrigin + "; ObjectScenePhase=" + Number(paper.Scene == null ? 0f : paper.Scene.Phase) +
            "; ObjectSceneRate=" + Number(paper.Scene == null ? 1f : paper.Scene.Rate) +
            "; BodyObjectPhaseDelta=" + Number(paper.Scene == null || scene == null ? 0f : paper.Scene.Phase - scene.Phase) +
            "; HandContactObserved=" + paper.HandContactObserved + "; MinimumHandDistance=" + Number(paper.MinimumHandDistance) +
            "; ReadingContactSamples=" + paper.ReadingContactSamples + "; ReadingContactFailures=" + paper.ReadingContactFailures +
            "; ReadingMaximumHandDistance=" + Number(paper.ReadingMaximumHandDistance) +
            "; ReadingContactPhaseWindow=0.25..0.75; InitialPaperOwnTableMeasured=" + tableMeasured +
            "; Position=" + paper.Entity.Position + "; Rotation=" + paper.Entity.Rotation + "; HasDrawable=" + paper.Entity.HasDrawable +
            "; HasSkeleton=" + paper.Entity.HasSkeleton + "; BoneCount=" + paper.Entity.Bones.Count);
        int count = System.Math.Min(paper.Entity.Bones.Count, 24);
        for (int i = 0; i < count; i++)
        {
            GTA.Math.Vector3 position = paper.Entity.Bones[i].Position; Check(position);
            result.Append("; Bone").Append(i).Append('=').Append(position).Append("; BoneName").Append(i).Append('=').Append(paper.Entity.Bones[i].Name);
        }
        result.Append("; BoneSampleCapped=24; AnimatedMeshFoldRequiresVideo=true; ProductionApproval=false");
        Log("CARTA_TRIAL_PAPER", result.ToString());
    }
    private void RequestModel(string name, int track)
    {
        var model = new GTA.Model(name);
        if (!model.IsInCdImage || !model.IsValid || !model.IsProp)
            throw Error("modelo selecionado indisponivel/nao Prop no Legacy: " + name + "; mudar somente selecao tecnica, nao simular");
        if (!ValidDuration(durations[spec.Segmento, track])) throw Error("track selecionado nao existe/carrega");
        model.Request();
        if (track == 1) modelSRequested = true; else modelBRequested = true;
    }
    private void InventoryModel(string name)
    {
        var model = new GTA.Model(name);
        Log("CARTA_TRIAL_MODEL", "Model=" + name + "; InCdImage=" + model.IsInCdImage +
            "; Valid=" + model.IsValid + "; IsProp=" + model.IsProp + "; Loaded=" + model.IsLoaded + "; NoAvailabilityAssumption=true");
    }
    private bool SelectedDurationsFit()
    { return ValidDuration(durations[spec.Segmento, 0]) && durations[spec.Segmento, 0] <= 9f &&
        (!UseS() || (ValidDuration(durations[spec.Segmento, 1]) && durations[spec.Segmento, 1] <= 9f)) &&
        (!UseB() || (ValidDuration(durations[spec.Segmento, 2]) && durations[spec.Segmento, 2] <= 9f)); }
    private bool UseS() { return spec.Objetos == "nota_s" || spec.Objetos == "ambos"; }
    private bool UseB() { return spec.Objetos == "nota_b" || spec.Objetos == "ambos"; }
    private bool Playing(int entity, int track)
    { return GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, entity, Dictionary(spec.Segmento), Clip(spec.Segmento, track), 3); }
    private float Current(int entity, int track)
    { return GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, entity, Dictionary(spec.Segmento), Clip(spec.Segmento, track)); }
    private GTA.Math.Vector3 InitialPosition(string clip, GTA.Math.Vector3 root, GTA.Math.Vector3 rotation)
    { return GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ANIM_INITIAL_OFFSET_POSITION, Dictionary(spec.Segmento), clip,
        root.X, root.Y, root.Z, rotation.X, rotation.Y, rotation.Z, 0f, 2); }
    private GTA.Math.Vector3 InitialRotation(string clip, GTA.Math.Vector3 root, GTA.Math.Vector3 rotation)
    { return GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ANIM_INITIAL_OFFSET_ROTATION, Dictionary(spec.Segmento), clip,
        root.X, root.Y, root.Z, rotation.X, rotation.Y, rotation.Z, 0f, 2); }
    private void RequireActor()
    {
        if (!OwnActor() || !actor.IsInvincible || actor.IsDead || actor.Health != health || actor.IsInjured || actor.IsRagdoll ||
            actor.IsInVehicle() || actor.IsInWater || actor.IsInAir || (ownTaskStarted && !warmBindingPending && (!actor.HasPhysics || !actor.IsCollisionEnabled)))
            throw Error("atriz propria mudou identidade/saude/apoio/physics");
    }
    private bool OwnActor()
    { return actor != null && actor.Exists() && ownsActor != null && ownsActor(actor) && actor.Handle == actorHandle && actor.MemoryAddress == actorAddress && actor.Model.Hash == actorModel; }
    private bool OwnPaper(Paper paper)
    { return paper.Entity != null && paper.Entity.Exists() && paper.Entity.Handle == paper.Handle && paper.Entity.MemoryAddress == paper.Address && paper.Entity.Model.Hash == paper.Model; }
    private void RequirePapers()
    { foreach (var paper in papers) if (!OwnPaper(paper) || !paper.Entity.IsVisible) throw Error("identidade/visibilidade do papel proprio mudou"); }
    private void VerifySources()
    {
        string animPath = System.IO.Path.Combine(menyoo ?? "", "PedAnimList.txt"), propPath = System.IO.Path.Combine(menyoo ?? "", "PropList.txt");
        VerifyFile(animPath, "2C537060BD97FCF0DACA407DB1436A5D54E4B8DD5274DBC9EF0D8466EF6F21E4");
        VerifyFile(propPath, "35BFD924919FC223F5AB131941643B2AE4E0082FCD0ECEF1A5A50FCDEE9B2552");
        string[] lines = System.IO.File.ReadAllLines(animPath);
        int[,] actual = { {24837,24838,24839}, {24842,24843,24841}, {24845,24846,24847}, {24848,24850,24851},
                          {24853,24854,24855}, {24857,24858,24859}, {24862,24860,24863} };
        for (int segment = 0; segment < 7; segment++) for (int track = 0; track < 3; track++)
            if (lines.Length < actual[segment, track] || lines[actual[segment, track] - 1].Trim() != Dictionary(segment) + " " + Clip(segment, track))
                throw Error("track instalado diverge de origem/linha");
        lines = System.IO.File.ReadAllLines(propPath);
        if (lines.Length < 9539 || lines[8922].Trim() != NoteS || lines[9538].Trim() != NoteB || lines[9537].Trim() != "prop_amanda_note_01")
            throw Error("modelos instalados divergem da origem");
        if (lines.Length < 11000 || lines[10999].Trim() != TableName)
            throw Error("modelo mesa propria diverge da origem verificada");
    }
    private static void VerifyFile(string path, string expected)
    {
        if (!System.IO.File.Exists(path)) throw Error("origem Menyoo ausente");
        using (var algorithm = System.Security.Cryptography.SHA256.Create())
        using (var file = System.IO.File.OpenRead(path))
            if (System.BitConverter.ToString(algorithm.ComputeHash(file)).Replace("-", "") != expected) throw Error("SHA da origem Menyoo diverge");
    }
    private static void Validate(EnsaioSpec value)
    {
        if (value == null || value.Segmento < 0 || value.Segmento > 6 || value.JanelaMs != 9000 ||
            (value.Objetos != "nota_s" && value.Objetos != "nota_b" && value.Objetos != "ambos"))
            throw Error("segmento0..6, objetos nota_s/nota_b/ambos e janela exata9000 exigidos");
    }
    private static string Dictionary(int segment) { return Prefix + segment.ToString(System.Globalization.CultureInfo.InvariantCulture); }
    private static string Clip(int segment, int track)
    { return (track == 0 ? "player_zero_dual-" : track == 1 ? "p_amanda_note_01_s-" : "prop_amanda_note_01b-") + segment.ToString(System.Globalization.CultureInfo.InvariantCulture); }
    private static bool ValidDuration(float value) { return !System.Single.IsNaN(value) && !System.Single.IsInfinity(value) && value > 0f; }
    private static void Check(GTA.Math.Vector3 value) { Check(value.X); Check(value.Y); Check(value.Z); }
    private static void Check(float value) { if (System.Single.IsNaN(value) || System.Single.IsInfinity(value)) throw Error("getter nao finito"); }
    private static void CheckPhase(float value) { Check(value); if (value < 0f || value > 1.001f) throw Error("fase fora do intervalo nativo"); }
    private static float Angle(float value) { value %= 360f; if (value > 180f) value -= 360f; if (value < -180f) value += 360f; return System.Math.Abs(value); }
    private static string Number(float value) { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private void Active() { if (disposed) throw new System.ObjectDisposedException("CartaFisicaDiretor"); }
    private static void Story() { if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS)) throw Error("somente Story"); }
    private void Log(string code, string message) { if (log != null) log(code, message); }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("EnsaioCarta: " + message); }
}

// Geometry authored by this project, rendered by DRAW_POLY. NOT an existing folded GTA model/collider.
// F6-only prototype; one actual own proxy parent. No Script, body task, hand reanchor, scale or action.
public sealed class PapelDobradoPoly86Diretor : System.IDisposable
{
    private readonly System.Action<string, string> log;
    private readonly GTA.Prop parent;
    private readonly GTA.Ped actor;
    private readonly int parentHandle, parentModel, actorHandle, actorModel;
    private readonly System.IntPtr parentAddress, actorAddress;
    private readonly bool originalVisible;
    private readonly GTA.Math.Vector3 originalActorPosition, proxyRestMin, proxyRestMax;
    private readonly GTA.Math.Vector3[] local;
    private readonly string sourceHash;
    private readonly float leafWidth, leafHeight, thickness, angle;
    private readonly int axisWidth, axisHeight, axisNormal;
    private GTA.Camera camera, previousCamera;
    private int cameraHandle, startedAt, startedFrame, nextLogAt;
    private System.IntPtr cameraAddress;
    private int frames;
    private bool disposed, hiddenByThisPreview, initialized, ready;
    private GTA.Math.Vector3 cameraPosition, cameraRotation;
    private float cameraFov;

    public PapelDobradoPoly86Diretor(GTA.Prop ownParent, GTA.Ped ownActor, string exactSourceHash,
        System.Action<string, string> logger)
    {
        log = logger; parent = ownParent; actor = ownActor; sourceHash = exactSourceHash;
        if (System.Object.ReferenceEquals(parent, null) || !parent.Exists() || parent.MemoryAddress == System.IntPtr.Zero ||
            System.Object.ReferenceEquals(actor, null) || !actor.Exists() || actor.MemoryAddress == System.IntPtr.Zero ||
            actor.IsPlayer || actor.IsDead || actor.IsInVehicle() || !actor.IsPersistent ||
            parent.Model.Hash != unchecked((int)GTA.StringHash.AtStringHash("p_amanda_note_01_s")) || !parent.HasDrawable ||
            !parent.IsPersistent || !parent.IsVisible || !parent.IsAttachedTo(actor) ||
            actor.Model.Hash != unchecked((int)GTA.StringHash.AtStringHash("mp_f_freemode_01")) ||
            !System.Text.RegularExpressions.Regex.IsMatch(exactSourceHash ?? "", "\\A[0-9A-F]{64}\\z"))
            throw Error("sole actual own note_s parent/drawable attached to saved own live Djanira required");
        Story();
        parentHandle = parent.Handle; parentModel = parent.Model.Hash; parentAddress = parent.MemoryAddress;
        actorHandle = actor.Handle; actorModel = actor.Model.Hash; actorAddress = actor.MemoryAddress;
        originalActorPosition = actor.Position; originalVisible = parent.IsVisible;
        Vector(originalActorPosition, 30000f);
        parent.Model.GetDimensions(out proxyRestMin, out proxyRestMax);
        Vector(proxyRestMin, 1f); Vector(proxyRestMax, 1f);
        GTA.Math.Vector3 extents = proxyRestMax - proxyRestMin;
        if (extents.X <= 0f || extents.Y <= 0f || extents.Z <= 0f) throw Error("real proxy rest bounds invalid");
        int[] axes = new int[] { 0, 1, 2 };
        for (int i = 0; i < 2; i++) for (int j = i + 1; j < 3; j++)
            if (Component(extents, axes[j]) > Component(extents, axes[i])) { int temp = axes[i]; axes[i] = axes[j]; axes[j] = temp; }
        axisHeight = axes[0]; axisWidth = axes[1]; axisNormal = axes[2];
        leafHeight = Component(extents, axisHeight); leafWidth = Component(extents, axisWidth) * 0.5f;
        if (leafHeight < 0.03f || leafHeight > 0.5f || leafWidth < 0.01f || leafWidth > 0.25f)
            throw Error("measured parent too large/small for one own letter preview");
        // Authored crease separation derived from measured width; never scales the proxy/model.
        thickness = leafWidth * 0.008f; angle = (float)(177.0 * System.Math.PI / 180.0);
        GTA.Math.Vector3 center = (proxyRestMin + proxyRestMax) * 0.5f;
        GTA.Math.Vector3 a = Unit(axisWidth), b = Unit(axisHeight), n = Unit(axisNormal);
        float halfHeight = leafHeight * 0.5f;
        GTA.Math.Vector3 foldedTip = a * (leafWidth * (float)System.Math.Cos(angle)) +
            n * (leafWidth * (float)System.Math.Sin(angle));
        // Two adjacent half sheets joined by one crease. Closed near pi, no moving hinge/clock.
        local = new GTA.Math.Vector3[] {
            center - b * halfHeight - n * (thickness * 0.5f),
            center - b * halfHeight - a * leafWidth - n * (thickness * 0.5f),
            center + b * halfHeight - a * leafWidth - n * (thickness * 0.5f),
            center + b * halfHeight - n * (thickness * 0.5f),
            center - b * halfHeight + n * (thickness * 0.5f),
            center - b * halfHeight + foldedTip + n * (thickness * 0.5f),
            center + b * halfHeight + foldedTip + n * (thickness * 0.5f),
            center + b * halfHeight + n * (thickness * 0.5f)
        };
        foreach (GTA.Math.Vector3 point in local) Vector(point, 1f);
        // Constructor only validates and stores readonly data. Main owns this instance before any side effect.
    }

    public void StartPreview()
    {
        if (disposed || initialized || !System.Object.ReferenceEquals(camera, null) || hiddenByThisPreview)
            throw Error("preview start state invalid; do not create another camera");
        Story();
        if (!ParentSame() || !parent.IsPersistent || !parent.IsVisible || !parent.IsAttachedTo(actor) ||
            !actor.Exists() || actor.Handle != actorHandle || actor.MemoryAddress != actorAddress || actor.Model.Hash != actorModel ||
            actor.IsDead || actor.IsPlayer || actor.IsInVehicle() || !actor.IsPersistent ||
            (actor.Position - originalActorPosition).Length() > 0.03f)
            throw Error("own preparation identity changed before start; no side effect");
            GTA.Math.Vector3 center = (proxyRestMin + proxyRestMax) * 0.5f;
            GTA.Math.Vector3 a = Unit(axisWidth), b = Unit(axisHeight), n = Unit(axisNormal);
            GTA.Math.Vector3[] world = WorldVertices();
            GTA.Math.Vector3 worldMin, worldMax; Bounds(world, out worldMin, out worldMax);
            GTA.Math.Vector3 target = (worldMin + worldMax) * 0.5f;
            float radius = (worldMax - worldMin).Length() * 0.5f;
            if (!Finite(radius) || radius <= 0f || radius > 1f) throw Error("rendered world bounds invalid");
            GTA.Math.Vector3 localView = n + a * 0.20f - b * 0.30f;
            GTA.Math.Vector3 direction = parent.GetOffsetPosition(center + localView) - parent.GetOffsetPosition(center);
            Vector(direction, 2f); float length = direction.Length();
            if (!Finite(length) || length < 0.5f) throw Error("actual rigid parent transform invalid");
            direction *= 1f / length;
            cameraFov = 35f;
            float distance = System.Math.Max(0.35f, radius * 1.35f / (float)System.Math.Sin(cameraFov * System.Math.PI / 360.0));
            cameraPosition = target + direction * distance;
            cameraRotation = Look(target - cameraPosition);
            previousCamera = GTA.ScriptCameraDirector.RenderingCam;
            camera = GTA.Camera.Create(GTA.ScriptedCameraNameHash.DefaultScriptedCamera,
                cameraPosition, cameraRotation, cameraFov, false, GTA.EulerRotationOrder.YXZ);
            if (System.Object.ReferenceEquals(camera, null) || !camera.Exists())
                throw Error("own preview camera was not actually created");
            cameraHandle = camera.Handle; cameraAddress = camera.MemoryAddress;
            if (cameraHandle < 0 || cameraAddress == System.IntPtr.Zero) throw Error("own created preview camera has invalid identity");
            camera.NearClip = 0.04f;
            // PREP-only visibility change of sole OWN proxy, never a map/world entity or body.
            hiddenByThisPreview = true; parent.IsVisible = false;
            camera.IsActive = true; GTA.ScriptCameraDirector.StartRendering();
            startedAt = GTA.Game.GameTime; startedFrame = GTA.Game.FrameCount; nextLogAt = 0;
            RequireIdentity(); initialized = true;
            ObserveCamera("START_SAME_FRAME_PENDING", true);
            Log("PAPER_POLY86_CAMERA_PENDING", Metadata() + "; StartedFrame=" + startedFrame + "; DeadlineMs=2000; RenderSetterIssuedOnce=True; ActualRenderingPending=True; DrawCalls=0; Ready=False");
        // Any throw leaves this Main-owned reference available for Cleanup/F10; no constructor orphan.
    }

    public bool IsReady { get { return initialized && ready && !disposed; } }

    public bool PollPreparation()
    {
        if (disposed || !initialized) throw Error("pending preview was not started/disposed");
        if (ready) return false;
        Story(); RequireIdentity();
        uint elapsed = unchecked((uint)(GTA.Game.GameTime - startedAt));
        bool actualMatches = ObserveCamera("POLL_PENDING", true); // Actual fields are logged BEFORE every verdict.
        if (System.Object.ReferenceEquals(camera, null) || !camera.Exists() || camera.Handle != cameraHandle ||
            camera.MemoryAddress != cameraAddress || cameraAddress == System.IntPtr.Zero)
            throw Error("own pending camera identity lost; no retry/setter/replacement");
        if (elapsed > 2000u)
            throw Error("actual own rendered camera did not satisfy unchanged getters within 2000ms");
        if (GTA.Game.FrameCount == startedFrame || !actualMatches) return false;
        ready = true;
        Log("PAPER_POLY86_PREPARED", Metadata() + "; ElapsedMs=" + elapsed + "; StartedFrame=" + startedFrame + "; ReadyFrame=" + GTA.Game.FrameCount +
            "; ActualOwnRenderedCameraConfirmed=True; ProxyRestMin=" + proxyRestMin + "; ProxyRestMax=" + proxyRestMax +
            "; WidthAxis=" + axisWidth + "; HeightAxis=" + axisHeight + "; NormalAxis=" + axisNormal +
            "; LeafWidth=" + Number(leafWidth) + "; LeafHeight=" + Number(leafHeight) + "; CreaseThickness=" + Number(thickness) +
            "; FoldAngleDegrees=177; ProxyHasPhysics=" + parent.HasPhysics + "; ProxyCollisionEnabled=" + parent.IsCollisionEnabled +
            "; ProxyDrawableVisible=" + parent.IsVisible + "; RenderedSurfaceIsNativeEntity=False; FoldedColliderProved=False; GeometryAuthored=True; ImagePending=True");
        return true;
    }

    public void DrawFrame()
    {
        if (!IsReady) throw Error("preview actual rendered camera not ready/disposed");
        Story(); RequireIdentity(); RequireCamera();
        GTA.Math.Vector3[] world = WorldVertices();
        Quad(world[0], world[1], world[2], world[3]);
        Quad(world[4], world[5], world[6], world[7]);
        Quad(world[0], world[3], world[7], world[4]); // Thin joined crease, not another loose page.
        frames++;
        uint elapsed = unchecked((uint)(GTA.Game.GameTime - startedAt));
        if (elapsed >= (uint)nextLogAt)
        {
            nextLogAt = (int)elapsed + 500;
            GTA.Math.Vector3 minimum, maximum; Bounds(world, out minimum, out maximum);
            Log("PAPER_POLY86_FRAME", Metadata() + "; ElapsedMs=" + elapsed + "; Frames=" + frames +
                "; ProxyPosition=" + parent.Position + "; ProxyRotation=" + parent.Rotation +
                "; ProxyHasPhysics=" + parent.HasPhysics + "; ProxyCollisionEnabled=" + parent.IsCollisionEnabled +
                "; ProxyVisible=" + parent.IsVisible + "; RenderSurfaceWorldMin=" + minimum + "; RenderSurfaceWorldMax=" + maximum +
                "; RenderSurfacePhysics=False; FoldedColliderProved=False; VertexGetterOnlyTransform=True; ParentTransformSetter=False; Handoff=False; OpenByClock=False; Approval=False");
        }
    }
    private void RequireIdentity()
    {
        if (!ParentSame() || !parent.IsPersistent || System.Object.ReferenceEquals(actor, null) || !actor.Exists() || actor.Handle != actorHandle ||
            actor.MemoryAddress != actorAddress || actor.Model.Hash != actorModel || actor.IsPlayer || actor.IsDead || actor.IsInVehicle() ||
            !actor.IsPersistent || !parent.IsAttachedTo(actor) || parent.IsVisible ||
            (actor.Position - originalActorPosition).Length() > 0.03f || (parent.Position - actor.Position).Length() > 3f)
            throw Error("own proxy/actor/attachment/static preview identity changed; no reanchor or body correction");
        Vector(actor.Position, 30000f); Vector(parent.Position, 30000f); Vector(parent.Rotation, 400f);
    }
    private bool ParentSame()
    {
        return !System.Object.ReferenceEquals(parent, null) && parent.Exists() && parent.Handle == parentHandle &&
            parent.MemoryAddress == parentAddress && parentAddress != System.IntPtr.Zero && parent.Model.Hash == parentModel;
    }
    private void RequireCamera()
    {
        if (!ObserveCamera("DRAW_FRAME_GUARD", false))
            throw Error("actual own rendered camera changed; do not accept preset getter alone");
    }
    private bool ObserveCamera(string phase, bool alwaysLog)
    {
        bool ownExists = !System.Object.ReferenceEquals(camera, null) && camera.Exists();
        int ownHandle = ownExists ? camera.Handle : -1;
        System.IntPtr ownAddress = ownExists ? camera.MemoryAddress : System.IntPtr.Zero;
        bool ownActive = ownExists && camera.IsActive;
        GTA.Math.Vector3 ownPosition = ownExists ? camera.Position : new GTA.Math.Vector3(float.NaN, float.NaN, float.NaN);
        GTA.Math.Vector3 ownRotation = ownExists ? camera.Rotation : new GTA.Math.Vector3(float.NaN, float.NaN, float.NaN);
        float ownFov = ownExists ? camera.FieldOfView : float.NaN;
        GTA.Camera rendering = GTA.ScriptCameraDirector.RenderingCam;
        bool renderedExists = !System.Object.ReferenceEquals(rendering, null) && rendering.Exists();
        int renderedHandle = renderedExists ? rendering.Handle : -1;
        System.IntPtr renderedAddress = renderedExists ? rendering.MemoryAddress : System.IntPtr.Zero;
        bool renderedActive = renderedExists && rendering.IsActive;
        GTA.Math.Vector3 renderedPosition = renderedExists ? rendering.Position : new GTA.Math.Vector3(float.NaN, float.NaN, float.NaN);
        GTA.Math.Vector3 renderedRotation = renderedExists ? rendering.Rotation : new GTA.Math.Vector3(float.NaN, float.NaN, float.NaN);
        float renderedFov = renderedExists ? rendering.FieldOfView : float.NaN;
        int nativeRenderingHandle = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_RENDERING_CAM);
        float positionDelta = (ownPosition - cameraPosition).Length();
        float rotationDelta = (ownRotation - cameraRotation).Length();
        float fovDelta = System.Math.Abs(ownFov - cameraFov);
        bool valuesFinite = Finite(positionDelta) && Finite(rotationDelta) && Finite(fovDelta);
        bool matches = ownExists && ownHandle == cameraHandle && ownAddress == cameraAddress && ownActive &&
            renderedExists && renderedHandle == cameraHandle && renderedAddress == cameraAddress &&
            valuesFinite && positionDelta <= 0.01f && rotationDelta <= 0.05f && fovDelta <= 0.01f;
        if (alwaysLog || !matches)
            Log("PAPER_POLY86_CAMERA_ACTUAL", Metadata() + "; Phase=" + phase + "; Frame=" + GTA.Game.FrameCount +
                "; ElapsedMs=" + unchecked((uint)(GTA.Game.GameTime - startedAt)) +
                "; ExpectedCameraAddress=" + cameraAddress + "; OwnExists=" + ownExists + "; OwnHandle=" + ownHandle +
                "; OwnAddress=" + ownAddress + "; OwnActive=" + ownActive + "; OwnPosition=" + ownPosition +
                "; OwnRotation=" + ownRotation + "; OwnFov=" + Number(ownFov) + "; RenderedExists=" + renderedExists +
                "; RenderedHandle=" + renderedHandle + "; RenderedAddress=" + renderedAddress + "; RenderedActive=" + renderedActive +
                "; RenderedPosition=" + renderedPosition + "; RenderedRotation=" + renderedRotation + "; RenderedFov=" + Number(renderedFov) +
                "; NativeRenderingHandleDiagnostic=" + nativeRenderingHandle + "; PositionDelta=" + Number(positionDelta) +
                "; RotationDelta=" + Number(rotationDelta) + "; FovDelta=" + Number(fovDelta) + "; ActualValuesFinite=" + valuesFinite +
                "; ExactOriginalGuardsMatch=" + matches + "; LoggedBeforeVerdict=True; RenderSetterRetry=False");
        return matches;
    }
    private GTA.Math.Vector3[] WorldVertices()
    {
        var result = new GTA.Math.Vector3[local.Length];
        for (int i = 0; i < local.Length; i++)
        {
            result[i] = parent.GetOffsetPosition(local[i]); Vector(result[i], 30000f);
            if ((parent.GetPositionOffset(result[i]) - local[i]).Length() > 0.001f)
                throw Error("rigid local/world transform failed .001m round trip");
        }
        return result;
    }
    private static void Quad(GTA.Math.Vector3 a, GTA.Math.Vector3 b, GTA.Math.Vector3 c, GTA.Math.Vector3 d)
    { Triangle(a,b,c); Triangle(a,c,d); Triangle(c,b,a); Triangle(d,c,a); }
    private static void Triangle(GTA.Math.Vector3 a, GTA.Math.Vector3 b, GTA.Math.Vector3 c)
    {
        // Actual GTA DRAW_POLY, cited Native0xAC26716048436851. Both winding orders, white on both faces.
        GTA.Native.Function.Call((GTA.Native.Hash)0xAC26716048436851UL, a.X,a.Y,a.Z,b.X,b.Y,b.Z,c.X,c.Y,c.Z,255,255,255,255);
    }
    public void Dispose()
    {
        if (disposed) return;
        bool parentAlreadyAbsent = !parent.Exists();
        try
        {
            // Camera restoration errors must never skip restoration of the own proxy visibility.
            if (hiddenByThisPreview && parent.Exists() && !ParentSame())
                throw Error("refuse to restore visibility of unknown replacement parent");
        }
        finally
        {
            if (hiddenByThisPreview && ParentSame())
            {
                parent.IsVisible = originalVisible;
                if (parent.IsVisible != originalVisible) throw Error("own proxy visibility restoration unconfirmed");
                hiddenByThisPreview = false;
            }
            if (parentAlreadyAbsent) hiddenByThisPreview = false;
        }
        RestoreAndDeleteCamera(); disposed = true; initialized = false; ready = false;
        Log("PAPER_POLY86_CLEANUP", "OwnProxyAlreadyAbsent=" + parentAlreadyAbsent + "; OwnProxyVisibilityRestored=" + !parentAlreadyAbsent + "; OnlyOwnCameraDeleted=True; ProxyOwnedCleanupRemainsNormal=True; DrawCallsStopped=True; NoOriginalWorldMutation=True");
    }
    private void RestoreAndDeleteCamera()
    {
        if (System.Object.ReferenceEquals(camera, null) || !camera.Exists()) return;
        if (camera.Handle != cameraHandle || camera.MemoryAddress != cameraAddress) throw Error("refuse deleting unknown camera identity");
        GTA.Camera rendering = GTA.ScriptCameraDirector.RenderingCam;
        if (!System.Object.ReferenceEquals(rendering, null) && rendering.Exists() && rendering.Handle == cameraHandle)
        {
            camera.IsActive = false;
            if (!System.Object.ReferenceEquals(previousCamera, null) && previousCamera.Exists())
            { previousCamera.IsActive = true; GTA.ScriptCameraDirector.StartRendering(); }
            else GTA.ScriptCameraDirector.StopRendering(false);
        }
        camera.Delete();
        if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_CAM_EXIST, cameraHandle))
            throw Error("own camera deletion unconfirmed; retain reference for F10");
        camera = null;
    }
    private string Metadata()
    { return "Scene=teste_fila_86; Mode=DRAW_POLY_AUTHORED_STATIC_FOLDED; SceneSHA256=" + sourceHash + "; ProxyModel=p_amanda_note_01_s; ProxyHandle=" + parentHandle +
        "; ProxyAddress=" + parentAddress + "; ActorHandle=" + actorHandle + "; CameraHandle=" + cameraHandle +
        "; CameraPosition=" + cameraPosition + "; CameraRotation=" + cameraRotation + "; CameraFov=" + Number(cameraFov) +
        "; ActorTask=False; ActorTransformSetter=False; PerFrameHandAttach=False; ProxyScaleSetter=False; HandContactProved=False; CameraOcclusionProved=False; ARM=False; SAVE=False; ProductionReady=False"; }
    private static void Bounds(GTA.Math.Vector3[] vertices, out GTA.Math.Vector3 minimum, out GTA.Math.Vector3 maximum)
    {
        minimum = maximum = vertices[0];
        foreach (GTA.Math.Vector3 p in vertices)
        {
            minimum.X = System.Math.Min(minimum.X,p.X); minimum.Y = System.Math.Min(minimum.Y,p.Y); minimum.Z = System.Math.Min(minimum.Z,p.Z);
            maximum.X = System.Math.Max(maximum.X,p.X); maximum.Y = System.Math.Max(maximum.Y,p.Y); maximum.Z = System.Math.Max(maximum.Z,p.Z);
        }
    }
    private static GTA.Math.Vector3 Unit(int axis) { return axis == 0 ? new GTA.Math.Vector3(1,0,0) : axis == 1 ? new GTA.Math.Vector3(0,1,0) : new GTA.Math.Vector3(0,0,1); }
    private static float Component(GTA.Math.Vector3 p,int axis) { return axis == 0 ? p.X : axis == 1 ? p.Y : p.Z; }
    private static GTA.Math.Vector3 Look(GTA.Math.Vector3 direction)
    {
        Vector(direction, 5f); float horizontal=(float)System.Math.Sqrt(direction.X*direction.X+direction.Y*direction.Y);
        if (horizontal < 0.00001f) throw Error("preview camera direction singular");
        return new GTA.Math.Vector3((float)(System.Math.Atan2(direction.Z,horizontal)*180.0/System.Math.PI),0f,
            (float)(System.Math.Atan2(-direction.X,direction.Y)*180.0/System.Math.PI));
    }
    private static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
    private static void Vector(GTA.Math.Vector3 p,float limit)
    { if (!Finite(p.X)||!Finite(p.Y)||!Finite(p.Z)||System.Math.Abs(p.X)>limit||System.Math.Abs(p.Y)>limit||System.Math.Abs(p.Z)>limit) throw Error("nonfinite/outside preview vector"); }
    private static string Number(float n) { return n.ToString("R",System.Globalization.CultureInfo.InvariantCulture); }
    private static void Story() { if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS)) throw Error("Story only"); }
    private void Log(string code,string value) { if (log!=null) log(code,value); }
    private static System.InvalidOperationException Error(string value) { return new System.InvalidOperationException("PapelPoly86: "+value); }
}

// One authored visual folded sheet on the existing OWN Native static_01 proxy.
// It does not supply body tasks, hand attachment, collision, actors or a camera.
public sealed class PapelDobradoAutoralC5Diretor : System.IDisposable
{
    public const string ExactSourceHash = "2DA3FBA3E423B44B0157BAF9CCE8168F7EEC9718713398BAD98FFACDDC503EE1";
    private readonly GTA.Prop parent;
    private readonly GTA.Ped donor, receiver;
    private readonly GTA.Camera camera;
    private readonly int parentHandle, parentModel, donorHandle, receiverHandle, donorModel, receiverModel, cameraHandle;
    private readonly System.IntPtr parentAddress, donorAddress, receiverAddress, cameraAddress;
    private readonly bool originalVisible;
    private readonly GTA.Math.Vector3 cameraPosition, cameraRotation, proxyRestMin, proxyRestMax;
    private readonly float cameraFov, leafWidth, leafHeight, thickness, angle;
    private readonly int axisWidth, axisHeight, axisNormal;
    private readonly GTA.Math.Vector3[] local;
    private readonly System.Action<string,string> log;
    private bool started, hidden, ready, disposed;
    private int startedAt, startedFrame, nextLogAt, frames;

    public PapelDobradoAutoralC5Diretor(GTA.Prop ownParent, GTA.Ped ownDonor, GTA.Ped ownReceiver,
        GTA.Camera ownHandoffCamera, string sourceHash, System.Action<string,string> logger)
    {
        parent=ownParent; donor=ownDonor; receiver=ownReceiver; camera=ownHandoffCamera; log=logger;
        if (sourceHash!=ExactSourceHash) throw Error("exact C5 source required");
        Story(); Ped(donor); Ped(receiver);
        if (donor.Handle==receiver.Handle || receiver.Model.Hash!=unchecked((int)GTA.StringHash.AtStringHash("mp_f_freemode_01")) ||
            System.Object.ReferenceEquals(parent,null) || !parent.Exists() || parent.MemoryAddress==System.IntPtr.Zero ||
            !parent.IsPersistent || !parent.IsVisible || !parent.HasDrawable || !parent.IsAttachedTo(donor) ||
            parent.Model.Hash!=unchecked((int)GTA.StringHash.AtStringHash("prop_amanda_note_01")) || !parent.Model.IsCollisionLoaded ||
            System.Object.ReferenceEquals(camera,null) || !camera.Exists() || camera.MemoryAddress==System.IntPtr.Zero)
            throw Error("own registered donor/receiver/static_01 proxy/known handoff camera required");
        parentHandle=parent.Handle;parentModel=parent.Model.Hash;parentAddress=parent.MemoryAddress;
        donorHandle=donor.Handle;donorModel=donor.Model.Hash;donorAddress=donor.MemoryAddress;
        receiverHandle=receiver.Handle;receiverModel=receiver.Model.Hash;receiverAddress=receiver.MemoryAddress;
        cameraHandle=camera.Handle;cameraAddress=camera.MemoryAddress;
        originalVisible=parent.IsVisible;cameraPosition=camera.Position;cameraRotation=camera.Rotation;cameraFov=camera.FieldOfView;
        Vector(cameraPosition,30000f);Vector(cameraRotation,400f);if(!Finite(cameraFov)||cameraFov<15||cameraFov>55)throw Error("own optics invalid");
        parent.Model.GetDimensions(out proxyRestMin, out proxyRestMax);
        Vector(proxyRestMin, 1f); Vector(proxyRestMax, 1f);
        GTA.Math.Vector3 extents = proxyRestMax - proxyRestMin;
        if (extents.X <= 0f || extents.Y <= 0f || extents.Z <= 0f) throw Error("real proxy rest bounds invalid");
        int[] axes = new int[] { 0, 1, 2 };
        for (int i = 0; i < 2; i++) for (int j = i + 1; j < 3; j++)
            if (Component(extents, axes[j]) > Component(extents, axes[i])) { int temp = axes[i]; axes[i] = axes[j]; axes[j] = temp; }
        axisHeight = axes[0]; axisWidth = axes[1]; axisNormal = axes[2];
        leafHeight = Component(extents, axisHeight); leafWidth = Component(extents, axisWidth) * 0.5f;
        if (leafHeight < 0.03f || leafHeight > 0.5f || leafWidth < 0.01f || leafWidth > 0.25f)
            throw Error("measured parent too large/small for one own letter");
        thickness = leafWidth * 0.008f; angle = (float)(177.0 * System.Math.PI / 180.0);
        GTA.Math.Vector3 center = (proxyRestMin + proxyRestMax) * 0.5f;
        GTA.Math.Vector3 a = Unit(axisWidth), b = Unit(axisHeight), n = Unit(axisNormal);
        float halfHeight = leafHeight * 0.5f;
        GTA.Math.Vector3 foldedTip = a * (leafWidth * (float)System.Math.Cos(angle)) +
            n * (leafWidth * (float)System.Math.Sin(angle));
        local = new GTA.Math.Vector3[] {
            center - b * halfHeight - n * (thickness * 0.5f),
            center - b * halfHeight - a * leafWidth - n * (thickness * 0.5f),
            center + b * halfHeight - a * leafWidth - n * (thickness * 0.5f),
            center + b * halfHeight - n * (thickness * 0.5f),
            center - b * halfHeight + n * (thickness * 0.5f),
            center - b * halfHeight + foldedTip + n * (thickness * 0.5f),
            center + b * halfHeight + foldedTip + n * (thickness * 0.5f),
            center + b * halfHeight + n * (thickness * 0.5f)
        };
        foreach(GTA.Math.Vector3 p in local)Vector(p,1f);
        // Readonly constructor. Main stores this instance before Start hides its OWN proxy.
    }
    public bool IsReady {get{return started&&hidden&&ready&&!disposed;}}
    public void RequireParent(GTA.Prop actual)
    {if(System.Object.ReferenceEquals(actual,null)||!actual.Exists()||actual.Handle!=parentHandle||actual.MemoryAddress!=parentAddress||actual.Model.Hash!=parentModel)throw Error("renderer/proxy ownership differs");}
    private void Identity(string owner)
    {
        Story();Ped(donor);Ped(receiver);RequireParent(parent);
        if(donor.Handle!=donorHandle||donor.MemoryAddress!=donorAddress||donor.Model.Hash!=donorModel||
           receiver.Handle!=receiverHandle||receiver.MemoryAddress!=receiverAddress||receiver.Model.Hash!=receiverModel||
           !parent.IsPersistent||!parent.HasDrawable||!parent.Model.IsCollisionLoaded||
           (owner!="DONA_IRACI"&&owner!="DJANIRA_POBRE")||!parent.IsAttachedTo(owner=="DONA_IRACI"?donor:receiver)||
           (parent.Position-(owner=="DONA_IRACI"?donor:receiver).Position).Length()>3f)
            throw Error("own C5 parent/registered actors/actual owner changed");
        Vector(parent.Position,30000f);Vector(parent.Rotation,400f);
    }
    public void Start()
    {
        if(disposed||started||hidden)throw Error("start state invalid");Identity("DONA_IRACI");
        if(!parent.IsVisible)throw Error("own original drawable already hidden");
        hidden=true;parent.IsVisible=false;started=true;startedAt=GTA.Game.GameTime;startedFrame=GTA.Game.FrameCount;
        ObserveCamera("START_PENDING",true);
        Log("PAPEL_AUTORAL_C5_CONFIGURADO",Metadata()+"; RestMin="+proxyRestMin+"; RestMax="+proxyRestMax+"; Width="+Number(leafWidth)+"; Height="+Number(leafHeight)+"; Thickness="+Number(thickness)+"; Angle177=True; ActualReady=False; ActorTask=False; AttachSetter=False; CameraSetter=False");
    }
    public bool PollCamera(string owner)
    {
        RequireConfigured(owner);if(ready)return false;
        uint elapsed=unchecked((uint)(GTA.Game.GameTime-startedAt));bool match=ObserveCamera("POLL_PENDING",true);
        if(!camera.Exists()||camera.Handle!=cameraHandle||camera.MemoryAddress!=cameraAddress)throw Error("own handoff camera identity lost");
        if(elapsed>2000u)throw Error("actual own handoff rendering missing after2000ms");
        if(GTA.Game.FrameCount==startedFrame||!match)return false;
        ready=true;Log("PAPEL_AUTORAL_C5_READY",Metadata()+"; ActualRenderingConfirmed=True; GeometryImagePending=True; FoldedColliderProved=False");return true;
    }
    public void RequireConfigured(string owner)
    {if(disposed||!started||!hidden)throw Error("authored surface not configured");Identity(owner);if(parent.IsVisible)throw Error("proxy drawable unexpectedly visible; no duplicate page allowed");WorldVertices();}
    public void RequireActionReady(string owner)
    {
        RequireConfigured(owner);if(!IsReady||!ObserveCamera("ANTES_ARM_OU_ACAO",true))throw Error("authored paper/camera not actually ready");
        GTA.PedBone hand=(owner=="DONA_IRACI"?donor:receiver).Bones[GTA.Bone.SkelRightHand];if(System.Object.ReferenceEquals(hand,null)||!hand.IsValid)throw Error("actual owner hand unavailable");
        float gap=SurfaceDistance(hand.Position);Log("PAPEL_AUTORAL_C5_GRIP_PREACAO",Metadata()+"; Owner="+owner+"; SurfaceHandGap="+Number(gap)+"; Limit=0.35; SkinContactProved=False; VisualApproval=False");
        if(!Finite(gap)||gap>.35f)throw Error("actual rendered surface is too far from own hand before action");
    }
    public GTA.Math.Vector3[] GetSurfaceWorld(string owner){RequireConfigured(owner);return WorldVertices();}
    public GTA.Math.Vector3 GetVisualCenter(string owner)
    {GTA.Math.Vector3[] v=GetSurfaceWorld(owner);GTA.Math.Vector3 min,max;Bounds(v,out min,out max);return(min+max)*.5f;}
    public bool TransferSurfaceContact(GTA.Math.Vector3 donorHand,GTA.Math.Vector3 receiverHand,int elapsedMs,string owner)
    {
        RequireConfigured(owner);if(!IsReady||!ObserveCamera("HANDOFF",false))throw Error("handoff renderer/camera lost readiness");
        Vector(donorHand,30000);Vector(receiverHand,30000);float first=SurfaceDistance(donorHand),second=SurfaceDistance(receiverHand);
        bool contact=Finite(first)&&Finite(second)&&first<=.35f&&second<=.35f;
        Log("PAPEL_AUTORAL_C5_CONTATO_SUPERFICIE",Metadata()+"; TimeMs="+elapsedMs+"; Owner="+owner+"; DonorSurfaceGap="+Number(first)+"; ReceiverSurfaceGap="+Number(second)+"; Limit=0.35; AnalyticTrianglesContact="+contact+"; ColliderContactClaim=False; OriginalRootHandsPhasesStillRequired=True");return contact;
    }
    public void BeforeReset(string owner)
    {RequireConfigured(owner);parent.IsVisible=originalVisible;if(parent.IsVisible!=originalVisible)throw Error("own reset visibility restoration failed");hidden=false;ready=false;}
    public void AfterReset()
    {Identity("DONA_IRACI");if(disposed||!started||hidden||!parent.IsVisible)throw Error("own reset setup state invalid");hidden=true;parent.IsVisible=false;startedAt=GTA.Game.GameTime;startedFrame=GTA.Game.FrameCount;ready=false;}
    public void DrawFrame(string owner)
    {
        RequireConfigured(owner);if(!IsReady||!ObserveCamera("FRAME",false))throw Error("actual authored draw/camera not ready");
        GTA.Math.Vector3[] v=WorldVertices();Quad(v[0],v[1],v[2],v[3],245);Quad(v[4],v[5],v[6],v[7],255);Quad(v[0],v[3],v[7],v[4],225);frames++;
        int now=GTA.Game.GameTime;if(unchecked(now-nextLogAt)>=0){nextLogAt=unchecked(now+500);GTA.Math.Vector3 min,max;Bounds(v,out min,out max);
            Log("PAPEL_AUTORAL_C5_FRAME",Metadata()+"; Frames="+frames+"; Owner="+owner+"; ProxyPosition="+parent.Position+"; ProxyRotation="+parent.Rotation+"; SurfaceMin="+min+"; SurfaceMax="+max+"; ProxyHasPhysics="+parent.HasPhysics+"; ProxyCollision="+parent.IsCollisionEnabled+"; ProxyVisible="+parent.IsVisible+"; SurfaceNativeEntity=False; FoldedColliderProved=False; ParentTransformSetter=False; PerFrameAttach=False; OpeningByClock=False; VisualApproval=False");}
    }
    private bool ObserveCamera(string phase,bool always)
    {
        bool own=camera.Exists();GTA.Camera rendered=GTA.ScriptCameraDirector.RenderingCam;bool rendering=!System.Object.ReferenceEquals(rendered,null)&&rendered.Exists();
        GTA.Math.Vector3 pos=own?camera.Position:new GTA.Math.Vector3(float.NaN,float.NaN,float.NaN),rot=own?camera.Rotation:new GTA.Math.Vector3(float.NaN,float.NaN,float.NaN);
        float fov=own?camera.FieldOfView:float.NaN,pd=(pos-cameraPosition).Length(),rd=(rot-cameraRotation).Length(),fd=System.Math.Abs(fov-cameraFov);
        bool ok=own&&camera.Handle==cameraHandle&&camera.MemoryAddress==cameraAddress&&camera.IsActive&&rendering&&rendered.Handle==cameraHandle&&rendered.MemoryAddress==cameraAddress&&Finite(pd)&&Finite(rd)&&Finite(fd)&&pd<=.01f&&rd<=.05f&&fd<=.01f;
        if(always||!ok)Log("PAPEL_AUTORAL_C5_CAMERA",Metadata()+"; Phase="+phase+"; ExpectedPosition="+cameraPosition+"; ExpectedRotation="+cameraRotation+"; ExpectedFov="+Number(cameraFov)+"; OwnExists="+own+"; OwnActive="+(own&&camera.IsActive)+"; ActualPosition="+pos+"; ActualRotation="+rot+"; ActualFov="+Number(fov)+"; RenderedHandle="+(rendering?rendered.Handle:-1)+"; RenderedAddress="+(rendering?rendered.MemoryAddress:System.IntPtr.Zero)+"; PositionDelta="+Number(pd)+"; RotationDelta="+Number(rd)+"; FovDelta="+Number(fd)+"; ExactMatch="+ok+"; LoggedBeforeVerdict=True");return ok;
    }
    private GTA.Math.Vector3[] WorldVertices()
    {
        GTA.Math.Vector3[] v=new GTA.Math.Vector3[local.Length];for(int i=0;i<local.Length;i++){v[i]=parent.GetOffsetPosition(local[i]);Vector(v[i],30000);if((parent.GetPositionOffset(v[i])-local[i]).Length()>.001f)throw Error("rigid matrix roundtrip exceeded.001m");}return v;
    }
    private float SurfaceDistance(GTA.Math.Vector3 point)
    {GTA.Math.Vector3[] v=WorldVertices();float result=float.MaxValue;int[,] triangles=new int[,]{{0,1,2},{0,2,3},{4,5,6},{4,6,7},{0,3,7},{0,7,4}};for(int i=0;i<6;i++)result=System.Math.Min(result,TriangleDistance(point,v[triangles[i,0]],v[triangles[i,1]],v[triangles[i,2]]));if(!Finite(result))throw Error("surface distance nonfinite");return result;}
    private static float TriangleDistance(GTA.Math.Vector3 p,GTA.Math.Vector3 a,GTA.Math.Vector3 b,GTA.Math.Vector3 c)
    {
        GTA.Math.Vector3 ab=b-a,ac=c-a,ap=p-a;float d1=GTA.Math.Vector3.Dot(ab,ap),d2=GTA.Math.Vector3.Dot(ac,ap);
        if(d1<=0&&d2<=0)return(p-a).Length();GTA.Math.Vector3 bp=p-b;float d3=GTA.Math.Vector3.Dot(ab,bp),d4=GTA.Math.Vector3.Dot(ac,bp);
        if(d3>=0&&d4<=d3)return(p-b).Length();float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return(p-(a+ab*(d1/(d1-d3)))).Length();
        GTA.Math.Vector3 cp=p-c;float d5=GTA.Math.Vector3.Dot(ab,cp),d6=GTA.Math.Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return(p-c).Length();
        float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return(p-(a+ac*(d2/(d2-d6)))).Length();
        float va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return(p-(b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6))))).Length();
        float total=va+vb+vc;if(!Finite(total)||System.Math.Abs(total)<1e-15f)throw Error("authored triangle degenerate");return(p-(a+ab*(vb/total)+ac*(vc/total))).Length();
    }
    private static void Quad(GTA.Math.Vector3 a,GTA.Math.Vector3 b,GTA.Math.Vector3 c,GTA.Math.Vector3 d,int shade)
    {Triangle(a,b,c,shade);Triangle(a,c,d,shade);Triangle(c,b,a,shade);Triangle(d,c,a,shade);}
    private static void Triangle(GTA.Math.Vector3 a,GTA.Math.Vector3 b,GTA.Math.Vector3 c,int shade)
    {GTA.Native.Function.Call((GTA.Native.Hash)0xAC26716048436851UL,a.X,a.Y,a.Z,b.X,b.Y,b.Z,c.X,c.Y,c.Z,shade,shade,shade,255);}
    public void Dispose()
    {
        if(disposed)return;if(parent.Exists()){RequireParent(parent);if(hidden){parent.IsVisible=originalVisible;if(parent.IsVisible!=originalVisible)throw Error("own drawable restore failed; retain ref forF10");}}
        hidden=false;ready=false;disposed=true;Log("PAPEL_AUTORAL_C5_CLEANUP",Metadata()+"; VisibilityRestoredOrOwnAbsent=True; CameraChanged=False; ProxyDeletedByNormalOwnerOnly=True; DrawStopped=True");
    }
    private string Metadata(){return"Scene=EP08_C05; SourceSHA256="+ExactSourceHash+"; ProxyModel=prop_amanda_note_01; ProxyHandle="+parentHandle+"; ProxyAddress="+parentAddress+"; DonorHandle="+donorHandle+"; ReceiverHandle="+receiverHandle+"; CameraHandle="+cameraHandle+"; CameraAddress="+cameraAddress+"; GeometryAuthored=True; SurfaceNativeEntity=False; FoldedColliderProved=False; PaperWordsForExternalEditor=True";}
    private static void Ped(GTA.Ped p){if(System.Object.ReferenceEquals(p,null)||!p.Exists()||p.MemoryAddress==System.IntPtr.Zero||!p.IsPersistent||p.IsDead||p.IsPlayer||p.IsInVehicle())throw Error("actual own live registered actor required");Vector(p.Position,30000);}
    private static void Story(){if(GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS))throw Error("Story only");}
    private static void Bounds(GTA.Math.Vector3[] v,out GTA.Math.Vector3 min,out GTA.Math.Vector3 max){min=max=v[0];foreach(GTA.Math.Vector3 p in v){min.X=System.Math.Min(min.X,p.X);min.Y=System.Math.Min(min.Y,p.Y);min.Z=System.Math.Min(min.Z,p.Z);max.X=System.Math.Max(max.X,p.X);max.Y=System.Math.Max(max.Y,p.Y);max.Z=System.Math.Max(max.Z,p.Z);}}
    private static GTA.Math.Vector3 Unit(int axis){return axis==0?new GTA.Math.Vector3(1,0,0):axis==1?new GTA.Math.Vector3(0,1,0):new GTA.Math.Vector3(0,0,1);}
    private static float Component(GTA.Math.Vector3 p,int axis){return axis==0?p.X:axis==1?p.Y:p.Z;}
    private static bool Finite(float n){return!float.IsNaN(n)&&!float.IsInfinity(n);}
    private static void Vector(GTA.Math.Vector3 p,float limit){if(!Finite(p.X)||!Finite(p.Y)||!Finite(p.Z)||System.Math.Abs(p.X)>limit||System.Math.Abs(p.Y)>limit||System.Math.Abs(p.Z)>limit)throw Error("nonfinite/outside own vector");}
    private static string Number(float n){return n.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
    private void Log(string code,string value){if(log!=null)log(code,value);}
    private static System.InvalidOperationException Error(string text){return new System.InvalidOperationException("PapelAutoralC5: "+text);}
}

// Technical98 only. Visual triangles and native proxy physics are separate proofs.
public sealed class PapelBolso98Diretor : System.IDisposable
{
    public const string ExactSourceHash = "6B40ACD0DB0BCB8FC0B398564279991E59F72D2D1B8AAFE7E1873D4E5DB5E7A9";
    public const string PocketDict = "anim@mugging@victim@toss_ped@";
    public const string PocketClip = "throw_object_right_pocket_female";
    private readonly ObjetosSerieDiretor objects;
    private readonly GTA.Prop parent;
    private readonly GTA.Ped actor;
    private readonly GTA.Camera camera;
    private readonly int propHandle, propModel, actorHandle, actorModel, cameraHandle;
    private readonly System.IntPtr propAddress, actorAddress, cameraAddress;
    private readonly bool originalVisible;
    private readonly GTA.Math.Vector3[] local, heldLocal;
    private readonly GTA.Math.Vector3 heldGripLocal;
    private readonly System.Action<string,string> log;
    private bool started, ready, disposed, hidden, acquired, phaseKnown;
    private float previousPhase;
    private int startedAt, startedFrame, nextLog, nextFrameLog, frames, acquisitionFrame=-1;
    private bool gripPreviousKnown;
    private GTA.Math.Vector3 gripPreviousHand,gripPreviousProxy;
    private int gripPreviousFrame=-1,gripNextLog=-1,gripPreviousSerial,lastGripUpdateElapsed=-1;
    private string gripObserver="PREP",gripPreviousObserver="UNAVAILABLE";

    public PapelBolso98Diretor(ObjetosSerieDiretor ownObjects, GTA.Ped ownRich, GTA.Camera ownCamera,
        string sourceHash, System.Action<string,string> logger)
    {
        objects=ownObjects;actor=ownRich;camera=ownCamera;log=logger;
        if(sourceHash!=ExactSourceHash||objects==null||!objects.IsReady)throw Error("exact technical98 source/own manager required");
        Ped(actor);parent=objects.GetProp("PAPEL_BOLSO");
        if(actor.Model.Hash!=unchecked((int)GTA.StringHash.AtStringHash("mp_f_freemode_01"))||
           !parent.Exists()||parent.MemoryAddress==System.IntPtr.Zero||!parent.IsPersistent||!parent.IsVisible||!parent.HasDrawable||
           parent.Model.Hash!=unchecked((int)GTA.StringHash.AtStringHash("prop_amanda_note_01"))||!parent.Model.IsCollisionLoaded||
           !parent.IsAttachedTo(actor)||camera==null||!camera.Exists()||camera.MemoryAddress==System.IntPtr.Zero)
            throw Error("registered Rich/native static note proxy and own camera required");
        propHandle=parent.Handle;propModel=parent.Model.Hash;propAddress=parent.MemoryAddress;
        actorHandle=actor.Handle;actorModel=actor.Model.Hash;actorAddress=actor.MemoryAddress;
        cameraHandle=camera.Handle;cameraAddress=camera.MemoryAddress;originalVisible=parent.IsVisible;
        GTA.Math.Vector3 min,max;parent.Model.GetDimensions(out min,out max);Vector(min,1);Vector(max,1);
        GTA.Math.Vector3 ext=max-min;if(ext.X<=0||ext.Y<=0||ext.Z<=0)throw Error("real proxy rest bounds invalid");
        int[] axes=new int[]{0,1,2};for(int i=0;i<2;i++)for(int j=i+1;j<3;j++)if(Component(ext,axes[j])>Component(ext,axes[i])){int t=axes[i];axes[i]=axes[j];axes[j]=t;}
        float height=Component(ext,axes[0]),width=Component(ext,axes[1])*.5f;
        if(height<.03f||height>.5f||width<.01f||width>.25f)throw Error("actual proxy dimensions unsuitable for one letter");
        GTA.Math.Vector3 center=(min+max)*.5f,a=Unit(axes[1]),b=Unit(axes[0]),n=Unit(axes[2]);
        float thickness=width*.008f,angle=(float)(177*System.Math.PI/180),half=height*.5f;
        GTA.Math.Vector3 tip=a*(width*(float)System.Math.Cos(angle))+n*(width*(float)System.Math.Sin(angle));
        local=new GTA.Math.Vector3[]{center-b*half-n*(thickness*.5f),center-b*half-a*width-n*(thickness*.5f),
            center+b*half-a*width-n*(thickness*.5f),center+b*half-n*(thickness*.5f),
            center-b*half+n*(thickness*.5f),center-b*half+tip+n*(thickness*.5f),
            center+b*half+tip+n*(thickness*.5f),center+b*half+n*(thickness*.5f)};
foreach(GTA.Math.Vector3 p in local)Vector(p,1);
// Root190 observed a stable attached proxy at HandLocalX=-.0863m. This is
// a measured-run candidate, not an author offset or proof of skin contact.
// Transform it once in PREP; never follow the hand or change geometry in ACTION.
GTA.PedBone hand=actor.Bones[GTA.Bone.SkelRightHand],finger=actor.Bones[GTA.Bone.SkelRightFinger12];
if(hand==null||!hand.IsValid||finger==null||!finger.IsValid)throw Error("98 PREP actual hand/finger basis unavailable");
GTA.Math.Vector3 handLocal=parent.GetPositionOffset(hand.Position),handX=hand.GetOffsetPosition(new GTA.Math.Vector3(1,0,0))-hand.Position;
Vector(handLocal,1);Vector(handX,2);
if((parent.GetOffsetPosition(handLocal)-hand.Position).Length()>.001f)throw Error("98 PREP hand/proxy roundtrip invalid");
GTA.Math.Vector3 measuredDelta=parent.GetPositionOffset(parent.Position+handX*.0863f);
Vector(measuredDelta,.35f);
GTA.Math.Vector3 longWorld=parent.GetOffsetPosition(b)-parent.Position;
float toward=GTA.Math.Vector3.Dot(finger.Position-hand.Position,longWorld);
if(!Finite(toward)||System.Math.Abs(toward)<.002f)throw Error("98 real finger direction does not determine folded short-edge grip");
GTA.Math.Vector3 grip=center-b*(toward>0?half:-half),shift=measuredDelta-grip;
if(!Finite(shift.Length())||shift.Length()>.35f)throw Error("98 rigid PREP grip candidate exceeds existing contact radius");
heldGripLocal=measuredDelta;heldLocal=new GTA.Math.Vector3[local.Length];
for(int i=0;i<local.Length;i++){heldLocal[i]=local[i]+shift;Vector(heldLocal[i],1);}
Log("BOLSO98_GRIP_PREP_CANDIDATO",Meta()+"; Frame="+GTA.Game.FrameCount+"; Hand="+hand.Position+"; Proxy="+parent.Position+"; NativeFactoryHandLocal="+handLocal+"; Observed190ProxyHandLocalX=-.0863; FixedProxyLocalCorrection="+measuredDelta+"; ShortEdgeGrip="+grip+"; FixedSurfaceTranslation="+shift+"; FingerLongProjection="+Num(toward)+"; DynamicGripLimit=.025; PREPOnly=True; ResetReusesSameGeometry=True; NativeSetter=False; SkinUnknown=True; VisualApproval=False");
Log("BOLSO98_GEOMETRIA_CONFIGURADA",Meta()+"; NativeRestMin="+min+"; NativeRestMax="+max+"; LeafWidth="+Num(width)+"; Height="+Num(height)+"; Angle177=True; ConstructorReadonly=True");
    }
    public bool IsReady {get{return started&&ready&&hidden&&!disposed;}}
    public bool IsFinished {get{return acquired&&objects.Pocket98Stage==2&&IsReady;}}
    public void RequireParent(GTA.Prop actual)
    {if(actual==null||!actual.Exists()||actual.Handle!=propHandle||actual.MemoryAddress!=propAddress||actual.Model.Hash!=propModel)throw Error("own proxy identity changed");}
    private void Identity()
    {
        Ped(actor);RequireParent(parent);
        if(actor.Handle!=actorHandle||actor.MemoryAddress!=actorAddress||actor.Model.Hash!=actorModel||!parent.IsPersistent||
            !parent.HasDrawable||!parent.Model.IsCollisionLoaded||(parent.Position-actor.Position).Length()>3f)
            throw Error("registered actor/proxy physics/3m bounds changed");
        int state=objects.Pocket98Stage;
        if(state<0||state>2||state<2&&!parent.IsAttachedTo(actor)||state==2&&(parent.IsAttached()||!parent.IsPositionFrozen||!parent.IsCollisionEnabled))
            throw Error("actual pocket/hand/support state differs from own manager");
        Vector(parent.Position,30000);Vector(parent.Rotation,400);
        if(state==1)
        {
            GTA.PedBone hand=actor.Bones[GTA.Bone.SkelRightHand];
            bool handValid=hand!=null&&hand.IsValid;
            GTA.Math.Vector3 actualHand=handValid?hand.Position:GTA.Math.Vector3.Zero,actualProxy=parent.Position;
            float rootGap=handValid?(actualHand-actualProxy).Length():float.NaN;
            float surfaceGap=handValid?SurfaceDistance(actualHand):float.NaN;
            bool gripValid=handValid&&Finite(rootGap)&&Finite(surfaceGap)&&rootGap<=.35f&&surfaceGap<=.35f;
            if(GTA.Game.GameTime>=gripNextLog||!gripValid)
            {
                gripNextLog=GTA.Game.GameTime+100;
                int attachedHandle=GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_ATTACHED_TO,propHandle);
                bool pocketPlaying=GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,actorHandle,PocketDict,PocketClip,3);
                float pocketPhase=GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME,actorHandle,PocketDict,PocketClip);
                bool givePlaying=GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,actorHandle,"mp_common","givetake1_a",3);
                float givePhase=GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME,actorHandle,"mp_common","givetake1_a");
                Log("BOLSO98_GRIP_MEDICAO",Meta()+"; Observer="+gripObserver+"; Frame="+GTA.Game.FrameCount+"; GameTimeMs="+GTA.Game.GameTime+"; LastUpdateElapsedMs="+lastGripUpdateElapsed+
                    "; HandValid="+handValid+"; Hand="+(handValid?actualHand.ToString():"UNAVAILABLE")+"; Proxy="+actualProxy+"; RootGap="+Num(rootGap)+"; SurfaceGap="+Num(surfaceGap)+"; Limit=.35; GripValid="+gripValid+
                    "; PocketPlayingFlag3="+pocketPlaying+"; PocketRawPhase="+Num(pocketPhase)+"; GivePlayingFlag3="+givePlaying+"; GiveRawPhase="+Num(givePhase)+
                    "; NativeAttachedHandle="+attachedHandle+"; AttachedToOwn="+parent.IsAttachedTo(actor)+"; SourceAttachBone=SkelRightHand; SourceAttachBoneTag="+(int)GTA.Bone.SkelRightHand+
                    "; CurrentRealHandBoneIndex="+(handValid?hand.Index:-1)+"; ActualAttachedBoneGetterUnavailable=True; SourceHandOffsetZero=True; ActualHandLocalProxy="+(handValid?hand.GetPositionOffset(actualProxy).ToString():"UNAVAILABLE")+
                    "; PreviousObservationKnown="+gripPreviousKnown+"; PreviousSerial="+gripPreviousSerial+"; PreviousFrame="+gripPreviousFrame+"; PreviousObserver="+gripPreviousObserver+
                    "; PreviousHand="+(gripPreviousKnown?gripPreviousHand.ToString():"UNAVAILABLE")+"; PreviousProxy="+(gripPreviousKnown?gripPreviousProxy.ToString():"UNAVAILABLE")+
                    "; ProxyToPreviousHandGap="+(gripPreviousKnown?Num((actualProxy-gripPreviousHand).Length()):"UNAVAILABLE")+
                    "; HandStep="+(gripPreviousKnown&&handValid?Num((actualHand-gripPreviousHand).Length()):"UNAVAILABLE")+"; ProxyStep="+(gripPreviousKnown?Num((actualProxy-gripPreviousProxy).Length()):"UNAVAILABLE")+
                    "; LoggedBeforeVerdict=True; ToleranceUnchanged=True; AttachmentProcessorCalled=False; NativeSetter=False");
            }
if(!gripValid)throw Error("actual held folded surface lost hand grip; raw transition sample logged");
GTA.Math.Vector3 drawnGrip=parent.GetOffsetPosition(heldGripLocal);
float drawnGripGap=(drawnGrip-actualHand).Length();
bool pendingAttachFrame=GTA.Game.FrameCount==acquisitionFrame;
if(!Finite(drawnGripGap)||!pendingAttachFrame&&drawnGripGap>.025f)
{
    Log("BOLSO98_GRIP_VISUAL_REJEITADO",Meta()+"; Frame="+GTA.Game.FrameCount+"; Hand="+actualHand+"; DrawnGrip="+drawnGrip+"; Gap="+Num(drawnGripGap)+"; Limit=.025; PendingOnlyExactAcquisitionFrame="+pendingAttachFrame+"; NativeSetter=False; SkinUnknown=True");
    throw Error("fixed PREP drawn grip does not meet the actual hand; no per-frame correction");
}
            gripPreviousKnown=true;gripPreviousHand=actualHand;gripPreviousProxy=actualProxy;gripPreviousFrame=GTA.Game.FrameCount;gripPreviousObserver=gripObserver;gripPreviousSerial++;
        }
        if(state==2)objects.RequirePocket98RestedSurface(Vertices());
    }
    public void Start()
    {
        if(started||disposed)throw Error("start state invalid");
        objects.PreparePocket98(ExactSourceHash);started=true;
        HideAndAwaitCamera();
    }
    private void HideAndAwaitCamera()
    {
        Identity();if(!parent.IsVisible)throw Error("own drawable not restored before hide");
        hidden=true;parent.IsVisible=false;ready=false;startedAt=GTA.Game.GameTime;startedFrame=GTA.Game.FrameCount;
        acquired=phaseKnown=false;previousPhase=0;nextLog=0;acquisitionFrame=-1;
        gripPreviousKnown=false;gripPreviousFrame=-1;gripNextLog=-1;gripPreviousSerial=0;lastGripUpdateElapsed=-1;gripObserver="PREP";gripPreviousObserver="UNAVAILABLE";
        Log("BOLSO98_PREP",Meta()+"; PocketParent="+parent.Position+"; ActualStage="+objects.Pocket98Stage+"; AcquisitionCount=0; SpecRightHandWasFactoryBaselineOnly=True; PREPAnchorNotHand=True; ProductionSourceUnchanged=True");
    }
    public bool PollCamera()
    {
        RequireConfigured();if(ready)return true;
        bool match=Camera("PREP",true);
        if(unchecked((uint)(GTA.Game.GameTime-startedAt))>2000u)throw Error("own rendering camera not confirmed in2000ms");
        if(GTA.Game.FrameCount==startedFrame||!match)return false;
        ready=true;Log("BOLSO98_READY",Meta()+"; OwnRenderingConfirmed=True; NoARM=True; Capture=False; PocketActionContactPending=True");return true;
    }
    public void RequireConfigured()
    {if(!started||disposed||!hidden)throw Error("renderer not configured");Identity();if(parent.IsVisible)throw Error("duplicate native drawable forbidden");Vertices();}
    public void RequireActionReady()
    {RequireConfigured();if(!IsReady||!Camera("BEFORE_ACTION",true)||objects.Pocket98Stage!=0||acquired)throw Error("fresh pocket preparation/own rendering required");}
    public void BeforeReset()
    {RequireConfigured();parent.IsVisible=originalVisible;if(parent.IsVisible!=originalVisible)throw Error("own visibility reset failed");hidden=ready=false;}
    public void AfterReset()
    {if(disposed||!started||hidden)throw Error("reset state invalid");objects.PreparePocket98(ExactSourceHash);HideAndAwaitCamera();}
    public void Update(int elapsed)
    {
        gripObserver="UPDATE";lastGripUpdateElapsed=elapsed;
        RequireConfigured();if(!IsReady||!Camera("ACTION",false)||elapsed<0||elapsed>10000)throw Error("technical10s/own rendering required");
        if(acquired)return;
        if(elapsed>=5000)throw Error("pocket clip finished without actual hand/surface contact; no attach or completion");
        bool playing=GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,actorHandle,PocketDict,PocketClip,3);
        float phase=GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME,actorHandle,PocketDict,PocketClip);
        GTA.PedBone hand=actor.Bones[GTA.Bone.SkelRightHand],pelvis=actor.Bones[GTA.Bone.SkelPelvis];
        if(hand==null||!hand.IsValid||pelvis==null||!pelvis.IsValid)throw Error("real right hand/pelvis bones unavailable");
        Vector(hand.Position,30000);Vector(pelvis.Position,30000);
        float rootGap=(hand.Position-parent.Position).Length(),surfaceGap=SurfaceDistance(hand.Position);
        bool phaseValid=Finite(phase)&&phase>=0f&&phase<=1f;
        bool progressed=phaseKnown&&phaseValid&&phase>previousPhase+.001f;
        bool contact=playing&&progressed&&phase>=.1f&&phase<=.65f&&rootGap<=.35f&&surfaceGap<=.35f;
        // Raw getters precede any verdict. -1 is distinct from NaN/Infinity and is never a contact phase.
        if(elapsed>=nextLog||contact||!phaseValid||(!phaseKnown&&!playing))
        {
            nextLog=elapsed+100;
            Log("BOLSO98_AQUISICAO_MEDICAO",Meta()+"; TimeMs="+elapsed+"; Playing="+playing+"; Phase="+Num(phase)+"; PhaseFinite="+Finite(phase)+"; PhaseInRange="+phaseValid+"; ValidPhasePreviouslyObserved="+phaseKnown+"; ProgressObserved="+progressed+"; Hand="+hand.Position+"; Pelvis="+pelvis.Position+"; Proxy="+parent.Position+"; RootGap="+Num(rootGap)+"; SurfaceGap="+Num(surfaceGap)+"; Limit=.35; PhaseMin=.1; PhaseMax=.65; Contact="+contact+"; AttachSetter=False; LoggedBeforeVerdict=True");
        }
        if(!Finite(rootGap)||!Finite(surfaceGap))throw Error("actual contact nonfinite; raw sample logged");
        bool initialSentinel=phase==-1f&&!phaseKnown&&elapsed<500;
        if(!phaseValid&&!initialSentinel)throw Error("actual phase invalid; raw sample logged; only initial -1 before500ms may wait");
        if(!phaseKnown&&(!playing||!phaseValid))
        {
            if(elapsed>=500)throw Error("first actual Playing/valid phase not observed before500ms; no acquisition");
            Log("BOLSO98_CLIP_INICIAL_AGUARDANDO",Meta()+"; TimeMs="+elapsed+"; Playing="+playing+"; Phase="+Num(phase)+"; InitialSentinel="+initialSentinel+"; DeadlineMs=500; TimelinePaused=False; Acquisition=False; ContactVerdictPending=True; NativeSetter=False");
            return;
        }
        if(playing)
        {
            if(!phaseKnown)Log("BOLSO98_CLIP_INICIAL_OBSERVADO",Meta()+"; TimeMs="+elapsed+"; Phase="+Num(phase)+"; Playing=True; PhaseInRange=True; ProgressiveContactStillRequired=True; AttachSetter=False");
            phaseKnown=true;previousPhase=phase;
        }
        if(contact){acquisitionFrame=GTA.Game.FrameCount;objects.AcquirePocket98(ExactSourceHash);acquired=true;Log("BOLSO98_AQUIRIDO",Meta()+"; TimeMs="+elapsed+"; Phase="+Num(phase)+"; NativeAttachedRight=True; AcquisitionCount=1; ActualContactRequired=True; ProductionApproval=False");}
    }
    public void RequireAcquisitionNow()
    {
        RequireConfigured();float phase=GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME,actorHandle,PocketDict,PocketClip);
        GTA.PedBone hand=actor.Bones[GTA.Bone.SkelRightHand];
        if(acquired||acquisitionFrame!=GTA.Game.FrameCount||objects.Pocket98Stage!=0||!Finite(phase)||phase<.1f||phase>.65f||
            !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,actorHandle,PocketDict,PocketClip,3)||hand==null||!hand.IsValid||
            (hand.Position-parent.Position).Length()>.35f||SurfaceDistance(hand.Position)>.35f)
            throw Error("single acquisition needs the same actual progressive contact frame and real pocket clip");
    }
    public bool PlacementContact(GTA.Math.Vector3 hand,GTA.Math.Vector3 point,GTA.Math.Vector3 position,float heading,GTA.Prop table,GTA.Math.Vector3 min,GTA.Math.Vector3 max)
    {
        RequireConfigured();if(!acquired||objects.Pocket98Stage!=1)throw Error("placement requires actual one-time pocket acquisition");
        float handGap=SurfaceDistance(hand),pointGap=SurfaceDistance(point);
        if(handGap>.35f||pointGap>.35f)return false;
        float r=heading*(float)(System.Math.PI/180),co=(float)System.Math.Cos(r),si=(float)System.Math.Sin(r),lowest=float.MaxValue;
        foreach(GTA.Math.Vector3 p in local){GTA.Math.Vector3 v=position+new GTA.Math.Vector3(p.X*co-p.Y*si,p.X*si+p.Y*co,p.Z),q=table.GetPositionOffset(v);Vector(v,30000);Vector(q,1);if(q.X<min.X||q.X>max.X||q.Y<min.Y||q.Y>max.Y)throw Error("rendered folded vertices do not fit measured own table");lowest=System.Math.Min(lowest,v.Z);}
        float planeGap=lowest-point.Z;
        GTA.Math.Vector3[] actualBefore=Vertices();float maximumDelta=0f;
        var releaseVertices=new System.Text.StringBuilder();
        for(int i=0;i<local.Length;i++)
        {
            GTA.Math.Vector3 predictedAfter=position+new GTA.Math.Vector3(local[i].X*co-local[i].Y*si,local[i].X*si+local[i].Y*co,local[i].Z);
            float delta=(actualBefore[i]-predictedAfter).Length();if(!Finite(delta))throw Error("98 nonfinite actual release mapping");
            maximumDelta=System.Math.Max(maximumDelta,delta);
            releaseVertices.Append("; V").Append(i).Append("Before=").Append(actualBefore[i]).Append("; V").Append(i).Append("PredictedAfter=").Append(predictedAfter).Append("; V").Append(i).Append("Delta=").Append(Num(delta));
        }
        Log("BOLSO98_SOLTURA_GEOMETRIA_MEDIDA",Meta()+"; MaxVertexWorldDeltaAfterOrdinaryPlace="+Num(maximumDelta)+"; ParentBefore="+parent.Position+"; ParentRotationBefore="+parent.Rotation+"; PredictedParentAfter="+position+"; PredictedHeadingAfter="+Num(heading)+"; NativeSetter=False; ContinuityAcceptedFromDistance=False; RootBeforeAfterImageRequired=True"+releaseVertices);
        Log("BOLSO98_COLOCAR_SUPERFICIE",Meta()+"; HandSurfaceGap="+Num(handGap)+"; PointSurfaceGap="+Num(pointGap)+"; ProjectedLowestZ="+Num(lowest)+"; MeasuredPlaneZ="+Num(point.Z)+"; ProjectedPlaneGap="+Num(planeGap)+"; NativeTryPlaceGuardsUnchanged=True; RenderedColliderContactClaim=False");
        if(!Finite(planeGap)||planeGap<-.005f||planeGap>.03f)throw Error("projected authored surface does not rest on measured table; no detach");
    GTA.Math.Vector3[] held=Vertices();bool realEdgeContact=false;float actualLowest=float.MaxValue;
    foreach(GTA.Math.Vector3 v in held)
    {
        GTA.Math.Vector3 q=table.GetPositionOffset(v);float g=v.Z-point.Z;
        actualLowest=System.Math.Min(actualLowest,g);
        if(q.X>=min.X&&q.X<=max.X&&q.Y>=min.Y&&q.Y<=max.Y&&g>=-.005f&&g<=.025f)realEdgeContact=true;
    }
    float handPlaneGap=hand.Z-point.Z;
    bool touch=Finite(handPlaneGap)&&handPlaneGap>=-.005f&&handPlaneGap<=.03f&&actualLowest>=-.005f&&realEdgeContact;
    Log("BOLSO98_COLOCAR_CONTATO_ATUAL",Meta()+"; ActualHandPlaneGap="+Num(handPlaneGap)+"; ActualLowestVertexPlaneGap="+Num(actualLowest)+"; ActualVertexInsideOwnSupportAndPlane="+realEdgeContact+"; Contact="+touch+"; GeometryFixedFromPREP=True; NativeTryPlaceGuardsUnchanged=True; NoFloatingPlacementAccepted=True");
    return touch;
}
public GTA.Math.Vector3[] GetSurfaceWorld(){RequireConfigured();return Vertices();}
    public GTA.Math.Vector3 VisualCenter(){GTA.Math.Vector3[] p=GetSurfaceWorld();GTA.Math.Vector3 min,max;Bounds(p,out min,out max);return(min+max)*.5f;}
    public void DrawFrame()
    {
        gripObserver="DRAW";
        RequireConfigured();if(!IsReady||!Camera("FRAME",false))throw Error("own rendering lost");
        GTA.Math.Vector3[] p=Vertices();Quad(p[0],p[1],p[2],p[3],245);Quad(p[4],p[5],p[6],p[7],255);Quad(p[0],p[3],p[7],p[4],225);frames++;
        if(GTA.Game.GameTime>=nextFrameLog){nextFrameLog=GTA.Game.GameTime+500;GTA.Math.Vector3 min,max;Bounds(p,out min,out max);Log("BOLSO98_FRAME",Meta()+"; Frames="+frames+"; Stage="+objects.Pocket98Stage+"; Proxy="+parent.Position+"; SurfaceMin="+min+"; SurfaceMax="+max+"; ParentPerFrameSetter=False; OpeningByClock=False; ProductionApproval=False");}
    }
    private bool Camera(string phase,bool always)
    {
        GTA.Camera current=GTA.ScriptCameraDirector.RenderingCam;
        bool good=camera.Exists()&&camera.Handle==cameraHandle&&camera.MemoryAddress==cameraAddress&&camera.IsActive&&current!=null&&current.Exists()&&current.Handle==cameraHandle&&current.MemoryAddress==cameraAddress;
        if(always||!good)Log("BOLSO98_CAMERA",Meta()+"; Phase="+phase+"; OwnActive="+(camera.Exists()&&camera.IsActive)+"; Rendered="+(current==null?-1:current.Handle)+"; IdentityExact="+good+"; LoggedBeforeVerdict=True");
        if(good){Vector(camera.Position,30000);Vector(camera.Rotation,400);float f=camera.FieldOfView;if(!Finite(f)||f<15||f>55)throw Error("actual optics invalid");}return good;
    }
    private GTA.Math.Vector3[] Vertices()
    {GTA.Math.Vector3[] points=objects.Pocket98Stage==1?heldLocal:local;GTA.Math.Vector3[] result=new GTA.Math.Vector3[points.Length];for(int i=0;i<points.Length;i++){result[i]=parent.GetOffsetPosition(points[i]);Vector(result[i],30000);if((parent.GetPositionOffset(result[i])-points[i]).Length()>.001f)throw Error("rigid matrix roundtrip exceeded.001m");}return result;}
    private float SurfaceDistance(GTA.Math.Vector3 point)
    {Vector(point,30000);GTA.Math.Vector3[] v=Vertices();float result=float.MaxValue;int[,] t=new int[,]{{0,1,2},{0,2,3},{4,5,6},{4,6,7},{0,3,7},{0,7,4}};for(int i=0;i<6;i++)result=System.Math.Min(result,TriangleDistance(point,v[t[i,0]],v[t[i,1]],v[t[i,2]]));if(!Finite(result))throw Error("nonfinite analytic surface distance");return result;}
    private static float TriangleDistance(GTA.Math.Vector3 p,GTA.Math.Vector3 a,GTA.Math.Vector3 b,GTA.Math.Vector3 c)
    {
        GTA.Math.Vector3 ab=b-a,ac=c-a,ap=p-a;float d1=GTA.Math.Vector3.Dot(ab,ap),d2=GTA.Math.Vector3.Dot(ac,ap);
        if(d1<=0&&d2<=0)return(p-a).Length();GTA.Math.Vector3 bp=p-b;float d3=GTA.Math.Vector3.Dot(ab,bp),d4=GTA.Math.Vector3.Dot(ac,bp);
        if(d3>=0&&d4<=d3)return(p-b).Length();float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return(p-(a+ab*(d1/(d1-d3)))).Length();
        GTA.Math.Vector3 cp=p-c;float d5=GTA.Math.Vector3.Dot(ab,cp),d6=GTA.Math.Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return(p-c).Length();
        float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return(p-(a+ac*(d2/(d2-d6)))).Length();
        float va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return(p-(b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6))))).Length();
        float total=va+vb+vc;if(!Finite(total)||System.Math.Abs(total)<1e-15f)throw Error("authored triangle degenerate");return(p-(a+ab*(vb/total)+ac*(vc/total))).Length();
    }
    private static void Quad(GTA.Math.Vector3 a,GTA.Math.Vector3 b,GTA.Math.Vector3 c,GTA.Math.Vector3 d,int shade)
    {Triangle(a,b,c,shade);Triangle(a,c,d,shade);Triangle(c,b,a,shade);Triangle(d,c,a,shade);}
    private static void Triangle(GTA.Math.Vector3 a,GTA.Math.Vector3 b,GTA.Math.Vector3 c,int shade)
    {GTA.Native.Function.Call((GTA.Native.Hash)0xAC26716048436851UL,a.X,a.Y,a.Z,b.X,b.Y,b.Z,c.X,c.Y,c.Z,shade,shade,shade,255);}
    public void Dispose()
    {if(disposed)return;if(parent.Exists()){RequireParent(parent);if(hidden){parent.IsVisible=originalVisible;if(parent.IsVisible!=originalVisible)throw Error("own visibility restore failed; retain ref forF10");}}hidden=ready=false;disposed=true;Log("BOLSO98_CLEANUP",Meta()+"; OwnVisibilityRestoredOrAbsent=True; NormalManagerDeletesProxy=True; DrawStopped=True");}
    private static void Bounds(GTA.Math.Vector3[] v,out GTA.Math.Vector3 min,out GTA.Math.Vector3 max){min=max=v[0];foreach(GTA.Math.Vector3 p in v){min.X=System.Math.Min(min.X,p.X);min.Y=System.Math.Min(min.Y,p.Y);min.Z=System.Math.Min(min.Z,p.Z);max.X=System.Math.Max(max.X,p.X);max.Y=System.Math.Max(max.Y,p.Y);max.Z=System.Math.Max(max.Z,p.Z);}}
    private static GTA.Math.Vector3 Unit(int a){return a==0?new GTA.Math.Vector3(1,0,0):a==1?new GTA.Math.Vector3(0,1,0):new GTA.Math.Vector3(0,0,1);}
    private static float Component(GTA.Math.Vector3 p,int a){return a==0?p.X:a==1?p.Y:p.Z;}
    private static void Ped(GTA.Ped p){if(GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS)||p==null||!p.Exists()||p.MemoryAddress==System.IntPtr.Zero||!p.IsPersistent||p.IsPlayer||p.IsDead||p.IsInVehicle())throw Error("Story/own live persistent actor required");Vector(p.Position,30000);}
    private static bool Finite(float v){return!float.IsNaN(v)&&!float.IsInfinity(v);}
    private static void Vector(GTA.Math.Vector3 v,float limit){if(!Finite(v.X)||!Finite(v.Y)||!Finite(v.Z)||System.Math.Abs(v.X)>limit||System.Math.Abs(v.Y)>limit||System.Math.Abs(v.Z)>limit)throw Error("nonfinite/out of bounds vector");}
    private string Meta(){return"Scene=teste_fila_98; SourceSHA256="+ExactSourceHash+"; Actor=DJANIRA_RICA; ActorHandle="+actorHandle+"; ProxyHandle="+propHandle+"; ProxyAddress="+propAddress+"; CameraHandle="+cameraHandle+"; GeometryAuthored=True; SurfaceNativeEntity=False; FoldedColliderProved=False; Capture=False; ProductionApproval=False";}
    private static string Num(float v){return v.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
    private void Log(string k,string v){if(log!=null)log(k,v);}
    private static System.InvalidOperationException Error(string m){return new System.InvalidOperationException("Bolso98: "+m);}
}


// Exact technical99 getter-only observations. No actor task, renderer or opening assertion.
// Caller must bind only teste_fila_99 + exact source, technical capture=false + no writer.
public sealed class ObservadorAbertura99
{
    public const string ExactSourceSHA = "21FD4802F162A49F8F84C32E22EBB993618943FD327F2254A3B2B9C66BA11520";
    private const string Dict = "switch@michael@reads_script";
    private const string Clip = "001404_01_mics3_16_reads_script_idle";
    private readonly GTA.Ped actor;
    private readonly GTA.Prop proxy;
    private readonly int actorHandle, propHandle, model, widthAxis, heightAxis, normalAxis;
    private readonly System.IntPtr actorAddress, propAddress;
    private readonly GTA.Math.Vector3 crease;
    private readonly float leafWidth, leafHeight;
    private readonly System.Action<string,string> log;
    private int lastFrame = -1, samples, lastElapsed = -1;
    private bool windowActive, phaseKnown;

    public ObservadorAbertura99(string sceneId, string sourceSHA, GTA.Ped ownPoor, GTA.Prop ownProxy,
        bool captureEnabled, bool actualWriterPresent, System.Action<string,string> logger)
    {
        if(sceneId!="teste_fila_99" || sourceSHA!=ExactSourceSHA || captureEnabled || actualWriterPresent)
            throw Error("exact technical no-capture fixture required");
        actor=ownPoor;proxy=ownProxy;log=logger;Identity(false);
        actorHandle=actor.Handle;propHandle=proxy.Handle;model=proxy.Model.Hash;
        actorAddress=actor.MemoryAddress;propAddress=proxy.MemoryAddress;
        GTA.Math.Vector3 min,max;proxy.Model.GetDimensions(out min,out max);Vector(min);Vector(max);
        GTA.Math.Vector3 size=max-min;int[] axes=new int[]{0,1,2};
        for(int i=0;i<2;i++)for(int j=i+1;j<3;j++)if(Component(size,axes[j])>Component(size,axes[i]))
            {int swap=axes[i];axes[i]=axes[j];axes[j]=swap;}
        heightAxis=axes[0];widthAxis=axes[1];normalAxis=axes[2];
        leafWidth=Component(size,widthAxis)*.5f;leafHeight=Component(size,heightAxis);crease=(min+max)*.5f;
        if(leafWidth<.01f || leafWidth>.25f || leafHeight<.03f || leafHeight>.5f)throw Error("own measured rest bounds invalid");
    }
    public void BeginWindow()
    {
        Identity(true); windowActive = true; phaseKnown = false; lastFrame = -1; samples = 0; lastElapsed = -1;
        if(log!=null)log("EP09_OBSERVER99_JANELA_INICIADA","SourceSHA256="+ExactSourceSHA+"; WindowMs=9000; ActorSetter=False; PropSetter=False; ReadingMeasurementOnly=True; OpeningProved=False");
    }
    public void FinishWindow(int actualElapsed)
    {
        Identity(true);
        if(!windowActive || actualElapsed<9000 || lastElapsed<8500 || lastElapsed>=9000 || samples<2 || !phaseKnown)
            throw Error("nine-second observation window incomplete; no opening proof");
        windowActive=false;
        if(log!=null)log("EP09_OBSERVER99_JANELA_CONCLUIDA","SourceSHA256="+ExactSourceSHA+"; ActualEndElapsed="+actualElapsed+"; LastActualSampleMs="+lastElapsed+"; Samples="+samples+"; SamplingWindowMs=9000; PhysicalOpeningProved=False; Capture=False; ProductionApproval=False");
    }
    public void StopWindow(){windowActive=false;lastFrame=-1;}
    public void Observe(int elapsedMs)
    {
        Identity(true);if(!windowActive || elapsedMs<0 || elapsedMs>=9000)throw Error("exact nine-second diagnostic window required");
        if(lastFrame==GTA.Game.FrameCount)return;lastFrame=GTA.Game.FrameCount;
        float phase=GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME,actor.Handle,Dict,Clip);float duration=GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION,Dict,Clip);
        bool playing=GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,actor.Handle,Dict,Clip,3);
        if(!Finite(phase)||!Finite(duration)||duration<=0f||duration>600f)throw Error("actual clip getters nonfinite/invalid");
        if(!playing || phase<0f || phase>1f)
        {
            if(log!=null)log("EP09_OBSERVER99_PHASE_PENDING","TimeMs="+elapsedMs+"; RawPhase="+Num(phase)+"; PlayingFlag3="+playing+"; LimitMs=500; PhaseKnown="+phaseKnown);
            if(phaseKnown || elapsedMs>=500 || phase < -1f || phase>1f)throw Error("actual regular reading clip not ready/progressing");
            return; // Initial normal engine blend only; not an opening/completion sample.
        }
        phaseKnown=true;GTA.Math.Vector3 right=Bone(GTA.Bone.PHRightHand),left=Bone(GTA.Bone.SkelLeftHand);
        float rightGap=(right-proxy.Position).Length();
        if(!Finite(rightGap)||rightGap>.35f)throw Error("actual own PH hand/proxy grip exceeds original.35m");
        samples++;lastElapsed=elapsedMs;
        Sample("SkelLeftFinger12",Bone(GTA.Bone.SkelLeftFinger12),right,left,phase,duration,playing,elapsedMs);
        Sample("SkelLeftFinger02",Bone(GTA.Bone.SkelLeftFinger02),right,left,phase,duration,playing,elapsedMs);
    }
    private void Sample(string bone,GTA.Math.Vector3 world,GTA.Math.Vector3 right,GTA.Math.Vector3 left,
        float phase,float duration,bool playing,int elapsedMs)
    {
        GTA.Math.Vector3 local=proxy.GetPositionOffset(world);Vector(local);
        if((proxy.GetOffsetPosition(local)-world).Length()>.001f)throw Error("rigid transform roundtrip differs");
        GTA.Math.Vector3 relative=local-crease;float w=Component(relative,widthAxis),n=Component(relative,normalAxis);
        float radius=(float)System.Math.Sqrt(w*w+n*n),angle=(float)(System.Math.Atan2(n,w)*180.0/System.Math.PI);
        float along=Component(relative,heightAxis),radialError=System.Math.Abs(radius-leafWidth);
        if(!Finite(radius)||!Finite(angle)||!Finite(radialError))throw Error("actual relative trajectory nonfinite");
        if(log!=null)log("EP09_ABERTURA99_MAO_MEDIDA","Scene=teste_fila_99; SourceSHA256="+ExactSourceSHA+
            "; TimeMs="+elapsedMs+"; Frame="+GTA.Game.FrameCount+"; Bone="+bone+"; ActualWorld="+world+
            "; ActualLocal="+local+"; RightHand="+right+"; LeftHand="+left+"; Proxy="+proxy.Position+
            "; ActualPlayingFlag3="+playing+"; RawClipPhase="+Num(phase)+"; NativeClipSeconds="+Num(duration)+
            "; HingeAngleRawDegrees="+Num(angle)+"; Radius="+Num(radius)+"; LeafWidth="+Num(leafWidth)+
            "; RadialError="+Num(radialError)+"; C5LeafWidth=0.07380104; C5RadiusError="+Num(System.Math.Abs(radius-.07380104f))+"; HeightCoordinate="+Num(along)+"; HalfLeafHeight="+Num(leafHeight*.5f)+
            "; ConfiguredBone=PHRightHand28422; ActualAttachedBoneGetterUnavailable=True; SkinRightHand="+Bone(GTA.Bone.SkelRightHand)+
            "; PhaseUsedToAnimatePaper=False; ActorSetter=False; PropSetter=False; AttachSetter=False; GeometryRendered=False"+
            "; OpeningProved=False; SkinContactProved=False; ProductionApproval=False; ReadingOnlyFixture=True");
    }
    private GTA.Math.Vector3 Bone(GTA.Bone bone)
    {GTA.PedBone result=actor.Bones[bone];if(System.Object.ReferenceEquals(result,null)||!result.IsValid)throw Error("actual bone unavailable");GTA.Math.Vector3 p=result.Position;Vector(p);return p;}
    private void Identity(bool frozenIdentity)
    {
        if(GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS))throw Error("Story only");
        if(System.Object.ReferenceEquals(actor,null)||!actor.Exists()||actor.MemoryAddress==System.IntPtr.Zero||actor.IsDead||actor.IsPlayer||
            actor.IsInVehicle()||!actor.IsPersistent||actor.Model.Hash!=unchecked((int)GTA.StringHash.AtStringHash("mp_f_freemode_01"))||
            System.Object.ReferenceEquals(proxy,null)||!proxy.Exists()||proxy.MemoryAddress==System.IntPtr.Zero||!proxy.IsPersistent||
            !proxy.HasDrawable||!proxy.HasPhysics||!proxy.Model.IsCollisionLoaded||!proxy.IsAttachedTo(actor)||proxy.Model.Hash!=unchecked((int)GTA.StringHash.AtStringHash("p_cs_script_s"))||
            (actor.Position-proxy.Position).Length()>3f)throw Error("same own registered female/right-attached proxy required");
        if(frozenIdentity&&(actor.Handle!=actorHandle||proxy.Handle!=propHandle||actor.MemoryAddress!=actorAddress||
            proxy.MemoryAddress!=propAddress||proxy.Model.Hash!=model))throw Error("actual own identity changed");
        Vector(actor.Position);Vector(proxy.Position);
    }
    private static bool Finite(float v){return!float.IsNaN(v)&&!float.IsInfinity(v);}
    private static void Vector(GTA.Math.Vector3 v){if(!Finite(v.X)||!Finite(v.Y)||!Finite(v.Z)||System.Math.Abs(v.X)>30000||System.Math.Abs(v.Y)>30000||System.Math.Abs(v.Z)>30000)throw Error("nonfinite/outside vector");}
    private static float Component(GTA.Math.Vector3 p,int a){return a==0?p.X:a==1?p.Y:p.Z;}
    private static string Num(float v){return v.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
    private static System.InvalidOperationException Error(string text){return new System.InvalidOperationException("ObservadorAbertura99: "+text);}
}

// Private technical99 option. Normal native scenario intro; no invented enter clip.
// Append to existing CartaFisicaDiretor source to keep the same37 sources/one Script.
public sealed class MapaEntrada99Diretor : System.IDisposable
{
    public const string ExactSourceSHA = "5CDC4878E18C349DE4D389BC6AB26CDDA2A71FF933A5FE8644D3BE5E3E6F3585";
    private const string Scenario = "WORLD_HUMAN_TOURIST_MAP";
    private readonly GTA.Ped actor;
    private readonly int handle,model;
    private readonly System.IntPtr address;
    private readonly GTA.Math.Vector3 origin;
    private readonly System.Action<string,string> log;
    private readonly System.Collections.Generic.HashSet<int> before = new System.Collections.Generic.HashSet<int>();
    private bool begun,started,namedOnce,stopRequested,disposed;
    private int nextLog,lastElapsed=-1,lastSample=-1,observations;
    public MapaEntrada99Diretor(string sceneId,string sourceSHA,GTA.Ped ownPoor,bool captureEnabled,bool writerPresent,System.Action<string,string> logger)
    {
        if(sceneId!="teste_fila_99"||sourceSHA!=ExactSourceSHA||captureEnabled||writerPresent)
            throw Error("exact technical99 source/NoCapture/NoWriter required");
        actor=ownPoor;log=logger;
        if(System.Object.ReferenceEquals(actor,null)||!actor.Exists()||actor.MemoryAddress==System.IntPtr.Zero)
            throw Error("own saved Poor missing");
        handle=actor.Handle;model=actor.Model.Hash;address=actor.MemoryAddress;origin=actor.Position;RequireActor();
        foreach(GTA.Prop prop in GTA.World.GetNearbyProps(origin,2f))
            if(!System.Object.ReferenceEquals(prop,null)&&prop.Exists())before.Add(prop.Handle);
        Log("EP09_MAPA99_PREPARADO","Scene=teste_fila_99; SourceSHA256="+ExactSourceSHA+"; WindowMs=9000; Scenario="+Scenario+
            "; IntroRequested=False; ManualProps=0; PaperOpeningProved=False; ProductionApproval=False");
    }
    public void BeginWindow()
    {
        RequireActor();if(started&&!ReleaseActorsForReset())throw Error("own prior scenario cleanup pending");
        begun=true;started=namedOnce=stopRequested=false;lastElapsed=lastSample=-1;nextLog=observations=0;
    }
    public void Start()
    {
        RequireActor();if(!begun||started)throw Error("reset/begin before scenario start");
        started=true;actor.Task.StartScenarioInPlace(Scenario,0,true);
        Log("EP09_MAPA99_INTRO_PEDIDA","SourceSHA256="+ExactSourceSHA+"; Handle="+handle+"; InPlace=True; PlayIntroClip=True; TimeToLeaveMs=0; WarpArgument=False; EnterClipNameInvented=False; NoManualAttach=True; OpeningProved=False");
    }
    public void Observe(int elapsed)
    {
        RequireActor();if(!started||elapsed<0||elapsed>=9000||elapsed<lastElapsed)throw Error("exact9s observation clock required");
        lastElapsed=elapsed;bool named=UsingScenario();
        if(named){namedOnce=true;observations++;}
        else if(namedOnce||elapsed>=2000)throw Error("named scenario unavailable/stopped on the same female");
        if(elapsed<nextLog)return;nextLog=elapsed+100;lastSample=elapsed;
        GTA.Math.Vector3 right=Bone(GTA.Bone.PHRightHand),left=Bone(GTA.Bone.SkelLeftHand);
        Log("EP09_MAPA99_NATIVE_AMOSTRA","ElapsedMs="+elapsed+"; Frame="+GTA.Game.FrameCount+"; NamedScenario="+named+"; Position="+actor.Position+
            "; Velocity="+actor.Velocity+"; Frozen="+actor.IsPositionFrozen+"; RightPH="+right+"; LeftHand="+left+
            "; NoManualProps=True; IntroObserved=False; PaperOpeningProved=False; ProductionApproval=False");
        Clip("amb@world_human_tourist_map@female@base","base",elapsed);
        string[] femaleA=new string[]{"idle_a","idle_b","idle_c"},femaleB=new string[]{"idle_d","idle_e"};
        foreach(string clip in femaleA)Clip("amb@world_human_tourist_map@female@idle_a",clip,elapsed);
        foreach(string clip in femaleB)Clip("amb@world_human_tourist_map@female@idle_b",clip,elapsed);
        Clip("amb@world_human_tourist_map@male@base","base",elapsed);
        ObserveProps(elapsed,right,left);
    }
    private void ObserveProps(int elapsed,GTA.Math.Vector3 right,GTA.Math.Vector3 left)
    {
        int inspected=0,attached=0,matching=0;
        foreach(GTA.Prop prop in GTA.World.GetNearbyProps(actor.Position,2f))
        {
            if(inspected++>=32)break;
            if(System.Object.ReferenceEquals(prop,null)||!prop.Exists()||before.Contains(prop.Handle)||prop.MemoryAddress==System.IntPtr.Zero||
                GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE,prop.Handle)!=3||!prop.IsAttachedTo(actor))continue;
            attached++;int actualModel=prop.Model.Hash;
            bool map=actualModel==unchecked((int)GTA.StringHash.AtStringHash("prop_tourist_map_01"))||actualModel==unchecked((int)GTA.StringHash.AtStringHash("p_tourist_map_01_s"));
            GTA.Math.Vector3 min,max;prop.Model.GetDimensions(out min,out max);Vector(min);Vector(max);Vector(prop.Position);Vector(prop.Rotation);
            if(min.X>=max.X||min.Y>=max.Y||min.Z>=max.Z||(max-min).Length()>3f)throw Error("actual scenario prop bounds invalid");
            Log("EP09_MAPA99_PROP_OBSERVADO","ElapsedMs="+elapsed+"; Handle="+prop.Handle+"; Address="+prop.MemoryAddress.ToInt64().ToString("X")+
                "; Model="+actualModel+"; Position="+prop.Position+"; Rotation="+prop.Rotation+"; RestMin="+min+"; RestMax="+max+
                "; AttachedToOwnPed=True; AbsentBefore=True; AllowedInstalledMap="+map+"; HasDrawable="+prop.HasDrawable+"; HasPhysics="+prop.HasPhysics+
                "; CollisionEnabled="+prop.IsCollisionEnabled+"; ModelCollisionLoaded="+prop.Model.IsCollisionLoaded+
                "; ManualAttach=False; PropAdopted=False; NativeEntityOpeningProved=False");
            if(!map)continue;matching++;
            Sample(prop,"SkelLeftFinger12",Bone(GTA.Bone.SkelLeftFinger12),right,left,min,max,elapsed);
            Sample(prop,"SkelLeftFinger02",Bone(GTA.Bone.SkelLeftFinger02),right,left,min,max,elapsed);
        }
        Log("EP09_MAPA99_PROPS_RESUMO","ElapsedMs="+elapsed+"; NewAttached="+attached+"; MatchingInstalledMaps="+matching+
            "; PoolInspected="+System.Math.Min(inspected,32)+"; PoolCapped="+(inspected>32)+"; ManualProps=0; NoDelete=True; NoDraw=True; NoFoldAnimationByClock=True; OpeningProved=False");
    }
    private void Sample(GTA.Prop prop,string bone,GTA.Math.Vector3 world,GTA.Math.Vector3 right,GTA.Math.Vector3 left,GTA.Math.Vector3 min,GTA.Math.Vector3 max,int elapsed)
    {
        GTA.Math.Vector3 local=prop.GetPositionOffset(world);Vector(local);
        if((prop.GetOffsetPosition(local)-world).Length()>.001f)throw Error("actual rigid local/world roundtrip differs");
        Log("EP09_MAPA99_MAO_MEDIDA","ElapsedMs="+elapsed+"; Frame="+GTA.Game.FrameCount+"; Bone="+bone+"; ActualWorld="+world+
            "; ActualLocal="+local+"; RightPH="+right+"; LeftHand="+left+"; Proxy="+prop.Position+"; RestMin="+min+"; RestMax="+max+
            "; GapToProxyRight="+Num((right-prop.Position).Length())+"; GapToProxyLeft="+Num((left-prop.Position).Length())+
            "; ReadOnly=True; ActualAttachmentBoneGetterUnavailable=True; SurfaceContactProved=False; OpeningProved=False");
    }
    private void Clip(string dict,string clip,int elapsed)
    {
        bool playing=GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,handle,dict,clip,3);
        if(!playing)return;
        float phase=GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME,handle,dict,clip);
        float duration=GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION,dict,clip);
        if(!Finite(phase)||!Finite(duration))throw Error("actual clip getter nonfinite");
        Log("EP09_MAPA99_CLIP_OBSERVADO","ElapsedMs="+elapsed+"; Dictionary="+dict+"; Clip="+clip+"; PlayingFlag3=True; RawPhase="+Num(phase)+
            "; NativeDuration="+Num(duration)+"; GetterPhaseValid="+(phase>=0f&&phase<=1f&&duration>0f)+"; NotInventedEnter=True; OpeningProved=False");
    }
    public void FinishWindow(int elapsed)
    {
        RequireActor();if(!started||elapsed<9000||lastSample<8500||lastSample>=9000||!namedOnce||observations<2)
            throw Error("full9 named scenario observation incomplete");
        Log("EP09_MAPA99_JANELA_CONCLUIDA","SourceSHA256="+ExactSourceSHA+"; ActualEndMs="+elapsed+"; LastSampleMs="+lastSample+
            "; NamedScenarioObservations="+observations+"; IntroRequested=True; IntroExistenceProved=False; PaperOpeningProved=False; Capture=False; ProductionApproval=False");
        RequestStop();begun=false;
    }
    private bool UsingScenario(){return GTA.Native.Function.Call<bool>((GTA.Native.Hash)0x1BF094736DD62C2EUL,handle,Scenario);}
    private void RequestStop()
    {
        RequireIdentity();if(!started||stopRequested)return;stopRequested=true;actor.Task.ClearAll();
        Log("EP09_MAPA99_STOP_PEDIDO","OwnHandle="+handle+"; SameAddress=True; ClearOwnTaskOnce=True; ForeignTask=False; ManualPropDelete=False");
    }
    public bool ReleaseActorsForReset()
    {
        if(!started)return true;
        if(!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST,handle)){started=false;return true;}
        RequireIdentity();RequestStop();if(UsingScenario())return false;started=begun=namedOnce=stopRequested=false;return true;
    }
    public void Dispose(){if(disposed)return;if(!ReleaseActorsForReset())throw Error("own scenario cleanup pending; retain helper");disposed=true;}
    private void RequireActor()
    {
        RequireIdentity();
        if(actor.IsDead||actor.IsInVehicle()||actor.IsInAir||actor.IsInWater||actor.IsRagdoll||GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS)||
            GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_PED_MALE,handle)||(actor.Position-origin).Length()>.35f)throw Error("same own saved live Poor, dry measured ground/Story/root.35 required");
        Vector(actor.Position);Vector(actor.Velocity);
    }
    private void RequireIdentity()
    {
        if(disposed||!actor.Exists()||actor.Handle!=handle||actor.MemoryAddress!=address||address==System.IntPtr.Zero||actor.Model.Hash!=model||
            model!=unchecked((int)GTA.StringHash.AtStringHash("mp_f_freemode_01"))||actor.IsPlayer||!actor.IsPersistent||
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE,handle)!=1)throw Error("own registered actor identity changed; no foreign task cleanup");
    }
    private GTA.Math.Vector3 Bone(GTA.Bone bone){GTA.PedBone b=actor.Bones[bone];if(System.Object.ReferenceEquals(b,null)||!b.IsValid)throw Error("actual bone unavailable");GTA.Math.Vector3 p=b.Position;Vector(p);return p;}
    private static bool Finite(float v){return!float.IsNaN(v)&&!float.IsInfinity(v);}
    private static void Vector(GTA.Math.Vector3 v){if(!Finite(v.X)||!Finite(v.Y)||!Finite(v.Z)||System.Math.Abs(v.X)>30000||System.Math.Abs(v.Y)>30000||System.Math.Abs(v.Z)>30000)throw Error("actual vector nonfinite/outside");}
    private static string Num(float v){return v.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
    private void Log(string name,string value){if(log!=null)log(name,value);}
    private static System.InvalidOperationException Error(string message){return new System.InvalidOperationException("MapaIntro99: "+message);}
}

// Technical99 only. Two authored rendered leaves; real own proxy collider is separate.
public sealed class PapelAberturaAutoral99Diretor : System.IDisposable
{
    public const string ExactSourceHash="B4FE040296A9565990C3CD265EBF2DB641A03AF772B671A6661DDB466AA37EB9";
    public const string Dict="anim@scripted@ulp_missions@paperwork@heeled@", Clip="action";
    private readonly GTA.Ped actor;
    private readonly GTA.Prop parent;
    private readonly GTA.Camera camera;
    private readonly int actorHandle,parentHandle,model,cameraHandle;
    private readonly System.IntPtr actorAddress,parentAddress,cameraAddress;
    private readonly bool originalVisible;
    private readonly GTA.Math.Vector3 originalRoot,camPosition,camRotation,crease;
    private readonly float originalHeading,camFov,width,height,thickness;
    private readonly int heightAxis,widthAxis,normalAxis;
    private readonly System.Action<string,string> log;
    private bool started,hidden,taskOwned,phaseHeld,observationPrepared,running,finished,disposed,progressed,leftOriginalFailed;
    private int stage,startedAt,heldAt,heldFrame,startAt,lastFrame=-1,nextLog,samples,lastElapsed=-1;
    private int firstLeftFailureMs=-1,leftContactSamples;
    private float actualDuration,maxPhase,angle=177f,firstLeftFailurePhase=-1f,firstLeftContactPhase=-1f;
    private float minimumClosedLeft=float.MaxValue,maximumClosedLeft,minimumOpenLeft=float.MaxValue,maximumOpenLeft;
    private int nextCameraLog,nextBindingLog;

    public PapelAberturaAutoral99Diretor(GTA.Ped ownPoor,GTA.Prop ownParent,GTA.Camera ownCamera,string sourceHash,
        bool captureEnabled,bool writerPresent,System.Action<string,string> logger)
    {
        actor=ownPoor;parent=ownParent;camera=ownCamera;log=logger;
        if(sourceHash!=ExactSourceHash||captureEnabled||writerPresent)throw Error("exact technical source/false capture/no writer required");
        LogConstructionState();
        Identity(false);
        if(!parent.IsVisible||!parent.HasDrawable||!parent.HasPhysics||!parent.Model.IsCollisionLoaded||!parent.IsAttachedTo(actor)||
           System.Object.ReferenceEquals(camera,null)||!camera.Exists()||camera.MemoryAddress==System.IntPtr.Zero)
            throw Error("own visible physically loaded right-attached letter and real camera required");
        actorHandle=actor.Handle;actorAddress=actor.MemoryAddress;parentHandle=parent.Handle;parentAddress=parent.MemoryAddress;model=parent.Model.Hash;
        cameraHandle=camera.Handle;cameraAddress=camera.MemoryAddress;originalVisible=parent.IsVisible;
        Vector(camera.Position,30000);Vector(camera.Rotation,400);if(!Finite(camera.FieldOfView)||camera.FieldOfView<=0f||camera.FieldOfView>=180f)throw Error("finite real camera pose/FOV required");
        originalRoot=actor.Position;originalHeading=actor.Heading;camPosition=camera.Position;camRotation=camera.Rotation;camFov=camera.FieldOfView;
        GTA.Math.Vector3 min,max;parent.Model.GetDimensions(out min,out max);Vector(min,1);Vector(max,1);
        GTA.Math.Vector3 size=max-min;int[] axes=new int[]{0,1,2};
        for(int i=0;i<2;i++)for(int j=i+1;j<3;j++)if(Component(size,axes[j])>Component(size,axes[i])){int t=axes[i];axes[i]=axes[j];axes[j]=t;}
        heightAxis=axes[0];widthAxis=axes[1];normalAxis=axes[2];height=Component(size,heightAxis);width=Component(size,widthAxis)*.5f;
        if(height<.03f||height>.5f||width<.01f||width>.25f)throw Error("measured letter dimensions invalid");
        thickness=width*.008f;crease=(min+max)*.5f;
        Log("PAPEL_ABRE99_DADOS",Meta()+"; Width="+Num(width)+"; Height="+Num(height)+"; RestMin="+min+"; RestMax="+max+"; ReadonlyConstructor=True; ActorTask=False; AttachSetter=False");
    }
    public bool IsReady{get{return false;}} // Diagnostic is never narrative-ready, regardless of contact samples.
    public bool IsObservationPrepared{get{return observationPrepared&&started&&!disposed;}}
    public bool IsObserving{get{return running&&!finished&&!disposed;}}
    public bool IsFinished{get{return finished;}}
    public void StartPreview()
    {
        if(started||disposed)throw Error("fresh preview required");Identity(true);started=true;startedAt=GTA.Game.GameTime;
        GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT,Dict);
    }
    public bool PollPreparation()
    {
        Identity(true);if(!started||running||finished||disposed)throw Error("invalid preparation state");
        if(observationPrepared){RequireObservationPrepared();return true;}
        int elapsed=unchecked(GTA.Game.GameTime-startedAt);if(elapsed<0||elapsed>5000)throw Error("actual own clip/held preparation exceeded5000ms");
        if(stage==0)
        {
            if(!ObserveCamera("PREP_RENDER",GTA.Game.GameTime>=nextCameraLog))
            {nextCameraLog=GTA.Game.GameTime+250;return false;} // Exact own rendering must appear before any task/hide; same5s budget.
            if(!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED,Dict))return false;
            actualDuration=GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION,Dict,Clip);
            bool fits=Finite(actualDuration)&&actualDuration>=.5f&&actualDuration*1000f<=9000f;
            Log("PAPEL_ABRE99_DURACAO",Meta()+"; Dictionary="+Dict+"; Clip="+Clip+"; NativeSeconds="+Num(actualDuration)+"; WindowMs=9000; FullFit="+fits+"; SpeedRequested=1; BeforeReady=True; PropTrackUsed=False");
            if(!fits)throw Error("full actual body clip does not fit9s; do not speed up/truncate or claim full track");
            actor.Task.PlayAnimation(new GTA.CrClipAsset(Dict,Clip),new GTA.AnimationBlendDelta(8f),new GTA.AnimationBlendDelta(-8f),-1,
                GTA.AnimationFlags.UpperBodyOnly|GTA.AnimationFlags.Secondary|GTA.AnimationFlags.StayInEndFrame|GTA.AnimationFlags.ProcessAttachmentsOnStart,
                0f,false,GTA.AnimationIKControlFlags.None);
            taskOwned=true;stage=1;return false;
        }
        if(stage==1)
        {
            bool flag1=GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,actorHandle,Dict,Clip,1);
            bool flag2=GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,actorHandle,Dict,Clip,2);
            bool flag3=Playing();float phase=Current();bool finite=Finite(phase),unknown=finite&&phase==-1f;
            bool inRange=finite&&phase>=0f&&phase<=1f;
            if(GTA.Game.GameTime>=nextBindingLog||(!inRange&&!unknown))
            {
                nextBindingLog=GTA.Game.GameTime+250;
                Log("PAPEL_ABRE99_BINDING_GETTERS",Meta()+"; Stage=AFTER_PLAY_BEFORE_HOLD; PrepElapsedMs="+elapsed+
                    "; Dictionary="+Dict+"; Clip="+Clip+"; NativeDuration="+Num(actualDuration)+"; Current="+Num(phase)+
                    "; Finite="+finite+"; Range0To1="+inRange+"; UnknownMinusOne="+unknown+
                    "; PlayingFlag1="+flag1+"; PlayingFlag2="+flag2+"; PlayingFlag3="+flag3+
                    "; TaskOwned="+taskOwned+"; ParentVisible="+parent.IsVisible+"; PhaseSetter=False; SpeedSetter=False; HideSetter=False; Ready=False; LoggedBeforeVerdict=True; ExistingGlobalBudgetMs=5000");
            }
            if(!finite||(!inRange&&!unknown))throw Error("actual binding phase invalid; raw phase/flags logged before verdict");
            if(!flag3||unknown)return false; // Only not-yet-playing or exact-1 sentinel; original global5s deadline remains above.
            // No phase/speed/visibility setter until the real owned task has valid0..1 phase.
            GTA.Native.Function.Call(GTA.Native.Hash.SET_ENTITY_ANIM_SPEED,actorHandle,Dict,Clip,0f);
            GTA.Native.Function.Call<bool>(GTA.Native.Hash.SET_ENTITY_ANIM_CURRENT_TIME,actorHandle,Dict,Clip,0f);
            phaseHeld=true;heldAt=GTA.Game.GameTime;heldFrame=GTA.Game.FrameCount;stage=2;
            // Own drawable only. Main stores this helper before this reversible mutation.
            hidden=true;parent.IsVisible=false;
            Log("PAPEL_ABRE99_PHASE0_PEDIDA",Meta()+"; PhaseRequested=0; RateRequested=0; WritesOncePreparation=True; NativeBindingObserved=True; ActorTransformSetter=False; AttachSetter=False");
            return false;
        }
        RequireHeld();RequireCamera();ObserveHands("PREP",-1,true,false);
        if(GTA.Game.FrameCount==heldFrame||unchecked(GTA.Game.GameTime-heldAt)<200)return false;
        observationPrepared=true;
        Log("PAPEL_ABRE99_PHASE0_OBSERVAVEL",Meta()+"; NativePhase0HeldObserved=True; ActualCamera=True; ClosedAngle177=True; FullBodyClipFits9=True; ObservationPrepared=True; NarrativeReady=False; LeftOriginalVerdict="+(leftOriginalFailed?"FAIL":"NO_FAILURE_OBSERVED")+"; ImagePending=True; F7OnlyStartsMeasurement=True");return true;
    }
    public void RequirePrepared(){throw Error("diagnostic99 is never narrative-ready; use explicit observation only");}
    public void StartAction(){throw Error("diagnostic99 cannot start narrative action");}
    public void Update(int elapsedMs){throw Error("diagnostic99 cannot enter generic narrative update");}
    public void Finish(int actualElapsed){throw Error("diagnostic99 cannot declare narrative completion/SAVE");}
    public void RequireObservationPrepared()
    {
        Identity(true);if(!IsObservationPrepared||running||finished||!phaseHeld)throw Error("fresh actual phase0 observation required; F10/F6 after measurement");
        RequireHeld();RequireCamera();ObserveHands("BEFORE_OBSERVATION",-1,false,false);
    }
    public void StartObservation()
    {
        RequireObservationPrepared();running=true;startAt=GTA.Game.GameTime;samples=0;lastElapsed=-1;lastFrame=-1;maxPhase=0;progressed=false;nextLog=0;
        GTA.Native.Function.Call(GTA.Native.Hash.SET_ENTITY_ANIM_SPEED,actorHandle,Dict,Clip,1f);
        Log("PAPEL_ABRE99_OBSERVACAO_INICIO",Meta()+"; OwnElapsedOrigin="+startAt+"; WindowMs=9000; NativeRateRequested=1; RateWrittenOnce=True; NoPhaseSetter=True; NoActorSetter=True; NoPropSetter=True; NoAttachSetter=True; RenderAngleFixed177=True; NarrativeStarted=False; ARM=False; SAVE=False");
    }
    public void PollObservation()
    {
        Identity(true);RequireCamera();if(!IsObserving||!phaseHeld)throw Error("exact technical observation state required");
        if(lastFrame==GTA.Game.FrameCount)return;lastFrame=GTA.Game.FrameCount;
        int elapsed=unchecked(GTA.Game.GameTime-startAt);if(elapsed<0||elapsed>9250)throw Error("actual observation exceeded9s plus one250ms sampling tick; no continuation");
        if(elapsed>=9000){FinishObservation(elapsed);return;}
        float phase=Current();bool playing=Playing();
        if(!playing||!Finite(phase)||phase<0f||phase>1f||phase+.001f<maxPhase)throw Error("real owned body clip lost/regressed; no invented phase");
        progressed|=phase>maxPhase+.0001f;maxPhase=System.Math.Max(maxPhase,phase);
        float expected=System.Math.Min(1f,elapsed/(actualDuration*1000f));
        if(!Finite(expected)||System.Math.Abs(phase-expected)>.08f)throw Error("actual body phase differs from real rate1 clock");
        bool record=elapsed>=nextLog;ObserveHands("BODY_FULLTRACK",elapsed,record,true);
        samples++;lastElapsed=elapsed;
        if(record)
        {
            nextLog=elapsed+250;
            Log("PAPEL_ABRE99_OBSERVACAO_FRAME",Meta()+"; TimeMs="+elapsed+"; Phase="+Num(phase)+"; NativeDuration="+Num(actualDuration)+"; PlayingFlag3="+playing+"; MaxPhase="+Num(maxPhase)+"; RenderHingeDegrees=177; Open0GeometryComparedOnly=True; BodyTrackFullObserved="+(progressed&&maxPhase>=.98f)+"; OpeningProved=False; NarrativeReady=False; ActorSetter=False; PropSetter=False; PerFrameAttach=False; VisualApproval=False");
        }
    }
    private void FinishObservation(int actualElapsed)
    {
        Identity(true);RequireCamera();float phase=Current();
        if(!Playing()||!Finite(phase)||phase<0f||phase>1f||phase+.001f<maxPhase)throw Error("actual fulltrack final phase/playing invalid");
        ObserveHands("FINAL_OBSERVATION",actualElapsed,true,false);
        if(!running||actualElapsed<9000||actualElapsed>9250||lastElapsed<8500||samples<2||!progressed||maxPhase<.98f)
            throw Error("body observation incomplete; no narrative/action completion");
        running=false;finished=true;
        Log("PAPEL_ABRE99_OBSERVACAO_FIM",Meta()+"; ActualEndMs="+actualElapsed+"; LastSampleMs="+lastElapsed+"; Samples="+samples+"; MaxBodyPhase="+Num(maxPhase)+"; BodyFullTrackObserved=True; LeftOriginalVerdict="+(leftOriginalFailed?"FAIL":"NO_FAILURE_OBSERVED")+
            "; FirstLeftFailureMs="+firstLeftFailureMs+"; FirstLeftFailurePhase="+Num(firstLeftFailurePhase)+"; ClosedLeftMinimum="+Num(minimumClosedLeft)+"; ClosedLeftMaximum="+Num(maximumClosedLeft)+"; OpenComparisonLeftMinimum="+Num(minimumOpenLeft)+"; OpenComparisonLeftMaximum="+Num(maximumOpenLeft)+"; LeftClosedContactSamples="+leftContactSamples+"; FirstLeftClosedContactPhase="+Num(firstLeftContactPhase)+
            "; RenderedHinge177Unchanged=True; OpeningProved=False; NarrativeReady=False; NarrativeComplete=False; NoCapture=True; NoSAVE=True; ProductionApproval=False; OriginalLeftLimit=.35; ContactVerdictNotOverridden=True");
    }
    public void RequireObservationFinished()
    {
        Identity(true);RequireCamera();if(!finished||running||!IsObservationPrepared)throw Error("completed diagnostic state required");
        ObserveHands("AFTER_DIAGNOSTIC",-1,false,false);
    }
    public void DrawFrame()
    {
        Identity(true);if(!hidden||parent.IsVisible||disposed||!phaseHeld)throw Error("own authored drawable not configured");RequireCamera();
        GTA.Math.Vector3[] v=Vertices();Quad(v[0],v[1],v[2],v[3],245);Quad(v[4],v[5],v[6],v[7],255);Quad(v[0],v[3],v[7],v[4],225);
    }
    private void RequireHeld()
    {float p=Current();if(!Playing()||!Finite(p)||System.Math.Abs(p)>.001f)throw Error("actual phase0 not held; no assumption from setter");angle=177f;}
    private void RequireCamera()
    {if(!ObserveCamera("REQUIRE",false))throw Error("own actual rendering camera differs; observed values logged before verdict");}
    private bool ObserveCamera(string tag,bool record)
    {
        bool ownExists=!System.Object.ReferenceEquals(camera,null)&&camera.Exists();
        GTA.Camera rendered=GTA.ScriptCameraDirector.RenderingCam;
        bool renderedExists=!System.Object.ReferenceEquals(rendered,null)&&rendered.Exists();
        int nativeHandle=GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_RENDERING_CAM);
        if(!ownExists)throw Error("own camera disappeared; cannot wait for foreign identity");
        Vector(camera.Position,30000);Vector(camera.Rotation,400);if(!Finite(camera.FieldOfView))throw Error("real camera finite FOV required");
        float pos=(camera.Position-camPosition).Length(),rot=(camera.Rotation-camRotation).Length(),fov=System.Math.Abs(camera.FieldOfView-camFov);
        bool good=camera.Handle==cameraHandle&&camera.MemoryAddress==cameraAddress&&camera.IsActive&&renderedExists&&
            rendered.Handle==cameraHandle&&rendered.MemoryAddress==cameraAddress&&nativeHandle==cameraHandle&&pos<=.01f&&rot<=.05f&&fov<=.01f;
        if(record||!good)Log("PAPEL_ABRE99_CAMERA_GETTERS",Meta()+"; Stage="+tag+"; OwnExists="+ownExists+"; OwnHandle="+camera.Handle+
            "; OwnAddress="+camera.MemoryAddress+"; OwnActive="+camera.IsActive+"; RenderingNull="+System.Object.ReferenceEquals(rendered,null)+
            "; RenderingExists="+renderedExists+"; RenderingHandle="+(renderedExists?rendered.Handle:0)+
            "; RenderingAddress="+(renderedExists?rendered.MemoryAddress:System.IntPtr.Zero)+"; NativeRenderingHandle="+nativeHandle+
            "; PositionDelta="+Num(pos)+"; RotationDelta="+Num(rot)+"; FOVDelta="+Num(fov)+"; ActualOwnRender="+good+
            "; LoggedBeforeVerdict=True; NoSetter=True; GlobalBudget5000Unchanged=True");
        return good;
    }
    private void LogConstructionState()
    {
        bool ae=!System.Object.ReferenceEquals(actor,null)&&actor.Exists(),pe=!System.Object.ReferenceEquals(parent,null)&&parent.Exists();
        bool ce=!System.Object.ReferenceEquals(camera,null)&&camera.Exists();
        int attached=pe?GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_ATTACHED_TO,parent.Handle):0;
        int type=pe?GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE,parent.Handle):0;
        bool physicalProp=pe&&type==3;
        Log("PAPEL_ABRE99_CTOR_GETTERS","Scene=teste_fila_99; ActorNull="+System.Object.ReferenceEquals(actor,null)+"; ActorExists="+ae+
            "; ActorHandle="+(ae?actor.Handle:0)+"; ActorAddress="+(ae?actor.MemoryAddress:System.IntPtr.Zero)+
            "; ParentNull="+System.Object.ReferenceEquals(parent,null)+"; ParentExists="+pe+"; ParentHandle="+(pe?parent.Handle:0)+
            "; ParentAddress="+(pe?parent.MemoryAddress:System.IntPtr.Zero)+"; ParentType="+type+
            "; Visible="+(pe&&parent.IsVisible)+"; Drawable="+(pe&&parent.HasDrawable)+"; Physics="+(pe&&parent.HasPhysics)+
            "; ModelCOL="+(physicalProp&&parent.Model.IsCollisionLoaded)+"; ParentModel="+(physicalProp?parent.Model.Hash:0)+
            "; AttachedToOwn="+(pe&&ae&&parent.IsAttachedTo(actor))+"; NativeAttachedOwner="+attached+
            "; RightBoneValid="+(ae&&actor.Bones[GTA.Bone.PHRightHand].IsValid)+
            "; CameraNull="+System.Object.ReferenceEquals(camera,null)+"; CameraExists="+ce+"; CameraHandle="+(ce?camera.Handle:0)+
            "; CameraAddress="+(ce?camera.MemoryAddress:System.IntPtr.Zero)+"; CameraActive="+(ce&&camera.IsActive)+
            "; NativeRenderingHandle="+GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_RENDERING_CAM)+
            "; LoggedBeforeVerdict=True; NoSetter=True; AttachmentChange=False; NoHideYet=True");
    }
    private void ObserveHands(string stageName,int elapsedMs,bool record,bool accumulate)
    {
        GTA.Math.Vector3 rh=Bone(GTA.Bone.SkelRightHand),ph=Bone(GTA.Bone.PHRightHand),lh=Bone(GTA.Bone.SkelLeftHand);
        GTA.Math.Vector3[] closed=Vertices(177f),open=Vertices(0f);
        float r=DistanceToSurface(rh,closed),l=DistanceToSurface(lh,closed),ro=DistanceToSurface(rh,open),lo=DistanceToSurface(lh,open),proxy=(ph-parent.Position).Length();
        if(!Finite(r)||!Finite(l)||!Finite(ro)||!Finite(lo)||!Finite(proxy))throw Error("actual hand/surface comparison nonfinite");
        bool holder=r<=.35f&&proxy<=.35f,original=holder&&l<=.35f;
        float phase=Current();if(!Finite(phase)||phase<0f||phase>1f)throw Error("actual measurement phase invalid");
        if(l>.35f&&!leftOriginalFailed){leftOriginalFailed=true;firstLeftFailureMs=elapsedMs;firstLeftFailurePhase=phase;record=true;}
        if(accumulate)
        {
            minimumClosedLeft=System.Math.Min(minimumClosedLeft,l);maximumClosedLeft=System.Math.Max(maximumClosedLeft,l);
            minimumOpenLeft=System.Math.Min(minimumOpenLeft,lo);maximumOpenLeft=System.Math.Max(maximumOpenLeft,lo);
            if(l<=.35f){leftContactSamples++;if(firstLeftContactPhase<0f)firstLeftContactPhase=phase;}
        }
        if(record||!holder)
        {
            bool flag1=GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,actorHandle,Dict,Clip,1);
            bool flag2=GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,actorHandle,Dict,Clip,2);
            Log("PAPEL_ABRE99_MAOS_MEDIDAS",Meta()+"; Stage="+stageName+"; TimeMs="+elapsedMs+"; Phase="+Num(phase)+"; PlayingFlag1="+flag1+"; PlayingFlag2="+flag2+"; PlayingFlag3="+Playing()+
                "; RightHand="+rh+"; LeftHand="+lh+"; PHRightHand="+ph+"; Proxy="+parent.Position+"; RightLocalInProxy="+parent.GetPositionOffset(rh)+"; LeftLocalInProxy="+parent.GetPositionOffset(lh)+
                "; Closed177RightGap="+Num(r)+"; Closed177LeftGap="+Num(l)+"; Open0ComparisonRightGap="+Num(ro)+"; Open0ComparisonLeftGap="+Num(lo)+"; PHProxyGap="+Num(proxy)+"; Limit=.35; OriginalBothHandsVerdict="+(original?"WITHIN_LIMIT":"FAIL")+
                "; LeftOriginalFailureEver="+leftOriginalFailed+"; HolderRequiredImmediately="+holder+"; Comparison0NotRendered=True; Render177Only=True; OpeningProved=False; ExactSkinGripProved=False; ColliderFolded=False; LoggedBeforeVerdict=True; ContactThresholdChanged=False; ActorSetter=False; PropSetter=False; AttachSetter=False");
        }
        if(!holder)throw Error("actual own RIGHT holder/rendered folded surface or PH/proxy contact exceeds original.35m");
        // LEFT original .35 verdict remains recorded FAIL; this observation is never a narrative/contact approval.
    }
    private GTA.Math.Vector3[] Vertices(){if(angle!=177f)throw Error("diagnostic rendered hinge must remain closed177");return Vertices(angle);}
    private GTA.Math.Vector3[] Vertices(float comparisonAngle)
    {
        if(!Finite(comparisonAngle)||(comparisonAngle!=0f&&comparisonAngle!=177f))throw Error("exact closed/open comparison angle required");
        GTA.Math.Vector3 a=Unit(widthAxis),b=Unit(heightAxis),n=Unit(normalAxis);float rad=comparisonAngle*(float)System.Math.PI/180f;
        GTA.Math.Vector3 tip=a*(width*(float)System.Math.Cos(rad))+n*(width*(float)System.Math.Sin(rad));float hh=height*.5f,t=thickness*.5f;
        GTA.Math.Vector3[] local=new GTA.Math.Vector3[]{crease-b*hh-n*t,crease-b*hh-a*width-n*t,crease+b*hh-a*width-n*t,crease+b*hh-n*t,
            crease-b*hh+n*t,crease-b*hh+tip+n*t,crease+b*hh+tip+n*t,crease+b*hh+n*t};
        GTA.Math.Vector3[] world=new GTA.Math.Vector3[8];for(int i=0;i<8;i++){world[i]=parent.GetOffsetPosition(local[i]);Vector(world[i],30000);if((parent.GetPositionOffset(world[i])-local[i]).Length()>.001f)throw Error("own rigid transform roundtrip>.001m");}return world;
    }
    private static float DistanceToSurface(GTA.Math.Vector3 p,GTA.Math.Vector3[] v)
    {int[,] tri=new int[,]{{0,1,2},{0,2,3},{4,5,6},{4,6,7},{0,3,7},{0,7,4}};float d=float.MaxValue;for(int i=0;i<6;i++)d=System.Math.Min(d,TriangleDistance(p,v[tri[i,0]],v[tri[i,1]],v[tri[i,2]]));return d;}
    private static float TriangleDistance(GTA.Math.Vector3 p,GTA.Math.Vector3 a,GTA.Math.Vector3 b,GTA.Math.Vector3 c)
    {
        GTA.Math.Vector3 ab=b-a,ac=c-a,ap=p-a;float d1=GTA.Math.Vector3.Dot(ab,ap),d2=GTA.Math.Vector3.Dot(ac,ap);
        if(d1<=0&&d2<=0)return(p-a).Length();GTA.Math.Vector3 bp=p-b;float d3=GTA.Math.Vector3.Dot(ab,bp),d4=GTA.Math.Vector3.Dot(ac,bp);
        if(d3>=0&&d4<=d3)return(p-b).Length();float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return(p-(a+ab*(d1/(d1-d3)))).Length();
        GTA.Math.Vector3 cp=p-c;float d5=GTA.Math.Vector3.Dot(ab,cp),d6=GTA.Math.Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return(p-c).Length();
        float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return(p-(a+ac*(d2/(d2-d6)))).Length();
        float va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return(p-(b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6))))).Length();
        float total=va+vb+vc;if(!Finite(total)||System.Math.Abs(total)<1e-15f)throw Error("authored triangle degenerate");return(p-(a+ab*(vb/total)+ac*(vc/total))).Length();
    }
    private void Identity(bool same)
    {
        if(GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS))throw Error("Story only");
        if(System.Object.ReferenceEquals(actor,null)||!actor.Exists()||actor.MemoryAddress==System.IntPtr.Zero||!actor.IsPersistent||actor.IsDead||actor.IsPlayer||
           actor.IsInVehicle()||actor.IsInAir||!actor.HasPhysics||!actor.IsCollisionEnabled||actor.Model.Hash!=unchecked((int)GTA.StringHash.AtStringHash("mp_f_freemode_01"))||
           System.Object.ReferenceEquals(parent,null)||!parent.Exists()||parent.MemoryAddress==System.IntPtr.Zero||!parent.IsPersistent||
           !parent.IsAttachedTo(actor)||!parent.HasPhysics||!parent.HasDrawable||!parent.Model.IsCollisionLoaded||parent.Model.Hash!=unchecked((int)GTA.StringHash.AtStringHash("prop_amanda_note_01")))
            throw Error("same healthy own grounded female/attached real letter proxy required");
        if(same&&(actor.Handle!=actorHandle||actor.MemoryAddress!=actorAddress||parent.Handle!=parentHandle||parent.MemoryAddress!=parentAddress||parent.Model.Hash!=model||
           (actor.Position-originalRoot).Length()>.05f||System.Math.Abs(actor.Heading-originalHeading)>.5f))throw Error("own identity/root/heading changed");
        Vector(actor.Position,30000);Vector(parent.Position,30000);Vector(parent.Rotation,400);
    }
    private GTA.Math.Vector3 Bone(GTA.Bone b){GTA.PedBone q=actor.Bones[b];if(System.Object.ReferenceEquals(q,null)||!q.IsValid)throw Error("actual own bone absent");GTA.Math.Vector3 p=q.Position;Vector(p,30000);return p;}
    private bool Playing(){return GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,actorHandle,Dict,Clip,3);}
    private float Current(){return GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME,actorHandle,Dict,Clip);}
    public void Dispose()
    {
        if(disposed)return;
        if(taskOwned&&actor.Exists()&&actor.Handle==actorHandle&&actor.MemoryAddress==actorAddress)
        {GTA.Native.Function.Call(GTA.Native.Hash.STOP_ANIM_TASK,actorHandle,Dict,Clip,-8f);taskOwned=false;}
        if(parent.Exists())
        {if(parent.Handle!=parentHandle||parent.MemoryAddress!=parentAddress||parent.Model.Hash!=model)throw Error("retain cleanup reference: own proxy identity changed");if(hidden){parent.IsVisible=originalVisible;if(parent.IsVisible!=originalVisible)throw Error("retain cleanup reference: visibility restore failed");}}
        hidden=observationPrepared=running=false;disposed=true;Log("PAPEL_ABRE99_CLEANUP",Meta()+"; OwnAnimationStopped=True; OwnVisibilityRestoredOrAbsent=True; AttachChanges=0; CameraSetter=False; NoForeignPropDeletion=True");
    }
    private static void Quad(GTA.Math.Vector3 a,GTA.Math.Vector3 b,GTA.Math.Vector3 c,GTA.Math.Vector3 d,int shade){Tri(a,b,c,shade);Tri(a,c,d,shade);Tri(c,b,a,shade);Tri(d,c,a,shade);}
    private static void Tri(GTA.Math.Vector3 a,GTA.Math.Vector3 b,GTA.Math.Vector3 c,int shade){GTA.Native.Function.Call((GTA.Native.Hash)0xAC26716048436851UL,a.X,a.Y,a.Z,b.X,b.Y,b.Z,c.X,c.Y,c.Z,shade,shade,shade,255);}
    private string Meta(){return"Scene=teste_fila_99; SourceSHA256="+ExactSourceHash+"; OwnProxy=prop_amanda_note_01; ProxyHandle="+parentHandle+"; ActorHandle="+actorHandle+"; BodyAuthoredFemaleResource=True; GeometryAuthored=True; NativePaperOpening=False; FoldedColliderProved=False; ColliderProxyOnly=True; FullObjectTrackUsed=False; ProductionApproval=False";}
    private static GTA.Math.Vector3 Unit(int axis){return axis==0?new GTA.Math.Vector3(1,0,0):axis==1?new GTA.Math.Vector3(0,1,0):new GTA.Math.Vector3(0,0,1);}
    private static float Component(GTA.Math.Vector3 p,int axis){return axis==0?p.X:axis==1?p.Y:p.Z;}
    private static bool Finite(float x){return!float.IsNaN(x)&&!float.IsInfinity(x);}
    private static void Vector(GTA.Math.Vector3 p,float bound){if(!Finite(p.X)||!Finite(p.Y)||!Finite(p.Z)||System.Math.Abs(p.X)>bound||System.Math.Abs(p.Y)>bound||System.Math.Abs(p.Z)>bound)throw Error("finite vector bound exceeded");}
    private static string Num(float n){return n.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
    private void Log(string code,string text){if(log!=null)log(code,text);}
    private static System.InvalidOperationException Error(string m){return new System.InvalidOperationException("PapelAbre99: "+m);}
}
