using UnityEditor;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>
    /// O prefab Jogador foi montado sem dois componentes que o jogador da SampleScene tinha:
    ///  - Derrubada: o arremesso da investida (voa para trás → fica no chão → levanta);
    ///  - FeedbackDeEsquiva: o brilho e a câmera lenta da esquiva perfeita.
    /// Sem a Derrubada, o boss e o robô laranja só tiram vida, sem derrubar.
    /// Coloca os dois no objeto principal do prefab (o mesmo da Vida), uma vez.
    /// </summary>
    [InitializeOnLoad]
    public static class CorrigirPrefabDoJogador
    {
        const string Caminho = "Assets/Prefabs/Jogador/Jogador.prefab";

        static CorrigirPrefabDoJogador() => EditorApplication.delayCall += () => Corrigir(false);

        [MenuItem("BackCountry/Jogador/Conferir componentes do prefab Jogador")]
        static void Menu() => Corrigir(true);

        static void Corrigir(bool avisarSeOk)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Caminho);
            if (asset == null) return;
            var vidaNoAsset = asset.GetComponentInChildren<Vida>(true);
            if (vidaNoAsset == null) return;
            bool falta = vidaNoAsset.GetComponent<Derrubada>() == null || vidaNoAsset.GetComponent<FeedbackDeEsquiva>() == null;
            if (!falta)
            {
                if (avisarSeOk) Debug.Log("Jogador: o prefab já tem a Derrubada e o FeedbackDeEsquiva.");
                return;
            }

            var raiz = PrefabUtility.LoadPrefabContents(Caminho);
            try
            {
                var alvo = raiz.GetComponentInChildren<Vida>(true).gameObject;
                string adicionados = "";
                if (alvo.GetComponent<Derrubada>() == null)
                {
                    var d = alvo.AddComponent<Derrubada>();
                    d.estadoDaQueda = "Derrubado";
                    d.estadoDoLevantar = "Levantado";
                    adicionados += "Derrubada ";
                }
                if (alvo.GetComponent<FeedbackDeEsquiva>() == null)
                {
                    alvo.AddComponent<FeedbackDeEsquiva>();
                    adicionados += "FeedbackDeEsquiva ";
                }
                PrefabUtility.SaveAsPrefabAsset(raiz, Caminho);
                Debug.Log($"Jogador: adicionado ao prefab Jogador: {adicionados.Trim()}. A investida do boss volta a derrubar.");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
    }
}
