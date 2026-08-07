// ============================================================================
//  ModuleTag.cs  —  camada WFC.Data
//
//  Tags classificam a PEÇA (o que ela é geometricamente).
//  Quem classifica a CÉLULA é o RoomRole (o que ela significa no andar).
//  O FloorSpec é a ponte entre os dois: "célula com Role_Escada só aceita peças
//  com tag Stairs".
//
//  Fora do escopo da v1 de propósito:
//   • tags de gameplay (Combat, Treasure) — isso é RoomRole;
//   • tags decorativas finas (tocha, tapete) — isso é população (Decisão 4).
// ============================================================================

namespace WFC.Data
{
    [System.Flags]
    public enum ModuleTag
    {
        None     = 0,
        Blocking = 1 << 0,   // bloqueia nav/conectividade (ausência = passável)
        Floor    = 1 << 1,
        Wall     = 1 << 2,
        Doorway  = 1 << 3,
        Corner   = 1 << 4,
        Entrance = 1 << 5,
        Stairs   = 1 << 6,
        Empty    = 1 << 7,
        Wildcard = 1 << 8,   // marca peças válvula-de-escape (SolidFill/Air), p/ debug
        Column   = 1 << 9,   // pilar decorativo; também serve de "filler" de quina — ver
                              // TileSet.FindPrefabWithTag e TileInstancer.PlaceCornerFillers
    }
}
