// 代码由 AI（DeepSeek V4, Claude Code）生成，非人类手写。https://github.com/Yuxcv/nx-ai-tools
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using NXOpen;
using NXOpen.UF;

public class AutoServer
{
    public static int Startup()
    {
        try{Assembly.LoadFrom(@"C:\nx_custom\startup\Server.dll")
            .GetType("NXOpenRemotingService").GetMethod("Start").Invoke(null,null);}catch{}
        new Thread(()=>{
            for(int i=0;i<60;i++){
                foreach(Process p in Process.GetProcessesByName("ugraf")){
                    if(p.MainWindowHandle!=IntPtr.Zero){
                        new NxHook().AssignHandle(p.MainWindowHandle);return;
                    }
                }
                Thread.Sleep(1000);
            }
        }){IsBackground=true}.Start();
        return 0;
    }
    public static int GetUnloadOption(string arg)
        {return Convert.ToInt32(Session.LibraryUnloadOption.AtTermination);}
}

public class NxHook : NativeWindow
{
    const int WM_COPYDATA=0x004A;
    const string TPL=@"E:\UGII\templates\model-plain-1-mm-template.prt";
    const string TMP=@"C:\temp\nx\c";
    [StructLayout(LayoutKind.Sequential)]
    struct CDS{public IntPtr dwData;public int cbData;public IntPtr lpData;}

    static Session S;static UFSession U;static Part W;
    static Tag _last,_prev,_mtx;static bool _inited;

    // ══ 初始化 ══
    static void Init(){
        if(_inited)return;_inited=true;
        S=Session.GetSession();
        for(int i=0;i<20;i++){try{S.ApplicationSwitchImmediate("UG_APP_MODELING");break;}catch{System.Threading.Thread.Sleep(3000);}}
        U=UFSession.GetUFSession();W=S.Parts.Work;
        if(W==null)NewCanvas();
        Tag w;U.Csys.AskWcs(out w);U.Csys.AskMatrixOfObject(w,out _mtx);
    }
    static void NewCanvas(){
        var p=TMP+DateTime.Now.Ticks+".prt";
        File.Copy(TPL,p);PartLoadStatus st;S.Parts.OpenBaseDisplay(p,out st);
        W=S.Parts.Work;_last=Tag.Null;_prev=Tag.Null;
    }

    // ══ JSON ══
    static double J(string j,string k,double d){
        var p="\""+k+"\":";int i=j.IndexOf(p);if(i<0)return d;i+=p.Length;
        while(i<j.Length&&j[i]==' ')i++;int e=i;
        while(e<j.Length&&(char.IsDigit(j[e])||j[e]=='.'||j[e]=='-'))e++;
        if(e>i){double v;if(double.TryParse(j.Substring(i,e-i),out v))return v;}return d;
    }
    static string Ss(string j,string k){
        var p="\""+k+"\":\"";int i=j.IndexOf(p);if(i<0)return"";i+=p.Length;
        int e=j.IndexOf('"',i);return e<0?"":j.Substring(i,e-i);
    }
    static string Res(string r){
        var sc=r.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n").Replace("\r","\\r");
        return"{\"ok\":true,\"r\":\""+sc+"\"}";
    }

