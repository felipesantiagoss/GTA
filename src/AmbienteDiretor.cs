// Concatenado ao .3.cs publicado: não adicionar diretivas using neste arquivo.
// Remove apenas população ambiental elegível; missões, permanentes e objetos ficam preservados.
public sealed class AmbienteDiretor : System.IDisposable
{
    private readonly System.Action<string, string> log;
    private readonly System.Collections.Generic.HashSet<int> savedPlayerVehicles =
        new System.Collections.Generic.HashSet<int>();
    private readonly System.Collections.Generic.HashSet<int> failedDeletions =
        new System.Collections.Generic.HashSet<int>();
    private GTA.Math.Vector3 center;
    private float radius;
    private int initialPlayerHandle;
    private uint lastSweep;
    private bool active, swept, vehiclesFirst, sweepLogged;
    private const uint SweepIntervalMs = 1000;
    private const int RemovalBudget = 12;

    public AmbienteDiretor(System.Action<string, string> log) { this.log = log; }

    public bool IsActive { get { return active; } }
    public bool IsReady { get; private set; }
    public int PendingCount { get; private set; }

    // Start não remove entidades: a primeira varredura recebe os atores em Update.
    // Nenhum flag global persistente é alterado; todos os controles abaixo valem por frame.
    public void Start(GTA.Math.Vector3 sceneCenter, float sceneRadius, GTA.Ped player)
    {
        RequirePlayer(player);
        if (!Finite(sceneCenter.X) || !Finite(sceneCenter.Y) || !Finite(sceneCenter.Z))
            throw new System.InvalidOperationException("Ambiente: o centro da limpeza deve ter coordenadas finitas.");
        if (!Finite(sceneRadius) || sceneRadius < 5f || sceneRadius > 100f)
            throw new System.InvalidOperationException("Ambiente: use um raio de limpeza entre 5 e 100 metros.");
        Cleanup();
        center = sceneCenter;
        radius = sceneRadius;
        initialPlayerHandle = player.Handle;
        RememberPlayerVehicles(player);
        GTA.Ped actualPlayer = GTA.Game.Player.Character;
        if (!System.Object.ReferenceEquals(actualPlayer, null) && actualPlayer.Exists())
            RememberPlayerVehicles(actualPlayer);
        IsReady = false;
        PendingCount = 0;
        swept = false;
        sweepLogged = false;
        vehiclesFirst = false;
        active = true;
        ApplyFrameControls();
        Log("EVENTOS_BLOQUEADOS", "Modo=por_frame; reações não temporárias de baixa prioridade de NPCs ambientais; " +
            "densidades=0. Missões, eventos de história, policiais e eventos de alta prioridade não são encerrados.");
    }

    public void Update(GTA.Ped player, System.Collections.Generic.IEnumerable<GTA.Ped> actors)
    {
        if (!active) return;
        RequirePlayer(player);
        if (System.Object.ReferenceEquals(actors, null))
            throw new System.InvalidOperationException("Ambiente: a lista de personagens da cena não foi fornecida.");
        ApplyFrameControls();
        uint now = unchecked((uint)GTA.Game.GameTime);
        if (swept && unchecked(now - lastSweep) < SweepIntervalMs) return;
        lastSweep = now;
        swept = true;
        Sweep(player, actors);
    }

    public void Cleanup()
    {
        bool wasActive = active;
        active = false;
        swept = false;
        sweepLogged = false;
        IsReady = false;
        PendingCount = 0;
        initialPlayerHandle = 0;
        savedPlayerVehicles.Clear();
        failedDeletions.Clear();
        // Não chamar setters globais para "restaurar": estes comandos expiram ao mudar o frame.
        if (wasActive)
            Log("EVENTOS_RESTAURADOS", "Chamadas por frame encerradas; flags persistentes não foram alterados. " +
                "NPCs e veículos ambientais removidos não são recriados.");
    }

    public void Dispose() { Cleanup(); }

