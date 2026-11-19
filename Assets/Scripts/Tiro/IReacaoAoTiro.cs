/// <summary>
/// Qualquer componente que precisa "fazer algo" quando a arma dispara implementa esta interface
/// (tocar animação, recuo, som, raycast...).
/// O ArmaTiro encontra todos sozinho, então para adicionar um efeito novo basta criar
/// um script novo com esta interface, sem precisar mexer no ArmaTiro.
/// </summary>
public interface IReacaoAoTiro
{
    void AoAtirar();
}
