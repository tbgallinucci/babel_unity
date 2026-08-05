// ============================================================================
//  TileSet.cs  —  camada WFC.Data
//
//  A lista de peças + o BAKE. O bake é o momento em que os conceitos de autoria
//  (prefab, socket, rotação permitida) viram a única coisa que o solver entende:
//  índices de variante, pesos e uma AdjacencyTable de bitsets.
//
//  O bake roda em EDIT-TIME (botão "Rebuild adjacency" no inspector), nunca em
//  runtime — na transição de andar o custo tem que ser zero.
//
//  VARIANTE = (módulo, rotação em Y). Uma parede com as 4 rotações permitidas
//  vira 4 variantes distintas para o solver, cada uma com seus sockets já
//  permutados. É isso que evita ter que modelar 4 prefabs.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WFC.Core;

namespace WFC.Data
{
    [CreateAssetMenu(fileName = "TileSet", menuName = "WFC/Tile Set", order = 2)]
    public sealed class TileSet : ScriptableObject
    {
        [Serializable]
        public struct Variant
        {
            public int moduleIndex;   // índice em `modules`
            public int rotationY;     // 0..3 quartos de volta
        }

        [Header("Autoria")]
        [Tooltip("Vocabulário de sockets usado pelas peças deste tileset.")]
        public SocketLibrary socketLibrary;

        [Tooltip("As peças. O bake expande cada uma nas rotações permitidas.")]
        public List<ModuleDefinition> modules = new List<ModuleDefinition>();

        [Header("Bake (gerado — não editar à mão)")]
        [SerializeField] private List<Variant> bakedVariants = new List<Variant>();
        [SerializeField] private float[] bakedWeights = Array.Empty<float>();
        [SerializeField] private int[] bakedTags = Array.Empty<int>();

        // Faces já rotacionadas: [variante * 6 + direção]
        [SerializeField] private FaceSocket[] bakedFaces = Array.Empty<FaceSocket>();

        // AdjacencyTable serializada. Guardamos como long[] porque o serializador da
        // Unity lida com long de forma previsível; convertemos para ulong ao carregar.
        [SerializeField] private long[] bakedAdjacency = Array.Empty<long>();
        [SerializeField] private int bakedWordCount;

        [SerializeField, TextArea(4, 20)] private string bakeReport;

        // ------------------------------------------------------------- consulta
        public int VariantCount => bakedVariants?.Count ?? 0;
        public bool IsBaked => VariantCount > 0 && bakedAdjacency != null && bakedAdjacency.Length > 0;
        public string BakeReport => bakeReport;

        public Variant GetVariant(int variant) => bakedVariants[variant];

        public ModuleDefinition GetModule(int variant)
        {
            int mi = bakedVariants[variant].moduleIndex;
            return (mi >= 0 && mi < modules.Count) ? modules[mi] : null;
        }

        public GameObject GetPrefab(int variant) => GetModule(variant)?.prefab;

        public int GetRotation(int variant) => bakedVariants[variant].rotationY;

        /// <summary>Rotação a aplicar na instanciação. Casa com a convenção de <see cref="Directions.RotateY"/>.</summary>
        public Quaternion GetRotationQuaternion(int variant)
            => Quaternion.Euler(0f, 90f * bakedVariants[variant].rotationY, 0f);

        public ModuleTag GetTags(int variant) => (ModuleTag)bakedTags[variant];

        public FaceSocket GetFace(int variant, Direction dir) => bakedFaces[variant * Directions.Count + (int)dir];

        /// <summary>Nome legível da variante, para logs e gizmos.</summary>
        public string GetVariantName(int variant)
        {
            ModuleDefinition m = GetModule(variant);
            string baseName = m != null ? m.name : "<módulo ausente>";
            int rot = GetRotation(variant);
            return rot == 0 ? baseName : $"{baseName}@{rot * 90}°";
        }

        // ------------------------------------------------------- para o solver
        /// <summary>Reconstrói a AdjacencyTable do bake. Aloca — chame uma vez por andar, não por frame.</summary>
        public AdjacencyTable CreateAdjacencyTable()
        {
            if (!IsBaked)
                throw new InvalidOperationException($"TileSet '{name}' não está bakeado. Use o botão \"Rebuild adjacency\".");

            var table = new AdjacencyTable(VariantCount);
            if (table.WordCount != bakedWordCount || table.Data.Length != bakedAdjacency.Length)
                throw new InvalidOperationException(
                    $"TileSet '{name}': bake inconsistente (esperado {table.Data.Length} palavras, " +
                    $"tem {bakedAdjacency.Length}). Rode \"Rebuild adjacency\" de novo.");

            for (int i = 0; i < bakedAdjacency.Length; i++)
                table.Data[i] = unchecked((ulong)bakedAdjacency[i]);

            return table;
        }

