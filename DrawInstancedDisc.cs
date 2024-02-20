
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static GraphicsFactory;

public class DrawInstancedDisc : GraphicsDrawBaseMono
{
    [System.Serializable]
    public struct Properties
    {
        public bool randomizeColor;

        public float radius;
        public float thickness;

        public float scale;
        [Range(0, 1)] public float scaleRatio;

        [Range(0.5f, 8)] public float distributionRadius;
        [Range(0.5f, 8)] public float distributionThickness;
        [Range(0, 1)] public float ratio;

        public uint seed;
    }

    public Properties properties = new Properties
    {
        randomizeColor = false,

        radius = 1f,
        thickness = 1f,

        scale = 1f,
        scaleRatio = 0f,

        distributionRadius = 1.7f,
        distributionThickness = 1.7f,
        ratio = 0f,

        seed = 777
    };

    #region Debug

    public int preview_drawCount;
    public int preview_matrixLen;
    public int preview_matrixCulledLen;
    //public Vector3[] preview_positions;
    public float3 preview_pos_last;

    private void OnDrawGizmos()
    {
        if( ! debugParams.showLogs ) return;

        if( ! _matricies.IsCreated || _matricies.Length < 1 ) return;

        if( drawCount > _matricies.Length ) return;
        
        bool culled = drawParams.cullingEnabled;

        var data = culled ? _matriciesCulled : _matricies;

        preview_drawCount = drawCount;
        preview_matrixCulledLen = _matriciesCulled.Length;
        preview_matrixLen = data.Length;
        //preview_positions = data.Select( x => (Vector3) x.position ).ToArray();
        preview_pos_last = data[ data.Length - 1 ].GetPosition();

        //float3 p = transform.position;
        //float r = drawParams.meshRadius;

        //for ( var i = 0; i < drawCount; ++i )
        //{
        //    Gizmos.color = data[ i ].IsVisible ? Color.green : Color.red;
        //    Gizmos.DrawWireSphere( p + data[ i ].position , data[ i ].GetRadius() * r );
        //}

        if( culled )
        {
            var planes = GetFrustumPlanesVR( drawParams.camera );

            var ct = drawParams.camera.transform;

            Gizmos.color = Color.red;
            Gizmos.DrawSphere( planes[0].ClosestPointOnPlane( ct.position + ct.forward * 15f ) , 1f );

            Gizmos.color = Color.green; 
            Gizmos.DrawSphere( planes[1].ClosestPointOnPlane( ct.position + ct.forward * 15f ) , 1f );

            Gizmos.color = Color.black;
            Gizmos.DrawSphere( planes[2].ClosestPointOnPlane( ct.position + ct.forward * 15f ) , 1f );

            Gizmos.color = Color.white; 
            Gizmos.DrawSphere( planes[3].ClosestPointOnPlane( ct.position + ct.forward * 15f ) , 1f );
        }

    }

    #endregion

    #region GraphicsDrawBaseMono

    public override Bounds CalculateBounds( )
    {
        return new Bounds( transform.position , Vector3.one * properties.radius * 2 );
	}

    public override void CalculateMatricies( )
    {
        new IDrawBurst { 
            array = _matricies, 
            data = properties, 
            transform = transformation, 
            count = count }
        .Schedule( count, 64 ).Complete();
    }

    public struct InstanceData
    {
        public static readonly int NameID = Shader.PropertyToID( "_PerInstanceData" );

        // ...

        public int index;

        public float4 color;

        // ...

        public static int Size() => sizeof( float ) * 5 ;
    }

    protected ComputeBuffer _dataBuffer;

    protected NativeArray<InstanceData> _data;

    protected override void DataInit( int size )
    {
        if ( _data.IsCreated ) _data.Dispose();

        _data = new NativeArray<InstanceData>( size , Allocator.Persistent );

        if (_dataBuffer != null) _dataBuffer.Release();

        _dataBuffer = new ComputeBuffer( size , InstanceData.Size() );

        _matPropBlock.SetBuffer( "_PerInstanceData" , _dataBuffer );
    }

