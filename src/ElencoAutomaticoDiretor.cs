// Concatenado ao .3.cs. Somente aparência: escolher no jogo e guardar os índices reais.
public static class ElencoAutomaticoDiretor
{
    private sealed class Plan
    {
        internal string Code;
        internal int Seed;
        internal VisualDiretor.Visual Visual;
        internal VisualDiretor.Visual OriginalVisual, ComparisonDjanira;
        internal int[,] BaseComponents;
        internal System.IntPtr Address;
        internal System.Diagnostics.Stopwatch GenerationTimer;
        internal System.Action<string, string> Log;
        internal bool Ready, Saved, NeedsApply, StatusTaken;
        internal int Revisions;
        internal string PendingStatus;
        internal readonly System.Collections.Generic.HashSet<string> RejectedPairs =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        internal readonly System.Collections.Generic.Dictionary<string, int> PieceAttempts =
            new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
        internal readonly System.Collections.Generic.List<Adjustment> Adjustments =
            new System.Collections.Generic.List<Adjustment>();
    }

    private sealed class Adjustment
    {
        internal bool Props;
        internal int Slot, ExpectedDrawable, ExpectedTexture, ObservedDrawable, ObservedTexture,
            Drawable, Texture, Attempt, PieceAttempt;
        internal long ElapsedMs;
        internal string Reason, Rejection;
    }

    private sealed class RandomStable
    {
        private uint state;
        internal RandomStable(int seed, string key)
        {
            state = unchecked((uint)seed) ^ 2166136261u;
            foreach (char letter in key) state = unchecked((state ^ letter) * 16777619u);
            if (state == 0) state = 0x6D2B79F5u;
        }
        internal int Next(int count)
        {
            if (count <= 0) throw Error("elenco.variacoes", "nenhuma opção real disponível");
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (int)(state % (uint)count);
        }
        internal float Unit() { return Next(1000001) / 1000000f; }
    }

    private const string GeneratorVersion = "ElencoAutomaticoDiretor.v2";
    private const string AlternativesVersion = "ElencoAutomaticoDiretor.Alternativas.v1";
    public const int GenerationTimeoutMs = 120000;
    private const int MaxRevisions = 16;
    private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
    private static readonly System.Collections.Generic.Dictionary<int, Plan> Plans =
        new System.Collections.Generic.Dictionary<int, Plan>();
    private static VisualDiretor.Visual DjaniraBase;

    public static void SetDjaniraBase(VisualDiretor.Visual visual)
    {
        if (visual == null || visual.Model != "mp_f_freemode_01" || !visual.IsFreemode)
            throw Error("elenco.DJANIRA", "a base precisa conter o rosto completo de mp_f_freemode_01");
        var copy = Copy(visual);
        if (DjaniraBase != null && !SameIdentity(DjaniraBase, copy))
            throw Error("elenco.DJANIRA", "as duas variantes precisam conservar todo o rosto e a máscara original");
        DjaniraBase = copy;
    }

