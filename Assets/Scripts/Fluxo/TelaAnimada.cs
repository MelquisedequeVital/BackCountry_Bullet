using UnityEngine;

/// <summary>
/// Animação das telas de pausa, morte e vitória: entra com fade e um leve zoom, e opcionalmente
/// gira os raios de fundo e faz o título "falhar" (glitch). Usa o tempo sem escala, então funciona
/// com o jogo pausado (Time.timeScale = 0).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TelaAnimada : MonoBehaviour
{
    [Header("Entrada")]
    [Tooltip("O que cresce durante a entrada (título, frase e botões)")]
    public RectTransform conteudo;
    [Min(0.01f)] public float duracao = 0.3f;
    [Range(0.5f, 1f)] public float escalaInicial = 0.94f;

    [Header("Raios (opcional)")]
    public RectTransform girar;
    public float grausPorSegundo = 6f;

    [Header("Glitch (opcional)")]
    [Tooltip("Cópias do título que tremem de vez em quando")]
    public RectTransform[] glitch = new RectTransform[0];
    [Range(0f, 1f)] public float chanceDoGlitch = 0.18f;
    public float forcaDoGlitch = 12f;

    private CanvasGroup grupo;
    private Vector2[] posicoesOriginais;
    private float tempo;
    private float proximoGlitch;

    void Awake()
    {
        grupo = GetComponent<CanvasGroup>();
        posicoesOriginais = new Vector2[glitch.Length];
        for (int i = 0; i < glitch.Length; i++)
            if (glitch[i] != null) posicoesOriginais[i] = glitch[i].anchoredPosition;
    }

    void OnEnable()
    {
        tempo = 0f;
        Aplicar(0f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (tempo < duracao)
        {
            tempo += dt;
            Aplicar(Mathf.Clamp01(tempo / duracao));
        }

        if (girar != null) girar.Rotate(0f, 0f, -grausPorSegundo * dt);

        if (glitch.Length > 0 && Time.unscaledTime >= proximoGlitch)
        {
            proximoGlitch = Time.unscaledTime + 0.07f;
            bool tremer = Random.value < chanceDoGlitch;
            for (int i = 0; i < glitch.Length; i++)
                if (glitch[i] != null)
                    glitch[i].anchoredPosition = posicoesOriginais[i] + (tremer ? Random.insideUnitCircle * forcaDoGlitch : Vector2.zero);
        }
    }

    // t de 0 a 1, com desaceleração no fim
    void Aplicar(float t)
    {
        float suave = 1f - Mathf.Pow(1f - t, 3f);
        grupo.alpha = suave;
        if (conteudo != null) conteudo.localScale = Vector3.one * Mathf.LerpUnclamped(escalaInicial, 1f, suave);
    }
}
