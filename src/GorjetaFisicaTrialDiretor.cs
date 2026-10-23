// Prototipo local e isolado: pares DRF/CFM reais; nenhuma abertura/contagem visual presumida.
// Assets DRF sao cutscene; CFM e autoria Network adaptada a scene LOCAL Story experimental.
// ID tecnico separado; CartaFisica89 e seus21 tracks permanecem intactos.
public sealed class GorjetaFisicaTrialDiretor : System.IDisposable
{
    public const string SceneId = "teste_fila_87", TableCode = "MESA_TRIAL_GORJETA", ChairCode = "CADEIRA_TRIAL_GORJETA", ActorId = "dinjer";
    public sealed class EnsaioSpec
    {
        public string Tipo, Corpo;
        public int Segmento, JanelaMs;
    }
    private sealed class Objeto
    {
        internal string Nome, Clipe;
        internal bool Suporte, Dinheiro, Envolope;
        internal GTA.Prop Entity;
        internal int Handle, Model;
        internal System.IntPtr Address;
        internal GTA.Math.Vector3 Minimum, Maximum, InitialPosition, InitialRotation;
        internal float Duration, MaximumPhase, MinimumContact = float.MaxValue;
        internal bool Progress, Contact, MotionContact;
        internal int ContactAt;
        internal float ContactPhase;
        internal GTA.Math.Vector3 ContactHand, ContactObject;
        internal GTA.Bone ContactBone;
    }
    private readonly System.Action<string, string> log;
    private readonly string menyoo;
    private readonly System.Collections.Generic.List<Objeto> objects = new System.Collections.Generic.List<Objeto>();
    private readonly System.Collections.Generic.HashSet<string> requestedModels = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.HashSet<string> modelsInitiallyLoaded = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
    private GTA.Ped actor;
    private System.Func<GTA.Ped, bool> actorOwnership;
    private ObjetosSerieDiretor ownObjects;
    private GTA.Prop table;
    private int actorHandle, actorModel, health, tableHandle, tableModel, stage, durationAt, modelAt, surfaceAt;
    private int warmAt, startedAt, lastElapsed = -1, nextLog;
    private int heldStableSince = -1, heldLastSampleAt = -1, heldNextLog;
    private GTA.Math.Vector3[] heldStableAnchorPoints;
    private bool heldOriginDerived;
    private System.IntPtr actorAddress, tableAddress;
    private GTA.Math.Vector3 originalRoot, sceneOrigin, sceneRotation, initialBodyRoot, tablePosition, tableRotation, tableMin, tableMax;
    private GTA.Math.Vector3 tablePoint, seatPoint;
    private GTA.Math.Vector3[] tablePoints = new GTA.Math.Vector3[5];
    private int surfaceId;
    private float floorLayer, bodyDuration, maximumBodyPhase;
    private float objectRoomRadius = 3f;
    private const float AuthoredRoomHardCap = 8f;
    private bool prepared, ready, running, finished, disposed, wasFrozen, requestedDictionary, dictionaryInitiallyLoaded;
    private bool taskOwned, bodyProgress, warmPoseChecked, warmSeatChecked, hasSeatPoint;
    private string dictionary, bodyClip;
    private EnsaioSpec spec;
    private GTA.FwSyncedScene scene;
    private PosesSerieDiretor upperPoses;
    private GTA.Prop upperChair;
    private int upperChairHandle, upperChairModel;
    private System.IntPtr upperChairAddress;
    private GTA.Math.Vector3 upperChairPosition, upperChairRotation, upperChairMin, upperChairMax;
    private GTA.Math.Vector3 upperRoot, upperPelvis, upperFootL, upperFootR;
    private float upperHeading;
    private bool upperPoseReady, upperBindingPending;
    private int upperSupportLogAt;
    private bool upperSharedOriginDerived;
    private bool UpperCounting { get { return spec != null && spec.Corpo == "cfm_feminino_upper"; } }

    private System.Diagnostics.Stopwatch loading;

    public GorjetaFisicaTrialDiretor(System.Action<string, string> writeLog, string pastaMenyoo)
    { log = writeLog; menyoo = pastaMenyoo; }
    public bool IsChair03SupportPreview { get { return spec != null && spec.Tipo == "contagem" && spec.Corpo == "cfm_feminino" && spec.Segmento == 4 && spec.JanelaMs == 33000; } }
    public bool IsReady { get { return prepared && ready && !disposed; } }
    public bool IsFinished { get { return IsReady && finished && TracksCompletos && ContatoComprovado; } }
    public bool TracksCompletos { get; private set; }
    public bool ContatoComprovado { get; private set; }
    public bool AberturaVisualConfirmada { get { return false; } }
    public bool ContagemVisualConfirmada { get { return false; } }
    public bool ControlsBody(string id, int elapsedMs)
    { return prepared && !disposed && System.String.Equals(id, ActorId, System.StringComparison.OrdinalIgnoreCase); }

    public static EnsaioSpec LoadSpec(string path)
    {
        RequirePlainFile(path);
        byte[] bytes = System.IO.File.ReadAllBytes(path);
        if (bytes.Length < 1 || bytes.Length > 512) throw Error("config exige1..512bytes");
        string json = new System.Text.UTF8Encoding(false, true).GetString(bytes);
        var fields = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
        var token = new System.Text.RegularExpressions.Regex(
            "\\G\\s*\\\"(?<k>[A-Za-z][A-Za-z0-9]*)\\\"\\s*:\\s*(?<v>\\\"[A-Za-z_0-9]+\\\"|0|[1-9][0-9]*)\\s*",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        int at = 0; while (at < json.Length && System.Char.IsWhiteSpace(json[at])) at++;
        if (at >= json.Length || json[at++] != '{') throw Error("JSON plano exigido");
        bool end = false;
        for (int count = 0; count < 6; count++)
        {
            var match = token.Match(json, at);
            if (!match.Success) throw Error("campo/tipo/escape invalido");
            string key = match.Groups["k"].Value;
            if (fields.ContainsKey(key)) throw Error("campo duplicado");
            fields.Add(key, match.Groups["v"].Value); at = match.Index + match.Length;
            if (at < json.Length && json[at] == '}') { at++; end = true; break; }
            if (at >= json.Length || json[at++] != ',') throw Error("separador invalido");
        }
        while (at < json.Length && System.Char.IsWhiteSpace(json[at])) at++;
        if (!end || at != json.Length || fields.Count != 6 || !fields.ContainsKey("versao") || !fields.ContainsKey("id") ||
            !fields.ContainsKey("tipo") || !fields.ContainsKey("segmento") || !fields.ContainsKey("corpo") || !fields.ContainsKey("janelaMs") ||
            fields["versao"] != "1" || fields["id"] != "\"" + SceneId + "\"") throw Error("contrato tecnico exclusivo teste_fila_87");
        int segment, window;
        if (!System.Int32.TryParse(fields["segmento"], out segment) || !System.Int32.TryParse(fields["janelaMs"], out window)) throw Error("inteiros exigidos");
        var value = new EnsaioSpec { Tipo = Unquote(fields["tipo"]), Corpo = Unquote(fields["corpo"]), Segmento = segment, JanelaMs = window };
        Validate(value);
        byte[] second = System.IO.File.ReadAllBytes(path);
        if (second.Length != bytes.Length) throw Error("config mudou durante leitura");
        for (int i = 0; i < bytes.Length; i++) if (bytes[i] != second[i]) throw Error("config mudou durante leitura");
        return value;
    }

    public void Prepare(string sceneId, GTA.Ped ownActor, System.Func<GTA.Ped, bool> ownsActor,
        ObjetosSerieDiretor measuredOwnObjects, EnsaioSpec request)
    {
        Active(); Story(); Validate(request);
        if (prepared || sceneId != SceneId || ownActor == null || ownsActor == null || !ownsActor(ownActor) ||
            measuredOwnObjects == null || !measuredOwnObjects.IsReady) throw Error("trial exige atriz integralmente propria e mesa propria pronta");
        spec = new EnsaioSpec { Tipo = request.Tipo, Corpo = request.Corpo, Segmento = request.Segmento, JanelaMs = request.JanelaMs };
        actor = ownActor; actorOwnership = ownsActor; ownObjects = measuredOwnObjects;
        upperSharedOriginDerived = false; // Fresh preparation never reuses the derived prop origin flag.
        actorHandle = actor.Handle; actorModel = actor.Model.Hash; actorAddress = actor.MemoryAddress; health = actor.Health;
        originalRoot = actor.Position; wasFrozen = actor.IsPositionFrozen; Check(originalRoot); RequireActor();
        table = ownObjects.GetProp(TableCode); tableHandle = table.Handle; tableModel = table.Model.Hash; tableAddress = table.MemoryAddress;
        if (tableModel != new GTA.Model("prop_table_01").Hash || !ownObjects.IsSupport(TableCode)) throw Error("mesa trial exige binding exacto prop_table_01");
        tablePosition = table.Position; tableRotation = table.Rotation; ownObjects.GetMeasuredBounds(TableCode, out tableMin, out tableMax);
        Check(tablePosition); Check(tableRotation); Check(tableMin); Check(tableMax); RequireTable();
        if (!MappedFloor(originalRoot, originalRoot.Z + 0.25f, originalRoot.Z - 3f, actor, out floorLayer, UpperCounting)) throw Error("piso da marca nao comprovado por Map real");
        BuildBindings(); VerifySources();
        if (UpperCounting) PrepareUpperPose();
        dictionaryInitiallyLoaded = new GTA.CrClipDictionary(dictionary).IsLoaded;
        foreach (Objeto item in objects) if (new GTA.Model(item.Nome).IsLoaded) modelsInitiallyLoaded.Add(item.Nome);
        prepared = true; loading = System.Diagnostics.Stopwatch.StartNew();
        Log("GORJETA_TRIAL_PREPARO", "Type=" + spec.Tipo + "; Segment=" + spec.Segmento + "; Body=" + bodyClip +
            "; WindowMs=" + spec.JanelaMs + "; Actor=" + actorHandle + "; ActorModel=" + actorModel + "; Floor=" + Number(floorLayer) +
            "; AuthorContext=" + (spec.Tipo == "envelope" ? "CUTSCENE_DRF_MIC_2_CS_2" : "NETWORK_CFM_COUNTING") +
            "; PlaybackLocalStoryOnly=true; RetargetExperimental=true; ProductionApproval=false");
    }

    public void PollLoading()
    {
        Active(); Story(); if (!prepared || ready) return;
        RequireActor(); RequireTable();
        if (loading.ElapsedMilliseconds >= 15000) throw Error("preparo/assets/contato inicial nao comprovados em15s");
        var resource = new GTA.CrClipDictionary(dictionary);
        if (!resource.Exists) throw Error("dicionario selecionado ausente no Legacy");
        if (!resource.IsLoaded) { resource.Request(); requestedDictionary = true; return; }
        if (UpperCounting && !upperPoseReady)
        {
            upperPoses.PollLoading(); if (!upperPoses.IsReady) return;
            upperPoses.Update(0); RequireUpperChair();
            upperRoot = originalRoot = actor.Position; upperHeading = actor.Heading;
            upperPelvis = Bone(GTA.Bone.SkelPelvis); upperFootL = Bone(GTA.Bone.SkelLeftFoot); upperFootR = Bone(GTA.Bone.SkelRightFoot);
            upperPoseReady = true;
            Log("GORJETA_TRIAL_UPPER_POSE", "Root=" + upperRoot + "; Heading=" + Number(upperHeading) + "; Pelvis=" + upperPelvis +
                "; FootL=" + upperFootL + "; FootR=" + upperFootR + "; PrimaryPoseValidatedByOriginalGeometry=true; CountingApproval=false");
        }
        if (UpperCounting) RequireUpperPose();
        if (stage == 0)
        {
            for (int count = 0; count < 3 && durationAt <= objects.Count; count++, durationAt++)
            {
                string clip = durationAt == 0 ? bodyClip : objects[durationAt - 1].Clipe;
                float seconds = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, dictionary, clip);
                Log("GORJETA_TRIAL_DURACAO", "Dictionary=" + dictionary + "; Clip=" + clip + "; Seconds=" + Number(seconds) +
                    "; WindowMs=" + spec.JanelaMs + "; FullFit=" + (Finite(seconds) && seconds > 0f && seconds * 1000f <= spec.JanelaMs) +
                    "; EqualDurationRequired=false; BeforeCreateReadyStart=true; Speed=1");
                if (!Finite(seconds) || seconds <= 0f || seconds * 1000f > spec.JanelaMs) throw Error("clipe inteiro nao cabe na janela tecnica; nao acelerar/truncar");
                if (durationAt == 0) bodyDuration = seconds;
                else
                {
                    objects[durationAt - 1].Duration = seconds;
                    if (UpperCounting && System.Math.Abs(seconds - bodyDuration) > 0.001f)
                        throw Error("upper body e props exigem duracao igual para relogios completos independentes a1");
                }
            }
            if (durationAt <= objects.Count) return;
            stage = 1; return;
        }
        if (stage == 1)
        {
            if (modelAt < objects.Count)
            {
                Objeto item = objects[modelAt]; var model = new GTA.Model(item.Nome);
                if (!model.IsInCdImage || !model.IsValid || !model.IsProp) throw Error("modelo real indisponivel/nao Prop: " + item.Nome);
                if (!model.IsLoaded) { model.Request(); requestedModels.Add(item.Nome); return; }
                model.RequestCollision(); model.GetDimensions(out item.Minimum, out item.Maximum); Check(item.Minimum); Check(item.Maximum);
                Log("GORJETA_TRIAL_MODELO", "Model=" + item.Nome + "; Loaded=" + model.IsLoaded + "; ModelCollisionLoaded=" + model.IsCollisionLoaded +
                    "; Min=" + item.Minimum + "; Max=" + item.Maximum + "; ColliderAndMeshRemainNativeProof=true");
                if (IsChair03SupportPreview && item.Suporte)
                {
                    GTA.Math.Vector3 size = item.Maximum - item.Minimum;
                    if (size.X <= 0f || size.Y <= 0f || size.Z <= 0f || size.Length() > 3f)
                        throw Error("chair03 actual native bounds unavailable before Create; no guessed seat");
                    Log("GORJETA87_CHAIR03_BOUNDS_BEFORE_CREATE", "Model=" + item.Nome + "; NativeHash=" + model.Hash +
                        "; Min=" + item.Minimum + "; Max=" + item.Maximum + "; ModelLoaded=" + model.IsLoaded +
                        "; ModelCollisionLoaded=" + model.IsCollisionLoaded + "; NativeEntityCreated=False; SeatLocalUnknown=True; OldChair01SeatNotReused=True; FitPending=True");
                    if (!model.IsCollisionLoaded) return; // Same bounded15s loading: require this actual collider before own Create.
                }
                modelAt++; return;
            }
            DeriveSharedOrigin(); stage = 2; return;
        }
        if (stage == 2)
        {
            if (surfaceAt < 5)
            {
                if (surfaceId == 0)
                {
                    float x = (tableMin.X + tableMax.X) * 0.5f, y = (tableMin.Y + tableMax.Y) * 0.5f;
                    if (surfaceAt > 0)
                    {
                        x += (surfaceAt == 1 || surfaceAt == 2 ? -1f : 1f) * (tableMax.X - tableMin.X) * 0.4f;
                        y += (surfaceAt == 1 || surfaceAt == 3 ? -1f : 1f) * (tableMax.Y - tableMin.Y) * 0.4f;
                    }
                    surfaceId = ownObjects.RequestSurface(TableCode, x, y); return;
                }
                ownObjects.PollLoading(); GTA.Math.Vector3 point, normal;
                if (!ownObjects.TryGetMeasuredSurface(surfaceId, out point, out normal)) return;
                Check(point); Check(normal);
                if (normal.Z < 0.98f || (surfaceAt > 0 && System.Math.Abs(point.Z - tablePoints[0].Z) > 0.08f)) throw Error("cinco superficies proprias nao confirmam plano da mesa");
                tablePoints[surfaceAt] = point; surfaceAt++; surfaceId = 0; return;
            }
            tablePoint = tablePoints[0]; CreateObjects(); stage = 3; return;
        }
        if (stage == 3)
        {
            // Holding phase0 e somente PREPARO. A acao futura toca cada track completo a rate1.
            BeginHeldScene(); warmAt = GTA.Game.GameTime; stage = 4; return;
        }
        if (stage == 4)
        {
            RequireObjects();
            if (!AllPlaying())
            {
                if (unchecked((uint)(GTA.Game.GameTime - warmAt)) > 1000u) throw Error("scene de preparo nao iniciou todos os tracks");
                return;
            }
            if (UpperCounting && upperBindingPending)
            {
                if (unchecked((uint)(GTA.Game.GameTime - warmAt)) > 1000u) throw Error("upper exact binding nao confirmou em1s");
                actor.SetAnimationSpeed(new GTA.CrClipAsset(dictionary, bodyClip), 0f);
                actor.SetAnimationCurrentTime(new GTA.CrClipAsset(dictionary, bodyClip), 0f);
                scene.HoldsLastFrame = true; scene.IsLooped = false; scene.Rate = 0f; scene.Phase = 0f;
                upperBindingPending = false; heldStableAnchorPoints = null; heldStableSince = -1; heldLastSampleAt = -1; warmAt = GTA.Game.GameTime;
                Log("GORJETA_TRIAL_UPPER_HELD_ONCE", "ScriptAnimSpeed=0; ScriptAnimPhase=0; PropSceneRate=0; PropScenePhase=0; AfterExactPlaying=true; PreparationOnly=true; ReapplyPerFrame=false");
                return;
            }
            CheckScene(0f);
            if (UpperCounting) RequireUpperHeldBody();
            if (!warmPoseChecked)
            {
                if (!PollHeldStability()) return;
                if (!UpperCounting && !heldOriginDerived && DeriveHeldSharedFloorOrigin()) return;
                if (UpperCounting && !upperSharedOriginDerived && DeriveUpperSharedTableOrigin()) return;
                Log("GORJETA_TRIAL_FISICA_GETTERS", "Stage=4; Actor=" + actorHandle + "; Collision=" + actor.IsCollisionEnabled +
                    "; HasPhysics=" + actor.HasPhysics + "; Root=" + actor.Position + "; Phase=" + Number(scene.Phase) +
                    "; Rate=" + Number(scene.Rate) + "; Playing=" + AllPlaying() + "; GuardsPassed=true; ActorCollisionSetter=false");
                LogBody("PREPARO", 0); RequireFeet(true); warmPoseChecked = true; return;
            }
            if (!warmSeatChecked) { MeasureSeatContact(); RequireMoneySupport(); warmSeatChecked = true; return; }
            ready = true;
            Log("GORJETA_TRIAL_PRONTO", "Type=" + spec.Tipo + "; Props=" + objects.Count + "; SharedOrigin=" + sceneOrigin +
                "; ActorRoot=" + actor.Position + "; BodyFullSeconds=" + Number(bodyDuration) + "; WarmHeldPhase=0; OpeningConfirmed=false; CountingConfirmed=false; ProductionApproval=false");
        }
    }

