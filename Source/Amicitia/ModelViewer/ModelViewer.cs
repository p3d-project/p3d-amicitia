using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OpenTK.Graphics;
using OpenTK.Input;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using AmicitiaLibrary.Graphics.RenderWare;
using AmicitiaLibrary.PS2.Graphics;
using SN = System.Numerics;

namespace Amicitia.ModelViewer
{
    public enum MeshBlendMode
    {
        Opaque,
        Alpha,
        Additive
    }

    public struct GpuModel
    {
        public int Vbo;
        public int[] Ibo;
        public int Vc;
        public int[] Ic;
        public string[] Tid;
        public Color[] Color;
        public bool Strip;
        public MeshBlendMode[] Blend;
        public Vector3[] Center;
        public bool[] Flat;
        public bool Collision;
        public string[] UvAnim;
        public GpuModel(int vbo, int[] ibo, int vc, int[] ic, string[] tid, Color[] color, bool strip, MeshBlendMode[] blend, Vector3[] center, bool[] flat, bool collision, string[] uvAnim)
        {
            this.Vbo = vbo;
            this.Ibo = ibo;
            this.Vc = vc;
            this.Ic = ic;
            this.Tid = tid;
            this.Color = color;
            this.Strip = strip;
            this.Blend = blend;
            this.Center = center;
            this.Flat = flat;
            this.Collision = collision;
            this.UvAnim = uvAnim;
        }
    }