    protected override void DataUpdate()
    {
        for (var i = 0; i < _data.Length; ++i )
        {
            float t = ( float ) i / _data.Length;

            _data[ i ] = new InstanceData 
            { 
                index = i,    
                color = (Vector4) Color.Lerp( Color.red, Color.green, t ) 
            };
        }

        _dataBuffer.SetData( _data );
    }

    protected override void DataDispose()
    {
        if( _data.IsCreated )
        {
            _data.Dispose();
        }

    }

    #endregion

    #region Burst

    [BurstCompile] struct IDrawBurst : IJobParallelFor
    {
        [WriteOnly]
        public NativeArray<InstanceMatrix> array;
        public Properties data;
        public TransformData transform;
        public int count;

        public void Execute( int i )
        {
            if( i > count - 1 ) return;

            float f, s, t;
            quaternion qi;
            float3 pi, si;

            uint state = data.seed + ( (uint) i );
            for( int k = 0; k < ( i % 5 ); ++k ) {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5; 
            }
            var rnd = new Unity.Mathematics.Random( state );

            // var rnd = new Unity.Mathematics.Random( (uint) ( data.seed + i ));

            //int i = 0 , len = array.Length;

            //for ( ; i < count ; ++i )
            {
                f = func(rnd.NextFloat(), data.distributionRadius, data.ratio);

                pi = new float3(0, rnd.NextFloat2Direction() * data.radius * f);

                t = func(rnd.NextFloat(), data.distributionThickness, 0);

                pi.x = (rnd.NextBool() ? 1 : -1) * (t * rnd.NextFloat()) * data.thickness;

                pi = math.mul(transform.rotation, pi);

                qi = math.mul(transform.rotation, rnd.NextQuaternionRotation());

                s = math.lerp(1, rnd.NextFloat() * 1.5f + 0.5f, data.scaleRatio);

                si = data.scale * s * transform.scale;

                array[ i ] = new InstanceMatrix { matrix = float4x4.TRS(pi, qi, si) };
            }
        }

        float func(float x, float k, float d)
        {
            // https://www.desmos.com/calculator/2mrqgrggry

            float f1 = math.pow(x, k);

            float f2 = 1 - math.lerp(f1, 1, f1);

            float f3 = f2 * (1 - d) + d;

            return f3;
        }
    }



    
    #endregion
}


/*
 
uint state = 7777;

Func<uint, int, string> str = (x, y) => Convert.ToString(x, y).ToUpper();
Action<uint> log = x => WriteLine($"{str(x, 2),32} : {str(x,8),11} : {str(x, 16),8} = {x}");
Action newline = () => WriteLine("\n");

log( uint.MaxValue );
log( state );                                 
newline();

for (int i = 0; i < 3; ++i)
{
    state ^= state << 13;     log(state);
    state ^= state >> 17;     log(state);
    state ^= state << 5;      log(state);     
    newline();
}

11111111111111111111111111111111 : 37777777777 : FFFFFFFF = 4294967295
                   1111001100001 :       17141 :     1E61 = 7777


      11110011000011111001100001 :   363037141 :  3CC3E61 = 63716961
      11110011000011111110000111 :   363037607 :  3CC3F87 = 63717255
 1111010010010111100111101100111 : 17222747547 : 7A4BCF67 = 2051788647


      11101001110010111101100111 :   351627547 :  3A72F67 = 61288295
      11101001110010111010110100 :   351627264 :  3A72EB4 = 61288116
 1110111010000101111100000110100 : 16720574064 : 7742F834 = 2000877620


  101000010001000111100000110100 :  5021074064 : 28447834 = 675575860
  101000010001000110110000010110 :  5021066026 : 28446C16 = 675572758
  100000110010011110111011010110 :  4062367326 : 20C9EED6 = 550104790

>
 
 */