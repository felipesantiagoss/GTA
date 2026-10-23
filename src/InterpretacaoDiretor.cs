// Helper C#5 concatenável. Só atua nos atores próprios; não altera DNA, roupa nem tarefas de corpo.
public sealed class InterpretacaoDiretor : System.IDisposable
{
    public sealed class ActorSpec
    {
        public string Ator, Expressao, OlharTipo, Alvo;
        public int InicioMs, DuracaoMs;
        public GTA.Math.Vector3 Deslocamento;
    }

    private sealed class Resource
    {
        public string Expression, MaleDictionary, FemaleDictionary, Clip;
        public int MaleLine, FemaleLine;
    }

    private sealed class LookObject
    {
        public GTA.Prop Prop;
        public int Handle, Model;
        public System.IntPtr Address;
        public GTA.Math.Vector3 LocalCenter;
    }

    private sealed class ActorState
    {
        public string Id, Face;
        public GTA.Ped Ped;
        public int Handle, Model, NextFace = -1, NextLook = -1, NextObjectLog = -1;
        public System.IntPtr Address;
        public bool Speaking, FaceTouched;
        public ActorSpec Look;
        public System.Collections.Generic.List<ActorSpec> Specs = new System.Collections.Generic.List<ActorSpec>();
    }

    private const string SourceFile = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Grand Theft Auto V\\menyooStuff\\PedAnimList.txt";
    private const string SourceSha = "2C537060BD97FCF0DACA407DB1436A5D54E4B8DD5274DBC9EF0D8466EF6F21E4";
    private static readonly Resource[] Resources = new Resource[]
    {
        new Resource { Expression = "normal", MaleDictionary = "facials@gen_male@variations@normal", FemaleDictionary = "facials@gen_female@variations@normal", Clip = "mood_normal_1", MaleLine = 59848, FemaleLine = 59762 },
        new Resource { Expression = "chateado", MaleDictionary = "facials@gen_male@base", FemaleDictionary = "facials@gen_female@base", Clip = "mood_sulk_1", MaleLine = 59793, FemaleLine = 59736 },
        new Resource { Expression = "feliz", MaleDictionary = "facials@gen_male@variations@happy", FemaleDictionary = "facials@gen_female@variations@happy", Clip = "mood_happy_1", MaleLine = 59841, FemaleLine = 59755 },
        new Resource { Expression = "machucado", MaleDictionary = "facials@gen_male@variations@injured", FemaleDictionary = "facials@gen_female@variations@injured", Clip = "mood_injured_1", MaleLine = 59845, FemaleLine = 59759 },
        new Resource { Expression = "comraiva", MaleDictionary = "facials@gen_male@variations@angry", FemaleDictionary = "facials@gen_female@variations@angry", Clip = "mood_angry_1", MaleLine = 59839, FemaleLine = 59753 },
        new Resource { Expression = "estressado", MaleDictionary = "facials@gen_male@variations@stressed", FemaleDictionary = "facials@gen_female@variations@stressed", Clip = "mood_stressed_1", MaleLine = 59854, FemaleLine = 59768 },
        new Resource { Expression = "convencido", MaleDictionary = "facials@gen_male@base", FemaleDictionary = "facials@gen_female@base", Clip = "mood_smug_1", MaleLine = 59831, FemaleLine = 59707 }
    };

    private readonly System.Action<string, string> log;
    private readonly System.Collections.Generic.Dictionary<string, ActorState> actors = new System.Collections.Generic.Dictionary<string, ActorState>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.HashSet<string> dictionaries = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.HashSet<string> ownedDictionaries = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, LookObject> lookObjects = new System.Collections.Generic.Dictionary<string, LookObject>(System.StringComparer.Ordinal);
    private ObjetosSerieDiretor objectOwner;
    private GTA.Math.Vector3 anchor, right, forward;
    private uint loadingStart;
    private bool prepared, ready, disposed;

    public InterpretacaoDiretor(System.Action<string, string> writeLog) { log = writeLog; }
    public bool IsReady { get { return prepared && ready && !disposed; } }

    // Resolver concreto: só objetos próprios já validados por este helper podem receber o olhar.
    public void BindLookObjects(ObjetosSerieDiretor ownedObjects)
    {
        Active();
        if (prepared || System.Object.ReferenceEquals(ownedObjects, null)) throw Error("vincule objetos próprios antes de Prepare");
        objectOwner = ownedObjects;
    }

