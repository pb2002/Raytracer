#version 430 core

in vec3 vPosition;
out vec3 position;
void main(){
    gl_Position = vec4(vPosition, 1.0);
    position = vPosition;
}
