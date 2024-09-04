# Anamorphic Sculptures with Shading
This project is an exploration into various approaches to restoring the normals of anamorphically deformed sculptures.

Anamorphic art uses perspective and mirrors to view a distorted image or sculpture
in the correct way. Normally, making anamorphic art is a difficult task where errors
can easily occur, since the artist has to create the image while thinking of their desired
perspective.

In recent years interest in the topic has been renewed and various papers have been
published on anamorphic art. One such [paper](https://doi.org/10.1016/j.cag.2023.05.023) [L. Pratt, A. Johnston, and N. Pietroni,
2023] has advanced the creation of anamorphic sculptures by computer simulation to
work with free-form 3D shapes as well as reflective and refractive materials. However,
the paper does not consider light sources or normals, which can lead to odd shadows or
lighter areas being introduced to models where it is not desired. 

This project expands on the original project by developing a simplified version with limited reflection depth
and adding lighting.

## The Deformation Method
The method by Pratt et al. works by placing a viewpoint and a reflective/refractive surface in a scene, and placing the 'correct' version of the object behind the surface. 
Next, a ray is traced from the viewpoint to each vertex of the correct object. 
These rays intersect with the surface, and are reflected/refracted using the laws of reflection and refraction.
To maintain the proper location of all vertices relative to each other, the deformed version of each vertex is constrained to its reflected/refracted ray.

## Setup
Our setup starts with a curved mirror, a camera, and a flat plane as the 'correct' object near the top.
We then deform the object using the method devised by Pratt et al. to create the curved plane near the bottom.
To highlight the issues that can arise with lighting due to the deformed normals, we place a light at infinity, shining perpendicular to the correct plane.
As the light shines perpendicular, it is dark. However, the deformed version of it, with the same light direction, is only dark in one half, and brightly illuminated in the other.

The picture below is taken by looking top-down, into the negative y-direction.
![A top-down perspective view of our testing setup: a curved mirror in the center, a correct object at the top with a light shining perpendicular to it to highlight lighting issues in the deformed version at the bottom.](Images/Light%20wrong.png)

## Implemented Approaches
Using this setup, we have explored various methods of restoring normals, both exact and approximate.
We list all of these approaches below, as well as a link to the paper they are explored in.

### Two-Dimensional Approach [[link]](https://fse.studenttheses.ub.rug.nl/33243/)
As our object is a vertical plane, we tried projecting the entire setup down into the 2-dimensional xz-plane.
The method works by picking one vertex as the reference point, and placing all other vertices using trigonometry.
This worked well for the simple plane, but swiftly broke down on shapes with more depth to them.

This method is implemented in [AnamorphicMapper.cs](Assets/Scripts/AnamorphicMapper.cs), in the `OptimizeXZPlane()` method.

### Three-Dimensional Approach [[link]](https://fse.studenttheses.ub.rug.nl/33243/)
The next approach also uses a reference point as its base. However, instead of a reference vertex, we use a reference triangle.
In 3D modelling, an object consists of multiple triangle faces. 
The idea is that we first deform the model, pick one triangle in the deformed model to be our reference point, and place the neighbouring triangles at angles relative to the reference based on what they should be in the correct model.
However, just the angle gives us a plane, but using the ray-constraint from the deformation algorithm we make a triangle.

In practice, this method runs out of degrees of freedom at some point and does not produce a stable 3D model.

This method is implemented in [AnamorphicMapper.cs](Assets/Scripts/AnamorphicMapper.cs), in the `OptimizeTriangleNormals()` method.

### Approximation by Iterative Descent [[link]](https://fse.studenttheses.ub.rug.nl/33243/)
Since the above methods did not work, we turned to an approximation approach.
We first deform the model to obtain a baseline deformation. Next, we calculate, for every vertex, the angle between the normal in the deformed model and the model in the correct model.
This is the total angular deviation.
We minimize this by randomly selecting a vertex and displacing it a small amount along its reflection ray, for many iterations.
During this process, we use simulated annealing to achieve a global minimum.
This approximation worked the best overall, though at most saw a 50% reduction in total angular deviation.

This method is implemented in [VertexNormalOptimizer.cs](Assets/Scripts/VertexNormalOptimizer.cs), in the `OptimizeAnnealing()` method.

## Possible Future Solutions
As none of our approaches completely solved the issue, we suggest several alternate approaches in the papers listed below.

## Papers and Theses
**Theses:**
- [T. Couperus: Anamorphic Sculptures with Shading, MSc Research Internship Thesis, University of Groningen, 2024](https://fse.studenttheses.ub.rug.nl/33243/)