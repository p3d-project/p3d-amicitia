using System.IO;

namespace AmicitiaLibrary.Graphics.RenderWare
{
    public enum RwMatFxType : uint
    {
        Null            = 0,
        BumpMap         = 1,
        EnvMap          = 2,
        BumpEnvMap      = 3,
        Dual            = 4,
        UVTransform     = 5,
        DualUVTransform = 6,
    }

    public class RwMatFxNode : RwNode
    {
        public RwMatFxType EffectType { get; set; }

        public float ReflectionCoefficient { get; set; }
        public bool UseFrameBufferAlpha { get; set; }
        public RwTextureReferenceNode EnvMapTexture { get; set; }

        public int SrcBlendMode { get; set; }
        public int DstBlendMode { get; set; }
        public RwTextureReferenceNode DualTexture { get; set; }

        internal RwMatFxNode( RwNodeFactory.RwNodeHeader header, BinaryReader reader )
            : base( header )
        {
            var data = reader.ReadBytes( (int)header.Size );
            using ( var ms = new System.IO.MemoryStream( data ) )
            using ( var r = new BinaryReader( ms ) )
            {
                if ( ms.Length < 8 ) return;
                EffectType = (RwMatFxType) r.ReadUInt32();
                var firstEffect = (RwMatFxType) r.ReadUInt32();

                if ( firstEffect == RwMatFxType.EnvMap )
                {
                    if ( ms.Position + 12 > ms.Length ) return;
                    ReflectionCoefficient = r.ReadSingle();
                    UseFrameBufferAlpha = r.ReadInt32() != 0;
                    bool hasTexture = r.ReadInt32() != 0;
                    if ( hasTexture && ms.Position < ms.Length )
                        EnvMapTexture = RwNodeFactory.GetNode<RwTextureReferenceNode>( this, r );
                }
                else if ( firstEffect == RwMatFxType.Dual )
                {
                    if ( ms.Position + 12 > ms.Length ) return;
                    SrcBlendMode = r.ReadInt32();
                    DstBlendMode = r.ReadInt32();
                    bool hasTexture = r.ReadInt32() != 0;
                    if ( hasTexture && ms.Position < ms.Length )
                        DualTexture = RwNodeFactory.GetNode<RwTextureReferenceNode>( this, r );
                }
                // remaining bytes (second effect slot etc) intentionally ignored
            }
        }

        protected internal override void WriteBody( BinaryWriter writer )
        {
            writer.Write( (uint) EffectType );
            writer.Write( (uint) EffectType );

            if ( EffectType == RwMatFxType.EnvMap )
            {
                writer.Write( ReflectionCoefficient );
                writer.Write( UseFrameBufferAlpha ? 1 : 0 );
                writer.Write( EnvMapTexture != null ? 1 : 0 );
                if ( EnvMapTexture != null )
                {
                    EnvMapTexture.Write( writer );
                }
            }
            else if ( EffectType == RwMatFxType.Dual )
            {
                writer.Write( SrcBlendMode );
                writer.Write( DstBlendMode );
                writer.Write( DualTexture != null ? 1 : 0 );
                if ( DualTexture != null )
                {
                    DualTexture.Write( writer );
                }
            }

            writer.Write( (uint) RwMatFxType.Null );
        }
    }
}
