// Operação local entre tomadas: entrada JSON preservada, deslocamentos relativos e nenhum teleporte.
public sealed class OperacaoLocalDiretor : System.IDisposable
{
    private sealed class Command
    {
        internal string Id, Action;
        internal float Front, Side, Speed, Delta, CameraHeading, Pitch;
        internal int TimeoutMs;
    }
    private sealed class Token
    {
        internal string Text;
        internal bool IsString;
    }
    private sealed class TerrainSample
    {
        internal int Index, OffsetX, OffsetY;
        internal GTA.Math.Vector3 Point, Normal;
        internal float ProbeZ, GroundNative, GroundNormal, Water;
        internal float GroundTopProbeZ, GroundTopZ60;
        internal bool GroundTopZ60Result;
        internal bool GroundNativeResult, GroundNormalResult, WaterResult;
        internal float WaterNoWaves;
        internal GTA.Math.Vector3 WaterProbeAllPoint;
        internal bool WaterDiagnostics, WaterNoWavesResult, WaterProbeAllResult;
        internal bool RoadQueried, RoadFound, RoadOutputValid;
        internal GTA.Math.Vector3 RoadPosition;
        internal float RoadHeading;
    }
    private sealed class RoadNodeSample
    {
        internal int Nth, Handle, Lanes, Flags;
        internal bool Found, LoadedBefore, LoadedAfter, OutputValid, WithinRange, Duplicate, Accepted, SwitchedOff, GpsAllowed;
        internal GTA.Math.Vector3 Position;
        internal float Heading, Distance3D;
        internal string FlagsName;
    }
    private string roadSurveyId;
    private GTA.Math.Vector3 roadOrigin;
    private int roadNextNth;
    private bool roadAreaLoaded;
    private System.Collections.Generic.List<RoadNodeSample> roadSamples;
    private System.Collections.Generic.HashSet<int> roadSeenHandles;

    private sealed class DoorState
    {
        internal GTA.Prop Prop;
        internal int Handle, Model, SystemId, OriginalState;
        internal System.IntPtr Address;
        internal GTA.Math.Vector3 Position, Rotation, HitPoint, Normal;
        internal string ModelName;
        internal float OriginalRatio, Ratio;
        internal bool Added, OriginalSaved, Modified, PhysicsLoaded;
        internal int State, FrontNextDiagnosticMs;
        internal uint FrontRequestTick, FrontForceIssuedTick;
        internal int FrontForceFrames;
        internal bool FrontForceSettling;
    }
    private readonly string inbox, results;
    private readonly System.Action<string, string> log;
    private readonly System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
    private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;
    private string lastContentHash;
    private bool disposed, pollStarted, taskIssued, taskObserved, stopping;
    private uint lastPoll, startedAt, issuedAt, stopStartedAt;
    private string pendingState, pendingMessage;
    private Command active;
    private GTA.Ped actor;
    private int actorHandle, actorModel, quietSamples;
    private System.IntPtr actorAddress;
    private GTA.Math.Vector3 destination;
    private float targetHeading;
    private string targetId;
    private Command deferredStatus;
    private int terrainCalls, nextProbe;
    private GTA.Math.Vector3 probeOrigin;
    private string probeId;
    private System.Collections.Generic.List<TerrainSample> probeSamples;
    private readonly string propListFile;
    private readonly System.Collections.Generic.Dictionary<int, string> doorModels = new System.Collections.Generic.Dictionary<int, string>();
    private DoorState trackedDoor;
    private int doorRay, doorStage;
    private string doorId;
    private bool doorModelsRead, doorRestorePending;

