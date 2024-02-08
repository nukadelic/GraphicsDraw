using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Camera;

public static class GraphicsFactory
{
    public static void SetBuffer( Mesh mesh, int count, ref ComputeBuffer buffer )
    {
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        args[0] = (uint) mesh.GetIndexCount(0);
        args[1] = (uint) count;
        args[2] = (uint) mesh.GetIndexStart(0);
        args[3] = (uint) mesh.GetBaseVertex(0);

        if ( buffer == null )
        {
            buffer = new ComputeBuffer( 1, args.Length * sizeof( uint ), ComputeBufferType.IndirectArguments );
        }

        buffer.SetData( args );
    }

    [System.Serializable] public class DebugParams
    {
        public bool showLogs = false;
        [HideInInspector] public float execTime = 0;
        public string benchmark = "-";
    }

    public enum GraphicsDrawMode { InstancedIndirect, RenderMeshPrimitives }

    [System.Serializable] public class DrawParamsII
    {
        public Mesh mesh = null;
        public Material material = null;
        public float meshRadius = 1;
        public GraphicsDrawMode drawMode = GraphicsDrawMode.InstancedIndirect;
        public int submeshIndex = 0;
        public ShadowCastingMode castShadows = ShadowCastingMode.Off;
        public bool receiveShadows = false;
        [Range(0, 11)] public int layer = 0;
        public bool occlusionCulling = false;
        public Camera camera = null;
        public LightProbeUsage lightProbeUsage = LightProbeUsage.Off;
        public LightProbeProxyVolume lightProbeProxyVolume = null;
        [Header("RenderMeshPrimitives")]
        public uint renderingLayerMask = 1;
        public int rendererPriority = 0;
        public ReflectionProbeUsage reflectionProbeUsage = ReflectionProbeUsage.Off;
        public MotionVectorGenerationMode motionVectorMode = MotionVectorGenerationMode.ForceNoMotion;

        public DrawParamsII Clone()
        {
            return new DrawParamsII
            {
                mesh = mesh, 
                material = material,
                meshRadius = meshRadius, 
                drawMode = drawMode,
                submeshIndex = submeshIndex,
                castShadows = castShadows,
                receiveShadows = receiveShadows,
                layer = layer,
                occlusionCulling = occlusionCulling,
                camera = camera,
                lightProbeUsage = lightProbeUsage,
                lightProbeProxyVolume = lightProbeProxyVolume,
                renderingLayerMask = renderingLayerMask,
                rendererPriority = rendererPriority,
                reflectionProbeUsage = reflectionProbeUsage,
                motionVectorMode = motionVectorMode
            };
        }

        [HideInInspector] public int meshTriangleCount = 0;

        public bool cullingEnabled => occlusionCulling && camera;

        public bool IsValid() => mesh != null && material != null;

        public void Validate()
        {
            if( mesh != null )
            {
                if( submeshIndex < 0 ) submeshIndex = 0;
                if( submeshIndex > mesh.subMeshCount - 1 )
                {
                    submeshIndex = mesh.subMeshCount - 1;
                    Debug.LogWarning("subMeshCount = " + mesh.subMeshCount );
                }

                if( ! mesh.isReadable ) Debug.Log( "Mesh is not read/write enabled, stats will not be accurate");

                else meshTriangleCount = mesh.triangles.Length / 3;
            }
        }
    }



    public static void DrawMeshInstancedIndirect( this DrawParamsII drawParams, Bounds bn, ComputeBuffer bf, MaterialPropertyBlock mp )
    {
        Graphics.DrawMeshInstancedIndirect
        (
            drawParams.mesh,
            drawParams.submeshIndex,
            drawParams.material,
            bn, bf, 0, mp,
            drawParams.castShadows,
            drawParams.receiveShadows,
            drawParams.layer,
            Application.isPlaying ? drawParams.camera : null,
            drawParams.lightProbeUsage,
            drawParams.lightProbeProxyVolume
        );
    }

    public static void RenderMeshPrimitives(  this DrawParamsII drawParams, Bounds bn, MaterialPropertyBlock mp, int count )
    {
        var renderParams = new RenderParams
        { 
            camera = Application.isPlaying ? drawParams.camera : null,
            layer = drawParams.layer,
            lightProbeProxyVolume = drawParams.lightProbeProxyVolume,
            lightProbeUsage = drawParams.lightProbeUsage,
            material = drawParams.material,
            matProps = mp,
            motionVectorMode = drawParams.motionVectorMode,
            receiveShadows = drawParams.receiveShadows,
            reflectionProbeUsage = drawParams.reflectionProbeUsage,
            rendererPriority = drawParams.rendererPriority,
            renderingLayerMask = drawParams.renderingLayerMask,
            shadowCastingMode = drawParams.castShadows,
            worldBounds = bn
        };

        Graphics.RenderMeshPrimitives( renderParams, drawParams.mesh, drawParams.submeshIndex, count );
    }

    public struct InstanceMatrix
    {
        // https://github.com/needle-mirror/com.unity.mathematics/blob/master/Unity.Mathematics/matrix.cs

        public float4x4 matrix;

