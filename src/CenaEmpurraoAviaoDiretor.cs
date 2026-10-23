// Inserto EP07_C05: braço real em contato medido, seguido na edição pela queda real do EP01.
// Não expulsa passageiros nem apresenta LeaveVehicle como um empurrão.
public sealed class CenaEmpurraoAviaoDiretor : System.IDisposable
{
    private readonly System.Action<string, string> log;
    private readonly string animSource;
    private CenaQuedaDiretor flight;
    private GTA.Ped chef, djanira;
    private int chefHandle, djaniraHandle, chefModel, djaniraModel;
    private System.IntPtr chefAddress, djaniraAddress;
    private bool prepared, ready, resetDone, running, started, contactObserved, completed, failed, disposed;
    private bool dictRequested, dictWasLoaded;
    private int startedAt, previousElapsed = -1, nextLog;
    private float clipDuration, maxPhase, initialLeftDistance, initialRightDistance;
    private System.Diagnostics.Stopwatch loading;
    private const string Dictionary = "melee@unarmed@streamed_core", Clip = "shove";
    private const string SourceSHA256 = "2C537060BD97FCF0DACA407DB1436A5D54E4B8DD5274DBC9EF0D8466EF6F21E4";
    private const int SourceLine = 61481, DurationMs = 5000;

    public CenaEmpurraoAviaoDiretor(System.Action<string, string> logger, string installedPedAnimListPath)
    { log = logger; animSource = installedPedAnimListPath; }
    public bool IsReady { get { return prepared && ready && !failed && !disposed; } }
    public bool IsFinished { get { return IsReady && completed && contactObserved && flight.IsSuccess; } }
    public bool UsesOwnCamera { get { return true; } }
    public bool ControlsBody(string actorId, int elapsedMs)
    { return IsReady && elapsedMs >= 0 && elapsedMs < DurationMs && (actorId == "O_CHEFE" || actorId == "DJANIRA_POBRE"); }
    public GTA.Entity[] OwnedEntities { get { return flight == null ? new GTA.Entity[0] : flight.OwnedEntities; } }
    public int[] OwnedHandles { get { return flight == null ? new int[0] : flight.OwnedHandles; } }