    public void Start()
    {
        Active(); Story(); if (IsChair03SupportPreview) throw Error("chair03 suporte87 somente F6/PREP phase0; F7/rate1/captura vetados ate prova Root");
        if (!IsReady || running || finished || scene == null || !AllPlaying()) throw Error("trial nao esta pronto para inicio unico");
        RequireActor(); RequireTable(); RequireObjects(); CheckScene(0f); RequireFeet(true);
        if ((actor.Position - initialBodyRoot).Length() > 0.05f) throw Error("atriz saiu da raiz inicial authored entre preparo e contagem");
        if (UpperCounting) { RequireUpperPose(); RequireUpperHeldBody(); }
        else actor.IsPositionFrozen = false;
        maximumBodyPhase = 0f; bodyProgress = false; lastElapsed = -1;
        foreach (Objeto item in objects) { item.MaximumPhase = 0f; item.Progress = item.Contact = item.MotionContact = false; item.MinimumContact = float.MaxValue; }
        if (UpperCounting) { upperSupportLogAt = 0; actor.SetAnimationSpeed(new GTA.CrClipAsset(dictionary, bodyClip), 1f); }
        scene.Rate = 1f; CheckScene(1f); running = true; startedAt = GTA.Game.GameTime; nextLog = 0;
        Log("GORJETA_TRIAL_PLAY", "Scene=" + scene.Handle + "; Type=" + spec.Tipo + "; Body=" + bodyClip +
            "; WindowMs=" + spec.JanelaMs + "; SharedAuthoredOrigin=" + !UpperCounting + "; IndependentPropRebase=false; PlaybackRate=1; ActorXYZSetterDuringAction=false" +
            "; BodyMode=" + (UpperCounting ? "SCRIPT_UPPER_SECONDARY_OVER_VALIDATED_PRIMARY" : "AUTHORED_FULL_BODY_FW") + "; OwnFwPropScene=true");
    }

    public void Update(int elapsedMs)
    {
        Active(); Story(); if (!running || finished) return;
        if (elapsedMs < 0 || elapsedMs < lastElapsed) throw Error("tempo regrediu"); lastElapsed = elapsedMs;
        int ownElapsed = unchecked((int)(GTA.Game.GameTime - startedAt));
        if (ownElapsed < 0 || ownElapsed > spec.JanelaMs + 1000) throw Error("deadline real do ensaio excedido");
        RequireActor(); RequireTable(); RequireObjects(); CheckScene(1f);
        if ((actor.Position - originalRoot).Length() > 3f) throw Error("corpo saiu do limite proprio3m");
        RequireFeet(false);
        if (UpperCounting) { RequireUpperPose(); RequireMoneySupport(); }
        bool bodyPlaying = Playing(actorHandle, bodyClip);
        if (bodyPlaying)
        {
            float phase = Current(actorHandle, bodyClip); CheckPhase(phase);
            if (UpperCounting)
            {
                float propPhase = scene.Phase; CheckPhase(propPhase);
                float gameTimePhase = System.Math.Min(1f, ownElapsed / (bodyDuration * 1000f));
                LogUpperClockIfNeeded(elapsedMs, phase, propPhase, gameTimePhase);
                if (System.Math.Abs(phase - propPhase) > 0.025f || System.Math.Abs(phase - gameTimePhase) > 0.035f)
                    throw Error("upper script/body/prop scene/full clock divergem; nao corrigir phase por frame");
            }
            if (phase > maximumBodyPhase + 0.0001f) bodyProgress = true;
            maximumBodyPhase = System.Math.Max(maximumBodyPhase, phase);
        }
        else if (ownElapsed >= 750 && maximumBodyPhase < 0.98f) throw Error("track corporal nao progrediu inteiro");
        foreach (Objeto item in objects)
        {
            bool playing = Playing(item.Handle, item.Clipe);
            if (playing)
            {
                float phase = UpperCounting ? scene.Phase : Current(item.Handle, item.Clipe); CheckPhase(phase);
                if (UpperCounting)
                {
                    float nativeEntityTime = Current(item.Handle, item.Clipe); CheckPhase(nativeEntityTime);
                    if (elapsedMs >= nextLog) Log("GORJETA_TRIAL_UPPER_PROP_CLOCK", "Clip=" + item.Clipe + "; ScenePhase=" + Number(phase) +
                        "; EntityTimeDiagnostic=" + Number(nativeEntityTime) + "; ExactClipPlaying=true; Clock=OwnFwScenePhase; Rate=" + Number(scene.Rate));
                }
                if (phase > item.MaximumPhase + 0.0001f) item.Progress = true;
                item.MaximumPhase = System.Math.Max(item.MaximumPhase, phase);
                if (item.Envolope || item.Dinheiro) ObserveContact(item, phase, ownElapsed);
            }
            else if (ownElapsed >= 750 && item.MaximumPhase < 0.98f) throw Error("track proprio nao progrediu inteiro: " + item.Clipe);
        }
        if (elapsedMs >= nextLog)
        {
            nextLog = elapsedMs + 250; LogBody("ACAO", elapsedMs);
            foreach (Objeto item in objects) Log("GORJETA_TRIAL_PROP", "Clip=" + item.Clipe + "; Model=" + item.Nome +
                "; Handle=" + item.Handle + "; XYZ=" + item.Entity.Position + "; PhaseMax=" + Number(item.MaximumPhase) +
                "; Progress=" + item.Progress + "; Contact=" + item.Contact + "; MotionContact=" + item.MotionContact +
                "; MinHandAabb=" + Number(item.MinimumContact) + "; AnimatedCollisionDisabled=" + !item.Entity.IsCollisionEnabled + "; ProductionApproval=false");
        }
        if (elapsedMs < spec.JanelaMs) return;
        TracksCompletos = bodyProgress && maximumBodyPhase >= 0.98f && ownElapsed >= System.Math.Ceiling(bodyDuration * 1000.0);
        ContatoComprovado = false;
        foreach (Objeto item in objects)
        {
            TracksCompletos &= item.Progress && item.MaximumPhase >= 0.98f && ownElapsed >= System.Math.Ceiling(item.Duration * 1000.0);
            if ((item.Envolope || item.Dinheiro) && item.MotionContact) ContatoComprovado = true;
        }
        Log("GORJETA_TRIAL_FIM", "Elapsed=" + elapsedMs + "; CompleteTracks=" + TracksCompletos + "; ContactAndMotion=" + ContatoComprovado +
            "; ActualElapsed=" + ownElapsed + "; BodyFullSeconds=" + Number(bodyDuration) + "; BodyMax=" + Number(maximumBodyPhase) + "; OpeningVisualConfirmed=false; ManualCountingVisualConfirmed=false; ProductionApproval=false");
        if (!TracksCompletos || !ContatoComprovado) throw Error("trial nao confirmou tracks completos e contato movente; nao promover captura");
        finished = true;
    }

