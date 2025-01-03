using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Common;

namespace RenderMeshInstancedWithComputeShader {
    public class GameOfLifeRenderMeshInstancedWithComputeShader : MonoBehaviour {
        [SerializeField] private UsageMode usageMode = UsageMode.LoadPatternFromFile;
        [SerializeField] private string patternName = "3enginecordershipeater.cells";

        [Header("Left Mouse Button: set cell alive")]
        [Header("Right Mouse Button: set cell dead")]
        [Header("Space: start simulation")]
        [SerializeField] private Mesh cellMesh;
        [SerializeField] private Material cellMaterial;
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private GridProperties gridProperties;
        [SerializeField] private SimulationProperties simulationProperties;

        private Matrix4x4[] _matrices;
        private Vector4[] _colors;
        private CellState[] _states;
        private Vector3[] _worldPositions;

        private Vector3 _positionCache = new Vector3(0, 0, 0);
        private SimulationInputModule _inputModule = new SimulationInputModule();
        private Camera _camera;
        private bool _simulationStarted;
        private float _t = 0;
        private int _iteration = 0;

        private ComputeBuffer _statesBuffer;
        private ComputeBuffer _nextStatesBuffer;
        private CellData[] _statesCache;

        private int _kernelIndex;

        private ComputeBuffer _matricesBuffer;

        private void Awake() {
            _camera = Camera.main;

            InitializeGrid(in gridProperties);
            InitializeComputeShader();

            if (usageMode == UsageMode.LoadPatternFromFile) {
                ApplyPattern();
                StartSimulation();
            }
        }

        private void ApplyPattern() {
            int[,] pattern = PatternLoader.Load(patternName, gridProperties.height, gridProperties.width);
            for (int i = 0; i < gridProperties.height; i++) {
                for (int j = 0; j < gridProperties.width; j++) {
                    int id = GetCellID(i, j, gridProperties.height, gridProperties.width);
                    TrySetState(id, pattern[i, j] == 1 ? CellState.Alive : CellState.Death);
                }
            }
        }

        private void StartSimulation() {
            _simulationStarted = true;
        }

        private void InitializeComputeShader() {
            _kernelIndex = computeShader.FindKernel("CSMain");

            int totalCells = gridProperties.width * gridProperties.height;
            int stride = sizeof(int) + 4 * sizeof(float); // state + color
            _statesBuffer = new ComputeBuffer(totalCells, stride);
            _nextStatesBuffer = new ComputeBuffer(totalCells, stride);
            _statesCache = new CellData[totalCells];

            var initialData = new CellData[totalCells];
            for (int i = 0; i < totalCells; i++) {
                initialData[i] = new CellData {
                    state = _states[i] == CellState.Alive ? 1 : 0,
                    color = _colors[i]
                };
            }

            _statesBuffer.SetData(initialData);
            _nextStatesBuffer.SetData(initialData);

            _matricesBuffer = new ComputeBuffer(_matrices.Length, 16 * sizeof(float));
            _matricesBuffer.SetData(_matrices);
        }

        private void OnDestroy() {
            if (_statesBuffer != null) {
                _statesBuffer.Release();
                _statesBuffer = null;
            }

            if (_nextStatesBuffer != null) {
                _nextStatesBuffer.Release();
                _nextStatesBuffer = null;
            }

            if (_matricesBuffer != null) {
                _matricesBuffer.Release();
                _matricesBuffer = null;
            }
        }

        private void InitializeGrid(in GridProperties gridProperties) {
            int count = gridProperties.height * gridProperties.width;
            _matrices = new Matrix4x4[count];
            _colors = new Vector4[count];
            _states = new CellState[count];
            _worldPositions = new Vector3[count];
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
                    _colors[id] = Color.black;
                }
            }

            Debug.Log($"Initializing grid: {gridProperties.width}x{gridProperties.height}, offset: {gridProperties.offset}");
        }

        private void Update() {
            if (usageMode == UsageMode.WithMouse && !_simulationStarted) {
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

        private void Simulate() {
            _iteration++;

            computeShader.SetBuffer(_kernelIndex, "_States", _statesBuffer);
            computeShader.SetBuffer(_kernelIndex, "_NextStates", _nextStatesBuffer);
            computeShader.SetInt("_Width", gridProperties.width);
            computeShader.SetInt("_Height", gridProperties.height);

            int threadGroupsX = Mathf.CeilToInt(gridProperties.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(gridProperties.height / 8.0f);
            computeShader.Dispatch(_kernelIndex, threadGroupsX, threadGroupsY, 1);

            var temp = _statesBuffer;
            _statesBuffer = _nextStatesBuffer;
            _nextStatesBuffer = temp;

            UpdateVisualStates();

            Debug.Log("Advance iteration: " + _iteration);
        }

        private void UpdateVisualStates() {
            _statesBuffer.GetData(_statesCache);

            for (int i = 0; i < _statesCache.Length; i++) {
                var cellData = _statesCache[i];
                CellState newState = cellData.state == 1 ? CellState.Alive : CellState.Death;
                if (_states[i] != newState) {
                    _states[i] = newState;
                    _colors[i] = cellData.color;
                }
            }
        }

        private void TrySetState(int id, CellState state) {
            if (_states[id] == state) return;

            _states[id] = state;
            _colors[id] = state == CellState.Alive ? Color.white : Color.black;

            var cellData = new CellData {
                state = state == CellState.Alive ? 1 : 0,
                color = _colors[id]
            };
            _statesBuffer.SetData(new[] { cellData }, 0, id, 1);
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

        public enum CellState {
            Death,
            Alive,
        }

        private void RenderInstances() {
            cellMaterial.SetBuffer("_CellBuffer", _statesBuffer);
            cellMaterial.SetBuffer("_Matrices", _matricesBuffer);

            Graphics.DrawMeshInstancedProcedural(
                cellMesh,
                0,
                cellMaterial,
                new Bounds(Vector3.zero, Vector3.one * 1000),
                _matrices.Length
            );
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CellData {
            public int state;
            public Vector4 color;
        }
    }
}