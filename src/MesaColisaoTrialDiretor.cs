// Ensaio diagnostico isolado. Nao monta atores e nao possui API IsReady.
internal sealed class MesaColisaoTrialDiretor : System.IDisposable
{
    private sealed class Probe
    {
        internal GTA.ShapeTestHandle Handle;
        internal GTA.Math.Vector3 From, To, Point, Normal;
        internal bool Started, Done, OwnSurface;
    }

    private const int DeadlineMs = 5000;
    private readonly System.Action<string, string> log;
    private readonly Probe[] probes = new Probe[5];
    private GTA.Model model;
    private GTA.Prop own;
    private GTA.Math.Vector3 position, rotation, minimum, maximum;
    private string modelName;
    private int ownHandle, started, requests, createdAt;
    private System.IntPtr ownAddress;
    private bool active, finished, requested, collisionBeforeCreate, measuredFloor;
    private string conclusion;

    internal MesaColisaoTrialDiretor(System.Action<string, string> logger)
    {
        log = logger;
    }

    internal bool IsFinished { get { return finished; } }
    internal string Conclusion { get { return conclusion; } }

    // Caller supplies a real measured mark/prepared own-prop position; no coordinates are invented here.
    internal void Prepare(string exactModel, GTA.Math.Vector3 measuredPosition, float measuredHeading)
    {
        Dispose();
        if (exactModel != "prop_table_01" && exactModel != "prop_table_03")
            throw Error("modelo fora dos dois candidatos instalados");
        Vector(measuredPosition);
        if (!Finite(measuredHeading)) throw Error("direcao nao finita");
        model = new GTA.Model(exactModel);
        if (!model.IsInCdImage || !model.IsProp) throw Error("modelo ausente ou nao e prop");
        modelName = exactModel;
        position = measuredPosition;
        rotation = new GTA.Math.Vector3(0f, 0f, measuredHeading);
        started = GTA.Game.GameTime;
        active = true;
        finished = false;
        requested = false;
        measuredFloor = false;
        requests = 0;
        conclusion = null;
        for (int i = 0; i < probes.Length; i++) probes[i] = new Probe();
        Log("MESA_TRIAL_INICIO", "Modelo=" + modelName + "; Hash=" + model.Hash +
            "; Position=" + position + "; Heading=" + measuredHeading +
            "; DeadlineMs=5000; Actors=0; ProductionReady=False; ProductionGuardsUnchanged=True");
    }

    internal void PrepareOnMeasuredFloor(string exactModel, GTA.Math.Vector3 measuredFloorPoint, float measuredHeading)
    {
        Prepare(exactModel, measuredFloorPoint, measuredHeading);
        measuredFloor = true;
    }

