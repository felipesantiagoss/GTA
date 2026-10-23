// Concatenado ao .3.cs: sem using/Script. Importa somente aparência de um XML Menyoo.
public static class VisualDiretor
{
    public sealed class Visual
    {
        public readonly string Model, Sha256, FaceSha256;
        public readonly uint ModelHash;
        public readonly bool IsFreemode;
        internal readonly int[,] Components, Props;
        internal readonly HeadData Head;

        internal Visual(string model, uint hash, string sha, int[,] components, int[,] props, HeadData head)
        {
            Model = model; ModelHash = hash; Sha256 = sha;
            Components = components; Props = props; Head = head;
            IsFreemode = head != null;
            FaceSha256 = Fingerprint(this);
        }
    }

    internal sealed class HeadData
    {
        internal readonly int[] Parents = new int[6];
        internal readonly float[] Mix = new float[3], Features = new float[20], Opacity = new float[13];
        internal readonly int[,] Overlays = new int[13, 3];
        internal int Hair, HairStreaks, Eyes;
        internal bool IsParent;
    }

    // Somente leituras que existem no GTA V Legacy. Traços, opacidade e cores
    // não possuem os getters de FiveM e não entram neste resultado.
    public sealed class HeadState
    {
        public readonly int[] Parents, OverlayIndexes;
        public readonly float[] Mix;
        public readonly int Eyes;
        public readonly bool IsParent;

        internal HeadState(int[] parents, float[] mix, int[] overlays, int eyes, bool isParent)
        { Parents = parents; Mix = mix; OverlayIndexes = overlays; Eyes = eyes; IsParent = isParent; }
    }

