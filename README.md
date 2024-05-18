# Unity CPU Raytracer
## Background
I've always been interested in Computer Graphics and the math that goes into rendering a 3D scene, so I decided to create my own raytracer in Unity as an excercise.

Rays are cast using Unity physics engine and colliders.

## Features
* 3 Types of lights with intensity and range controls
  * Directional
  * Point
  * Ambient
* Shadows
* Glass
  * Refraction
  * IOR controls per object
* Metal
* Specular Reflections
* Texture Mapping
* [Optimize] flag to render half of pixels every other frame to improve realtime performance
* Render resolution controls independent of viewport resolution

## Limitations
* All rendered objects must use a mesh collider in order for texture mapping to work
* [Unity Materials Raytracer] component must be disabled and enabled when adding new mesh collider objects in runtime
* All rendered objects must have a [Raytraced Material Properties] component
* Transparency for non-glass materials is not implemented
* Roughness (scattering) is not implemented for glass
* Performance is not great due to being a purely CPU based implementation

## Resources
Below is a short list of websites I referenced in order to create this implementation:
* https://www.scratchapixel.com/lessons/3d-basic-rendering/introduction-to-ray-tracing/implementing-the-raytracing-algorithm.html
* https://raytracing.github.io/books/RayTracingInOneWeekend.html
* https://www.gamedev.net/forums/topic/687535-implementing-a-cube-map-lookup-function/
* https://blog.demofox.org/2017/01/09/raytracing-reflection-refraction-fresnel-total-internal-reflection-and-beers-law/
* https://www.shadertoy.com/view/NdKyWy