    private void BuildBindings()
    {
        if (spec.Tipo == "envelope")
        {
            string s = spec.Segmento.ToString(System.Globalization.CultureInfo.InvariantCulture);
            dictionary = "drf_mic_2_cs_2-" + s;
            bodyClip = (spec.Corpo == "player" ? "player_zero_dual-" : "cs_drfriedlander_dual-") + s;
            Add("p_cash_envelope_01_s", "p_cash_envelope_01_s-" + s, false, false, true);
            Add("p_ilev_p_easychair_s", "p_ilev_p_easychair_s-" + s, true, false, false);
            Add("p_v_med_p_sofa_s", "p_v_med_p_sofa_s-" + s, true, false, false);
        }
        else
        {
            dictionary = "anim@amb@business@cfm@cfm_counting_notes@";
            string p = "note_counting_v" + spec.Segmento.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_";
            bodyClip = p + "counter";
            if (!UpperCounting) Add(IsChair03SupportPreview ? "bkr_prop_clubhouse_chair_03" : "bkr_prop_clubhouse_chair_01", p + "chair", true, false, false);
            if (IsChair03SupportPreview) Log("GORJETA87_CHAIR03_RESOURCE", "Scene=teste_fila_87; Model=bkr_prop_clubhouse_chair_03; PropListLine=867; Body=" + bodyClip +
                "; ChairTrack=" + p + "chair; SameAuthoredOrigin=True; ReplacementMeshCompatibilityUnknown=True; OwnSeatFloorContactPending=True; F6Only=True; Rate1=False; ProductionApproval=False");
            Add("bkr_prop_money_wrapped_01", p + "moneywrap", false, true, false);
            Add("bkr_prop_money_unsorted_01", p + "moneyunsorted", false, true, false);
            Add("bkr_prop_coke_tin_01", p + "moneybin", false, false, false);
            Add("bkr_prop_money_wrapped_01", p + "moneywrap^1", false, true, false);
            Add("bkr_prop_money_unsorted_01", p + "moneyunsorted^1", false, true, false);
            Add("bkr_prop_tin_cash_01a", p + "binmoney", false, false, false);
        }
    }
    private void Add(string model, string clip, bool support, bool money, bool envelope)
    { objects.Add(new Objeto { Nome = model, Clipe = clip, Suporte = support, Dinheiro = money, Envolope = envelope }); }
    private void DeriveSharedOrigin()
    {
        GTA.Math.Vector3 localRotation = InitialRotation(bodyClip, GTA.Math.Vector3.Zero, GTA.Math.Vector3.Zero); Check(localRotation);
        if (System.Math.Abs(localRotation.X) > 0.1f || System.Math.Abs(localRotation.Y) > 0.1f) throw Error("body inicial inclinado sem derivacao composta provada");
        sceneRotation = new GTA.Math.Vector3(0f, 0f, actor.Heading - localRotation.Z);
        sceneOrigin = originalRoot - InitialPosition(bodyClip, GTA.Math.Vector3.Zero, sceneRotation); Check(sceneOrigin);
        initialBodyRoot = InitialPosition(bodyClip, sceneOrigin, sceneRotation); Check(initialBodyRoot);
        if ((initialBodyRoot - originalRoot).Length() > 0.01f) throw Error("origem compartilhada nao reproduz raiz propria atual");
        float initialBoundsRadius = 0f;
        bool offsetsFinite = true;
        // Imprimir TODOS os tracks antes de rejeitar a geometria do comodo authored.
        // O envelope DRF atravessa a sala entre Michael/sofa e Therapist/cadeira.
        foreach (Objeto item in objects)
        {
            item.InitialPosition = InitialPosition(item.Clipe, sceneOrigin, sceneRotation);
            item.InitialRotation = InitialRotation(item.Clipe, sceneOrigin, sceneRotation); Check(item.InitialPosition); Check(item.InitialRotation);
            float pivotDistance = (item.InitialPosition - originalRoot).Length();
            float modelRadius = System.Math.Max(item.Minimum.Length(), item.Maximum.Length());
            float boundsRadius = pivotDistance + modelRadius;
            offsetsFinite &= Finite(pivotDistance) && Finite(modelRadius) && Finite(boundsRadius);
            if (Finite(boundsRadius)) initialBoundsRadius = System.Math.Max(initialBoundsRadius, boundsRadius);
            Log("GORJETA_TRIAL_OFFSET", "Clip=" + item.Clipe + "; Initial=" + item.InitialPosition + "; Rotation=" + item.InitialRotation +
                "; DeltaRoot=" + (item.InitialPosition - originalRoot) + "; SharedOrigin=" + sceneOrigin + "; RebaseToHand=false" +
                "; PivotDistance=" + Number(pivotDistance) + "; NativeMin=" + item.Minimum + "; NativeMax=" + item.Maximum +
                "; InitialBoundsRadius=" + Number(boundsRadius) + "; BeforeRoomGuard=true");
        }
        // Derivacao conservadora: extent dos modelos/offsets reais + limite corporal
        // existente3m, pela desigualdade triangular. O trial DRF continua limitado
        // ao orcamento tecnico8m; nao se afirma que8m seja dimensao do asset autor.
        objectRoomRadius = spec.Tipo == "envelope" ?
            System.Math.Min(AuthoredRoomHardCap, System.Math.Max(3f, initialBoundsRadius + 3f)) : 3f;
        Log("GORJETA_TRIAL_SALA", "Type=" + spec.Tipo + "; Body=" + bodyClip + "; SharedOrigin=" + sceneOrigin +
            "; ActorRoot=" + originalRoot + "; InitialBodyRoot=" + initialBodyRoot +
            "; InitialBoundsRadius=" + Number(initialBoundsRadius) + "; ExistingBodyTravelBudget=3" +
            "; ObjectPivotBudget=" + Number(objectRoomRadius) + "; TechnicalRoomHardCap=8" +
            "; DerivedFromNativeOffsetsAndModelBounds=true; ContactFloorFullClockStillMandatory=true; BeforeCreate=true");
        if (!offsetsFinite || !Finite(objectRoomRadius) ||
            (spec.Tipo == "envelope" && initialBoundsRadius > AuthoredRoomHardCap))
            throw Error("comodo authored nao cabe no orcamento tecnico finito8m; nao rebasear props");
        foreach (Objeto item in objects)
            if ((item.InitialPosition - originalRoot).Length() > objectRoomRadius)
                throw Error("objeto authored fora do volume proprio derivado; nao deslocar prop ate a mao");
    }
    private void CreateObjects()
    {
        foreach (Objeto item in objects)
        {
            item.Entity = GTA.Prop.CreateNoOffset(new GTA.Model(item.Nome), item.InitialPosition, item.InitialRotation, true);
            if (item.Entity == null || !item.Entity.Exists()) throw Error("criacao propria recusada");
            item.Handle = item.Entity.Handle; item.Model = item.Entity.Model.Hash; item.Address = item.Entity.MemoryAddress;
            item.Entity.IsPersistent = true; item.Entity.IsVisible = true; item.Entity.IsPositionFrozen = true; item.Entity.IsCollisionEnabled = true;
            if (!Own(item)) throw Error("identidade de criacao diverge");
            Log("GORJETA_TRIAL_CREATE", "Model=" + item.Nome + "; Handle=" + item.Handle + "; Address=" + item.Address +
                "; Position=" + item.Entity.Position + "; HasPhysics=" + item.Entity.HasPhysics + "; Collision=" + item.Entity.IsCollisionEnabled + "; NoPhysicalApproval=true");
        }
    }
    private void BeginHeldScene()
    {
        if (UpperCounting) { BeginUpperHeld(); return; }
        scene = GTA.FwSyncedScene.Create(sceneOrigin, sceneRotation, GTA.EulerRotationOrder.YXZ);
        if (scene == null || !scene.Exists()) throw Error("scene LOCAL recusada");
        scene.HoldsLastFrame = true; scene.IsLooped = false; scene.Phase = 0f; scene.Rate = 0f;
        taskOwned = true; actor.IsPositionFrozen = false;
        // API191 UseKinematicPhysics mantem colisoes pela tarefa normal; nenhum setter por frame.
        Log("GORJETA_TRIAL_FISICA_PEDIDO", "Stage=3; Actor=" + actorHandle + "; Collision=" + actor.IsCollisionEnabled +
            "; HasPhysics=" + actor.HasPhysics + "; Root=" + actor.Position +
            "; Flags=UseKinematicPhysics|AbortOnDeath|OnAbortStopScene; PreparationOnly=true; ActorCollisionSetter=false; GuardUnchanged=true");
        actor.Task.PlaySynchronizedScene(scene, new GTA.CrClipAsset(dictionary, bodyClip),
            new GTA.AnimationBlendDelta(8f), new GTA.AnimationBlendDelta(8f),
            GTA.SyncedSceneFlags.UseKinematicPhysics | GTA.SyncedSceneFlags.AbortOnDeath | GTA.SyncedSceneFlags.OnAbortStopScene,
            (GTA.RagdollBlockingFlags)0, new GTA.AnimationBlendDelta(1.5f), (GTA.AnimationIKControlFlags)0);
        Log("GORJETA_TRIAL_FISICA_APOS_PEDIDO", "Stage=3; Actor=" + actorHandle + "; Collision=" + actor.IsCollisionEnabled +
            "; HasPhysics=" + actor.HasPhysics + "; Root=" + actor.Position +
            "; Task=" + actor.CurrentScriptTaskNameHash + "; TaskStatus=" + actor.CurrentScriptTaskStatus +
            "; GetterImmediate=true; TaskNextTickStillMandatory=true; ActorCollisionSetter=false; GuardUnchanged=true");
        foreach (Objeto item in objects)
        {
            item.Entity.IsPositionFrozen = false;
            if (!item.Entity.PlaySynchronizedAnim(scene, new GTA.CrClipAsset(dictionary, item.Clipe),
                new GTA.AnimationBlendDelta(8f), new GTA.AnimationBlendDelta(8f), (GTA.SyncedSceneFlags)0, new GTA.AnimationBlendDelta(1.5f)))
                throw Error("track proprio recusado: " + item.Clipe);
            // API191 PlaySynchronizedAnim exige colisao desligada. Geometria/identidade ficam verificadas; nao e prova Collider.
            item.Entity.IsCollisionEnabled = false;
        }
    }

