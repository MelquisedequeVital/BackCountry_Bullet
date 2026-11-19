using UnityEngine;

/// <summary>
/// Som do disparo: a cada tiro sorteia um dos sons da lista (sem repetir o anterior),
/// varia um pouco o tom e, se houver, toca um "eco" mais baixo por cima (o som do tiro
/// se espalhando pelo ambiente).
/// O AudioSource é criado sozinho se o campo ficar vazio.
/// </summary>
public class SomDoTiro : MonoBehaviour, IReacaoAoTiro
{
    [Header("Sons")]
    [Tooltip("Sons do disparo. Um é sorteado a cada tiro (ex.: sfx_wpn_ar_gunshot_01, 02 e 03)")]
    public AudioClip[] sonsDoTiro;

    [Tooltip("Eco opcional tocado junto com o tiro (ex.: sfx_wpn_ar_tail_01)")]
    public AudioClip somDoEco;

    [Header("Volume e Tom")]
    [Range(0f, 1f)]
    public float volumeDoTiro = 0.35f;

    [Range(0f, 1f)]
    public float volumeDoEco = 0.3f;

    [Tooltip("Variação aleatória do tom, para os tiros não soarem todos iguais")]
    [Range(0f, 0.3f)]
    public float variacaoDeTom = 0.05f;

    [Header("Avançado")]
    [Tooltip("Caixa de som usada (pode deixar vazio: uma é criada automaticamente)")]
    public AudioSource fonte;

    private int ultimoSorteado = -1;

    void Awake()
    {
        if (fonte == null)
            fonte = CriarFonte();
    }

    public void AoAtirar()
    {
        AudioClip som = SortearSom();
        if (som == null) return;

        fonte.pitch = 1f + Random.Range(-variacaoDeTom, variacaoDeTom);

        // PlayOneShot deixa um tiro tocar por cima do outro, sem cortar o anterior
        fonte.PlayOneShot(som, volumeDoTiro);

        if (somDoEco != null)
            fonte.PlayOneShot(somDoEco, volumeDoEco);
    }

    // Sorteia um som diferente do último, para não repetir duas vezes seguidas
    private AudioClip SortearSom()
    {
        if (sonsDoTiro == null || sonsDoTiro.Length == 0) return null;
        if (sonsDoTiro.Length == 1) return sonsDoTiro[0];

        int indice;
        do { indice = Random.Range(0, sonsDoTiro.Length); } while (indice == ultimoSorteado);

        ultimoSorteado = indice;
        return sonsDoTiro[indice];
    }

    // Som 2D: a arma é do próprio jogador, então o volume não depende da posição da câmera
    private AudioSource CriarFonte()
    {
        AudioSource nova = gameObject.AddComponent<AudioSource>();
        nova.playOnAwake = false;
        nova.spatialBlend = 0f;
        return nova;
    }
}
