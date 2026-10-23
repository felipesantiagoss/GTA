// Poses iniciais em apoios próprios; nenhuma classe Script ou reposição durante a filmagem.
public sealed class PosesSerieDiretor : System.IDisposable
{
    public sealed class PoseSpec
    {
        public string Ator, Objeto, Recurso, Tipo, Dicionario, Clipe, OrigemArquivo, OrigemSha256, ObjetoAbracado;
        public int OrigemLinha;
        public GTA.Math.Vector3 SuperficieRelativa;
        public bool UsarCentroMedido = true;
        public float RotacaoRelativa;
    }
    public sealed class TransitionSpec
    {
        public string Ator, Tipo, Recurso, Dicionario, Clipe, OrigemArquivo, OrigemSha256;
        public int InicioMs, DuracaoMs, OrigemLinha;
    }
    private sealed class PoseState
    {
        public PoseSpec Spec;
        public GTA.Ped Ped;
        public int Handle, Model, Surface, StartedAt, Corrections, LastTick = -1, EmbraceLogAt;
        public bool EmbraceLogged;
        public System.IntPtr Address;
        public GTA.Math.Vector3 OriginalPosition, SurfacePoint, EntryStartPosition, EntryProbeStartPosition;
        public float OriginalHeading, MeasuredClearance, EntryStartHeading, FloorContactMaximum;
        public bool OriginalFrozen, Started, Ready, ClearanceMeasured, FloorSurfaceFound;
        public GTA.Math.Vector3 Office84PlaneNormal;
        public bool Office84PlaneMeasured, Office84PreparationGapSet;
        public TransitionSpec Transition;
        public bool TransitionStarted, TransitionDone, TransitionObserved;
        public float TransitionMaxPhase;
        public int TransitionDurationMs, TransitionHealth, TransitionStartedAt, TransitionTelemetryAt;
        public bool TransitionTelemetryLogged;
        public bool ExitChairPending, ExitChairOriginalCollision;
        public int ExitChairHandle, ExitChairModel;
        public System.IntPtr ExitChairAddress;
        public GTA.Math.Vector3 ExitChairPosition, ExitChairRotation, ExitChairMin, ExitChairMax, ExitActorMin, ExitActorMax;
        public int EntryProbeStage, EntryProbeAt;
        public bool EntryStartMeasured, EntryBaseRequested;
    }
    private sealed class ClipInfo
    {
        public string Dictionary, Clip, Type;
        public bool Male;
    }
    private static readonly System.Collections.Generic.Dictionary<string, ClipInfo> Clips =
        new System.Collections.Generic.Dictionary<string, ClipInfo>(System.StringComparer.Ordinal)
    {
        { "sentada_cadeira_f", new ClipInfo { Dictionary = "amb@prop_human_seat_chair@female@arms_folded@base", Clip = "base", Type = "sentada" } },
        { "sentada_cadeira_livre_f", new ClipInfo { Dictionary = "amb@prop_human_seat_chair@female@proper@base", Clip = "base", Type = "sentada" } },
        { "sentado_cadeira_m", new ClipInfo { Dictionary = "amb@prop_human_seat_chair@male@generic@base", Clip = "base", Type = "sentada", Male = true } },
        { "sentada_meio_fio_f", new ClipInfo { Dictionary = "amb@world_human_seat_steps@female@hands_by_sides@base", Clip = "base", Type = "sentada" } },
        { "sentada_chao_f", new ClipInfo { Dictionary = "amb@world_human_picnic@female@base", Clip = "base", Type = "chao" } },
        { "deitada_cama_f", new ClipInfo { Dictionary = "mp_bedmid", Clip = "f_sleep_l_loop_bighouse", Type = "deitada" } },
        { "sono_f", new ClipInfo { Dictionary = "mp_sleep", Clip = "sleep_loopfemale", Type = "deitada" } },
        { "sono_banco_tracy_f", new ClipInfo { Dictionary = "timetable@tracy@sleep@", Clip = "base", Type = "deitada" } },
        { "office_cell_sleep_f", new ClipInfo { Dictionary = "anim@scripted@freemode@ig6_office_cell_sleep@female@", Clip = "base_pose_02", Type = "deitada" } }
    };
    private readonly System.Action<string, string> log;
    private readonly System.Collections.Generic.List<PoseState> poses = new System.Collections.Generic.List<PoseState>();
    private readonly System.Collections.Generic.HashSet<string> requested = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
    private ObjetosSerieDiretor props;
    private System.Diagnostics.Stopwatch loading;
    private bool prepared, ready, disposed;
    private bool synchronousSurfacePreparation;
    private bool curbC3RigidPreparation;
    private int lastElapsed = -1;
    private bool transitionsConfigured;
    private int officeCell84StartAt = -1, officeCell84LastAt = -1, officeCell84Samples;
    private bool officeCell84WindowObserved, office84BagBoundsLogged;

    public PosesSerieDiretor(System.Action<string, string> log) { this.log = log; }
    public bool IsReady { get { return prepared && ready && !disposed; } }
    public int Count { get { return poses.Count; } }
    public bool AreTransitionsComplete
    { get { foreach (PoseState state in poses) if (state.Transition != null && !state.TransitionDone) return false; return true; } }
    public static string GetCatalogDictionary(string recurso) { return Catalog(recurso).Dictionary; }
    public static string GetCatalogClip(string recurso) { return Catalog(recurso).Clip; }
    public static string GetCatalogType(string recurso) { return Catalog(recurso).Type; }

    // Pares existentes, ainda candidatos visuais. Nunca libera qualquer clipe sobre qualquer pose.
    public static bool CanOverlay(string pose, string dictionary, string clip)
    {
        if (pose != "sentada_cadeira_livre_f" && pose != "sentado_cadeira_m") return false;
        return IsOverlayClip(dictionary, clip);
    }
    public static bool IsOverlayClip(string dictionary, string clip)
    {
        return (dictionary == "mp_common" && (clip == "givetake1_a" || clip == "givetake1_b")) ||
            (dictionary == "mp_common_miss" && clip == "put_away_coke");
    }
    public void RequireSeatedOverlay(GTA.Ped ped, string dictionary, string clip)
    {
        Active(); if (!IsReady) throw Error("aguarde assentamento antes do gesto sentado");
        foreach (PoseState state in poses)
        {
            if (System.Object.ReferenceEquals(ped, null) || ped.Handle != state.Handle) continue;
            RequireOwned(state);
            if (ped.Model.Hash != state.Model || ped.MemoryAddress != state.Address ||
                !CanOverlay(state.Spec.Recurso, dictionary, clip)) throw Error("gesto/identidade incompatível com a pose sentada");
            GTA.Prop support = props.GetProp(state.Spec.Objeto);
            if (!support.IsVisible || !support.IsPositionFrozen || !Playing(state)) throw Error("o gesto exige apoio e pose primária preservados");
            CheckGeometry(state, Bone(state, GTA.Bone.SkelPelvis), Bone(state, GTA.Bone.SkelHead),
                Bone(state, GTA.Bone.SkelLeftFoot), Bone(state, GTA.Bone.SkelRightFoot));
            return;
        }
        throw Error("ator sem pose sentada própria preparada");
    }

