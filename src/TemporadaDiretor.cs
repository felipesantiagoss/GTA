// Seleção do episódio não presume que suas cenas já foram encenadas.
public sealed class TemporadaDiretor
{
    public sealed class Cena
    {
        public string Id, Nome, Arquivo, Sha256;
        public int Episodio, Numero;
    }
    private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Cena>> episodes =
        new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Cena>>();
    private Cena shared;
    public Cena GetVinheta()
    {
        if (System.Object.ReferenceEquals(shared, null)) throw Error("vinheta ausente no índice");
        return new Cena { Id = shared.Id, Nome = shared.Nome, Arquivo = shared.Arquivo, Sha256 = shared.Sha256, Episodio = 0, Numero = 0 };
    }
    public System.Collections.Generic.List<Cena> GetEpisode(int number)
    {
        System.Collections.Generic.List<Cena> rows;
        if (!episodes.TryGetValue(number, out rows)) throw Error("episódio ausente no índice");
        return new System.Collections.Generic.List<Cena>(rows);
    }
    public static TemporadaDiretor Load(string projectRoot)
    {
        string directory = System.IO.Path.Combine(projectRoot, "producao", "cenas_serie");
        string path = System.IO.Path.Combine(directory, "selecao.xml");
        NoLinks(path);
        var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = 100000, IgnoreWhitespace = true, IgnoreComments = true };
        var document = new System.Xml.XmlDocument { XmlResolver = null };
        using (var reader = System.Xml.XmlReader.Create(path, settings)) document.Load(reader);
        var root = document.DocumentElement;
        if (System.Object.ReferenceEquals(root, null) || root.Name != "Temporada" || root.GetAttribute("versao") != "1")
            throw Error("índice da temporada inválido");
        Attributes(root, "versao", "roteiroSha256");
        string original = System.IO.Path.Combine(projectRoot, "ROTEIRO_A_CALOTEIRA_DO_OESTE_15_EPISODIOS.md");
        if (Hash(original) != root.GetAttribute("roteiroSha256")) throw Error("roteiro mudou; preparar novamente a temporada antes de montar");
        var result = new TemporadaDiretor(); int nextEpisode = 1, count = 0; bool hasShared = false;
        foreach (System.Xml.XmlNode node in root.ChildNodes)
        {
            var episode = node as System.Xml.XmlElement;
            if (System.Object.ReferenceEquals(episode, null)) throw Error("conteúdo inesperado no índice");
            if (episode.Name == "Compartilhada")
            {
                if (hasShared || nextEpisode != 16 || episode.GetAttribute("id") != "VINHETA_01") throw Error("vinheta única deve aparecer depois dos15episódios");
                hasShared = true;
                Attributes(episode, "id", "numero", "nome", "arquivo", "sha256");
                if (episode.GetAttribute("numero") != "0") throw Error("número da vinheta compartilhada deve ser0");
                if (episode.GetAttribute("nome").Length < 1 || episode.GetAttribute("nome").Length > 150 || episode.HasChildNodes)
                    throw Error("nome ou conteúdo da vinheta inválido");
                ValidateFile(episode.GetAttribute("arquivo"), episode.GetAttribute("sha256"), "compartilhados/VINHETA_01.json");
                result.shared = new Cena { Id = "VINHETA_01", Nome = episode.GetAttribute("nome"), Arquivo = episode.GetAttribute("arquivo"), Sha256 = episode.GetAttribute("sha256") };
                continue;
            }
            if (episode.Name != "Episodio") throw Error("elemento inesperado no índice");
            Attributes(episode, "numero", "total");
            int number = Integer(episode, "numero"), total = Integer(episode, "total");
            if (number != nextEpisode++ || number > 15 || total < 1 || total > 20) throw Error("episódios fora de ordem ou quantidade inválida");
            var rows = new System.Collections.Generic.List<Cena>();
            foreach (System.Xml.XmlNode sceneNode in episode.ChildNodes)
            {
                var scene = sceneNode as System.Xml.XmlElement;
                if (System.Object.ReferenceEquals(scene, null) || scene.Name != "Cena") throw Error("cena inesperada no índice");
                Attributes(scene, "id", "numero", "nome", "arquivo", "sha256");
                int index = Integer(scene, "numero");
                string id = "EP" + number.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "_C" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
                if (index != rows.Count + 1 || scene.GetAttribute("id") != id || scene.GetAttribute("nome").Length < 1 || scene.GetAttribute("nome").Length > 150)
                    throw Error("identidade ou ordem da cena inválida");
                string file = scene.GetAttribute("arquivo"), sha = scene.GetAttribute("sha256");
                ValidateFile(file, sha, "EP" + number.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "/" + id + ".json");
                rows.Add(new Cena { Id = id, Nome = scene.GetAttribute("nome"), Episodio = number, Numero = index, Arquivo = file, Sha256 = sha });
            }
            if (rows.Count != total) throw Error("total de cenas difere do índice");
            result.episodes.Add(number, rows); count += rows.Count;
        }
        if (nextEpisode != 16 || count != 94 || !hasShared) throw Error("índice precisa conter15episódios,94cenas e1vinheta");
        return result;
    }
    public static string DraftPath(string projectRoot, Cena scene)
    {
        if (System.String.IsNullOrEmpty(scene.Arquivo)) throw Error(scene.Id + " ainda depende de atuação integral; consulte o plano dessa cena");
        string path = System.IO.Path.Combine(projectRoot, "producao", "cenas_serie", scene.Arquivo.Replace('/', System.IO.Path.DirectorySeparatorChar));
        string actualHash = Hash(path);
        if (scene.Id == "EP13_C03")
        {
            // Somente as tres fontes literais13s; o indice original15s permanece historico.
            if (scene.Episodio != 13 || scene.Numero != 3 || scene.Arquivo != "EP13/EP13_C03.json" ||
                scene.Sha256 != "A54724F4B3A94CA49B04305AC196FA69A55E832326B03D06B09710AD64FF1ED1")
                throw Error("EP13_C03 perdeu identidade/arquivo/hash do indice preservado");
            PurchasePartFromSourceHash(actualHash);
        }
        else if (scene.Id == "EP11_C05" && actualHash != scene.Sha256)
        {
            // Opt-in somente cinco fontes literais27s; roteiro/catalagos originais preservados.
            if (scene.Episodio != 11 || scene.Numero != 5 || scene.Arquivo != "EP11/EP11_C05.json" ||
                scene.Sha256 != "44386C6A183748D04D398A3EF33868860B88B8FC10A84F54FDD7471E24B0CA6D")
                throw Error("EP11_C05 perdeu identidade/arquivo/hash do indice preservado");
            SecondLinePartFromSourceHash(actualHash);
        }
        else if (actualHash != scene.Sha256) throw Error("o rascunho de " + scene.Id + " mudou; conferir antes de montar");
        return path;
    }
    public static string SecondLinePartFromSourceHash(string sourceHash)
    {
        if (sourceHash == "849D86B6D80C59173B6E7E579E444E36D0FA95AD4876515AA45353117B7ABF73") return "A_ENVELOPE";
        if (sourceHash == "493938C3E169A7BAF59F691440D9DBFA16628102A0AB8C125BA27D7B333EF883") return "B_RELATORIO";
        if (sourceHash == "8DE35815A9363541BE7D05F5B0724FD197137DAA9E79FD0D01160D2AA828AA1D") return "B_RELATORIO";
        if (sourceHash == "24DB6726910CF3650C2EF5647DC246BCFF75BCF9CEE3B30887B6DB530A04A600") return "C1_PEGAR_PAPEL";
        if (sourceHash == "E6510D8795912CFD7B662F7282780ADDF60EA53E8767776D2FF335F841CC92B8") return "C1_PEGAR_PAPEL";
        if (sourceHash == "79BA7B6E1C6BAC341A573C2DAA8A9B8478E080FD058DC2D866E238D312E0415D") return "C1_PEGAR_PAPEL";
        if (sourceHash == "842460306D7EF31EDEC6777907D528EBB1258C7DF2A961B24BB1D7E97325857E") return "C2_LER_PAPEL";
        if (sourceHash == "A90A9A8536CF047FAE4A4CEC77F737232B97D9329E32D8C1303336EEF2BDD3A7") return "C2_LER_PAPEL";
        if (sourceHash == "2FCC273D434D0D6FD1934265508AF2287E8D18F057D181D06EB71254E7228F5F") return "D_PARAR_RISO";
        if (sourceHash == "D3FFA039812602DE159B1B3DEF71F4BD9070903FD5AFF839753A7E89C7B38330") return "D_PARAR_RISO";
        if (sourceHash == "AE654707FB23C3A668166FB5FCD71C9187E75F48F1404366AAEF485D9A03B2A4") return "D_PARAR_RISO";
        throw Error("EP11_C05 exige uma das cinco fontes literais27s; fonte alterada recusada");
    }
    public static string PurchasePartFromSourceHash(string sourceHash)
    {
        if (sourceHash == "AF3E97E42AA1ECFB3F269030A3E25105DE0F0A9E23FFAD46AFE52BF5B7845DE8") return "A_GALPAO_VAZIO";
        if (sourceHash == "FBEBBA1057B8642FD81D4E571B43BBFA2D3E6BABAE65A14A18244794692A7BBE") return "B_CAMINHOES_SAINDO";
        if (sourceHash == "4BE0D09F53F2412C0ACFD2EDC0F77438265402A4FE894FF7EB67195CD91D3263") return "C_PORTAO_FECHANDO";
        throw Error("EP13_C03 exige uma das tres fontes literais13s; rascunho15s ou fonte alterada recusados");
    }
    private static void ValidateFile(string file, string sha, string expected)
    {
        if (file.Length == 0 && sha.Length == 0) return;
        if (file != expected || !System.Text.RegularExpressions.Regex.IsMatch(sha, "\\A[0-9A-F]{64}\\z"))
            throw Error("arquivo ou hash de rascunho inválido");
    }
    private static void Attributes(System.Xml.XmlElement element, params string[] names)
    {
        if (element.Attributes.Count != names.Length) throw Error("atributos inesperados em " + element.Name);
        foreach (string name in names) if (!element.HasAttribute(name)) throw Error("atributo ausente: " + name);
    }
    private static int Integer(System.Xml.XmlElement element, string name)
    {
        int value;
        if (!System.Int32.TryParse(element.GetAttribute(name), System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out value)) throw Error("inteiro inválido no índice");
        return value;
    }
    private static string Hash(string path)
    {
        NoLinks(path);
        using (var input = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return System.BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
    }
    private static void NoLinks(string path)
    {
        string current = System.IO.Path.GetFullPath(path);
        while (!System.String.IsNullOrEmpty(current))
        {
            if ((System.IO.File.Exists(current) || System.IO.Directory.Exists(current)) &&
                (System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0) throw Error("links/reparse não são aceitos: " + current);
            current = System.IO.Path.GetDirectoryName(current);
        }
    }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("Temporada: " + message); }
}
