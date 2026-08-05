// ============================================================================
//  ModuleDefinition.cs  —  camada WFC.Data
//
//  Uma peça do tileset: o prefab + os 6 sockets + peso + quais rotações em Y
//  são permitidas + as tags.
//
//  IMPORTANTE: aqui se autora UMA orientação da peça. As rotações de 90° são
//  geradas no bake do TileSet — não crie 4 assets para a mesma parede.
// ============================================================================

using UnityEngine;
using WFC.Core;

namespace WFC.Data
{
    [CreateAssetMenu(fileName = "Module_", menuName = "WFC/Module Definition", order = 1)]
    public sealed class ModuleDefinition : ScriptableObject
    {
        [Tooltip("Prefab instanciado quando este módulo é escolhido. Pivô no CENTRO da célula, " +
                 "topo do piso em y=0, footprint = Cell Size.")]
        public GameObject prefab;

        [Tooltip("Sockets das 6 faces, na ordem do enum Direction: PosX, NegX, PosY, NegY, PosZ, NegZ.")]
        public FaceSocket[] faces = new FaceSocket[Directions.Count];

        [Tooltip("Peso relativo no sorteio. Maior = aparece mais. Coringas ficam bem baixos (0.1).")]
        [Min(0.0001f)]
        public float weight = 1f;

        [Tooltip("Quais múltiplos de 90° em Y são permitidos: [0°, 90°, 180°, 270°]. " +
                 "Desmarque as que produzem uma peça idêntica (ex.: um piso liso só precisa de 0°).")]
        public bool[] allowedYRotations = { true, true, true, true };

        [Tooltip("Classificação geométrica da peça. Ver ModuleTag.")]
        public ModuleTag tags = ModuleTag.None;

        [TextArea(1, 3)]
        [Tooltip("Anotação livre para você — o plugin ignora.")]
        public string notes;

        public FaceSocket GetFace(Direction dir) => faces[(int)dir];

        public void SetFace(Direction dir, FaceSocket value) => faces[(int)dir] = value;

        /// <summary>Quantas variantes rotacionadas esta peça vai gerar no bake (mínimo 1).</summary>
        public int RotationVariantCount
        {
            get
            {
                int n = 0;
                for (int r = 0; r < 4; r++)
                    if (allowedYRotations != null && r < allowedYRotations.Length && allowedYRotations[r]) n++;
                return n > 0 ? n : 1; // nenhuma marcada = trata como só 0°, em vez de sumir com a peça
            }
        }

        public bool IsRotationAllowed(int quarterTurns)
        {
            if (allowedYRotations == null || allowedYRotations.Length < 4) return quarterTurns == 0;
            bool any = false;
            for (int r = 0; r < 4; r++) any |= allowedYRotations[r];
            if (!any) return quarterTurns == 0;
            return allowedYRotations[quarterTurns & 3];
        }

        private void OnValidate()
        {
            // Arrays de tamanho fixo: o inspector adora deixar o usuário mudar isso sem querer.
            if (faces == null || faces.Length != Directions.Count)
            {
                var resized = new FaceSocket[Directions.Count];
                if (faces != null)
                    for (int i = 0; i < Mathf.Min(faces.Length, Directions.Count); i++) resized[i] = faces[i];
                faces = resized;
            }

            if (allowedYRotations == null || allowedYRotations.Length != 4)
            {
                var resized = new bool[4];
                if (allowedYRotations != null)
                    for (int i = 0; i < Mathf.Min(allowedYRotations.Length, 4); i++) resized[i] = allowedYRotations[i];
                else
                    resized[0] = true;
                allowedYRotations = resized;
            }

            if (weight <= 0f) weight = 0.0001f;
        }
    }
}
