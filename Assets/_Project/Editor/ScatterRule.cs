using System;
using System.Collections.Generic;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>Regras de distribuição de uma categoria (rochas, árvores, cactos...). Usado por <see cref="ScatterSet"/>.</summary>
    [CreateAssetMenu(menuName = "Backcountry Bullet/Scatter Rule", fileName = "Scatter_Nova")]
    public class ScatterRule : ScriptableObject
    {
        public enum Region { Qualquer, Planicie, Serras, AreaJogavel, ForaDaAreaJogavel }
        /// <summary>GameObject: instância de prefab (colisor próprio). TerrainTree: instância do Terrain (leve, só rotação Y).</summary>
        public enum Output { GameObject, TerrainTree }

        [Serializable]
        public class WeightedPrefab
        {
            public GameObject prefab;
            [Min(0f)] public float weight = 1f;
        }

        public bool enabled = true;
        [Tooltip("Nome do grupo na cena (Cenario/Scatter/<nome>). Vazio = nome do asset.")]
        public string categoryName = "";
        public List<WeightedPrefab> prefabs = new List<WeightedPrefab>();
        public Output output = Output.GameObject;

        [Header("Campo de cobertura da mata (VegetationField)")]
        [Range(0f, 1f), Tooltip("0 = ignora o campo; 1 = densidade multiplicada pela cobertura da mata (clareiras, estrada, lajedos, rocha).")]
        public float vegetationField = 0f;
        [Tooltip("Usa 1 - cobertura (para coisas de áreas abertas).")]
        public bool invertVegetationField;
        [Tooltip("Faixa aceita da máscara de lajedo (0 = fora, 1 = sobre o lajedo). Padrão (0, 1) = qualquer lugar.")]
        public Vector2 lajedoRange = new Vector2(0f, 1f);
        [Tooltip("Só no corredor da trilha principal (VegetationField.TrailFull/TrailEnd a partir da borda da estrada).")]
        public bool onlyNearTrail;

        [Header("Densidade")]
        [Tooltip("Instâncias por hectare (100 x 100 m) onde todas as condições são ideais.")]
        public float densityPerHectare = 4f;
        public Region region = Region.Qualquer;
        [Tooltip("Escala do ruído que cria manchas mais e menos densas (m). 0 = sem manchas.")]
        public float patchScale = 120f;
        [Range(0f, 1f), Tooltip("0 = densidade uniforme, 1 = só nas manchas.")]
        public float patchiness = 0.5f;

        [Header("Terreno")]
        public Vector2 slopeRange = new Vector2(0f, 25f);
        [Tooltip("Faixa de altura relativa ao chão da planície (m).")]
        public Vector2 heightRange = new Vector2(-10f, 300f);
        public bool avoidRiverBed = true;

        [Header("Zonas de exclusão")]
        [Tooltip("Distância mínima às áreas reservadas (vila, datacenter) (m).")]
        public float reservedClearance = 8f;
        [Tooltip("Distância mínima ao eixo da estrada, além da meia-largura (m).")]
        public float roadClearance = 3f;
        [Tooltip("Se > 0, só coloca dentro desta faixa de distância à borda da estrada (m), ex.: cobertura ao longo da estrada.")]
        public Vector2 roadBand = Vector2.zero;
        [Tooltip("Densidade perto da estrada/vila em relação à densidade longe delas.")]
        [Range(0f, 1f)] public float nearReservedDensity = 0.3f;
        [Tooltip("Distância (m) a partir da qual a densidade é total.")]
        public float densityFalloffDistance = 60f;

        [Header("Agrupamento")]
        public Vector2Int groupSize = new Vector2Int(1, 3);
        public float groupRadius = 6f;
        [Tooltip("Se definido, os grupos nascem perto de instâncias desta outra regra (ex.: coroa-de-frade sobre rochas).")]
        public ScatterRule nearRule;
        public float nearRuleDistance = 4f;
        [Tooltip("Coloca em cima dos colisores de outros objetos do Scatter (ex.: coroa-de-frade sobre lajedo) em vez do terreno.")]
        public bool placeOnColliders;

        [Header("Espaçamento")]
        [Tooltip("Distância mínima entre objetos (m).")]
        public float minDistance = 2f;
        [Range(0f, 1.5f), Tooltip("Fator sobre a soma dos raios dos objetos: <1 deixa encostar/sobrepor um pouco.")]
        public float footprintFactor = 0.7f;

        [Header("Transformação")]
        [Tooltip("Variação de escala uniforme (padrão ±20%).")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
        public bool randomYaw = true;
        [Range(0f, 1f), Tooltip("0 = em pé, 1 = alinhado à normal do terreno.")]
        public float alignToNormal = 0f;
        [Range(0f, 0.8f), Tooltip("Fração da altura do objeto enterrada no chão.")]
        public float sinkFraction = 0f;
        [Tooltip("Inclinação aleatória extra (graus).")]
        public float randomTilt = 0f;

        public string GroupName => string.IsNullOrEmpty(categoryName) ? name : categoryName;
    }
}