    public OperacaoLocalDiretor(string dataRoot, System.Action<string, string> logger, string installedPropList = null)
    {
        if (System.String.IsNullOrWhiteSpace(dataRoot) || !System.IO.Path.IsPathRooted(dataRoot))
            throw new System.ArgumentException("Operação local: informe a pasta absoluta do runtime.");
        string root = System.IO.Path.GetFullPath(dataRoot);
        inbox = System.IO.Path.Combine(root, "operacao_local.json");
        results = System.IO.Path.Combine(root, "logs"); log = logger;
        propListFile = installedPropList ?? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(GTA.Script).Assembly.Location), "menyooStuff", "PropList.txt");
    }

    public bool IsBusy { get { return active != null || doorRestorePending; } }

    // O main também deve impedir montar/marcar/navegar enquanto IsBusy; não chama Wait nem bloqueia o Tick.
    public void Update(bool allow)
    {
        if (disposed) return;
        terrainCalls = 0; // Update é chamado uma vez por Tick; não depende de variações de GET_GAME_TIMER.
        try
        {
            GTA.Ped player = GTA.Game.Player.Character;
            bool permitted = allow && PlayerUsable(player) && !GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS);
            if ((!permitted || doorRestorePending) && trackedDoor != null) CleanupDoors();
            if (deferredStatus != null)
            {
                Command waiting = deferredStatus; deferredStatus = null;
                if (permitted) RespondStatus(waiting, player);
                else Respond(waiting, "rejeitada", "Guarda da cena/jogador/modo história mudou antes dos getters", 0);
            }
            if (active != null) Advance(permitted, player);
            uint now = unchecked((uint)GTA.Game.GameTime);
            if (pollStarted && unchecked(now - lastPoll) < 250) return;
            pollStarted = true; lastPoll = now;
            if (!System.IO.File.Exists(inbox)) return;
            if ((System.IO.File.GetAttributes(inbox) & System.IO.FileAttributes.ReparsePoint) != 0)
                throw Error("a entrada não pode ser vínculo");
            byte[] bytes;
            using (var input = new System.IO.FileStream(inbox, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                if (input.Length < 2 || input.Length > 4096) throw Error("JSON deve ter entre2 e4096bytes");
                bytes = new byte[(int)input.Length]; int read = 0;
                while (read < bytes.Length)
                {
                    int count = input.Read(bytes, read, bytes.Length - read);
                    if (count == 0) throw Error("a entrada mudou durante a leitura");
                    read += count;
                }
            }
            string hash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                hash = System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            if (hash == lastContentHash) return;
            lastContentHash = hash;
            string text = new System.Text.UTF8Encoding(false, true).GetString(bytes);
            if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
            Command command = Parse(text);
            if (seen.Contains(command.Id) || System.IO.File.Exists(ResultPath(command.Id)) || System.IO.File.Exists(ReceiptPath(command.Id))) return;
            seen.Add(command.Id);
            if (!permitted)
            { Respond(command, "rejeitada", "Cena deve estar limpa; jogador vivo/a pé/descongelado; somente modo história", 0); return; }
            Execute(command, player);
        }
        catch (System.Exception ex)
        {
            Log("OPERACAO_LOCAL_ERRO", ex.Message);
            if (active != null)
            {
                try { Finish("erro", "Falha técnica: " + ex.Message, (int)unchecked((uint)GTA.Game.GameTime - startedAt), true); }
                catch (System.Exception finalError) { active = null; taskIssued = false; actor = null; Log("OPERACAO_LOCAL_ERRO_FINAL", finalError.Message); }
            }
        }
    }

    private void Execute(Command command, GTA.Ped player)
    {
        if (command.Action == "status") { RespondStatus(command, player); return; }
        if (command.Action == "cancelar")
        {
            Cancel(); Respond(command, "concluida", IsBusy ? "Cancelamento solicitado; aguardando parada física" : "Operação própria cancelada; entrada preservada", 0); return;
        }
        if (active != null) { Respond(command, "rejeitada", "Já existe operação física em andamento", 0); return; }
        if (command.Action == "vias") { BeginRoadSurvey(command, player); return; }
        if (command.Action == "abrirPorta")
        {
            ReadDoorModels();
            if (!CleanupDoors()) { Respond(command, "rejeitada", "Restauração da porta anterior ainda não confirmou; nenhuma porta nova alterada", 0); return; }
            var doorReceipt = new System.Text.StringBuilder("{\"versao\":1,\"id\":"); Quote(doorReceipt, command.Id);
            doorReceipt.Append(",\"acao\":\"abrirPorta\",\"estado\":\"aceita\",\"origem\":"); VectorJson(doorReceipt, player.Position);
            doorReceipt.Append(",\"dataUtc\":"); Quote(doorReceipt, System.DateTimeOffset.UtcNow.ToString("o", Invariant)); doorReceipt.Append('}');
            WriteNew(ReceiptPath(command.Id), doorReceipt.ToString());
            active = command; actor = player; actorHandle = player.Handle; actorAddress = player.MemoryAddress; actorModel = player.Model.Hash;
            startedAt = unchecked((uint)GTA.Game.GameTime); taskIssued = false; stopping = false; doorRay = 0; doorStage = 0; doorId = command.Id;
            Log("OPERACAO_LOCAL_PORTA_INICIADA", "Id=" + command.Id + "; ray relativo real de 1.8m; jogador/tarefas/posição preservados");
            return;
        }
        if (command.Action == "olhar")
        {
            if (!GTA.GameplayCamera.IsRendering)
            { Respond(command, "rejeitada", "A câmera gameplay não está renderizando", 0); return; }
            GTA.GameplayCamera.ForceRelativeHeadingAndPitch(command.CameraHeading, command.Pitch, 1f);
            Respond(command, "concluida", "Orientação solicitada à câmera gameplay; getters registrados", 0); return;
        }
        if (command.Action == "sondar")
        {
            var probeReceipt = new System.Text.StringBuilder("{\"versao\":1,\"id\":"); Quote(probeReceipt, command.Id);
            probeReceipt.Append(",\"acao\":\"sondar\",\"estado\":\"aceita\",\"origem\":"); VectorJson(probeReceipt, player.Position);
            probeReceipt.Append(",\"dataUtc\":"); Quote(probeReceipt, System.DateTimeOffset.UtcNow.ToString("o", Invariant)); probeReceipt.Append('}');
            WriteNew(ReceiptPath(command.Id), probeReceipt.ToString());
            active = command; actor = player; actorHandle = player.Handle; actorAddress = player.MemoryAddress; actorModel = player.Model.Hash;
            startedAt = unchecked((uint)GTA.Game.GameTime); taskIssued = false; taskObserved = false; stopping = false;
            probeOrigin = player.Position; probeId = command.Id; nextProbe = 0;
            probeSamples = new System.Collections.Generic.List<TerrainSample>(81);
            Log("OPERACAO_LOCAL_SONDAGEM_INICIADA", "Id=" + command.Id + "; origem=" + probeOrigin +
                "; XY do mundo ±20m/passo5m; 81 pontos; seis consultas por ponto, um ponto por frame; máximo6 consultas de terreno/água; somente getters");
            return; // A primeira amostra acontece em outro Tick; não altera tarefa/posição/câmera.
        }
        GTA.ScriptTaskNameHash currentName; GTA.ScriptTaskStatus currentStatus;
        player.GetCurrentScriptTaskNameHashAndStatus(out currentName, out currentStatus);
        if (!TaskInactive(currentStatus))
        { Respond(command, "rejeitada", "Já existe tarefa de script ativa: " + currentName + "/" + currentStatus + "; tarefa preservada", 0); return; }
        var receipt = new System.Text.StringBuilder("{\"versao\":1,\"id\":"); Quote(receipt, command.Id);
        receipt.Append(",\"acao\":"); Quote(receipt, command.Action);
        receipt.Append(",\"estado\":\"aceita\",\"origem\":"); VectorJson(receipt, player.Position);
        receipt.Append(",\"dataUtc\":"); Quote(receipt, System.DateTimeOffset.UtcNow.ToString("o", Invariant)); receipt.Append('}');
        WriteNew(ReceiptPath(command.Id), receipt.ToString()); // Persistir antes da primeira tarefa impede repetir deslocamento relativo após recarga.
        active = command; targetId = command.Id; actor = player; actorHandle = player.Handle; actorAddress = player.MemoryAddress; actorModel = player.Model.Hash;
        startedAt = unchecked((uint)GTA.Game.GameTime); taskIssued = false; taskObserved = false;
        if (command.Action == "virar")
            targetHeading = Normalize(player.Heading + command.Delta);
        else
            destination = player.GetOffsetPosition(new GTA.Math.Vector3(command.Side, command.Front, 0f));
        Log("OPERACAO_LOCAL_INICIADA", "Id=" + command.Id + "; Acao=" + command.Action + "; origem=" + player.Position + "; destino=" + destination);
        Advance(true, player);
    }

    private void Advance(bool permitted, GTA.Ped player)
    {
        if (active == null) return;
        int elapsed = (int)unchecked((uint)GTA.Game.GameTime - startedAt);
        if (active.Action == "sondar") { AdvanceProbe(permitted, player, elapsed); return; }
        if (active.Action == "vias") { AdvanceRoadSurvey(permitted, player, elapsed); return; }
        if (active.Action == "abrirPorta") { AdvanceDoor(permitted, player, elapsed); return; }
        if (stopping) { AdvanceStop(player, elapsed); return; }
        if (!permitted || !Owned(player))
        { Finish("cancelada", "Guarda do main/identidade/estado do jogador mudou", elapsed, true); return; }
        if (!taskIssued)
        {
            if (!TaskInactive(actor.CurrentScriptTaskStatus))
            { Finish("erro", "Uma tarefa de script surgiu durante o preparo; tarefa preservada", elapsed, false); return; }
            if (elapsed >= active.TimeoutMs)
            { Finish("erro", "Preparo ultrapassou o prazo; nenhuma tarefa nova enviada", elapsed, false); return; }
            if (active.Action == "andar")
            {
                if (terrainCalls >= 6) return; // Uma leitura status/sondar já consumiu o orçamento deste Tick.
                GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, destination.X, destination.Y, destination.Z);
                float ground;
                terrainCalls++;
                GTA.Math.Vector3 groundProbe = new GTA.Math.Vector3(destination.X, destination.Y, actor.Position.Z + 0.25f);
                if (!GTA.World.GetGroundHeight(groundProbe, out ground, GTA.GetGroundHeightMode.Normal))
                {
                    if (elapsed >= active.TimeoutMs) Finish("erro", "Chão/colisão não disponíveis no limite; sem teleporte", elapsed, false);
                    return;
                }
                if (!Finite(ground) || System.Math.Abs(ground - actor.Position.Z) > 3f)
                { Finish("erro", "Destino relativo não tem chão próximo à altura atual; sem teleporte", elapsed, false); return; }
                destination.Z = ground + 1f;
                if (Distance(actor.Position, destination) <= 0.35f && System.Math.Abs(actor.Position.Z - destination.Z) <= 1f)
                { Finish("concluida", "Jogador já está no destino; nenhuma tarefa enviada", elapsed, false); return; }
                GTA.Math.Vector3 delta = destination - actor.Position;
                targetHeading = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_HEADING_FROM_VECTOR_2D, delta.X, delta.Y);
                actor.Task.GoStraightTo(destination, -1, new GTA.PedMoveBlendRatio(active.Speed), targetHeading, 0.20f);
            }
            else
            {
                if (HeadingDifference(actor.Heading, targetHeading) <= 2f)
                { Finish("concluida", "Jogador já está na direção; nenhuma tarefa enviada", elapsed, false); return; }
                GTA.Native.Function.Call(GTA.Native.Hash.TASK_ACHIEVE_HEADING, actor.Handle, targetHeading, active.TimeoutMs);
            }
            taskIssued = true; issuedAt = unchecked((uint)GTA.Game.GameTime);
            return; // Getters da tarefa só a partir do próximo Tick, após o pedido nativo.
        }
        if (HasOwnTask(active)) taskObserved = true;
        bool reached = active.Action == "andar" ? Distance(actor.Position, destination) <= 0.35f && System.Math.Abs(actor.Position.Z - destination.Z) <= 1f :
            HeadingDifference(actor.Heading, targetHeading) <= 2f;
        if (reached && taskObserved) { Finish("concluida", "Destino/direção alcançados fisicamente", elapsed, true); return; }
        if (unchecked((uint)GTA.Game.GameTime - issuedAt) >= 500 && !HasOwnTask(active))
        { Finish("erro", "A tarefa própria terminou ou foi substituída; tarefa atual preservada", elapsed, false); return; }
        if (elapsed >= active.TimeoutMs) Finish("erro", "Timeout físico; nenhum teleporte ou mudança de posição foi aplicado", elapsed, true);
    }

    public void Cancel()
    {
        CleanupDoors();
        if (stopping) { pendingState = "cancelada"; pendingMessage = "Cancelamento solicitado durante a parada"; return; }
        if (active != null) Finish("cancelada", "Cancelamento solicitado", (int)unchecked((uint)GTA.Game.GameTime - startedAt), true);
    }

    private void Finish(string state, string message, int elapsed, bool stop)
    {
        if (active != null && active.Action == "abrirPorta" && state != "concluida" && !RestoreTrackedDoor())
        { state = "erro"; message += "; restauração da porta ainda pendente; outras portas preservadas"; }
        if (stopping) { Complete(state, message + "; parada não confirmada", elapsed); return; }
        try
        {
            if (stop && taskIssued && Owned(GTA.Game.Player.Character) && !actor.IsDead && HasOwnTask(active))
            {
                pendingState = state; pendingMessage = message;
                stopStartedAt = unchecked((uint)GTA.Game.GameTime); stopping = true; quietSamples = 0;
                // Teste27: 100ms terminou antes da desaceleração física; manter a primária até parar.
                actor.Task.StandStill(1000); // Substitui apenas a primária; nenhuma secondary/flag global é limpa.
                Log("OPERACAO_LOCAL_PARADA", "Id=" + active.Id + "; duração_ms=1000; aguardando getters entre frames; posição=" +
                    actor.Position + "; distânciaXY=" + Distance(actor.Position, destination).ToString("R", Invariant) +
                    "; velocidade=" + actor.Speed.ToString("R", Invariant));
                return;
            }
        }
        catch (System.Exception ex) { state = "erro"; message += "; parada falhou: " + ex.Message; }
        Complete(state, message, elapsed);
    }

    private void AdvanceStop(GTA.Ped player, int elapsed)
    {
        if (!Owned(player) || player.IsDead)
        { Complete("cancelada", "Identidade/vida mudou durante a parada; jogador atual preservado", elapsed); return; }
        uint stopElapsed = unchecked((uint)GTA.Game.GameTime - stopStartedAt);
        if (stopElapsed < 1000) return;
        GTA.ScriptTaskNameHash name; GTA.ScriptTaskStatus status;
        actor.GetCurrentScriptTaskNameHashAndStatus(out name, out status);
        if (TaskInactive(status) && actor.Speed <= 0.15f)
        {
            quietSamples++;
            if (quietSamples >= 2)
            {
                if (pendingState == "concluida" && active.Action == "andar" &&
                    (Distance(actor.Position, destination) > 0.35f || System.Math.Abs(actor.Position.Z - destination.Z) > 1f))
                    Complete("erro", "Jogador parou fora do destino; distânciaXY=" + Distance(actor.Position, destination).ToString("R", Invariant) +
                        "; destino/posição reais registrados; nenhuma correção por teleporte", elapsed);
                else Complete(pendingState, pendingMessage + "; parada física confirmada em dois frames, secondary preservada", elapsed);
                return;
            }
        }
        else quietSamples = 0;
        if (!TaskInactive(status) && name != GTA.ScriptTaskNameHash.StandStill && !HasOwnTask(active))
        { Complete("erro", "Outra tarefa surgiu durante a parada: " + name + "/" + status + "; tarefa preservada", elapsed); return; }
        if (stopElapsed >= 2000)
            Complete("erro", "Parada não confirmou estado quieto em2s: " + name + "/" + status + "; velocidade=" + actor.Speed.ToString("R", Invariant) + "; tarefas preservadas", elapsed);
    }

    private void Complete(string state, string message, int elapsed)
    {
        Command completed = active;
        active = null; taskIssued = false; stopping = false; quietSamples = 0;
        try { Respond(completed, state, message, elapsed); }
        finally
        {
            actor = null; actorHandle = 0; actorAddress = System.IntPtr.Zero;
            if (completed.Action == "sondar") { probeSamples = null; probeId = null; nextProbe = 0; }
            if (completed.Action == "vias") { roadSamples = null; roadSeenHandles = null; roadSurveyId = null; roadNextNth = 0; }
        }
    }

    // Vias: somente leitura do mapa carregado, a partir da posicao atual real.
    private void BeginRoadSurvey(Command command, GTA.Ped player)
    {
        GTA.ScriptTaskNameHash name; GTA.ScriptTaskStatus status;
        player.GetCurrentScriptTaskNameHashAndStatus(out name, out status);
        if (!TaskInactive(status) || player.Speed > 0.15f)
        { Respond(command, "rejeitada", "Consulta de vias exige jogador parado e sem tarefa de script; nenhuma tarefa alterada", 0); return; }
        GTA.Math.Vector3 origin = player.Position;
        if (!FiniteVector(origin)) throw Error("origem da consulta de vias invalida");
        var receipt = new System.Text.StringBuilder("{\"versao\":1,\"id\":"); Quote(receipt, command.Id);
        receipt.Append(",\"acao\":\"vias\",\"estado\":\"aceita\",\"origem\":"); VectorJson(receipt, origin);
        receipt.Append(",\"dataUtc\":"); Quote(receipt, System.DateTimeOffset.UtcNow.ToString("o", Invariant)); receipt.Append('}');
        WriteNew(ReceiptPath(command.Id), receipt.ToString());
        active = command; actor = player; actorHandle = player.Handle; actorAddress = player.MemoryAddress; actorModel = player.Model.Hash;
        startedAt = unchecked((uint)GTA.Game.GameTime); taskIssued = false; taskObserved = false; stopping = false;
        roadOrigin = origin; roadSurveyId = command.Id; roadNextNth = 1;
        roadSamples = new System.Collections.Generic.List<RoadNodeSample>(8);
        roadSeenHandles = new System.Collections.Generic.HashSet<int>();
        roadAreaLoaded = GTA.PathFind.ArePathNodesLoadedForArea(
            new GTA.Math.Vector2(origin.X - 100f, origin.Y - 100f), new GTA.Math.Vector2(origin.X + 100f, origin.Y + 100f));
        Log("OPERACAO_LOCAL_VIAS_INICIADA", "Id=" + command.Id + "; Origem=" + origin +
            "; nth1..8; raio3D100m; flags0; pesoZ0/tolerancia0; umNoPorTick; sem streaming/teleporte/marca");
    }

    private void AdvanceRoadSurvey(bool permitted, GTA.Ped player, int elapsed)
    {
        if (!permitted || !Owned(player) || player.Speed > 0.15f ||
            (player.Position - roadOrigin).Length() > 0.25f || !TaskInactive(player.CurrentScriptTaskStatus))
        { Finish("cancelada", "Guarda/identidade/parada/origem mudou durante a consulta de vias; amostras preservadas", elapsed, false); return; }
        if (elapsed >= active.TimeoutMs)
        { Finish("erro", "Consulta de oito nos nao terminou em5s; sem repetir ou carregar mapa", elapsed, false); return; }
        var sample = new RoadNodeSample { Nth = roadNextNth };
        float heading; int lanes;
        GTA.PathNode node = GTA.PathFind.GetNthClosestVehicleNodeWithHeading(
            roadOrigin, roadNextNth, out heading, out lanes, GTA.GetClosestVehicleNodeFlags.None, 0f, 0f);
        sample.Found = !System.Object.ReferenceEquals(node, null);
        if (sample.Found)
        {
            sample.Handle = node.Handle;
            sample.LoadedBefore = node.IsLoaded;
            if (sample.Handle != 0 && sample.LoadedBefore)
            {
                // Os wrappers PathNode da API191 verificam a regiao carregada; nao usar os getters nativos crus por handle.
                sample.Position = node.Position; sample.Heading = heading; sample.Lanes = lanes;
                sample.Flags = (int)node.GetVehicleNodePropertyFlags();
                sample.FlagsName = node.GetVehicleNodePropertyFlags().ToString();
                sample.SwitchedOff = node.IsSwitchedOff; sample.GpsAllowed = node.IsGpsAllowed;
                sample.LoadedAfter = node.IsLoaded;
                sample.Distance3D = (sample.Position - roadOrigin).Length();
                sample.OutputValid = sample.LoadedAfter && FiniteVector(sample.Position) && Finite(heading) &&
                    heading >= 0f && heading < 360f && lanes >= 0 && lanes <= 64 && Finite(sample.Distance3D);
                sample.WithinRange = sample.OutputValid && sample.Distance3D <= 100f;
                sample.Duplicate = !roadSeenHandles.Add(sample.Handle);
                sample.Accepted = sample.WithinRange && !sample.Duplicate;
            }
        }
        roadSamples.Add(sample); roadNextNth++;
        if (roadNextNth > 8)
            Finish("concluida", "Oito consultas do mapa carregado concluidas; somente nos validos/unicos em100m sao candidatos; nenhum no prova chao ou cobertura", elapsed, false);
    }

    private void RoadSurveyJsonFields(System.Text.StringBuilder output)
    {
        output.Append(",\"vias\":{\"origem\":"); VectorJson(output, roadOrigin);
        output.Append(",\"origemDados\":\"API191_GET_NTH_CLOSEST_VEHICLE_NODE_ID_WITH_HEADING_e_PathNode_carregado\"");
        output.Append(",\"consultaFlags\":0,\"zMeasureMult\":0,\"zTolerance\":0,\"raioMaximoMetros3D\":100,\"maxConsultas\":8,\"maxConsultasPorTick\":1");
        output.Append(",\"area100mCarregada\":").Append(roadAreaLoaded ? "true" : "false");
        output.Append(",\"naoProvaChao\":true,\"naoProvaCobertura\":true,\"teleporteExecutado\":false,\"marcaCriada\":false,\"nos\":[");
        for (int i = 0; i < roadSamples.Count; i++)
        {
            RoadNodeSample sample = roadSamples[i]; if (i > 0) output.Append(',');
            output.Append("{\"nth\":").Append(sample.Nth).Append(",\"nodeHandle\":").Append(sample.Handle);
            output.Append(",\"encontrado\":").Append(sample.Found ? "true" : "false");
            output.Append(",\"carregadoAntes\":").Append(sample.LoadedBefore ? "true" : "false").Append(",\"carregadoDepois\":").Append(sample.LoadedAfter ? "true" : "false");
            output.Append(",\"saidaValida\":").Append(sample.OutputValid ? "true" : "false").Append(",\"dentroRaio\":").Append(sample.WithinRange ? "true" : "false");
            output.Append(",\"duplicado\":").Append(sample.Duplicate ? "true" : "false").Append(",\"candidato\":").Append(sample.Accepted ? "true" : "false");
            output.Append(",\"posicao\":"); if (sample.OutputValid) VectorJson(output, sample.Position); else output.Append("null");
            output.Append(",\"heading\":"); if (sample.OutputValid) NumberJson(output, sample.Heading); else output.Append("null");
            output.Append(",\"faixas\":"); if (sample.OutputValid) output.Append(sample.Lanes); else output.Append("null");
            output.Append(",\"propertyFlags\":"); if (sample.OutputValid) output.Append(sample.Flags); else output.Append("null");
            output.Append(",\"propertyFlagsNome\":"); if (sample.OutputValid) Quote(output, sample.FlagsName); else output.Append("null");
            output.Append(",\"switchedOff\":"); output.Append(sample.OutputValid ? (sample.SwitchedOff ? "true" : "false") : "null");
            output.Append(",\"gpsAllowed\":"); output.Append(sample.OutputValid ? (sample.GpsAllowed ? "true" : "false") : "null");
            output.Append(",\"distancia3D\":"); if (sample.OutputValid) NumberJson(output, sample.Distance3D); else output.Append("null");
            output.Append('}');
        }
        output.Append("]}");
    }

    private bool CanReadTerrain()
    {
        return terrainCalls == 0; // Cada ponto faz seis consultas; um ponto por Tick, total máximo seis.
    }

    private TerrainSample ReadTerrain(GTA.Math.Vector3 point, int index, int offsetX, int offsetY)
    {
        if (!CanReadTerrain()) throw Error("limite de consultas de terreno/água por frame");
        terrainCalls += 6;
        var sample = new TerrainSample { Point = point, Index = index, OffsetX = offsetX, OffsetY = offsetY, ProbeZ = point.Z + 5f };
        GTA.Math.Vector3 probe = new GTA.Math.Vector3(point.X, point.Y, sample.ProbeZ);
        sample.GroundNativeResult = GTA.World.GetGroundHeight(probe, out sample.GroundNative, GTA.GetGroundHeightMode.Normal);
        sample.GroundNormalResult = GTA.World.GetGroundHeightAndNormal(probe, out sample.GroundNormal, out sample.Normal);
        using (var height = new GTA.Native.OutputArgument())
        {
            sample.WaterResult = GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_WATER_HEIGHT, probe.X, probe.Y, probe.Z, height);
            if (sample.WaterResult) sample.Water = height.GetResult<float>();
        }
        sample.WaterDiagnostics = true;
        using (var height = new GTA.Native.OutputArgument())
        {
            sample.WaterNoWavesResult = GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_WATER_HEIGHT_NO_WAVES,
                sample.Point.X, sample.Point.Y, sample.ProbeZ, height);
            if (sample.WaterNoWavesResult) sample.WaterNoWaves = height.GetResult<float>();
        }
        using (var hit = new GTA.Native.OutputArgument())
        {
            // O nativo acrescenta flag128 (água); flag0 evita testar objetos/terreno. BOOL é retorno direto.
            sample.WaterProbeAllResult = GTA.Native.Function.Call<bool>(GTA.Native.Hash.TEST_PROBE_AGAINST_ALL_WATER,
                sample.Point.X, sample.Point.Y, sample.Point.Z + 10f,
                sample.Point.X, sample.Point.Y, sample.Point.Z - 20f, 0, hit);
            if (sample.WaterProbeAllResult) sample.WaterProbeAllPoint = hit.GetResult<GTA.Math.Vector3>();
        }
        // Diagnóstico da mesma altura de busca usada pela guarda de água; somente leitura.
        sample.GroundTopProbeZ = point.Z + 60f;
        sample.GroundTopZ60Result = GTA.World.GetGroundHeight(
            new GTA.Math.Vector3(point.X, point.Y, sample.GroundTopProbeZ), out sample.GroundTopZ60, GTA.GetGroundHeightMode.Normal);
        return sample; // BOOL é retorno direto; outputs contêm apenas float/vetor, nunca GetResult<bool>().
    }

    private void RespondStatus(Command command, GTA.Ped player)
    {
        if (!CanReadTerrain()) { deferredStatus = command; return; }
        TerrainSample current = ReadTerrain(player.Position, -1, 0, 0);
        ReadClosestRoad(current);
        Respond(command, "concluida", "Getters atuais de jogador, câmera, terreno e água", 0, current);
    }

    private static void ReadClosestRoad(TerrainSample sample)
    {
        sample.RoadQueried = true;
        using (var position = new GTA.Native.OutputArgument())
        using (var heading = new GTA.Native.OutputArgument())
        {
            sample.RoadFound = GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING,
                sample.Point.X, sample.Point.Y, sample.Point.Z, position, heading, 0, 3f, 0);
            if (!sample.RoadFound) return;
            sample.RoadPosition = position.GetResult<GTA.Math.Vector3>(); sample.RoadHeading = heading.GetResult<float>();
            sample.RoadOutputValid = Finite(sample.RoadPosition.X) && Finite(sample.RoadPosition.Y) && Finite(sample.RoadPosition.Z) && Finite(sample.RoadHeading);
        }
    }

    private void AdvanceDoor(bool permitted, GTA.Ped player, int elapsed)
    {
        if (!permitted || !Owned(player))
        { Finish("cancelada", "Guarda da cena/jogador/modo história mudou; restaurando somente a porta rastreada", elapsed, false); return; }
        if (elapsed >= active.TimeoutMs)
        { LogMeasuredFrontDoorSample(elapsed, "BEFORE_TIMEOUT_RESTORE", true); Finish("erro", "Porta real não confirmou física/abertura em5s; restauração do estado original solicitada", elapsed, false); return; }
        if (doorStage == 0)
        {
            GTA.Math.Vector3 from = actor.Bones[GTA.Bone.SkelPelvis].Position + new GTA.Math.Vector3(0f, 0f, 0.5f);
            bool farmLowerReadOnly = active.Action == "abrirPorta" && Distance(actor.Position, new GTA.Math.Vector3(2456.232f, 4977.46533f, 46.81026f)) <= 12f;
            if (farmLowerReadOnly)
            {
                GTA.EntityBone left = actor.Bones[GTA.Bone.SkelLeftFoot], right = actor.Bones[GTA.Bone.SkelRightFoot];
                if (System.Object.ReferenceEquals(left, null) || System.Object.ReferenceEquals(right, null) || !left.IsValid || !right.IsValid)
                    throw Error("Farm lower-leaf readonly probe requires actual valid foot bones");
                GTA.Math.Vector3 lp = left.Position, rp = right.Position, root = actor.Position;
                if (!FiniteVector(lp) || !FiniteVector(rp) || !FiniteVector(root) || System.Math.Abs(lp.Z-rp.Z) > 0.15f ||
                    root.Z-System.Math.Min(lp.Z,rp.Z) < 0f || root.Z-System.Math.Min(lp.Z,rp.Z) > 1.5f)
                    throw Error("Farm lower-leaf readonly probe has invalid actual foot/root geometry");
                from = new GTA.Math.Vector3(root.X, root.Y, System.Math.Min(lp.Z, rp.Z) + 0.6f);
                Log("FARM_LOWER_LEAF_RAY_REQUEST", "Id=" + active.Id + "; Root=" + root + "; LeftFoot=" + lp + "; RightFoot=" + rp +
                    "; From=" + from + "; To=" + (from + actor.ForwardVector * 1.8f) +
                    "; HeightAboveActualLowestFoot=0.6; Flags=17; Options=7; Reach=1.8; SingleRay=True; ReadOnly=True; NoDoorRegistration=True; NoOpening=True");
            }
            GTA.Math.Vector3 to = from + actor.ForwardVector * 1.8f;
            if (!FiniteVector(from) || !FiniteVector(to)) throw Error("ray da porta tem vetor inválido");
            doorRay = GTA.Native.Function.Call<int>(GTA.Native.Hash.START_SHAPE_TEST_LOS_PROBE,
                from.X, from.Y, from.Z, to.X, to.Y, to.Z, 17, actorHandle, 7);
            if (doorRay == 0) throw Error("ray da porta não iniciou");
            doorStage = 1; return;
        }
        if (doorStage == 1)
        {
            int status, hitHandle; GTA.Math.Vector3 hitPoint, normal;
            using (var hit = new GTA.Native.OutputArgument())
            using (var point = new GTA.Native.OutputArgument())
            using (var surfaceNormal = new GTA.Native.OutputArgument())
            using (var entity = new GTA.Native.OutputArgument())
            {
                status = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_SHAPE_TEST_RESULT, doorRay, hit, point, surfaceNormal, entity);
                if (status == 1) return;
                // Farm only: inspect this SAME completed ray and a bounded nearby prop pool before the unchanged verdicts.
                if (active.Action == "abrirPorta" && Distance(actor.Position, new GTA.Math.Vector3(2456.232f, 4977.46533f, 46.81026f)) <= 12f)
                {
                    try
                    {
                        int diagnosticHit = status == 2 ? hit.GetResult<int>() : 0;
                        bool outputsKnown = status == 2 && diagnosticHit == 1; // Miss outputs are unspecified: never inspect stale point/normal/handle.
                        int diagnosticHandle = outputsKnown ? entity.GetResult<int>() : 0;
                        GTA.Math.Vector3 diagnosticPoint = outputsKnown ? point.GetResult<GTA.Math.Vector3>() : GTA.Math.Vector3.Zero;
                        GTA.Math.Vector3 diagnosticNormal = outputsKnown ? surfaceNormal.GetResult<GTA.Math.Vector3>() : GTA.Math.Vector3.Zero;
                        bool rayEntityExists = diagnosticHandle != 0 && GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, diagnosticHandle);
                        int rayEntityType = rayEntityExists ? GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, diagnosticHandle) : 0;
                        bool rayEntityIsProp = rayEntityExists && rayEntityType == 3 && GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_AN_OBJECT, diagnosticHandle);
                        Log("FARM_DOOR_RAY_READONLY", "Id=" + active.Id + "; ElapsedMs=" + elapsed + "; Status=" + status +
                            "; OutputsKnown=" + outputsKnown + "; Hit=" + diagnosticHit + "; HitPoint=" + diagnosticPoint + "; Normal=" + diagnosticNormal +
                            "; VectorsFinite=" + (outputsKnown && FiniteVector(diagnosticPoint) && FiniteVector(diagnosticNormal)) +
                            "; HitHandle=" + diagnosticHandle + "; Exists=" + rayEntityExists + "; EntityType=" + rayEntityType + "; IsEntityAProp=" + rayEntityIsProp +
                            "; IsEntityAPropNative=IS_ENTITY_AN_OBJECT; ActorPos=" + actor.Position + "; ActorHeading=" + actor.Heading +
                            "; NewRays=False; ModelOnNonProp=False; WorldChanged=False");
                        GTA.Prop[] pool = GTA.World.GetNearbyProps(actor.Position, 2.5f);
                        var candidates = new System.Collections.Generic.List<GTA.Prop>(17);
                        if (rayEntityIsProp)
                        {
                            GTA.Prop rayProp = GTA.Entity.FromHandle(diagnosticHandle) as GTA.Prop;
                            if (!System.Object.ReferenceEquals(rayProp, null)) candidates.Add(rayProp);
                        }
                        for (int pi = 0; pi < pool.Length && pi < 16; pi++) candidates.Add(pool[pi]);
                        var actual = new System.Collections.Generic.Dictionary<int, GTA.Prop>();
                        var wanted = new System.Collections.Generic.HashSet<int>();
                        foreach (GTA.Prop candidate in candidates)
                        {
                            if (System.Object.ReferenceEquals(candidate, null) || !candidate.Exists() || candidate.MemoryAddress == System.IntPtr.Zero) continue;
                            int ph = candidate.Handle;
                            if (actual.ContainsKey(ph) || !GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, ph) ||
                                GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, ph) != 3 ||
                                !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_AN_OBJECT, ph)) continue;
                            int mh = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_MODEL, ph); // Only after real Prop identity; never Building.Model.
                            actual.Add(ph, candidate); wanted.Add(mh);
                        }
                        byte[] propListBytes = System.IO.File.ReadAllBytes(propListFile); string propListHash;
                        using (var sh = System.Security.Cryptography.SHA256.Create()) propListHash = System.BitConverter.ToString(sh.ComputeHash(propListBytes)).Replace("-", "");
                        if (propListHash != "35BFD924919FC223F5AB131941643B2AE4E0082FCD0ECEF1A5A50FCDEE9B2552") throw Error("Farm diagnostic PropList changed; no model whitelist changed");
                        var nameMap = new System.Collections.Generic.Dictionary<int, string>();
                        foreach (string line in System.Text.Encoding.UTF8.GetString(propListBytes).Split('\n'))
                        {
                            string name = line.Trim(); if (!System.Text.RegularExpressions.Regex.IsMatch(name, "\\A[A-Za-z0-9_]+\\z")) continue;
                            uint j = 0; unchecked { foreach (char ch in name.ToLowerInvariant()) { j += (byte)ch; j += j << 10; j ^= j >> 6; } j += j << 3; j ^= j >> 11; j += j << 15; }
                            int mapped = unchecked((int)j); if (!wanted.Contains(mapped)) continue;
                            nameMap[mapped] = nameMap.ContainsKey(mapped) ? nameMap[mapped] + "," + name : name;
                        }
                        Log("FARM_DOOR_NEARBY_READONLY", "Id=" + active.Id + "; Radius=2.5; NativePoolCount=" + pool.Length +
                            "; ExaminedPoolEntries=" + System.Math.Min(16, pool.Length) + "; PoolTruncated=" + (pool.Length > 16) +
                            "; ActualUniqueProps=" + actual.Count + "; PropListSha256=" + propListHash + "; BoundsKind=ModelRestBounds; PhysicsOrFloorProven=False");
                        foreach (var pair in actual)
                        {
                            GTA.Prop item = pair.Value; int ph = pair.Key; System.IntPtr address = item.MemoryAddress;
                            if (!item.Exists() || address == System.IntPtr.Zero || GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, ph) != 3 ||
                                !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_AN_OBJECT, ph)) continue;
                            int mh = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_MODEL, ph); var md = new GTA.Model(mh);
                            GTA.Math.Vector3 mn = GTA.Math.Vector3.Zero, mx = GTA.Math.Vector3.Zero;
                            bool boundsRead = md.IsValid && md.IsInCdImage; if (boundsRead) md.GetDimensions(out mn, out mx);
                            GTA.Math.Vector3 pos = item.Position, rot = item.Rotation, size = mx - mn;
                            bool boundsFinite = boundsRead && FiniteVector(mn) && FiniteVector(mx) && size.X > 0f && size.Y > 0f && size.Z > 0f;
                            bool boundsWithin16 = boundsFinite && size.X <= 16f && size.Y <= 16f && size.Z <= 16f;
                            bool identityStable = item.Exists() && item.Handle == ph && item.MemoryAddress == address &&
                                GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, ph) == 3 && GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_MODEL, ph) == mh;
                            string name; bool mapped = nameMap.TryGetValue(mh, out name);
                            Log("FARM_DOOR_PROP_READONLY", "Id=" + active.Id + "; Handle=" + ph + "; Address=" + address.ToInt64().ToString("X", Invariant) +
                                "; IsRayHit=" + (ph == diagnosticHandle) + "; IsEntityAProp=True; Model=" + mh + "; InstalledName=" + (mapped ? name : "UNMAPPED") +
                                "; ModelInDoorWhitelist=" + doorModels.ContainsKey(mh) + "; Pos=" + pos + "; Rot=" + rot + "; Distance=" + Distance(actor.Position, pos).ToString("R", Invariant) +
                                "; ActualVectorsFinite=" + (FiniteVector(pos) && FiniteVector(rot)) + "; RestBoundsMin=" + mn + "; RestBoundsMax=" + mx +
                                "; BoundsRead=" + boundsRead + "; BoundsFinite=" + boundsFinite + "; BoundsWithin16=" + boundsWithin16 + "; IdentityStable=" + identityStable +
                                "; PhysicsBoundsProven=False; SettersExecuted=False; NewRays=False");
                        }
                    }
                    catch (System.Exception diagnosticError)
                    {
                        Log("FARM_DOOR_READONLY_DIAG_ERROR", "Id=" + active.Id + "; " + diagnosticError.ToString() + "; NoRegistration=True; NoOpening=True");
                        throw;
                    }
                    // This Farm invocation is diagnostic only, including real Prop hits. Never enter RegisterTrackedDoor/setters.
                    Finish(status == 2 ? "concluida" : "erro", status == 2 ?
                        "Sonda inferior Farm concluida; apenas getters; folha/contato/abertura nao aprovados" :
                        "Sonda inferior Farm terminou sem resultado valido; nenhuma porta alterada", elapsed, false);
                    return;
                }
                if (status != 2 || hit.GetResult<int>() == 0) throw Error("ray não encontrou porta física à frente; nenhuma parede alterada");
                hitHandle = entity.GetResult<int>(); hitPoint = point.GetResult<GTA.Math.Vector3>(); normal = surfaceNormal.GetResult<GTA.Math.Vector3>();
            }
            GTA.Prop prop = GTA.Entity.FromHandle(hitHandle) as GTA.Prop; string model;
            if (System.Object.ReferenceEquals(prop, null) || !prop.Exists() || !doorModels.TryGetValue(prop.Model.Hash, out model) ||
                !FiniteVector(hitPoint) || !FiniteVector(normal) || Distance(actor.Position, hitPoint) > 2f)
                throw Error("colisor não é porta real da PropList instalada perto do jogador");
            trackedDoor = new DoorState { Prop = prop, Handle = prop.Handle, Model = prop.Model.Hash, Address = prop.MemoryAddress,
                ModelName = model, Position = prop.Position, Rotation = prop.Rotation, HitPoint = hitPoint, Normal = normal };
            RegisterTrackedDoor(); doorStage = 2;
            Log("OPERACAO_LOCAL_PORTA_IDENTIFICADA", "Id=" + active.Id + "; modelo=" + model + "; handle=" + trackedDoor.Handle +
                "; pos=" + trackedDoor.Position + "; hit=" + hitPoint + "; system=" + trackedDoor.SystemId + "; Added=" + trackedDoor.Added);
            return;
        }
        if (!StillTrackedDoor()) throw Error("porta perdeu identidade; nenhuma outra entidade alterada");
        DoorState door = trackedDoor;
        door.PhysicsLoaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, door.SystemId);
        if (!door.PhysicsLoaded) return;
        door.State = GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, door.SystemId);
        door.Ratio = GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_OPEN_RATIO, door.SystemId);
        if (!Finite(door.Ratio)) throw Error("ratio nativo da porta inválido");
        if (doorStage == 2)
        {
            door.OriginalState = door.State; door.OriginalRatio = door.Ratio; door.OriginalSaved = true;
            door.Modified = true; // Registrar obrigação de restaurar antes da primeira alteração.
            if (IsMeasuredFrontDoorDiagnostic())
            {
                door.FrontRequestTick = unchecked((uint)GTA.Game.GameTime); door.FrontNextDiagnosticMs = elapsed;
                Log("OPERACAO_LOCAL_FH_PORTA_PEDIDA", "Id=" + active.Id + "; Handle=" + door.Handle + "; System=" + door.SystemId +
                    "; OriginalState=" + door.OriginalState + "; OriginalRatio=" + door.OriginalRatio.ToString("R", Invariant) +
                    "; BaselinePos=" + door.Position + "; BaselineRot=" + door.Rotation +
                    "; TargetState=0; TargetRatio=1; RequestDoor=false; ForceUpdate=true; Reassert=false; WindowMs=" + active.TimeoutMs);
            }
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, door.SystemId, 0, false, true);
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_OPEN_RATIO, door.SystemId, 1f, false, true);
            doorStage = 3; return; // Conferir getters somente depois de a física processar outro Tick.
        }
        LogMeasuredFrontDoorSample(elapsed, "AFTER_FRAME", false);
        GTA.Math.Vector3 rotation = door.Prop.Rotation;
        bool physicallyChanged = (door.Prop.Position - door.Position).Length() >= 0.05f ||
            HeadingDifference(rotation.X, door.Rotation.X) >= 5f || HeadingDifference(rotation.Y, door.Rotation.Y) >= 5f ||
            HeadingDifference(rotation.Z, door.Rotation.Z) >= 5f || System.Math.Abs(door.OriginalRatio) >= 0.9f;
        bool measuredForceDoor = IsMeasuredFrontDoorDiagnostic() && door.Model == 308207762 &&
            door.OriginalState == 1 && System.Math.Abs(door.OriginalRatio) <= 0.01f;
        uint forceTick = unchecked((uint)GTA.Game.GameTime);
        if (measuredForceDoor)
        {
            RequireMeasuredFrontDoorExistingRegistration(door);
            if (door.FrontForceFrames > 0 && !door.FrontForceSettling && forceTick != door.FrontForceIssuedTick &&
                System.Math.Abs(door.Ratio) >= 0.9f && (door.State == 0 || door.State == 5) && physicallyChanged)
            {
                // Release THIS_FRAME force; success still requires original UNLOCKED getter on another Tick.
                door.FrontForceSettling = true; door.FrontForceIssuedTick = forceTick;
                GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, door.SystemId, 0, false, true);
                Log("OPERACAO_LOCAL_FH_FORCE_SETTLE", "Id=" + active.Id + "; Handle=" + door.Handle + "; System=" + door.SystemId +
                    "; TempoMs=" + elapsed + "; ObservedStateBefore=" + door.State + "; RatioBefore=" + door.Ratio.ToString("R", Invariant) +
                    "; ForceFrames=" + door.FrontForceFrames + "; TargetState=0; RequestDoor=false; ForceUpdate=true; SuccessPendingNextTick=true");
                return;
            }
            if (door.FrontForceSettling && forceTick == door.FrontForceIssuedTick) return;
        }
        if (System.Math.Abs(door.Ratio) >= 0.9f && door.State == 0 && physicallyChanged)
        {
            Log("OPERACAO_LOCAL_PORTA_ABERTA", "Id=" + active.Id + "; handle=" + door.Handle + "; ratio=" + door.Ratio.ToString("R", Invariant) +
                "; physics=" + door.PhysicsLoaded + "; rotation=" + rotation + "; jogador=" + player.Position);
            Finish("concluida", "Porta real aberta e rastreada; posição/tarefas do jogador preservadas; limpeza restaura estado original", elapsed, false);
            return;
        }
        if (measuredForceDoor && !door.FrontForceSettling)
        {
            // State5 documented FORCE_OPEN_THIS_FRAME. Read physical getters above first, then request next frame.
            RequireMeasuredFrontDoorExistingRegistration(door);
            door.FrontForceIssuedTick = forceTick; door.FrontForceFrames++;
            if (door.FrontForceFrames == 1)
                Log("OPERACAO_LOCAL_FH_FORCE_PEDIDO", "Id=" + active.Id + "; Handle=" + door.Handle + "; System=" + door.SystemId +
                    "; TempoMs=" + elapsed + "; ObservedStateBefore=" + door.State + "; RatioBefore=" + door.Ratio.ToString("R", Invariant) +
                    "; SnapshotState=" + door.OriginalState + "; SnapshotRatio=" + door.OriginalRatio.ToString("R", Invariant) +
                    "; TargetState=5; ThisFrameOnly=true; RequestDoor=false; ForceUpdate=true; NoNewRegistration=true; ProductionApproval=false");
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, door.SystemId, 5, false, true);
        }
    }

    // Somente a folha fisica medida pelo Root em 177; diagnostico nao muda Native/setters/prazo.
    private bool IsMeasuredFrontDoorDiagnostic()
    {
        return active != null && active.Action == "abrirPorta" && trackedDoor != null &&
            trackedDoor.ModelName == "v_ilev_fh_frontdoor" && !trackedDoor.Added && trackedDoor.OriginalSaved &&
            (trackedDoor.Position - new GTA.Math.Vector3(7.518358f, 539.5269f, 176.1776f)).Length() <= 0.1f;
    }
    private void LogMeasuredFrontDoorSample(int elapsed, string phase, bool finalSample)
    {
        if (!IsMeasuredFrontDoorDiagnostic() || !StillTrackedDoor()) return;
        DoorState door = trackedDoor;
        if (!finalSample && elapsed < door.FrontNextDiagnosticMs) return;
        door.FrontNextDiagnosticMs = elapsed + 500;
        bool physics = GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, door.SystemId);
        int state = GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, door.SystemId);
        // Primary citizenfx/natives OBJECT/DoorSystemGetDoorPendingState.md: 0x4BC2854478F3A749.
        int pending = GTA.Native.Function.Call<int>((GTA.Native.Hash)0x4BC2854478F3A749UL, door.SystemId);
        float ratio = GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_OPEN_RATIO, door.SystemId);
        GTA.Math.Vector3 position = door.Prop.Position, rotation = door.Prop.Rotation;
        float displacement = (position - door.Position).Length();
        float rotationDelta = System.Math.Max(HeadingDifference(rotation.X, door.Rotation.X),
            System.Math.Max(HeadingDifference(rotation.Y, door.Rotation.Y), HeadingDifference(rotation.Z, door.Rotation.Z)));
        uint tick = unchecked((uint)GTA.Game.GameTime);
        Log("OPERACAO_LOCAL_FH_PORTA_AMOSTRA", "Id=" + active.Id + "; Phase=" + phase + "; TempoMs=" + elapsed +
            "; Handle=" + door.Handle + "; Address=" + door.Address + "; Model=" + door.Model + "; System=" + door.SystemId +
            "; PhysicsLoaded=" + physics + "; State=" + state + "; PendingState=" + pending +
            "; Ratio=" + ratio.ToString("R", Invariant) + "; Pos=" + position + "; Rot=" + rotation +
            "; Displacement=" + displacement.ToString("R", Invariant) + "; RotationDelta=" + rotationDelta.ToString("R", Invariant) +
            "; OriginalState=" + door.OriginalState + "; OriginalRatio=" + door.OriginalRatio.ToString("R", Invariant) +
            "; RequestTick=" + door.FrontRequestTick + "; ObservedTick=" + tick + "; ProcessedNextTick=" + (tick != door.FrontRequestTick) +
            "; ForceOpenFrameWrites=" + door.FrontForceFrames + "; ForceOpenSettling=" + door.FrontForceSettling +
            "; LeafFrozen=" + door.Prop.IsPositionFrozen + "; ReassertState0=false; ReadonlyDiagnostic=true; StockRelockProven=false; ProductionApproval=false");
    }

    private void RequireMeasuredFrontDoorExistingRegistration(DoorState door)
    {
        if (door != trackedDoor || !IsMeasuredFrontDoorDiagnostic() || !StillTrackedDoor() || door.Model != 308207762 ||
            !door.OriginalSaved || !door.Modified || door.Added || door.OriginalState != 1 || System.Math.Abs(door.OriginalRatio) > 0.01f ||
            !GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, door.SystemId) ||
            !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_DOOR_REGISTERED_WITH_SYSTEM, door.SystemId))
            throw Error("FH force-open requires same measured real leaf/existing physics/snapshot; no other door modified");
        using (var identifier = new GTA.Native.OutputArgument())
        {
            bool found = GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_FIND_EXISTING_DOOR,
                door.Position.X, door.Position.Y, door.Position.Z, door.Model, identifier);
            if (!found || identifier.GetResult<int>() != door.SystemId)
                throw Error("FH existing registration changed; no new registration or replacement");
        }
    }

    private void RegisterTrackedDoor()
    {
        DoorState door = trackedDoor; bool found;
        using (var id = new GTA.Native.OutputArgument())
        {
            found = GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_FIND_EXISTING_DOOR,
                door.Position.X, door.Position.Y, door.Position.Z, door.Model, id);
            if (found) door.SystemId = id.GetResult<int>();
        }
        if (found) return;
        // Só a folha real do trailer conferida em PropList18566 recebe um registro novo.
        // Outros nomes door/gate exigem classificação prévia pelo DoorSystem do jogo.
        if (door.ModelName != "v_ilev_trevtraildr") throw Error("porta não registrada pelo jogo; modelo exige revisão antes de acrescentar registro próprio");
        door.SystemId = unchecked((int)GTA.StringHash.AtStringHash("DIRETOR_OPLOCAL_PORTA_" + System.Guid.NewGuid().ToString("N")));
        if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_DOOR_REGISTERED_WITH_SYSTEM, door.SystemId)) throw Error("identificador de porta já registrado; preservado");
        door.Added = true;
        GTA.Native.Function.Call(GTA.Native.Hash.ADD_DOOR_TO_SYSTEM, door.SystemId, door.Model,
            door.Position.X, door.Position.Y, door.Position.Z, false, false, false);
    }

    private bool StillTrackedDoor()
    {
        return trackedDoor != null && trackedDoor.Prop.Exists() && trackedDoor.Prop.Handle == trackedDoor.Handle &&
            trackedDoor.Prop.MemoryAddress == trackedDoor.Address && trackedDoor.Prop.Model.Hash == trackedDoor.Model;
    }

    // Pode ser chamado pelo main na limpeza. Não limpa tarefas ou câmeras, nem remove a porta original.
    public bool CleanupDoors()
    {
        if (active != null && active.Action == "abrirPorta")
            Complete("cancelada", "Operação da porta cancelada; restauração original solicitada", (int)unchecked((uint)GTA.Game.GameTime - startedAt));
        doorRay = 0; doorStage = 0;
        return RestoreTrackedDoor();
    }

    private bool RestoreTrackedDoor()
    {
        if (trackedDoor == null) { doorRestorePending = false; return true; }
        if (!StillTrackedDoor())
        { Log("OPERACAO_LOCAL_PORTA_RESTAURO", "Porta original não existe mais/identidade mudou; nenhuma outra entidade tocada"); trackedDoor = null; doorRestorePending = false; return true; }
        DoorState door = trackedDoor;
        try
        {
            if (door.Modified && door.OriginalSaved)
            {
                GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, door.SystemId, 0, false, true);
                GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_OPEN_RATIO, door.SystemId, door.OriginalRatio, false, true);
                GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, door.SystemId, door.OriginalState, false, true);
            }
            if (door.Added)
            {
                GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_DOOR_FROM_SYSTEM, door.SystemId);
                if (door.Modified) door.Prop.Rotation = door.Rotation;
            }
            Log("OPERACAO_LOCAL_PORTA_RESTAURO_SOLICITADO", "Handle=" + door.Handle + "; estadoOriginal=" + door.OriginalState +
                "; ratioOriginal=" + door.OriginalRatio.ToString("R", Invariant) + "; registro próprio removido=" + door.Added);
            trackedDoor = null; doorRestorePending = false; return true;
        }
        catch (System.Exception ex) { doorRestorePending = true; Log("OPERACAO_LOCAL_PORTA_RESTAURO_ERRO", ex.Message); return false; }
    }

    private void ReadDoorModels()
    {
        if (doorModelsRead) return;
        if (System.String.IsNullOrEmpty(propListFile) || !System.IO.Path.IsPathRooted(propListFile) ||
            !System.String.Equals(System.IO.Path.GetFileName(propListFile), "PropList.txt", System.StringComparison.OrdinalIgnoreCase))
            throw Error("informe a PropList instalada absoluta para identificar portas");
        byte[] bytes = System.IO.File.ReadAllBytes(propListFile); string hash;
        using (var sha = System.Security.Cryptography.SHA256.Create()) hash = System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        if (hash != "35BFD924919FC223F5AB131941643B2AE4E0082FCD0ECEF1A5A50FCDEE9B2552") throw Error("PropList mudou; porta não autorizada antes de revisar a fonte");
        foreach (string line in System.Text.Encoding.UTF8.GetString(bytes).Split('\n'))
        {
            string model = line.Trim();
            if (model != "v_ilev_trevtraildr" && model.IndexOf("door", System.StringComparison.OrdinalIgnoreCase) < 0 && model.IndexOf("gate", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (!System.Text.RegularExpressions.Regex.IsMatch(model, "\\A[A-Za-z0-9_]+\\z")) continue;
            int modelHash = new GTA.Model(model).Hash; if (!doorModels.ContainsKey(modelHash)) doorModels.Add(modelHash, model);
        }
        doorModelsRead = true;
    }

    private static bool FiniteVector(GTA.Math.Vector3 value) { return Finite(value.X) && Finite(value.Y) && Finite(value.Z); }

    private void AdvanceProbe(bool permitted, GTA.Ped player, int elapsed)
    {
        if (!permitted || !Owned(player))
        { Finish("cancelada", "Guarda do main/identidade/estado do jogador mudou durante a sondagem", elapsed, false); return; }
        if (elapsed >= active.TimeoutMs)
        { Finish("erro", "Sondagem não concluiu81pontos em15s; amostras já lidas preservadas", elapsed, false); return; }
        while (nextProbe < 81 && CanReadTerrain())
        {
            int offsetX = (nextProbe % 9 - 4) * 5, offsetY = (nextProbe / 9 - 4) * 5;
            GTA.Math.Vector3 point = probeOrigin + new GTA.Math.Vector3(offsetX, offsetY, 0f);
            probeSamples.Add(ReadTerrain(point, nextProbe, offsetX, offsetY)); nextProbe++;
        }
        if (nextProbe == 81)
            Finish("concluida", "81pontos consultados; somente getters, sem mover jogador, câmera ou tarefas", elapsed, false);
    }

    private static void TerrainJsonFields(System.Text.StringBuilder output, TerrainSample sample)
    {
        output.Append(",\"probeZ\":"); NumberJson(output, sample.ProbeZ);
        output.Append(",\"groundNativeResult\":").Append(sample.GroundNativeResult ? "true" : "false");
        output.Append(",\"groundNative\":"); if (sample.GroundNativeResult) NumberJson(output, sample.GroundNative); else output.Append("null");
        output.Append(",\"groundTopProbeZ\":"); NumberJson(output, sample.GroundTopProbeZ);
        output.Append(",\"groundTopZ60Result\":").Append(sample.GroundTopZ60Result ? "true" : "false");
        output.Append(",\"groundTopZ60\":"); if (sample.GroundTopZ60Result) NumberJson(output, sample.GroundTopZ60); else output.Append("null");
        output.Append(",\"groundNormalResult\":").Append(sample.GroundNormalResult ? "true" : "false");
        output.Append(",\"groundNormalHeight\":"); if (sample.GroundNormalResult) NumberJson(output, sample.GroundNormal); else output.Append("null");
        output.Append(",\"groundNormal\":"); if (sample.GroundNormalResult) VectorJson(output, sample.Normal); else output.Append("null");
        output.Append(",\"waterHeightCurrentResult\":").Append(sample.WaterResult ? "true" : "false");
        output.Append(",\"waterHeightCurrent\":"); if (sample.WaterResult) NumberJson(output, sample.Water); else output.Append("null");
        if (sample.WaterDiagnostics)
        {
            output.Append(",\"waterHeightNoWavesResult\":").Append(sample.WaterNoWavesResult ? "true" : "false");
            output.Append(",\"waterHeightNoWaves\":"); if (sample.WaterNoWavesResult) NumberJson(output, sample.WaterNoWaves); else output.Append("null");
            output.Append(",\"waterProbeAllResult\":").Append(sample.WaterProbeAllResult ? "true" : "false");
            output.Append(",\"waterProbeAllPoint\":"); if (sample.WaterProbeAllResult) VectorJson(output, sample.WaterProbeAllPoint); else output.Append("null");
        }
        if (sample.RoadQueried)
        {
            output.Append(",\"closestVehicleNodeResult\":").Append(sample.RoadFound ? "true" : "false");
            output.Append(",\"closestVehicleNodeOutputValid\":").Append(sample.RoadOutputValid ? "true" : "false");
            output.Append(",\"closestVehicleNodePosition\":"); if (sample.RoadOutputValid) VectorJson(output, sample.RoadPosition); else output.Append("null");
            output.Append(",\"closestVehicleNodeHeading\":"); if (sample.RoadOutputValid) NumberJson(output, sample.RoadHeading); else output.Append("null");
            output.Append(",\"closestVehicleNodeDistanceXY\":"); if (sample.RoadOutputValid) NumberJson(output, Distance(sample.Point, sample.RoadPosition)); else output.Append("null");
            output.Append(",\"closestVehicleNodeFlags\":0");
        }
    }

    private static bool TaskInactive(GTA.ScriptTaskStatus status)
    { return status == GTA.ScriptTaskStatus.Vacant || status == GTA.ScriptTaskStatus.Finished; }

    private bool HasOwnTask(Command command)
    {
        if (!Owned(GTA.Game.Player.Character)) return false;
        GTA.ScriptTaskNameHash expected = command.Action == "andar" ? GTA.ScriptTaskNameHash.GoStraightToCoord : GTA.ScriptTaskNameHash.AchieveHeading;
        return actor.CurrentScriptTaskNameHash == expected;
    }

    private void Respond(Command command, string state, string message, int elapsed, TerrainSample currentTerrain = null)
    {
        GTA.Ped player = GTA.Game.Player.Character;
        var output = new System.Text.StringBuilder();
        output.Append("{\"versao\":1,\"id\":"); Quote(output, command.Id);
        output.Append(",\"acao\":"); Quote(output, command.Action); output.Append(",\"estado\":"); Quote(output, state);
        output.Append(",\"mensagem\":"); Quote(output, message);
        output.Append(",\"dataUtc\":"); Quote(output, System.DateTimeOffset.UtcNow.ToString("o", Invariant));
        output.Append(",\"tempoMs\":").Append(elapsed).Append(",\"ocupado\":").Append(IsBusy ? "true" : "false");
        output.Append(",\"operacaoAtivaId\":"); Quote(output, active == null ? "" : active.Id);
        if (!System.Object.ReferenceEquals(player, null) && player.Exists())
        {
            output.Append(",\"jogador\":{\"handle\":").Append(player.Handle).Append(",\"modelo\":").Append(player.Model.Hash);
            output.Append(",\"posicao\":"); VectorJson(output, player.Position);
            output.Append(",\"heading\":"); NumberJson(output, player.Heading);
            output.Append(",\"velocidade\":"); NumberJson(output, player.Speed);
            output.Append(",\"congelado\":").Append(player.IsPositionFrozen ? "true" : "false");
            output.Append(",\"noVeiculo\":").Append(player.IsInVehicle() ? "true" : "false");
            output.Append(",\"inWater\":").Append(player.IsInWater ? "true" : "false");
            output.Append(",\"isSwimming\":").Append(player.IsSwimming ? "true" : "false");
            if (currentTerrain != null) TerrainJsonFields(output, currentTerrain);
            GTA.ScriptTaskNameHash taskName; GTA.ScriptTaskStatus taskStatus;
            player.GetCurrentScriptTaskNameHashAndStatus(out taskName, out taskStatus);
            output.Append(",\"tarefaHash\":"); Quote(output, "0x" + taskName.ToString("X"));
            output.Append(",\"tarefaNome\":"); Quote(output, taskName.ToString()); output.Append(",\"tarefaEstado\":"); Quote(output, taskStatus.ToString());
            output.Append('}');
        }
        output.Append(",\"camera\":{\"headingRelativo\":"); NumberJson(output, GTA.GameplayCamera.RelativeHeading);
        output.Append(",\"pitch\":"); NumberJson(output, GTA.GameplayCamera.RelativePitch);
        output.Append(",\"rotacao\":"); VectorJson(output, GTA.GameplayCamera.Rotation); output.Append('}');
        output.Append(",\"gameplayRender\":").Append(GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_GAMEPLAY_CAM_RENDERING) ? "true" : "false");
        if (command.Action == "status")
        {
            // Somente getters no mesmo Tick: distinguir camera scripted, cinematic idle e controles.
            output.Append(",\"cameraScriptHandle\":").Append(GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_RENDERING_CAM));
            output.Append(",\"controleJogador\":").Append(GTA.Game.Player.CanControlCharacter ? "true" : "false");
            output.Append(",\"cameraCinematicaRender\":").Append(GTA.CinematicCameraDirector.IsRendering ? "true" : "false");
            output.Append(",\"cameraCinematicaOciosa\":").Append(GTA.CinematicCameraDirector.IsRenderingIdleCam ? "true" : "false");
            output.Append(",\"controlesHabilitados\":{\"MoveUpDown\":").Append(
                GTA.Input.Controls.IsControlEnabled(GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.MoveUpDown) ? "true" : "false");
            output.Append(",\"MoveLeftRight\":").Append(
                GTA.Input.Controls.IsControlEnabled(GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.MoveLeftRight) ? "true" : "false");
            output.Append(",\"LookLeftRight\":").Append(
                GTA.Input.Controls.IsControlEnabled(GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.LookLeftRight) ? "true" : "false");
            output.Append(",\"LookUpDown\":").Append(
                GTA.Input.Controls.IsControlEnabled(GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.LookUpDown) ? "true" : "false");
            output.Append('}');
        }
        if (command.Action == "vias" && command.Id == roadSurveyId && roadSamples != null) RoadSurveyJsonFields(output);
        if (command.Action == "sondar" && command.Id == probeId && probeSamples != null)
        {
            output.Append(",\"sondagem\":{\"origem\":"); VectorJson(output, probeOrigin);
            output.Append(",\"eixos\":\"XY_mundo\",\"passoMetros\":5,\"alcanceMetros\":20,\"probeOffsetZ\":5,\"groundTopProbeOffsetZ\":60,\"totalPontos\":81,\"consultasPorPonto\":6,\"maxPontosPorFrame\":1,\"maxConsultasTerrenoAguaPorFrame\":6,\"pontosConcluidos\":").Append(probeSamples.Count);
            output.Append(",\"pontos\":[");
            for (int i = 0; i < probeSamples.Count; i++)
            {
                if (i > 0) output.Append(','); TerrainSample sample = probeSamples[i];
                output.Append("{\"indice\":").Append(sample.Index).Append(",\"offsetX\":").Append(sample.OffsetX).Append(",\"offsetY\":").Append(sample.OffsetY);
                output.Append(",\"ponto\":"); VectorJson(output, sample.Point); TerrainJsonFields(output, sample); output.Append('}');
            }
            output.Append("]}");
        }
        if (command.Action == "andar" && command.Id == targetId)
        {
            output.Append(",\"destino\":"); VectorJson(output, destination);
            if (!System.Object.ReferenceEquals(player, null) && player.Exists())
            { output.Append(",\"distanciaXY\":"); NumberJson(output, Distance(player.Position, destination)); }
        }
        if (command.Action == "virar" && command.Id == targetId) { output.Append(",\"headingSolicitado\":"); NumberJson(output, targetHeading); }
        if (trackedDoor != null && (command.Action == "status" || command.Id == doorId))
        {
            output.Append(",\"porta\":{\"handle\":").Append(trackedDoor.Handle).Append(",\"modeloHash\":").Append(trackedDoor.Model);
            output.Append(",\"modelo\":"); Quote(output, trackedDoor.ModelName);
            output.Append(",\"posicaoInicial\":"); VectorJson(output, trackedDoor.Position);
            output.Append(",\"pontoRay\":"); VectorJson(output, trackedDoor.HitPoint);
            output.Append(",\"systemId\":").Append(trackedDoor.SystemId).Append(",\"registroAdicionado\":").Append(trackedDoor.Added ? "true" : "false");
            output.Append(",\"estadoOriginal\":").Append(trackedDoor.OriginalState).Append(",\"ratioOriginal\":"); NumberJson(output, trackedDoor.OriginalRatio);
            output.Append(",\"estadoObservado\":").Append(trackedDoor.State).Append(",\"ratioObservado\":"); NumberJson(output, trackedDoor.Ratio);
            output.Append(",\"physicsLoaded\":").Append(trackedDoor.PhysicsLoaded ? "true" : "false").Append('}');
        }
        output.Append('}').AppendLine();
        WriteNew(ResultPath(command.Id), output.ToString());
        Log("OPERACAO_LOCAL_RESULTADO", output.ToString().TrimEnd());
    }

    private void WriteNew(string path, string content)
    {
        if (!System.IO.Directory.Exists(results)) System.IO.Directory.CreateDirectory(results);
        if ((System.IO.File.GetAttributes(results) & System.IO.FileAttributes.ReparsePoint) != 0) throw Error("logs não pode ser vínculo");
        byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes(content);
        string temporary = path + ".tmp-" + System.Guid.NewGuid().ToString("N");
        bool created = false;
        try
        {
            using (var file = new System.IO.FileStream(temporary, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None))
            { created = true; file.Write(bytes, 0, bytes.Length); file.Flush(true); }
            System.IO.File.Move(temporary, path); created = false; // Move recusa destino existente.
        }
        finally { if (created && System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
    }

    private string ResultPath(string id) { return System.IO.Path.Combine(results, "operacao_local_" + id + ".json"); }
    private string ReceiptPath(string id) { return System.IO.Path.Combine(results, "operacao_local_" + id + ".aceita.json"); }
    private bool Owned(GTA.Ped player)
    { return !System.Object.ReferenceEquals(actor, null) && actor.Exists() && !System.Object.ReferenceEquals(player, null) && player.Exists() &&
        player.Handle == actorHandle && player.MemoryAddress == actorAddress && player.Model.Hash == actorModel && actor.Handle == actorHandle && actor.MemoryAddress == actorAddress; }
    private static bool PlayerUsable(GTA.Ped player)
    { return !System.Object.ReferenceEquals(player, null) && player.Exists() && !player.IsDead && !player.IsInVehicle() && !player.IsPositionFrozen; }
    private static float Normalize(float heading) { return (heading % 360f + 360f) % 360f; }
    private static float HeadingDifference(float a, float b) { return System.Math.Abs((a - b + 540f) % 360f - 180f); }
    private static float Distance(GTA.Math.Vector3 a, GTA.Math.Vector3 b)
    { float x = a.X - b.X, y = a.Y - b.Y; return (float)System.Math.Sqrt(x * x + y * y); }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

    private static Command Parse(string text)
    {
        var values = new FlatReader(text).Read();
        string id = StringValue(values, "id"), action = StringValue(values, "acao");
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, @"\A[A-Za-z0-9][A-Za-z0-9_-]{0,47}\z")) throw Error("id deve ser simples e único, até48caracteres");
        Token version; if (!values.TryGetValue("versao", out version) || version.IsString || version.Text != "1") throw Error("versao deve ser inteiro1");
        var command = new Command { Id = id, Action = action };
        string allowed = "versao,id,acao";
        if (action == "olhar")
        { allowed += ",headingRelativo,pitch"; command.CameraHeading = (float)Numeric(values, "headingRelativo", -180m, 180m); command.Pitch = (float)Numeric(values, "pitch", -60m, 60m); }
        else if (action == "virar")
        { allowed += ",delta"; command.Delta = (float)Numeric(values, "delta", -180m, 180m); command.TimeoutMs = 5000; }
        else if (action == "andar")
        {
            allowed += ",frente,lado,velocidade,timeoutSegundos";
            command.Front = (float)Numeric(values, "frente", -15m, 15m); command.Side = (float)Numeric(values, "lado", -15m, 15m);
            decimal speed = Numeric(values, "velocidade", 0m, 3m);
            if (speed <= 0m) throw Error("velocidade deve ser maior que zero e até3 (proporção de movimento)"); command.Speed = (float)speed;
            decimal milliseconds = Numeric(values, "timeoutSegundos", 0.5m, 15m) * 1000m;
            if (milliseconds != decimal.Truncate(milliseconds)) throw Error("timeoutSegundos precisa de resolução exata de ms");
            command.TimeoutMs = (int)milliseconds;
            if (!System.Text.RegularExpressions.Regex.IsMatch(values["timeoutSegundos"].Text, @"\A[0-9]+(?:\.[0-9]{1,3})?\z"))
                throw Error("timeoutSegundos deve ser decimal simples com até três casas");
        }
        else if (action == "vias") command.TimeoutMs = 5000;
        else if (action == "sondar") command.TimeoutMs = 15000;
        else if (action == "abrirPorta") command.TimeoutMs = 5000;
        else if (action != "status" && action != "cancelar") throw Error("acao não autorizada");
        var keys = new System.Collections.Generic.HashSet<string>(allowed.Split(','), System.StringComparer.Ordinal);
        foreach (string key in values.Keys) if (!keys.Contains(key)) throw Error("chave desconhecida: " + key);
        return command;
    }

    private static string StringValue(System.Collections.Generic.Dictionary<string, Token> values, string key)
    { Token token; if (!values.TryGetValue(key, out token) || !token.IsString) throw Error(key + " deve ser texto"); return token.Text; }
    private static decimal Numeric(System.Collections.Generic.Dictionary<string, Token> values, string key, decimal min, decimal max)
    {
        Token token; decimal value;
        if (!values.TryGetValue(key, out token) || token.IsString ||
            !decimal.TryParse(token.Text, System.Globalization.NumberStyles.Float, Invariant, out value) || value < min || value > max)
            throw Error(key + " deve ser número finito entre" + min.ToString(Invariant) + " e" + max.ToString(Invariant));
        return value;
    }

    // Contrato deliberadamente plano: apenas strings ASCII simples e números JSON, sem objetos/arrays/escapes.
    private sealed class FlatReader
    {
        private readonly string text; private int cursor;
        internal FlatReader(string source) { text = source; }
        internal System.Collections.Generic.Dictionary<string, Token> Read()
        {
            var values = new System.Collections.Generic.Dictionary<string, Token>(System.StringComparer.Ordinal);
            White(); Need('{'); White(); if (Peek() == '}') { cursor++; White(); End(); return values; }
            while (true)
            {
                White(); string key = ReadString(); White(); Need(':'); White(); Token token;
                if (Peek() == '"') token = new Token { Text = ReadString(), IsString = true };
                else
                {
                    int start = cursor;
                    while (cursor < text.Length && ",} \t\r\n".IndexOf(text[cursor]) < 0) cursor++;
                    string number = text.Substring(start, cursor - start);
                    if (!System.Text.RegularExpressions.Regex.IsMatch(number, @"\A-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?\z")) throw Error("número JSON inválido");
                    token = new Token { Text = number };
                }
                if (values.ContainsKey(key)) throw Error("chave duplicada: " + key); values.Add(key, token);
                White(); if (Peek() == '}') { cursor++; White(); End(); return values; }
                Need(',');
            }
        }
        private string ReadString()
        {
            Need('"'); int start = cursor;
            while (cursor < text.Length && text[cursor] != '"')
            {
                char value = text[cursor++];
                if (value < 32 || value > 126 || value == '\\') throw Error("texto deve ser ASCII simples, sem escapes");
            }
            string valueText = text.Substring(start, cursor - start); Need('"'); return valueText;
        }
        private char Peek() { return cursor < text.Length ? text[cursor] : '\0'; }
        private void Need(char value) { if (Peek() != value) throw Error("estrutura JSON inválida"); cursor++; }
        private void White() { while (cursor < text.Length && " \t\r\n".IndexOf(text[cursor]) >= 0) cursor++; }
        private void End() { if (cursor != text.Length) throw Error("conteúdo após objeto JSON"); }
    }

    private static void Quote(System.Text.StringBuilder output, string value)
    {
        output.Append('"'); foreach (char ch in value)
        { if (ch == '"' || ch == '\\') output.Append('\\').Append(ch); else if (ch < 32) output.Append("\\u").Append(((int)ch).ToString("x4", Invariant)); else output.Append(ch); }
        output.Append('"');
    }
    private static void NumberJson(System.Text.StringBuilder output, float value) { output.Append(Finite(value) ? value.ToString("R", Invariant) : "null"); }
    private static void VectorJson(System.Text.StringBuilder output, GTA.Math.Vector3 value)
    { output.Append("{\"x\":"); NumberJson(output, value.X); output.Append(",\"y\":"); NumberJson(output, value.Y); output.Append(",\"z\":"); NumberJson(output, value.Z); output.Append('}'); }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("Operação local: " + message + "."); }
    private void Log(string code, string message) { if (log != null) { try { log(code, message); } catch (System.Exception) { } } }
    public void Dispose()
    {
        if (disposed) return;
        Cancel();
        if (active != null)
            Complete("cancelada", "Script encerrado; parada curta solicitada, sem confirmação em frames posteriores", (int)unchecked((uint)GTA.Game.GameTime - startedAt));
        if (deferredStatus != null)
        { Command waiting = deferredStatus; deferredStatus = null; Respond(waiting, "cancelada", "Script encerrado antes dos getters", 0); }
        disposed = true;
    }
}

