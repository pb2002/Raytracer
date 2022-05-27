#version 430 core

#define MAX_PRIMITIVES 8192
#define MAX_LIGHTS 256

#define EPSILON 0.0001
#define MAXDST 100000000.0

in vec3 position;
out vec4 outputColor;

struct Ray {
    vec3 origin;
    vec3 direction;
};

struct Material {
    vec3 color;
    float specular;
};
struct Sphere {
    vec3 position;
    float radius;
    Material material;
};

struct Plane {
    vec3 position;
    // 4 bytes padding
    vec3 normal;
    // 4 bytes padding
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
    // 4 bytes padding
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

layout (std430, binding = 2) buffer SceneBlock {    
    Sphere spheres[MAX_PRIMITIVES];     // MAX_PRIMITIVES * 8
    Plane planes[MAX_PRIMITIVES]; 
    Light lights[MAX_LIGHTS];
};

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

void IntersectSphere(Ray ray, Sphere sphere, inout Intersection closest)
{    
    if (!CheckAABB(ray.origin, ray.direction, sphere.position - sphere.radius, sphere.position + sphere.radius)) return;
        
    float r2 = sphere.radius * sphere.radius;
    vec3 c = sphere.position - ray.origin;
            
    float t = dot(c, ray.direction);
    vec3 q = c - t * ray.direction;
    float p2 = dot(q,q);

    if (p2 > r2) return;
    t -= sqrt(r2 - p2);
    
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

Intersection IntersectWithScene(Ray ray) {
    
    // dummy hit with big distance
    Intersection hit = Intersection(MAXDST, vec3(0.0, 0.0, 0.0), vec3(0.0, 0.0, 0.0), Material(vec3(0.0, 0.0, 0.0), 0));
    
    for (int i = 0; i < sphereCount; i++){
        IntersectSphere(ray, spheres[i], hit);
    }
    for (int i = 0; i < planeCount; i++){
        IntersectPlane(ray, planes[i], hit);
    }
    
    return hit;
}
vec3 Shade(Ray ray, Intersection hit) {
    if (hit.distance >= MAXDST){
        return skyColor;
    }
    vec3 result = skyColor * ambientIntensity;
    
    for (int i = 0; i < lightCount; i++) {
        vec3 delta =  lights[i].position - hit.point;
        float dst2 = dot(delta, delta);
        vec3 lightDir = normalize(delta);
        
        // shadows ---------------------------------------------------------
        Ray lightRay = Ray(hit.point + 0.0001 * lightDir, lightDir);
        Intersection lightHit = IntersectWithScene(lightRay);
        bool shadow = false;
        
        if (lightHit.distance >= MAXDST) shadow = false;
        else shadow = length(delta) > lightHit.distance;
        // -----------------------------------------------------------------
        
        // specular --------------------------------------------------------
        vec3 refl = lightDir - 2 * dot(lightDir, hit.normal) * hit.normal;
        float rvdot = dot(ray.direction, refl);
        float specularFactor = pow(max(rvdot, 0), specularPow) * hit.material.specular;
        // -----------------------------------------------------------------
        
        // diffuse
        float diffuseFactor = max(dot(lightDir, hit.normal), 0);
        
        vec3 composite = (hit.material.color * diffuseFactor + specularFactor) * lights[i].intensity / dst2;
        if (shadow) composite *= 1 - shadowStrength;        
        result += composite;
    }
    return result;
}

vec3 Trace(Ray ray, int depth){
    vec3 result = vec3(0,0,0);
    
    float factor = 1;
    for (int i = 0; i <= depth; i++) {
        Intersection hit = IntersectWithScene(ray);
        result += Shade(ray, hit) * factor;
        
        vec3 refl = ray.direction - 2 * dot(ray.direction, hit.normal) * hit.normal;
              
        float fresnel = pow(1 - abs(dot(ray.direction, hit.normal)), 3.5f);
        ray = Ray(hit.point + refl * 0.001, refl);
        factor *= fresnel * hit.material.specular;
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
const float D = 0.50; // Toe strength
const float E = 0.03; // Toe numerator
const float F = 0.30; // Toe denominator
const float W = 11.2; // Linear white point value
    
vec3 Uncharted2Tonemap(vec3 x)
{
   return ((x*(A*x+C*B)+D*E)/(x*(A*x+B)+D*F))-E/F;
}
vec3 Tonemap(vec3 v) {    
    vec3 toned = Uncharted2Tonemap(v * exposureBias);
    
    // white balance & white point
    vec3 w = Uncharted2Tonemap(vec3(W, W, W));
    vec3 whiteScale = 1 / w;
    vec3 wbColor = toned * whiteScale;
      
    // gamma correction
    return vec3(pow(wbColor.x, 1 / 2.2), pow(wbColor.y, 1 / 2.2), pow(wbColor.z, 1 / 2.2));
}

void main()
{
    vec3 uv = vec3(2 * position.x - 1, position.y, 0);
    Ray r = CreateCameraRay(camera, uv);
    vec3 col = Trace(r, reflectionBounces);
    
    if (useTonemapping)    
        outputColor = vec4(Tonemap(col), 1.0);
    else
        outputColor = vec4(col, 1.0);
}