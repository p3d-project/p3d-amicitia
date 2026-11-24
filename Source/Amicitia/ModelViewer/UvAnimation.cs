using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;

namespace Amicitia.ModelViewer
{
    /// <summary>
    /// A looping RenderWare UV animation, used to scroll water, spray and clouds.
    /// 
    /// The library doesn't parse these, so this reads them from the raw body of the UV animation dictionary node.
    /// Each animation chunk is laid out as:
    ///   0x00  u32 version (0x100), u32 type (0x1C1), u32 key count, u32 flags, f32 duration
    ///   0x14  u32 unused, char[32] name, int[8] uv channel map (unused)
    ///   0x58  key frames, 32 bytes each: f32 time, f32[6] parameters, i32 previous key offset
    /// The parameters are rotation, x scale, y scale, skew, x offset and y offset.
    /// </summary>
    public class UvAnimation
    {
        private const int ChunkHeaderSize = 12;
        private const uint AnimationChunkId = 0x1B;
        private const uint UvAnimationTypeId = 0x1C1;

        private const int NameOffset = 0x18;
        private const int NameLength = 32;
        private const int KeyFramesOffset = 0x58;
        private const int KeyFrameSize = 32;
        private const int ParameterCount = 6;

        // the material plugin is: u32 count, u32 size, u32 version, u32 unused, then the animation name
        private const int MaterialPluginNameOffset = 16;

        private readonly float[] mTimes;
        private readonly float[][] mParameters;

        public string Name { get; }

        /// <summary>
        /// Length of one loop in seconds.
        /// </summary>
        public float Duration { get; }

        private UvAnimation(string name, float duration, float[] times, float[][] parameters)
        {
            Name = name;
            Duration = duration;
            mTimes = times;
            mParameters = parameters;
        }

        /// <summary>
        /// Reads every UV animation in the body of a UV animation dictionary node.
        /// </summary>
        public static List<UvAnimation> ParseDictionary(byte[] body)
        {
            var animations = new List<UvAnimation>();
            if (body == null)
                return animations;

            // the body is a list of chunks, a struct holding the count followed by one chunk per animation
            int position = 0;
            while (position + ChunkHeaderSize <= body.Length)
            {
                uint id = BitConverter.ToUInt32(body, position);
                int size = BitConverter.ToInt32(body, position + 4);
                int start = position + ChunkHeaderSize;

                if (size < 0 || start + size > body.Length)
                    break;

                if (id == AnimationChunkId)
                {
                    var animation = TryParse(body, start, size);
                    if (animation != null)
                        animations.Add(animation);
                }

                position = start + size;
            }

            return animations;
        }

        /// <summary>
        /// Reads the name of the UV animation a material plays from its UV animation plugin, or null if it has none.
        /// </summary>
        public static string ParseMaterialAnimationName(byte[] pluginData)
        {
            if (pluginData == null || pluginData.Length < MaterialPluginNameOffset + 1)
                return null;

            string name = ReadString(pluginData, MaterialPluginNameOffset, Math.Min(NameLength, pluginData.Length - MaterialPluginNameOffset));
            return name.Length > 0 ? name : null;
        }

        private static UvAnimation TryParse(byte[] data, int start, int size)
        {
            if (size < KeyFramesOffset)
                return null;

            uint type = BitConverter.ToUInt32(data, start + 4);
            int keyCount = BitConverter.ToInt32(data, start + 8);
            float duration = BitConverter.ToSingle(data, start + 16);

            if (type != UvAnimationTypeId || keyCount <= 0 || KeyFramesOffset + keyCount * KeyFrameSize > size)
                return null;

            string name = ReadString(data, start + NameOffset, NameLength);

            var times = new float[keyCount];
            var parameters = new float[keyCount][];

            for (int i = 0; i < keyCount; i++)
            {
                int key = start + KeyFramesOffset + i * KeyFrameSize;

                times[i] = BitConverter.ToSingle(data, key);
                parameters[i] = new float[ParameterCount];
                for (int p = 0; p < ParameterCount; p++)
                    parameters[i][p] = BitConverter.ToSingle(data, key + 4 + p * 4);
            }

            // fall back to the last key if the header has no usable duration
            if (!(duration > 0f))
                duration = times[keyCount - 1];

            return new UvAnimation(name, duration, times, parameters);
        }

        private static string ReadString(byte[] data, int offset, int maxLength)
        {
            int length = 0;
            while (length < maxLength && data[offset + length] != 0)
                length++;

            return Encoding.ASCII.GetString(data, offset, length);
        }

        /// <summary>
        /// Gets the texture coordinate transform at the given time in seconds. The animation loops forever.
        /// flipU and flipV reverse the scroll direction on that axis.
        /// </summary>
        public Matrix4 GetMatrix(double time, bool flipU = false, bool flipV = false)
        {
            double t = Duration > 0f ? time % Duration : 0.0;
            int last = mTimes.Length - 1;

            float[] p;
            if (last == 0 || t <= mTimes[0])
            {
                p = mParameters[0];
            }
            else if (t >= mTimes[last])
            {
                p = mParameters[last];
            }
            else
            {
                // find the key frames either side of the time and blend between them
                int next = 1;
                while (next < last && mTimes[next] < t)
                    next++;

                float t0 = mTimes[next - 1];
                float t1 = mTimes[next];
                float amount = t1 > t0 ? (float)((t - t0) / (t1 - t0)) : 0f;

                p = new float[ParameterCount];
                for (int i = 0; i < ParameterCount; i++)
                    p[i] = mParameters[next - 1][i] + (mParameters[next][i] - mParameters[next - 1][i]) * amount;
            }

            float theta = p[0], scaleX = p[1], scaleY = p[2], skew = p[3], offsetX = p[4], offsetY = p[5];

            if (flipU) offsetX = -offsetX;
            if (flipV) offsetY = -offsetY;

            Matrix4 matrix = Matrix4.CreateScale(scaleX, scaleY, 1f);

            if (skew != 0f)
            {
                Matrix4 shear = Matrix4.Identity;
                shear.M21 = skew;
                matrix = matrix * shear;
            }

            if (theta != 0f)
                matrix = matrix * Matrix4.CreateRotationZ(theta);

            return matrix * Matrix4.CreateTranslation(offsetX, offsetY, 0f);
        }
    }
}