// PRIVATE CANDIDATE. No Script subclass; not compiled, installed or run.
// Root must retain this instance before Begin(), tick it, and restore on every exit.
// Append to an existing bundled helper only after Root review. No production hook here.

internal sealed class VinewoodRoomNoneProbeDiretor
{
    internal const int AuthorInteriorReference = 304385;
    internal static readonly GTA.Math.Vector3 AuthorEntry = new GTA.Math.Vector3(539.7012f, 749.089f, 201.36f);
    private static readonly string[] ActiveIpls = {
        "m25_2_ch2_04_mansion_interior_a", "m25_2_ch2_04_mansion_interior_b", "m25_2_ch2_04_mansion_interior_c",
        "apa_ch2_04_mansion_shared", "apa_ch2_04_mansion_shared_distantlights", "apa_ch2_04_mansion_shared_lodlights",
        "apa_ch2_04_mansion_private_distantlights", "apa_ch2_04_mansion_private_lodlights", "apa_ch2_04_mansion_player_bounds",
        "apa_ch2_04_mansion_firepit_distantlights", "apa_ch2_04_mansion_firepit_lodlights", "m25_2_knoway_sign",
        "m25_2_legacy_fixes", "m25_2_payphones", "m25_2_mansion_props", "apa_ch2_04_mansion_private",
        "apa_ch2_04_mansion_railings_m", "apa_ch2_04_mansion_furniture", "apa_ch2_04_mansion_firepit",
        "m25_2_east_mansion_gym", "m25_2_east_dog_house"
    };
    private static readonly string[] InactiveIpls = {
        "apa_ch2_04_mansion_original", "apa_ch2_04_props_original", "apa_ch2_04_mansion_grass",
        "apa_ch2_04_mansion_generic", "apa_ch2_04_mansion_railings_p", "apa_ch2_04_mansion_shutters"
    };
    private static readonly string[] Styles = { "SET_STYLE_CALI", "SET_STYLE_HOLLY", "SET_STYLE_LOFT" };
    private readonly System.Collections.Generic.Dictionary<string, bool> originalIpls = new System.Collections.Generic.Dictionary<string, bool>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, bool> originalStyles = new System.Collections.Generic.Dictionary<string, bool>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.List<string> changedIpls = new System.Collections.Generic.List<string>();
    private readonly System.Collections.Generic.List<string> changedStyles = new System.Collections.Generic.List<string>();
    private readonly System.Action<string> persistSnapshot;
    private readonly System.Action<string, string> log;
    private bool begun, stylesCaptured, styleWriteStarted, restored;
    private bool caliNoFurniture, madrazo, farmOriginal, modern1;
    // MenyooSP Locations.cpp145..171/421: one complete residential theme, not an unfurnished shell.
    private static readonly string[] Modern1ActiveIpls = { "apa_v_mp_h_01_a" };
    private static readonly string[] Modern1InactiveIpls = { "apa_v_mp_h_02_a", "apa_v_mp_h_03_a", "apa_v_mp_h_04_a", "apa_v_mp_h_05_a", "apa_v_mp_h_06_a", "apa_v_mp_h_07_a", "apa_v_mp_h_08_a" };
    private const string FurnitureIpl = "apa_ch2_04_mansion_furniture";
    private static readonly string[] CaliStyles = { "SET_STYLE_CALI", "SET_STYLE_HOLLY", "SET_STYLE_LOFT",
        "SET_STYLE_CALI_TINT", "SET_STYLE_LOFT_TINT", "SET_STYLE_REG_TINT" };
    private static readonly string[] MadrazoIpls = { "apa_ch2_03c_interior_v_ranch_milo_" };
    // Exact author MenyooSP Locations.cpp367, ONeil's Farm (unburnt). This is not a captured mark.
    private static readonly string[] FarmActiveIpls = { "farm", "farm_props", "farmint", "des_farmhs_start_occl", "des_farmhs_startimap" };
    private static readonly string[] FarmInactiveIpls = { "farm_burnt", "farm_burnt_props", "farmint_cap", "des_farmhs_endimap", "des_farmhs_end_occl" };
    private static readonly string[] NoResources = new string[0];
    private string[] ProfileActiveIpls { get { return modern1 ? Modern1ActiveIpls : (farmOriginal ? FarmActiveIpls : (madrazo ? MadrazoIpls : ActiveIpls)); } }
    private string[] ProfileInactiveIpls { get { return modern1 ? Modern1InactiveIpls : (farmOriginal ? FarmInactiveIpls : (madrazo ? NoResources : InactiveIpls)); } }
    private string[] ProfileStyles { get { return modern1 || farmOriginal || madrazo ? NoResources : (caliNoFurniture ? CaliStyles : Styles); } }
    internal string Profile { get { return modern1 ? "apartment_modern1" : (farmOriginal ? "farm_original" : (madrazo ? "madrazo" : (caliNoFurniture ? "cali_sem_mobiliario" : "none"))); } }
    internal bool IsMadrazo { get { return madrazo; } }
    // Farm entry is the existing CASA_VO_INTERIOR captured mark, checked before any IPL/player write.
    internal GTA.Math.Vector3 VisitEntry { get { return modern1 ? new GTA.Math.Vector3(-787.7805f, 334.9232f, 215.8384f) : (farmOriginal ? new GTA.Math.Vector3(2456.232f, 4977.46533f, 46.81026f) : (madrazo ? new GTA.Math.Vector3(1398.08f, 1143.8001f, 114.332f) : AuthorEntry)); } }
    internal string InteriorReferenceInfo { get { return modern1 ? "227329_author_reference_only" : (farmOriginal || madrazo ? "UnknownAuthorId" : "304385"); } }
    internal string ReferenceMatches(int interior) { return modern1 ? (interior == 227329).ToString() : (farmOriginal || madrazo ? "UnknownAuthorId" : (interior == AuthorInteriorReference).ToString()); }
    private bool DesiredIpl(string name) { return !caliNoFurniture || name != FurnitureIpl; }
    private bool DesiredStyle(string name) { return caliNoFurniture && (name == "SET_STYLE_CALI" || name == "SET_STYLE_CALI_TINT"); }
    private int beginMs, lastLogMs, measuredInterior;
    internal int MeasuredInterior { get { return measuredInterior; } }
    internal bool HasBegun { get { return begun; } }
    internal bool CanVisit { get { return styleWriteStarted && Ready(AtEntry()) && AllStylesMatch(); } }