    public void Prepare(System.Collections.Generic.Dictionary<string, GTA.Ped> ownActors,
                        System.Collections.Generic.IEnumerable<PoseSpec> specs, ObjetosSerieDiretor ownProps, bool sameCallSurfaceProof = false, string curbSceneId = null)
    {
        Active();
        if (prepared || poses.Count != 0) throw Error("limpe as poses anteriores");
        if (ownActors == null || specs == null) throw Error("elenco/specs e objetos próprios já preparados são necessários");
        var actors = new System.Collections.Generic.Dictionary<string, GTA.Ped>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var actor in ownActors) actors.Add(actor.Key, actor.Value);
        var copy = new System.Collections.Generic.List<PoseSpec>();
        foreach (PoseSpec s in specs) copy.Add(Copy(s));
        VerifySourceEvidence(copy); // Puro_FILE antes de alterar qualquer tarefa/posição do ator.
        if (curbSceneId != null)
        {
            if (curbSceneId != "EP03_C03" || copy.Count != 1 || actors.Count != 1 ||
                copy[0].Ator != "DJANIRA_POBRE" || copy[0].Recurso != "sentada_meio_fio_f" || copy[0].Tipo != "chao" ||
                copy[0].Objeto != null || copy[0].ObjetoAbracado != null || copy[0].RotacaoRelativa != 0f ||
                copy[0].Dicionario != "amb@world_human_seat_steps@female@hands_by_sides@base" || copy[0].Clipe != "base")
                throw Error("C3 rigid contact exclusive own Poor/curb/female base/chao, no prop or extra actor.");
            curbC3RigidPreparation = true;
        }
        bool needsProps = false;
        foreach (PoseSpec s in copy)
            if (!System.String.IsNullOrEmpty(s.Objeto) || !System.String.IsNullOrEmpty(s.ObjetoAbracado)) needsProps = true;
        if (needsProps && (ownProps == null || !ownProps.IsReady))
            throw Error("apoios/objetos da pose exigem objetos proprios ja preparados");
        foreach (PoseSpec s in copy)
        {
            GTA.Ped ped;
            if (!actors.TryGetValue(s.Ator, out ped)) throw Error("ator da pose não pertence ao elenco");
            RequirePed(ped);
            if (s.Tipo != "sentada" && s.Tipo != "deitada" && s.Tipo != "chao") throw Error("pose inicial exige base sentada/deitada/chão");
            if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_PED_MALE, ped.Handle) != Catalog(s.Recurso).Male) throw Error("variante de pose incompatível com sexo do modelo real");
            if (s.Tipo != "chao" && !ownProps.IsSupport(s.Objeto)) throw Error("pose exige móvel de apoio próprio");
            if (s.Recurso == "office_cell_sleep_f")
            {
                float seatX, seatY;
                if (copy.Count != 1 || actors.Count != 1 || s.Ator != "DJANIRA_POBRE" || s.Tipo != "deitada" || s.RotacaoRelativa != 90f ||
                    s.Objeto != "BANCO_PRAIA" || s.ObjetoAbracado != null || ownProps == null || !ownProps.IsOfficeCellBodyPreview84 ||
                    s.Dicionario != "anim@scripted@freemode@ig6_office_cell_sleep@female@" || s.Clipe != "base_pose_02" || !ownProps.TryGetSleepBenchSurfaceXY(s.Objeto, out seatX, out seatY))
                    throw Error("office_cell_sleep_f exclusivo corpo tecnico84 deitado90 em bench02 proprio, sem sacola/abraco.");
            }
            if (s.Recurso == "sono_banco_tracy_f")
            {
                float seatX, seatY;
                if (copy.Count != 1 || actors.Count != 1 || s.Ator != "DJANIRA_POBRE" ||
                    s.Tipo != "deitada" || s.Objeto != "BANCO_PRAIA" || s.ObjetoAbracado != "SACOLA_DJANIRA" ||
                    s.Dicionario != "timetable@tracy@sleep@" || s.Clipe != "base" ||
                    ownProps == null || !ownProps.TryGetSleepBenchSurfaceXY(s.Objeto, out seatX, out seatY))
                    throw Error("Tracy sleep loop exclusive measured own EP10_C01 bench02/Djanira/bag; never arbitrary support");
            }
            if (s.ObjetoAbracado != null)
            {
                Identifier(s.ObjetoAbracado, true);
                if ((s.Recurso != "sono_f" && s.Recurso != "sono_banco_tracy_f") || s.Tipo != "deitada" || s.ObjetoAbracado == s.Objeto)
                    throw Error("abraçar objeto exige sono_f deitada e prop próprio diferente do banco");
                GTA.Prop embraced = ownProps.GetProp(s.ObjetoAbracado);
                if (!embraced.IsVisible || !embraced.IsAttachedTo(ped))
                    throw Error("a sacola abraçada precisa estar visível e anexada ao próprio ator");
            }
            Vector(s.SuperficieRelativa, 10f);
            if (s.SuperficieRelativa.Z != 0f || !Finite(s.RotacaoRelativa) || System.Math.Abs(s.RotacaoRelativa) > 180f)
                throw Error("superfície só recebe XY relativo e heading±180graus; Z é medido no apoio");
            poses.Add(new PoseState { Spec = s, Ped = ped, Handle = ped.Handle, Model = ped.Model.Hash, Address = ped.MemoryAddress,
                OriginalPosition = ped.Position, OriginalHeading = ped.Heading, OriginalFrozen = ped.IsPositionFrozen });
        }
        if (sameCallSurfaceProof && (!needsProps || copy.Count != 1)) throw Error("same-call surface proof requires one own supported preparation pose");
        synchronousSurfacePreparation = sameCallSurfaceProof;
        props = needsProps ? ownProps : null; prepared = true; ready = poses.Count == 0;
        loading = System.Diagnostics.Stopwatch.StartNew();
        foreach (PoseState state in poses)
        {
            string dictionary = state.Spec.Dicionario;
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, dictionary)) requested.Add(dictionary);
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, dictionary);
        }
        RequestSurfaces();
        Log("POSES_SERIE_PREPARO", "Quantidade=" + poses.Count + "; InicialSomente=true; TesteVisual=pendente");
    }

    // Saída de cadeira/chão real; uma transição por ator, nunca reposiciona durante a ação.
    public void PrepareTransitions(System.Collections.Generic.IEnumerable<TransitionSpec> specs)
    {
        Active();
        if (!prepared || specs == null || transitionsConfigured || lastElapsed >= 0) throw Error("transições são configuradas uma vez antes da ação");
        var used = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var sourceHashes = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (TransitionSpec original in specs)
        {
            if (original == null || !used.Add(original.Ator ?? "") || used.Count > 5) throw Error("transições exigem até5atores únicos");
            TransitionSpec spec = new TransitionSpec { Ator = (original.Ator ?? "").ToUpperInvariant(), Tipo = original.Tipo,
                Recurso = original.Recurso, Dicionario = original.Dicionario, Clipe = original.Clipe,
                InicioMs = original.InicioMs, DuracaoMs = original.DuracaoMs, OrigemArquivo = original.OrigemArquivo,
                OrigemLinha = original.OrigemLinha, OrigemSha256 = original.OrigemSha256 };
            if ((spec.Tipo != "levantar" && spec.Tipo != "sentar") || spec.InicioMs < 0 || spec.DuracaoMs < 500 || spec.DuracaoMs > 8000 ||
                spec.InicioMs > 180000 - spec.DuracaoMs) throw Error("transição exige janela real de0.5–8s até180s");
            PoseState state = null;
            foreach (PoseState candidate in poses) if (candidate.Spec.Ator == spec.Ator) { state = candidate; break; }
            if (state == null) throw Error("transição exige pose inicial do próprio ator");
            string dictionary, clip, resource;
            if (state.Spec.Recurso == "sentada_cadeira_f")
            { resource = "levantar_cadeira_f"; dictionary = "amb@prop_human_seat_chair@female@arms_folded@exit"; clip = "exit_fwd"; }
            else if (state.Spec.Recurso == "sentada_cadeira_livre_f")
            { resource = "levantar_cadeira_livre_f"; dictionary = "amb@prop_human_seat_chair@female@proper@exit"; clip = "exit_forward"; }
            else if (state.Spec.Recurso == "sentado_cadeira_m")
            { resource = "levantar_cadeira_m"; dictionary = "amb@prop_human_seat_chair@male@generic@exit"; clip = "exit_forward"; }
            else if (state.Spec.Recurso == "sentada_chao_f")
            { resource = "levantar_chao_f"; dictionary = "amb@world_human_picnic@female@exit"; clip = "exit"; }
            else throw Error("esta pose ainda não tem saída canônica pesquisada");
            if (spec.Tipo == "sentar")
            {
                resource = resource.Replace("levantar_", "sentar_"); dictionary = dictionary.Replace("@exit", "@enter");
                clip = state.Spec.Recurso == "sentado_cadeira_m" ? "enter_forward" : state.Spec.Recurso == "sentada_chao_f" ? "enter" : "enter_fwd";
            }
            if (spec.Recurso != resource || spec.Dicionario != dictionary || spec.Clipe != clip) throw Error("recurso de saída não corresponde à pose inicial real");
            VerifyTransitionSource(spec, sourceHashes);
            state.Transition = spec;
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, dictionary)) requested.Add(dictionary);
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, dictionary);
        }
        transitionsConfigured = true; ready = poses.Count == 0;
    }

    private static void VerifyTransitionSource(TransitionSpec spec, System.Collections.Generic.Dictionary<string, string> hashes)
    {
        if (spec.OrigemLinha < 1 || spec.OrigemLinha > 500000 || System.String.IsNullOrEmpty(spec.OrigemArquivo) ||
            !System.IO.Path.IsPathRooted(spec.OrigemArquivo) || !System.Text.RegularExpressions.Regex.IsMatch(spec.OrigemSha256 ?? "", "\\A[0-9A-Fa-f]{64}\\z"))
            throw Error("transição exige origem local, linha e SHA256 reais");
        string path = System.IO.Path.GetFullPath(spec.OrigemArquivo);
        if (path.StartsWith("\\\\", System.StringComparison.Ordinal) || path.Substring(2).IndexOf(':') >= 0 ||
            !System.String.Equals(System.IO.Path.GetFileName(path), "PedAnimList.txt", System.StringComparison.OrdinalIgnoreCase) ||
            !System.String.Equals(new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(path)).Name, "menyooStuff", System.StringComparison.OrdinalIgnoreCase))
            throw Error("transição exige PedAnimList.txt instalada");
        NoLinks(path);
        string digest;
        if (!hashes.TryGetValue(path, out digest))
        {
            using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                if (stream.Length > 64L * 1024 * 1024) throw Error("fonte de transições excede64MB");
                using (var sha = System.Security.Cryptography.SHA256.Create()) digest = System.BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }
            hashes.Add(path, digest);
        }
        if (digest != spec.OrigemSha256.ToUpperInvariant()) throw Error("SHA256 da transição diverge da fonte instalada");
        using (var reader = new System.IO.StreamReader(path, new System.Text.UTF8Encoding(false, true), true))
        {
            string line = null;
            for (int i = 0; i < spec.OrigemLinha; i++) { line = reader.ReadLine(); if (line == null) throw Error("linha da transição ausente"); }
            if (line.Trim() != spec.Dicionario + " " + spec.Clipe) throw Error("par da transição não consta na linha real");
        }
        NoLinks(path);
    }

    private void RequestSurfaces()
    {
        foreach (PoseState state in poses)
        {
            if (state.Spec.Tipo == "chao")
            {
                state.Surface = -1; state.FloorSurfaceFound = false;
                state.Ready = false; state.Started = false; state.Corrections = 0; state.LastTick = -1;
                state.MeasuredClearance = 0f; state.ClearanceMeasured = false; state.FloorContactMaximum = 0f;
                continue;
            }
            GTA.Math.Vector3 minimum, maximum; props.GetMeasuredBounds(state.Spec.Objeto, out minimum, out maximum);
            float x = state.Spec.UsarCentroMedido ? (minimum.X + maximum.X) * 0.5f : state.Spec.SuperficieRelativa.X;
            float y = state.Spec.UsarCentroMedido ? (minimum.Y + maximum.Y) * 0.5f : state.Spec.SuperficieRelativa.Y;
            float seatX, seatY;
            if (props.TryGetSleepBenchSurfaceXY(state.Spec.Objeto, out seatX, out seatY)) { x = seatX; y = seatY; }
            state.Surface = props.RequestSurface(state.Spec.Objeto, x, y, synchronousSurfacePreparation);
            state.Office84PreparationGapSet = false;
            state.Ready = false; state.Started = false; state.Corrections = 0; state.LastTick = -1;
            state.MeasuredClearance = 0f; state.ClearanceMeasured = false; state.FloorContactMaximum = 0f;
        }
    }

    // Cada chamada deve ser em um Tick; streaming/raycasts não bloqueiam nem usam Wait.
    public void PollLoading()
    {
        Active(); if (!prepared) throw Error("prepare as poses antes de carregar");
        if (ready) return;
        if (loading.ElapsedMilliseconds > 8000) throw Error("apoio/clipe/alinhamento não ficou pronto em8s");
        if (props != null) props.PollLoading(); bool all = true;
        foreach (PoseState state in poses)
        {
            if (state.Transition == null) continue;
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, state.Transition.Dicionario)) { all = false; continue; }
            float seconds = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, state.Transition.Dicionario, state.Transition.Clipe);
            if (!Finite(seconds) || seconds <= 0f || seconds * 1000f > state.Transition.DuracaoMs)
                throw Error("janela termina antes do clipe real de levantar");
            state.TransitionDurationMs = (int)System.Math.Ceiling(seconds * 1000f);
        }
        foreach (PoseState state in poses)
        {
            RequireOwned(state);
            if (state.Ready) continue;
            if (state.EntryProbeStage != 0) { if (!PollEntryStart(state)) all = false; continue; }
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, state.Spec.Dicionario)) { all = false; continue; }
            float duration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, state.Spec.Dicionario, state.Spec.Clipe);
            if (!Finite(duration) || duration <= 0f) throw Error("clipe de pose não tem duração real positiva");
            GTA.Math.Vector3 normal;
            if (state.Spec.Tipo == "chao")
            {
                if (!state.FloorSurfaceFound)
                {
                    float ground;
                    GTA.Math.Vector3 query = state.OriginalPosition + new GTA.Math.Vector3(0f, 0f, 0.5f);
                    if (!GTA.World.GetGroundHeightAndNormal(query, out ground, out normal)) { all = false; continue; }
                    Vector(normal, 2f);
                    if (!Finite(ground) || normal.Z < 0.98f || System.Math.Abs(ground - state.OriginalPosition.Z) > 2f)
                        throw Error("pose no chão exige piso real plano até2m abaixo/acima da marca");
                    state.SurfacePoint = new GTA.Math.Vector3(state.OriginalPosition.X, state.OriginalPosition.Y, ground);
                    state.FloorSurfaceFound = true;
                    Log("POSES_CHAO_MEDIDO", "Ator=" + state.Spec.Ator + "; Ponto=" + state.SurfacePoint + "; Normal=" + normal + "; SomentePreparo=true");
                }
            }
            else
            {
                if (!props.TryGetMeasuredSurface(state.Surface, out state.SurfacePoint, out normal)) { all = false; continue; }
                if (IsOfficeCellBench84Scope(state))
                {
                    Vector(normal, 2f); if (normal.Z < 0.98f) throw Error("Sono84 own measured plane invalid.");
                    state.Office84PlaneNormal = normal; state.Office84PlaneMeasured = true;
                }
            }
            if (!state.Started)
            {
                state.Ped.Task.ClearAllImmediately(); state.Ped.Velocity = GTA.Math.Vector3.Zero;
                state.Ped.Heading = (state.Spec.Tipo == "chao" ? state.OriginalHeading : props.GetProp(state.Spec.Objeto).Heading) + state.Spec.RotacaoRelativa;
                state.Ped.IsPositionFrozen = true;
                state.Ped.Task.PlayAnimation(state.Spec.Dicionario, state.Spec.Clipe, 8f, -8f, -1, GTA.AnimationFlags.Loop, 0f);
                state.Started = true; state.StartedAt = GTA.Game.GameTime; all = false; continue;
            }
            if (!Playing(state) || unchecked((uint)(GTA.Game.GameTime - state.StartedAt)) < 250u) { all = false; continue; }
            if (state.LastTick == GTA.Game.GameTime) { all = false; continue; }
            state.LastTick = GTA.Game.GameTime;
            GTA.Math.Vector3 pelvis = Bone(state, GTA.Bone.SkelPelvis), head = Bone(state, GTA.Bone.SkelHead);
            GTA.Math.Vector3 leftFoot = Bone(state, GTA.Bone.SkelLeftFoot), rightFoot = Bone(state, GTA.Bone.SkelRightFoot);
            if (state.Spec.Recurso == "office_cell_sleep_f") LogOfficeCell84(state, "ANTES_CORRECAO", 0, pelvis, head, leftFoot, rightFoot);
            LogSleepBenchAxis(state, "ANTES_CORRECAO", pelvis, head, leftFoot, rightFoot);
            bool seated = state.Spec.Tipo == "sentada" || state.Spec.Tipo == "chao";
            float contactZ = seated ? pelvis.Z : System.Math.Min(System.Math.Min(pelvis.Z, head.Z), System.Math.Min(leftFoot.Z, rightFoot.Z));
            GTA.Math.Vector3 correction = new GTA.Math.Vector3(state.SurfacePoint.X - pelvis.X, state.SurfacePoint.Y - pelvis.Y, state.SurfacePoint.Z + state.MeasuredClearance - contactZ);
            bool office84 = IsOfficeCellBench84Scope(state);
            float office84MinimumGap = office84 ? OfficeCellBench84MinimumPlaneGap(state, pelvis, head, leftFoot, rightFoot) : 0f;
            if (office84) correction.Z = 0.01f - office84MinimumGap; // PREP only: measured common body targets ten millimetres above own plane.
            if ((state.Spec.Recurso == "sono_banco_tracy_f" && IsSleepBenchAxisScope(state)) || office84)
            {
                // PREP only, measured rigid common-body origin. Never change heading/phase/bones.
                // Pelvis centered173 left the actual asymmetric footprint across the backrest.
                GTA.Prop bench = props.GetProp(state.Spec.Objeto);
                GTA.Math.Vector3 minimum = new GTA.Math.Vector3(float.MaxValue, float.MaxValue, 0f);
                GTA.Math.Vector3 maximum = new GTA.Math.Vector3(float.MinValue, float.MinValue, 0f);
                foreach (GTA.Math.Vector3 actual in new GTA.Math.Vector3[] { pelvis, head, leftFoot, rightFoot, Bone(state, GTA.Bone.SkelSpine2) })
                {
                    GTA.Math.Vector3 local = bench.GetPositionOffset(actual); Vector(local, 10f);
                    minimum.X = System.Math.Min(minimum.X, local.X); minimum.Y = System.Math.Min(minimum.Y, local.Y);
                    maximum.X = System.Math.Max(maximum.X, local.X); maximum.Y = System.Math.Max(maximum.Y, local.Y);
                }
                GTA.Math.Vector3 center = bench.GetOffsetPosition(new GTA.Math.Vector3((minimum.X+maximum.X)*0.5f, (minimum.Y+maximum.Y)*0.5f, 0f));
                Vector(center, 10000f);
                correction = new GTA.Math.Vector3(state.SurfacePoint.X-center.X, state.SurfacePoint.Y-center.Y, correction.Z);
                Log("POSES_C1_ENVELOPE_CENTRO_PREP", "BodyMinLocal=" + minimum + "; BodyMaxLocal=" + maximum +
                    "; ActualEnvelopeCenter=" + center + "; OwnSurface=" + state.SurfacePoint + "; Correction=" + correction +
                    "; BodyXYMargin=0; HeadingChanged=False; PhaseChanged=False; BodySource=5NativeBones; PoseActionSetter=False");
            }
            if (correction.Length() > 3f || (state.Ped.Position + correction - state.OriginalPosition).Length() > 3f)
                throw Error("apoio/pose longe da marca original; nenhuma correção arbitrária será aplicada");
            if (correction.Length() > 0.05f || (office84 && (!state.Office84PreparationGapSet || office84MinimumGap < 0f || office84MinimumGap > 0.05f)) || (seated &&
                (pelvis.Z < state.SurfacePoint.Z || pelvis.Z > state.SurfacePoint.Z + SeatedContactMaximum(state))))
            {
                if (state.Corrections >= 4) throw Error("pose não alinhou ao contato medido em4correções");
                state.Ped.PositionNoOffset = state.Ped.Position + correction; state.Ped.Velocity = GTA.Math.Vector3.Zero;
                if (office84)
                {
                    state.Office84PreparationGapSet = true;
                    Log("SONO84_PLANE_CLEARANCE_PREP", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "ObservedMinimumSixBoneGap={0:R}; TargetMinimumGap=0.01; AppliedCommonBodyZ={1:R}; OwnNormal={2}; ObservationGuard=0..0.05; BodyXYMargin=0; PhaseSetter=False; PreparationOnly=True; ActionSetter=False; ProductionReady=False",
                        office84MinimumGap, correction.Z, state.Office84PlaneNormal));
                }
                state.Corrections++; all = false; continue;
            }
            if (seated && !state.ClearanceMeasured)
            {
                float lift;
                CheckGeometry(state, pelvis, head, leftFoot, rightFoot, true, out lift);
                float clearance = pelvis.Z - state.SurfacePoint.Z + lift;
                float contactMaximum = SeatedContactMaximum(state);
                if (!Finite(lift) || !Finite(clearance) || lift > contactMaximum || clearance < 0f || clearance > contactMaximum)
                {
                    Log("POSES_ELEVACAO_CONTATO_FAIL", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "Ator={0}; Recurso={1}; Tipo={2}; Lift={3:R}; Clearance={4:R}; SurfaceZ={5:R}; PelvisZ={6:R}; HeadZ={7:R}; FootLeftZ={8:R}; FootRightZ={9:R}; Surface={10}; Pelvis={11}; Head={12}; FootLeft={13}; FootRight={14}; Original={15}; Correcoes={16}; LimiteLift={17:R}; ContatoZ=0..{17:R}; ChaoPicnic={18}; SomentePreparo=true",
                        state.Spec.Ator, state.Spec.Recurso, state.Spec.Tipo, lift, clearance, state.SurfacePoint.Z,
                        pelvis.Z, head.Z, leftFoot.Z, rightFoot.Z, state.SurfacePoint, pelvis, head, leftFoot, rightFoot,
                        state.OriginalPosition, state.Corrections, contactMaximum, IsFloorContact(state)));
                    Log("POSES_CONTEXTO_CONTATO_FAIL", "Ator=" + state.Spec.Ator + "; Tipo=" + state.Spec.Tipo +
                        "; LimiteReal=" + contactMaximum.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; ChaoPicnic=" + IsFloorContact(state));
                    throw Error(IsFloorContact(state) ? "elevação excede os spans reais do corpo sentado no piso; escolher outro recurso real" :
                        "elevação medida dos pés excede contato de0–10cm com o assento; escolher outro recurso real");
                }
                state.MeasuredClearance = clearance; state.ClearanceMeasured = true;
                if (IsFloorContact(state))
                    Log("POSES_CHAO_CONTATO_MEDIDO", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "Ator={0}; Clearance={1:R}; LimiteReal={2:R}; SpanCabecaPelve={3:R}; SpanPelvePeInferior={4:R}; ToleranciaPe=0.08; SurfaceZ={5:R}; SomentePreparo=true",
                        state.Spec.Ator, clearance, state.FloorContactMaximum, head.Z - pelvis.Z,
                        pelvis.Z - System.Math.Min(leftFoot.Z, rightFoot.Z), state.SurfacePoint.Z));
                Log("POSES_PES_ELEVACAO_MEDIDA", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Ator={0}; Recurso={1}; Lift={2:R}; Clearance={3:R}; SurfaceZ={4:R}; PelvisZ={5:R}; FootLeftZ={6:R}; FootRightZ={7:R}; SomentePreparo=true",
                    state.Spec.Ator, state.Spec.Recurso, lift, clearance, state.SurfacePoint.Z, pelvis.Z, leftFoot.Z, rightFoot.Z));
                if (lift > 0f)
                {
                    correction = new GTA.Math.Vector3(0f, 0f, lift);
                    if (correction.Length() > 3f || (state.Ped.Position + correction - state.OriginalPosition).Length() > 3f)
                        throw Error("apoio/pose longe da marca original; nenhuma correção arbitrária será aplicada");
                    if (state.Corrections >= 4) throw Error("pose não alinhou ao contato medido em4correções");
                    state.Ped.PositionNoOffset = state.Ped.Position + correction; state.Ped.Velocity = GTA.Math.Vector3.Zero;
                    state.Corrections++; all = false; continue;
                }
            }
            else CheckGeometry(state, pelvis, head, leftFoot, rightFoot);
            if (state.Spec.Recurso == "sono_banco_tracy_f" && IsSleepBenchAxisScope(state) &&
                props.PrepareSleepBagRigidGrip(state.Spec.ObjetoAbracado))
            { all = false; continue; } // Actual next-Tick contact still required before Ready.
            CheckEmbrace(state);
            if (state.Transition != null && state.Transition.Tipo == "sentar" && !state.EntryStartMeasured)
            { BeginEntryProbe(state); all = false; continue; }
            state.Ready = true;
            Log("POSE_SERIE_ALINHADA", "Ator=" + state.Spec.Ator + "; Recurso=" + state.Spec.Recurso + "; Correcoes=" + state.Corrections + "; FonteReal=true; TesteVisual=pendente");
        }
        ready = all;
    }

    private void CheckGeometry(PoseState state, GTA.Math.Vector3 pelvis, GTA.Math.Vector3 head, GTA.Math.Vector3 leftFoot, GTA.Math.Vector3 rightFoot)
    {
        float ignored;
        CheckGeometry(state, pelvis, head, leftFoot, rightFoot, false, out ignored);
    }

    // O clipe de sono não comprova abraço: medir as mãos contra o volume real do prop.
    private void CheckEmbrace(PoseState state)
    {
        if (state.Spec.ObjetoAbracado == null) return;
        GTA.Prop embraced = props.GetProp(state.Spec.ObjetoAbracado);
        if (!embraced.IsVisible || !embraced.IsAttachedTo(state.Ped))
            throw Error("a sacola perdeu o attachment/visibilidade no corpo próprio");
        GTA.Math.Vector3 min, max;
        props.GetMeasuredBounds(state.Spec.ObjetoAbracado, out min, out max);
        GTA.Math.Vector3 left = Bone(state, GTA.Bone.SkelLeftHand), rightHand = Bone(state, GTA.Bone.SkelRightHand);
        GTA.Math.Vector3 torso = Bone(state, GTA.Bone.SkelSpine3);
        float leftDistance = BoundsDistance(embraced.GetPositionOffset(left), min, max);
        float rightDistance = BoundsDistance(embraced.GetPositionOffset(rightHand), min, max);
        float torsoDistance = BoundsDistance(embraced.GetPositionOffset(torso), min, max);
        bool valid = leftDistance <= 0.35f && rightDistance <= 0.35f && torsoDistance <= 0.35f;
        if (!state.EmbraceLogged || !valid || unchecked((uint)(GTA.Game.GameTime - state.EmbraceLogAt)) >= 1000u)
        {
            state.EmbraceLogged = true; state.EmbraceLogAt = GTA.Game.GameTime;
            Log(valid ? "POSES_ABRACO_OBJETO_MEDIDO" : "POSES_ABRACO_OBJETO_FAIL",
                System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Ator={0}; Objeto={1}; MaoEsquerda={2}; MaoDireita={3}; Tronco={4}; PropPos={5}; BoundsMin={6}; BoundsMax={7}; DistEsquerda={8:R}; DistDireita={9:R}; DistTronco={10:R}; LimiteContato=0.35; Valido={11}; TesteVisual=pendente",
                state.Spec.Ator, state.Spec.ObjetoAbracado, left, rightHand, torso, embraced.Position, min, max, leftDistance, rightDistance, torsoDistance, valid));
        }
        if (!valid) throw Error("o sono não mantém as duas mãos e o tronco perto do volume real da sacola; escolher encaixe/clipe real");
    }
    private static float BoundsDistance(GTA.Math.Vector3 point, GTA.Math.Vector3 min, GTA.Math.Vector3 max)
    {
        Vector(point, 30000f); Vector(min, 1000f); Vector(max, 1000f);
        if (min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z) throw Error("volume do objeto abraçado inválido");
        float x = System.Math.Max(System.Math.Max(min.X - point.X, point.X - max.X), 0f);
        float y = System.Math.Max(System.Math.Max(min.Y - point.Y, point.Y - max.Y), 0f);
        float z = System.Math.Max(System.Math.Max(min.Z - point.Z, point.Z - max.Z), 0f);
        return (float)System.Math.Sqrt(x * x + y * y + z * z);
    }
    private void CheckGeometry(PoseState state, GTA.Math.Vector3 pelvis, GTA.Math.Vector3 head, GTA.Math.Vector3 leftFoot, GTA.Math.Vector3 rightFoot,
                               bool measureClearance, out float measuredLift)
    {
        measuredLift = 0f;
        bool curbRigid = measureClearance && curbC3RigidPreparation && poses.Count == 1 && !ready && lastElapsed < 0 &&
            state.Spec.Ator == "DJANIRA_POBRE" && state.Spec.Tipo == "chao" && state.Spec.Recurso == "sentada_meio_fio_f";
        float rigidMinimum = System.Math.Max(0f, state.SurfacePoint.Z - pelvis.Z);
        float rigidMaximum = state.SurfacePoint.Z + 0.1f - pelvis.Z;
        float strictGroundLift = 0f;
        float originalMinimumFootGap = float.MaxValue;
        if (state.Spec.Tipo == "sentada" || state.Spec.Tipo == "chao")
        {
            GTA.Math.Vector3 contact = pelvis - state.SurfacePoint;
            if (Horizontal(contact) > 0.05f || contact.Z < 0f || contact.Z > SeatedContactMaximum(state))
                throw Error(IsFloorContact(state) ? "pelve fora do contato medido no piso e dos spans reais do corpo" :
                    "pelve fora do contato medido: XY até5cm e Z de0–10cm sobre o assento");
            if (head.Z - pelvis.Z < 0.3f || !BentKnee(state, true) || !BentKnee(state, false)) throw Error("geometria real não confirma pose sentada; não apresentar ator em pé como sentado");
            if (measureClearance && IsFloorContact(state))
            {
                // A pelve do clipe picnic fica acima dos tornozelos; o piso nao e um assento na pelve.
                // Limite anatomico medido, com a mesma tolerancia de contato de8cm dos pes.
                float torsoSpan = head.Z - pelvis.Z, ankleSpan = pelvis.Z - System.Math.Min(leftFoot.Z, rightFoot.Z);
                float floorMaximum = System.Math.Min(torsoSpan, ankleSpan + 0.08f);
                if (!Finite(torsoSpan) || !Finite(ankleSpan) || ankleSpan < 0f || !Finite(floorMaximum) || floorMaximum <= 0f)
                    throw Error("spans reais invalidos para contato da pose picnic no piso");
                state.FloorContactMaximum = floorMaximum;
            }
            foreach (GTA.Math.Vector3 foot in new GTA.Math.Vector3[] { leftFoot, rightFoot })
            {
                float ground;
                GTA.Math.Vector3 groundQuery = foot + new GTA.Math.Vector3(0f, 0f, 0.5f);
                bool groundFound = GTA.World.GetGroundHeightExcludingProps(groundQuery, out ground, GTA.GetGroundHeightMode.Normal);
                if (measureClearance)
                    Log("POSES_PES_LIFT_MEDICAO", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "Ator={0}; Recurso={1}; Tipo={2}; Query=({3:R},{4:R},{5:R}); Found={6}; Ground={7:R}; Foot=({8:R},{9:R},{10:R}); Diff={11:R}; LiftCandidato={12:R}; PelvisZ={13:R}; HeadZ={14:R}; SurfaceZ={15:R}; Correcoes={16}; Getter=GetGroundHeightExcludingProps.Normal; SomentePreparo=true",
                        state.Spec.Ator, state.Spec.Recurso, state.Spec.Tipo, groundQuery.X, groundQuery.Y, groundQuery.Z,
                        groundFound, ground, foot.X, foot.Y, foot.Z, foot.Z - ground, ground - foot.Z, pelvis.Z, head.Z,
                        state.SurfacePoint.Z, state.Corrections));
                if (state.Spec.Tipo == "chao" && state.Spec.Recurso == "sentada_meio_fio_f" && groundFound && Finite(ground))
                {
                    float stepHeight = state.SurfacePoint.Z - ground;
                    if (stepHeight < 0.08f || stepHeight > 0.50f || foot.Z > ground + 0.35f)
                        throw Error("meio-fio exige pés em piso real8–50cm abaixo da superfície e até35cm acima do chão; nenhuma bancada substitui o degrau");
                }
                if (!groundFound || !Finite(ground) || (!measureClearance && foot.Z < ground - 0.08f) ||
                    (IsFloorContact(state) && (System.Math.Abs(ground - state.SurfacePoint.Z) > 0.08f || foot.Z > ground + 0.08f)))
                {
                    Log("POSES_PES_GROUND_FAIL", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "Ator={0}; Recurso={1}; Apoio={2}; Query=({3:R},{4:R},{5:R}); Found={6}; Ground={7:R}; Foot=({8:R},{9:R},{10:R}); Diff={11:R}; Pelvis=({12:R},{13:R},{14:R}); Head=({15:R},{16:R},{17:R}); Surface=({18:R},{19:R},{20:R}); Original=({21:R},{22:R},{23:R}); Correcoes={24}",
                        state.Spec.Ator, state.Spec.Recurso, state.Spec.Objeto, groundQuery.X, groundQuery.Y, groundQuery.Z,
                        groundFound, ground, foot.X, foot.Y, foot.Z, foot.Z - ground, pelvis.X, pelvis.Y, pelvis.Z,
                        head.X, head.Y, head.Z, state.SurfacePoint.X, state.SurfacePoint.Y, state.SurfacePoint.Z,
                        state.OriginalPosition.X, state.OriginalPosition.Y, state.OriginalPosition.Z, state.Corrections));
                    throw Error("pés da pose não têm chão real ou atravessam o piso");
                }
                if (measureClearance) measuredLift = System.Math.Max(measuredLift, ground - foot.Z);
                if (curbRigid)
                {
                    // Same existing final guards: foot >= ground-.08, foot <= ground+.35,
                    // pelvis >= surface and <= surface+.1. Only one rigid PREP Z is selected.
                    rigidMinimum = System.Math.Max(rigidMinimum, ground - 0.08f - foot.Z);
                    rigidMaximum = System.Math.Min(rigidMaximum, ground + 0.35f - foot.Z);
                    strictGroundLift = System.Math.Max(strictGroundLift, ground - foot.Z);
                    originalMinimumFootGap = System.Math.Min(originalMinimumFootGap, foot.Z - ground);
                    float inclusiveGround; GTA.Math.Vector3 inclusiveNormal;
                    bool inclusiveFound = GTA.World.GetGroundHeightAndNormal(groundQuery, out inclusiveGround, out inclusiveNormal);
                    Log("POSES_C3_FOOT_GROUND_COMPARE", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "Query={0}; Foot={1}; ExcludingPropsGround={2:R}; IncludingStaticPropsFound={3}; IncludingStaticPropsGround={4:R}; IncludingStaticPropsNormal={5}; OwnProps=0; BuildingModelRead=False; DiagnosticOnly=True; PreparationOnly=True",
                        groundQuery, foot, ground, inclusiveFound, inclusiveGround, inclusiveNormal));
                }
            }
            if (curbRigid)
            {
                if (!Finite(rigidMinimum) || !Finite(rigidMaximum) || rigidMinimum > rigidMaximum || rigidMinimum < 0f)
                    throw Error("C3 physical foot/pelvis rigid placement intersection empty; no contact guard relaxed.");
                // Prefer no negative foot bone. When that is infeasible, choose the
                // closest permitted lift, one millimetre inside the upper boundary.
                // This is a numerical placement target, never a larger guard tolerance.
                float numericalInterior = System.Math.Min(0.001f, (rigidMaximum - rigidMinimum) * 0.5f);
                measuredLift = strictGroundLift <= rigidMaximum ? System.Math.Max(strictGroundLift, rigidMinimum) :
                    rigidMaximum - numericalInterior;
                Log("POSES_C3_RIGID_INTERVAL_PREP", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "DeltaMinimum={0:R}; DeltaMaximum={1:R}; StrictNonnegativeFootLift={2:R}; SelectedCommonLift={3:R}; MinimumFootBoneGapAfter={4:R}; PelvisGapAfter={5:R}; ExistingFootMinimum=-0.08; ExistingFootMaximum=0.35; ExistingPelvisContact=0..0.1; FullNonnegativeFootBonesPossible={6}; NumericalInterior={7:R}; Strategy=NearestStrictLiftWithinExistingInterval; ShoeSoleContactProven=False; VisualApproval=False; PreparationOnly=True; ActionSetter=False",
                    rigidMinimum, rigidMaximum, strictGroundLift, measuredLift,
                    originalMinimumFootGap + measuredLift, pelvis.Z + measuredLift - state.SurfacePoint.Z,
                    strictGroundLift <= rigidMaximum, numericalInterior));
            }
        }
        else
        {
            float lowest = System.Math.Min(System.Math.Min(pelvis.Z, head.Z), System.Math.Min(leftFoot.Z, rightFoot.Z));
            float highest = System.Math.Max(System.Math.Max(pelvis.Z, head.Z), System.Math.Max(leftFoot.Z, rightFoot.Z));
            if (highest - lowest > 0.45f || Horizontal(head - pelvis) < 0.3f)
            {
                LogSleepBenchAxis(state, "EIXO_REJEITADO", pelvis, head, leftFoot, rightFoot);
                throw Error("eixo real não confirma corpo deitado no apoio");
            }
            GTA.Prop prop = props.GetProp(state.Spec.Objeto); GTA.Math.Vector3 min, max;
            props.GetMeasuredBounds(state.Spec.Objeto, out min, out max);
            float seatX, seatY;
            if (props.TryGetSleepBenchSurfaceXY(state.Spec.Objeto, out seatX, out seatY))
                props.RequireSleepBenchBodyProjection(state.Spec.Objeto, new GTA.Math.Vector3[] { pelvis, head, leftFoot, rightFoot, Bone(state, GTA.Bone.SkelSpine2) });
            if (IsOfficeCellBench84Scope(state))
            {
                float minimumGap = OfficeCellBench84MinimumPlaneGap(state, pelvis, head, leftFoot, rightFoot);
                if (!state.Ready || minimumGap < 0f || minimumGap > 0.05f)
                    Log("SONO84_APOIO_CORPO_DIAGNOSTICO", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "MinimumSixBonePlaneGap={0:R}; OwnSurface={1}; OwnNormal={2}; CoreProjection=5; XYMargin=0; BelowPlaneAllowed=0; MaximumLowestGap=0.05; PhaseSetter=False; SomenteCorpo=True; Abraco=False; ProductionReady=False",
                        minimumGap, state.SurfacePoint, state.Office84PlaneNormal));
                if (minimumGap < 0f || minimumGap > 0.05f)
                    throw Error("Sono84 lowest actual body bone must contact the measured own plane without penetration.");
            }
            foreach (GTA.Math.Vector3 point in new GTA.Math.Vector3[] { head, leftFoot, rightFoot })
            {
                GTA.Math.Vector3 local = prop.GetPositionOffset(point);
                if (local.X < min.X - 0.1f || local.X > max.X + 0.1f || local.Y < min.Y - 0.1f || local.Y > max.Y + 0.1f)
                    throw Error("cabeça/pés ficam fora dos limites reais da cama; alinhar outro XY/heading medido");
            }
        }
    }

    // Mede o deslocamento do clipe real uma vez no preparo. O início é corrigido antes da contagem;
    // durante a filmagem só tarefas de animação movem o corpo até o apoio medido.
    private void BeginEntryProbe(PoseState state)
    {
        state.EntryStartHeading = state.Ped.Heading;
        state.Ped.Task.ClearAllImmediately(); state.Ped.Velocity = GTA.Math.Vector3.Zero;
        state.Ped.PositionNoOffset = state.OriginalPosition;
        state.Ped.Heading = state.EntryStartHeading; state.Ped.IsPositionFrozen = true;
        state.Ped.Task.StandStill(-1);
        // Confirmar o corpo ereto em outro frame antes do clipe; nao iniciar da base sentada.
        state.EntryProbeAt = GTA.Game.GameTime; state.EntryProbeStage = 3;
        Log("POSES_SENTAR_ERETO_AGUARDANDO", "Ator=" + state.Spec.Ator + "; Start=" + state.OriginalPosition + "; Surface=" + state.SurfacePoint + "; SomentePreparo=true");
    }
    private bool PollEntryStart(PoseState state)
    {
        if (state.EntryProbeStage == 3)
        {
            if (unchecked((uint)(GTA.Game.GameTime - state.EntryProbeAt)) < 250u) return false;
            if (!Standing(state, true)) throw Error("início original do sentar não tem corpo ereto e pés no chão real antes do clipe");
            state.EntryProbeStartPosition = state.Ped.Position; Vector(state.EntryProbeStartPosition, 30000f);
            state.Ped.IsPositionFrozen = false;
            state.Ped.Task.PlayAnimation(state.Transition.Dicionario, state.Transition.Clipe, 8f, -8f, -1, GTA.AnimationFlags.StayInEndFrame, 0f);
            state.EntryProbeAt = GTA.Game.GameTime; state.EntryProbeStage = 1;
            Log("POSES_SENTAR_MEDICAO_INICIO", "Ator=" + state.Spec.Ator + "; Start=" + state.EntryProbeStartPosition +
                "; Surface=" + state.SurfacePoint + "; CorpoEretoEChaoConfirmados=true; Flags=StayInEndFrame; SomentePreparo=true");
            return false;
        }
        if (state.EntryProbeStage == 1)
        {
            uint since = unchecked((uint)(GTA.Game.GameTime - state.EntryProbeAt));
            bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, state.Handle, state.Transition.Dicionario, state.Transition.Clipe, 3);
            float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, state.Handle, state.Transition.Dicionario, state.Transition.Clipe);
            if ((!playing || phase == -1f || !Finite(phase)) && since < 500u) return false;
            if (!playing || !Finite(phase) || phase < 0f || phase > 1f)
                throw Error("fase/playing do clipe de sentar inválidos durante medição; Phase=" + phase.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; Playing=" + playing + "; SinceStart=" + since);
            if (phase < 0.95f) return false;
            GTA.Math.Vector3 pelvis = Bone(state, GTA.Bone.SkelPelvis), head = Bone(state, GTA.Bone.SkelHead);
            GTA.Math.Vector3 left = Bone(state, GTA.Bone.SkelLeftFoot), rightFoot = Bone(state, GTA.Bone.SkelRightFoot);
            float contactZ = pelvis.Z - state.SurfacePoint.Z, deltaZ = state.SurfacePoint.Z + state.MeasuredClearance - pelvis.Z;
            bool body = head.Z - pelvis.Z >= 0.3f && BentKnee(state, true) && BentKnee(state, false);
            bool feet = true;
            foreach (GTA.Math.Vector3 foot in new GTA.Math.Vector3[] { left, rightFoot })
            {
                GTA.Math.Vector3 query = foot + new GTA.Math.Vector3(0f, 0f, 0.5f); float ground;
                bool found = GTA.World.GetGroundHeightExcludingProps(query, out ground, GTA.GetGroundHeightMode.Normal);
                bool valid = found && Finite(ground) && foot.Z >= ground - 0.08f &&
                    (!IsFloorContact(state) || (System.Math.Abs(ground - state.SurfacePoint.Z) <= 0.08f && foot.Z <= ground + 0.08f));
                feet = valid && feet;
                Log("POSES_SENTAR_FIM_PE", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Ator={0}; Phase={1:R}; Query={2}; Found={3}; Ground={4:R}; Foot={5}; Diff={6:R}; Valido={7}; SomentePreparo=true",
                    state.Spec.Ator, phase, query, found, ground, foot, foot.Z - ground, valid));
            }
            Log("POSES_SENTAR_FIM_GEOMETRIA", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Ator={0}; Phase={1:R}; Playing={2}; Start={3}; RootEnd={4}; Pelvis={5}; Head={6}; LeftFoot={7}; RightFoot={8}; Surface={9}; ContactZ={10:R}; Clearance={11:R}; DeltaZNAOAplicado={12:R}; BodySeated={13}; FeetGround={14}; LimiteContato={15:R}; SomentePreparo=true",
                state.Spec.Ator, phase, playing, state.EntryProbeStartPosition, state.Ped.Position, pelvis, head, left, rightFoot,
                state.SurfacePoint, contactZ, state.MeasuredClearance, deltaZ, body, feet, SeatedContactMaximum(state)));
            if (!body || !feet || contactZ < 0f || contactZ > SeatedContactMaximum(state))
                throw Error("fim real do sentar não cabe no apoio medido a partir de início ereto no piso; não baixar início nem teleportar durante ação");
            GTA.Math.Vector3 correction = new GTA.Math.Vector3(state.SurfacePoint.X - pelvis.X, state.SurfacePoint.Y - pelvis.Y, 0f);
            if (correction.Length() > 3f) throw Error("entrada sentada precisa de início medido a mais de3m da marca; escolher apoio/clip compatível");
            state.EntryStartPosition = state.EntryProbeStartPosition + correction;
            if ((state.EntryStartPosition - state.OriginalPosition).Length() > 3f) throw Error("início da entrada sentada ficou a mais de3m da marca original");
            Vector(state.EntryStartPosition, 30000f); state.EntryStartMeasured = true;
            state.Ped.Task.ClearAllImmediately(); state.Ped.Velocity = GTA.Math.Vector3.Zero;
            state.Ped.PositionNoOffset = state.EntryStartPosition; state.Ped.IsPositionFrozen = true;
            state.Ped.Task.StandStill(-1);
            state.EntryProbeAt = GTA.Game.GameTime; state.EntryProbeStage = 2;
            Log("POSES_SENTAR_INICIO_MEDIDO", "Ator=" + state.Spec.Ator + "; EntryStart=" + state.EntryStartPosition + "; MeasuredEndPelvis=" + pelvis + "; Correction=" + correction + "; ZInicialEretoPreservado=true; SemReposicaoDuranteAcao=true");
            return false;
        }
        if (unchecked((uint)(GTA.Game.GameTime - state.EntryProbeAt)) < 250u) return false;
        if (!Standing(state, true)) throw Error("início medido do sentar não tem corpo ereto e pés no chão real");
        state.Ready = true; state.EntryProbeStage = 0;
        return true;
    }
    private bool Standing(PoseState state, bool report = false)
    {
        GTA.Math.Vector3 pelvis = Bone(state, GTA.Bone.SkelPelvis), head = Bone(state, GTA.Bone.SkelHead);
        GTA.Math.Vector3 left = Bone(state, GTA.Bone.SkelLeftFoot), rightFoot = Bone(state, GTA.Bone.SkelRightFoot);
        bool bodyStanding = head.Z - pelvis.Z >= 0.3f && pelvis.Z - System.Math.Min(left.Z, rightFoot.Z) >= 0.45f &&
            (!BentKnee(state, true) || !BentKnee(state, false));
        bool standing = bodyStanding;
        foreach (GTA.Math.Vector3 foot in new GTA.Math.Vector3[] { left, rightFoot })
        {
            float ground; GTA.Math.Vector3 query = foot + new GTA.Math.Vector3(0f, 0f, 0.5f);
            bool found = GTA.World.GetGroundHeightExcludingProps(query, out ground, GTA.GetGroundHeightMode.Normal);
            bool valid = found && Finite(ground) && foot.Z >= ground - 0.08f && foot.Z <= ground + 0.35f;
            if (!valid) standing = false;
            if (report) Log("POSES_SENTAR_ERETO_PE", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Ator={0}; Stage={1}; Query={2}; Found={3}; Ground={4:R}; Foot={5}; Diff={6:R}; Valido={7}; SomentePreparo=true",
                state.Spec.Ator, state.EntryProbeStage, query, found, ground, foot, foot.Z - ground, valid));
        }
        if (report) Log("POSES_SENTAR_ERETO_GEOMETRIA", "Ator=" + state.Spec.Ator + "; Stage=" + state.EntryProbeStage + "; Root=" + state.Ped.Position +
            "; Pelvis=" + pelvis + "; Head=" + head + "; LeftFoot=" + left + "; RightFoot=" + rightFoot + "; BodyStanding=" + bodyStanding + "; Standing=" + standing + "; SomentePreparo=true");
        return standing;
    }
    private static bool BentKnee(PoseState state, bool left)
    {
        GTA.Math.Vector3 hip = Bone(state, left ? GTA.Bone.SkelLeftThigh : GTA.Bone.SkelRightThigh);
        GTA.Math.Vector3 knee = Bone(state, left ? GTA.Bone.SkelLeftCalf : GTA.Bone.SkelRightCalf);
        GTA.Math.Vector3 foot = Bone(state, left ? GTA.Bone.SkelLeftFoot : GTA.Bone.SkelRightFoot);
        GTA.Math.Vector3 thigh = knee - hip, calf = foot - knee;
        if (thigh.Length() < 0.05f || calf.Length() < 0.05f) throw Error("ossos de perna não fornecem segmentos reais");
        return GTA.Math.Vector3.Dot(thigh / thigh.Length(), calf / calf.Length()) < 0.86f;
    }

    public void Reset()
    {
        Active(); if (!prepared || (props != null && !props.IsReady)) throw Error("objetos precisam estar restaurados antes das poses");
        ReleaseActorsForReset(); ready = poses.Count == 0; lastElapsed = -1;
        officeCell84StartAt = officeCell84LastAt = -1; officeCell84Samples = 0; officeCell84WindowObserved = false; office84BagBoundsLogged = false;
        loading = System.Diagnostics.Stopwatch.StartNew(); RequestSurfaces();
    }
    public bool ReleaseActorsForReset()
    {
        Active();
        foreach (PoseState state in poses)
        {
            RequireOwned(state);
            GTA.Native.Function.Call(GTA.Native.Hash.STOP_ANIM_TASK, state.Handle, state.Spec.Dicionario, state.Spec.Clipe, -4f);
            state.Ped.Task.ClearAllImmediately(); state.Ped.Velocity = GTA.Math.Vector3.Zero;
            state.Ped.PositionNoOffset = state.OriginalPosition; state.Ped.Heading = state.OriginalHeading;
            state.Ped.IsPositionFrozen = state.OriginalFrozen;
            RestoreExitChair(state, true, "RESET");
            state.Ready = false; state.Started = false;
            state.MeasuredClearance = 0f; state.ClearanceMeasured = false; state.FloorContactMaximum = 0f;
            state.TransitionStarted = false; state.TransitionDone = false; state.TransitionObserved = false; state.TransitionMaxPhase = 0f;
            state.EntryBaseRequested = false; state.EntryProbeStage = 0;
            state.TransitionTelemetryLogged = false; state.TransitionTelemetryAt = 0;
            if (state.Transition != null && state.Transition.Tipo == "sentar" && state.EntryStartMeasured)
            {
                state.Ped.PositionNoOffset = state.EntryStartPosition; state.Ped.IsPositionFrozen = true;
                state.Ped.Heading = state.EntryStartHeading;
                state.EntryProbeAt = GTA.Game.GameTime; state.EntryProbeStage = 2;
            }
        }
        ready = poses.Count == 0;
        return true;
    }
    // F6-only technical body observation; no task/phase/transform setters, action or capture.
    public void PollOfficeCellBodyPreview84()
    {
        Active();
        if (!IsReady || poses.Count != 1 || props == null || !props.IsOfficeCellBodyPreview84 ||
            poses[0].Spec.Recurso != "office_cell_sleep_f" || poses[0].Spec.ObjetoAbracado != null || lastElapsed != -1)
            throw Error("Sono84 observation needs exact own prepared body only; never action or production.");
        PoseState state = poses[0]; RequireOwned(state);
        GTA.Prop bench = props.GetProp(state.Spec.Objeto);
        if (!bench.IsVisible || !bench.IsPositionFrozen || !Playing(state) ||
            GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw Error("Sono84 lost actual own support/primary clip/Story.");
        GTA.Math.Vector3 pelvis = Bone(state, GTA.Bone.SkelPelvis), head = Bone(state, GTA.Bone.SkelHead);
        GTA.Math.Vector3 left = Bone(state, GTA.Bone.SkelLeftFoot), right = Bone(state, GTA.Bone.SkelRightFoot);
        if (officeCell84StartAt < 0) officeCell84StartAt = GTA.Game.GameTime;
        uint elapsed = unchecked((uint)(GTA.Game.GameTime - officeCell84StartAt));
        if (officeCell84LastAt < 0 || unchecked((uint)(GTA.Game.GameTime - officeCell84LastAt)) >= 250u)
        {
            officeCell84LastAt = GTA.Game.GameTime; officeCell84Samples++;
            LogOfficeCell84(state, "OBSERVACAO", (int)elapsed, pelvis, head, left, right);
        }
        CheckGeometry(state, pelvis, head, left, right); // Actual lying axis, own5-bone seat and six-bone plane contact; no setters.
        props.PollOfficeBag84RigidPreview(); // Optional technical own prop only; body/bench guard already passed.
        if (!officeCell84WindowObserved && elapsed >= 10000u)
        {
            if (officeCell84Samples < 20) throw Error("Sono84 insufficient actual body samples in ten-second window.");
            officeCell84WindowObserved = true;
            Log("SONO84_CORPO_JANELA_OBSERVADA", "Scene=teste_fila_84; DurationMs=" + elapsed + "; Samples=" + officeCell84Samples +
                "; BodyObservation=True; AbracoProvado=False; ProductionReady=False; ARM=False; SAVE=False; FullClipClaim=False; ImagePending=True");
        }
    }
    private void LogOfficeCell84(PoseState state, string stage, int elapsed, GTA.Math.Vector3 pelvis, GTA.Math.Vector3 head,
        GTA.Math.Vector3 left, GTA.Math.Vector3 right)
    {
        float duration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, state.Spec.Dicionario, state.Spec.Clipe);
        float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, state.Handle, state.Spec.Dicionario, state.Spec.Clipe);
        if (!Finite(duration) || duration <= 0f || !Finite(phase) || phase < 0f || phase > 1f) throw Error("Sono84 real duration/phase invalid.");
        Log("SONO84_CORPO_DIAGNOSTICO", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
            "Stage={0}; Scene=teste_fila_84; Recurso=office_cell_sleep_f; Ator=DJANIRA_POBRE; Handle={1}; Model={2}; Address={3}; ElapsedMs={4}; NativeDurationSec={5:R}; Phase={6:R}; Playing={7}; Pelvis={8}; Head={9}; LeftFoot={10}; RightFoot={11}; LeftHand={12}; RightHand={13}; Spine3={14}; Surface={15}; HeadAbovePelvis={16:R}; BentLeft={17}; BentRight={18}; BodyObservation=True; Abraco=False; ARM=False; ProductionReady=False",
            stage, state.Handle, state.Model, state.Address, elapsed, duration, phase, Playing(state), pelvis, head, left, right,
            Bone(state, GTA.Bone.SkelLeftHand), Bone(state, GTA.Bone.SkelRightHand), Bone(state, GTA.Bone.SkelSpine3),
            state.SurfacePoint, head.Z - pelvis.Z, BentKnee(state, true), BentKnee(state, false)));
        if (stage == "ANTES_CORRECAO" && !office84BagBoundsLogged) LogOffice84CompactBagModelsReadOnly(state);
        if (stage == "OBSERVACAO" && elapsed >= 0 && elapsed <= 10000) LogOffice84BagBonesReadOnly(state, elapsed, phase);
    }

