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
    static string Ok(string r,object v){
        var s=r.Replace("\\","\\\\").Replace("\"","\\\"");
        return"{\"ok\":true,\"r\":\""+s+"\"}";
    }

    // ══ 生命周期 ══
    static void Init(){
        if(_inited)return;_inited=true;
        S=Session.GetSession();
        for(int i=0;i<20;i++){try{S.ApplicationSwitchImmediate("UG_APP_MODELING");break;}catch{System.Threading.Thread.Sleep(3000);}}
        U=UFSession.GetUFSession();EnsPart();
    }
    static void EnsPart(){
        if(S.Parts.Work==null){
            var p=TMP+DateTime.Now.Ticks+".prt";
            File.Copy(TPL,p);PartLoadStatus st;S.Parts.OpenBaseDisplay(p,out st);
            _last=Tag.Null;_prev=Tag.Null;
        }
        W=S.Parts.Work;
        Tag w;U.Csys.AskWcs(out w);U.Csys.AskMatrixOfObject(w,out _mtx);
    }

    // ══ WM_COPYDATA ══
    protected override void WndProc(ref Message m){
        if(m.Msg!=WM_COPYDATA){base.WndProc(ref m);return;}
        string r="?";
        try{
            var c=(CDS)Marshal.PtrToStructure(m.LParam,typeof(CDS));
            var buf=new byte[c.cbData];Marshal.Copy(c.lpData,buf,0,c.cbData);
            var j=System.Text.Encoding.UTF8.GetString(buf);
            var cmd=Ss(j,"cmd");Init();

            switch(cmd){
            case"ping":r="pong";break;
            case"clear":NewCanvas();EnsPart();r="ok";break;
            case"block":r=CmdBlk(J(j,"x",0),J(j,"y",0),J(j,"z",0),J(j,"w",100),J(j,"h",50),J(j,"d",30));break;
            case"extrude":r=CmdExt(J(j,"x",0),J(j,"y",0),J(j,"z",0),J(j,"w",50),J(j,"h",50),J(j,"d",20),(int)J(j,"sign",0));break;
            case"pocket":r=CmdPkt(J(j,"x",0),J(j,"y",0),J(j,"z",0),J(j,"r",15),J(j,"d",20));break;
            case"sphere":r=CmdSph(J(j,"d",50),J(j,"x",0),J(j,"y",0),J(j,"z",50));break;
            case"cylinder":r=CmdCyl(J(j,"d",50),J(j,"h",100),J(j,"x",0),J(j,"y",0),J(j,"z",0));break;
            case"hole":r=CmdHol(J(j,"x",0),J(j,"y",0),J(j,"r",6),J(j,"d",15));break;
            case"blend":r=CmdBlend(J(j,"r",5));break;
            case"chamfer":r=CmdChmf(J(j,"d",2));break;
            case"revolve":r=CmdRevolve(J(j,"x",0),J(j,"y",0),J(j,"w",50),J(j,"h",30),J(j,"a",360));break;
            case"unite":r=CmdUnite();break;
            case"sub":r=CmdSub();break;
            case"trim":r=CmdTrim(J(j,"nx",0),J(j,"ny",0),J(j,"nz",1),J(j,"ox",0),J(j,"oy",0),J(j,"oz",0));break;
            case"mirror":r=CmdMirror(J(j,"nx",0),J(j,"ny",1),J(j,"nz",0),J(j,"ox",0),J(j,"oy",0),J(j,"oz",0));break;
            case"array":r=CmdArray((int)J(j,"n",4),J(j,"dx",50),J(j,"dy",0),J(j,"dz",0));break;
            // 草图
            case"skline":r=SkLine(J(j,"x1",0),J(j,"y1",0),J(j,"z1",0),J(j,"x2",50),J(j,"y2",50),J(j,"z2",0));break;
            case"skarc":r=SkArc(J(j,"cx",0),J(j,"cy",0),J(j,"cz",0),J(j,"r",20),J(j,"a1",0),J(j,"a2",360));break;
            case"skrect":r=SkRect(J(j,"x",0),J(j,"y",0),J(j,"z",0),J(j,"w",50),J(j,"h",30));break;
            case"skcircle":r=SkCir(J(j,"cx",0),J(j,"cy",0),J(j,"cz",0),J(j,"r",20));break;
            case"skpoly":r=SkPoly(J(j,"cx",0),J(j,"cy",0),J(j,"cz",0),J(j,"r",25),(int)J(j,"n",6));break;
            case"save":U.Part.Save();File.Copy(W.FullPath,@"C:\Users\Administrator\Desktop\nx_output.prt",true);r="saved";break;
            }
        }catch(Exception ex){r="ERR:"+ex.Message;}
        try{File.WriteAllText(@"C:\temp\nx\rslt.json",Ok(r,null));}catch{}
        base.WndProc(ref m);
    }

    // ══ 工具 ══
    static void Track(Tag t){_prev=_last;_last=t;}
    static Tag FT(NXOpen.Features.Feature f){Tag t;U.Modl.AskFeatBody(f.Tag,out t);return t;}
    static Tag Plan(double ox,double oy,double oz,double nx,double ny,double nz){
        Tag p;U.Modl.CreatePlane(new double[]{ox,oy,oz},new double[]{nx,ny,nz},out p);return p;
    }
    static void Refresh(){U.Disp.Refresh();}

    static void SketchAct(out Sketch sk){
        var sb=W.Sketches.CreateSketchInPlaceBuilder2(null);
        try{sb.PlaneOption=NXOpen.Sketch.PlaneOption.Inferred;sk=(NXOpen.Sketch)sb.Commit();}finally{sb.Destroy();}
        sk.Activate(NXOpen.Sketch.ViewReorient.False);
    }
    static void SketchDeact(Sketch sk){
        sk.Deactivate(NXOpen.Sketch.ViewReorient.False,NXOpen.Sketch.UpdateLevel.SketchOnly);
    }

    static Tag[] SketchRect(double x,double y,double z,double w,double h){
        Sketch sk;SketchAct(out sk);
        var ts=new Tag[4];double[][]ps={new[]{x,y,z},new[]{x+w,y,z},new[]{x+w,y+h,z},new[]{x,y+h,z}};
        for(int i=0;i<4;i++){var ln=new UFCurve.Line();ln.start_point=ps[i];ln.end_point=ps[(i+1)%4];Tag t;U.Curve.CreateLine(ref ln,out t);ts[i]=t;}
        SketchDeact(sk);return ts;
    }
    static Tag SketchCircle(double x,double y,double z,double r){
        Sketch sk;SketchAct(out sk);
        var a=new UFCurve.Arc(){arc_center=new double[]{x,y,z},radius=r,start_angle=0,end_angle=2*Math.PI,matrix_tag=_mtx};
        Tag t;U.Curve.CreateArc(ref a,out t);
        SketchDeact(sk);return t;
    }
    static Tag ExtrudeCrv(Tag[] curves,double dist,double px,double py,double pz,double dx,double dy,double dz,FeatureSigns sign){
        Tag[] feats;U.Modl.CreateExtruded(curves,"0",new[]{"0",dist.ToString()},new[]{px,py,pz},new[]{dx,dy,dz},sign,out feats);
        if(feats!=null&&feats.Length>0){Tag bt;U.Modl.AskFeatBody(feats[0],out bt);return bt;}
        return Tag.Null;
    }

    // ══ 命令 ══
    static void NewCanvas(){
        var p=TMP+DateTime.Now.Ticks+".prt";
        File.Copy(TPL,p);PartLoadStatus st;S.Parts.OpenBaseDisplay(p,out st);
        W=S.Parts.Work;_last=Tag.Null;_prev=Tag.Null;
    }

    // 基础体
    static string CmdBlk(double x,double y,double z,double w,double h,double d){
        var b=W.Features.CreateBlockFeatureBuilder(null);
        try{b.SetOriginAndLengths(new Point3d(x,y,z),w.ToString(),h.ToString(),d.ToString());Track(FT(b.CommitFeature()));}finally{b.Destroy();}
        Refresh();return"ok";
    }
    static string CmdCyl(double diam,double h,double x,double y,double z){
        var b=W.Features.CreateCylinderBuilder(null);
        try{b.Diameter.RightHandSide=diam.ToString();b.Height.RightHandSide=h.ToString();b.Origin=new Point3d(x,y,z);b.Direction=new Vector3d(0,0,1);Track(FT(b.CommitFeature()));}finally{b.Destroy();}
        Refresh();return"ok";
    }
    static string CmdSph(double diam,double x,double y,double z){
        NXOpen.Features.Sphere ns=null;var b=W.Features.CreateSphereBuilder(ns);
        try{b.CenterPoint=W.Points.CreatePoint(new Point3d(x,y,z));b.Diameter.RightHandSide=diam.ToString();Track(FT(b.CommitFeature()));}finally{b.Destroy();}
        Refresh();return"ok";
    }

    // 草图拉伸
    static string CmdExt(double x,double y,double z,double w,double h,double d,int sign){
        U.Ui.DisplayMessage("extrude: "+w+"x"+h+"x"+d,1);
        var ts=SketchRect(x,y,z,w,h);
        var sg=sign==1?FeatureSigns.Positive:sign==2?FeatureSigns.Negative:FeatureSigns.Nullsign;
        Track(ExtrudeCrv(ts,d,x+w/2,y+h/2,z,0,0,1,sg));
        Refresh();return"ok";
    }
    static string CmdPkt(double x,double y,double z,double r,double d){
        var t=SketchCircle(x,y,z,r);
        ExtrudeCrv(new[]{t},d,x,y,z,0,0,1,FeatureSigns.Negative);
        Refresh();return"ok";
    }
    static string CmdHol(double x,double y,double r,double d){
        SketchCircle(x,y,5,r);
        var tool=CylBody(r*2,d,x,y,5,0,0,-1);
        if(_last!=Tag.Null&&tool!=Tag.Null){Tag e;U.Modl.SubtractBodiesWithRetainedOptions(_last,tool,false,true,out e);}
        Refresh();return"ok";
    }
    static string CmdRevolve(double x,double y,double w,double h,double ang){
        var ts=SketchRect(x,y,0,w,h);
        Tag[] feats;U.Modl.CreateRevolved(ts,new[]{"0",ang.ToString()},new[]{x,0.0,0.0},new[]{0.0,1.0,0.0},FeatureSigns.Nullsign,out feats);
        if(feats!=null&&feats.Length>0){Tag bt;U.Modl.AskFeatBody(feats[0],out bt);Track(bt);}
        Refresh();return"ok";
    }

    // 边操作
    static string CmdBlend(double r){
        if(_last==Tag.Null)return"no body";
        Tag[] es;U.Modl.AskBodyEdges(_last,out es);
        if(es.Length==0)return"no edges";
        Tag t;U.Modl.CreateBlend(r.ToString(),es,es.Length,0,0,r,out t);
        Track(t);Refresh();return"ok";
    }
    static string CmdChmf(double d){
        if(_last==Tag.Null)return"no body";
        Tag[] es;U.Modl.AskBodyEdges(_last,out es);
        if(es.Length==0)return"no edges";
        Tag t;U.Modl.CreateChamfer(1,d.ToString(),d.ToString(),"0",es,out t);
        Track(t);Refresh();return"ok";
    }

    // 布尔
    static string CmdUnite(){
        if(_last==Tag.Null||_prev==Tag.Null)return"need 2 bodies";
        U.Modl.UniteBodies(_last,_prev);_prev=Tag.Null;Refresh();return"ok";
    }
    static string CmdSub(){
        if(_last==Tag.Null||_prev==Tag.Null)return"need 2 bodies";
        Tag e;U.Modl.SubtractBodiesWithRetainedOptions(_prev,_last,false,true,out e);
        _last=_prev;_prev=Tag.Null;Refresh();return"ok";
    }

    // 面操作
    static string CmdTrim(double nx,double ny,double nz,double ox,double oy,double oz){
        if(_last==Tag.Null)return"no body";
        Tag t;U.Modl.TrimBody(_last,Plan(ox,oy,oz,nx,ny,nz),0,out t);
        Track(t);Refresh();return"ok";
    }
    static string CmdMirror(double nx,double ny,double nz,double ox,double oy,double oz){
        if(_last==Tag.Null)return"no body";
        Tag t;U.Modl.CreateMirrorBody(_last,Plan(ox,oy,oz,nx,ny,nz),out t);
        Track(t);Refresh();return"ok";
    }

    // 阵列
    static string CmdArray(int n,double dx,double dy,double dz){
        if(_last==Tag.Null)return"no body";
        if(n<2)return"n>=2";
        for(int i=1;i<n;i++){
            var b=W.Features.CreateBlockFeatureBuilder(null);
            try{b.SetOriginAndLengths(new Point3d(dx*i,dy*i,dz*i),"50","50","50");FT(b.CommitFeature());}finally{b.Destroy();}
        }
        Refresh();return"ok";
    }

    // ══ 草图专栏 ══
    static string SkLine(double x1,double y1,double z1,double x2,double y2,double z2){
        Sketch sk;SketchAct(out sk);
        var ln=new UFCurve.Line(){start_point=new double[]{x1,y1,z1},end_point=new double[]{x2,y2,z2}};
        Tag t;U.Curve.CreateLine(ref ln,out t);
        SketchDeact(sk);Refresh();return"ok";
    }
    static string SkArc(double cx,double cy,double cz,double r,double a1,double a2){
        Sketch sk;SketchAct(out sk);
        var a=new UFCurve.Arc(){arc_center=new double[]{cx,cy,cz},radius=r,
            start_angle=a1*Math.PI/180,end_angle=a2*Math.PI/180,matrix_tag=_mtx};
        Tag t;U.Curve.CreateArc(ref a,out t);
        SketchDeact(sk);Refresh();return"ok";
    }
    static string SkRect(double x,double y,double z,double w,double h){
        SketchRect(x,y,z,w,h);Refresh();return"ok";
    }
    static string SkCir(double cx,double cy,double cz,double r){
        SketchCircle(cx,cy,cz,r);Refresh();return"ok";
    }
    static string SkPoly(double cx,double cy,double cz,double r,int n){
        if(n<3)return"n>=3";
        Sketch sk;SketchAct(out sk);
        for(int i=0;i<n;i++){
            double a1=2*Math.PI*i/n,a2=2*Math.PI*(i+1)/n;
            double x1=cx+r*Math.Cos(a1),y1=cy+r*Math.Sin(a1);
            double x2=cx+r*Math.Cos(a2),y2=cy+r*Math.Sin(a2);
            var ln=new UFCurve.Line(){start_point=new double[]{x1,y1,cz},end_point=new double[]{x2,y2,cz}};
            Tag t;U.Curve.CreateLine(ref ln,out t);
        }
        SketchDeact(sk);Refresh();return"ok";
    }

    static Tag CylBody(double diam,double h,double x,double y,double z,double dx,double dy,double dz){
        var bb=W.Features.CreateCylinderBuilder(null);
        try{bb.Diameter.RightHandSide=diam.ToString();bb.Height.RightHandSide=h.ToString();bb.Origin=new Point3d(x,y,z);bb.Direction=new Vector3d(dx,dy,dz);return FT(bb.CommitFeature());}finally{bb.Destroy();}
    }
}