    internal VinewoodRoomNoneProbeDiretor(System.Action<string> durableSnapshotWriter, System.Action<string, string> logger, bool useCaliNoFurniture, bool useMadrazo, bool useFarmOriginal = false, bool useModern1 = false)
    {
        if ((useCaliNoFurniture ? 1 : 0) + (useMadrazo ? 1 : 0) + (useFarmOriginal ? 1 : 0) + (useModern1 ? 1 : 0) > 1)
            throw new System.InvalidOperationException("Only one protected author profile is allowed.");
        caliNoFurniture = useCaliNoFurniture; madrazo = useMadrazo; farmOriginal = useFarmOriginal; modern1 = useModern1;
        if (System.Object.ReferenceEquals(durableSnapshotWriter, null) || System.Object.ReferenceEquals(logger, null))
            throw new System.ArgumentNullException("A durable pre-write snapshot and Native logger are required.");
        persistSnapshot = durableSnapshotWriter; log = logger;
    }

    internal void Begin()
    {
        if (begun || originalIpls.Count != 0) throw new System.InvalidOperationException("Probe cannot be reused.");
        foreach (string name in ProfileActiveIpls) originalIpls.Add(name, Ipl(name));
        foreach (string name in ProfileInactiveIpls) originalIpls.Add(name, Ipl(name));
        CaptureStylesIfReady();
        Save("BEFORE_ANY_IPL_WRITE"); // Root writer must finish and fsync before returning.
        begun = true; beginMs = GTA.Game.GameTime;
        foreach (string name in ProfileInactiveIpls) SetIpl(name, false);
        foreach (string name in ProfileActiveIpls) SetIpl(name, DesiredIpl(name));
        log("C4_VINEWOOD_PROBE", "AuthorEntry=" + VisitEntry + "; AuthorInteriorReference=" + InteriorReferenceInfo + "; Ipls=" + originalIpls.Count + "; Profile=" + Profile + (farmOriginal ? "; EntryOrigin=CASA_VO_INTERIOR_captured_no_jogo; AuthorExteriorEntryNotUsed=True" : "") + "; ProductionReady=False");
    }

