using System;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;

namespace RenderMeshInstancedWithComputeShader {
    public class GameOfLifeRenderMeshInstancedWithComputeShader : MonoBehaviour {
        [Header("Left Mouse Button: set cell alive")]
        [Header("Right Mouse Button: set cell dead")]
        [Header("Space: start simulation")]
        [SerializeField] private Mesh cellMesh;
        [SerializeField] private Material cellMaterial;
        [SerializeField] private GridProperties gridProperties;
        [SerializeField] private SimulationProperties simulationProperties;

        private Matrix4x4[] _matrices;
        private MaterialPropertyBlock _propertyBlock;
        private Vector4[] _colors;
        private CellState[] _states;
        private Vector3[] _worldPositions;
        private CellProperties[] _cellProperties;

        private static readonly int c_color_hash = Shader.PropertyToID("_BaseColor");

        private Vector3 _positionCache = new Vector3(0, 0, 0);
        private SimulationInputModule _inputModule = new SimulationInputModule();
        private Camera _camera;
        private bool _simulationStarted;
        private float _t = 0;
        private int _iteration = 0;

        private Vector4[] _batchColors;
        private Matrix4x4[] _batchMatrices;

        private int _chunksCount;
        private NativeArray<CellState> _currentStates;
        private NativeArray<CellState> _nextStates;
        private NativeArray<int> _neighborCounts;
        private NativeArray<int> _neighborOffsets;

        private void Awake() {
            _chunksCount = SystemInfo.processorCount;
            _camera = Camera.main;
            _propertyBlock = new MaterialPropertyBlock();
            _batchColors = new Vector4[1023];
            _batchMatrices = new Matrix4x4[1023];

            _neighborOffsets = new NativeArray<int>(8, Allocator.Persistent);

            InitializeGrid(in gridProperties);

            int totalCells = gridProperties.width * gridProperties.height;
            _currentStates = new NativeArray<CellState>(totalCells, Allocator.Persistent);
            _nextStates = new NativeArray<CellState>(totalCells, Allocator.Persistent);
            _neighborCounts = new NativeArray<int>(totalCells, Allocator.Persistent);

            for (int i = 0; i < totalCells; i++) {
                _currentStates[i] = _states[i];
            }
        }

        private void OnDestroy() {
            if (_currentStates.IsCreated) _currentStates.Dispose();
            if (_nextStates.IsCreated) _nextStates.Dispose();
            if (_neighborCounts.IsCreated) _neighborCounts.Dispose();
            if (_neighborOffsets.IsCreated) _neighborOffsets.Dispose();
        }

        private void InitializeGrid(in GridProperties gridProperties) {
            int count = gridProperties.height * gridProperties.width;
            _matrices = new Matrix4x4[count];
            _colors = new Vector4[count];
            _states = new CellState[count];
            _worldPositions = new Vector3[count];
            _cellProperties = new CellProperties[count];
            _camera.orthographicSize = gridProperties.height / 2f;
            _camera.transform.position = new Vector3(gridProperties.width / 2f, gridProperties.height / 2f, _camera.transform.position.z);

            for (int i = 0; i < gridProperties.height; i++) {
                for (int j = 0; j < gridProperties.width; j++) {
                    int id = GetCellID(i, j, gridProperties.height, gridProperties.width);
                    _positionCache.Set(j * gridProperties.offset, i * gridProperties.offset, 0);

                    _matrices[id] = Matrix4x4.TRS(
                        _positionCache,
                        Quaternion.identity,
                        Vector3.one
                    );

                    _worldPositions[id] = _positionCache;
                    _states[id] = CellState.Death;
                    _cellProperties[id].neighborCount = 0;
                    _colors[id] = Color.black;
                }
            }

            Debug.Log($"Initializing grid: {gridProperties.width}x{gridProperties.height}, offset: {gridProperties.offset}, chunksCount: {_chunksCount}");
        }

        private void Update() {
            if (!_simulationStarted) {
                var input = _inputModule.Update();

                if (input.spaceKeyDown) {
                    Debug.Log("Simulation started.");
                    _simulationStarted = true;
                    return;
                }

                if (input.mouseClicked) {
                    if (input.mouseKey == MouseKey.Left) {
                        var mousePos = _camera.ScreenToWorldPoint(input.screenPos);
                        mousePos = new Vector3(mousePos.x, mousePos.y, 0);
                        for (int i = 0; i < _worldPositions.Length; i++) {
                            var pos = _worldPositions[i];
                            if (RectangleCheck(mousePos, pos, gridProperties.offset)) {
                                int id = i;
                                TrySetState(id, CellState.Alive);
                                break;
                            }
                        }
                    }
                    else {
                        var mousePos = _camera.ScreenToWorldPoint(input.screenPos);
                        mousePos = new Vector3(mousePos.x, mousePos.y, 0);
                        for (int i = 0; i < _worldPositions.Length; i++) {
                            var pos = _worldPositions[i];
                            if (RectangleCheck(mousePos, pos, gridProperties.offset)) {
                                int id = i;
                                TrySetState(id, CellState.Death);
                                break;
                            }
                        }
                    }
                }
            }
            else {
                _t += Time.deltaTime;
                if (_t > simulationProperties.advanceDelay) {
                    _t = 0;
                    Simulate();
                }
            }

            RenderInstances();
        }

        [BurstCompile]
        private struct CountNeighborsJob : IJobParallelFor {
            [ReadOnly] public NativeArray<CellState> states;
            [ReadOnly] public NativeArray<int> neighborOffsets;
            public NativeArray<int> neighborCounts;
            public int width;
            public int height;