    public void Prepare(System.Collections.Generic.Dictionary<string, GTA.Ped> ownRoster, CenaQuedaDiretor.QuedaSpec realWaterSpec)
    {
        Active(); if (prepared || ownRoster == null || ownRoster.Count != 3 || !ownRoster.ContainsKey("O_CHEFE") ||
            !ownRoster.ContainsKey("DJANIRA_POBRE") || !ownRoster.ContainsKey("DINJER") || realWaterSpec == null || realWaterSpec.Local != "CANAL_LS")
            throw Error("inserto exige os três figurinos próprios e marca realCANAL_LS");
        VerifyAnimationSource();
        chef = ownRoster["O_CHEFE"]; djanira = ownRoster["DJANIRA_POBRE"];
        Capture(chef, out chefHandle, out chefAddress, out chefModel);
        Capture(djanira, out djaniraHandle, out djaniraAddress, out djaniraModel);
        if (chefHandle == djaniraHandle) throw Error("Chefe e Djanira precisam ser entidades distintas");
        if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ANIM_DICT_EXIST, Dictionary)) throw Error("dicionário real do empurrão ausente");
        dictWasLoaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, Dictionary);
        GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, Dictionary); dictRequested = true;
        flight = new CenaQuedaDiretor(log); prepared = true; loading = System.Diagnostics.Stopwatch.StartNew();
        flight.Prepare(djanira, new CenaQuedaDiretor.QuedaSpec { Cena = "EP07_C05", Local = "CANAL_LS", Agua = realWaterSpec.Agua,
            Direcao = realWaterSpec.Direcao, AlturaRelativa = 35f, VelocidadeAviao = 35f, DuracaoSolicitadaMs = DurationMs,
            SomenteVoo = true, InsertoEmpurrao = true, PilotoDoElenco = chef, PassageiroTraseiro = ownRoster["DINJER"],
            InverterEixo = realWaterSpec.InverterEixo, CompletarFuga = false, TimeoutMs = 60000 });
        Log("EMPURRAO_AVIAO_PREPARADO", "Modelo=velum; três assentos reais; inserto5s; gesto shove fonte61481; contato e imagem ainda não testados");
    }
    public void PollLoading()
    {
        Active(); if (!prepared || failed) throw Error("preparo indisponível"); if (ready) return;
        try
        {
            RequireActors(); if (loading.ElapsedMilliseconds >= 15000) throw Error("avião/gesto não ficou pronto em15s"); flight.PollLoading();
            if (!flight.IsReady || !GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, Dictionary)) return;
            clipDuration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, Dictionary, Clip);
            if (!Finite(clipDuration) || clipDuration <= 0f || clipDuration * 1000f > DurationMs + 1f)
                throw Error("clipe deve terminar inteiro dentro dos5s reservados; duração=" + Number(clipDuration));
            ready = true; Log("EMPURRAO_AVIAO_RECURSOS_PRONTOS", "DuraçãoClip=" + Number(clipDuration) + "; sem comprovar contato por tempo");
        }
        catch { failed = true; ready = false; throw; }
    }
    public bool Reset()
    {
        RequireReady(); StopClip(); running = false; resetDone = false;
        if (!flight.Reset()) return false;
        started = contactObserved = completed = false; maxPhase = 0f; previousElapsed = -1; nextLog = 0; resetDone = true;
        return true;
    }
    public bool ReleaseActorsForReset()
    {
        Active(); StopClip(); running = false; resetDone = false;
        return flight == null || flight.ReleaseActorsForReset();
    }
    public void Start()
    {
        RequireReady(); if (!resetDone || running) throw Error("reinicie fora da captura antes de iniciar");
        RequireActors(); flight.Start(); running = true;
    }
    public void Update(int elapsedMs)
    {
        RequireReady(); if (completed) return;
        if (!running || elapsedMs < 0 || elapsedMs < previousElapsed || elapsedMs > DurationMs + 500) throw Error("relógio fora do inserto5s");
        try
        {
            RequireActors(); previousElapsed = elapsedMs; flight.Update(elapsedMs);
            bool startedNow = false;
            if (!started)
            {
                if (DurationMs - elapsedMs + 1f < clipDuration * 1000f) throw Error("início atrasado não permite gesto inteiro");
                initialLeftDistance = HandShoulderDistance(GTA.Bone.SkelLeftHand);
                initialRightDistance = HandShoulderDistance(GTA.Bone.SkelRightHand);
                // Secondary preserva a pilotagem normal, as posições e os assentos do avião real.
                GTA.Native.Function.Call(GTA.Native.Hash.TASK_PLAY_ANIM, chefHandle, Dictionary, Clip, 4f, -4f, DurationMs,
                    (int)(GTA.AnimationFlags.UpperBodyOnly | GTA.AnimationFlags.Secondary | GTA.AnimationFlags.StayInEndFrame), 0f, false, false, false);
                startedAt = GTA.Game.GameTime; started = startedNow = true;
            }
            if (!startedNow) PollContact(elapsedMs);
            if (elapsedMs < DurationMs) return;
            uint since = unchecked((uint)(GTA.Game.GameTime - startedAt));
            if (!contactObserved || maxPhase < 0.9f || since + 1f < clipDuration * 1000f || !flight.IsSuccess)
                throw Error("inserto terminou sem gesto completo e contato real mão/ombro; não usar como empurrão");
            completed = true; running = false;
            Log("EMPURRAO_AVIAO_FINALIZADO", "ContatoGetter=True; gesto completo; avião continua voo; sem queda/LeaveVehicle nesta subtomada; corte para queda real separado");
        }
        catch { failed = true; running = false; throw; }
    }
    private void PollContact(int elapsedMs)
    {
        uint since = unchecked((uint)(GTA.Game.GameTime - startedAt));
        bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, chefHandle, Dictionary, Clip, 3);
        float phase = playing ? GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, chefHandle, Dictionary, Clip) : -1f;
        if ((!playing || phase == -1f) && since < 500u) return;
        bool finishedClip = since + 1f >= clipDuration * 1000f && maxPhase >= 0.9f;
        if ((!playing && !finishedClip) || playing && (!Finite(phase) || phase < 0f || phase > 1f))
            throw Error("clipe/fase real inválida: fase=" + Number(phase) + "; dicionário=" + Dictionary + "; clipe=" + Clip + "; desde_início_ms=" + since);
        if (playing) maxPhase = System.Math.Max(maxPhase, phase);
        float left = HandShoulderDistance(GTA.Bone.SkelLeftHand), right = HandShoulderDistance(GTA.Bone.SkelRightHand);
        // Distâncias relativas entre os ossos eliminam o avanço35m/s do avião da prova de movimento do braço.
        bool touching = playing && phase >= 0.05f && phase <= 0.95f &&
            (left <= 0.35f && initialLeftDistance - left >= 0.06f || right <= 0.35f && initialRightDistance - right >= 0.06f);
        if (touching && !contactObserved)
        {
            contactObserved = true;
            Log("EMPURRAO_AVIAO_CONTATO", "MãoEsquerdaDist=" + Number(left) + "; MãoDireitaDist=" + Number(right) + "; Fase=" + Number(phase) + "; TempoMs=" + elapsedMs + "; sem reposicionar atores");
        }
        if (elapsedMs >= nextLog)
        {
            nextLog = elapsedMs + 500;
            Log("EMPURRAO_AVIAO_GETTERS", "TempoMs=" + elapsedMs + "; Playing=" + playing + "; Fase=" + Number(phase) +
                "; DistE=" + Number(left) + "; DistD=" + Number(right) + "; AvançoRelE=" + Number(initialLeftDistance - left) + "; AvançoRelD=" + Number(initialRightDistance - right));
        }
    }
    private float HandShoulderDistance(GTA.Bone handBone)
    {
        GTA.Math.Vector3 hand = chef.Bones[handBone].Position;
        GTA.Math.Vector3 shoulder = djanira.Bones[GTA.Bone.SkelLeftClavicle].Position;
        GTA.Math.Vector3 arm = djanira.Bones[GTA.Bone.SkelLeftUpperArm].Position;
        Vector(hand); Vector(shoulder); Vector(arm);
        return System.Math.Min((hand - shoulder).Length(), (hand - arm).Length());
    }
    private void StopClip()
    {
        if (!started || !Own(chef, chefHandle, chefAddress, chefModel)) return;
        chef.Task.StopScriptedAnimationTask(new GTA.CrClipAsset(Dictionary, Clip)); started = false;
    }
    public void Cleanup()
    {
        if (disposed) return;
        if (!ReleaseActorsForReset()) throw Error("saída dos assentos pendente; continuar em próximoTick fora da filmagem");
        if (flight != null) flight.Dispose();
        if (dictRequested && !dictWasLoaded) GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT, Dictionary);
        flight = null; chef = djanira = null; prepared = ready = resetDone = running = false;
    }
    public void Dispose() { Cleanup(); disposed = true; }
    private void VerifyAnimationSource()
    {
        if (System.String.IsNullOrEmpty(animSource) || !System.IO.Path.IsPathRooted(animSource)) throw Error("exige PedAnimList instalada em caminho absoluto");
        string path = System.IO.Path.GetFullPath(animSource);
        if (path.StartsWith("\\\\", System.StringComparison.Ordinal) || path.Substring(2).IndexOf(':') >= 0 ||
            System.IO.Path.GetFileName(path) != "PedAnimList.txt") throw Error("origem local inválida");
        for (string current = path; !System.String.IsNullOrEmpty(current); current = System.IO.Path.GetDirectoryName(current))
            if ((System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0) throw Error("origem não pode atravessar links");
        using (var file = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
        {
            if (file.Length > 64L * 1024 * 1024) throw Error("lista excede64MB");
            using (var sha = System.Security.Cryptography.SHA256.Create())
                if (System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") != SourceSHA256) throw Error("SHA256 da animação difere da fonte conferida");
            file.Position = 0;
            using (var reader = new System.IO.StreamReader(file, new System.Text.UTF8Encoding(false, true), true))
            {
                string row = null; for (int index = 1; index <= SourceLine; index++) { row = reader.ReadLine(); if (row == null) break; }
                if (row == null || row.Trim() != Dictionary + " " + Clip) throw Error("par literal do empurrão difere da linha61481");
            }
        }
    }
    private static void Capture(GTA.Ped ped, out int handle, out System.IntPtr address, out int model)
    {
        if (System.Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsPlayer || ped.IsDead || !ped.IsPersistent || ped.IsInVehicle()) throw Error("forneça somente ator próprio vivo a pé");
        handle = ped.Handle; address = ped.MemoryAddress; model = ped.Model.Hash;
    }
    private static bool Own(GTA.Ped ped, int handle, System.IntPtr address, int model)
    { return !System.Object.ReferenceEquals(ped, null) && ped.Exists() && !ped.IsPlayer && ped.Handle == handle && ped.MemoryAddress == address && ped.Model.Hash == model; }
    private void RequireActors()
    { if (!Own(chef, chefHandle, chefAddress, chefModel) || !Own(djanira, djaniraHandle, djaniraAddress, djaniraModel) || chef.IsDead || djanira.IsDead) throw Error("identidade/vida do elenco mudou"); }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static void Vector(GTA.Math.Vector3 value) { if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Z)) throw Error("osso real forneceu posição inválida"); }
    private static string Number(float value) { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private void RequireReady() { Active(); if (!IsReady) throw Error("aguarde recursos e avião reais"); }
    private void Active() { if (disposed) throw new System.ObjectDisposedException("CenaEmpurraoAviaoDiretor"); }
    private static System.InvalidOperationException Error(string text) { return new System.InvalidOperationException("Inserto empurrãoEP07: " + text); }
    private void Log(string tag, string text) { if (log != null) log(tag, text); }
}
