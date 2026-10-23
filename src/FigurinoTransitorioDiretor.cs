// Sapatos retirados somente na ação original; nunca escreve/regenera um figurino XML.
// Fonte: qbcore-fivem/qb-radialmenu/client/clothing.lua, Shoes: component6/female35/texture0.
public sealed class FigurinoTransitorioDiretor : System.IDisposable
{
    private readonly System.Action<string, string> log;
    private GTA.Ped actor;
    private int handle, model, drawable, texture, palette;
    private System.IntPtr address;
    private bool applied, disposed;
    public FigurinoTransitorioDiretor(System.Action<string, string> logger) { log = logger; }
    public bool IsReady { get { return applied && !disposed && Owned() && Matches(35, 0, 0); } }
    public static bool Supports(string scene) { return scene == "EP03_C05" || scene == "EP08_C01" || scene == "EP08_C02"; }
    public void ApplyBareFeet(string scene, GTA.Ped ownActor)
    {
        if (disposed || applied || !Supports(scene) || System.Object.ReferenceEquals(ownActor, null) || !ownActor.Exists() ||
            ownActor.IsPlayer || ownActor.IsDead || !ownActor.IsPersistent || ownActor.IsInVehicle() || ownActor.Model.Hash != new GTA.Model("mp_f_freemode_01").Hash)
            throw Error("ação/modelo/ator inválido para retirar sapatos temporariamente");
        if (GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS, ownActor.Handle, 6) <= 35 ||
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, ownActor.Handle, 6, 35) <= 0 ||
            !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_PED_COMPONENT_VARIATION_VALID, ownActor.Handle, 6, 35, 0))
            throw Error("variante35/0 da fonte autora não existe neste modelo instalado");
        actor = ownActor; handle = actor.Handle; model = actor.Model.Hash; address = actor.MemoryAddress;
        drawable = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_DRAWABLE_VARIATION, handle, 6);
        texture = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_TEXTURE_VARIATION, handle, 6);
        palette = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_PALETTE_VARIATION, handle, 6);
        applied = true; // Registrar restauração antes do setter: erro seguinte não perde o par original.
        GTA.Native.Function.Call(GTA.Native.Hash.SET_PED_COMPONENT_VARIATION, handle, 6, 35, 0, 0);
        if (!Matches(35, 0, 0)) throw Error("getter não confirmou retirada temporária dos sapatos");
        Log("FIGURINO_SAPATO_TRANSITORIO", "Cena=" + scene + "; Modelo=mp_f_freemode_01; Comp6=35/0/0; Original=" + drawable + "/" + texture + "/" + palette + "; XMLs preservados; adequação visual ainda exige imagem");
    }
    public void Cleanup()
    {
        if (disposed || !applied) return;
        if (!Owned()) throw Error("identidade do ator mudou; não aplicar sapato a outra entidade");
        GTA.Native.Function.Call(GTA.Native.Hash.SET_PED_COMPONENT_VARIATION, handle, 6, drawable, texture, palette);
        if (!Matches(drawable, texture, palette)) throw Error("getter não confirmou restauração exata dos sapatos");
        Log("FIGURINO_SAPATO_RESTAURADO", "Handle=" + handle + "; Comp6=" + drawable + "/" + texture + "/" + palette + "; XML intacto");
        applied = false; actor = null;
    }
    public void Dispose() { Cleanup(); disposed = true; }
    private bool Owned() { return actor != null && actor.Exists() && !actor.IsPlayer && actor.Handle == handle && actor.Model.Hash == model && actor.MemoryAddress == address; }
    private bool Matches(int d, int t, int p) { return Owned() && GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_DRAWABLE_VARIATION, handle, 6) == d && GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_TEXTURE_VARIATION, handle, 6) == t && GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_PALETTE_VARIATION, handle, 6) == p; }
    private void Log(string tag, string value) { if (log != null) log(tag, value); }
    private static System.InvalidOperationException Error(string value) { return new System.InvalidOperationException("Figurino transitório: " + value); }
}
