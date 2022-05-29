#version 430 core

#define MAX_PRIMITIVES 8192
#define MAX_LIGHTS 256

#define EPSILON 0.0001
#define MAXDST 100000000.0
#define AA_SAMPLES 2
#define PI 3.141592654

in vec3 position;
out vec4 outputColor;

// Structs -------------------------------------------------------------------------

struct Ray {
    vec3 origin;
    vec3 direction;
};

struct Material {
    vec3 color;
    float specular;
    bool metallic;
};
struct Sphere {
    vec3 position;
    float radius;
    Material material;
};

struct Plane {
    vec3 position;
    float _p0; // forced padding
    vec3 normal;
    float _p1; // forced padding
    Material material;
};

struct Intersection {
    float distance;
    vec3 point;
    vec3 normal;
    Material material;
};

struct Light {
    vec3 position;
    float size;
    vec3 color;    
    float intensity;
};

struct Camera {
    vec3 position;
    vec3 screenCenter;
    vec3 up;
    vec3 right;
    float aspectRatio;    
};
// ---------------------------------------------------------------------------------

// Uniforms ------------------------------------------------------------------------
uniform Camera camera;

uniform bool useTonemapping;
uniform float exposureBias;

uniform int reflectionBounces;
uniform float specularPow;
uniform vec3 skyColor;
uniform float ambientIntensity;
uniform float shadowStrength;

uniform int sphereCount;
uniform int planeCount;
uniform int lightCount;

uniform vec2 resolution;
uniform sampler2D texture1;
uniform sampler2D texture2;
// ---------------------------------------------------------------------------------

// SSBO ----------------------------------------------------------------------------
layout (std430, binding = 2) buffer SceneBlock {    
    Sphere spheres[MAX_PRIMITIVES];     // MAX_PRIMITIVES * 8
    Plane planes[MAX_PRIMITIVES]; 
    Light lights[MAX_LIGHTS];
};
// ---------------------------------------------------------------------------------

// AABB
// https://gist.github.com/DomNomNom/46bb1ce47f68d255fd5d
bool CheckAABB(vec3 rayOrigin, vec3 rayDir, vec3 boxMin, vec3 boxMax) {
    vec3 tMin = (boxMin - rayOrigin) / rayDir;
    vec3 tMax = (boxMax - rayOrigin) / rayDir;
    vec3 t1 = min(tMin, tMax);
    vec3 t2 = max(tMin, tMax);
    float tNear = max(max(t1.x, t1.y), t1.z);
    float tFar = min(min(t2.x, t2.y), t2.z);
    return tNear <= tFar && tFar > 0;
};

// nicer texture sampling for spheres
vec3 TextureSample(Intersection hit){
    vec3 n = abs(hit.normal);
    vec3 xsample = texture(texture1, hit.point.zy * 0.5 + vec2(0.217,-0.142)).xyz * n.x;
    vec3 ysample = texture(texture1, hit.point.xz * 0.5 + vec2(-0.072,-0.208)).xyz * n.y; 
    vec3 zsample = texture(texture1, hit.point.xy * 0.5).xyz * n.z;      
    return sqrt(xsample * xsample + ysample * ysample + zsample * zsample);
}

// equirectangular sampling
vec3 SkyboxSample(vec3 direction){
    
    // prevent division by zero
    if (abs(direction.x) < 0.0001)
        direction.x = 0.0001;         
        
    float longitude = atan(direction.z / direction.x);        
    if (direction.x < 0) longitude += PI;
    longitude /= 2 * PI;
    
    float latitude = 0.5 + 0.5 * dot(vec3(0,1,0), direction);
    
    // gamma correction
    return pow(texture(texture2, vec2(longitude, latitude)).xyz, vec3(2.2, 2.2, 2.2));        
}

void IntersectSphere(Ray ray, Sphere sphere, inout Intersection closest)
{
    // AABB Check
    // branch prediction misses are very slow so this actually doesn't improve performance
    
    // if (!CheckAABB(ray.origin, ray.direction, sphere.position - sphere.radius, sphere.position + sphere.radius)) return;
        
    float r2 = sphere.radius * sphere.radius;
    vec3 c = sphere.position - ray.origin;
            
    float t = dot(c, ray.direction);
    
    vec3 q = c - t * ray.direction;
    float determinant = r2 - dot(q,q);

    if (determinant < 0) return;
    t -= sqrt(determinant);
    
    if (t < 0) return; // intersection point is behind the ray    
    if (t > closest.distance) return;
    
    vec3 point = ray.origin + ray.direction * t;
    vec3 normal = normalize(point - sphere.position);
    
    closest.distance = t;
    closest.point = point;
    closest.normal = normal;
    closest.material = sphere.material;
}
void IntersectPlane(Ray ray, Plane plane, inout Intersection closest) {
        float denom = dot(ray.direction, plane.normal);
        if (abs(denom) <= EPSILON) return;
        
        float t = dot(plane.position - ray.origin, plane.normal) / denom;
        if (t < 0 || t > closest.distance) return;

        closest.distance = t;
        closest.point = ray.origin + t * ray.direction;
        closest.normal = plane.normal;                        
        closest.material = plane.material;
}

// Get the closest intersection with the scene
Intersection IntersectWithScene(Ray ray) {
    
    // dummy hit with big distance
    Intersection hit = Intersection(MAXDST, vec3(0.0, 0.0, 0.0), vec3(0.0, 0.0, 0.0), Material(vec3(0.0, 0.0, 0.0), 0, false));
    
    // check all primitives
    for (int i = 0; i < sphereCount; i++){
        IntersectSphere(ray, spheres[i], hit);
    }
    for (int i = 0; i < planeCount; i++){
        IntersectPlane(ray, planes[i], hit);
    }
        
    return hit;
}