    public struct BasicCol4
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public BasicCol4(Color color)
        {
            R = color.R;
            G = color.G;
            B = color.B;
            A = color.A;
        }
    }

    public struct BasicVec4
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public BasicVec4(float x, float y, float z, float w)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.W = w;
        }

        public BasicVec4(Vector4 v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
            W = v.W;
        }

        public BasicVec4(Color4 v)
        {
            X = v.R;
            Y = v.G;
            Z = v.B;
            W = v.A;
        }
    }

    public struct BasicVec3
    {
        public float X;
        public float Y;
        public float Z;

        public BasicVec3(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public BasicVec3(SN.Vector3 v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
        }
    }

    public struct BasicVec2
    {
        public float X, Y;

        public BasicVec2(float x, float y)
        {
            this.X = x;
            this.Y = y;
        }

        public BasicVec2(SN.Vector2 v)
        {
            X = v.X;
            Y = v.Y;
        }

    }

    public struct Vertex
    {
        public BasicVec3 Pos; 
        public BasicVec3 Nrm; 
        public BasicVec2 Tex;
        public BasicVec4 Col;

        public Vertex(BasicVec3 pos, BasicVec3 nrm, BasicVec2 tex, BasicVec4 col)
        {
            this.Pos = pos;
            this.Nrm = nrm;
            this.Tex = tex;
            this.Col = col;
        }
    }

    public class Camera
    {
        // Yaw = rotation around Y axis (left/right)
        // Pitch = rotation around X axis (up/down)
        private float mYaw   = -90f; // facing -Z by default
        private float mPitch =   0f;

        public Camera()
        {
            Position = new Vector3(0, 100, 250);
            Up       = new Vector3(0, 1, 0);
            UpdateForward();
        }

        public Vector3 Position { get; set; }
        public Vector3 Forward  { get; private set; }
        public Vector3 Up       { get; private set; }
        public Vector3 Right    => Vector3.Normalize(Vector3.Cross(Forward, Up));
        public float   Near     { get; set; } = 0.1f;
        public float   Far      { get; set; } = 100000f;

        public void Rotate(float dx, float dy, float sensitivity = 0.2f)
        {
            mYaw   += dx * sensitivity;
            mPitch -= dy * sensitivity;          // subtract: dragging down looks up
            mPitch  = MathHelper.Clamp(mPitch, -89f, 89f);
            UpdateForward();
        }

        public void LookAt(Vector3 position, Vector3 target)
        {
            Vector3 dir = Vector3.Normalize(target - position);
            Position = position;
            mYaw   = MathHelper.RadiansToDegrees((float)Math.Atan2(dir.Z, dir.X));
            mPitch = MathHelper.RadiansToDegrees((float)Math.Asin(dir.Y));
            UpdateForward();
        }

        private void UpdateForward()
        {
            float yawRad   = MathHelper.DegreesToRadians(mYaw);
            float pitchRad = MathHelper.DegreesToRadians(mPitch);
            Forward = Vector3.Normalize(new Vector3(
                (float)(Math.Cos(pitchRad) * Math.Cos(yawRad)),
                (float) Math.Sin(pitchRad),
                (float)(Math.Cos(pitchRad) * Math.Sin(yawRad))
            ));
        }

        public void Bind(ShaderProgram shader, GLControl control)
        {
            Matrix4 proj = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(60.0f), (float)control.Width / control.Height, Near, Far);
            Matrix4 view = Matrix4.LookAt(Position, Position + Forward, Up);
            shader.SetUniform("proj", proj);
            shader.SetUniform("view", view);
        }
    }

    internal static class RwtoGlConversionHelper
    {
        public static Dictionary<PS2FilterMode, int> FilterDictionary { get; } = new Dictionary<PS2FilterMode, int>()
        {
            { PS2FilterMode.None,               (int)TextureMagFilter.Linear },
            { PS2FilterMode.Nearest,            (int)TextureMagFilter.Nearest },
            { PS2FilterMode.Linear,             (int)TextureMagFilter.Linear },
            { PS2FilterMode.MipNearest,         (int)TextureMagFilter.Nearest },
            { PS2FilterMode.MipLinear,          (int)TextureMagFilter.Linear },
            { PS2FilterMode.LinearMipNearest,   (int)TextureMagFilter.Nearest },
            { PS2FilterMode.LinearMipLinear,    (int)TextureMagFilter.Linear }
        };

        public static Dictionary<PS2AddressingMode, int> WrapDictionary { get; } = new Dictionary<PS2AddressingMode, int>()
        {
            { PS2AddressingMode.None,       (int)TextureWrapMode.Repeat },
            { PS2AddressingMode.Wrap,       (int)TextureWrapMode.Repeat },
            { PS2AddressingMode.Mirror,     (int)TextureWrapMode.MirroredRepeat },
            { PS2AddressingMode.Clamp,      (int)TextureWrapMode.ClampToBorder }
        };
    }

    public class ModelViewer
    {
        // view states
        private bool mCanRender;
        private bool mIsViewReady;
        private bool mIsViewFocused;

        // store a handle to the loaded scene

        // gl control handle
        private GLControl mViewerCtrl;

        // model render data
        private List<GpuModel> mModels;
        private Dictionary<string, int> mTexLookup;
        private ShaderProgram mShaderProg;
        private int mCurrentGpuModelId;
        private int mCurrentTextureId;
        private Matrix4 mTransform;

        // camera data
        private Camera mCamera;
        private Vector3 mTp;

        // freecam mouse look
        private bool  mRightMouseDown = false;
        private Point mLastMousePos;
        private float mMoveSpeed = 200f;

        // positions of the loaded geometry, used for framing
        private List<Vector3> mVertexPositions = new List<Vector3>();
        private Vector3 mSceneMin, mSceneMax;

        // transparent meshes sorted back to front each frame
        private struct TransparentDraw
        {
            public int Model, Mesh;
            public float DistSq;
            public bool NoWrite;
        }
        private List<TransparentDraw> mTransparentDraws = new List<TransparentDraw>();

        private System.Diagnostics.Stopwatch mFrameTimer = System.Diagnostics.Stopwatch.StartNew();

        // looping texture animations (water, spray, clouds), looked up by name from the materials
        private Dictionary<string, UvAnimation> mUvAnims = new Dictionary<string, UvAnimation>();
        private System.Diagnostics.Stopwatch mAnimClock = System.Diagnostics.Stopwatch.StartNew();
        private double mAnimTime;

        // temporary calibration for the uv animations while the real playback speed and direction are worked out:
        // J flips the U scroll, K flips the V scroll, comma and period step the speed
        private static readonly float[] UvSpeeds = { 0.25f, 0.5f, 1f, 2f, 4f, 8f, 15f, 30f, 60f };
        private int mUvSpeedIndex = 2;
        private bool mUvFlipU, mUvFlipV;

        // debug toggles for tracking down the white rings: B cycles blended meshes, O polygon offset, M mipmaps
        private int mDebugView = 0; // 0 all, 1 opaque only, 2 blended only
        private bool mUsePolyOffset = true;
        private bool mUseMips = false;

        private void ShowDebugState()
        {
            string[] views = { "all", "opaque only", "blended only" };
            string text = string.Format("[view: {0} | poly offset: {1} | mipmaps: {2} | uv speed: {3}x | uv flip U: {4} V: {5}]", views[mDebugView], mUsePolyOffset ? "on" : "off", mUseMips ? "on" : "off",
                UvSpeeds[mUvSpeedIndex], mUvFlipU ? "on" : "off", mUvFlipV ? "on" : "off");
            Console.WriteLine(text);
            var form = mViewerCtrl.FindForm();
            if (form != null)
            {
                int i = form.Text.IndexOf(" [view:");
                if (i >= 0) form.Text = form.Text.Substring(0, i);
                form.Text += " " + text;
            }
        }

        private void ApplyMipSetting()
        {
            foreach (int tex in mTexLookup.Values)
            {
                GL.BindTexture(TextureTarget.Texture2D, tex);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                    mUseMips ? (int)TextureMinFilter.LinearMipmapLinear : (int)TextureMinFilter.Linear);
            }
            mCurrentTextureId = 0;
        }

        // collision clumps (untextured geometry) loaded as part of a scene can be hidden
        private bool mShowCollision = false;

        public bool ShowCollision
        {
            get { return mShowCollision; }
            set
            {
                mShowCollision = value;
                if (mViewerCtrl != null)
                    mViewerCtrl.Invalidate();
            }
        }

        // clear color
        private Color mBgColor;

        public Color BgColor
        {
            get { return mBgColor; }
            set
            {
                mBgColor = value;
                GL.ClearColor(mBgColor);
                mViewerCtrl.Invalidate();
            }
        }

        public static bool IsSupported
        {
            get
            {
                var version = GL.GetString( StringName.Version );
                return version[0] >= '3' && version[2] >= '3'; // X.Y
            }
        }

        public ModelViewer(GLControl controller)
        {
            mCanRender = false;

            if ( !InitializeShaderProgram() )
                return;

            mCanRender = true;

            string version = GL.GetString(StringName.Version);
            Console.WriteLine( version );

            mIsViewReady = false;
            mViewerCtrl = controller;
            mModels = new List<GpuModel>();
            mTexLookup = new Dictionary<string, int>();

            GL.ClearColor(new Color4(128, 128, 128, 255));
            GL.FrontFace(FrontFaceDirection.Ccw);
            // no back face culling, single sided meshes (flowers, planes) stay visible from behind
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            mViewerCtrl.Paint += ModelViewerPaint;
            mViewerCtrl.Resize += ModelViewerResize;
            mViewerCtrl.Enter += (s, e) => { mIsViewFocused = true; };
            mViewerCtrl.Leave += (s, e) => { mIsViewFocused = false; };
            mViewerCtrl.GotFocus += (s, e) => { mIsViewFocused = true; };
            mViewerCtrl.LostFocus += (s, e) => ReleaseInputState();
            mViewerCtrl.KeyDown  += OnKeyDown;

            var parentForm = controller.FindForm();
            if (parentForm != null)
                parentForm.Deactivate += (s, e) => ReleaseInputState();
            mViewerCtrl.MouseDown += OnMouseDown;
            mViewerCtrl.MouseUp   += OnMouseUp;
            mViewerCtrl.MouseMove += OnMouseMove;
            mViewerCtrl.MouseWheel += OnMouseWheel;

            mTp = new Vector3();
            mIsViewReady = true;
            GL.Viewport(0, 0, controller.Width, controller.Height);
            mCamera = new Camera();

            Application.Idle += OnApplicationIdle;
        }

        private bool InitializeShaderProgram()
        {
            try
            {
                // set up shader program
                mShaderProg = new ShaderProgram( "shader" );
                mShaderProg.AddUniform( "proj" );
                mShaderProg.AddUniform( "view" );
                mShaderProg.AddUniform( "tran" );
                mShaderProg.AddUniform( "diffuse" );
                mShaderProg.AddUniform( "diffuseColor" );
                mShaderProg.AddUniform( "isTextured" );
                mShaderProg.AddUniform( "uvTran" );
                mShaderProg.Bind();
            }
            catch ( Exception )
            {
                return false;
            }

            return true;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool PeekMessage(out NativeMsg msg, IntPtr hWnd,
            uint filterMin, uint filterMax, uint removeMsg);

        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct NativeMsg
        {
            public IntPtr hWnd, wParam, lParam;
            public uint message, time;
            public System.Drawing.Point pt;
        }

        private static bool IsMessageQueueEmpty()
            => !PeekMessage(out _, IntPtr.Zero, 0, 0, 0);

        private void OnApplicationIdle(object sender, EventArgs e)
        {
            while (mIsViewReady && mCanRender && mViewerCtrl.IsHandleCreated && IsMessageQueueEmpty())
            {
                float dt = (float)mFrameTimer.Elapsed.TotalSeconds;
                mFrameTimer.Restart();
                if (dt > 0.1f) dt = 0.1f; // clamp so a hitch can't teleport the camera

                UpdateCamera(dt);

                ModelViewerPaint(this, null);
            }
        }

        private void UpdateCamera(float dt)
        {
            // poll real focus, Enter isn't raised on window reactivation
            mIsViewFocused = mViewerCtrl.Focused;
            if (!mIsViewFocused) return;

            Vector3 oldPos = mCamera.Position;

            float speed = mMoveSpeed * dt;
            if (IsKeyDown(Keys.ShiftKey)) speed *= 3f;

            if (IsKeyDown(Keys.W)) mCamera.Position += mCamera.Forward * speed;
            if (IsKeyDown(Keys.S)) mCamera.Position -= mCamera.Forward * speed;
            if (IsKeyDown(Keys.D)) mCamera.Position += mCamera.Right   * speed;
            if (IsKeyDown(Keys.A)) mCamera.Position -= mCamera.Right   * speed;
            if (IsKeyDown(Keys.E)) mCamera.Position += mCamera.Up      * speed;
            if (IsKeyDown(Keys.Q)) mCamera.Position -= mCamera.Up      * speed;

            if (IsNaN(mCamera.Position))
                mCamera.Position = IsNaN(oldPos) ? Vector3.Zero : oldPos;
        }

        private void OnKeyDown(object sender, KeyEventArgs args)
        {
            // movement polled in idle loop
            if (args.KeyCode == Keys.F) FrameModel();
            if (args.KeyCode == Keys.B) { mDebugView = (mDebugView + 1) % 3; ShowDebugState(); }
            if (args.KeyCode == Keys.O && !args.Control) { mUsePolyOffset = !mUsePolyOffset; ShowDebugState(); }
            if (args.KeyCode == Keys.M) { mUseMips = !mUseMips; ApplyMipSetting(); ShowDebugState(); }
            if (args.KeyCode == Keys.J) { mUvFlipU = !mUvFlipU; ShowDebugState(); }
            if (args.KeyCode == Keys.K) { mUvFlipV = !mUvFlipV; ShowDebugState(); }
            if (args.KeyCode == Keys.OemPeriod) { mUvSpeedIndex = Math.Min(mUvSpeedIndex + 1, UvSpeeds.Length - 1); ShowDebugState(); }
            if (args.KeyCode == Keys.Oemcomma) { mUvSpeedIndex = Math.Max(mUvSpeedIndex - 1, 0); ShowDebugState(); }
        }

        private void TrackVertex(BasicVec3 p)
        {
            var v = new Vector3(p.X, p.Y, p.Z);
            if (IsNaN(v))
                return;

            if (mVertexPositions.Count == 0)
            {
                mSceneMin = mSceneMax = v;
            }
            else
            {
                mSceneMin = Vector3.ComponentMin(mSceneMin, v);
                mSceneMax = Vector3.ComponentMax(mSceneMax, v);
            }

            mVertexPositions.Add(v);
        }

        // keep the depth range tight around the scene to limit z fighting
        private void UpdateClipPlanes()
        {
            if (mVertexPositions.Count == 0)
            {
                mCamera.Near = 0.1f;
                mCamera.Far = 100000f;
                return;
            }

            Vector3 center = (mSceneMin + mSceneMax) * 0.5f;
            float radius = (mSceneMax - mSceneMin).Length * 0.5f;
            float far = (mCamera.Position - center).Length + radius;

            mCamera.Far = Math.Max(far * 1.05f, 10f);
            mCamera.Near = Math.Max(mCamera.Far / 1000f, 0.1f);
        }

        private static float Percentile(float[] sorted, float p) => sorted[(int)((sorted.Length - 1) * p)];

        // put the camera in front of the loaded model
        public void FrameModel()
        {
            if (!mCanRender || mVertexPositions.Count == 0)
                return;

            // trim outliers so stray geometry doesn't push the camera away
            int n = mVertexPositions.Count;
            float[] xs = new float[n], ys = new float[n], zs = new float[n];
            for (int i = 0; i < n; i++)
            {
                xs[i] = mVertexPositions[i].X;
                ys[i] = mVertexPositions[i].Y;
                zs[i] = mVertexPositions[i].Z;
            }
            Array.Sort(xs); Array.Sort(ys); Array.Sort(zs);

            Vector3 min = new Vector3(Percentile(xs, 0.02f), Percentile(ys, 0.02f), Percentile(zs, 0.02f));
            Vector3 max = new Vector3(Percentile(xs, 0.98f), Percentile(ys, 0.98f), Percentile(zs, 0.98f));

            Vector3 center = (min + max) * 0.5f;
            float radius = Math.Max((max - min).Length * 0.5f, 1f);

            // back off until the bounding sphere fits the narrowest fov
            float aspect = (float)mViewerCtrl.Width / Math.Max(mViewerCtrl.Height, 1);
            float vHalf = MathHelper.DegreesToRadians(30f);
            float hHalf = (float)Math.Atan(Math.Tan(vHalf) * aspect);
            float dist = radius / (float)Math.Sin(Math.Min(vHalf, hHalf)) * 1.1f;

            // 3/4 view, raised and turned off the front
            float az = MathHelper.DegreesToRadians(35f);
            float el = MathHelper.DegreesToRadians(25f);
            Vector3 offset = new Vector3((float)(Math.Sin(az) * Math.Cos(el)), (float)Math.Sin(el), (float)(Math.Cos(az) * Math.Cos(el)));

            mCamera.LookAt(center + offset * dist, center);
            mViewerCtrl.Invalidate();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        // OpenTK's keyboard state goes stale after long unfocus
        private static bool IsKeyDown(Keys key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

        // clear held input on focus loss
        private void ReleaseInputState()
        {
            mIsViewFocused = false;
            mRightMouseDown = false;
            if (mViewerCtrl != null && mViewerCtrl.IsHandleCreated)
                mViewerCtrl.Capture = false;
        }

        private void OnMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                mRightMouseDown = true;
                mLastMousePos   = e.Location;
                mViewerCtrl.Capture = true;   // keep receiving events outside the control
            }
        }

        private void OnMouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                mRightMouseDown = false;
                mViewerCtrl.Capture = false;
            }
        }

        private void OnMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!mRightMouseDown || !mCanRender)
                return;

            float dx = e.X - mLastMousePos.X;
            float dy = e.Y - mLastMousePos.Y;
            mLastMousePos = e.Location;

            mCamera.Rotate(dx, dy);
            mViewerCtrl.Invalidate();
        }

        private void OnMouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!mCanRender)
                return;

            // each notch = 120 units; scale speed up/down by 20 %
            float factor = e.Delta > 0 ? 1.2f : 1f / 1.2f;
            mMoveSpeed = Math.Max(1f, mMoveSpeed * factor);
        }

        // persona 3 stores the blend mode in the material user data
        private static int GetMaterialBlendId(RwMaterial material)
        {
            if (material.Extension == null)
                return -1;

            foreach (var ext in material.Extension)
            {
                if (ext is RwUserDataList list)
                {
                    var data = list["per3MatAlpha"];
                    if (data != null)
                        return data.IntValue;
                }
            }

            return -1;
        }

        // 1, 2 and 5 are alpha blended, 3 is used by glows and light rays so treat it as additive
        private static MeshBlendMode BlendModeFromId(int id)
        {
            switch (id)
            {
                case 0: return MeshBlendMode.Opaque;
                case 3: return MeshBlendMode.Additive;
                default: return MeshBlendMode.Alpha;
            }
        }

        // name of the uv animation a material plays, or null if it is static
        private static string GetMaterialUvAnimationName(RwMaterial material)
        {
            if (material.Extension == null)
                return null;

            foreach (var ext in material.Extension)
            {
                if (ext.Id == RwNodeId.RwUVAnimationPluginNode)
                    return UvAnimation.ParseMaterialAnimationName(ext.RawData);
            }

            return null;
        }

        // reads the uv animations stored with a scene so the materials that reference them can play them
        private void LoadUvAnimations(RmdScene rmdScene)
        {
            if (!mCanRender || rmdScene == null)
                return;

            foreach (var animationSet in rmdScene.Animations)
            {
                foreach (var node in animationSet)
                {
                    if (node.Id != RwNodeId.RwUVAnimationDictionaryNode)
                        continue;

                    foreach (var animation in UvAnimation.ParseDictionary(node.RawData))
                    {
                        if (!mUvAnims.ContainsKey(animation.Name))
                            mUvAnims.Add(animation.Name, animation);
                    }
                }
            }
        }

        public static bool IsNaN(Vector3 value)
        {
            return float.IsNaN(value.X) || float.IsNaN(value.Y) || float.IsNaN(value.Z);
        }

        private static byte ScalePS2Alpha(byte a) => (byte)Math.Min((a / 128.0f) * 255, 255);

        private HashSet<string> mTransparentTextures = new HashSet<string>();

        public void LoadTextures(RwTextureDictionaryNode textures)
        {
            if (textures == null || !mCanRender )
                return;

            int textureIdx = 0;
            foreach ( RwTextureNativeNode texture in textures.Textures )
            {
                // already loaded (RMD files commonly share textures with the field's .rws)
                if ( mTexLookup.ContainsKey( texture.Name ) )
                    continue;

                Console.WriteLine( "processing texture: {0}", textureIdx++ );

                var colorpixels = texture.GetPixels();

                // get the pixel array
                var pixels = new BasicCol4[texture.Width * texture.Height];
                for ( int i = 0; i < texture.Width * texture.Height; i++ )
                {
                    var c = colorpixels[i];
                    pixels[i] = new BasicCol4( Color.FromArgb(
                        texture.IsIndexed ? c.A : ScalePS2Alpha( c.A ),
                        c.R, c.G, c.B ) );
                }

                // flag texture as transparent if any pixel is non-opaque
                bool hasAlpha = false;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].A < 255) { hasAlpha = true; break; }
                }
                if (hasAlpha)
                    mTransparentTextures.Add(texture.Name);

                // create the texture
                int tex = GL.GenTexture();
                GL.BindTexture( TextureTarget.Texture2D, tex );

                // GL 4.5
                //GL.CreateTextures( TextureTarget.Texture2D, 1, out int tex );

                // set up params
                GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureWrapS, RwtoGlConversionHelper.WrapDictionary[texture.HorrizontalAddressingMode] );
                GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureWrapT, RwtoGlConversionHelper.WrapDictionary[texture.VerticalAddressingMode] );
                GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, RwtoGlConversionHelper.FilterDictionary[texture.FilterMode] );
                GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, RwtoGlConversionHelper.FilterDictionary[texture.FilterMode] );

                // GL 4.5
                //GL.TextureParameter( tex, TextureParameterName.TextureWrapS, RwtoGlConversionHelper.WrapDictionary[texture.HorrizontalAddressingMode] );
                //GL.TextureParameter( tex, TextureParameterName.TextureWrapT, RwtoGlConversionHelper.WrapDictionary[texture.VerticalAddressingMode] );
                //GL.TextureParameter( tex, TextureParameterName.TextureMagFilter, RwtoGlConversionHelper.FilterDictionary[texture.FilterMode] );
                //GL.TextureParameter( tex, TextureParameterName.TextureMinFilter, RwtoGlConversionHelper.FilterDictionary[texture.FilterMode] );

                // set up the bitmap data
                GL.TexImage2D( TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, texture.Width, texture.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels );
                GL.GenerateMipmap( GenerateMipmapTarget.Texture2D );

                // GL 4.5
                //GL.TextureStorage2D( tex, 1, SizedInternalFormat.Rgba8, texture.Width, texture.Height );
                //GL.TextureSubImage2D( tex, 0, 0, 0, texture.Width, texture.Height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels );

                // add a texture lookup for the processed texture
                mTexLookup.Add( texture.Name, tex );
            }
        }

        public void LoadModel(RwClumpNode clump, bool tagCollision = false)
        {
            if ( !mCanRender )
                return;

            // only tag collision when loading a whole scene, a clump picked directly in the tree is always shown
            bool collision = tagCollision && clump.IsCollision;

            Console.WriteLine( "geometry count: {0}", clump.GeometryCount );

            for ( var atomicIndex = 0; atomicIndex < clump.Atomics.Count; atomicIndex++ )
            {
                RwAtomicNode atomic = clump.Atomics[atomicIndex];
                Console.WriteLine( "processing draw call: {0}", atomicIndex );
                Console.WriteLine( "geo:{0}\t frame:{1}\t flag1:{2}\t flag2{3}\t", atomic.GeometryIndex, atomic.FrameIndex, atomic.Flag1, atomic.Flag2 );

                var geom = clump.GeometryList[atomic.GeometryIndex];
                var frame = clump.FrameList[atomic.FrameIndex];

                LoadGeometry(geom, frame.WorldTransform, collision);
            }
        }

        public void LoadGeometry(RwGeometryNode geom, SN.Matrix4x4 transform, bool collision = false)
        {
            if ( !mCanRender )
                return;

            // fingerprint by size and world placement so a model already in the scene isn't loaded twice
            string geometryKey = GetGeometryKey( geom, transform );
            if ( mSkipDuplicateGeometry && mGeometryKeys.Contains( geometryKey ) )
                return;
            mGeometryKeys.Add( geometryKey );

            if (geom.MeshListNode == null)
            {
                geom.MeshListNode = new RwMeshListNode(geom);
            }

            Console.WriteLine( geom.MeshListNode.MeshCount );
            Console.WriteLine( geom.MeshListNode.PrimitiveType );

            Vertex[] vertices = new Vertex[geom.VertexCount];
            int[][] allIndices = new int[geom.MeshListNode.MeshCount][];
            int[] allIndicesCount = new int[geom.MeshListNode.MeshCount];
            int[] fullIndices = new int[geom.TriangleCount * 3];

            for ( int i = 0; i < geom.TriangleCount; i++ )
            {
                fullIndices[i * 3 + 0] = geom.Triangles[i].A;
                fullIndices[i * 3 + 1] = geom.Triangles[i].B;
                fullIndices[i * 3 + 2] = geom.Triangles[i].C;
            }

            // remap the vertices
            for ( int i = 0; i < geom.VertexCount; i++ )
            {
                // set the new interleaved vertex
                Vertex vtx = new Vertex
                {
                    Pos = new BasicVec3( SN.Vector3.Transform( geom.Vertices[i], transform ) )
                };

                if ( geom.HasNormals )
                    vtx.Nrm = new BasicVec3( SN.Vector3.TransformNormal( geom.Normals[i], transform ) );

                if ( geom.TextureCoordinateChannelCount > 0 )
                    vtx.Tex = new BasicVec2( geom.TextureCoordinateChannels[0][i] );

                if ( geom.HasColors )
                    vtx.Col = new BasicVec4( geom.Colors[i] );
                else
                    vtx.Col = new BasicVec4( 1, 1, 1, 1 );

                vertices[i] = vtx;
                TrackVertex( vtx.Pos );
            }

            for ( int i = 0; i < geom.MeshListNode.MeshCount; i++ )
            {
                allIndices[i] = geom.MeshListNode.MaterialMeshes[i].Indices;
                allIndicesCount[i] = geom.MeshListNode.MaterialMeshes[i].IndexCount;
            }

            Console.WriteLine( "tex channels: " + geom.TextureCoordinateChannelCount );
            Console.WriteLine( "num materials: " + geom.MaterialCount );

            for ( int m = 0; m < geom.MaterialCount; m++ )
                Console.WriteLine( "material: {0}", geom.Materials[m] );

            // setup the vbo
            int vbo = GL.GenBuffer();
            GL.BindBuffer( BufferTarget.ArrayBuffer, vbo );
            GL.BufferData( BufferTarget.ArrayBuffer, sizeof( float ) * 12 * vertices.Length, vertices, BufferUsageHint.StaticDraw );

            // setup the ibo
            int[] ibo = new int[allIndices.Length];
            GL.GenBuffers( allIndices.Length, ibo );
            for ( int i = 0; i < ibo.Length; i++ )
            {
                GL.BindBuffer( BufferTarget.ElementArrayBuffer, ibo[i] );
                GL.BufferData( BufferTarget.ElementArrayBuffer, sizeof( int ) * allIndices[i].Length, allIndices[i], BufferUsageHint.StaticDraw );
            }

            // setup the vertex attribs
            GL.VertexAttribPointer( 0, 3, VertexAttribPointerType.Float, false, sizeof( float ) * 12, 0 );
            GL.VertexAttribPointer( 1, 3, VertexAttribPointerType.Float, false, sizeof( float ) * 12, ( sizeof( float ) * 3 ) );
            GL.VertexAttribPointer( 2, 2, VertexAttribPointerType.Float, false, sizeof( float ) * 12, ( sizeof( float ) * 6 ) );
            GL.VertexAttribPointer( 3, 4, VertexAttribPointerType.Float, false, sizeof( float ) * 12, ( sizeof( float ) * 8 ) );

            // get the mesh color from the material
            // get the texture name if there's a texture assigned
            string[] texname = new string[geom.MeshListNode.MeshCount];
            Color[] colors = new Color[geom.MeshListNode.MeshCount];
            for ( int i = 0; i < texname.Length; i++ )
            {
                texname[i] = geom.MaterialCount > 0 && geom.Materials[geom.MeshListNode.MaterialMeshes[i].MaterialIndex].IsTextured ?
                        geom.Materials[geom.MeshListNode.MaterialMeshes[i].MaterialIndex].TextureReferenceNode.Name
                        : string.Empty;

                colors[i] = geom.MaterialCount > 0
                    ? geom.Materials[geom.MeshListNode.MaterialMeshes[i].MaterialIndex].Color
                    : Color.White;
            }

            // pick a blend mode per mesh
            MeshBlendMode[] blend = new MeshBlendMode[texname.Length];
            for ( int i = 0; i < blend.Length; i++ )
            {
                int blendId = geom.MaterialCount > 0
                    ? GetMaterialBlendId( geom.Materials[geom.MeshListNode.MaterialMeshes[i].MaterialIndex] )
                    : -1;

                if ( blendId >= 0 )
                    blend[i] = BlendModeFromId( blendId );
                else if ( ScalePS2Alpha( colors[i].A ) < 255 || ( texname[i] != string.Empty && mTransparentTextures.Contains( texname[i] ) ) )
                    blend[i] = MeshBlendMode.Alpha;
            }

            // the uv animation each mesh plays, if any
            string[] uvAnim = new string[texname.Length];
            for ( int i = 0; i < uvAnim.Length; i++ )
            {
                if ( geom.MaterialCount > 0 )
                    uvAnim[i] = GetMaterialUvAnimationName( geom.Materials[geom.MeshListNode.MaterialMeshes[i].MaterialIndex] );
            }

            // mesh centers, used to sort transparent meshes
            Vector3[] centers = new Vector3[allIndices.Length];
            bool[] flat = new bool[allIndices.Length];
            for ( int i = 0; i < centers.Length; i++ )
            {
                Vector3 min = new Vector3( float.MaxValue );
                Vector3 max = new Vector3( float.MinValue );
                int count = Math.Min( allIndicesCount[i], allIndices[i].Length );
                bool any = false;

                for ( int k = 0; k < count; k++ )
                {
                    int idx = allIndices[i][k];
                    if ( idx < 0 || idx >= vertices.Length )
                        continue;

                    Vector3 p = new Vector3( vertices[idx].Pos.X, vertices[idx].Pos.Y, vertices[idx].Pos.Z );
                    min = Vector3.ComponentMin( min, p );
                    max = Vector3.ComponentMax( max, p );
                    any = true;
                }

                centers[i] = any ? ( min + max ) * 0.5f : Vector3.Zero;

                // a mesh that is flat along any axis is a decal (shadow quads, floor patches), see the blended pass
                Vector3 size = max - min;
                flat[i] = any && ( size.X < 1f || size.Y < 1f || size.Z < 1f );
            }

            // add the render model to the list
            mModels.Add( new GpuModel( vbo, ibo, geom.VertexCount, allIndicesCount, texname, colors,
                geom.MeshListNode.PrimitiveType == RwPrimitiveType.TriangleStrip, blend, centers, flat, collision, uvAnim ) );
        }

        private HashSet<string> mGeometryKeys = new HashSet<string>();
        private bool mSkipDuplicateGeometry;

        private static string GetGeometryKey(RwGeometryNode geom, SN.Matrix4x4 transform)
        {
            SN.Vector3 center = SN.Vector3.Transform( geom.BoundingSphere.Center, transform );

            return string.Format( "{0}|{1}|{2}|{3}|{4}|{5}", geom.VertexCount, geom.TriangleCount,
                ( long )Math.Round( center.X * 10f ), ( long )Math.Round( center.Y * 10f ), ( long )Math.Round( center.Z * 10f ),
                ( long )Math.Round( geom.BoundingSphere.Radius * 10f ) );
        }

        /// <summary>
        /// Adds another scene (eg. an RMD prop next to a field's .rws) to what is currently loaded, skipping any geometry
        /// that is already there. Models are placed using their own frame transforms.
        /// </summary>
        public void LoadExtraScene(RmdScene rmdScene)
        {
            if ( !mCanRender )
                return;

            LoadTextures( rmdScene.TextureDictionary );
            LoadUvAnimations( rmdScene );

            mSkipDuplicateGeometry = true;
            try
            {
                foreach ( var clump in rmdScene.Clumps )
                    LoadModel( clump );
            }
            finally
            {
                mSkipDuplicateGeometry = false;
            }
        }

        public void LoadScene(RmdScene rmdScene)
        {
            if ( !mCanRender )
                return;

            Console.WriteLine("loading scene");
            Console.WriteLine("num textures: {0} ", rmdScene.TextureDictionary != null ? rmdScene.TextureDictionary.TextureCount : 0);

            LoadTextures(rmdScene.TextureDictionary);
            LoadUvAnimations(rmdScene);

            foreach (var clump in rmdScene.Clumps)
            {
                LoadModel( clump, true );
            }
        }

        public void DeleteScene()
        {
            if ( !mCanRender )
                return;

            Console.WriteLine("deleting scene");
            EndBind();
            Console.WriteLine("unbind models");
            GL.BindTexture(TextureTarget.Texture2D, 0);
            Console.WriteLine("unbind textures");
            mCurrentTextureId = mCurrentGpuModelId = 0;

            // death to the textures
            int[] texIds = new int[mTexLookup.Count];
            mTexLookup.Values.CopyTo(texIds, 0);
            for (int i = 0; i < mTexLookup.Count; i++)
            {
                Console.WriteLine("deleting texture: " + i);
                GL.DeleteTexture(texIds[i]);
            }

            // death to models
            for (int i = 0; i < mModels.Count; i++)
            {
                Console.WriteLine("deleting model: " + i);

                if(mModels[i].Vbo != 0)
                    GL.DeleteBuffer(mModels[i].Vbo);

                if(mModels[i].Ibo[0] != 0)
                    GL.DeleteBuffers(mModels[0].Ibo.Length, mModels[i].Ibo);
            }

            mModels.Clear();
            mTexLookup.Clear();
            mTransparentTextures.Clear();
            mVertexPositions.Clear();
            mGeometryKeys.Clear();
            mUvAnims.Clear();
            mAnimClock.Restart();
        }

        public void DisposeViewer()
        {
            Console.WriteLine("disposing");
            Application.Idle -= OnApplicationIdle;
            DeleteScene();
            mShaderProg.Delete();
            mIsViewReady = false;
        }

        private void BeginBind(GpuModel data)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, data.Vbo);
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, sizeof(float) * 12, 0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, sizeof(float) * 12, sizeof(float) * 3);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, sizeof(float) * 12, sizeof(float) * 6);
            GL.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, sizeof(float) * 12, sizeof(float) * 8);
        }

        private void SubBind(GpuModel data, int first, int last, bool transparent)
        {
            for (int i = first; i < last; i++)
            {
                if ((data.Blend[i] != MeshBlendMode.Opaque) != transparent)
                    continue;

                int isTextured = 0;

                if (data.Tid[i] != string.Empty)
                {
                    bool texPresent = mTexLookup.TryGetValue( data.Tid[i], out int texIdx );

                    if (texPresent)
                    {
                        isTextured = 1;
                        FullTexBind(texIdx);
                    }
                }

                // set shader vars
                mShaderProg.SetUniform("diffuseColor", data.Color[i]);
                mShaderProg.SetUniform("isTextured", isTextured);

                // scroll the texture if the material has a uv animation, otherwise leave the coordinates alone
                Matrix4 uvTransform = Matrix4.Identity;
                if (data.UvAnim != null && !string.IsNullOrEmpty(data.UvAnim[i]) && mUvAnims.TryGetValue(data.UvAnim[i], out UvAnimation uvAnimation))
                    uvTransform = uvAnimation.GetMatrix(mAnimTime * UvSpeeds[mUvSpeedIndex], mUvFlipU, mUvFlipV);
                mShaderProg.SetUniform("uvTran", uvTransform);

                // bind model
                GL.BindBuffer(BufferTarget.ArrayBuffer, data.Vbo);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, data.Ibo[i]);
                GL.DrawElements(data.Strip ? BeginMode.TriangleStrip : BeginMode.Triangles, data.Ic[i], DrawElementsType.UnsignedInt, 0);
            }
        }

        private void EndBind()
        {
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        }

        private void FullBind(GpuModel data, int first, int last, bool transparent) // for optimization
        {
            if (data.Vbo == 0)
                return;

            if (mCurrentGpuModelId != data.Vbo)
            {
                EndBind();  // incase the previous was a SubBind
                BeginBind(data);
                mCurrentGpuModelId = data.Vbo;
            }

            SubBind(data, first, last, transparent);
        }

        private void FullTexBind(int texture)
        {
            if (texture == 0 || texture == mCurrentTextureId)
                return;

            GL.BindTexture(TextureTarget.Texture2D, texture);
            mCurrentTextureId = texture;
        }

        private void ModelViewerResize(object sender, EventArgs e)
        {
            if (!mIsViewReady || !mCanRender )
                return;

            GL.Viewport(0, 0, mViewerCtrl.Width, mViewerCtrl.Height);
        }

        private void ModelViewerPaint(object sender, PaintEventArgs e)
        {
            if (!mIsViewReady || !mViewerCtrl.Visible || !mCanRender || !mViewerCtrl.IsHandleCreated)
                return;

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            mShaderProg.Bind();
            UpdateClipPlanes();
            mCamera.Bind( mShaderProg, mViewerCtrl );
            mTransform = Matrix4.CreateTranslation( mTp ) * Matrix4.CreateFromQuaternion( Quaternion.FromEulerAngles( 0, 0, 0 ) ) * Matrix4.CreateScale( new Vector3( 1.0f, 1.0f, 1.0f ) );
            mShaderProg.SetUniform( "tran", mTransform );
            mAnimTime = mAnimClock.Elapsed.TotalSeconds;

            if (mModels.Count > 0)
            {
                // opaque meshes first, with depth writes
                GL.Disable(EnableCap.Blend);
                GL.DepthMask(true);
                for (int i = 0; i < mModels.Count; i++)
                    if (mDebugView != 2 && (mShowCollision || !mModels[i].Collision)) FullBind(mModels[i], 0, mModels[i].Ibo.Length, false);

                mTransparentDraws.Clear();
                for (int i = 0; i < mModels.Count; i++)
                {
                    GpuModel model = mModels[i];
                    if (!mShowCollision && model.Collision)
                        continue;
                    for (int j = 0; j < model.Blend.Length; j++)
                    {
                        if (model.Blend[j] != MeshBlendMode.Opaque)
                        {
                            bool noWrite = model.Blend[j] == MeshBlendMode.Additive || model.Flat[j] || model.Color[j].A < 240;
                            mTransparentDraws.Add(new TransparentDraw { Model = i, Mesh = j, NoWrite = noWrite, DistSq = (model.Center[j] - mCamera.Position).LengthSquared });
                        }
                    }
                }

                mTransparentDraws.Sort((a, b) =>
                {
                    int c = a.NoWrite.CompareTo(b.NoWrite);
                    if (c != 0) return c;
                    c = b.DistSq.CompareTo(a.DistSq);
                    if (c != 0) return c;
                    c = a.Model.CompareTo(b.Model);
                    return c != 0 ? c : a.Mesh.CompareTo(b.Mesh);
                });

                // pull blended meshes towards the camera so decals like shadows don't z fight with the floor
                GL.Enable(EnableCap.Blend);
                if (mUsePolyOffset) GL.Enable(EnableCap.PolygonOffsetFill);
                GL.PolygonOffset(-1f, -1f);
                foreach (var draw in mTransparentDraws)
                {
                    // additive doesn't write depth
                    if (mModels[draw.Model].Blend[draw.Mesh] == MeshBlendMode.Additive)
                    {
                        GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.One);
                        GL.DepthMask(false);
                    }
                    else
                    {
                        GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                        GL.DepthMask(!draw.NoWrite);
                    }

                    if (mDebugView != 1) FullBind(mModels[draw.Model], draw.Mesh, draw.Mesh + 1, true);
                }

                GL.Disable(EnableCap.PolygonOffsetFill);
                GL.DepthMask(true);
                GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            }

            mViewerCtrl.SwapBuffers();
        }
    }
}