    // PREPARO phase0: esperar o blend native e medir varios frames antes de decidir apoio.
    // Nunca ler um primeiro tick transitorio como a geometria final do corpo retargetado.
    private bool PollHeldStability()
    {
        if (running || ready || scene == null) throw Error("estabilidade held exige somente preparo anterior a gravacao");
        CheckScene(0f); RequireActor(); RequireTable(); RequireObjects();
        int now = GTA.Game.GameTime;
        int sinceTask = unchecked((int)(now - warmAt));
        if (sinceTask < 0 || sinceTask > 2500) throw Error("pose held phase0 nao estabilizou em2,5s; nao reposicionar por frame");
        GTA.Math.Vector3[] points = new GTA.Math.Vector3[7 + objects.Count];
        points[0] = actor.Position;
        points[1] = Bone(GTA.Bone.SkelPelvis); points[2] = Bone(GTA.Bone.SkelHead);
        points[3] = Bone(GTA.Bone.SkelLeftFoot); points[4] = Bone(GTA.Bone.SkelRightFoot);
        points[5] = Bone(GTA.Bone.PHLeftHand); points[6] = Bone(GTA.Bone.PHRightHand);
        for (int i = 0; i < objects.Count; i++) points[7 + i] = objects[i].Entity.Position;
        float maximumDelta = 0f;
        if (heldStableAnchorPoints == null || heldStableAnchorPoints.Length != points.Length)
        {
            heldStableSince = now; heldStableAnchorPoints = points;
        }
        else
        {
            if (heldLastSampleAt == now) return false; // Require distinct game frames.
            for (int i = 0; i < points.Length; i++)
            {
                Check(points[i]);
                maximumDelta = System.Math.Max(maximumDelta, (points[i] - heldStableAnchorPoints[i]).Length());
            }
            if (!Finite(maximumDelta)) throw Error("delta de pose held nao finito");
            if (maximumDelta > 0.005f)
            {
                heldStableSince = now; heldStableAnchorPoints = points;
            }
        }
        heldLastSampleAt = now;
        int stableMs = unchecked((int)(now - heldStableSince));
        bool stable = sinceTask >= 500 && stableMs >= 250;
        if (sinceTask >= heldNextLog || stable)
        {
            heldNextLog = sinceTask + 200;
            Log("GORJETA_TRIAL_HELD_ESTABILIDADE", "Elapsed=" + sinceTask + "; StableMs=" + stableMs +
                "; MaxDelta=" + Number(maximumDelta) + "; Root=" + points[0] + "; Pelvis=" + points[1] +
                "; FootL=" + points[3] + "; FootR=" + points[4] + "; Phase=" + Number(scene.Phase) +
                "; Rate=" + Number(scene.Rate) + "; MinWarmMs=500; MinStableMs=250; MaxDeltaMeters=.005" +
                "; Stable=" + stable + "; DeltaReference=stable_window_anchor; AccumulatedDriftChecked=true" +
                "; PositionSetter=false; FootGuardStillMandatory=true");
        }
        return stable;
    }

    private void ResetHeldStability()
    {
        warmAt = GTA.Game.GameTime; heldStableSince = heldLastSampleAt = -1;
        heldNextLog = 0; heldStableAnchorPoints = null;
    }

    // Exclusive upper2 preparation: one constant translation of ALL six prop tracks.
    // Body is already separate SCRIPT upper, primary chair/actor/table remain untouched.
    private bool DeriveUpperSharedTableOrigin()
    {
        if (!UpperCounting || spec.Segmento != 2 || spec.JanelaMs != 24000 || objects.Count != 6 ||
            running || ready || stage != 4 || upperSharedOriginDerived)
            throw Error("common upper props derivation requires exclusive held upper2/24s preparation");
        CheckScene(0f); RequireUpperHeldBody(); RequireActor(); RequireTable(); RequireObjects();
        if (System.Math.Abs(tableRotation.X) > 0.01f || System.Math.Abs(tableRotation.Y) > 0.01f)
            throw Error("upper common translation requires measured horizontal own table transform");
        Objeto container = null;
        foreach (Objeto item in objects) if (item.Clipe == "note_counting_v2_moneybin" && item.Nome == "bkr_prop_coke_tin_01") container = item;
        if (container == null) throw Error("exact native moneybin container absent");
        float lowX = float.MaxValue, highX = float.MinValue, lowY = float.MaxValue, highY = float.MinValue;
        float lowestZ = float.MaxValue;
        GTA.Math.Vector3[] bottomCorners = new GTA.Math.Vector3[4];
        for (int i = 0; i < 8; i++)
        {
            GTA.Math.Vector3 local = new GTA.Math.Vector3((i & 1) == 0 ? container.Minimum.X : container.Maximum.X,
                (i & 2) == 0 ? container.Minimum.Y : container.Maximum.Y, (i & 4) == 0 ? container.Minimum.Z : container.Maximum.Z);
            GTA.Math.Vector3 world = container.Entity.GetOffsetPosition(local), onTable = table.GetPositionOffset(world);
            Check(world); Check(onTable);
            lowX = System.Math.Min(lowX, onTable.X); highX = System.Math.Max(highX, onTable.X);
            lowY = System.Math.Min(lowY, onTable.Y); highY = System.Math.Max(highY, onTable.Y);
            if (i < 4) { bottomCorners[i] = world; lowestZ = System.Math.Min(lowestZ, world.Z); }
        }
        GTA.Math.Vector3 center = container.Entity.GetOffsetPosition(new GTA.Math.Vector3(
            (container.Minimum.X + container.Maximum.X) * 0.5f, (container.Minimum.Y + container.Maximum.Y) * 0.5f, container.Minimum.Z));
        GTA.Math.Vector3 centerLocal = table.GetPositionOffset(center); Check(centerLocal);
        float midX = (tableMin.X + tableMax.X) * .5f, midY = (tableMin.Y + tableMax.Y) * .5f;
        float measuredX = (tableMax.X - tableMin.X) * .4f, measuredY = (tableMax.Y - tableMin.Y) * .4f;
        float minX = System.Math.Max(tableMin.X - lowX, midX - measuredX - centerLocal.X);
        float maxX = System.Math.Min(tableMax.X - highX, midX + measuredX - centerLocal.X);
        float minY = System.Math.Max(tableMin.Y - lowY, midY - measuredY - centerLocal.Y);
        float maxY = System.Math.Min(tableMax.Y - highY, midY + measuredY - centerLocal.Y);
        if (!Finite(minX) || !Finite(maxX) || !Finite(minY) || !Finite(maxY) || minX > maxX || minY > maxY)
            throw Error("native container footprint does not fit measured own table; no transform applied");
        float x = ClampUpper(0f, minX, maxX), y = ClampUpper(0f, minY, maxY);
        float z = tablePoint.Z - lowestZ; if (!Finite(z)) throw Error("native table/container height difference invalid");
        GTA.Math.Vector3 delta = UpperTableDelta(x, y, z), right = Bone(GTA.Bone.PHRightHand), left = Bone(GTA.Bone.PHLeftHand);
        bool feasible = UpperPredictedMoneySupport(delta, right, left, false);
        // Project a failing real money AABB towards a real hand, constrained by SAME common footprint rectangle.
        // No XYZ scan: finite proposals derived from four money objects/two hands, verified by original .35/.08 predicate.
        if (!feasible) foreach (Objeto item in objects)
        {
            if (!item.Dinheiro || UpperPredictedItemSupport(item, delta, right, left)) continue;
            foreach (GTA.Math.Vector3 hand in new GTA.Math.Vector3[] { right, left })
            {
                GTA.Math.Vector3 nearest = UpperShiftedClosest(item, hand, delta);
                GTA.Math.Vector3 localResidual = table.GetPositionOffset(table.Position + hand - nearest);
                Check(localResidual);
                float tx = ClampUpper(x + localResidual.X, minX, maxX), ty = ClampUpper(y + localResidual.Y, minY, maxY);
                GTA.Math.Vector3 proposed = UpperTableDelta(tx, ty, z);
                if (!UpperPredictedMoneySupport(proposed, right, left, false)) continue;
                // Retain the verified projected pose, rather than ending exactly on the .35 acceptance boundary.
                delta = proposed; feasible = true; break;
            }
            if (feasible) break;
        }
        UpperPredictedMoneySupport(delta, right, left, true);
        Log("GORJETA_TRIAL_UPPER_COMMON_LAYOUT", "Compatible=" + feasible + "; NativeFootprintMinXY=(" + Number(lowX) + "," + Number(lowY) +
            "); NativeFootprintMaxXY=(" + Number(highX) + "," + Number(highY) + "); AllowedMinXY=(" + Number(minX) + "," + Number(minY) +
            "); AllowedMaxXY=(" + Number(maxX) + "," + Number(maxY) + "); LowestNativeContainerZ=" + Number(lowestZ) +
            "; RealTableZ=" + Number(tablePoint.Z) + "; CommonDelta=" + delta + "; NoLiteralHeightCorrection=true; AllSixTracks=true; PreparationOnly=true");
        if (!feasible) throw Error("no compatible common translation for six upper props/table/native hands; no individual rebase");
        // Real own surface under hypothetical translated container center and all four lower native corners.
        // Prove BEFORE applying the scene translation; bbox maximum never substitutes for collider surface.
        foreach (GTA.Math.Vector3 point in new GTA.Math.Vector3[] { center, bottomCorners[0], bottomCorners[1], bottomCorners[2], bottomCorners[3] })
        {
            GTA.Math.Vector3 proposed = point + delta; GTA.ShapeTestResult hit; GTA.Entity own;
            var probe = GTA.ShapeTest.StartExpensiveSyncTestLOSProbe(proposed + new GTA.Math.Vector3(0,0,.25f),
                proposed - new GTA.Math.Vector3(0,0,.25f), GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, actor, (GTA.ShapeTestOptions)0);
            bool ownHit = probe.GetResult(out hit) == GTA.ShapeTestStatus.Ready && hit.DidHit && hit.TryGetHitEntity(out own) &&
                !System.Object.ReferenceEquals(own,null) && own.Handle == tableHandle && own.Model.Hash == tableModel && own.MemoryAddress == tableAddress;
            if (ownHit) { Check(hit.HitPosition); Check(hit.SurfaceNormal); }
            float gap = ownHit ? proposed.Z - hit.HitPosition.Z : float.NaN;
            Log("GORJETA_TRIAL_UPPER_COMMON_SURFACE", "ProspectiveContainerBottom=" + proposed + "; OwnHit=" + ownHit +
                "; Surface=" + (ownHit ? hit.HitPosition.ToString() : "missing") + "; Gap=" + Number(gap) +
                "; PreparationOnly=true; ActualColliderRequired=true; NoTransformAppliedYet=true");
            if (!ownHit || hit.SurfaceNormal.Z < .98f || !Finite(gap) || System.Math.Abs(gap) > .02f)
                throw Error("common upper layout lacks real flat own table under full container footprint; no transform applied");
        }
        GTA.Math.Vector3 previous = sceneOrigin; sceneOrigin += delta; Check(sceneOrigin);
        foreach (Objeto item in objects)
        {
            item.InitialPosition = InitialPosition(item.Clipe, sceneOrigin, sceneRotation);
            item.InitialRotation = InitialRotation(item.Clipe, sceneOrigin, sceneRotation);
            Check(item.InitialPosition); Check(item.InitialRotation);
            if ((item.InitialPosition - originalRoot).Length() > objectRoomRadius)
                throw Error("common upper layout leaves original own room budget; no transform applied");
        }
        upperSharedOriginDerived = true;
        scene.SetOrigin(sceneOrigin, sceneRotation, GTA.EulerRotationOrder.YXZ); // Exactly once, PREP only, ALL six props.
        CheckScene(0f); ResetHeldStability();
        Log("GORJETA_TRIAL_UPPER_COMMON_ORIGIN", "Previous=" + previous + "; CommonOrigin=" + sceneOrigin + "; Delta=" + delta +
            "; SetOriginOnce=true; Props=6; BodyRootRetained=" + initialBodyRoot +
            "; ActorTableChairXYZSetter=false; IndependentAssetRebase=false; OriginalContactGuardsUnchanged=true; FullPathProven=false; CountingConfirmed=false");
        return true;
    }
    private static float ClampUpper(float value, float lower, float upper)
    { return System.Math.Max(lower, System.Math.Min(upper, value)); }
    private GTA.Math.Vector3 UpperTableDelta(float x, float y, float z)
    { return table.GetOffsetPosition(new GTA.Math.Vector3(x,y,0f)) - table.Position + new GTA.Math.Vector3(0f,0f,z); }
    private GTA.Math.Vector3 UpperShiftedClosest(Objeto item, GTA.Math.Vector3 world, GTA.Math.Vector3 delta)
    {
        GTA.Math.Vector3 local = item.Entity.GetPositionOffset(world - delta); Check(local);
        var nearest = new GTA.Math.Vector3(ClampUpper(local.X,item.Minimum.X,item.Maximum.X),
            ClampUpper(local.Y,item.Minimum.Y,item.Maximum.Y),ClampUpper(local.Z,item.Minimum.Z,item.Maximum.Z));
        GTA.Math.Vector3 result = item.Entity.GetOffsetPosition(nearest) + delta; Check(result); return result;
    }
    private bool UpperPredictedItemSupport(Objeto item, GTA.Math.Vector3 delta, GTA.Math.Vector3 right, GTA.Math.Vector3 left)
    {
        float distance = System.Math.Min((right - UpperShiftedClosest(item,right,delta)).Length(), (left - UpperShiftedClosest(item,left,delta)).Length());
        GTA.Math.Vector3 bottom = item.Entity.GetOffsetPosition(new GTA.Math.Vector3((item.Minimum.X+item.Maximum.X)*.5f,
            (item.Minimum.Y+item.Maximum.Y)*.5f,item.Minimum.Z)) + delta;
        GTA.Math.Vector3 local = table.GetPositionOffset(bottom); Check(local); Check(bottom);
        bool plane = System.Math.Abs(local.X - (tableMin.X+tableMax.X)*.5f) <= (tableMax.X-tableMin.X)*.4f &&
            System.Math.Abs(local.Y - (tableMin.Y+tableMax.Y)*.5f) <= (tableMax.Y-tableMin.Y)*.4f && System.Math.Abs(bottom.Z-tablePoint.Z) <= .08f;
        return Finite(distance) && (distance <= .35f || plane);
    }
    private bool UpperPredictedMoneySupport(GTA.Math.Vector3 delta, GTA.Math.Vector3 right, GTA.Math.Vector3 left, bool report)
    {
        bool all = true;
        foreach (Objeto item in objects)
        {
            if (!item.Dinheiro) continue;
            bool valid = UpperPredictedItemSupport(item,delta,right,left); all &= valid;
            if (report) Log("GORJETA_TRIAL_UPPER_COMMON_MONEY_PREDICTION", "Clip=" + item.Clipe + "; SameGuardCompatible=" + valid +
                "; CommonDelta=" + delta + "; HandDistance=" + Number(System.Math.Min((right-UpperShiftedClosest(item,right,delta)).Length(),(left-UpperShiftedClosest(item,left,delta)).Length())) +
                "; NoNativeTransformYet=true; HandAabbLimit=.35; TableGapLimit=.08; FullPathProven=false");
        }
        return all;
    }

