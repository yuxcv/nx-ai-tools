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
    static string Ok(string r){var s=r.Replace("\\","\\\\").Replace("\"","\\\"");return"{\"ok\":true,\"r\":\""+s+"\"}";}

    // ══ 生命周期 ══
    static void Init(){
        if(_inited)return;_inited=true;
        S=Session.GetSession();
        for(int i=0;i<20;i++){try{S.ApplicationSwitchImmediate("UG_APP_MODELING");break;}catch{System.Threading.Thread.Sleep(3000);}}
        U=UFSession.GetUFSession();EnsPart();
    }
    static void EnsPart(){
        if(S.Parts.Work==null){
            var p=TMP+DateTime.Now.Ticks+".prt";File.Copy(TPL,p);
            PartLoadStatus st;S.Parts.OpenBaseDisplay(p,out st);_last=Tag.Null;_prev=Tag.Null;
        }
        W=S.Parts.Work;Tag w;U.Csys.AskWcs(out w);U.Csys.AskMatrixOfObject(w,out _mtx);
    }

    // ══ WM_COPYDATA ══
    protected override void WndProc(ref Message m){
        if(m.Msg!=WM_COPYDATA){base.WndProc(ref m);return;}
        string r="?",cmd="?";
        try{
            var c=(CDS)Marshal.PtrToStructure(m.LParam,typeof(CDS));
            var buf=new byte[c.cbData];Marshal.Copy(c.lpData,buf,0,c.cbData);
            var j=System.Text.Encoding.UTF8.GetString(buf);
            cmd=Ss(j,"cmd");Init();

            switch(cmd){
            case"ping":r="pong";break;
            case"clear":SkDeact();NewCanvas();EnsPart();r="ok";break;
            case"block":r=CmdBlk(J(j,"x",0),J(j,"y",0),J(j,"z",0),J(j,"w",100),J(j,"h",50),J(j,"d",30));break;
            case"extrude":r=CmdExt(J(j,"x",0),J(j,"y",0),J(j,"z",0),J(j,"w",50),J(j,"h",50),J(j,"d",20),(int)J(j,"sign",0));break;
            case"exsketch":r=ExSketch(J(j,"d",20),(int)J(j,"sign",0));break;
            case"revsketch":r=RevSketch(J(j,"a",360));break;
            case"pocksketch":r=PokSketch(J(j,"d",20));break;
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
            case"save":U.Part.Save();File.Copy(W.FullPath,@"C:\Users\Administrator\Desktop\nx_output.prt",true);r="saved";break;
            // 草图
            case"skline":r=SkLine(J(j,"x1",0),J(j,"y1",0),J(j,"z1",0),J(j,"x2",50),J(j,"y2",50),J(j,"z2",0));break;
            case"skarc":r=SkArc(J(j,"cx",0),J(j,"cy",0),J(j,"cz",0),J(j,"r",20),J(j,"a1",0),J(j,"a2",360));break;
            case"skrect":r=SkRect(J(j,"x",0),J(j,"y",0),J(j,"z",0),J(j,"w",50),J(j,"h",30));break;
            case"skcircle":r=SkCir(J(j,"cx",0),J(j,"cy",0),J(j,"cz",0),J(j,"r",20));break;
            case"skpoly":r=SkPoly(J(j,"cx",0),J(j,"cy",0),J(j,"cz",0),J(j,"r",25),(int)J(j,"n",6));break;
            case"skpoint":r=SkPoint(J(j,"x",0),J(j,"y",0),J(j,"z",0));break;
            case"skellipse":r=SkEllipse(J(j,"cx",0),J(j,"cy",0),J(j,"cz",0),J(j,"rx",30),J(j,"ry",15));break;
            case"skcorner":r=SkCorner();break;
            case"skchamf":r=SkChamf();break;
            case"sktrim":r=SkTrim();break;
            case"skautocon":r=SkAutoCon();break;
            case"sksym":r=SkSym();break;
            case"skdim":r=SkDim();break;
            case"skoffs":r=SkOffs();break;
            case"skproj":r=SkProj();break;
            case"skisect":r=SkIsect();break;
            case"skspline":r=SkUse(()=>W.Features.CreateSketchSplineBuilder(null));break;
            case"skconic":r=SkUse(()=>W.Sketches.CreateSketchConicBuilder(null));break;
            case"skhoriz":r=SkCon("Ho");break;
            case"skvert":r=SkCon("Ve");break;
            case"skfix":r=SkCon("Fi");break;
            case"skpara":r=SkCon("Pa");break;
            case"skperp":r=SkCon("Pe");break;
            case"skequal":r=SkCon("Eq");break;
            case"skdone":SkDeact();r="sketch done";break;
            case"undo":r=CmdUndo();break;
            }
        }catch(Exception ex){r="ERR:["+cmd+"] "+ex.Message;}
        try{File.WriteAllText(@"C:\temp\nx\rslt.json",Ok(r));}catch{}
        // 命令日志
        try{File.AppendAllText(@"C:\temp\nx\cmd_log.txt","["+DateTime.Now.ToString("HH:mm:ss")+"] "+cmd+" → "+(r.Length>100?r.Substring(0,100):r)+"\n");}catch{}
        base.WndProc(ref m);
    }

    // ══ 工具 ══
    static void Track(Tag t){_prev=_last;_last=t;}
    static Tag FT(NXOpen.Features.Feature f){Tag t;U.Modl.AskFeatBody(f.Tag,out t);return t;}
    static Tag Plan(double ox,double oy,double oz,double nx,double ny,double nz){
        Tag p;U.Modl.CreatePlane(new double[]{ox,oy,oz},new double[]{nx,ny,nz},out p);return p;
    }
    static void Refresh(){U.Disp.Refresh();}
    static Session.UndoMarkId _umark;static bool _umarked;
    static void Mark(){try{_umark=S.SetUndoMark(Session.MarkVisibility.Visible,"auto");_umarked=true;}catch{}}
    static string CmdUndo(){
        if(!_umarked)return"nothing to undo";
        try{S.UndoToMark(_umark,"undo");_umarked=false;Refresh();return"ok";}catch(Exception ex){return"undo ERR:"+ex.Message;}
    }
    static void SketchAct(out Sketch sk){
        var sb=W.Sketches.CreateSketchInPlaceBuilder2(null);
        try{sb.PlaneOption=NXOpen.Sketch.PlaneOption.Inferred;sk=(NXOpen.Sketch)sb.Commit();}finally{sb.Destroy();}
        sk.Activate(NXOpen.Sketch.ViewReorient.False);
    }
    static void SketchDeact(Sketch sk){sk.Deactivate(NXOpen.Sketch.ViewReorient.False,NXOpen.Sketch.UpdateLevel.SketchOnly);}
    static Tag[] SketchRect(double x,double y,double z,double w,double h){
        Sketch sk;SketchAct(out sk);
        var ts=new Tag[4];double[][]ps={new[]{x,y,z},new[]{x+w,y,z},new[]{x+w,y+h,z},new[]{x,y+h,z}};
        for(int i=0;i<4;i++){var ln=new UFCurve.Line();ln.start_point=ps[i];ln.end_point=ps[(i+1)%4];Tag t;U.Curve.CreateLine(ref ln,out t);ts[i]=t;}
        SketchDeact(sk);return ts;
    }
    static Tag SketchCircle(double x,double y,double z,double r){
        Sketch sk;SketchAct(out sk);
        var a=new UFCurve.Arc(){arc_center=new double[]{x,y,z},radius=r,start_angle=0,end_angle=2*Math.PI,matrix_tag=_mtx};
        Tag t;U.Curve.CreateArc(ref a,out t);SketchDeact(sk);return t;
    }
    static Tag ExtrudeCrv(Tag[] curves,double dist,double px,double py,double pz,double dx,double dy,double dz,FeatureSigns sign){
        Tag[] feats;U.Modl.CreateExtruded(curves,"0",new[]{"0",dist.ToString()},new[]{px,py,pz},new[]{dx,dy,dz},sign,out feats);
        if(feats!=null&&feats.Length>0){Tag bt;U.Modl.AskFeatBody(feats[0],out bt);return bt;}return Tag.Null;
    }

    // ══ 建模命令 ══
    static void NewCanvas(){
        var p=TMP+DateTime.Now.Ticks+".prt";File.Copy(TPL,p);
        PartLoadStatus st;S.Parts.OpenBaseDisplay(p,out st);W=S.Parts.Work;_last=Tag.Null;_prev=Tag.Null;_skCurves=null;
    }
    static string CmdBlk(double x,double y,double z,double w,double h,double d){
        Mark();var b=W.Features.CreateBlockFeatureBuilder(null);
        try{b.SetOriginAndLengths(new Point3d(x,y,z),w.ToString(),h.ToString(),d.ToString());Track(FT(b.CommitFeature()));}finally{b.Destroy();}
        Refresh();return"ok";
    }
    static string CmdCyl(double diam,double h,double x,double y,double z){
        Mark();var b=W.Features.CreateCylinderBuilder(null);
        try{b.Diameter.RightHandSide=diam.ToString();b.Height.RightHandSide=h.ToString();b.Origin=new Point3d(x,y,z);b.Direction=new Vector3d(0,0,1);Track(FT(b.CommitFeature()));}finally{b.Destroy();}
        Refresh();return"ok";
    }
    static string CmdSph(double diam,double x,double y,double z){
        NXOpen.Features.Sphere ns=null;var b=W.Features.CreateSphereBuilder(ns);
        try{b.CenterPoint=W.Points.CreatePoint(new Point3d(x,y,z));b.Diameter.RightHandSide=diam.ToString();Track(FT(b.CommitFeature()));}finally{b.Destroy();}
        Refresh();return"ok";
    }
    static string ExSketch(double d,int sign){
        Mark();if(_skCurves==null||_skCurves.Length==0)return"no sketch curves";
        var sg=sign==1?FeatureSigns.Positive:sign==2?FeatureSigns.Negative:FeatureSigns.Nullsign;
        Track(ExtrudeCrv(_skCurves,d,0,0,0,0,0,1,sg));Refresh();return"ok";
    }
    static string RevSketch(double ang){
        if(_skCurves==null||_skCurves.Length==0)return"no sketch curves";
        Tag[] feats;U.Modl.CreateRevolved(_skCurves,new[]{"0",ang.ToString()},new[]{0.0,0.0,0.0},new[]{0.0,1.0,0.0},FeatureSigns.Nullsign,out feats);
        if(feats!=null&&feats.Length>0){Tag bt;U.Modl.AskFeatBody(feats[0],out bt);Track(bt);}Refresh();return"ok";
    }
    static string PokSketch(double d){
        if(_skCurves==null||_skCurves.Length==0)return"no sketch curves";
        ExtrudeCrv(_skCurves,d,0,0,0,0,0,1,FeatureSigns.Negative);Refresh();return"ok";
    }

    static string CmdExt(double x,double y,double z,double w,double h,double d,int sign){
        Mark();U.Ui.DisplayMessage("extrude: "+w+"x"+h+"x"+d,1);
        var ts=SketchRect(x,y,z,w,h);
        var sg=sign==1?FeatureSigns.Positive:sign==2?FeatureSigns.Negative:FeatureSigns.Nullsign;
        Track(ExtrudeCrv(ts,d,x+w/2,y+h/2,z,0,0,1,sg));Refresh();return"ok";
    }
    static string CmdPkt(double x,double y,double z,double r,double d){
        ExtrudeCrv(new[]{SketchCircle(x,y,z,r)},d,x,y,z,0,0,1,FeatureSigns.Negative);Refresh();return"ok";
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
        if(feats!=null&&feats.Length>0){Tag bt;U.Modl.AskFeatBody(feats[0],out bt);Track(bt);}Refresh();return"ok";
    }
    static string CmdBlend(double r){
        if(_last==Tag.Null)return"no body";
        Tag[] es;U.Modl.AskBodyEdges(_last,out es);if(es.Length==0)return"no edges";
        Tag t;U.Modl.CreateBlend(r.ToString(),es,es.Length,0,0,r,out t);Track(t);Refresh();return"ok";
    }
    static string CmdChmf(double d){
        if(_last==Tag.Null)return"no body";
        Tag[] es;U.Modl.AskBodyEdges(_last,out es);if(es.Length==0)return"no edges";
        Tag t;U.Modl.CreateChamfer(1,d.ToString(),d.ToString(),"0",es,out t);Track(t);Refresh();return"ok";
    }
    static string CmdUnite(){
        Mark();if(_last==Tag.Null||_prev==Tag.Null)return"need 2 bodies";
        U.Modl.UniteBodies(_last,_prev);_prev=Tag.Null;Refresh();return"ok";
    }
    static string CmdSub(){
        Mark();if(_last==Tag.Null||_prev==Tag.Null)return"need 2 bodies";
        Tag e;U.Modl.SubtractBodiesWithRetainedOptions(_prev,_last,false,true,out e);
        _last=_prev;_prev=Tag.Null;Refresh();return"ok";
    }
    static string CmdTrim(double nx,double ny,double nz,double ox,double oy,double oz){
        if(_last==Tag.Null)return"no body";
        Tag t;U.Modl.TrimBody(_last,Plan(ox,oy,oz,nx,ny,nz),0,out t);Track(t);Refresh();return"ok";
    }
    static string CmdMirror(double nx,double ny,double nz,double ox,double oy,double oz){
        if(_last==Tag.Null)return"no body";
        Tag t;U.Modl.CreateMirrorBody(_last,Plan(ox,oy,oz,nx,ny,nz),out t);Track(t);Refresh();return"ok";
    }
    static string CmdArray(int n,double dx,double dy,double dz){
        if(_last==Tag.Null)return"no body";if(n<2)return"n>=2";
        for(int i=1;i<n;i++){
            var b=W.Features.CreateBlockFeatureBuilder(null);
            try{b.SetOriginAndLengths(new Point3d(dx*i,dy*i,dz*i),"50","50","50");FT(b.CommitFeature());}finally{b.Destroy();}
        }Refresh();return"ok";
    }

    // ══ 草图（开放模式，不自动关闭） ══
    static Sketch _skOpen;static DisplayableObject _skGeom,_skPrevG;
    static Tag[] _skCurves; // 关草图后保存的曲线 tags
    static void SkEnsure(){
        if(_skOpen!=null)return;
        var sb=W.Sketches.CreateSketchInPlaceBuilder2(null);
        try{sb.PlaneOption=NXOpen.Sketch.PlaneOption.Inferred;_skOpen=(NXOpen.Sketch)sb.Commit();}finally{sb.Destroy();}
        _skOpen.Activate(NXOpen.Sketch.ViewReorient.False);
    }
    static void SkDeact(){
        if(_skOpen==null)return;
        // 存下草图中所有几何的 tag
        var geoms=_skOpen.GetAllGeometry();
        if(geoms!=null&&geoms.Length>0){_skCurves=new Tag[geoms.Length];for(int i=0;i<geoms.Length;i++)_skCurves[i]=geoms[i].Tag;}
        _skOpen.Deactivate(NXOpen.Sketch.ViewReorient.False,NXOpen.Sketch.UpdateLevel.SketchOnly);
        _skOpen=null;_skGeom=null;_skPrevG=null;Refresh();
    }
    static void SkAdd(DisplayableObject g){_skPrevG=_skGeom;_skGeom=g;_skOpen.AddGeometry(g,Sketch.InferConstraintsOption.InferNoConstraints);}
    // 任意 Builder 模式：SkEnsure → Create → Commit/Destroy → Refresh
    static string SkUse<T>(Func<T> f)where T:class{SkEnsure();var b=f();try{((dynamic)b).Commit();}finally{((dynamic)b).Destroy();}Refresh();return"ok";}

    // 草图绘制
    static string SkLine(double x1,double y1,double z1,double x2,double y2,double z2){
        SkEnsure();SkAdd(W.Curves.CreateLine(new Point3d(x1,y1,z1),new Point3d(x2,y2,z2)));Refresh();return"ok";
    }
    static string SkArc(double cx,double cy,double cz,double r,double a1,double a2){
        return SkArcCir(cx,cy,cz,r,a1*Math.PI/180,a2*Math.PI/180);
    }
    static string SkCir(double cx,double cy,double cz,double r){
        return SkArcCir(cx,cy,cz,r,0,2*Math.PI);
    }
    static string SkArcCir(double cx,double cy,double cz,double r,double a1,double a2){
        SkEnsure();var a=new UFCurve.Arc(){arc_center=new double[]{cx,cy,cz},radius=r,start_angle=a1,end_angle=a2,matrix_tag=_mtx};
        Tag t;U.Curve.CreateArc(ref a,out t);SkAdd((DisplayableObject)NXOpen.Utilities.NXObjectManager.Get(t));Refresh();return"ok";
    }
    static string SkRect(double x,double y,double z,double w,double h){
        SkEnsure();double[][]ps={new[]{x,y,z},new[]{x+w,y,z},new[]{x+w,y+h,z},new[]{x,y+h,z}};
        for(int i=0;i<4;i++)SkAdd(W.Curves.CreateLine(new Point3d(ps[i][0],ps[i][1],ps[i][2]),new Point3d(ps[(i+1)%4][0],ps[(i+1)%4][1],ps[(i+1)%4][2])));
        Refresh();return"ok";
    }
    static string SkPoly(double cx,double cy,double cz,double r,int n){
        if(n<3)return"n>=3";SkEnsure();
        for(int i=0;i<n;i++){double a1=2*Math.PI*i/n,a2=2*Math.PI*(i+1)/n;
            SkAdd(W.Curves.CreateLine(new Point3d(cx+r*Math.Cos(a1),cy+r*Math.Sin(a1),cz),new Point3d(cx+r*Math.Cos(a2),cy+r*Math.Sin(a2),cz)));
        }Refresh();return"ok";
    }
    static string SkPoint(double x,double y,double z){
        SkEnsure();SkAdd(W.Points.CreatePoint(new Point3d(x,y,z)));Refresh();return"ok";
    }
    static string SkEllipse(double cx,double cy,double cz,double rx,double ry){
        SkEnsure();var eb=W.Sketches.CreateSketchEllipseBuilder(null);
        try{eb.CenterPoint=W.Points.CreatePoint(new Point3d(cx,cy,cz));
            eb.MajorRadius.RightHandSide=rx.ToString();eb.MinorRadius.RightHandSide=ry.ToString();
            SkAdd((DisplayableObject)eb.Commit());}finally{eb.Destroy();}
        Refresh();return"ok";
    }
    static string SkCorner(){return SkUse(()=>W.Sketches.CreateCornerBuilder());}
    static string SkChamf(){return SkUse(()=>W.Sketches.CreateSketchChamferBuilder());}
    static string SkTrim(){return SkUse(()=>W.Sketches.CreateQuickTrimBuilder());}
    static string SkAutoCon(){return SkUse(()=>W.Sketches.CreateAutoConstrainBuilder());}
    static string SkSym(){return SkUse(()=>W.Sketches.CreateMakeSymmetricBuilder());}
    static string SkDim(){return SkUse(()=>W.Sketches.CreateRapidDimensionBuilder());}
    static string SkOffs(){return SkUse(()=>W.Sketches.CreateSketchOffsetBuilder(null));}
    static string SkProj(){return SkUse(()=>W.Sketches.CreateProjectBuilder(null));}
    static string SkIsect(){return SkUse(()=>W.Sketches.CreateIntersectionCurveBuilder(null));}

    // 草图约束（Builder 模式）
    static bool SkConNeed2(string t){return t=="Pa"||t=="Pe"||t=="Eq";}
    static string SkCon(string t){
        if(_skOpen==null||_skGeom==null)return"no geom";
        if(SkConNeed2(t)&&_skPrevG==null)return"need 2nd geom";
        var cb=W.Sketches.CreateConstraintBuilder();
        try{
            switch(t){case"Ho":cb.ConstraintType=SketchConstraintBuilder.Constraint.Horizontal;break;case"Ve":cb.ConstraintType=SketchConstraintBuilder.Constraint.Vertical;break;case"Fi":cb.ConstraintType=SketchConstraintBuilder.Constraint.Fixed;break;case"Pa":cb.ConstraintType=SketchConstraintBuilder.Constraint.Parallel;break;case"Pe":cb.ConstraintType=SketchConstraintBuilder.Constraint.Perpendicular;break;case"Eq":cb.ConstraintType=SketchConstraintBuilder.Constraint.EqualLength;break;default:return"bad type";}
            cb.GeometryToConstrain.Add(_skGeom);
            if(SkConNeed2(t))cb.GeometryToConstrain.Add(_skPrevG);
            cb.Commit();Refresh();return"ok";
        }finally{cb.Destroy();}
    }

    static Tag CylBody(double diam,double h,double x,double y,double z,double dx,double dy,double dz){
        var bb=W.Features.CreateCylinderBuilder(null);
        try{bb.Diameter.RightHandSide=diam.ToString();bb.Height.RightHandSide=h.ToString();bb.Origin=new Point3d(x,y,z);bb.Direction=new Vector3d(dx,dy,dz);return FT(bb.CommitFeature());}finally{bb.Destroy();}
    }
}
