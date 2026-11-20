using UnityEngine;

/// <summary>
/// Esfera de energia disparada pelos inimigos.
/// - Anda em linha reta, ou TELEGUIADA (chamando Teleguiar): curva na direção do alvo com um limite de giro,
///   e para de seguir quando chega perto (assim um passo para o lado ou o mergulho no último instante funcionam);
/// - Dá dano em quem tiver Vida (IDanificavel) e some ao bater em algo;
/// - Atravessa o jogador se ele estiver invencível (mergulho no tempo certo);
/// - Ignora o inimigo que atirou e os outros inimigos.
/// Use ProjetilInimigo.Criar(...) para disparar: o visual (esfera, rastro e luz) é montado sozinho.
/// </summary>
public class ProjetilInimigo : MonoBehaviour
{
    private Vector3 direcao;
    private float velocidade;
    private float dano;
    private float raio;
    private float morreEm;
    private Transform dono;

    // Teleguiado
    private Transform alvo;
    private Collider colisorDoAlvo;
    private float giroPorSegundo;
    private float paraDeSeguirEm;
    private float distanciaParaParar;
    private bool seguindo;

    /// <summary>
    /// Faz a esfera perseguir o alvo: vira no máximo "giroPorSegundo" graus por segundo, durante "duracao"
    /// segundos, e para de seguir quando fica a menos de "distanciaParaParar" metros (segue reto dali).
    /// </summary>
    public void Teleguiar(Transform alvo, Collider colisorDoAlvo, float giroPorSegundo, float duracao, float distanciaParaParar)
    {
        this.alvo = alvo;
        this.colisorDoAlvo = colisorDoAlvo;
        this.giroPorSegundo = giroPorSegundo;
        this.distanciaParaParar = distanciaParaParar;
        paraDeSeguirEm = Time.time + duracao;
        seguindo = alvo != null;
    }

    // ---------- Criação ----------

    /// <summary>Cria a esfera visual (sem disparar). Usada para mostrar a energia na mão antes do arremesso.</summary>
    public static GameObject CriarVisual(Color cor, float diametro, bool comRastro)
    {
        GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.name = "Esfera de Energia";
        Destroy(esfera.GetComponent<Collider>()); // a colisão é feita por código, não por colisor
        esfera.transform.localScale = Vector3.one * diametro;

        // Shader simples e sem luz: a esfera fica "acesa", sem sombra
        var material = new Material(ShaderSemLuz()) { color = Color.Lerp(cor, Color.white, 0.35f) };
        esfera.GetComponent<MeshRenderer>().material = material;
        esfera.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Light luz = esfera.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = cor;
        luz.range = 3f;
        luz.intensity = 3f;

        if (comRastro)
            AdicionarRastro(esfera, cor, diametro);

        return esfera;
    }

    /// <summary>Dispara uma esfera de energia a partir de "origem" na "direcao" informada.</summary>
    public static ProjetilInimigo Criar(Vector3 origem, Vector3 direcao, float velocidade, float dano,
                                        Color cor, float diametro, float tempoDeVida, Transform dono)
    {
        GameObject esfera = CriarVisual(cor, diametro, comRastro: true);
        esfera.transform.position = origem;

        ProjetilInimigo projetil = esfera.AddComponent<ProjetilInimigo>();
        projetil.direcao = direcao.normalized;
        projetil.velocidade = velocidade;
        projetil.dano = dano;
        projetil.raio = diametro * 0.5f;
        projetil.morreEm = Time.time + tempoDeVida;
        projetil.dono = dono;
        return projetil;
    }

    private static void AdicionarRastro(GameObject esfera, Color cor, float diametro)
    {
        TrailRenderer rastro = esfera.AddComponent<TrailRenderer>();
        rastro.time = 0.35f;
        rastro.startWidth = diametro * 0.9f;
        rastro.endWidth = 0f;
        rastro.material = new Material(ShaderSemLuz());
        rastro.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var gradiente = new Gradient();
        gradiente.SetKeys(
            new[] { new GradientColorKey(cor, 0f), new GradientColorKey(cor, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        rastro.colorGradient = gradiente;
    }

    // "Sprites/Default" sempre existe no Unity; o Unlit do URP fica de reserva
    private static Shader ShaderSemLuz()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        return shader;
    }

    // ---------- Voo ----------

    void Update()
    {
        if (Time.time >= morreEm)
        {
            Destroy(gameObject);
            return;
        }

        Perseguir(Time.deltaTime);

        float distancia = velocidade * Time.deltaTime;

        if (VerificarColisao(distancia))
            return;

        transform.position += direcao * distancia;
    }

    // Vira aos poucos na direção do alvo; perto dele (ou depois do tempo) "se compromete" e segue reto
    private void Perseguir(float deltaTime)
    {
        if (!seguindo) return;

        if (alvo == null || Time.time >= paraDeSeguirEm)
        {
            seguindo = false;
            return;
        }

        Vector3 centro = colisorDoAlvo != null && colisorDoAlvo.enabled ? colisorDoAlvo.bounds.center : alvo.position + Vector3.up;
        Vector3 paraOAlvo = centro - transform.position;

        if (paraOAlvo.magnitude <= distanciaParaParar)
        {
            seguindo = false;
            return;
        }

        direcao = Vector3.RotateTowards(direcao, paraOAlvo.normalized, giroPorSegundo * Mathf.Deg2Rad * deltaTime, 0f);
    }

    // Lança uma "esfera invisível" do tamanho do projétil no trecho que ele vai andar neste quadro
    private bool VerificarColisao(float distancia)
    {
        RaycastHit[] acertos = Physics.SphereCastAll(transform.position, raio, direcao, distancia,
                                                     ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit acerto in acertos)
        {
            if (DeveIgnorar(acerto.collider)) continue;

            Vida vidaAtingida = acerto.collider.GetComponentInParent<Vida>();
            if (vidaAtingida != null && vidaAtingida.EstaInvencivel())
            {
                // Esquivou no tempo certo: a Vida ignora o dano e avisa "desviou"; o projétil passa direto
                vidaAtingida.ReceberDano(dano, acerto.point, direcao);
                continue;
            }

            IDanificavel alvo = acerto.collider.GetComponentInParent<IDanificavel>();
            if (alvo != null && alvo.EstaVivo)
                alvo.ReceberDano(dano, acerto.point, direcao);

            Destroy(gameObject);
            return true;
        }
        return false;
    }

    // Não bate em quem atirou nem em outros inimigos
    private bool DeveIgnorar(Collider colisor)
    {
        if (dono != null && colisor.transform.IsChildOf(dono)) return true;
        return colisor.GetComponentInParent<InimigoBase>() != null;
    }
}
