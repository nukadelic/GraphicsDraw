
using System.Collections.Generic;
using UnityEngine;

public class GraphicsDrawStats : MonoBehaviour
{
    #region Static ------------------------------------------------------------

    static List<GraphicsDrawBaseMono> drawers = new List<GraphicsDrawBaseMono>();
    public static void Add( GraphicsDrawBaseMono item ) => drawers.Add( item );
    public static void Remove( GraphicsDrawBaseMono item ) => drawers.Remove( item );

    #endregion

    // -----------------------------------------------

    public bool showGUI = false;

    // -----------------------------------------------


    public string[] GetLines()
    {
        List<string> lines = new List<string>();

        // -----------------------------------------------

        int disabledCount = 0;

        int triTotal = 0;
        float extTotal = 0;

        for (var i = 0; i < drawers.Count; ++i)
        {
            var drawer = drawers[i];

            if ( drawer.active )
            {
                float ext = drawer.debugParams.execTime;
                int tri = drawer.drawParams.meshTriangleCount * drawer.drawCount;

                triTotal += tri;
                extTotal += ext;

                lines.Add( $"{i}. EXEC: {ext:N2} ms \t TRIS: {format(tri)}" );
            }
            else disabledCount ++ ;
        }

        if( disabledCount > 0 ) lines.Add("Disabled count : " + disabledCount );

        lines.Add( $"(totals) EXEC: {extTotal:N2} ms \t TRIS: {format(triTotal)}");

#if UNITY_EDITOR // https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/UnityStats.bindings.cs
        var trigCountMono = UnityEditor.UnityStats.triangles;
#endif

        return lines.ToArray();
    }
    string format(int value)
    {
        if (value > 1e6) return Mathf.RoundToInt(value / 1e5f) / 10f + "m";
        if (value > 1e3) return Mathf.RoundToInt(value / 1e2f) / 10f + "k";
        return value.ToString();
    }

    #region GUI ------------------------------------------------------------

    private void OnGUI()
    {
        if( ! showGUI ) return;

        var lines = GetLines();

        int w = 300, h = 120;
        //GUI.matrix = Matrix4x4.TRS( Vector3.zero, Quaternion.identity, Vector3.one * 0.9f );
        GUI.color = Color.black;
        GUILayout.BeginArea( new Rect(5, 5, w, h), GUI.skin.box );
        //GUILayout.Space(5);GUILayout.BeginVertical();GUILayout.Space(5);
        //foreach (var line in lines) GUILayout.Label(line);
        //GUILayout.Space(5);GUILayout.EndVertical();
        GUILayout.EndArea();
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(6, 6, w, h), GUI.skin.box);
        GUILayout.Space(6);GUILayout.BeginVertical();GUILayout.Space(6);
        foreach (var line in lines) GUILayout.Label(line); 
        GUILayout.Space(5);GUILayout.EndVertical();GUILayout.EndArea();
        GUI.matrix = Matrix4x4.identity;
    }

    #endregion
}
