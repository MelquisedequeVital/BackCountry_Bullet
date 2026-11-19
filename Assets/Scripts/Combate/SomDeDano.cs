using UnityEngine;

/// <summary>
/// Toca um som quando o personagem leva dano (ex.: um gemido de dor).
/// Sorteia um som da lista sem repetir o anterior e pula o silêncio que costuma existir
/// no começo dos arquivos, para o som sair junto com o golpe.
/// Coloque no mesmo objeto que tem o componente Vida.
/// </summary>
public class SomDeDano : MonoBehaviour
{
    [Header("Sons")]
    [Tooltip("Sons de dano. Um é sorteado a cada golpe recebido")]
    public AudioClip[] sonsDeDano;

    [Header("Ajustes")]
    [Range(0f, 1f)]
    public float volume = 0.9f;

    [Tooltip("Variação aleatória do tom")]
    [Range(0f, 0.3f)]
    public float variacaoDeTom = 0.06f;

    [Tooltip("Segundos de silêncio pulados no começo de cada som " +
             "(os gemidos importados começam com 0,08 a 0,16 s de silêncio)")]
    [Range(0f, 0.3f)]
    public float pularInicio = 0.08f;

    [Tooltip("Tempo mínimo entre dois gemidos, para não virar um 'coral' com golpes seguidos")]
    [Min(0f)]
    public float intervaloMinimo = 0.25f;

    [Tooltip("0 = som 2D (bom para o jogador), 1 = som 3D que diminui com a distância (bom para inimigos)")]
    [Range(0f, 1f)]
    public float somNoEspaco = 0f;

    private Vida vida;
    private AudioSource fonte;
    private int ultimoSorteado = -1;
    private float proximoSomPermitido;

    void Awake()
    {
        vida = GetComponentInParent<Vida>();
        if (vida == null)
            Debug.LogWarning("SomDeDano: nenhum componente Vida encontrado.", this);

        fonte = gameObject.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.spatialBlend = somNoEspaco;
    }

    void OnEnable()
    {
        if (vida != null) vida.AoReceberDano += TocarSom;
    }

    void OnDisable()
    {
        if (vida != null) vida.AoReceberDano -= TocarSom;
    }

    private void TocarSom(float quantidade, Vector3 ponto, Vector3 direcao)
    {
        if (Time.time < proximoSomPermitido) return;

        AudioClip som = SortearSom();
        if (som == null) return;

        proximoSomPermitido = Time.time + intervaloMinimo;

        // Toca o arquivo a partir de "pularInicio", cortando o silêncio do começo
        fonte.clip = som;
        fonte.volume = volume;
        fonte.pitch = 1f + Random.Range(-variacaoDeTom, variacaoDeTom);
        fonte.time = Mathf.Min(pularInicio, som.length * 0.5f);
        fonte.Play();
    }

    private AudioClip SortearSom()
    {
        if (sonsDeDano == null || sonsDeDano.Length == 0) return null;
        if (sonsDeDano.Length == 1) return sonsDeDano[0];

        int indice;
        do { indice = Random.Range(0, sonsDeDano.Length); } while (indice == ultimoSorteado);

        ultimoSorteado = indice;
        return sonsDeDano[indice];
    }
}
