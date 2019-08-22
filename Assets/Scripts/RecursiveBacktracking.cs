using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

public class RecursiveBacktracking : MonoBehaviour {
    public int width, height;
    public int numberOfEnemies;
    Cell[,] cells;
    public List<Cell> visitable = new List<Cell>();
    public Stack<Cell> visited = new Stack<Cell>();
    public List<Cell> path = new List<Cell>();
    public List<Vector3> p = new List<Vector3>();
    int visitX = 1, visitY = 1;
    static bool work = true;
    EntityManager entityManager;
    EntityArchetype entityArchetype;
    public Mesh mesh;
    public Material mat;
    static bool firstCall = true;
    public static NativeHashMap<int, bool> hashGrid;
    void Start() {
        width = height = PlayerPrefs.GetInt("Size") / 2;
        numberOfEnemies = PlayerPrefs.GetInt("Enemies");
        entityManager = World.Active.EntityManager;
        entityArchetype = entityManager.CreateArchetype(
           typeof(Translation),
           typeof(RenderMesh),
           typeof(LocalToWorld),
           typeof(NonUniformScale),
           typeof(Wall)
           );
        cells = new Cell[width, height];
        for (int i = 0; i < width; i++) {
            for (int j = 0; j < height; j++) {
                cells[i, j] = new Cell();
                cells[i, j].position = new Vector3(i, 0, j);
            }
        }
    }

    void Visit(int x, int y) {
        if (!cells[x, y].isVisited) {
            path.Add(cells[x, y]);
            cells[x, y].isVisited = true;
            visited.Push(cells[x, y]);
            p.Add(new Vector3(x, 0, y));
        }

        //find visitable cells 
        
        
        visitable.Clear();
        //i jump in 2s otherwise I would be generating an expensive rectangle
        if (y + 2 < height) {
            if (!cells[x, y + 2].isVisited) { visitable.Add(cells[x, y + 2]); }
        }
        if (x + 2 < width) {
            if (!cells[x + 2, y].isVisited) { visitable.Add(cells[x + 2, y]); }
        }
        if (y - 2 >= 0) {
            if (!cells[x, y - 2].isVisited) { visitable.Add(cells[x, y - 2]); }
        }
        if (x - 2 >= 0) {
            if (!cells[x - 2, y].isVisited) { visitable.Add(cells[x - 2, y]); }

        }

        //if there is a visitable cell
        if (visitable.Count > 0) {
            //pick random cell
            Cell c = visitable[UnityEngine.Random.Range(0, visitable.Count)];
            p.Add(new Vector3(x, 0, y) + (new Vector3(x, 0, y) - c.position).normalized);
            //set coordinate of next cell to visit
            visitX = (int)c.position.x;
            visitY = (int)c.position.z;
        } else {
            //no visitable cells... time to backtrack
            if (visited.Count > 0) {
                Cell c = visited.Pop();
                //Visit((int)c.position.x, (int)c.position.z);
                visitX = (int)c.position.x;
                visitY = (int)c.position.z;
            } else {
                if (work) {
                    //we're done bois
                    CreateMaze();
                }
                work = false;
            }
        }

    }


    void CreateMaze() {
        //allocate space for the entities
        NativeArray<Entity> entities = new NativeArray<Entity>(p.Count, Allocator.Temp);
        //create them all at once this is much faster like this
        entityManager.CreateEntity(entityArchetype, entities);
        //create a hashgrid where each wall is added with its position so I won't have to iterate over them huge timesaver
        //initially I had an array but it would have a lot of empty space
        hashGrid = new NativeHashMap<int, bool>(entities.Length, Allocator.Persistent);
        ///set the required components
        for (int i = 0; i < entities.Length; i++) {
            Entity entity = entities[i];
            entityManager.SetComponentData(entity, new Translation { Value = new float3(p[i].x, p[i].y + 0.5f, p[i].z) });
            entityManager.SetComponentData(entity, new NonUniformScale { Value = new float3(1, 2, 1) });
            entityManager.SetSharedComponentData(entity, new RenderMesh { mesh = mesh, material = mat, castShadows = UnityEngine.Rendering.ShadowCastingMode.Off });
            //index it like a 2d array this is what 2d arrays do internally anyways afaik
            hashGrid.TryAdd((int)p[i].z * width + (int)p[i].x, true);
        }
        entities.Dispose();
        //ALWAYS DISPOSE
        //I don't dispose of the hashgrid and the leak detection is bitching about that even tho its persistent but I need it for the lifetime of the maze so what.
        //create ground
        Entity ground = entityManager.CreateEntity(entityArchetype);
        entityManager.SetComponentData(ground, new Translation { Value = new float3(width / 2, -1, height / 2) });
        entityManager.SetSharedComponentData(ground, new RenderMesh { mesh = mesh, material = mat });
        entityManager.SetComponentData(ground, new NonUniformScale { Value = new float3(width, 1, height) });
        //create the communicator, i use it to share some infos between the systems
        var Communicator = entityManager.CreateArchetype(
            typeof(Communicator),
            typeof(Translation)
            );
        Entity comEntity = entityManager.CreateEntity(Communicator);
        entityManager.SetComponentData(comEntity, new Translation { Value = new float3(0, 0, 0) });
        entityManager.SetComponentData(comEntity, new Communicator { generationFinished = true, numberOfWalls = p.Count, numberOfEnemies = numberOfEnemies, width = width, height = height, random = UnityEngine.Random.Range(0, 100) });
        //creatthe enemycontrol I use this and the playerControl to give states to the system, without them i wouldnt be able to restart the scene properly
        var EnemyControl = entityManager.CreateArchetype(
            typeof(EnemyControl),
            typeof(Translation)
            );
        Entity enemyEntity = entityManager.CreateEntity(EnemyControl);
        entityManager.SetComponentData(enemyEntity, new Translation { Value = new float3(0, 0, 0) });
        entityManager.SetComponentData(enemyEntity, new EnemyControl { run = true, initialized = false });
        var PlayerControl = entityManager.CreateArchetype(
            typeof(PlayerControl),
            typeof(Translation)
            );
        Entity playerEntity = entityManager.CreateEntity(PlayerControl);
        entityManager.SetComponentData(playerEntity, new Translation { Value = new float3(0, 0, 0) });
        entityManager.SetComponentData(playerEntity, new PlayerControl { run = true, initialized = false});

    }

    public static void StaticReset() {
        hashGrid.Dispose();
        work = true;
        firstCall = true;
    }
    void Update() {
        //I decided I don't want real recursion, as with a high number of recursions its hard to keep track of whats happening, and this way I can also ease out the frames
        if (work) {
            for (int i = 0; i < 1000; i++) {
                if (firstCall) {
                    int visitX = 1, visitY = 1;
                    Start();
                    path.Clear();
                    p.Clear();
                    Visit(visitX, visitY);
                    firstCall = false;
                } else {
                    Visit(visitX, visitY);

                }
            }

        }

    }

}
[System.Serializable]
public class Cell {
    public Vector3 position;
    public bool isVisited = false;
}
public struct Wall : IComponentData { }
public struct Communicator : IComponentData {
    public bool generationFinished;
    public int numberOfWalls;
    public int numberOfEnemies;
    public int width, height;
    public int random;
}
public struct EnemyControl : IComponentData {
    public bool run;
    public bool initialized;
}
public struct PlayerControl : IComponentData {
    public bool run;
    public bool initialized;
}
