using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static GraphicsFactory;

public class DrawInstancedSphere : GraphicsDrawBaseMono
{
    [System.Serializable] public struct Properties
    {
        public float radius;
        public bool insideSphere;
        [Range(0, 1)] public float insideRatio;
        public float minScale;
        public float maxScale;
        public uint seed;
    }

    public Properties properties = new Properties
	{
		maxScale = 0.2f,
		minScale = 0.1f,
		radius = 1,
		seed = 777
	};

    #region GraphicsDrawBaseMono

    public override Bounds CalculateBounds()
    {
        return new Bounds( transform.position , Vector3.one * properties.radius * 2 );
	}

    public override void CalculateMatricies()
    {
        new Burst { array = _matricies, data = properties, transform = transformation }.Run();
    }

    #endregion

    #region Burst

    [BurstCompile] struct Burst : IJob
    {
        [WriteOnly]
        public NativeArray<InstanceMatrix> array;
        public Properties data;
        public TransformData transform;

        public void Execute()
        {
            quaternion qi;
            float3 pi, si, vi;
            int len = array.Length, i = 0;
            float scaleSize = data.maxScale - data.minScale;
            var rnd = new Unity.Mathematics.Random(data.seed);

            for (; i < len; ++i)
            {
                vi = rnd.NextFloat3Direction();
                vi = math.mul(transform.rotation, vi);
                pi = vi * data.radius;
                if (data.insideSphere) pi *= math.clamp(math.lerp(f2(rnd.NextFloat()), 1, data.insideRatio), 0, 1);
                qi = math.mul(transform.rotation, rnd.NextQuaternionRotation());
                si = (rnd.NextFloat() * scaleSize + data.minScale) * transform.scale;
                array[i] = new InstanceMatrix { matrix = float4x4.TRS(pi, qi, si) };
            }
        }

        // See function graph plot : https://www.desmos.com/calculator/pzmgpc4kal
        float f1(float x) => 1 - (3 * x / 10f) / (math.log10(x / 2f));
        float f2(float x) => 1 - math.pow(x - 0.05f, 3f);
    }
    #endregion
}
