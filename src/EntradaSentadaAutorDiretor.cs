// Candidato ISOLADO80. Sem Script, sem Main, sem TASK_PLAY_ANIM_ADVANCED ou setter XYZ da atriz.
// Fonte: am_mp_property_int.c@7a800e34172bc50f13b07884ca55942028e8c8d0,
// 76990..77004,470529..470551,470599..470604,472581..472635.
public sealed class EntradaSentadaAutorDiretor : System.IDisposable
{
    public const string Dictionary = "anim@amb@office@boardroom@boss@female@";
    public const string ChairModel = "ex_prop_offchair_exec_02";
    private const int LoadMaximumMs = 5000, ProbeMaximumMs = 2000;
    private readonly System.Action<string, string> log;
    private readonly string menyoo;
    private GTA.Ped actor;
    private GTA.Prop chair;
    private ObjetosSerieDiretor objects;
    private System.Func<GTA.Ped, bool> ownsActor;
    private string chairCode;
    private int actorHandle, actorModel, health, chairHandle, chairHash;
    private System.IntPtr actorAddress, chairAddress;
    private GTA.Math.Vector3 originalActor, chairOrigin, chairRotation, minimum, maximum;
    private GTA.Math.Vector3 entryStart, entryRotation, surfaceLocal, currentSurface, authoredBaseRoot;
    private GTA.Math.Vector3 probePosition, probeRotation, rayTop, rayBottom;
    private GTA.FwSyncedScene scene;
    private System.Diagnostics.Stopwatch loading;
    private int surfaceId, stage, standAt, clipAt, probeAt, ray, lastElapsed = -1, nextLog, resetAt;
    private int startMs, windowMs, originalFirstReplyMs;
    private float enterDurationMs, maximumPhase;
    private bool prepared, ready, disposed, running, finished, dictionaryWasLoaded, requested, resetPending;
    private bool observed, baseRequested, phasePendingLogged;
    private long nextLoadingLog;
    private bool surfaceMeasured;
    private GTA.Math.Vector3 measuredSurfaceNormal;

    public EntradaSentadaAutorDiretor(System.Action<string, string> writeLog, string pastaMenyoo)
    { log = writeLog; menyoo = pastaMenyoo; }
    public bool IsReady { get { return prepared && ready && !disposed; } }
    public bool IsFinished { get { return IsReady && finished; } }
    public bool ControlsBody(GTA.Ped ped)
    { return IsReady && ped != null && ped.Handle == actorHandle; }
    public bool ControlsBody(string id, int elapsedMs)
    { return IsReady && elapsedMs >= 0 && System.String.Equals(id, "djanira_pobre", System.StringComparison.OrdinalIgnoreCase); }