    // Se necessario, derivar UMA translacao vertical para a origem COMUM de todos os tracks.
    // A faixa deve satisfazer simultaneamente ossos/piso e bounds inferiores do suporte real.
    // Faixas incompatíveis abortam; nao afundar cadeira para fazer apenas o osso passar.
    private bool DeriveHeldSharedFloorOrigin()
    {
        if (running || ready || stage != 4 || heldOriginDerived) throw Error("origem comum held so pode ser derivada uma vez no preparo");
        CheckScene(0f); RequireActor(); RequireTable(); RequireObjects();
        heldOriginDerived = true;
        if (spec.Tipo != "contagem") return false; // Envelope/terapeuta conserva derivacao propria original.
        GTA.Math.Vector3 left = Bone(GTA.Bone.SkelLeftFoot), right = Bone(GTA.Bone.SkelRightFoot);
        float gl, gr;
        if (!MappedFloor(left, left.Z + 0.09f, left.Z - 0.36f, actor, out gl) ||
            !MappedFloor(right, right.Z + 0.09f, right.Z - 0.36f, actor, out gr))
            throw Error("derivacao comum sem piso curto real dos dois pes");
        if (System.Math.Abs(gl - floorLayer) > 0.35f || System.Math.Abs(gr - floorLayer) > 0.35f)
            throw Error("derivacao comum rejeita outra camada de piso");
        float gapL = left.Z - gl, gapR = right.Z - gr;
        float lower = System.Math.Max(-0.08f - gapL, -0.08f - gapR);
        float upper = System.Math.Min(0.08f - gapL, 0.08f - gapR);
        Log("GORJETA_TRIAL_ORIGEM_FAIXA_PES", "FootL=" + left + "; GroundL=" + Number(gl) +
            "; FootR=" + right + "; GroundR=" + Number(gr) + "; GapL=" + Number(gapL) +
            "; GapR=" + Number(gapR) + "; DeltaMin=" + Number(lower) + "; DeltaMax=" + Number(upper) +
            "; DerivedFromOriginalFootGuard=true; PreparationOnly=true");
        if (lower <= 0f && upper >= 0f)
        {
            Log("GORJETA_TRIAL_ORIGEM_COMUM", "Changed=false; Cause=stable_feet_already_fit; SharedOrigin=" + sceneOrigin +
                "; OriginalFootSeatMoneyGuardsStillMandatory=true; PositionSetter=false");
            return false;
        }
        int supports = 0;
        foreach (Objeto item in objects)
        {
            if (!item.Suporte) continue;
            supports++;
            GTA.Math.Vector3 bottom = item.Entity.GetOffsetPosition(item.Minimum); Check(bottom);
            for (int corner = 0; corner < 8; corner++)
            {
                GTA.Math.Vector3 local = new GTA.Math.Vector3((corner & 1) == 0 ? item.Minimum.X : item.Maximum.X,
                    (corner & 2) == 0 ? item.Minimum.Y : item.Maximum.Y,
                    (corner & 4) == 0 ? item.Minimum.Z : item.Maximum.Z);
                GTA.Math.Vector3 world = item.Entity.GetOffsetPosition(local); Check(world);
                if (world.Z < bottom.Z) bottom = world;
            }
            float supportGround;
            if (!MappedFloor(bottom, bottom.Z + 0.09f, floorLayer - 0.36f, item.Entity, out supportGround) ||
                System.Math.Abs(supportGround - floorLayer) > 0.35f)
                throw Error("derivacao comum sem piso sob suporte proprio de bounds reais");
            float gap = bottom.Z - supportGround;
            // Limite conservador adicional de2cm para bounds do suporte; nao substitui own-seat ray.
            float supportLower = -0.02f - gap, supportUpper = 0.02f - gap;
            lower = System.Math.Max(lower, supportLower); upper = System.Math.Min(upper, supportUpper);
            Log("GORJETA_TRIAL_ORIGEM_FAIXA_SUPORTE", "Model=" + item.Nome + "; Clip=" + item.Clipe +
                "; BottomFromEightNativeBounds=" + bottom + "; Floor=" + Number(supportGround) +
                "; Gap=" + Number(gap) + "; SupportDeltaMin=" + Number(supportLower) +
                "; SupportDeltaMax=" + Number(supportUpper) + "; CommonDeltaMin=" + Number(lower) +
                "; CommonDeltaMax=" + Number(upper) + "; ColliderContactNotPresumed=true; OwnSeatRayStillMandatory=true");
        }
        if (supports == 0 || !Finite(lower) || !Finite(upper) || lower > upper)
        {
            Log("GORJETA_TRIAL_ORIGEM_COMUM", "Changed=false; Compatible=false; Min=" + Number(lower) +
                "; Max=" + Number(upper) + "; GapL=" + Number(gapL) + "; GapR=" + Number(gapR) +
                "; Cause=feet_and_native_support_bounds_cannot_share_safe_floor_origin; NoSetters=true");
            throw Error("ossos dos pes e bounds do suporte nao admitem a mesma origem comum; nao afundar cadeira nem afrouxar guardas");
        }
        float delta = lower > 0f ? lower : upper; // Menor magnitude dentro da intersecao medida.
        if (!Finite(delta) || System.Math.Abs(delta) > 0.35f) throw Error("derivacao comum excede camada curta de piso0,35m");
        GTA.Math.Vector3 oldOrigin = sceneOrigin;
        sceneOrigin += new GTA.Math.Vector3(0f, 0f, delta); Check(sceneOrigin);
        initialBodyRoot = InitialPosition(bodyClip, sceneOrigin, sceneRotation); Check(initialBodyRoot);
        foreach (Objeto item in objects)
        {
            item.InitialPosition = InitialPosition(item.Clipe, sceneOrigin, sceneRotation);
            item.InitialRotation = InitialRotation(item.Clipe, sceneOrigin, sceneRotation);
            Check(item.InitialPosition); Check(item.InitialRotation);
            if ((item.InitialPosition - originalRoot).Length() > objectRoomRadius)
                throw Error("origem comum excede volume proprio original; nao mover prop independente");
        }
        // API191 SetOrigin desloca a scene propria inteira. Nenhum XYZ de ator/mao/prop e escrito.
        scene.SetOrigin(sceneOrigin, sceneRotation, GTA.EulerRotationOrder.YXZ);
        CheckScene(0f); ResetHeldStability();
        Log("GORJETA_TRIAL_ORIGEM_COMUM", "Changed=true; DeltaZ=" + Number(delta) + "; Previous=" + oldOrigin +
            "; SharedOrigin=" + sceneOrigin + "; InitialBodyRoot=" + initialBodyRoot +
            "; Method=FwSyncedScene.SetOrigin_once_preparation; AllTracksShareOrigin=true; IndependentRebase=false" +
            "; ActorXYZSetter=false; PropXYZSetter=false; FloorFeetSeatMoneyFullTracksGuardsUnchanged=true; RecheckAfterStability=true");
        return true;
    }

