// ============================================================================
//  GreyboxFixture.cs  —  apoio dos testes
//
//  Monta EM MEMÓRIA o mesmo tileset que o WFCTilesetBootstrap gera em disco,
//  sem depender de nenhum asset. Assim os testes rodam num clone recém-clonado
//  do repositório, antes de qualquer um ter clicado no bootstrap.
//
//  Se você mudar a tabela de sockets no bootstrap, mude aqui também — é de
//  propósito que sejam dois lugares: o teste tem que ter uma cópia independente
//  daquilo que ele afirma, senão não está verificando nada.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.Tests
{
    public sealed class GreyboxFixture
    {
        private readonly List<Object> _created = new List<Object>();

        public const string Open = "open";
        public const string Wall = "wall";
        public const string Door = "door";
        public const string Ground = "ground";
        public const string Sky = "sky";

        /// <summary>Quantas variantes o bake do tileset greybox deve produzir.</summary>
        public const int ExpectedVariantCount = 29;

        public static readonly bool[] Rot0 = { true, false, false, false };
        public static readonly bool[] Rot01 = { true, true, false, false };
        public static readonly bool[] RotAll = { true, true, true, true };

        public T New<T>(string nome) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = nome;
            _created.Add(asset);
            return asset;
        }

        public void Cleanup()
        {
            foreach (Object o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        public SocketLibrary Library()
        {
            var lib = New<SocketLibrary>("SocketLibrary");
            lib.UpsertHorizontal(Open, true, Color.green);
            lib.UpsertHorizontal(Wall, true, Color.gray);
            lib.UpsertHorizontal(Door, true, Color.yellow);
            lib.UpsertVertical(Ground, true, Color.red);
            lib.UpsertVertical(Sky, true, Color.blue);
            return lib;
        }

        public ModuleDefinition Module(string nome, string posX, string negX, string posZ, string negZ,
                                       float peso, ModuleTag tags, bool[] rots)
        {
            var m = New<ModuleDefinition>(nome);
            m.SetFace(Direction.PosX, new FaceSocket(posX));
            m.SetFace(Direction.NegX, new FaceSocket(negX));
            m.SetFace(Direction.PosZ, new FaceSocket(posZ));
            m.SetFace(Direction.NegZ, new FaceSocket(negZ));
            m.SetFace(Direction.PosY, new FaceSocket(Sky));
            m.SetFace(Direction.NegY, new FaceSocket(Ground));
            m.weight = peso;
            m.tags = tags;
            m.allowedYRotations = rots;
            return m;
        }

        /// <summary>Réplica do tileset greybox. Não bakeado — chame <see cref="Baked"/> se quiser pronto.</summary>
        public TileSet TileSet()
        {
            var ts = New<TileSet>("TileSet_Greybox");
            ts.socketLibrary = Library();
            ts.modules = new List<ModuleDefinition>
            {
                Module("Floor_Open",    Open, Open, Open, Open, 3.00f, ModuleTag.Floor, Rot0),
                Module("Wall",          Open, Open, Wall, Open, 2.00f, ModuleTag.Floor | ModuleTag.Wall, RotAll),
                Module("Corner",        Open, Wall, Wall, Open, 1.20f, ModuleTag.Floor | ModuleTag.Wall | ModuleTag.Corner, RotAll),
                Module("Corridor",      Open, Open, Wall, Wall, 1.20f, ModuleTag.Floor | ModuleTag.Wall, Rot01),
                Module("DeadEnd",       Wall, Wall, Wall, Open, 0.40f, ModuleTag.Floor | ModuleTag.Wall, RotAll),
                Module("Door",          Open, Open, Door, Open, 0.35f, ModuleTag.Floor | ModuleTag.Doorway, RotAll),
                Module("Door_Corridor", Wall, Wall, Door, Open, 0.20f, ModuleTag.Floor | ModuleTag.Doorway | ModuleTag.Wall, RotAll),
                Module("Stairs",        Wall, Wall, Wall, Open, 0.08f, ModuleTag.Floor | ModuleTag.Stairs, RotAll),
                Module("SolidFill",     Wall, Wall, Wall, Wall, 0.10f, ModuleTag.Blocking | ModuleTag.Wildcard, Rot0),
                Module("Air",           Wall, Wall, Wall, Wall, 0.10f, ModuleTag.Empty | ModuleTag.Blocking | ModuleTag.Wildcard, Rot0),
            };
            return ts;
        }

        public TileSet Baked()
        {
            TileSet ts = TileSet();
            if (!ts.Bake(out string report))
                throw new System.InvalidOperationException("Bake do fixture falhou:\n" + report);
            return ts;
        }

        /// <summary>FloorSpec de teste, com as mesmas regras de papel que o bootstrap grava.</summary>
        public FloorSpec Spec(TileSet tileSet, Vector3Int gridSize)
        {
            var spec = New<FloorSpec>("FloorSpec_Test");
            spec.tileSet = tileSet;
            spec.gridSize = gridSize;
            spec.cellSize = 6f;
            spec.cellHeight = 5f;
            spec.randomSeed = false;
            spec.sealBorders = true;
            spec.borderSocket = Wall;
            spec.maxAttempts = 12;
            spec.skeleton = new SkeletonSettings();
            spec.roleRules = new List<FloorSpec.RoleRule>
            {
                new FloorSpec.RoleRule { role = RoomRole.Escada,   requiredTags = ModuleTag.Stairs },
                new FloorSpec.RoleRule { role = RoomRole.Entrada,  requiredTags = ModuleTag.Floor, forbiddenTags = ModuleTag.Wildcard | ModuleTag.Stairs },
                new FloorSpec.RoleRule { role = RoomRole.Combate,  requiredTags = ModuleTag.Floor, forbiddenTags = ModuleTag.Wildcard | ModuleTag.Stairs },
                new FloorSpec.RoleRule { role = RoomRole.Corredor, requiredTags = ModuleTag.Floor, forbiddenTags = ModuleTag.Stairs },
                new FloorSpec.RoleRule { role = RoomRole.Vazio,    requiredTags = ModuleTag.Wildcard },
            };
            return spec;
        }
    }
}
