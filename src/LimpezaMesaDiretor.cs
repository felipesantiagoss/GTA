// Capacidade isolada EP13_C05. O helper nao cria/adota props nem move o ator.
// AcoesSerie executa o clipe fresco de quatro segundos; aqui a acao usa somente getters/shape tests.
public sealed class LimpezaMesaDiretor : System.IDisposable
{
    private const string Dictionary = "timetable@maid@cleaning_surface@base", Clip = "base";
    private const int WipeMs = 4000, LoadingMs = 5000;
    private sealed class FingerTrack
    {
        public GTA.Bone Bone;
        public bool Contact;
        public GTA.Math.Vector3 First;
        public float Phase;
        public int EmissionMs, Samples;
    }
    private readonly System.Action<string, string> log;
    private GTA.Ped actor;
    private GTA.Prop table;
    private ObjetosSerieDiretor props;
    private System.Func<GTA.Ped, bool> ownsActor;
    private string tableCode, tableModelName;
    private int actorHandle, actorModel, tableHandle, tableModel, health;
    private System.IntPtr actorAddress, tableAddress;
    private GTA.Math.Vector3 minimum, maximum, tablePosition, tableRotation, normal, center;
    private int[] surfaces = new int[5];
    private GTA.Math.Vector3[] surfacePoints = new GTA.Math.Vector3[5], surfaceNormals = new GTA.Math.Vector3[5];
    private int nextSurface, warmAt = -1, lastElapsed = -1, nextLog, actionAt, nextFinger;
    private System.Diagnostics.Stopwatch loading;
    private bool prepared, ready, resetting, resetDone, running, finished, failed, disposed;
    private bool geometryKnown, warmStarted, frozenBeforeWarm, wipeMeasured;
    private float nativeDurationSeconds;
    private readonly FingerTrack[] fingers = {
        new FingerTrack { Bone = GTA.Bone.SkelRightFinger12 },
        new FingerTrack { Bone = GTA.Bone.SkelLeftFinger12 }
    };
    private GTA.ShapeTestHandle fingerRay;
    private bool rayPending;
    private GTA.Math.Vector3 rayFrom, rayTo, emissionFinger;
    private float emissionPhase;
    private int emissionMs, emissionGameTime, emissionIndex;

    public LimpezaMesaDiretor(System.Action<string, string> log) { this.log = log; }
    public bool IsReady { get { return prepared && ready && !resetting && !failed && !disposed; } }
    public bool IsFinished { get { return IsReady && finished && !running; } }

