// ============================================================================
//  SocketLibrary.cs  —  camada WFC.Data
//
//  O vocabulário de sockets do tileset. Duas células vizinhas só coexistem se
//  as faces que se tocam forem compatíveis — e é AQUI que "compatível" é
//  definido.
//
//  CONVENÇÃO (padrão Oskar Stålberg / DeBroglie), mas sem string mágica:
//
//   • Horizontais (X/Z)
//       - simétrico   → conecta consigo mesmo. (ex.: "open", "wall")
//       - assimétrico → conecta com a versão ESPELHADA de si mesmo, nunca
//                       consigo. É o clássico "3" ↔ "3F": no asset, em vez de
//                       digitar o sufixo F, você marca o checkbox `flipped` na
//                       face. Serve para peças cuja geometria é diferente da
//                       esquerda para a direita naquela face (ex.: um degrau
//                       que sobe só de um lado).
//
//   • Verticais (Y)
//       - invariante  → conecta com qualquer rotação. (ex.: "ground", "sky")
//       - com índice  → só conecta se o índice de rotação bater, porque a peça
//                       de cima precisa estar girada igual à de baixo.
//
//  O tileset greybox usa SÓ sockets simétricos e verticais invariantes: nenhuma
//  face dele é assimétrica de verdade. O suporte a flipped/rotacionado existe
//  porque kits de arte de verdade precisam — não porque o greybox precise.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using WFC.Core;

namespace WFC.Data
{
    [CreateAssetMenu(fileName = "SocketLibrary", menuName = "WFC/Socket Library", order = 0)]
    public sealed class SocketLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class HorizontalSocket
        {
            [Tooltip("Nome usado nas faces dos ModuleDefinition. Diferencia maiúsculas.")]
            public string name = "novo";

            [Tooltip("Simétrico conecta consigo mesmo. Assimétrico só conecta com a face espelhada (flipped).")]
            public bool symmetric = true;

            [Tooltip("Só para os gizmos e o inspector.")]
            public Color debugColor = Color.white;
        }

        [Serializable]
        public sealed class VerticalSocket
        {
            public string name = "novo";

            [Tooltip("Invariante conecta em qualquer rotação. Caso contrário o índice de rotação precisa bater.")]
            public bool rotationInvariant = true;

            public Color debugColor = Color.white;
        }

        [SerializeField] private List<HorizontalSocket> horizontal = new List<HorizontalSocket>();
        [SerializeField] private List<VerticalSocket> vertical = new List<VerticalSocket>();

        public IReadOnlyList<HorizontalSocket> Horizontal => horizontal;
        public IReadOnlyList<VerticalSocket> Vertical => vertical;

        // ------------------------------------------------------------- lookup
        public HorizontalSocket FindHorizontal(string socketName)
        {
            if (string.IsNullOrEmpty(socketName)) return null;
            for (int i = 0; i < horizontal.Count; i++)
                if (horizontal[i] != null && horizontal[i].name == socketName) return horizontal[i];
            return null;
        }

        public VerticalSocket FindVertical(string socketName)
        {
            if (string.IsNullOrEmpty(socketName)) return null;
            for (int i = 0; i < vertical.Count; i++)
                if (vertical[i] != null && vertical[i].name == socketName) return vertical[i];
            return null;
        }

        /// <summary>Cor da face para os gizmos/inspector. Magenta = socket não declarado na biblioteca.</summary>
        public Color GetDebugColor(Direction dir, string socketName)
        {
            if (Directions.IsVertical(dir))
            {
                var v = FindVertical(socketName);
                return v != null ? v.debugColor : Color.magenta;
            }

            var h = FindHorizontal(socketName);
            return h != null ? h.debugColor : Color.magenta;
        }

        // --------------------------------------------------------- compatibilidade
        /// <summary>
        /// As faces que se tocam podem coexistir?
        /// <paramref name="a"/> é a face do módulo A na direção <paramref name="dir"/>;
        /// <paramref name="b"/> é a face do módulo B na direção OPOSTA.
        /// </summary>
        public bool AreCompatible(Direction dir, in FaceSocket a, in FaceSocket b)
        {
            if (string.IsNullOrEmpty(a.socket) || string.IsNullOrEmpty(b.socket)) return false;
            if (a.socket != b.socket) return false;

            if (Directions.IsVertical(dir))
            {
                VerticalSocket def = FindVertical(a.socket);
                if (def == null) return false;                       // socket não declarado
                if (def.rotationInvariant) return true;
                return ((a.verticalRotation & 3) == (b.verticalRotation & 3));
            }

            HorizontalSocket hdef = FindHorizontal(a.socket);
            if (hdef == null) return false;                          // socket não declarado
            if (hdef.symmetric) return true;
            return a.flipped != b.flipped;                           // assimétrico: só com o espelho
        }

        // ------------------------------------------------------------- autoria
        /// <summary>Cria (ou atualiza) um socket horizontal. Usado pelo script de bootstrap.</summary>
        public HorizontalSocket UpsertHorizontal(string socketName, bool isSymmetric, Color color)
        {
            HorizontalSocket s = FindHorizontal(socketName);
            if (s == null)
            {
                s = new HorizontalSocket { name = socketName };
                horizontal.Add(s);
            }
            s.symmetric = isSymmetric;
            s.debugColor = color;
            return s;
        }

        /// <summary>Cria (ou atualiza) um socket vertical. Usado pelo script de bootstrap.</summary>
        public VerticalSocket UpsertVertical(string socketName, bool isRotationInvariant, Color color)
        {
            VerticalSocket s = FindVertical(socketName);
            if (s == null)
            {
                s = new VerticalSocket { name = socketName };
                vertical.Add(s);
            }
            s.rotationInvariant = isRotationInvariant;
            s.debugColor = color;
            return s;
        }

        public void ClearAll()
        {
            horizontal.Clear();
            vertical.Clear();
        }
    }
}