    private static void ApplyFrameControls()
    {
        // A API 191 documenta este controle como válido somente no frame atual.
        // Exclui policiais/militares; não substitui o controle de polícia do script principal.
        GTA.Ped.RandomPedsBlockingNonTempEventsThisFrame = true;
        GTA.Native.Function.Call(GTA.Native.Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
        GTA.Native.Function.Call(GTA.Native.Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
        GTA.Native.Function.Call(GTA.Native.Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
        GTA.Native.Function.Call(GTA.Native.Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
        GTA.Native.Function.Call(GTA.Native.Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f, 0f);
    }

    private void Sweep(GTA.Ped player, System.Collections.Generic.IEnumerable<GTA.Ped> actors)
    {
        System.Collections.Generic.HashSet<int> protectedPeds = new System.Collections.Generic.HashSet<int>();
        System.Collections.Generic.HashSet<int> protectedVehicles =
            new System.Collections.Generic.HashSet<int>(savedPlayerVehicles);
        if (initialPlayerHandle != 0) protectedPeds.Add(initialPlayerHandle);
        ProtectPed(player, protectedPeds, protectedVehicles);
        ProtectPed(GTA.Game.Player.Character, protectedPeds, protectedVehicles);
        foreach (GTA.Ped actor in actors) ProtectPed(actor, protectedPeds, protectedVehicles);

        GTA.Ped[] peds = GTA.World.GetNearbyPeds(center, radius);
        GTA.Vehicle[] vehicles = GTA.World.GetNearbyVehicles(center, radius);
        RefreshFailedDeletions();
        int pedIndex = 0, vehicleIndex = 0, attempts = 0, removedPeds = 0, removedVehicles = 0;
        bool vehicleTurn = vehiclesFirst;
        vehiclesFirst = !vehiclesFirst;
        // Alternar evita que uma multidão ocupe todo o orçamento e deixe veículos sem tratamento.
        while (attempts < RemovalBudget && (pedIndex < peds.Length || vehicleIndex < vehicles.Length))
        {
            if ((vehicleTurn && vehicleIndex < vehicles.Length) || pedIndex >= peds.Length)
            {
                GTA.Vehicle vehicle = vehicles[vehicleIndex++];
                if (EligibleVehicle(vehicle, protectedPeds, protectedVehicles))
                {
                    attempts++;
                    if (TryDelete(vehicle, "veículo")) removedVehicles++;
                }
            }
            else
            {
                GTA.Ped ped = peds[pedIndex++];
                if (EligiblePed(ped, protectedPeds, protectedVehicles))
                {
                    attempts++;
                    if (TryDelete(ped, "NPC")) removedPeds++;
                }
            }
            vehicleTurn = !vehicleTurn;
        }

        // Delete pode tornar persistente uma entidade cuja remoção falhou. A falha não deve
        // desaparecer quando o filtro de missão a preservar na varredura seguinte.
        System.Collections.Generic.HashSet<int> pending = new System.Collections.Generic.HashSet<int>(failedDeletions);
        int preservedPeds = 0, preservedVehicles = 0;
        foreach (GTA.Ped ped in peds)
        {
            if (EligiblePed(ped, protectedPeds, protectedVehicles)) pending.Add(ped.Handle);
            else if (!System.Object.ReferenceEquals(ped, null) && ped.Exists()) preservedPeds++;
        }
        foreach (GTA.Vehicle vehicle in vehicles)
        {
            if (EligibleVehicle(vehicle, protectedPeds, protectedVehicles)) pending.Add(vehicle.Handle);
            else if (!System.Object.ReferenceEquals(vehicle, null) && vehicle.Exists()) preservedVehicles++;
        }
        bool wasReady = IsReady;
        PendingCount = pending.Count;
        IsReady = PendingCount == 0;
        if (!sweepLogged || attempts > 0 || PendingCount > 0 || wasReady != IsReady)
        {
            Log("AREA_LIMPA", "raio=" + Number(radius) + "; NPCs_removidos=" + removedPeds +
                "; veículos_removidos=" + removedVehicles + "; tentativas=" + attempts +
                "; NPCs_preservados=" + preservedPeds + "; veículos_preservados=" + preservedVehicles +
                "; pendentes=" + PendingCount + "; pronta=" + IsReady);
            sweepLogged = true;
        }
    }

    private bool EligiblePed(GTA.Ped ped, System.Collections.Generic.HashSet<int> protectedPeds,
        System.Collections.Generic.HashSet<int> protectedVehicles)
    {
        if (ProtectedPed(ped, protectedPeds) || !InsideRadius(ped.Position)) return false;
        GTA.Vehicle vehicle = ped.CurrentVehicle;
        // Preservar também passageiros e motoristas de veículos protegidos.
        return System.Object.ReferenceEquals(vehicle, null) || !vehicle.Exists() ||
            !ProtectedVehicle(vehicle, protectedPeds, protectedVehicles);
    }

    private bool EligibleVehicle(GTA.Vehicle vehicle, System.Collections.Generic.HashSet<int> protectedPeds,
        System.Collections.Generic.HashSet<int> protectedVehicles)
    {
        return !ProtectedVehicle(vehicle, protectedPeds, protectedVehicles) && InsideRadius(vehicle.Position);
    }

    private static bool ProtectedPed(GTA.Ped ped, System.Collections.Generic.HashSet<int> protectedPeds)
    {
        if (System.Object.ReferenceEquals(ped, null) || !ped.Exists()) return true;
        return protectedPeds.Contains(ped.Handle) || ped.IsPlayer || ped.IsPersistent || !Ambient(ped.PopulationType);
    }

    private static bool ProtectedVehicle(GTA.Vehicle vehicle, System.Collections.Generic.HashSet<int> protectedPeds,
        System.Collections.Generic.HashSet<int> protectedVehicles)
    {
        if (System.Object.ReferenceEquals(vehicle, null) || !vehicle.Exists()) return true;
        if (protectedVehicles.Contains(vehicle.Handle) || vehicle.IsPersistent || !Ambient(vehicle.PopulationType))
            return true;
        foreach (GTA.Ped occupant in vehicle.Occupants)
            if (ProtectedPed(occupant, protectedPeds)) return true;
        return false;
    }

    private static bool Ambient(GTA.EntityPopulationType type)
    {
        // IsPersistent lê IS_ENTITY_A_MISSION_ENTITY na API 191. O filtro adicional conserva
        // população desconhecida/permanente/replay/tool, mesmo que não tenha esse flag.
        return type == GTA.EntityPopulationType.RandomAmbient || type == GTA.EntityPopulationType.RandomScenario ||
            type == GTA.EntityPopulationType.RandomParked || type == GTA.EntityPopulationType.RandomPatrol;
    }

    private bool TryDelete(GTA.Entity entity, string label)
    {
        int handle = entity.Handle;
        try
        {
            entity.Delete();
            // Delete zera o Handle do wrapper; consultar o handle original detecta falha real.
            if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, handle))
                throw new System.InvalidOperationException("A entidade ainda existe após a remoção.");
            return true;
        }
        catch (System.Exception ex)
        {
            failedDeletions.Add(handle);
            Log("ERRO_LIMPEZA_AMBIENTE", label + "; handle=" + handle + "; " + ex.ToString());
            return false;
        }
    }

    private void RefreshFailedDeletions()
    {
        System.Collections.Generic.List<int> removed = new System.Collections.Generic.List<int>();
        foreach (int handle in failedDeletions)
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, handle)) removed.Add(handle);
        foreach (int handle in removed) failedDeletions.Remove(handle);
    }