    private void RequireFeet(bool initial)
    {
        GTA.Math.Vector3 left = Bone(GTA.Bone.SkelLeftFoot), right = Bone(GTA.Bone.SkelRightFoot), pelvis = Bone(GTA.Bone.SkelPelvis);
        float gl, gr, gp;
        if (!MappedFloor(left, left.Z + 0.09f, left.Z - 0.36f, actor, out gl, UpperCounting) ||
            !MappedFloor(right, right.Z + 0.09f, right.Z - 0.36f, actor, out gr, UpperCounting) ||
            !MappedFloor(actor.Position, actor.Position.Z + 0.09f, System.Math.Min(left.Z, right.Z) - 0.36f, actor, out gp, UpperCounting))
            throw Error("pes/root sem piso real curto completo; nenhum HAG substitui prova");
        if (System.Math.Abs(gl - floorLayer) > 0.35f || System.Math.Abs(gr - floorLayer) > 0.35f || System.Math.Abs(gp - floorLayer) > 0.35f)
            throw Error("corpo mudou camada de piso");
        float max = initial || spec.Tipo == "contagem" ? 0.08f : 0.35f;
        if (left.Z - gl < -0.08f || right.Z - gr < -0.08f || left.Z - gl > max || right.Z - gr > max || pelvis.Z < gp - 0.08f)
            throw Error("pes/corpo fora do contato fisiologico real; consultar log de ossos, sem ajustar XYZ");
    }
    private void MeasureSeatContact()
    {
        if (UpperCounting) { MeasureUpperSeat(); return; }
        GTA.Math.Vector3 pelvis = Bone(GTA.Bone.SkelPelvis);
        foreach (Objeto item in objects)
        {
            if (!item.Suporte) continue;
            GTA.Math.Vector3 local = item.Entity.GetPositionOffset(pelvis);
            if (local.X < item.Minimum.X || local.X > item.Maximum.X || local.Y < item.Minimum.Y || local.Y > item.Maximum.Y) continue;
            item.Entity.IsCollisionEnabled = true;
            try
            {
                GTA.ShapeTestResult result;
                GTA.ShapeTestHandle ray = GTA.ShapeTest.StartExpensiveSyncTestLOSProbe(
                    pelvis + new GTA.Math.Vector3(0f, 0f, 0.25f), pelvis - new GTA.Math.Vector3(0f, 0f, 2f),
                    GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, actor, (GTA.ShapeTestOptions)0);
                GTA.ShapeTestStatus status = ray.GetResult(out result);
                GTA.Entity hit;
                if (status != GTA.ShapeTestStatus.Ready || !result.DidHit || !result.TryGetHitEntity(out hit) || hit == null || hit.Handle != item.Handle) continue;
                Check(result.HitPosition); Check(result.SurfaceNormal);
                if (result.SurfaceNormal.Z < 0.98f) continue;
                float contact = pelvis.Z - result.HitPosition.Z;
                Log("GORJETA_TRIAL_ASSENTO", "Prop=" + item.Nome + "; OwnHit=true; Point=" + result.HitPosition +
                    "; Normal=" + result.SurfaceNormal + "; Pelvis=" + pelvis + "; ContactZ=" + Number(contact));
                LogSeatSkeleton(item.Entity, item.Minimum, item.Maximum, result.HitPosition);
                if (IsChair03SupportPreview && !MeasureChair03SupportGeometry(item, result.HitPosition)) continue;
                if (contact < 0f || contact > 0.10f) continue;
                hasSeatPoint = true; seatPoint = result.HitPosition; break;
            }
            finally { item.Entity.IsCollisionEnabled = false; }
        }
        if (spec.Tipo == "contagem" && !hasSeatPoint) throw Error("CFM feminina sem pelvis apoiada em cadeira propria real0..10cm");
    }
    // One owned chair03 at real held phase0. All surfaces are actual LOS hits; no model-top seat guess.
    private bool MeasureChair03SupportGeometry(Objeto item, GTA.Math.Vector3 actualSeat)
    {
        if (!IsChair03SupportPreview || running || scene == null || !Own(item) || item.Nome != "bkr_prop_clubhouse_chair_03" ||
            !item.Suporte || !item.Entity.HasPhysics || !item.Entity.IsCollisionEnabled || !item.Entity.Model.IsCollisionLoaded)
            throw Error("chair03 support measurement lost exclusive PREP/own real collider identity");
        CheckScene(0f); Check(actualSeat);
        GTA.Math.Vector3 bottom = item.Entity.GetOffsetPosition(item.Minimum); Check(bottom);
        for (int n = 0; n < 8; n++)
        {
            GTA.Math.Vector3 local = new GTA.Math.Vector3((n&1)==0?item.Minimum.X:item.Maximum.X,
                (n&2)==0?item.Minimum.Y:item.Maximum.Y,(n&4)==0?item.Minimum.Z:item.Maximum.Z);
            GTA.Math.Vector3 point = item.Entity.GetOffsetPosition(local); Check(point);
            if (point.Z < bottom.Z) bottom = point;
        }
        float ground;
        bool floorKnown = MappedFloor(bottom,bottom.Z+.09f,floorLayer-.36f,item.Entity,out ground,true);
        float gap = floorKnown ? bottom.Z-ground : float.NaN;
        bool floorGood = floorKnown && System.Math.Abs(ground-floorLayer)<=.35f && Finite(gap) && System.Math.Abs(gap)<=.02f;
        Log("GORJETA87_CHAIR03_FLOOR", "Handle="+item.Handle+"; Model="+item.Nome+"; Pivot="+item.Entity.Position+
            "; Rotation="+item.Entity.Rotation+"; NativeMin="+item.Minimum+"; NativeMax="+item.Maximum+"; LowestEightBounds="+bottom+
            "; FloorKnown="+floorKnown+"; Floor="+Number(ground)+"; FloorLayer="+Number(floorLayer)+"; BottomGap="+Number(gap)+
            "; BottomWithin2cm="+floorGood+"; SeatWorldMeasured="+actualSeat+"; SeatLocalMeasured="+item.Entity.GetPositionOffset(actualSeat)+
            "; OldSeatLocalNotUsed=True; OwnAnimatedTrack=True; Phase0=True; IndependentRebase=False; BodySetter=False; ProductionApproval=False");
        bool thighGood = true;
        foreach (GTA.Bone bone in new GTA.Bone[] { GTA.Bone.SkelLeftThigh, GTA.Bone.SkelRightThigh })
        {
            GTA.Math.Vector3 world = Bone(bone), local = item.Entity.GetPositionOffset(world); Check(local);
            bool xy = local.X>=item.Minimum.X && local.X<=item.Maximum.X && local.Y>=item.Minimum.Y && local.Y<=item.Maximum.Y;
            bool ownHit = false; float contact = float.NaN; GTA.Math.Vector3 surface = GTA.Math.Vector3.Zero, normal = GTA.Math.Vector3.Zero;
            if (xy)
            {
                GTA.ShapeTestResult measured; GTA.Entity hit;
                GTA.ShapeTestHandle ray = GTA.ShapeTest.StartExpensiveSyncTestLOSProbe(world+new GTA.Math.Vector3(0f,0f,.25f),
                    world-new GTA.Math.Vector3(0f,0f,2f),GTA.IntersectFlags.Map|GTA.IntersectFlags.Objects,actor,(GTA.ShapeTestOptions)0);
                GTA.ShapeTestStatus status = ray.GetResult(out measured);
                ownHit = status==GTA.ShapeTestStatus.Ready && measured.DidHit && measured.TryGetHitEntity(out hit) &&
                    !System.Object.ReferenceEquals(hit,null) && hit.Handle==item.Handle && Own(item);
                if (ownHit) {surface=measured.HitPosition;normal=measured.SurfaceNormal;Check(surface);Check(normal);contact=world.Z-surface.Z;}
            }
            bool good = xy && ownHit && normal.Z>=.98f && Finite(contact) && contact>=0f && contact<=.10f;
            thighGood &= good;
            Log("GORJETA87_CHAIR03_THIGH", "Bone="+bone+"; World="+world+"; Local="+local+"; InsideOwnXY="+xy+
                "; OwnHit="+ownHit+"; Point="+surface+"; Normal="+normal+"; Gap="+Number(contact)+"; Contact0to10cm="+good+
                "; NativeJointOriginNotSkin=True; NoSetters=True; NoToleranceChange=True");
        }
        return floorGood && thighGood;
    }

