using UnityEngine;

namespace Common {
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
}