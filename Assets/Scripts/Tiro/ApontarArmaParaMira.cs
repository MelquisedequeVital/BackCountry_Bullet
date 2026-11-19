using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Faz a arma acompanhar a mira para cima e para baixo. Enquanto o personagem está armado,
/// inclina o tronco no mesmo ângulo vertical da câmera.
/// Para não "quebrar" o boneco, a inclinação é dividida entre os ossos da coluna
/// (Spine, Chest e UpperChest), tem limites e é suavizada.
/// </summary>
[RequireComponent(typeof(Animator))]
public class ApontarArmaParaMira : MonoBehaviour
{
    [Header("Limites")]
    [Tooltip("Máximo de graus que o tronco inclina para cima")]
    [Range(0f, 90f)]
    public float limiteParaCima = 60f;

    [Tooltip("Máximo de graus que o tronco inclina para baixo")]
    [Range(0f, 90f)]
    public float limiteParaBaixo = 50f;

    [Header("Ajuste Fino")]
    [Tooltip("Quanto do ângulo da câmera o tronco segue (1 = segue 100%)")]
    [Range(0f, 1.5f)]
    public float intensidade = 1f;

    [Tooltip("Correção fixa em graus, caso a arma aponte um pouco acima ou abaixo da mira")]
    [Range(-30f, 30f)]
    public float correcaoVertical = 0f;

    [Header("Suavização")]
    [Tooltip("Rapidez com que o tronco acompanha a câmera (maior = mais grudado no mouse)")]
    [Min(0.1f)]
    public float velocidadeSeguir = 20f;

    [Tooltip("Rapidez com que o efeito liga ao sacar e desliga ao guardar a arma")]
    [Min(0.1f)]
    public float velocidadeTransicao = 8f;

    // Quanto da inclinação total cada osso recebe (somam 1). Mais em cima = movimento mais natural.
    private static readonly (HumanBodyBones osso, float parte)[] DistribuicaoNaColuna =
    {
        (HumanBodyBones.Spine, 0.3f),
        (HumanBodyBones.Chest, 0.3f),
        (HumanBodyBones.UpperChest, 0.4f),
    };

    private IEstadoArma estadoArma;
    private IEstadoEsquiva estadoEsquiva;
    private Transform cameraPrincipal;
    private Transform[] ossos;
    private float[] partes;
    private float anguloAtual;
    private float pesoAtual;

    void Awake()
    {
        estadoArma = GetComponent<IEstadoArma>();
        estadoEsquiva = GetComponent<IEstadoEsquiva>();

        if (Camera.main != null)
            cameraPrincipal = Camera.main.transform;

        BuscarOssosDaColuna(GetComponent<Animator>());
        AvisarSeFaltarDependencias();
    }

    // Roda depois do Animator, por cima da pose da animação
    void LateUpdate()
    {
        float deltaTime = Time.deltaTime;

        AtualizarPeso(deltaTime);
        AtualizarAngulo(deltaTime);
        AplicarNaColuna(anguloAtual * pesoAtual);
    }

    // ---------- Configuração ----------

    // Pega os ossos que existem no personagem e redistribui as partes caso algum esteja faltando
    private void BuscarOssosDaColuna(Animator animator)
    {
        var ossosEncontrados = new List<Transform>();
        var partesEncontradas = new List<float>();
        float somaDasPartes = 0f;

        foreach (var (osso, parte) in DistribuicaoNaColuna)
        {
            Transform transformDoOsso = animator.GetBoneTransform(osso);
            if (transformDoOsso == null) continue;

            ossosEncontrados.Add(transformDoOsso);
            partesEncontradas.Add(parte);
            somaDasPartes += parte;
        }

        ossos = ossosEncontrados.ToArray();
        partes = partesEncontradas.ToArray();

        for (int i = 0; i < partes.Length; i++)
            partes[i] /= somaDasPartes;
    }

    private void AvisarSeFaltarDependencias()
    {
        if (estadoArma == null)
            Debug.LogWarning("ApontarArmaParaMira: nenhum componente de estado da arma (ex.: EquiparArma) encontrado.", this);

        if (cameraPrincipal == null)
            Debug.LogWarning("ApontarArmaParaMira: nenhuma câmera com a tag MainCamera encontrada.", this);

        if (ossos.Length == 0)
            Debug.LogWarning("ApontarArmaParaMira: ossos da coluna não encontrados. O personagem precisa ser Humanoid.", this);
    }

    // ---------- Cálculo ----------

    // Liga o efeito aos poucos ao sacar a arma e desliga aos poucos ao guardar.
    // Continua ativo na esquiva armada curta (a arma segue a mira), mas desliga no mergulho.
    private void AtualizarPeso(float deltaTime)
    {
        float pesoAlvo = EstaArmado() && !EstaMergulhando() ? 1f : 0f;
        pesoAtual = Mathf.MoveTowards(pesoAtual, pesoAlvo, velocidadeTransicao * deltaTime);
    }

    private void AtualizarAngulo(float deltaTime)
    {
        float anguloAlvo = CalcularAnguloAlvo();
        anguloAtual = Mathf.Lerp(anguloAtual, anguloAlvo, FatorSuavizacao(velocidadeSeguir, deltaTime));
    }

    // Ângulo vertical da câmera (positivo = olhando para cima), com intensidade, correção e limites
    private float CalcularAnguloAlvo()
    {
        if (cameraPrincipal == null) return 0f;

        float anguloDaCamera = Mathf.Asin(Mathf.Clamp(cameraPrincipal.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        float angulo = anguloDaCamera * intensidade + correcaoVertical;

        return Mathf.Clamp(angulo, -limiteParaBaixo, limiteParaCima);
    }

    private bool EstaArmado()
    {
        return estadoArma != null && estadoArma.EstaArmado;
    }

    private bool EstaMergulhando()
    {
        return estadoEsquiva != null && estadoEsquiva.TipoAtual == TipoDeEsquiva.Mergulho;
    }

    // ---------- Aplicação ----------

    private void AplicarNaColuna(float anguloTotal)
    {
        if (Mathf.Abs(anguloTotal) < 0.01f) return;

        for (int i = 0; i < ossos.Length; i++)
        {
            // Girar no eixo "direita" do personagem com ângulo negativo inclina para cima
            ossos[i].Rotate(transform.right, -anguloTotal * partes[i], Space.World);
        }
    }

    // Suavização que funciona igual em qualquer FPS
    private static float FatorSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-velocidade * deltaTime);
    }
}
