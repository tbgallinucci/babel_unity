// ============================================================================
//  FaceSocket.cs  —  camada WFC.Data
//
//  O socket de UMA das 6 faces de um módulo. É um struct serializável simples —
//  a semântica de "conecta com quem" mora na SocketLibrary, não aqui.
// ============================================================================

using System;
using UnityEngine;

namespace WFC.Data
{
    [Serializable]
    public struct FaceSocket
    {
        [Tooltip("Nome do socket, declarado na SocketLibrary do TileSet.")]
        public string socket;

        [Tooltip("Só para sockets horizontais ASSIMÉTRICOS: marca esta face como a versão espelhada " +
                 "(o clássico \"3F\"). Ignorado em sockets simétricos.")]
        public bool flipped;

        [Tooltip("Só para sockets verticais NÃO invariantes: índice de rotação 0..3 que precisa bater " +
                 "com o da peça vizinha. Ignorado em sockets invariantes.")]
        [Range(0, 3)]
        public int verticalRotation;

        public FaceSocket(string socket, bool flipped = false, int verticalRotation = 0)
        {
            this.socket = socket;
            this.flipped = flipped;
            this.verticalRotation = verticalRotation;
        }

        public bool IsDefined => !string.IsNullOrEmpty(socket);

        public override string ToString()
        {
            if (!IsDefined) return "<vazio>";
            if (flipped) return socket + " (flip)";
            if (verticalRotation != 0) return $"{socket} (rot {verticalRotation})";
            return socket;
        }
    }
}