    // Poll once every script tick. At most two pending reads plus two new rays per tick.
    internal void Poll()
    {
        if (!active || finished) return;
        try
        {
            bool loaded = model.IsLoaded;
            bool collision = model.IsCollisionLoaded;
            model.Request();
            model.RequestCollision();
            requests++;
            if (!requested)
            {
                requested = true;
                Log("MESA_TRIAL_MODELO", "Fase=PEDIDO; Loaded=" + loaded +
                    "; CollisionLoaded=" + collision + "; Requests=" + requests);
                return;
            }
            if (System.Object.ReferenceEquals(own, null))
            {
                if (Elapsed(started) >= DeadlineMs)
                {
                    Finish(loaded ? "deadline_antes_create" : "modelo_nao_carregou", false);
                    return;
                }
                if (!loaded)
                {
                    return;
                }
                collisionBeforeCreate = collision;
                model.GetDimensions(out minimum, out maximum);
                Vector(minimum); Vector(maximum);
                if (minimum.X >= maximum.X || minimum.Y >= maximum.Y || minimum.Z >= maximum.Z)
                    throw Error("limites do modelo invalidos");
                if (measuredFloor)
                {
                    float floor = position.Z;
                    position.Z = floor - minimum.Z;
                    Vector(position);
                    Log("MESA_TRIAL_BOTTOM_MEDIDO", "Floor=" + floor + "; ModelMinZ=" + minimum.Z +
                        "; OwnPivot=" + position + "; AntesCreate=True; Actors=0; ProductionReady=False");
                }
                Log("MESA_TRIAL_MODELO", "Fase=ANTES_CREATE; Loaded=" + loaded +
                    "; CollisionLoaded=" + collisionBeforeCreate + "; Requests=" + requests +
                    "; Min=" + minimum + "; Max=" + maximum +
                    "; DiagnosticCreateAllowedByAuthorAPI=True; ProductionReady=False");
                own = GTA.Prop.CreateNoOffset(model, position, rotation, true);
                if (System.Object.ReferenceEquals(own, null) || !own.Exists())
                    throw Error("criacao diagnostica falhou");
                // Register identity before any setter which may fail.
                ownHandle = own.Handle;
                ownAddress = own.MemoryAddress;
                if (ownAddress == System.IntPtr.Zero) throw Error("endereco proprio indisponivel");
                own.IsPersistent = true;
                own.IsInvincible = true;
                own.IsCollisionEnabled = true;
                own.IsVisible = true;
                own.IsPositionFrozen = true;
                createdAt = GTA.Game.GameTime;
                RequireOwn();
                BuildProbes();
                LogState("APOS_CREATE");
                return;
            }

            RequireOwn();
            int pending = 0;
            for (int i = 0; i < probes.Length; i++)
            {
                Probe p = probes[i];
                if (!p.Started || p.Done) continue;
                GTA.ShapeTestResult measured;
                GTA.ShapeTestStatus status = p.Handle.GetResult(out measured);
                if (status == GTA.ShapeTestStatus.NotReady) { pending++; continue; }
                p.Done = true;
                if (status != GTA.ShapeTestStatus.Ready)
                {
                    Log("MESA_TRIAL_RAY", "Indice=" + i + "; Status=" + status + "; SurfaceEvidence=False");
                    continue;
                }
                GTA.Entity physical = null;
                bool hitOwn = measured.DidHit && measured.TryGetHitEntity(out physical) &&
                    !System.Object.ReferenceEquals(physical, null) && physical.Handle == ownHandle;
                p.Point = measured.HitPosition;
                p.Normal = measured.SurfaceNormal;
                bool finite = VectorFinite(p.Point) && VectorFinite(p.Normal);
                p.OwnSurface = hitOwn && finite &&
                    System.Math.Abs(p.Point.X - p.From.X) <= 0.01f &&
                    System.Math.Abs(p.Point.Y - p.From.Y) <= 0.01f &&
                    p.Point.Z <= p.From.Z && p.Point.Z >= p.To.Z &&
                    System.Math.Abs(p.Normal.X) <= 1.01f && System.Math.Abs(p.Normal.Y) <= 1.01f &&
                    p.Normal.Z >= 0.98f && p.Normal.Z <= 1.01f;
                Log("MESA_TRIAL_RAY", "Indice=" + i + "; Status=" + status +
                    "; DidHit=" + measured.DidHit + "; HitHandle=" +
                    (System.Object.ReferenceEquals(physical, null) ? 0 : physical.Handle) +
                    "; OwnHandle=" + ownHandle + "; Point=" + p.Point + "; Normal=" + p.Normal +
                    "; SurfaceEvidence=" + p.OwnSurface +
                    "; Flags=17; Options=0; NonPhysicalModelGetters=False; ProductionReady=False");
            }

            bool allDone = true, allOwn = true;
            float lowest = System.Single.MaxValue, highest = System.Single.MinValue;
            for (int i = 0; i < probes.Length; i++)
            {
                Probe p = probes[i];
                allDone &= p.Done;
                allOwn &= p.Done && p.OwnSurface;
                if (p.Done && p.OwnSurface) { lowest = System.Math.Min(lowest, p.Point.Z); highest = System.Math.Max(highest, p.Point.Z); }
            }
            if (allDone)
            {
                bool plane = allOwn && highest - lowest <= 0.08f;
                Finish(plane ? "cinco_superficies_proprias_medidas" : "prova_superficie_incompleta", plane);
                return;
            }
            if (Elapsed(started) >= DeadlineMs)
            {
                Finish("deadline_diagnostico", false);
                return;
            }
            for (int i = 0; i < probes.Length && pending < 2; i++)
            {
                Probe p = probes[i];
                if (p.Started) continue;
                p.Handle = GTA.ShapeTest.StartTestLOSProbe(p.From, p.To,
                    GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, null, (GTA.ShapeTestOptions)0);
                p.Started = true;
                pending++;
                Log("MESA_TRIAL_RAY_START", "Indice=" + i + "; From=" + p.From +
                    "; To=" + p.To + "; Flags=17; Options=0; IgnoreOwnTable=False");
            }
        }
        catch (System.Exception error)
        {
            Log("MESA_TRIAL_ERRO", error.Message);
            Finish("erro_diagnostico", false);
        }
    }

    private void BuildProbes()
    {
        float centerX = (minimum.X + maximum.X) * 0.5f;
        float centerY = (minimum.Y + maximum.Y) * 0.5f;
        // Five locations derived only from actual model bounds, not hand-entered world offsets.
        float extentX = (maximum.X - minimum.X) * 0.4f;
        float extentY = (maximum.Y - minimum.Y) * 0.4f;
        float[] dx = { 0f, -extentX, extentX, -extentX, extentX };
        float[] dy = { 0f, -extentY, -extentY, extentY, extentY };
        for (int i = 0; i < probes.Length; i++)
        {
            probes[i].From = own.GetOffsetPosition(new GTA.Math.Vector3(centerX + dx[i], centerY + dy[i], maximum.Z + 0.25f));
            probes[i].To = own.GetOffsetPosition(new GTA.Math.Vector3(centerX + dx[i], centerY + dy[i], minimum.Z - 0.10f));
            Vector(probes[i].From); Vector(probes[i].To);
        }
    }

