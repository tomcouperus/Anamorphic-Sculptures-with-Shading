# Notes
Mirrors should be on the "Mirror" layer for raycasting purposes and the mapped object on the "mapped object" layer.

# 2D optimizer
- [ ] Rotation around the x and z axes
- [ ] Optimization of other planes
- [ ] Fixing the abnormal gamma values
- [ ] Related: fixing the NaN gamma values patch
- [ ] Possibly extending the optimization method to 3D? Requires math first

Conclusion: doesn't work for shapes in 3D other than those extruded from 2D.

# 3D triangle plane intersection
- [x] Find way of fixing the rotation. Maybe instead of negating the y-axis rotation, negate the major component? Because something is going wonky... Solution: negate the entire rotation (which makes sense in hindsight lol)
- [ ] Sphere is still wonky, because of the cubic-sphere thing. The edges where two of the six sides meet is probably duplicate vertices. To fix: use the duplicate vertices trick to continue the optimizing algo.

Conclusion: does not have enough freedoms once enough vertices have been placed.

# 3D iterative descent
Tries to reduce the angle of each vertex/triangle compared to the original.