// Three installed Menyoo literals only. Model metadata getters; no Request,
// Spawn, phase/body/prop setter or readiness/production approval.
private void LogOffice84CompactBagModelsReadOnly(PoseState state)
{
    if (!IsOfficeCellBench84Scope(state) || office84BagBoundsLogged) return;
    office84BagBoundsLogged = true;
    try
    {
        GTA.PedBone torso = state.Ped.Bones[GTA.Bone.SkelSpine3];
        if (System.Object.ReferenceEquals(torso, null) || !torso.IsValid)
            throw Error("84 compact bag query needs actual own torso frame");
        GTA.Math.Vector3 l = torso.GetPositionOffset(Bone(state, GTA.Bone.SkelLeftHand));
        GTA.Math.Vector3 r = torso.GetPositionOffset(Bone(state, GTA.Bone.SkelRightHand));
        GTA.Math.Vector3 le = torso.GetPositionOffset(Bone(state, GTA.Bone.SkelLeftForearm));
        GTA.Math.Vector3 re = torso.GetPositionOffset(Bone(state, GTA.Bone.SkelRightForearm));
        string[] names = new string[] { "prop_cs_shopping_bag", "prop_ld_handbag", "prop_ld_handbag_s" };
        foreach (string name in names)
        {
            GTA.Model model = new GTA.Model(name);
            bool valid = model.IsValid, inImage = model.IsInCdImage, isProp = model.IsProp;
            GTA.Math.Vector3 min = GTA.Math.Vector3.Zero, max = GTA.Math.Vector3.Zero;
            bool boundsKnown = false;
            if (valid && inImage && isProp)
            {
                model.GetDimensions(out min, out max);
                boundsKnown = Finite(min.X) && Finite(min.Y) && Finite(min.Z) && Finite(max.X) && Finite(max.Y) && Finite(max.Z) &&
                    max.X > min.X && max.Y > min.Y && max.Z > min.Z && (max - min).Length() <= 3f;
            }
            Log("SONO84_SACOLA_MODELO_READONLY", "Scene=teste_fila_84; Model=" + name + "; Hash=" + model.Hash +
                "; ModelValid=" + valid + "; InCdImage=" + inImage + "; IsProp=" + isProp + "; IsLoaded=" + model.IsLoaded +
                "; ModelCOL=" + model.IsCollisionLoaded + "; BoundsKnown=" + boundsKnown + "; BoundsMin=" + Office84BagPoint(min) +
                "; BoundsMax=" + Office84BagPoint(max) + "; Extents=" + Office84BagPoint(max-min) +
                "; LeftHandSpine3=" + Office84BagPoint(l) + "; RightHandSpine3=" + Office84BagPoint(r) +
                "; LeftElbowSpine3=" + Office84BagPoint(le) + "; RightElbowSpine3=" + Office84BagPoint(re) +
                "; HandLateralSeparation=" + System.Math.Abs(l.Z-r.Z).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; BothPalmsBodyUpMinimum=" + System.Math.Min(l.Y,r.Y).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; PrimaryAvailability=MenyooInstalledPropList; NameDoesNotProveCompact=True; UnloadedZeroBoundsNotAccepted=True;" +
                " Queries=3Finite; RequestModel=False; NativeEntityCreated=False; ActorSetter=False; PropSetter=False; PhaseSetter=False;" +
                " SkinUnknown=True; MeshVoidUnknown=True; BagMeshNotMeasured=True; HugFalse=True; Body10AlreadyObserved=True; NoNewBodyWindowRequired=True; ProductionReady=False");
        }
    }
    catch (System.Exception ex)
    {
        Log("SONO84_SACOLA_MODELOS_INCOMPLETOS", "Scene=teste_fila_84; GetterFailure=" + ex.Message +
            "; QueriesNotRetried=True; ActorSetter=False; PropSetter=False; SkinUnknown=True; HugFalse=True; ProductionReady=False");
    }
}

    // Technical84 only. Called by the existing250ms observer in its first10s; getters only.
    private void LogOffice84BagBonesReadOnly(PoseState state, int elapsed, float phase)
    {
        if (!IsOfficeCellBench84Scope(state) || elapsed < 0 || elapsed > 10000) return;
        try
        {
            GTA.Ped ped = state.Ped;
            GTA.Prop bench = props.GetProp(state.Spec.Objeto);
            GTA.PedBone torso = ped.Bones[GTA.Bone.SkelSpine3];
            var text = new System.Text.StringBuilder();
            text.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                "Scene=teste_fila_84; Handle={0}; Address={1}; BenchHandle={2}; BenchAddress={3}; ElapsedMs={4}; Phase={5:R}; Frame={6}; SkinUnknown=True; BagMeshNotMeasured=True; HugFalse=True; ActorSetter=False; PropSetter=False; PhaseSetter=False; ProductionReady=False; ShoulderDefinition=SKEL_UpperArm; ElbowDefinition=SKEL_Forearm; BentFlagsAreKnees=True;",
                state.Handle, state.Address, bench.Handle, bench.MemoryAddress, elapsed, phase, GTA.Game.FrameCount);
            text.Append(" ActorOrigin=" + Office84BagPoint(ped.Position));
            text.Append("; ActorAxisX=" + Office84BagPoint(ped.GetOffsetPosition(new GTA.Math.Vector3(1f,0f,0f)) - ped.Position));
            text.Append("; ActorAxisY=" + Office84BagPoint(ped.GetOffsetPosition(new GTA.Math.Vector3(0f,1f,0f)) - ped.Position));
            text.Append("; ActorAxisZ=" + Office84BagPoint(ped.GetOffsetPosition(new GTA.Math.Vector3(0f,0f,1f)) - ped.Position));
            text.Append("; BenchOrigin=" + Office84BagPoint(bench.Position));
            text.Append("; BenchAxisX=" + Office84BagPoint(bench.GetOffsetPosition(new GTA.Math.Vector3(1f,0f,0f)) - bench.Position));
            text.Append("; BenchAxisY=" + Office84BagPoint(bench.GetOffsetPosition(new GTA.Math.Vector3(0f,1f,0f)) - bench.Position));
            text.Append("; BenchAxisZ=" + Office84BagPoint(bench.GetOffsetPosition(new GTA.Math.Vector3(0f,0f,1f)) - bench.Position));
            bool torsoValid = !System.Object.ReferenceEquals(torso, null) && torso.IsValid;
            text.Append("; Spine3FrameValid=" + torsoValid);
            if (torsoValid)
            {
                text.Append("; Spine3AxisX=" + Office84BagPoint(torso.GetOffsetPosition(new GTA.Math.Vector3(1f,0f,0f)) - torso.Position));
                text.Append("; Spine3AxisY=" + Office84BagPoint(torso.GetOffsetPosition(new GTA.Math.Vector3(0f,1f,0f)) - torso.Position));
                text.Append("; Spine3AxisZ=" + Office84BagPoint(torso.GetOffsetPosition(new GTA.Math.Vector3(0f,0f,1f)) - torso.Position));
            }
            GTA.Bone[] tags = new GTA.Bone[] {
                GTA.Bone.SkelHead, GTA.Bone.SkelNeck1, GTA.Bone.SkelSpine3, GTA.Bone.SkelSpine2,
                GTA.Bone.SkelSpine1, GTA.Bone.SkelSpine0, GTA.Bone.SkelSpineRoot, GTA.Bone.SkelPelvis,
                GTA.Bone.SkelLeftClavicle, GTA.Bone.SkelRightClavicle,
                GTA.Bone.SkelLeftUpperArm, GTA.Bone.SkelRightUpperArm,
                GTA.Bone.SkelLeftForearm, GTA.Bone.SkelRightForearm,
                GTA.Bone.MHLeftElbow, GTA.Bone.MHRightElbow,
                GTA.Bone.SkelLeftHand, GTA.Bone.SkelRightHand,
                GTA.Bone.SkelLeftFinger00, GTA.Bone.SkelLeftFinger01, GTA.Bone.SkelLeftFinger02,
                GTA.Bone.SkelRightFinger00, GTA.Bone.SkelRightFinger01, GTA.Bone.SkelRightFinger02,
                GTA.Bone.SkelLeftFinger10, GTA.Bone.SkelLeftFinger11, GTA.Bone.SkelLeftFinger12,
                GTA.Bone.SkelRightFinger10, GTA.Bone.SkelRightFinger11, GTA.Bone.SkelRightFinger12 };
            foreach (GTA.Bone tag in tags)
            {
                GTA.PedBone bone = ped.Bones[tag];
                bool valid = !System.Object.ReferenceEquals(bone, null) && bone.IsValid;
                string name = tag.ToString(); text.Append("; " + name + "_Tag=" + ((int)tag) + "; " + name + "_Valid=" + valid);
                if (!valid) continue; // Missing diagnostic data is reported, never fabricated or a body-guard change.
                GTA.Math.Vector3 world = bone.Position;
                text.Append("; " + name + "_World=" + Office84BagPoint(world));
                text.Append("; " + name + "_ActorLocal=" + Office84BagPoint(ped.GetPositionOffset(world)));
                text.Append("; " + name + "_BenchLocal=" + Office84BagPoint(bench.GetPositionOffset(world)));
                if (torsoValid) text.Append("; " + name + "_Spine3Local=" + Office84BagPoint(torso.GetPositionOffset(world)));
            }
            Log("SONO84_SACOLA_BONES_READONLY", text.ToString());
        }
        catch (System.Exception ex)
        {
            Log("SONO84_SACOLA_BONES_INCOMPLETOS", "Scene=teste_fila_84; ElapsedMs=" + elapsed + "; SkinUnknown=True; BagMeshNotMeasured=True; HugFalse=True; GetterFailure=" + ex.Message);
        }
    }
    private static string Office84BagPoint(GTA.Math.Vector3 p)
    {
        return System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:R},{1:R},{2:R})", p.X, p.Y, p.Z);
    }

    public void Update(int elapsedMs)
    {
        Active(); if (!IsReady || elapsedMs < 0 || elapsedMs > 180000 || elapsedMs < lastElapsed) throw Error("pose inicial exige preparo e relógio crescente0–180s");
        foreach (PoseState state in poses)
        {
            RequireOwned(state);
            if (state.ExitChairPending) RestoreExitChair(state, false, "AFASTAMENTO");
            if (state.TransitionDone && state.Transition.Tipo == "levantar") continue;
            if (state.Transition != null && state.Transition.Tipo == "sentar" && !state.TransitionDone)
            {
                if (elapsedMs >= state.Transition.InicioMs) UpdateSitTransition(state, elapsedMs);
                else if (!Standing(state)) throw Error("ator deixou o início ereto antes do sentar real");
                continue;
            }
            if (state.Transition != null && !state.TransitionDone && elapsedMs >= state.Transition.InicioMs)
            {
                UpdateTransition(state, elapsedMs);
                continue;
            }
            if (state.Spec.Tipo != "chao")
            {
                GTA.Prop prop = props.GetProp(state.Spec.Objeto);
                if (!prop.IsVisible || !prop.IsPositionFrozen) throw Error("apoio da pose mudou durante a ação; reinicie, não teleportar para corrigir");
            }
            if (!Playing(state)) throw Error("pose primária mudou durante a ação; reinicie, não teleportar para corrigir");
            if (state.Spec.Recurso == "sono_banco_tracy_f" && (!state.EmbraceLogged ||
                unchecked((uint)(GTA.Game.GameTime - state.EmbraceLogAt)) >= 1000u))
                LogSleepBenchAxis(state, "ACAO", Bone(state, GTA.Bone.SkelPelvis), Bone(state, GTA.Bone.SkelHead),
                    Bone(state, GTA.Bone.SkelLeftFoot), Bone(state, GTA.Bone.SkelRightFoot));
            // Gesto secundário não pode transformar a pose inicial em corpo em pé.
            CheckGeometry(state, Bone(state, GTA.Bone.SkelPelvis), Bone(state, GTA.Bone.SkelHead),
                Bone(state, GTA.Bone.SkelLeftFoot), Bone(state, GTA.Bone.SkelRightFoot));
            CheckEmbrace(state);
        }
        lastElapsed = elapsedMs; // Não altera posição/tarefa primária nem gesto/facial secundários na filmagem.
    }
    private void UpdateSitTransition(PoseState state, int elapsedMs)
    {
        TransitionSpec spec = state.Transition; int local = elapsedMs - spec.InicioMs;
        if (!state.TransitionStarted)
        {
            if (local >= spec.DuracaoMs || !state.EntryStartMeasured || !Standing(state)) throw Error("sentar precisa iniciar ereto na marca medida dentro da janela");
            state.TransitionHealth = state.Ped.Health; state.Ped.IsPositionFrozen = false;
            state.Ped.Task.PlayAnimation(spec.Dicionario, spec.Clipe, 8f, -8f, -1, GTA.AnimationFlags.StayInEndFrame, 0f);
            state.TransitionStarted = true; state.TransitionStartedAt = GTA.Game.GameTime;
            Log("POSES_SENTAR_INICIO", "Ator=" + spec.Ator + "; Recurso=" + spec.Recurso + "; TempoMs=" + elapsedMs + "; Start=" + state.Ped.Position + "; NativeDurationMs=" + state.TransitionDurationMs);
            return;
        }
        if (state.Ped.Health != state.TransitionHealth || state.Ped.IsRagdoll || state.Ped.IsInAir || state.Ped.IsInWater)
            throw Error("entrada sentada perdeu saúde/apoio físico");
        if (!state.EntryBaseRequested)
        {
            bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, state.Handle, spec.Dicionario, spec.Clipe, 3);
            if (playing)
            {
                float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, state.Handle, spec.Dicionario, spec.Clipe);
                if (phase == -1f && unchecked((uint)(GTA.Game.GameTime - state.TransitionStartedAt)) < 500u) return;
                if (!Finite(phase) || phase < 0f || phase > 1f) throw Error("fase real da entrada sentada inválida");
                state.TransitionMaxPhase = System.Math.Max(state.TransitionMaxPhase, phase);
                if (phase >= 0.15f) state.TransitionObserved = true;
            }
            if (local < state.TransitionDurationMs || !state.TransitionObserved || state.TransitionMaxPhase < 0.95f)
            { if (local >= spec.DuracaoMs) throw Error("entrada sentada não mostrou clipe real completo dentro da janela"); return; }
            state.Ped.Task.PlayAnimation(state.Spec.Dicionario, state.Spec.Clipe, 8f, -8f, -1, GTA.AnimationFlags.Loop, 0f);
            state.EntryBaseRequested = true; state.TransitionHealth = state.Ped.Health;
            return;
        }
        if (!Playing(state)) { if (local >= spec.DuracaoMs) throw Error("base sentada não iniciou após entrada real"); return; }
        CheckGeometry(state, Bone(state, GTA.Bone.SkelPelvis), Bone(state, GTA.Bone.SkelHead), Bone(state, GTA.Bone.SkelLeftFoot), Bone(state, GTA.Bone.SkelRightFoot));
        state.Ped.IsPositionFrozen = true; state.TransitionDone = true;
        Log("POSES_SENTAR_CONCLUIDO", "Ator=" + spec.Ator + "; TempoMs=" + elapsedMs + "; PhaseMax=" + state.TransitionMaxPhase.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; Health=" + state.Ped.Health + "; Position=" + state.Ped.Position + "; Surface=" + state.SurfacePoint + "; SemReposicao=true");
    }
    private void UpdateTransition(PoseState state, int elapsedMs)
    {
        TransitionSpec spec = state.Transition;
        int local = elapsedMs - spec.InicioMs;
        if (!state.TransitionStarted)
        {
            if (local >= spec.DuracaoMs || !Playing(state)) throw Error("saída da pose não iniciou dentro da janela própria");
            state.TransitionHealth = state.Ped.Health;
            ObserveExitGeometry(state, elapsedMs, "ANTES_TASK", false, float.NaN, true);
            SuspendExitChair(state);
            state.Ped.IsPositionFrozen = false;
            state.Ped.Task.PlayAnimation(spec.Dicionario, spec.Clipe, 8f, -8f, spec.DuracaoMs, (GTA.AnimationFlags)0, 0f);
            state.TransitionStarted = true; state.TransitionStartedAt = GTA.Game.GameTime;
            Log("POSES_LEVANTAR_INICIO", "Ator=" + spec.Ator + "; Recurso=" + spec.Recurso + "; TempoMs=" + elapsedMs + "; NativeDurationMs=" + state.TransitionDurationMs + "; Flags=0; Position=" + state.Ped.Position);
            return;
        }
        bool report = !state.TransitionTelemetryLogged || unchecked((uint)(GTA.Game.GameTime - state.TransitionTelemetryAt)) >= 250u || local >= spec.DuracaoMs;
        if (state.Ped.Health != state.TransitionHealth || state.Ped.IsRagdoll || state.Ped.IsInAir || state.Ped.IsInWater)
        {
            ObserveExitGeometry(state, elapsedMs, "SAUDE_APOIO_FAIL", false, float.NaN, true);
            throw Error("saída da pose perdeu saúde/apoio físico");
        }
        bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, state.Handle, spec.Dicionario, spec.Clipe, 3);
        float phase = float.NaN;
        if (playing)
        {
            phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, state.Handle, spec.Dicionario, spec.Clipe);
            if (phase == -1f && unchecked((uint)(GTA.Game.GameTime - state.TransitionStartedAt)) < 500u)
            { if (report) ObserveExitGeometry(state, elapsedMs, "FASE_PENDENTE", playing, phase, true); return; }
            if (!Finite(phase) || phase < 0f || phase > 1f) throw Error("fase real da saída inválida");
            state.TransitionMaxPhase = System.Math.Max(state.TransitionMaxPhase, phase);
            if (phase >= 0.15f) state.TransitionObserved = true;
        }
        if (local < state.TransitionDurationMs)
        { if (report) ObserveExitGeometry(state, elapsedMs, "CLIPE_EM_CURSO", playing, phase, true); return; }
        if (!state.TransitionObserved || state.TransitionMaxPhase < 0.90f)
        {
            if (report) ObserveExitGeometry(state, elapsedMs, "PROGRESSO_PENDENTE", playing, phase, true);
            if (local >= spec.DuracaoMs) throw Error("saída não mostrou progresso completo do clipe real"); return;
        }
        bool standing = ObserveExitGeometry(state, elapsedMs, "VALIDAR_ERETO", playing, phase, report);
        if (!standing) { if (local >= spec.DuracaoMs) throw Error("saída terminou sem corpo ereto e pés no chão real"); return; }
        state.TransitionDone = true;
        Log("POSES_LEVANTAR_CONCLUIDO", "Ator=" + spec.Ator + "; TempoMs=" + elapsedMs + "; PhaseMax=" + state.TransitionMaxPhase.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; Health=" + state.Ped.Health + "; Position=" + state.Ped.Position + "; SemReposicao=true");
    }
    // Somente cadeira criada/registrada por ObjetosSerie. O corpo mantem sua colisao e todos
    // os limites originais; a cadeira nao pode servir de piso durante o exit primario.
    private void SuspendExitChair(PoseState state)
    {
        if (state.Spec.Tipo != "sentada" || (state.Spec.Recurso != "sentada_cadeira_f" &&
            state.Spec.Recurso != "sentada_cadeira_livre_f" && state.Spec.Recurso != "sentado_cadeira_m")) return;
        if (state.ExitChairPending) throw Error("colisao da cadeira anterior ainda precisa ser restaurada");
        foreach (PoseState other in poses)
            if (!System.Object.ReferenceEquals(other, state) && other.Spec.Objeto == state.Spec.Objeto)
                throw Error("nao suspender cadeira compartilhada com outra pose");
        GTA.Prop chair = props.GetProp(state.Spec.Objeto);
        if (!chair.IsVisible || !chair.IsPositionFrozen || chair.IsAttached())
            throw Error("saida exige cadeira propria visivel, fixa e livre");
        props.GetMeasuredBounds(state.Spec.Objeto, out state.ExitChairMin, out state.ExitChairMax);
        state.Ped.Model.GetDimensions(out state.ExitActorMin, out state.ExitActorMax);
        Vector(state.ExitChairMin, 10f); Vector(state.ExitChairMax, 10f);
        Vector(state.ExitActorMin, 10f); Vector(state.ExitActorMax, 10f);
        if (state.ExitChairMin.X >= state.ExitChairMax.X || state.ExitChairMin.Y >= state.ExitChairMax.Y || state.ExitChairMin.Z >= state.ExitChairMax.Z ||
            state.ExitActorMin.X >= state.ExitActorMax.X || state.ExitActorMin.Y >= state.ExitActorMax.Y || state.ExitActorMin.Z >= state.ExitActorMax.Z)
            throw Error("bounds reais de cadeira/ator invalidos para restaurar colisao");
        state.ExitChairHandle = chair.Handle; state.ExitChairModel = chair.Model.Hash; state.ExitChairAddress = chair.MemoryAddress;
        state.ExitChairPosition = chair.Position; state.ExitChairRotation = chair.Rotation;
        state.ExitChairOriginalCollision = chair.IsCollisionEnabled;
        state.ExitChairPending = true; // Registro antes do setter: qualquer erro conserva a restauracao.
        chair.IsCollisionEnabled = false;
        if (chair.IsCollisionEnabled) throw Error("a propria cadeira nao confirmou colisao desligada antes da saida");
        Log("POSES_LEVANTAR_CADEIRA_COLISAO", "Ator=" + state.Spec.Ator + "; Objeto=" + state.Spec.Objeto +
            "; Stage=SUSPENSA; Handle=" + chair.Handle + "; Original=" + state.ExitChairOriginalCollision +
            "; Atual=" + chair.IsCollisionEnabled + "; Position=" + chair.Position + "; Rotation=" + chair.Rotation +
            "; ApenasCadeiraPropria=True; ColisaoAtor=" + state.Ped.IsCollisionEnabled + "; SemReposicao=true");
    }
    private void RestoreExitChair(PoseState state, bool resetting, string reason)
    {
        if (!state.ExitChairPending) return;
        GTA.Prop chair = props.GetProp(state.Spec.Objeto);
        if (chair.Handle != state.ExitChairHandle || chair.Model.Hash != state.ExitChairModel || chair.MemoryAddress != state.ExitChairAddress)
            throw Error("identidade da cadeira mudou; conservar registro de colisao");
        if (!resetting)
        {
            if (!chair.IsVisible || !chair.IsPositionFrozen || chair.IsAttached() ||
                (chair.Position - state.ExitChairPosition).Length() > 0.01f ||
                (chair.Rotation - state.ExitChairRotation).Length() > 0.1f || chair.IsCollisionEnabled)
                throw Error("cadeira suspensa mudou durante a saida; preservar registro de restauracao");
            if (!state.TransitionDone || !ExitChairSeparated(state, chair)) return;
        }
        chair.IsCollisionEnabled = state.ExitChairOriginalCollision;
        if (chair.IsCollisionEnabled != state.ExitChairOriginalCollision)
            throw Error("a cadeira propria nao confirmou restauracao exata da colisao");
        state.ExitChairPending = false;
        Log("POSES_LEVANTAR_CADEIRA_COLISAO", "Ator=" + state.Spec.Ator + "; Objeto=" + state.Spec.Objeto +
            "; Stage=RESTAURADA; Motivo=" + reason + "; Handle=" + chair.Handle + "; Original=" + state.ExitChairOriginalCollision +
            "; Atual=" + chair.IsCollisionEnabled + "; SeparacaoBBoxMedida=" + !resetting + "; SemReposicaoNaAcao=true");
    }
    private static bool ExitChairSeparated(PoseState state, GTA.Prop chair)
    {
        // Projetar os oito cantos do bbox real do ator no referencial atual da cadeira.
        // Restaurar na acao somente com separacao total; sem raio/capsula inventado.
        GTA.Math.Vector3 low = new GTA.Math.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        GTA.Math.Vector3 high = new GTA.Math.Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            GTA.Math.Vector3 corner = new GTA.Math.Vector3((i & 1) == 0 ? state.ExitActorMin.X : state.ExitActorMax.X,
                (i & 2) == 0 ? state.ExitActorMin.Y : state.ExitActorMax.Y, (i & 4) == 0 ? state.ExitActorMin.Z : state.ExitActorMax.Z);
            GTA.Math.Vector3 p = chair.GetPositionOffset(state.Ped.GetOffsetPosition(corner)); Vector(p, 100000f);
            low.X = System.Math.Min(low.X, p.X); low.Y = System.Math.Min(low.Y, p.Y); low.Z = System.Math.Min(low.Z, p.Z);
            high.X = System.Math.Max(high.X, p.X); high.Y = System.Math.Max(high.Y, p.Y); high.Z = System.Math.Max(high.Z, p.Z);
        }
        return high.X < state.ExitChairMin.X || low.X > state.ExitChairMax.X ||
            high.Y < state.ExitChairMin.Y || low.Y > state.ExitChairMax.Y ||
            high.Z < state.ExitChairMin.Z || low.Z > state.ExitChairMax.Z;
    }
    // Dois getters de chao da verificacao original. Antes do fim servem apenas de telemetria,
    // no maximo uma amostra a cada250ms; nao corrigem raiz, ossos, fase nem retorno de Found.
    private bool ObserveExitGeometry(PoseState state, int elapsedMs, string stage, bool playing, float phase, bool report)
    {
        GTA.Math.Vector3 pelvis = Bone(state, GTA.Bone.SkelPelvis), head = Bone(state, GTA.Bone.SkelHead);
        GTA.Math.Vector3 left = Bone(state, GTA.Bone.SkelLeftFoot), rightFoot = Bone(state, GTA.Bone.SkelRightFoot);
        bool leftBent = BentKnee(state, true), rightBent = BentKnee(state, false);
        bool bodyStanding = head.Z - pelvis.Z >= 0.3f && pelvis.Z - System.Math.Min(left.Z, rightFoot.Z) >= 0.45f &&
            (!leftBent || !rightBent);
        bool standing = bodyStanding;
        int index = 0;
        foreach (GTA.Math.Vector3 foot in new GTA.Math.Vector3[] { left, rightFoot })
        {
            float ground; GTA.Math.Vector3 query = foot + new GTA.Math.Vector3(0f, 0f, 0.5f);
            bool found = GTA.World.GetGroundHeightExcludingProps(query, out ground, GTA.GetGroundHeightMode.Normal);
            bool valid = found && Finite(ground) && foot.Z >= ground - 0.08f && foot.Z <= ground + 0.35f;
            if (!valid) standing = false;
            if (report) Log("POSES_LEVANTAR_PE_MEDICAO", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Ator={0}; Recurso={1}; Stage={2}; TempoMs={3}; Lado={4}; Query={5}; Found={6}; Ground={7:R}; Foot={8}; Diff={9:R}; Valido={10}; LimiteBaixo=-0.08; LimiteAlto=0.35; Getter=GetGroundHeightExcludingProps.Normal",
                state.Transition.Ator, state.Transition.Recurso, stage, elapsedMs, index++ == 0 ? "esquerdo" : "direito", query, found, ground, foot, foot.Z-ground, valid));
        }
        if (report)
        {
            state.TransitionTelemetryLogged = true; state.TransitionTelemetryAt = GTA.Game.GameTime;
            Log("POSES_LEVANTAR_CORPO_MEDICAO", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Ator={0}; Recurso={1}; Stage={2}; TempoMs={3}; LocalMs={4}; SinceTaskMs={5}; NativeDurationMs={6}; Playing={7}; Phase={8:R}; PhaseMax={9:R}; Observed={10}; Root={11}; Pelvis={12}; Head={13}; LeftFoot={14}; RightFoot={15}; Surface={16}; InitialRoot={17}; InitialHeading={18:R}; HeadPelvis={19:R}; PelvisMinFeet={20:R}; LeftBent={21}; RightBent={22}; BodyStanding={23}; Standing={24}; Health={25}; ExpectedHealth={26}; Frozen={27}; Collision={28}; Physics={29}; IsInAir={30}; IsInWater={31}; IsRagdoll={32}; Flags=0; SemReposicao=true",
                state.Transition.Ator, state.Transition.Recurso, stage, elapsedMs, elapsedMs-state.Transition.InicioMs,
                state.TransitionStarted ? unchecked((uint)(GTA.Game.GameTime-state.TransitionStartedAt)) : 0u,
                state.TransitionDurationMs, playing, phase, state.TransitionMaxPhase, state.TransitionObserved,
                state.Ped.Position, pelvis, head, left, rightFoot, state.SurfacePoint, state.OriginalPosition, state.OriginalHeading,
                head.Z-pelvis.Z, pelvis.Z-System.Math.Min(left.Z,rightFoot.Z), leftBent, rightBent, bodyStanding, standing,
                state.Ped.Health, state.TransitionHealth, state.Ped.IsPositionFrozen, state.Ped.IsCollisionEnabled,
                state.Ped.HasPhysics, state.Ped.IsInAir, state.Ped.IsInWater, state.Ped.IsRagdoll));
        }
        return standing;
    }
    public void Cleanup()
    {
        if (disposed) return;
        foreach (PoseState state in poses)
        {
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, state.Handle))
            { RestoreExitChair(state, true, "CLEANUP_ATOR_AUSENTE"); continue; }
            if (System.Object.ReferenceEquals(state.Ped, null) || state.Ped.Handle != state.Handle || state.Ped.Model.Hash != state.Model ||
                state.Ped.MemoryAddress != state.Address || state.Ped.IsPlayer) throw Error("identidade do ator mudou; preservar referência na limpeza");
            GTA.Native.Function.Call(GTA.Native.Hash.STOP_ANIM_TASK, state.Handle, state.Spec.Dicionario, state.Spec.Clipe, -4f);
            state.Ped.Task.ClearAllImmediately(); state.Ped.Velocity = GTA.Math.Vector3.Zero;
            if (!state.Ped.IsDead)
            { state.Ped.PositionNoOffset = state.OriginalPosition; state.Ped.Heading = state.OriginalHeading; state.Ped.IsPositionFrozen = state.OriginalFrozen; }
            RestoreExitChair(state, true, "CLEANUP");
        }
        foreach (string dictionary in requested) GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT, dictionary);
        requested.Clear(); poses.Clear(); props = null; prepared = false; ready = false; lastElapsed = -1; loading = null; transitionsConfigured = false; curbC3RigidPreparation = false;
    }
    public void Dispose() { Cleanup(); disposed = true; }

    public static void VerifySourceEvidence(System.Collections.Generic.IEnumerable<PoseSpec> specs)
    {
        if (specs == null) throw Error("evidências de pose ausentes");
        var codes = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var sources = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<int, string>>(System.StringComparer.OrdinalIgnoreCase);
        var hashes = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        int count = 0;
        foreach (PoseSpec spec in specs)
        {
            if (spec == null || ++count > 5 || !codes.Add(spec.Ator ?? "")) throw Error("poses admitem0–5atores únicos");
            Identifier(spec.Ator, false); ClipInfo clip = Catalog(spec.Recurso);
            if (spec.Tipo == "chao")
            {
                if (!System.String.IsNullOrEmpty(spec.Objeto) || !spec.UsarCentroMedido || spec.SuperficieRelativa != GTA.Math.Vector3.Zero)
                    throw Error("pose no chão usa somente marca/piso reais; objeto e superfície manual são proibidos");
            }
            else Identifier(spec.Objeto, true);
            if ((spec.Tipo != clip.Type && !(spec.Tipo == "chao" && spec.Recurso == "sentada_meio_fio_f")) || spec.Dicionario != clip.Dictionary || spec.Clipe != clip.Clip) throw Error("tipo/par da pose diverge do recurso real canônico");
            if (spec.OrigemLinha < 1 || spec.OrigemLinha > 500000 || !System.Text.RegularExpressions.Regex.IsMatch(spec.OrigemSha256 ?? "", "\\A[0-9A-Fa-f]{64}\\z") ||
                System.String.IsNullOrEmpty(spec.OrigemArquivo) || !System.IO.Path.IsPathRooted(spec.OrigemArquivo)) throw Error("pose exige arquivo/linha/SHA256 de origem");
            string path = System.IO.Path.GetFullPath(spec.OrigemArquivo);
            if (path.StartsWith("\\\\", System.StringComparison.Ordinal) || path.Substring(2).IndexOf(':') >= 0 ||
                !System.String.Equals(System.IO.Path.GetFileName(path), "PedAnimList.txt", System.StringComparison.OrdinalIgnoreCase) ||
                !System.String.Equals(new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(path)).Name, "menyooStuff", System.StringComparison.OrdinalIgnoreCase)) throw Error("use a PedAnimList.txt local instalada em menyooStuff");
            if (!sources.ContainsKey(path)) { sources.Add(path, new System.Collections.Generic.Dictionary<int, string>()); hashes.Add(path, spec.OrigemSha256.ToUpperInvariant()); }
            if (hashes[path] != spec.OrigemSha256.ToUpperInvariant()) throw Error("hashes conflitantes da fonte");
            string pair = spec.Dicionario + " " + spec.Clipe, previous;
            if (sources[path].TryGetValue(spec.OrigemLinha, out previous) && previous != pair) throw Error("linha de origem conflitante");
            sources[path][spec.OrigemLinha] = pair;
        }
        foreach (var source in sources)
        {
            NoLinks(source.Key);
            using (var stream = new System.IO.FileStream(source.Key, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                if (stream.Length > 64L * 1024 * 1024) throw Error("fonte de poses excede64MB");
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    if (System.BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != hashes[source.Key]) throw Error("SHA256 da origem de pose diverge");
                stream.Position = 0; int number = 0, found = 0;
                using (var reader = new System.IO.StreamReader(stream, new System.Text.UTF8Encoding(false, true), true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    { number++; string expected; if (source.Value.TryGetValue(number, out expected)) { if (line.Trim() != expected) throw Error("par de pose não consta na linha real"); found++; } }
                }
                if (found != source.Value.Count) throw Error("linha real de pose ausente");
            }
            NoLinks(source.Key);
        }
    }
    private static PoseSpec Copy(PoseSpec s)
    {
        if (s == null) throw Error("pose nula");
        return new PoseSpec { Ator = (s.Ator ?? "").ToUpperInvariant(), Objeto = System.String.IsNullOrEmpty(s.Objeto) ? null : s.Objeto.ToUpperInvariant(), Recurso = s.Recurso, Tipo = s.Tipo,
            Dicionario = s.Dicionario, Clipe = s.Clipe, OrigemArquivo = s.OrigemArquivo, OrigemSha256 = s.OrigemSha256,
            ObjetoAbracado = s.ObjetoAbracado == null ? null : s.ObjetoAbracado.ToUpperInvariant(),
            OrigemLinha = s.OrigemLinha, SuperficieRelativa = s.SuperficieRelativa, UsarCentroMedido = s.UsarCentroMedido, RotacaoRelativa = s.RotacaoRelativa };
    }
    private static ClipInfo Catalog(string code)
    { ClipInfo clip; if (code == null || !Clips.TryGetValue(code, out clip)) throw Error("pose fora dos5recursos canônicos pesquisados"); return clip; }
    private static GTA.Math.Vector3 Bone(PoseState state, GTA.Bone id)
    {
        GTA.PedBone bone = state.Ped.Bones[id];
        if (System.Object.ReferenceEquals(bone, null) || !bone.IsValid) throw Error("osso necessário para medir a pose não existe");
        GTA.Math.Vector3 point = bone.Position; Vector(point, 30000f); return point;
    }
    private static bool IsFloorContact(PoseState s) { return s.Spec.Tipo == "chao" && s.Spec.Recurso == "sentada_chao_f"; }
    // Technical84 only. Actual182 bones proved horizontal; no action, bag or production.
    private bool IsOfficeCellBench84Scope(PoseState state)
    {
        return props != null && props.IsOfficeCellBodyPreview84 && poses.Count == 1 &&
            state.Spec.Ator == "DJANIRA_POBRE" && state.Spec.Objeto == "BANCO_PRAIA" &&
            state.Spec.Recurso == "office_cell_sleep_f" && state.Spec.Tipo == "deitada" &&
            state.Spec.RotacaoRelativa == 90f && state.Spec.ObjetoAbracado == null &&
            state.Spec.Dicionario == "anim@scripted@freemode@ig6_office_cell_sleep@female@" && state.Spec.Clipe == "base_pose_02";
    }

    private float OfficeCellBench84MinimumPlaneGap(PoseState state, GTA.Math.Vector3 pelvis,
        GTA.Math.Vector3 head, GTA.Math.Vector3 left, GTA.Math.Vector3 right)
    {
        if (!IsOfficeCellBench84Scope(state) || !state.Office84PlaneMeasured)
            throw Error("Sono84 exact own measured plane required; no arbitrary support.");
        GTA.Math.Vector3 normal = state.Office84PlaneNormal; Vector(normal, 2f);
        if (normal.Z < 0.98f) throw Error("Sono84 actual own plane must remain flat.");
        float minimum = float.MaxValue;
        foreach (GTA.Math.Vector3 actual in new GTA.Math.Vector3[] { pelvis, head, left, right,
            Bone(state, GTA.Bone.SkelSpine2), Bone(state, GTA.Bone.SkelSpine3) })
        {
            GTA.Math.Vector3 delta = actual - state.SurfacePoint;
            float gap = (delta.X * normal.X + delta.Y * normal.Y + delta.Z * normal.Z) / normal.Z;
            if (!Finite(gap)) throw Error("Sono84 actual own-plane contact invalid.");
            minimum = System.Math.Min(minimum, gap);
        }
        return minimum;
    }

    // Diagnostico so do opt-in EP10_C01/bench02; nao muda espera, tarefa, transform ou guardas.
    private bool IsSleepBenchAxisScope(PoseState state)
    {
        if (props == null || state.Spec.Ator != "DJANIRA_POBRE" || state.Spec.Tipo != "deitada" ||
            !((state.Spec.Recurso == "sono_f" && state.Spec.Dicionario == "mp_sleep" && state.Spec.Clipe == "sleep_loopfemale") ||
              (state.Spec.Recurso == "sono_banco_tracy_f" && state.Spec.Dicionario == "timetable@tracy@sleep@" && state.Spec.Clipe == "base")) ||
            state.Spec.Objeto != "BANCO_PRAIA" ||
            state.Spec.ObjetoAbracado != "SACOLA_DJANIRA") return false;
        float seatX, seatY;
        return props.TryGetSleepBenchSurfaceXY(state.Spec.Objeto, out seatX, out seatY);
    }

    private void LogSleepBenchAxis(PoseState state, string stage, GTA.Math.Vector3 pelvis,
        GTA.Math.Vector3 head, GTA.Math.Vector3 leftFoot, GTA.Math.Vector3 rightFoot)
    {
        try
        {
            if (!IsSleepBenchAxisScope(state)) return;
            float lowest = System.Math.Min(System.Math.Min(pelvis.Z, head.Z), System.Math.Min(leftFoot.Z, rightFoot.Z));
            float highest = System.Math.Max(System.Math.Max(pelvis.Z, head.Z), System.Math.Max(leftFoot.Z, rightFoot.Z));
            float span = highest - lowest, horizontal = Horizontal(head - pelvis);
            float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME,
                state.Handle, state.Spec.Dicionario, state.Spec.Clipe);
            float duration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION,
                state.Spec.Dicionario, state.Spec.Clipe);
            Log("POSES_C1_EIXO_DIAGNOSTICO", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Stage={0}; Ator={1}; Apoio={2}; ObjetoAbracado={3}; Dictionary={4}; Clip={5}; Playing={6}; Phase={7:R}; DurationSeconds={8:R}; FrameGameTime={9}; StartedAt={10}; SinceTaskMs={11}; ElapsedActionMs={12}; Root={13}; Heading={14:R}; Frozen={15}; Pelvis={16}; Head={17}; LeftFoot={18}; RightFoot={19}; Surface={20}; Original={21}; Corrections={22}; LowestZ={23:R}; HighestZ={24:R}; VerticalSpan={25:R}; HorizontalHeadPelvis={26:R}; MaximumSpan=0.45; MinimumHorizontal=0.3; AxisValid={27}; Ready={28}; SetterTransform=False; WaitChanged=False; VisualApproval=False",
                stage, state.Spec.Ator, state.Spec.Objeto, state.Spec.ObjetoAbracado,
                state.Spec.Dicionario, state.Spec.Clipe, Playing(state), phase, duration,
                GTA.Game.GameTime, state.StartedAt, unchecked((uint)(GTA.Game.GameTime - state.StartedAt)),
                lastElapsed, state.Ped.Position, state.Ped.Heading, state.Ped.IsPositionFrozen,
                pelvis, head, leftFoot, rightFoot, state.SurfacePoint, state.OriginalPosition, state.Corrections,
                lowest, highest, span, horizontal, span <= 0.45f && horizontal >= 0.3f, state.Ready));
        }
        catch (System.Exception error)
        {
            Log("POSES_C1_EIXO_DIAGNOSTICO_FAIL", "Stage=" + stage + "; Error=" + error.Message +
                "; OriginalGeometryGuardPreserved=True; SetterTransform=False");
        }
    }

    private static float SeatedContactMaximum(PoseState s)
    { return IsFloorContact(s) && s.FloorContactMaximum > 0f ? s.FloorContactMaximum : 0.10f; }
    private static bool Playing(PoseState s) { return GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, s.Handle, s.Spec.Dicionario, s.Spec.Clipe, 3); }
    private static void RequirePed(GTA.Ped p)
    { if (System.Object.ReferenceEquals(p, null) || !p.Exists() || p.IsPlayer || p.IsDead || p.IsInVehicle() || !p.IsPersistent) throw Error("pose exige ped próprio vivo a pé e persistente"); }
    private static void RequireOwned(PoseState s)
    { RequirePed(s.Ped); if (s.Ped.Handle != s.Handle || s.Ped.Model.Hash != s.Model || s.Ped.MemoryAddress != s.Address) throw Error("identidade do ator de pose mudou"); }
    private static void Identifier(string s, bool upper)
    { if (!System.Text.RegularExpressions.Regex.IsMatch(s ?? "", upper ? "\\A[A-Z][A-Z0-9_]{0,47}\\z" : "\\A[A-Za-z][A-Za-z0-9_]{0,47}\\z")) throw Error("identificador de ator/apoio inválido"); }
    private static void NoLinks(string path)
    { string current = path; while (!System.String.IsNullOrEmpty(current)) { if ((System.IO.File.Exists(current) || System.IO.Directory.Exists(current)) && (System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0) throw Error("origem não pode usar link/junction"); current = System.IO.Path.GetDirectoryName(current); } }
    private static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
    private static void Vector(GTA.Math.Vector3 v, float limit) { if (!Finite(v.X) || !Finite(v.Y) || !Finite(v.Z) || System.Math.Abs(v.X) > limit || System.Math.Abs(v.Y) > limit || System.Math.Abs(v.Z) > limit) throw Error("vetor de pose fora dos limites"); }
    private static float Horizontal(GTA.Math.Vector3 p) { return (float)System.Math.Sqrt(p.X * p.X + p.Y * p.Y); }
    private void Active() { if (disposed) throw Error("helper encerrado"); }
    private static System.InvalidOperationException Error(string text) { return new System.InvalidOperationException("Poses da série: " + text); }
    private void Log(string code, string text) { if (log != null) { try { log(code, text); } catch (System.Exception) { } } }
}
