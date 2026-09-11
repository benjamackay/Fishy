using UnityEngine;
using UnityEngine.UI;

/// <summary>Resolution-independent UI icons; no font glyphs or external textures.</summary>
public class PhoneMenuIcon : MaskableGraphic
{
    public enum Kind { Profile, Quests, Inventory, Map, Back, Settings, Status }
    public Kind kind;
    VertexHelper mesh;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); mesh = vh;
        switch (kind)
        {
            case Kind.Profile:
                Disc(50, 28, 24); Rect(10, 81, 80, 19); Rect(23, 63, 54, 37); Disc(29, 81, 19); Disc(71, 81, 19); break;
            case Kind.Quests:
                Path(5, 22,18, 84,18, 84,94, 16,94, 16,18, 27,18);
                Path(6, 30,11, 40,11, 43,4, 54,4, 60,11, 70,11, 70,23, 30,23, 30,11);
                for (int y = 40; y <= 76; y += 18) { Path(6, 29,y, 34,y+5, 41,y-3); Path(6, 53,y, 71,y); } break;
            case Kind.Inventory:
                Path(5, 21,27, 17,14, 11,19, 10,82, 18,93, 82,93, 90,82, 89,19, 83,14, 79,27);
                Path(4, 32,18, 38,7, 61,7, 68,18);
                Path(5, 24,18, 76,18, 80,49, 71,56, 29,56, 20,49, 24,18);
                Path(4, 25,91, 25,72, 30,66, 70,66, 75,72, 75,91);
                Path(4, 26,78, 74,78); Path(6, 36,44, 36,63); Path(6, 65,44, 65,63); break;
            case Kind.Map:
                Path(4, 4,29, 33,18, 62,30, 96,21, 96,84, 65,96, 33,84, 4,96, 4,29);
                Path(4, 33,18, 33,84); Path(4, 65,55, 65,96);
                Ring(76,20,18,4); Ring(76,20,7,4); Path(4, 60,29, 76,58, 92,29); break;
            case Kind.Back:
                Path(12, 38,20, 13,49, 38,76); Path(12, 15,49, 62,49, 80,58, 87,78); break;
            case Kind.Settings:
                Ring(55,45,25,10);
                for (int i=0;i<8;i++) { float a=i*Mathf.PI/4; Line(new Vector2(55,45)+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*26, new Vector2(55,45)+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*37, 11); }
                Ring(15,84,10,4); break;
            case Kind.Status:
                for (int i=0;i<4;i++) Rect(i*8, 75-i*15, 5, 20+i*15);
                Path(5, 38,30, 57,30); Path(5, 41,47, 54,47); Disc(48,65,3);
                Path(4, 68,24, 94,24, 94,83, 68,83, 68,24); Rect(72,32,18,43); Rect(95,42,4,23); break;
        }
    }
    Vector2 Map(Vector2 p) { var r = rectTransform.rect; return new Vector2(r.xMin+p.x*r.width/100, r.yMax-p.y*r.height/100); }
    void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int n=mesh.currentVertCount;
        mesh.AddVert(Map(a),color,Vector2.zero); mesh.AddVert(Map(b),color,Vector2.zero); mesh.AddVert(Map(c),color,Vector2.zero); mesh.AddVert(Map(d),color,Vector2.zero);
        mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
    }
    void Rect(float x,float y,float w,float h) => Quad(new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h));
    void Line(Vector2 a,Vector2 b,float width) { var normal = new Vector2(-(b-a).y,(b-a).x).normalized*width/2; Quad(a-normal,b-normal,b+normal,a+normal); }
    void Path(float width,params float[] xy) { for(int i=2;i<xy.Length;i+=2) Line(new Vector2(xy[i-2],xy[i-1]),new Vector2(xy[i],xy[i+1]),width); }
    void Disc(float x,float y,float radius) => Ring(x,y,radius,radius);
    void Ring(float x,float y,float radius,float width)
    {
        Vector2 center=new Vector2(x,y);
        for(int i=0;i<48;i++) { float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48; var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a)); var q=new Vector2(Mathf.Cos(b),Mathf.Sin(b)); Quad(center+p*radius,center+q*radius,center+q*(radius-width),center+p*(radius-width)); }
    }
}
