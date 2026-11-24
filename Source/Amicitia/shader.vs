#version 330
layout(location = 0) in vec3 pos;
layout(location = 1) in vec3 nrm;
layout(location = 2) in vec2 tex;
layout(location = 3) in vec4 col;

out Data
{
  vec3 pos;
  vec3 nrm;
  vec2 tex;
  vec4 col;
} od;

uniform mat4 proj;
uniform mat4 view;
uniform mat4 tran;
uniform mat4 uvTran; // texture coordinate transform, identity unless the material has a uv animation

void main()
{
  od.pos = pos;
  od.nrm = (tran * vec4(nrm, 0.0)).xyz;
  od.tex = (uvTran * vec4(tex, 0.0, 1.0)).xy;
  od.col = col;
  gl_Position = proj * view * tran * vec4(pos, 1.0);
}