vec3 Shade(Ray ray, Intersection hit) {
    // ray did not hit anything
    if (hit.distance >= MAXDST) {
        // return skybox color
        return SkyboxSample(ray.direction);
    }              
    
    // sample color from texture
    // all objects use the same texture because per-object textures are hard
    // to achieve using glsl (because of 16 textures per shader limitation).    
    vec3 t = TextureSample(hit);
    
    // ambient color component
    vec3 ambient = skyColor * ambientIntensity * hit.material.color * t;
         
    vec3 result = ambient;
    
    // iterate through all lights
    for (int i = 0; i < lightCount; i++) {                       
        Light l = lights[i];
        vec3 toLight =  l.position - hit.point; // point to light vector        
        float dst2 = dot(toLight, toLight); // squared light distance
        // light ray direction
        vec3 lightDir = normalize(toLight);       
        
        // shadows ---------------------------------------------------------
               
        // cast a light ray
        Ray lightRay = Ray(hit.point + 0.0001 * lightDir, lightDir);      
        Intersection lightHit = IntersectWithScene(lightRay);                
        
        // there is only a shadow if the lightRay hits something closer to
        // the ray origin than the light source.
        bool shadow = lightHit.distance < MAXDST && dst2 > lightHit.distance * lightHit.distance;
                
        // -----------------------------------------------------------------
                      
        // diffuse
        float diffuseFactor = max(dot(lightDir, hit.normal), 0);
        vec3 diffuse = (hit.material.metallic) ? vec3(0,0,0) : hit.material.color * t * diffuseFactor;
        
        // specular --------------------------------------------------------
        vec3 refl = lightDir - 2 * dot(lightDir, hit.normal) * hit.normal; // reflected light ray
        float rvdot = dot(ray.direction, refl);
        float specularFactor = pow(max(rvdot, 0), specularPow) * hit.material.specular;
        vec3 specularColor = hit.material.metallic ? hit.material.color : vec3(1,1,1);
        vec3 specular = specularColor * specularFactor;
        // -----------------------------------------------------------------

        
        vec3 composite = (diffuse + specular) 
            * l.color     // color
            * l.intensity // intensity
            / dst2; // inverse square law
        
        // apply shadow
        if (shadow) composite *= 1.0 - shadowStrength;
                        
        result += composite;
    }
    return result;
}

vec3 Trace(Ray ray, int depth) {
    vec3 result = vec3(0,0,0);
    
    vec3 factor = vec3(1,1,1); // reflection contribution factor
    
    for (int i = 0; i <= depth; i++) { // for the given number of bounces
    
        Intersection hit = IntersectWithScene(ray);
        result += Shade(ray, hit) * factor;
        
        // reflection ray
        vec3 refl = ray.direction - 2 * dot(ray.direction, hit.normal) * hit.normal;             
        ray = Ray(hit.point + refl * 0.001, refl);
        
        // fresnel factor
        float fresnel = pow(1 - abs(dot(ray.direction, hit.normal)), 5f);
        
        // update reflection factor
        factor *= hit.material.specular * (hit.material.metallic ? hit.material.color : fresnel * vec3(1,1,1));
    }
    return result;
}

Ray CreateCameraRay(Camera c, vec3 p){
    return Ray(
        c.position,
        normalize(c.screenCenter + c.right * p.x + c.up * p.y - c.position)
    );
}

// uncharted 2 tonemapper
// https://www.gdcvault.com/play/1012351/Uncharted-2-HDR
// http://filmicworlds.com/blog/filmic-tonemapping-operators/
// https://github.com/Zackin5/Filmic-Tonemapping-ReShade/blob/master/Uncharted2.fx

const float A = 0.15; // Shoulder strength
const float B = 0.60; // Linear strength
const float C = 0.10; // Linear angle
const float D = 0.10; // Toe strength
const float E = 0.03; // Toe numerator
const float F = 0.30; // Toe denominator
const float W = 11.2; // Linear white point value
    
vec3 Uncharted2Tonemap(vec3 x)
{
   return ((x*(A*x+C*B)+D*E)/(x*(A*x+B)+D*F))-E/F;
}
vec3 Tonemap(vec3 v) {    
    vec3 toned = Uncharted2Tonemap(v * exposureBias);
    
    // white balance
    vec3 w = Uncharted2Tonemap(vec3(W, W, W));
    vec3 whiteScale = 1 / w;
    vec3 wbColor = toned * whiteScale;
      
    // gamma correction
    return vec3(pow(wbColor.x, 1 / 2.2), pow(wbColor.y, 1 / 2.2), pow(wbColor.z, 1 / 2.2));
}
// ------------------------------------------------------------------------------------------

void main()
{
    vec3 uv = vec3(2 * position.x - 1, position.y, 0);
    vec3 col = vec3(0,0,0);
    
    vec3 pixelOffset = vec3(1 / resolution.x, 1 / resolution.y, 0) / AA_SAMPLES;
      
    // antialiasing (SSAA)
    for (int y = 0; y < AA_SAMPLES; y++) {
        for (int x = 0; x < AA_SAMPLES; x++) {
            vec3 offset = x * camera.right * pixelOffset.x + y * camera.up * pixelOffset.y;                         
            Ray _r = CreateCameraRay(camera, uv + offset);
            col += 1.0 / (AA_SAMPLES * AA_SAMPLES) * Trace(_r, reflectionBounces);
        }
    }          
    if (useTonemapping)    
        outputColor = vec4(Tonemap(col), 1.0);
    else
        outputColor = vec4(col, 1.0);
}