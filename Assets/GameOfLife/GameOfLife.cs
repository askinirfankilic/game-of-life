using System;
using UnityEngine;

namespace Game {
    public class GameOfLife : MonoBehaviour {
        [Header("Left Mouse Button: set cell alive")]
        [Header("Right Mouse Button: set cell dead")]
        [Header("Space: start simulation")]
        [SerializeField] private MeshRenderer cellRef;

        [SerializeField] private GridProperties gridProperties;
        [SerializeField] private SimulationProperties simulationProperties;

        private MeshRenderer[] _renderers;
        private CellState[] _states;
        private Vector3[] _worldPositions;
        private static readonly int c_color_hash = Shader.PropertyToID("_BaseColor");
        private Vector3 _positionCache = new Vector3(0, 0, 0);
        private SimulationInputModule _inputModule = new SimulationInputModule();
        private Camera _camera;
        private bool _simulationStarted;
        private float _t = 0;
        private int _iteration = 0;

        private void Awake() {
            _camera = Camera.main;
            InitializeGrid(in gridProperties);
        }

        private void InitializeGrid(in GridProperties gridProperties) {
            int count = gridProperties.height * gridProperties.width;
            _renderers = new MeshRenderer[count];
            _states = new CellState[count];
            _worldPositions = new Vector3[count];
            _camera.orthographicSize = gridProperties.height / 2f;
            _camera.transform.position = new Vector3(gridProperties.width / 2f, gridProperties.height / 2f, _camera.transform.position.z);

            for (int i = 0; i < gridProperties.height; i++) {
                for (int j = 0; j < gridProperties.width; j++) {
                    int id = GetCellID(i, j, gridProperties.height);
                    var instance = Instantiate(cellRef, transform);
                    _positionCache.Set(j * gridProperties.offset, i * gridProperties.offset, 0);
                    instance.transform.position = _positionCache;
                    _renderers[id] = instance;
                    _worldPositions[id] = _positionCache;
                    _states[id] = CellState.Death;
                    SetCellVisual(_states[id], _renderers[id]);
                }
            }

            StaticBatchingUtility.Combine(gameObject);
            GC.Collect();
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
                        Debug.Log("set alive" + input.screenPos);
                        var mousePos = _camera.ScreenToWorldPoint(input.screenPos);
                        mousePos = new Vector3(mousePos.x, mousePos.y, 0);
                        for (int i = 0; i < _worldPositions.Length; i++) {
                            var pos = _worldPositions[i];
                            if (RectangleCheck(mousePos, pos, gridProperties.offset)) {
                                int id = i;
                                TrySetState(id, CellState.Alive);
                                return;
                            }
                        }
                    }
                    else {
                        Debug.Log("set death" + input.screenPos);
                        var mousePos = _camera.ScreenToWorldPoint(input.screenPos);
                        mousePos = new Vector3(mousePos.x, mousePos.y, 0);
                        for (int i = 0; i < _worldPositions.Length; i++) {
                            var pos = _worldPositions[i];
                            if (RectangleCheck(mousePos, pos, gridProperties.offset)) {
                                int id = i;
                                TrySetState(id, CellState.Death);
                                return;
                            }
                        }
                    }
                }

                return;
            }


            _t += Time.deltaTime;
            if (_t > simulationProperties.advanceDelay) {
                _t = 0;
                Simulate();
            }
        }

        private void Simulate() {
            _iteration++;
            Debug.Log("Advance iteration: " + _iteration);
        }

        private void TrySetState(int id, CellState state) {
            if (_states[id] == state) {
                return;
            }

            _states[id] = state;
            SetCellVisual(_states[id], _renderers[id]);
        }

        private static bool RectangleCheck(Vector3 mousePos, Vector3 pos, float offset) {
            var minX = pos.x - offset / 2;
            var maxX = pos.x + offset / 2;
            var minY = pos.y - offset / 2;
            var maxY = pos.y + offset / 2;

            return mousePos.x >= minX && mousePos.x < maxX && mousePos.y >= minY && mousePos.y < maxY;
        }


        public static void SetCellVisual(CellState state, MeshRenderer renderer) {
            Color color = default;
            switch (state) {
                case CellState.Death:
                    color = Color.black;
                    break;
                case CellState.Alive:
                    color = Color.white;
                    break;
            }

            renderer.material.SetColor(c_color_hash, color);
        }

        public static int GetCellID(int heightIndex, int widthIndex, int height) {
            return heightIndex * height + widthIndex;
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

                if (Input.GetMouseButtonDown(0)) {
                    input.mouseClicked = true;
                    input.mouseKey = MouseKey.Left;
                    input.screenPos = Input.mousePosition;
                    return input;
                }

                if (Input.GetMouseButtonDown(1)) {
                    input.mouseClicked = true;
                    input.mouseKey = MouseKey.Right;
                    input.screenPos = Input.mousePosition;
                    return input;
                }

                return default;
            }
        }
    }
}