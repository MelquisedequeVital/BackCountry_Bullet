using System;
using UnityEngine;

/// <summary>
/// Descobre o que a bala acertou: lança um raio invisível do centro da tela (onde fica a mira)
/// e causa dano em quem puder levar dano (IDanificavel, ex.: o script Vida dos robôs).
/// Outros scripts podem "escutar" o evento AoAcertar (por exemplo, para efeitos de impacto).
/// </summary>
public class DisparoRaycast : MonoBehaviour, IReacaoAoTiro
{
    [Tooltip("Quanto de vida cada tiro tira")]
    [Min(0f)]
    public float dano = 25f;

    [Tooltip("Distância máxima que a bala alcança")]
    [Min(0f)]
    public float alcance = 100f;

    [Tooltip("Camadas que a bala pode acertar (deixe Everything se não souber)")]
    public LayerMask camadasAtingiveis = ~0;

    [Tooltip("Desenha a linha do tiro na aba Scene (vermelha = acertou, amarela = errou)")]
    public bool mostrarLinhaDeTeste = true;

    /// <summary>Disparado quando a bala acerta algum objeto.</summary>
    public event Action<RaycastHit> AoAcertar;

    private Camera cameraPrincipal;

    void Awake()
    {
        cameraPrincipal = Camera.main;

        if (cameraPrincipal == null)
            Debug.LogWarning("DisparoRaycast: nenhuma câmera com a tag MainCamera encontrada.", this);
    }

    public void AoAtirar()
    {
        if (cameraPrincipal == null) return;

        Ray raio = CriarRaioDoCentroDaTela();

        if (BuscarPrimeiroAcerto(raio, out RaycastHit acerto))
            RegistrarAcerto(raio, acerto);
        else
            RegistrarErro(raio);
    }

    private Ray CriarRaioDoCentroDaTela()
    {
        return cameraPrincipal.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    }

    // Pega o objeto mais próximo que o raio atravessa, ignorando o próprio atirador
    // (a câmera fica atrás do ombro, então o raio pode passar pelo corpo do jogador).
    private bool BuscarPrimeiroAcerto(Ray raio, out RaycastHit maisProximo)
    {
        RaycastHit[] acertos = Physics.RaycastAll(raio, alcance, camadasAtingiveis, QueryTriggerInteraction.Ignore);
        maisProximo = default;
        float menorDistancia = float.MaxValue;

        foreach (RaycastHit acerto in acertos)
        {
            if (acerto.collider.transform.IsChildOf(transform)) continue;

            if (acerto.distance < menorDistancia)
            {
                menorDistancia = acerto.distance;
                maisProximo = acerto;
            }
        }

        return menorDistancia < float.MaxValue;
    }

    private void RegistrarAcerto(Ray raio, RaycastHit acerto)
    {
        if (mostrarLinhaDeTeste)
        {
            Debug.DrawLine(raio.origin, acerto.point, Color.red, 1f);
            Debug.Log($"Acertou: {acerto.collider.name}", acerto.collider);
        }

        CausarDano(raio, acerto);
        AoAcertar?.Invoke(acerto);
    }

    // Procura a Vida no objeto atingido ou nos "pais" dele (o colisor pode estar num filho do robô)
    private void CausarDano(Ray raio, RaycastHit acerto)
    {
        IDanificavel alvo = acerto.collider.GetComponentInParent<IDanificavel>();

        if (alvo != null && alvo.EstaVivo)
            alvo.ReceberDano(dano, acerto.point, raio.direction);
    }

    private void RegistrarErro(Ray raio)
    {
        if (mostrarLinhaDeTeste)
            Debug.DrawRay(raio.origin, raio.direction * alcance, Color.yellow, 1f);
    }
}
