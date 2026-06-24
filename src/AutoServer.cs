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
    [StructLayout(LayoutKind.Sequential)]
    struct CDS{public IntPtr dwData;public int cbData;public IntPtr lpData;}

    static Session S;static UFSession U;static Part W;
    static Tag _last,_prev,_mtx;static bool _inited;

    static void Init(){
        if(_inited)return;_inited=true;
        S=Session.GetSession();
        for(int i=0;i<20;i++){try{S.ApplicationSwitchImmediate("UG_APP_MODELING");break;}catch{System.Threading.Thread.Sleep(3000);}}
        U=UFSession.GetUFSession();W=S.Parts.Work;
        if(W==null){var tpl=@"E:\UGII\templates\model-plain-1-mm-template.prt";
            var cv=@"C:\temp\nx\c"+DateTime.Now.Ticks+".prt";
            File.Copy(tpl,cv);PartLoadStatus st;S.Parts.OpenBaseDisplay(cv,out st);W=S.Parts.Work;}
        Tag w;U.Csys.AskWcs(out w);U.Csys.AskMatrixOfObject(w,out _mtx);
    }

    // JSON 轻量解析
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

    protected override void WndProc(ref Message m){
        if(m.Msg==WM_COPYDATA){string r="?";try{
            var c=(CDS)Marshal.PtrToStructure(m.LParam,typeof(CDS));
            var buf=new byte[c.cbData];Marshal.Copy(c.lpData,buf,0,c.cbData);
            var j=System.Text.Encoding.UTF8.GetString(buf);
            var cmd=Ss(j,"cmd");Init();W=S.Parts.Work;

            switch(cmd){
            case"ping":r="pong";break;
            case"clear":
                var tpl=@"E:\UGII\templates\model-plain-1-mm-template.prt";
                var cv=@"C:\temp\nx\c"+DateTime.Now.Ticks+".prt";
                File.Copy(tpl,cv);PartLoadStatus st;S.Parts.OpenBaseDisplay(cv,out st);
                W=S.Parts.Work;_last=Tag.Null;_prev=Tag.Null;
                Tag wt;U.Csys.AskWcs(out wt);U.Csys.AskMatrixOfObject(wt,out _mtx);r="ok";break;
            case"block":
                r=Blk(J(j,"x",0),J(j,"y",0),J(j,"z",0),
                      J(j,"w",100),J(j,"h",50),J(j,"d",30));break;
            case"extrude":
                r=Ext(J(j,"x",0),J(j,"y",0),J(j,"z",0),
                      J(j,"w",50),J(j,"h",50),J(j,"d",20),(int)J(j,"sign",0));break;
            case"pocket":
                r=Pkt(J(j,"x",0),J(j,"y",0),J(j,"z",0),
                      J(j,"r",15),J(j,"d",20));break;
            case"sphere":
                r=Sph(J(j,"d",50),J(j,"x",0),J(j,"y",0),J(j,"z",50));break;
            case"cylinder":
                r=Cyl(J(j,"d",50),J(j,"h",100),J(j,"x",0),J(j,"y",0),J(j,"z",0));break;
            case"hole":
                r=Hol(J(j,"x",0),J(j,"y",0),J(j,"r",6),J(j,"d",15));break;
            case"save":
                U.Part.Save();File.Copy(W.FullPath,
                    @"C:\Users\Administrator\Desktop\nx_output.prt",true);r="saved";break;
            }
        }catch(Exception ex){r="ERR:"+ex.Message;}
        try{var esc=r.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n").Replace("\r","\\r");
            File.WriteAllText(@"C:\temp\nx\rslt.json","{\"ok\":true,\"r\":\""+esc+"\"}");}catch{}}
        base.WndProc(ref m);
    }

    // ══ 建模 ══
    static void Set(Tag t){_prev=_last;_last=t;}
    static Tag FT(NXOpen.Features.Feature f){Tag t;U.Modl.AskFeatBody(f.Tag,out t);return t;}

    static string Blk(double x,double y,double z,double w,double h,double d){
        var b=W.Features.CreateBlockFeatureBuilder(null);
        try{b.SetOriginAndLengths(new Point3d(x,y,z),w.ToString(),h.ToString(),d.ToString());
            Set(FT(b.CommitFeature()));}finally{b.Destroy();}U.Disp.Refresh();return"ok";
    }
    static string Cyl(double diam,double h,double x,double y,double z){
        var b=W.Features.CreateCylinderBuilder(null);
        try{b.Diameter.RightHandSide=diam.ToString();b.Height.RightHandSide=h.ToString();
            b.Origin=new Point3d(x,y,z);b.Direction=new Vector3d(0,0,1);
            Set(FT(b.CommitFeature()));}finally{b.Destroy();}U.Disp.Refresh();return"ok";
    }
    static string Sph(double diam,double x,double y,double z){
        NXOpen.Features.Sphere ns=null;var b=W.Features.CreateSphereBuilder(ns);
        try{b.CenterPoint=W.Points.CreatePoint(new Point3d(x,y,z));
            b.Diameter.RightHandSide=diam.ToString();
            Set(FT(b.CommitFeature()));}finally{b.Destroy();}U.Disp.Refresh();return"ok";
    }
    static string Ext(double x,double y,double z,double w,double h,double d,int sign){
        U.Ui.DisplayMessage("extrude: "+w+"x"+h+"x"+d,1);
        var sb=W.Sketches.CreateSketchInPlaceBuilder2(null);Sketch sk;
        try{sb.PlaneOption=NXOpen.Sketch.PlaneOption.Inferred;
            sk=(NXOpen.Sketch)sb.Commit();}finally{sb.Destroy();}
        sk.Activate(NXOpen.Sketch.ViewReorient.False);
        var ts=new Tag[4];double[][]ps={new[]{x,y,z},new[]{x+w,y,z},new[]{x+w,y+h,z},new[]{x,y+h,z}};
        for(int i=0;i<4;i++){var ln=new UFCurve.Line();ln.start_point=ps[i];ln.end_point=ps[(i+1)%4];
            Tag t;U.Curve.CreateLine(ref ln,out t);ts[i]=t;}
        sk.Deactivate(NXOpen.Sketch.ViewReorient.False,NXOpen.Sketch.UpdateLevel.SketchOnly);
        var sg=sign==1?FeatureSigns.Positive:sign==2?FeatureSigns.Negative:FeatureSigns.Nullsign;
        Tag[] feats;U.Modl.CreateExtruded(ts,"0",new[]{"0",d.ToString()},
            new[]{x+w/2,y+h/2,z},new[]{0.0,0.0,1.0},sg,out feats);
        if(feats!=null&&feats.Length>0){Tag bt;U.Modl.AskFeatBody(feats[0],out bt);Set(bt);}
        U.Disp.Refresh();return"ok";
    }
    static string Pkt(double x,double y,double z,double r,double d){
        var sb=W.Sketches.CreateSketchInPlaceBuilder2(null);Sketch sk;
        try{sb.PlaneOption=NXOpen.Sketch.PlaneOption.Inferred;
            sk=(NXOpen.Sketch)sb.Commit();}finally{sb.Destroy();}
        sk.Activate(NXOpen.Sketch.ViewReorient.False);
        var a=new UFCurve.Arc();a.arc_center=new double[]{x,y,z};a.radius=r;
        a.start_angle=0;a.end_angle=2*Math.PI;a.matrix_tag=_mtx;
        Tag at;U.Curve.CreateArc(ref a,out at);
        sk.Deactivate(NXOpen.Sketch.ViewReorient.False,NXOpen.Sketch.UpdateLevel.SketchOnly);
        Tag[] feats;U.Modl.CreateExtruded(new[]{at},"0",new[]{"0",d.ToString()},
            new[]{x,y,z},new[]{0.0,0.0,1.0},FeatureSigns.Negative,out feats);
        U.Disp.Refresh();return"ok";
    }
    static string Hol(double x,double y,double r,double d){
        var sb=W.Sketches.CreateSketchInPlaceBuilder2(null);Sketch sk;
        try{sb.PlaneOption=NXOpen.Sketch.PlaneOption.Inferred;
            sk=(NXOpen.Sketch)sb.Commit();}finally{sb.Destroy();}
        sk.Activate(NXOpen.Sketch.ViewReorient.False);
        var a=new UFCurve.Arc();a.arc_center=new double[]{x,y,5};a.radius=r;
        a.start_angle=0;a.end_angle=2*Math.PI;a.matrix_tag=_mtx;
        Tag at;U.Curve.CreateArc(ref a,out at);
        sk.Deactivate(NXOpen.Sketch.ViewReorient.False,NXOpen.Sketch.UpdateLevel.SketchOnly);
        var bb=W.Features.CreateCylinderBuilder(null);
        Tag tool=Tag.Null;
        try{bb.Diameter.RightHandSide=(r*2).ToString();bb.Height.RightHandSide=d.ToString();
            bb.Origin=new Point3d(x,y,5);bb.Direction=new Vector3d(0,0,-1);
            tool=FT(bb.CommitFeature());}finally{bb.Destroy();}
        if(_last!=Tag.Null&&tool!=Tag.Null){Tag e;U.Modl.SubtractBodiesWithRetainedOptions(_last,tool,false,true,out e);}
        U.Disp.Refresh();return"ok";
    }
}
