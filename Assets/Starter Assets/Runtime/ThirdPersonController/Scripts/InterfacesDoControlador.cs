using UnityEngine;

// Estas interfaces ficam nesta pasta porque o ThirdPersonController está em outro assembly
// (Unity.StarterAssets) e só consegue implementar interfaces que estejam no mesmo assembly que ele.
// Os scripts do jogo (tiro, esquiva...) dependem destes contratos, e não do controlador em si.
namespace StarterAssets
{
    /// <summary>
    /// Permite girar a mira (câmera) por código. Usado pelo recuo da câmera.
    /// </summary>
    public interface IControleMira
    {
        /// <param name="grausParaCima">Positivo sobe a mira, negativo desce.</param>
        /// <param name="grausParaDireita">Positivo vira para a direita, negativo para a esquerda.</param>
        void GirarMira(float grausParaCima, float grausParaDireita);
    }

    /// <summary>
    /// Dá acesso ao movimento do personagem. Usado pela esquiva.
    /// </summary>
    public interface IControleMovimento
    {
        /// <summary>Direção que o jogador está apertando (WASD / analógico).</summary>
        Vector2 EntradaMovimento { get; }

        /// <summary>Se o personagem está pisando no chão.</summary>
        bool NoChao { get; }

        /// <summary>Quando verdadeiro, o controlador para de andar e pular (só a gravidade continua).</summary>
        bool MovimentoBloqueado { get; set; }
    }
}
