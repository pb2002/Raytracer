#version 430 core

#define MAX_PRIMITIVES 256
#define MAX_LIGHTS 64

#define EPSILON 0.00001
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

uniform vec3 ambientColor;

uniform bool useTonemapping;
uniform float exposureBias;
uniform float specularPow;

uniform int sphereCount;
uniform int planeCount;
uniform int lightCount;

layout (std140, binding = 2) uniform SceneBlock {    
    Sphere spheres[MAX_PRIMITIVES];     // MAX_PRIMITIVES * 8
    Plane planes[MAX_PRIMITIVES]; 
    Light lights[MAX_LIGHTS];
};

void IntersectSphere(Ray ray, Sphere sphere, inout Intersection closest){

    float r2 = sphere.radius * sphere.radius;
    vec3 c = sphere.position - ray.origin;
            
    float t = dot(c, ray.direction);
    vec3 q = c - t * ray.direction;
    float p2 = dot(q,q);

    if (p2 > r2) return;
    if (dot(c,c) <= r2) t += sqrt(r2 - p2); 
    else t -= sqrt(r2 - p2);
    if (t < 0) return;
    
    vec3 point = ray.origin + ray.direction * t;
    vec3 normal = normalize(point - sphere.position);
    if (!(t <= closest.distance)) return;
    
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
    vec3 ambient = vec3(0, 0.7, 1.0);
    if (hit.distance >= MAXDST){
        return ambient;
    }
    vec3 result = ambient * 0.05;
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
        float specularFactor = pow(max(rvdot, 0), 500) * hit.material.specular;
        // -----------------------------------------------------------------
        
        float diffuseFactor = max(dot(lightDir, hit.normal), 0);
        
        vec3 composite = (hit.material.color * diffuseFactor + specularFactor) * lights[i].intensity / dst2;
        if (shadow) composite *= 0.05f;        
        result += composite;
    }
    return result;
}

vec3 Trace(Ray ray, int depth){
    vec3 result = vec3(0,0,0);
    
    float factor = 1;
    for (int i = 0; i < depth; i++) {
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
// http://filmicworlds.com/blog/filmic-tonemapping-operators/
const float A = 0.15;
const float B = 0.50;
const float C = 0.10;
const float D = 0.20;
const float E = 0.02;
const float F = 0.30;
const float W = 11.2;
    
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
    vec3 col = Trace(r, 3);
    
    if (useTonemapping)    
        outputColor = vec4(Tonemap(col), 1.0);
    else
        outputColor = vec4(col, 1.0);
}