using UnityEngine;

/// <summary>
/// Pinta o corpo de um personagem com uma cor (por cima das cores originais) e acende uma luz junto.
/// Usado como aviso visual: "vai dar o ataque especial", "está vulnerável", "mudou de fase"...
/// Não altera os materiais de verdade (usa MaterialPropertyBlock), então nada fica "manchado" depois.
/// </summary>
public class BrilhoDoCorpo
{
    private readonly Renderer[] renderizadores;
    private readonly Color[][] coresOriginais;
    private readonly MaterialPropertyBlock bloco = new MaterialPropertyBlock();
    private readonly Light luz;
    private readonly float intensidadeMaximaDaLuz;

    /// <param name="raiz">Objeto do personagem (pega todos os modelos dentro dele).</param>
    /// <param name="posicaoDaLuz">Onde a luz fica, em relação ao personagem.</param>
    /// <param name="alcanceDaLuz">Até onde a luz ilumina (metros).</param>
    /// <param name="intensidadeMaximaDaLuz">Força da luz com o brilho no máximo.</param>
    public BrilhoDoCorpo(Transform raiz, Vector3 posicaoDaLuz, float alcanceDaLuz = 5f, float intensidadeMaximaDaLuz = 6f)
    {
        renderizadores = raiz.GetComponentsInChildren<Renderer>();
        coresOriginais = new Color[renderizadores.Length][];

        for (int r = 0; r < renderizadores.Length; r++)
        {
            Material[] materiais = renderizadores[r].sharedMaterials;
            coresOriginais[r] = new Color[materiais.Length];
            for (int m = 0; m < materiais.Length; m++)
                coresOriginais[r][m] = CorDoMaterial(materiais[m]);
        }

        var objetoDaLuz = new GameObject("Brilho");
        objetoDaLuz.transform.SetParent(raiz, false);
        objetoDaLuz.transform.localPosition = posicaoDaLuz;
        luz = objetoDaLuz.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.range = alcanceDaLuz;
        luz.enabled = false;
        this.intensidadeMaximaDaLuz = intensidadeMaximaDaLuz;
    }

    /// <param name="intensidade">0 = cores normais, 1 = brilho máximo.</param>
    /// <param name="cor">Cor do brilho.</param>
    /// <param name="forca">Quanto a cor cobre o corpo no máximo (0 a 1).</param>
    public void Aplicar(float intensidade, Color cor, float forca = 0.75f)
    {
        for (int r = 0; r < renderizadores.Length; r++)
        {
            if (renderizadores[r] == null) continue;

            for (int m = 0; m < coresOriginais[r].Length; m++)
            {
                Color misturada = Color.Lerp(coresOriginais[r][m], cor, intensidade * forca);
                renderizadores[r].GetPropertyBlock(bloco, m);
                bloco.SetColor("_BaseColor", misturada);
                bloco.SetColor("_Color", misturada);
                renderizadores[r].SetPropertyBlock(bloco, m);
            }
        }

        if (luz == null) return;
        luz.enabled = intensidade > 0.01f;
        luz.color = cor;
        luz.intensity = intensidade * intensidadeMaximaDaLuz;
    }

    public void Desligar() => Aplicar(0f, Color.white);

    /// <summary>Valor de 0 a 1 que sobe e desce sozinho (para piscar).</summary>
    public static float Piscar(float tempo, float vezesPorSegundo)
    {
        return (Mathf.Sin(tempo * vezesPorSegundo * Mathf.PI * 2f) + 1f) * 0.5f;
    }

    private static Color CorDoMaterial(Material material)
    {
        if (material == null) return Color.white;
        if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color")) return material.GetColor("_Color");
        return Color.white;
    }
}
