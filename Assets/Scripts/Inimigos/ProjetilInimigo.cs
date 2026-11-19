using UnityEngine;

/// <summary>
/// Esfera de energia disparada pelos inimigos.
/// - Anda em linha reta (não persegue), então andar de lado ou esquivar funciona;
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

        float distancia = velocidade * Time.deltaTime;

        if (VerificarColisao(distancia))
            return;

        transform.position += direcao * distancia;
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
                continue; // esquivou no tempo certo: o projétil passa direto

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