    private void RequireMoneySupport()
    {
        if (spec.Tipo != "contagem") return;
        GTA.Math.Vector3 right = Bone(GTA.Bone.PHRightHand), left = Bone(GTA.Bone.PHLeftHand);
        // Checks remain every Tick. Only healthy upper-action telemetry is throttled.
        bool report = !UpperCounting || !running || lastElapsed >= upperSupportLogAt;
        foreach (Objeto item in objects)
        {
            if (!item.Dinheiro) continue;
            float handDistance = System.Math.Min(AabbDistance(item, right), AabbDistance(item, left));
            GTA.Math.Vector3 bottom = item.Entity.GetOffsetPosition(new GTA.Math.Vector3(
                (item.Minimum.X + item.Maximum.X) * 0.5f, (item.Minimum.Y + item.Maximum.Y) * 0.5f, item.Minimum.Z));
            GTA.Math.Vector3 local = table.GetPositionOffset(bottom); Check(local); Check(bottom);
            float centerX = (tableMin.X + tableMax.X) * 0.5f, centerY = (tableMin.Y + tableMax.Y) * 0.5f;
            bool inMeasuredPlane = System.Math.Abs(local.X - centerX) <= (tableMax.X - tableMin.X) * 0.4f &&
                System.Math.Abs(local.Y - centerY) <= (tableMax.Y - tableMin.Y) * 0.4f && System.Math.Abs(bottom.Z - tablePoint.Z) <= 0.08f;
            bool invalidSupport = handDistance > 0.35f && !inMeasuredPlane;
            if (report || invalidSupport) Log("GORJETA_TRIAL_DINHEIRO_APOIO", "Clip=" + item.Clipe + "; Bottom=" + bottom + "; TableLocal=" + local +
                "; TablePlaneZ=" + Number(tablePoint.Z) + "; InMeasuredOwnPlane=" + inMeasuredPlane + "; HandAabb=" + Number(handDistance));
            if (invalidSupport) throw Error("dinheiro nao esta na mao nem sobre o plano medido da mesa propria; nao mover assets para simular par");
        }
        if (UpperCounting && running && report) upperSupportLogAt = lastElapsed + 250;
    }
    private bool MappedFloor(GTA.Math.Vector3 point, float top, float bottom, GTA.Entity ignore, out float z, bool mapOnly = false)
    {
        z = float.NaN; Check(point);
        float excludedGround = float.NaN, rayTop = top, rayBottom = bottom;
        if (mapOnly)
        {
            // IntersectWorld also hit the frozen own chair in native126. Use the same excluding-props ground API as Poses.
            bool found = GTA.World.GetGroundHeightExcludingProps(new GTA.Math.Vector3(point.X, point.Y, top),
                out excludedGround, GTA.GetGroundHeightMode.Normal);
            if (!found || !Finite(excludedGround) || excludedGround > top + 0.01f || excludedGround < bottom - 0.01f)
            {
                Log("GORJETA_TRIAL_FLOOR_FAIL", "Found=" + found + "; ExcludedGround=" + Number(excludedGround) +
                    "; Query=" + point + "; Top=" + Number(top) + "; Bottom=" + Number(bottom) + "; OwnPropsExcluded=true");
                return false;
            }
            rayTop = System.Math.Min(top, excludedGround + 0.04f);
            rayBottom = System.Math.Max(bottom, excludedGround - 0.04f);
            if (rayTop <= rayBottom) return false;
        }
        GTA.ShapeTestResult result;
        GTA.ShapeTestHandle probe = GTA.ShapeTest.StartExpensiveSyncTestLOSProbe(
            new GTA.Math.Vector3(point.X, point.Y, rayTop), new GTA.Math.Vector3(point.X, point.Y, rayBottom),
            mapOnly ? GTA.IntersectFlags.Map : GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, ignore, (GTA.ShapeTestOptions)4);
        GTA.ShapeTestStatus status = probe.GetResult(out result);
        if (status != GTA.ShapeTestStatus.Ready || !result.DidHit) return false;
        Check(result.HitPosition); Check(result.SurfaceNormal);
        GTA.Entity hit = null;
        if (mapOnly) result.TryGetHitEntity(out hit);
        bool ownSupportHit = mapOnly && !System.Object.ReferenceEquals(hit, null) && hit.Exists() &&
            (hit.Handle == tableHandle || hit.Handle == upperChairHandle);
        bool invalid = System.Math.Abs(result.HitPosition.X - point.X) > 0.01f || System.Math.Abs(result.HitPosition.Y - point.Y) > 0.01f ||
            result.HitPosition.Z > top + 0.01f || result.HitPosition.Z < bottom - 0.01f || result.SurfaceNormal.Z < 0.98f ||
            (mapOnly && (System.Math.Abs(result.HitPosition.Z - excludedGround) > 0.02f || ownSupportHit));
        if (mapOnly && (!running || lastElapsed >= nextLog || invalid))
            Log("GORJETA_TRIAL_FLOOR", "Query=" + point + "; ExcludedGround=" + Number(excludedGround) +
                "; RayTop=" + Number(rayTop) + "; RayBottom=" + Number(rayBottom) + "; Point=" + result.HitPosition +
                "; Normal=" + result.SurfaceNormal + "; HitEntity=" + (System.Object.ReferenceEquals(hit, null) ? 0 : hit.Handle) +
                "; OwnSupportHit=" + ownSupportHit + "; Rejected=" + invalid + "; ExcludingPropsPlusOriginalNormalGuard=true");
        if (invalid) return false;
        z = result.HitPosition.Z; return true;
    }
    private void ObserveContact(Objeto item, float phase, int ownElapsed)
    {
        GTA.Math.Vector3 right = Bone(GTA.Bone.PHRightHand), left = Bone(GTA.Bone.PHLeftHand);
        float dr = AabbDistance(item, right), dl = AabbDistance(item, left);
        GTA.Math.Vector3 hand = dr <= dl ? right : left; float d = System.Math.Min(dr, dl);
        GTA.Bone handBone = dr <= dl ? GTA.Bone.PHRightHand : GTA.Bone.PHLeftHand;
        item.MinimumContact = System.Math.Min(item.MinimumContact, d);
        if (phase < 0.05f || phase > 0.95f || d > 0.35f || !item.Progress || !bodyProgress) return;
        GTA.Math.Vector3 objectPoint = item.Entity.Position;
        if (!item.Contact)
        {
            item.Contact = true; item.ContactAt = ownElapsed; item.ContactPhase = phase; item.ContactHand = hand; item.ContactObject = objectPoint; item.ContactBone = handBone;
            Log("GORJETA_TRIAL_CONTATO", "Clip=" + item.Clipe + "; Elapsed=" + ownElapsed + "; Phase=" + Number(phase) +
                "; HandBone=" + handBone + "; Hand=" + hand + "; Object=" + objectPoint + "; HandAabb=" + Number(d) + "; Limite=.35; VisualSemanticConfirmed=false");
        }
        else if (item.ContactBone == handBone && ownElapsed >= item.ContactAt + 50 && phase > item.ContactPhase + 0.01f &&
            ((hand - item.ContactHand).Length() >= 0.025f || (objectPoint - item.ContactObject).Length() >= 0.025f)) item.MotionContact = true;
    }
    private static float AabbDistance(Objeto item, GTA.Math.Vector3 world)
    {
        GTA.Math.Vector3 p = item.Entity.GetPositionOffset(world); Check(p);
        var nearest = new GTA.Math.Vector3(System.Math.Max(item.Minimum.X, System.Math.Min(item.Maximum.X, p.X)),
            System.Math.Max(item.Minimum.Y, System.Math.Min(item.Maximum.Y, p.Y)), System.Math.Max(item.Minimum.Z, System.Math.Min(item.Maximum.Z, p.Z)));
        float distance = (world - item.Entity.GetOffsetPosition(nearest)).Length();
        if (!Finite(distance)) throw Error("distancia mao AABB nao finita"); return distance;
    }
    private void LogBody(string context, int elapsed)
    {
        Log("GORJETA_TRIAL_CORPO", "Context=" + context + "; Elapsed=" + elapsed + "; Root=" + actor.Position +
            "; Pelvis=" + Bone(GTA.Bone.SkelPelvis) + "; Head=" + Bone(GTA.Bone.SkelHead) +
            "; FootL=" + Bone(GTA.Bone.SkelLeftFoot) + "; FootR=" + Bone(GTA.Bone.SkelRightFoot) +
            "; HandL=" + Bone(GTA.Bone.PHLeftHand) + "; HandR=" + Bone(GTA.Bone.PHRightHand) +
            "; FloorLayer=" + Number(floorLayer) + "; ScenePhase=" + Number(scene.Phase) + "; SceneRate=" + Number(scene.Rate) +
            "; BodyMax=" + Number(maximumBodyPhase) + "; SeatPoint=" + seatPoint + "; TablePoint=" + tablePoint + "; NoProductionApproval=true");
    }
    private bool AllPlaying()
    {
        if (!Playing(actorHandle, bodyClip)) return false;
        foreach (Objeto item in objects) if (!Playing(item.Handle, item.Clipe)) return false;
        return true;
    }
    private void CheckScene(float rate)
    {
        if (scene == null || !scene.Exists()) throw Error("scene local interrompida");
        CheckPhase(scene.Phase);
        if (!Finite(scene.Rate) || System.Math.Abs(scene.Rate - rate) > 0.001f) throw Error("rate da scene diverge");
        if (rate == 0f && scene.Phase > 0.001f) throw Error("preparo avancou o clipe");
    }
    private bool Playing(int handle, string clip) { return GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, handle, dictionary, clip, 3); }
    private float Current(int handle, string clip) { return GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, handle, dictionary, clip); }
    private GTA.Math.Vector3 InitialPosition(string clip, GTA.Math.Vector3 origin, GTA.Math.Vector3 rotation)
    { return GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ANIM_INITIAL_OFFSET_POSITION, dictionary, clip, origin.X, origin.Y, origin.Z, rotation.X, rotation.Y, rotation.Z, 0f, 2); }
    private GTA.Math.Vector3 InitialRotation(string clip, GTA.Math.Vector3 origin, GTA.Math.Vector3 rotation)
    { return GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ANIM_INITIAL_OFFSET_ROTATION, dictionary, clip, origin.X, origin.Y, origin.Z, rotation.X, rotation.Y, rotation.Z, 0f, 2); }
    private GTA.Math.Vector3 Bone(GTA.Bone bone)
    {
        GTA.PedBone result = actor.Bones[bone]; if (result == null || !result.IsValid) throw Error("osso corporal ausente");
        Check(result.Position); return result.Position;
    }
    private void RequireActor()
    {
        if (!OwnActor())
        {
            Log("GORJETA_TRIAL_ATOR_FAIL", "OwnActor=false; Stage=" + stage + "; BeforeSameGuard=true; NoUnsafePedGetter=true");
            throw Error("identidade/saude/apoio da atriz diverge");
        }
        bool dead = actor.IsDead, injured = actor.IsInjured, ragdoll = actor.IsRagdoll;
        bool vehicle = actor.IsInVehicle(), water = actor.IsInWater, air = actor.IsInAir;
        bool physics = actor.HasPhysics, collision = actor.IsCollisionEnabled;
        int actualHealth = actor.Health;
        if (dead || injured || actualHealth != health || ragdoll || vehicle || water || air || !physics || !collision)
        {
            Log("GORJETA_TRIAL_ATOR_FAIL", "OwnActor=true; Stage=" + stage + "; Dead=" + dead + "; Injured=" + injured +
                "; Health=" + actualHealth + "; ExpectedHealth=" + health + "; Ragdoll=" + ragdoll + "; Vehicle=" + vehicle +
                "; Water=" + water + "; InAir=" + air + "; HasPhysics=" + physics + "; Collision=" + collision +
                "; Root=" + actor.Position + "; OriginalRoot=" + originalRoot + "; SharedOrigin=" + sceneOrigin +
                "; KnownMeasuredFloor=" + Number(floorLayer) + "; BeforeSameGuard=true");
            try
            {
                Log("GORJETA_TRIAL_ATOR_OSSOS_FAIL", "LeftFoot=" + Bone(GTA.Bone.SkelLeftFoot) +
                    "; RightFoot=" + Bone(GTA.Bone.SkelRightFoot) + "; Pelvis=" + Bone(GTA.Bone.SkelPelvis) +
                    "; Head=" + Bone(GTA.Bone.SkelHead) + "; OwnActor=true; NoAdditionalFloorQuery=true; NoPositionSetter=true");
            }
            catch (System.Exception ex) { Log("GORJETA_TRIAL_ATOR_OSSOS_FAIL", "MeasurementError=" + ex.Message + "; SameActorGuardStillRejects=true"); }
            throw Error("identidade/saude/apoio da atriz diverge");
        }
    }
    private bool OwnActor()
    { return actor != null && actor.Exists() && actorOwnership != null && actorOwnership(actor) && actor.Handle == actorHandle && actor.MemoryAddress == actorAddress && actor.Model.Hash == actorModel; }
    private bool Own(Objeto item)
    { return item.Entity != null && item.Entity.Exists() && item.Entity.Handle == item.Handle && item.Entity.MemoryAddress == item.Address && item.Entity.Model.Hash == item.Model; }
    private void RequireObjects()
    {
        foreach (Objeto item in objects)
        {
            if (!Own(item)) throw Error("identidade/visibilidade/volume do objeto proprio diverge");
            GTA.Math.Vector3 position = item.Entity.Position; Check(position);
            float distance = (position - originalRoot).Length();
            bool visible = item.Entity.IsVisible, attached = item.Entity.IsAttached();
            if (!visible || attached || !Finite(distance) || distance > objectRoomRadius)
            {
                Log("GORJETA_TRIAL_OBJETO_FAIL", "Model=" + item.Nome + "; Clip=" + item.Clipe + "; Position=" + position +
                    "; Initial=" + item.InitialPosition + "; OriginalRoot=" + originalRoot + "; Visible=" + visible +
                    "; Attached=" + attached + "; Distance=" + Number(distance) + "; ObjectRoomRadius=" + Number(objectRoomRadius) +
                    "; SharedOrigin=" + sceneOrigin + "; BeforeSameOwnershipVolumeGuard=true");
                throw Error("identidade/visibilidade/volume do objeto proprio diverge");
            }
        }
    }
    private void RequireTable()
    {
        if (ownObjects == null || !ownObjects.IsReady || table == null || !table.Exists() ||
            !System.Object.ReferenceEquals(table, ownObjects.GetProp(TableCode)) || table.Handle != tableHandle || table.Model.Hash != tableModel || table.MemoryAddress != tableAddress ||
            !table.IsVisible || !table.IsPositionFrozen || !table.IsCollisionEnabled || !table.HasPhysics || table.IsAttached() ||
            (table.Position - tablePosition).Length() > 0.01f || (table.Rotation - tableRotation).Length() > 0.01f) throw Error("mesa propria medida perdeu identidade/transform/physics");
    }

    public bool Cleanup()
    {
        if (disposed) return true;
        if (surfaceId != 0 && ownObjects != null && ownObjects.IsReady)
        {
            ownObjects.PollLoading(); GTA.Math.Vector3 point, normal;
            if (!ownObjects.TryGetMeasuredSurface(surfaceId, out point, out normal)) return false;
            surfaceId = 0;
        }
        if (taskOwned && OwnActor())
        {
            if (UpperCounting) GTA.Native.Function.Call(GTA.Native.Hash.STOP_ANIM_TASK, actorHandle, dictionary, bodyClip, -4f);
            else actor.Task.ClearAll();
            actor.IsPositionFrozen = wasFrozen;
        }
        if (upperPoses != null) { upperPoses.Cleanup(); upperPoses.Dispose(); upperPoses = null; }
        taskOwned = false;
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            Objeto item = objects[i];
            if (!Own(item)) { if (item.Entity != null && item.Entity.Exists()) throw Error("identidade propria mudou; nao deletar entidade substituta"); objects.RemoveAt(i); continue; }
            if (Playing(item.Handle, item.Clipe) && !item.Entity.StopSynchronizedAnim(new GTA.AnimationBlendDelta(8f), false)) return false;
            item.Entity.Delete(); if (item.Entity.Exists()) return false; objects.RemoveAt(i);
        }
        if (scene != null && scene.Exists()) { scene.HoldsLastFrame = false; scene.Rate = 1f; } scene = null;
        if (requestedDictionary && !dictionaryInitiallyLoaded) new GTA.CrClipDictionary(dictionary).MarkAsNoLongerNeeded(); requestedDictionary = false;
        foreach (string name in requestedModels) if (!modelsInitiallyLoaded.Contains(name)) new GTA.Model(name).MarkAsNoLongerNeeded(); requestedModels.Clear();
        running = ready = prepared = false;
        // Mesa e atriz sao propriedade do Main; nunca removidas/reposicionadas por este helper.
        return true;
    }
    public void Dispose() { if (!Cleanup()) throw Error("cleanup pendente; conservar instancia e mesa/atriz"); disposed = true; }

    // Variante tecnica exclusiva87. Pose primaria e apoiada pela mesma infraestrutura
    // de EP06; contador e TASK_PLAY_ANIM upper secondary, nunca flags de SyncedScene.
    private void PrepareUpperPose()
    {
        upperChair = ownObjects.GetProp(ChairCode);
        if (upperChair == null || !upperChair.Exists() || !ownObjects.IsSupport(ChairCode) ||
            upperChair.Model.Hash != new GTA.Model("prop_chair_01a").Hash)
            throw Error("upper counting exige cadeira propria prop_chair_01a declarada no ensaio");
        upperChairHandle = upperChair.Handle; upperChairModel = upperChair.Model.Hash; upperChairAddress = upperChair.MemoryAddress;
        upperChairPosition = upperChair.Position; upperChairRotation = upperChair.Rotation;
        ownObjects.GetMeasuredBounds(ChairCode, out upperChairMin, out upperChairMax);
        string path = System.IO.Path.Combine(menyoo, "PedAnimList.txt");
        string primaryDictionary = PosesSerieDiretor.GetCatalogDictionary("sentada_cadeira_livre_f");
        string primaryClip = PosesSerieDiretor.GetCatalogClip("sentada_cadeira_livre_f");
        string[] lines = System.IO.File.ReadAllLines(path); int line = 0;
        for (int i = 0; i < lines.Length; i++) if (lines[i].Trim() == primaryDictionary + " " + primaryClip) { line = i + 1; break; }
        if (line == 0) throw Error("pose primaria feminina nao consta na fonte instalada");
        upperPoses = new PosesSerieDiretor(log);
        var actors = new System.Collections.Generic.Dictionary<string, GTA.Ped>(System.StringComparer.OrdinalIgnoreCase);
        actors.Add(ActorId, actor);
        upperPoses.Prepare(actors, new PosesSerieDiretor.PoseSpec[] {
            new PosesSerieDiretor.PoseSpec { Ator = ActorId, Objeto = ChairCode, Recurso = "sentada_cadeira_livre_f", Tipo = "sentada",
                Dicionario = primaryDictionary, Clipe = primaryClip, RotacaoRelativa = 180f,
                OrigemArquivo = path, OrigemLinha = line, OrigemSha256 = "2C537060BD97FCF0DACA407DB1436A5D54E4B8DD5274DBC9EF0D8466EF6F21E4" }
        }, ownObjects, true); // Only exclusive UpperCounting preparation uses same-call own seat proof.
    }
    private void RequireUpperChair()
    {
        if (upperChair == null || !upperChair.Exists() || !System.Object.ReferenceEquals(upperChair, ownObjects.GetProp(ChairCode)) ||
            upperChair.Handle != upperChairHandle || upperChair.Model.Hash != upperChairModel || upperChair.MemoryAddress != upperChairAddress ||
            !upperChair.IsVisible || !upperChair.IsPositionFrozen || !upperChair.IsCollisionEnabled || !upperChair.HasPhysics || upperChair.IsAttached() ||
            (upperChair.Position - upperChairPosition).Length() > 0.01f || (upperChair.Rotation - upperChairRotation).Length() > 0.01f)
            throw Error("cadeira propria da base upper perdeu identidade/transform/fisica");
    }
    private void RequireUpperPose()
    {
        if (!upperPoseReady || upperPoses == null || !upperPoses.IsReady) throw Error("aguarde base feminina apoiada antes do upper");
        RequireUpperChair(); upperPoses.Update(System.Math.Max(0, lastElapsed));
        if ((actor.Position - upperRoot).Length() > 0.05f || HeadingDelta(actor.Heading, upperHeading) > 0.5f ||
            (Bone(GTA.Bone.SkelPelvis) - upperPelvis).Length() > 0.05f ||
            (Bone(GTA.Bone.SkelLeftFoot) - upperFootL).Length() > 0.05f || (Bone(GTA.Bone.SkelRightFoot) - upperFootR).Length() > 0.05f)
            throw Error("upper alterou raiz/pelve/pernas da base sentada; nenhuma reposicao durante acao");
    }
    private void BeginUpperHeld()
    {
        RequireUpperPose();
        scene = GTA.FwSyncedScene.Create(sceneOrigin, sceneRotation, GTA.EulerRotationOrder.YXZ);
        if (scene == null) throw Error("scene propria dos props upper recusada");
        actor.Task.PlayAnimation(new GTA.CrClipAsset(dictionary, bodyClip), new GTA.AnimationBlendDelta(8f), new GTA.AnimationBlendDelta(-8f), -1,
            GTA.AnimationFlags.UpperBodyOnly | GTA.AnimationFlags.Secondary | GTA.AnimationFlags.StayInEndFrame, 0f, false, GTA.AnimationIKControlFlags.None);
        taskOwned = true;
        foreach (Objeto item in objects)
        {
            item.Entity.IsPositionFrozen = false;
            if (!item.Entity.PlaySynchronizedAnim(scene, new GTA.CrClipAsset(dictionary, item.Clipe), new GTA.AnimationBlendDelta(8f),
                new GTA.AnimationBlendDelta(8f), GTA.SyncedSceneFlags.None, new GTA.AnimationBlendDelta(1.5f))) throw Error("upper prop track recusado");
            item.Entity.IsCollisionEnabled = false;
        }
        upperBindingPending = true;
        Log("GORJETA_TRIAL_UPPER_BEGIN", "BodyFlags=UpperBodyOnly|Secondary|StayInEndFrame; PrimaryFemaleBaseRetained=true; PropsOwnFwScene=true; NoSyncedFlagsCast=true; NoFullBodyReposition=true; AwaitExactBinding=true; ProductionApproval=false");
    }
    private void RequireUpperHeldBody()
    {
        RequireUpperPose();
        float phase = Current(actorHandle, bodyClip); CheckPhase(phase);
        if (!Playing(actorHandle, bodyClip) || phase > 0.001f) throw Error("upper clipe corporal avancou durante preparo/contagem");
    }
    private void MeasureUpperSeat()
    {
        RequireUpperPose(); GTA.Math.Vector3 pelvis = Bone(GTA.Bone.SkelPelvis);
        GTA.Math.Vector3 local = upperChair.GetPositionOffset(pelvis); Check(local);
        if (local.X < upperChairMin.X || local.X > upperChairMax.X || local.Y < upperChairMin.Y || local.Y > upperChairMax.Y)
            throw Error("upper pelve fora do volume XY da cadeira propria");
        GTA.ShapeTestResult result;
        GTA.ShapeTestHandle ray = GTA.ShapeTest.StartExpensiveSyncTestLOSProbe(pelvis + new GTA.Math.Vector3(0f, 0f, 0.25f),
            pelvis - new GTA.Math.Vector3(0f, 0f, 2f), GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, actor, (GTA.ShapeTestOptions)0);
        GTA.Entity hit;
        if (ray.GetResult(out result) != GTA.ShapeTestStatus.Ready || !result.DidHit || !result.TryGetHitEntity(out hit) || hit == null ||
            hit.Handle != upperChairHandle || hit.Model.Hash != upperChairModel || hit.MemoryAddress != upperChairAddress)
            throw Error("upper assento nao confirmou own hit da propria cadeira");
        Check(result.HitPosition); Check(result.SurfaceNormal); float gap = pelvis.Z - result.HitPosition.Z;
        Log("GORJETA_TRIAL_ASSENTO", "Prop=prop_chair_01a; OwnHit=true; Point=" + result.HitPosition + "; Normal=" + result.SurfaceNormal +
            "; Pelvis=" + pelvis + "; ContactZ=" + Number(gap) + "; UpperBodyExperimental=true; OriginalGuard0to10cm=true");
        LogSeatSkeleton(upperChair, upperChairMin, upperChairMax, result.HitPosition);
        if (result.SurfaceNormal.Z < 0.98f || gap < 0f || gap > 0.10f) throw Error("upper assento diverge da guarda original0..10cm");
        hasSeatPoint = true; seatPoint = result.HitPosition;
    }
    private void LogUpperClockIfNeeded(int elapsed, float bodyPhase, float propPhase, float gameTimePhase)
    {
        if (elapsed < nextLog) return;
        Log("GORJETA_TRIAL_UPPER_CLOCK", "Elapsed=" + elapsed + "; ScriptBodyPhase=" + Number(bodyPhase) + "; PropFwPhase=" + Number(propPhase) +
            "; GameTimePhase=" + Number(gameTimePhase) + "; ElapsedClock=GTA.Game.GameTime" + "; BodyPropDelta=" + Number(System.Math.Abs(bodyPhase - propPhase)) +
            "; OwnPropsRate=" + Number(scene.Rate) + "; PerFramePhaseSetter=false; PerFrameSpeedSetter=false; ProductionApproval=false");
    }
    private static float HeadingDelta(float a, float b)
    { float d = (a - b) % 360f; if (d > 180f) d -= 360f; if (d < -180f) d += 360f; return System.Math.Abs(d); }


    // Telemetria somente: ossos sao centros de articulacao, nao vertices/pele.
    // Nunca usa thigh/calves/model AABB para aceitar contato que a guarda rejeitou.
    private void LogSeatSkeleton(GTA.Prop support, GTA.Math.Vector3 minimum, GTA.Math.Vector3 maximum, GTA.Math.Vector3 surface)
    {
        try
        {
            GTA.Math.Vector3 bodyMin, bodyMax; actor.Model.GetDimensions(out bodyMin, out bodyMax); Check(bodyMin); Check(bodyMax);
            float supportLow = float.MaxValue, supportHigh = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                GTA.Math.Vector3 corner = support.GetOffsetPosition(new GTA.Math.Vector3((i & 1) == 0 ? minimum.X : maximum.X,
                    (i & 2) == 0 ? minimum.Y : maximum.Y, (i & 4) == 0 ? minimum.Z : maximum.Z)); Check(corner);
                supportLow = System.Math.Min(supportLow, corner.Z); supportHigh = System.Math.Max(supportHigh, corner.Z);
            }
            Log("GORJETA_TRIAL_ASSENTO_BOUNDS", "ActorModel=" + actorModel + "; ActorMin=" + bodyMin + "; ActorMax=" + bodyMax +
                "; OwnSupportHandle=" + support.Handle + "; OwnSupportModel=" + support.Model.Hash + "; OwnSupportPosition=" + support.Position +
                "; OwnSupportRotation=" + support.Rotation + "; SupportMin=" + minimum + "; SupportMax=" + maximum +
                "; SupportWorldLow=" + Number(supportLow) + "; SupportWorldHigh=" + Number(supportHigh) +
                "; SupportBottomFloorGap=" + Number(supportLow - floorLayer) + "; OwnMeasuredSeat=" + surface +
                "; ModelAabbIsNotSkinnedSurface=true; ContactGuardUnchanged=true");
            foreach (GTA.Bone tag in new GTA.Bone[] { GTA.Bone.SkelPelvis, GTA.Bone.SkelLeftThigh, GTA.Bone.SkelRightThigh,
                GTA.Bone.SkelLeftCalf, GTA.Bone.SkelRightCalf, GTA.Bone.SkelLeftFoot, GTA.Bone.SkelRightFoot })
            {
                GTA.PedBone bone = actor.Bones[tag];
                if (bone == null || !bone.IsValid) { Log("GORJETA_TRIAL_ASSENTO_OSSO", "Tag=" + tag + "; Missing=true; DiagnosticOnly=true"); continue; }
                GTA.Math.Vector3 world = bone.Position, local = support.GetPositionOffset(world); Check(world); Check(local);
                bool xy = local.X >= minimum.X && local.X <= maximum.X && local.Y >= minimum.Y && local.Y <= maximum.Y;
                Log("GORJETA_TRIAL_ASSENTO_OSSO", "Tag=" + tag + "; Index=" + bone.Index + "; World=" + world + "; SupportLocal=" + local +
                    "; InOwnSupportAabbXY=" + xy + "; ZAboveMeasuredOwnSeat=" + Number(world.Z - surface.Z) +
                    "; BoneOriginIsNotSkin=true; AcceptanceUsed=false; ContactGuard0to10cmUnchanged=true");
            }
        }
        catch (System.Exception ex) { Log("GORJETA_TRIAL_ASSENTO_DIAGNOSTICO_FAIL", "Error=" + ex.Message + "; ExistingContactGuardStillRuns=true"); }
    }

    private void VerifySources()
    {
        string ap = System.IO.Path.Combine(menyoo ?? "", "PedAnimList.txt"), pp = System.IO.Path.Combine(menyoo ?? "", "PropList.txt");
        VerifyFile(ap, "2C537060BD97FCF0DACA407DB1436A5D54E4B8DD5274DBC9EF0D8466EF6F21E4");
        VerifyFile(pp, "35BFD924919FC223F5AB131941643B2AE4E0082FCD0ECEF1A5A50FCDEE9B2552");
        var anim = new System.Collections.Generic.HashSet<string>(System.IO.File.ReadAllLines(ap), System.StringComparer.Ordinal);
        var models = new System.Collections.Generic.HashSet<string>(System.IO.File.ReadAllLines(pp), System.StringComparer.Ordinal);
        if (!anim.Contains(dictionary + " " + bodyClip)) throw Error("par corporal instalado ausente");
        foreach (Objeto item in objects) if (!anim.Contains(dictionary + " " + item.Clipe) || !models.Contains(item.Nome)) throw Error("par/modelo instalado ausente: " + item.Clipe);
    }
    private static void VerifyFile(string path, string expected)
    {
        RequirePlainFile(path);
        using (var alg = System.Security.Cryptography.SHA256.Create()) using (var file = System.IO.File.OpenRead(path))
            if (System.BitConverter.ToString(alg.ComputeHash(file)).Replace("-", "") != expected) throw Error("SHA de fonte instalada diverge");
    }
    private static void RequirePlainFile(string path)
    {
        string full = System.IO.Path.GetFullPath(path);
        if (!System.IO.File.Exists(full)) throw Error("arquivo ausente");
        string p = full;
        while (p != null)
        {
            if ((System.IO.File.GetAttributes(p) & System.IO.FileAttributes.ReparsePoint) != 0) throw Error("link/reparse proibido");
            p = System.IO.Path.GetDirectoryName(p);
        }
    }
    private static string Unquote(string value) { if (value.Length < 2 || value[0] != '"') throw Error("string exigida"); return value.Substring(1, value.Length - 2); }
    private static void Validate(EnsaioSpec value)
    {
        if (value == null || (value.Tipo != "envelope" && value.Tipo != "contagem")) throw Error("tipo envelope/contagem exigido");
        if (value.Tipo == "envelope" && (value.Segmento < 0 || value.Segmento > 56 || (value.Corpo != "player" && value.Corpo != "terapeuta") || value.JanelaMs != 14000)) throw Error("envelope0..56/player|terapeuta/14s");
        bool measuredFullV4Window33 = value.Tipo == "contagem" && value.Segmento == 4 && value.Corpo == "cfm_feminino" && value.JanelaMs == 33000;
        if (value.Tipo == "contagem" && ((value.Segmento != 2 && value.Segmento != 4 && value.Segmento != 6) || (value.Corpo != "cfm_feminino" && value.Corpo != "cfm_feminino_upper") || value.JanelaMs < 12000 || (value.JanelaMs > 30000 && !measuredFullV4Window33))) throw Error("CFM2|4|6/janela12..30s; somente CFM4 full feminino admite33s medidos; fullFit dos8 tracks obrigatorio");
    }
    private static bool Finite(float value) { return !System.Single.IsNaN(value) && !System.Single.IsInfinity(value); }
    private static void Check(GTA.Math.Vector3 value) { if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Z)) throw Error("vetor nao finito"); }
    private static void CheckPhase(float value) { if (!Finite(value) || value < 0f || value > 1.001f) throw Error("fase nativa invalida"); }
    private static string Number(float value) { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private static void Story() { if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS)) throw Error("somente Story; nenhum NETWORK scene e executado"); }
    private void Active() { if (disposed) throw new System.ObjectDisposedException("GorjetaFisicaTrialDiretor"); }
    private void Log(string code, string message) { if (log != null) log(code, message); }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("GorjetaTrial: " + message); }
}
