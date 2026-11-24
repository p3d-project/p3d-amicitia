#version 330

out vec4 outColor;

in Data
{
  vec3 pos;
  vec3 nrm;
  vec2 tex;
  vec4 col;
} id;

uniform sampler2D diffuse;
uniform vec4 diffuseColor;
uniform int isTextured;

void main()
{
  vec4 color = vec4(1, 1, 1, 1);
  if(isTextured != 0)
  {
    if (texture2D(diffuse, id.tex).a < 0.1) discard;
    color = texture2D(diffuse, id.tex);
  }

  // alpha is texture * vertex * material
  float alpha = color.a * id.col.a * diffuseColor.a;

  //outColor = color;
  // fixed world space light so shading doesn't change with the camera, meshes without normals stay fully lit
  float light = 1.0;
  if (length(id.nrm) > 0.001)
    light = 0.5 + 0.5 * max(dot(normalize(id.nrm), normalize(vec3(0.3, 1.0, 0.4))), 0.0);

  outColor = vec4(color.rgb * light, alpha);
  //outColor = vec4((color*diffuseColor*clamp(dot(-vec3(0, 0, 1), id.nrm), 0.0, 1.0)).rgb, color.a);
}
