using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Faz o botão Play do Editor sempre começar pela primeira cena do Build Profiles (o MenuPrincipal),
/// não importa qual cena esteja aberta. Igual ao jogo compilado.
/// Para testar uma cena direto (ex.: montar a Caatinga), desmarque
/// "BackCountry → Começar o Play pelo Menu" na barra de menus do Unity.
/// </summary>
[InitializeOnLoad]
public static class ComecarPeloMenu
{
    private const string Chave = "BackCountry.ComecarPeloMenu";
    private const string ItemDoMenu = "BackCountry/Começar o Play pelo Menu";

    private static bool Ligado
    {
        get => EditorPrefs.GetBool(Chave, true);
        set => EditorPrefs.SetBool(Chave, value);
    }

    static ComecarPeloMenu()
    {
        EditorBuildSettings.sceneListChanged += Aplicar;
        // Espera o Editor terminar de carregar antes de ler a lista de cenas.
        EditorApplication.delayCall += Aplicar;
    }

    [MenuItem(ItemDoMenu, false, 0)]
    private static void Alternar()
    {
        Ligado = !Ligado;
        Aplicar();
        Debug.Log(Ligado
            ? "Play agora começa pelo MenuPrincipal."
            : "Play agora começa pela cena aberta.");
    }

    [MenuItem(ItemDoMenu, true)]
    private static bool AlternarValidar()
    {
        Menu.SetChecked(ItemDoMenu, Ligado);
        return true;
    }

    private static void Aplicar()
    {
        if (!Ligado)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        foreach (var cena in EditorBuildSettings.scenes)
        {
            if (!cena.enabled) continue;
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(cena.path);
            if (asset != null)
            {
                EditorSceneManager.playModeStartScene = asset;
                return;
            }
        }

        EditorSceneManager.playModeStartScene = null;
        Debug.LogWarning("ComecarPeloMenu: nenhuma cena ligada no Build Profiles (File → Build Profiles → Scene List).");
    }
}
