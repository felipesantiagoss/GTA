// Candidata isolada; concatenar ao .3.cs apenas na integracao autorizada.
// Origem: Bob74/bob74_ipl 037dd6c71f6894be771e9343008223d7a3305d79,
// dlc_afterhours/nightclubs.lua:165-198,599; lib/common.lua:85-113.
// Bands+Lasers restauraveis. Preenchimento de rosto opt-in usa duas luzes de um frame;
// nenhum outro entity set, IPL, camera, PTFX ou entidade e alterado.
public sealed class LuzesBoateDiretor : System.IDisposable
{
    private enum Stage { Idle, Resolve, Snapshot, Confirm, Active, Restore }
    private static readonly string[] EntitySets = {
        "DJ_01_Lights_03", "DJ_02_Lights_03", "DJ_03_Lights_03", "DJ_04_Lights_03",
        "DJ_01_Lights_04", "DJ_02_Lights_04", "DJ_03_Lights_04", "DJ_04_Lights_04"
    };
    private readonly System.Action<string, string> log;
    private readonly bool[] original = new bool[EntitySets.Length];
    private GTA.Ped actor;
    private int actorHandle, actorModel, interior, cursor;
    private System.IntPtr actorAddress;
    private Stage stage;
    private bool restoreRequired, cycleInactive;
    private uint startedAt, lastBandQuery;
    private bool queriedBand;
    private bool softFillKnown;
    private GTA.Math.Vector3 softKeyPosition, softFillPosition;
    private const uint PrepareTimeoutMs = 5000;

    public LuzesBoateDiretor(System.Action<string, string> log) { this.log = log; }

    public bool IsActive { get { return stage != Stage.Idle; } }
    public bool IsReady { get; private set; }
    public bool HasPendingRestore { get { return restoreRequired; } }
    // Main habilita apenas nas refilmagens EP02_C01/C02, antes de Prepare.
    // False e o padrao de todas as demais cenas. Nao cria fonte persistente.
    public bool SoftFillEnabled { get; set; }

    // ownActor deve pertencer ao elenco criado pelo Diretor. Fora da boate e no-op.
    public void Prepare(string localId, GTA.Ped ownActor)
    {
        if (!Cleanup()) throw Error("a restauracao anterior ficou pendente; nao sobrescrever o registro.");
        if (!System.String.Equals(localId, "BOATE_DENTRO", System.StringComparison.Ordinal))
        {
            IsReady = true;
            return;
        }
        if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw Error("use somente o modo historia.");
        if (System.Object.ReferenceEquals(ownActor, null) || !ownActor.Exists() ||
            ownActor.IsPlayer || ownActor.IsDead || ownActor.MemoryAddress == System.IntPtr.Zero)
            throw Error("o personagem proprio vivo da cena nao foi fornecido.");
        GTA.Math.Vector3 actualPosition = ownActor.Position;
        if (!Finite(actualPosition.X) || !Finite(actualPosition.Y) || !Finite(actualPosition.Z))
            throw Error("a posicao atual do personagem nao e finita.");
        actor = ownActor;
        actorHandle = actor.Handle;
        actorAddress = actor.MemoryAddress;
        actorModel = actor.Model.Hash;
        startedAt = unchecked((uint)GTA.Game.GameTime);
        stage = Stage.Resolve;
        IsReady = false;
        Log("BOATE_LUZES_PREPARO", "Local=" + localId + "; Ator=" + actorHandle +
            "; PosicaoAtual=" + Vector(actualPosition) + "; Interior=aguardando_getter;");
    }