    // ══ WM_COPYDATA ══
    protected override void WndProc(ref Message m){
        if(m.Msg!=WM_COPYDATA){base.WndProc(ref m);return;}
        string r="?";
        try{
            var c=(CDS)Marshal.PtrToStructure(m.LParam,typeof(CDS));
            var buf=new byte[c.cbData];Marshal.Copy(c.lpData,buf,0,c.cbData);
            var j=System.Text.Encoding.UTF8.GetString(buf);
            var cmd=Ss(j,"cmd");Init();W=S.Parts.Work;

            switch(cmd){
            case"ping":r="pong";break;
            case"clear":NewCanvas();Tag w;U.Csys.AskWcs(out w);U.Csys.AskMatrixOfObject(w,out _mtx);r="ok";break;
            case"block":r=Blk(J(j,"x",0),J(j,"y",0),J(j,"z",0),J(j,"w",100),J(j,"h",50),J(j,"d",30));break;
            case"extrude":r=Ext(J(j,"x",0),J(j,"y",0),J(j,"z",0),J(j,"w",50),J(j,"h",50),J(j,"d",20),(int)J(j,"sign",0));break;
            case"pocket":r=Pkt(J(j,"x",0),J(j,"y",0),J(j,"z",0),J(j,"r",15),J(j,"d",20));break;
            case"sphere":r=Sph(J(j,"d",50),J(j,"x",0),J(j,"y",0),J(j,"z",50));break;
            case"cylinder":r=Cyl(J(j,"d",50),J(j,"h",100),J(j,"x",0),J(j,"y",0),J(j,"z",0));break;
            case"hole":r=Hol(J(j,"x",0),J(j,"y",0),J(j,"r",6),J(j,"d",15));break;
            case"blend":r=Blend(J(j,"r",5));break;
            case"chamfer":r=Chmf(J(j,"d",2));break;
            case"revolve":r=Revolve(J(j,"x",0),J(j,"y",0),J(j,"w",50),J(j,"h",30),J(j,"a",360));break;
            case"unite":r=UniteLast();break;
            case"save":U.Part.Save();File.Copy(W.FullPath,@"C:\Users\Administrator\Desktop\nx_output.prt",true);r="saved";break;
            }
        }catch(Exception ex){r="ERR:"+ex.Message;}
        try{File.WriteAllText(@"C:\temp\nx\rslt.json",Res(r));}catch{}
        base.WndProc(ref m);
    }

    // ══ 工具 ══
    static void Set(Tag t){_prev=_last;_last=t;}
    static Tag FT(NXOpen.Features.Feature f){Tag t;U.Modl.AskFeatBody(f.Tag,out t);return t;}

    // 草图矩形 → 返回曲线 tags
    static Tag[] SketchRect(double x,double y,double z,double w,double h){
        var sb=W.Sketches.CreateSketchInPlaceBuilder2(null);Sketch sk;
        try{sb.PlaneOption=NXOpen.Sketch.PlaneOption.Inferred;sk=(NXOpen.Sketch)sb.Commit();}finally{sb.Destroy();}
        sk.Activate(NXOpen.Sketch.ViewReorient.False);
        var ts=new Tag[4];double[][]ps={new[]{x,y,z},new[]{x+w,y,z},new[]{x+w,y+h,z},new[]{x,y+h,z}};
        for(int i=0;i<4;i++){var ln=new UFCurve.Line();ln.start_point=ps[i];ln.end_point=ps[(i+1)%4];Tag t;U.Curve.CreateLine(ref ln,out t);ts[i]=t;}
        sk.Deactivate(NXOpen.Sketch.ViewReorient.False,NXOpen.Sketch.UpdateLevel.SketchOnly);
        return ts;
    }
    // 草图圆 → 返回曲线 tag
    static Tag SketchCircle(double x,double y,double z,double r){
        var sb=W.Sketches.CreateSketchInPlaceBuilder2(null);Sketch sk;
        try{sb.PlaneOption=NXOpen.Sketch.PlaneOption.Inferred;sk=(NXOpen.Sketch)sb.Commit();}finally{sb.Destroy();}
        sk.Activate(NXOpen.Sketch.ViewReorient.False);
        var a=new UFCurve.Arc();a.arc_center=new double[]{x,y,z};a.radius=r;a.start_angle=0;a.end_angle=2*Math.PI;a.matrix_tag=_mtx;
        Tag t;U.Curve.CreateArc(ref a,out t);
        sk.Deactivate(NXOpen.Sketch.ViewReorient.False,NXOpen.Sketch.UpdateLevel.SketchOnly);
        return t;
    }
    // 拉伸草图曲线
    static Tag ExtrudeCurves(Tag[] curves,double dist,double px,double py,double pz,double dx,double dy,double dz,FeatureSigns sign){
        Tag[] feats;U.Modl.CreateExtruded(curves,"0",new[]{"0",dist.ToString()},new[]{px,py,pz},new[]{dx,dy,dz},sign,out feats);
        if(feats!=null&&feats.Length>0){Tag bt;U.Modl.AskFeatBody(feats[0],out bt);return bt;}
        return Tag.Null;
    }

