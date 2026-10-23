using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using GTA.UI;

public static class VerificarAvisos
{
    public static int Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
        {
            if (new AssemblyName(e.Name).Name != "ScriptHookVDotNet3") return null;
            return Assembly.LoadFrom(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "dependencias", "ScriptHookVDotNet-v3.7.0-nightly.191", "ScriptHookVDotNet3.dll")));
        };
        return RunChecks();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunChecks()
    {
        // Objeto gerenciado sem construtor e sem chamar o jogo ou funções nativas.
        FeedPost notification = (FeedPost)FormatterServices.GetUninitializedObject(typeof(FeedPost));
        bool reproduced = false;
        try { bool oldResult = notification != null; GC.KeepAlive(oldResult); }
        catch (NullReferenceException) { reproduced = true; }
        bool correctPresent = !Object.ReferenceEquals(notification, null);
        bool correctAbsent = Object.ReferenceEquals((FeedPost)null, null);
        if (!reproduced || !correctPresent || !correctAbsent)
        {
            Console.WriteLine("FALHA: resultado inesperado no teste dos avisos.");
            return 1;
        }
        Console.WriteLine("PASSOU: erro anterior reproduzido; verificação corrigida aceita referência presente e ausente.");
        Console.WriteLine("Teste somente .NET; não comprova exibição nem montagem no jogo.");
        return 0;
    }
}
