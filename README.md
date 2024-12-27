## What is Game of Life?
A cellular automaton where cells live or die based on simple rules:
- Cells with 2-3 neighbors stay alive
- Dead cells with 3 neighbors become alive
- All other cells die

## Implementation Approaches

### 1. GameObject Approach (`Approaches/1_GameObjects/`)
- Each cell is a separate GameObject
- Uses MonoBehaviour for state management
- Easy to understand and implement
- Suitable for learning
- ⚠️ Performance: Crashes with 1M cells
- Best for: Learning and small-scale prototypes

### 2. RenderMeshInstanced (`Approaches/2_MeshInstancing/`)
- Uses Graphics.RenderMeshInstanced for efficient rendering
- Single mesh instance for all cells
- CPU-based state calculations
- 📊 Performance: 10-15 FPS with 1M cells
- Best for: Medium-scale simulations

### 3. Job System + RenderMeshInstanced (`Approaches/3_JobSystem/`)
- Parallel processing with IJob implementations
- Burst-compiled jobs for state calculations
- RenderMeshInstanced for rendering
- 📊 Performance: 25-30 FPS with 1M cells
- Best for: Large-scale CPU-based simulations

### 4. Compute Shader (`Approaches/4_ComputeShader/`)
- Full GPU implementation
- State calculations in compute shader
- Direct rendering to texture
- Minimal CPU-GPU data transfer
- 📊 Performance: 300+ FPS with 1M cells! 🚀
- Best for: Massive-scale simulations

## Performance Comparison
Testing environment: M1 Macbook Pro 16GB
Grid size: 1 million cells (1000x1000)

| Approach | FPS | Notes |
|----------|-----|-------|
| GameObjects | N/A | Unity crashes |
| MeshInstancing | 10-15 | CPU bottleneck |
| Job System | 25-30 | Good CPU utilization |
| Compute Shader | 300+ | Exceptional performance |

## Key Learnings
1. GameObjects are excellent for prototyping but don't scale well
2. RenderMeshInstanced provides good optimization for rendering
3. Job System significantly improves CPU-based calculations
4. Compute Shaders offer exceptional performance for parallel computations
5. ⚠️ Important: CPU-GPU data transfer can be a major bottleneck
   - Initial compute shader implementation with frequent CPU/GPU data transfer performed worse than the Job System version
   - Keeping data on GPU is crucial for performance

## Each Approach Includes
- Complete source code
- Example scene
- Performance benchmarking tools

## Requirements
- Unity 2022.3.49f1
- This implementation does not provde Game Of Life levels from text files. You should prepare them at runtime.

## Getting Started
1. Clone the repository
2. Open in Unity
3. Load one of the example scenes from the Scenes folder
4. Press Play to run the simulation