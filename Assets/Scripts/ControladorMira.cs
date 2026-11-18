using UnityEngine;

public class ControladorMira : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Arraste aqui o objeto Mira de dentro do Canvas")]
    public GameObject imagemMira;

    private EquiparArma scriptEquipar;
    private bool miraVisivel;

    void Start()
    {
        scriptEquipar = GetComponent<EquiparArma>();
        AtualizarVisibilidade(false);
    }

    void Update()
    {
        VerificarEstadoArma();
    }

    // Mostra ou esconde a mira central conforme a arma é equipada ou guardada
    private void VerificarEstadoArma()
    {
        bool armadoAgora = (scriptEquipar != null && scriptEquipar.estaArmado);

        if (armadoAgora != miraVisivel)
        {
            miraVisivel = armadoAgora;
            AtualizarVisibilidade(miraVisivel);
        }
    }

    private void AtualizarVisibilidade(bool ativar)
    {
        if (imagemMira != null)
        {
            imagemMira.SetActive(ativar);
        }
    }
}
