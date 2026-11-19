namespace StarterAssets
{
    /// <summary>
    /// Permite girar a mira (câmera) por código, sem depender de qual controlador está sendo usado.
    /// Fica nesta pasta porque o ThirdPersonController está em outro assembly (Unity.StarterAssets)
    /// e só consegue implementar interfaces que estejam no mesmo assembly que ele.
    /// </summary>
    public interface IControleMira
    {
        /// <param name="grausParaCima">Positivo sobe a mira, negativo desce.</param>
        /// <param name="grausParaDireita">Positivo vira para a direita, negativo para a esquerda.</param>
        void GirarMira(float grausParaCima, float grausParaDireita);
    }
}