    // Verificação puramente de arquivos, chamada pelo loader antes de desmontar a cena atual.
    public static void CheckSources()
    {
        var lines = new System.Collections.Generic.Dictionary<int, string>();
        foreach (Resource resource in Resources)
        {
            lines.Add(resource.MaleLine, resource.MaleDictionary + " " + resource.Clip);
            lines.Add(resource.FemaleLine, resource.FemaleDictionary + " " + resource.Clip);
        }
        try
        {
            NoLinks(SourceFile);
            using (var stream = new System.IO.FileStream(SourceFile, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                if (stream.Length > 64L * 1024 * 1024) throw Error("a lista de origem excede64 MiB");
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    if (System.BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != SourceSha)
                        throw Error("SHA256 da PedAnimList difere da fonte conferida; pesquisar a nova lista antes de usar");
                stream.Position = 0;
                using (var reader = new System.IO.StreamReader(stream, new System.Text.UTF8Encoding(false, true), true))
                {
                    int number = 0, found = 0; string row, pair;
                    while ((row = reader.ReadLine()) != null)
                    {
                        if (++number > 500000) throw Error("a lista de origem excede500000 linhas");
                        if (!lines.TryGetValue(number, out pair)) continue;
                        if (row.Trim() != pair) throw Error("par facial diverge da linha" + number);
                        if (++found == lines.Count) break;
                    }
                    if (found != lines.Count) throw Error("a lista de origem não contém todas as14 linhas faciais");
                }
            }
            NoLinks(SourceFile);
        }
        catch (System.IO.IOException ex) { throw Error("não foi possível ler a fonte facial: " + ex.Message); }
        catch (System.UnauthorizedAccessException) { throw Error("acesso à fonte facial não permitido"); }
        catch (System.Text.DecoderFallbackException) { throw Error("a fonte facial exige UTF-8 válido"); }
    }

    public static void ValidateSpecs(System.Collections.Generic.IEnumerable<ActorSpec> source)
    {
        if (source == null) throw Error("informe a lista de interpretação");
        var specs = new System.Collections.Generic.List<ActorSpec>();
        foreach (ActorSpec value in source)
        {
            if (value == null || specs.Count >= 64) throw Error("lista de interpretação exige até64 objetos válidos");
            string id = Identifier(value.Ator);
            if (value.InicioMs < 0 || value.DuracaoMs < 100 || value.InicioMs > 180000 || value.DuracaoMs > 180000 ||
                (long)value.InicioMs + value.DuracaoMs > 180000) throw Error("intervalo de interpretação deve ficar entre100ms e180s e terminar até180s");
            if (!System.String.IsNullOrEmpty(value.Expressao)) FindResource(value.Expressao);
            string type = LookType(value);
            if (type != "padrao" && type != "ator" && type != "objeto" && type != "chao" && type != "horizonte" && type != "marca" && type != "desvio")
                throw Error("tipo de olhar não suportado");
            if (type == "ator")
            {
                if (Identifier(value.Alvo) == id) throw Error("olhar para ator exige outro personagem");
            }
            else if (type == "objeto") ObjectIdentifier(value.Alvo);
            else if (!System.String.IsNullOrEmpty(value.Alvo)) throw Error("alvo só se aplica ao olhar de tipo ator ou objeto");
            Vector(value.Deslocamento);
            if (type == "marca")
            {
                if (System.Math.Abs(value.Deslocamento.X) > 10f || System.Math.Abs(value.Deslocamento.Y) > 10f || System.Math.Abs(value.Deslocamento.Z) > 2f)
                    throw Error("destino de olhar relativo deve ficar em direita/frente±10m e altura±2m");
            }
            else if (value.Deslocamento != GTA.Math.Vector3.Zero) throw Error("destino só se aplica ao olhar de tipo marca");
            if (type == "padrao" && System.String.IsNullOrEmpty(value.Expressao)) throw Error("declare ao menos uma expressão ou um olhar específico");
            foreach (ActorSpec earlier in specs)
                if (Identifier(earlier.Ator) == id && value.InicioMs < earlier.InicioMs + earlier.DuracaoMs && earlier.InicioMs < value.InicioMs + value.DuracaoMs)
                    throw Error("interpretações simultâneas para o mesmo ator não são permitidas");
            specs.Add(value);
        }
    }

    public void Prepare(System.Collections.Generic.Dictionary<string, GTA.Ped> ownedActors, System.Collections.Generic.IEnumerable<ActorSpec> source,
        GTA.Math.Vector3 realAnchor, GTA.Math.Vector3 lateral, GTA.Math.Vector3 frontal)
    {
        Active();
        if (prepared) throw Error("limpe a interpretação antes de preparar outra cena");
        if (ownedActors == null || ownedActors.Count > 5) throw Error("informe até5 atores próprios");
        var specs = new System.Collections.Generic.List<ActorSpec>();
        if (source == null) throw Error("informe a lista de interpretação");
        foreach (ActorSpec value in source)
        {
            if (value == null || specs.Count >= 64) throw Error("lista de interpretação exige até64 objetos válidos");
            specs.Add(Copy(value));
        }
        ValidateSpecs(specs);
        Vector(realAnchor); Vector(lateral); Vector(frontal);
        if (System.Math.Abs(lateral.Length() - 1f) > 0.05f || System.Math.Abs(frontal.Length() - 1f) > 0.05f ||
            System.Math.Abs(lateral.X * frontal.X + lateral.Y * frontal.Y + lateral.Z * frontal.Z) > 0.05f)
            throw Error("eixo de interpretação exige vetores unitários perpendiculares");
        if (specs.Count > 0) CheckSources();
        foreach (var entry in ownedActors)
        {
            string id = Identifier(entry.Key); RequirePed(entry.Value);
            if (actors.ContainsKey(id)) throw Error("ator repetido");
            actors.Add(id, new ActorState { Id = id, Ped = entry.Value, Handle = entry.Value.Handle, Model = entry.Value.Model.Hash, Address = entry.Value.MemoryAddress });
        }
        foreach (ActorSpec spec in specs)
        {
            if (!actors.ContainsKey(spec.Ator)) throw Error("personagem de interpretação ausente do elenco: " + spec.Ator);
            if (LookType(spec) == "ator" && !actors.ContainsKey(spec.Alvo)) throw Error("alvo de olhar ausente do elenco: " + spec.Alvo);
            if (LookType(spec) == "objeto" && System.Object.ReferenceEquals(objectOwner, null))
                throw Error("olhar objeto exige BindLookObjects antes de Prepare");
            actors[spec.Ator].Specs.Add(spec);
            if (!System.String.IsNullOrEmpty(spec.Expressao))
            {
                bool male = actors[spec.Ator].Ped.Gender == GTA.Gender.Male;
                AddDictionary(FindResource(spec.Expressao), male);
                AddDictionary(FindResource("normal"), male);
            }
        }
        anchor = realAnchor; right = lateral; forward = frontal;
        loadingStart = unchecked((uint)GTA.Game.GameTime);
        prepared = true; ready = false;
        foreach (string dictionary in dictionaries)
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, dictionary))
            { ownedDictionaries.Add(dictionary); GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, dictionary); }
        Log("INTERPRETACAO_PREPARADA", "Instruções=" + specs.Count + "; recursos faciais de origem conferida; teste visual pendente");
    }

    public void PollLoading()
    {
        RequirePrepared(); if (ready) return;
        RequireActors();
        if (unchecked((uint)GTA.Game.GameTime - loadingStart) > 8000u) throw Error("tempo esgotado ao carregar expressões/colisão para o olhar");
        if (!System.Object.ReferenceEquals(objectOwner, null) && !objectOwner.IsReady) return;
        foreach (string dictionary in dictionaries)
        {
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, dictionary))
            { ownedDictionaries.Add(dictionary); GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, dictionary); return; }
        }
        foreach (ActorState actor in actors.Values)
        {
            foreach (ActorSpec spec in actor.Specs)
            {
                if (LookType(spec) == "objeto" && !lookObjects.ContainsKey(spec.Alvo))
                {
                    GTA.Prop prop = objectOwner.GetProp(spec.Alvo);
                    RequireLookProp(prop);
                    GTA.Math.Vector3 minimum, maximum;
                    prop.Model.GetDimensions(out minimum, out maximum); Vector(minimum); Vector(maximum);
                    lookObjects.Add(spec.Alvo, new LookObject { Prop = prop, Handle = prop.Handle, Model = prop.Model.Hash,
                        Address = prop.MemoryAddress, LocalCenter = (minimum + maximum) * 0.5f });
                }
                if (!System.String.IsNullOrEmpty(spec.Expressao))
                {
                    Resource resource = FindResource(spec.Expressao);
                    string dictionary = Dictionary(resource, actor);
                    float duration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, dictionary, resource.Clip);
                    if (!Finite(duration) || duration <= 0f) throw Error("recurso facial não existe neste modelo/jogo: " + spec.Expressao);
                }
                if (LookType(spec) == "chao")
                {
                    float height; GTA.Math.Vector3 point = actor.Ped.Position + actor.Ped.ForwardVector * 0.8f;
                    if (!GTA.World.GetGroundHeight(point + new GTA.Math.Vector3(0f, 0f, 3f), out height, GTA.GetGroundHeightMode.Normal))
                    { GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, point.Z); return; }
                }
            }
        }
        ready = true;
        Log("INTERPRETACAO_CARREGADA", "Faciais carregados e duração nativa positiva; aparência ainda não confirmada");
    }

    public bool HasExplicitLook(string actorId, int elapsedMs) { return FindSpec(actorId, elapsedMs, true) != null; }
    public bool HasExpression(string actorId, int elapsedMs) { return FindSpec(actorId, elapsedMs, false) != null; }
    public void InvalidateFace(string actorId)
    {
        ActorState actor; if (actorId != null && actors.TryGetValue(actorId.ToLowerInvariant(), out actor)) actor.NextFace = -1;
    }

    public void InvalidateLook(string actorId)
    {
        ActorState actor; if (actorId != null && actors.TryGetValue(actorId.ToLowerInvariant(), out actor)) actor.NextLook = -1;
    }

    // O caller pode executar isto antes de StartSegment, que aplica o olhar automático.
    public System.Collections.Generic.List<string> ReleaseEndedLooks(int elapsedMs)
    {
        RequireReady(); RequireActors();
        var released = new System.Collections.Generic.List<string>();
        foreach (ActorState actor in actors.Values)
            if (actor.Look != null && FindSpec(actor.Id, elapsedMs, true) == null)
            { actor.Ped.Task.ClearLookAt(); actor.Look = null; actor.NextLook = -1; released.Add(actor.Id); }
        return released;
    }

    public void ApplyIdle() { Update(0, new string[0]); }

    public void Update(int elapsedMs, System.Collections.Generic.IEnumerable<string> actorsSpeaking)
    {
        RequireReady(); RequireActors();
        if (elapsedMs < 0 || elapsedMs > 600000) throw Error("tempo de interpretação inválido");
        var speaking = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        if (actorsSpeaking != null) foreach (string id in actorsSpeaking) speaking.Add(Identifier(id));
        ReleaseEndedLooks(elapsedMs);
        foreach (ActorState actor in actors.Values)
        {
            ActorSpec face = FindSpec(actor.Id, elapsedMs, false);
            string expression = face == null ? "" : face.Expressao;
            bool talks = speaking.Contains(actor.Id);
            if (talks)
            {
                // A boca é responsabilidade do Diretor. Nenhum facial é iniciado no falante.
                actor.Speaking = true; actor.NextFace = -1;
            }
            else
            {
                bool changed = actor.Face != expression || actor.Speaking;
                actor.Speaking = false;
                if (expression.Length > 0 && (changed || elapsedMs >= actor.NextFace))
                {
                    PlayFace(actor, expression); actor.NextFace = elapsedMs + 500;
                }
                else if (expression.Length == 0 && !System.String.IsNullOrEmpty(actor.Face))
                {
                    PlayFace(actor, "normal"); actor.NextFace = -1;
                }
                actor.Face = expression;
            }
            ActorSpec look = FindSpec(actor.Id, elapsedMs, true);
            if (look != null && (LookType(look) == "objeto" || actor.Look != look || elapsedMs >= actor.NextLook))
            {
                ApplyLook(actor, look, elapsedMs);
                actor.Look = look; actor.NextLook = elapsedMs + 400;
            }
        }
    }

    public void Reset()
    {
        RequireReady(); RequireActors();
        foreach (ActorState actor in actors.Values)
        {
            if (actor.Look != null) actor.Ped.Task.ClearLookAt();
            if (actor.FaceTouched) PlayFace(actor, "normal");
            actor.Look = null; actor.Face = null; actor.Speaking = false; actor.FaceTouched = false; actor.NextFace = -1; actor.NextLook = -1; actor.NextObjectLog = -1;
        }
        ApplyIdle();
    }

    public void Cleanup()
    {
        foreach (ActorState actor in actors.Values)
        {
            if (!StillOwn(actor)) continue;
            try
            {
                if (actor.Look != null) actor.Ped.Task.ClearLookAt();
                if (actor.FaceTouched)
                {
                    string dictionary = Dictionary(FindResource("normal"), actor);
                    if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, dictionary)) PlayFace(actor, "normal");
                }
            }
            catch (System.Exception ex) { Log("ERRO_INTERPRETACAO_LIMPEZA", ex.Message); }
        }
        foreach (string dictionary in ownedDictionaries)
        {
            try { GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT, dictionary); }
            catch (System.Exception ex) { Log("ERRO_INTERPRETACAO_RECURSO", ex.Message); }
        }
        actors.Clear(); dictionaries.Clear(); ownedDictionaries.Clear(); lookObjects.Clear(); objectOwner = null; prepared = false; ready = false;
    }

    public void Dispose() { if (disposed) return; Cleanup(); disposed = true; }

    private void ApplyLook(ActorState actor, ActorSpec spec, int elapsedMs)
    {
        string type = LookType(spec);
        const GTA.LookAtFlags flags = GTA.LookAtFlags.SlowTurnRate | GTA.LookAtFlags.NarrowYawLimit;
        if (type == "ator")
        {
            actor.Ped.Task.LookAt(actors[spec.Alvo].Ped, 650, flags, GTA.LookAtPriority.VeryHigh); return;
        }
        GTA.Math.Vector3 point;
        if (type == "objeto")
        {
            LookObject item = lookObjects[spec.Alvo];
            GTA.Prop prop = objectOwner.GetProp(spec.Alvo); RequireLookProp(prop);
            if (prop.Handle != item.Handle || prop.Model.Hash != item.Model || prop.MemoryAddress != item.Address)
                throw Error("a identidade do alvo de olhar mudou: " + spec.Alvo);
            if (!objectOwner.IsObjectVisualSourceConfigured(spec.Alvo)) throw Error("objeto de olhar não tem fonte visual própria configurada: " + spec.Alvo);
            point = objectOwner.GetObjectVisualCenter(spec.Alvo, item.LocalCenter); Vector(point);
            GTA.Math.Vector3 head = actor.Ped.Bones[GTA.Bone.SkelHead].Position; Vector(head);
            if ((point - head).Length() > 20f) throw Error("objeto de olhar saiu do alcance físico: " + spec.Alvo);
            if (elapsedMs >= actor.NextObjectLog)
            {
                Log("OLHAR_OBJETO_GETTERS", "Alvo=" + spec.Alvo + "; Ator=" + actor.Id + "; TempoMs=" + elapsedMs +
                    "; Handle=" + prop.Handle + "; Modelo=" + prop.Model.Hash + "; CentroMedido=" + point +
                    "; Cabeca=" + head + "; Anexado=" + prop.IsAttached() + "; Visivel=" + prop.IsVisible);
                actor.NextObjectLog = elapsedMs + 1000;
            }
        }
        else if (type == "marca") point = anchor + right * spec.Deslocamento.X + forward * spec.Deslocamento.Y + new GTA.Math.Vector3(0f, 0f, spec.Deslocamento.Z);
        else
        {
            GTA.Math.Vector3 head = actor.Ped.Bones[GTA.Bone.SkelHead].Position;
            Vector(head);
            if (type == "horizonte") point = head + actor.Ped.ForwardVector * 10f;
            else if (type == "desvio") point = head + actor.Ped.RightVector * 1.5f + actor.Ped.ForwardVector * 4f;
            else
            {
                point = actor.Ped.Position + actor.Ped.ForwardVector * 0.8f;
                float ground;
                if (!GTA.World.GetGroundHeight(point + new GTA.Math.Vector3(0f, 0f, 3f), out ground, GTA.GetGroundHeightMode.Normal))
                    throw Error("colisão do chão ainda indisponível para o olhar: " + actor.Id);
                point.Z = ground + 0.05f;
            }
        }
        actor.Ped.Task.LookAt(point, 650, flags, GTA.LookAtPriority.VeryHigh);
    }

    private ActorSpec FindSpec(string id, int elapsedMs, bool look)
    {
        ActorState actor; if (id == null || !actors.TryGetValue(id.ToLowerInvariant(), out actor)) return null;
        foreach (ActorSpec spec in actor.Specs)
            if (elapsedMs >= spec.InicioMs && elapsedMs < spec.InicioMs + spec.DuracaoMs &&
                (look ? LookType(spec) != "padrao" : !System.String.IsNullOrEmpty(spec.Expressao))) return spec;
        return null;
    }

    private static void PlayFace(ActorState actor, string expression)
    {
        Resource resource = FindResource(expression);
        actor.Ped.PlayFacialAnimation(new GTA.CrClipAsset(Dictionary(resource, actor), resource.Clip));
        actor.FaceTouched = true;
    }
    private void AddDictionary(Resource resource, bool male) { dictionaries.Add(male ? resource.MaleDictionary : resource.FemaleDictionary); }
    private static string Dictionary(Resource resource, ActorState actor) { return actor.Ped.Gender == GTA.Gender.Male ? resource.MaleDictionary : resource.FemaleDictionary; }
    private static Resource FindResource(string expression)
    {
        foreach (Resource resource in Resources) if (resource.Expression == expression) return resource;
        throw Error("expressão não suportada; use normal, chateado, feliz, machucado, comraiva, estressado ou convencido");
    }
    private static ActorSpec Copy(ActorSpec s)
    {
        return new ActorSpec { Ator = Identifier(s.Ator), Expressao = s.Expressao, OlharTipo = LookType(s),
            Alvo = System.String.IsNullOrEmpty(s.Alvo) ? null : LookType(s) == "objeto" ? ObjectIdentifier(s.Alvo) : Identifier(s.Alvo),
            InicioMs = s.InicioMs, DuracaoMs = s.DuracaoMs, Deslocamento = s.Deslocamento };
    }
    private static string LookType(ActorSpec spec) { return System.String.IsNullOrEmpty(spec.OlharTipo) ? "padrao" : spec.OlharTipo; }
    private static string Identifier(string value)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? "", "\\A[A-Za-z][A-Za-z0-9_]{0,47}\\z")) throw Error("identificador de ator inválido");
        return value.ToLowerInvariant();
    }
    private static bool StillOwn(ActorState actor)
    {
        return actor != null && !System.Object.ReferenceEquals(actor.Ped, null) && actor.Ped.Exists() && actor.Ped.Handle == actor.Handle &&
            actor.Ped.MemoryAddress == actor.Address && actor.Ped.Model.Hash == actor.Model;
    }
    private static string ObjectIdentifier(string value)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? "", "\\A[A-Z][A-Z0-9_]{0,47}\\z")) throw Error("identificador de objeto inválido");
        return value;
    }
    private static void RequireLookProp(GTA.Prop prop)
    {
        if (System.Object.ReferenceEquals(prop, null) || !prop.Exists() || !prop.IsPersistent || prop.MemoryAddress == System.IntPtr.Zero)
            throw Error("objeto próprio de olhar inválido");
    }
    private static void RequirePed(GTA.Ped ped)
    {
        if (System.Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsDead || !ped.IsPersistent || ped.MemoryAddress == System.IntPtr.Zero)
            throw Error("ator próprio inválido");
        GTA.Ped player = GTA.Game.Player.Character;
        if (!System.Object.ReferenceEquals(player, null) && player.Handle == ped.Handle) throw Error("o jogador não pode receber interpretação");
    }
    private void RequireActors() { foreach (ActorState actor in actors.Values) if (!StillOwn(actor) || actor.Ped.IsDead) throw Error("a identidade/estado do ator mudou: " + actor.Id); }
    private static void NoLinks(string path)
    {
        for (string current = System.IO.Path.GetFullPath(path); !System.String.IsNullOrEmpty(current); current = System.IO.Path.GetDirectoryName(current))
            if ((System.IO.File.Exists(current) || System.IO.Directory.Exists(current)) && (System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0)
                throw Error("não usar lista facial redirecionada por vínculo");
    }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static void Vector(GTA.Math.Vector3 value) { if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Z)) throw Error("vetor deve ser finito"); }
    private void Active() { if (disposed) throw Error("helper já encerrado"); }
    private void RequirePrepared() { Active(); if (!prepared) throw Error("prepare a interpretação primeiro"); }
    private void RequireReady() { RequirePrepared(); if (!ready) throw Error("aguarde o carregamento entre frames"); }
    private static System.InvalidOperationException Error(string text) { return new System.InvalidOperationException("Interpretação: " + text + "."); }
    private void Log(string kind, string text) { if (log != null) { try { log(kind, text); } catch (System.Exception) { } } }
}