    // Returns Native loader/room coherence, never visual or production approval.
    internal bool Tick()
    {
        if (!begun || restored) throw new System.InvalidOperationException("Probe is not active.");
        if (!CanVisit && GTA.Game.GameTime - beginMs > 10000) throw new System.InvalidOperationException("Vinewood Native probe exceeded 10s; restore required.");
        int interior = AtEntry();
        if (measuredInterior > 0 && interior != measuredInterior)
            throw new System.InvalidOperationException("Measured interior changed at the exact author entry: " + interior);
        bool ready = Ready(interior);
        if (ready && measuredInterior == 0) measuredInterior = interior;
        if (GTA.Game.GameTime - lastLogMs >= 500)
        {
            lastLogMs = GTA.Game.GameTime;
            GTA.Ped player = GTA.Game.Player.Character;
            bool exists = !System.Object.ReferenceEquals(player, null) && player.Exists();
            bool valid = interior > 0 && GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_VALID_INTERIOR, interior);
            bool nativeReady = valid && GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_INTERIOR_READY, interior);
            GTA.InteriorProxy proxy = valid ? GTA.InteriorProxy.FromHandle(interior) : null;
            bool proxyExists = !System.Object.ReferenceEquals(proxy, null) && proxy.Exists();
            log("C4_VINEWOOD_NATIVE", "InteriorAtEntry=" + interior + "; AuthorInteriorReference=" + InteriorReferenceInfo + "; AuthorReferenceMatches=" + ReferenceMatches(interior) + "; Ready=" + ready +
                "; NativeValid=" + valid + "; NativeReadyKnown=" + valid + "; NativeReady=" + nativeReady + "; ProxyExists=" + proxyExists +
                "; Disabled=" + (proxyExists ? proxy.IsDisabled.ToString() : "Unknown") + "; Capped=" + (proxyExists ? proxy.IsCapped.ToString() : "Unknown") + "; PlayerInterior=" +
                (exists ? GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_INTERIOR_FROM_ENTITY, player.Handle) : 0) + "; PlayerRoom=" +
                (exists ? player.CurrentInteriorRoomKey : 0) + "; ProductionReady=False");
            if (!ready)
            {
                GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, VisitEntry.X, VisitEntry.Y, VisitEntry.Z);
                log("C4_VINEWOOD_PRELOAD", "RequestCollisionAtAuthorEntry=True; PinWritten=False; DisableWritten=False; CapWritten=False; ReadyBeforeRequest=False; ProductionReady=False");
            }
        }
        if (!ready) return false;
        foreach (string name in ProfileActiveIpls) if (Ipl(name) != DesiredIpl(name)) return false;
        foreach (string name in ProfileInactiveIpls) if (Ipl(name)) return false;
        CaptureStylesIfReady();
        if (!styleWriteStarted)
        {
            styleWriteStarted = true;
            foreach (string name in ProfileStyles)
            {
                bool target = DesiredStyle(name);
                if (SetActive(name) == target) continue;
                changedStyles.Add(name); Save("BEFORE_STYLE_PROFILE:" + name + ":" + target);
                GTA.Native.Function.Call(target ? GTA.Native.Hash.ACTIVATE_INTERIOR_ENTITY_SET : GTA.Native.Hash.DEACTIVATE_INTERIOR_ENTITY_SET, interior, name);
            }
            log("C4_VINEWOOD_PROFILE", "Profile=" + Profile + "; Interior=" + interior + "; EntitySetFlags=" + ProfileStyles.Length +
                "; ExteriorFurnitureIpl=" + (modern1 || madrazo || farmOriginal ? "NotTouched" : Ipl(FurnitureIpl).ToString()) + "; TintIndexWritten=False; EmptyRoomApproved=False; ProductionReady=False");
            if (!madrazo && !farmOriginal && !modern1) GTA.Native.Function.Call(GTA.Native.Hash.REFRESH_INTERIOR, interior);
            return false; // Never certify in the same tick as a Native write.
        }
        foreach (string name in ProfileStyles) if (SetActive(name) != DesiredStyle(name)) return false;
        GTA.Ped actualPlayer = GTA.Game.Player.Character;
        return !System.Object.ReferenceEquals(actualPlayer, null) && actualPlayer.Exists() &&
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_INTERIOR_FROM_ENTITY, actualPlayer.Handle) == interior &&
            actualPlayer.CurrentInteriorRoomKey != 0 && actualPlayer.CurrentInteriorRoomKey != -1;
    }

    // Root restores its player/environment/viewport first. Do not unload support beneath anyone.
    // If this throws, preserve the instance/snapshot and retry normal cleanup; do not Reload.
    internal bool Restore()
    {
        if (restored) return true;
        if (!begun) return true;
        GTA.Ped player = GTA.Game.Player.Character;
        if (!System.Object.ReferenceEquals(player, null) && player.Exists() &&
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_INTERIOR_FROM_ENTITY, player.Handle) == measuredInterior && measuredInterior > 0)
            throw new System.InvalidOperationException("Restore the player outside the probed interior before owned IPL cleanup.");
        if (changedStyles.Count != 0)
        {
            if (!Ready(AtEntry())) throw new System.InvalidOperationException("Original entity-set restoration needs the measured interior Ready.");
            foreach (string name in changedStyles)
            {
                bool original = originalStyles[name];
                if (SetActive(name) != original)
                {
                    Save("RESTORE_STYLE:" + name);
                    GTA.Native.Function.Call(original ? GTA.Native.Hash.ACTIVATE_INTERIOR_ENTITY_SET : GTA.Native.Hash.DEACTIVATE_INTERIOR_ENTITY_SET,
                        measuredInterior, name);
                }
            }
            GTA.Native.Function.Call(GTA.Native.Hash.REFRESH_INTERIOR, measuredInterior);
            foreach (string name in changedStyles) if (SetActive(name) != originalStyles[name]) return false;
        }
        // Restore explicit authored removals first; remove only our originally inactive requests.
        foreach (string name in changedIpls) if (originalIpls[name] && !Ipl(name))
        { Save("RESTORE_ORIGINALLY_ACTIVE:" + name); GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_IPL, name); }
        for (int i = changedIpls.Count - 1; i >= 0; --i)
        {
            string name = changedIpls[i];
            if (!originalIpls[name] && Ipl(name))
            { Save("REMOVE_OWN_REQUEST:" + name); GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_IPL, name); }
        }
        foreach (string name in changedIpls) if (Ipl(name) != originalIpls[name]) return false;
        restored = true; Save("RESTORED_EXACT_ORIGINAL_FLAGS");
        log("C4_VINEWOOD_RESTAURADA", "ChangedIpls=" + changedIpls.Count + "; ChangedStyles=" + changedStyles.Count + "; Verified=True");
        return true;
    }

    // Technical PREP only: proximity may stream a valid enabled interior before Ready.
    // No Pin/Disable/Cap and no production approval; bind the actual handle before player writes.
    internal bool PrepareProtectedVisitCandidate()
    {
        if (!begun || restored) throw new System.InvalidOperationException("Shell loader is not active.");
        if (madrazo && !Ipl(MadrazoIpls[0])) return false;
        if (modern1)
        {
            foreach (string name in Modern1ActiveIpls) if (!Ipl(name)) return false;
            foreach (string name in Modern1InactiveIpls) if (Ipl(name)) return false;
        }
        if (farmOriginal)
        {
            foreach (string name in FarmActiveIpls) if (!Ipl(name)) return false;
            foreach (string name in FarmInactiveIpls) if (Ipl(name)) return false;
        }
        int interior = AtEntry();
        if (interior <= 0 || !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_VALID_INTERIOR, interior)) return false;
        GTA.InteriorProxy proxy = GTA.InteriorProxy.FromHandle(interior);
        if (System.Object.ReferenceEquals(proxy, null) || !proxy.Exists() || proxy.IsDisabled || proxy.IsCapped) return false;
        if (measuredInterior > 0 && measuredInterior != interior) throw new System.InvalidOperationException("Shell visit interior identity changed.");
        if (measuredInterior == 0) { measuredInterior = interior; Save("BEFORE_PROTECTED_AUTHOR_VISIT"); }
        return true;
    }

    private void SetIpl(string name, bool target)
    {
        if (Ipl(name) == target) return;
        changedIpls.Add(name); Save("BEFORE_IPL_WRITE:" + name + ":" + target);
        GTA.Native.Function.Call(target ? GTA.Native.Hash.REQUEST_IPL : GTA.Native.Hash.REMOVE_IPL, name);
    }
    private void CaptureStylesIfReady()
    {
        if (stylesCaptured || !Ready(AtEntry())) return;
        if (measuredInterior == 0) measuredInterior = AtEntry();
        foreach (string name in ProfileStyles) originalStyles.Add(name, SetActive(name));
        stylesCaptured = true;
    }
    private void Save(string phase)
    {
        System.Text.StringBuilder text = new System.Text.StringBuilder("C4_VINEWOOD_PROBE_V1\nPhase=").Append(phase)
            .Append("\nUtc=").Append(System.DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture))
            .Append("\nProfile=").Append(Profile)
            .Append("\nAuthorEntry=").Append(VisitEntry.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
            .Append(VisitEntry.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
            .Append(VisitEntry.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append("\nAuthorInteriorReference=").Append(InteriorReferenceInfo).Append("\nMeasuredInterior=").Append(measuredInterior).Append("\nStylesCaptured=").Append(stylesCaptured).Append('\n');
        if (farmOriginal) text.Append("EntryOrigin=CASA_VO_INTERIOR_captured_no_jogo\n");
        foreach (System.Collections.Generic.KeyValuePair<string, bool> item in originalIpls) text.Append("OriginalIpl|").Append(item.Key).Append('|').Append(item.Value).Append('\n');
        foreach (System.Collections.Generic.KeyValuePair<string, bool> item in originalStyles) text.Append("OriginalStyle|").Append(item.Key).Append('|').Append(item.Value).Append('\n');
        foreach (string name in changedIpls) text.Append("ChangedIpl|").Append(name).Append('\n');
        foreach (string name in changedStyles) text.Append("ChangedStyle|").Append(name).Append('\n');
        persistSnapshot(text.ToString());
    }

    private bool AllStylesMatch()
    { foreach (string name in ProfileStyles) if (SetActive(name) != DesiredStyle(name)) return false; return true; }
    internal void LoadRestoreState(string snapshot)
    {
        if (begun || originalIpls.Count != 0) throw new System.InvalidOperationException("Recovery instance is fresh only.");
        string savedProfile = null;
        foreach (string line in snapshot.Split('\n')) if (line.StartsWith("Profile=", System.StringComparison.Ordinal))
        { if (savedProfile != null) throw new System.InvalidOperationException("Duplicate shell profile."); savedProfile = line.Substring(8); }
        if (savedProfile != null && savedProfile != "none" && savedProfile != "cali_sem_mobiliario" && savedProfile != "madrazo" && savedProfile != "farm_original" && savedProfile != "apartment_modern1")
            throw new System.InvalidOperationException("Recovery shell profile not whitelisted.");
        caliNoFurniture = savedProfile == "cali_sem_mobiliario"; madrazo = savedProfile == "madrazo"; farmOriginal = savedProfile == "farm_original"; modern1 = savedProfile == "apartment_modern1"; // Older None snapshots have no profile line.
        var known = new System.Collections.Generic.HashSet<string>(ProfileActiveIpls, System.StringComparer.Ordinal);
        foreach (string name in ProfileInactiveIpls) known.Add(name);
        var knownStyles = new System.Collections.Generic.HashSet<string>(ProfileStyles, System.StringComparer.Ordinal);
        foreach (string line in snapshot.Split('\n'))
        {
            string[] parts = line.Split('|');
            if (parts.Length == 3 && parts[0] == "OriginalIpl")
            { if (!known.Contains(parts[1]) || originalIpls.ContainsKey(parts[1])) throw new System.InvalidOperationException("Recovery IPL whitelist/duplicate."); originalIpls.Add(parts[1], bool.Parse(parts[2])); }
            else if (parts.Length == 3 && parts[0] == "OriginalStyle")
            { if (!knownStyles.Contains(parts[1]) || originalStyles.ContainsKey(parts[1])) throw new System.InvalidOperationException("Recovery style whitelist/duplicate."); originalStyles.Add(parts[1], bool.Parse(parts[2])); }
            else if (parts.Length == 2 && parts[0] == "ChangedIpl")
            { if (!known.Contains(parts[1]) || changedIpls.Contains(parts[1])) throw new System.InvalidOperationException("Recovery IPL intent."); changedIpls.Add(parts[1]); }
            else if (parts.Length == 2 && parts[0] == "ChangedStyle")
            { if (!knownStyles.Contains(parts[1]) || changedStyles.Contains(parts[1])) throw new System.InvalidOperationException("Recovery style intent."); changedStyles.Add(parts[1]); }
            else if (line.StartsWith("MeasuredInterior=", System.StringComparison.Ordinal)) measuredInterior = int.Parse(line.Substring(17), System.Globalization.CultureInfo.InvariantCulture);
        }
        if (originalIpls.Count != ProfileActiveIpls.Length + ProfileInactiveIpls.Length || (originalStyles.Count != 0 && originalStyles.Count != ProfileStyles.Length) ||
            (changedStyles.Count > 0 && (originalStyles.Count != ProfileStyles.Length || measuredInterior <= 0)))
            throw new System.InvalidOperationException("Recovery snapshot is incomplete.");
        stylesCaptured = originalStyles.Count == ProfileStyles.Length; begun = true;
    }

    private static bool Ipl(string name) { return GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_IPL_ACTIVE, name); }
    private bool SetActive(string name) { return GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_INTERIOR_ENTITY_SET_ACTIVE, measuredInterior, name); }
    private int AtEntry() { GTA.Math.Vector3 entry = VisitEntry; return GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_INTERIOR_AT_COORDS, entry.X, entry.Y, entry.Z); }
    private static bool Ready(int interior)
    {
        if (interior <= 0 || !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_VALID_INTERIOR, interior) ||
            !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_INTERIOR_READY, interior)) return false;
        GTA.InteriorProxy proxy = GTA.InteriorProxy.FromHandle(interior);
        return !System.Object.ReferenceEquals(proxy, null) && proxy.Exists() && !proxy.IsDisabled && !proxy.IsCapped;
    }
}


