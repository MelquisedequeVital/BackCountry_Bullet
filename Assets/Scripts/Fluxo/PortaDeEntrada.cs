using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Porta que leva o jogador para outro lugar (ex.: a entrada da torre da NimbusCorp → o galpão do boss).
/// Perto dela aparece "[E] Entrar" (no mesmo lugar do "[E] Falar com ..."); apertando E a tela escurece,
/// o jogador é levado para o ponto "Destino", olhando para a frente dele (seta azul), e a tela clareia.
/// Coloque num objeto vazio na frente da porta e arraste o ponto de chegada para "Destino".
/// </summary>
public class PortaDeEntrada : MonoBehaviour
{
    [Tooltip("Para onde o jogador vai (a posição e a frente deste objeto)")]
    public Transform destino;
    [Tooltip("Texto do aviso (fica \"[E] Entrar\")")]
    public string texto = "Entrar";
    [Tooltip("Distância (m) em que o aviso aparece")]
    [Min(0.5f)] public float distancia = 4f;
    [Tooltip("Duração do escurecer e do clarear da tela (s)")]
    [Min(0f)] public float duracaoDoFade = 0.35f;

    private static readonly List<PortaDeEntrada> todas = new List<PortaDeEntrada>();
    private static Image telaPreta;
    private bool entrando;

    void OnEnable() => todas.Add(this);
    void OnDisable() => todas.Remove(this);

    // A caixa de diálogo é quem mostra o aviso e lê o E; garante que ela existe mesmo numa cena sem NPC
    void Start() => _ = CaixaDeDialogo.Instancia;

    /// <summary>A porta mais perto do jogador, dentro da distância dela (ou nula).</summary>
    public static PortaDeEntrada MaisProxima(Vector3 posicao)
    {
        PortaDeEntrada melhor = null;
        float menor = float.MaxValue;
        foreach (var porta in todas)
        {
            if (porta.destino == null || porta.entrando) continue;
            Vector3 d = posicao - porta.transform.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist > porta.distancia || dist >= menor) continue;
            melhor = porta; menor = dist;
        }
        return melhor;
    }

    /// <summary>Leva o jogador para o destino (chamado pela caixa de diálogo quando o jogador aperta E).</summary>
    public void Entrar(Transform jogador)
    {
        if (entrando || destino == null || jogador == null) return;
        StartCoroutine(Levar(jogador));
    }

    private IEnumerator Levar(Transform jogador)
    {
        entrando = true;
        var controle = jogador.GetComponent<IControleMovimento>();
        if (controle != null) controle.MovimentoBloqueado = true;

        yield return Fade(0f, 1f);

        Vector3 antes = jogador.position;
        Quaternion giro = Quaternion.Euler(0f, destino.eulerAngles.y, 0f);
        // O CharacterController desfaz teleportes enquanto está ligado: desliga, move e religa (igual ao Reviver)
        var controlador = jogador.GetComponent<CharacterController>();
        if (controlador != null) controlador.enabled = false;
        jogador.SetPositionAndRotation(destino.position + Vector3.up * 0.05f, giro); // pivô nos pés; um tiquinho acima do chão
        if (controlador != null) controlador.enabled = true;

        // Avisa as câmeras que seguem o jogador, para elas pularem junto em vez de "voarem" até lá
        Vector3 delta = jogador.position - antes;
        foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
        {
            Transform alvo = cam.Follow != null ? cam.Follow : cam.LookAt;
            if (alvo != null && alvo.IsChildOf(jogador)) CinemachineCore.OnTargetObjectWarped(alvo, delta);
        }

        yield return null;
        if (controle != null) controle.MovimentoBloqueado = false;
        yield return Fade(1f, 0f);
        entrando = false;
    }

    private IEnumerator Fade(float de, float para)
    {
        if (duracaoDoFade <= 0f) yield break;
        var tela = TelaPreta();
        tela.enabled = true;
        for (float t = 0f; t < duracaoDoFade; t += Time.unscaledDeltaTime)
        {
            tela.color = new Color(0f, 0f, 0f, Mathf.Lerp(de, para, t / duracaoDoFade));
            yield return null;
        }
        tela.color = new Color(0f, 0f, 0f, para);
        tela.enabled = para > 0f;
    }

    private static Image TelaPreta()
    {
        if (telaPreta != null) return telaPreta;
        var canvasGo = new GameObject("Fade da Porta");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60; // por cima do HUD, do diálogo e das telas
        var img = new GameObject("Preto").AddComponent<Image>();
        img.transform.SetParent(canvasGo.transform, false);
        img.rectTransform.anchorMin = Vector2.zero; img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
        img.raycastTarget = false;
        img.color = new Color(0f, 0f, 0f, 0f);
        img.enabled = false;
        telaPreta = img;
        return img;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, distancia);
        if (destino == null) return;
        Gizmos.DrawLine(transform.position + Vector3.up, destino.position + Vector3.up);
        Gizmos.color = Color.blue;
        Vector3 inicio = destino.position + Vector3.up;
        Gizmos.DrawLine(inicio, inicio + destino.forward * 1.5f);
        Gizmos.DrawSphere(inicio + destino.forward * 1.5f, 0.1f);
    }
}
