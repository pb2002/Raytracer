#version 430 core

#define MAX_PRIMITIVES 8192
#define MAX_LIGHTS 256

#define EPSILON 0.0001
#define MAXDST 100000000.0
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
    float roughness;
    float metallic;
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
uniform vec2 tnoise;
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

uniform bool fastMode;
uniform bool firstSample;

// ---------------------------------------------------------------------------------

// SSBO ----------------------------------------------------------------------------
layout (std430, binding = 2) buffer SceneBlock {    
    Sphere spheres[MAX_PRIMITIVES];     // MAX_PRIMITIVES * 8
    Plane planes[MAX_PRIMITIVES]; 
    Light lights[MAX_LIGHTS];
};
// ---------------------------------------------------------------------------------

// 2d noise
float drand48(vec2 co) {
  return 2 * fract(sin(dot(co.xy, vec2(12.9898,78.233))) * 43758.5453) - 1;
}

// reflect vector
vec3 reflect(in vec3 v, in vec3 n) {
  return v - 2 * dot(v, n) * n;
}

// random vec3 generator
vec3 randv3(vec2 co) {
    return vec3(drand48(co + vec2(4.381769)), 
                        drand48(co - vec2(2.782163)), 
                        drand48(co + vec2(9.428055)));
}



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
    
    float latitude = 0.5 - 0.5 * dot(vec3(0,1,0), direction);
        
    
    return pow(texture(texture2, vec2(longitude, latitude)).xyz, vec3(1.5)) * 4;       
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
    if (dot(c,c) < r2) t += sqrt(determinant);
    else t -= sqrt(determinant);
    
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
    Intersection hit = Intersection(MAXDST, vec3(0), vec3(0), Material(vec3(0), 0, 0));
    
    // check all primitives
    for (int i = 0; i < sphereCount; i++){
        IntersectSphere(ray, spheres[i], hit);
    }
    for (int i = 0; i < planeCount; i++){
        IntersectPlane(ray, planes[i], hit);
    }
        
    return hit;
}

vec3 LambertianScatter(vec2 uv, vec3 normal) {        
    return randv3(uv) + normal;
}
vec3 SpecularScatter(vec2 uv, vec3 v, vec3 normal, float roughness) {    
    return reflect(v, normal) + roughness * randv3(uv);              
}
vec3 RandomPointInLight(vec2 uv, Light l) {    
    return l.position + randv3(uv) * l.size;                      
}

float FresnelSchlick(float F0, float cos_theta_incident) {
    float p = 1.f - cos_theta_incident;
    
    // fast n^5
    float p2 = p * p;
    return mix(F0, 1.0, p2 * p2 * p);
}

vec3 ShadeDirect(Intersection hit, vec2 uv) {
    vec3 result = vec3(0);
    for (int j = 0; j < lightCount; j++) {
        vec3 lpoint = RandomPointInLight(uv, lights[j]);
        vec3 delta = lpoint - hit.point;
        vec3 lightDir = normalize(delta);
        Ray lightRay = Ray(hit.point + EPSILON * lightDir, lightDir);
        Intersection lightHit = IntersectWithScene(lightRay);
        
        if (lightHit.distance >= length(lpoint - hit.point)) {
            float dfactor = max(0, dot(lightDir, hit.normal));
            result += dfactor * hit.material.color * TextureSample(hit) * lights[j].color * lights[j].intensity / dot(delta,delta);
        }            
    }   
    return result;
}

vec3 Trace(Ray ray, int depth, vec2 uv) {
    vec3 color = vec3(0,0,0);                      
    vec3 factor = vec3(1,1,1); // reflection contribution factor
             
    Ray sampleRay = ray;                    
       
    for (int i = 0; i <= depth; i++) { // for the given number of bounces

        Intersection hit = IntersectWithScene(sampleRay);                
        if (hit.distance >= MAXDST){            
            color += factor * SkyboxSample(sampleRay.direction);
            break;                
        }                    
        
        vec3 t = TextureSample(hit);                
        
        if (hit.material.metallic > 0) {            
            sampleRay.direction = SpecularScatter(uv, sampleRay.direction, hit.normal, hit.material.roughness);
            if (dot(sampleRay.direction, hit.normal) > 0) factor *= hit.material.color * t * 0.5;
        } 
        else {            
            vec3 direct = ShadeDirect(hit, uv);
            color += direct * factor;
            factor *= 0.5 * t * hit.material.color;            
            sampleRay.direction = LambertianScatter(uv, hit.normal);          
        }                                        
        sampleRay.origin = hit.point + EPSILON * sampleRay.direction;                                   
    }
        
    return color;
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
    vec3 pixelOffset = vec3(1 / resolution.x, 1 / resolution.y, 0);
    vec3 uv = vec3(2 * position.x - 1, position.y, 0);
    vec3 col = vec3(0,0,0);
    
    
    if (fastMode) {
        Ray _r = CreateCameraRay(camera, uv);        
        col = Trace(_r, 1, uv.xy + tnoise * pixelOffset.xy);
    }
    else {            
        for (int i = 0; i < 16; i++) {
            vec2 rand = randv3(uv.xy + tnoise * i).xy;
            vec3 offset = pixelOffset.x * rand.x * camera.right + pixelOffset.y * rand.y * camera.up;
            Ray _r = CreateCameraRay(camera, uv + offset);
            col += Trace(_r, reflectionBounces, rand) * 1.0/16;
        }
    }          
    if (useTonemapping)    
        outputColor = vec4(Tonemap(col), 1.0);
    else
        outputColor = vec4(col, 1.0);
}