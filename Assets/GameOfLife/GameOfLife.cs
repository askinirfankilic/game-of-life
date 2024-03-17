using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game {
    public class GameOfLife : MonoBehaviour {
        [SerializeField] private MeshRenderer cellRef;
        [SerializeField] private GridProperties properties;

        private MeshRenderer[] _renderers;
        private CellState[] _states;
        private static readonly int c_color_hash = Shader.PropertyToID("_Color");
        private Vector3 _positionCache = new Vector3(0, 0, 0);

        private void Awake() {
            InitializeGrid(in properties);
        }

        private void InitializeGrid(in GridProperties gridProperties) {
            int count = gridProperties.height * gridProperties.width;
            _renderers = new MeshRenderer[count];
            _states = new CellState[count];
            // sol alttan basla
            // once widthi bitir
            // sonra hegiht arttir
            // 0. elemeant i = 0, j = 0
            // 1. eleman i = 0, j = 1
            // 2. eleman i = 0, j = 2
            // on tane var desek
            // 11. eleaman i = 1, j = 0

            for (int i = 0; i < gridProperties.height; i++) {
                for (int j = 0; j < gridProperties.width; j++) {
                    int id = i * gridProperties.height + j;
                    var instance = Instantiate(cellRef);
                    _positionCache.Set(j * gridProperties.offset, i * gridProperties.offset, 0);
                    instance.transform.position = _positionCache;
                    _renderers[id] = instance;
                    _states[id] = CellState.Death;
                    SetCellVisual(_states[id], _renderers[id]);
                }
            }
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
    }
}