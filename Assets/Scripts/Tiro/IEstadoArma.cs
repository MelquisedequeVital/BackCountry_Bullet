/// <summary>
/// Informa se o personagem está com a arma na mão.
/// O sistema de tiro depende só desta interface, e não do script EquiparArma diretamente.
/// Assim, se um dia você trocar o jeito de equipar armas, o tiro continua funcionando.
/// </summary>
public interface IEstadoArma
{
    bool EstaArmado { get; }
}
