# Notes
Mirrors should be on the "Mirror" layer for raycasting purposes and the mapped object on the "mapped object" layer.

# TODO
- [ ] Rotation around the x and z axes
- [ ] Optimization of other planes
- [ ] Fixing the abnormal gamma values
- [ ] Related: fixing the NaN gamma values patch
- [ ] Possibly extending the optimization method to 3D? Requires math first

# TODO 2
- [ ] Find way of fixing the rotation. Maybe instead of negating the y-axis rotation, negate the major component? Because something is going wonky...