    public static bool TryReadHeadState(GTA.Ped ped, out HeadState state)
    {
        RequirePed(ped); state = null;
        uint model = unchecked((uint)ped.Model.Hash);
        if (model != ModelHash("mp_f_freemode_01") && model != ModelHash("mp_m_freemode_01"))
            throw Error("visual.cabeca", "a leitura de DNA exige um modelo freemode");
        if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_PED_HEAD_BLEND_FINISHED, ped.Handle)) return false;
        // GET_PED_HEAD_BLEND_DATA usa dez campos espaçados em8bytes (80bytes).
        // OutputArgument() da nightly191 só aloca24bytes: não usar para esse bloco.
        byte[] bytes = new byte[80];
        System.IntPtr pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(bytes.Length);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, pointer, bytes.Length);
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_PED_HEAD_BLEND_DATA, ped.Handle, pointer)) return false;
            System.Runtime.InteropServices.Marshal.Copy(pointer, bytes, 0, bytes.Length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
        int[] parents = new int[6], overlays = new int[13];
        float[] mix = new float[3];
        for (int i = 0; i < parents.Length; i++)
        {
            parents[i] = System.BitConverter.ToInt32(bytes, i * 8);
            if (parents[i] < 0 || parents[i] > 45) throw Error("visual.cabeca.DNA", "o getter devolveu um pai inválido");
        }
        for (int i = 0; i < mix.Length; i++)
        {
            mix[i] = System.BitConverter.ToSingle(bytes, 48 + i * 8);
            if (System.Single.IsNaN(mix[i]) || System.Single.IsInfinity(mix[i]) || mix[i] < 0f || mix[i] > 1f)
                throw Error("visual.cabeca.DNA", "o getter devolveu uma mistura inválida");
        }
        for (int i = 0; i < overlays.Length; i++)
        {
            overlays[i] = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_HEAD_OVERLAY, ped.Handle, i);
            if (overlays[i] < 0 || overlays[i] > 255) throw Error("visual.cabeca.overlay." + i, "o getter devolveu um índice inválido");
        }
        state = new HeadState(parents, mix, overlays,
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_HEAD_BLEND_EYE_COLOR, ped.Handle), bytes[72] != 0);
        return true;
    }

    // Dados estruturados para a geração de um candidato ainda não salvo.
    // Aplicação de XML continua exata: este importador nunca escolhe outra peça.
    public sealed class VariationRejectedException : System.InvalidOperationException
    {
        public readonly bool Props;
        public readonly int Slot, ExpectedDrawable, ExpectedTexture, ObservedDrawable, ObservedTexture;

        internal VariationRejectedException(Visual visual, bool props, int slot, int drawable, int texture, string reason)
            : base("visual.PedProperties." + (props ? "PedProps" : "PedComps") + "._" + slot + ": " + visual.Model +
                "; esperado=" + (props ? visual.Props : visual.Components)[slot, 0] + "," + (props ? visual.Props : visual.Components)[slot, 1] +
                "; observado=" + drawable + "," + texture + "; " + reason + ".")
        {
            Props = props; Slot = slot;
            int[,] pairs = props ? visual.Props : visual.Components;
            ExpectedDrawable = pairs[slot, 0]; ExpectedTexture = pairs[slot, 1];
            ObservedDrawable = drawable; ObservedTexture = texture;
        }
    }

    public sealed class HeadOverlayRejectedException : System.InvalidOperationException
    {
        public readonly int Slot, ExpectedIndex, ObservedIndex;
        public readonly float ExpectedOpacity;

        internal HeadOverlayRejectedException(Visual visual, int slot, int observed)
            : base("visual.PedProperties.HeadFeatures.Overlays._" + slot + ": " + visual.Model +
                ": índice esperado=" + visual.Head.Overlays[slot, 0] + "; observado=" + observed +
                "; opacidade solicitada=" + visual.Head.Opacity[slot].ToString(Inv) +
                "; cores solicitadas=" + visual.Head.Overlays[slot, 1] + "," + visual.Head.Overlays[slot, 2] +
                "; o efeito não persistiu após a aplicação e a conferência.")
        {
            Slot = slot; ExpectedIndex = visual.Head.Overlays[slot, 0]; ObservedIndex = observed;
            ExpectedOpacity = visual.Head.Opacity[slot];
        }
    }

    private enum ApplyPhase { Blend, Request, Load, Check, HeadDetails, HeadOverlayRequest, HeadOverlayCheck, Verify, Failed }

    private sealed class Pending
    {
        internal Visual Visual;
        internal GTA.Ped Ped;
        internal System.IntPtr Address;
        internal System.Diagnostics.Stopwatch Timer, PhaseTimer;
        internal ApplyPhase Phase;
        internal int Slot, LastFrame = -1, StableFrames;
        internal int HeadSlot;
        internal readonly int[] HeadRetries = new int[13];
        internal bool Props, Preloaded;
        internal System.Exception Failure;
    }

    public const int ApplicationTimeoutMs = 45000;
    private const int MaxBytes = 1048576, MaxChars = 100000, BlendTimeoutMs = 10000,
        PreloadTimeoutMs = 5000, SettleTimeoutMs = 2000;
    private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
    private static readonly System.Collections.Generic.Dictionary<int, Pending> PendingHeads =
        new System.Collections.Generic.Dictionary<int, Pending>();

    public static Visual Load(string path, string expectedModel)
    {
        if (System.String.IsNullOrWhiteSpace(path)) throw Error("visual", "informe o arquivo XML");
        if (expectedModel == null || !System.Text.RegularExpressions.Regex.IsMatch(expectedModel, "\\A[a-z0-9_]{1,64}\\z"))
            throw Error("visual.modelo", "use o identificador conferido do catálogo");
        byte[] bytes;
        try
        {
            using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                if (stream.Length > MaxBytes) throw Error("visual", "o XML excede 1 MiB");
                bytes = new byte[(int)stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int count = stream.Read(bytes, read, bytes.Length - read);
                    if (count == 0) throw Error("visual", "a leitura do XML foi interrompida");
                    read += count;
                }
            }
        }
        catch (System.IO.IOException) { throw Error("visual", "não foi possível ler " + System.IO.Path.GetFileName(path)); }
        catch (System.UnauthorizedAccessException) { throw Error("visual", "acesso ao XML não permitido"); }
        var doc = new System.Xml.XmlDocument(); doc.XmlResolver = null;
        var settings = new System.Xml.XmlReaderSettings {
            DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = MaxChars, IgnoreComments = true };
        try
        {
            using (var memory = new System.IO.MemoryStream(bytes, false))
            using (var reader = System.Xml.XmlReader.Create(memory, settings))
                while (reader.Read())
                    if (reader.Depth >= 32) throw Error("visual", "o XML excede 32 níveis");
            using (var memory = new System.IO.MemoryStream(bytes, false))
            using (var reader = System.Xml.XmlReader.Create(memory, settings)) doc.Load(reader);
        }
        catch (System.Xml.XmlException ex) { throw Error("visual", "XML inválido ou DTD proibido, linha " + ex.LineNumber); }
        var root = doc.DocumentElement;
        if (root == null || root.Name != "OutfitPedData" || root.NamespaceURI.Length != 0)
            throw Error("visual", "esperado OutfitPedData do Menyoo, sem namespace");
        NoAttributes(root, "visual");
        // Metadados e anexos do Menyoo são apenas dados ignorados, nunca instruções executadas.
        UniqueChildren(root, "visual");
        if (Integer(One(root, "Type", "visual"), "visual.Type", 1, 1) != 1)
            throw Error("visual.Type", "o arquivo precisa representar um personagem");
        string hashText = Scalar(One(root, "ModelHash", "visual"), "visual.ModelHash");
        uint hash;
        bool hexadecimal = System.Text.RegularExpressions.Regex.IsMatch(hashText, "\\A0[xX][0-9A-Fa-f]{1,8}\\z");
        if (hexadecimal)
        {
            if (!System.UInt32.TryParse(hashText.Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier, Inv, out hash))
                throw Error("visual.ModelHash", "hash hexadecimal inválido");
        }
        else if (!System.Text.RegularExpressions.Regex.IsMatch(hashText, "\\A[0-9]{1,10}\\z") ||
            !System.UInt32.TryParse(hashText, System.Globalization.NumberStyles.None, Inv, out hash))
            throw Error("visual.ModelHash", "use o hash original hexadecimal ou decimal do Menyoo");
        if (hash != ModelHash(expectedModel)) throw Error("visual.ModelHash", "o modelo difere de " + expectedModel + " no catálogo");
        var ped = One(root, "PedProperties", "visual"); NoAttributes(ped, "visual.PedProperties");
        UniqueChildren(ped, "visual.PedProperties");
        int[,] components = Pairs(One(ped, "PedComps", "visual.PedProperties"), "visual.PedProperties.PedComps", 12, false);
        int[,] props = Pairs(One(ped, "PedProps", "visual.PedProperties"), "visual.PedProperties.PedProps", 10, true);
        bool freemode = expectedModel == "mp_f_freemode_01" || expectedModel == "mp_m_freemode_01";
        var headNode = Optional(ped, "HeadFeatures");
        if (freemode && headNode == null) throw Error("visual.PedProperties.HeadFeatures", "o freemode exige o rosto completo salvo no Menyoo");
        // Algumas exportações antigas trazem uma cabeça genérica em NPCs.
        // O modelo nativo determina o rosto; esse bloco não se aplica e é ignorado.
        HeadData head = freemode ? ReadHead(headNode) : null;
        return new Visual(expectedModel, hash, Hash(bytes), components, props, head);
    }

    public static void Apply(GTA.Ped ped, Visual visual)
    {
        RequirePed(ped);
        if (visual == null) throw Error("visual", "a aparência não foi carregada");
        if (unchecked((uint)ped.Model.Hash) != visual.ModelHash) throw Error("visual.modelo", "o ator não usa o modelo do XML");
        RemoveDeletedPending();
        if (PendingHeads.ContainsKey(ped.Handle)) throw Error("visual", "a aparência desse ator ainda está sendo aplicada");
        ValidateVariations(ped, visual);
        if (PendingHeads.Count >= 64) throw Error("visual", "há muitas aplicações de aparência pendentes");
        var pending = new Pending { Visual = visual, Ped = ped, Address = ped.MemoryAddress,
            Timer = System.Diagnostics.Stopwatch.StartNew(), PhaseTimer = System.Diagnostics.Stopwatch.StartNew(),
            Phase = visual.IsFreemode ? ApplyPhase.Blend : ApplyPhase.Request };
        PendingHeads.Add(ped.Handle, pending);
        try
        {
            if (visual.IsFreemode)
            {
                var head = visual.Head;
                GTA.Native.Function.Call(GTA.Native.Hash.SET_PED_HEAD_BLEND_DATA, ped.Handle,
                    head.Parents[0], head.Parents[1], head.Parents[2], head.Parents[3], head.Parents[4], head.Parents[5],
                    head.Mix[0], head.Mix[1], head.Mix[2], head.IsParent);
            }
        }
        catch (System.Exception ex) { throw FailPending(pending, ex); }
    }

    // Uma peça por vez: preload, setter, conferência em frames posteriores.
    // XML salvo é aplicado exatamente; nunca adaptar índices ou salvar antes da estabilidade integral.
    public static bool IsReady(GTA.Ped ped)
    {
        RequirePed(ped);
        Pending pending;
        if (!PendingHeads.TryGetValue(ped.Handle, out pending)) return true;
        if (pending.Failure != null) throw pending.Failure;
        try
        {
            if (!HasIdentity(pending) || ped.MemoryAddress != pending.Address)
                throw Error("visual.ator", "a identidade do personagem mudou; nenhuma entidade alheia será alterada");
            if (pending.Timer.ElapsedMilliseconds >= ApplicationTimeoutMs)
                throw Error("visual", pending.Visual.Model + " não terminou a aparência em 45 segundos; fase=" + pending.Phase + "; peça=" + pending.Slot);
            int frame = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_FRAME_COUNT);
            if (pending.LastFrame == frame) return false;
            pending.LastFrame = frame;
            if (pending.Phase == ApplyPhase.Blend)
            {
                if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_PED_HEAD_BLEND_FINISHED, ped.Handle))
                {
                    if (pending.PhaseTimer.ElapsedMilliseconds >= BlendTimeoutMs)
                        throw Error("visual.PedProperties.HeadFeatures", pending.Visual.Model + ": o rosto não terminou de carregar em 10 segundos");
                    return false;
                }
                ValidateVariations(ped, pending.Visual);
                SetPhase(pending, ApplyPhase.Request);
                return false;
            }
            if (pending.Phase == ApplyPhase.Request)
            {
                if (pending.Slot == (pending.Props ? 10 : 12))
                {
                    if (!pending.Props)
                    {
                        ped.Style.ClearProps();
                        pending.Props = true; pending.Slot = 0;
                        SetPhase(pending, ApplyPhase.Request);
                    }
                    else SetPhase(pending, ApplyPhase.HeadDetails);
                    return false;
                }
                int[,] pairs = pending.Props ? pending.Visual.Props : pending.Visual.Components;
                if ((pending.Props && pairs[pending.Slot, 0] < 0) ||
                    (!pending.Props && SkipUnavailableComponent(ped, pending.Visual, pending.Slot)))
                { pending.Slot++; return false; }
                if (pending.Props)
                    ped.Style.PreloadPropData((GTA.PedPropAnchorPoint)pending.Slot, pairs[pending.Slot, 0], pairs[pending.Slot, 1]);
                else
                    ped.Style.PreloadVariationData((GTA.PedComponentType)pending.Slot, pairs[pending.Slot, 0], pairs[pending.Slot, 1]);
                pending.Preloaded = true;
                SetPhase(pending, ApplyPhase.Load);
                return false;
            }
            if (pending.Phase == ApplyPhase.Load)
            {
                bool loaded = pending.Props ? ped.Style.HasLoadedPreloadPropData() : ped.Style.HasLoadedPreloadVariationData();
                if (!loaded)
                {
                    if (pending.PhaseTimer.ElapsedMilliseconds >= PreloadTimeoutMs)
                        throw ReadPieceRejection(ped, pending.Visual, pending.Props, pending.Slot, "preload não carregou em cinco segundos", true);
                    return false;
                }
                int[,] pairs = pending.Props ? pending.Visual.Props : pending.Visual.Components;
                bool applied;
                if (!pending.Props && !pending.Visual.IsFreemode)
                {
                    ValidateNpcComponentBounds(ped, pending.Visual, pending.Slot);
                    // SetVariation consulta IsVariationValid novamente, mesmo após preload.
                    // NPC usa limites reais e setter nativo; a aceitação depende dos getters posteriores.
                    GTA.Native.Function.Call(GTA.Native.Hash.SET_PED_COMPONENT_VARIATION, ped.Handle, pending.Slot,
                        pairs[pending.Slot, 0], pairs[pending.Slot, 1], 0);
                    applied = true;
                }
                else applied = pending.Props
                    ? ped.Style[(GTA.PedPropAnchorPoint)pending.Slot].SetVariation(pairs[pending.Slot, 0] + 1, pairs[pending.Slot, 1])
                    : ped.Style[(GTA.PedComponentType)pending.Slot].SetVariation(pairs[pending.Slot, 0], pairs[pending.Slot, 1]);
                ReleasePreload(pending);
                if (!applied) throw ReadPieceRejection(ped, pending.Visual, pending.Props, pending.Slot, "o jogo recusou o setter", true);
                SetPhase(pending, ApplyPhase.Check);
                return false;
            }
            if (pending.Phase == ApplyPhase.Check)
            {
                VariationRejectedException mismatch = ReadPieceRejection(ped, pending.Visual, pending.Props, pending.Slot,
                    "preload concluído; setter aceito; esperou dois segundos", false);
                if (mismatch != null)
                {
                    pending.StableFrames = 0;
                    if (pending.PhaseTimer.ElapsedMilliseconds >= SettleTimeoutMs)
                        throw mismatch;
                    return false;
                }
                if (++pending.StableFrames < 2) return false;
                pending.Slot++;
                SetPhase(pending, ApplyPhase.Request);
                return false;
            }
            if (pending.Phase == ApplyPhase.HeadDetails)
            {
                ApplyHeadDetails(ped, pending.Visual);
                SetPhase(pending, pending.Visual.IsFreemode ? ApplyPhase.HeadOverlayRequest : ApplyPhase.Verify);
                return false;
            }
            if (pending.Phase == ApplyPhase.HeadOverlayRequest)
            {
                if (pending.HeadSlot == 13)
                { SetPhase(pending, ApplyPhase.Verify); return false; }
                ApplyHeadOverlay(ped, pending.Visual, pending.HeadSlot);
                SetPhase(pending, ApplyPhase.HeadOverlayCheck);
                return false;
            }
            if (pending.Phase == ApplyPhase.HeadOverlayCheck)
            {
                int observed = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_HEAD_OVERLAY, ped.Handle, pending.HeadSlot);
                if (observed != pending.Visual.Head.Overlays[pending.HeadSlot, 0])
                {
                    pending.StableFrames = 0;
                    if (pending.PhaseTimer.ElapsedMilliseconds >= SettleTimeoutMs)
                    {
                        var mismatch = new HeadOverlayRejectedException(pending.Visual, pending.HeadSlot, observed);
                        if (!RetryHeadOverlay(pending, mismatch)) throw mismatch;
                    }
                    return false;
                }
                if (++pending.StableFrames < 2) return false;
                pending.HeadSlot++;
                SetPhase(pending, ApplyPhase.HeadOverlayRequest);
                return false;
            }
            System.Exception finalMismatch = AppearanceMismatch(ped, pending.Visual);
            if (finalMismatch != null)
            {
                pending.StableFrames = 0;
                if (pending.PhaseTimer.ElapsedMilliseconds >= SettleTimeoutMs)
                {
                    var overlayMismatch = finalMismatch as HeadOverlayRejectedException;
                    if (overlayMismatch == null || !RetryHeadOverlay(pending, overlayMismatch)) throw finalMismatch;
                }
                return false;
            }
            if (++pending.StableFrames < 2) return false;
            PendingHeads.Remove(ped.Handle);
            return true;
        }
        catch (System.Exception ex) { throw FailPending(pending, ex); }
    }

    public static void Forget(GTA.Ped ped)
    {
        if (System.Object.ReferenceEquals(ped, null)) return;
        Pending pending;
        if (PendingHeads.TryGetValue(ped.Handle, out pending)) ReleasePreload(pending);
        PendingHeads.Remove(ped.Handle);
    }

    // DNA, traços, olhos, máscara e overlays da pele permanecem iguais.
    // Maquiagem4, blush5 e batom8 são produção e podem variar entre as fases.
    public static bool SameFaceDetails(Visual first, Visual second)
    {
        if (first == null || second == null || first.Head == null || second.Head == null || first.FaceSha256 != second.FaceSha256)
            return false;
        for (int j = 0; j < 2; j++) if (first.Components[1, j] != second.Components[1, j]) return false;
        for (int i = 0; i < 13; i++)
        {
            if (i == 4 || i == 5 || i == 8) continue;
            if (first.Head.Opacity[i] != second.Head.Opacity[i]) return false;
            for (int j = 0; j < 3; j++) if (first.Head.Overlays[i, j] != second.Head.Overlays[i, j]) return false;
        }
        return true;
    }

    // A diferença é objetiva nos índices guardados, não uma promessa de estilo visual.
    public static bool SameClothes(Visual first, Visual second)
    {
        if (first == null || second == null || first.Model != second.Model) return false;
        for (int i = 3; i < 12; i++)
            for (int j = 0; j < 2; j++)
                if (first.Components[i, j] != second.Components[i, j]) return false;
        return true;
    }

    public static bool SameHair(Visual first, Visual second)
    {
        if (first == null || second == null || first.Model != second.Model || first.Head == null || second.Head == null)
            return false;
        return first.Components[2, 0] == second.Components[2, 0] &&
            first.Components[2, 1] == second.Components[2, 1] &&
            first.Head.Hair == second.Head.Hair && first.Head.HairStreaks == second.Head.HairStreaks;
    }

    private static void ValidateVariations(GTA.Ped ped, Visual visual)
    {
        for (int i = 0; i < 12; i++)
        {
            int drawable = visual.Components[i, 0], texture = visual.Components[i, 1];
            var component = ped.Style[(GTA.PedComponentType)i];
            if (SkipUnavailableComponent(ped, visual, i)) continue;
            if (!visual.IsFreemode) ValidateNpcComponentBounds(ped, visual, i);
            else if (!component.IsVariationValid(drawable, texture))
                throw ReadPieceRejection(ped, visual, false, i, "roupa/cabelo inexistente nesse modelo ou versão do GTA; Count=" + component.Count +
                    "; texturas=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, ped.Handle, i, drawable), true);
        }
        for (int i = 0; i < 10; i++)
        {
            if (visual.Props[i, 0] < 0) continue;
            var prop = ped.Style[(GTA.PedPropAnchorPoint)i];
            if (!prop.IsVariationValid(visual.Props[i, 0] + 1, visual.Props[i, 1]))
                throw Error("visual.PedProperties.PedProps._" + i, "acessório inexistente nesse modelo ou versão do GTA");
        }
        if (!visual.IsFreemode) return;
        int hairCount = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUM_PED_HAIR_TINTS);
        if (visual.Head.Hair >= hairCount || visual.Head.HairStreaks >= hairCount)
            throw Error("visual.PedProperties.HeadFeatures.HairColour", "a cor de cabelo não existe neste GTA");
        for (int i = 0; i < 13; i++)
        {
            int index = visual.Head.Overlays[i, 0];
            if (index != 255 && index >= GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_HEAD_OVERLAY_NUM, i))
                throw Error("visual.PedProperties.HeadFeatures.Overlays._" + i, "overlay inexistente neste GTA");
        }
    }

    private static void ApplyHeadDetails(GTA.Ped ped, Visual visual)
    {
        if (visual.IsFreemode)
        {
            var head = visual.Head;
            for (int i = 0; i < 20; i++)
                GTA.Native.Function.Call(GTA.Native.Hash.SET_PED_MICRO_MORPH, ped.Handle, i, head.Features[i]);
            GTA.Native.Function.Call(GTA.Native.Hash.SET_PED_HAIR_TINT, ped.Handle, head.Hair, head.HairStreaks);
            GTA.Native.Function.Call(GTA.Native.Hash.SET_HEAD_BLEND_EYE_COLOR, ped.Handle, head.Eyes);
        }
    }

    private static void ApplyHeadOverlay(GTA.Ped ped, Visual visual, int slot)
    {
        var head = visual.Head;
        GTA.Native.Function.Call(GTA.Native.Hash.SET_PED_HEAD_OVERLAY, ped.Handle, slot, head.Overlays[slot, 0], head.Opacity[slot]);
        // Tint só se aplica aos overlays coloridos ativos; não chamar para pele ou None.
        int colourType = slot == 1 || slot == 2 || slot == 4 || slot == 10 ? 1 : (slot == 5 || slot == 8 ? 2 : 0);
        if (colourType == 0 || head.Overlays[slot, 0] == 255) return;
        bool hasColour = head.Overlays[slot, 1] >= 0;
        int appliedColourType = hasColour ? colourType : 0;
        int colour = hasColour ? head.Overlays[slot, 1] : 0;
        int secondaryColour = hasColour ? head.Overlays[slot, 2] : 0;
        if (slot == 2)
        {
            // Sobrancelha usa a paleta1 mesmo se o XML não declarar cor.
            appliedColourType = 1;
            colour = System.Math.Max(0, head.Overlays[slot, 1]);
            secondaryColour = System.Math.Max(0, head.Overlays[slot, 2]);
        }
        GTA.Native.Function.Call(GTA.Native.Hash.SET_PED_HEAD_OVERLAY_TINT, ped.Handle, slot,
            appliedColourType, colour, secondaryColour);
    }

    private static bool RetryHeadOverlay(Pending pending, HeadOverlayRejectedException mismatch)
    {
        if (pending.HeadRetries[mismatch.Slot] != 0) return false;
        pending.HeadRetries[mismatch.Slot]++;
        pending.HeadSlot = mismatch.Slot;
        // Reaplicar os mesmos dados; nunca trocar o índice de um XML registrado.
        ApplyHeadOverlay(pending.Ped, pending.Visual, pending.HeadSlot);
        SetPhase(pending, ApplyPhase.HeadOverlayCheck);
        return true;
    }

    private static void ValidateNpcComponentBounds(GTA.Ped ped, Visual visual, int slot)
    {
        int drawable = visual.Components[slot, 0], texture = visual.Components[slot, 1];
        int count = ped.Style[(GTA.PedComponentType)slot].Count;
        int textures = drawable >= 0 && drawable < count ?
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, ped.Handle, slot, drawable) : 0;
        if (drawable < 0 || drawable >= count || texture < 0 || texture >= textures)
            throw ReadPieceRejection(ped, visual, false, slot,
                "par fora das contagens reais do modelo NPC; Count=" + count + "; texturas=" + textures, true);
    }

    private static bool SkipUnavailableComponent(GTA.Ped ped, Visual visual, int slot)
    {
        if (ped.Style[(GTA.PedComponentType)slot].Count != 0) return false;
        // Ausência registrada como0,0 não exige preload, setter ou getters de uma peça inexistente.
        if (visual.Components[slot, 0] == 0 && visual.Components[slot, 1] == 0) return true;
        // NPCs podem conservar um componente fixo nos getters sem oferecer variações.
        // Só aceitar o par exato já presente; não aplicar, normalizar ou substituir o XML.
        if (visual.IsFreemode)
            throw Error("visual.PedProperties.PedComps._" + slot, "o freemode não oferece variações para o par registrado; Count=0");
        int drawable = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_DRAWABLE_VARIATION, ped.Handle, slot);
        int texture = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_TEXTURE_VARIATION, ped.Handle, slot);
        if (drawable != visual.Components[slot, 0] || texture != visual.Components[slot, 1])
            throw new VariationRejectedException(visual, false, slot, drawable, texture,
                "componente NPC fixo sem variações; Count=0; o par presente difere do XML preservado");
        return true;
    }

    private static VariationRejectedException ReadPieceRejection(GTA.Ped ped, Visual visual, bool props, int slot, string reason, bool evenIfExact)
    {
        if (!props && !evenIfExact && SkipUnavailableComponent(ped, visual, slot)) return null;
        int[,] pairs = props ? visual.Props : visual.Components;
        int drawable = GTA.Native.Function.Call<int>(props ? GTA.Native.Hash.GET_PED_PROP_INDEX : GTA.Native.Hash.GET_PED_DRAWABLE_VARIATION, ped.Handle, slot);
        int texture = GTA.Native.Function.Call<int>(props ? GTA.Native.Hash.GET_PED_PROP_TEXTURE_INDEX : GTA.Native.Hash.GET_PED_TEXTURE_VARIATION, ped.Handle, slot);
        if (!evenIfExact && drawable == pairs[slot, 0] && ((props && drawable < 0) || texture == pairs[slot, 1])) return null;
        return new VariationRejectedException(visual, props, slot, drawable, texture, reason);
    }

    private static System.Exception AppearanceMismatch(GTA.Ped ped, Visual visual)
    {
        for (int i = 0; i < 12; i++)
        {
            VariationRejectedException mismatch = ReadPieceRejection(ped, visual, false, i,
                "aparência não estabilizou integralmente em dois segundos", false);
            if (mismatch != null) return mismatch;
        }
        for (int i = 0; i < 10; i++)
        {
            VariationRejectedException mismatch = ReadPieceRejection(ped, visual, true, i,
                "aparência não estabilizou integralmente em dois segundos", false);
            if (mismatch != null) return mismatch;
        }
        if (visual.IsFreemode)
        {
            HeadState state;
            if (!TryReadHeadState(ped, out state))
                return Error("visual.PedProperties.HeadFeatures", visual.Model + ": o getter de DNA ainda não está pronto");
            for (int i = 0; i < state.Parents.Length; i++)
                if (state.Parents[i] != visual.Head.Parents[i])
                    return Error("visual.PedProperties.HeadFeatures.ShapeAndSkinTone.pai" + i,
                        visual.Model + ": esperado=" + visual.Head.Parents[i] + "; observado=" + state.Parents[i]);
            for (int i = 0; i < state.Mix.Length; i++)
                if (System.Math.Abs(state.Mix[i] - visual.Head.Mix[i]) > 0.00001f)
                    return Error("visual.PedProperties.HeadFeatures.ShapeAndSkinTone.mistura" + i,
                        visual.Model + ": esperado=" + visual.Head.Mix[i].ToString(Inv) + "; observado=" + state.Mix[i].ToString(Inv));
            if (state.IsParent != visual.Head.IsParent)
                return Error("visual.PedProperties.HeadFeatures.ShapeAndSkinTone.IsP", visual.Model + ": flag do getter difere do XML");
            if (state.Eyes != visual.Head.Eyes)
                return Error("visual.PedProperties.HeadFeatures.EyeColour", visual.Model + ": esperado=" + visual.Head.Eyes + "; observado=" + state.Eyes);
            for (int i = 0; i < state.OverlayIndexes.Length; i++)
                if (state.OverlayIndexes[i] != visual.Head.Overlays[i, 0])
                    return new HeadOverlayRejectedException(visual, i, state.OverlayIndexes[i]);
        }
        return null;
    }

    private static void SetPhase(Pending pending, ApplyPhase phase)
    { pending.Phase = phase; pending.PhaseTimer.Restart(); pending.StableFrames = 0; }

    private static bool HasIdentity(Pending pending)
    {
        return !System.Object.ReferenceEquals(pending.Ped, null) && pending.Ped.Exists() &&
            pending.Ped.MemoryAddress == pending.Address && unchecked((uint)pending.Ped.Model.Hash) == pending.Visual.ModelHash;
    }

    private static void ReleasePreload(Pending pending)
    {
        if (!pending.Preloaded) return;
        // Um handle reutilizado não autoriza liberar/alterar o preload de outra entidade.
        if (!HasIdentity(pending)) { pending.Preloaded = false; return; }
        if (pending.Props) pending.Ped.Style.ReleasePreloadPropData();
        else pending.Ped.Style.ReleasePreloadVariationData();
        pending.Preloaded = false;
    }

    private static System.Exception FailPending(Pending pending, System.Exception failure)
    {
        pending.Failure = failure; pending.Phase = ApplyPhase.Failed;
        try { ReleasePreload(pending); }
        catch (System.Exception ex) { pending.Failure = new System.AggregateException("Falha ao aplicar aparência e liberar preload", failure, ex); }
        // Conservar a falha até Forget: IsReady nunca vira true após uma aplicação abortada.
        return pending.Failure;
    }

    private static HeadData ReadHead(System.Xml.XmlElement node)
    {
        const string path = "visual.PedProperties.HeadFeatures";
        if (node.Attributes.Count != 1 || node.GetAttribute("WasInArray") != "true")
            throw Error(path, "exige WasInArray=true; ajuste/salve Head Features no Menyoo para conservar todo o rosto");
        ExactChildren(node, path, "ShapeAndSkinTone,HairColour,HairColourStreaks,EyeColour,FacialFeatures,Overlays");
        var result = new HeadData();
        var blend = One(node, "ShapeAndSkinTone", path); NoAttributes(blend, path + ".ShapeAndSkinTone");
        string[] parents = { "ShapeFatherId", "ShapeMotherId", "ShapeOverrideId", "ToneFatherId", "ToneMotherId", "ToneOverrideId" };
        string[] mixes = { "ShapeVal", "ToneVal", "OverrideVal" };
        ExactChildren(blend, path + ".ShapeAndSkinTone", System.String.Join(",", parents) + "," + System.String.Join(",", mixes) + ",IsP");
        for (int i = 0; i < parents.Length; i++)
            result.Parents[i] = Integer(One(blend, parents[i], path), path + ".ShapeAndSkinTone." + parents[i], 0, 45);
        for (int i = 0; i < mixes.Length; i++)
            result.Mix[i] = Real(One(blend, mixes[i], path), path + ".ShapeAndSkinTone." + mixes[i], 0, 1);
        string isParent = Scalar(One(blend, "IsP", path), path + ".ShapeAndSkinTone.IsP");
        result.IsParent = isParent == "true" || (isParent != "false" && IntText(isParent, path + ".ShapeAndSkinTone.IsP", 0, 1) == 1);
        result.Hair = Integer(One(node, "HairColour", path), path + ".HairColour", 0, 255);
        result.HairStreaks = Integer(One(node, "HairColourStreaks", path), path + ".HairColourStreaks", 0, 255);
        result.Eyes = Integer(One(node, "EyeColour", path), path + ".EyeColour", 0, 32);
        var features = One(node, "FacialFeatures", path); NoAttributes(features, path + ".FacialFeatures");
        Indexed(features, path + ".FacialFeatures", 20);
        for (int i = 0; i < 20; i++) result.Features[i] = Real(One(features, "_" + i, path), path + ".FacialFeatures._" + i, -1, 1);
        var overlays = One(node, "Overlays", path); NoAttributes(overlays, path + ".Overlays");
        Indexed(overlays, path + ".Overlays", 13);
        for (int i = 0; i < 13; i++)
        {
            var item = One(overlays, "_" + i, path); string field = path + ".Overlays._" + i;
            if (item.Attributes.Count != 4 || !item.HasAttribute("index") || !item.HasAttribute("colour") ||
                !item.HasAttribute("colourSecondary") || !item.HasAttribute("opacity") || item.InnerText.Trim().Length != 0 || item.HasChildNodes)
                throw Error(field, "exige somente index/colour/colourSecondary/opacity e nenhum conteúdo");
            result.Overlays[i, 0] = IntText(item.GetAttribute("index"), field + ".index", 0, 255);
            result.Overlays[i, 1] = IntText(item.GetAttribute("colour"), field + ".colour", -1, 255);
            result.Overlays[i, 2] = IntText(item.GetAttribute("colourSecondary"), field + ".colourSecondary", -1, 255);
            result.Opacity[i] = FloatText(item.GetAttribute("opacity"), field + ".opacity", 0, 1);
        }
        return result;
    }

    private static int[,] Pairs(System.Xml.XmlElement node, string path, int count, bool props)
    {
        NoAttributes(node, path); Indexed(node, path, count);
        var result = new int[count, 2];
        for (int i = 0; i < count; i++)
        {
            string field = path + "._" + i;
            string[] pair = Scalar(One(node, "_" + i, path), field).Split(',');
            if (pair.Length != 2) throw Error(field, "use o par drawable,textura salvo no Menyoo");
            result[i, 0] = IntText(pair[0].Trim(), field + ".drawable", props ? -1 : 0, 10000);
            result[i, 1] = IntText(pair[1].Trim(), field + ".textura", props ? -1 : 0, 10000);
            if (props && ((result[i, 0] == -1 && result[i, 1] != -1 && result[i, 1] != 0) || (result[i, 0] >= 0 && result[i, 1] < 0)))
                throw Error(field, "acessório ausente exige -1,-1 ou -1,0; acessórios presentes exigem textura não negativa");
        }
        return result;
    }

    private static System.Xml.XmlElement Optional(System.Xml.XmlElement parent, string name)
    {
        foreach (System.Xml.XmlNode child in parent.ChildNodes)
        {
            var element = child as System.Xml.XmlElement;
            if (element != null && element.Name == name && element.NamespaceURI.Length == 0) return element;
        }
        return null;
    }

    private static System.Xml.XmlElement One(System.Xml.XmlElement parent, string name, string path)
    {
        var item = Optional(parent, name);
        if (item == null) throw Error(path + "." + name, "campo obrigatório ausente");
        return item;
    }

    private static void NoAttributes(System.Xml.XmlElement node, string path)
    {
        if (node.Attributes.Count != 0) throw Error(path, "atributos desconhecidos");
    }

    private static void UniqueChildren(System.Xml.XmlElement node, string path)
    {
        var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (System.Xml.XmlNode child in node.ChildNodes)
        {
            var element = child as System.Xml.XmlElement;
            if (element != null)
            {
                if (element.NamespaceURI.Length != 0) throw Error(path, "namespace não permitido");
                if (!seen.Add(element.Name)) throw Error(path + "." + element.Name, "campo duplicado");
            }
            else if (child.NodeType != System.Xml.XmlNodeType.Whitespace && child.NodeType != System.Xml.XmlNodeType.SignificantWhitespace && child.Value != null && child.Value.Trim().Length != 0)
                throw Error(path, "texto solto não permitido");
        }
    }

    private static void ExactChildren(System.Xml.XmlElement node, string path, string names)
    {
        UniqueChildren(node, path);
        var allowed = new System.Collections.Generic.HashSet<string>(names.Split(','), System.StringComparer.Ordinal);
        foreach (System.Xml.XmlNode child in node.ChildNodes)
        {
            var element = child as System.Xml.XmlElement;
            if (element != null && !allowed.Contains(element.Name)) throw Error(path + "." + element.Name, "campo desconhecido");
        }
        foreach (string name in names.Split(',')) One(node, name, path);
    }

    private static void Indexed(System.Xml.XmlElement node, string path, int count)
    {
        var names = new string[count];
        for (int i = 0; i < count; i++) names[i] = "_" + i;
        ExactChildren(node, path, System.String.Join(",", names));
    }

    private static string Scalar(System.Xml.XmlElement node, string path)
    {
        NoAttributes(node, path);
        foreach (System.Xml.XmlNode child in node.ChildNodes)
            if (child is System.Xml.XmlElement) throw Error(path, "esperado apenas um valor, sem subcampos");
        return node.InnerText.Trim();
    }

    private static int Integer(System.Xml.XmlElement node, string path, int min, int max)
    { return IntText(Scalar(node, path), path, min, max); }
    private static int IntText(string text, string path, int min, int max)
    {
        int value;
        if (!System.Text.RegularExpressions.Regex.IsMatch(text, "\\A-?[0-9]+\\z") ||
            !System.Int32.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign, Inv, out value) || value < min || value > max)
            throw Error(path, "esperado inteiro entre " + min + " e " + max);
        return value;
    }
    private static float Real(System.Xml.XmlElement node, string path, double min, double max)
    { return FloatText(Scalar(node, path), path, min, max); }
    private static float FloatText(string text, string path, double min, double max)
    {
        double value;
        if (!System.Text.RegularExpressions.Regex.IsMatch(text, "\\A-?(?:[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)(?:[eE][+-]?[0-9]+)?\\z") ||
            !System.Double.TryParse(text, System.Globalization.NumberStyles.Float, Inv, out value) ||
            System.Double.IsNaN(value) || System.Double.IsInfinity(value) || value < min || value > max)
            throw Error(path, "esperado número finito entre " + min.ToString(Inv) + " e " + max.ToString(Inv));
        return (float)value;
    }

    private static string Fingerprint(Visual visual)
    {
        var text = new System.Text.StringBuilder("VisualDiretor.Face.v1\n");
        text.Append(visual.Model).Append('\n');
        text.Append(visual.Components[0, 0].ToString(Inv)).Append(',').Append(visual.Components[0, 1].ToString(Inv)).Append('\n');
        if (visual.Head != null)
        {
            var head = visual.Head;
            for (int i = 0; i < 6; i++) { if (i > 0) text.Append(','); text.Append(head.Parents[i].ToString(Inv)); }
            text.Append('\n');
            for (int i = 0; i < 3; i++) { if (i > 0) text.Append(','); text.Append(FloatHex(head.Mix[i])); }
            text.Append('\n').Append(head.IsParent ? "1\n" : "0\n");
            for (int i = 0; i < 20; i++) { if (i > 0) text.Append(','); text.Append(FloatHex(head.Features[i])); }
            text.Append('\n').Append(head.Eyes.ToString(Inv)).Append('\n');
        }
        return Hash(new System.Text.UTF8Encoding(false).GetBytes(text.ToString()));
    }
    private static string FloatHex(float value)
    {
        byte[] bytes = System.BitConverter.GetBytes(value == 0f ? 0f : value);
        if (!System.BitConverter.IsLittleEndian) System.Array.Reverse(bytes);
        return System.BitConverter.ToString(bytes).Replace("-", "");
    }
    private static string Hash(byte[] bytes)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }
    private static uint ModelHash(string name)
    {
        uint hash = 0;
        unchecked
        {
            foreach (char letter in name) { hash += letter; hash += hash << 10; hash ^= hash >> 6; }
            hash += hash << 3; hash ^= hash >> 11; hash += hash << 15;
        }
        return hash;
    }
    private static void RequirePed(GTA.Ped ped)
    {
        if (System.Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsDead)
            throw Error("visual.ator", "personagem ausente ou morto; monte a cena novamente");
    }
    private static void RemoveDeletedPending()
    {
        var removed = new System.Collections.Generic.List<int>();
        foreach (var item in PendingHeads)
            if (!HasIdentity(item.Value)) removed.Add(item.Key);
        foreach (int handle in removed) PendingHeads.Remove(handle);
    }
    private static System.InvalidOperationException Error(string path, string message)
    { return new System.InvalidOperationException(path + ": " + message + "."); }
}