    private void RequireOwn()
    {
        if (System.Object.ReferenceEquals(own, null) || !own.Exists() || own.Handle != ownHandle ||
            own.MemoryAddress != ownAddress || own.Model.Hash != model.Hash)
            throw Error("identidade da mesa propria mudou");
        if (!own.IsCollisionEnabled || !own.IsPositionFrozen || !own.IsVisible)
            throw Error("estado diagnostico proprio mudou");
        Vector(own.Position); Vector(own.Rotation);
        if ((own.Position - position).Length() > 0.01f ||
            System.Math.Abs(own.Rotation.X - rotation.X) > 0.1f ||
            System.Math.Abs(own.Rotation.Y - rotation.Y) > 0.1f ||
            System.Math.Abs(Angle(own.Rotation.Z - rotation.Z)) > 0.1f)
            throw Error("transformacao da mesa propria mudou");
    }

    private void LogState(string phase)
    {
        Log("MESA_TRIAL_ESTADO", "Fase=" + phase + "; Modelo=" + modelName +
            "; ModelLoaded=" + model.IsLoaded + "; ModelCollisionLoaded=" + model.IsCollisionLoaded +
            "; CollisionBeforeCreate=" + collisionBeforeCreate + "; Handle=" + ownHandle +
            "; Address=" + ownAddress + "; Position=" + own.Position + "; Rotation=" + own.Rotation +
            "; HasPhysics=" + own.HasPhysics + "; CollisionEnabled=" + own.IsCollisionEnabled +
            "; Frozen=" + own.IsPositionFrozen + "; Interior=" +
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_INTERIOR_FROM_ENTITY, ownHandle) +
            "; SinceCreateMs=" + Elapsed(createdAt) + "; ProductionReady=False");
    }

    private void Finish(string reason, bool surfaceEvidence)
    {
        if (finished) return;
        conclusion = reason;
        finished = true;
        active = false;
        try
        {
            if (!System.Object.ReferenceEquals(own, null) && own.Exists() &&
                own.Handle == ownHandle && own.MemoryAddress == ownAddress && own.Model.Hash == model.Hash)
                LogState("FIM");
            Log("MESA_TRIAL_FIM", "Motivo=" + reason + "; OwnSurfaceEvidence=" + surfaceEvidence +
                "; ModelLoaded=" + model.IsLoaded + "; ModelCollisionLoaded=" + model.IsCollisionLoaded +
                "; Requests=" + requests + "; ElapsedMs=" + Elapsed(started) +
                "; Actors=0; ProductionReady=False; ProductionGuardsUnchanged=True");
        }
        finally { ReleaseOwn(); }
    }

    public void Dispose()
    {
        ReleaseOwn();
        active = false;
        if (requested) model.MarkAsNoLongerNeeded();
        requested = false;
    }

    private void ReleaseOwn()
    {
        if (System.Object.ReferenceEquals(own, null)) return;
        if (own.Exists() && own.Handle == ownHandle && own.MemoryAddress == ownAddress && own.Model.Hash == model.Hash)
        {
            own.Delete();
            Log("MESA_TRIAL_LIMPEZA", "OwnHandle=" + ownHandle + "; DeletedOwned=True");
        }
        else Log("MESA_TRIAL_LIMPEZA", "OwnHandle=" + ownHandle + "; IdentityChangedOrGone=True; ExternalDeleted=False");
        own = null;
        ownHandle = 0;
        ownAddress = System.IntPtr.Zero;
    }

    private static int Elapsed(int begin) { return unchecked(GTA.Game.GameTime - begin); }
    private static float Angle(float value) { while (value > 180f) value -= 360f; while (value < -180f) value += 360f; return value; }
    private static bool Finite(float value) { return !System.Single.IsNaN(value) && !System.Single.IsInfinity(value); }
    private static bool VectorFinite(GTA.Math.Vector3 value) { return Finite(value.X) && Finite(value.Y) && Finite(value.Z); }
    private static void Vector(GTA.Math.Vector3 value) { if (!VectorFinite(value)) throw Error("vetor nao finito"); }
    private static System.InvalidOperationException Error(string detail) { return new System.InvalidOperationException("Trial mesa: " + detail); }
    private void Log(string code, string detail) { if (log != null) log(code, detail); }
}
