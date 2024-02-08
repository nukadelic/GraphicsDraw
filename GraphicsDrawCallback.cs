
using UnityEngine;

public class GraphicsDrawCallback : GraphicsDrawBaseMono
{
    public Bounds calculatedBounds;

    public event System.Action<GraphicsDrawCallback> CalculateMatriciesCallback;

    public event System.Action<GraphicsDrawCallback> PostCullCallback;

    protected override void OnPostCull() => PostCullCallback?.Invoke( this );
    public override Bounds CalculateBounds() => calculatedBounds;
    public override void CalculateMatricies() => CalculateMatriciesCallback?.Invoke( this );
}