    public void Prepare(string sceneId, GTA.Ped ownActor, System.Func<GTA.Ped, bool> actorIsOwned,
                        ObjetosSerieDiretor ownProps, string supportCode, int startMs, int durationMs)
    {
        if (disposed || prepared || sceneId != "EP13_C05" || startMs != 0 || durationMs != WipeMs ||
            ownProps == null || !ownProps.IsReady || supportCode != "MESA_LIMPEZA" ||
            actorIsOwned == null || !actorIsOwned(ownActor))
            throw Error("exige EP13_C05/0..4s, ator proprio e objetos prontos; nao serve para cena de bandeja");
        RequirePed(ownActor);
        actor = ownActor; ownsActor = actorIsOwned; props = ownProps; tableCode = supportCode;
        actorHandle = actor.Handle; actorModel = actor.Model.Hash; actorAddress = actor.MemoryAddress; health = actor.Health;
        table = props.GetProp(tableCode);
        bool model07 = table.Model.Hash == new GTA.Model("prop_table_07").Hash;
        bool candidateCounter = table.Model.Hash == new GTA.Model("prop_ff_counter_02").Hash;
        if (!props.IsSupport(tableCode) || (!model07 && !candidateCounter))
            throw Error("exige mesa07 ou bancada02 propria, com cinco superficies nativas antesReady");
        tableModelName = model07 ? "prop_table_07" : "prop_ff_counter_02";
        tableHandle = table.Handle; tableModel = table.Model.Hash; tableAddress = table.MemoryAddress;
        props.GetMeasuredBounds(tableCode, out minimum, out maximum);
        Vector(minimum); Vector(maximum);
        if (minimum.X >= maximum.X || minimum.Y >= maximum.Y || minimum.Z >= maximum.Z)
            throw Error("bounds reais da mesa invalidos");
        prepared = true; BeginCycle();
        Log("LIMPEZA_PREPARO", "Scene=EP13_C05; Actor=" + actorHandle + "; OwnTable=" + tableHandle +
            "; Model=" + tableModelName + "; Dictionary=" + Dictionary + "; Clip=" + Clip + "; OriginalWipeMs=4000; TesteVisual=pendente");
    }
    private void BeginCycle()
    {
        Owned();
        if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, Dictionary))
            throw Error("AcoesSerie deve carregar o par corporal antes de preparar a limpeza");
        nativeDurationSeconds = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, Dictionary, Clip);
        if (!Finite(nativeDurationSeconds) || nativeDurationSeconds <= 0f)
            throw Error("duracao native do par corporal nao e valida");
        tablePosition = table.Position; tableRotation = table.Rotation;
        surfaces = new int[5]; nextSurface = 0; geometryKnown = false;
        ready = false; running = false; finished = false; failed = false; warmAt = -1; lastElapsed = -1;
        ResetMeasurements(); loading = System.Diagnostics.Stopwatch.StartNew();
    }
    public void PollLoading()
    {
        Owned(); if (ready) return;
        if (loading.ElapsedMilliseconds > LoadingMs) throw Error("superficie/gesto/medicoes fisicas nao ficaram prontos em5s");
        RequireTransform(); RequireOutsideTable();
        if (!geometryKnown)
        {
            // Um probe ativo: GetResult/retry/start <=3 consultas neste Tick, sem Wait.
            props.PollLoading();
            if (surfaces[nextSurface] == 0)
            {
                float cx = (minimum.X + maximum.X) * 0.5f, cy = (minimum.Y + maximum.Y) * 0.5f;
                float ex = (maximum.X - minimum.X) * 0.4f, ey = (maximum.Y - minimum.Y) * 0.4f;
                // Native113 confirmou o centro da mesa07, mas as diagonais do bbox
                // atingiram somente o piso. Cinco pontos centro/cardinais exigem novos
                // hits proprios; cada contato dos dedos continua com seu proprio ray.
                float[] xs = { 0f, -ex, ex, 0f, 0f }, ys = { 0f, 0f, 0f, -ey, ey };
                surfaces[nextSurface] = props.RequestSurface(tableCode, cx + xs[nextSurface], cy + ys[nextSurface]);
                return;
            }
            GTA.Math.Vector3 point, measuredNormal;
            if (!props.TryGetMeasuredSurface(surfaces[nextSurface], out point, out measuredNormal)) return;
            Vector(point); Vector(measuredNormal);
            if (measuredNormal.Z < 0.98f) throw Error("mesa propria nao confirmou normal horizontal real");
            surfacePoints[nextSurface] = point; surfaceNormals[nextSurface] = measuredNormal; nextSurface++;
            if (nextSurface < surfaces.Length) return;
            float low = float.MaxValue, high = float.MinValue; normal = GTA.Math.Vector3.Zero;
            foreach (GTA.Math.Vector3 p in surfacePoints) { low = System.Math.Min(low, p.Z); high = System.Math.Max(high, p.Z); }
            foreach (GTA.Math.Vector3 n in surfaceNormals) normal += n;
            if (high - low > 0.08f || !Finite(normal.Length()) || normal.Length() <= 0f)
                throw Error("cinco superficies proprias nao confirmam o mesmo plano");
            normal = normal / normal.Length(); center = surfacePoints[0]; geometryKnown = true;
            Log("LIMPEZA_MESA_MEDIDA", "OwnTable=" + tableHandle + "; Center=" + center + "; Normal=" + normal +
                "; FiveOwnSurfaces=True; Pattern=CenterCardinals; Model=" + tableModelName + "; VariationZ=" + (high - low) + "; Position=" + tablePosition + "; Rotation=" + tableRotation);
            return;
        }
        if (warmAt < 0)
        {
            frozenBeforeWarm = actor.IsPositionFrozen;
            actor.IsPositionFrozen = true; // Igual a AcoesSerie para este clipe fullbody nao pareado.
            actor.Task.PlayAnimation(Dictionary, Clip, 8f, -8f, -1,
                GTA.AnimationFlags.Loop | GTA.AnimationFlags.StayInEndFrame, 0f);
            warmAt = GTA.Game.GameTime; warmStarted = true;
            Log("LIMPEZA_WARM_INICIO", "Actor=" + actor.Position + "; NativeDurationSeconds=" + nativeDurationSeconds +
                "; Dictionary=" + Dictionary + "; Clip=" + Clip + "; WindowMs=4000; NoPositionSetter=True");
            return;
        }
        int elapsed = Since(warmAt);
        PollFingerResult();
        if (wipeMeasured)
        {
            StopWarm(); ready = true;
            Log("LIMPEZA_GEOMETRIA_PRONTA", "WarmMeasured=True; OriginalActionStillFull4s=True; IsReady=True; TesteVisual=pendente");
            return;
        }
        if (elapsed >= WipeMs)
        {
            StopWarm();
            if (rayPending) return; // Somente resultado de emissao ANTES dos4s; nenhuma nova amostra tardia.
            throw Error("par corporal nao comprovou proximidade e movimento sobre a propria mesa nos4s originais");
        }
        float phase;
        if (ReadClipPhase(elapsed, out phase)) EmitFingerRay(elapsed, phase);
    }
    // Main chama ANTES do reset generico/props.Reset; Drain evita perder handle de shape test pendente.
    public bool ReleaseActorsForReset()
    {
        Owned(); StopWarm();
        if (!DrainRay()) return false;
        ready = false; resetDone = false; resetting = false; running = false; finished = false;
        return true;
    }
    public bool Reset()
    {
        Owned();
        if (resetDone && IsReady && !running && !finished) { RequireTransform(); return true; }
        if (!resetting)
        {
            if (!DrainRay()) return false;
            StopWarm(); BeginCycle(); resetting = true;
            return false;
        }
        PollLoading();
        if (!ready) return false;
        resetting = false; resetDone = true; return true;
    }
    public bool ControlsBody(string actorId, int elapsedMs)
    { return prepared && !disposed && actorId == "dinjer" && elapsedMs >= 0 && elapsedMs < WipeMs; }
    public void RequireReadyForCountdown()
    {
        Owned(); RequireTransform(); RequireOutsideTable();
        if (!IsReady || !resetDone || running || finished || rayPending)
            throw Error("aguarde reset fisico/superficies novas antes de iniciar");
    }
    public void Start()
    {
        RequireReadyForCountdown();
        ResetMeasurements(); running = true; actionAt = GTA.Game.GameTime; lastElapsed = -1;
        Log("LIMPEZA_ACAO_INICIO", "ActionMs=0..4000; GettersOnly=True; FreshProofRequired=True");
    }
    public void Update(int elapsedMs)
    {
        Owned(); if (finished) return;
        if (!running || elapsedMs < 0 || elapsedMs < lastElapsed) throw Error("relogio de limpeza precisa avancar depoisStart");
        lastElapsed = elapsedMs; RequireTransform();
        PollFingerResult();
        if (elapsedMs >= WipeMs)
        {
            if (rayPending) return;
            if (!wipeMeasured) { failed = true; throw Error("acao4s terminou sem novo movimento medido dos dedos sobre a propria mesa"); }
            running = false; finished = true;
            Log("LIMPEZA_ACAO_MEDIDA", "FreshFingerRayMovement=True; Original4s=True; TesteVisual=pendente");
            return;
        }
        RequireOutsideTable();
        float phase;
        if (ReadClipPhase(Since(actionAt), out phase)) EmitFingerRay(elapsedMs, phase);
    }
    private bool ReadClipPhase(int elapsed, out float phase)
    {
        bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, actorHandle, Dictionary, Clip, 3);
        phase = playing ? GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, actorHandle, Dictionary, Clip) : -1f;
        if (!playing || !Finite(phase) || phase < 0f || phase > 1f)
        {
            if (elapsed < 500) return false;
            throw Error("par corporal nao esta playing com fase native valida: Playing=" + playing + "; Phase=" + phase);
        }
        return true;
    }
    private void EmitFingerRay(int elapsed, float phase)
    {
        if (rayPending) return;
        for (int attempt = 0; attempt < fingers.Length; attempt++)
        {
            int index = nextFinger; nextFinger = (nextFinger + 1) % fingers.Length;
            GTA.PedBone bone = actor.Bones[fingers[index].Bone];
            if (bone.Index < 0) throw Error("modelo proprio nao possui dedo terminal real: " + fingers[index].Bone);
            GTA.Math.Vector3 finger = bone.Position; Vector(finger);
            GTA.Math.Vector3 local = table.GetPositionOffset(finger); Vector(local);
            if (local.X < minimum.X || local.X > maximum.X || local.Y < minimum.Y || local.Y > maximum.Y)
            { fingers[index].Contact = false; continue; }
            // O ponto corporal/fase/tempo sao capturados NA EMISSAO, nao misturados com osso futuro.
            emissionFinger = finger; emissionPhase = phase; emissionMs = elapsed;
            emissionGameTime = GTA.Game.GameTime; emissionIndex = index;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
            {
                GTA.Math.Vector3 vertex = table.GetOffsetPosition(new GTA.Math.Vector3(x == 0 ? minimum.X : maximum.X,
                    y == 0 ? minimum.Y : maximum.Y, z == 0 ? minimum.Z : maximum.Z));
                lo = System.Math.Min(lo, vertex.Z); hi = System.Math.Max(hi, vertex.Z);
            }
            rayFrom = new GTA.Math.Vector3(finger.X, finger.Y, hi + 0.25f);
            rayTo = new GTA.Math.Vector3(finger.X, finger.Y, lo - 0.10f);
            Vector(rayFrom); Vector(rayTo);
            fingerRay = GTA.ShapeTest.StartTestLOSProbe(rayFrom, rayTo,
                GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, actor, (GTA.ShapeTestOptions)0);
            if (fingerRay.IsRequestFailed) throw Error("motor recusou ray do dedo; nenhuma proximidade aceita");
            rayPending = true; return; // Um unico pending; start/result <=2 consultas por Tick.
        }
        if (elapsed >= nextLog)
        { nextLog = elapsed + 200; Log("LIMPEZA_DEDOS_FORA", "ElapsedMs=" + elapsed + "; Phase=" + phase + "; Table=" + tablePosition); }
    }
    private void PollFingerResult()
    {
        if (!rayPending) return;
        GTA.ShapeTestResult result;
        GTA.ShapeTestStatus status = fingerRay.GetResult(out result);
        if (status == GTA.ShapeTestStatus.NotReady)
        {
            if (Since(emissionGameTime) > 500) throw Error("resultado proprio do dedo permaneceu pendente500ms");
            return;
        }
        rayPending = false;
        if (status != GTA.ShapeTestStatus.Ready) throw Error("shape test do dedo retornou estado invalido");
        GTA.Entity physical;
        bool ownHit = result.DidHit && result.TryGetHitEntity(out physical) && physical != null && physical.Handle == tableHandle;
        GTA.Math.Vector3 point = result.HitPosition, n = result.SurfaceNormal;
        bool geometry = ownHit && VecFinite(point) && VecFinite(n) && n.Z >= 0.98f &&
            System.Math.Abs(n.Length() - 1f) <= 0.02f && System.Math.Abs(point.X - rayFrom.X) <= 0.01f &&
            System.Math.Abs(point.Y - rayFrom.Y) <= 0.01f && point.Z <= rayFrom.Z && point.Z >= rayTo.Z &&
            System.Math.Abs(Dot(point - center, normal)) <= 0.08f;
        FingerTrack track = fingers[emissionIndex];
        float clearance = geometry ? Dot(emissionFinger - point, n) : float.NaN;
        GTA.Math.Vector3 difference = emissionFinger - point;
        float tangentGap = geometry ? (difference - n * clearance).Length() : float.NaN;
        bool proximity = geometry && Finite(clearance) && clearance >= -0.02f && clearance <= 0.10f && tangentGap <= 0.01f;
        float movement = 0f; bool hadWipe = wipeMeasured;
        if (proximity)
        {
            if (!track.Contact)
            { track.First = emissionFinger; track.Phase = emissionPhase; track.EmissionMs = emissionMs; track.Samples = 0; }
            track.Contact = true; track.Samples++;
            GTA.Math.Vector3 delta = emissionFinger - track.First;
            movement = (delta - normal * Dot(delta, normal)).Length();
            bool phaseProgress = emissionPhase > track.Phase + 0.0001f || track.Phase > emissionPhase + 0.5f;
            if (track.Samples >= 2 && emissionMs > track.EmissionMs && phaseProgress && movement >= 0.025f) wipeMeasured = true;
        }
        else { track.Contact = false; track.Samples = 0; }
        // Valores numericos e amostras contemporaneas; proximidade.10 nao afirma toque de pele aprovado.
        if (emissionMs >= nextLog || (!hadWipe && wipeMeasured))
        {
            nextLog = emissionMs + 200;
            Log("LIMPEZA_DEDO_MEDICAO", "Bone=" + track.Bone + "; EmissionMs=" + emissionMs +
                "; EmissionGameTime=" + emissionGameTime + "; ResultGameTime=" + GTA.Game.GameTime +
                "; EmissionFinger=" + emissionFinger + "; EmissionPhase=" + emissionPhase + "; OwnHit=" + ownHit +
                "; OwnTable=" + tableHandle + "; Point=" + point + "; Normal=" + n + "; Clearance=" + clearance +
                "; TangentGap=" + tangentGap + "; TangentMovementSameFinger=" + movement +
                "; SamplesSameFinger=" + track.Samples + "; GeometricProximity=" + proximity +
                "; MovementMeasured=" + wipeMeasured + "; Flags=17; Options=0; TesteVisual=pendente");
        }
    }
    private void ResetMeasurements()
    {
        wipeMeasured = false; nextLog = 0; nextFinger = 0;
        foreach (FingerTrack finger in fingers) { finger.Contact = false; finger.Samples = 0; }
    }
    private void RequireTransform()
    {
        Owned();
        if (!table.HasPhysics || !table.IsVisible || !table.IsCollisionEnabled || !table.IsPositionFrozen || table.IsAttached() ||
            (table.Position - tablePosition).Length() > 0.01f || (table.Rotation - tableRotation).Length() > 0.1f)
            throw Error("mesa propria mudou identidade/physics/colisao/transform medido");
    }
    private void RequireOutsideTable()
    {
        Vector(actor.Position); GTA.Math.Vector3 local = table.GetPositionOffset(actor.Position);
        if (local.X >= minimum.X && local.X <= maximum.X && local.Y >= minimum.Y && local.Y <= maximum.Y)
            throw Error("raiz do ator esta dentro dos boundsXY da propria mesa");
        if (!actor.HasPhysics || !actor.IsCollisionEnabled || actor.IsInAir || actor.IsRagdoll || actor.IsInVehicle())
            throw Error("limpeza exige corpo proprio a pe com physics/colisao e sem suspensao");
    }
    private void Owned()
    {
        if (disposed || !prepared) throw Error("limpeza nao preparada");
        RequirePed(actor);
        if (!ownsActor(actor) || actor.Handle != actorHandle || actor.Model.Hash != actorModel ||
            actor.MemoryAddress != actorAddress || actor.Health != health)
            throw Error("ator perdeu identidade/propriedade/saude");
        GTA.Prop current = props.GetProp(tableCode);
        if (current.Handle != tableHandle || current.Model.Hash != tableModel || current.MemoryAddress != tableAddress)
            throw Error("mesa perdeu identidade/propriedade");
    }
    private void StopWarm()
    {
        if (!warmStarted) return;
        Owned(); actor.Task.StopScriptedAnimationTask(new GTA.CrClipAsset(Dictionary, Clip));
        actor.IsPositionFrozen = frozenBeforeWarm; warmStarted = false;
    }
    private bool DrainRay()
    {
        if (!rayPending) return true;
        GTA.ShapeTestResult ignored;
        if (fingerRay.GetResult(out ignored) == GTA.ShapeTestStatus.NotReady) return false;
        rayPending = false; return true;
    }
    public bool Cleanup()
    {
        if (disposed || !prepared) return true;
        Owned(); StopWarm(); if (!DrainRay()) return false;
        actor = null; table = null; props = null; ownsActor = null; prepared = ready = running = finished = resetting = false;
        return true;
    }
    public void Dispose()
    { if (!Cleanup()) throw Error("aguarde resultado shape antes de descartar; preservar props/ator"); disposed = true; }
    private static void RequirePed(GTA.Ped ped)
    { if (ped == null || !ped.Exists() || ped.IsPlayer || ped.IsDead || !ped.IsPersistent) throw Error("exige ped proprio vivo/persistente"); }
    private static int Since(int at) { return unchecked((int)(unchecked((uint)GTA.Game.GameTime) - unchecked((uint)at))); }
    private static float Dot(GTA.Math.Vector3 a, GTA.Math.Vector3 b) { return a.X*b.X + a.Y*b.Y + a.Z*b.Z; }
    private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
    private static bool VecFinite(GTA.Math.Vector3 v) { return Finite(v.X) && Finite(v.Y) && Finite(v.Z); }
    private static void Vector(GTA.Math.Vector3 v) { if (!VecFinite(v)) throw Error("vetor native nao finito"); }
    private static System.InvalidOperationException Error(string text) { return new System.InvalidOperationException("LimpezaMesa: " + text); }
    private void Log(string kind, string text) { if (log != null) log(kind, text); }
}
