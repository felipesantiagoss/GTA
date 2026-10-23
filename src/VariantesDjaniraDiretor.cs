// Comparacao autorizada de tres macacoes: nenhum XML canonico e substituido.
public static class VariantesDjaniraDiretor
{
    private sealed class Plan
    {
        internal GTA.Ped Ped;
        internal System.IntPtr Address;
        internal VisualDiretor.Visual Poor, Original, Visual;
        internal System.Xml.XmlDocument OriginalXml;
        internal System.Diagnostics.Stopwatch Timer;
        internal System.Action<string, string> Log;
        internal string DataRoot;
        internal int Option, Candidate, Attempts;
        internal int[] Textures;
        internal readonly bool[] Tried = new bool[6];
        internal readonly bool[] DisabledMakeup = new bool[13];
        internal byte[] Xml;
        internal bool Ready, Applying;
    }

    private static readonly System.Collections.Generic.Dictionary<int, Plan> Plans =
        new System.Collections.Generic.Dictionary<int, Plan>();
    private static readonly System.Collections.Generic.Dictionary<string, int> VerifiedPairs =
        new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
    private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
    private const int BudgetMs = 120000;

    public static void Begin(GTA.Ped ped, VisualDiretor.Visual basisPoor, VisualDiretor.Visual currentRich,
        int option, string dataRoot, System.Action<string, string> log)
    {
        RequirePed(ped);
        if (option < 1 || option > 3 || basisPoor == null || currentRich == null ||
            !currentRich.IsFreemode || currentRich.Model != "mp_f_freemode_01" ||
            unchecked((uint)ped.Model.Hash) != currentRich.ModelHash ||
            basisPoor.FaceSha256 != currentRich.FaceSha256 ||
            !VisualDiretor.SameFaceDetails(basisPoor, currentRich))
            throw Error("opcao/modelo/identidade de Djanira invalidos");
        if (Plans.ContainsKey(ped.Handle)) throw Error("limpe a comparacao anterior antes de montar outra opcao");
        string root = System.IO.Path.GetFullPath(dataRoot);
        NoLinks(root);
        string originalPath = System.IO.Path.Combine(root, "catalogos", "visuais", "DJANIRA_RICA.xml");
        NoLinks(originalPath);
        var checkedOriginal = VisualDiretor.Load(originalPath, currentRich.Model);
        if (checkedOriginal.Sha256 != currentRich.Sha256) throw Error("o XML rico mudou antes da comparacao; preservar arquivo");
        var document = new System.Xml.XmlDocument(); document.XmlResolver = null;
        var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = 100000 };
        byte[] originalBytes = System.IO.File.ReadAllBytes(originalPath);
        if (Hash(originalBytes) != currentRich.Sha256) throw Error("o XML rico mudou durante a leitura; preservar arquivo");
        using (var memory = new System.IO.MemoryStream(originalBytes, false))
        using (var reader = System.Xml.XmlReader.Create(memory, settings)) document.Load(reader);
        int[] all = { 0, 1, 3 };
        var plan = new Plan { Ped = ped, Address = ped.MemoryAddress, Poor = basisPoor, Original = currentRich,
            OriginalXml = document, DataRoot = root, Log = log, Option = option,
            Textures = new int[] { all[option - 1], all[option % 3], all[(option + 1) % 3] },
            Timer = System.Diagnostics.Stopwatch.StartNew() };
        Plans.Add(ped.Handle, plan);
        try
        {
            int first = 0, previousOption;
            if (VerifiedPairs.TryGetValue(VerifiedKey(plan, 0), out previousOption) && previousOption != option)
            {
                int alternative = NextCandidate(plan, false);
                if (alternative >= 0) first = alternative;
                WriteLog(plan, "DJANIRA_OPCAO_EVITAR_IGUAL", "Opcao=" + option + "; outro conjunto completo antes da previsualizacao; primeira tentativa=" + first);
            }
            StartCandidate(plan, first);
        }
        catch { Forget(ped); throw; }
    }

    public static bool IsReady(GTA.Ped ped)
    {
        Plan plan = RequirePlan(ped);
        if (plan.Ready) return true;
        Budget(plan);
        try
        {
            if (!plan.Applying)
            {
                VisualDiretor.Apply(ped, plan.Visual); plan.Applying = true;
                return false;
            }
            if (!VisualDiretor.IsReady(ped)) return false;
        }
        catch (VisualDiretor.VariationRejectedException rejection)
        {
            // Toda troca e um preset completo ou outro cabelo real, nunca um indice sorteado.
            if (rejection.Props || (rejection.Slot != 2 && rejection.Slot != 3 && rejection.Slot != 4 &&
                rejection.Slot != 6 && rejection.Slot != 7 && rejection.Slot != 8 && rejection.Slot != 11)) throw;
            if (rejection.ExpectedDrawable != plan.Visual.Components[rejection.Slot, 0] ||
                rejection.ExpectedTexture != plan.Visual.Components[rejection.Slot, 1]) throw;
            int next = NextCandidate(plan, rejection.Slot == 2);
            if (next < 0) throw Error("os conjuntos/cabelos reais foram recusados; nenhum XML de opcao foi salvo. " + rejection.Message);
            WriteLog(plan, "DJANIRA_OPCAO_ALTERNATIVA", "Opcao=" + plan.Option + "; anterior=" + plan.Candidate +
                "; seguinte=" + next + "; rejeicao=" + rejection.Message);
            VisualDiretor.Forget(ped);
            StartCandidate(plan, next);
            return false;
        }
        catch (VisualDiretor.HeadOverlayRejectedException rejection)
        {
            int slot = rejection.Slot;
            // Só maquiagem inédita e opcional. DNA, pele e XMLs canônicos nunca recebem fallback.
            if ((slot != 4 && slot != 5 && slot != 8) || plan.Ready || plan.DisabledMakeup[slot] ||
                rejection.ExpectedIndex == 255 || rejection.ExpectedOpacity <= 0f || rejection.ObservedIndex != 255 ||
                rejection.ExpectedIndex != plan.Visual.Head.Overlays[slot, 0]) throw;
            plan.DisabledMakeup[slot] = true;
            WriteLog(plan, "DJANIRA_MAQUIAGEM_RECUSADA", "Opcao=" + plan.Option + "; efeito=" + slot +
                "; " + rejection.Message + "; retirar apenas este efeito opcional do candidato ainda nao salvo");
            VisualDiretor.Forget(ped);
            BuildCandidate(plan);
            return false;
        }
        SaveReady(plan);
        int previousOption;
        string pairKey = VerifiedKey(plan, plan.Candidate);
        if (VerifiedPairs.TryGetValue(pairKey, out previousOption) && previousOption != plan.Option)
            WriteLog(plan, "DJANIRA_OPCOES_IGUAIS", "Opcao=" + plan.Option + "; igual a opcao=" + previousOption +
                "; o jogo aceitou a mesma roupa/cabelo; nao apresentar como tres visuais distintos");
        else VerifiedPairs[pairKey] = plan.Option;
        plan.Ready = true;
        return true;
    }

    public static VisualDiretor.Visual Current(GTA.Ped ped) { return RequirePlan(ped).Visual; }

    public static string SavedPath(GTA.Ped ped)
    {
        Plan plan = RequirePlan(ped);
        if (!plan.Ready) throw Error("aguarde os getters e a gravacao antes de escolher a opcao");
        string path = System.IO.Path.Combine(plan.DataRoot, "catalogos", "visuais", "opcoes",
            "DJANIRA_RICA_opcao" + plan.Option + "_" + plan.Visual.Sha256 + ".xml");
        NoLinks(path);
        var checkedVisual = VisualDiretor.Load(path, plan.Visual.Model);
        if (checkedVisual.Sha256 != plan.Visual.Sha256) throw Error("o arquivo da opcao mudou apos o teste; preservar escolha");
        ValidateIdentity(plan, checkedVisual);
        return path;
    }

    public static void Forget(GTA.Ped ped)
    {
        if (System.Object.ReferenceEquals(ped, null)) return;
        Plan plan;
        if (Plans.TryGetValue(ped.Handle, out plan) && ped.Exists() && ped.MemoryAddress == plan.Address)
            VisualDiretor.Forget(ped);
        Plans.Remove(ped.Handle);
    }

    private static int NextCandidate(Plan plan, bool hairRejected)
    {
        int currentHair = plan.Candidate / 3, currentSet = plan.Candidate % 3;
        if (hairRejected)
        {
            int alternate = (1 - currentHair) * 3 + currentSet;
            return plan.Tried[alternate] ? -1 : alternate;
        }
        int duplicate = -1;
        for (int i = 0; i < 3; i++)
        {
            int next = currentHair * 3 + i;
            if (plan.Tried[next]) continue;
            int previousOption;
            if (!VerifiedPairs.TryGetValue(VerifiedKey(plan, next), out previousOption) || previousOption == plan.Option) return next;
            duplicate = next;
        }
        return duplicate;
    }

    private static void StartCandidate(Plan plan, int candidate)
    {
        RequirePlan(plan.Ped); Budget(plan);
        if (candidate < 0 || candidate >= plan.Tried.Length || plan.Tried[candidate] || plan.Attempts >= 6)
            throw Error("limite de conjuntos completos atingido");
        plan.Candidate = candidate; plan.Tried[candidate] = true; plan.Attempts++; plan.Applying = false;
        BuildCandidate(plan);
    }

    private static void BuildCandidate(Plan plan)
    {
        RequirePlan(plan.Ped); Budget(plan); plan.Applying = false;
        int candidate = plan.Candidate;
        int texture = plan.Textures[candidate % 3];
        int[,] components = (int[,])plan.Original.Components.Clone(), props = (int[,])plan.Original.Props.Clone();
        Pair(components, 3, 7, 0); Pair(components, 4, 102, texture); Pair(components, 6, 77, texture);
        Pair(components, 7, 0, 0); Pair(components, 8, 14, 0); Pair(components, 11, 262, texture);
        // Opcao2 com Big Bangs; 1/3 com Loose Swept Back. Alternativa apenas entre estes dois.
        bool bangs = (candidate / 3 == 0) == (plan.Option == 2);
        Pair(components, 2, bangs ? 21 : 32, bangs ? 2 : 0);
        Pair(props, 2, 6, 0);
        var head = CopyHead(plan.Original.Head); head.Hair = 24; head.HairStreaks = 24;
        Makeup(head, 4, 0, 0.12f); Makeup(head, 5, 0, 0.08f); Makeup(head, 8, 0, 0.18f);
        foreach (int slot in new int[] { 4, 5, 8 })
            if (plan.DisabledMakeup[slot]) { head.Overlays[slot, 0] = 255; head.Opacity[slot] = 0f; }
        var visual = new VisualDiretor.Visual(plan.Original.Model, plan.Original.ModelHash, "", components, props, head);
        ValidateIdentity(plan, visual);
        plan.Xml = Serialize(plan, visual, texture);
        plan.Visual = new VisualDiretor.Visual(visual.Model, visual.ModelHash, Hash(plan.Xml), components, props, head);
        WriteLog(plan, "DJANIRA_OPCAO_PREPARO", "Opcao=" + plan.Option + "; preset=DLC_MP_X17_F_OUTFIT_CATSUIT_" + texture +
            "; cabelo=" + components[2, 0] + "," + components[2, 1] + "; tentativa=" + plan.Attempts + "; visual pendente");
    }

    private static void Makeup(VisualDiretor.HeadData head, int slot, int colour, float opacity)
    {
        head.Overlays[slot, 0] = 0; head.Overlays[slot, 1] = colour;
        head.Overlays[slot, 2] = colour; head.Opacity[slot] = opacity;
    }

    private static VisualDiretor.HeadData CopyHead(VisualDiretor.HeadData source)
    {
        var result = new VisualDiretor.HeadData();
        System.Array.Copy(source.Parents, result.Parents, 6); System.Array.Copy(source.Mix, result.Mix, 3);
        System.Array.Copy(source.Features, result.Features, 20); System.Array.Copy(source.Opacity, result.Opacity, 13);
        System.Array.Copy(source.Overlays, result.Overlays, source.Overlays.Length);
        result.Hair = source.Hair; result.HairStreaks = source.HairStreaks; result.Eyes = source.Eyes; result.IsParent = source.IsParent;
        return result;
    }

    private static void ValidateIdentity(Plan plan, VisualDiretor.Visual visual)
    {
        if (visual.FaceSha256 != plan.Original.FaceSha256 || visual.FaceSha256 != plan.Poor.FaceSha256 ||
            !VisualDiretor.SameFaceDetails(plan.Original, visual) || !VisualDiretor.SameFaceDetails(plan.Poor, visual))
            throw Error("a opcao alteraria DNA, tracos ou olhos");
        for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++)
            if (visual.Components[i, j] != plan.Original.Components[i, j]) throw Error("a opcao alteraria face ou mascara");
        for (int i = 0; i < 13; i++)
        {
            if (i == 4 || i == 5 || i == 8) continue;
            if (visual.Head.Opacity[i] != plan.Original.Head.Opacity[i]) throw Error("a opcao alteraria overlay fora da maquiagem");
            for (int j = 0; j < 3; j++) if (visual.Head.Overlays[i, j] != plan.Original.Head.Overlays[i, j])
                throw Error("a opcao alteraria overlay fora da maquiagem");
        }
    }

    private static byte[] Serialize(Plan plan, VisualDiretor.Visual visual, int texture)
    {
        var doc = (System.Xml.XmlDocument)plan.OriginalXml.CloneNode(true);
        for (int i = 0; i < 12; i++) SetText(doc, "/OutfitPedData/PedProperties/PedComps/_" + i, PairText(visual.Components, i));
        for (int i = 0; i < 10; i++) SetText(doc, "/OutfitPedData/PedProperties/PedProps/_" + i, PairText(visual.Props, i));
        SetText(doc, "/OutfitPedData/PedProperties/HeadFeatures/HairColour", "24");
        SetText(doc, "/OutfitPedData/PedProperties/HeadFeatures/HairColourStreaks", "24");
        foreach (int i in new int[] { 4, 5, 8 })
        {
            var node = (System.Xml.XmlElement)doc.SelectSingleNode("/OutfitPedData/PedProperties/HeadFeatures/Overlays/_" + i);
            node.SetAttribute("index", visual.Head.Overlays[i, 0].ToString(Inv));
            node.SetAttribute("colour", visual.Head.Overlays[i, 1].ToString(Inv));
            node.SetAttribute("colourSecondary", visual.Head.Overlays[i, 2].ToString(Inv));
            node.SetAttribute("opacity", visual.Head.Opacity[i].ToString("R", Inv));
        }
        System.Xml.XmlNode previous;
        while ((previous = doc.DocumentElement.SelectSingleNode("DiretorOpcao")) != null)
            doc.DocumentElement.RemoveChild(previous);
        var meta = doc.CreateElement("DiretorOpcao"); doc.DocumentElement.AppendChild(meta);
        string[,] fields = { { "Numero", plan.Option.ToString(Inv) }, { "Preset", "DLC_MP_X17_F_OUTFIT_CATSUIT_" + texture },
            { "SHA256CanonicoOrigem", plan.Original.Sha256 }, { "Estado", "getters conferidos antes de salvar; teste visual pendente" },
            { "Fonte", "https://github.com/MnkyArts/charcreator/blob/main/data/outfits_female.json" } };
        for (int i = 0; i < fields.GetLength(0); i++) { var node = doc.CreateElement(fields[i, 0]); node.InnerText = fields[i, 1]; meta.AppendChild(node); }
        using (var stream = new System.IO.MemoryStream())
        {
            var settings = new System.Xml.XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = true, CloseOutput = false };
            using (var writer = System.Xml.XmlWriter.Create(stream, settings)) doc.Save(writer);
            return stream.ToArray();
        }
    }

    private static void SaveReady(Plan plan)
    {
        RequirePlan(plan.Ped); Budget(plan); ValidateIdentity(plan, plan.Visual);
        string canonical = System.IO.Path.Combine(plan.DataRoot, "catalogos", "visuais", "DJANIRA_RICA.xml");
        NoLinks(canonical);
        if (Hash(System.IO.File.ReadAllBytes(canonical)) != plan.Original.Sha256)
            throw Error("o XML canonico mudou durante o teste; nenhuma opcao sera salva");
        string directory = System.IO.Path.Combine(plan.DataRoot, "catalogos", "visuais", "opcoes");
        NoLinks(directory); System.IO.Directory.CreateDirectory(directory); NoLinks(directory);
        string name = "DJANIRA_RICA_opcao" + plan.Option + "_" + plan.Visual.Sha256;
        string path = System.IO.Path.Combine(directory, name + ".xml"); NoLinks(path);
        if (System.IO.File.Exists(path))
        {
            if (Hash(System.IO.File.ReadAllBytes(path)) != plan.Visual.Sha256) throw Error("arquivo de opcao existente divergente; preservado");
        }
        else WriteNew(path, plan.Xml);
        var saved = VisualDiretor.Load(path, plan.Visual.Model); ValidateIdentity(plan, saved);
        if (saved.Sha256 != plan.Visual.Sha256) throw Error("o hash da opcao salva divergiu");
        plan.Visual = saved;
        string manifest = "{\"opcao\":" + plan.Option + ",\"arquivo\":\"" + name + ".xml\",\"sha256\":\"" + saved.Sha256 +
            "\",\"faceSHA256\":\"" + saved.FaceSha256 + "\",\"preset\":\"DLC_MP_X17_F_OUTFIT_CATSUIT_" + plan.Textures[plan.Candidate % 3] +
            "\",\"componentes\":" + JsonPairs(saved.Components, 12) + ",\"props\":" + JsonPairs(saved.Props, 10) +
            ",\"corCabelo\":24,\"segundaCorCabelo\":24,\"estado\":\"getters_conferidos;teste_visual_pendente\",\"canonicoAlterado\":false,\"dataUtc\":\"" +
            System.DateTime.UtcNow.ToString("o", Inv) + "\"}\n";
        WriteNew(System.IO.Path.Combine(directory, name + "_" + System.Guid.NewGuid().ToString("N") + ".json"), new System.Text.UTF8Encoding(false).GetBytes(manifest));
        WriteLog(plan, "DJANIRA_OPCAO_SALVA", "Opcao=" + plan.Option + "; XML=" + path + "; SHA256=" + saved.Sha256 +
            "; Canonico preservado; teste visual pendente");
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        NoLinks(path);
        string temp = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), ".opcao-" + System.Guid.NewGuid().ToString("N") + ".tmp");
        bool ownTemp = false;
        try
        {
            using (var stream = new System.IO.FileStream(temp, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None))
            { ownTemp = true; stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            NoLinks(path); NoLinks(temp);
            System.IO.File.Move(temp, path); ownTemp = false;
        }
        finally { if (ownTemp && System.IO.File.Exists(temp)) { NoLinks(temp); System.IO.File.Delete(temp); } }
    }
    private static string VerifiedKey(Plan plan, int candidate)
    {
        bool bangs = (candidate / 3 == 0) == (plan.Option == 2);
        return plan.Original.Sha256 + ":" + plan.Textures[candidate % 3] + ":" + (bangs ? "21,2" : "32,0");
    }
    private static string JsonPairs(int[,] pairs, int count)
    {
        var text = new System.Text.StringBuilder("[");
        for (int i = 0; i < count; i++) { if (i > 0) text.Append(','); text.Append('[').Append(pairs[i, 0].ToString(Inv)).Append(',').Append(pairs[i, 1].ToString(Inv)).Append(']'); }
        return text.Append(']').ToString();
    }
    private static void NoLinks(string path)
    {
        if (System.IO.File.Exists(path) && (System.IO.File.GetAttributes(path) & System.IO.FileAttributes.ReparsePoint) != 0)
            throw Error("links nao permitidos no arquivo de opcao");
        for (var item = new System.IO.DirectoryInfo(System.IO.Directory.Exists(path) ? path : System.IO.Path.GetDirectoryName(path)); item != null; item = item.Parent)
            if (item.Exists && (item.Attributes & System.IO.FileAttributes.ReparsePoint) != 0) throw Error("links/junctions nao permitidos no destino");
    }
    private static Plan RequirePlan(GTA.Ped ped)
    {
        RequirePed(ped); Plan plan;
        if (!Plans.TryGetValue(ped.Handle, out plan) || ped.MemoryAddress != plan.Address || unchecked((uint)ped.Model.Hash) != plan.Original.ModelHash)
            throw Error("a entidade da opcao mudou; nenhuma entidade alheia sera alterada");
        return plan;
    }
    private static void RequirePed(GTA.Ped ped)
    { if (System.Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsDead) throw Error("ator proprio indisponivel"); }
    private static void Budget(Plan plan)
    { if (plan.Timer.ElapsedMilliseconds >= BudgetMs) throw Error("comparacao excedeu120segundos; nenhum candidato incompleto sera salvo"); }
    private static void Pair(int[,] pairs, int slot, int drawable, int texture) { pairs[slot, 0] = drawable; pairs[slot, 1] = texture; }
    private static string PairText(int[,] pairs, int slot) { return pairs[slot, 0].ToString(Inv) + "," + pairs[slot, 1].ToString(Inv); }
    private static void SetText(System.Xml.XmlDocument doc, string path, string value) { doc.SelectSingleNode(path).InnerText = value; }
    private static string Hash(byte[] bytes)
    { using (var hash = System.Security.Cryptography.SHA256.Create()) return System.BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
    private static void WriteLog(Plan plan, string eventName, string message) { if (plan.Log != null) plan.Log(eventName, message); }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("opcoes.DJANIRA_RICA: " + message); }
}
