
using Unity.Collections;
using UnityEngine;
using static GraphicsFactory;

[ExecuteInEditMode]
[DefaultExecutionOrder(1000)]
public class GraphicsDrawBaseMono : MonoBehaviour
{
	public virtual Bounds CalculateBounds() => throw new System.NotImplementedException();
	public virtual void CalculateMatricies() => throw new System.NotImplementedException();

	public virtual bool allowExecInEditMode => true;
	bool canExecNow => Application.isPlaying || allowExecInEditMode;

	protected Bounds _bounds;
	protected ComputeBuffer _instancesBuffer;
	protected ComputeBuffer _argsBuffer;
	internal NativeArray<InstanceMatrix> _matricies;
	internal NativeArray<InstanceMatrix> _matriciesCulled;
	protected MaterialPropertyBlock _matPropBlock;

	[Tooltip("Will run the transform calcualtions every frame, use this if you plan to move or rotate this drawer")]
	public bool alwaysUpdate = true;
	
	public int count = 1000;

	public DebugParams debugParams;

	public int drawCount { get; protected set; } = 1000;

	public TransformData transformation => new TransformData
	{
		position = transform.position,
		rotation = transform.rotation,
		scale = transform.lossyScale
	};

	public DrawParamsII drawParams;

	public virtual bool ParamsAreValid() 
	{
		if( drawParams == null || ! drawParams.IsValid() ) return false;

		return true;
	}

	public virtual bool DataIsValid()
    {
		if ( ! _matricies.IsCreated ) return false;

		return true;
    }

	protected bool initialized = false;

	protected bool isEnabled = false;

	public bool active => isActiveAndEnabled && initialized && isEnabled && ParamsAreValid();

	public virtual void OnEnable()
	{
		if( ! canExecNow ) return;

		GraphicsDrawStats.Add( this );

		isEnabled = true;

		if( debugParams == null ) debugParams = new DebugParams();

		if( typeof( GraphicsDrawBaseMono ) == this.GetType() )
        {
			Debug.LogWarning("GraphicsDrawBaseMono is a base class and cannot be used on its own");
			if( Application.isPlaying ) Destroy( this );
			else DestroyImmediate( this );
			return;
        }

		if ( debugParams.showLogs ) Debug.Log("enabled");

		SetParams( drawParams );
	}

	public void SetParams( DrawParamsII value )
    {
		this.drawParams = value;

		BufferInit();

		BufferUpdate();
    }

	// for example if count is 888 and value is 256 , then set new count to 1024 to have some extra space

	static int bufferExtendValue = 256;


	void BufferInit()
	{
		if ( ! ParamsAreValid() ) return;

		DataPreInit();

		var t = Time.realtimeSinceStartup;

		int resized = ( 1 + count / bufferExtendValue ) * bufferExtendValue;  

		if ( _matricies.IsCreated ) _matricies.Dispose();

		_matricies = new NativeArray<InstanceMatrix>( resized , Allocator.Persistent );

		if( _matriciesCulled.IsCreated ) _matriciesCulled.Dispose();

		_matriciesCulled = new NativeArray<InstanceMatrix>( resized , Allocator.Persistent );

		if (_instancesBuffer != null) _instancesBuffer.Release();

		_instancesBuffer = new ComputeBuffer( resized , InstanceMatrix.Size() );

		if( _matPropBlock == null ) _matPropBlock = new MaterialPropertyBlock();

		_matPropBlock.SetBuffer( "_PerInstanceMatrix", _instancesBuffer );

		DataInit( resized );

		initialized = true;
			
		debugParams.execTime = ( ( Time.realtimeSinceStartup - t ) * 1000f );

		debugParams.benchmark = debugParams.execTime.ToString("N3") + " ms";

		if ( debugParams.showLogs ) Debug.Log( "buffer init " + debugParams.benchmark );
	}

	void BufferUpdate() // OnValidate , Update , OnEnable
	{
		if ( ! initialized || ! ParamsAreValid() || ! DataIsValid() ) return;

		var t = Time.realtimeSinceStartup;

		_bounds = CalculateBounds();
		
		CalculateMatricies();

		GraphicsBurst.FillNaNs.Run( _matricies , drawCount = count );

		if( drawParams.cullingEnabled ) drawCount = GraphicsBurst.Cull.Run( this );

		OnPostCull();

		// drawCount = PostCull( drawParams.cullingEnabled ? _matriciesCulled : _matricies , drawCount );

		SetBuffer( drawParams.mesh, drawCount, ref _argsBuffer );

		_instancesBuffer.SetData( drawParams.cullingEnabled ? _matriciesCulled : _matricies );

		DataUpdate();

		debugParams.benchmark = ( ( Time.realtimeSinceStartup - t ) * 1000f ).ToString("N3") + " ms";
	}
	protected virtual void DataPreInit() { }

	protected virtual void DataInit(int size) { }
	protected virtual void DataUpdate() { }
	protected virtual void DataDispose() { }

	protected virtual void OnPostCull() { }

	// Override this method to do additional culling 
	protected virtual int PostCull( NativeArray<InstanceMatrix> array, int count ) => count;

    protected virtual void OnValidate()
	{
		if( ! isEnabled || ! isActiveAndEnabled ) return;

		if ( ! ParamsAreValid() ) return;

		drawParams.Validate();

		if( ! canExecNow ) return;

		Validate();
	}

	internal void Validate( bool update = true )
    {	
		if ( count < 0 ) count = 0;

		drawCount = count;

		if ( count > _matricies.Length || ! initialized ) 
			
			BufferInit();

		if( update ) BufferUpdate();
	}

	internal bool manualUpdate = false;

	public virtual void Update()
	{
		if( ! canExecNow ) return;

		if ( ! initialized || ! ParamsAreValid( ) ) return;

		if( manualUpdate ) return;

		ManualUpdate();
	}

	public void ManualUpdate()
    {
		if ( alwaysUpdate ) BufferUpdate();

		if( drawParams.drawMode == GraphicsDrawMode.InstancedIndirect )

			drawParams.DrawMeshInstancedIndirect( _bounds, _argsBuffer, _matPropBlock );

		if( drawParams.drawMode == GraphicsDrawMode.RenderMeshPrimitives )

			drawParams.RenderMeshPrimitives( _bounds, _matPropBlock, drawCount );
    }

	public virtual void OnDisable()
	{
		if ( ! canExecNow ) return;

		GraphicsDrawStats.Remove( this );

		isEnabled = false;

		if (_instancesBuffer != null)
		{
			_instancesBuffer.Release();
			_instancesBuffer = null;
		}
		if (_argsBuffer != null)
		{
			_argsBuffer.Release();
			_argsBuffer = null;
		}

		if (_matricies.IsCreated )
		{
			_matricies.Dispose();
		}

		if( _matriciesCulled.IsCreated )
        {
			_matriciesCulled.Dispose();
        }

		DataDispose();

		initialized = false;
	}
}