    // ══ 命令 ══
    static string Blk(double x,double y,double z,double w,double h,double d){
        var b=W.Features.CreateBlockFeatureBuilder(null);
        try{b.SetOriginAndLengths(new Point3d(x,y,z),w.ToString(),h.ToString(),d.ToString());Set(FT(b.CommitFeature()));}finally{b.Destroy();}
        U.Disp.Refresh();return"ok";
    }
    static string Cyl(double diam,double h,double x,double y,double z){
        var b=W.Features.CreateCylinderBuilder(null);
        try{b.Diameter.RightHandSide=diam.ToString();b.Height.RightHandSide=h.ToString();b.Origin=new Point3d(x,y,z);b.Direction=new Vector3d(0,0,1);Set(FT(b.CommitFeature()));}finally{b.Destroy();}
        U.Disp.Refresh();return"ok";
    }
    static string Sph(double diam,double x,double y,double z){
        NXOpen.Features.Sphere ns=null;var b=W.Features.CreateSphereBuilder(ns);
        try{b.CenterPoint=W.Points.CreatePoint(new Point3d(x,y,z));b.Diameter.RightHandSide=diam.ToString();Set(FT(b.CommitFeature()));}finally{b.Destroy();}
        U.Disp.Refresh();return"ok";
    }
    static string Ext(double x,double y,double z,double w,double h,double d,int sign){
        U.Ui.DisplayMessage("extrude: "+w+"x"+h+"x"+d,1);
        var ts=SketchRect(x,y,z,w,h);
        var sg=sign==1?FeatureSigns.Positive:sign==2?FeatureSigns.Negative:FeatureSigns.Nullsign;
        Set(ExtrudeCurves(ts,d,x+w/2,y+h/2,z,0,0,1,sg));
        U.Disp.Refresh();return"ok";
    }
    static string Pkt(double x,double y,double z,double r,double d){
        var t=SketchCircle(x,y,z,r);
        ExtrudeCurves(new[]{t},d,x,y,z,0,0,1,FeatureSigns.Negative);
        U.Disp.Refresh();return"ok";
    }
    static string Hol(double x,double y,double r,double d){
        SketchCircle(x,y,5,r);
        var tool=MakeCylinder(r*2,d,x,y,5,0,0,-1);
        if(_last!=Tag.Null&&tool!=Tag.Null){Tag e;U.Modl.SubtractBodiesWithRetainedOptions(_last,tool,false,true,out e);}
        U.Disp.Refresh();return"ok";
    }
    static string Blend(double r){
        if(_last==Tag.Null)return"no body";
        Tag[] edges;U.Modl.AskBodyEdges(_last,out edges);
        if(edges.Length==0)return"no edges";
        Tag t;U.Modl.CreateBlend(r.ToString(),edges,edges.Length,0,0,r,out t);
        _prev=_last;_last=t;U.Disp.Refresh();return"ok";
    }
    static string Chmf(double d){
        if(_last==Tag.Null)return"no body";
        Tag[] edges;U.Modl.AskBodyEdges(_last,out edges);
        if(edges.Length==0)return"no edges";
        Tag t;U.Modl.CreateChamfer(1,d.ToString(),d.ToString(),"0",edges,out t);
        _prev=_last;_last=t;U.Disp.Refresh();return"ok";
    }
    static string Revolve(double x,double y,double w,double h,double ang){
        var ts=SketchRect(x,y,0,w,h);
        Tag[] feats;U.Modl.CreateRevolved(ts,new[]{"0",ang.ToString()},new[]{x,0.0,0.0},new[]{0.0,1.0,0.0},FeatureSigns.Nullsign,out feats);
        if(feats!=null&&feats.Length>0){Tag bt;U.Modl.AskFeatBody(feats[0],out bt);Set(bt);}
        U.Disp.Refresh();return"ok";
    }
    static string UniteLast(){
        if(_last==Tag.Null||_prev==Tag.Null)return"need 2 bodies";
        U.Modl.UniteBodies(_last,_prev);
        _prev=Tag.Null;U.Disp.Refresh();return"ok";
    }

    static Tag MakeCylinder(double diam,double h,double x,double y,double z,double dx,double dy,double dz){
        var bb=W.Features.CreateCylinderBuilder(null);
        try{bb.Diameter.RightHandSide=diam.ToString();bb.Height.RightHandSide=h.ToString();bb.Origin=new Point3d(x,y,z);bb.Direction=new Vector3d(dx,dy,dz);return FT(bb.CommitFeature());}finally{bb.Destroy();}
    }
}
