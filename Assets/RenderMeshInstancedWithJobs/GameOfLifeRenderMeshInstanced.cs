using System;

using UnityEngine;

namespace RenderMeshInstancedWithJobs {
    public class GameOfLifeRenderMeshInstancedWithJobs : MonoBehaviour {
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

        private void Awake() {
            _camera = Camera.main;
            _propertyBlock = new MaterialPropertyBlock();
            _batchColors = new Vector4[1023];
            _batchMatrices = new Matrix4x4[1023];
            InitializeGrid(in gridProperties);
        }

        private void InitializeGrid(in GridProperties gridProperties) {
            Debug.Log($"Initializing grid: {gridProperties.width}x{gridProperties.height}, offset: {gridProperties.offset}");
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

        }



        private void Update() {
            if (!_simulationStarted) {
                var input = _inputModule.Update();

                if (input.spaceKeyDown) {
                    Debug.Log("simulation started");
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
            RefreshCellNeighbors();
            ApplySimulationLogic();
            Debug.Log("Advance iteration: " + _iteration);
        }

        private void ApplySimulationLogic() {
            for (int i = 0; i < _cellProperties.Length; i++) {
                var id = i;
                var cellProp = _cellProperties[id];
                var state = _states[id];
                if (state == CellState.Alive) {
                    if (cellProp.neighborCount < 2) {
                        TrySetState(i, CellState.Death);
                    }

                    if (cellProp.neighborCount > 3) {
                        TrySetState(id, CellState.Death);
                    }
                }

                else {
                    if (cellProp.neighborCount == 3) {
                        TrySetState(id, CellState.Alive);
                    }
                }
            }
        }

        private void RefreshCellNeighbors() {
            for (int i = 0; i < _cellProperties.Length; i++) {
                int id = i;
                _cellProperties[id].neighborCount = 0;
                GetCellCoordinate(id, gridProperties.width, gridProperties.height, out int hIndex, out int wIndex);
                int[] directions = new int[8];
                int left = GetCellID(hIndex, wIndex - 1, gridProperties.height, gridProperties.width);
                int leftUp = GetCellID(hIndex + 1, wIndex - 1, gridProperties.height, gridProperties.width);
                int up = GetCellID(hIndex + 1, wIndex, gridProperties.height, gridProperties.width);
                int rightUp = GetCellID(hIndex + 1, wIndex + 1, gridProperties.height, gridProperties.width);
                int right = GetCellID(hIndex, wIndex + 1, gridProperties.height, gridProperties.width);
                int rightDown = GetCellID(hIndex - 1, wIndex + 1, gridProperties.height, gridProperties.width);
                int down = GetCellID(hIndex - 1, wIndex, gridProperties.height, gridProperties.width);
                int leftDown = GetCellID(hIndex - 1, wIndex - 1, gridProperties.height, gridProperties.width);

                directions[0] = left;
                directions[1] = leftUp;
                directions[2] = up;
                directions[3] = rightUp;
                directions[4] = right;
                directions[5] = rightDown;
                directions[6] = down;
                directions[7] = leftDown;

                for (int j = 0; j < directions.Length; j++) {
                    var directionID = directions[j];
                    if (IsValid(directionID)) {
                        if (_states[directionID] == CellState.Alive) {
                            _cellProperties[id].neighborCount++;
                        }
                    }
                }
            }
        }

        private bool IsValid(int id) {
            if (id < 0) {
                return false;
            }

            if (id > (gridProperties.height * gridProperties.width - 1)) {
                return false;
            }

            return true;
        }

        private void TrySetState(int id, CellState state) {
            if (_states[id] == state) {
                return;
            }

            _states[id] = state;
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
            return heightIndex * height + widthIndex;
        }



        public static void GetCellCoordinate(int id, int width, int height, out int heightIndex, out int widthIndex) {
            // w10 h10 id 0
            //hi 0, wi 0
            // id = 5
            // hi = 0, wi = 5
            widthIndex = id % width;
            heightIndex = id / height;
        }

        [Serializable]
        public struct SimulationProperties {
            public int initialEpoch;
            public float advanceDelay;
        }

        [Serializable]
        public struct GridProperties {
            public int width;
            public int height;
            public float offset;
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
            public bool mouseClicked;
            public MouseKey mouseKey;
            public Vector3 screenPos;
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
            Debug.Log($"Rendering {_matrices.Length} instances");
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