using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tela de Configurações do menu principal: abas Áudio (volume geral), Imagem (tela cheia e resolução)
/// e Controles (só a lista de teclas). As escolhas ficam salvas (PlayerPrefs) e são reaplicadas
/// sozinhas toda vez que o jogo abre, em qualquer cena.
/// Ligue o botão Configurações em Abrir() e o Voltar em Fechar(). Os botões das abas chamam MostrarAba(0, 1 ou 2).
/// </summary>
public class MenuConfiguracoes : MonoBehaviour
{
    const string ChaveVolume = "config_volume";
    const string ChaveTelaCheia = "config_telaCheia";
    const string ChaveLargura = "config_resolucaoLargura";
    const string ChaveAltura = "config_resolucaoAltura";

    [Header("Abas (na ordem: Áudio, Imagem, Controles)")]
    public GameObject[] abas;
    [Tooltip("Botões das abas, na mesma ordem; o da aba aberta fica destacado")]
    public Button[] botoesDasAbas;

    [Header("Áudio")]
    public Slider sliderVolume;
    public TMP_Text textoVolume;

    [Header("Imagem")]
    public Toggle toggleTelaCheia;
    public TMP_Dropdown dropdownResolucao;

    private readonly List<Vector2Int> resolucoes = new List<Vector2Int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AplicarConfiguracoesSalvas()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(ChaveVolume, 1f);
        if (Application.isEditor) return; // no Editor a janela do Game não muda de resolução

        bool telaCheia = PlayerPrefs.GetInt(ChaveTelaCheia, Screen.fullScreen ? 1 : 0) == 1;
        int largura = PlayerPrefs.GetInt(ChaveLargura, Screen.width);
        int altura = PlayerPrefs.GetInt(ChaveAltura, Screen.height);
        Screen.SetResolution(largura, altura, telaCheia ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    void OnEnable()
    {
        AtualizarCampos();
    }

    /// <summary>Mostra a tela de configurações na aba Áudio.</summary>
    public void Abrir()
    {
        gameObject.SetActive(true);
        MostrarAba(0);
    }

    /// <summary>Esconde a tela de configurações e grava as escolhas.</summary>
    public void Fechar()
    {
        PlayerPrefs.Save();
        gameObject.SetActive(false);
    }

    /// <summary>Mostra só a aba pedida (0 = Áudio, 1 = Imagem, 2 = Controles).</summary>
    public void MostrarAba(int indice)
    {
        for (int i = 0; i < abas.Length; i++)
            if (abas[i] != null) abas[i].SetActive(i == indice);

        for (int i = 0; i < botoesDasAbas.Length; i++)
            if (botoesDasAbas[i] != null) botoesDasAbas[i].interactable = i != indice;
    }

    public void MudarVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(ChaveVolume, volume);
        if (textoVolume != null) textoVolume.text = Mathf.RoundToInt(volume * 100f) + "%";
    }

    public void MudarTelaCheia(bool telaCheia)
    {
        Screen.fullScreenMode = telaCheia ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        PlayerPrefs.SetInt(ChaveTelaCheia, telaCheia ? 1 : 0);
    }

    public void MudarResolucao(int indice)
    {
        if (indice < 0 || indice >= resolucoes.Count) return;
        Vector2Int resolucao = resolucoes[indice];
        Screen.SetResolution(resolucao.x, resolucao.y, Screen.fullScreenMode);
        PlayerPrefs.SetInt(ChaveLargura, resolucao.x);
        PlayerPrefs.SetInt(ChaveAltura, resolucao.y);
    }

    // Preenche os campos com o que está valendo agora, sem disparar os eventos de mudança
    void AtualizarCampos()
    {
        float volume = PlayerPrefs.GetFloat(ChaveVolume, AudioListener.volume);
        if (sliderVolume != null) sliderVolume.SetValueWithoutNotify(volume);
        if (textoVolume != null) textoVolume.text = Mathf.RoundToInt(volume * 100f) + "%";

        if (toggleTelaCheia != null) toggleTelaCheia.SetIsOnWithoutNotify(Screen.fullScreen);

        if (dropdownResolucao == null) return;
        resolucoes.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            var tamanho = new Vector2Int(r.width, r.height);
            if (!resolucoes.Contains(tamanho)) resolucoes.Add(tamanho);
        }
        resolucoes.Sort((a, b) => b.x != a.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));

        var opcoes = new List<string>();
        int atual = 0;
        for (int i = 0; i < resolucoes.Count; i++)
        {
            opcoes.Add($"{resolucoes[i].x} × {resolucoes[i].y}");
            if (resolucoes[i].x == Screen.width && resolucoes[i].y == Screen.height) atual = i;
        }
        dropdownResolucao.ClearOptions();
        dropdownResolucao.AddOptions(opcoes);
        dropdownResolucao.SetValueWithoutNotify(atual);
    }
}
