using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Uma conversa: a lista de falas na ordem em que aparecem.
/// Cada fala é de alguém (aparece na caixa de diálogo, embaixo) ou uma dica (aparece na caixa amarela, no alto).
/// O nome de quem fala NÃO fica escrito aqui: vem do componente NPC (campo Nome) ou do nome do jogador.
/// Assim, para renomear um personagem, basta mudar o Nome no prefab dele.
/// Crie pelo menu: Create → BackCountry → Diálogo.
/// </summary>
[CreateAssetMenu(menuName = "BackCountry/Diálogo", fileName = "NovoDialogo")]
public class Dialogo : ScriptableObject
{
    public enum TipoDeFala { Fala, Dica }

    /// <summary>Quem diz a fala.</summary>
    public enum Quem
    {
        [Tooltip("O NPC desta conversa (usa o Nome do componente NPC)")] NPC,
        [Tooltip("O jogador (usa o nome do jogador da CaixaDeDialogo)")] Jogador,
        [Tooltip("Outro personagem: o nome vem do campo Personagem (ex.: um robô)")] Outro,
    }

    [Serializable]
    public class Fala
    {
        public TipoDeFala tipo = TipoDeFala.Fala;
        public Quem quem = Quem.NPC;

        [Tooltip("Só usado quando Quem = Outro (ex.: ROBÔ ROXO)")]
        public string personagem;

        [TextArea(2, 6)]
        public string texto;
    }

    public List<Fala> falas = new List<Fala>();

    [Tooltip("Em vez de tocar todas as falas, sorteia UMA delas (ex.: moradores com falas soltas)")]
    public bool sortearUmaFala;

    /// <summary>As falas que vão ser mostradas desta vez (todas, ou uma sorteada).</summary>
    public List<Fala> FalasParaTocar()
    {
        if (!sortearUmaFala || falas.Count <= 1) return falas;
        return new List<Fala> { falas[UnityEngine.Random.Range(0, falas.Count)] };
    }
}