    public static void Generate(GTA.Ped ped, string codigo, int seed, System.Action<string, string> log = null)
    {
        RequirePed(ped);
        var generationTimer = System.Diagnostics.Stopwatch.StartNew();
        string model = ModelFor(codigo);
        uint modelHash = unchecked((uint)new GTA.Model(model).Hash);
        if (unchecked((uint)ped.Model.Hash) != modelHash)
            throw Error("elenco." + codigo + ".modelo", "o ator não usa " + model);
        if (Plans.ContainsKey(ped.Handle))
            throw Error("elenco." + codigo, "já existe geração para esse ator; limpe antes de repetir");
        var random = new RandomStable(seed, GeneratorVersion + ":roupa:" + codigo);
        int[,] components = CaptureComponents(ped);
        int[,] baseComponents = (int[,])components.Clone();
        bool djanira = codigo == "DJANIRA_POBRE" || codigo == "DJANIRA_RICA";
        // A primeira variante não deve ser comparada com sua própria proposta recém-criada.
        VisualDiretor.Visual comparisonDjanira = djanira && DjaniraBase != null ? Copy(DjaniraBase) : null;
        int[,] props = new int[10, 2];
        for (int i = 0; i < 10; i++) { props[i, 0] = -1; props[i, 1] = -1; }
        // Face e máscara ficam nas variações reais originais. Sem capacetes/óculos na boca.
        for (int i = 2; i < 12; i++)
        {
            if (i == 5 || i == 9) continue; // bolsas e coletes não são figurino de conversa.
            // Freemode: conservar juntos braços/torso, camiseta interna e top da base real.
            // Validade de uma peça isolada não comprova a compatibilidade das três malhas.
            if (model == "mp_f_freemode_01" && (i == 3 || i == 8 || i == 11)) continue;
            ChooseComponent(ped, i, components, random);
        }
        if (codigo != "DJANIRA_POBRE")
            foreach (int anchor in new int[] { 2, 6, 7 }) ChooseProp(ped, anchor, props, random);

        VisualDiretor.HeadData head = null;
        if (model == "mp_f_freemode_01")
        {
            head = djanira && DjaniraBase != null ? CopyHead(DjaniraBase.Head) : MakeHead(ped, codigo, seed);
            if (djanira && DjaniraBase != null)
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < 2; j++) components[i, j] = DjaniraBase.Components[i, j];
            head.Hair = HairColour(djanira, random);
            head.HairStreaks = head.Hair;
        }
        if (djanira && DjaniraBase != null)
        {
            // Uma colisão do sorteio não pode transformar as duas variantes no mesmo figurino.
            if (SameClothingComponents(components, DjaniraBase.Components))
            {
                bool different = false;
                foreach (int slot in new int[] { 4, 6, 7, 10 })
                    if (ChooseDifferentComponent(ped, slot, components, DjaniraBase.Components, random))
                    { different = true; break; }
                if (!different) throw Error("elenco.DJANIRA.roupa", "não há alternativa real de roupa dentro do limite de busca; a base foi preservada");
            }
            if (components[2, 0] == DjaniraBase.Components[2, 0] && components[2, 1] == DjaniraBase.Components[2, 1] &&
                head.Hair == DjaniraBase.Head.Hair && head.HairStreaks == DjaniraBase.Head.HairStreaks)
            {
                if (!ChooseDifferentComponent(ped, 2, components, DjaniraBase.Components, random))
                {
                    int colours = Count(GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUM_PED_HAIR_TINTS), "cabelo.cores");
                    if (colours < 2) throw Error("elenco.DJANIRA.cabelo", "o jogo não ofereceu outro cabelo/cor; a base foi preservada");
                    head.Hair = (DjaniraBase.Head.Hair + 1) % colours;
                    head.HairStreaks = head.Hair;
                }
            }
        }
        var visual = new VisualDiretor.Visual(model, modelHash, "", components, props, head);
        if (djanira && DjaniraBase != null && !SameIdentity(DjaniraBase, visual))
            throw Error("elenco.DJANIRA", "a geração alteraria a identidade facial guardada");
        var plan = new Plan { Code = codigo, Seed = seed, Visual = visual, OriginalVisual = Copy(visual),
            ComparisonDjanira = comparisonDjanira, BaseComponents = baseComponents, Address = ped.MemoryAddress,
            GenerationTimer = generationTimer, Log = log, NeedsApply = true };
        if (plan.Address == System.IntPtr.Zero) throw Error("elenco.ator", "o endereço do personagem não está disponível");
        ValidatePlanVisual(plan, visual);
        Plans.Add(ped.Handle, plan);
        // Apply acontece na primeira IsReady: todas as rejeições recuperáveis passam pelo mesmo catch.
    }

    public static bool IsReady(GTA.Ped ped)
    {
        Plan plan = RequirePlan(ped);
        if (plan.Ready) return true;
        RequireBudget(plan);
        try
        {
            if (plan.NeedsApply)
            {
                VisualDiretor.Apply(ped, plan.Visual);
                plan.NeedsApply = false;
                return false;
            }
            if (!VisualDiretor.IsReady(ped)) return false;
            ValidatePlanVisual(plan, plan.Visual);
            plan.Ready = true;
            return true;
        }
        catch (VisualDiretor.VariationRejectedException rejection)
        {
            ReviseUnsavedPlan(ped, plan, rejection);
            return false;
        }
    }

    public static string TakeStatus(GTA.Ped ped)
    {
        Plan plan = RequirePlan(ped);
        string status = plan.PendingStatus;
        plan.PendingStatus = null;
        if (status != null) plan.StatusTaken = true;
        return status;
    }

    public static VisualDiretor.Visual Save(string path, GTA.Ped ped, string codigo, int seed)
    {
        Plan plan = RequirePlan(ped);
        if (plan.Code != codigo || plan.Seed != seed)
            throw Error("elenco." + codigo, "código/seed não correspondem à geração desse ator");
        if (plan.Saved || !System.String.IsNullOrEmpty(plan.Visual.Sha256))
            throw Error("elenco." + codigo, "o visual já foi salvo e será preservado");
        if (!IsReady(ped)) throw Error("elenco." + codigo, "aguarde a aparência inteira terminar entre frames");
        ValidatePlanVisual(plan, plan.Visual);
        if (System.String.IsNullOrWhiteSpace(path)) throw Error("elenco.arquivo", "informe o destino do XML");
        string full = System.IO.Path.GetFullPath(path);
        if (System.IO.Path.GetFileName(full) != codigo + ".xml")
            throw Error("elenco.arquivo", "use exatamente " + codigo + ".xml");
        string directory = System.IO.Path.GetDirectoryName(full);
        NoLinks(directory);
        System.IO.Directory.CreateDirectory(directory);
        NoLinks(directory);
        if (System.IO.File.Exists(full) || System.IO.Directory.Exists(full))
            throw Error("elenco.arquivo", "o perfil já existe; ele será preservado");
        int[,] components = CaptureComponents(ped), props = CaptureProps(ped);
        for (int i = 0; i < 12; i++)
            for (int j = 0; j < 2; j++)
                if (components[i, j] != plan.Visual.Components[i, j])
                    throw Error("elenco." + codigo + ".roupa", "o visual mudou antes de ser guardado");
        for (int i = 0; i < 10; i++)
            if (props[i, 0] != plan.Visual.Props[i, 0] || (props[i, 0] >= 0 && props[i, 1] != plan.Visual.Props[i, 1]))
                throw Error("elenco." + codigo + ".acessorio", "o visual mudou antes de ser guardado");
        if (plan.Visual.IsFreemode && GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_HEAD_BLEND_EYE_COLOR, ped.Handle) != plan.Visual.Head.Eyes)
            throw Error("elenco." + codigo + ".olhos", "a cor mudou antes de ser guardada");
        var captured = new VisualDiretor.Visual(plan.Visual.Model, plan.Visual.ModelHash, "", components, props, CopyHead(plan.Visual.Head));
        ValidatePlanVisual(plan, captured);
        string temp = System.IO.Path.Combine(directory, "." + codigo + "." + System.Guid.NewGuid().ToString("N") + ".tmp");
        bool ownedTemp = false;
        try
        {
            using (var stream = new System.IO.FileStream(temp, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None))
            {
                ownedTemp = true;
                var settings = new System.Xml.XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = true, CloseOutput = false };
                using (var writer = System.Xml.XmlWriter.Create(stream, settings)) WriteVisual(writer, captured, codigo, seed, plan);
                stream.Flush(true);
            }
            var checkedVisual = VisualDiretor.Load(temp, captured.Model);
            if (checkedVisual.FaceSha256 != captured.FaceSha256)
                throw Error("elenco." + codigo + ".rosto", "a gravação não conservou a identidade facial");
            RequirePlan(ped);
            ValidatePlanVisual(plan, checkedVisual);
            NoLinks(directory);
            System.IO.File.Move(temp, full); // nunca substituir um perfil existente.
            ownedTemp = false;
            plan.Saved = true;
            var result = VisualDiretor.Load(full, captured.Model);
            if (result.Sha256 != checkedVisual.Sha256)
                throw Error("elenco.arquivo", "o hash do XML guardado diverge do temporário conferido");
            plan.Visual = result;
            if (codigo == "DJANIRA_POBRE" || codigo == "DJANIRA_RICA") SetDjaniraBase(result);
            return result;
        }
        finally { if (ownedTemp && System.IO.File.Exists(temp)) System.IO.File.Delete(temp); }
    }

    public static void Forget(GTA.Ped ped)
    {
        VisualDiretor.Forget(ped);
        if (!System.Object.ReferenceEquals(ped, null)) Plans.Remove(ped.Handle);
    }

    private static Plan RequirePlan(GTA.Ped ped)
    {
        RequirePed(ped);
        Plan plan;
        if (!Plans.TryGetValue(ped.Handle, out plan)) throw Error("elenco", "não há geração iniciada para esse ator");
        if (ped.MemoryAddress != plan.Address || unchecked((uint)ped.Model.Hash) != plan.Visual.ModelHash)
            throw Error("elenco.ator", "a identidade/endereço do personagem mudou; não alterar uma entidade alheia");
        return plan;
    }

    private static void RequireBudget(Plan plan)
    {
        if (plan.GenerationTimer.ElapsedMilliseconds >= GenerationTimeoutMs)
            throw Error("elenco." + plan.Code, "a geração excedeu 120 segundos; conservar os XMLs existentes");
    }

    private static void ValidatePlanVisual(Plan plan, VisualDiretor.Visual visual)
    {
        VisualDiretor.Visual original = plan.OriginalVisual;
        if (visual.Model != original.Model || visual.ModelHash != original.ModelHash || visual.FaceSha256 != original.FaceSha256)
            throw Error("elenco." + plan.Code + ".rosto", "a alternativa mudaria o rosto/modelo original");
        for (int slot = 0; slot < 2; slot++)
            for (int item = 0; item < 2; item++)
                if (visual.Components[slot, item] != original.Components[slot, item])
                    throw Error("elenco." + plan.Code + ".rosto", "face e máscara não são alternativas de figurino");
        if (original.IsFreemode && !VisualDiretor.SameFaceDetails(original, visual))
            throw Error("elenco." + plan.Code + ".rosto", "a alternativa mudaria DNA, olhos, máscara ou overlays");
        if (plan.ComparisonDjanira != null)
        {
            if (!VisualDiretor.SameFaceDetails(plan.ComparisonDjanira, visual))
                throw Error("elenco.DJANIRA.rosto", "as variantes precisam conservar a identidade facial fixa");
            if (VisualDiretor.SameClothes(plan.ComparisonDjanira, visual) || VisualDiretor.SameHair(plan.ComparisonDjanira, visual))
                throw Error("elenco.DJANIRA.figurino", "a alternativa eliminaria a diferença de roupa/cabelo; nenhum XML será salvo");
        }
    }

    private static string PieceKey(bool props, int slot)
    { return (props ? "prop:" : "componente:") + slot.ToString(Inv); }
    private static string PairKey(bool props, int slot, int drawable, int texture)
    { return PieceKey(props, slot) + ":" + drawable.ToString(Inv) + ":" + texture.ToString(Inv); }

    private static VisualDiretor.Visual WithPair(VisualDiretor.Visual source, bool props, int slot, int drawable, int texture)
    {
        int[,] components = (int[,])source.Components.Clone(), accessories = (int[,])source.Props.Clone();
        int[,] pairs = props ? accessories : components;
        pairs[slot, 0] = drawable; pairs[slot, 1] = texture;
        return new VisualDiretor.Visual(source.Model, source.ModelHash, "", components, accessories, CopyHead(source.Head));
    }

    private static bool PairAvailable(GTA.Ped ped, Plan plan, bool props, int slot, int drawable, int texture)
    {
        int[,] current = props ? plan.Visual.Props : plan.Visual.Components;
        if ((current[slot, 0] == drawable && current[slot, 1] == texture) ||
            plan.RejectedPairs.Contains(PairKey(props, slot, drawable, texture))) return false;
        if (props)
        {
            if (drawable != -1 || texture != -1) return false;
        }
        else if (!ped.Style[(GTA.PedComponentType)slot].IsVariationValid(drawable, texture)) return false;
        VisualDiretor.Visual candidate = WithPair(plan.Visual, props, slot, drawable, texture);
        if (plan.ComparisonDjanira != null &&
            (VisualDiretor.SameClothes(plan.ComparisonDjanira, candidate) || VisualDiretor.SameHair(plan.ComparisonDjanira, candidate)))
            return false;
        return true;
    }

    private static bool DifferentDrawable(GTA.Ped ped, Plan plan, int slot, int stage, out int drawable, out int texture)
    {
        drawable = texture = 0;
        var component = ped.Style[(GTA.PedComponentType)slot];
        int count = Count(component.Count, "alternativas.componentes." + slot);
        if (count == 0) return false;
        var random = new RandomStable(plan.Seed, AlternativesVersion + ":" + plan.Code + ":" + slot.ToString(Inv) + ":" + stage.ToString(Inv));
        int start = random.Next(count);
        for (int index = 0; index < System.Math.Min(count, 64); index++)
        {
            RequireBudget(plan);
            int next = (start + index) % count;
            if (next == plan.Visual.Components[slot, 0] || next == plan.OriginalVisual.Components[slot, 0]) continue;
            int textures = Count(GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, ped.Handle, slot, next), "alternativas.texturas." + slot);
            if (textures == 0) continue;
            int nextTexture = random.Next(textures);
            if (!PairAvailable(ped, plan, false, slot, next, nextTexture)) continue;
            drawable = next; texture = nextTexture; return true;
        }
        return false;
    }

    private static void ReviseUnsavedPlan(GTA.Ped ped, Plan plan, VisualDiretor.VariationRejectedException rejection)
    {
        RequirePlan(ped); RequireBudget(plan);
        if (plan.Ready || plan.Saved || !System.String.IsNullOrEmpty(plan.Visual.Sha256)) throw rejection;
        if (plan.Revisions >= MaxRevisions)
            throw Error("elenco." + plan.Code, "atingiu 16 ajustes de figurino; nenhum visual incompleto será salvo. Última rejeição: " + rejection.Message);
        int slot = rejection.Slot;
        bool props = rejection.Props;
        if (props ? (slot != 2 && slot != 6 && slot != 7) :
            (slot < 2 || slot > 11 || slot == 5 || slot == 9 ||
             (plan.Visual.IsFreemode && slot != 2 && slot != 4 && slot != 6 && slot != 7 && slot != 10))) throw rejection;
        int[,] pairs = props ? plan.Visual.Props : plan.Visual.Components;
        if (rejection.ExpectedDrawable != pairs[slot, 0] || rejection.ExpectedTexture != pairs[slot, 1])
            throw Error("elenco." + plan.Code, "a rejeição não corresponde ao candidato atual; interromper sem mudar o XML");
        plan.RejectedPairs.Add(PairKey(props, slot, rejection.ExpectedDrawable, rejection.ExpectedTexture));
        string piece = PieceKey(props, slot);
        int stage;
        if (!plan.PieceAttempts.TryGetValue(piece, out stage)) stage = 0;
        int drawable = 0, texture = 0;
        bool found = false;
        string reason = "";
        if (props)
        {
            drawable = texture = -1;
            found = stage == 0 && PairAvailable(ped, plan, true, slot, drawable, texture);
            reason = "acessório novo recusado; retirar antes de salvar";
        }
        else
        {
            if (stage == 0 && rejection.ExpectedTexture != 0)
            {
                drawable = rejection.ExpectedDrawable; texture = 0;
                found = PairAvailable(ped, plan, false, slot, drawable, texture);
                if (found) reason = "testar textura0 do mesmo drawable, diferente da recusada";
            }
            if (!found && stage < 2)
            {
                found = DifferentDrawable(ped, plan, slot, stage, out drawable, out texture);
                if (found) reason = "testar outro drawable real, distinto e ainda não recusado";
            }
            if (!found && stage < 3)
            {
                stage = 2;
                drawable = plan.BaseComponents[slot, 0]; texture = plan.BaseComponents[slot, 1];
                found = PairAvailable(ped, plan, false, slot, drawable, texture);
                if (found) reason = "usar somente a base capturada antes do sorteio";
            }
        }
        if (!found) throw Error("elenco." + plan.Code + ".alternativas", "não há outra combinação permitida e distinta; face/figurino salvo preservados. Última rejeição: " + rejection.Message);
        VisualDiretor.Visual revised = WithPair(plan.Visual, props, slot, drawable, texture);
        ValidatePlanVisual(plan, revised);
        VisualDiretor.Forget(ped); // Não chamar Forget deste gerador: relógio/base/histórico precisam sobreviver.
        plan.Revisions++;
        plan.PieceAttempts[piece] = stage + 1;
        plan.Visual = revised; plan.Ready = false; plan.NeedsApply = true;
        var adjustment = new Adjustment { Props = props, Slot = slot, ExpectedDrawable = rejection.ExpectedDrawable,
            ExpectedTexture = rejection.ExpectedTexture, ObservedDrawable = rejection.ObservedDrawable,
            ObservedTexture = rejection.ObservedTexture, Drawable = drawable, Texture = texture,
            Attempt = plan.Revisions, PieceAttempt = stage + 1, ElapsedMs = plan.GenerationTimer.ElapsedMilliseconds,
            Reason = reason, Rejection = rejection.Message };
        plan.Adjustments.Add(adjustment);
        if (!plan.StatusTaken) plan.PendingStatus = plan.Code + ": ajustando figurino automaticamente.";
        LogAlternative(plan, adjustment);
        // Próximo Tick chama Apply completo dentro de IsReady; nenhuma recursão/espera bloqueante.
    }

    private static void LogAlternative(Plan plan, Adjustment adjustment)
    {
        if (plan.Log == null) return;
        try
        {
            plan.Log("VISUAL_ALTERNATIVA", "Codigo=" + plan.Code + "; seed=" + plan.Seed.ToString(Inv) +
                "; tipo=" + (adjustment.Props ? "prop" : "componente") + "; peça=" + adjustment.Slot.ToString(Inv) +
                "; esperado=" + adjustment.ExpectedDrawable.ToString(Inv) + "," + adjustment.ExpectedTexture.ToString(Inv) +
                "; observado=" + adjustment.ObservedDrawable.ToString(Inv) + "," + adjustment.ObservedTexture.ToString(Inv) +
                "; novo=" + adjustment.Drawable.ToString(Inv) + "," + adjustment.Texture.ToString(Inv) +
                "; tentativa=" + adjustment.Attempt.ToString(Inv) + "; tentativa_peça=" + adjustment.PieceAttempt.ToString(Inv) +
                "; decorrido_ms=" + adjustment.ElapsedMs.ToString(Inv) + "; motivo=" + adjustment.Reason +
                "; rejeição=" + adjustment.Rejection + "; candidato=true; teste_visual_pendente=true");
        }
        catch { /* Histórico também será escrito no XML real, sem interromper pela mensagem de log. */ }
    }

    private static void ChooseComponent(GTA.Ped ped, int slot, int[,] output, RandomStable random)
    {
        var component = ped.Style[(GTA.PedComponentType)slot];
        int count = Count(component.Count, "componentes." + slot);
        if (count == 0) { output[slot, 0] = 0; output[slot, 1] = 0; return; }
        int start = random.Next(count);
        // No máximo64 drawables por slot: não varrer milhares de peças em um Tick.
        for (int attempt = 0; attempt < System.Math.Min(count, 64); attempt++)
        {
            int drawable = (start + attempt) % count;
            int textures = Count(GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, ped.Handle, slot, drawable), "texturas." + slot);
            if (textures == 0) continue;
            int texture = random.Next(textures);
            if (!component.IsVariationValid(drawable, texture)) continue;
            output[slot, 0] = drawable; output[slot, 1] = texture; return;
        }
        if (!component.IsVariationValid(output[slot, 0], output[slot, 1]))
            throw Error("elenco.componentes." + slot, "o jogo não ofereceu uma combinação válida");
    }

    private static bool SameClothingComponents(int[,] first, int[,] second)
    {
        for (int slot = 3; slot < 12; slot++)
            for (int j = 0; j < 2; j++) if (first[slot, j] != second[slot, j]) return false;
        return true;
    }

    private static bool ChooseDifferentComponent(GTA.Ped ped, int slot, int[,] output, int[,] previous, RandomStable random)
    {
        var component = ped.Style[(GTA.PedComponentType)slot];
        int count = Count(component.Count, "componentes." + slot);
        if (count == 0) return false;
        int start = random.Next(count);
        for (int attempt = 0; attempt < System.Math.Min(count, 64); attempt++)
        {
            int drawable = (start + attempt) % count;
            int textures = Count(GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS,
                ped.Handle, slot, drawable), "texturas." + slot);
            if (textures == 0) continue;
            int texture = random.Next(textures);
            if (drawable == previous[slot, 0] && texture == previous[slot, 1])
            {
                if (textures < 2) continue;
                texture = (texture + 1) % textures;
            }
            if (!component.IsVariationValid(drawable, texture)) continue;
            output[slot, 0] = drawable; output[slot, 1] = texture; return true;
        }
        return false;
    }

    private static void ChooseProp(GTA.Ped ped, int anchor, int[,] output, RandomStable random)
    {
        int count = Count(GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUMBER_OF_PED_PROP_DRAWABLE_VARIATIONS, ped.Handle, anchor), "acessorios." + anchor);
        if (count == 0 || random.Next(2) == 0) return;
        int start = random.Next(count);
        for (int attempt = 0; attempt < System.Math.Min(count, 64); attempt++)
        {
            int drawable = (start + attempt) % count;
            int textures = Count(GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUMBER_OF_PED_PROP_TEXTURE_VARIATIONS, ped.Handle, anchor, drawable), "acessorios.texturas." + anchor);
            if (textures == 0) continue;
            int texture = random.Next(textures);
            if (!ped.Style[(GTA.PedPropAnchorPoint)anchor].IsVariationValid(drawable + 1, texture)) continue;
            output[anchor, 0] = drawable; output[anchor, 1] = texture; return;
        }
    }

    private static VisualDiretor.HeadData MakeHead(GTA.Ped ped, string code, int seed)
    {
        bool djanira = code == "DJANIRA_POBRE" || code == "DJANIRA_RICA";
        var random = new RandomStable(seed, GeneratorVersion + ":rosto:" + (djanira ? "DJANIRA" : code));
        var heads = new System.Collections.Generic.List<int>();
        // Tipos1/3 são os ranges femininos nativos base/DLC, consultados no próprio jogo.
        foreach (int type in new int[] { 1, 3 })
        {
            int first = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_HEAD_BLEND_FIRST_INDEX, type);
            int count = Count(GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_HEAD_BLEND_NUM_HEADS, type), "cabecas." + type);
            for (int i = 0; i < count; i++)
            {
                long id = (long)first + i;
                if (id >= 0 && id <= 45 && !heads.Contains((int)id)) heads.Add((int)id);
            }
        }
        if (heads.Count == 0) throw Error("elenco.rosto", "o GTA não ofereceu cabeças femininas nativas compatíveis");
        var head = new VisualDiretor.HeadData();
        for (int i = 0; i < 6; i++) head.Parents[i] = heads[random.Next(heads.Count)];
        head.Mix[0] = random.Unit(); head.Mix[1] = random.Unit(); head.Mix[2] = 0f;
        for (int i = 0; i < 20; i++) head.Features[i] = (random.Unit() - 0.5f) * 0.5f;
        // Valores de olhos documentados na referência nativa CitizenFX:7 azul,4 castanho.
        head.Eyes = djanira ? 7 : 4;
        for (int i = 0; i < 13; i++)
        {
            head.Overlays[i, 0] = 255; head.Overlays[i, 1] = -1; head.Overlays[i, 2] = -1;
        }
        int eyebrows = Count(GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_HEAD_OVERLAY_NUM, 2), "sobrancelhas");
        if (eyebrows > 0) { head.Overlays[2, 0] = random.Next(eyebrows); head.Opacity[2] = 1f; }
        if (!djanira)
        {
            int lipstick = Count(GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_HEAD_OVERLAY_NUM, 8), "batom");
            if (lipstick > 0) { head.Overlays[8, 0] = random.Next(lipstick); head.Opacity[8] = 1f; }
        }
        return head;
    }

    private static int HairColour(bool red, RandomStable random)
    {
        int count = Count(GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUM_PED_HAIR_TINTS), "coresCabelo");
        if (count == 0 || count > 256) throw Error("elenco.coresCabelo", "paleta nativa ausente ou incompatível");
        var options = new System.Collections.Generic.List<int>();
        int darkest = -1, darkestSum = System.Int32.MaxValue;
        for (int i = 0; i < count; i++)
        {
            using (var r = new GTA.Native.OutputArgument())
            using (var g = new GTA.Native.OutputArgument())
            using (var b = new GTA.Native.OutputArgument())
            {
                GTA.Native.Function.Call(GTA.Native.Hash.GET_PED_HAIR_TINT_COLOR, i, r, g, b);
                int rv = r.GetResult<int>(), gv = g.GetResult<int>(), bv = b.GetResult<int>();
                if (rv < 0 || rv > 255 || gv < 0 || gv > 255 || bv < 0 || bv > 255) continue;
                if (rv + gv + bv < darkestSum) { darkestSum = rv + gv + bv; darkest = i; }
                if (rv > gv + 30 && rv > bv + 30 && gv >= bv) options.Add(i);
            }
        }
        if (red && options.Count != 0) return options[random.Next(options.Count)];
        if (darkest >= 0) return darkest;
        throw Error("elenco.coresCabelo", "o GTA não devolveu cores RGB válidas");
    }

    private static int[,] CaptureComponents(GTA.Ped ped)
    {
        var result = new int[12, 2];
        for (int i = 0; i < 12; i++)
        {
            if (ped.Style[(GTA.PedComponentType)i].Count == 0) continue;
            result[i, 0] = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_DRAWABLE_VARIATION, ped.Handle, i);
            result[i, 1] = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_TEXTURE_VARIATION, ped.Handle, i);
        }
        return result;
    }

    private static int[,] CaptureProps(GTA.Ped ped)
    {
        var result = new int[10, 2];
        for (int i = 0; i < 10; i++)
        {
            result[i, 0] = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_PROP_INDEX, ped.Handle, i);
            result[i, 1] = result[i, 0] < 0 ? -1 : GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_PROP_TEXTURE_INDEX, ped.Handle, i);
        }
        return result;
    }

    private static VisualDiretor.Visual Copy(VisualDiretor.Visual source)
    { return new VisualDiretor.Visual(source.Model, source.ModelHash, source.Sha256, (int[,])source.Components.Clone(), (int[,])source.Props.Clone(), CopyHead(source.Head)); }

    private static VisualDiretor.HeadData CopyHead(VisualDiretor.HeadData source)
    {
        if (source == null) return null;
        var result = new VisualDiretor.HeadData();
        System.Array.Copy(source.Parents, result.Parents, 6); System.Array.Copy(source.Mix, result.Mix, 3);
        System.Array.Copy(source.Features, result.Features, 20); System.Array.Copy(source.Opacity, result.Opacity, 13);
        System.Array.Copy(source.Overlays, result.Overlays, source.Overlays.Length);
        result.Hair = source.Hair; result.HairStreaks = source.HairStreaks; result.Eyes = source.Eyes; result.IsParent = source.IsParent;
        return result;
    }

    private static bool SameIdentity(VisualDiretor.Visual first, VisualDiretor.Visual second)
    {
        return VisualDiretor.SameFaceDetails(first, second);
    }

    private static void WriteVisual(System.Xml.XmlWriter writer, VisualDiretor.Visual visual, string code, int seed, Plan plan)
    {
        writer.WriteStartDocument(); writer.WriteStartElement("OutfitPedData");
        Field(writer, "ModelHash", "0x" + visual.ModelHash.ToString("X8", Inv)); Field(writer, "Type", "1");
        writer.WriteStartElement("PedProperties"); WritePairs(writer, "PedComps", visual.Components, 12); WritePairs(writer, "PedProps", visual.Props, 10);
        if (visual.Head != null)
        {
            var head = visual.Head;
            writer.WriteStartElement("HeadFeatures"); writer.WriteAttributeString("WasInArray", "true");
            writer.WriteStartElement("ShapeAndSkinTone");
            string[] parents = { "ShapeFatherId", "ShapeMotherId", "ShapeOverrideId", "ToneFatherId", "ToneMotherId", "ToneOverrideId" };
            string[] mixes = { "ShapeVal", "ToneVal", "OverrideVal" };
            for (int i = 0; i < 6; i++) Field(writer, parents[i], head.Parents[i].ToString(Inv));
            for (int i = 0; i < 3; i++) Field(writer, mixes[i], head.Mix[i].ToString("R", Inv));
            Field(writer, "IsP", head.IsParent ? "1" : "0"); writer.WriteEndElement();
            Field(writer, "HairColour", head.Hair.ToString(Inv)); Field(writer, "HairColourStreaks", head.HairStreaks.ToString(Inv)); Field(writer, "EyeColour", head.Eyes.ToString(Inv));
            writer.WriteStartElement("FacialFeatures");
            for (int i = 0; i < 20; i++) Field(writer, "_" + i, head.Features[i].ToString("R", Inv));
            writer.WriteEndElement(); writer.WriteStartElement("Overlays");
            for (int i = 0; i < 13; i++)
            {
                writer.WriteStartElement("_" + i); writer.WriteAttributeString("index", head.Overlays[i, 0].ToString(Inv));
                writer.WriteAttributeString("colour", head.Overlays[i, 1].ToString(Inv)); writer.WriteAttributeString("colourSecondary", head.Overlays[i, 2].ToString(Inv));
                writer.WriteAttributeString("opacity", head.Opacity[i].ToString("R", Inv)); writer.WriteEndElement();
            }
            writer.WriteEndElement(); writer.WriteEndElement();
        }
        writer.WriteEndElement(); writer.WriteStartElement("DiretorVisual");
        Field(writer, "Codigo", code); Field(writer, "Modelo", visual.Model); Field(writer, "Seed", seed.ToString(Inv));
        Field(writer, "GeradorVersao", GeneratorVersion); Field(writer, "FaceSHA256", visual.FaceSha256);
        Field(writer, "PoliticaAlternativas", AlternativesVersion);
        Field(writer, "LimiteGeracaoMs", GenerationTimeoutMs.ToString(Inv));
        Field(writer, "QuantidadeAjustes", plan.Adjustments.Count.ToString(Inv));
        if (plan.ComparisonDjanira != null)
        {
            Field(writer, "IdentidadeDjaniraConferida", "true");
            Field(writer, "FigurinoECabeloDistintosConferidos", "true");
        }
        writer.WriteStartElement("Ajustes");
        foreach (Adjustment adjustment in plan.Adjustments)
        {
            writer.WriteStartElement("Ajuste");
            Field(writer, "Tipo", adjustment.Props ? "prop" : "componente");
            Field(writer, "Peca", adjustment.Slot.ToString(Inv));
            Field(writer, "Esperado", adjustment.ExpectedDrawable.ToString(Inv) + "," + adjustment.ExpectedTexture.ToString(Inv));
            Field(writer, "Observado", adjustment.ObservedDrawable.ToString(Inv) + "," + adjustment.ObservedTexture.ToString(Inv));
            Field(writer, "Novo", adjustment.Drawable.ToString(Inv) + "," + adjustment.Texture.ToString(Inv));
            Field(writer, "Tentativa", adjustment.Attempt.ToString(Inv));
            Field(writer, "TentativaPeca", adjustment.PieceAttempt.ToString(Inv));
            Field(writer, "DecorridoMs", adjustment.ElapsedMs.ToString(Inv));
            Field(writer, "Motivo", adjustment.Reason); Field(writer, "Rejeicao", adjustment.Rejection);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        Field(writer, "Estado", "gerado no jogo; teste visual pendente"); Field(writer, "DataUtc", System.DateTime.UtcNow.ToString("o", Inv));
        Field(writer, "Origem", "GTA Legacy: contagens, texturas e RGB reais do modelo; parâmetros faciais aplicados pelo Diretor");
        Field(writer, "LimiteVisual", "Índices válidos não comprovam tecido, penteado afro, uniforme, tom de pele ou ausência de sobreposição entre roupas.");
        writer.WriteEndElement(); writer.WriteEndElement(); writer.WriteEndDocument();
    }

    private static void WritePairs(System.Xml.XmlWriter writer, string name, int[,] values, int count)
    {
        writer.WriteStartElement(name);
        for (int i = 0; i < count; i++) Field(writer, "_" + i, values[i, 0].ToString(Inv) + "," + values[i, 1].ToString(Inv));
        writer.WriteEndElement();
    }
    private static void Field(System.Xml.XmlWriter writer, string name, string value) { writer.WriteElementString(name, value); }

    private static string ModelFor(string code)
    {
        switch (code)
        {
            case "DJANIRA_POBRE": case "DJANIRA_RICA": case "DINJER": return "mp_f_freemode_01";
            case "DONA_IRACI": return "a_f_o_genstreet_01";
            case "O_CHEFE": return "a_m_m_business_01";
            case "CAPANGA_1": return "s_m_m_highsec_01";
            case "CAPANGA_2": return "s_m_m_highsec_02";
            case "CAPANGA_3": return "a_m_y_business_02";
            case "CAPANGA_4": return "a_m_y_business_03";
            case "COLEGA_1": return "a_m_y_genstreet_01";
            case "COLEGA_2": return "a_m_y_genstreet_02";
            case "COLEGA_3": return "a_m_y_stbla_01";
            case "ATENDENTE": return "u_m_m_bankman";
            default: throw Error("elenco.codigo", "código fora dos13 personagens do roteiro");
        }
    }
    private static int Count(int value, string field)
    {
        if (value < 0 || value > 10001) throw Error("elenco." + field, "contagem nativa inválida ou fora do limite");
        return value;
    }
    private static void NoLinks(string directory)
    {
        for (var item = new System.IO.DirectoryInfo(directory); item != null; item = item.Parent)
            if (item.Exists && (item.Attributes & System.IO.FileAttributes.ReparsePoint) != 0)
                throw Error("elenco.arquivo", "links/junctions não são permitidos no caminho");
    }
    private static void RequirePed(GTA.Ped ped)
    {
        if (System.Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsDead)
            throw Error("elenco.ator", "personagem ausente ou morto");
    }
    private static System.InvalidOperationException Error(string path, string message)
    { return new System.InvalidOperationException(path + ": " + message + "."); }
}