// Only ensaioshell/vinewood_none. Every Native mutation has a durable intent snapshot.
internal sealed class C4VinewoodShellTrialDiretor
{
    private readonly string root, commandId, pending;
    private readonly System.Action<string, string> log;
    private readonly VinewoodRoomNoneProbeDiretor loader;
    private GTA.Math.Vector3 originalPosition;
    private float originalHeading;
    private int playerHandle, processId, revision, beginMs;
    private System.IntPtr playerAddress;
    private bool visited, restoreRequested, returnIssued, finished;
    private bool originalFrozen, frozenWritten, freezeReleased, floorRayStarted, floorProven;
    private GTA.ShapeTestHandle floorRay;
    private int floorRayAt, modern1FloorRetryAt, modern1FloorAttempts;
    private GTA.Math.Vector3 visitFloorRayFrom, visitFloorRayTo;
    private GTA.Math.Vector3 visitFloorPoint;
    internal bool IsReady { get; private set; }
    internal bool IsRestoring { get { return restoreRequested; } }
    internal int MeasuredInterior { get { return loader.MeasuredInterior; } }
    internal bool IsMadrazo { get { return loader.IsMadrazo; } }
    internal bool IsModern1 { get { return loader.Profile == "apartment_modern1"; } }
    internal bool IsFarmOriginal { get { return loader.Profile == "farm_original"; } }
    internal string Profile { get { return loader.Profile; } }
    internal string InteriorReferenceInfo { get { return loader.InteriorReferenceInfo; } }

