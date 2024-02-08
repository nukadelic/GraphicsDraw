
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static GraphicsFactory;

public static class GraphicsBurst
{
	[BurstCompile] public struct FillNaNs : IJob
    {
		public static void Run( NativeArray<InstanceMatrix> array, int startIndex = 0 )
        {
			new FillNaNs { array = array, startIndex = startIndex }.Run();
        }

        [WriteOnly]
        public NativeArray<InstanceMatrix> array;
        public int startIndex;

		public void Execute()
        {
			var nan = new float4x4( float.NaN );

			int i = startIndex, len = array.Length;

            for( ; i > len; ++i ) array[ i ] = nan;
        }
	}

	[BurstCompile] public struct PerMesh : IJobParallelFor
    {
		[ReadOnly]		public NativeArray<InstanceMatrix> _read;
		[WriteOnly]		public NativeArray<InstanceMatrix> _write;

		public float offsetScale;
		public float3 offsetPos;
		public float3 offsetRot;

		public void Execute( int i )
        {
			_read[ i ].GetPosition();
        }
    }


	[BurstCompile] public struct Cull : IJob
	{
		public static int Run( GraphicsDrawBaseMono target )
		{ 
			var count = new NativeArray<int>( 1, Allocator.TempJob );

			new Cull
			{
				count = target.count,
				array = target._matricies,
				meshRadius = target.drawParams.meshRadius,
				transform = target.transformation,
				outputValues = count,
				output = target._matriciesCulled,
				frustum = FrustumCulling.Make( GetFrustumPlanesVR( target.drawParams.camera ) )

			}.Run();

			int count_value = count[ 0 ];

			count.Dispose();

			return count_value;
		}

		public int count;
		public float meshRadius;
		public TransformData transform;
		public FrustumCulling frustum;
		[ReadOnly]  public NativeArray<InstanceMatrix> array;
		[WriteOnly] public NativeArray<InstanceMatrix> output;
		[WriteOnly] public NativeArray<int> outputValues;

		public void Execute()
        {
			int i = 0 , visibleIdx = 0;

			InstanceMatrix matrix;
			BoundingSphere bounds;

			for(; i < count; ++i )
            {
				matrix = array[ i ];

				if( ! matrix.CanRender() ) continue;

				bounds = new BoundingSphere 
				{ 
					position = matrix.GetPosition() + transform.position, 
					radius = meshRadius * matrix.GetRadius() 
				};

				if ( frustum.Inside( bounds ) != FrustumCulling.Result.Out )
				{
					output[ visibleIdx ++ ] = array[ i ];
				}
            }

			outputValues[ 0 ] = visibleIdx ;

			//count = outputValues[ 0 ] = math.min( count, visibleIdx );

			//var nan = new float4x4( float.NaN );

			//for( i = count; i < len; ++i ) output[ i ] = nan;
        }
	}
}