            public void Execute(int index) {
                int count = 0;
                int row = index / width;
                int col = index % width;

                for (int i = -1; i <= 1; i++) {
                    for (int j = -1; j <= 1; j++) {
                        if (i == 0 && j == 0) continue;

                        int newRow = row + i;
                        int newCol = col + j;

                        if (newRow >= 0 && newRow < height && newCol >= 0 && newCol < width) {
                            int neighborIndex = newRow * width + newCol;
                            if (states[neighborIndex] == CellState.Alive) {
                                count++;
                            }
                        }
                    }
                }

                neighborCounts[index] = count;
            }
        }

        [BurstCompile]
        private struct UpdateStatesJob : IJobParallelFor {
            [ReadOnly] public NativeArray<int> neighborCounts;
            [ReadOnly] public NativeArray<CellState> currentStates;
            public NativeArray<CellState> nextStates;

            public void Execute(int index) {
                int neighbors = neighborCounts[index];
                CellState currentState = currentStates[index];

                if (currentState == CellState.Alive) {
                    if (neighbors < 2 || neighbors > 3) {
                        nextStates[index] = CellState.Death;
                    }
                    else {
                        nextStates[index] = CellState.Alive;
                    }
                }
                else {
                    nextStates[index] = (neighbors == 3) ? CellState.Alive : CellState.Death;
                }
            }
        }

        private void Simulate() {
            _iteration++;
            int totalCells = gridProperties.width * gridProperties.height;

            var countJob = new CountNeighborsJob {
                states = _currentStates,
                neighborOffsets = _neighborOffsets,
                neighborCounts = _neighborCounts,
                width = gridProperties.width,
                height = gridProperties.height
            };

            var countHandle = countJob.Schedule(totalCells, _chunksCount);

            var updateJob = new UpdateStatesJob {
                neighborCounts = _neighborCounts,
                currentStates = _currentStates,
                nextStates = _nextStates
            };

            var updateHandle = updateJob.Schedule(totalCells, _chunksCount, countHandle);
            updateHandle.Complete();

            SwapAndUpdateBuffers();
            Debug.Log("Advance iteration: " + _iteration);
        }

        private void SwapAndUpdateBuffers() {
            var temp = _currentStates;
            _currentStates = _nextStates;
            _nextStates = temp;

            for (int i = 0; i < _states.Length; i++) {
                CellState newState = _currentStates[i];
                if (_states[i] != newState) {
                    _states[i] = newState;
                    _colors[i] = newState == CellState.Alive ? Color.white : Color.black;
                }
            }
        }

        private void TrySetState(int id, CellState state) {
            if (_states[id] == state) {
                return;
            }

            _states[id] = state;
            _currentStates[id] = state;
            _colors[id] = state == CellState.Alive ? Color.white : Color.black;
        }

        private static bool RectangleCheck(Vector3 mousePos, Vector3 pos, float offset) {
            var minX = pos.x - offset / 2;
            var maxX = pos.x + offset / 2;
            var minY = pos.y - offset / 2;
            var maxY = pos.y + offset / 2;

            return mousePos.x >= minX && mousePos.x < maxX && mousePos.y >= minY && mousePos.y < maxY;
        }

        public static int GetCellID(int heightIndex, int widthIndex, int height, int width) {
            if (heightIndex >= height || heightIndex < 0) {
                return -1;
            }

            if (widthIndex >= width || widthIndex < 0) {
                return -1;
            }
            return heightIndex * width + widthIndex;
        }

        [Serializable]
        public struct SimulationProperties {
            public float advanceDelay;
            public int initialEpoch;
        }

        [Serializable]
        public struct GridProperties {
            public float offset;
            public int width;
            public int height;
        }

        [Serializable]
        public enum CellState {
            Death,
            Alive,
        }

        public struct CellProperties {
            public int neighborCount;
        }

        public struct SimulationInput {
            public Vector3 screenPos;
            public MouseKey mouseKey;
            public bool mouseClicked;
            public bool spaceKeyDown;
        }

        public enum MouseKey {
            Left = 0,
            Right = 1,
        }

        public struct SimulationInputModule {
            public SimulationInput Update() {
                var input = new SimulationInput();
                if (Input.GetKeyDown(KeyCode.Space)) {
                    input.spaceKeyDown = true;
                    return input;
                }

                if (Input.GetMouseButtonDown(0) || Input.GetMouseButton(0)) {
                    input.mouseClicked = true;
                    input.mouseKey = MouseKey.Left;
                    input.screenPos = Input.mousePosition;
                    return input;
                }

                if (Input.GetMouseButtonDown(1) || Input.GetMouseButton(1)) {
                    input.mouseClicked = true;
                    input.mouseKey = MouseKey.Right;
                    input.screenPos = Input.mousePosition;
                    return input;
                }
                return default;
            }
        }

        private void RenderInstances() {
            int batchSize = 1023;
            int totalInstances = _matrices.Length;
            int batchCount = Mathf.CeilToInt((float) totalInstances / batchSize);

            for (int i = 0; i < batchCount; i++) {
                int remainingInstances = totalInstances - (i * batchSize);
                int currentBatchSize = Mathf.Min(batchSize, remainingInstances);

                Array.Copy(_colors, i * batchSize, _batchColors, 0, currentBatchSize);
                Array.Copy(_matrices, i * batchSize, _batchMatrices, 0, currentBatchSize);

                _propertyBlock.SetVectorArray(c_color_hash, _batchColors);

                Graphics.DrawMeshInstanced(
                    cellMesh,
                    0,
                    cellMaterial,
                    _batchMatrices,
                    currentBatchSize,
                    _propertyBlock
                );
            }
        }
    }
}