    internal C4VinewoodShellTrialDiretor(string dataRoot, string id, System.Action<string, string> logger, bool recovery, bool useCaliNoFurniture, bool useMadrazo, bool useFarmOriginal = false, bool useModern1 = false)
    {
        System.Guid guid;
        if (!System.Guid.TryParseExact(id, "N", out guid)) throw new System.InvalidOperationException("Shell probe requires GUID32.");
        root = System.IO.Path.Combine(System.IO.Path.GetFullPath(dataRoot), "logs", "ensaioshell");
        commandId = id; pending = System.IO.Path.Combine(root, "pendente.txt"); log = logger;
        if (useFarmOriginal)
        {
            // Read-only captured-catalog validation; no visit to an unproved exterior entry.
            var captured = new LocaisDiretor(log).Load(System.IO.Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), "CASA_VO_INTERIOR");
            if ((captured.Position - new GTA.Math.Vector3(2456.232f, 4977.46533f, 46.81026f)).Length() > 0.001f)
                throw new System.InvalidOperationException("Farm captured interior mark changed; review before protected visit.");
            log("FARM_VISIT_ENTRY_CAPTURADA", "Local=CASA_VO_INTERIOR; Entry=" + captured.Position + "; Source=captured_no_jogo; HeadingPreserved=True; MarkWritten=False; InteriorIdUnknown=True; ProductionReady=False");
        }
        loader = new VinewoodRoomNoneProbeDiretor(Persist, log, useCaliNoFurniture, useMadrazo, useFarmOriginal, useModern1);
        GTA.Ped player = GTA.Game.Player.Character;
        if (System.Object.ReferenceEquals(player, null) || !player.Exists() || player.IsDead || player.IsInVehicle())
            throw new System.InvalidOperationException("Shell probe requires actual live player on foot.");
        if (recovery)
        {
            string text = ReadPending();
            if (text.Contains("Phase=RESTORED_EXACT_ORIGINAL_FLAGS")) throw new System.InvalidOperationException("No unresolved shell snapshot.");
            string line = FindLine(text, "OriginalPlayer|"); string[] p = line.Split('|');
            if (p.Length != 9) throw new System.InvalidOperationException("Player snapshot including original frozen flag is incomplete.");
            processId = int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
            playerHandle = int.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
            playerAddress = new System.IntPtr(long.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture));
            originalPosition = new GTA.Math.Vector3(ReadFloat(p[4]), ReadFloat(p[5]), ReadFloat(p[6])); originalHeading = ReadFloat(p[7]);
            originalFrozen = bool.Parse(p[8]); frozenWritten = bool.Parse(FindLine(text, "PlayerFrozenWritten=").Substring(20));
            RequirePlayer(); loader.LoadRestoreState(text); restoreRequested = true;
        }
        else
        {
            if (System.IO.File.Exists(pending) && !ReadPending().Contains("Phase=RESTORED_EXACT_ORIGINAL_FLAGS"))
                throw new System.InvalidOperationException("Previous shell snapshot unresolved; use ensaioshell referencia=restaurar.");
            originalPosition = player.Position; originalHeading = player.Heading; originalFrozen = player.IsPositionFrozen;
            playerHandle = player.Handle; playerAddress = player.MemoryAddress; processId = System.Diagnostics.Process.GetCurrentProcess().Id;
        }
    }

    internal void Begin()
    {
        if (restoreRequested) return;
        RequirePlayer();
        beginMs = GTA.Game.GameTime;
        try { loader.Begin(); }
        catch { restoreRequested = true; throw; }
    }
    internal void Poll()
    {
        if (finished || restoreRequested) return;
        RequirePlayer();
        if (!IsReady && GTA.Game.GameTime - beginMs > 10000)
            throw new System.InvalidOperationException("Shell actual player/collision coherence exceeded 10s; no ARM.");
        bool coherent = loader.Tick();
        if (!visited && loader.PrepareProtectedVisitCandidate())
        {
            frozenWritten = true;
            Persist("Phase=BEFORE_PROTECTED_PREP_VISIT\nMeasuredInterior=" + loader.MeasuredInterior + "\n" + LastLoaderSnapshot());
            GTA.Ped player = GTA.Game.Player.Character;
            GTA.Math.Vector3 entry = loader.VisitEntry;
            player.IsPositionFrozen = true;
            if (!player.IsPositionFrozen) throw new System.InvalidOperationException("Shell own-player freeze not observed; no visit.");
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, entry.X, entry.Y, entry.Z);
            player.Position = entry; // Authored SetPosition/SET_ENTITY_COORDS semantics, PREP only.
            visited = true;
            log("C4_SHELL_VISITA_AUTOR", "Id=" + commandId + "; Entry=" + entry + "; ActualPos=" + player.Position +
                "; VisitBeforeReady=True; FrozenObserved=True; PositionUsesNativeModelOffset=True; HeadingPreserved=true; CapturaDeMarca=False; ProductionReady=False");
            return;
        }
        if (!visited || !coherent) { IsReady = false; return; }
        GTA.Ped actual = GTA.Game.Player.Character;
        bool collision = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, actual.Handle);
        if (!collision) { IsReady = false; return; }
        if (!freezeReleased)
        {
            if (!actual.IsPositionFrozen) throw new System.InvalidOperationException("Shell own freeze changed before floor proof; no repair.");
            if (!PollVisitFloor(actual)) { IsReady = false; return; }
            Persist("Phase=BEFORE_FROZEN_FLAG_RESTORE_AFTER_FLOOR\n" + LastLoaderSnapshot());
            actual.IsPositionFrozen = originalFrozen;
            if (actual.IsPositionFrozen != originalFrozen) throw new System.InvalidOperationException("Shell original freeze flag was not observed after floor proof.");
            freezeReleased = true; IsReady = false; return; // Require a later actual physics/room Tick.
        }
        if (actual.IsPositionFrozen != originalFrozen) throw new System.InvalidOperationException("Shell original freeze flag changed after floor proof.");
        if (!floorProven || actual.IsInAir) { IsReady = false; return; }
        if (!IsReady)
        {
            IsReady = true;
            if (loader.Profile == "apartment_modern1") LogModern1PhysicalPropsReadOnly();
            log("C4_SHELL_NATIVE_COHERENTE", "Id=" + commandId + "; Interior=" + loader.MeasuredInterior +
                "; Profile=" + loader.Profile + "; AuthorInteriorReference=" + loader.InteriorReferenceInfo + "; AuthorReferenceMatches=" + loader.ReferenceMatches(loader.MeasuredInterior) +
                "; Room=" + actual.CurrentInteriorRoomKey + "; Pos=" + actual.Position +
                "; CollisionLoaded=" + collision + "; VisitFloor=" + visitFloorPoint + "; GroundRayObserved=True; OriginalFrozenRestored=True; GeometryApproved=False; EmptyRoomApproved=False; ProductionReady=False");
        }
    }
    private void LogModern1PhysicalPropsReadOnly()
    {
        RequirePlayer();
        GTA.Ped player = GTA.Game.Player.Character;
        if (loader.Profile != "apartment_modern1" || !IsReady || !floorProven ||
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_INTERIOR_FROM_ENTITY, player.Handle) != loader.MeasuredInterior)
            throw new System.InvalidOperationException("Modern1 query requires actual own visit/floor/interior, no production.");
        GTA.Math.Vector3 origin = player.Position;
        GTA.Prop[] pool = GTA.World.GetNearbyProps(origin, 12f);
        if (System.Object.ReferenceEquals(pool, null)) throw new System.InvalidOperationException("Modern1 actual prop pool absent.");
        int reported = 0;
        log("C4_MODERN1_QUERY", "Origin=" + origin + "; Interior=" + loader.MeasuredInterior + "; PlayerRoom=" + player.CurrentInteriorRoomKey +
            "; Radius=12; PoolCount=" + pool.Length + "; MaxInspect=64; Truncated=" + (pool.Length > 64) +
            "; NoHide=True; NoMissionOrPersistentEntityRemoved=True; MarkWritten=False; EmptyRoomApproved=False; ProductionReady=False");
        for (int i = 0; i < pool.Length && i < 64; i++)
        {
            GTA.Prop prop = pool[i];
            if (System.Object.ReferenceEquals(prop, null) || !prop.Exists() || prop.MemoryAddress == System.IntPtr.Zero) continue;
            int handle = prop.Handle; System.IntPtr address = prop.MemoryAddress;
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, handle) ||
                GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, handle) != 3 ||
                !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_AN_OBJECT, handle)) continue;
            int model = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_MODEL, handle); // Actual physical Prop only, never Building.Model.
            GTA.Math.Vector3 position = prop.Position, rotation = prop.Rotation, min = GTA.Math.Vector3.Zero, max = GTA.Math.Vector3.Zero;
            var md = new GTA.Model(model); bool boundsRead = md.IsValid && md.IsInCdImage;
            if (boundsRead) md.GetDimensions(out min, out max);
            int interior = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_INTERIOR_FROM_ENTITY, handle);
            int room = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ROOM_KEY_FROM_ENTITY, handle);
            bool mission = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_A_MISSION_ENTITY, handle);
            bool finite = VecFinite(position) && VecFinite(rotation) && (!boundsRead || VecFinite(min) && VecFinite(max));
            bool identity = prop.Exists() && prop.Handle == handle && prop.MemoryAddress == address &&
                GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, handle) == 3 &&
                GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_MODEL, handle) == model;
            log("C4_MODERN1_PHYSICAL_PROP", "Handle=" + handle + "; Address=" + address.ToInt64().ToString("X", System.Globalization.CultureInfo.InvariantCulture) +
                "; Model=" + model + "; Pos=" + position + "; Rot=" + rotation + "; Interior=" + interior + "; Room=" + room +
                "; SamePlayerRoom=" + (room == player.CurrentInteriorRoomKey && interior == loader.MeasuredInterior) +
                "; Mission=" + mission + "; Persistent=" + prop.IsPersistent + "; Visible=" + prop.IsVisible +
                "; CollisionEnabled=" + prop.IsCollisionEnabled + "; BoundsRead=" + boundsRead + "; RestBoundsMin=" + min + "; RestBoundsMax=" + max +
                "; VectorsFinite=" + finite + "; IdentityStable=" + identity + "; PhysicsBoundsOrFurnitureSemanticsProven=False; Hide=False");
            reported++;
        }
        log("C4_MODERN1_QUERY_FINISHED", "ActualPropsReported=" + reported + "; RootPixelsAndLiteralFurnitureWhitelistRequired=True; ProductionReady=False");
    }
    private static bool VecFinite(GTA.Math.Vector3 v)
    { return !float.IsNaN(v.X) && !float.IsInfinity(v.X) && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y) && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z); }

    internal bool Restore()
    {
        if (finished) return true;
        if (!loader.HasBegun) { finished = true; return true; } // Failed durable save happened before any Native write.
        restoreRequested = true; IsReady = false; RequirePlayer();
        if (!returnIssued)
        {
            Persist("Phase=BEFORE_PLAYER_RETURN\n" + LastLoaderSnapshot());
            GTA.Ped player = GTA.Game.Player.Character;
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, originalPosition.X, originalPosition.Y, originalPosition.Z);
            player.PositionNoOffset = originalPosition; player.Heading = originalHeading; returnIssued = true;
            return false; // Actual interior/room may update only on the following frame.
        }
        GTA.Ped actual = GTA.Game.Player.Character;
        if ((actual.Position - originalPosition).Length() > 0.05f) throw new System.InvalidOperationException("Original player return not measured.");
        if (frozenWritten)
        {
            bool collision = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, actual.Handle);
            if (!collision)
            { GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, originalPosition.X, originalPosition.Y, originalPosition.Z); return false; }
            if (actual.IsPositionFrozen != originalFrozen)
            {
                Persist("Phase=BEFORE_ORIGINAL_FROZEN_FLAG_RESTORE\n" + LastLoaderSnapshot());
                actual.IsPositionFrozen = originalFrozen;
                if (actual.IsPositionFrozen != originalFrozen) throw new System.InvalidOperationException("Original player frozen flag restoration not observed.");
                return false; // Observe the actual returned room after restoring original physics.
            }
            if (actual.IsPositionFrozen != originalFrozen) throw new System.InvalidOperationException("Original player frozen flag restoration not observed.");
        }
        if (loader.MeasuredInterior > 0 && GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_INTERIOR_FROM_ENTITY, actual.Handle) == loader.MeasuredInterior)
            return false;
        if (!loader.Restore()) return false;
        finished = true; log("C4_SHELL_RESTAURO", "Id=" + commandId + "; PlayerIdentity=True; OriginalPosition=" + actual.Position + "; IPLFlagsOriginal=True; ProductionReady=False");
        return true;
    }
    // A short actual map/object ray, ignoring only the original own player.
    // No entity/model cast, no authored Z presented as measured ground.
    private bool PollVisitFloor(GTA.Ped player)
    {
        if (floorProven) return true;
        if (!floorRayStarted)
        {
            if (loader.Profile == "apartment_modern1" && GTA.Game.GameTime < modern1FloorRetryAt) return false;
            GTA.Math.Vector3 pos = player.Position;
            if (!FloorFinite(pos)) throw new System.InvalidOperationException("Shell actual player position is not finite.");
            visitFloorRayFrom = pos + new GTA.Math.Vector3(0f, 0f, 0.2f);
            visitFloorRayTo = pos - new GTA.Math.Vector3(0f, 0f, 2f);
            floorRay = GTA.ShapeTest.StartTestLOSProbe(visitFloorRayFrom, visitFloorRayTo,
                GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, player, (GTA.ShapeTestOptions)0);
            if (loader.Profile == "apartment_modern1") ++modern1FloorAttempts;
            floorRayAt = GTA.Game.GameTime; floorRayStarted = true; return false;
        }
        GTA.ShapeTestResult result; GTA.ShapeTestStatus status = floorRay.GetResult(out result);
        if (status == GTA.ShapeTestStatus.NotReady)
        { if (GTA.Game.GameTime - floorRayAt > 1000) throw new System.InvalidOperationException("Shell short floor ray timed out."); return false; }
        GTA.Math.Vector3 point = result.HitPosition, normal = result.SurfaceNormal, actual = player.Position;
        float gap = actual.Z - point.Z;
        bool valid = status == GTA.ShapeTestStatus.Ready && result.DidHit && FloorFinite(point) && FloorFinite(normal) &&
            System.Math.Abs(point.X-actual.X) <= 0.01f && System.Math.Abs(point.Y-actual.Y) <= 0.01f &&
            normal.Z >= 0.98f && System.Math.Abs(normal.Length()-1f) <= 0.02f && gap > 0f && gap <= 2f;
        log("C4_SHELL_CHAO_MEDIDO", "Id=" + commandId + "; Status=" + status + "; DidHit=" + result.DidHit +
            "; Point=" + point + "; Normal=" + normal + "; PlayerPos=" + actual + "; RootGap=" + gap + "; Valid=" + valid +
            "; PlayerFrozen=" + player.IsPositionFrozen + "; AuthoredZIsGroundProof=False; ProductionReady=False");
        if (loader.Profile == "apartment_modern1")
        {
            int actualInterior = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_INTERIOR_FROM_ENTITY, player.Handle);
            bool collisionLoaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, player.Handle);
            bool interiorReady = loader.MeasuredInterior > 0 && GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_INTERIOR_READY, loader.MeasuredInterior);
            log("C4_MODERN1_FLOOR_STREAM_GETTERS", "Id=" + commandId + "; Attempt=" + modern1FloorAttempts +
                "; GlobalElapsedMs=" + (GTA.Game.GameTime - beginMs) + "; OriginalBudgetMs=10000; From=" + visitFloorRayFrom + "; To=" + visitFloorRayTo +
                "; Status=" + status + "; DidHit=" + result.DidHit + "; HitPoint=" + point + "; Normal=" + normal + "; PlayerPos=" + actual +
                "; CollisionAroundPlayer=" + collisionLoaded + "; InteriorReady=" + interiorReady + "; PlayerInterior=" + actualInterior +
                "; MeasuredInterior=" + loader.MeasuredInterior + "; PlayerRoom=" + player.CurrentInteriorRoomKey + "; PlayerFrozen=" + player.IsPositionFrozen +
                "; InAir=" + player.IsInAir + "; ValidFloor=" + valid + "; LoggedBeforeVerdict=True; GetterOnly=True; ProductionReady=False");
        }
        if (!valid && loader.Profile == "apartment_modern1" && status == GTA.ShapeTestStatus.Ready && !result.DidHit &&
            GTA.Game.GameTime - beginMs <= 10000)
        {
            // A completed miss proves no support yet. It never grants floorProven or releases the own freeze.
            floorRayStarted = false; modern1FloorRetryAt = GTA.Game.GameTime + 250;
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, actual.X, actual.Y, actual.Z);
            IsReady = false; return false; // Same beginMs: global10s and per-ray1s deadlines stay intact.
        }
        if (!valid) throw new System.InvalidOperationException("Shell actual floor not observed; no unfreeze or Ready.");
        visitFloorPoint = point; floorProven = true; return true;
    }
    private static bool FloorFinite(GTA.Math.Vector3 v)
    { return !float.IsNaN(v.X) && !float.IsInfinity(v.X) && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y) && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z); }

    private void RequirePlayer()
    {
        GTA.Ped player = GTA.Game.Player.Character;
        if (processId != System.Diagnostics.Process.GetCurrentProcess().Id || System.Object.ReferenceEquals(player, null) ||
            !player.Exists() || player.Handle != playerHandle || player.MemoryAddress != playerAddress || player.IsDead || player.IsInVehicle())
            throw new System.InvalidOperationException("Original player/process identity changed; keep snapshot, no foreign entity modified.");
    }
    private void Persist(string loaderSnapshot)
    {
        System.IO.Directory.CreateDirectory(root); string folder = System.IO.Path.Combine(root, commandId); System.IO.Directory.CreateDirectory(folder);
        RequirePlain(root); RequirePlain(folder); if (System.IO.File.Exists(pending)) RequirePlain(pending);
        string player = "OriginalPlayer|" + processId + "|" + playerHandle + "|" + playerAddress.ToInt64() + "|" +
            originalPosition.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
            originalPosition.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
            originalPosition.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
            originalHeading.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + originalFrozen + "\n";
        byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes("Command=" + commandId + "\n" + player +
            "PlayerFrozenWritten=" + frozenWritten + "\n" + loaderSnapshot);
        string prefix = (++revision).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        string version = System.IO.Path.Combine(folder, prefix + ".txt"); WriteNew(version, bytes);
        string temporary = System.IO.Path.Combine(folder, prefix + ".pointer.tmp"); WriteNew(temporary, bytes);
        if (System.IO.File.Exists(pending)) System.IO.File.Replace(temporary, pending, System.IO.Path.Combine(folder, prefix + ".pointer-before.txt"));
        else System.IO.File.Move(temporary, pending);
    }
    private string LastLoaderSnapshot()
    { string text = ReadPending(); int i = text.IndexOf("C4_VINEWOOD_PROBE_V1", System.StringComparison.Ordinal); if (i < 0) throw new System.InvalidOperationException("Loader snapshot absent."); return text.Substring(i); }
    private string ReadPending()
    {
        RequirePlain(pending); var file = new System.IO.FileInfo(pending);
        if (!file.Exists || file.Length < 20 || file.Length > 16384) throw new System.InvalidOperationException("Shell snapshot absent/invalid size.");
        return System.IO.File.ReadAllText(pending, new System.Text.UTF8Encoding(false, true));
    }
    private static string FindLine(string text, string prefix)
    { string found = null; foreach (string line in text.Split('\n')) if (line.StartsWith(prefix, System.StringComparison.Ordinal)) { if (found != null) throw new System.InvalidOperationException("Duplicate snapshot field."); found = line; } if (found == null) throw new System.InvalidOperationException("Snapshot field absent."); return found; }
    private static float ReadFloat(string text)
    { float value = float.Parse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture); if (float.IsNaN(value) || float.IsInfinity(value) || System.Math.Abs(value) > 15000f) throw new System.InvalidOperationException("Snapshot coordinate invalid."); return value; }
    private static void RequirePlain(string path)
    { if ((System.IO.File.GetAttributes(path) & System.IO.FileAttributes.ReparsePoint) != 0) throw new System.InvalidOperationException("Shell snapshot path cannot be linked."); }
    private static void WriteNew(string path, byte[] bytes)
    { using (var stream = new System.IO.FileStream(path, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.Read)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); } }
}