        public double[] CreateWeights()
        {
            var w = new double[VariantCount];
            for (int i = 0; i < w.Length; i++) w[i] = bakedWeights[i];
            return w;
        }

        // ------------------------------------------------------------ máscaras
        /// <summary>Máscara das variantes cuja face <paramref name="dir"/> usa o socket informado.</summary>
        public ulong[] MaskWithFaceSocket(Direction dir, string socketName)
            => ConstraintBuilder.MaskWhere(VariantCount, v => GetFace(v, dir).socket == socketName);

        /// <summary>Máscara das variantes que têm todas as tags exigidas e nenhuma das proibidas.</summary>
        public ulong[] MaskWithTags(ModuleTag required, ModuleTag forbidden)
            => ConstraintBuilder.MaskWhere(VariantCount, v =>
            {
                ModuleTag t = GetTags(v);
                if (required != ModuleTag.None && (t & required) != required) return false;
                if (forbidden != ModuleTag.None && (t & forbidden) != 0) return false;
                return true;
            });

        // =====================================================================
        //  BAKE
        // =====================================================================

        /// <summary>
        /// Expande as rotações em Y e gera a tabela de adjacência.
        /// Não usa nenhuma API de UnityEditor de propósito: quem chama (o inspector)
        /// é que marca o asset como sujo e salva.
        /// </summary>
        /// <returns><c>true</c> se o bake ficou utilizável (pode ter avisos mesmo assim).</returns>
        public bool Bake(out string report)
        {
            var log = new StringBuilder();
            var errors = new List<string>();
            var warnings = new List<string>();

            if (socketLibrary == null) errors.Add("SocketLibrary não atribuída.");
            if (modules == null || modules.Count == 0) errors.Add("Nenhum ModuleDefinition na lista.");

            if (errors.Count > 0)
            {
                report = FormatReport(log, errors, warnings);
                bakeReport = report;
                return false;
            }

            // ---- 1. Expandir variantes -------------------------------------
            var variants = new List<Variant>();
            for (int mi = 0; mi < modules.Count; mi++)
            {
                ModuleDefinition m = modules[mi];
                if (m == null) { warnings.Add($"Slot {mi} da lista de módulos está vazio — ignorado."); continue; }
                if (m.prefab == null) warnings.Add($"'{m.name}' não tem prefab (o solver funciona, mas nada é instanciado).");

                for (int rot = 0; rot < 4; rot++)
                    if (m.IsRotationAllowed(rot))
                        variants.Add(new Variant { moduleIndex = mi, rotationY = rot });
            }

            if (variants.Count == 0)
            {
                errors.Add("Nenhuma variante gerada — todos os módulos estão vazios ou sem rotação permitida.");
                report = FormatReport(log, errors, warnings);
                bakeReport = report;
                return false;
            }

            int vCount = variants.Count;

            // ---- 2. Faces rotacionadas -------------------------------------
            var faces = new FaceSocket[vCount * Directions.Count];
            for (int v = 0; v < vCount; v++)
            {
                ModuleDefinition m = modules[variants[v].moduleIndex];
                int rot = variants[v].rotationY;

                foreach (Direction dir in Directions.All)
                {
                    FaceSocket f = RotatedFace(m, rot, dir);
                    faces[v * Directions.Count + (int)dir] = f;

                    if (!f.IsDefined)
                        warnings.Add($"'{m.name}'@{rot * 90}° tem a face {dir} sem socket — ela nunca vai conectar com nada.");
                    else if (Directions.IsVertical(dir) && socketLibrary.FindVertical(f.socket) == null)
                        errors.Add($"'{m.name}' usa o socket vertical '{f.socket}' ({dir}), que não existe na SocketLibrary.");
                    else if (!Directions.IsVertical(dir) && socketLibrary.FindHorizontal(f.socket) == null)
                        errors.Add($"'{m.name}' usa o socket horizontal '{f.socket}' ({dir}), que não existe na SocketLibrary.");
                }
            }

            if (errors.Count > 0)
            {
                report = FormatReport(log, errors, warnings);
                bakeReport = report;
                return false;
            }

            // Rotações que produzem exatamente os mesmos sockets são desperdício:
            // dobram o custo do solver sem acrescentar variedade.
            for (int a = 0; a < vCount; a++)
            {
                for (int b = a + 1; b < vCount; b++)
                {
                    if (variants[a].moduleIndex != variants[b].moduleIndex) continue;
                    if (!SameFaces(faces, a, b)) continue;
                    ModuleDefinition m = modules[variants[a].moduleIndex];
                    warnings.Add($"'{m.name}': as rotações {variants[a].rotationY * 90}° e {variants[b].rotationY * 90}° " +
                                 "têm sockets idênticos. Se a geometria também for igual, desmarque uma delas.");
                }
            }

            // ---- 3. Adjacência ---------------------------------------------
            var table = new AdjacencyTable(vCount);
            long pairs = 0;

            for (int a = 0; a < vCount; a++)
            {
                foreach (Direction dir in Directions.All)
                {
                    Direction opp = Directions.Opposite(dir);
                    FaceSocket fa = faces[a * Directions.Count + (int)dir];

                    for (int b = 0; b < vCount; b++)
                    {
                        FaceSocket fb = faces[b * Directions.Count + (int)opp];
                        if (socketLibrary.AreCompatible(dir, fa, fb))
                        {
                            table.Allow(a, dir, b);
                            pairs++;
                        }
                    }
                }
            }

            if (!table.Validate(out string validationError))
                warnings.Add("Validação da tabela: " + validationError);

            // ---- 4. Serializar ---------------------------------------------
            bakedVariants = variants;
            bakedWeights = new float[vCount];
            bakedTags = new int[vCount];
            for (int v = 0; v < vCount; v++)
            {
                ModuleDefinition m = modules[variants[v].moduleIndex];
                bakedWeights[v] = Mathf.Max(0.0001f, m.weight);
                bakedTags[v] = (int)m.tags;
            }

            bakedFaces = faces;
            bakedWordCount = table.WordCount;
            bakedAdjacency = new long[table.Data.Length];
            for (int i = 0; i < table.Data.Length; i++)
                bakedAdjacency[i] = unchecked((long)table.Data[i]);

            log.AppendLine($"Módulos: {modules.Count}   →   Variantes: {vCount}");
            log.AppendLine($"Palavras por bitset: {bakedWordCount} (cabem {bakedWordCount * 64} módulos)");
            log.AppendLine($"Pares de adjacência permitidos: {pairs}");
            log.AppendLine();
            log.AppendLine("Vizinhos aceitos por variante e direção:");
            for (int v = 0; v < vCount; v++)
            {
                var line = new StringBuilder("  " + GetVariantNameFrom(variants[v]) + "  ");
                foreach (Direction dir in Directions.All)
                    line.Append($"{dir}:{table.AllowedCount(v, dir)}  ");
                log.AppendLine(line.ToString());
            }

            report = FormatReport(log, errors, warnings);
            bakeReport = report;
            return true;
        }