        public static int Size() => sizeof(float) * 4 * 4;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TRS( float3 p , quaternion r, float3 s ) => matrix = float4x4.TRS( p, r, s );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InstanceMatrix SetVisible( bool v ) { matrix.c3 = new float4( GetPosition() , v ? 1 : 0 ); return this; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsVisible() => matrix.c3.w > 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanRender() => IsVisible() && ! IsNanOrZero();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 GetPosition() => new float3( matrix.c3.x , matrix.c3.y , matrix.c3.z );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPosition( float3 p ) => matrix.c3 = new float4( p.x, p.y, p.z, matrix.c3.w );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 GetScale() => math.sqrt( new float3( math.lengthsq(matrix[0].xyz), math.lengthsq(matrix[1].xyz), math.lengthsq(matrix[2].xyz)) );
        //public float3 GetScale() => math.mul( matrix , new float4(1, 1, 1, 0) ).xyz;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNanOrZero() => matrix.c0.x == float.NaN || matrix.Equals( float4x4.zero );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetRadius() { var s = GetScale(); return math.max( math.max( s.x, s.y ) , s.z ); }

        public static implicit operator InstanceMatrix( float4x4 m ) => new InstanceMatrix() { matrix = m };
    }

    public struct TransformData : System.IEquatable<TransformData>
    {
        public float3 position;
        public quaternion rotation;
        public float3 scale;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TransformData other)
        {
            bool3 b1 = position == other.position, b2 = scale == other.scale;
            bool4 b3 = rotation.value == other.rotation.value;
            return b1.x && b1.y && b1.z && b2.x && b2.y && b2.z && b3.x && b3.y && b3.z && b3.w;
        }

        public void Identity()
        {
            position = float3.zero;
            rotation = quaternion.identity;
            scale = new float3(1);
        }
    }

    public struct FrustumCulling
    {
        // https://forum.unity.com/threads/frustumplanes-fromcamera-garbage.673114/#post-4506769

        public float4 Left;
        public float4 Right;
        public float4 Down;
        public float4 Up;
        public float4 Near;
        public float4 Far;

        public static FrustumCulling Make( Plane[] planes )
        {
            return new FrustumCulling
            {
                Left    = new float4( planes[ 0 ].normal,  planes[ 0 ].distance ) ,
                Right   = new float4( planes[ 1 ].normal,  planes[ 1 ].distance ) ,
                Down    = new float4( planes[ 2 ].normal,  planes[ 2 ].distance ) ,
                Up      = new float4( planes[ 3 ].normal,  planes[ 3 ].distance ) ,
                Near    = new float4( planes[ 4 ].normal,  planes[ 4 ].distance ) ,
                Far     = new float4( planes[ 5 ].normal,  planes[ 5 ].distance ) ,
            };
        }
        public enum Result { Out, In, Partial };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result Inside( BoundingSphere sphere )
        {
            float4 center = new float4( sphere.position , 1 );
            
            var lDistance = math.dot( Left,  center );
            if( lDistance < - sphere.radius ) return Result.Out;
            
            var rDistance = math.dot( Right, center );
            if( rDistance < - sphere.radius ) return Result.Out;
            
            var dDistance = math.dot( Down,  center );
            if( dDistance < - sphere.radius ) return Result.Out;
            
            var uDistance = math.dot( Up,    center );
            if( uDistance < - sphere.radius ) return Result.Out;
            
            //var nDistance = math.dot( Near,  center );
            //if( nDistance < - sphere.radius ) return Result.Out;
            
            //var fDistance = math.dot( Far,   center );
            //if( fDistance < - sphere.radius ) return Result.Out;
            
            var lIn = lDistance > sphere.radius;
            var rIn = rDistance > sphere.radius;
            var dIn = dDistance > sphere.radius;
            var uIn = uDistance > sphere.radius;
            //var nIn = nDistance > sphere.radius;
            //var fIn = fDistance > sphere.radius;
            
            if( lIn && rIn && dIn && uIn /*&& nIn && fIn*/ ) return Result.In;

            return Result.Partial;
        }
    }

    public static Plane[] GetFrustumPlanesVR( Camera camera, float z = 10f )
    {
        Vector3[] cornersL = new Vector3[4];
        Vector3[] cornersR = new Vector3[4];

        camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), z, MonoOrStereoscopicEye.Left , cornersL);
        camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), z, MonoOrStereoscopicEye.Right, cornersR);

        Vector3[] corners = { cornersL[0], cornersL[1], cornersR[2], cornersR[3] };

        Vector3[][] triangles = // red , green , black , white 
        {
            new Vector3[3] { corners[0], Vector3.zero, corners[1] }, // LEFT 
            new Vector3[3] { corners[2], Vector3.zero, corners[3] }, // RIGHT 
            new Vector3[3] { corners[3], Vector3.zero, corners[0] }, // BOTTOM
            new Vector3[3] { corners[1], Vector3.zero, corners[2] }, // TOP
        };

        Plane[] planes = new Plane[4];

        for (var i = 0; i < 4; ++i) planes[i] = new Plane(triangles[i][0], triangles[i][1], triangles[i][2]);

        var t = camera.transform;

        planes = new Plane[] { // [0] = Left, [1] = Right, [2] = Bottom, [3] = Top, [4] = Near , [5] = Far
            new Plane( t.TransformDirection( planes[ 0 ].normal ) , t.position ),
            new Plane( t.TransformDirection( planes[ 1 ].normal ) , t.position ),
            new Plane( t.TransformDirection( planes[ 2 ].normal ) , t.position ),
            new Plane( t.TransformDirection( planes[ 3 ].normal ) , t.position ),
            new Plane( t.forward , t.position + t.forward * camera.nearClipPlane ),
            new Plane( t.forward , t.position + t.forward * camera.farClipPlane )
        };

        return planes;
    }
}