    // Chamar uma vez por Tick durante o preparo; nenhum Wait nem loop de carregamento.
    public void PollLoading()
    {
        if (stage == Stage.Idle || stage == Stage.Active) return;
        if (stage == Stage.Restore) throw Error("restauracao pendente; finalize Cleanup antes de montar.");
        RequireActor();
        if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw Error("o preparo deixou o modo historia.");
        uint now = unchecked((uint)GTA.Game.GameTime);
        if (unchecked(now - startedAt) > PrepareTimeoutMs)
            throw Error("o interior ou a confirmacao das oito entity sets nao concluiu em 5 segundos.");
        if (stage == Stage.Resolve)
        {
            // Interior obtido da entidade fisica atual: nao usar o id fixo do autor FiveM.
            int actualInterior = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_INTERIOR_FROM_ENTITY, actorHandle);
            if (actualInterior <= 0 ||
                !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_VALID_INTERIOR, actualInterior) ||
                !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_INTERIOR_READY, actualInterior)) return;
            interior = actualInterior;
            cursor = 0;
            stage = Stage.Snapshot;
            Log("BOATE_LUZES_INTERIOR", "InteriorReal=" + interior + "; Ator=" + actorHandle +
                "; PosicaoAtual=" + Vector(actor.Position) + "; Pronto=True;");
            return;
        }
        if (!BeginBandQuery(now)) return;
        bool actual = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_INTERIOR_ENTITY_SET_ACTIVE,
            interior, EntitySets[cursor]);
        if (stage == Stage.Snapshot)
        {
            original[cursor] = actual;
            cursor++;
            if (cursor < EntitySets.Length) return;
            // Snapshot COMPLETO e duravel no objeto antes de qualquer setter.
            restoreRequired = true;
            stage = Stage.Restore;
            Log("BOATE_LUZES_SNAPSHOT", "InteriorReal=" + interior + "; " + OriginalStates());
            for (int i = 0; i < EntitySets.Length; i++)
                if (original[i]) GTA.Native.Function.Call(GTA.Native.Hash.DEACTIVATE_INTERIOR_ENTITY_SET,
                    interior, EntitySets[i]);
            GTA.Native.Function.Call(GTA.Native.Hash.REFRESH_INTERIOR, interior);
            cursor = 0;
            cycleInactive = true;
            stage = Stage.Confirm;
            return;
        }
        // Verificar em frames posteriores ao Refresh, um getter dos oito sets por Tick.
        if (actual) cycleInactive = false;
        cursor++;
        if (cursor < EntitySets.Length) return;
        cursor = 0;
        if (!cycleInactive) { cycleInactive = true; return; }
        IsReady = true;
        stage = Stage.Active;
        Log("BOATE_LUZES_INATIVAS", "InteriorReal=" + interior +
            "; OitoGetters=False; ApenasBandsELasers=desativados; OutrosEntitySets=preservados; TesteVisual=pendente;");
    }

    // Um unico getter readonly, rotativo. Nao reaplicar setters na acao.
    public void RunFrame()
    {
        if (stage == Stage.Idle) return;
        if (stage != Stage.Active) throw Error("o preparo das luzes ainda nao terminou.");
        DrawSoftFill();
        uint now = unchecked((uint)GTA.Game.GameTime);
        if (!BeginBandQuery(now)) return;
        bool actual = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_INTERIOR_ENTITY_SET_ACTIVE,
            interior, EntitySets[cursor]);
        if (actual)
        {
            IsReady = false;
            Log("BOATE_LUZES_REATIVADA", "InteriorReal=" + interior + "; EntitySet=" + EntitySets[cursor] +
                "; Ativo=True; NenhumSetterDuranteAcao=True;");
            throw Error("um entity set de luz foi reativada depois do preparo; confira a iluminacao antes de filmar.");
        }
        cursor = (cursor + 1) % EntitySets.Length;
    }

    private void DrawSoftFill()
    {
        if (!SoftFillEnabled) return;
        RequireActor();
        if (!softFillKnown)
        {
            GTA.PedBone headBone = actor.Bones[GTA.Bone.SkelHead];
            if (System.Object.ReferenceEquals(headBone, null) || !headBone.IsValid)
                throw Error("preenchimento opt-in exige a cabeca real do ator proprio.");
            GTA.Math.Vector3 head = headBone.Position;
            GTA.Math.Vector3 forward = actor.ForwardVector;
            forward.Z = 0f;
            float length = forward.Length();
            if (!Finite(head.X) || !Finite(head.Y) || !Finite(head.Z) || !Finite(length) || length < .5f)
                throw Error("preenchimento opt-in recebeu cabeca/direcao invalida.");
            forward /= length;
            GTA.Math.Vector3 right = new GTA.Math.Vector3(forward.Y, -forward.X, 0f);
            // Medidas da cabeca apos a pose pronta. Posições fixas durante a tomada
            // evitam que a luz acompanhe cada oscilacao da danca ou de um gesto.
            softKeyPosition = head + forward * 1.1f + right * 1.2f + new GTA.Math.Vector3(0f, 0f, .5f);
            softFillPosition = head - forward * .4f - right * 1.4f + new GTA.Math.Vector3(0f, 0f, .2f);
            softFillKnown = true;
            Log("BOATE_PREENCHIMENTO_OPTIN", "Ator=" + actorHandle + "; CabecaReal=" + Vector(head) +
                "; LuzPrincipal=" + Vector(softKeyPosition) + "; LuzSuave=" + Vector(softFillPosition) +
                "; RGB=255,220,190; Alcance=4; Intensidades=1.3,0.4; FontesPersistentes=False; TesteVisual=pendente;");
        }
        GTA.World.DrawLightWithRange(softKeyPosition, System.Drawing.Color.FromArgb(255, 220, 190), 4f, 1.3f);
        GTA.World.DrawLightWithRange(softFillPosition, System.Drawing.Color.FromArgb(255, 220, 190), 4f, .4f);
    }

    // Chamar antes da limpeza/remocao do elenco. False preserva o snapshot para retry.
    public bool Cleanup()
    {
        IsReady = false;
        if (!restoreRequired) { Clear(); return true; }
        stage = Stage.Restore;
        try
        {
            if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS))
            {
                Log("BOATE_LUZES_RESTAURACAO_FALHOU", "ModoHistoria=False; SnapshotPreservado=True;");
                return false;
            }
            if (interior <= 0 || !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_VALID_INTERIOR, interior) ||
                !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_INTERIOR_READY, interior))
            {
                Log("BOATE_LUZES_RESTAURACAO_FALHOU", "InteriorReal=" + interior +
                    "; InteriorValidoEPronto=False; SnapshotPreservado=True;");
                return false;
            }
            for (int i = 0; i < EntitySets.Length; i++)
            {
                bool actual = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_INTERIOR_ENTITY_SET_ACTIVE,
                    interior, EntitySets[i]);
                if (actual == original[i]) continue;
                GTA.Native.Function.Call(original[i] ? GTA.Native.Hash.ACTIVATE_INTERIOR_ENTITY_SET :
                    GTA.Native.Hash.DEACTIVATE_INTERIOR_ENTITY_SET, interior, EntitySets[i]);
            }
            GTA.Native.Function.Call(GTA.Native.Hash.REFRESH_INTERIOR, interior);
            bool exact = true;
            for (int i = 0; i < EntitySets.Length; i++)
            {
                bool actual = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_INTERIOR_ENTITY_SET_ACTIVE,
                    interior, EntitySets[i]);
                if (actual != original[i]) exact = false;
            }
            if (!exact)
            {
                Log("BOATE_LUZES_RESTAURACAO_FALHOU", "InteriorReal=" + interior +
                    "; GettersExatos=False; SnapshotPreservado=True; " + OriginalStates());
                return false;
            }
            Log("BOATE_LUZES_RESTAURADAS", "InteriorReal=" + interior +
                "; GettersExatos=True; " + OriginalStates());
            Clear();
            return true;
        }
        catch (System.Exception ex)
        {
            Log("BOATE_LUZES_RESTAURACAO_FALHOU", "InteriorReal=" + interior +
                "; SnapshotPreservado=True; Erro=" + ex.Message);
            return false;
        }
    }

    public void Dispose() { Cleanup(); }

    private void RequireActor()
    {
        if (System.Object.ReferenceEquals(actor, null) || !actor.Exists() || actor.IsPlayer || actor.IsDead ||
            actor.Handle != actorHandle || actor.MemoryAddress != actorAddress || actor.Model.Hash != actorModel)
            throw Error("o personagem proprio perdeu a identidade durante o preparo.");
    }

    private bool BeginBandQuery(uint now)
    {
        if (queriedBand && lastBandQuery == now) return false;
        queriedBand = true;
        lastBandQuery = now;
        return true;
    }

    private string OriginalStates()
    {
        var states = new System.Text.StringBuilder();
        for (int i = 0; i < EntitySets.Length; i++)
            states.Append(EntitySets[i]).Append("=").Append(original[i]).Append("; ");
        return states.ToString();
    }

    private void Clear()
    {
        stage = Stage.Idle;
        restoreRequired = false;
        actor = null;
        actorHandle = actorModel = interior = cursor = 0;
        actorAddress = System.IntPtr.Zero;
        queriedBand = false;
        softFillKnown = false;
        softKeyPosition = softFillPosition = GTA.Math.Vector3.Zero;
        for (int i = 0; i < original.Length; i++) original[i] = false;
    }

    private void Log(string name, string text) { if (log != null) log(name, text); }
    private static bool Finite(float n) { return !System.Single.IsNaN(n) && !System.Single.IsInfinity(n); }
    private static string Number(float n) { return n.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private static string Vector(GTA.Math.Vector3 p) { return "(" + Number(p.X) + "," + Number(p.Y) + "," + Number(p.Z) + ")"; }
    private static System.InvalidOperationException Error(string text) { return new System.InvalidOperationException("Luzes da boate: " + text); }
}