        /// <summary>
        /// Socket da face <paramref name="dir"/> depois de girar a peça em Y.
        ///
        /// A face que TERMINA apontando para `dir` é a que ESTAVA em RotateY(dir, -rot)
        /// antes de girar — daí o sinal negativo. Sockets verticais não trocam de face,
        /// mas o índice de rotação deles acompanha o giro.
        /// </summary>
        public static FaceSocket RotatedFace(ModuleDefinition module, int rot, Direction dir)
        {
            if (Directions.IsVertical(dir))
            {
                FaceSocket f = module.GetFace(dir);
                f.verticalRotation = (f.verticalRotation + rot) & 3;
                return f;
            }

            return module.GetFace(Directions.RotateY(dir, -rot));
        }

        private static bool SameFaces(FaceSocket[] faces, int a, int b)
        {
            for (int d = 0; d < Directions.Count; d++)
            {
                FaceSocket fa = faces[a * Directions.Count + d];
                FaceSocket fb = faces[b * Directions.Count + d];
                if (fa.socket != fb.socket || fa.flipped != fb.flipped || fa.verticalRotation != fb.verticalRotation)
                    return false;
            }
            return true;
        }

        private string GetVariantNameFrom(Variant v)
        {
            ModuleDefinition m = (v.moduleIndex >= 0 && v.moduleIndex < modules.Count) ? modules[v.moduleIndex] : null;
            string n = m != null ? m.name : "<?>";
            return v.rotationY == 0 ? n : $"{n}@{v.rotationY * 90}°";
        }

        private static string FormatReport(StringBuilder log, List<string> errors, List<string> warnings)
        {
            var sb = new StringBuilder();
            sb.AppendLine(errors.Count == 0 ? "BAKE OK" : $"BAKE FALHOU ({errors.Count} erro(s))");
            sb.AppendLine();

            if (errors.Count > 0)
            {
                sb.AppendLine("ERROS:");
                foreach (string e in errors) sb.AppendLine("  • " + e);
                sb.AppendLine();
            }

            if (warnings.Count > 0)
            {
                sb.AppendLine("AVISOS:");
                foreach (string w in warnings) sb.AppendLine("  • " + w);
                sb.AppendLine();
            }

            sb.Append(log);
            return sb.ToString();
        }
    }
}