// PRIVATE, UNINTEGRATED: append to an existing helper only after Root review.
// This class creates no entity/camera/interior and has no production Ready result.
internal sealed class C4OwnShellModelsProbeDiretor
{
    private static readonly string[] Names = { "v_16_mesh_shell", "v_44_shell", "v_44_shell2" };
    private readonly GTA.Model[] models = new GTA.Model[3];
    private readonly bool[] wasLoaded = new bool[3], requested = new bool[3], verified = new bool[3];
    private readonly System.Action<string, string> log;
    private readonly System.Action requireNoWriter;
    private readonly GTA.Math.Vector3 capturedAnchor;
    private readonly int playerHandle;
    private readonly System.IntPtr playerAddress;
    private int beginMs, lastLog;
    private bool begun, finished, released;
    internal bool QueryFinished { get { return finished; } } // Never means floor/room production-ready.

    internal C4OwnShellModelsProbeDiretor(LocaisDiretor.MarcaLocal capturedMansion, System.Action<string, string> logger, System.Action noWriter)
    {
        if (System.Object.ReferenceEquals(capturedMansion, null) || !Finite(capturedMansion.Position) ||
            System.Object.ReferenceEquals(logger, null) || System.Object.ReferenceEquals(noWriter, null))
            throw new System.InvalidOperationException("C4 model query requires real captured mark and retained Root guard/logger.");
        capturedAnchor = capturedMansion.Position; log = logger; requireNoWriter = noWriter;
        GTA.Ped player = GTA.Game.Player.Character;
        if (System.Object.ReferenceEquals(player, null) || !player.Exists() || player.IsDead || player.IsInVehicle())
            throw new System.InvalidOperationException("C4 model query requires live actual player on foot.");
        playerHandle = player.Handle; playerAddress = player.MemoryAddress;
        for (int i = 0; i < Names.Length; i++) models[i] = new GTA.Model(Names[i]);
    }
    internal void Begin()
    {
        if (begun || released) throw new System.InvalidOperationException("C4 model query cannot be reused.");
        RequireQuiet(); begun = true; beginMs = GTA.Game.GameTime;
        for (int i = 0; i < Names.Length; i++)
        {
            GTA.Model model = models[i]; wasLoaded[i] = model.IsLoaded;
            if (!model.IsInCdImage || !model.IsValid)
            { log("C4_OWN_SHELL_MODEL_UNAVAILABLE", "Name=" + Names[i] + "; Hash=" + model.Hash + "; Requested=False; EntityCreated=False"); continue; }
            log("C4_OWN_SHELL_MODEL_REQUEST_INTENT", "Name=" + Names[i] + "; Hash=" + model.Hash + "; LoadedBefore=" + wasLoaded[i] + "; RequestModelWritten=" + !wasLoaded[i] + "; RequestCollisionWritten=True; EntityCreated=False; MarkWritten=False");
            requested[i] = true; if (!wasLoaded[i]) model.Request(); model.RequestCollision();
            log("C4_OWN_SHELL_MODEL_REQUEST", "Name=" + Names[i] + "; Hash=" + model.Hash + "; LoadedBefore=" + wasLoaded[i] + "; Anchor=" + capturedAnchor + "; EntityCreated=False; ActorMoved=False; NoIPL=True; NoHide=True; ProductionReady=False");
        }
    }
    internal bool Poll()
    {
        if (!begun || released) throw new System.InvalidOperationException("C4 model query is not active.");
        if (finished) return true;
        RequireQuiet(); int elapsed = GTA.Game.GameTime - beginMs;
        if (elapsed <= 0) return false; // Do not certify streaming getters on the request frame.
        bool allSettled = true, emit = lastLog == 0 || elapsed - lastLog >= 500 || elapsed >= 10000; // First eligible actual sample must log even if all3 are already streamed.
        if (emit) lastLog = elapsed;
        for (int i = 0; i < Names.Length; i++)
        {
            if (!requested[i]) { allSettled = false; continue; }
            GTA.Model model = models[i]; bool loaded = model.IsLoaded, collision = model.IsCollisionLoaded;
            GTA.Math.Vector3 min = new GTA.Math.Vector3(), max = new GTA.Math.Vector3(); bool finiteBounds = false;
            if (loaded)
            {
                model.GetDimensions(out min, out max);
                finiteBounds = Finite(min) && Finite(max) && min.X < max.X && min.Y < max.Y && min.Z < max.Z &&
                    max.X - min.X <= 100f && max.Y - min.Y <= 100f && max.Z - min.Z <= 100f;
                if (!finiteBounds) throw new System.InvalidOperationException("C4 own-shell actual restbounds invalid: " + Names[i]);
            }
            verified[i] = loaded && collision && finiteBounds;
            allSettled &= verified[i];
            if (emit) log("C4_OWN_SHELL_MODEL_ACTUAL", "Name=" + Names[i] + "; Hash=" + model.Hash + "; ElapsedMs=" + elapsed +
                "; Loaded=" + loaded + "; CollisionLoaded=" + collision + "; DimensionsRead=" + loaded + "; Min=" + min + "; Max=" + max +
                "; StreamAndRestBoundsVerified=" + verified[i] + "; BoundsKind=ModelRestBounds; OwnColliderMeasured=False; EntityCreated=False; FloorProven=False; EmptyRoomApproved=False; ProductionReady=False");
        }
        if (allSettled || elapsed >= 10000)
        {
            finished = true;
            log("C4_OWN_SHELL_MODEL_QUERY_FINISHED", "ElapsedMs=" + elapsed + "; AllThreeVerified=" + allSettled + "; QueryFinishedOnly=True; EntityCreated=False; Ready=False; ProductionReady=False");
        }
        return finished;
    }
    internal bool StreamAndRestBoundsVerified(string literal)
    {
        if (!finished || released) return false;
        for (int i = 0; i < Names.Length; i++) if (literal == Names[i]) return verified[i];
        throw new System.InvalidOperationException("C4 shell query allows exactly three literal models.");
    }
    internal void ReleaseOwnRequests()
    {
        if (released) return;
        requireNoWriter();
        for (int i = 0; i < Names.Length; i++) if (requested[i])
        {
            if (!wasLoaded[i]) models[i].MarkAsNoLongerNeeded();
            requested[i] = false; // A successful release is not repeated if a later request cleanup throws.
        }
        released = true;
        log("C4_OWN_SHELL_MODEL_REQUESTS_RELEASED", "OriginallyLoadedModelsNotReleased=True; EntityDeleted=False; WorldChanged=False");
    }
    private void RequireQuiet()
    {
        requireNoWriter();
        if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_SESSION_STARTED) || GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_SESSION_ACTIVE))
            throw new System.InvalidOperationException("C4 shell model query is Story-only.");
        GTA.Ped player = GTA.Game.Player.Character;
        if (System.Object.ReferenceEquals(player, null) || !player.Exists() || player.Handle != playerHandle || player.MemoryAddress != playerAddress ||
            player.IsDead || player.IsInVehicle() || !Finite(player.Position))
            throw new System.InvalidOperationException("C4 shell query actual player changed.");
    }
    private static bool Finite(GTA.Math.Vector3 v) { return Number(v.X) && Number(v.Y) && Number(v.Z); }
    private static bool Number(float v) { return !System.Single.IsNaN(v) && !System.Single.IsInfinity(v); }
}
