using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class EnemySystem : ComponentSystem {
    public float tickrate = .1f;
    public float lastTick = -1;
    //public NativeArray<bool> grid;
    int mazeWidth;
    public float3[] orientations = new float3[] { new float3(0, 0, 1), new float3(1, 0, 0), new float3(0, 0, -1), new float3(-1, 0, 0) };
    int enemySize;

    void StartSystem() {
        SetupEnemies.startPositions.Clear();
        List<Translation> entities = new List<Translation>();
        Entities.ForEach((ref Translation translation, ref Wall wall) => {
            entities.Add(translation);
        });
        EntityManager entityManager = World.Active.EntityManager;
        EntityArchetype enemyArchetype = entityManager.CreateArchetype(
           typeof(Translation),
           typeof(RenderMesh),
           typeof(LocalToWorld),
           typeof(Enemy)
           );
        while (SetupEnemies.startPositions.Count < enemySize) {
            float3 pos = new float3(UnityEngine.Random.Range(10, mazeWidth - 10), 0, UnityEngine.Random.Range(10, mazeWidth - 10));
            if (!RecursiveBacktracking.hashGrid.ContainsKey((int)(pos.z) * mazeWidth + (int)(pos.x))) {
                SetupEnemies.startPositions.Add(pos);
                Entity entity = entityManager.CreateEntity(enemyArchetype);
                entityManager.SetComponentData(entity, new Translation { Value = pos });
                entityManager.SetSharedComponentData(entity, new RenderMesh { mesh = SetupEnemies.enemyMesh, material = SetupEnemies.enemyMaterial });
                entityManager.SetComponentData(entity, new Enemy { isForward = true, forward = new float3(0, 0, 1), targetPosition = pos });
            }
        }
    }
    protected override void OnUpdate() {
        Entities.ForEach((Entity e, ref EnemyControl ec) => {
            if (!ec.initialized) {
                Entities.ForEach((ref Communicator com) => {
                    mazeWidth = com.width;
                    enemySize = com.numberOfEnemies;
                });
                StartSystem();
                EntityManager.SetComponentData(e, new EnemyControl { run = true, initialized = true });
            }
            if (ec.run) {
                if (lastTick == -1) {
                    lastTick = Time.time;
                }
                if (Time.time - lastTick >= tickrate) {
                    Tick();
                    lastTick = Time.time;
                }
                Job job = new Job {
                    delta = Time.deltaTime,
                };
                JobHandle j = job.Schedule(this);
                j.Complete();
            }
        });
    }
    void Tick() {
        NativeArray<float3> orients = new NativeArray<float3>(4, Allocator.TempJob);
        NativeArray<int> trueResults = new NativeArray<int>(4, Allocator.TempJob);
        for (int i = 0; i < 4; i++) {
            orients[i] = orientations[i];
        }
        PathWalker pathWalker = new PathWalker {
            delta = Time.deltaTime,
            grid = RecursiveBacktracking.hashGrid,
            mazeWidth = mazeWidth,
            orientations = orients,
            trueResults = trueResults,
            seed = (uint)math.abs(Time.time * 23423)
        };
        JobHandle pathWalkerJobHandle = pathWalker.Schedule(this);
        pathWalkerJobHandle.Complete();
        orients.Dispose();
        trueResults.Dispose();
    }

    [BurstCompile]
    private struct PathWalker : IJobForEachWithEntity<Enemy> {
        [ReadOnly] public float delta;
        [ReadOnly] public NativeHashMap<int, bool> grid;
        [ReadOnly] public int mazeWidth;
        [ReadOnly] public NativeArray<float3> orientations;
        [NativeDisableParallelForRestriction] public NativeArray<int> trueResults;
        [ReadOnly] public uint seed;

        public void Execute(Entity entity, int index, ref Enemy enemy) {
            //translation.Value = new float3(math.lerp(translation.Value.x, positionArray[index].x, delta * 20), translation.Value.y, math.lerp(translation.Value.z, positionArray[index].z, delta * 20));
            Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
            int k = (int)(enemy.targetPosition.z + enemy.forward.z) * mazeWidth + (int)(enemy.targetPosition.x + enemy.forward.x);
            if (!grid.ContainsKey(k)) {
                enemy.targetPosition = enemy.targetPosition + enemy.forward;

            } else {
                NativeArray<bool> results = new NativeArray<bool>(4, Allocator.Temp);
                results[0] = true;
                results[1] = true;
                results[2] = true;
                results[3] = true;
                NativeArray<float2> dirs = new NativeArray<float2>(4, Allocator.Temp);
                dirs[0] = new float2(enemy.targetPosition.x, enemy.targetPosition.z + 1);
                dirs[1] = new float2(enemy.targetPosition.x + 1, enemy.targetPosition.z);
                dirs[2] = new float2(enemy.targetPosition.x, enemy.targetPosition.z - 1);
                dirs[3] = new float2(enemy.targetPosition.x - 1, enemy.targetPosition.z);
                for (int i = 0; i < 4; i++) {
                    int j = (int)(dirs[i].y) * mazeWidth + (int)(dirs[i].x);
                    if (grid.ContainsKey(j)) {
                        results[i] = false;
                    }
                }
                int n = 0;
                for (int i = 0; i < results.Length; i++) if (results[i]) trueResults[n++] = i;
                int r = trueResults[random.NextInt(0, n)];
                enemy.forward = new float3(orientations[r].x, 0, orientations[r].z);
            }
        }
    }

    [BurstCompile]
    public struct CanMoveToJob : IJobParallelFor {
        [NativeDisableParallelForRestriction] public NativeArray<bool> positionArray;
        public int x, y, z;
        [NativeDisableParallelForRestriction] public NativeArray<bool> result;
        public void Execute(int index) {
            if (z * y + x < positionArray.Length) {
                if (positionArray[z * y + x]) {
                    result[0] = false;
                }
            }
        }
    }
    [BurstCompile]
    public struct PossibleWays : IJobParallelFor {
        public NativeArray<float3> positionArray;
        [NativeDisableParallelForRestriction] public NativeArray<float2> possibilities;
        [NativeDisableParallelForRestriction] public NativeArray<bool> res;
        public void Execute(int index) {
            for (int i = 0; i < 4; i++) {
                if (positionArray[index].x == possibilities[i].x && positionArray[index].z == possibilities[i].y) {
                    res[i] = false;
                }
            }
        }
    }

    [BurstCompile]
    private struct Job : IJobForEachWithEntity<Translation, Enemy> {
        [ReadOnly] public float delta;
        public void Execute(Entity entity, int index, ref Translation translation, ref Enemy enemy) {
            translation.Value = new float3(math.lerp(translation.Value.x, enemy.targetPosition.x, delta * 20), translation.Value.y, math.lerp(translation.Value.z, enemy.targetPosition.z, delta * 20));

        }
    }

}


