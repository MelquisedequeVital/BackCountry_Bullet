using System;
using UnityEngine;

/// <summary>
/// Descobre o que a bala acertou: lança um raio invisível do centro da tela (onde fica a mira).
/// Outros scripts podem "escutar" o evento AoAcertar (por exemplo, para aplicar dano no futuro).
/// </summary>
public class DisparoRaycast : MonoBehaviour, IReacaoAoTiro
{
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

        if (Physics.Raycast(raio, out RaycastHit acerto, alcance, camadasAtingiveis, QueryTriggerInteraction.Ignore))
            RegistrarAcerto(raio, acerto);
        else
            RegistrarErro(raio);
    }

    private Ray CriarRaioDoCentroDaTela()
    {
        return cameraPrincipal.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    }

    private void RegistrarAcerto(Ray raio, RaycastHit acerto)
    {
        if (mostrarLinhaDeTeste)
        {
            Debug.DrawLine(raio.origin, acerto.point, Color.red, 1f);
            Debug.Log($"Acertou: {acerto.collider.name}", acerto.collider);
        }

        AoAcertar?.Invoke(acerto);
    }

    private void RegistrarErro(Ray raio)
    {
        if (mostrarLinhaDeTeste)
            Debug.DrawRay(raio.origin, raio.direction * alcance, Color.yellow, 1f);
    }
}