    private void RememberPlayerVehicles(GTA.Ped player)
    {
        RememberVehicle(player.CurrentVehicle, savedPlayerVehicles);
        RememberVehicle(player.LastVehicle, savedPlayerVehicles);
    }

    private static void ProtectPed(GTA.Ped ped, System.Collections.Generic.HashSet<int> protectedPeds,
        System.Collections.Generic.HashSet<int> protectedVehicles)
    {
        if (System.Object.ReferenceEquals(ped, null) || !ped.Exists()) return;
        protectedPeds.Add(ped.Handle);
        RememberVehicle(ped.CurrentVehicle, protectedVehicles);
        RememberVehicle(ped.LastVehicle, protectedVehicles);
    }

    private static void RememberVehicle(GTA.Vehicle vehicle, System.Collections.Generic.HashSet<int> destination)
    {
        if (!System.Object.ReferenceEquals(vehicle, null) && vehicle.Exists()) destination.Add(vehicle.Handle);
    }

    private bool InsideRadius(GTA.Math.Vector3 position)
    {
        double x = position.X - center.X, y = position.Y - center.Y, z = position.Z - center.Z;
        return x * x + y * y + z * z <= (double)radius * radius;
    }

    private static void RequirePlayer(GTA.Ped player)
    {
        if (System.Object.ReferenceEquals(player, null) || !player.Exists())
            throw new System.InvalidOperationException("Ambiente: o jogador não está disponível para proteger a área.");
    }

    private static bool Finite(float value) { return !System.Single.IsNaN(value) && !System.Single.IsInfinity(value); }
    private static string Number(float value) { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private void Log(string level, string message)
    {
        if (System.Object.ReferenceEquals(log, null)) return;
        try { log(level, message); } catch (System.Exception) { }
    }
}