    // A cadeira e o ator ja sao proprios do chamador. Props fica pronto antes deste bind.
    public void Prepare(GTA.Ped ownActor, System.Func<GTA.Ped, bool> actorOwnership,
                        ObjetosSerieDiretor ownObjects, string ownChair, int inicioMs, int duracaoMs, int primeiraRespostaMs)
    {
        Active();
        if (prepared || actorOwnership == null || ownObjects == null || !ownObjects.IsReady ||
            ownActor == null || !actorOwnership(ownActor)) throw Error("ator e cadeira proprios preparados exigidos");
        if (primeiraRespostaMs <= 0 || primeiraRespostaMs > 180000 || inicioMs < 0 || duracaoMs <= 0 ||
            inicioMs >= primeiraRespostaMs || duracaoMs > primeiraRespostaMs - inicioMs)
            throw Error("entrada precisa de janela positiva anterior a primeira resposta, derivada do roteiro original");
        VerifySources();
        actor = ownActor; ownsActor = actorOwnership; objects = ownObjects; chairCode = ownChair;
        chair = objects.GetProp(chairCode);
        actorHandle = actor.Handle; actorModel = actor.Model.Hash; actorAddress = actor.MemoryAddress; health = actor.Health;
        chairHandle = chair.Handle; chairHash = chair.Model.Hash; chairAddress = chair.MemoryAddress;
        if (chairHash != new GTA.Model(ChairModel).Hash || !objects.IsSupport(chairCode))
            throw Error("cadeira propria deve usar exatamente o modelo do par autor");
        if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_PED_MALE, actorHandle)) throw Error("par autor feminino exige modelo feminino");
        RequireActor(); RequireChair(true);
        originalActor = actor.Position; chairOrigin = chair.Position; chairRotation = chair.Rotation;
        Check(originalActor); Check(chairOrigin); Check(chairRotation);
        objects.GetMeasuredBounds(chairCode, out minimum, out maximum); Check(minimum); Check(maximum);
        if (System.Math.Abs(chairRotation.X) > 0.01f || System.Math.Abs(chairRotation.Y) > 0.01f)
            throw Error("cadeira precisa estar horizontal no piso real");
        startMs = inicioMs; windowMs = duracaoMs; originalFirstReplyMs = primeiraRespostaMs; loading = System.Diagnostics.Stopwatch.StartNew(); prepared = true;
        dictionaryWasLoaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, Dictionary);
        GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, Dictionary); requested = true;
        chair.Model.RequestCollision();
        Log("SENTAR_AUTOR_LOADING", "Actor=" + actorHandle + "; Chair=" + chairHandle + "; Model=" + ChairModel +
            "; ChairPos=" + chairOrigin + "; ChairRot=" + chairRotation + "; EntryWindow=" + startMs + "+" + windowMs +
            "; OriginalFirstReplyStartMs=" + originalFirstReplyMs + "; LimitDerivedFromScene=true; LoadMaximum=5000; NoActorXYZSetter=true");
    }

    public void PollLoading()
    {
        Active(); if (!prepared || ready) return;
        RequireActor(); RequireChair(true);
        if (loading.ElapsedMilliseconds >= LoadMaximumMs)
        { LogLoadingStage(true); throw Error("clip/cadeira/inicio ereto nao prontos em5s"); }
        if (!chair.Model.IsCollisionLoaded) { chair.Model.RequestCollision(); return; }
        if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, Dictionary))
        { GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, Dictionary); return; }
        if ((chair.Position - chairOrigin).Length() > 0.01f || (chair.Rotation - chairRotation).Length() > 0.01f)
            throw Error("cadeira moveu durante o preparo");
        if (stage == 0)
        {
            float pedEnterDurationMs = Duration("enter"), chairDuration = Duration("enter_chair");
            float baseDurationMs = Duration("base"), chairBaseDurationMs = Duration("base_chair");
            // Tracks authored podem ter duracoes distintas; ambos precisam caber inteiros.
            enterDurationMs = System.Math.Max(pedEnterDurationMs, chairDuration);
            Log("SENTAR_AUTOR_DURACOES", "Dictionary=" + Dictionary +
                "; EnterMs=" + Number(pedEnterDurationMs) + "; ChairEnterMs=" + Number(chairDuration) +
                "; BaseMs=" + Number(baseDurationMs) + "; ChairBaseMs=" + Number(chairBaseDurationMs) +
                "; PairFullMs=" + Number(enterDurationMs) + "; StartMs=" + startMs + "; WindowMs=" + windowMs +
                "; OriginalFirstReplyStartMs=" + originalFirstReplyMs + "; LimitDerivedFromScene=true; EqualDurationRequired=false; FullTracksRequired=true");
            if (enterDurationMs > windowMs)
                throw Error("par completo nao cabe na janela declarada: EnterMs=" + Number(pedEnterDurationMs) +
                    "; ChairEnterMs=" + Number(chairDuration) + "; PairFullMs=" + Number(enterDurationMs) +
                    "; StartMs=" + startMs + "; WindowMs=" + windowMs + "; OriginalFirstReplyStartMs=" + originalFirstReplyMs);
            entryStart = Initial("enter");
            entryRotation = GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ANIM_INITIAL_OFFSET_ROTATION,
                Dictionary, "enter", chairOrigin.X, chairOrigin.Y, chairOrigin.Z,
                chairRotation.X, chairRotation.Y, chairRotation.Z, 0f, 2);
            GTA.Math.Vector3 baseRoot = Initial("base"), chairStart = Initial("enter_chair");
            Check(entryStart); Check(entryRotation); Check(baseRoot); Check(chairStart);
            authoredBaseRoot = baseRoot;
            if ((entryStart - originalActor).Length() > 3f) throw Error("inicio autor esta fora dos3m originais");
            if ((chairStart - chairOrigin).Length() > 0.01f)
                throw Error("track da cadeira nao inicia no transform real; nao reposicionar para simular encaixe");
            // Confere o Z authored com os pes do ator ereto e getter de piso; nao converte pelvis final em Z inicial.
            RequireStanding(true);
            ValidateAuthoredStartFeet();
            GTA.Math.Vector3 local = chair.GetPositionOffset(baseRoot);
            if (local.X < minimum.X || local.X > maximum.X || local.Y < minimum.Y || local.Y > maximum.Y)
                throw Error("base authored projeta fora da cadeira real");
            surfaceId = objects.RequestSurface(chairCode, local.X, local.Y);
            actor.Task.ClearAllImmediately(); actor.IsPositionFrozen = false;
            actor.Task.GoStraightTo(entryStart, (int)(LoadMaximumMs - loading.ElapsedMilliseconds),
                new GTA.PedMoveBlendRatio(0.8f), entryRotation.Z, 0.05f);
            stage = 1;
            Log("SENTAR_AUTOR_INICIO_AUTHORED", "EnterStart=" + entryStart + "; EnterRot=" + entryRotation +
                "; BaseRoot=" + baseRoot + "; ChairStart=" + chairStart + "; DurationMs=" + Number(enterDurationMs) +
                "; NativeP8=0; NativeP9=2; CaminhadaReal=true; SemBaixarRaiz=true");
            return;
        }
        if (stage == 1)
        {
            // Props ja esta pronto; o chamador deixou de consulta-lo na barreira original.
            // Avancar o ray proprio entre frames antes de ler sua medicao.
            objects.PollLoading();
            GTA.Math.Vector3 point, normal;
            bool known = objects.TryGetMeasuredSurface(surfaceId, out point, out normal);
            if (known) { surfaceMeasured = true; currentSurface = point; measuredSurfaceNormal = normal; }
            LogLoadingStage(false);
            if (!known) return;
            Check(point); Check(normal);
            if (normal.Z < 0.98f) throw Error("superficie propria sem normal horizontal medida");
            surfaceLocal = chair.GetPositionOffset(point); currentSurface = point;
            if ((actor.Position - entryStart).Length() > 0.05f || Angle(actor.Heading - entryRotation.Z) > 5f) return;
            actor.Task.StandStill(-1); actor.IsPositionFrozen = true; standAt = GTA.Game.GameTime; stage = 2;
            return;
        }
        if (unchecked((uint)(GTA.Game.GameTime - standAt)) < 250u) return;
        RequireStanding(true);
        if ((actor.Position - entryStart).Length() > 0.05f) throw Error("inicio nao ficou na marca authored a pe");
        ready = true;
        Log("SENTAR_AUTOR_PRONTO", "Root=" + actor.Position + "; Surface=" + currentSurface +
            "; ChairCollisionLoaded=" + chair.Model.IsCollisionLoaded + "; SomenteInicioEretoValidado=true; FimSentadoAindaNaoTestado=true");
    }

    // Somente getters de diagnostico; nao decide chegada, nao altera tarefas/flags/limites.
    private void LogLoadingStage(bool final)
    {
        if (loading == null || (!final && loading.ElapsedMilliseconds < nextLoadingLog)) return;
        nextLoadingLog = loading.ElapsedMilliseconds + 500;
        try
        {
            GTA.Math.Vector3 root = actor.Position, delta = root - entryStart;
            GTA.Math.Vector3 localRoot = chair.GetPositionOffset(root);
            GTA.ScriptTaskNameHash task; GTA.ScriptTaskStatus status;
            actor.GetCurrentScriptTaskNameHashAndStatus(out task, out status);
            float heading = actor.Heading, distance = delta.Length(), xy = Horizontal(delta);
            var data = new System.Text.StringBuilder("Stage=" + stage + "; LoadingMs=" + loading.ElapsedMilliseconds +
                "; Final=" + final + "; Actor=" + actorHandle + "; ActorRoot=" + root + "; OriginalActor=" + originalActor +
                "; EntryTargetAvailable=" + (stage > 0) + "; EntryStart=" + entryStart +
                "; Distance3D=" + Number(distance) + "; DistanceXY=" + Number(xy) + "; DeltaZ=" + Number(delta.Z) +
                "; Heading=" + Number(heading) + "; EntryHeading=" + Number(entryRotation.Z) +
                "; HeadingAngle=" + Number(Angle(heading - entryRotation.Z)) +
                "; RootReady=" + (stage > 0 && distance <= 0.05f) +
                "; HeadingReady=" + (stage > 0 && Angle(heading - entryRotation.Z) <= 5f) +
                "; Frozen=" + actor.IsPositionFrozen + "; Collision=" + actor.IsCollisionEnabled +
                "; HasPhysics=" + actor.HasPhysics + "; Speed=" + Number(actor.Speed) + "; Velocity=" + actor.Velocity +
                "; ScriptTask=" + task + "; ScriptTaskStatus=" + status +
                "; SurfaceId=" + surfaceId + "; SurfaceMeasured=" + surfaceMeasured +
                "; Surface=" + currentSurface + "; SurfaceNormal=" + measuredSurfaceNormal +
                "; Chair=" + chairHandle + "; ChairPos=" + chair.Position + "; ChairRot=" + chair.Rotation +
                "; ChairFrozen=" + chair.IsPositionFrozen + "; ChairCollision=" + chair.IsCollisionEnabled +
                "; ChairHasPhysics=" + chair.HasPhysics + "; ChairMin=" + minimum + "; ChairMax=" + maximum +
                "; ActorRootChairLocal=" + localRoot + "; RootInsideChairBBox=" + InsideChairBounds(localRoot) +
                "; RootChairBBoxDistance=" + Number(DistanceChairBounds(localRoot)) +
                "; BBoxIsPotentialOverlapOnly=true; DistanceLimit=.05; HeadingLimit=5; LoadMaximum=5000; NoNewFloorQuery=true");
            foreach (GTA.Bone bone in new GTA.Bone[] { GTA.Bone.SkelPelvis, GTA.Bone.SkelHead, GTA.Bone.SkelLeftFoot, GTA.Bone.SkelRightFoot })
            {
                GTA.EntityBone value = actor.Bones[bone];
                bool valid = value.IsValid;
                data.Append("; ").Append(bone).Append("Valid=").Append(valid);
                if (!valid) continue;
                GTA.Math.Vector3 position = value.Position, local = chair.GetPositionOffset(position);
                data.Append("; ").Append(bone).Append("=").Append(position).Append("; ").Append(bone)
                    .Append("ChairLocal=").Append(local).Append("; ").Append(bone).Append("InsideChairBBox=").Append(InsideChairBounds(local));
            }
            // Lista limitada para correlacionar o outro ator com as identidades de montagem do Main.
            // A funcao ownsActor pertence exclusivamente a esta atriz; nao chama outros peds de proprios.
            int count = 0;
            foreach (GTA.Ped nearby in GTA.World.GetNearbyPeds(root, 3f))
            {
                if (nearby == null || !nearby.Exists() || nearby.Handle == actorHandle) continue;
                if (++count > 8) break;
                data.Append("; NearbyPed").Append(count).Append("Handle=").Append(nearby.Handle)
                    .Append(",Model=").Append(nearby.Model.Hash).Append(",Pos=").Append(nearby.Position)
                    .Append(",Distance=").Append(Number((nearby.Position - root).Length()))
                    .Append(",Frozen=").Append(nearby.IsPositionFrozen).Append(",Collision=").Append(nearby.IsCollisionEnabled);
            }
            Log("SENTAR_AUTOR_LOADING_STAGE", data.ToString());
        }
        catch (System.Exception error)
        { Log("SENTAR_AUTOR_LOADING_STAGE_INDISPONIVEL", "Stage=" + stage + "; LoadingMs=" + loading.ElapsedMilliseconds + "; Error=" + error.Message + "; GuardasInalteradas=true"); }
    }
    private bool InsideChairBounds(GTA.Math.Vector3 value)
    { return value.X >= minimum.X && value.X <= maximum.X && value.Y >= minimum.Y && value.Y <= maximum.Y && value.Z >= minimum.Z && value.Z <= maximum.Z; }
    private float DistanceChairBounds(GTA.Math.Vector3 value)
    {
        float x = System.Math.Max(minimum.X - value.X, System.Math.Max(0f, value.X - maximum.X));
        float y = System.Math.Max(minimum.Y - value.Y, System.Math.Max(0f, value.Y - maximum.Y));
        float z = System.Math.Max(minimum.Z - value.Z, System.Math.Max(0f, value.Z - maximum.Z));
        return (float)System.Math.Sqrt(x * x + y * y + z * z);
    }

    public void Start()
    {
        Active(); if (!IsReady || running || resetPending) throw Error("preparo concluido exigido antes da contagem/acao");
        RequireActor(); RequireChair(true); RequireStanding(true);
        if ((actor.Position - entryStart).Length() > 0.05f) throw Error("marca authored mudou antes da acao");
        actor.IsPositionFrozen = true; lastElapsed = -1;
    }
    public void Update(int elapsedMs)
    {
        Active(); if (!IsReady || elapsedMs < 0 || elapsedMs > 180000 || elapsedMs < lastElapsed)
            throw Error("preparo e relogio crescente original exigidos");
        RequireActor(); RequireChair(!running);
        if (!running)
        {
            RequireStanding(false);
            if (elapsedMs < startMs) { lastElapsed = elapsedMs; return; }
            if (elapsedMs - startMs >= 500 || (actor.Position - entryStart).Length() > 0.05f)
                throw Error("entrada nao iniciou dentro da marca/janela original");
            actor.IsPositionFrozen = false; chair.IsPositionFrozen = false;
            BeginPair("enter", "enter_chair", false, 1.5f);
            running = true; clipAt = GTA.Game.GameTime;
            Log("SENTAR_AUTOR_ENTRADA", "Root=" + actor.Position + "; Chair=" + chair.Position +
                "; Scene=" + scene.Handle + "; PedSyncedFlags=13; BlockMoverUpdate=false; TaskAdvanced=false; ActorXYZSetter=false");
            lastElapsed = elapsedMs; return;
        }
        if (scene == null || !scene.Exists()) throw Error("scene propria nativa deixou de existir");
        float phase = scene.Phase;
        bool pedPlaying = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, actorHandle, Dictionary, baseRequested ? "base" : "enter", 3);
        bool chairPlaying = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, chairHandle, Dictionary, baseRequested ? "base_chair" : "enter_chair", 3);
        uint since = unchecked((uint)(GTA.Game.GameTime - clipAt));
        if (!Finite(phase) || phase < 0f || phase > 1f || !pedPlaying || !chairPlaying)
        {
            if (since < 500u && !finished)
            {
                if (!phasePendingLogged) { Log("SENTAR_AUTOR_PHASE_PENDING", "Phase=" + Number(phase) + "; PedPlaying=" + pedPlaying + "; ChairPlaying=" + chairPlaying); phasePendingLogged = true; }
                lastElapsed = elapsedMs; return;
            }
            throw Error("phase/playing do par real invalida; phase=" + Number(phase));
        }
        if (!baseRequested)
        {
            maximumPhase = System.Math.Max(maximumPhase, phase); observed = observed || phase >= 0.15f;
            if (!observed || maximumPhase < 0.999f || since < enterDurationMs)
            { CheckWindow(elapsedMs); lastElapsed = elapsedMs; return; }
            BeginPair("base", "base_chair", true, 4f);
            baseRequested = true; clipAt = GTA.Game.GameTime; phasePendingLogged = false;
            Log("SENTAR_AUTOR_BASE", "Root=" + actor.Position + "; Chair=" + chair.Position + "; MaxEntryPhase=" + Number(maximumPhase));
            lastElapsed = elapsedMs; return;
        }
        if (!finished)
        {
            if (!PollOwnSurface()) { CheckWindow(elapsedMs); lastElapsed = elapsedMs; return; }
            CheckSeated(true);
            finished = true;
            Log("SENTAR_AUTOR_CONCLUIDO", "ElapsedMs=" + elapsedMs + "; Root=" + actor.Position +
                "; Chair=" + chair.Position + "; Surface=" + currentSurface + "; ActorXYZSetter=false; OwnEntityHit=true");
        }
        else
        {
            if ((chair.Position - probePosition).Length() > 0.01f || (chair.Rotation - probeRotation).Length() > 0.01f)
                throw Error("cadeira mudou depois do contato final medido");
            CheckSeated(false);
        }
        lastElapsed = elapsedMs;
    }

    private void BeginPair(string pedClip, string chairClip, bool loop, float blend)
    {
        GTA.FwSyncedScene next = GTA.FwSyncedScene.Create(chairOrigin, chairRotation, GTA.EulerRotationOrder.YXZ);
        if (next == null) throw Error("motor recusou scene local Story");
        next.HoldsLastFrame = true; next.IsLooped = loop;
        // Valores do autor; nao BlockMoverUpdate, nao OverridePhysics, nao RepositionWhenFinished.
        actor.Task.PlaySynchronizedScene(next, new GTA.CrClipAsset(Dictionary, pedClip),
            new GTA.AnimationBlendDelta(blend), new GTA.AnimationBlendDelta(blend),
            (GTA.SyncedSceneFlags)13, (GTA.RagdollBlockingFlags)16,
            new GTA.AnimationBlendDelta(loop ? 1000f : 1.5f), (GTA.AnimationIKControlFlags)0);
        if (!chair.PlaySynchronizedAnim(next, new GTA.CrClipAsset(Dictionary, chairClip),
            new GTA.AnimationBlendDelta(4f), new GTA.AnimationBlendDelta(4f),
            (GTA.SyncedSceneFlags)32781, new GTA.AnimationBlendDelta(1000f)))
            throw Error("motor recusou track authored da cadeira propria");
        // A API desliga a colisao do objeto ao iniciar o track; restaura explicitamente so o objeto proprio.
        chair.IsCollisionEnabled = true;
        if (!chair.IsCollisionEnabled) throw Error("track proprio nao permite colisao real da cadeira");
        scene = next;
    }

    private bool PollOwnSurface()
    {
        RequireChair(false);
        if (ray == 0)
        {
            probePosition = chair.Position; probeRotation = chair.Rotation;
            if (System.Math.Abs(probeRotation.X) > 0.01f || System.Math.Abs(probeRotation.Y) > 0.01f)
                throw Error("track da cadeira inclinou o apoio");
            rayTop = chair.GetOffsetPosition(new GTA.Math.Vector3(surfaceLocal.X, surfaceLocal.Y, maximum.Z + 0.2f));
            rayBottom = chair.GetOffsetPosition(new GTA.Math.Vector3(surfaceLocal.X, surfaceLocal.Y, minimum.Z - 0.2f));
            ray = GTA.Native.Function.Call<int>(GTA.Native.Hash.START_SHAPE_TEST_LOS_PROBE,
                rayTop.X, rayTop.Y, rayTop.Z, rayBottom.X, rayBottom.Y, rayBottom.Z, 17, actorHandle, 0);
            if (ray == 0) throw Error("motor recusou ray do apoio proprio final");
            probeAt = GTA.Game.GameTime; return false;
        }
        if (unchecked((uint)(GTA.Game.GameTime - probeAt)) > ProbeMaximumMs) throw Error("ray do apoio proprio nao concluiu em2s");
        if ((chair.Position - probePosition).Length() > 0.01f || (chair.Rotation - probeRotation).Length() > 0.01f)
            throw Error("apoio proprio moveu enquanto ray final estava pendente");
        System.IntPtr hit = System.IntPtr.Zero, point = System.IntPtr.Zero, normal = System.IntPtr.Zero, entity = System.IntPtr.Zero;
        try
        {
            hit = Buffer(); point = Buffer(); normal = Buffer(); entity = Buffer();
            int status = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_SHAPE_TEST_RESULT, ray, hit, point, normal, entity);
            if (status == 1) return false;
            int rawHit = System.Runtime.InteropServices.Marshal.ReadInt32(hit), handle = System.Runtime.InteropServices.Marshal.ReadInt32(entity);
            GTA.Math.Vector3 p = ReadVector(point), n = ReadVector(normal); Check(p); Check(n);
            Log("SENTAR_AUTOR_SURFACE", "Status=" + status + "; Hit=" + rawHit + "; HitEntity=" + handle +
                "; Expected=" + chairHandle + "; Point=" + p + "; Normal=" + n + "; Flags=17; OptionFlags=0; ChairCollision=" + chair.IsCollisionEnabled);
            if (status != 2 || rawHit == 0 || handle != chairHandle || n.Z < 0.98f) throw Error("apoio final nao atingiu cadeira propria horizontal");
            GTA.Math.Vector3 local = chair.GetPositionOffset(p);
            if (local.X < minimum.X || local.X > maximum.X || local.Y < minimum.Y || local.Y > maximum.Y ||
                local.Z < minimum.Z - 0.02f || local.Z > maximum.Z + 0.02f) throw Error("hit fora dos bounds reais");
            currentSurface = p; surfaceLocal = local; ray = 0; return true;
        }
        finally
        {
            Free(hit); Free(point); Free(normal); Free(entity);
        }
    }

    private void CheckSeated(bool report)
    {
        GTA.Math.Vector3 pelvis = Bone(GTA.Bone.SkelPelvis), head = Bone(GTA.Bone.SkelHead);
        GTA.Math.Vector3 left = Bone(GTA.Bone.SkelLeftFoot), right = Bone(GTA.Bone.SkelRightFoot);
        GTA.Math.Vector3 surface = chair.GetOffsetPosition(surfaceLocal); Check(surface);
        // O par autor ancora a raiz nativa na cadeira real; SkelPelvis e centro
        // anatomico, nao a superficie de contato do assento. Nenhum setter XYZ.
        GTA.Math.Vector3 root = actor.Position; Check(root); Check(authoredBaseRoot);
        float dz = root.Z - surface.Z, xy = Horizontal(root - surface);
        float authoredRootDistance = (root - authoredBaseRoot).Length();
        float pelvisContactZ = pelvis.Z - surface.Z, pelvisContactXY = Horizontal(pelvis - surface);
        bool body = head.Z - pelvis.Z >= 0.3f && Bent(true) && Bent(false);
        bool leftValid = FootGround(left, false, report), rightValid = FootGround(right, false, report);
        bool feet = leftValid && rightValid;
        if (report || GTA.Game.GameTime >= nextLog)
        {
            nextLog = GTA.Game.GameTime + 500;
            Log("SENTAR_AUTOR_GEOMETRIA", "Root=" + root + "; AuthoredBaseRoot=" + authoredBaseRoot +
                "; RootVsAuthoredDistance=" + Number(authoredRootDistance) + "; Pelvis=" + pelvis + "; Head=" + head +
                "; Left=" + left + "; Right=" + right + "; Surface=" + surface + "; ContactXY=" + Number(xy) +
                "; ContactZ=" + Number(dz) + "; ContactReference=NativeActorRootAuthoredPair" +
                "; PelvisContactXY=" + Number(pelvisContactXY) + "; PelvisContactZ=" + Number(pelvisContactZ) +
                "; BodySeated=" + body + "; FeetGround=" + feet + "; LimitsXY=.05; LimitsZ=0..10cm; AuthoredRootLimit=.05; NoCorrection=true");
        }
        if (!body || !feet || authoredRootDistance > 0.05f || xy > 0.05f || dz < 0f || dz > 0.10f)
            throw Error("raiz nativa do par autor/corpo/pes nao cabem no apoio real; nao aplicar correcao Z");
    }
    private void ValidateAuthoredStartFeet()
    {
        GTA.Math.Vector3 delta = entryStart - actor.Position;
        foreach (GTA.Bone bone in new GTA.Bone[] { GTA.Bone.SkelLeftFoot, GTA.Bone.SkelRightFoot })
        {
            GTA.Math.Vector3 point = Bone(bone) + delta;
            if (!FootGround(point, true, true)) throw Error("inicio authored nao corresponde a corpo ereto apoiado no piso real");
        }
    }
    private void RequireStanding(bool report)
    {
        GTA.Math.Vector3 pelvis = Bone(GTA.Bone.SkelPelvis), head = Bone(GTA.Bone.SkelHead);
        GTA.Math.Vector3 left = Bone(GTA.Bone.SkelLeftFoot), right = Bone(GTA.Bone.SkelRightFoot);
        bool body = head.Z - pelvis.Z >= 0.3f && pelvis.Z - System.Math.Min(left.Z, right.Z) >= 0.45f && (!Bent(true) || !Bent(false));
        bool leftValid = FootGround(left, true, report), rightValid = FootGround(right, true, report);
        bool feet = leftValid && rightValid;
        if (report) Log("SENTAR_AUTOR_ERETO", "Root=" + actor.Position + "; Pelvis=" + pelvis + "; Head=" + head +
            "; BodyStanding=" + body + "; FeetGround=" + feet);
        if (!body || !feet) throw Error("inicio precisa ficar ereto e com pes no piso real");
    }
    private bool FootGround(GTA.Math.Vector3 foot, bool standing, bool report)
    {
        float ground; GTA.Math.Vector3 query = foot + new GTA.Math.Vector3(0f, 0f, 0.5f);
        bool found = GTA.World.GetGroundHeightExcludingProps(query, out ground, GTA.GetGroundHeightMode.Normal);
        // Mesmo limite de piso real do inicio ereto; .55m flutuante da77 e rejeitado.
        bool valid = found && Finite(ground) && foot.Z >= ground - 0.08f && foot.Z <= ground + 0.35f;
        if (report) Log("SENTAR_AUTOR_PE", "Standing=" + standing + "; Query=" + query + "; Found=" + found +
            "; Ground=" + Number(ground) + "; Foot=" + foot + "; Diff=" + Number(foot.Z - ground) + "; Valid=" + valid);
        return valid;
    }
    private bool Bent(bool left)
    {
        GTA.Math.Vector3 hip = Bone(left ? GTA.Bone.SkelLeftThigh : GTA.Bone.SkelRightThigh);
        GTA.Math.Vector3 knee = Bone(left ? GTA.Bone.SkelLeftCalf : GTA.Bone.SkelRightCalf);
        GTA.Math.Vector3 foot = Bone(left ? GTA.Bone.SkelLeftFoot : GTA.Bone.SkelRightFoot);
        GTA.Math.Vector3 thigh = knee - hip, calf = foot - knee;
        if (thigh.Length() < 0.05f || calf.Length() < 0.05f) throw Error("segmentos reais da perna indisponiveis");
        return GTA.Math.Vector3.Dot(thigh / thigh.Length(), calf / calf.Length()) < 0.86f;
    }
    private GTA.Math.Vector3 Bone(GTA.Bone bone)
    {
        GTA.EntityBone b = actor.Bones[bone]; if (!b.IsValid) throw Error("osso real indisponivel: " + bone);
        GTA.Math.Vector3 p = b.Position; Check(p); return p;
    }
    private GTA.Math.Vector3 Initial(string clip)
    {
        return GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ANIM_INITIAL_OFFSET_POSITION,
            Dictionary, clip, chairOrigin.X, chairOrigin.Y, chairOrigin.Z, chairRotation.X, chairRotation.Y, chairRotation.Z, 0f, 2);
    }
    private float Duration(string clip)
    {
        float value = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, Dictionary, clip);
        if (!Finite(value) || value <= 0f || value > 180f) throw Error("clip autor sem duracao nativa valida: " + clip);
        return value * 1000f;
    }
    private void CheckWindow(int elapsed) { if (elapsed - startMs >= windowMs) throw Error("par/contato nao concluiu dentro da janela original"); }
    private void RequireActor()
    {
        if (actor == null || !actor.Exists() || ownsActor == null || !ownsActor(actor) || actor.Handle != actorHandle ||
            actor.Model.Hash != actorModel || actor.MemoryAddress != actorAddress || actor.IsDead || actor.Health != health ||
            actor.IsInVehicle() || actor.IsRagdoll || actor.IsInWater || actor.IsInAir) throw Error("identidade/vida/apoio fisico da atriz mudou");
    }
    private void RequireChair(bool frozen)
    {
        if (chair == null || !chair.Exists() || objects == null || !objects.IsReady || objects.GetProp(chairCode).Handle != chairHandle ||
            chair.Handle != chairHandle || chair.Model.Hash != chairHash || chair.MemoryAddress != chairAddress || !chair.IsVisible ||
            !chair.IsCollisionEnabled || chair.IsAttached() || (frozen && !chair.IsPositionFrozen)) throw Error("cadeira propria perdeu identidade/colisao/estado");
    }
    public bool ReleaseActorsForReset()
    {
        if (disposed) return true;
        if (OwnActor()) { actor.Task.ClearAllImmediately(); actor.IsPositionFrozen = false; actor.Task.StandStill(-1); }
        if (OwnChair())
        {
            string clip = baseRequested ? "base_chair" : "enter_chair";
            bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, chairHandle, Dictionary, clip, 3);
            if (playing && !chair.StopSynchronizedAnim(new GTA.AnimationBlendDelta(4f), true)) return false;
            chair.IsPositionFrozen = true; chair.IsCollisionEnabled = true;
            if (!chair.IsCollisionEnabled) return false;
        }
        scene = null; running = ready = false; ray = 0;
        // Depois, chamador Objects.Reset restaura somente sua cadeira; nenhuma reposicao da atriz aqui.
        return true;
    }
    // Chamar somente depois de Actors/Objects.Reset do Diretor; o helper nunca repoe ator.
    public bool Reset()
    {
        Active();
        if (!prepared || running || objects == null || !objects.IsReady) throw Error("restaure elenco/objetos antes deste reset");
        RequireActor(); RequireChair(true);
        if (!resetPending)
        {
            if ((chair.Position - chairOrigin).Length() > 0.01f || (chair.Rotation - chairRotation).Length() > 0.01f)
                throw Error("cadeira nao retornou a marca propria original antes do reset");
            ready = finished = baseRequested = observed = phasePendingLogged = false;
            stage = surfaceId = ray = 0; lastElapsed = -1; maximumPhase = 0f;
            nextLoadingLog = 0; surfaceMeasured = false; measuredSurfaceNormal = GTA.Math.Vector3.Zero;
            actor.Task.StandStill(-1); resetAt = GTA.Game.GameTime; resetPending = true;
            loading = System.Diagnostics.Stopwatch.StartNew(); return false;
        }
        if (unchecked((uint)(GTA.Game.GameTime - resetAt)) < 250u) return false;
        PollLoading();
        if (!IsReady) return false;
        resetPending = false; return true;
    }
    public bool Cleanup()
    {
        if (disposed) return true;
        if (!ReleaseActorsForReset()) return false;
        if (requested && !dictionaryWasLoaded) GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT, Dictionary);
        prepared = false; requested = false; actor = null; chair = null; objects = null;
        return true;
    }
    public void Dispose() { if (!Cleanup()) throw Error("liberacao propria pendente; preservar recursos para nova limpeza"); disposed = true; }
    private bool OwnActor() { return actor != null && actor.Exists() && actor.Handle == actorHandle && actor.Model.Hash == actorModel && actor.MemoryAddress == actorAddress && ownsActor != null && ownsActor(actor); }
    private bool OwnChair() { return chair != null && chair.Exists() && chair.Handle == chairHandle && chair.Model.Hash == chairHash && chair.MemoryAddress == chairAddress; }
    private void VerifySources()
    {
        string props = System.IO.Path.Combine(menyoo ?? "", "PropList.txt"), anims = System.IO.Path.Combine(menyoo ?? "", "PedAnimList.txt");
        VerifyFile(props, "35BFD924919FC223F5AB131941643B2AE4E0082FCD0ECEF1A5A50FCDEE9B2552");
        string[] lines = System.IO.File.ReadAllLines(props);
        if (lines.Length < 2266 || lines[2265].Trim() != ChairModel) throw Error("modelo diverge da origem instalada");
        VerifyFile(anims, "2C537060BD97FCF0DACA407DB1436A5D54E4B8DD5274DBC9EF0D8466EF6F21E4");
        lines = System.IO.File.ReadAllLines(anims);
        foreach (var pair in new System.Collections.Generic.Dictionary<int, string> { {166553,"enter"}, {166559,"enter_chair"}, {166551,"base"}, {166563,"base_chair"} })
            if (lines.Length < pair.Key || lines[pair.Key - 1].Trim() != Dictionary + " " + pair.Value) throw Error("track diverge da origem instalada: " + pair.Value);
    }
    private static void VerifyFile(string path, string expected)
    { using (var sha = System.Security.Cryptography.SHA256.Create()) if (System.BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(path))).Replace("-", "") != expected) throw Error("hash da origem instalada mudou: " + path); }
    private void Active() { if (disposed) throw new System.ObjectDisposedException("EntradaSentadaAutorDiretor"); }
    private void Log(string kind, string data) { if (log != null) log(kind, data); }
    private static System.InvalidOperationException Error(string value) { return new System.InvalidOperationException("Sentar autor isolado80: " + value); }
    private static string Number(float value) { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static void Check(GTA.Math.Vector3 p) { if (!Finite(p.X) || !Finite(p.Y) || !Finite(p.Z)) throw Error("getter geometrico nao finito"); }
    private static float Horizontal(GTA.Math.Vector3 p) { return (float)System.Math.Sqrt(p.X * p.X + p.Y * p.Y); }
    private static float Angle(float value) { value %= 360f; if (value > 180f) value -= 360f; if (value < -180f) value += 360f; return System.Math.Abs(value); }
    private static System.IntPtr Buffer()
    { System.IntPtr value = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(24); System.Runtime.InteropServices.Marshal.Copy(new byte[24], 0, value, 24); return value; }
    private static void Free(System.IntPtr value) { if (value != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(value); }
    private static GTA.Math.Vector3 ReadVector(System.IntPtr p)
    { float[] values = new float[6]; System.Runtime.InteropServices.Marshal.Copy(p, values, 0, 6); return new GTA.Math.Vector3(values[0], values[2], values[4]); }
}
